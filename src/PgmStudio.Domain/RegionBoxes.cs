namespace PgmStudio.Domain;

/// <summary>
/// A PGM region reduced to the block boxes it covers — the bridge between a map's declared geometry and the
/// world it describes.
///
/// <para>Reduction is deliberately incomplete. A region may be a cylinder, a sphere, a half-space or a
/// transform, and none of those is a box; rather than approximate one, a shape that does not reduce
/// contributes <b>nothing</b>. Every caller is written so that "no boxes" is the safe answer — a region that
/// cannot be stated exactly leaves the world exactly as it reads — so an approximation would trade a knowable
/// gap for an unknowable error.</para>
///
/// <para>A composite contributes only its <b>additive</b> children: every child of a union or intersect, and
/// the first of a complement or negative, since the rest are subtracted. Nothing here computes the
/// subtraction, which is the other reason the result is a lower bound on the region and never an upper
/// one.</para>
/// </summary>
public static class RegionBoxes
{
    /// <summary>The block boxes a region covers, empty when its shape does not reduce to boxes.</summary>
    public static IReadOnlyList<BlockBox> Of(IReadOnlyDictionary<string, Region> registry, Region region) =>
        [.. Walk(registry, region, [])];

    /// <summary>The block boxes a region id covers, empty when the id is unknown.</summary>
    public static IReadOnlyList<BlockBox> Of(IReadOnlyDictionary<string, Region> registry, string regionId) =>
        registry.TryGetValue(regionId, out var region) ? Of(registry, region) : [];

    /// <summary>The single box enclosing everything a region covers, or null when it reduces to nothing.</summary>
    public static BlockBox? Enclosing(IReadOnlyDictionary<string, Region> registry, string regionId)
    {
        var boxes = Of(registry, regionId);
        if (boxes.Count == 0) return null;
        return new BlockBox(
            boxes.Min(b => b.MinX), boxes.Min(b => b.MinY), boxes.Min(b => b.MinZ),
            boxes.Max(b => b.MaxX), boxes.Max(b => b.MaxY), boxes.Max(b => b.MaxZ));
    }

    private static IEnumerable<BlockBox> Walk(IReadOnlyDictionary<string, Region> registry, Region region,
                                              HashSet<string> path)
    {
        if (region.Id.Length > 0 && !path.Add(region.Id)) yield break;

        switch (region.Type)
        {
            case "cuboid":
                if (Box(region.MinX, region.MinY, region.MinZ, region.MaxX, region.MaxY, region.MaxZ) is { } cuboid)
                    yield return cuboid;
                break;

            case "block":
                if (region.PosX is { } bx && region.PosY is { } by && region.PosZ is { } bz)
                {
                    int x = (int)Math.Floor(bx), y = (int)Math.Floor(by), z = (int)Math.Floor(bz);
                    yield return new BlockBox(x, y, z, x, y, z);
                }
                break;

            case "union" or "intersect" or "complement" or "negative":
                var children = region.Children ?? [];
                var additive = region.Type is "union" or "intersect" ? children : children.Take(1);
                foreach (var childId in additive)
                    if (registry.TryGetValue(childId, out var child))
                        foreach (var box in Walk(registry, child, [.. path])) yield return box;
                break;
        }
    }

    // PGM cuboid bounds are [min, max) on every axis, so the last block is one inside max. A component that is
    // absent (a template variable) or infinite makes the box unstatable.
    private static BlockBox? Box(double? minX, double? minY, double? minZ, double? maxX, double? maxY, double? maxZ)
    {
        double?[] parts = [minX, minY, minZ, maxX, maxY, maxZ];
        if (parts.Any(p => p is null || double.IsInfinity(p.Value) || double.IsNaN(p.Value))) return null;

        static (int Lo, int Hi) Span(double a, double b)
            => ((int)Math.Floor(Math.Min(a, b)), (int)Math.Ceiling(Math.Max(a, b)) - 1);

        var (loX, hiX) = Span(minX!.Value, maxX!.Value);
        var (loY, hiY) = Span(minY!.Value, maxY!.Value);
        var (loZ, hiZ) = Span(minZ!.Value, maxZ!.Value);
        return hiX < loX || hiY < loY || hiZ < loZ ? null : new BlockBox(loX, loY, loZ, hiX, hiY, hiZ);
    }
}
