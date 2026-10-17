using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The rules a board's structure answers, read off the ground the sketch builds rather than the plan's
/// rectangles (<see cref="SketchGround"/>): a face's front line (<c>FR8</c>, <c>FR9</c>), the narrowest run of
/// each gap beside a goal, beside a team's own ground and in a plain hole (<c>WL12</c>, <c>WL20</c>,
/// <c>LN6</c>), and the strait between the two teams of a wool board (<c>CT12</c>). Each is the rule the plan
/// is asked under the same id, measured in blocks, and an owner it names is a group or a room.
/// </summary>
public static class SketchBoardCheck
{
    /// <summary>The rules asked here, which a map's plan no longer answers once its sketch is asked.</summary>
    public static readonly IReadOnlySet<string> Asks = new HashSet<string>(
    [
        LayoutRules.FrontlineShare, LayoutRules.MinimumFrontline, LayoutRules.RoomFrontGapWidth,
        LayoutRules.RoomHomeGapWidth, LayoutRules.HoleWidth, LayoutRules.TeamGapWidth,
    ]);

    /// <summary>What the board the layout and the intent build breaks; nothing where it builds no ground.</summary>
    public static Findings Check(string layoutJson, MapIntent intent, SymmetryIntent? board = null)
    {
        if (SketchGround.Of(layoutJson, intent) is not { } ground) return Findings.None;
        BoardStructure structure;
        try { structure = BoardDeriver.Derive(ground); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        { return Findings.None; }
        var rooms = ground.SpawnOwners.Concat(ground.WoolOwners).ToHashSet(StringComparer.Ordinal);
        var strait = (intent.Wools ?? []).Count > 0 && IntentCheck.Teams(intent, board) == 2;
        return new Findings([.. PlanValidator.BoardEdges(structure, rooms, strait)]);
    }
}
