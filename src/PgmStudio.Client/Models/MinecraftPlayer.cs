using System.Net.Http.Json;
using System.Text.Json;

namespace PgmStudio.Client.Models;

/// <summary>Minecraft player identity resolution shared by the author editors (the Overview activity and
/// the Configure Info phase): the mc-heads avatar URL + placeholder, and the Mojang lookup via
/// <c>GET api/minecraft/player</c>. Both editors resolved a typed username to its canonical uuid+name on
/// blur and rendered the head from the uuid; that logic lives here once.</summary>
public static class MinecraftPlayer
{
    /// <summary>1×1 transparent gif — the avatar placeholder shown before a username resolves.</summary>
    public const string AvatarEmpty = "data:image/gif;base64,R0lGODlhEAAQAAAAACwAAAAAEAAQAAABEIQBADs=";

    /// <summary>The mc-heads 16px head for a resolved uuid, or the placeholder when there is none.</summary>
    public static string Avatar(string? uuid) =>
        string.IsNullOrEmpty(uuid) ? AvatarEmpty : $"https://mc-heads.net/avatar/{uuid}/16";

    /// <summary>Look a player up by username <em>or</em> uuid → its canonical <c>(uuid, name)</c>, or null
    /// on an empty value, an unknown player, or Mojang being unreachable. A value containing a dash and
    /// longer than 30 chars is treated as a uuid; otherwise a username.</summary>
    public static async Task<(string Uuid, string Name)?> ResolveAsync(HttpClient http, string value)
    {
        var val = value.Trim();
        if (val.Length == 0) return null;
        var isUuid = val.Contains('-') && val.Length > 30;
        var q = isUuid ? $"uuid={Uri.EscapeDataString(val)}" : $"name={Uri.EscapeDataString(val)}";
        try
        {
            var r = await http.GetFromJsonAsync<JsonElement>($"api/minecraft/player?{q}");
            return (Str(r, "uuid"), Str(r, "name"));
        }
        catch { return null; }
    }

    private static string Str(JsonElement e, string key)
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
