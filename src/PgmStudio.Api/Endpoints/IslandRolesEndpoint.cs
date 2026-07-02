using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using NetTopologySuite.Geometries;
using PgmStudio.Analysis.Footprint;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/map/{slug}/island-roles — per detected island, in the stored <c>islands_json</c> /
/// island-sketch order (1:1 with the sketch shapes): its semantic <c>role</c> (team / objective /
/// neutral / decorative), <c>blockCount</c>, and the objective <c>anchors</c> it carries
/// (<c>{ kind: "spawn"|"wool", x, z }</c>). Plus the <c>buildRegion</c> outline (the
/// <c>RegionCategorizer</c> build geometry as GeoJSON). Reuses <see cref="IslandRoleClassifier"/>;
/// reflects the new detection only on maps re-scanned through it.
/// </summary>
public sealed class IslandRolesEndpoint(MapRepository repo, MapReader reader, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/island-roles"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.IslandsJson, ct);
        var islands = IslandRoleData.ParseIslands(art?.Data);
        var geoms = islands.Select(i => i.Geom).ToList();

        // Roles/anchors/build need the map doc; without it (xml-only / not-yet-imported) report islands only.
        var doc = geoms.Count > 0 ? await reader.ReadDocAsync(slug, ct) : null;
        List<IslandRoleClassifier.Anchor> anchors = [];
        Geometry? build = null;
        if (doc is not null) (anchors, build) = IslandRoleData.Context(doc, geoms);
        var assessed = IslandRoleClassifier.Assess(geoms, anchors, build);

        var result = islands.Select((isl, i) => new
        {
            index = i,
            role = assessed[i].Role.ToString().ToLowerInvariant(),
            blockCount = isl.Blocks,
            anchors = ClusterAnchors(assessed[i].Anchors
                    .Select(a => { var pt = a.Geom.InteriorPoint; return (kind: a.IsSpawn ? "spawn" : "wool", x: pt.X, z: pt.Y); }))
                .Select(a => new { a.kind, a.x, a.z }).ToList(),
        });

        await Send.OkAsync(new { islands = result, buildRegion = GeometryToGeoJson(build) }, ct);
    }

    // One anchor per objective: a wool emits several near-coincident footprints (its location point + wool
    // room + dispensing spawner), so greedily keep an anchor unless one of the same kind sits within a few
    // blocks. Distance-based (not coordinate rounding) so a symmetric map yields symmetric anchor counts.
    private static List<(string kind, double x, double z)> ClusterAnchors(IEnumerable<(string kind, double x, double z)> pts)
    {
        const double tol = 4;
        var reps = new List<(string kind, double x, double z)>();
        foreach (var p in pts)
            if (!reps.Any(r => r.kind == p.kind && Math.Abs(r.x - p.x) <= tol && Math.Abs(r.z - p.z) <= tol))
                reps.Add(p);
        return reps;
    }

    // NTS geometry (the build-region union — a Polygon or MultiPolygon) → GeoJSON coordinates, matching the
    // [x, z] ring ordering of islands_json so the canvas can render it like an island outline.
    private static object? GeometryToGeoJson(Geometry? g)
    {
        if (g is null || g.IsEmpty) return null;
        if (g is Polygon p) return new { type = "Polygon", coordinates = PolygonCoords(p) };
        var polys = new List<List<List<double[]>>>();
        for (var i = 0; i < g.NumGeometries; i++)
            if (g.GetGeometryN(i) is Polygon part && !part.IsEmpty) polys.Add(PolygonCoords(part));
        return polys.Count == 0 ? null : new { type = "MultiPolygon", coordinates = polys };
    }

    private static List<List<double[]>> PolygonCoords(Polygon p)
    {
        var rings = new List<List<double[]>> { RingCoords(p.ExteriorRing) };
        foreach (var hole in p.InteriorRings) rings.Add(RingCoords(hole));
        return rings;
    }

    private static List<double[]> RingCoords(LineString ring) =>
        ring.Coordinates.Select(c => new[] { c.X, c.Y }).ToList();
}
