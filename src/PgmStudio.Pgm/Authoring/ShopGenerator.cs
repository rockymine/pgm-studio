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

        // A menu is written only where it can load: PGM refuses a shop with no category and a category with
        // no icon, so a shop whose tabs all fall away is left out rather than written unloadable.
        var stated = shops
            .Where(shop => shop.Id.Trim().Length > 0)
            .Select(shop => (Shop: shop, Categories: Categories(shop)))
            .Where(menu => menu.Categories.Count > 0)
            .ToList();
        if (stated.Count == 0) return;

        doc["shops"] = stated.Select(menu => (object?)Shop(menu.Shop, menu.Categories)).ToList();

        var keepers = new List<object?>();
        // A keeper naming its own place stands there once; the rest are derived, one per shop per spawn, and
        // rank apart at each spawn so two menus do not share a block.
        foreach (var (shop, _) in stated)
            if (shop.Keeper is { Stands: true } placed) keepers.Add(Standing(shop, placed));
        foreach (var spawn in intent.Spawns)
        {
            var rank = 0;
            foreach (var (shop, _) in stated)
            {
                if (shop.Keeper is not { Stands: false } keeper) continue;
                keepers.Add(Keeper(shop, keeper, spawn, rank++));
            }
        }
        if (keepers.Count > 0) doc["shopkeepers"] = keepers;
    }

    private static Dict Shop(ShopIntent shop, List<object?> categories)
    {
        var menu = new Dict { ["id"] = IntentNaming.Slug(shop.Id), ["categories"] = categories };
        if (shop.Name.Length > 0) menu["name"] = shop.Name;
        return menu;
    }

    /// <summary>A keeper standing where the board said: on the centre of the block it named, or in the region
    /// it named. One keeper, because a place is a place — a shop building in the middle of a board is one
    /// shop for everybody rather than a villager per spawn.</summary>
    private static Dict Standing(ShopIntent shop, ShopkeeperIntent keeper)
    {
        var entry = new Dict { ["shop"] = IntentNaming.Slug(shop.Id) };
        if (keeper.At is { } at)
            entry["location"] = new Dict { ["x"] = Centre(at.X), ["y"] = at.Y, ["z"] = Centre(at.Z) };
        else
            entry["region"] = keeper.Region.Trim();
        if (keeper.Yaw is { } yaw) entry["yaw"] = yaw;
        Label(entry, keeper);
        return entry;
    }

    /// <summary>The tabs a shop is written with: those carrying an id, a stack to draw and at least one thing
    /// to buy. PGM refuses a category with no <c>&lt;item&gt;</c> child, so a tab whose icons all fall away is
    /// left out for the same reason a shop with no tab is — a menu that cannot load is worse than one that is
    /// not there.</summary>
    private static List<object?> Categories(ShopIntent shop) =>
    [
        .. shop.Categories
            .Where(category => category.Id.Trim().Length > 0 && category.Material.Trim().Length > 0)
            .Select(category => (Category: category, Icons: category.Items
                .Where(item => item.Material.Trim().Length > 0).Select(Icon).ToList<object?>()))
            .Where(tab => tab.Icons.Count > 0)
            .Select(tab =>
            {
                var icon = new Dict { ["material"] = tab.Category.Material.Trim() };
                if (tab.Category.Name.Length > 0) icon["name"] = tab.Category.Name;
                return (object?)new Dict
                {
                    ["id"] = IntentNaming.Slug(tab.Category.Id),
                    ["icon"] = icon,
                    ["icons"] = tab.Icons,
                };
            }),
    ];

    /// <summary>
    /// One purchasable: the stack the menu draws, every payment it costs, and what buying it does.
    ///
    /// <para>Payments are written as stated and in order. PGM takes every one of them, so two entries are a
    /// price in two currencies at once; the writer decides the spelling — one payment rides on the icon
    /// element and several become <c>&lt;payment&gt;</c> children — because an element can only state one of
    /// each attribute. An icon with no payment is free, which is what an empty list already means to PGM.</para>
    /// </summary>
    private static object? Icon(ShopItemIntent item)
    {
        var stack = new Dict { ["material"] = item.Material.Trim() };
        if (item.Amount != 1) stack["amount"] = item.Amount;
        if (item.Name.Length > 0) stack["name"] = item.Name;
        if (item.TeamColor) stack["team_color"] = true;

        var icon = new Dict { ["item"] = stack };
        if (item.Action.Trim().Length > 0) icon["action"] = item.Action.Trim();

        var payments = item.Payments
            .Where(payment => payment.Price > 0 || payment.Currency.Trim().Length > 0)
            .Select(payment =>
            {
                var paid = new Dict { ["price"] = payment.Price, ["currency"] = payment.Currency.Trim() };
                if (payment.Color.Trim().Length > 0) paid["color"] = payment.Color.Trim();
                return (object?)paid;
            }).ToList();
        if (payments.Count > 0) icon["payments"] = payments;
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
            // Facing back along the offset, so the keeper looks at the player who has just arrived — unless
            // the board stated a facing of its own.
            ["yaw"] = keeper.Yaw ?? Yaw(-stepX, -stepZ),
            ["location"] = new Dict
            {
                ["x"] = baseX + stepX * blocks, ["y"] = spawn.Point.Y, ["z"] = baseZ + stepZ * blocks,
            },
        };
        Label(entry, keeper);
        return entry;
    }

    /// <summary>What the entity is called and what it is — the two statements every keeper makes wherever it
    /// stands. Each is left off where the board says nothing, so PGM's own defaults answer: the shop's id in
    /// grey, on a villager.</summary>
    private static void Label(Dict entry, ShopkeeperIntent keeper)
    {
        if (keeper.Name.Length > 0) entry["name"] = keeper.Name;
        if (keeper.Mob.Trim().Length > 0) entry["mob"] = keeper.Mob.Trim();
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
