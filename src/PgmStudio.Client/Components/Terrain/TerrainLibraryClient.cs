using System.Net;
using System.Net.Http.Json;
using System.Text;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Components;

/// <summary>
/// The one place the client knows the terrain-paint HTTP surface: the block palette, the two preview endpoints,
/// and the style/theme library's CRUD. Three surfaces author terrain paint — the library page, the sketch's
/// theme phase and its apply rail — and each of them wants the same handful of calls, so the routes and the
/// "a failed preview leaves the picture blank rather than throwing the page" rule live here once.
/// </summary>
public sealed class TerrainLibraryClient(HttpClient http)
{
    // ── palette + previews ──────────────────────────────────────────────────────
    /// <summary>The blocks a solid material may be built from. Empty when the call fails: a picker with no
    /// offers still edits the (id, data) pair underneath.</summary>
    public async Task<IReadOnlyList<PaintBlockDto>> BlocksAsync()
        => await GetOrDefault<List<PaintBlockDto>>("api/terrain/blocks") ?? [];

    /// <summary>Both views of one material, or null when the material cannot be previewed.</summary>
    public Task<MaterialPreviewDto?> MaterialPreviewAsync(string materialJson)
        => PostJsonDocument<MaterialPreviewDto>("api/terrain/material-preview", materialJson);

    /// <summary>A theme's cut-open sample plateau plus its per-bucket swatches, or null when it cannot be
    /// previewed.</summary>
    public Task<ThemePreviewDto?> ThemePreviewAsync(string themeJson)
        => PostJsonDocument<ThemePreviewDto>("api/terrain/theme-preview", themeJson);

    // ── dressing: the pickers, and one prop's own picture ───────────────────────
    // Every option a prop inspector offers is drawn by the pass itself rather than described, so a picker can
    // never promise a look the export does not produce. Empty on failure: a picker with no cards still edits
    // the value underneath.
    public async Task<IReadOnlyList<PropOptionDto>> PathStylesAsync(string? blocks = null)
        => await GetOrDefault<List<PropOptionDto>>(
            string.IsNullOrEmpty(blocks) ? "api/terrain/path-styles" : $"api/terrain/path-styles?blocks={blocks}") ?? [];

    public async Task<IReadOnlyList<PropOptionDto>> BoulderFormsAsync()
        => await GetOrDefault<List<PropOptionDto>>("api/terrain/boulder-forms") ?? [];

    public async Task<IReadOnlyList<PropOptionDto>> SpeciesAsync()
        => await GetOrDefault<List<PropOptionDto>>("api/terrain/species") ?? [];

    /// <summary>The woods a grown tree can be cut from, drawn on the tree the author is editing —
    /// <paramref name="knobs"/> is that tree's shape as a query string.</summary>
    public async Task<IReadOnlyList<PropOptionDto>> WoodsAsync(string? knobs = null)
        => await GetOrDefault<List<PropOptionDto>>(
            string.IsNullOrEmpty(knobs) ? "api/terrain/woods" : $"api/terrain/woods?{knobs}") ?? [];

    /// <summary>A sample patch the pass actually dressed with one prop, from above and cut open. The theme is
    /// passed because what the paint leaves on top is what decides whether flora grows and what a path may
    /// repaint — previewing against the built-in default would promise a meadow the map's finish would refuse.</summary>
    public async Task<DressingPreviewDto?> PropPreviewAsync(string propJson, string? themeJson)
        => await ReadOrNull<DressingPreviewDto>(await http.PostAsJsonAsync(
            "api/terrain/prop-preview", new PropPreviewRequest(propJson, themeJson)));

    // ── room styles ─────────────────────────────────────────────────────────────
    // The same shape as the theme half below: a library list carrying each row's picture, one detail read, a
    // draft preview that composes exactly as a save would, and the write/forget pair.
    public async Task<IReadOnlyList<RoomStyleSummary>> RoomStylesAsync()
        => await GetOrDefault<List<RoomStyleSummary>>("api/room-styles") ?? [];

    /// <summary>The doors a room may be stamped with. Served, never restated here: the authoritative list is
    /// the table the wool-room block filter is built from.</summary>
    public async Task<IReadOnlyList<DoorOptionDto>> RoomDoorsAsync()
        => await GetOrDefault<List<DoorOptionDto>>("api/room-styles/doors") ?? [];

    public Task<RoomStyleDetail?> RoomStyleAsync(long id)
        => GetOrDefault<RoomStyleDetail>($"api/room-styles/{id}");

    public async Task<RoomStylePreviewDto?> RoomStyleDraftPreviewAsync(RoomStyleSaveRequest draft)
        => await ReadOrNull<RoomStylePreviewDto>(await http.PostAsJsonAsync("api/room-styles/preview", draft));

    public async Task<RoomStyleDetail?> CreateRoomStyleAsync(RoomStyleSaveRequest request)
        => await ReadOrNull<RoomStyleDetail>(await http.PostAsJsonAsync("api/room-styles", request));

    public async Task<RoomStyleDetail?> UpdateRoomStyleAsync(long id, RoomStyleSaveRequest request)
        => await ReadOrNull<RoomStyleDetail>(await http.PutAsJsonAsync($"api/room-styles/{id}", request));

    public Task DeleteRoomStyleAsync(long id) => http.DeleteAsync($"api/room-styles/{id}");

    // ── styles ──────────────────────────────────────────────────────────────────
    /// <summary>The style library, newest first; <paramref name="kind"/> narrows it to one material kind.</summary>
    public async Task<IReadOnlyList<StyleDto>> StylesAsync(string? kind = null)
        => await GetOrDefault<List<StyleDto>>(
            string.IsNullOrEmpty(kind) ? "api/styles" : $"api/styles?kind={kind}") ?? [];

    public async Task<StyleDto?> CreateStyleAsync(StyleSaveRequest request)
        => await ReadOrNull<StyleDto>(await http.PostAsJsonAsync("api/styles", request));

    public async Task<StyleDto?> UpdateStyleAsync(long id, StyleSaveRequest request)
        => await ReadOrNull<StyleDto>(await http.PutAsJsonAsync($"api/styles/{id}", request));

    /// <summary>Forget a style, or report the themes that still bind it — a shared style is not deletable while
    /// something composes it, and the caller shows which themes those are rather than a bare failure.</summary>
    public async Task<StyleInUseDto?> DeleteStyleAsync(long id)
    {
        var response = await http.DeleteAsync($"api/styles/{id}");
        return response.StatusCode == HttpStatusCode.Conflict
            ? await ReadOrNull<StyleInUseDto>(response)
            : null;
    }

    // ── themes ──────────────────────────────────────────────────────────────────
    /// <summary>The theme library, newest first, each with the sample plateau it finishes.</summary>
    public async Task<IReadOnlyList<ThemeSummary>> ThemesAsync()
        => await GetOrDefault<List<ThemeSummary>>("api/themes") ?? [];

    public Task<ThemeDetail?> ThemeAsync(long id) => GetOrDefault<ThemeDetail>($"api/themes/{id}");

    public async Task<ThemeDetail?> CreateThemeAsync(ThemeSaveRequest request)
        => await ReadOrNull<ThemeDetail>(await http.PostAsJsonAsync("api/themes", request));

    public async Task<ThemeDetail?> UpdateThemeAsync(long id, ThemeSaveRequest request)
        => await ReadOrNull<ThemeDetail>(await http.PutAsJsonAsync($"api/themes/{id}", request));

    public Task DeleteThemeAsync(long id) => http.DeleteAsync($"api/themes/{id}");

    /// <summary>What a set of bindings composes to, previewed without saving — the theme editor's live picture.</summary>
    public async Task<ThemePreviewDto?> ThemeDraftPreviewAsync(ThemeSaveRequest draft)
        => await ReadOrNull<ThemePreviewDto>(await http.PostAsJsonAsync("api/themes/preview", draft));

    /// <summary>A library theme assembled into the painter's theme JSON — what a map takes a copy of when it
    /// applies the theme.</summary>
    public async Task<string?> ThemeJsonAsync(long id)
        => (await GetOrDefault<ThemeJsonResponse>($"api/themes/{id}/json"))?.ThemeJson;

    /// <summary>Lift a whole theme JSON into the library as one style per bucket plus a theme binding them.
    /// Returns the new theme's id, or null when the JSON was refused.</summary>
    public async Task<long?> ImportThemeAsync(string name, string themeJson)
        => (await ReadOrNull<ImportedTheme>(
            await http.PostAsJsonAsync("api/themes/import", new ThemeImportRequest(name, themeJson))))?.Id;

    private sealed record ThemeJsonResponse(string ThemeJson);
    private sealed record ImportedTheme(long Id);

    // ── plumbing ────────────────────────────────────────────────────────────────
    // Every read answers null rather than throwing: these surfaces degrade to "no picture / no list" on a
    // failure, which is a state they can render, and a thrown request from a render path is not.
    private async Task<T?> GetOrDefault<T>(string url)
    {
        try { return await http.GetFromJsonAsync<T>(url); }
        catch { return default; }
    }

    private static async Task<T?> ReadOrNull<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Conflict) return default;
        try { return await response.Content.ReadFromJsonAsync<T>(); }
        catch { return default; }
    }

    // The preview endpoints take the document itself as the body, not a wrapper.
    private async Task<T?> PostJsonDocument<T>(string url, string json)
    {
        try
        {
            var response = await http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : default;
        }
        catch { return default; }
    }
}
