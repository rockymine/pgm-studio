using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The door a wool room stating none is cut with: one, centred on the side of its region that meets the most
/// ground — terrain the layout builds or a build area — just outside it, the side nearer the board's centre on a
/// tie. A house is entered one or two ways, so a room is never given a door on every wall for want of one stated.
/// </summary>
public static class WoolDoors
{
    /// <summary>The intent with every wool that states no door given its one, read off the ground
    /// <paramref name="layoutJson"/> builds; a wool stating any is left as it is.</summary>
    public static MapIntent Settled(MapIntent intent, string layoutJson)
    {
        if ((intent.Wools ?? []).All(wool => wool.Doors.Count > 0)) return intent;
        var ground = Ground(layoutJson, intent);
        var symmetry = intent.Symmetry ?? SketchLayout.Parse(layoutJson)?.Setup?.Board;
        var centre = (symmetry?.CenterX ?? 0, symmetry?.CenterZ ?? 0);
        return intent with
        {
            Wools = [.. intent.Wools!.Select(wool => wool.Doors.Count > 0 || wool.Protection.Count == 0
                ? wool
                : wool with { Doors = [StatedDoor.Centred(Side(wool.Protection, ground, centre))] })],
        };
    }

    /// <summary>The side of <paramref name="regions"/> a wool's one door opens on: the one whose outside meets
    /// the most of <paramref name="ground"/>, the one facing <paramref name="centre"/> most on a tie.</summary>
    public static RoomEdge Side(IReadOnlyList<Rect> regions, Func<(int X, int Z), bool> ground, (double X, double Z) centre)
    {
        int minX = (int)regions.Min(r => r.MinX), minZ = (int)regions.Min(r => r.MinZ);
        int maxX = (int)regions.Max(r => r.MaxX), maxZ = (int)regions.Max(r => r.MaxZ);
        bool Inside((int X, int Z) cell) => regions.Any(r =>
            cell.X + 0.5 > r.MinX && cell.X + 0.5 < r.MaxX && cell.Z + 0.5 > r.MinZ && cell.Z + 0.5 < r.MaxZ);
        IEnumerable<(int, int)> Outside(RoomEdge edge) => edge switch
        {
            RoomEdge.NegZ => Enumerable.Range(minX, maxX - minX).Select(x => (x, minZ - 1)),
            RoomEdge.PosZ => Enumerable.Range(minX, maxX - minX).Select(x => (x, maxZ)),
            RoomEdge.NegX => Enumerable.Range(minZ, maxZ - minZ).Select(z => (minX - 1, z)),
            _ => Enumerable.Range(minZ, maxZ - minZ).Select(z => (maxX, z)),
        };
        double towardX = centre.X - (minX + maxX) / 2.0, towardZ = centre.Z - (minZ + maxZ) / 2.0;
        return RoomEdges.All
            .OrderByDescending(edge => Outside(edge).Count(cell => !Inside(cell) && ground(cell)))
            .ThenByDescending(edge => edge.Outward().X * towardX + edge.Outward().Z * towardZ)
            .First();
    }

    /// <summary>The ground a door can open onto: every column the layout builds and every block of a build
    /// area.</summary>
    public static Func<(int X, int Z), bool> Ground(string layoutJson, MapIntent intent)
    {
        var cells = SketchRasterizer.Rasterize(layoutJson).ToHashSet();
        foreach (var area in intent.Build?.Areas ?? [])
            for (var x = (int)Math.Floor(area.MinX); x < (int)Math.Ceiling(area.MaxX); x++)
                for (var z = (int)Math.Floor(area.MinZ); z < (int)Math.Ceiling(area.MaxZ); z++)
                    cells.Add((x, z));
        return cells.Contains;
    }
}
