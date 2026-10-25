namespace PgmStudio.Geom.Relief;

/// <summary>Which terraform operation a <see cref="ModifierSpec"/> runs.</summary>
public enum ModifierOp
{
    /// <summary>Ground raised inside the mask, highest at its middle and falling to nothing at its rim.</summary>
    Mound,

    /// <summary>A bowl sunk to a floor, flat in its middle, its wall rounded into the ground by a melt.</summary>
    Crater,

    /// <summary>The ground eased to one height inside the mask and back out over a skirt.</summary>
    Level,

    /// <summary>Noise in one or more sizes, added inside the mask.</summary>
    Noise,

    /// <summary>A crest along a line or the spine of an area, its foot wandering.</summary>
    Ridge,

    /// <summary>A flat-topped mass on an area, a cliff round its top and a talus apron below.</summary>
    Butte,

    /// <summary>A needle standing on the middle of an area.</summary>
    Spire,

    /// <summary>One side of a line stepped up by a cliff, a talus apron on the other.</summary>
    Scarp,

    /// <summary>A trench along a line with a floor, walls and optional ledges.</summary>
    Canyon,

    /// <summary>A bed cut along a line that never climbs, holding water.</summary>
    River,

    /// <summary>A bowl cut into an area, holding water to a level.</summary>
    Lake,

    /// <summary>A route graded into the ground along a line.</summary>
    Road,

    /// <summary>Grooves cut outward from the middle of an area.</summary>
    Gullies,

    /// <summary>The ground inside resampled at noise-shifted positions.</summary>
    Warp,

    /// <summary>A cross-section set by distance from a line: a floor, then steps of cliff and bench.</summary>
    Profile,

    /// <summary>A sea over an area: its floor falling from the shore, a beach easing the land down to it.</summary>
    Coast,

    /// <summary>Lumps strewn over an area: fallen blocks, rubble, hummocks.</summary>
    Lumps,

    /// <summary>The ground inside broken into pieces, each lifted and tilted on its own, clefts between.</summary>
    Shatter,
}

/// <summary>Which way a modifier's change may move the ground.</summary>
public enum ModifierMode
{
    /// <summary>Either way: whatever the operation computes is applied.</summary>
    Unrestricted,

    /// <summary>The ground only rises.</summary>
    Lift,

    /// <summary>The ground only falls.</summary>
    Cut,
}

/// <summary>One size of a Roughness: the width of its largest feature, how many finer octaves ride on it, and
/// how far it moves the ground either way.</summary>
public readonly record struct NoiseSize(double Cell, int Octaves, double Rise);

/// <summary>One step of a Profile: from <see cref="Start"/> to <see cref="End"/> blocks out from its line the
/// ground climbs, or falls, to <see cref="Rise"/> blocks over the profile's floor, and holds there beyond.</summary>
public readonly record struct ProfileStep(double Start, double End, double Rise);

/// <summary>
/// One terraform operation over a field of ground heights: which operation, how it may move the ground, where it
/// works, and the numbers it reads. A number left null takes <see cref="ModifierDefaults"/>.
/// </summary>
public sealed record ModifierSpec
{
    public ModifierOp Op { get; init; } = ModifierOp.Mound;
    public ModifierMode Mode { get; init; } = ModifierMode.Unrestricted;

    /// <summary>Where it works, every area already standing where it applies.</summary>
    public IReadOnlyList<MaskArea> Areas { get; init; } = [];

    /// <summary>A Mound's height at its middle, in blocks.</summary>
    public double? Rise { get; init; }

    /// <summary>A Mound's shoulder: above one it holds its height further out, below one it peaks.</summary>
    public double? Power { get; init; }

    /// <summary>The share of the mask, in from its rim, over which the change fades in.</summary>
    public double? Soft { get; init; }

    /// <summary>The exponent areas of one Mound or Crater are combined by; zero keeps the greater change.</summary>
    public double? Join { get; init; }

    /// <summary>A Crater's floor, as a height.</summary>
    public double? Floor { get; init; }

    /// <summary>How many blocks out from its middle a Crater's floor stays flat.</summary>
    public double? Flat { get; init; }

    /// <summary>How many blocks a Crater's wall rises for each block out from its flat floor.</summary>
    public double? Slope { get; init; }

    /// <summary>Over how many blocks a Crater's rim is rounded into the ground.</summary>
    public double? Melt { get; init; }

    /// <summary>A Level's height; absent, the median of the ground inside its inner area.</summary>
    public double? Top { get; init; }

    /// <summary>Where a Level stops being held flat, in <see cref="MaskArea.E"/>.</summary>
    public double? Inner { get; init; }

    /// <summary>Where a Level's skirt has eased all the way back to the ground, in <see cref="MaskArea.E"/>.</summary>
    public double? Outer { get; init; }

    /// <summary>A Noise's sizes, each its own fractal added to the others.</summary>
    public IReadOnlyList<NoiseSize>? Sizes { get; init; }

    /// <summary>How far a Ridge's foot wanders in from its mask, in blocks; also how far Gullies bend.</summary>
    public double? Wander { get; init; }

    /// <summary>Width in blocks of a Butte's or a Scarp's cliff.</summary>
    public double? Cliff { get; init; }

    /// <summary>Width in blocks of the talus apron below a Butte's or a Scarp's cliff.</summary>
    public double? Talus { get; init; }

    /// <summary>The share of the way up its cliff a Butte's or a Scarp's talus starts at.</summary>
    public double? Apron { get; init; }

    /// <summary>How much a Butte's or a Spire's outline is roughened, as a share of its radius.</summary>
    public double? Jag { get; init; }

    /// <summary>How a Spire's sides fall: above one they bulge out toward a dome, below one they hollow in to a needle.</summary>
    public double? Taper { get; init; }

    /// <summary>How far a Scarp lifts its raised side, in blocks.</summary>
    public double? Height { get; init; }

    /// <summary>Which hand of a Scarp's line is raised, +1 right and -1 left walking along it.</summary>
    public int? Side { get; init; }

    /// <summary>How deep a Canyon, River, Lake or Gully cuts, in blocks.</summary>
    public double? Depth { get; init; }

    /// <summary>The share of a Canyon's half width that is its flat floor.</summary>
    public double? Bed { get; init; }

    /// <summary>A Canyon wall's steepness: the wall climbs as its distance across to this power.</summary>
    public double? Wall { get; init; }

    /// <summary>Blocks between a Canyon's ledges; zero cuts smooth walls.</summary>
    public double? Ledges { get; init; }

    /// <summary>How many blocks of water stand in a River's bed.</summary>
    public double? Water { get; init; }

    /// <summary>Over how many blocks a River's banks ease back to the ground.</summary>
    public double? Bank { get; init; }

    /// <summary>The height a Lake's water surface stands at; absent, one under the ground a tenth of its rim stands at or below.</summary>
    public double? Level { get; init; }

    /// <summary>Over how many blocks a Lake's shore is held at the water and eases back to the ground.</summary>
    public double? Shore { get; init; }

    /// <summary>The steepest a Road climbs or falls, in blocks per block.</summary>
    public double? Grade { get; init; }

    /// <summary>Over how many blocks a Road's shoulders ease into the ground.</summary>
    public double? Shoulder { get; init; }

    /// <summary>How many grooves Gullies cuts round its middle.</summary>
    public int? Count { get; init; }

    /// <summary>How narrow each groove is against the ground between: higher is narrower.</summary>
    public double? Sharpness { get; init; }

    /// <summary>The farthest a Warp moves a position, in blocks.</summary>
    public double? Amount { get; init; }

    /// <summary>The width in blocks of a Warp's largest swirl.</summary>
    public double? Cell { get; init; }

    /// <summary>A Profile's steps out from its line, nearest first.</summary>
    public IReadOnlyList<ProfileStep>? Steps { get; init; }

    /// <summary>Over how many blocks inland a Coast's land eases down to its beach.</summary>
    public double? Shelf { get; init; }

    /// <summary>How long each of a Lumps' lumps is across its long axis, least and most, in blocks.</summary>
    public (double Least, double Most)? Across { get; init; }

    /// <summary>How high each of a Lumps' lumps stands, least and most, in blocks.</summary>
    public (double Least, double Most)? Heights { get; init; }

    /// <summary>A Lumps' short axis as a share of its long one, least and most.</summary>
    public (double Least, double Most)? Stretch { get; init; }

    /// <summary>The heading a Lumps' lumps line up along, in degrees from east toward south, each within 25
    /// either way; absent, each turns at random.</summary>
    public double? Angle { get; init; }

    /// <summary>The steepest a Shatter's piece tilts, in blocks per block.</summary>
    public double? Tilt { get; init; }

    /// <summary>How many blocks either side of a seam between a Shatter's pieces a cleft is cut; zero cuts none.</summary>
    public double? Gap { get; init; }

    /// <summary>The noise row it reads; a size takes the seed plus thirty-seven times its place.</summary>
    public int Seed { get; init; } = 1;
}

/// <summary>What a modifier reads where its document states nothing — the one place the numbers are set.</summary>
public static class ModifierDefaults
{
    public const double MoundRise = 8;
    public const double MoundPower = 1.6;
    public const double CraterFloor = 0;
    public const double CraterFlat = 0;
    public const double CraterSlope = 1;
    public const double CraterMelt = 0;
    public const double Join = 0;
    public const double LevelInner = 1;
    public const double LevelOuter = 1.5;

    /// <summary>The one size a Noise with none stated is made of.</summary>
    public static readonly NoiseSize Size = new(12, 2, 1);

    public const double RidgeRise = 8;
    public const double RidgeWander = 2;

    /// <summary>How far over the median ground inside its mask a Butte with no top stated stands.</summary>
    public const double ButteHeight = 12;
    public const double ButteCliff = 1.5;
    public const double ButteTalus = 6;
    public const double Apron = 0.3;
    public const double ButteJag = 0.12;

    /// <summary>How far over the median ground inside its mask a Spire with no top stated stands.</summary>
    public const double SpireHeight = 20;
    public const double SpireTaper = 1.6;
    public const double SpireJag = 0.15;

    public const double ScarpHeight = 6;
    public const int ScarpSide = 1;
    public const double ScarpCliff = 2;
    public const double ScarpTalus = 5;

    public const double CanyonDepth = 12;
    public const double CanyonBed = 0.35;
    public const double CanyonWall = 2.5;
    public const double CanyonLedges = 0;

    public const double RiverDepth = 2;
    public const double RiverWater = 1;
    public const double RiverBank = 4;

    /// <summary>The drop in a River's bed that starts a fall, in blocks.</summary>
    public const double RiverFall = 2;

    /// <summary>The run a River's bed holds level before it may fall, in blocks.</summary>
    public const double RiverReach = 10;

    /// <summary>How steeply a River's valley side, or a Lake's shore, climbs from the water to meet higher ground,
    /// in blocks of rise a block: the steepest a player walks.</summary>
    public const double WaterBankSlope = 1;

    /// <summary>How far past its stated bank or shore that climb may run to meet higher ground, in blocks.</summary>
    public const double WaterBankReach = 12;

    public const double LakeDepth = 3;
    public const double LakeShore = 3;

    public const double RoadGrade = 0.25;
    public const double RoadShoulder = 4;

    public const int GulliesCount = 8;
    public const double GulliesDepth = 3;
    public const double GulliesWander = 0.9;
    public const double GulliesSharpness = 3;

    public const double WarpAmount = 6;
    public const double WarpCell = 24;

    /// <summary>The two steps a Profile with none stated climbs, as shares of its half width and blocks of rise.</summary>
    public static readonly IReadOnlyList<ProfileStep> ProfileSteps = [new(0.25, 0.4, 6), new(0.6, 0.75, 12)];

    /// <summary>How far a Profile's step edges wander in and out, in blocks.</summary>
    public const double ProfileJag = 1.5;
    public const double ProfileTalus = 0;

    public const double CoastShelf = 10;
    public const double CoastDepth = 6;
    public const double CoastSlope = 0.35;

    public const int LumpsCount = 12;
    public static readonly (double Least, double Most) LumpsAcross = (6, 16);
    public static readonly (double Least, double Most) LumpsHeights = (2, 5);
    public static readonly (double Least, double Most) LumpsStretch = (0.5, 1);
    public const double LumpsJag = 0.25;
    public const double LumpsPower = 1.8;

    /// <summary>How far apart a Shatter's pieces are, in blocks.</summary>
    public const double ShatterCell = 10;

    /// <summary>The most a Shatter lifts or drops one piece, in blocks.</summary>
    public const double ShatterAmount = 3;
    public const double ShatterTilt = 0.25;
    public const double ShatterGap = 1;
    public const double ShatterDepth = 4;

    /// <summary>How far a Shatter's seams bend, in blocks.</summary>
    public const double ShatterWander = 3;
}
