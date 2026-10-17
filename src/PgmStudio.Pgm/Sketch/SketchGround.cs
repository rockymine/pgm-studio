using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Derive;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The ground a sketched board's structure is read off, one block to a cell: every column the layout builds,
/// owned by the group whose shape forms it, and every spawn and wool region, owned by the room it holds. A
/// cell's team is the image of the spawn nearest it, the half or quarter of the board that team plays from;
/// building is allowed where the intent states a build area, and each wool's room is its region at every
/// image. <see cref="BoardDeriver.Derive(BoardGround)"/> reads it as it reads a plan's.
/// </summary>
public static class SketchGround
{
    /// <summary>The ground the layout and the intent's play pieces make; null where the layout builds nothing.</summary>
    public static BoardGround? Of(string layoutJson, MapIntent intent)
    {
        var footprint = SketchRasterizer.Rasterize(layoutJson);
        if (footprint.Count == 0) return null;
        var groups = SketchRasterizer.GroupOwners(layoutJson);
        var spawns = intent.Spawns.Where(spawn => spawn.Protection.Count > 0).ToList();
        int TeamOf(double x, double z) => spawns.Count == 0 ? 0
            : spawns.MinBy(spawn => Distance(spawn.Point.X, spawn.Point.Z, x + 0.5, z + 0.5))!.Stamp.Image;

        var filled = new Dictionary<(int, int), (string Owner, int K)>();
        foreach (var (x, z) in footprint)
            filled[(x, z)] = (groups.GetValueOrDefault((x, z), GroundOwner), TeamOf(x, z));

        var wools = intent.Wools ?? [];
        foreach (var spawn in spawns)
            foreach (var cell in Cells(spawn.Protection)) filled[cell] = (spawn.Id, spawn.Stamp.Image);
        foreach (var wool in wools)
            foreach (var cell in Cells(wool.Protection)) filled[cell] = (wool.Id, wool.Stamp.Image);

        var build = Cells((intent.Build?.Areas ?? []).Select(area => area.Rect)).ToHashSet();
        var units = wools.Select(wool => wool.Stamp.Unit).Distinct().ToList();
        var rooms = wools.Where(wool => wool.Protection.Count > 0)
            .Select(wool => new BoardWoolRoom(units.IndexOf(wool.Stamp.Unit), wool.Stamp.Image,
                Cells(wool.Protection).ToHashSet(), wool.Spawn.X, wool.Spawn.Z))
            .ToList();

        // A room's ground is its owner's however the regions overlap, so a spawn sharing ground with a wool is both.
        HashSet<string> OwnersOf(IEnumerable<Rect> regions) =>
            Cells(regions).Where(filled.ContainsKey).Select(cell => filled[cell].Owner).ToHashSet(StringComparer.Ordinal);
        return new BoardGround(1, filled, build, [],
            OwnersOf(spawns.SelectMany(spawn => spawn.Protection)), OwnersOf(wools.SelectMany(wool => wool.Protection)), rooms,
            [.. filled.Where(entry => entry.Value.K == 0).Select(entry => entry.Key)]);
    }

    /// <summary>Who a column standing in no group is owned by.</summary>
    public const string GroundOwner = "ground";

    private static IEnumerable<(int, int)> Cells(IEnumerable<Rect> rects)
    {
        foreach (var rect in rects)
            for (var x = (int)Math.Floor(rect.MinX); x < (int)Math.Ceiling(rect.MaxX); x++)
                for (var z = (int)Math.Floor(rect.MinZ); z < (int)Math.Ceiling(rect.MaxZ); z++)
                    yield return (x, z);
    }

    private static double Distance(double x1, double z1, double x2, double z2) =>
        (x1 - x2) * (x1 - x2) + (z1 - z2) * (z1 - z2);
}
