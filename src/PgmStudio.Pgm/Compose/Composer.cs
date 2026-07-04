using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// Composes a full <see cref="PlanModel"/> from nothing but a player count and team shape: derive the board
/// envelope from the player count (<see cref="Envelope"/>), grow one team's authored unit
/// (<see cref="TeamUnitGrower"/>), then assemble the pieces and placements into a plan. Zones, cliffs, and
/// walls are left empty — later stages (mid carving, elevation) fill them in.
/// </summary>
public static class Composer
{
    public static PlanModel Compose(ComposeRequest request)
    {
        var (envelope, unit) = ComposeStages(request);
        return Assemble(request, envelope, unit);
    }

    /// <summary>The same pipeline as <see cref="Compose"/>, but stopping short of assembly so tests can gate
    /// the envelope and the grown unit independently.</summary>
    public static (ComposeEnvelope Envelope, GrownUnit Unit) ComposeStages(ComposeRequest request)
    {
        var rng = new ComposeRng(request.Seed);
        var envelope = Envelope.Derive(request, rng);
        var unit = TeamUnitGrower.Grow(envelope, rng);
        return (envelope, unit);
    }

    private static PlanModel Assemble(ComposeRequest request, ComposeEnvelope envelope, GrownUnit unit)
    {
        var plan = new PlanModel
        {
            Meta = new PlanMeta { Name = $"Composed p{request.PlayersPerTeam} t{request.Teams} #{request.Seed}" },
            Globals = new PlanGlobals
            {
                Cell = envelope.Cell,
                Symmetry = envelope.Symmetry,
                MaxPlayers = request.PlayersPerTeam,
                Surface = envelope.Surface,
                Headroom = envelope.Headroom,
            },
        };

        foreach (var piece in unit.Pieces)
            plan.Pieces.Add(new PlanPiece { Id = piece.Id, Role = PlanRoles.Piece, Rect = piece.Rect });

        plan.Placements.Spawns.Add(new SpawnPlacement
        {
            Piece = unit.Spawn.Piece, At = unit.Spawn.At, Facing = unit.Spawn.Facing,
        });
        foreach (var wool in unit.Wools)
            plan.Placements.Wools.Add(new WoolPlacement { Piece = wool.Piece, At = wool.At });

        return plan;
    }
}
