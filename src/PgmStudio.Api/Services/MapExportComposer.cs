using System.Text;
using PgmStudio.Analysis.Playability;
using PgmStudio.Api.Endpoints;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>The outcome of composing a map for export: either a structured error (HTTP status + JSON body)
/// or the composed <c>map.xml</c>. For a sketch-originated map <see cref="World"/> also carries the
/// synthesised voxel world so the caller can bundle its region files.</summary>
public sealed record ExportComposition(int? ErrorStatus, Dict? ErrorBody, string? Xml, SketchWorld? World)
{
    public bool IsError => ErrorStatus is not null;
}

/// <summary>
/// The shared pipeline behind <c>GET /map/{slug}/xml</c> and <c>GET /map/{slug}/export</c>: the
/// traversability gate, surface/resource prep, sketch-intent resolution, and XML composition — so the two
/// routes can't drift, and the reviewed XML is exactly what ships. For a sketch map it builds the world and
/// re-projects the resolved intent (snapped spawns + auto-derived monument locations) so the XML agrees
/// with the world; the build + compose run under one guard, surfacing any failure as a structured error.
/// </summary>
public static class MapExportComposer
{
    public static async Task<ExportComposition> ComposeAsync(
        long mapId, Dict doc, byte[]? layoutBytes, FeatureData feature, PgmDb db, CancellationToken ct)
    {
        var isIntent = await IntentStore.HasAsync(db, mapId, ct);

        // Playability gate: intent-authored maps must be traversable before they can export (§9).
        if (isIntent)
        {
            var segs = await feature.SegmentsAsync(mapId, ct);
            var trav = Traversability.Check(doc, segs?.SurfaceColumns(), segs?.Y0Columns());
            if (!trav.Connected)
                return new(409, new Dict
                {
                    ["error"] = "not traversable",
                    ["message"] = trav.Message,
                    ["isolated"] = trav.Isolated.Select(i => new Dict { ["kind"] = i.Kind, ["name"] = i.Name }).ToList(),
                }, null, null);
        }

        try
        {
            // Sketch-originated: synthesise the world, re-project the resolved intent so the XML agrees with
            // it, then compose (no scanned surface/resources for a sketch map).
            if (layoutBytes is not null)
            {
                var intent = await IntentStore.LoadAsync(db, mapId, ct);
                var built = SketchWorldBuilder.Build(Encoding.UTF8.GetString(layoutBytes), intent);
                IntentGenerator.Apply(doc, built.ResolvedIntent);
                var sketchXml = MapXmlComposer.Compose(doc, isIntent: true, surfaceBlockIds: null, resources: []);
                return new(null, null, sketchXml, built);
            }

            // Other maps get plain XML (they already ship a world). Intent maps additionally get the cached
            // surface palette + spawn-ore renewables — cache-only, never triggering a world scan on export.
            IReadOnlySet<int>? surfacePalette = null;
            IReadOnlyList<(string Type, int X, int Y, int Z)> resources = [];
            if (isIntent)
            {
                var surface = await ConfigureLayers.CellsAsync(db, mapId, "surface", ct);
                surfacePalette = surface?.Select(c => c.BlockId).ToHashSet();
                resources = (await feature.ResourceBlocksAsync(mapId, ct)).Select(b => (b.Type, b.X, b.Y, b.Z)).ToList();
            }

            var xml = MapXmlComposer.Compose(doc, isIntent, surfacePalette, resources);
            return new(null, null, xml, null);
        }
        catch (Exception ex)
        {
            return new(500, new Dict { ["error"] = ex.Message }, null, null);
        }
    }
}
