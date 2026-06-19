using PgmStudio.Contracts;

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

    /// <summary>Orbit <paramref name="rect"/> by every step of <paramref name="mode"/> about (cx,cz) and
    /// key each image by the anchor it contains (block-rounded). The k=0 image is the authored rectangle.
    /// Each identity is assigned at most once (first containing image wins); an image over no anchor is
    /// dropped. Returns the images in orbit order, so index 0 is the authored owner when it covers a point.</summary>
    public static List<Zone> ByCoveredAnchor(
        (double MinX, double MinZ, double MaxX, double MaxZ) rect,
        string? mode, double cx, double cz, IReadOnlyList<Anchor> anchors)
    {
        var result = new List<Zone>();
        var taken = new HashSet<string>();
        var order = Symmetry.Order(mode);
        for (var k = 0; k < order; k++)
        {
            var r = k == 0
                ? rect
                : Symmetry.Rect(rect.MinX, rect.MinZ, rect.MaxX, rect.MaxZ, mode, cx, cz, k);
            double minX = Math.Round(r.MinX), minZ = Math.Round(r.MinZ), maxX = Math.Round(r.MaxX), maxZ = Math.Round(r.MaxZ);
            var hit = anchors.FirstOrDefault(a =>
                !taken.Contains(a.Id) && a.X >= minX && a.X <= maxX && a.Z >= minZ && a.Z <= maxZ);
            if (hit.Id is null) continue;   // default(Anchor) → no uncovered anchor in this image
            taken.Add(hit.Id);
            result.Add(new Zone(hit.Id, minX, minZ, maxX, maxZ));
        }
        return result;
    }
}
