namespace PgmStudio.Client.Models;

/// <summary>PGM chat / Minecraft dye colour palettes (port of shared/game-colors.js).</summary>
public static class GameColors
{
    public readonly record struct Color(string Value, string Label, string Hex);

    public static readonly Color[] ChatColors =
    [
        new("black", "Black", "#000000"), new("dark blue", "Dark Blue", "#0000AA"),
        new("dark green", "Dark Green", "#00AA00"), new("dark aqua", "Dark Aqua", "#00AAAA"),
        new("dark red", "Dark Red", "#AA0000"), new("dark purple", "Dark Purple", "#AA00AA"),
        new("gold", "Gold", "#FFAA00"), new("gray", "Gray", "#AAAAAA"),
        new("dark gray", "Dark Gray", "#555555"), new("blue", "Blue", "#5555FF"),
        new("green", "Green", "#55FF55"), new("aqua", "Aqua", "#55FFFF"),
        new("red", "Red", "#FF5555"), new("light purple", "Light Purple", "#FF55FF"),
        new("yellow", "Yellow", "#FFFF55"), new("white", "White", "#FFFFFF"),
    ];

    public static readonly Color[] DyeColors =
    [
        new("white", "White", "#FFFFFF"), new("orange", "Orange", "#D87F33"),
        new("magenta", "Magenta", "#B24CD8"), new("light blue", "Light Blue", "#6699D8"),
        new("yellow", "Yellow", "#E5E533"), new("lime", "Lime", "#7FCC19"),
        new("pink", "Pink", "#F27FA5"), new("gray", "Gray", "#4C4C4C"),
        new("silver", "Silver", "#999999"), new("cyan", "Cyan", "#4C7F99"),
        new("purple", "Purple", "#7F3FB2"), new("blue", "Blue", "#334CB2"),
        new("brown", "Brown", "#664C33"), new("green", "Green", "#667F33"),
        new("red", "Red", "#993333"), new("black", "Black", "#191919"),
    ];

    // Auto-assign order for a new team's colour (bright, distinguishable first).
    private static readonly string[] TeamColorPriority =
    [
        "red", "blue", "green", "yellow", "aqua", "gold", "light purple", "dark purple",
        "dark aqua", "dark green", "dark red", "dark blue", "gray", "dark gray", "white", "black",
    ];

    private static string Norm(string? name) => (name ?? "").Replace('_', ' ').ToLowerInvariant();

    public static string ChatHex(string? name)
        => ChatColors.FirstOrDefault(c => c.Value == Norm(name)).Hex is { Length: > 0 } h ? h : "#475569";

    public static string DyeHex(string? name)
        => DyeColors.FirstOrDefault(c => c.Value == Norm(name)).Hex is { Length: > 0 } h ? h : "#475569";

    /// <summary>Next unused team colour in priority order, or null when all 16 are taken.</summary>
    public static Color? NextTeamColor(IEnumerable<string?> usedColors)
    {
        var used = usedColors.Select(Norm).ToHashSet();
        foreach (var value in TeamColorPriority)
            if (!used.Contains(value)) return ChatColors.First(c => c.Value == value);
        return null;
    }
}
