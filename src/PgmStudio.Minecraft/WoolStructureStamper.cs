using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>
/// A wool structure, named by the parts a wool room is made of rather than assembled at each call site.
///
/// <para><b>Three of them are the room and are always stamped:</b> the foundation it stands on, the wool
/// itself, and — where the plan gives one — the entrance line marking its approach. A room missing the
/// foundation or the wool is not a wool room; it is a shed, or a decoration.</para>
///
/// <para><b>Two are dressing and may be left off:</b> the <see cref="Shell"/> around it and the
/// <see cref="Chests"/> inside it. No shell is wool standing on open ground, which a map may want; no chests
/// is a room whose region hands a kit at the door instead of stocking a corner. Both default on, because that
/// is what a wool room usually is.</para>
///
/// <para>The foundation and the entrance line carry their own geometry rather than deriving it here: a
/// foundation needs the terrain's surface to know how deep to reach, and an entrance line is a plan fact — it
/// runs along the piece's frontline edge and is fanned across the symmetry orbit, neither of which a room can
/// see from its own footprint.</para>
/// </summary>
public sealed record WoolStructure
{
    public required RoomFrame Frame { get; init; }

    /// <summary>The course players walk on inside it.</summary>
    public required int FloorY { get; init; }

    /// <summary>Which wool this room gives out — the colour of its pad, and what the XML names.</summary>
    public required string WoolSlug { get; init; }

    /// <summary>The terrain it stands on: the foundation fills bedrock from y=0 to the surface under the
    /// footprint, so the room cannot be tunnelled into from below.</summary>
    public required IReadOnlyDictionary<(int X, int Z), int> Ground { get; init; }

    /// <summary>The entrance row, as a pair of block ends sharing an axis.</summary>
    public (int X1, int Z1, int X2, int Z2)? Entrance { get; init; }

    /// <summary>The building around the wool, or null for wool on open ground. A house style rather than a
    /// room style, because this is the stamping shape: a stored room style reaches it through
    /// <see cref="RoomStyle.AsHouse"/>, and a caller that wants a gabled roof over its wool can simply say
    /// so.</summary>
    public HouseStyle? Shell { get; init; } = RoomStyle.Wool.AsHouse();

    public bool Chests { get; init; } = true;
}

/// <summary>Stamps a <see cref="WoolStructure"/> (docs/contracts/sketch-world-export.md §2).</summary>
public static class WoolStructureStamper
{
    public static PlacedWoolSpawn Stamp(VoxelWorld world, WoolStructure room)
    {
        var frame = room.Frame;
        StructureStamper.StampFoundation(world, room.Ground, frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ);

        if (room.Shell is { } shell)
            HouseStamper.Stamp(world, frame, room.FloorY, shell, BlockColors.BlockDamage(room.WoolSlug));

        // After the shell, so the pad is the floor the room's point sits on rather than whatever a style laid.
        var placed = WoolSpawnStamper.Place(world, frame.Pad, room.FloorY, room.WoolSlug);

        if (room.Chests) WoolChests.Stamp(world, frame, room.FloorY);
        if (room.Entrance is { } line)
            StructureStamper.StampRedstoneLine(world, room.Ground, line.X1, line.Z1, line.X2, line.Z2);

        return placed;
    }
}
