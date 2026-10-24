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

    /// <summary>A Hill's height at its deepest point, in blocks.</summary>
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

    /// <summary>A Level's height, as a world y; absent, the median of the ground it holds flat.</summary>
    [JsonPropertyName("top"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Top { get; set; }

    /// <summary>How far out a Level holds the ground flat, in the mask's units: one is its rim.</summary>
    [JsonPropertyName("inner"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Inner { get; set; }

    /// <summary>Where a Level's skirt has eased back to the ground, in the mask's units.</summary>
    [JsonPropertyName("outer"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Outer { get; set; }

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

    /// <summary>A polygon's outline, as <c>[x, z]</c> pairs.</summary>
    [JsonPropertyName("points"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[][]? Points { get; set; }
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
