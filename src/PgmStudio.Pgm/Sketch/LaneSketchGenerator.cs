using PgmStudio.Geom;

namespace PgmStudio.Pgm.Sketch;

/// <summary>Knobs for the lane-layout sketch generator. Defaults give a 60×90 two-team board with
/// 12-wide lanes.</summary>
public sealed record LaneLayoutOptions
{
    public double Width { get; init; } = 60;
    public double Height { get; init; } = 90;
    /// <summary>Target lane width in blocks (legs and crossbar).</summary>
    public double LaneWidth { get; init; } = 12;
    /// <summary>Empty border kept clear of the map edge.</summary>
    public double Margin { get; init; } = 6;
    /// <summary>Bow the crossbar into a curve (a smoothed centerline) instead of a straight strip.</summary>
    public bool CurvedCrossbar { get; init; }
    /// <summary>Drop a neutral island in the mid gap (the contested centre).</summary>
    public bool MidIsland { get; init; } = true;
    /// <summary>Symmetry that reflects the authored team onto its opponent. <c>mirror_z</c> reflects across
    /// the horizontal mid line (both teams keep the wool on the same side); <c>rot_180</c> is point-symmetric.</summary>
    public string MirrorMode { get; init; } = "mirror_z";
}

/// <summary>Where an objective belongs on the generated board, for the Configure step to consume.</summary>
public sealed record ObjectiveHint(string Kind, int Team, double X, double Z);

/// <summary>The generated layout plus the objective placements it implies.</summary>
public sealed record LaneLayoutResult(SketchLayout Layout, IReadOnlyList<ObjectiveHint> Objectives);

/// <summary>
/// Builds a starter Capture-the-Wool sketch from lane primitives. The board is a per-team "H" — a wool
/// leg (a dead-end toward the far side), a spawn leg, and a crossbar that is their only link (so the spawn
/// branches off rather than sharing the wool lane) — authored once and mirrored across the map mid, with
/// an optional neutral island in the gap. Leg ends stop short of the mid so the two teams land on separate
/// islands; the gaps are what the Configure build regions bridge into one navigable plane. The output is a
/// <see cref="SketchLayout"/> ready to store on a draft map or feed straight to <see cref="SketchRasterizer"/>.
/// </summary>
public static class LaneSketchGenerator
{
    public static LaneLayoutResult HLayout(LaneLayoutOptions? options = null)
    {
        var o = options ?? new LaneLayoutOptions();
        double cx = o.Width / 2, cz = o.Height / 2;
        double lx = o.Margin + o.LaneWidth / 2;            // wool leg centerline x (left)
        double rx = o.Width - o.Margin - o.LaneWidth / 2;  // spawn leg centerline x (right)
        double top = o.Margin + 2;                         // dead-end (far) side
        double bot = cz - 5;                               // stop short of mid → a gap to bridge
        double crossZ = top + 16;                          // crossbar height (upper third)

        var woolLeg = Lane.Strip([[lx, top], [lx, bot]], o.LaneWidth);
        var spawnLeg = Lane.Strip([[rx, top], [rx, bot]], o.LaneWidth);
        var cross = o.CurvedCrossbar
            ? Lane.Strip(Lane.Smooth([[o.Margin, crossZ], [cx, crossZ - 6], [o.Width - o.Margin, crossZ]]), o.LaneWidth)
            : Lane.Strip([[o.Margin, crossZ], [o.Width - o.Margin, crossZ]], o.LaneWidth);

        var shapes = new List<SketchShape> { Poly("wool_leg", woolLeg), Poly("spawn_leg", spawnLeg), Poly("cross", cross) };
        var islands = new List<SketchIsland>
        {
            new() { Id = "team", Name = "Team", Mirrors = true, ShapeIds = ["wool_leg", "spawn_leg", "cross"] },
        };
        if (o.MidIsland)
        {
            shapes.Add(Poly("mid", RegularPolygon(cx, cz, o.LaneWidth * 0.7, 8)));
            islands.Add(new() { Id = "mid", Name = "Mid", Mirrors = false, ShapeIds = ["mid"] });
        }

        var layout = new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = o.MirrorMode, Center = new SketchCenter { Cx = cx, Cz = cz } },
            Layout = new SketchShapes { Shapes = shapes, Islands = islands },
        };

        // Team 0 owns the authored H; team 1 is its mirror image. Objectives sit at the leg tops.
        var (woolX, woolZ) = (lx, top + 1);
        var (spawnX, spawnZ) = (rx, top + 1);
        var (mWoolX, mWoolZ) = Symmetry.Apply(woolX, woolZ, o.MirrorMode, cx, cz);
        var (mSpawnX, mSpawnZ) = Symmetry.Apply(spawnX, spawnZ, o.MirrorMode, cx, cz);
        var objectives = new List<ObjectiveHint>
        {
            new("wool", 0, woolX, woolZ),   new("spawn", 0, spawnX, spawnZ),
            new("wool", 1, mWoolX, mWoolZ), new("spawn", 1, mSpawnX, mSpawnZ),
        };
        return new LaneLayoutResult(layout, objectives);
    }

    private static SketchShape Poly(string id, List<double[]> ring) =>
        new() { Id = id, Type = "polygon", Operation = "add", Vertices = [.. ring] };

    private static List<double[]> RegularPolygon(double cx, double cz, double r, int n)
    {
        var ring = new List<double[]>(n);
        for (var i = 0; i < n; i++)
        {
            var a = 2 * Math.PI * i / n;
            ring.Add([cx + r * Math.Cos(a), cz + r * Math.Sin(a)]);
        }
        return ring;
    }
}
