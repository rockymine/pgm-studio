using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>
/// A team's spawn structure, named by its parts the same way a <see cref="WoolStructure"/> is.
///
/// <para>The <b>spawn point</b> is the room — a pad in the team's colour is what a player comes into the world
/// on, and everything else is optional around it. The <see cref="Shell"/> may be left off for a spawn on open
/// ground, and <see cref="CapturedWools"/> may be empty for a team with nothing to capture there.</para>
///
/// <para>A spawn's doorway is always open whatever the bound style says, because a player spawning in has to
/// be able to walk straight out and the spawn-protection rule already keeps enemies from walking in.</para>
/// </summary>
public sealed record SpawnStructure
{
    public required RoomFrame Frame { get; init; }

    public required int FloorY { get; init; }

    /// <summary>The team's colour, on the 0–15 wool scale — the pad's own material and the tint channel every
    /// team-tinted course in the shell reads.</summary>
    public required int TeamColor { get; init; }

    /// <summary>The terrain it stands on, or null to leave the ground as it is — a spawn on a plateau the plan
    /// already raised needs no foundation of its own.</summary>
    public IReadOnlyDictionary<(int X, int Z), int>? Ground { get; init; }

    /// <summary>The wools this team must capture, one monument seat each, in
    /// <see cref="RoomFrames.MonumentSlots"/> order: the door-wall corners, the back-wall corners, then the
    /// back wall filling inward and the door wall — so capacity scales with the interior perimeter.</summary>
    public IReadOnlyList<string> CapturedWools { get; init; } = [];

    /// <summary>The building around the spawn, or null for a spawn on open ground. A house style rather than
    /// a room style, for the reason a wool structure's is: this is the stamping shape.</summary>
    public HouseStyle? Shell { get; init; } = HouseStyle.Spawn;
}

/// <summary>What one spawn structure placed: where the team comes in, and every monument it carries.</summary>
public sealed record PlacedSpawn(PlacedPlayerSpawn Point, IReadOnlyList<PlacedMonument> Monuments);

/// <summary>Stamps a <see cref="SpawnStructure"/> (docs/world-export/sketch-world-export.md §2–3).</summary>
public static class SpawnStructureStamper
{
    public static PlacedSpawn Stamp(VoxelWorld world, SpawnStructure room)
    {
        var frame = room.Frame;
        if (room.Ground is { } ground)
            StructureStamper.StampFoundation(world, ground, frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ);

        if (room.Shell is { } shell)
            HouseStamper.Stamp(world, frame, room.FloorY,
                shell with { Doorway = shell.Doorway with { Door = DoorMaterial.Air } }, room.TeamColor);

        // After the shell, so the pad is the floor the spawn point sits on rather than whatever a style laid.
        var point = PlayerSpawnStamper.Place(world, frame.Pad, room.FloorY, room.TeamColor);

        var slots = frame.Doors.Count > 0 ? RoomFrames.MonumentSlots(frame, frame.Doors[0]) : [];
        var monuments = new List<PlacedMonument>();
        for (var i = 0; i < room.CapturedWools.Count && i < slots.Count; i++)
        {
            var slot = slots[i];
            monuments.Add(MonumentStamper.Place(world, slot.X, room.FloorY, slot.Z,
                                                room.CapturedWools[i], slot.Wall));
        }
        return new PlacedSpawn(point, monuments);
    }
}
