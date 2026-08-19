using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Domain;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// What a gate said that did not stop the work, carried onto the response by the pipeline rather than by the
/// endpoint that happened to ask.
///
/// <para><b>Why this is not the endpoint's job.</b> <see cref="Refusals.StopAsync"/> writes the refusals and
/// answers false, which is only half the sentence: an endpoint that ran a gate is left holding the complaints
/// too. Leaving that to each endpoint makes carrying them a thing to remember, and a dropped complaint fails
/// nothing — no test breaks, no status changes, the board simply reads as cleaner than it is. So the
/// complaints are handed over here and the response carries them whether or not the endpoint thought about
/// them; running the gate is the whole of an endpoint's duty.</para>
///
/// <para><b>One key, one rule for when it appears.</b> A 2xx JSON object answers <c>warnings</c> when anything
/// was complained about and carries no such key when nothing was. That single rule is what makes an absent
/// <c>warnings</c> readable: without it the key's absence covers an endpoint with no gate, one whose gate
/// found nothing, one that dropped what its gate found and one answering a shape with nowhere to put it — four
/// states a caller cannot tell apart. A refusal carries refusals only, in
/// <see cref="Contracts.RefusalDto"/>'s <c>findings</c>: the work did not happen, so nothing rides along with
/// it.</para>
///
/// <para><b>A report whose subject is findings is not this.</b> <c>/plan/evaluate</c>'s <c>lint</c> and
/// <c>/plan/feasibility</c>'s per-box findings are the answer being asked for rather than a remark alongside
/// one, and they stay named fields of their own documents.</para>
/// </summary>
internal static class Complaints
{
    private const string Slot = "pgm-studio.complaints";

    /// <summary>The key a success answers them under.</summary>
    private const string Key = "warnings";

    /// <summary>The options the body was serialized with, so an injected key reads like the ones beside it.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Hand complaints to the response being written. Refusals among them are ignored rather than
    /// downgraded: a refusal that reaches here has already been written or was never the caller's to pass on.</summary>
    public static void Add(HttpContext http, IEnumerable<Finding> findings)
    {
        var carried = Carried(http);
        foreach (var finding in findings)
            if (!finding.Refuses) carried.Add(finding);
    }

    /// <summary>The fields a reader had nowhere to keep, as <c>RQ3</c> complaints. The reading is the same
    /// wherever a document is deserialized, so it is stated once here rather than at each reader's endpoint.</summary>
    public static void Unread(HttpContext http, IReadOnlyList<string> fields) =>
        Add(http, fields.Select(field => new Finding(
            RequestRules.Unread,
            $"field '{field}' was not read — no part of this document has that name, and no upgrade "
            + "claimed it, so whatever it was meant to say was not said",
            Severity.Complaint, Field: field)));

    private static List<Finding> Carried(HttpContext http) =>
        http.Items[Slot] as List<Finding> ?? (List<Finding>)(http.Items[Slot] = new List<Finding>())!;

    /// <summary>The middleware half: buffer a JSON success that has complaints waiting, put them in it, and let
    /// everything else through untouched. Nothing is buffered unless there is something to add, so the cost
    /// falls only on the responses that carry one — a column payload is megabytes and is written straight
    /// through when the board drew cleanly.</summary>
    public static async Task CarryAsync(HttpContext http, RequestDelegate next)
    {
        if (!http.Request.Path.StartsWithSegments("/api")) { await next(http); return; }

        var real = http.Response.Body;
        var carrier = new Carrier(real, http);
        http.Response.Body = carrier;
        try { await next(http); }
        finally { http.Response.Body = real; }

        await carrier.FinishAsync(http.RequestAborted);
    }

    /// <summary>The response body while it is still ours to add to. The decision is taken at the first write,
    /// by which point the status and content type are set: a JSON 2xx with complaints waiting is held, and
    /// anything else — a refusal, a rendered SVG, an ASCII board, a parquet stream — goes straight through.</summary>
    private sealed class Carrier(Stream inner, HttpContext http) : Stream
    {
        private MemoryStream? held;
        private bool decided;

        private Stream Target()
        {
            if (decided) return held ?? inner;
            decided = true;
            var json = http.Response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false;
            if (json && http.Response.StatusCode is >= 200 and < 300 && Carried(http).Count > 0)
                held = new MemoryStream();
            return held ?? inner;
        }

        public async Task FinishAsync(CancellationToken ct)
        {
            var carried = Carried(http);
            if (held is null)
            {
                // Complaints computed and nowhere to put them. That is the failure this channel exists to
                // remove, so it is loud in the log rather than silent on the wire — but only where the work
                // succeeded: a request that went on to be refused answers the refusal instead of an answer,
                // and there is no success for anything to ride on.
                if (carried.Count > 0 && http.Response.StatusCode is >= 200 and < 300)
                    Complain(carried, "the success body is not JSON");
                return;
            }

            held.Position = 0;
            JsonNode? body;
            try { body = JsonNode.Parse(held); }
            catch (JsonException) { body = null; }

            byte[] bytes;
            if (body is JsonObject document)
            {
                // One writer, or the last one wins silently. Nothing in the studio should be filling this key
                // itself any more — an endpoint that does has grown a second channel for the same thing.
                if (document.ContainsKey(Key)) Complain(carried, $"the body already carries '{Key}'");
                document[Key] = JsonSerializer.SerializeToNode(Refusals.Dtos(carried), Json);
                bytes = JsonSerializer.SerializeToUtf8Bytes(document, Json);
            }
            else
            {
                Complain(carried, "the body is not a JSON object");
                bytes = held.ToArray();
            }

            http.Response.ContentLength = bytes.Length;
            await inner.WriteAsync(bytes, ct);
        }

        private void Complain(IReadOnlyList<Finding> carried, string why) =>
            http.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("PgmStudio.Api.Complaints")
                .LogError("{Count} complaint(s) could not ride on {Method} {Path} — {Why}: {Rules}",
                    carried.Count, http.Request.Method, http.Request.Path, why,
                    string.Join(", ", carried.Select(finding => finding.Rule)));

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => Target().Flush();
        public override Task FlushAsync(CancellationToken ct) => Target().FlushAsync(ct);
        public override void Write(byte[] buffer, int offset, int count) => Target().Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            Target().WriteAsync(buffer, offset, count, ct);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) =>
            Target().WriteAsync(buffer, ct);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
