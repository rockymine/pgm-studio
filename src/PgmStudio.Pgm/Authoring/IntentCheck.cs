using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// The rules about the play pieces themselves, asked of the intent: what a map holds, what a goal or a wool
/// states, and where a spawn, its iron and a room stand in the region they are placed on. Each is the rule the
/// plan is asked under the same id, read off the pieces the map actually builds, so a map drawn on the sketch
/// hears it too.
///
/// <para>A unit is asked once, by its authored image — a spawn's mirror stands where the symmetry carries it
/// and answers the same — and the board's symmetry is the intent's, or the sketch's where it states none.</para>
/// </summary>
public static class IntentCheck
{
    /// <summary>The rules asked here, which a map's plan no longer answers once it holds an intent.</summary>
    public static readonly IReadOnlySet<string> Asks = new HashSet<string>(
    [
        PlanRules.NoSpawn, PlanRules.NoObjective, PlanRules.UnknownStyle, PlanRules.UnknownColor,
        ObjectiveRules.FloatCap, ObjectiveRules.Casing, ObjectiveRules.TwoTeamOnly,
        LayoutRules.IronInSpawn, LayoutRules.SpawnAtBack, LayoutRules.BuildingFootprint, LayoutRules.RoomRegionSize,
    ]);

    /// <summary>What the intent's play pieces break, under <paramref name="board"/>'s symmetry where the intent
    /// states none.</summary>
    public static Findings Check(MapIntent intent, SymmetryIntent? board = null)
    {
        var symmetry = intent.Symmetry ?? board;
        var wools = intent.Wools ?? [];
        var goals = intent.Destroyables ?? [];
        var cores = intent.Cores ?? [];
        var findings = new List<Finding>();

        if (intent.Spawns.Count == 0)
            findings.Add(new Finding(PlanRules.NoSpawn, "the map has no spawn"));
        if (wools.Count == 0 && goals.Count == 0 && cores.Count == 0 && (intent.ControlPoints ?? []).Count == 0)
            findings.Add(new Finding(PlanRules.NoObjective, "the map has no wool, monument, core or capture point",
                Severity.Complaint));

        foreach (var wool in wools.Where(wool => Authored(wool.Stamp)))
            if (!string.IsNullOrEmpty(wool.Color) && !WoolColors.IsColor(wool.Color))
                findings.Add(new Finding(PlanRules.UnknownColor,
                    $"wool '{wool.Id}' names the colour '{wool.Color}', which is not one of {string.Join(", ", WoolColors.All)}",
                    Subjects: [wool.Id]));

        foreach (var goal in goals.Where(goal => Authored(goal.Stamp)))
        {
            if (!string.IsNullOrEmpty(goal.Style) && !DestroyableStyles.IsKnown(goal.Style))
                findings.Add(new Finding(PlanRules.UnknownStyle,
                    $"monument '{goal.Id}' names the form '{goal.Style}', which is not one of {string.Join(", ", DestroyableStyles.All)}",
                    Subjects: [goal.Id]));
            if (goal.Float > ObjectiveDefaults.MaxFloat)
                findings.Add(new Finding(ObjectiveRules.FloatCap,
                    $"monument '{goal.Id}' has a float of {goal.Float} blocks, more than {ObjectiveDefaults.MaxFloat} blocks",
                    Subjects: [goal.Id]));
        }

        foreach (var core in cores.Where(core => Authored(core.Stamp)))
        {
            if (core.Float > ObjectiveDefaults.MaxFloat)
                findings.Add(new Finding(ObjectiveRules.FloatCap,
                    $"core '{core.Id}' has a float of {core.Float} blocks, more than {ObjectiveDefaults.MaxFloat} blocks",
                    Subjects: [core.Id]));
            if (core.Lava < ObjectiveDefaults.MinCoreLava || core.Lava > ObjectiveDefaults.MaxCoreLava)
                findings.Add(new Finding(ObjectiveRules.Casing,
                    $"core '{core.Id}' has a lava footprint of {core.Lava} blocks, not between "
                    + $"{ObjectiveDefaults.MinCoreLava} and {ObjectiveDefaults.MaxCoreLava} blocks", Subjects: [core.Id]));
            if (core.LavaHeight < ObjectiveDefaults.MinCoreLavaHeight || core.LavaHeight > ObjectiveDefaults.MaxCoreLavaHeight)
                findings.Add(new Finding(ObjectiveRules.Casing,
                    $"core '{core.Id}' has a lava height of {core.LavaHeight} blocks, not between "
                    + $"{ObjectiveDefaults.MinCoreLavaHeight} and {ObjectiveDefaults.MaxCoreLavaHeight} blocks",
                    Subjects: [core.Id]));
        }

        var teams = Teams(intent, board);
        if (teams != 2)
            foreach (var (kind, count) in new[] { ("monuments", goals.Count), ("cores", cores.Count) })
                if (count > 0)
                    findings.Add(new Finding(ObjectiveRules.TwoTeamOnly,
                        $"the map has {kind} and {Wording.Count(teams, "team")}, not 2"));

        var regions = intent.Spawns.SelectMany(spawn => spawn.Protection).ToList();
        foreach (var cube in (intent.Structures?.IronCubes ?? []).Where(cube => Authored(cube.Stamp)))
        {
            var cells = new Rect(cube.MinX, cube.MinZ, cube.MinX + RoomFrames.IronSpan, cube.MinZ + RoomFrames.IronSpan);
            if (!regions.Any(region => Holds(region, cells)))
                findings.Add(new Finding(LayoutRules.IronInSpawn,
                    $"iron '{cube.Id}' at ({cube.MinX}, {cube.MinZ}) has its {RoomFrames.IronSpan} by {RoomFrames.IronSpan} "
                    + "cube outside the region of every spawn", Severity.Complaint, Subjects: [cube.Id]));
        }

        foreach (var spawn in intent.Spawns.Where(spawn => Authored(spawn.Stamp) && spawn.Protection.Count > 0))
            if (symmetry is not null && !AtBack(spawn.Point, spawn.Protection[0], symmetry))
                findings.Add(new Finding(LayoutRules.SpawnAtBack,
                    $"spawn '{spawn.Id}' at ({spawn.Point.X:0.#}, {spawn.Point.Z:0.#}) stands in the half of its region "
                    + "closer to the symmetry centre", Severity.Complaint, Subjects: [spawn.Id]));

        var rooms = intent.Spawns.Where(spawn => Authored(spawn.Stamp)).Select(spawn => (spawn.Id, spawn.Protection, spawn.House))
            .Concat(wools.Where(wool => Authored(wool.Stamp)).Select(wool => (wool.Id, wool.Protection, wool.House)));
        foreach (var (id, protection, house) in rooms)
        {
            if (house?.Bounds is { } building
                && (building.MaxX - building.MinX > RoomFrames.FootprintCap || building.MaxZ - building.MinZ > RoomFrames.FootprintCap))
                findings.Add(new Finding(LayoutRules.BuildingFootprint,
                    $"the building of '{id}' is {Span(building)} blocks, more than {RoomFrames.FootprintCap} by "
                    + $"{RoomFrames.FootprintCap} blocks", Severity.Complaint, Subjects: [id]));
            foreach (var region in protection)
            {
                var (width, depth) = (region.MaxX - region.MinX, region.MaxZ - region.MinZ);
                if (Math.Min(width, depth) <= RoomFrames.RegionCapAcross && Math.Max(width, depth) <= RoomFrames.RegionCapAlong)
                    continue;
                findings.Add(new Finding(LayoutRules.RoomRegionSize,
                    $"the region of '{id}' is {Span(region)} blocks, more than {RoomFrames.RegionCapAcross} by "
                    + $"{RoomFrames.RegionCapAlong} blocks", Severity.Complaint, Subjects: [id]));
            }
        }
        return findings;
    }

    /// <summary>How many teams the map plays: the ones it states, or where it states none, as many as its
    /// symmetry — the intent's, else <paramref name="board"/>'s — has images.</summary>
    public static int Teams(MapIntent intent, SymmetryIntent? board = null) =>
        intent.Teams is { Count: > 0 } stated ? stated.Count : Symmetry.Order((intent.Symmetry ?? board)?.Mode);

    /// <summary>Whether a piece is the authored unit rather than an image the symmetry carried.</summary>
    private static bool Authored(StampId stamp) => string.IsNullOrEmpty(stamp.Kind) || stamp.Image == 0;

    /// <summary>Whether a spawn's point stands in the half of its region farther from the symmetry centre, along
    /// the region's longer side.</summary>
    private static bool AtBack(Pt point, Rect region, SymmetryIntent symmetry)
    {
        var alongZ = region.MaxZ - region.MinZ >= region.MaxX - region.MinX;
        var (at, middle, centre) = alongZ
            ? (point.Z, (region.MinZ + region.MaxZ) / 2, symmetry.CenterZ)
            : (point.X, (region.MinX + region.MaxX) / 2, symmetry.CenterX);
        return Math.Abs(at - centre) >= Math.Abs(middle - centre);
    }

    private static bool Holds(Rect outer, Rect inner) =>
        inner.MinX >= outer.MinX && inner.MaxX <= outer.MaxX && inner.MinZ >= outer.MinZ && inner.MaxZ <= outer.MaxZ;

    private static string Span(Rect r) => $"{r.MaxX - r.MinX:0.#} by {r.MaxZ - r.MinZ:0.#}";
}
