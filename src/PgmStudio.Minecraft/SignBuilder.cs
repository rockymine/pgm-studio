using System.Text.Json;
using fNbt;
using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>One formatted line of sign text: the string plus an optional colour (a Minecraft chat-colour
/// name) and bold/italic flags. Serialised as a 1.8 JSON text component.</summary>
public readonly record struct SignLine(string Text, string? Color = null, bool Bold = false, bool Italic = false);

/// <summary>
/// Builds 1.8 wall-sign blocks + <c>Sign</c> tile entities (Text1–4 as JSON text components, the format
/// real maps store). Also maps a wool colour slug to the nearest Minecraft chat colour for sign text.
/// </summary>
public static class SignBuilder
{
    private const string EmptyLine = "{\"text\":\"\"}";

    // Wool colour slug → nearest Minecraft chat-colour name (the 16 dyes don't all have exact chat colours).
    private static readonly Dictionary<string, string> WoolChatColor = new()
    {
        ["white"] = "white", ["orange"] = "gold", ["magenta"] = "light_purple", ["light_blue"] = "aqua",
        ["yellow"] = "yellow", ["lime"] = "green", ["pink"] = "light_purple", ["gray"] = "dark_gray",
        ["silver"] = "gray", ["cyan"] = "dark_aqua", ["purple"] = "dark_purple", ["blue"] = "blue",
        ["brown"] = "dark_red", ["green"] = "dark_green", ["red"] = "red", ["black"] = "black",
    };

    /// <summary>Nearest chat-colour name for a wool colour slug (defaults to white).</summary>
    public static string ChatColor(string woolSlug)
        => WoolChatColor.GetValueOrDefault(WoolColors.Normalize(woolSlug), "white");

    /// <summary>The 1.8 wall-sign (id 68) block data for a sign whose front faces <paramref name="facing"/>.</summary>
    public static int WallSignData(Facing facing) => facing switch
    {
        Facing.NegZ => 2,   // faces north
        Facing.PosZ => 3,   // faces south
        Facing.NegX => 4,   // faces west
        Facing.PosX => 5,   // faces east
        _ => 2,
    };

    /// <summary>Serialise one line as a 1.8 JSON text component.</summary>
    public static string LineJson(SignLine line)
    {
        var obj = new Dictionary<string, object> { ["text"] = line.Text };
        if (line.Color is not null) obj["color"] = line.Color;
        if (line.Bold) obj["bold"] = true;
        if (line.Italic) obj["italic"] = true;
        return JsonSerializer.Serialize(obj);
    }

    /// <summary>A <c>Sign</c> tile entity at the given coords with up to four lines (extras ignored, missing
    /// lines blank).</summary>
    public static NbtCompound Sign(int x, int y, int z, IReadOnlyList<SignLine> lines)
    {
        var sign = new NbtCompound
        {
            new NbtString("id", "Sign"),
            new NbtInt("x", x),
            new NbtInt("y", y),
            new NbtInt("z", z),
        };
        for (var i = 0; i < 4; i++)
            sign.Add(new NbtString($"Text{i + 1}", i < lines.Count ? LineJson(lines[i]) : EmptyLine));
        return sign;
    }

    /// <summary>Place a wall sign (block + tile entity) at the given coords, its front facing
    /// <paramref name="facing"/>.</summary>
    public static void PlaceWallSign(VoxelWorld world, int x, int y, int z, Facing facing, IReadOnlyList<SignLine> lines)
    {
        world.SetBlock(x, y, z, Blocks.WallSign, WallSignData(facing));
        world.AddTileEntity(x, z, Sign(x, y, z, lines));
    }
}
