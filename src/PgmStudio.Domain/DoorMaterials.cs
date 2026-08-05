namespace PgmStudio.Domain;

/// <summary>What a room's doorway is filled with. A closed set, not a free material choice: the wool-room
/// block rule whitelists exactly the materials an attacker may break, so a door made of anything else seals
/// the cage — an entrance nobody can open, which no gate would catch.</summary>
public enum DoorMaterial
{
    /// <summary>An open doorway. The spawn cube's only choice — a player spawning in has to be able to walk
    /// straight out — and a legitimate cage style in its own right.</summary>
    Air,
    /// <summary>Cobweb: passable but slow, so the doorway costs an attacker time rather than a pickaxe.</summary>
    Web,
    /// <summary>Solid stained glass in the room's colour — seen through, not walked through.</summary>
    StainedGlass,
    /// <summary>Stained-glass panes in the room's colour. The shipped cage door.</summary>
    StainedGlassPane,
}

/// <summary>
/// The door vocabulary: for each choice, the block the stamper places and the PGM material the wool-room
/// block filter must name so an attacker can break it.
///
/// <para>Both halves live in one row on purpose, and this is the whole reason the table exists. The stamper
/// (<c>Minecraft</c>) reads the block; the filter generator (<c>Pgm</c>) reads the material name; the two
/// projects are siblings, so <c>Domain</c> is the lowest place both reach. Split across two lists they would
/// drift, and the drift is not cosmetic — a door whose material the filter does not name cannot be broken,
/// and a wool room with no way in is a map that cannot be played.</para>
///
/// <para>Block ids are the numeric 1.8 format, the same scale <see cref="BlockColors"/> documents.</para>
/// </summary>
public static class DoorMaterials
{
    /// <summary>One door choice. <paramref name="Coloured"/> takes the room's colour as its damage value;
    /// <paramref name="PgmMaterial"/> is null for <see cref="DoorMaterial.Air"/> alone — an opening is
    /// already open, so there is nothing for the filter to permit. <paramref name="Label"/> is the name a
    /// picker offers it under, here rather than in the client for the reason the rest of the row is: the set
    /// is served, never restated.</summary>
    public readonly record struct DoorChoice(
        DoorMaterial Material, string Slug, string Label, int BlockId, bool Coloured,
        string? PgmMaterial, string? FilterId);

    /// <summary>Every choice, in enum order — the authoring vocabulary, and the order the wool-room filter
    /// emits its breakable leaves in.</summary>
    public static readonly IReadOnlyList<DoorChoice> All =
    [
        new(DoorMaterial.Air, "air", "Open", 0, Coloured: false, null, null),
        new(DoorMaterial.Web, "web", "Cobweb", 30, Coloured: false, "web", "__wr-web"),
        new(DoorMaterial.StainedGlass, "stained-glass", "Stained glass", 95, Coloured: true, "stained glass", "__wr-glass"),
        new(DoorMaterial.StainedGlassPane, "stained-glass-pane", "Glass panes", 160, Coloured: true, "stained glass pane", "__wr-pane"),
    ];

    /// <summary>The choices the wool-room block filter has to whitelist — every one that places a block.</summary>
    public static IEnumerable<DoorChoice> Breakable => All.Where(choice => choice.PgmMaterial is not null);

    public static DoorChoice Of(DoorMaterial material) => All[(int)material];

    public static string Slug(DoorMaterial material) => All[(int)material].Slug;

    /// <summary>Resolve a slug, or false when it names no door. Never guesses: an unknown slug is an
    /// authoring error to report, not a value to silently replace with the default.</summary>
    public static bool TryParse(string? slug, out DoorMaterial material)
    {
        foreach (var choice in All)
            if (string.Equals(choice.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                material = choice.Material;
                return true;
            }
        material = DoorMaterial.StainedGlassPane;
        return false;
    }

    /// <summary>True when <paramref name="slug"/> names a door.</summary>
    public static bool IsKnown(string? slug) => TryParse(slug, out _);
}
