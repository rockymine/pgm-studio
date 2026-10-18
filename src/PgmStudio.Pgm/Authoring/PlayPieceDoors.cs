using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// A room's doors, by the room's id: set whole, and read as the room is built. The named image takes the doors as
/// posted, and unless <c>alone</c> every other image of its unit takes them carried through the symmetry step
/// between them, each point mapped and each wall turned. Refused where no door is posted, a door names no wall,
/// states half a point, or stands outside the room's region.
/// </summary>
public static class PlayPieceDoors
{
    /// <summary>The intent with the doors of the room <paramref name="id"/> made <paramref name="doors"/>, or the
    /// findings that stopped it and a null intent.</summary>
    public static (MapIntent? Intent, Findings Refused) Apply(
        MapIntent intent, string id, IReadOnlyList<StatedDoor> doors, bool alone = false, SymmetryIntent? board = null)
    {
        var symmetry = intent.Symmetry ?? board;
        if (intent.Spawns.FindIndex(spawn => spawn.Id == id) is var spawnAt and >= 0)
        {
            if (Refused(doors, intent.Spawns[spawnAt].Protection, id) is { } refused) return (null, refused);
            return (intent with
            {
                Spawns = PlayPieceMove.Images(intent.Spawns, spawnAt, entry => entry.Stamp, symmetry?.Mode, alone,
                    (entry, step) => entry with { Doors = Carried(doors, symmetry, step) }),
            }, Findings.None);
        }
        var wools = intent.Wools ?? [];
        if (wools.FindIndex(wool => wool.Id == id) is var woolAt and >= 0)
        {
            if (Refused(doors, wools[woolAt].Protection, id) is { } refused) return (null, refused);
            return (intent with
            {
                Wools = PlayPieceMove.Images(wools, woolAt, entry => entry.Stamp, symmetry?.Mode, alone,
                    (entry, step) => entry with { Doors = Carried(doors, symmetry, step) }),
            }, Findings.None);
        }
        return (null, NoSuchRoom(id));
    }

    /// <summary>The doors the room <paramref name="id"/> is built with over <paramref name="layoutJson"/>, and
    /// whether it states them: a spawn stating none opens on the wall its yaw leans into, a wool stating none on
    /// the side its ground gives it. Null with the finding where the map has no such room.</summary>
    public static (List<StatedDoor>? Doors, bool Stated, Findings Refused) Built(MapIntent intent, string id, string? layoutJson)
    {
        if (intent.Spawns.FirstOrDefault(spawn => spawn.Id == id) is { } spawn)
            return spawn.Doors.Count > 0
                ? (spawn.Doors, true, Findings.None)
                : ([.. spawn.AskedDoors().Select(door => StatedDoor.Centred(door.Edge))], false, Findings.None);
        if ((intent.Wools ?? []).FirstOrDefault(wool => wool.Id == id) is { } wool)
        {
            if (wool.Doors.Count > 0) return (wool.Doors, true, Findings.None);
            var settled = layoutJson is null
                ? [StatedDoor.Centred(WoolDoors.Side(wool.Protection, _ => false, (0, 0)))]
                : WoolDoors.Settled(intent with { Wools = [wool] }, layoutJson).Wools![0].Doors;
            return (settled, false, Findings.None);
        }
        return (null, false, NoSuchRoom(id));
    }

    private static Findings NoSuchRoom(string id) => Findings.Of(new Finding(RequestRules.NoSuchSubject,
        $"the map has no spawn or wool room with the id '{id}'", Field: "id", Subjects: [id]));

    private static Findings? Refused(IReadOnlyList<StatedDoor> doors, IReadOnlyList<Rect> region, string id)
    {
        if (doors.Count == 0)
            return Findings.Of(new Finding(RequestRules.Unreadable, "a room opens through at least one door", Field: "doors"));
        for (var at = 0; at < doors.Count; at++)
        {
            var door = doors[at];
            if (RoomEdges.OfWord(door.Edge) is null)
                return Findings.Of(new Finding(RequestRules.Unreadable,
                    $"door {at} names the edge '{door.Edge}', which is not one of {string.Join(", ", EdgeWords.All)}",
                    Field: $"doors[{at}].edge"));
            if (door.X is null != door.Z is null)
                return Findings.Of(new Finding(RequestRules.Unreadable,
                    $"door {at} states one coordinate of its point without the other", Field: $"doors[{at}]"));
            if (door.X is { } x && door.Z is { } z && region.Count > 0
                && !region.Any(rect => x >= rect.MinX && x <= rect.MaxX && z >= rect.MinZ && z <= rect.MaxZ))
                return Findings.Of(new Finding(RequestRules.Unreadable,
                    $"door {at} of '{id}' at ({x:0.#}, {z:0.#}) stands outside the region of its room",
                    Field: $"doors[{at}]", Subjects: [id]));
        }
        return null;
    }

    /// <summary>The doors carried <paramref name="step"/> images round the orbit.</summary>
    private static List<StatedDoor> Carried(IReadOnlyList<StatedDoor> doors, SymmetryIntent? symmetry, int step) =>
        step == 0 || symmetry is null ? [.. doors] : [.. doors.Select(door => door.Fanned(symmetry.Mode, symmetry.CenterX, symmetry.CenterZ, step))];
}
