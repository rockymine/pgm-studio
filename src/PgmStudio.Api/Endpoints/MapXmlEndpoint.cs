using FastEndpoints;
using Microsoft.AspNetCore.Http;
using PgmStudio.Data.Repositories;
using PgmStudio.Pgm;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// GET /api/map/{slug}/xml — render the map as a PGM <c>map.xml</c> (proto 1.5.0). The export leg of the
/// round-trip: doc → <c>Deserializer.FromDict</c> → <c>MapXml</c> → <c>XmlWriter.ToXml</c> — the same
/// path the round-trip harness (check #2) exercises across the corpus. For intent-authored maps this is
/// what proves the generated document is a real, loadable PGM map.
/// </summary>
public sealed class MapXmlEndpoint(MapRepository repo, MapReader reader) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/xml"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var doc = await reader.ReadDocAsync(map, ct);
        string xml;
        try { xml = XmlWriter.ToXml(Deserializer.FromDict(doc)); }
        catch (Exception ex) { await Send.ResponseAsync(new Dict { ["error"] = ex.Message }, 500, ct); return; }

        HttpContext.Response.ContentType = "application/xml; charset=utf-8";
        HttpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{slug}.xml\"";
        await HttpContext.Response.WriteAsync(xml, ct);
    }
}
