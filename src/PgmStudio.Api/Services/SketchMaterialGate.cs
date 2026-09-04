using System.Text.Json;
using PgmStudio.Export;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>The gate everything a sketch's <b>finish</b> is made of runs through before the layout is stored:
/// the house styles it binds and the terrain themes it registers. One gate because they enter together and
/// are built together — a board is refused for what it is made of, whichever half of the finish said it.
///
/// <para><b>The styles</b> are the wool cage, the spawn and every building the dressing places, against the
/// same universal checks, since none of the three wears a rule the others don't: the style's own materials and
/// geometry (<c>HS*</c>), and the one thing a style cannot answer about itself, whether the shell it builds
/// stands under the map's build ceiling (<c>WX10</c>). A placed building is a house style like the bound ones
/// and is checked as one — <see cref="HouseProp"/> carries its shell as a snapshot rather than a library
/// reference, so the style entering the studio here is the style the export stamps.</para>
///
/// <para><b>The themes</b> answer for what their own materials cannot do at the depth a bucket claims
/// (<c>PT*</c>), and — where the caller is paying for the ground anyway — for what a theme cannot do on the
/// shape it was scoped to (<c>SK23</c>), which is the one theme question that needs the board rasterized and
/// so rides the same <see cref="LayoutReading"/> the sketch check is taking at the same call site. Neither
/// half is read anywhere else before the world is built.</para></summary>
public static class SketchMaterialGate
{
    public static Findings Check(string layoutJson, LayoutReading reading = LayoutReading.Ground)
    {
        // A layout the room-style shape does not parse against is not this gate's business — the blob is
        // authoring-source JSON of arbitrary shape, and only a well-formed roomStyles snapshot is checked, the
        // same leniency RoomStyleScope already gives export.
        (HouseStyle? Wool, HouseStyle? Spawn) styles;
        try { styles = RoomStyleScope.StylesOf(layoutJson); }
        catch (JsonException) { return Findings.None; }

        var findings = new List<Finding>();
        if (styles.Wool is { } wool)
            findings.AddRange(HouseStyleValidation.Check(wool).Under("roomStyles.cage"));
        if (styles.Spawn is { } spawn)
            findings.AddRange(HouseStyleValidation.Check(spawn).Under("roomStyles.spawn"));
        findings.AddRange(Buildings(layoutJson));
        // And what neither can answer about itself: a shell too tall to stand under the map's build ceiling,
        // which is a fact about the pair of numbers rather than about the style's own materials.
        findings.AddRange(RoomStyleScope.Check(styles.Wool, "roomStyles.cage"));
        findings.AddRange(RoomStyleScope.Check(styles.Spawn, "roomStyles.spawn"));
        findings.AddRange(Themes(layoutJson));
        findings.AddRange(Materials(layoutJson));
        // SK23 walks every column of the board's extent to ask which of a shape's are edges, so it is a
        // ground rule and a partial write skips it exactly as it skips the sketch check's own — and says so
        // through `SketchLayoutCheck.GroundRules`, which names it.
        if (reading == LayoutReading.Ground) findings.AddRange(TerrainThemeScope.Check(layoutJson));
        return findings;
    }

    /// <summary>Every theme the layout registers, against what a theme's own materials cannot do. A registry
    /// entry that will not parse as a theme is left alone: the painter drops it the same way, and a blob this
    /// gate cannot read is not a theme it can judge.</summary>
    private static Findings Themes(string layoutJson)
    {
        var findings = new List<Finding>();
        foreach (var (id, theme) in TerrainThemeScope.ThemesOf(layoutJson))
            findings.AddRange(TerrainThemeValidation.Check(theme).Under($"themes.{id}"));
        return findings;
    }

    /// <summary>Every shape stating what it is <b>made of</b>, against the same rules a theme's buckets answer
    /// — because a shape's material is a theme with one bucket (<see cref="TerrainTheme.OfMaterial"/>) and it
    /// is checked as the one it is. The depth rule is the one that bites here and does not on ground: a
    /// material claims the shape's <em>whole span</em>, so a block that only surfaces ground — grass, podzol,
    /// a path — would be written into every course of it (<c>PT1</c>), which a theme's one-course surface
    /// bucket never does.</summary>
    private static Findings Materials(string layoutJson)
    {
        var layout = SketchLayout.Parse(layoutJson);
        if (layout is null) return Findings.None;
        var findings = new List<Finding>();
        foreach (var layer in SketchLayout.Stack(layout))
            foreach (var shape in layer.Shapes)
                if (TerrainThemeScope.MaterialOf(shape) is { } material)
                    findings.AddRange(TerrainThemeValidation
                        .Check(TerrainTheme.OfMaterial(material, TerrainTheme.Default))
                        .Under($"layers.{layer.Id}.shapes.{shape.Id}.material"));
        return findings;
    }

    /// <summary>Every placed building's own shell, each finding under the prop that carries it.
    ///
    /// <para>A dressing document that will not parse is answered here rather than left to the export.
    /// The gate cannot judge a style it cannot read, but "this will not parse" is itself the finding, and it
    /// is the one an author can act on: the export reads the same document while it is building the world, so
    /// a fault carried that far arrives after the ground is laid and with no field named. A
    /// <see cref="DressingParseException"/> already states itself as <c>DR-DOC</c>; a binder fault carries the
    /// JSON path it gave up at, which is the field to fix.</para></summary>
    private static Findings Buildings(string layoutJson)
    {
        IReadOnlyList<PlacedProp> props;
        try { props = DressingScope.PropsOf(layoutJson); }
        catch (DressingParseException fault) { return new List<Finding> { fault.Finding }; }
        catch (JsonException fault)
        {
            return new List<Finding>
            {
                new(DressingParseException.Rule, fault.Message,
                    Field: string.IsNullOrEmpty(fault.Path) ? "dressing" : fault.Path),
            };
        }

        var findings = new List<Finding>();
        var at = 0;
        foreach (var prop in props)
        {
            if (prop is HouseProp house)
                findings.AddRange(HouseStyleValidation.Check(house.Style)
                    .Under($"dressing.props[{at}].style")
                    .Select(finding => finding with { Subjects = [house.Id.Length > 0 ? house.Id : $"props[{at}]"] }));
            at++;
        }
        return findings;
    }
}
