using System.Text.Json;
using PgmStudio.Export;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>The gate every house style in a sketch runs through before the layout is stored — the wool cage,
/// the spawn and every building the dressing places, against the same universal checks, since none of the
/// three wears a rule the others don't: the style's own materials and geometry (<c>HS*</c>), and the one
/// thing a style cannot answer about itself, whether the shell it builds stands under the map's build ceiling
/// (<c>WX10</c>).
///
/// <para>A placed building is a house style like the bound ones and is checked as one. <see cref="HouseProp"/>
/// carries its shell as a snapshot rather than a library reference, so the style entering the studio here is
/// the style the export stamps, and this is the only place it is read before it is built.</para></summary>
public static class SketchRoomStyleGate
{
    public static Findings Check(string layoutJson)
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
        return findings;
    }

    /// <summary>Every placed building's own shell, each finding under the prop that carries it. A dressing
    /// document that will not parse answers <c>DR-DOC</c> at export and nothing here — the same leniency the
    /// bound styles get, since a blob this gate cannot read is not a style it can judge.</summary>
    private static Findings Buildings(string layoutJson)
    {
        IReadOnlyList<PlacedProp> props;
        try { props = DressingScope.PropsOf(layoutJson); }
        catch (Exception fault) when (fault is JsonException or DressingParseException) { return Findings.None; }

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
