using System.Text.Json;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Sketch;

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

    private static HouseStyle? Shell(JsonElement? snapshot, HouseStyle builtIn) => snapshot?.ValueKind switch
    {
        JsonValueKind.Null => null,                    // asked for open ground
        null or JsonValueKind.Undefined => builtIn,    // never asked
        _ => HouseStyleJson.DeserializeOr(snapshot.Value.GetRawText(), builtIn),
    };
}
