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
/// <para>A terrain theme is per cell because a shape may override it. A room style is <b>map-wide</b>: one
/// shell for every wool cage, one for every spawn cube. Rooms are fanned across the symmetry orbit so both
/// sides face the same building, and a cage that differed between teams would be a sightline that differed
/// between teams — so there is nothing per-room to look up, and this answers a pair rather than a function.</para>
/// </summary>
public static class RoomStyleScope
{
    /// <summary>The pair a map binds, <b>either of which may be none</b> — a pad on open ground rather than a
    /// building over it, which is what a spawn on a plateau the plan already shaped often wants to be.
    ///
    /// <para>Three answers, because absent and none are different questions. A snapshot that is absent — or
    /// unreadable, since it is a hand-editable leaf — falls back to that kind's built-in shell, so a sketch
    /// that never opened the step exports exactly as it did before the step existed. A snapshot that is
    /// explicitly null asked for no building, and gets none.</para></summary>
    public static (HouseStyle? Wool, HouseStyle? Spawn) StylesOf(string layoutJson)
        => StylesOf(SketchLayout.Parse(layoutJson));

    public static (HouseStyle? Wool, HouseStyle? Spawn) StylesOf(SketchLayout? layout)
    {
        var bound = layout?.RoomStyles;
        return (Shell(bound?.Wool, HouseStyle.Wool), Shell(bound?.Spawn, HouseStyle.Spawn));
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
    /// <para><b>Asked on the smallest shell a room may be</b> (<see cref="RoomFrames.MinShellSpan"/>). Every
    /// sloped form climbs with the span it crosses, so a style's height is at its lowest there and a bigger
    /// footprint only ever raises it: a style refused here is one no footprint could have saved.</para></summary>
    public static Findings Check(HouseStyle? style, string field)
    {
        if (style is null) return Findings.None;       // open ground carries no shell to be too tall
        var span = RoomFrames.MinShellSpan;
        var reach = style.TopLayerOver(span, span);
        if (reach <= BuildCeiling.OverGround) return Findings.None;
        return new List<Finding>
        {
            new(RoomFrameRules.ShellOverCeiling,
                $"the shell stands {reach} courses over its floor even on the smallest room there is "
                + $"({span}×{span}), past the {BuildCeiling.OverGround}-course build ceiling — and the goal "
                + $"marker hangs {BuildCeiling.MarkerOver} courses above that, inside the building",
                Field: field),
        };
    }

    private static HouseStyle? Shell(JsonElement? snapshot, HouseStyle builtIn) => snapshot?.ValueKind switch
    {
        JsonValueKind.Null => null,                    // asked for open ground
        null or JsonValueKind.Undefined => builtIn,    // never asked
        _ => HouseStyleJson.DeserializeOr(snapshot.Value.GetRawText(), builtIn),
    };
}
