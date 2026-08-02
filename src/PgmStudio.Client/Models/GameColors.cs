using System.Text.Json;

namespace PgmStudio.Client.Models;

/// <summary>
/// Minecraft 1.8 colour tables from Bukkit, loaded from the shared <c>game-colors.json</c> — the same file the
/// JS side reads (<c>render/palette.js</c>), so the two copies cannot drift. <see cref="ChatColors"/> is
/// <c>org.bukkit.ChatColor</c> (chat / formatting colours), <see cref="DyeColors"/> is <c>org.bukkit.DyeColor</c>
/// (dye / wool colours, indexed by data value); the hex is display-only, the value is what the wire carries.
/// Team-colour assignment is a studio convenience, not a Bukkit thing, so its order lives here in code.
/// </summary>
public static class GameColors
{
    public readonly record struct Color(string Value, string Label, string Hex);

    /// <summary><c>org.bukkit.ChatColor</c>, in enum-ordinal order (black 0 … white 15).</summary>
    public static readonly Color[] ChatColors;

    /// <summary><c>org.bukkit.DyeColor</c>, indexed by data value (white 0 … black 15).</summary>
    public static readonly Color[] DyeColors;

    static GameColors()
    {
        var assembly = typeof(GameColors).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith("game-colors.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var doc = JsonDocument.Parse(stream);
        ChatColors = ReadTable(doc.RootElement.GetProperty("chat"));
        DyeColors = ReadTable(doc.RootElement.GetProperty("dye"));
    }

    // Parse via JsonDocument rather than a typed deserialize: the WASM app disables reflection-based
    // serialization, and this keeps the load working without a source-generated context for one small file.
    private static Color[] ReadTable(JsonElement array)
    {
        var colors = new List<Color>(array.GetArrayLength());
        foreach (var entry in array.EnumerateArray())
            colors.Add(new Color(
                entry.GetProperty("value").GetString()!,
                entry.GetProperty("label").GetString()!,
                entry.GetProperty("hex").GetString()!));
        return [.. colors];
    }

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

    /// <summary>The first <paramref name="n"/> team colours in priority order (red, blue, green, …).</summary>
    public static IReadOnlyList<Color> FirstTeamColors(int n)
    {
        var result = new List<Color>();
        var used = new List<string?>();
        for (var i = 0; i < n && NextTeamColor(used) is { } c; i++) { result.Add(c); used.Add(c.Value); }
        return result;
    }
}
