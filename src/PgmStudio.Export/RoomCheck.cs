using PgmStudio.Domain;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export;

/// <summary>
/// The rules about a room the export stamps, asked of the room it resolves for each spawn and wool: whether the
/// room frames at all on its region (<c>WX2</c>, <c>WX12</c>, <c>WX3</c>, <c>WX4</c>, <c>WX6</c>), whether its
/// pad moved to keep clear of the walls (<c>WX4</c>), whether its iron finds a place (<c>WX8</c>), whether the
/// chests and seats its house states stand on the floor inside its walls (<c>WX15</c>, <c>WX16</c>) and whether a
/// spawn room seats a monument for every wool its team captures (<c>PL8</c>, <c>WX17</c> where its house states
/// the seats). A room that refuses still builds, as the default room around its marker and with the interior it
/// derives, which is what makes the refusal worth saying.
///
/// <para>Each unit is asked once, by its authored image, against the shell the map binds for its kind.</para>
/// </summary>
public static class RoomCheck
{
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
            if (frame is null) continue;
            if (frame.Pad.Shifted) findings.Add(Shifted("wool", wool.Id));
            findings.AddRange(Built("wool", wool.Id, wool.House, frame with { Reflected = wool.Reflected }, 0).Refused);
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

            var captured = Captured(wools, spawn.Team);
            var built = Built("spawn", spawn.Id, spawn.House, room.Frame with { Reflected = spawn.Reflected }, captured);
            findings.AddRange(built.Refused);
            if (built.Stated || room.Frame.Doors.Count == 0) continue;
            var seats = built.Interior.Monuments.Count;
            if (captured > seats)
                findings.Add(new Finding(PlanRules.MonumentSeats,
                    $"the room of spawn '{spawn.Id}' seats {seats} wool monuments, less than the {captured} wools its "
                    + "team captures", Subjects: [spawn.Id]));
        }
        return findings;
    }

    /// <summary>The interior the room <paramref name="id"/> is built with under the shell
    /// <paramref name="shells"/> binds for its kind — on any image of it — and what its house's stated cells
    /// refuse; null where the intent has no spawn or wool by that id.</summary>
    public static BuiltInterior? Interior(MapIntent intent, string id, RoomShells shells)
    {
        var wools = intent.Wools ?? [];
        if (intent.Spawns.FirstOrDefault(spawn => spawn.Id == id) is { } spawn)
            return Built("spawn", spawn.Id, spawn.House, WorldBuilder.SpawnRoom(spawn, shells.Spawn).Frame,
                         Captured(wools, spawn.Team));
        if (wools.FirstOrDefault(wool => wool.Id == id) is { } wool)
            return Built("wool", wool.Id, wool.House, WorldBuilder.WoolFrame(wool, shells.Wool), 0);
        return null;
    }

    /// <summary>The interior a room's <paramref name="house"/> holds inside <paramref name="frame"/>, and the
    /// refusals its stated cells raise: one off the floor or two on one cell, and fewer seats stated than the
    /// <paramref name="seated"/> wools the room must seat.</summary>
    private static BuiltInterior Built(string kind, string id, RoomHouse? house, RoomFrame frame, int seated)
    {
        var interior = RoomHouse.Interior(house, frame, out var refusal);
        var refused = new List<Finding>();
        if (refusal is not null) refused.Add(Refused(refusal, kind, id));
        var spawn = kind == "spawn";
        if (spawn && house?.Monuments is { } stated && stated.Count < seated)
            refused.Add(new Finding(RoomFrameRules.MonumentSeatsStated,
                $"the house of spawn '{id}' states {stated.Count} monument seats, less than the {seated} wools its "
                + "team captures", Field: "house.monuments", Subjects: [id]));
        return new BuiltInterior(frame, interior, seated, (spawn ? house?.Monuments : house?.Chests) is not null, refused);
    }

    private static int Captured(IReadOnlyList<WoolIntent> wools, string team) =>
        wools.Count(wool => wool.Monuments.Any(monument => monument.Team == team));

    private static bool Authored(StampId stamp) => string.IsNullOrEmpty(stamp.Kind) || stamp.Image == 0;

    private static Finding Refused(Finding refusal, string kind, string id) =>
        refusal with { Message = $"the {kind} room of '{id}' {refusal.Message}", Subjects = [id] };

    private static Finding Shifted(string kind, string id) =>
        new(RoomFrameRules.PadClearance,
            $"the {kind} room of '{id}' has its pad moved inward to keep 1 block from its walls",
            Severity.Complaint, Subjects: [id]);
}

/// <summary>The interior a room is built with, as <see cref="RoomCheck.Interior"/> answers it: the
/// <see cref="Frame"/> it stands in, its chests and seats, the wools it must seat, whether its house states the
/// list its kind holds — a wool room's chests, a spawn room's seats — and what the stated cells refuse.</summary>
public sealed record BuiltInterior(RoomFrame Frame, RoomInterior Interior, int Seated, bool Stated, Findings Refused);
