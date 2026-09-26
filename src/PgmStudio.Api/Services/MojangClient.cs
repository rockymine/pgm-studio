using System.Text.Json;
using System.Text.RegularExpressions;

namespace PgmStudio.Api.Services;

/// <summary>
/// Resolves a Minecraft username or UUID to <c>(uuid, name)</c> via Mojang's public APIs.
/// Mojang profile lookup: a dashed-UUID lookup hits the session
/// server, a username lookup hits the profiles API. Returns the canonical dashed uuid + current name.
/// </summary>
public sealed partial class MojangClient(HttpClient http)
{
    [GeneratedRegex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex UuidRe();

    /// <summary>Look up a player. Throws <see cref="InvalidOperationException"/> when not found.</summary>
    public async Task<(string Uuid, string Name)> LookupAsync(string nameOrUuid, CancellationToken ct)
    {
        var url = UuidRe().IsMatch(nameOrUuid)
            ? $"https://sessionserver.mojang.com/session/minecraft/profile/{nameOrUuid.Replace("-", "")}"
            : $"https://api.mojang.com/users/profiles/minecraft/{Uri.EscapeDataString(nameOrUuid)}";

        using var resp = await http.GetAsync(url, ct);
        // Mojang returns 204/404 (and historically an empty 200) for an unknown player.
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode || string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException($"Player not found: {nameOrUuid} ({(int)resp.StatusCode})");

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(id)) throw new InvalidOperationException($"Player not found: {nameOrUuid}");
        var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? nameOrUuid : nameOrUuid;
        return (FormatUuid(id), name);
    }

    /// <summary>The largest skin the studio keeps. A skin is a 64×64 PNG of a few kilobytes; anything past
    /// this is not one.</summary>
    public const int MaxSkinBytes = 64 * 1024;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// A player's name and skin PNG, from the session server's profile and the texture it names. Only a
    /// texture on <c>textures.minecraft.net</c> is fetched, over https, and only a PNG no larger than
    /// <see cref="MaxSkinBytes"/> is answered. Null where the player has no skin or either server does not
    /// answer with one.
    /// </summary>
    public async Task<(string Name, byte[] Skin)?> SkinAsync(string uuid, CancellationToken ct)
    {
        if (!UuidRe().IsMatch(uuid)) return null;
        using var profile = await http.GetAsync(
            $"https://sessionserver.mojang.com/session/minecraft/profile/{uuid.Replace("-", "")}", ct);
        if (!profile.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await profile.Content.ReadAsStringAsync(ct));
        var name = doc.RootElement.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
        if (!doc.RootElement.TryGetProperty("properties", out var properties)) return null;

        string? url = null;
        foreach (var property in properties.EnumerateArray())
        {
            if (property.GetProperty("name").GetString() != "textures") continue;
            var textures = JsonDocument.Parse(Convert.FromBase64String(property.GetProperty("value").GetString() ?? ""));
            if (textures.RootElement.GetProperty("textures").TryGetProperty("SKIN", out var skin))
                url = skin.GetProperty("url").GetString();
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var texture) || texture.Host != "textures.minecraft.net")
            return null;

        using var response = await http.GetAsync(
            new UriBuilder(texture) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri,
            HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxSkinBytes) return null;
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return bytes.Length <= MaxSkinBytes && bytes.AsSpan().StartsWith(PngSignature) ? (name, bytes) : null;
    }

    /// <summary>Insert dashes into a 32-char undashed uuid; leave anything else unchanged.</summary>
    private static string FormatUuid(string raw) =>
        raw.Length == 32 && !raw.Contains('-')
            ? $"{raw[..8]}-{raw[8..12]}-{raw[12..16]}-{raw[16..20]}-{raw[20..]}"
            : raw;
}
