using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// What may be done to a map next, from where it has got to.
///
/// <para><b>A stage is a progress marker, not a lock.</b> <c>flow.md</c>'s one-way flow means nothing reads
/// back up — a later level never writes into an earlier one — not that a built map may never be re-planned.
/// So this names the moves a driver would reach for from here and nothing refuses on the stage: an author who
/// re-plans a configured map is doing something the studio supports, and the plan is still upstream of
/// everything a recompile derives.</para>
///
/// <para><b>The layers decide more than the stage does.</b> Rebuilding a sketch from a plan needs a plan to
/// rebuild from, whatever stage the map is at, so a move is offered when the documents it reads are stored
/// rather than when the stage is right. The stage only orders them: it says which move is the one the author
/// was about to make.</para>
/// </summary>
public static class MapMoves
{
    public static IReadOnlyList<MapMove> From(string stage, MapArtifacts layers) =>
    [
        .. Every(stage, layers).OrderByDescending(move => move.Next),
    ];

    private static IEnumerable<MapMove> Every(string stage, MapArtifacts layers)
    {
        yield return new MapMove("edit the plan", "PUT /api/map/{slug}/plan",
            Next: stage == MapStage.Plan);

        if (layers.Plan)
        {
            yield return new MapMove("rebuild the drawing from the plan",
                "PUT /api/map/{slug}/sketch/from-plan", Next: stage == MapStage.Plan);
            yield return new MapMove("rebuild the intent from the plan",
                "PUT /api/map/{slug}/intent/from-plan", Next: false);
        }

        if (layers.Sketch)
        {
            yield return new MapMove("draw", "PUT /api/map/{slug}/sketch", Next: stage == MapStage.Sketch);
            yield return new MapMove("declare the drawing done", "POST /api/map/{slug}/sketch/finish",
                Next: stage == MapStage.Sketch);
        }

        yield return new MapMove("state what the map is played for", "PUT /api/map/{slug}/intent",
            Next: stage == MapStage.Configure);

        if (layers.Intent || layers.World)
            yield return new MapMove("export the world", "GET /api/map/{slug}/export",
                Next: stage == MapStage.Edit);
    }
}
