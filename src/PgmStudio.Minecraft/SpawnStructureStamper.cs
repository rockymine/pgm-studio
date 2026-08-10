using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>
/// Stamps a team's spawn structure (docs/contracts/sketch-world-export.md §2–3): the shell over its
/// <see cref="RoomFrame"/> (team colour, single open-air door on the frame's yaw-derived edge) plus its
/// auto-wired monuments, each stamped by <see cref="MonumentStamper"/>.
/// Seats come from <see cref="RoomFrames.MonumentSlots"/>: the door-wall corners, the back-wall corners,
/// then the back wall filling inward and the door wall (skipping the door opening) — so capacity scales
/// with the interior perimeter.
/// </summary>
public static class SpawnStructureStamper
{
    public static IReadOnlyList<PlacedMonument> Stamp(
        VoxelWorld world, RoomFrame frame, int floorY, int teamColor, IReadOnlyList<string> capturedWools,
        RoomStyle? style = null)
    {
        // A spawn's doorway is always open, whatever the bound style says: a player spawning in has to be able
        // to walk straight out, and the spawn protection rule already keeps enemies from walking in.
        CubeStamper.Stamp(world, frame, floorY, teamColor,
            (style ?? RoomStyle.Spawn) with { Door = DoorMaterial.Air });

        var slots = RoomFrames.MonumentSlots(frame, frame.Doors[0]);
        var placed = new List<PlacedMonument>();
        for (var i = 0; i < capturedWools.Count && i < slots.Count; i++)
        {
            var slot = slots[i];
            placed.Add(MonumentStamper.Place(world, slot.X, floorY, slot.Z, capturedWools[i], slot.Wall));
        }
        return placed;
    }
}
