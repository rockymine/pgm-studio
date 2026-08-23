using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Geom.Relief;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The wire form of an island's relief — the interior elevation an author states inside a landmass, as
/// against the outline heights a shape already carries (docs/world-export/relief.md). It rides
/// <b>top-level on the layout, keyed by island id</b>, not nested inside the shapes: a plan recompile
/// replaces every shape it produced, and a relief is far more expensive hand work than a shape.
///
/// <para>One flat mark shape carries every kind, discriminated by <see cref="ReliefMarkJson.Kind"/>. That is
/// deliberate rather than lazy — a relief is short enough to write by hand and is meant to be, so the
/// document an agent or a generator emits is the same one the editor saves.</para>
/// </summary>
public sealed class SketchReliefJson
{
    /// <summary>The level the field falls back to where nothing is stated, and the level the block step is
    /// measured from.</summary>
    [JsonPropertyName("base")] public double Base { get; set; } = 4;

    /// <summary>How far a mark's influence travels, in blocks. Zero is unlimited — the marks decide the whole
    /// surface, which is what a room-sized island wants.</summary>
    [JsonPropertyName("reach")] public double Reach { get; set; }

    /// <summary>The block quantum the finished surface snaps to.</summary>
    [JsonPropertyName("step")] public int Step { get; set; } = 1;

    /// <summary>Whether to cut a way up out of ground the block step stranded. Worth asking for whenever the
    /// step is more than one, since that is what turns a riser into a wall.</summary>
    [JsonPropertyName("stairs")] public bool Stairs { get; set; }

    /// <summary>The noise laid over everything the marks decide, or absent for a surface with none.</summary>
    [JsonPropertyName("grain")]  public ReliefGrainJson? Grain { get; set; }

    /// <summary>The heights the author stated, each over the patch it states them for.</summary>
    [JsonPropertyName("marks")]  public List<ReliefMarkJson>? Marks { get; set; }

    /// <summary>The shapes of ground lifted or lowered after the marks are solved.</summary>
    [JsonPropertyName("pushes")] public List<ReliefPushJson>? Pushes { get; set; }

    /// <summary>The spec the solver takes, with the map's symmetry folded in. The fold is passed whatever the
    /// island is: for one lying wholly on one side of the axis every mirror lookup falls outside its own
    /// footprint and nothing changes, and for one spanning the axis it is exactly what makes the two halves
    /// agree cell for cell.</summary>
    public ReliefSpec ToSpec(string? mirrorMode = null, double centreX = 0, double centreZ = 0) => new()
    {
        Base = Base,
        Reach = Reach,
        Step = Math.Max(1, Step),
        Stairs = Stairs,
        Grain = Grain?.Amplitude ?? 0,
        GrainScale = Math.Max(1, Grain?.Scale ?? 9),
        Seed = Grain?.Seed ?? 1,
        Marks = (Marks ?? []).Select(mark => mark.ToMark()).OfType<Mark>().ToList(),
        Pushes = (Pushes ?? []).Select(push => push.ToPush()).OfType<PushMark>().ToList(),
        FoldMode = mirrorMode,
        FoldCentreX = centreX,
        FoldCentreZ = centreZ,
    };
}

/// <summary>The wobble added after the field is solved: how far it moves the surface, over what feature size,
/// from which seed.</summary>
public sealed class ReliefGrainJson
{
    /// <summary>How far the noise moves the surface, in blocks. Zero leaves it as the marks solved it.</summary>
    [JsonPropertyName("amplitude")] public double Amplitude { get; set; }

    /// <summary>How wide a feature the noise makes, in blocks — small is rubble, large is dunes.</summary>
    [JsonPropertyName("scale")]     public int Scale { get; set; } = 9;

    /// <summary>The noise row it reads, so the same relief solves identically on every export until the
    /// author rerolls it.</summary>
    [JsonPropertyName("seed")]      public uint Seed { get; set; } = 1;
}

/// <summary>One stated height, whatever shape the patch it states it over happens to be. Fields not belonging
/// to the named <see cref="Kind"/> are ignored, so a mark can be retyped in place without rewriting it.</summary>
public sealed class ReliefMarkJson
{
    /// <summary>The editor's handle on this mark. The solver has no use for it — a mark is a set of pinned
    /// cells — but a mark is a <em>placed thing</em>, and a placed thing has to survive being selected, moved
    /// and edited among its neighbours. Carried on the wire rather than assigned per session because a stored
    /// relief that renumbered its marks on every load would move the selection under the author's hands.</summary>
    [JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>What shape of patch this mark states a height over: <c>point</c>, <c>line</c>, <c>area</c>,
    /// <c>rim</c> or <c>scarp</c>. It decides which of the fields below are read; the rest are ignored, so a
    /// mark can be retyped in place without being rewritten.</summary>
    [JsonPropertyName("kind")] public string Kind { get; set; } = "point";

    /// <summary>A point's position, as an <c>[x, z]</c> pair.</summary>
    [JsonPropertyName("at")]     public double[]? At { get; set; }

    /// <summary>How far a point's or a line's height reaches from what it is drawn on, in blocks. A line's
    /// band is twice it, since it reaches either side of the centerline.</summary>
    [JsonPropertyName("r")]      public double Radius { get; set; } = 2;

    /// <summary>A line's or scarp's course, as <c>[x, z]</c> pairs.</summary>
    [JsonPropertyName("points")] public double[][]? Points { get; set; }

    /// <summary>An area's outline, as <c>[x, z]</c> pairs.</summary>
    [JsonPropertyName("ring")]   public double[][]? Ring { get; set; }
    // A point, an area and a rim state one height and a line states one per vertex, so the field reads
    // either — `"h": 9` and `"h": [7, 9, 5]` are both what an author would write, and making one of them
    // wrong would be a rule the format exists to avoid.
    /// <summary>The height the mark states. A point, an area and a rim state one and a line states one per
    /// vertex, so the field reads either — <c>"h": 9</c> and <c>"h": [7, 9, 5]</c> are both what an author
    /// would write.</summary>
    [JsonPropertyName("h"), JsonConverter(typeof(ScalarOrArrayJsonConverter))]
    public double[]? Heights { get; set; }
    /// <summary>How many cells in from the island's outline a rim states its height over.</summary>
    [JsonPropertyName("depth")]  public int Depth { get; set; } = 1;

    /// <summary>The height on a scarp's upper side.</summary>
    [JsonPropertyName("high")]   public double High { get; set; }

    /// <summary>The height on its lower side.</summary>
    [JsonPropertyName("low")]    public double Low { get; set; }

    /// <summary>How far the drop itself runs across, in blocks — a narrow face is a cliff, a wide one a
    /// slope.</summary>
    [JsonPropertyName("face")]   public double Face { get; set; } = 2;

    /// <summary>How far either side is held at its own height before the surface is free again.</summary>
    [JsonPropertyName("band")]   public double Band { get; set; } = 5;

    private double FirstHeight => Heights is { Length: > 0 } heights ? heights[0] : 0;

    /// <summary>The solver's mark, or null when the JSON does not carry what the kind needs — a half-written
    /// mark is dropped rather than allowed to place terrain nobody asked for.</summary>
    public Mark? ToMark() => Kind switch
    {
        "point" when At is { Length: >= 2 } at => new PointMark(at[0], at[1], FirstHeight, Radius),
        "line" when Points is { Length: >= 2 } => new LineMark(Points, Heights ?? [0], Radius),
        "area" when Ring is { Length: >= 3 } ring => new AreaMark(ring, FirstHeight),
        "rim" => new RimMark(FirstHeight, Depth),
        "scarp" when Points is { Length: >= 2 } points => new ScarpMark(points, High, Low, Face, Band),
        _ => null,
    };
}

/// <summary>A shape of ground lifted or lowered: the drawn ring, how far it lifts, and how its top and skirt
/// are shaped.</summary>
public sealed class ReliefPushJson
{
    /// <summary>The editor's handle on this push — see <see cref="ReliefMarkJson.Id"/>.</summary>
    [JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>The ring drawn around the ground to move, as <c>[x, z]</c> pairs.</summary>
    [JsonPropertyName("ring")]      public double[][]? Ring { get; set; }

    /// <summary>How far it lifts, in blocks. Negative lowers.</summary>
    [JsonPropertyName("amount")]    public double Amount { get; set; }

    /// <summary>An amount per ring vertex, where the lift varies around the outline rather than being
    /// even.</summary>
    [JsonPropertyName("amounts")]   public double[]? Amounts { get; set; }

    /// <summary>How far outside the ring the lift eases back to the ground, in blocks.</summary>
    [JsonPropertyName("falloff")]   public double Falloff { get; set; } = 10;

    /// <summary>How much the skirt wanders, so the lifted shape does not read as stamped.</summary>
    [JsonPropertyName("roughness")] public double Roughness { get; set; }

    /// <summary>How much the top domes rather than sitting flat.</summary>
    [JsonPropertyName("crown")]     public double Crown { get; set; }

    /// <summary>The noise row the roughness reads, so the shape is identical on every export.</summary>
    [JsonPropertyName("seed")]      public uint Seed { get; set; } = 1;

    public PushMark? ToPush() => Ring is { Length: >= 3 } ring
        ? new PushMark(ring, Amount, Falloff, Roughness, Seed, Amounts, Crown)
        : null;
}


/// <summary>Reads a number or an array of numbers into the same array, and writes a lone value back as a
/// number. It is what lets one mark shape carry a spot height and a falling ridgeline without asking an
/// author to remember which of them takes brackets.</summary>
public sealed class ScalarOrArrayJsonConverter : JsonConverter<double[]?>
{
    public override double[]? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.Number) return [reader.GetDouble()];
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("expected a number or an array");

        var values = new List<double>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.Number) throw new JsonException("expected a number");
            values.Add(reader.GetDouble());
        }
        return [.. values];
    }

    public override void Write(Utf8JsonWriter writer, double[]? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        if (value.Length == 1) { writer.WriteNumberValue(value[0]); return; }
        writer.WriteStartArray();
        foreach (var height in value) writer.WriteNumberValue(height);
        writer.WriteEndArray();
    }
}
