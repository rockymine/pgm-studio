using FastEndpoints;
using Microsoft.AspNetCore.Http;
using PgmStudio.Analysis.Playability;
using PgmStudio.Api.Http;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// GET /api/map/{slug}/xml — render the map as a PGM <c>map.xml</c> (proto 1.5.0). The export leg of the
/// round-trip: doc → <c>Deserializer.FromDict</c> → <c>MapXml</c> → <c>XmlWriter.ToXml</c> — the same
/// path the round-trip harness (check #2) exercises across the corpus. For intent-authored maps this is
/// what proves the generated document is a real, loadable PGM map.
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
        var isIntent = await IntentStore.HasAsync(db, map.Id, ct);

        // Playability gate: intent-authored maps must be traversable before they can export (§9).
        if (isIntent)
        {
            var segs = await feature.SegmentsAsync(map.Id, ct);
            var trav = Traversability.Check(doc, segs?.SurfaceColumns(), segs?.Y0Columns());
            if (!trav.Connected)
            {
                await Send.ResponseAsync(new Dict
                {
                    ["error"] = "not traversable",
                    ["message"] = trav.Message,
                    ["isolated"] = trav.Isolated.Select(i => new Dict { ["kind"] = i.Kind, ["name"] = i.Name }).ToList(),
                }, 409, ct);
                return;
            }
        }

        // Generated maps get the standard CTW boilerplate + spawn-ore renewables (corpus maps export as
        // parsed). Surface palette / resource blocks are cache-only — never trigger a world scan from an
        // export request (fall back to armor-only itemremove / no renewables when not cached).
        IReadOnlySet<int>? surfacePalette = null;
        IReadOnlyList<(string Type, int X, int Y, int Z)> resources = [];
        if (isIntent)
        {
            var surface = await ConfigureLayers.CellsAsync(db, map.Id, "surface", ct);
            surfacePalette = surface?.Select(c => c.BlockId).ToHashSet();
            resources = (await feature.ResourceBlocksAsync(map.Id, ct)).Select(b => (b.Type, b.X, b.Y, b.Z)).ToList();
        }

        string xml;
        try { xml = MapXmlComposer.Compose(doc, isIntent, surfacePalette, resources); }
        catch (Exception ex) { await Send.ResponseAsync(new Dict { ["error"] = ex.Message }, 500, ct); return; }

        HttpContext.Response.ContentType = "application/xml; charset=utf-8";
        HttpContext.Response.Headers.ContentDisposition = ContentDispositionHeader.Attachment($"{slug}.xml");
        await HttpContext.Response.WriteAsync(xml, ct);
    }
}
