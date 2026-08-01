using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>A monument placed in a spawn cube: the wool it captures + the world air-cell (where the wool
/// goes) so the exporter can wire the matching XML monument location.</summary>
public sealed record PlacedMonument(string WoolSlug, int X, int Y, int Z);

/// <summary>
/// Stamps a team's spawn cube (docs/contracts/sketch-world-export.md §2–3): the shared shell over its
/// <see cref="RoomFrame"/> (team colour, single open-air door on the frame's yaw-derived edge) plus its
/// auto-wired in-cube monuments. Each monument = a bedrock pedestal (one block above the floor), an air
/// placement cell, a wool-colour stained-glass cap, and a label sign against the pedestal facing the room.
/// Seats come from <see cref="RoomFrames.MonumentSlots"/>: the door-wall corners, the back-wall corners,
/// then the back wall filling inward and the door wall (skipping the door opening) — so capacity scales
/// with the interior perimeter.
/// </summary>
public static class SpawnCubeStamper
{
    public static IReadOnlyList<PlacedMonument> Stamp(
        VoxelWorld world, RoomFrame frame, int floorY, int teamColor, IReadOnlyList<string> capturedWools)
    {
        CubeStamper.Stamp(world, frame, floorY, teamColor, CubeKind.SpawnCube);

        var slots = RoomFrames.MonumentSlots(frame, frame.Doors[0]);
        var placed = new List<PlacedMonument>();
        for (var i = 0; i < capturedWools.Count && i < slots.Count; i++)
        {
            var slot = slots[i];
            var slug = capturedWools[i];
            var color = WoolColors.WoolDamage(slug);

            world.SetBlock(slot.X, floorY + 1, slot.Z, Blocks.Bedrock);                 // pedestal (elevated one block)
            // floorY + 2 is the air placement cell (wool goes here) — left air.
            world.SetBlock(slot.X, floorY + 3, slot.Z, Blocks.StainedGlass, color);      // wool-colour cap

            // The sign hangs one cell toward the room centre, its face turned back at the pedestal's wall.
            var (signDx, signDz, signFacing) = slot.Wall switch
            {
                RoomEdge.NegZ => (0, 1, RoomEdge.PosZ),
                RoomEdge.PosZ => (0, -1, RoomEdge.NegZ),
                RoomEdge.NegX => (1, 0, RoomEdge.PosX),
                _ => (-1, 0, RoomEdge.NegX),
            };
            SignBuilder.PlaceWallSign(world, slot.X + signDx, floorY + 1, slot.Z + signDz, signFacing, Label(slug));

            placed.Add(new PlacedMonument(slug, slot.X, floorY + 2, slot.Z));
        }
        return placed;
    }

    /// <summary>The monument label: <c>Place the / {Colour} (bold) / Wool / here!</c>, coloured per wool.</summary>
    private static IReadOnlyList<SignLine> Label(string slug)
    {
        var chat = SignBuilder.ChatColor(slug);
        return
        [
            new SignLine("Place the"),
            new SignLine(DisplayName(slug), chat, Bold: true),
            new SignLine("Wool", chat),
            new SignLine("here!"),
        ];
    }

    private static string DisplayName(string slug)
        => string.Join(' ', slug.Split('_').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));
}
