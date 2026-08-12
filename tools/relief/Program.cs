using PgmStudio.Relief;
using Geom = PgmStudio.Geom;

// relief: an exploration of interior elevation for the sketch tool — how a shape says where its ground
// rises and falls, how that surface is solved, and what it costs a player to cross. Every figure is rendered
// from the algorithms in this folder, so a picture cannot claim something the code does not do.
//
//   dotnet run --project tools/relief   →   tools/relief/out/*.png

var outputRoot = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../out");
Directory.CreateDirectory(outputRoot);

var log = new List<string>();
void Say(string line) { Console.WriteLine(line); log.Add(line); }

// Measuring a built world instead of drawing figures: `--measure <regionDir>` for one map, `--corpus <dir>`
// for every map under a directory. What comes back is the same report the solved fields get, which is the
// only way the tiers and the places-and-ledges split get calibrated against maps people shipped.
if (args.Length >= 2 && args[0] == "--measure")
{
    var surface = args.Contains("--terrain") ? Measure.Surface.Natural : Measure.Surface.Built;
    var read = Measure.Read(args[1], surface);
    if (read is not var (built, flooded)) { Console.Error.WriteLine($"no world at {args[1]}"); return 1; }
    var (low, high) = Measure.Body(built);
    Console.WriteLine(Terrain.Report(built, Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(args[1])) ?? args[1])));
    Console.WriteLine($"  body: 95% of columns sit in y {low}..{high} ({high - low} blocks) of a {built.Max - built.Min}-block range");
    if (flooded.Count > 0)
        Console.WriteLine($"  under liquid: {flooded.Count * 100.0 / built.Footprint.Count:0.0}% of columns, " +
            $"waterline y {flooded.Values.Min()}..{flooded.Values.Max()}");
    return 0;
}
if (args.Length >= 2 && args[0] == "--corpus")
{
    Measure.Corpus(args[1], Console.WriteLine,
        args.Contains("--terrain") ? Measure.Surface.Natural : Measure.Surface.Built);
    return 0;
}

// ── the shapes the figures are drawn on, at the sizes maps are actually built at ──────────────────

// A room-sized L — one arm high, the other low, and a corner between them.
double[][] Lshape = [[0, 0], [50, 0], [50, 19], [22, 19], [22, 44], [0, 44]];

// A horseshoe: two arms separated by a slot eight blocks across. The tips are ten blocks apart in a straight
// line and seventy apart along the land, which is the whole question an interpolator has to answer.
double[][] Horseshoe = [[0, 0], [58, 0], [58, 50], [33, 50], [33, 13], [25, 13], [25, 50], [0, 50]];

// The board a destroy-the-monument map gives one team.
double[][] Board = [[0, 0], [90, 0], [90, 135], [0, 135]];

// The room-sized piece a capture-the-wool map is actually made of.
double[][] Room = [[0, 0], [45, 0], [45, 30], [0, 30]];

// An organic outline of the kind the sketch tool's Bézier edges already produce.
double[][] Blob = Enumerable.Range(0, 48).Select(i =>
{
    var angle = 2 * Math.PI * i / 48;
    var radius = 29 + 10 * Math.Sin(angle * 2.0 + 0.8) + 6 * Math.Sin(angle * 3.0 + 2.1);
    return new[] { 38 + radius * Math.Cos(angle) * 1.15, 38 + radius * Math.Sin(angle) * 0.95 };
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
    const int Scale = 4;

    var flat = FromFunction(footprint, (_, _) => 4);

    // Today's per-vertex surface: heights at the outline's own corners, interpolated across the ear-clipped
    // triangles of the footprint. High at both ends of the L, low at the inside corner.
    var polygon = Lshape.Select(v => new[] { v[0], v[1] }).ToList();
    double[] anchors = [9, 9, 9, 4, 4, 9];
    var triangles = Geom.Triangulation.EarClip(polygon);
    var tin = FromFunction(footprint, (x, z) => Geom.Triangulation.Interpolate(polygon, anchors, triangles, x, z));

    var tilt = FromFunction(footprint, (x, z) => 4 + 0.10 * x + 0.05 * z);

    List<Mark> marks =
    [
        new PointMark(9, 8, 9, 6), new PointMark(9, 37, 9, 6),
        new PointMark(41, 9, 9, 6), new PointMark(16, 21, 4, 5),
    ];
    var relief = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 4, Marks = marks });

    var panels = new List<(string, Canvas)>
    {
        ("uniform base height", Render.TopDown(flat, Scale, floor: 3, ceiling: 10)),
        ("per-vertex anchors (tin)", Render.TopDown(tin, Scale, floor: 3, ceiling: 10)),
        ("fitted tilt plane", Render.TopDown(tilt, Scale, floor: 3, ceiling: 10)),
        ("marks + solved field", Render.TopDown(relief, Scale, floor: 3, ceiling: 10)),
    };
    Render.DrawMarks(panels[3].Item2, footprint, marks, Scale);
    Png.Write(Path.Combine(outputRoot, "01-today.png"), Render.Row(panels).Upscale(2));

    Say("figure 1 — the height a 50x44 L-shaped room can state");
    Say(Terrain.Report(tin, "  per-vertex anchors"));
    Say(Terrain.Report(relief, "  marks + solved field"));
    Say("");
}

// ═══ figure 2 — the same statement, three ways of filling the space between ═══════════════════════
{
    var footprint = FootprintOf(Horseshoe);
    const int Scale = 4;

    List<Mark> marks =
    [
        new PointMark(12, 44, 14, 4),
        new PointMark(46, 44, 4, 4),
        new PointMark(29, 6, 9, 4),
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
        var panel = Render.TopDown(solved, Scale, floor: 3, ceiling: 15);
        Render.DrawMarks(panel, footprint, marks, Scale);
        panels.Add((name, panel));

        var acrossSlot = Math.Abs(solved.At(24, 44) - solved.At(34, 44));
        Say($"  {name}: across the eight-block slot the two banks differ by {acrossSlot} block(s)");
    }
    Png.Write(Path.Combine(outputRoot, "02-interpolators.png"), Render.Row(panels).Upscale(2));
    Say("");
}

// ═══ figure 3 — the vocabulary: five kinds of statement on one board ══════════════════════════════
{
    var footprint = FootprintOf(Board);
    const int Scale = 3;

    var vocabulary = new (string Name, List<Mark> Marks)[]
    {
        ("two summits", [new PointMark(24, 30, 11, 6), new PointMark(66, 102, 12, 7), new RimMark(4)]),
        ("a ridge line", [new LineMark([[9, 122], [35, 90], [44, 50], [76, 18]], [5, 11, 10, 13], 3.5), new RimMark(4)]),
        ("a bench", [new AreaMark([[15, 58], [58, 50], [67, 90], [20, 100]], 11), new RimMark(4)]),
        ("a bowl inside a rise", [new PointMark(45, 67, 3, 12),
            new LineMark([[12, 18], [78, 15], [81, 117], [15, 122], [12, 18]], [12, 12, 12, 12, 12], 4), new RimMark(6)]),
        ("a scarp", [new RimMark(5),
            new ScarpMark([[10, 8], [40, 46], [44, 92], [22, 128]], High: 15, Low: 5, FaceWidth: 3, BandWidth: 14)]),
    };

    var panels = new List<(string, Canvas)>();
    foreach (var (name, marks) in vocabulary)
    {
        var solved = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 4, Marks = marks });
        var panel = Render.TopDown(solved, Scale, floor: 2, ceiling: 16);
        Render.DrawMarks(panel, footprint, marks, Scale);
        panels.Add((name, panel));
    }
    Png.Write(Path.Combine(outputRoot, "03-vocabulary.png"), Render.Row(panels).Upscale(2));
    Say("figure 3 — the five kinds of statement, on a 90x135 board");
    Say("");
}

// ═══ figure 4 — the finishing knobs: reach, grain, step ═══════════════════════════════════════════
{
    var footprint = FootprintOf(Board);
    const int Scale = 3;
    List<Mark> marks = [new PointMark(24, 30, 12, 6), new PointMark(66, 102, 13, 7), new PointMark(73, 26, 4, 6), new RimMark(5)];

    var variants = new (string Name, ReliefSpec Spec)[]
    {
        ("reach unlimited", new ReliefSpec { Base = 5, Marks = marks, Reach = 0 }),
        ("reach 26", new ReliefSpec { Base = 5, Marks = marks, Reach = 26 }),
        ("+ grain 1.4", new ReliefSpec { Base = 5, Marks = marks, Reach = 26, Grain = 1.4, GrainScale = 15 }),
        ("+ step 2", new ReliefSpec { Base = 5, Marks = marks, Reach = 26, Grain = 1.4, GrainScale = 15, Step = 2 }),
    };

    var panels = new List<(string, Canvas)>();
    HeightField? terraced = null;
    foreach (var (name, spec) in variants)
    {
        var solved = ReliefSolver.Solve(footprint, spec);
        if (spec.Step == 2) terraced = solved;
        panels.Add((name, Render.TopDown(solved, Scale, floor: 2, ceiling: 15)));
        Say(Terrain.Report(solved, $"  {name}"));
    }

    var staired = Terrain.Stair(terraced!);
    panels.Add(("step 2, stairs cut", Render.TopDown(staired, Scale, floor: 2, ceiling: 15)));
    Say(Terrain.Report(staired, "  step 2 with stairs cut"));

    Png.Write(Path.Combine(outputRoot, "04-knobs.png"), Render.Row(panels).Upscale(2));
    Say("");
}

// ═══ figure 5 — what relief costs a player, tier by tier ══════════════════════════════════════════
{
    var footprint = FootprintOf(Room);
    const int Scale = 6;
    var panels = new List<(string, Canvas)>();
    var reach = new List<(string, Canvas)>();
    int[] amplitudes = [2, 4, 8, 14];

    foreach (var amplitude in amplitudes)
    {
        List<Mark> marks =
        [
            new PointMark(9, 8, 4 + amplitude, 5),
            new PointMark(37, 22, 4 + amplitude, 5),
            new PointMark(36, 6, 4, 4),
            new RimMark(4),
        ];
        var solved = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 4, Marks = marks, Grain = 0.7, Seed = 5 });
        panels.Add(($"relief {amplitude}", Render.TopDown(solved, Scale, floor: 3, ceiling: 19)));
        reach.Add(($"on foot, relief {amplitude}", Render.ReachMap(solved, Terrain.Passage.Walk, Scale)));
        Say(Terrain.Report(solved, $"  45x30 room, relief {amplitude}"));
    }
    Png.Write(Path.Combine(outputRoot, "05-scale.png"),
        Render.Stack([Render.Row(panels), Render.Row(reach)]).Upscale(2));
    Say("figure 5 — the same three marks at four amplitudes on a 45x30 room");
    Say("");
}

// ═══ figure 6 — shapes erected out of a solved field ══════════════════════════════════════════════
{
    var footprint = FootprintOf(Blob);
    List<Mark> marks =
    [
        new PointMark(20, 23, 11, 6), new PointMark(60, 55, 10, 7),
        new LineMark([[9, 49], [38, 43], [66, 26]], [4, 5, 4], 3.5),
        new RimMark(4),
    ];
    var solved = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 4, Marks = marks, Grain = 1.0, GrainScale = 13, Seed = 7 });

    double[][] monolith = [[36, 33], [44, 33], [44, 41], [36, 41]];
    double[][] mesa = [[9, 46], [32, 40], [38, 63], [14, 69]];
    double[][] quarry = [[52, 17], [72, 17], [72, 37], [52, 37]];

    var raised = Terrain.Erect(solved, monolith, Terrain.Erection.Raise, 10);
    var withMesa = Terrain.Erect(raised, mesa, Terrain.Erection.Level, 15);
    var full = Terrain.Erect(withMesa, quarry, Terrain.Erection.Sink, 5);

    var panels = new List<(string, Canvas)>
    {
        ("solved relief", Render.Isometric(solved, floor: 0, ceiling: 16)),
        ("+ raised monolith", Render.Isometric(raised, floor: 0, ceiling: 16)),
        ("+ levelled mesa", Render.Isometric(withMesa, floor: 0, ceiling: 16)),
        ("+ sunken quarry", Render.Isometric(full, floor: 0, ceiling: 16)),
    };
    Png.Write(Path.Combine(outputRoot, "06-erected.png"), Render.Row(panels).Upscale(2));

    var flat = Render.Row([("from above", Render.TopDown(full, 4, floor: 0, ceiling: 16)),
                           ("what each cell costs", Render.StepMap(full, 4)),
                           ("reachable on foot", Render.ReachMap(full, Terrain.Passage.Walk, 4)),
                           ("reachable with a block", Render.ReachMap(full, Terrain.Passage.Scramble, 4))]);
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
    List<Mark> marks =
    [
        new RimMark(6),
        new LineMark([[0, 50], [32, 44], [61, 56], [90, 50]], [20, 21, 20, 21], 4),
        new LineMark([[58, 35], [61, 56], [58, 73]], [7, 7, 7], 4),
        new PointMark(35, 12, 6, 8), new PointMark(64, 120, 3, 10),
    ];
    var solved = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 6, Marks = marks, Grain = 1.0, GrainScale = 14, Seed = 3 });
    var terraced = ReliefSolver.Solve(footprint, new ReliefSpec
    { Base = 6, Marks = marks, Grain = 1.0, GrainScale = 14, Seed = 3, Step = 3 });

    var straight = StraightLine((14, 9), (73, 126));
    var routed = Terrain.Route(solved, (14, 9), (73, 126));
    var graded = Terrain.Grade(terraced, straight);

    var filled = Terrain.FillDepressions(solved);
    var source = (40, 61);
    var flow = Terrain.Flow(solved, filled, source);
    var channel = Terrain.Carve(solved, flow, radius: 3.5, depth: 2);

    Canvas WithRoute(HeightField field, List<(int X, int Z)> route, int colour)
    {
        var panel = Render.TopDown(field, 3, floor: 2, ceiling: 22);
        foreach (var (x, z) in route)
            panel.Disc((x - footprint.MinX) * 3 + 1.5, (z - footprint.MinZ) * 3 + 1.5, 2.0, colour, 0.95);
        return panel;
    }

    var waterPanel = Render.TopDown(channel.Bed, 3, floor: 2, ceiling: 22);
    foreach (var (cell, _) in channel.Water)
        waterPanel.Disc((cell.X - footprint.MinX) * 3 + 1.5, (cell.Z - footprint.MinZ) * 3 + 1.5, 2.0, 0x3C7FE0, 0.9);

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

// ═══ figure 8 — the scarp: one number decides whether a line can be crossed ═══════════════════════
{
    var footprint = FootprintOf(Board);
    const int Scale = 3;
    double[][] bank = [[30, 0], [42, 40], [38, 95], [48, 135]];

    var panels = new List<(string, Canvas)>();
    var reach = new List<(string, Canvas)>();
    foreach (var (name, face) in new[] { ("drop 10 over 10", 10.0), ("over 5", 5.0), ("over 2", 2.0) })
    {
        List<Mark> marks =
        [
            new RimMark(6),
            new ScarpMark(bank, High: 16, Low: 6, FaceWidth: face, BandWidth: 16),
        ];
        var solved = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 6, Marks = marks, Grain = 0.8, GrainScale = 13, Seed = 4 });
        var grade = 10.0 / face;

        bool InCorridor((int X, int Z) cell)
            => LineMark.NearestOnPolyline(cell.X + 0.5, cell.Z + 0.5, bank).Distance <= face / 2 + 3;

        var uphill = Terrain.Fords(solved, InCorridor, Terrain.Passage.Walk, eastbound: false);
        var uphillBlock = Terrain.Fords(solved, InCorridor, Terrain.Passage.Scramble, eastbound: false);
        var downhill = Terrain.Fords(solved, InCorridor, Terrain.Passage.Walk, eastbound: true);

        panels.Add(($"{name} — grade {grade:0.#}/1", Render.TopDown(solved, Scale, floor: 4, ceiling: 18)));
        reach.Add(($"reachable on foot", Render.ReachMap(solved, Terrain.Passage.Walk, Scale)));
        var (steps, direct, detour) = Terrain.CrossingCost(solved, (12, 68), (78, 68), Terrain.Passage.Walk);
        Say($"  {name} (rise {grade:0.#} per block of run): " +
            $"crossable straight on foot along {Rows(uphill)} of 135 rows, " +
            $"with a block {Rows(uphillBlock)}, descendable {Rows(downhill)}; " +
            (double.IsInfinity(detour)
                ? "no walkable way across at all"
                : $"the walk from one side to the other is {steps} cells against {direct} direct, a {detour:0.0}x detour"));
    }
    Png.Write(Path.Combine(outputRoot, "08-scarp.png"),
        Render.Stack([Render.Row(panels), Render.Row(reach)]).Upscale(2));
    Say("figure 8 — a scarp at three grades: the same 10-block drop over 10, 5 and 2 blocks of run");
    Say("");
}

// ═══ figure 9 — a whole map, designed ═════════════════════════════════════════════════════════════
{
    const double CentreX = 96, CentreZ = 64;
    double[][] whole = [[0, 0], [192, 0], [192, 128], [0, 128]];
    var footprint = Footprint.Of([(whole, false)]);

    // The river is drawn once and is its own mirror image under the half-turn, so it belongs to neither
    // team: every point maps onto another point of the same line.
    double[][] river = [[86, 0], [100, 32], [96, 64], [92, 96], [106, 128]];

    // Everything else is stated for one team and fanned. The spawn hill sits hard into the corner with its
    // centre off the board, so the ground rises to the corner and stops — there is no back slope behind the
    // spawn to fall down, and no strip of map nobody will ever stand on.
    // Everything else is stated for one team and fanned. The spawn hill sits hard into the corner with its
    // centre off the board, so the ground rises to the corner and stops — there is no back slope behind the
    // spawn to fall down, and no strip of map nobody will ever stand on.
    //
    // The banks are the map's flow control. A scarp's high side is the left of the way it is drawn, so a
    // west-bank cliff runs with the river and an east-bank cliff runs against it; each pair is broken where
    // a crossing is wanted, and the fan puts the same breaks on the other team's half.
    List<Mark> authored =
    [
        new PointMark(-6, -6, 19, 26),                                        // the spawn hill, cornered
        new LineMark([[26, 34], [54, 26], [72, 38]], [15, 14, 13], 7),        // the shoulder it looks over
        new AreaMark([[16, 44], [40, 40], [44, 58], [20, 62]], 13),           // the bench a wool room stands on
        new PointMark(58, 66, 11, 15),                                        // the swale the approach runs down
        new PointMark(20, 100, 14, 16),                                       // the flank rise behind the front
        new PointMark(46, 118, 10, 12),                                       // the hollow behind that
        // The cliffed banks. A narrow held band, at the height of the ground already there rather than above
        // it, so the mark reads as the edge of the high ground and not as a wall standing on top of it. Each
        // is broken twice, and the breaks are where the map can be crossed.
        new ScarpMark([[79, -2], [83, 8], [86, 16]], High: 15, Low: 6, FaceWidth: 2, BandWidth: 5),
        new ScarpMark([[100, 16], [97, 8], [93, -2]], High: 15, Low: 6, FaceWidth: 2, BandWidth: 5),
        new ScarpMark([[92, 30], [91, 44], [90, 58]], High: 15, Low: 6, FaceWidth: 2, BandWidth: 5),
        new ScarpMark([[104, 58], [105, 44], [106, 30]], High: 15, Low: 6, FaceWidth: 2, BandWidth: 5),
    ];
    var mirrored = authored.SelectMany(mark => new[] { mark, MirrorMark(mark, CentreX, CentreZ) }).ToList();

    // The river itself is stated last so it cuts through everything above it, including the scarps' low bands.
    var marks = new List<Mark>(mirrored) { new LineMark(river, [4, 4, 4, 4, 4], 6) };

    // The same statement at two amplitudes. Every mark's departure from the base is scaled, and the banks
    // steepen with it — so the second board is the first one's design read louder, not a different design.
    List<Mark> Lifted(double lift) => marks.Select(mark => mark switch
    {
        PointMark point => new PointMark(point.X, point.Z, 8 + (point.MarkHeight - 8) * lift, point.Radius),
        LineMark line => new LineMark(line.Points, line.Heights.Select(h => 8 + (h - 8) * lift).ToArray(), line.Width),
        AreaMark area => new AreaMark(area.Ring, 8 + (area.MarkHeight - 8) * lift),
        ScarpMark scarp => new ScarpMark(scarp.Points, 8 + (scarp.High - 8) * lift, 8 + (scarp.Low - 8) * lift,
                                         scarp.FaceWidth, scarp.BandWidth),
        _ => mark,
    }).ToList();

    ReliefSpec SpecAt(double lift) => new()
    {
        Base = 8,
        Marks = Lifted(lift),
        Grain = 1.3 * lift,
        GrainScale = 17,
        Seed = 21,
        FoldMode = "rot_180",
        FoldCentreX = CentreX,
        FoldCentreZ = CentreZ,
    };
    var spec = SpecAt(1.0);

    var clock = System.Diagnostics.Stopwatch.StartNew();
    var solved = ReliefSolver.Solve(footprint, spec);
    var solveCost = clock.Elapsed.TotalMilliseconds;

    // The water is carved along the line that was drawn, at one level, and the surface is folded again
    // afterwards: a carve walks the route from one end and a half-turn reverses that walk, so a pass that
    // runs after the solve has to be folded again or it hands one team a bank the other does not have.
    var riverRoute = Trace(river);
    var channel = Terrain.Carve(solved, riverRoute, radius: 4, depth: 2, level: 5);
    var finished = Terrain.Fold(channel.Bed, "rot_180", CentreX, CentreZ);

    bool InRiverCorridor((int X, int Z) cell)
        => LineMark.NearestOnPolyline(cell.X + 0.5, cell.Z + 0.5, river).Distance <= 8;

    var eastward = Terrain.Fords(finished, InRiverCorridor, Terrain.Passage.Walk);
    var westward = Terrain.Fords(finished, InRiverCorridor, Terrain.Passage.Walk, eastbound: false);
    var eastwardBlock = Terrain.Fords(finished, InRiverCorridor, Terrain.Passage.Scramble);

    var plan = Render.TopDown(finished, 4, floor: 2, ceiling: 20);
    foreach (var (cell, _) in channel.Water)
        plan.Disc((cell.X - footprint.MinX) * 4 + 2, (cell.Z - footprint.MinZ) * 4 + 2, 2.4, 0x3C7FE0, 0.92);
    for (var y = 0; y < plan.Height; y += 9)
        plan.Line((int)((CentreX - footprint.MinX) * 4), y, (int)((CentreX - footprint.MinX) * 4), y + 4, Render.Axis, 0.5);
    // Where each team's spawn hill crowns, so the two corners can be read as the places they are.
    foreach (var (sx, sz) in new[] { (10, 10), (182, 118) }) plan.Ring((sx - footprint.MinX) * 4, (sz - footprint.MinZ) * 4, 9, 1.8, Render.Ink);

    Png.Write(Path.Combine(outputRoot, "09-map-plan.png"),
        Render.Row([("the board", plan),
                    ("what each cell costs", Render.StepMap(finished, 4)),
                    ("reachable on foot", Render.ReachMap(finished, Terrain.Passage.Walk, 4))]).Upscale(2));

    var iso = Render.Isometric(finished, tileWidth: 4, tileDepth: 2, blockRise: 3, floor: 0, ceiling: 20,
        tintOf: cell => channel.Water.ContainsKey(cell) ? 0x2E6FC4 : null);
    Png.Write(Path.Combine(outputRoot, "09b-map-iso.png"), Render.Row([("the same board in blocks", iso)]).Upscale(2));

    // The corpus says this board is at the flat end of what gets shipped, so here is the same design read
    // louder: the marks lifted, the banks steepened with them, nothing moved.
    var loudSpec = SpecAt(2.1);
    var loudSolved = ReliefSolver.Solve(footprint, loudSpec);
    var loudChannel = Terrain.Carve(loudSolved, riverRoute, radius: 4, depth: 3, level: 6);
    var loud = Terrain.Fold(loudChannel.Bed, "rot_180", CentreX, CentreZ);

    var loudPlan = Render.TopDown(loud, 4, floor: 2, ceiling: 34);
    foreach (var (cell, _) in loudChannel.Water)
        loudPlan.Disc((cell.X - footprint.MinX) * 4 + 2, (cell.Z - footprint.MinZ) * 4 + 2, 2.4, 0x3C7FE0, 0.92);
    Png.Write(Path.Combine(outputRoot, "09c-map-louder.png"),
        Render.Row([("the same design, lifted to the corpus median", loudPlan),
                    ("what each cell costs", Render.StepMap(loud, 4)),
                    ("reachable on foot", Render.ReachMap(loud, Terrain.Passage.Walk, 4))]).Upscale(2));
    Png.Write(Path.Combine(outputRoot, "09d-map-louder-iso.png"),
        Render.Row([("lifted, in blocks", Render.Isometric(loud, tileWidth: 4, tileDepth: 2, blockRise: 3,
            floor: 0, ceiling: 34, tintOf: cell => loudChannel.Water.ContainsKey(cell) ? 0x2E6FC4 : null))]).Upscale(2));

    // The same board with the fold switched off, so the cost of leaving it off is a number rather than an
    // assertion: the marks are still mirrored, and only the grain and the quantized surface go unfolded.
    var unfolded = ReliefSolver.Solve(footprint, spec with { FoldMode = null });
    var (unfoldedWorst, _) = Terrain.SymmetryError(unfolded, "rot_180", CentreX, CentreZ);
    var unfoldedCells = footprint.Cells().Count(cell =>
    {
        var (mx, mz) = Terrain.Mirror(cell.X + 0.5, cell.Z + 0.5, "rot_180", CentreX, CentreZ);
        int ix = (int)Math.Floor(mx), iz = (int)Math.Floor(mz);
        return unfolded.Has(ix, iz) && unfolded.At(cell.X, cell.Z) != unfolded.At(ix, iz);
    });

    Say("figure 9 — a whole 192x128 rot_180 map, designed");
    Say($"  solved in {solveCost:0} ms");
    Say(Terrain.Report(finished, "  the board", "rot_180", CentreX, CentreZ));
    Say($"  the same marks with the fold switched off: worst mirrored difference {unfoldedWorst} block(s), " +
        $"over {unfoldedCells} cells ({unfoldedCells * 100.0 / footprint.Count:0.0}% of the board)");
    var (bodyLow, bodyHigh) = Measure.Body(finished);
    Say($"  body: 95% of columns in a {bodyHigh - bodyLow}-block band");
    Say($"  spawn hill crowns at y{finished.At(6, 6)}; the river runs at y{finished.At(96, 64)}");
    Say($"  pushing straight across eastward on foot: {eastward.Count} ford(s) — " +
        $"{string.Join(", ", eastward.Select(ford => $"z{ford.From}-{ford.To}"))}");
    Say($"  westward: {westward.Count} ford(s) — " +
        $"{string.Join(", ", westward.Select(ford => $"z{ford.From}-{ford.To}"))}");
    Say($"  eastward with a block to place: {eastwardBlock.Count} ford(s) — " +
        $"{string.Join(", ", eastwardBlock.Select(ford => $"z{ford.From}-{ford.To}"))}");
    foreach (var (label, from, to) in new[]
    {
        ("spawn to spawn", (10, 10), (182, 118)),
        ("across the northern reach", (70, 18), (122, 18)),
        ("across the middle", (72, 64), (120, 64)),
    })
    {
        var (steps, direct, detour) = Terrain.CrossingCost(finished, from, to, Terrain.Passage.Walk);
        Say(double.IsInfinity(detour)
            ? $"  {label}: no walkable route at all"
            : $"  {label}: {steps} cells walked against {direct} direct — a detour of {detour:0.0}x");
    }
    foreach (var scarp in Terrain.Scarps(finished).Take(4))
        Say($"    scarp {scarp.Width} wide, drops {scarp.Drop} — {(scarp.IsCliff ? "cliff" : "step edge")}");

    var (loudLow, loudHigh) = Measure.Body(loud);
    Say(Terrain.Report(loud, "  the same design lifted", "rot_180", CentreX, CentreZ));
    Say($"  lifted body: 95% of columns in a {loudHigh - loudLow}-block band");
    var loudEast = Terrain.Fords(loud, InRiverCorridor, Terrain.Passage.Walk);
    var loudEastBlock = Terrain.Fords(loud, InRiverCorridor, Terrain.Passage.Scramble);
    Say($"  lifted, pushing straight across eastward: {loudEast.Count} ford(s) on foot, {loudEastBlock.Count} with a block");
    Say("");
}

// ═══ figure 10 — the island is the unit, and a shape can opt out of it ════════════════════════════
{
    // Three abutting pieces of one landmass — what a plan's equal-level pieces fuse into — and a walled
    // compound standing on the middle one.
    double[][] north = [[0, 0], [90, 0], [90, 50], [0, 50]];
    double[][] middle = [[0, 50], [90, 50], [90, 95], [0, 95]];
    double[][] south = [[0, 95], [90, 95], [90, 135], [0, 135]];
    double[][] city = [[20, 56], [56, 56], [56, 88], [20, 88]];
    const int Scale = 3;

    List<Mark> marks =
    [
        new RimMark(5),
        new PointMark(20, 22, 15, 9),
        new LineMark([[8, 118], [40, 100], [70, 66], [82, 24]], [7, 12, 13, 14], 4),
        new PointMark(74, 120, 6, 10),
    ];
    var spec = new ReliefSpec { Base = 6, Marks = marks, Grain = 1.1, GrainScale = 15, Seed = 9 };

    // Solved per shape: each piece is its own domain, so a mark outside a piece says nothing to it and the
    // two sides of a seam settle independently.
    var whole = Footprint.Of(new[] { north, middle, south }.Select(ring => (ring, false)));
    var perShape = new int[whole.Width * whole.Depth];
    foreach (var ring in new[] { north, middle, south })
    {
        var piece = Footprint.Of([(ring, false)]);
        var solved = ReliefSolver.Solve(piece, spec);
        foreach (var (x, z) in piece.Cells())
            if (whole.Inside(x, z)) perShape[whole.Index(x, z)] = solved.At(x, z);
    }
    var seamed = new HeightField(whole, perShape.Select(height => (double)height).ToArray(), perShape);

    var fused = new Island([new ReliefShape(north), new ReliefShape(middle), new ReliefShape(south)], spec).Build();
    var held = new Island([new ReliefShape(north), new ReliefShape(middle), new ReliefShape(south),
        new ReliefShape(city, Participation.Hold, 13)], spec).Build();
    var excluded = new Island([new ReliefShape(north), new ReliefShape(middle), new ReliefShape(south),
        new ReliefShape(city, Participation.Exclude, 13)], spec).Build();

    Canvas WithCity(Canvas panel, bool outline)
    {
        if (!outline) return panel;
        for (var i = 0; i < city.Length; i++)
        {
            var next = city[(i + 1) % city.Length];
            panel.Line((int)((city[i][0] - whole.MinX) * Scale), (int)((city[i][1] - whole.MinZ) * Scale),
                       (int)((next[0] - whole.MinX) * Scale), (int)((next[1] - whole.MinZ) * Scale), Render.Ink, 0.9);
        }
        return panel;
    }

    var panels = new List<(string, Canvas)>
    {
        ("solved per shape", Render.TopDown(seamed, Scale, floor: 3, ceiling: 17)),
        ("solved per island", Render.TopDown(fused, Scale, floor: 3, ceiling: 17)),
        ("a held compound", WithCity(Render.TopDown(held, Scale, floor: 3, ceiling: 17), true)),
        ("an excluded compound", WithCity(Render.TopDown(excluded, Scale, floor: 3, ceiling: 17), true)),
    };
    Png.Write(Path.Combine(outputRoot, "10-island.png"), Render.Row(panels).Upscale(2));

    var blocks = Render.Row([("held — the land arrives at it", Render.Isometric(held, floor: 0, ceiling: 17)),
                             ("excluded — the land ignores it", Render.Isometric(excluded, floor: 0, ceiling: 17))]);
    Png.Write(Path.Combine(outputRoot, "10b-island-iso.png"), blocks.Upscale(2));

    // What the seam costs, measured at the two piece boundaries rather than described.
    int WorstAcross(HeightField field, int z)
        => Enumerable.Range(whole.MinX, whole.Width)
                     .Where(x => field.Has(x, z) && field.Has(x, z + 1))
                     .Select(x => Math.Abs(field.At(x, z + 1) - field.At(x, z)))
                     .DefaultIfEmpty(0).Max();

    Say("figure 10 — the island is the unit a relief is solved over");
    Say($"  solved per shape: worst step across the seams at z50 and z95 is {WorstAcross(seamed, 49)} and {WorstAcross(seamed, 94)} blocks");
    Say($"  solved per island: {WorstAcross(fused, 49)} and {WorstAcross(fused, 94)}");
    Say(Terrain.Report(fused, "  fused island"));
    Say(Terrain.Report(held, "  with the compound held at 13"));
    Say(Terrain.Report(excluded, "  with the compound excluded at 13"));
    var heldEdge = EdgeStep(held, city);
    var excludedEdge = EdgeStep(excluded, city);
    Say($"  at the compound's wall: held meets the land at a worst step of {heldEdge}, excluded at {excludedEdge}");
    Say("");
}

// ═══ figure 11 — a landform's plan is whatever was drawn ══════════════════════════════════════════
{
    var footprint = FootprintOf(Board);
    const int Scale = 3;

    // The same amount of ground lifted by the same height, four outlines. Nothing else differs.
    double[][] Spur = [[30, 18], [52, 26], [58, 52], [50, 78], [56, 104], [44, 118], [36, 96], [42, 66], [34, 42]];
    double[][] Crescent = [[16, 30], [42, 20], [66, 34], [72, 60], [62, 56], [56, 38], [38, 32], [24, 44], [22, 78],
                           [34, 100], [26, 104], [12, 76]];
    double[][] Saddle = [[18, 34], [40, 26], [44, 56], [66, 48], [76, 78], [56, 92], [50, 66], [30, 74], [20, 60]];

    var panels = new List<(string, Canvas)>();
    foreach (var (name, ring, rough) in new[]
    {
        ("a point mark, radius 22", (double[][])[], 0.0),
        ("a drawn spur", Spur, 0.0),
        ("a crescent ridge", Crescent, 0.0),
        ("a saddleback, skirt roughened", Saddle, 5.0),
    })
    {
        var spec = ring.Length == 0
            ? new ReliefSpec { Base = 6, Marks = [new RimMark(6), new PointMark(45, 66, 20, 22)], Grain = 0.8, Seed = 4 }
            : new ReliefSpec
            {
                Base = 6,
                Marks = [new RimMark(6)],
                Pushes = [new PushMark(ring, Amount: 14, Falloff: 16, Roughness: rough, Seed: 4)],
                Grain = 0.8,
                Seed = 4,
            };
        var solved = ReliefSolver.Solve(footprint, spec);
        var panel = Render.TopDown(solved, Scale, floor: 4, ceiling: 22);
        if (ring.Length > 0)
            for (var i = 0; i < ring.Length; i++)
            {
                var next = ring[(i + 1) % ring.Length];
                panel.Line((int)((ring[i][0] - footprint.MinX) * Scale), (int)((ring[i][1] - footprint.MinZ) * Scale),
                           (int)((next[0] - footprint.MinX) * Scale), (int)((next[1] - footprint.MinZ) * Scale), Render.Ink, 0.85);
            }
        panels.Add((name, panel));
    }
    Png.Write(Path.Combine(outputRoot, "11-push.png"), Render.Row(panels).Upscale(2));
    Say("figure 11 — the same lift, four outlines: a landform's plan is whatever ring was drawn");
    Say("");
}

// ═══ figure 11b — a push's top is a field, not a number ═══════════════════════════════════════════
{
    var footprint = FootprintOf(Board);
    const int Scale = 3;

    // Two outlines that differ only in proportion: a fat blob and a long spur. The same crown is applied to
    // both, and what it crowns toward is decided by the shape rather than by anything authored.
    double[][] blob = Enumerable.Range(0, 20).Select(i =>
    {
        var angle = 2 * Math.PI * i / 20;
        var radius = 20 + 4 * Math.Sin(angle * 3 + 1.1);
        return new[] { 45 + radius * Math.Cos(angle), 44 + radius * Math.Sin(angle) };
    }).ToArray();
    double[][] spur = [[30, 14], [52, 24], [56, 52], [48, 82], [54, 112], [42, 124], [34, 98], [40, 62], [32, 36]];

    var panels = new List<(string, Canvas)>();
    void Panel(string name, double[][] ring, double amount, double[]? amounts, double crown, int ceiling)
    {
        var spec = new ReliefSpec
        {
            Base = 6,
            Marks = [new RimMark(6)],
            Pushes = [new PushMark(ring, amount, Falloff: 16, Roughness: 4, Seed: 4, Amounts: amounts, Crown: crown)],
            Grain = 0.7,
            Seed = 4,
        };
        var solved = ReliefSolver.Solve(footprint, spec);
        var panel = Render.TopDown(solved, Scale, floor: 4, ceiling: ceiling);
        for (var i = 0; i < ring.Length; i++)
        {
            var next = ring[(i + 1) % ring.Length];
            panel.Line((int)((ring[i][0] - footprint.MinX) * Scale), (int)((ring[i][1] - footprint.MinZ) * Scale),
                       (int)((next[0] - footprint.MinX) * Scale), (int)((next[1] - footprint.MinZ) * Scale), Render.Ink, 0.8);
        }
        panels.Add((name, panel));
        Say($"  {name}: y {solved.Min}..{solved.Max}");
    }

    Panel("flat top (crown 0)", blob, 12, null, 0, 26);
    Panel("crowned — a point", blob, 12, null, 10, 26);
    Panel("crowned — a line", spur, 12, null, 10, 26);
    Panel("dished (crown -9)", blob, 12, null, -9, 26);
    // One amount per drawn vertex, so the crest falls along the line the author drew rather than holding level.
    Panel("a lift per vertex", spur, 12,
        [16, 15, 13, 11, 8, 6, 7, 10, 14], 8, 26);

    Png.Write(Path.Combine(outputRoot, "11b-crown.png"), Render.Row(panels).Upscale(2));
    Png.Write(Path.Combine(outputRoot, "11c-crown-iso.png"), Render.Row(
    [
        ("crowned blob", Render.Isometric(ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 6, Marks = [new RimMark(6)], Grain = 0.7, Seed = 4,
            Pushes = [new PushMark(blob, 12, 16, 4, 4, Crown: 10)],
        }), tileWidth: 3, tileDepth: 2, blockRise: 3, floor: 0, ceiling: 26)),
        ("crowned spur, lift per vertex", Render.Isometric(ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 6, Marks = [new RimMark(6)], Grain = 0.7, Seed = 4,
            Pushes = [new PushMark(spur, 12, 16, 4, 4, [16, 15, 13, 11, 8, 6, 7, 10, 14], 8)],
        }), tileWidth: 3, tileDepth: 2, blockRise: 3, floor: 0, ceiling: 26)),
    ]).Upscale(2));
    Say("figure 11b — a flat top, a dome, a ridge, a hollow and a falling crest: one ring, four numbers");
    Say("");
}

// ═══ figure 12 — the character of a mined valley, not a copy of one ═══════════════════════════════
{
    // A long board, a river that widens into a lake, spurs reaching down to it between side valleys, and a
    // bench cut into one flank where a mine would sit. The shapes are drawn, not radial.
    double[][] valley = [[0, 0], [140, 0], [140, 260], [0, 260]];
    var footprint = FootprintOf(valley);

    double[][] river = [[64, 0], [72, 44], [66, 92], [70, 130], [62, 170], [74, 216], [66, 260]];
    double[][] lake = [[44, 104], [92, 98], [104, 132], [96, 168], [56, 174], [38, 140]];

    double[][] westSpur = [[10, 22], [40, 34], [46, 60], [30, 76], [14, 62]];
    double[][] eastSpur = [[96, 30], [130, 20], [134, 58], [110, 74], [92, 56]];
    double[][] westRidge = [[4, 118], [30, 108], [40, 148], [34, 200], [12, 214], [2, 168]];
    double[][] eastRidge = [[104, 186], [136, 176], [138, 236], [112, 246], [100, 216]];
    double[][] bench = [[86, 74], [118, 68], [124, 96], [92, 102]];

    var spec = new ReliefSpec
    {
        Base = 14,
        Marks =
        [
            new RimMark(20, 2),
            new LineMark(river, [6, 5, 4, 4, 4, 5, 6], 5),
            new AreaMark(lake, 3),
            new AreaMark(bench, 26),
            new ScarpMark([[86, 66], [122, 60]], High: 27, Low: 17, FaceWidth: 2, BandWidth: 5),
        ],
        Pushes =
        [
            new PushMark(westSpur, 16, 20, 5, 11),
            new PushMark(eastSpur, 18, 22, 5, 12),
            new PushMark(westRidge, 20, 24, 6, 13),
            new PushMark(eastRidge, 17, 22, 6, 14),
            new PushMark(lake, -7, 26, 6, 15),
        ],
        Grain = 1.6,
        GrainScale = 19,
        Seed = 31,
    };

    var solved = ReliefSolver.Solve(footprint, spec);
    var filled = Terrain.FillDepressions(solved);
    var flow = Terrain.Flow(solved, filled, (68, 6));
    var channel = Terrain.Carve(solved, flow, radius: 4, depth: 2);
    var finished = channel.Bed;

    var plan = Render.TopDown(finished, 3, floor: 0, ceiling: 44);
    foreach (var (cell, _) in channel.Water)
        plan.Disc((cell.X - footprint.MinX) * 3 + 1.5, (cell.Z - footprint.MinZ) * 3 + 1.5, 1.8, 0x3C7FE0, 0.9);

    Png.Write(Path.Combine(outputRoot, "12-valley.png"),
        Render.Row([("a mined valley", plan),
                    ("what each cell costs", Render.StepMap(finished, 3)),
                    ("reachable on foot", Render.ReachMap(finished, Terrain.Passage.Walk, 3))]).Upscale(2));
    Png.Write(Path.Combine(outputRoot, "12b-valley-iso.png"),
        Render.Row([("the same valley in blocks",
            Render.Isometric(finished, tileWidth: 3, tileDepth: 2, blockRise: 2, floor: 0, ceiling: 44,
                tintOf: cell => channel.Water.ContainsKey(cell) ? 0x2E6FC4 : null))]).Upscale(2));

    var (low, high) = Measure.Body(finished);
    Say("figure 12 — the character of a mined valley: drawn spurs, a lake, a benched flank");
    Say(Terrain.Report(finished, "  the valley"));
    Say($"  body: 95% of columns in a {high - low}-block band");
    Say($"  water: {channel.Water.Count} cells at {channel.Water.Values.Distinct().Count()} distinct level(s)");
    Say("");
}

// ═══ what a solve costs, at the sizes maps are actually built at ══════════════════════════════════
{
    Say("solve cost — whether the field can be re-solved while a mark is dragged");
    foreach (var (name, ring) in new[]
    {
        ("45x30 room", Room),
        ("90x135 board", Board),
        ("192x128 whole map", new[] { new[] { 0.0, 0.0 }, [192, 0], [192, 128], [0, 128] }),
    })
    {
        var footprint = FootprintOf(ring);
        List<Mark> marks =
        [
            new PointMark(footprint.MinX + footprint.Width * 0.25, footprint.MinZ + footprint.Depth * 0.3, 14, 6),
            new PointMark(footprint.MinX + footprint.Width * 0.7, footprint.MinZ + footprint.Depth * 0.7, 18, 6),
            new RimMark(4),
        ];
        var spec = new ReliefSpec { Base = 4, Marks = marks, Grain = 1.0 };

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var flatSolve = ReliefSolver.Solve(footprint, spec, cascade: false);
        var flatCost = clock.Elapsed.TotalMilliseconds;

        clock.Restart();
        var cascaded = ReliefSolver.Solve(footprint, spec);
        var cascadeCost = clock.Elapsed.TotalMilliseconds;
        var cascadeDrift = footprint.Cells().Max(cell => Math.Abs(cascaded.At(cell.X, cell.Z) - flatSolve.At(cell.X, cell.Z)));

        var moved = new List<Mark>(marks);
        moved[0] = new PointMark(footprint.MinX + footprint.Width * 0.25 + 4,
                                 footprint.MinZ + footprint.Depth * 0.3 + 3, 14, 6);
        var movedSpec = spec with { Marks = moved };
        var settled = ReliefSolver.Solve(footprint, movedSpec);

        clock.Restart();
        var resumed = ReliefSolver.Solve(footprint, movedSpec, cascaded.Continuous, sweeps: 40);
        var resumeCost = clock.Elapsed.TotalMilliseconds;
        var drifted = footprint.Cells().Count(cell => resumed.At(cell.X, cell.Z) != settled.At(cell.X, cell.Z));
        var worstDrift = footprint.Cells().Max(cell => Math.Abs(resumed.At(cell.X, cell.Z) - settled.At(cell.X, cell.Z)));

        Say($"  {name}: {footprint.Count} cells — one grid {flatCost:0} ms, coarse-to-fine {cascadeCost:0} ms " +
            $"(worst difference between the two {cascadeDrift} block); " +
            $"a mark moved and resumed in 40 sweeps {resumeCost:0} ms, " +
            $"{drifted} cell(s) off the settled answer, worst by {worstDrift}");
    }
    Say("");
}

File.WriteAllText(Path.Combine(outputRoot, "report.txt"), string.Join("\n", log));
Console.WriteLine($"wrote figures to {Path.GetFullPath(outputRoot)}");
return 0;

// ── helpers the figures use ───────────────────────────────────────────────────────────────────────

static int Rows(List<(int From, int To)> runs) => runs.Sum(run => run.To - run.From + 1);

/// The worst step between a ring's own cells and the land immediately outside it — how a compound meets the
/// ground it stands on, which is the whole difference between holding a shape and excluding it.
static int EdgeStep(HeightField field, double[][] ring)
{
    var worst = 0;
    foreach (var (x, z) in field.Footprint.Cells())
    {
        if (!Footprint.PointInRing(x + 0.5, z + 0.5, ring)) continue;
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            if (!field.Has(x + dx, z + dz)) continue;
            if (Footprint.PointInRing(x + dx + 0.5, z + dz + 0.5, ring)) continue;
            worst = Math.Max(worst, Math.Abs(field.At(x + dx, z + dz) - field.At(x, z)));
        }
    }
    return worst;
}

/// The cells a drawn polyline passes through, in order — an authored stroke turned into the route the
/// carve walks, so what is cut is the line the author drew rather than one a search found.
static List<(int X, int Z)> Trace(double[][] points)
{
    var cells = new List<(int X, int Z)>();
    for (var i = 0; i + 1 < points.Length; i++)
    {
        double ax = points[i][0], az = points[i][1], bx = points[i + 1][0], bz = points[i + 1][1];
        var steps = (int)Math.Ceiling(Math.Max(Math.Abs(bx - ax), Math.Abs(bz - az)));
        for (var step = 0; step < steps; step++)
        {
            var t = (double)step / steps;
            var cell = ((int)Math.Round(ax + (bx - ax) * t), (int)Math.Round(az + (bz - az) * t));
            if (cells.Count == 0 || cells[^1] != cell) cells.Add(cell);
        }
    }
    cells.Add(((int)Math.Round(points[^1][0]), (int)Math.Round(points[^1][1])));
    return cells;
}

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
    // A scarp's high side is the left of its drawn direction, and a half-turn preserves handedness, so the
    // point order carries the sides across unchanged.
    ScarpMark scarp => new ScarpMark(
        scarp.Points.Select(p => new[] { 2 * centreX - p[0], 2 * centreZ - p[1] }).ToArray(),
        scarp.High, scarp.Low, scarp.FaceWidth, scarp.BandWidth),
    _ => mark,
};
