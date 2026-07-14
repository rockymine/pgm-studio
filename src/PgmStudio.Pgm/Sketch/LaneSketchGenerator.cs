using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Pgm.Sketch;

/// <summary>The board shape a generated sketch follows.</summary>
public enum LaneArchetype
{
    /// <summary>Two teams, an 'H' of straight lanes mirrored across the mid (one wool per team).</summary>
    H,
    /// <summary>Four teams, curved blade lanes pinwheeling around the centre under rot_90 (one wool per team).</summary>
    Pinwheel,
    /// <summary>Two teams, a 'Y'/trident of diagonal arms (three wools per team) with chevron mid islands.</summary>
    Trident,
    /// <summary>Two teams, an organically grown island — lanes grown from the spawn hub out to noise-spread
    /// wool tips, with variable-width jittered polygon hulls and optional diamond holes (seeded).</summary>
    Organic,
}

/// <summary>Knobs for the lane-layout sketch generator. Defaults give a 60×90 two-team board with
/// 12-wide lanes; the <see cref="Archetype"/> picks the board shape (Pinwheel forces a square board).</summary>
public sealed record LaneLayoutOptions
{
    public LaneArchetype Archetype { get; init; } = LaneArchetype.H;
    public double Width { get; init; } = 60;
    public double Height { get; init; } = 90;
    /// <summary>Target lane width in blocks (legs, arms, blades).</summary>
    public double LaneWidth { get; init; } = 12;
    /// <summary>Empty border kept clear of the map edge.</summary>
    public double Margin { get; init; } = 6;
    /// <summary>Bow the 'H' crossbar into a curve instead of a straight strip.</summary>
    public bool CurvedCrossbar { get; init; }
    /// <summary>Drop neutral island(s) in the mid gap (the contested centre).</summary>
    public bool MidIsland { get; init; } = true;
    /// <summary>Symmetry override; empty → the archetype's natural mode (H/Trident mirror_z, Pinwheel rot_90).</summary>
    public string MirrorMode { get; init; } = "";
    /// <summary>Seed for the Organic archetype — same seed → same island.</summary>
    public int Seed { get; init; } = 1;
    /// <summary>Wools per team for the Organic archetype (one dead-end lane each).</summary>
    public int Wools { get; init; } = 2;
    /// <summary>Probability a grown lane carries a diamond hole (Organic archetype).</summary>
    public double HoleChance { get; init; } = 0.45;
    /// <summary>Organic: number of near-mid trunk branches the island reaches the centre with — each becomes a
    /// bridge across the void, so 2 gives two crossing points (two angles of attack) instead of one chokepoint.</summary>
    public int MidBranches { get; init; } = 2;
    /// <summary>Organic: the void gap (blocks) between the two team islands at the mid line — the span each
    /// bridge crosses. Smaller → the islands almost touch; larger → a longer, more exposed crossing.</summary>
    public double VoidDistance { get; init; } = 16;
    /// <summary>Organic: minimum angular separation (degrees) between branches meeting at the spawn hub, so no
    /// two lanes fan out tightly enough to leave a thin sliver of land between them.</summary>
    public double MinHubAngle { get; init; } = 35;
    /// <summary>Organic: probability a (long enough) wool lane <b>forks</b> — grows a child branch off a point
    /// partway along it (a <c>_/-</c> shape with its own offspring). The wool stays on the primary tip for now;
    /// where wools go on a fork's leaves is a deliberate TBD.</summary>
    public double BranchChance { get; init; } = 0.35;
}

/// <summary>Where an objective belongs on the generated board, for the Configure step to consume.</summary>
public sealed record ObjectiveHint(string Kind, int Team, double X, double Z);

/// <summary>The generated layout plus the objective placements it implies.</summary>
public sealed record LaneLayoutResult(SketchLayout Layout, IReadOnlyList<ObjectiveHint> Objectives);

/// <summary>
/// Builds a starter Capture-the-Wool sketch from lane primitives. One team's unit — lanes that dead-end at
/// the far side (wool tips) plus a spawn hub — is authored once and fanned to every team by the board's
/// symmetry, with optional neutral mid islands in the contested centre. Wools sit at the lane tips and
/// spawns at the hub, matching how real maps place objectives. The output is
/// a <see cref="SketchLayout"/> ready to store on a draft map or feed straight to <see cref="SketchRasterizer"/>.
/// </summary>
public static class LaneSketchGenerator
{
    public static LaneLayoutResult Build(LaneLayoutOptions? options = null) => (options ?? new()).Archetype switch
    {
        LaneArchetype.Pinwheel => Pinwheel(options ?? new()),
        LaneArchetype.Trident => Trident(options ?? new()),
        LaneArchetype.Organic => Organic(options ?? new()),
        _ => HLayout(options ?? new()),
    };

    /// <summary>The board options the Organic archetype actually grows on: organic islands want room for the
    /// lanes to read as distinct corridors, so the small 60×90 default is upsized to 120×150 (a non-default
    /// board is left as the author set it). Shared so the demonstration page grows on the same board.</summary>
    public static LaneLayoutOptions OrganicOptions(LaneLayoutOptions o) =>
        o is { Width: 60, Height: 90 } ? o with { Width = 120, Height = 150 } : o;

    // ── Organic: lanes grown from the hub out to noise-spread wool tips ──
    private static LaneLayoutResult Organic(LaneLayoutOptions o)
    {
        o = OrganicOptions(o);
        var unit = OrganicLane.Grow(o);
        return Assemble(o, Mode(o, "mirror_z"), unit.Shapes, unit.Spawn, unit.Tips, mids: [], bridges: unit.TrunkTips);
    }

    // ── H: two teams, straight legs + crossbar, one wool each ─────────────────────────────────────
    public static LaneLayoutResult HLayout(LaneLayoutOptions? options = null)
    {
        var o = options ?? new();
        double lx = o.Margin + o.LaneWidth / 2;            // wool leg (left)
        double rx = o.Width - o.Margin - o.LaneWidth / 2;  // spawn leg (right)
        double top = o.Margin + 2;                         // dead-end (far) side
        double bot = o.Height / 2 - 5;                     // stop short of mid → a gap to bridge
        double crossZ = top + 16;

        var unit = new List<SketchShape>
        {
            Poly("wool_leg", Lane.Strip([[lx, top], [lx, bot]], o.LaneWidth)),
            Poly("spawn_leg", Lane.Strip([[rx, top], [rx, bot]], o.LaneWidth)),
            Poly("cross", o.CurvedCrossbar
                ? Lane.Strip(CatmullRom.Spline([[o.Margin, crossZ], [o.Width / 2, crossZ - 6], [o.Width - o.Margin, crossZ]]), o.LaneWidth)
                : Lane.Strip([[o.Margin, crossZ], [o.Width - o.Margin, crossZ]], o.LaneWidth)),
        };
        var mids = o.MidIsland ? new List<SketchShape> { Poly("mid", Regular(o.Width / 2, o.Height / 2, o.LaneWidth * 0.7, 8)) } : [];
        return Assemble(o, Mode(o, "mirror_z"), unit, (rx, top + 1), [(lx, top + 1)], mids);
    }

    // ── Pinwheel: four teams, a curved blade per team rotating about the centre (rot_90) ───────────
    private static LaneLayoutResult Pinwheel(LaneLayoutOptions options)
    {
        var s = Math.Min(options.Width, options.Height);          // rot_90 wants a square board
        var o = options with { Width = s, Height = s };
        double c = s / 2, m = o.Margin, w = o.LaneWidth;

        // team 0's blade points "north": it starts at a safe inner radius (so the four rot_90 copies stay
        // separate around a hollow centre) and curls out to a tip near the top-left corner — the swirl.
        double rin = w * 1.6;                                       // inner radius keeps blades apart
        double rout = c - m - w * 0.5;                             // reach toward the corner
        var hub = (c, c - rin);                                     // inner end (spawn), straight up
        var tip = (c - rout * 0.5, c - rout * 0.87);               // outer end (wool), up-left (~120°)
        var ctrl = (c + w * 0.4, c - rin - (rout - rin) * 0.5);    // bow right → the comma curl
        var blade = Lane.Strip(CatmullRom.Spline([
            [hub.Item1, hub.Item2], [ctrl.Item1, ctrl.Item2], [tip.Item1, tip.Item2],
        ]), w);

        var unit = new List<SketchShape> { Poly("blade", blade) };
        var mids = o.MidIsland ? new List<SketchShape> { Poly("mid", Regular(c, c, w * 0.55, 6)) } : [];
        // one wool per team at the blade tip; spawn at the hub
        return Assemble(o, Mode(o, "rot_90"), unit, hub, [tip], mids);
    }

    // ── Trident: two teams, diagonal arms + central stem, three wools each ─────────────────────────
    private static LaneLayoutResult Trident(LaneLayoutOptions options)
    {
        var o = options;
        double cx = o.Width / 2, m = o.Margin, w = o.LaneWidth;
        double top = m + 2, hub = o.Height / 2 - w;     // hub sits below the arms, near the mid
        double armTipZ = top + 4;

        var leftTip = (m + w * 0.6, armTipZ);
        var rightTip = (o.Width - m - w * 0.6, armTipZ);
        var stemTip = (cx, hub - w * 1.4);
        var hubPt = (cx, hub);

        var unit = new List<SketchShape>
        {
            Poly("arm_l", Lane.Strip([[leftTip.Item1, leftTip.Item2], [hubPt.Item1, hubPt.Item2]], w)),
            Poly("arm_r", Lane.Strip([[rightTip.Item1, rightTip.Item2], [hubPt.Item1, hubPt.Item2]], w)),
            Poly("stem", Lane.Strip([[stemTip.Item1, stemTip.Item2], [hubPt.Item1, hubPt.Item2]], w)),
        };
        // chevron mid islands (two angled lanes) on the mid line, not mirrored (self-contested centre)
        var mids = o.MidIsland
            ? new List<SketchShape>
            {
                Poly("mid_l", Lane.Strip([[cx - w * 1.8, o.Height / 2 - w], [cx - w * 0.4, o.Height / 2], [cx - w * 1.8, o.Height / 2 + w]], w * 0.7)),
                Poly("mid_r", Lane.Strip([[cx + w * 1.8, o.Height / 2 - w], [cx + w * 0.4, o.Height / 2], [cx + w * 1.8, o.Height / 2 + w]], w * 0.7)),
            }
            : [];
        return Assemble(o, Mode(o, "mirror_z"), unit, hubPt, [leftTip, rightTip, stemTip], mids);
    }

    // ── assembly: fan one team's unit + objectives to every team by the board symmetry ────────────
    private static LaneLayoutResult Assemble(
        LaneLayoutOptions o, string mirrorMode, List<SketchShape> unit,
        (double X, double Z) spawn, List<(double X, double Z)> wools, List<SketchShape> mids,
        List<(double X, double Z)>? bridges = null)
    {
        double cx = o.Width / 2, cz = o.Height / 2;
        var shapes = new List<SketchShape>(unit);
        var islands = new List<SketchIsland>
        {
            new() { Id = "team", Name = "Team", Mirrors = true, ShapeIds = [.. unit.Select(s => s.Id)] },
        };
        if (mids.Count > 0)
        {
            shapes.AddRange(mids);
            islands.Add(new() { Id = "mid", Name = "Mid", Mirrors = false, ShapeIds = [.. mids.Select(s => s.Id)] });
        }
        var layout = new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = mirrorMode, Center = new SketchCenter { Cx = cx, Cz = cz } },
            Layout = new SketchShapes { Shapes = shapes, Islands = islands },
        };

        var teams = Symmetry.Order(mirrorMode);
        var hints = new List<ObjectiveHint>();
        for (var k = 0; k < teams; k++)
        {
            var (sx, sz) = Image(spawn, mirrorMode, cx, cz, k);
            hints.Add(new("spawn", k, sx, sz));
            foreach (var w in wools)
            {
                var (wx, wz) = Image(w, mirrorMode, cx, cz, k);
                hints.Add(new("wool", k, wx, wz));
            }
        }
        // bridge anchors: one per mid trunk-tip of team 0 (NOT fanned). Each names a point the island reaches
        // near mid; the map generator spans it across to its mirror so every trunk gets its own crossing.
        if (bridges is not null)
            foreach (var (bx, bz) in bridges) hints.Add(new("bridge", 0, bx, bz));
        return new LaneLayoutResult(layout, hints);
    }

    // team 0 stays put; teams 1..n are the orbit images (rot_90 fans to 90/180/270, mirrors reflect).
    private static (double X, double Z) Image((double X, double Z) p, string mode, double cx, double cz, int k) =>
        k == 0 ? p : Symmetry.Point(p.X, p.Z, mode, cx, cz, k);

    private static string Mode(LaneLayoutOptions o, string fallback) =>
        string.IsNullOrEmpty(o.MirrorMode) ? fallback : o.MirrorMode;

    private static SketchShape Poly(string id, List<double[]> ring) =>
        new() { Id = id, Type = "polygon", Operation = "add", Vertices = [.. ring] };

    private static List<double[]> Regular(double cx, double cz, double r, int n)
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
