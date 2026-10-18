using PgmStudio.Domain;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export;

/// <summary>
/// The rules about a room the export stamps, asked of the room it resolves for each spawn and wool: whether the
/// room frames at all on its region (<c>WX2</c>, <c>WX12</c>, <c>WX3</c>, <c>WX4</c>, <c>WX6</c>), whether its
/// pad moved to keep clear of the walls (<c>WX4</c>), whether its iron finds a place (<c>WX8</c>) and whether a
/// spawn room seats a monument for every wool its team captures (<c>PL8</c>). A room that refuses still
/// builds, as the default room around its marker, which is what makes the refusal worth saying.
///
/// <para>Each unit is asked once, by its authored image, against the shell the map binds for its kind.</para>
/// </summary>
public static class RoomCheck
{
    /// <summary>The rules asked here, which a map's plan no longer answers once it holds an intent.</summary>
    public static readonly IReadOnlySet<string> Asks = new HashSet<string>(
    [
        RoomFrameRules.FootprintTooSmall, RoomFrameRules.FootprintOffPiece, RoomFrameRules.MarkerParity,
        RoomFrameRules.PadClearance, RoomFrameRules.RoomUnreachable, RoomFrameRules.IronFit, PlanRules.MonumentSeats,
    ]);

    /// <summary>What the intent's rooms break, each resolved under the shell <paramref name="shells"/> binds
    /// for its kind.</summary>
    public static Findings Check(MapIntent intent, RoomShells shells)
    {
        var findings = new List<Finding>();
        var wools = intent.Wools ?? [];

        foreach (var wool in wools.Where(wool => Authored(wool.Stamp)))
        {
            var shellBound = RoomStyleScope.StyleOf(wool.House, shells.Wool, HouseStyle.Wool) is not null;
            var frame = WorldBuilder.ResolvedWool(wool, shellBound, out var refusal);
            if (refusal is not null) findings.Add(Refused(refusal, "wool", wool.Id));
            if (frame is { Pad.Shifted: true }) findings.Add(Shifted("wool", wool.Id));
        }

        foreach (var spawn in intent.Spawns.Where(spawn => Authored(spawn.Stamp)))
        {
            var shellBound = RoomStyleScope.StyleOf(spawn.House, shells.Spawn, HouseStyle.Spawn) is not null;
            var room = WorldBuilder.ResolvedSpawn(spawn, shellBound, out var refusal);
            if (refusal is not null) findings.Add(Refused(refusal, "spawn", spawn.Id));
            if (room is null) continue;
            if (room.Frame.Pad.Shifted) findings.Add(Shifted("spawn", spawn.Id));
            foreach (var iron in room.Iron.Where(iron => !iron.Placeable))
                findings.Add(new Finding(RoomFrameRules.IronFit,
                    $"iron at ({iron.MarkerX}, {iron.MarkerZ}) in the room of spawn '{spawn.Id}' has no place for its "
                    + $"{RoomFrames.IronSpan} by {RoomFrames.IronSpan} cube inside its region and at least "
                    + $"{RoomFrames.IronGap} blocks from the room", Severity.Complaint, Subjects: [spawn.Id]));

            if (room.Frame.Doors.Count == 0) continue;
            var captured = wools.Count(wool => wool.Monuments.Any(monument => monument.Team == spawn.Team));
            var seats = RoomFrames.MonumentSlots(room.Frame, room.Frame.Doors[0]).Count;
            if (captured > seats)
                findings.Add(new Finding(PlanRules.MonumentSeats,
                    $"the room of spawn '{spawn.Id}' seats {seats} wool monuments, less than the {captured} wools its "
                    + "team captures", Subjects: [spawn.Id]));
        }
        return findings;
    }

    private static bool Authored(StampId stamp) => string.IsNullOrEmpty(stamp.Kind) || stamp.Image == 0;

    private static Finding Refused(Finding refusal, string kind, string id) =>
        refusal with { Message = $"the {kind} room of '{id}' {refusal.Message}", Subjects = [id] };

    private static Finding Shifted(string kind, string id) =>
        new(RoomFrameRules.PadClearance,
            $"the {kind} room of '{id}' has its pad moved inward to keep 1 block from its walls",
            Severity.Complaint, Subjects: [id]);
}
