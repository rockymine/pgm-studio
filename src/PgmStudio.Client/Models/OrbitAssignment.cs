using PgmStudio.Geom;

namespace PgmStudio.Client.Models;

/// <summary>
/// The point-aware orbit-assignment "generator" shared by the rectangle-over-point Configure phases
/// (spawn Protection, and the Wools room step). Given a rectangle the author drew over one anchor point
/// plus the confirmed symmetry, it orbits the rectangle across every side and keys each copy by the
/// identity of the anchor it <b>covers</b> — spatial containment, never default orbit order (which would
/// drop a team's spawn inside another team's region and violate PGM). Geometry is the canonical
/// <see cref="Symmetry"/>; this is the authoring counterpart of the server generator's orbit-fill.
/// </summary>
public static class OrbitAssignment
{
    /// <summary>An identified point the orbit assigns to (a spawn → its team; a wool spawn → its wool).</summary>
    public readonly record struct Anchor(string Id, double X, double Z);

    /// <summary>An assigned rectangle: the identity that owns it + its block-rounded bounds.</summary>
    public readonly record struct Zone(string Id, double MinX, double MinZ, double MaxX, double MaxZ);

    /// <summary>One identity's multi-rect footprint after orbit: the owning anchor + its rectangles.</summary>
    public readonly record struct ZoneSet(string Id, List<Zone> Rects);

    /// <summary>Orbit a <b>set</b> of rectangles (one authored unit's multi-rect footprint) and assign each
    /// orbit image-set to the anchor its <i>primary</i> rect covers. <paramref name="rects"/>[0] is the
    /// primary — it must cover an anchor to seed the authored owner; the rest are extra pieces of the same
    /// unit that ride the same orbit step (orbit-order, not coverage). Returns, per covered orbit step, the
    /// owning anchor id + the block-rounded transformed rects (input order). The k=0 set is the authored
    /// owner. Each identity is assigned at most once (first covering step wins).</summary>
    public static List<ZoneSet> ByCoveredAnchorSet(
        IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> rects,
        string? mode, double cx, double cz, IReadOnlyList<Anchor> anchors)
    {
        var result = new List<ZoneSet>();
        if (rects.Count == 0) return result;
        var taken = new HashSet<string>();
        var order = Symmetry.Order(mode);
        for (var k = 0; k < order; k++)
        {
            var images = rects.Select(r => Transform(r, mode, cx, cz, k)).ToList();
            var (pMinX, pMinZ, pMaxX, pMaxZ) = images[0];   // the primary keys the owner by coverage
            var hit = anchors.FirstOrDefault(a =>
                !taken.Contains(a.Id) && a.X >= pMinX && a.X <= pMaxX && a.Z >= pMinZ && a.Z <= pMaxZ);
            if (hit.Id is null) continue;   // default(Anchor) → the primary image covers no free anchor
            taken.Add(hit.Id);
            result.Add(new ZoneSet(hit.Id, images.Select(i => new Zone(hit.Id, i.MinX, i.MinZ, i.MaxX, i.MaxZ)).ToList()));
        }
        return result;
    }

    // Transform a rect by orbit step k (k=0 is identity) and snap its bounds to the block grid.
    private static (double MinX, double MinZ, double MaxX, double MaxZ) Transform(
        (double MinX, double MinZ, double MaxX, double MaxZ) r, string? mode, double cx, double cz, int k)
    {
        var t = k == 0 ? r : Symmetry.Rect(r.MinX, r.MinZ, r.MaxX, r.MaxZ, mode, cx, cz, k);
        return (Math.Round(t.MinX), Math.Round(t.MinZ), Math.Round(t.MaxX), Math.Round(t.MaxZ));
    }
}
