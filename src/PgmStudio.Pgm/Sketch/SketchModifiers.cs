using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// One terraform operation over the built ground (<c>docs/tools/sketch.md</c>, <i>Terraform</i>). A sketch's
/// <c>modifiers[]</c> run in list order after every layer is rasterized, each over the top of the ground as the
/// one before it left it, inside its mask. A room's ground holds: no modifier moves it.
///
/// <para>One flat shape carries every operation, discriminated by <see cref="Op"/>; a number an operation does
/// not read is left out of the document.</para>
/// </summary>
public sealed class SketchModifierJson
{
    /// <summary>What the list and every read name this modifier by, unique in the document.</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    /// <summary>What the author calls it, or absent for the operation's own name.</summary>
    [JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>Which operation it is.</summary>
    [JsonPropertyName("op")] [WordSet(typeof(ModifierOps))] public string Op { get; set; } = ModifierOps.Mound;

    /// <summary>Whether it is switched off, so the stack runs as if it were not there and keeps its numbers.</summary>
    [JsonPropertyName("off"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Off { get; set; }

    /// <summary>Which half of the change is kept: <c>lift</c> only what raises the ground, <c>cut</c> only what
    /// lowers it, absent both. A Level also reads it for its skirt.</summary>
    [JsonPropertyName("mode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [WordSet(typeof(ModifierModes))] public string? Mode { get; set; }

    /// <summary>Where it works.</summary>
    [JsonPropertyName("mask")] public ModifierMaskJson Mask { get; set; } = new();

    /// <summary>A Hill's or a Ridge's height at its deepest point, in blocks.</summary>
    [JsonPropertyName("rise"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Rise { get; set; }

    /// <summary>A Hill's shoulder: above one it holds full height further out and falls steeply at the rim,
    /// below one it peaks sharply.</summary>
    [JsonPropertyName("power"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Power { get; set; }

    /// <summary>The share of the mask, measured in from its rim, over which the change fades in.</summary>
    [JsonPropertyName("soft"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Soft { get; set; }

    /// <summary>How the areas of one Hill or Hollow add up where they overlap: zero keeps the larger change, and
    /// above zero the changes are summed as a p-norm, so the saddle between two hills fills into a ridge.</summary>
    [JsonPropertyName("join"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Join { get; set; }

    /// <summary>A Hollow's floor, as a world y.</summary>
    [JsonPropertyName("floor"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Floor { get; set; }

    /// <summary>How many blocks out from its middle a Hollow's floor stays flat.</summary>
    [JsonPropertyName("flat"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Flat { get; set; }

    /// <summary>How many blocks a Hollow's wall rises for each block out from its flat floor.</summary>
    [JsonPropertyName("slope"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Slope { get; set; }

    /// <summary>Over how many blocks a Hollow's rim is rounded into the ground; zero leaves a sharp lip.</summary>
    [JsonPropertyName("melt"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Melt { get; set; }

    /// <summary>A Level's, Butte's or Spire's height, as a world y; absent, the median of the ground inside the
    /// mask (a Level holds it flat, a Butte stands 12 over it and a Spire 20).</summary>
    [JsonPropertyName("top"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Top { get; set; }

    /// <summary>How far out a Level holds the ground flat, in the mask's units: one is its rim.</summary>
    [JsonPropertyName("inner"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Inner { get; set; }

    /// <summary>Where a Level's skirt has eased back to the ground, in the mask's units.</summary>
    [JsonPropertyName("outer"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Outer { get; set; }

    /// <summary>How far a Ridge's foot wanders in from its mask, in blocks; how far Gullies' grooves bend, in
    /// turns.</summary>
    [JsonPropertyName("wander"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Wander { get; set; }

    /// <summary>A Butte's or a Scarp's cliff, in blocks wide.</summary>
    [JsonPropertyName("cliff"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Cliff { get; set; }

    /// <summary>The talus apron below a Butte's or a Scarp's cliff, in blocks wide.</summary>
    [JsonPropertyName("talus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Talus { get; set; }

    /// <summary>The share of the way up its cliff a Butte's or a Scarp's talus apron starts at.</summary>
    [JsonPropertyName("apron"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Apron { get; set; }

    /// <summary>How roughened a Butte's or a Spire's outline is, as a share of its radius.</summary>
    [JsonPropertyName("jag"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Jag { get; set; }

    /// <summary>How a Spire's sides fall: above one they bulge out toward a dome, below one they hollow in to a needle.</summary>
    [JsonPropertyName("taper"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Taper { get; set; }

    /// <summary>How far a Scarp lifts its raised side, in blocks.</summary>
    [JsonPropertyName("height"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Height { get; set; }

    /// <summary>Which hand of a Scarp's line is raised: 1 right, -1 left, walking along it.</summary>
    [JsonPropertyName("side"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Side { get; set; }

    /// <summary>How deep a Canyon, River, Lake or Gullies cuts, in blocks.</summary>
    [JsonPropertyName("depth"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Depth { get; set; }

    /// <summary>The share of a Canyon's half width that is its flat floor.</summary>
    [JsonPropertyName("bed"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Bed { get; set; }

    /// <summary>A Canyon wall's steepness: it climbs as the distance across to this power.</summary>
    [JsonPropertyName("wall"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Wall { get; set; }

    /// <summary>Blocks between a Canyon's ledges; absent or zero cuts smooth walls.</summary>
    [JsonPropertyName("ledges"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Ledges { get; set; }

    /// <summary>How many blocks of water stand in a River's bed.</summary>
    [JsonPropertyName("water"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Water { get; set; }

    /// <summary>Over how many blocks a River's banks ease back to the ground.</summary>
    [JsonPropertyName("bank"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Bank { get; set; }

    /// <summary>The height a Lake's water surface stands at, as a world y like <c>top</c>; absent, one under
    /// the ground a tenth of its rim stands at or below, so one cleft in the rim does not drain it.</summary>
    [JsonPropertyName("level"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Level { get; set; }

    /// <summary>Over how many blocks a Lake's shore is held at the water and eases back to the ground.</summary>
    [JsonPropertyName("shore"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Shore { get; set; }

    /// <summary>The material laid under a River's, Lake's or Coast's water: the floor it covers and the beach at
    /// its edge. A terrain material in the form a theme's buckets take (<c>{"kind": "solid", "id": 13}</c>, a
    /// pattern, a band stack), read by the world build; absent, the water lies on the default bank, a gravel,
    /// coarse dirt and sand patchwork. The name differs from <see cref="Bed"/>, which is a Canyon's flat floor
    /// share.</summary>
    [JsonPropertyName("bed_material"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? BedMaterial { get; set; }

    /// <summary>The steepest a Road climbs or falls, in blocks per block.</summary>
    [JsonPropertyName("grade"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Grade { get; set; }

    /// <summary>Over how many blocks a Road's shoulders ease into the ground.</summary>
    [JsonPropertyName("shoulder"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Shoulder { get; set; }

    /// <summary>How many grooves Gullies cuts round the middle of its mask.</summary>
    [JsonPropertyName("count"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Count { get; set; }

    /// <summary>How narrow each of Gullies' grooves is against the ground between: higher is narrower.</summary>
    [JsonPropertyName("sharpness"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Sharpness { get; set; }

    /// <summary>The farthest a Warp moves a position, in blocks.</summary>
    [JsonPropertyName("amount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Amount { get; set; }

    /// <summary>The width in blocks of a Warp's largest swirl.</summary>
    [JsonPropertyName("cell"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Cell { get; set; }

    /// <summary>A Profile's steps out from its line, nearest first; absent, two steps at a quarter and at
    /// three fifths of its half width, rising 6 and 12.</summary>
    [JsonPropertyName("steps"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProfileStepJson>? Steps { get; set; }

    /// <summary>Over how many blocks inland a Coast's land eases down to its beach.</summary>
    [JsonPropertyName("shelf"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Shelf { get; set; }

    /// <summary>How long each of a Lumps' lumps is, least and most, as <c>[least, most]</c> blocks.</summary>
    [JsonPropertyName("across"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[]? Across { get; set; }

    /// <summary>How high each of a Lumps' lumps stands, as <c>[least, most]</c> blocks.</summary>
    [JsonPropertyName("heights"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[]? Heights { get; set; }

    /// <summary>A Lumps' short axis as a share of its long one, as <c>[least, most]</c>.</summary>
    [JsonPropertyName("stretch"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[]? Stretch { get; set; }

    /// <summary>The heading a Lumps' lumps line up along, in degrees from east toward south, each within 25
    /// either way; absent, each turns at random.</summary>
    [JsonPropertyName("angle"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Angle { get; set; }

    /// <summary>The steepest a Shatter's piece tilts, in blocks per block.</summary>
    [JsonPropertyName("tilt"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Tilt { get; set; }

    /// <summary>How many blocks either side of a seam between a Shatter's pieces a cleft is cut; zero cuts none.</summary>
    [JsonPropertyName("gap"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Gap { get; set; }

    /// <summary>The fewest blocks of ground a Tunnel or a Chamber keeps over it, away from a Tunnel's mouths.</summary>
    [JsonPropertyName("cover"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Cover { get; set; }

    /// <summary>How far in from each end a Tunnel opens through its cover, in blocks.</summary>
    [JsonPropertyName("mouth"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Mouth { get; set; }

    /// <summary>How far a Tunnel's floor dips under the straight run between its ends at its middle, in blocks.</summary>
    [JsonPropertyName("sag"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Sag { get; set; }

    /// <summary>Whether a Shaft hangs a ladder up one wall; absent, it does.</summary>
    [JsonPropertyName("ladder"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Ladder { get; set; }

    /// <summary>Whether a carve's cave is dressed with gravel and clay floors, stalactites and ore; absent, it is.</summary>
    [JsonPropertyName("dress"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Dress { get; set; }

    /// <summary>A Roughness's sizes, each its own noise added to the others.</summary>
    [JsonPropertyName("sizes"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NoiseSizeJson>? Sizes { get; set; }

    /// <summary>The noise row it reads, so it builds the same every time until the author rerolls it.</summary>
    [JsonPropertyName("seed")] public int Seed { get; set; } = 1;
}

/// <summary>The area a modifier works in: one or more outlines, and whether the map's symmetry fans them.</summary>
public sealed class ModifierMaskJson
{
    /// <summary>The outlines. A column under several takes each in turn; a Hill melts them by its join.</summary>
    [JsonPropertyName("areas")] public List<MaskAreaJson> Areas { get; set; } = [];

    /// <summary>Whether every area also stands at each of its images under the map's symmetry, which is what a
    /// fair board wants; off, the modifier works where it is drawn and nowhere else.</summary>
    [JsonPropertyName("mirror")] public bool Mirror { get; set; } = true;

    /// <summary>The ids of shapes this one modifier leaves alone: their columns, and the columns of their
    /// images under the map's symmetry, are out of its run as though the mask did not reach them. Every other
    /// modifier still shapes them.</summary>
    [JsonPropertyName("leaves"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Leaves { get; set; }
}

/// <summary>One outline of a mask, discriminated by <see cref="Kind"/>.</summary>
public sealed class MaskAreaJson
{
    /// <summary>Which outline it is.</summary>
    [JsonPropertyName("kind")] [WordSet(typeof(MaskKinds))] public string Kind { get; set; } = MaskKinds.Ellipse;

    /// <summary>An ellipse's centre, as <c>[x, z]</c>.</summary>
    [JsonPropertyName("at"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[]? At { get; set; }

    /// <summary>An ellipse's radius along its own x, in blocks.</summary>
    [JsonPropertyName("rx"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Rx { get; set; }

    /// <summary>An ellipse's radius along its own z, in blocks.</summary>
    [JsonPropertyName("rz"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Rz { get; set; }

    /// <summary>How far an ellipse is turned, in degrees.</summary>
    [JsonPropertyName("angle"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Angle { get; set; }

    /// <summary>A polygon's outline or a line's path, as <c>[x, z]</c> pairs; a Tunnel's point may add its floor as a
    /// third number, <c>[x, z, y]</c>.</summary>
    [JsonPropertyName("points"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[][]? Points { get; set; }

    /// <summary>A line's width in blocks; its edge lies half of it from the path.</summary>
    [JsonPropertyName("width"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Width { get; set; }
}

/// <summary>One step of a Profile: between <c>start</c> and <c>end</c> blocks out from the line the ground
/// climbs, or falls, to <c>rise</c> blocks over the floor, and holds there beyond.</summary>
public sealed class ProfileStepJson
{
    /// <summary>How many blocks out from the line the cliff begins.</summary>
    [JsonPropertyName("start")] public double Start { get; set; }

    /// <summary>How many blocks out from the line the cliff has reached its height.</summary>
    [JsonPropertyName("end")] public double End { get; set; }

    /// <summary>How far over the profile's floor the step stands, in blocks; negative falls.</summary>
    [JsonPropertyName("rise")] public double Rise { get; set; }
}

/// <summary>One size of a Roughness: how wide its features are, how many finer octaves ride on it, and how far
/// it moves the ground.</summary>
public sealed class NoiseSizeJson
{
    /// <summary>The width of its largest feature, in blocks.</summary>
    [JsonPropertyName("cell")] public double Cell { get; set; } = 12;

    /// <summary>How many octaves, each half the width of the one before.</summary>
    [JsonPropertyName("octaves")] public int Octaves { get; set; } = 2;

    /// <summary>How far it moves the ground either way, in blocks.</summary>
    [JsonPropertyName("rise")] public double Rise { get; set; } = 1;
}
