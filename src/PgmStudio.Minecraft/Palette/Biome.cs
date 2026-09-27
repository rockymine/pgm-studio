namespace PgmStudio.Minecraft.Palette;

/// <summary>One biome a chunk may carry: the id its <c>Biomes</c> array stores, what an author reads it as, and
/// the three multipliers the client applies under it — to grass, to leaves and vines, and to water.</summary>
public readonly record struct BiomeRow(byte Id, string Name, uint Grass, uint Foliage, uint Water);

/// <summary>
/// Every biome 1.8 can store, by name and by what it does to colour. A biome places nothing and costs nothing:
/// it is the byte the client reads to tint grass, leaves, vines and water, so a board painted with them varies
/// in colour without a single extra block.
///
/// <para>The colours are the 1.8 grass and foliage colour maps read at each biome's own temperature and
/// rainfall. Three biomes override the maps: mesa states its grass and foliage outright, swampland states
/// both and tints water, and roofed forest darkens its grass by averaging it with a fixed brown. A mutated
/// biome (an id past 128) takes its parent's colours, which is what the game does.</para>
///
/// <para>Many biomes share one colour — plains and beach, the eight frozen and cold ones, every savanna and desert — and
/// all of them are listed anyway, so a board may name the biome it means; <see cref="SharingTint"/> says which
/// are the same on the ground.</para>
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
    public const byte ColdBeach = 26;
    public const byte BirchForest = 27;
    public const byte RoofedForest = 29;
    public const byte ColdTaiga = 30;
    public const byte Savanna = 35;
    public const byte Mesa = 37;
    public const byte SwamplandM = 134;

    private const uint White = 0xFFFFFF;

    /// <summary>Every biome, in the order the game numbers them.</summary>
    public static readonly BiomeRow[] All =
    [
        new(0, "Ocean", 0x8EB971, 0x71A74D, White),
        new(1, "Plains", 0x91BD59, 0x77AB2F, White),
        new(2, "Desert", 0xBFB755, 0xAEA42A, White),
        new(3, "Extreme hills", 0x8AB689, 0x6DA36B, White),
        new(4, "Forest", 0x79C05A, 0x59AE30, White),
        new(5, "Taiga", 0x86B783, 0x68A464, White),
        new(6, "Swampland", 0x6A7039, 0x6A7039, 0xE0FFAE),
        new(7, "River", 0x8EB971, 0x71A74D, White),
        new(8, "Hell", 0xBFB755, 0xAEA42A, White),
        new(9, "The end", 0x8EB971, 0x71A74D, White),
        new(10, "Frozen ocean", 0x80B497, 0x60A17B, White),
        new(11, "Frozen river", 0x80B497, 0x60A17B, White),
        new(12, "Ice plains", 0x80B497, 0x60A17B, White),
        new(13, "Ice mountains", 0x80B497, 0x60A17B, White),
        new(14, "Mushroom island", 0x55C93F, 0x2BBB0F, White),
        new(15, "Mushroom island shore", 0x55C93F, 0x2BBB0F, White),
        new(16, "Beach", 0x91BD59, 0x77AB2F, White),
        new(17, "Desert hills", 0xBFB755, 0xAEA42A, White),
        new(18, "Forest hills", 0x79C05A, 0x59AE30, White),
        new(19, "Taiga hills", 0x86B783, 0x68A464, White),
        new(20, "Extreme hills edge", 0x8AB689, 0x6DA36B, White),
        new(21, "Jungle", 0x59C93C, 0x30BB0B, White),
        new(22, "Jungle hills", 0x59C93C, 0x30BB0B, White),
        new(23, "Jungle edge", 0x64C73F, 0x3EB80F, White),
        new(24, "Deep ocean", 0x8EB971, 0x71A74D, White),
        new(25, "Stone beach", 0x8AB689, 0x6DA36B, White),
        new(26, "Cold beach", 0x83B593, 0x64A278, White),
        new(27, "Birch forest", 0x88BB67, 0x6BA941, White),
        new(28, "Birch forest hills", 0x88BB67, 0x6BA941, White),
        new(29, "Roofed forest", 0x507A32, 0x59AE30, White),
        new(30, "Cold taiga", 0x80B497, 0x60A17B, White),
        new(31, "Cold taiga hills", 0x80B497, 0x60A17B, White),
        new(32, "Mega taiga", 0x86B87F, 0x68A55F, White),
        new(33, "Mega taiga hills", 0x86B87F, 0x68A55F, White),
        new(34, "Extreme hills+", 0x8AB689, 0x6DA36B, White),
        new(35, "Savanna", 0xBFB755, 0xAEA42A, White),
        new(36, "Savanna plateau", 0xBFB755, 0xAEA42A, White),
        new(37, "Mesa", 0x90814D, 0x9E814D, White),
        new(38, "Mesa plateau F", 0x90814D, 0x9E814D, White),
        new(39, "Mesa plateau", 0x90814D, 0x9E814D, White),
        new(129, "Sunflower plains", 0x91BD59, 0x77AB2F, White),
        new(130, "Desert M", 0xBFB755, 0xAEA42A, White),
        new(131, "Extreme hills M", 0x8AB689, 0x6DA36B, White),
        new(132, "Flower forest", 0x79C05A, 0x59AE30, White),
        new(133, "Taiga M", 0x86B783, 0x68A464, White),
        new(134, "Swampland M", 0x6A7039, 0x6A7039, 0xE0FFAE),
        new(140, "Ice plains spikes", 0x80B497, 0x60A17B, White),
        new(149, "Jungle M", 0x59C93C, 0x30BB0B, White),
        new(151, "Jungle edge M", 0x64C73F, 0x3EB80F, White),
        new(155, "Birch forest M", 0x88BB67, 0x6BA941, White),
        new(156, "Birch forest hills M", 0x88BB67, 0x6BA941, White),
        new(157, "Roofed forest M", 0x507A32, 0x59AE30, White),
        new(158, "Cold taiga M", 0x80B497, 0x60A17B, White),
        new(160, "Mega spruce taiga", 0x86B783, 0x68A464, White),
        new(161, "Redwood taiga hills M", 0x86B87F, 0x68A55F, White),
        new(162, "Extreme hills+ M", 0x8AB689, 0x6DA36B, White),
        new(163, "Savanna M", 0xBFB755, 0xAEA42A, White),
        new(164, "Savanna plateau M", 0xBFB755, 0xAEA42A, White),
        new(165, "Mesa (Bryce)", 0x90814D, 0x9E814D, White),
        new(166, "Mesa plateau F M", 0x90814D, 0x9E814D, White),
        new(167, "Mesa plateau M", 0x90814D, 0x9E814D, White),
    ];

    private static readonly Dictionary<byte, BiomeRow> ById = All.ToDictionary(row => row.Id);

    /// <summary>The biome stored under <paramref name="id"/>, or null for a byte the game gives no biome.</summary>
    public static BiomeRow? Row(byte id) => ById.TryGetValue(id, out var row) ? row : null;

    /// <summary>What an author reads the id as, or the number itself for one no biome claims.</summary>
    public static string NameOf(byte id) => Row(id)?.Name ?? $"Biome {id}";

    /// <summary>The other biomes whose grass, foliage and water are all this one's — interchangeable on the
    /// ground, so two of them side by side draw no boundary.</summary>
    public static IEnumerable<BiomeRow> SharingTint(byte id) =>
        Row(id) is { } row
            ? All.Where(other => other.Id != id && (other.Grass, other.Foliage, other.Water) == (row.Grass, row.Foliage, row.Water))
            : [];
}
