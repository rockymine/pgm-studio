using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export;

/// <summary>
/// The room styles a map's shells are stamped with (docs/world-export/structures.md §9): the snapshots on the
/// <see cref="SketchLayout"/> read back into the stamper's model. <see cref="TerrainThemeScope"/>'s sibling,
/// with one difference that is the point — there is no scope to resolve.
///
/// <para>The map binds one shell for every wool room and one for every spawn room, and a room's house may
/// state a style of its own over it (<see cref="StyleOf"/>). A room is stored once and fanned across the
/// symmetry orbit, so every team's image of it wears the same style either way.</para>
/// </summary>
public static class RoomStyleScope
{
    /// <summary>The pair a map binds, <b>either of which may be none</b> — a pad on open ground rather than a
    /// building over it, which is what a spawn on a plateau the plan already shaped often wants to be.
    ///
    /// <para>Three answers, because absent and none are different questions. A snapshot that is absent — or
    /// unreadable, since it is a hand-editable leaf — falls back to that kind's built-in shell, so a sketch
    /// that never opened the step exports the shipped rooms. A snapshot that is explicitly null asked for no
    /// building, and gets none.</para>
    ///
    /// <para>A binding is a wish: whether a shell actually stands is the room's, resolved per footprint
    /// (<see cref="RoomFrameRules.FootprintTooSmall"/>), and the stampers read the frame's wall rather than
    /// this pair.</para></summary>
    public static RoomShells StylesOf(string layoutJson)
        => StylesOf(SketchLayout.Parse(layoutJson));

    public static RoomShells StylesOf(SketchLayout? layout)
    {
        var bound = layout?.RoomStyles;
        return new RoomShells(Shell(bound?.Wool, HouseStyle.Wool), Shell(bound?.Spawn, HouseStyle.Spawn));
    }

    /// <summary>What a bound shell's own height refuses (<see cref="RoomFrameRules.ShellOverCeiling"/>): a
    /// style that cannot stand under the build ceiling on <b>any</b> footprint a room may take.
    ///
    /// <para>A goal marker hangs <see cref="BuildCeiling.MarkerOver"/> blocks over the ceiling and a wool
    /// room's shell is authored geometry subject to no cap, so a tall stack swallows the marker standing over
    /// it. Nothing else compares the two — the world builder clamps a shell's floor against the world's own
    /// 255 and never against this — and a correction at stamp time would silently shorten a building the
    /// author drew, so this refuses at the point the style is bound.</para>
    ///
    /// <para><b>Asked on the smallest footprint a shell can stand on</b> (<see cref="RoomFrames.MinSpan"/>
    /// with a wall). Every sloped form climbs with the span it crosses, so a style's height is at its lowest
    /// there and a bigger footprint only ever raises it: a style refused here is one none could have
    /// saved.</para></summary>
    public static Findings Check(HouseStyle? style, string field)
    {
        if (style is null) return Findings.None;       // open ground carries no shell to be too tall
        var span = RoomFrames.MinSpan(walled: true);
        var reach = style.TopLayerOver(span, span);
        if (reach <= BuildCeiling.OverGround) return Findings.None;
        return new List<Finding>
        {
            new(RoomFrameRules.ShellOverCeiling,
                $"the shell stands {reach} courses over its floor on a footprint of {span} by {span} blocks, "
                + $"more than the build ceiling of {BuildCeiling.OverGround} courses",
                Field: field),
        };
    }

    /// <summary>The style a room's <paramref name="house"/> is stamped in: its own where it states one —
    /// null asking for open ground — else the map's for its kind, <paramref name="mapWide"/>. An own style
    /// that does not read wears the map's, or <paramref name="builtIn"/> where the map asks for none.</summary>
    public static HouseStyle? StyleOf(Pgm.Authoring.RoomHouse? house, HouseStyle? mapWide, HouseStyle builtIn) =>
        house is { Style.ValueKind: not JsonValueKind.Undefined } ? Shell(house.Style, mapWide ?? builtIn) : mapWide;

    private static HouseStyle? Shell(JsonElement? snapshot, HouseStyle builtIn) => snapshot?.ValueKind switch
    {
        JsonValueKind.Null => null,                    // asked for open ground
        null or JsonValueKind.Undefined => builtIn,    // never asked
        _ => HouseStyleJson.DeserializeOr(snapshot.Value.GetRawText(), builtIn),
    };
}

/// <summary>The shell each kind of room is stamped in, as <see cref="RoomStyleScope.StylesOf(string)"/> resolves it
/// from a board's finish: a bound style, that kind's built-in shell, or <b>null</b> for open ground.
///
/// <para>It travels rather than being re-asked because a room with no building over it takes its whole
/// region, while one with a shell is inset by a wall (<see cref="RoomFrames"/>) and sizes a different default
/// rectangle; the frame resolvers take it and ask each room's house over it (<see cref="RoomStyleScope.StyleOf"/>).
/// A caller that guesses names a different room from the one the export stamps.</para></summary>
public readonly record struct RoomShells(HouseStyle? Wool, HouseStyle? Spawn)
{

    /// <summary>Both kinds on their built-in shell: what a board that bound nothing is stamped with, and the
    /// pair to state where there is no board to read one from.</summary>
    public static RoomShells BuiltIn { get; } = new(HouseStyle.Wool, HouseStyle.Spawn);
}
