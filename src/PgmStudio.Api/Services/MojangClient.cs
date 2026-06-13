using System.Text.Json;
using System.Text.RegularExpressions;

namespace PgmStudio.Api.Services;

/// <summary>
/// Resolves a Minecraft username or UUID to <c>(uuid, name)</c> via Mojang's public APIs.
/// Port of the reference studio's <c>services/mojang.py</c>: a dashed-UUID lookup hits the session
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

    /// <summary>Insert dashes into a 32-char undashed uuid; leave anything else unchanged.</summary>
    private static string FormatUuid(string raw) =>
        raw.Length == 32 && !raw.Contains('-')
            ? $"{raw[..8]}-{raw[8..12]}-{raw[12..16]}-{raw[16..20]}-{raw[20..]}"
            : raw;
}
