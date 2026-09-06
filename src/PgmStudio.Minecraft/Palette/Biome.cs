namespace PgmStudio.Minecraft.Palette;

/// <summary>
/// The biome ids a chunk's <c>Biomes</c> array carries, by name. A biome places nothing and costs nothing: it
/// is the byte the client reads to tint grass, leaves, vines and water, so a board painted with them varies in
/// colour without a single extra block.
///
/// <para>Not the whole 1.8 table — the ones listed are the ones whose tint an author would reach for, and a
/// field may name any id whatever is here. What a name buys is a document that reads.</para>
/// </summary>
public static class Biome
{
    /// <summary>The default every chunk carries where nothing else is said, and the tint every static render
    /// in the studio already assumes.</summary>
    public const byte Plains = 1;

    public const byte Desert = 2;
    public const byte ExtremeHills = 3;
    public const byte Forest = 4;
    public const byte Taiga = 5;
    public const byte Swampland = 6;
    public const byte River = 7;
    public const byte FrozenRiver = 11;
    public const byte IcePlains = 12;
    public const byte MushroomIsland = 14;
    public const byte Jungle = 21;
    public const byte BirchForest = 27;
    public const byte RoofedForest = 29;
    public const byte ColdTaiga = 30;
    public const byte Savanna = 35;
    public const byte Mesa = 37;

    /// <summary>The named ids, in the order a picker offers them — the plain grounds first, then the cold
    /// ones, then the odd ones. A field may state any id whatever is here; what a name buys is a document
    /// that reads and a control an author can use.</summary>
    public static readonly (byte Id, string Name)[] All =
    [
        (Plains, "Plains"),
        (Forest, "Forest"),
        (BirchForest, "Birch forest"),
        (RoofedForest, "Roofed forest"),
        (Taiga, "Taiga"),
        (ExtremeHills, "Extreme hills"),
        (Savanna, "Savanna"),
        (Desert, "Desert"),
        (Mesa, "Mesa"),
        (Jungle, "Jungle"),
        (Swampland, "Swampland"),
        (River, "River"),
        (IcePlains, "Ice plains"),
        (ColdTaiga, "Cold taiga"),
        (FrozenRiver, "Frozen river"),
        (MushroomIsland, "Mushroom island"),
    ];

    /// <summary>What an author reads the id as, or the number itself for one no name claims.</summary>
    public static string NameOf(byte id)
    {
        foreach (var (named, name) in All) if (named == id) return name;
        return $"Biome {id}";
    }
}
