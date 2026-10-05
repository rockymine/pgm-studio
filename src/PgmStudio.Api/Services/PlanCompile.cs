using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>What compiling a plan came to: a refusal, or the layout and intent it compiled to and what the
/// completeness gate said without refusing.</summary>
public sealed record PlanCompiled(
    Refusal? Refusal, SketchLayout? Layout = null, MapIntent? Intent = null, Findings? Complaints = null);

/// <summary>
/// A plan compiled one way into the pair a map is stored from. The structural gate and the completeness gate ask
/// first, and either refusing stops it; then the compile; then each island's team is filled in on the compiled
/// footprint — a spawn's team owns its island, else a wool's owner, else it stays neutral — so the intent carries
/// it downstream and Configure opens with it assigned.
/// </summary>
public static class PlanCompile
{
    public static PlanCompiled Run(PlanModel plan)
    {
        Findings completeness;
        SketchLayout layout;
        MapIntent intent;
        try
        {
            completeness = PlanValidator.Completeness(plan);
            var judged = PlanValidator.Check(plan).And(completeness);
            if (judged.Refuses) return new(new Refusal(422, "plan not compilable", [.. judged.Refusals]));
            (layout, intent) = PlanCompiler.Compile(plan);
        }
        catch (Exception fault) when (fault is ArgumentException or InvalidOperationException)
        {
            return new(Refusal.At(400, "invalid plan structure", JsonFaults.Said(RequestRules.Unreadable, fault, document: "the plan")));
        }

        var footprint = SketchRasterizer.RasterizeColumns(JsonSerializer.Serialize(layout, SketchLayout.Json))
            .Select(column => (column.X, column.Z));
        foreach (var (islandId, team) in TeamTerritory.Assign(footprint, intent)) intent.IslandTeams[islandId] = team;
        return new(null, layout, intent, new Findings([.. completeness.Complaints]));
    }
}
