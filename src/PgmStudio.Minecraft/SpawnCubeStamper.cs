using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>A monument placed in a spawn cube: the wool it captures + the world air-cell (where the wool
/// goes) so the exporter can wire the matching XML monument location.</summary>
public sealed record PlacedMonument(string WoolSlug, int X, int Y, int Z);

/// <summary>
/// Stamps a team's spawn cube (docs/contracts/sketch-world-export.md §2–3): the shared cube shell (team
/// colour, single open-air door facing <paramref name="doorFacing"/>) plus its auto-wired in-cube
/// monuments. Each monument = a bedrock pedestal (one block above the floor), an air placement cell, a
/// wool-colour stained-glass cap, and a label sign against the pedestal facing the room. Placement follows
/// the captured-wool count: 1–2 → the door-wall corners; 3–4 → also the back-wall corners; 5+ → a row
/// filling the back wall (overflowing to the door wall).
/// </summary>
public static class SpawnCubeStamper
{
    private readonly record struct Placement(int Lx, int Lz, Facing SignFacing, int SignDx, int SignDz);

    public static IReadOnlyList<PlacedMonument> Stamp(
        VoxelWorld world, int anchorX, int anchorZ, int floorY, int teamColor, Facing doorFacing,
        IReadOnlyList<string> capturedWools)
    {
        CubeStamper.Stamp(world, anchorX, anchorZ, floorY, teamColor, CubeKind.SpawnCube, doorFacing);

        var x0 = anchorX - 4;
        var z0 = anchorZ - 4;
        var placed = new List<PlacedMonument>();

        var placements = Placements(doorFacing, capturedWools.Count);
        for (var i = 0; i < capturedWools.Count && i < placements.Count; i++)
        {
            var p = placements[i];
            var slug = capturedWools[i];
            var color = WoolColors.WoolDamage(slug);
            var wx = x0 + p.Lx;
            var wz = z0 + p.Lz;

            world.SetBlock(wx, floorY + 1, wz, Blocks.Bedrock);                 // pedestal (elevated one block)
            // floorY + 2 is the air placement cell (wool goes here) — left air.
            world.SetBlock(wx, floorY + 3, wz, Blocks.StainedGlass, color);      // wool-colour cap

            SignBuilder.PlaceWallSign(world, wx + p.SignDx, floorY + 1, wz + p.SignDz, p.SignFacing, Label(slug));

            placed.Add(new PlacedMonument(slug, wx, floorY + 2, wz));
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

    // Ordered monument positions for a captured-wool count, given the door facing.
    private static List<Placement> Placements(Facing doorFacing, int count)
    {
        var axisIsZ = doorFacing is Facing.NegZ or Facing.PosZ;
        // The near-axis local coord of the door-wall corners vs the back-wall corners.
        var (doorNear, backNear) = doorFacing is Facing.NegZ or Facing.NegX ? (1, 6) : (6, 1);

        var result = new List<Placement>();
        if (count <= 4)
        {
            foreach (var near in (int[])[doorNear, backNear])
                foreach (var perp in (int[])[1, 6])
                    result.Add(Make(near, perp, axisIsZ));
            // Order: door corners first, then back corners.
            result = [.. result.OrderBy(p => Near(p, axisIsZ) == doorNear ? 0 : 1)];
        }
        else
        {
            foreach (var near in (int[])[backNear, doorNear])          // fill back wall, then overflow to door wall
                for (var perp = 1; perp <= 6; perp++)
                    result.Add(Make(near, perp, axisIsZ));
        }
        return [.. result.Take(count)];
    }

    private static int Near(Placement p, bool axisIsZ) => axisIsZ ? p.Lz : p.Lx;

    private static Placement Make(int near, int perp, bool axisIsZ)
    {
        var (lx, lz) = axisIsZ ? (perp, near) : (near, perp);
        // Sign faces the cube centre (perpendicular walls sit at local 0/7; centre is 3.5).
        var toward = near < 4 ? 1 : -1;
        var facing = axisIsZ
            ? (toward > 0 ? Facing.PosZ : Facing.NegZ)
            : (toward > 0 ? Facing.PosX : Facing.NegX);
        var (dx, dz) = axisIsZ ? (0, toward) : (toward, 0);
        return new Placement(lx, lz, facing, dx, dz);
    }
}
