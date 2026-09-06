using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Minecraft.Palette;

/// <summary>Which of a block's three tints it takes, or none. The channel is a property of the block rather
/// than of the biome: grass and sugar cane read the grass colour, leaves and vines the foliage colour, water
/// the water colour, and everything else is its own texture whatever the ground around it is.</summary>
public enum TintChannel
{
    /// <summary>Untinted — the stored colour is the block, wherever it stands.</summary>
    None,

    /// <summary>The biome's grass colour: the grass block, the short plants, sugar cane.</summary>
    Grass,

    /// <summary>The biome's foliage colour: leaves and vines.</summary>
    Foliage,

    /// <summary>The biome's water colour, which only swampland moves off white.</summary>
    Water,
}

/// <summary>
/// What a biome does to a block's colour — the multiplier a client applies to grass, foliage and water, so a
/// studio render can show the ground the colour the game will.
///
/// <para><b>It is a rescale of what the palette already holds rather than a second table of colours.</b>
/// <see cref="BlockPaletteData"/> bakes the temperate (forest) tint into every tinted block's stored mean, so
/// the biome's own colour is applied by dividing that reference out and multiplying the biome's in. A block
/// whose texture is nearly white — the grass block — therefore comes out as the biome's colour itself, and one
/// with a colour of its own keeps it and shifts.</para>
///
/// <para><b>Swampland is the one biome that paints itself</b> (docs/world-export/terrain-painting.md §5b).
/// Vanilla chooses between a brown and a dark green over a noise field of its own, so its ground reads
/// splotchy whatever a field says about it. The two colours and the scale of the mottling are vanilla's; the
/// field that places them is the studio's own <see cref="PatternNoise"/>, so the picture shows that swamp is
/// two-tone at about the right size without claiming to be the same splotches the client will draw.</para>
///
/// <para>A biome no row names answers <see cref="Plains"/>'s colours, which is what the format's own default
/// is and what an unpainted chunk carries.</para>
/// </summary>
public static class BiomeTint
{
    /// <summary>The tint the palette's stored colours already carry — the temperate grass and foliage a
    /// forest column shows, and white for water, which is every biome but swampland. Dividing it out is what
    /// makes a stored mean re-tintable.</summary>
    public const uint ReferenceGrass = 0x79C05A;
    public const uint ReferenceFoliage = 0x59AE30;
    public const uint ReferenceWater = 0xFFFFFF;

    /// <summary>One biome's three multipliers.</summary>
    private readonly record struct Tints(uint Grass, uint Foliage, uint Water);

    // The 1.8 colormap read at each biome's own temperature and rainfall, plus the two biomes that override
    // it outright: mesa states its grass and foliage directly, and swampland states both and is the only one
    // that tints water.
    private static readonly Dictionary<byte, Tints> Table = new()
    {
        [Biome.Plains] = new(0x91BD59, 0x77AB2F, ReferenceWater),
        [Biome.Desert] = new(0xBFB755, 0xAEA42A, ReferenceWater),
        [Biome.ExtremeHills] = new(0x8AB689, 0x6DA36B, ReferenceWater),
        [Biome.Forest] = new(0x79C05A, 0x59AE30, ReferenceWater),
        [Biome.Taiga] = new(0x86B783, 0x68A464, ReferenceWater),
        [Biome.Swampland] = new(SwampGreen, SwampGreen, 0xE0FF70),
        [Biome.River] = new(0x91BD59, 0x77AB2F, ReferenceWater),
        [Biome.FrozenRiver] = new(0x80B497, 0x60A17B, ReferenceWater),
        [Biome.IcePlains] = new(0x80B497, 0x60A17B, ReferenceWater),
        [Biome.MushroomIsland] = new(0x55C93F, 0x2BBB0F, ReferenceWater),
        [Biome.Jungle] = new(0x59C93C, 0x30BB0B, ReferenceWater),
        [Biome.BirchForest] = new(0x88BB67, 0x6BA941, ReferenceWater),
        [Biome.RoofedForest] = new(0x79C05A, 0x59AE30, ReferenceWater),
        [Biome.ColdTaiga] = new(0x80B497, 0x60A17B, ReferenceWater),
        [Biome.Savanna] = new(0xBFB755, 0xAEA42A, ReferenceWater),
        [Biome.Mesa] = new(0x90814D, 0x9E814D, ReferenceWater),
    };

    /// <summary>The two colours swampland's own noise chooses between: the brown it mostly is, and the dark
    /// green of the wetter patches.</summary>
    private const uint SwampGreen = 0x6A7039;
    private const uint SwampDark = 0x4C763C;

    /// <summary>How wide swampland's mottling runs, in blocks. Vanilla samples its noise at 0.0225 per block,
    /// which is a feature about this many blocks across.</summary>
    private const int SwampScale = 44;

    private const uint SwampSeed = 0x5A17u;

    /// <summary>How much of the field is the darker of swampland's two greens. Vanilla takes its noise below
    /// −0.1 out of a roughly symmetric range, which is a little under half.</summary>
    private const double SwampDarkShare = 0.45;

    /// <summary>The multiplier <paramref name="channel"/> takes at <paramref name="biome"/>, over the column
    /// at <paramref name="x"/>, <paramref name="z"/> — the position matters only for swampland's grass, and
    /// every other answer is the biome's alone.</summary>
    public static uint Of(byte biome, TintChannel channel, int x, int z)
    {
        var tints = Table.TryGetValue(biome, out var found) ? found : Table[Biome.Plains];
        return channel switch
        {
            TintChannel.Grass => biome == Biome.Swampland ? SwampGrass(x, z) : tints.Grass,
            TintChannel.Foliage => tints.Foliage,
            TintChannel.Water => tints.Water,
            _ => 0xFFFFFF,
        };
    }

    /// <summary>The reference tint the palette baked into a block of this channel.</summary>
    public static uint ReferenceOf(TintChannel channel) => channel switch
    {
        TintChannel.Grass => ReferenceGrass,
        TintChannel.Foliage => ReferenceFoliage,
        TintChannel.Water => ReferenceWater,
        _ => 0xFFFFFF,
    };

    /// <summary>A stored colour moved from the tint the palette baked in to the one this biome applies, per
    /// channel and clamped. An untinted block answers itself.</summary>
    public static uint Rescale(uint stored, byte biome, TintChannel channel, int x, int z)
    {
        if (channel == TintChannel.None) return stored;
        var wanted = Of(biome, channel, x, z);
        var reference = ReferenceOf(channel);
        if (wanted == reference) return stored;
        return Pack(Channel(stored, 16, wanted, reference),
                    Channel(stored, 8, wanted, reference),
                    Channel(stored, 0, wanted, reference));
    }

    private static int Channel(uint stored, int shift, uint wanted, uint reference)
    {
        var from = (int)((reference >> shift) & 0xFF);
        if (from == 0) return (int)((stored >> shift) & 0xFF);
        return Math.Clamp((int)((stored >> shift) & 0xFF) * (int)((wanted >> shift) & 0xFF) / from, 0, 255);
    }

    private static uint Pack(int r, int g, int b) => (uint)((r << 16) | (g << 8) | b);

    /// <summary>Swampland's grass over one column: the darker green where its own field is low, the brown
    /// elsewhere.</summary>
    private static uint SwampGrass(int x, int z) =>
        PatternNoise.Value(x, z, SwampSeed, SwampScale) < SwampDarkShare ? SwampDark : SwampGreen;
}
