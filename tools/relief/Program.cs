using PgmStudio.Relief;
using Geom = PgmStudio.Geom;

// relief: an exploration of interior elevation for the sketch tool — how a shape says where its ground
// rises and falls, how that surface is solved, and what it costs to walk on. Every figure is rendered from
// the algorithms in this folder, so a picture cannot claim something the code does not do.
//
//   dotnet run --project tools/relief   →   tools/relief/out/*.png

var outputRoot = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../out");
Directory.CreateDirectory(outputRoot);

var log = new List<string>();
void Say(string line) { Console.WriteLine(line); log.Add(line); }

// ── the shapes the figures are drawn on ───────────────────────────────────────────────────────────

// A room-sized L — one arm high, the other low, and a corner between them.
double[][] Lshape = [[0, 0], [34, 0], [34, 13], [15, 13], [15, 30], [0, 30]];

// A horseshoe: two arms separated by a slot six blocks across. The tips are eight blocks apart in a straight
// line and nearly fifty apart along the land, which is the whole question an interpolator has to answer.
double[][] Horseshoe =
[
    [0, 0], [40, 0], [40, 34], [23, 34], [23, 9], [17, 9], [17, 34], [0, 34],
];

// The board a destroy-the-monument map gives one team: a single polygon with nothing inside it, at the size
// where cutting elevation out of sub-shapes by hand stops being reasonable.
double[][] Board = [[0, 0], [62, 0], [62, 92], [0, 92]];

// The room-sized piece a capture-the-wool map is actually made of.
double[][] Room = [[0, 0], [30, 0], [30, 20], [0, 20]];

// An organic outline of the kind the sketch tool's Bézier edges already produce.
double[][] Blob = Enumerable.Range(0, 48).Select(i =>
{
    var angle = 2 * Math.PI * i / 48;
    var radius = 20 + 7 * Math.Sin(angle * 2.0 + 0.8) + 4 * Math.Sin(angle * 3.0 + 2.1);
    return new[] { 26 + radius * Math.Cos(angle) * 1.15, 26 + radius * Math.Sin(angle) * 0.95 };
}).ToArray();

Footprint FootprintOf(double[][] ring) => Footprint.Of([(ring, false)]);

/// A height field built straight from a function — how the figures show what today's model produces.
HeightField FromFunction(Footprint footprint, Func<double, double, double> height)
{
    var continuous = new double[footprint.Width * footprint.Depth];
    var blocks = new int[continuous.Length];
    foreach (var (x, z) in footprint.Cells())
    {
        var index = footprint.Index(x, z);
        continuous[index] = height(x + 0.5, z + 0.5);
        blocks[index] = Math.Max(1, (int)Math.Round(continuous[index]));
    }
    return new HeightField(footprint, continuous, blocks);
}

// ═══ figure 1 — what a shape can already say about its own height ═════════════════════════════════
{
    var footprint = FootprintOf(Lshape);
    const int Scale = 5;

    var flat = FromFunction(footprint, (_, _) => 4);

    // Today's per-vertex surface: heights at the outline's own corners, interpolated across the ear-clipped
    // triangles of the footprint. High at both ends of the L, low at the inside corner.
    var polygon = Lshape.Select(v => new[] { v[0], v[1] }).ToList();
    double[] anchors = [8, 8, 8, 4, 4, 8];
    var triangles = Geom.Triangulation.EarClip(polygon);
    var tin = FromFunction(footprint, (x, z) => Geom.Triangulation.Interpolate(polygon, anchors, triangles, x, z));

    var tilt = FromFunction(footprint, (x, z) => 4 + 0.13 * x + 0.06 * z);

    // The same intent said as marks: both arms high, the corner between them low.
    List<Mark> marks =
    [
        new PointMark(6, 5, 8, 4), new PointMark(6, 25, 8, 4),
        new PointMark(28, 6, 8, 4), new PointMark(11, 14, 4, 3),
    ];
    var relief = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 4, Marks = marks });

    var panels = new List<(string, Canvas)>
    {
        ("uniform base height", Render.TopDown(flat, Scale, floor: 3, ceiling: 9)),
        ("per-vertex anchors (tin)", Render.TopDown(tin, Scale, floor: 3, ceiling: 9)),
        ("fitted tilt plane", Render.TopDown(tilt, Scale, floor: 3, ceiling: 9)),
        ("marks + solved field", Render.TopDown(relief, Scale, floor: 3, ceiling: 9)),
    };
    Render.DrawMarks(panels[3].Item2, footprint, marks, Scale);
    Png.Write(Path.Combine(outputRoot, "01-today.png"), Render.Row(panels).Upscale(2));

    Say("figure 1 — the height an L-shaped room can state");
    Say(Terrain.Report(tin, "  per-vertex anchors"));
    Say(Terrain.Report(relief, "  marks + solved field"));
    Say("");
}

// ═══ figure 2 — the same statement, three ways of filling the space between ═══════════════════════
{
    var footprint = FootprintOf(Horseshoe);
    const int Scale = 5;

    // One arm's tip is high ground, the other's is low, and the crown between them sits in the middle.
    List<Mark> marks =
    [
        new PointMark(8, 30, 12, 3),
        new PointMark(32, 30, 4, 3),
        new PointMark(20, 4, 8, 3),
    ];

    var panels = new List<(string, Canvas)>();
    foreach (var (name, interpolator) in new[]
    {
        ("straight-line weighting", Interpolator.Idw),
        ("on-land weighting", Interpolator.GeodesicIdw),
        ("smoothest surface", Interpolator.Diffusion),
    })
    {
        var solved = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 4, Marks = marks, Interpolator = interpolator });
        var panel = Render.TopDown(solved, Scale, floor: 3, ceiling: 12);
        Render.DrawMarks(panel, footprint, marks, Scale);
        panels.Add((name, panel));

        // The measurement the picture is making: the two facing cells across the slot are eight blocks apart
        // and forty-eight apart on foot, so the further apart their heights, the more the fill respected the
        // land rather than the map's straight lines.
        var acrossSlot = Math.Abs(solved.At(16, 30) - solved.At(24, 30));
        Say($"  {name}: across the six-block slot the two banks differ by {acrossSlot} block(s)");
    }
    Png.Write(Path.Combine(outputRoot, "02-interpolators.png"), Render.Row(panels).Upscale(2));
    Say("");
}

// ═══ figure 3 — the vocabulary: four kinds of statement on one board ══════════════════════════════
{
    var footprint = FootprintOf(Board);
    const int Scale = 4;

    var vocabulary = new (string Name, List<Mark> Marks)[]
    {
        ("two summits", [new PointMark(16, 20, 10, 4), new PointMark(46, 70, 11, 5), new RimMark(4)]),
        ("a ridge line", [new LineMark([[6, 84], [24, 62], [30, 34], [52, 12]], [5, 10, 9, 12], 2.5), new RimMark(4)]),
        ("a bench", [new AreaMark([[10, 40], [40, 34], [46, 62], [14, 68]], 10), new RimMark(4)]),
        ("a bowl inside a rise", [new PointMark(31, 46, 3, 8),
            new LineMark([[8, 12], [54, 10], [56, 80], [10, 84], [8, 12]], [11, 11, 11, 11, 11], 3), new RimMark(6)]),
    };

    var panels = new List<(string, Canvas)>();
    foreach (var (name, marks) in vocabulary)
    {
        var solved = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 4, Marks = marks });
        var panel = Render.TopDown(solved, Scale, floor: 2, ceiling: 13);
        Render.DrawMarks(panel, footprint, marks, Scale);
        panels.Add((name, panel));
    }
    Png.Write(Path.Combine(outputRoot, "03-vocabulary.png"), Render.Row(panels).Upscale(2));
    Say("figure 3 — the four kinds of statement, on a 62x92 board");
    Say("");
}

// ═══ figure 4 — the finishing knobs: reach, grain, step ═══════════════════════════════════════════
{
    var footprint = FootprintOf(Board);
    const int Scale = 4;
    List<Mark> marks = [new PointMark(16, 20, 11, 4), new PointMark(46, 70, 12, 5), new PointMark(50, 18, 4, 4), new RimMark(5)];

    var variants = new (string Name, ReliefSpec Spec)[]
    {
        ("reach unlimited", new ReliefSpec { Base = 5, Marks = marks, Reach = 0 }),
        ("reach 18", new ReliefSpec { Base = 5, Marks = marks, Reach = 18 }),
        ("+ grain 1.2", new ReliefSpec { Base = 5, Marks = marks, Reach = 18, Grain = 1.2, GrainScale = 11 }),
        ("+ step 2", new ReliefSpec { Base = 5, Marks = marks, Reach = 18, Grain = 1.2, GrainScale = 11, Step = 2 }),
    };

    var panels = new List<(string, Canvas)>();
    HeightField? terraced = null;
    foreach (var (name, spec) in variants)
    {
        var solved = ReliefSolver.Solve(footprint, spec);
        if (spec.Step == 2) terraced = solved;
        panels.Add((name, Render.TopDown(solved, Scale, floor: 2, ceiling: 14)));
        Say(Terrain.Report(solved, $"  {name}"));
    }

    // The coarse step is the one knob that can break the map, so the repair is shown beside it.
    var staired = Terrain.Stair(terraced!);
    panels.Add(("step 2, stairs cut", Render.TopDown(staired, Scale, floor: 2, ceiling: 14)));
    Say(Terrain.Report(staired, "  step 2 with stairs cut"));

    Png.Write(Path.Combine(outputRoot, "04-knobs.png"), Render.Row(panels).Upscale(2));
    Say("");
}

// ═══ figure 5 — how much relief a room-sized shape can take ═══════════════════════════════════════
{
    var footprint = FootprintOf(Room);
    const int Scale = 8;
    var panels = new List<(string, Canvas)>();
    var sections = new List<Canvas>();

    foreach (var amplitude in new[] { 2, 4, 8 })
    {
        List<Mark> marks =
        [
            new PointMark(6, 5, 4 + amplitude, 3),
            new PointMark(25, 15, 4 + amplitude, 3),
            new PointMark(24, 4, 4, 2),
            new RimMark(4),
        ];
        var solved = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 4, Marks = marks, Grain = 0.5, Seed = 5 });
        panels.Add(($"relief {amplitude}", Render.TopDown(solved, Scale, floor: 3, ceiling: 13)));
        sections.Add(Render.Section(solved, 10, Scale, floor: 3, ceiling: 13));
        Say(Terrain.Report(solved, $"  30x20 room, relief {amplitude}"));
    }
    var figure = Render.Stack([Render.Row(panels), Render.Row(sections.Select((section, i) =>
        ($"section through the middle, relief {new[] { 2, 4, 8 }[i]}", section)))]);
    Png.Write(Path.Combine(outputRoot, "05-scale.png"), figure.Upscale(2));
    Say("figure 5 — the same three marks at three amplitudes on a 30x20 room");
    Say("");
}

// ═══ figure 6 — shapes erected out of a solved field ══════════════════════════════════════════════
{
    var footprint = FootprintOf(Blob);
    List<Mark> marks =
    [
        new PointMark(14, 16, 9, 4), new PointMark(42, 38, 8, 5),
        new LineMark([[6, 34], [26, 30], [46, 18]], [4, 5, 4], 2.5),
        new RimMark(4),
    ];
    var solved = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 4, Marks = marks, Grain = 0.9, GrainScale = 9, Seed = 7 });

    double[][] monolith = [[24, 22], [32, 22], [32, 30], [24, 30]];
    double[][] mesa = [[6, 32], [22, 28], [26, 44], [10, 48]];
    double[][] quarry = [[36, 12], [50, 12], [50, 26], [36, 26]];

    var raised = Terrain.Erect(solved, monolith, Terrain.Erection.Raise, 9);
    var withMesa = Terrain.Erect(raised, mesa, Terrain.Erection.Level, 13);
    var full = Terrain.Erect(withMesa, quarry, Terrain.Erection.Sink, 4);

    var panels = new List<(string, Canvas)>
    {
        ("solved relief", Render.Isometric(solved, floor: 0, ceiling: 14)),
        ("+ raised monolith", Render.Isometric(raised, floor: 0, ceiling: 14)),
        ("+ levelled mesa", Render.Isometric(withMesa, floor: 0, ceiling: 14)),
        ("+ sunken quarry", Render.Isometric(full, floor: 0, ceiling: 14)),
    };
    Png.Write(Path.Combine(outputRoot, "06-erected.png"), Render.Row(panels).Upscale(2));

    var flat = Render.Row([("from above", Render.TopDown(full, 5, floor: 0, ceiling: 14)),
                           ("how it steps", Render.StepMap(full, 5))]);
    Png.Write(Path.Combine(outputRoot, "06b-erected-plan.png"), flat.Upscale(2));

    Say("figure 6 — shapes erected out of a solved field");
    Say(Terrain.Report(solved, "  field alone"));
    Say(Terrain.Report(full, "  with three shapes"));
    foreach (var scarp in Terrain.Scarps(full).Take(5))
        Say($"    scarp {scarp.Width} wide, drops {scarp.Drop} — {(scarp.IsCliff ? "cliff" : "step edge")}");
    Say("");
}

// ═══ figure 7 — a route across relief, and a channel down it ══════════════════════════════════════
{
    var footprint = FootprintOf(Board);
    // A ridge across the board with one low saddle in it, a strip of low ground north of the ridge and a
    // basin in the south — the case where the way across and the straight line are not the same line. The
    // ridge is stated after the rim so it survives where the two meet: a rim written last would cut a
    // doorway through both ends of the ridge and hand the router a free way round.
    List<Mark> marks =
    [
        new RimMark(6),
        new LineMark([[0, 34], [22, 30], [42, 38], [62, 34]], [18, 19, 18, 19], 3),
        // A pass, not a notch: the low ground has to be stated as a line that crosses the ridge, because a
        // single low point inside a high ridge leaves its own shoulders to be climbed on the way in.
        new LineMark([[40, 24], [42, 38], [40, 50]], [7, 7, 7], 3),
        new PointMark(24, 8, 6, 6), new PointMark(44, 82, 3, 7),
    ];
    var solved = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 6, Marks = marks, Grain = 0.9, GrainScale = 10, Seed = 3 });
    var terraced = ReliefSolver.Solve(footprint, new ReliefSpec
    { Base = 6, Marks = marks, Grain = 0.9, GrainScale = 10, Seed = 3, Step = 3 });

    var straight = StraightLine((10, 6), (50, 86));
    var routed = Terrain.Route(solved, (10, 6), (50, 86));
    var graded = Terrain.Grade(terraced, straight);

    var filled = Terrain.FillDepressions(solved);
    var source = (28, 42);
    var flow = Terrain.Flow(solved, filled, source);
    var channel = Terrain.Carve(solved, flow, radius: 3, depth: 2);

    Canvas WithRoute(HeightField field, List<(int X, int Z)> route, int colour)
    {
        var panel = Render.TopDown(field, 4, floor: 2, ceiling: 16);
        foreach (var (x, z) in route)
            panel.Disc((x - footprint.MinX) * 4 + 2, (z - footprint.MinZ) * 4 + 2, 2.2, colour, 0.95);
        return panel;
    }

    var waterPanel = Render.TopDown(channel.Bed, 4, floor: 2, ceiling: 16);
    foreach (var (cell, _) in channel.Water)
        waterPanel.Disc((cell.X - footprint.MinX) * 4 + 2, (cell.Z - footprint.MinZ) * 4 + 2, 2.4, 0x3C7FE0, 0.9);

    var panels = new List<(string, Canvas)>
    {
        ("drawn straight", WithRoute(solved, straight, Render.Bad)),
        ("routed for least climb", WithRoute(solved, routed, Render.Accent)),
        ("terraced, drawn line graded", WithRoute(graded, straight, Render.Accent)),
        ("water, found and carved", waterPanel),
    };
    Png.Write(Path.Combine(outputRoot, "07-routes.png"), Render.Row(panels).Upscale(2));

    int Highest(HeightField field, List<(int X, int Z)> route) => route.Max(cell => field.At(cell.X, cell.Z));

    Say("figure 7 — a route across relief, and a channel down it");
    Say($"  drawn straight: {straight.Count} cells, climbs {Climb(solved, straight)} blocks, tops out at y{Highest(solved, straight)}");
    Say($"  routed for least climb: {routed.Count} cells, climbs {Climb(solved, routed)}, tops out at y{Highest(solved, routed)}");
    Say($"  terraced at step 3: the drawn line's worst step is {WorstStep(terraced, straight)}; " +
        $"after grading it is {WorstStep(graded, straight)}");
    var raw = Terrain.Descend(solved, source);
    Say($"  steepest descent on the raw surface stops after {raw.Count} cells (the first pit the grain made)");
    Say($"  on the depression-filled surface the flow runs {flow.Count} cells, " +
        $"y{solved.At(source.Item1, source.Item2)} to y{solved.At(flow[^1].X, flow[^1].Z)}");
    Say($"  the carve holds {channel.Water.Count} water cells at {channel.Water.Values.Distinct().Count()} distinct level(s)");
    Say("");
}

// ═══ figure 8 — a whole map, and whether it is fair ═══════════════════════════════════════════════
{
    const double CentreX = 62, CentreZ = 46;
    double[][] whole = [[0, 0], [124, 0], [124, 92], [0, 92]];
    var footprint = Footprint.Of([(whole, false)]);

    // The statement is made once, on one side, and mirrored — the discipline every placed thing on a
    // competitive map follows.
    List<Mark> authored =
    [
        new PointMark(18, 20, 13, 5),
        new PointMark(30, 70, 10, 4),
        new LineMark([[46, 8], [52, 40], [46, 84]], [8, 7, 8], 3),
        new RimMark(5),
    ];
    var mirrored = authored.SelectMany(mark => new[] { mark, MirrorMark(mark, CentreX, CentreZ) }).ToList();

    ReliefSpec Spec(bool fold) => new()
    {
        Base = 5,
        Marks = mirrored,
        Grain = 0.9,
        GrainScale = 12,
        Seed = 11,
        FoldMode = fold ? "rot_180" : null,
        FoldCentreX = CentreX,
        FoldCentreZ = CentreZ,
    };

    var loose = ReliefSolver.Solve(footprint, Spec(false));
    var folded = ReliefSolver.Solve(footprint, Spec(true));

    var panel = Render.TopDown(folded, 4, floor: 2, ceiling: 16);
    Render.DrawMarks(panel, footprint, mirrored, 4);
    for (var y = 0; y < panel.Height; y += 7)
        panel.Line((int)((CentreX - footprint.MinX) * 4), y, (int)((CentreX - footprint.MinX) * 4), y + 4, Render.Axis, 0.85);

    var difference = new Canvas(footprint.Width, footprint.Depth, Render.Void);
    foreach (var (x, z) in footprint.Cells())
    {
        var (mx, mz) = Terrain.Mirror(x + 0.5, z + 0.5, "rot_180", CentreX, CentreZ);
        int ix = (int)Math.Floor(mx), iz = (int)Math.Floor(mz);
        if (!loose.Has(ix, iz)) continue;
        var delta = Math.Abs(loose.At(x, z) - loose.At(ix, iz));
        difference.Set(x - footprint.MinX, z - footprint.MinZ, delta == 0 ? 0x223146 : delta == 1 ? 0xB08428 : 0xA6383A);
    }

    var figure = Render.Row([("relief stated once, mirrored", panel),
                             ("grain drawn per side: where it differs", difference.Upscale(4))]);
    Png.Write(Path.Combine(outputRoot, "08-fairness.png"), figure.Upscale(2));
    Png.Write(Path.Combine(outputRoot, "08b-map-iso.png"),
        Render.Isometric(folded, tileWidth: 3, tileDepth: 2, blockRise: 3, floor: 0, ceiling: 16).Upscale(2));

    Say("figure 8 — a whole rot_180 map, its relief stated once and mirrored");
    Say(Terrain.Report(folded, "  grain folded through the symmetry", "rot_180", CentreX, CentreZ));
    var (looseWorst, _) = Terrain.SymmetryError(loose, "rot_180", CentreX, CentreZ);
    Say($"  the same map with the grain drawn per side: worst mirrored difference {looseWorst} block(s)");
    var unmirrored = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 5, Marks = authored, Grain = 0.9, GrainScale = 12, Seed = 11 });
    var (unmirroredWorst, _) = Terrain.SymmetryError(unmirrored, "rot_180", CentreX, CentreZ);
    Say($"  the marks left unmirrored altogether: worst mirrored difference {unmirroredWorst} block(s)");
    Say("");
}

// ═══ how long a solve takes, which is what decides whether it can run under a drag ════════════════
{
    Say("solve cost — the question is whether the field can be re-solved while a mark is dragged");
    foreach (var (name, ring) in new[] { ("30x20 room", Room), ("62x92 board", Board),
        ("124x92 whole map", new[] { new[] { 0.0, 0.0 }, [124, 0], [124, 92], [0, 92] }) })
    {
        var footprint = FootprintOf(ring);
        List<Mark> marks =
        [
            new PointMark(footprint.MinX + footprint.Width * 0.25, footprint.MinZ + footprint.Depth * 0.3, 10, 4),
            new PointMark(footprint.MinX + footprint.Width * 0.7, footprint.MinZ + footprint.Depth * 0.7, 12, 4),
            new RimMark(4),
        ];
        var spec = new ReliefSpec { Base = 4, Marks = marks, Grain = 1.0 };

        var clock = System.Diagnostics.Stopwatch.StartNew();
        ReliefSolver.Solve(footprint, spec);
        var cold = clock.Elapsed.TotalMilliseconds;

        clock.Restart();
        for (var repeat = 0; repeat < 5; repeat++) ReliefSolver.Solve(footprint, spec);
        var warm = clock.Elapsed.TotalMilliseconds / 5;

        // What a drag actually asks for: the same relief with one mark moved a little, resumed from the
        // surface already on screen. The comparison is against the full solve of the moved relief, because
        // what matters is not that it is fast but that it lands in the same place.
        var moved = new List<Mark>(marks);
        moved[0] = new PointMark(footprint.MinX + footprint.Width * 0.25 + 3,
                                 footprint.MinZ + footprint.Depth * 0.3 + 2, 10, 4);
        var movedSpec = spec with { Marks = moved };
        var settled = ReliefSolver.Solve(footprint, movedSpec);
        var previous = ReliefSolver.Solve(footprint, spec).Continuous;

        clock.Restart();
        var resumed = ReliefSolver.Solve(footprint, movedSpec, previous, sweeps: 40);
        var resumeCost = clock.Elapsed.TotalMilliseconds;
        var worstDrift = footprint.Cells().Max(cell => Math.Abs(resumed.At(cell.X, cell.Z) - settled.At(cell.X, cell.Z)));
        var driftedCells = footprint.Cells().Count(cell => resumed.At(cell.X, cell.Z) != settled.At(cell.X, cell.Z));

        Say($"  {name}: {footprint.Count} cells, first solve {cold:0} ms, repeated {warm:0} ms; " +
            $"one mark moved and resumed in 40 sweeps {resumeCost:0} ms, " +
            $"{driftedCells} cell(s) differ from the settled answer, worst by {worstDrift}");
    }
    Say("");
}

File.WriteAllText(Path.Combine(outputRoot, "report.txt"), string.Join("\n", log));
Console.WriteLine($"wrote figures to {Path.GetFullPath(outputRoot)}");

// ── helpers the figures use ───────────────────────────────────────────────────────────────────────

static List<(int X, int Z)> StraightLine((int X, int Z) from, (int X, int Z) to)
{
    var cells = new List<(int X, int Z)>();
    var steps = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Z - from.Z));
    for (var i = 0; i <= steps; i++)
        cells.Add((from.X + (to.X - from.X) * i / steps, from.Z + (to.Z - from.Z) * i / steps));
    return cells;
}

static int Climb(HeightField field, List<(int X, int Z)> route)
{
    var total = 0;
    for (var i = 1; i < route.Count; i++)
    {
        var rise = field.At(route[i].X, route[i].Z) - field.At(route[i - 1].X, route[i - 1].Z);
        if (rise > 0) total += rise;
    }
    return total;
}

static int WorstStep(HeightField field, List<(int X, int Z)> route)
{
    var worst = 0;
    for (var i = 1; i < route.Count; i++)
        worst = Math.Max(worst, Math.Abs(field.At(route[i].X, route[i].Z) - field.At(route[i - 1].X, route[i - 1].Z)));
    return worst;
}

static Mark MirrorMark(Mark mark, double centreX, double centreZ) => mark switch
{
    PointMark point => new PointMark(2 * centreX - point.X, 2 * centreZ - point.Z, point.MarkHeight, point.Radius),
    LineMark line => new LineMark(
        line.Points.Select(p => new[] { 2 * centreX - p[0], 2 * centreZ - p[1] }).ToArray(), line.Heights, line.Width),
    AreaMark area => new AreaMark(
        area.Ring.Select(p => new[] { 2 * centreX - p[0], 2 * centreZ - p[1] }).ToArray(), area.MarkHeight),
    _ => mark,
};
