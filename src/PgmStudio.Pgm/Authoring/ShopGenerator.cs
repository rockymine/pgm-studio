using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Shop slice of the declarative generator: the <c>&lt;shops&gt;</c> catalogue the intent states, and one
/// <c>&lt;shopkeeper&gt;</c> per shop at every team's spawn.
///
/// <para><b>It stamps nothing.</b> A shopkeeper is an entity PGM spawns itself at match load, frozen where
/// the XML puts it, so the whole of one is an element — no blocks, no entity data in the world, and nothing
/// for the export path to resolve. That is what lets this slice run beside the objective generators instead
/// of waiting for the world build the way a capture point's pad does.</para>
///
/// <para><b>Where a keeper stands is derived rather than authored.</b> The intent says which shop a keeper
/// opens and what it is; the studio puts it on the spawn's own floor, beside the point players arrive on and
/// turned to face them. A team that has to reach a shop between lives reaches it at its spawn, and a
/// plan-compiled intent carries no other place that is reliably indoors, reliably level and reliably the
/// team's own. Several shops flank the spawn point alternately, and each keeper is held inside the spawn's
/// room so one never lands in a wall.</para>
///
/// <para>Idempotent clear-then-build, like every other slice: a board that states no shop leaves none behind.</para>
/// </summary>
public static class ShopGenerator
{
    /// <summary>How far from the spawn point the first keeper stands, across the way players face. Two
    /// blocks: one is close enough to be in the way of a player turning round, and the room a spawn piece
    /// opens is not wide enough to promise more.</summary>
    private const int FirstOffset = 2;

    /// <summary>How much further out each further keeper stands, so two shops at one spawn do not share a
    /// block.</summary>
    private const int OffsetStep = 2;

    public static void Apply(Dict doc, MapIntent intent)
    {
        doc.Remove("shops");
        doc.Remove("shopkeepers");
        if (intent.Shops is not { Count: > 0 } shops) return;

        var stated = shops.Where(shop => shop.Id.Trim().Length > 0 && shop.Categories.Count > 0).ToList();
        if (stated.Count == 0) return;

        doc["shops"] = stated.Select(Shop).ToList<object?>();

        var keepers = new List<object?>();
        foreach (var spawn in intent.Spawns)
        {
            var rank = 0;
            foreach (var shop in stated)
            {
                if (shop.Keeper is not { } keeper) continue;
                keepers.Add(Keeper(shop, keeper, spawn, rank++));
            }
        }
        if (keepers.Count > 0) doc["shopkeepers"] = keepers;
    }

    private static Dict Shop(ShopIntent shop)
    {
        var categories = shop.Categories
            .Where(category => category.Id.Trim().Length > 0 && category.Material.Trim().Length > 0)
            .Select(category =>
            {
                var icon = new Dict { ["material"] = category.Material.Trim() };
                if (category.Name.Length > 0) icon["name"] = category.Name;
                return (object?)new Dict
                {
                    ["id"] = IntentNaming.Slug(category.Id),
                    ["icon"] = icon,
                    ["icons"] = category.Items
                        .Where(item => item.Material.Trim().Length > 0)
                        .Select(Icon).ToList<object?>(),
                };
            }).ToList();

        var menu = new Dict { ["id"] = IntentNaming.Slug(shop.Id), ["categories"] = categories };
        if (shop.Name.Length > 0) menu["name"] = shop.Name;
        return menu;
    }

    private static object? Icon(ShopItemIntent item)
    {
        var stack = new Dict { ["material"] = item.Material.Trim() };
        if (item.Amount != 1) stack["amount"] = item.Amount;
        if (item.Name.Length > 0) stack["name"] = item.Name;
        if (item.TeamColor) stack["team_color"] = true;

        var icon = new Dict { ["item"] = stack };
        // A price is one payment even when it is free: an icon that states neither price nor currency is one
        // PGM hands over for nothing, and that is what an empty payment list already says.
        if (item.Price > 0 || item.Currency.Trim().Length > 0)
            icon["payments"] = new List<object?>
            {
                new Dict { ["price"] = item.Price, ["currency"] = item.Currency.Trim() },
            };
        return icon;
    }

    /// <summary>
    /// One keeper, standing on the spawn's floor beside the point players arrive on and turned to face it.
    /// The side alternates with <paramref name="rank"/> so a second shop at the same spawn stands opposite
    /// the first rather than inside it, and the distance is clamped to the spawn's room so a keeper never
    /// lands in a wall.
    /// </summary>
    private static Dict Keeper(ShopIntent shop, ShopkeeperIntent keeper, SpawnIntent spawn, int rank)
    {
        var (facingX, facingZ) = Cardinal(spawn.Yaw);
        var (acrossX, acrossZ) = (facingZ, -facingX);
        var side = rank % 2 == 0 ? 1 : -1;
        var (stepX, stepZ) = (side * acrossX, side * acrossZ);

        // Blocks are counted from the one the spawn point stands in, so the two sides of it are the same
        // distance out and every keeper lands on a block centre — PGM spawns the entity exactly where the
        // point says, and a whole number puts it on a corner with half of it in the next block.
        var (baseX, baseZ) = (Centre(spawn.Point.X), Centre(spawn.Point.Z));
        var blocks = Reach(spawn, baseX, baseZ, stepX, stepZ, FirstOffset + rank / 2 * OffsetStep);

        var entry = new Dict
        {
            ["shop"] = IntentNaming.Slug(shop.Id),
            // Facing back along the offset, so the keeper looks at the player who has just arrived.
            ["yaw"] = Yaw(-stepX, -stepZ),
            ["location"] = new Dict
            {
                ["x"] = baseX + stepX * blocks, ["y"] = spawn.Point.Y, ["z"] = baseZ + stepZ * blocks,
            },
        };
        if (keeper.Name.Length > 0) entry["name"] = keeper.Name;
        if (keeper.Mob.Trim().Length > 0) entry["mob"] = keeper.Mob.Trim();
        return entry;
    }

    /// <summary>
    /// How many blocks along <paramref name="stepX"/>/<paramref name="stepZ"/> a keeper may stand: the
    /// wanted distance, cut back to the room's innermost floor block where the room is too narrow for it,
    /// and never less than one — a keeper on the spawn point itself would be standing in the players.
    /// <para>The room is the stated footprint less the course its wall stands in, or the protection ground
    /// less that wall and the clean ring a piece keeps outside it (<c>WX1</c>); a spawn that states neither
    /// is a hand-authored one with no room to be held inside. Its corners are block indices and both ends
    /// are inside it, so the last centre a keeper may stand on is half a block past the far corner.</para>
    /// </summary>
    private static double Reach(SpawnIntent spawn, double x, double z, double stepX, double stepZ, int wanted)
    {
        if (Room(spawn) is not { } room) return wanted;
        var limit = (double)wanted;
        if (stepX != 0) limit = Math.Min(limit, stepX > 0 ? room.MaxX + 0.5 - x : x - room.MinX - 0.5);
        if (stepZ != 0) limit = Math.Min(limit, stepZ > 0 ? room.MaxZ + 0.5 - z : z - room.MinZ - 0.5);
        return Math.Max(1, Math.Floor(limit));
    }

    private static Rect? Room(SpawnIntent spawn)
    {
        if (spawn.Footprint is { } footprint) return Inset(footprint, 1);
        if (spawn.Protection.Count == 0) return null;
        var bounds = new Rect(
            spawn.Protection.Min(rect => rect.MinX), spawn.Protection.Min(rect => rect.MinZ),
            spawn.Protection.Max(rect => rect.MaxX), spawn.Protection.Max(rect => rect.MaxZ));
        return Inset(bounds, 2);
    }

    private static Rect Inset(Rect rect, double by) =>
        new(rect.MinX + by, rect.MinZ + by, rect.MaxX - by, rect.MaxZ - by);

    /// <summary>The unit facing a yaw states, snapped to the nearest of the four compass directions. A hall
    /// that opens on a corner faces between two of them, and a keeper placed on that diagonal would sit off
    /// the block lattice the room is built on.</summary>
    private static (double X, double Z) Cardinal(double yaw)
    {
        var quarter = ((int)Math.Round(yaw / 90) % 4 + 4) % 4;
        return quarter switch { 0 => (0, 1), 1 => (-1, 0), 2 => (0, -1), _ => (1, 0) };
    }

    /// <summary>The yaw that looks along a direction — Minecraft's, where 0 is +z and the angle runs the
    /// other way round from the mathematical one.</summary>
    private static double Yaw(double x, double z) => Math.Round(Math.Atan2(-x, z) * 180 / Math.PI);

    /// <summary>The centre of the block a coordinate falls in.</summary>
    private static double Centre(double v) => Math.Floor(v) + 0.5;
}
