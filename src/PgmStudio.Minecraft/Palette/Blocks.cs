namespace PgmStudio.Minecraft.Palette;

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
    public const int Planks = 5;
    public const int Bedrock = 7;
    public const int Water = 8;              // flowing; a filled channel bed is stationary water
    public const int StationaryWater = 9;
    public const int Cobweb = 30;
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

    /// <summary>The blocks a house is dressed with, whose <b>metadata is geometry rather than colour</b>: a
    /// stair's four facings and its upside-down flag, a slab's upper half. A material resolves its own data
    /// value from where a cell sits, which is the wrong answer for these — a window's stairs are turned by the
    /// window, not by the wall's pattern — so the pieces that carry a facing are chosen as a block id and the
    /// stamper supplies the nibble.
    ///
    /// <para>Ids are the wood families' first member. The five other woods are the neighbouring ids for stairs
    /// (<c>134</c>–<c>136</c>, <c>163</c>–<c>164</c>) and fences (<c>188</c>–<c>192</c>), and the low three bits
    /// of the data for slabs, which is why only one of each is named here.</para></summary>
    public const int Glass = 20;
    public const int StoneSlab = 44;
    public const int OakStairs = 53;
    public const int CobblestoneStairs = 67;
    public const int OakFence = 85;
    public const int GlassPane = 102;
    public const int Ladder = 65;
    public const int WoodenSlab = 126;

    /// <summary>The bit a slab's data carries when it sits in the <em>upper</em> half of its cube — the lintel
    /// over a window, as against the sill under it. The low three bits stay the slab's material.</summary>
    public const int SlabUpperHalf = 8;

    /// <summary>The bit a stair's data carries when it is upside down: its full half is the top of the cube and
    /// its step hangs below. The low two bits stay the direction it climbs toward
    /// (<see cref="StairEast"/>…<see cref="StairNorth"/>).</summary>
    public const int StairUpsideDown = 4;

    /// <summary>Which way a stair climbs — the side of the cube its raised half sits on.</summary>
    public const int StairEast = 0, StairWest = 1, StairSouth = 2, StairNorth = 3;

    /// <summary>Log and leaf, in the two id pairs the numeric format splits the six woods across: the low two
    /// data bits are the wood (log 0–3 oak · spruce · birch · jungle, log2 0–1 acacia · dark oak), and the bits
    /// above them are the log's placement axis and the leaf's decay flags. Here rather than beside the dressing
    /// pass because <see cref="BlockPalette"/> has to know them too, and a block id is not a feature's.</summary>
    public const int Log = 17;
    public const int Leaves = 18;
    public const int Leaves2 = 161;
    public const int Log2 = 162;
}
