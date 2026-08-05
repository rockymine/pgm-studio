namespace PgmStudio.Minecraft;

/// <summary>
/// Numeric (1.8–1.12) block ids used when synthesising a world. Colour-coded blocks (wool, stained clay,
/// stained glass + panes) take a 0–15 data value = the dye colour; <see cref="BlockPalette"/> maps those.
/// </summary>
public static class Blocks
{
    public const int Air = 0;
    public const int Stone = 1;
    public const int Grass = 2;
    public const int Dirt = 3;
    public const int Cobblestone = 4;
    public const int Bedrock = 7;
    public const int Sand = 12;
    public const int Gravel = 13;
    public const int Sandstone = 24;
    public const int Clay = 82;
    public const int HardenedClay = 172;
    public const int QuartzBlock = 155;
    public const int Lava = 10;              // flowing; a core's interior is stationary lava
    public const int StationaryLava = 11;
    public const int GoldBlock = 41;
    public const int Wool = 35;
    public const int IronBlock = 42;
    public const int Obsidian = 49;
    public const int Chest = 54;
    public const int RedstoneWire = 55;
    public const int StandingSign = 63;
    public const int WallSign = 68;
    public const int RedstoneTorch = 76;
    public const int StainedGlass = 95;
    public const int EndStone = 121;
    public const int EmeraldBlock = 133;
    public const int StainedClay = 159;
    public const int StainedGlassPane = 160;

    /// <summary>Log and leaf, in the two id pairs the numeric format splits the six woods across: the low two
    /// data bits are the wood (log 0–3 oak · spruce · birch · jungle, log2 0–1 acacia · dark oak), and the bits
    /// above them are the log's placement axis and the leaf's decay flags. Here rather than beside the dressing
    /// pass because <see cref="BlockPalette"/> has to know them too, and a block id is not a feature's.</summary>
    public const int Log = 17;
    public const int Leaves = 18;
    public const int Leaves2 = 161;
    public const int Log2 = 162;
}
