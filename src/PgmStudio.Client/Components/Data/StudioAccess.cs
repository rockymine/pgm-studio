using System.Net.Http.Json;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>
/// Who the browser is signed in as, and whether the page in front of it may write. The server refuses
/// every write the caller may not make whatever the client shows (<c>docs/access.md</c>); this is what lets
/// the client say so before the edit rather than after it.
/// </summary>
public sealed class StudioAccess(HttpClient http)
{
    private Task<CallerDto>? me;
    private readonly Dictionary<string, Task<bool>> mapEdits = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Who the request is, asked once per page load. A studio that does not answer is treated as a
    /// signed-out visitor, which is the reading that offers nothing it cannot keep.</summary>
    public Task<CallerDto> MeAsync() => me ??= LoadMeAsync();

    /// <summary>Forget what was asked, after a sign-out or a change to the whitelist.</summary>
    public void Forget()
    {
        me = null;
        mapEdits.Clear();
    }

    /// <summary>Whether the caller may write anything at all — someone on the whitelist.</summary>
    public async Task<bool> MayWriteAsync() => (await MeAsync()).Role is not null;

    public async Task<bool> IsAdminAsync() => (await MeAsync()).Role == StudioRoles.Admin;

    /// <summary>Whether the caller may change the map <paramref name="slug"/> names.</summary>
    public Task<bool> MayEditMapAsync(string slug)
    {
        if (!mapEdits.TryGetValue(slug, out var asked))
            mapEdits[slug] = asked = LoadMapEditAsync(slug);
        return asked;
    }

    /// <summary>
    /// Why the page at <paramref name="path"/> opens read-only, or null where the caller may write there. A
    /// page under <c>/maps/{slug}/</c> edits that map; the map list, the catalogue and the design page write
    /// nothing; every other page — a new map, the plan editor, the library, the generator — needs someone on
    /// the whitelist.
    /// </summary>
    public async Task<string?> ReadOnlyReasonAsync(string path)
    {
        var segments = Segments(path);
        if (WritesNothing(segments))
            return null;

        var caller = await MeAsync();
        if (segments[0] == "maps" && segments.Length >= 3)
            return await MayEditMapAsync(segments[1]) ? null
                : !caller.SignedIn ? "You are not signed in, so this map opens read-only."
                : caller.Role is null ? "You are not on this studio's whitelist, so this map opens read-only."
                : "Only this map's owner, its credited authors, or an admin can change it, so it opens read-only.";

        return caller.Role is not null ? null
            : !caller.SignedIn ? "You are not signed in, so this page opens read-only."
            : "You are not on this studio's whitelist, so this page opens read-only.";
    }

    /// <summary>
    /// Why an action that writes is closed on the page at <paramref name="path"/>, or null where it is open. On
    /// a page that writes it is the page's own read-only reason; on a page that writes nothing it is whether
    /// the caller may write at all, since an action there starts something on a page that does.
    /// </summary>
    public async Task<string?> WriteReasonAsync(string path)
    {
        if (!WritesNothing(Segments(path)))
            return await ReadOnlyReasonAsync(path);

        var caller = await MeAsync();
        return caller.Role is not null ? null
            : !caller.SignedIn ? "Sign in with a whitelisted account to do this."
            : "Your account is not on this studio's whitelist, so you can only look.";
    }

    /// <summary>
    /// Why deleting a shared row — a library entry, a pinned plan — is closed on the page at
    /// <paramref name="path"/>, or null where it is open: the page's write reason, and past it an admin's alone,
    /// since every map using the row loses it.
    /// </summary>
    public async Task<string?> DeleteReasonAsync(string path) =>
        await WriteReasonAsync(path)
        ?? (await IsAdminAsync() ? null : "Only an admin can delete this, because everyone shares it.");

    private static string[] Segments(string path) =>
        path.Split('?', '#')[0].Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static bool WritesNothing(string[] segments) =>
        segments.Length == 0 || segments[0] is "maps" && segments.Length == 1
        || segments[0] is "catalog" or "design" or "not-found" or "admin";

    /// <summary>The sign-in link that brings the browser back to <paramref name="returnPath"/>.</summary>
    public static string SignInHref(string returnPath) =>
        $"api/auth/discord?returnUrl={Uri.EscapeDataString("/" + returnPath.TrimStart('/'))}";

    private async Task<CallerDto> LoadMeAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<CallerDto>("api/me")
                   ?? new CallerDto(AccessModes.Invited, false, null, null, null);
        }
        catch (HttpRequestException)
        {
            return new CallerDto(AccessModes.Invited, false, null, null, null);
        }
    }

    private async Task<bool> LoadMapEditAsync(string slug)
    {
        try
        {
            return (await http.GetFromJsonAsync<MapAccessDto>($"api/map/{Uri.EscapeDataString(slug)}/access"))?.MayEdit
                   ?? false;
        }
        catch (HttpRequestException)
        {
            // A slug no map is stored under is the tool's own 404 to render; it is not read-only because of it.
            return await MayWriteAsync();
        }
    }
}
