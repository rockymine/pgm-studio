using PgmStudio.Geom;
using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Export;

/// <summary>
/// The columns two builds of a board disagree on, each sorted by what changed in it: <see cref="Ground"/> where
/// the ground rose, fell, came or went; <see cref="Surface"/> where it stands at the same height and the block
/// on top of it is another; <see cref="Structure"/> where both hold and something else in the column changed —
/// a building, a prop, a room, a made thing, a course under the surface.
/// </summary>
public sealed record WorldDiff(
    IReadOnlyList<(int X, int Z)> Ground, IReadOnlyList<(int X, int Z)> Surface, IReadOnlyList<(int X, int Z)> Structure)
{
    /// <summary>The three things a changed column can have changed in.</summary>
    public enum Changes { Ground, Surface, Structure }

    private const int GroundRgb = 0xE8A33D, SurfaceRgb = 0x3DB8E8, StructureRgb = 0xD04DD0;
    private const int LowRgb = 0x3A3D45, HighRgb = 0xB8BCC6;

    /// <summary>How many columns changed at all.</summary>
    public int Count => Ground.Count + Surface.Count + Structure.Count;

    /// <summary>The columns <paramref name="after"/> disagrees with <paramref name="before"/> on, in raster
    /// order within each kind.</summary>
    public static WorldDiff Between(BuiltWorld before, BuiltWorld after)
    {
        List<(int X, int Z)> ground = [], surface = [], structure = [];
        foreach (var cell in after.World.ColumnsDifferingFrom(before.World).Order())
        {
            int? was = before.Surface.TryGetValue(cell, out var top) ? top : null;
            int? now = after.Surface.TryGetValue(cell, out var next) ? next : null;
            if (was != now) ground.Add(cell);
            else if (was is { } course
                     && before.World.GetBlock(cell.X, course - 1, cell.Z) != after.World.GetBlock(cell.X, course - 1, cell.Z))
                surface.Add(cell);
            else structure.Add(cell);
        }
        return new(ground, surface, structure);
    }

    /// <summary>The columns of one kind.</summary>
    public IReadOnlyList<(int X, int Z)> Of(Changes kind) => kind switch
    {
        Changes.Ground => Ground,
        Changes.Surface => Surface,
        _ => Structure,
    };

    /// <summary>The changed columns of one kind as 4-connected runs, largest first, each with the box to find it
    /// in.</summary>
    public IReadOnlyList<(int Cells, CellRect Box)> Runs(Changes kind) =>
        [.. Cells.Stretches(Of(kind), floor: 1).Named.Select(run => (run.Area, Cells.BoundingBox(run.Cells)))];

    /// <summary>
    /// Both boards' ground, shaded lighter where it stands higher, with every changed column drawn over it in its
    /// kind's colour. The frame holds every column either build stands on, so ground taken away is drawn where
    /// it was. Null where neither build stands on anything.
    /// </summary>
    public byte[]? Png(BuiltWorld before, BuiltWorld after, int scale)
    {
        var columns = before.Surface.Keys.Concat(after.Surface.Keys).Concat(Ground).Concat(Surface).Concat(Structure)
            .ToHashSet();
        if (columns.Count == 0) return null;

        int minX = columns.Min(cell => cell.X), maxX = columns.Max(cell => cell.X);
        int minZ = columns.Min(cell => cell.Z), maxZ = columns.Max(cell => cell.Z);
        int wide = maxX - minX + 1, high = maxZ - minZ + 1;
        var pixels = new byte[wide * high * 3];
        Raster.FillRect(pixels, wide, high, 0, 0, wide, high, RenderCategories.VoidRgb);

        var tops = new Dictionary<(int X, int Z), int>(before.Surface);
        foreach (var (cell, top) in after.Surface) tops[cell] = top;
        if (tops.Count > 0)
        {
            int lowest = tops.Values.Min(), highest = tops.Values.Max();
            foreach (var (cell, top) in tops)
                Raster.Set(pixels, wide, cell.X - minX, cell.Z - minZ,
                    Raster.Lerp(LowRgb, HighRgb, highest == lowest ? 0.5 : (top - lowest) / (double)(highest - lowest)));
        }

        foreach (var (kind, rgb) in (ReadOnlySpan<(Changes, int)>)[
                     (Changes.Structure, StructureRgb), (Changes.Surface, SurfaceRgb), (Changes.Ground, GroundRgb)])
            foreach (var cell in Of(kind)) Raster.Over(pixels, wide, cell.X - minX, cell.Z - minZ, rgb, 0.85);

        var scaled = Raster.Upscale(pixels, wide, high, scale);
        var keyed = Legend.AppendBelow(scaled, wide * scale, high * scale,
        [
            new Legend.Entry($"GROUND CHANGED - {Ground.Count}", GroundRgb),
            new Legend.Entry($"SURFACE BLOCK CHANGED - {Surface.Count}", SurfaceRgb),
            new Legend.Entry($"STRUCTURE CHANGED - {Structure.Count}", StructureRgb),
            new Legend.Entry("UNCHANGED - LIGHTER IS HIGHER", HighRgb),
            new Legend.Entry("VOID", RenderCategories.VoidRgb),
        ], out var height, scaleLabel: $"SCALE: 1 BLOCK = {scale} PX - X {minX}..{maxX} Z {minZ}..{maxZ}");
        return PngWriter.Encode(wide * scale, height, keyed);
    }
}
