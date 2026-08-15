using System.Reflection;
using System.Text.Json.Serialization;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Minecraft.Palette;

/// <summary>Which fact about a cell a material varies with — the half of a pattern's description that decides
/// where it can be used at all, and the half a field list cannot carry.
///
/// <para>It is the answer to "why did my wall run come out one colour": a run varies along the perimeter arc,
/// so it says nothing in the middle of a plateau, and a frame reads the bend and the direction, so it needs a
/// traced outline rather than a synthesised one. An author or an agent choosing a pattern is choosing where it
/// will be legible as much as what it looks like.</para></summary>
public enum CellFact
{
    /// <summary>The cell's own x/z. Everything that draws a field over the ground reads this and nothing
    /// else — it works in any bucket, anywhere.</summary>
    Position,

    /// <summary>How far down from the top of the bucket. A depth stack's axis.</summary>
    Depth,

    /// <summary>How far in from the landmass's void-facing edge. An inward stack's axis: a material reading it
    /// draws concentric rings and answers its fallback everywhere off a footprint.</summary>
    Inset,

    /// <summary>How far round the outer perimeter. A material reading it varies only on cells the boundary
    /// trace reached — a wall, a rim, an island's edge — and is flat everywhere inside.</summary>
    Arc,

    /// <summary>How sharply the perimeter bends here, and which way it runs. Needs a traced outline; corners
    /// are where these two say something a plain arc cannot.</summary>
    Bend,

    /// <summary>The cell's height above the bottom of its band. What shears a stripe onto the diagonal.</summary>
    Height,

    /// <summary>The owning team's colour, where the cell belongs to one.</summary>
    Team,
}

/// <summary>One field a material carries, as the deserializer will accept it.</summary>
/// <param name="Name">The JSON name — camelCase, the same string the reader binds.</param>
/// <param name="Type">A wire type word: <c>int</c>, <c>bool</c>, <c>material</c>, <c>material[]</c>,
/// <c>band[]</c>, <c>stripe[]</c>, <c>bandStack</c>, or an enum's name.</param>
/// <param name="Required">Whether the constructor demands it. A field with no default has to be written.</param>
/// <param name="Default">What it is when left out, for an optional one.</param>
/// <param name="Choices">The values an enum field takes, absent for everything else.</param>
public readonly record struct MaterialField(
    string Name, string Type, bool Required, object? Default = null, IReadOnlyList<string>? Choices = null);

/// <summary>One material kind, whole: what it is, what it varies with, and what it takes.</summary>
public readonly record struct MaterialKindInfo(
    string Kind, string Name, string Summary, IReadOnlyList<CellFact> Reads, IReadOnlyList<MaterialField> Fields);

/// <summary>
/// Every terrain-paint material kind as data — the discriminator, a sentence, the cell facts it varies with,
/// and its fields with their types and defaults.
///
/// <para><b>The kinds and the fields are read off the types, not restated.</b> The list comes from
/// <see cref="TerrainMaterial"/>'s own <c>JsonDerivedType</c> attributes and each entry's fields from that
/// record's primary constructor, so this cannot offer a kind the deserializer would reject, miss one it
/// accepts, or name a default that is not the one the painter uses. Only the prose and the
/// <see cref="CellFact"/> list are written by hand, because neither is derivable.</para>
///
/// <para><b>Why it exists.</b> The fourteen kinds were reachable only through a refusal message that listed
/// their names, and nothing anywhere gave a kind's fields — so an author or an agent could discover that
/// <c>turbulence</c> is a word the parser accepts and not what it takes or what it does. The observed effect
/// was reaching for the same two or three patterns and leaving the rest unused.</para>
/// </summary>
public static class MaterialVocabulary
{
    // What each kind is and what it varies with. The order here is the order they are offered in; a kind
    // present on TerrainMaterial and absent here is a build error rather than a silent omission (see Build).
    private static readonly (string Kind, string Name, string Summary, CellFact[] Reads)[] Described =
    [
        ("solid", "Solid block", "One block everywhere in the bucket.", []),
        ("layered", "Band stack",
            "An ordered run of materials, each claiming a thickness, read along a stated axis: down from the "
          + "top of the bucket, or in from the landmass's edge. Reads inset only when its axis says inward.",
            [CellFact.Depth, CellFact.Inset]),
        ("teamTint", "Team tint",
            "A colour-by-damage block — clay, wool, stained glass — stamped with the owning team's colour, "
          + "falling back to a neutral material where no team owns the cell.", [CellFact.Team]),
        ("voronoi", "Voronoi cells",
            "Irregular cells grown from scattered seeds: the ground broken into patches with hard edges. Its "
          + "bands are read outward from each cell's centre, so a one-block first band draws a grid of lines.",
            [CellFact.Position]),
        ("cell", "Cell patches",
            "Square patches on a jittered grid, warped so the edges wander. Reads as tiling where a voronoi "
          + "reads as growth.", [CellFact.Position]),
        ("noise", "Fractal field",
            "Fractal noise across the ground, its value picking a stop from the palette — the softest of the "
          + "three fields, with broad drifting regions.", [CellFact.Position]),
        ("turbulence", "Turbulence",
            "The same field folded, which doubles its frequency and puts a crease where the fold lands: "
          + "billowy rather than drifting.", [CellFact.Position]),
        ("electric", "Electric",
            "The field ridged rather than folded, so the creases become bright veins running through it.",
            [CellFact.Position]),
        ("checker", "Checkerboard",
            "Two materials alternating on a square of the given size, laid in the face it paints: on a wall the "
          + "squares tile the arc and the height, so they read as squares to whoever is looking at it, and "
          + "anywhere else they tile the two ground axes. That is why it reads position on open ground and the "
          + "perimeter on a face — one pattern, two frames.",
            [CellFact.Position, CellFact.Arc, CellFact.Height]),
        ("logChecker", "Log checkerboard",
            "A checkerboard of one log, turning it upright on one square and on its side on the next, so the "
          + "board reads as grain rather than as colour. Laid in the face it paints, like any checkerboard.",
            [CellFact.Position, CellFact.Arc, CellFact.Height]),
        ("laidLog", "Laid log",
            "One log laid along the face it is painting — across a wall rather than up it, so a beam reads as "
          + "a beam, and standing upright where the wall turns. It varies the log's axis and never its "
          + "material, so it is the one kind a colour swatch cannot show: the picture is the same block "
          + "whichever way it is turned.", [CellFact.Bend]),
        ("wallRun", "Wall stripes",
            "Stripes wrapping the outer perimeter, each with its own width. Varies along the arc, so it "
          + "stripes a wall or a rim and is flat everywhere inside a plateau.", [CellFact.Arc]),
        ("wallDiagonal", "Diagonal stripes",
            "The same stripes sheared by height, so they climb a wall at a slope instead of standing upright.",
            [CellFact.Arc, CellFact.Height]),
        ("wallFrame", "Wall frame",
            "An edge material inked round a wall wherever it turns sharply enough, with a fill panelled inside "
          + "it — and its top and bottom courses inked too, so the panel is framed on all four sides. A wall "
          + "material through and through: seen from directly above, one course deep, every cell is a top "
          + "course and the whole swatch is edge. The elevation is the view that shows it.",
            [CellFact.Depth, CellFact.Height, CellFact.Bend]),
    ];

    /// <summary>Every kind, in the order the pickers offer them: the plain ones, then the composites, then the
    /// fields, then the wall-geometry ones.</summary>
    public static IReadOnlyList<MaterialKindInfo> All { get; } = Build();

    /// <summary>One kind by its discriminator, or null for a word this build does not know.</summary>
    public static MaterialKindInfo? Of(string kind)
    {
        foreach (var entry in All) if (entry.Kind == kind) return entry;
        return null;
    }

    private static IReadOnlyList<MaterialKindInfo> Build()
    {
        // The discriminators the deserializer actually accepts, taken from the attributes that define them.
        var derived = typeof(TerrainMaterial)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .ToDictionary(a => (string)a.TypeDiscriminator!, a => a.DerivedType);

        var all = new List<MaterialKindInfo>(Described.Length);
        foreach (var (kind, name, summary, reads) in Described)
        {
            if (!derived.TryGetValue(kind, out var type))
                throw new InvalidOperationException(
                    $"the vocabulary describes '{kind}', which TerrainMaterial does not declare");
            all.Add(new MaterialKindInfo(kind, name, summary, reads, FieldsOf(type)));
        }

        // The other direction: a kind the painter accepts and nobody described would be undiscoverable, which
        // is the whole fault this type exists to fix, so it is a failure rather than a gap.
        var missing = derived.Keys.Except(all.Select(entry => entry.Kind)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"TerrainMaterial declares {string.Join(", ", missing)}, which the vocabulary does not describe");
        return all;
    }

    /// <summary>A record's primary constructor, as fields. The parameter names are the JSON names — the
    /// serializer camelCases them and binds by the same names — so reading them here is reading the contract
    /// rather than a description of it.</summary>
    private static IReadOnlyList<MaterialField> FieldsOf(Type type)
    {
        var ctor = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        return [.. ctor.GetParameters().Select(p => new MaterialField(
            Camel(p.Name!),
            WireType(p.ParameterType),
            !p.HasDefaultValue,
            p.HasDefaultValue ? Wire(p.DefaultValue) : null,
            p.ParameterType.IsEnum ? Enum.GetNames(p.ParameterType).Select(Camel).ToList() : null))];
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    /// <summary>An enum default goes on the wire as its name, because that is how it is written back.</summary>
    private static object? Wire(object? value) => value?.GetType().IsEnum == true ? Camel(value.ToString()!) : value;

    private static string WireType(Type type)
    {
        var inner = Nullable.GetUnderlyingType(type) ?? type;
        if (inner.IsEnum) return Camel(inner.Name);
        if (inner == typeof(int) || inner == typeof(uint)) return "int";
        if (inner == typeof(bool)) return "bool";
        if (inner == typeof(string)) return "string";
        if (inner == typeof(BandStack)) return "bandStack";
        if (typeof(TerrainMaterial).IsAssignableFrom(inner)) return "material";
        if (inner.IsGenericType)
        {
            var item = inner.GetGenericArguments()[0];
            if (typeof(TerrainMaterial).IsAssignableFrom(item)) return "material[]";
            return Camel(item.Name) + "[]";
        }
        return Camel(inner.Name);
    }
}
