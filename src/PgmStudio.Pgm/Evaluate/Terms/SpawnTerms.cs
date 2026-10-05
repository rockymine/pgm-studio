using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>WL2: spawn and wool sit apart. Distance is <b>the walk over the walkable surface</b>
/// (terrain + build cells), not straight-line — the same measure as <see cref="WoolWoolDistance"/> — so it is
/// how far a player actually travels from spawn to the objective, routing around voids. Measured as the smallest
/// spawn→wool traversal (the nearest wool to spawn); only applies when both a spawn and a wool exist. Draws the
/// spawn, the nearest wool, and the route between them.</summary>
public sealed class SpawnWoolDistance : SoftTerm
{
    public override string Id => "spawn-wool-distance";
    public override string RuleId => LayoutRules.WoolSpawnDistance;

    public override double? Value(EvalContext ctx) => Closest(ctx).Blocks;

    public override MeasureUnit Unit => MeasureUnit.Blocks;

    protected override string Reads(EvalContext ctx, string value)
    {
        var closest = Closest(ctx);
        return $"wool '{closest.Wool}' has a walking distance of {value} from spawn '{closest.Spawn}'";
    }

    protected override IReadOnlyList<string> Subjects(EvalContext ctx) =>
        ctx.Plan.Placements.Spawns.Select(s => s.Piece)
            .Concat(ctx.Plan.Placements.Wools.Select(w => w.Piece)).Distinct().ToList();

    protected override IReadOnlyList<Evidence> Evidence(EvalContext ctx, double value, Band band)
    {
        var (_, a, b, _, _) = Closest(ctx);
        return a is null || b is null
            ? []
            : SurfaceNav.RouteEvidence(SurfaceNav.Ground(ctx), a.Value, b.Value, $"{value:0} < {band.Lo:0}");
    }

    // The nearest spawn→wool pair by surface traversal, in blocks, its endpoint cells and the two markers' ids.
    internal static (double? Blocks, (int, int)? A, (int, int)? B, string Spawn, string Wool) Closest(EvalContext ctx)
    {
        var ground = SurfaceNav.Ground(ctx);
        var spawns = ctx.Plan.Placements.Spawns
            .Select(s => (s.Id, Cell: SurfaceNav.MarkerCell(ctx, s.Piece, s.At, ground.Footprint)))
            .Where(s => s.Cell is not null).Select(s => (s.Id, Cell: s.Cell!.Value)).ToList();
        var wools = ctx.Plan.Placements.Wools
            .Select(w => (w.Id, Cell: SurfaceNav.MarkerCell(ctx, w.Piece, w.At, ground.Footprint)))
            .Where(w => w.Cell is not null).Select(w => (w.Id, Cell: w.Cell!.Value)).ToList();
        if (spawns.Count == 0 || wools.Count == 0) return (null, null, null, "", "");

        double? best = null;
        (int, int)? ba = null, bb = null;
        string spawnId = "", woolId = "";
        foreach (var s in spawns)
            foreach (var w in wools)
                if (ground.Stand(s.Cell) is { } from && ground.Stand(w.Cell) is { } to
                    && Walk.Between(from, to, ground) is { } walked
                    && walked.Cost.Distance < (best ?? double.MaxValue))
                    { best = walked.Cost.Distance; ba = s.Cell; bb = w.Cell; spawnId = s.Id; woolId = w.Id; }
        return (best, ba, bb, spawnId, woolId);
    }
}

/// <summary>WL2 as a hard floor: the spawn and its nearest wool must be at least <see cref="MinBlocks"/> blocks
/// apart <b>by the walk</b> (the same measure as <see cref="SpawnWoolDistance"/>, not
/// straight-line). The generator clears it comfortably — the authored floor is ~30 by traversal — so it costs
/// nothing today; the surface measure is the correct one to guard against a generator cramming a wool onto
/// spawn, which a Euclidean gate would let through wherever the walk round is long.</summary>
public sealed class SpawnWoolFloor : ILayoutTerm
{
    /// <summary>WL2's 20 blocks, now read as real travel rather than straight-line.</summary>
    public const int MinBlocks = 20;

    public string Id => "spawn-wool-floor";
    public string RuleId => LayoutRules.WoolSpawnFloor;
    public TermKind Kind => TermKind.Hard;

    public TermScore Measure(EvalContext ctx)
    {
        var (blocks, a, b, _, _) = SpawnWoolDistance.Closest(ctx);
        if (blocks is null || blocks.Value >= MinBlocks) return TermScores.Clean(this);

        var evidence = a is null || b is null
            ? (IReadOnlyList<Evidence>)[]
            : SurfaceNav.RouteEvidence(SurfaceNav.Ground(ctx), a.Value, b.Value, $"{blocks:0} < {MinBlocks}");
        var subjects = ctx.Plan.Placements.Spawns.Select(s => s.Piece)
            .Concat(ctx.Plan.Placements.Wools.Select(w => w.Piece)).Distinct().ToList();
        return TermScores.Violated(this, $"spawn↔wool traversal {blocks:0} < {MinBlocks} blocks", subjects, evidence);
    }
}

/// <summary>WL2's lane clause as a hard term: a wool room may not share an edge with its own team's spawn. Reads
/// the land seams of the authored unit (<see cref="PieceInterfaces.Seams"/> — full-width or narrow), whose
/// pieces are one team's, so every wool-room↔spawn seam it finds is a room against its own spawn. A corner
/// touch shares no edge, and a room a third piece or a gap away from its spawn is the distance terms' to judge.
/// Indicts both pieces and draws the shared edge.</summary>
public sealed class WoolRoomSpawnSeam : ILayoutTerm
{
    public string Id => "wool-room-spawn-seam";
    public string RuleId => LayoutRules.WoolRoomTouchesSpawn;
    public TermKind Kind => TermKind.Hard;

    public TermScore Measure(EvalContext ctx)
    {
        var seams = PieceInterfaces.Seams(ctx.Contacts)
            .Where(s => (s.RoleA, s.RoleB) is (PlanRoles.WoolRoom, PlanRoles.Spawn) or (PlanRoles.Spawn, PlanRoles.WoolRoom))
            .ToList();
        if (seams.Count == 0) return TermScores.Clean(this);

        double cell = ctx.Contacts.Cell;
        var evidence = seams
            .Select(s => (Evidence)Ev.Segment(EvidenceTags.Offender, s.X1 / cell, s.Z1 / cell, s.X2 / cell, s.Z2 / cell))
            .ToList();
        var subjects = seams.SelectMany(s => new[] { s.A, s.B }).Distinct().ToList();
        var pairs = string.Join(", ", seams.Select(s => $"{s.A}–{s.B}"));
        return TermScores.Violated(this, $"a wool room shares an edge with its own spawn ({pairs})", subjects, evidence);
    }
}
