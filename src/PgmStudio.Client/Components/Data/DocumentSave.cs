using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>What a save did. <see cref="SaveStatus.NotSent"/> is a save that never reached the studio —
/// superseded by a later one while it waited its turn, or with nothing new to carry — and says nothing.</summary>
public enum SaveStatus
{
    NotSent,
    Landed,
    Refused,
}

/// <summary>A save's status, for a refusal the sentence an author reads, and the findings the studio answered
/// with: a refusal's own, or the warnings a landed save carried.</summary>
public readonly record struct SaveOutcome(SaveStatus Status, string? Message = null, IReadOnlyList<Finding>? Findings = null)
{
    public bool Landed => Status == SaveStatus.Landed;

    /// <summary>The findings, never null.</summary>
    public IReadOnlyList<Finding> Raised => Findings ?? [];
}

/// <summary>
/// The write of one stored document that states the revision it stands on.
///
/// <para>The studio answers every read of a document with its <c>ETag</c>; the tab holds it
/// (<see cref="Hold"/>) and states it as <c>If-Match</c> on every write, so a tab holding an older document
/// than the studio's is refused (<c>RQ13</c>, 409) instead of writing it back over the newer one. A refusal
/// of that kind marks the tab <see cref="Superseded"/>: the stored document has moved on without it, every
/// further write would be refused the same way, so none is sent until the document is read again. A document
/// the studio held nothing for states no precondition, and the revision its first write lands at is held for
/// the next.</para>
///
/// <para>Writes are serialised — two in flight would state the same revision, and the second would be
/// refused against the one the first just wrote. A refused or failed write is a completed round-trip, so
/// nothing throws and the status is the only thing that says the document is not in the studio; the outcome
/// carries the server's own sentence, because it is the one that knows which shape it could not take.</para>
/// </summary>
public sealed class DocumentSave(HttpClient http)
{
    /// <summary>The sentence for a write refused as stale.</summary>
    public const string SupersededMessage =
        "Not saved: this map was saved from somewhere else after this tab opened it. "
        + "Reload the page to get the latest version. Edits made here since then will be lost.";

    private const string Unreachable = "Not saved. Check your connection and try again.";

    private string? held;
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Whether a write was refused as stale since the document was last read.</summary>
    public bool Superseded { get; private set; }

    /// <summary>Take the revision a read of the document answered as the one this tab stands on.</summary>
    public void Hold(HttpResponseMessage read)
    {
        held = read.Headers.ETag?.Tag;
        Superseded = false;
    }

    /// <summary>Stand on no revision, for a document the tab has not read.</summary>
    public void Forget()
    {
        held = null;
        Superseded = false;
    }

    /// <summary>Replace the document at <paramref name="uri"/> with what <paramref name="body"/> builds,
    /// which runs inside the turn so it reads the document as it is when the write is sent.
    /// <paramref name="needed"/>, when given, is asked in the same turn and a false answer sends nothing.
    /// <paramref name="token"/> cancels the wait for the turn and never a write already sent: one the studio
    /// took and this tab never heard the answer to would leave the held revision behind the stored one.</summary>
    public async Task<SaveOutcome> PutAsync(string uri, Func<Task<HttpContent>> body,
        Func<bool>? needed = null, CancellationToken token = default)
    {
        try { await gate.WaitAsync(token); }
        catch (OperationCanceledException) { return new(SaveStatus.NotSent); }
        try
        {
            if (Superseded) return new(SaveStatus.Refused, SupersededMessage);
            if (needed is not null && !needed()) return new(SaveStatus.NotSent);

            using var request = new HttpRequestMessage(HttpMethod.Put, uri) { Content = await body() };
            if (held is not null) request.Headers.TryAddWithoutValidation("If-Match", held);
            using var response = await http.SendAsync(request, CancellationToken.None);
            if (response.IsSuccessStatusCode)
            {
                held = response.Headers.ETag?.Tag ?? held;
                var warnings = ServerWarnings.Carried(response).Count == 0 ? []
                    : (await ServerWarnings.AnsweredAsync<System.Text.Json.JsonElement>(response)).Warnings;
                return new(SaveStatus.Landed, Findings: warnings);
            }
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict && held is not null)
            {
                Superseded = true;
                return new(SaveStatus.Refused, SupersededMessage);
            }
            var refusal = await ServerRefusal.ReadAsync(response);
            return new(SaveStatus.Refused, "Not saved. " + refusal.Message, refusal.Findings);
        }
        catch { return new(SaveStatus.Refused, Unreachable); }
        finally { gate.Release(); }
    }
}
