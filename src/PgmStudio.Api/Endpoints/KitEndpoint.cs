using FastEndpoints;
using NSwag.Generation;
using PgmStudio.Api.Services;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/kit.py — a Python kit written from this studio's own schema: a constructor per shape a route
/// takes, a client over every route and a search over what each says (<see cref="PythonKit"/>).
///
/// <para>It is written once a process, from the document the process publishes, and its <c>ETag</c> is that
/// document's hash: a caller holding a kit asks with <c>If-None-Match</c> and is answered <c>304</c> while the
/// schema has not moved, and a kit written against another build of the studio says so by its hash.</para></summary>
public sealed class KitEndpoint(IOpenApiDocumentGenerator documents) : EndpointWithoutRequest
{
    private static Task<(string Script, string Hash)>? written;

    public override void Configure()
    {
        Get("/kit.py");
        Description(b => b.Produces(200, typeof(string), "text/x-python").Produces(304));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var (script, hash) = await Written(documents);
        var tag = $"\"{hash}\"";
        HttpContext.Response.Headers.ETag = tag;
        if (HttpContext.Request.Headers.IfNoneMatch.Contains(tag))
        {
            await Send.StatusCodeAsync(StatusCodes.Status304NotModified, ct);
            return;
        }
        HttpContext.Response.ContentType = "text/x-python; charset=utf-8";
        await HttpContext.Response.WriteAsync(script, ct);
    }

    private static Task<(string Script, string Hash)> Written(IOpenApiDocumentGenerator documents)
    {
        if (written is { } done) return done;
        var started = Write(documents);
        return Interlocked.CompareExchange(ref written, started, null) ?? started;
    }

    private static async Task<(string Script, string Hash)> Write(IOpenApiDocumentGenerator documents) =>
        PythonKit.Write((await documents.GenerateAsync("v1")).ToJson());
}
