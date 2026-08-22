using System.Text.Json;
using PgmStudio.Export;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>The gate a sketch's bound <c>roomStyles</c> runs through before the layout is stored — the wool
/// cage and the spawn against the same universal checks, since neither wears a rule the other doesn't: the
/// style's own materials and geometry (<c>HS*</c>), and the one thing a style cannot answer about itself,
/// whether the shell it builds stands under the map's build ceiling (<c>WX10</c>).</summary>
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
        // And what neither can answer about itself: a shell too tall to stand under the map's build ceiling,
        // which is a fact about the pair of numbers rather than about the style's own materials.
        findings.AddRange(RoomStyleScope.Check(styles.Wool, "roomStyles.cage"));
        findings.AddRange(RoomStyleScope.Check(styles.Spawn, "roomStyles.spawn"));
        return findings;
    }
}
