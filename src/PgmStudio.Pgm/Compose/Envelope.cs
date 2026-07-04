namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The globals a plan derives from its player count alone: land budget, fanned board size, and the cell
/// region the team-unit grower may fill. See <see cref="Envelope.Derive"/>.
/// </summary>
/// <param name="Symmetry">The resolved symmetry mode (never null).</param>
/// <param name="Teams">2 or 4.</param>
/// <param name="PlayersPerTeam">The clamped per-team player count — some structure rules key on it
/// directly rather than on the land budget (a third wool lane wants a big team, WL6).</param>
/// <param name="Cell">Blocks per proxy cell.</param>
/// <param name="Surface">Base island surface height.</param>
/// <param name="Headroom">Build cap above <see cref="Surface"/>.</param>
/// <param name="BoardWidthBlocks">The fanned board's non-doubled-axis extent, in blocks.</param>
/// <param name="BoardLengthBlocks">The fanned board's doubled-axis extent (the direction symmetry fans), in
/// blocks. Equal to <see cref="BoardWidthBlocks"/> for the 4-team square board.</param>
/// <param name="LandPerTeam">The per-team land budget (blocks²) the grower targets — G8's player-count
/// coupling.</param>
/// <param name="UnitMinX">The authored unit's cell bounds — the region <see cref="TeamUnitGrower"/> may fill.</param>
public sealed record ComposeEnvelope(
    string Symmetry,
    int Teams,
    int PlayersPerTeam,
    int Cell,
    int Surface,
    int Headroom,
    int BoardWidthBlocks,
    int BoardLengthBlocks,
    double LandPerTeam,
    int UnitMinX,
    int UnitMinZ,
    int UnitMaxX,
    int UnitMaxZ);

/// <summary>
/// Stage one of composition: derive the board-wide globals from nothing but the player count and team
/// shape (G8's land/player coupling), before any geometry is grown. Pure aside from its <see cref="ComposeRng"/>
/// draws, which happen in one fixed order — <b>sampling order is part of the golden contract</b>: (1) the
/// fanned-board coverage ratio, (2) the 2-team board aspect (skipped for 4 teams, whose board is square).
/// </summary>
public static class Envelope
{
    /// <summary>The margin (cells) the grower keeps between its frontmost piece and the symmetry axis.</summary>
    public const int AxisMarginCells = 2;

    // G8: land per player rises with per-team land, interpolated piecewise-linearly over the corpus anchors
    // (players/team → blocks/player), clamped outside the table's ends.
    private static readonly (double Players, double Bp)[] BpAnchors =
    [
        (5, 65), (10, 95), (12, 105), (14, 115), (16, 155), (18, 160), (20, 175), (32, 185),
    ];

    public static ComposeEnvelope Derive(ComposeRequest request, ComposeRng rng)
    {
        var bp = InterpolateBp(request.PlayersPerTeam);
        var landPerTeam = request.PlayersPerTeam * bp;

        // (1) coverage ratio — the corpus's measured 21–49% land coverage of the fanned board, sampled 0.28..0.42
        var coverage = rng.NextDouble(0.28, 0.42);
        var fannedArea = request.Teams * landPerTeam / coverage;

        int boardWidthBlocks, boardLengthBlocks;
        if (request.Teams == 4)
        {
            var side = Math.Clamp(Math.Sqrt(fannedArea), 90, 180);
            boardWidthBlocks = boardLengthBlocks = (int)Math.Round(side);
        }
        else
        {
            // (2) board aspect (2-team only) — length/width in 1.0..3.0
            var aspect = rng.NextDouble(1.0, 3.0);
            var width = Math.Clamp(Math.Sqrt(fannedArea / aspect), 25, 130);
            var length = Math.Clamp(fannedArea / width, 100, 280);
            boardWidthBlocks = (int)Math.Round(width);
            boardLengthBlocks = (int)Math.Round(length);
        }

        var symmetry = request.Symmetry;
        var frame = Frame.For(symmetry);
        var cell = request.Cell;
        var boardWidthCells = Math.Max(1, (int)Math.Round(boardWidthBlocks / (double)cell));
        var boardLengthCells = Math.Max(1, (int)Math.Round(boardLengthBlocks / (double)cell));

        // The authored unit's cell bounds: for rot_180/mirror_z/rot_90 the board's z-extent is the doubled
        // (length) axis and the unit gets half of it (u), with the full x-extent as its cross range (v); for
        // mirror_x the roles of x/z swap per Frame. rot_90's board is square, so length==width and this bound
        // is really a generous bounding box for the wedge — the grower's fan-overlap check is the real limit.
        var uMax = Math.Max(AxisMarginCells + 1, boardLengthCells / 2);
        var vHalf = Math.Max(1, boardWidthCells / 2);
        var bounds = frame.ToRect(AxisMarginCells, uMax - AxisMarginCells, -vHalf, 2 * vHalf);

        return new ComposeEnvelope(
            symmetry, request.Teams, request.PlayersPerTeam, cell, Surface: 9, Headroom: 11,
            boardWidthBlocks, boardLengthBlocks, landPerTeam,
            bounds[0], bounds[1], bounds[0] + bounds[2], bounds[1] + bounds[3]);
    }

    private static double InterpolateBp(double players)
    {
        if (players <= BpAnchors[0].Players) return BpAnchors[0].Bp;
        if (players >= BpAnchors[^1].Players) return BpAnchors[^1].Bp;
        for (var i = 1; i < BpAnchors.Length; i++)
        {
            var (p1, b1) = BpAnchors[i - 1];
            var (p2, b2) = BpAnchors[i];
            if (players > p2) continue;
            var t = (players - p1) / (p2 - p1);
            return b1 + t * (b2 - b1);
        }
        return BpAnchors[^1].Bp;
    }
}
