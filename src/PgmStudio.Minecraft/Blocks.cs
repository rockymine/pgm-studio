namespace PgmStudio.Minecraft;

/// <summary>
/// Numeric (1.8–1.12) block ids used when synthesising a world. Colour-coded blocks (wool, stained clay,
/// stained glass + panes) take a 0–15 data value = the dye colour; <see cref="BlockColors"/> maps those.
/// </summary>
public static class Blocks
{
    public const int Air = 0;
    public const int Stone = 1;
    public const int Bedrock = 7;
    public const int Wool = 35;
    public const int IronBlock = 42;
    public const int Chest = 54;
    public const int RedstoneWire = 55;
    public const int StandingSign = 63;
    public const int WallSign = 68;
    public const int RedstoneTorch = 76;
    public const int StainedGlass = 95;
    public const int StainedClay = 159;
    public const int StainedGlassPane = 160;
}
