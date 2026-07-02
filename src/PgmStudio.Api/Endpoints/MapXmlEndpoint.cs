using FastEndpoints;
using Microsoft.AspNetCore.Http;
using PgmStudio.Analysis.Playability;
using PgmStudio.Api.Http;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/map/{slug}/xml — render the map as a PGM <c>map.xml</c> (proto 1.5.0). The export leg of the
/// round-trip: doc → <c>Deserializer.FromDict</c> → <c>MapXml</c> → <c>XmlWriter.ToXml</c> — the same
/// path the round-trip harness (check #2) exercises across the corpus. For intent-authored maps this is
/// what proves the generated document is a real, loadable PGM map. Delegates to
/// <see cref="MapExportComposer"/> so what's reviewed here is exactly what <see cref="MapExportEndpoint"/>
/// ships (for a sketch map, the <em>resolved</em> XML — snapped spawns + auto-derived monuments).
/// <para><b>Playability gate (new-map-authoring.md §9).</b> For intent-authored maps (those with a stored
/// intent blob), export is <b>blocked</b> (HTTP 409) unless <see cref="Traversability"/> reports the
/// spawn↔wool chain connected — a valid, mirror-correct document can still be unplayable (islands not
/// bridged), and this is the only check that catches it. Corpus maps have no intent and export
/// unconditionally (unchanged).</para>
/// </summary>
public sealed class MapXmlEndpoint(MapRepository repo, MapReader reader, FeatureData feature, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/xml"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var doc = await reader.ReadDocAsync(map, ct);
        var layoutBytes = await SketchStore.LoadAsync(db, map.Id, ct);
        var result = await MapExportComposer.ComposeAsync(map.Id, doc, layoutBytes, feature, db, ct);
        if (result.IsError) { await Send.ResponseAsync(result.ErrorBody!, result.ErrorStatus!.Value, ct); return; }

        HttpContext.Response.ContentType = "application/xml; charset=utf-8";
        HttpContext.Response.Headers.ContentDisposition = ContentDispositionHeader.Attachment($"{slug}.xml");
        await HttpContext.Response.WriteAsync(result.Xml!, ct);
    }
}
