using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// One of a board's paint rules (<c>docs/world-export/terrain-painting.md</c>, TP28): a material laid over a
/// column's top courses where the ground is what the rule asks for. The rules are read in order and a column takes
/// the first it meets; every condition a rule states must hold, and one stating none lays everywhere. A shape that
/// states its own <c>material</c> is made of it, and no rule lays over it.
/// </summary>
public sealed class SketchPaintRuleJson
{
    /// <summary>What the list names the rule by, unique in the document.</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    /// <summary>What the author calls it, or absent.</summary>
    [JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>Whether it is switched off, so it lays nothing and keeps its conditions.</summary>
    [JsonPropertyName("off"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Off { get; set; }

    /// <summary>What it lays: a terrain material, in the form a shape's <c>material</c> takes.</summary>
    [JsonPropertyName("material")] public JsonElement? Material { get; set; }

    /// <summary>How many top courses it lays; absent, one.</summary>
    [JsonPropertyName("depth"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Depth { get; set; }

    /// <summary>The ground's steepness, as <c>[above, upTo]</c> degrees: steeper than the first and at most the
    /// second. Either may be null.</summary>
    [JsonPropertyName("slope"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int?[]? Slope { get; set; }

    /// <summary>The top course's y, as <c>[from, to]</c>, both included. Either may be null.</summary>
    [JsonPropertyName("height"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int?[]? Height { get; set; }

    /// <summary>Patches by noise: it lays only where the noise reads above a share.</summary>
    [JsonPropertyName("noise"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaintNoiseJson? Noise { get; set; }

    /// <summary>Only near one modifier: the ground within a width of its mask.</summary>
    [JsonPropertyName("near"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaintNearJson? Near { get; set; }
}

/// <summary>Where a paint rule lays by noise.</summary>
public sealed class PaintNoiseJson
{
    /// <summary>The width of a patch, in blocks.</summary>
    [JsonPropertyName("cell")] public double Cell { get; set; } = 8;

    /// <summary>The share the noise must read above, 0 to 1: 0.5 lays about half the ground.</summary>
    [JsonPropertyName("above")] public double Above { get; set; } = 0.5;

    /// <summary>The noise row it reads.</summary>
    [JsonPropertyName("seed")] public int Seed { get; set; } = 1;
}

/// <summary>The ground near one modifier: within <see cref="Width"/> blocks of its mask, a line's band or an
/// outline's rim, on every image the symmetry fans it to.</summary>
public sealed class PaintNearJson
{
    /// <summary>The id of the modifier the rule lays beside.</summary>
    [JsonPropertyName("modifier")] public string Modifier { get; set; } = "";

    /// <summary>How far past the mask's edge the rule reaches, in blocks; the inside is near too.</summary>
    [JsonPropertyName("width")] public double Width { get; set; } = 3;
}
