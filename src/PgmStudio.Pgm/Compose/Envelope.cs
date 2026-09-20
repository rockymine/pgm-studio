using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Compose;

/// <summary> The globals a plan derives from its player count alone: size band, land budget, corridor width,
/// fanned board size, and the cell region the team-unit grower may fill. See <see cref="Envelope.Derive"/>. </summary>
/// <param name="Symmetry">The resolved symmetry mode (never null).</param>
/// <param name="Teams">2 or 4.</param>
/// <param name="PlayersPerTeam">The clamped per-team player count. Nothing keys on it directly: it selects the
/// <see cref="Band"/> and the band is what the structure rules read.</param>
/// <param name="Band">The <see cref="SizeBands"/> word the player count falls in — the size this board is
/// built at.</param>
/// <param name="Cell">Blocks per proxy cell.</param>
/// <param name="Surface">Base island surface height.</param>
/// <param name="CorridorCells">The map-wide corridor width in cells: the band's width in blocks
/// (<see cref="UnitTuning.CorridorBlocks"/>) laid on this grid. Every non-wool box builds to it.</param>
/// <param name="WoolCorridorCells">The same for a wool approach, one rung narrower
/// (<see cref="UnitTuning.WoolCorridorBlocks"/>).</param>
/// <param name="BoardWidthBlocks">The fanned board's non-doubled-axis extent, in blocks.</param>
/// <param name="BoardLengthBlocks">The fanned board's doubled-axis extent (the direction symmetry fans), in
/// blocks. Equal to <see cref="BoardWidthBlocks"/> for the 4-team square board.</param>
/// <param name="LandPerTeam">The per-team land budget (blocks²) the grower spends — the band's measured land,
/// G8's player-count coupling.</param>
/// <param name="UnitMinX">The authored unit's cell bounds — the region the allocator may fill. The four
/// corners are one statement; a unit is the box between them.</param>
/// <param name="UnitMinZ">The Z lower bound of that box.</param>
/// <param name="UnitMaxX">The X upper bound of that box.</param>
/// <param name="UnitMaxZ">The Z upper bound of that box.</param>
public sealed record ComposeEnvelope(
    string Symmetry,
    int Teams,
    int PlayersPerTeam,
    string Band,
    int Cell,
    int Surface,
    int CorridorCells,
    int WoolCorridorCells,
    int BoardWidthBlocks,
    int BoardLengthBlocks,
    double LandPerTeam,
    int UnitMinX,
    int UnitMinZ,
    int UnitMaxX,
    int UnitMaxZ)
{
    /// <summary>The land budget in cells — the band's whole measured land for one team's half of a board.</summary>
    public double BudgetCells => LandPerTeam / (Cell * (double)Cell);

    /// <summary>The land the <b>team unit</b> may spend, in cells, and what the
    /// <see cref="LandBudget"/> is opened at: the band's budget less the mid's share
    /// (<see cref="MidCarver.MidShare"/>).</summary>
    public double UnitBudgetCells => BudgetCells * (1 - MidCarver.MidShare);

    /// <summary>The land the <b>mid</b> may spend on stones, in cells. Twice what one unit gave up, because
    /// the mid is one piece of ground both units paid for.</summary>
    public double MidLandCells => BudgetCells * 2 * MidCarver.MidShare;
}

/// <summary>
/// Stage one of composition: derive the board-wide globals from nothing but the player count and team
/// shape (G8's land coupling), before any geometry is grown. Pure aside from its <see cref="ComposeRng"/>
/// draws, which happen in one fixed order — <b>sampling order is part of the golden contract</b>: (1) the
/// fanned-board coverage ratio, (2) the 2-team board aspect (skipped for 4 teams, whose board is square).
/// </summary>
public static class Envelope
{
    /// <summary>The margin (cells) the grower keeps between its frontmost piece and the symmetry axis.</summary>
    public const int AxisMarginCells = 2;

    // G8: the land a team's half of a board holds, per size band, measured over 331 CTW corpus maps
    // (docs/world-scan/map-size-ladder.md). A band rather than a per-count curve because a map is built for a
    // range of counts, not one; the medians sit at about 250 blocks² a player at every size, which is the
    // reading `land/team = 176 × players^1.12` states continuously.
    private static readonly Dictionary<string, double> LandPerTeamByBand = new()
    {
        [SizeBands.Nano] = 2250,
        [SizeBands.Micro] = 4025,
        [SizeBands.Milli] = 7075,
        [SizeBands.Centi] = 8730,
    };

    // The measured coverage of a CTW map's bounding box by its land: 32-41% across every band, sampled over
    // the same range. The board follows the budget through it rather than being clamped to a size of its own.
    private const double CoverageLow = 0.30, CoverageHigh = 0.42;

    /// <summary>The land a team's half of a board holds at <paramref name="band"/>, in blocks².</summary>
    public static double LandPerTeamOf(string band) => LandPerTeamByBand[SizeBands.Canonical(band)];

    public static ComposeEnvelope Derive(ComposeRequest request, ComposeRng rng)
    {
        var band = SizeBands.Of(request.PlayersPerTeam);
        var landPerTeam = LandPerTeamOf(band);

        // (1) coverage ratio
        var coverage = rng.NextDouble(CoverageLow, CoverageHigh);
        var fannedArea = request.Teams * landPerTeam / coverage;

        int boardWidthBlocks, boardLengthBlocks;
        if (request.Teams == 4)
        {
            // measured 4-team boards are square and run 165-279 blocks a side (median 222)
            var side = Math.Clamp(Math.Sqrt(fannedArea), 90, 340);
            boardWidthBlocks = boardLengthBlocks = (int)Math.Round(side);
        }
        else
        {
            // (2) board aspect (2-team only) — measured length/width is 1.4-2.6 at the quartiles, 2.0 median
            var aspect = rng.NextDouble(1.4, 2.6);
            var width = Math.Clamp(Math.Sqrt(fannedArea / aspect), 40, 300);
            var length = Math.Clamp(fannedArea / width, 100, 620);
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
            symmetry, request.Teams, request.PlayersPerTeam, band, cell, Surface: 9,
            UnitTuning.CorridorCells(band, cell), UnitTuning.WoolCorridorCells(band, cell),
            boardWidthBlocks, boardLengthBlocks, landPerTeam,
            bounds.X, bounds.Z, bounds.X + bounds.Width, bounds.Z + bounds.Height);
    }
}
