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
public sealed class MapXmlEndpoint(MapRepository repo, MapReader reader, FeatureData feature, PgmDb db, MapsRoots roots) : EndpointWithoutRequest
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

        string xml;
        try
        {
            var mx = Deserializer.FromDict(doc);
            // Generated maps get the standard CTW boilerplate (itemkeep/itemremove/toolrepair from the kit +
            // the kill-reward include + hunger off); corpus maps export exactly as parsed. itemremove is also
            // extended with the terrain drops of the blocks present on the top surface (seeds, saplings,
            // string, …) — best-effort: skipped when the surface palette isn't available (no world folder).
            if (isIntent)
            {
                // cache-only: use an already-scanned surface palette if present, but never trigger a world
                // scan from an export request — fall back to armor-only itemremove when it isn't cached.
                var surface = await ConfigureLayers.CellsAsync(db, roots, slug, map.Id, "surface", ct, cacheOnly: true);
                CtwStandards.Apply(mx, surface?.Select(c => c.BlockId).ToHashSet());

                // Renewables for the world-scanned resource blocks (iron/gold/diamond); tight region each.
                var resources = (await feature.ResourceBlocksAsync(map.Id, ct))
                    .Select(b => (b.Type, b.X, b.Y, b.Z)).ToList();
                ResourceRenewables.Apply(mx, resources);
            }
            xml = XmlWriter.ToXml(mx);
        }
        catch (Exception ex) { await Send.ResponseAsync(new Dict { ["error"] = ex.Message }, 500, ct); return; }

        HttpContext.Response.ContentType = "application/xml; charset=utf-8";
        HttpContext.Response.Headers.ContentDisposition = ContentDispositionHeader.Attachment($"{slug}.xml");
        await HttpContext.Response.WriteAsync(xml, ct);
    }
}
