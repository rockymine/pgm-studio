using System.Net;
using System.Net.Http.Json;
using System.Text;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>What a prop preview answered: the pictures, or the sentence the gate refused the prop with. A
/// building whose wings are no building is a refusal an author has to read — <c>HP1</c>–<c>HP3</c> for the
/// prop's own shape, <c>HJ1</c>–<c>HJ5</c> for how its wings meet — and rendering it as a missing picture
/// makes a refused building look like a slow one.</summary>
public sealed record PropPreview(DressingPreviewDto? Pictures, string? Refusal);

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
    /// <summary>The path-shape cards, drawn in <paramref name="paveJson"/> — the material the author already
    /// chose — so the picker answers "what would <em>mine</em> look like shaped that way".</summary>
    public async Task<IReadOnlyList<PropOptionDto>> StrokeStylesAsync(string? paveJson = null)
        => await GetOrDefault<List<PropOptionDto>>(Card("api/terrain/stroke-styles", "pave", paveJson)) ?? [];

    /// <summary>The rock-shape cards, drawn in the author's own rock material, for the same reason.</summary>
    public async Task<IReadOnlyList<PropOptionDto>> BoulderFormsAsync(string? rockJson = null)
        => await GetOrDefault<List<PropOptionDto>>(Card("api/terrain/boulder-forms", "rock", rockJson)) ?? [];

    // A material is JSON, so it has to be escaped into the query rather than pasted in — braces and quotes
    // would otherwise arrive as a different document or as none.
    private static string Card(string route, string field, string? json)
        => string.IsNullOrEmpty(json) ? route : $"{route}?{field}={Uri.EscapeDataString(json)}";

    public async Task<IReadOnlyList<PropOptionDto>> WaterFormsAsync()
        => await GetOrDefault<List<PropOptionDto>>("api/terrain/water-forms") ?? [];

    public async Task<IReadOnlyList<PropOptionDto>> SpeciesAsync()
        => await GetOrDefault<List<PropOptionDto>>("api/terrain/species") ?? [];

    /// <summary>The woods a grown tree can be cut from, drawn on the tree the author is editing —
    /// <paramref name="knobs"/> is that tree's shape as a query string.</summary>
    public async Task<IReadOnlyList<PropOptionDto>> WoodsAsync(string? knobs = null)
        => await GetOrDefault<List<PropOptionDto>>(
            string.IsNullOrEmpty(knobs) ? "api/terrain/woods" : $"api/terrain/woods?{knobs}") ?? [];

    /// <summary>A sample patch the pass actually dressed with one prop, from above and cut open. The theme is
    /// passed because what the paint leaves on top is what decides whether flora grows and what a path may
    /// repaint — previewing against unthemed stone would promise ground the map's own finish would refuse.</summary>
    public async Task<PropPreview> PropPreviewAsync(string propJson, string? themeJson)
    {
        try
        {
            var response = await http.PostAsJsonAsync(
                "api/terrain/prop-preview", new PropPreviewRequest(propJson, themeJson));
            if (response.IsSuccessStatusCode)
                return new PropPreview(await response.Content.ReadFromJsonAsync<DressingPreviewDto>(), null);
            return new PropPreview(null, await ServerRefusal.SentenceAsync(response));
        }
        catch { return new PropPreview(null, null); }
    }

    // ── the six libraries ───────────────────────────────────────────────────────
    // One verb per concept over LibraryKinds, because the six differ only in a route stem and the shapes the
    // caller names: repeating list/get/preview/create/update/delete per kind is how the delete half came to
    // have three return types for one question.

    /// <summary>Every row of a library, newest first, each carrying its own card picture. Empty when the call
    /// fails: a grid with no cards is a state the page can render.</summary>
    public async Task<IReadOnlyList<TSummary>> ListAsync<TSummary>(LibraryKind kind, string? query = null)
        => await GetOrDefault<List<TSummary>>(
            string.IsNullOrEmpty(query) ? $"api/{kind.Route}" : $"api/{kind.Route}?{query}") ?? [];

    /// <summary>One row in full — its parts, courses and bindings.</summary>
    public Task<TDetail?> GetAsync<TDetail>(LibraryKind kind, long id)
        => GetOrDefault<TDetail>($"api/{kind.Route}/{id}");

    /// <summary>What a draft composes to, saving nothing. The body is the save request itself, so the picture
    /// cannot promise something the save would not build. Every kind but <see cref="LibraryKinds.Styles"/>
    /// answers it — a style draws through <see cref="MaterialPreviewAsync"/> instead.
    ///
    /// <para><paramref name="footprint"/> is the sample piece a building is drawn on, one of
    /// <see cref="HouseFootprints"/>; absent draws it on the default.</para></summary>
    /// <summary><paramref name="part"/> cuts the pictures to the row the editor has open, so what an author is
    /// working on is what they are looking at. Absent draws the whole of it.</summary>
    public Task<TPreview?> DraftPreviewAsync<TPreview>(
        LibraryKind kind, object draft, string? footprint = null, string? part = null)
    {
        var query = string.Join('&', new[]
        {
            footprint is null ? null : $"footprint={footprint}",
            string.IsNullOrEmpty(part) ? null : $"part={part}",
        }.OfType<string>());
        return PostOrNull<TPreview>(
            query.Length == 0 ? $"api/{kind.Route}/preview" : $"api/{kind.Route}/preview?{query}", draft);
    }

    public Task<TDetail?> CreateAsync<TDetail>(LibraryKind kind, object request)
        => PostOrNull<TDetail>($"api/{kind.Route}", request);

    public Task<TDetail?> UpdateAsync<TDetail>(LibraryKind kind, long id, object request)
        => PutOrNull<TDetail>($"api/{kind.Route}/{id}", request);

    /// <summary>Forget a row, or report the compositions that still bind it — a style, a roof, a storey and a
    /// porch are each refused while something composes them, and the caller shows which.</summary>
    public async Task<LibraryDelete> DeleteAsync(LibraryKind kind, long id)
    {
        HttpResponseMessage response;
        try { response = await http.DeleteAsync($"api/{kind.Route}/{id}"); }
        catch { return LibraryDelete.Failed; }

        if (response.IsSuccessStatusCode) return LibraryDelete.Gone;
        var refusal = await ReadOrNull<RefusalDto>(response);
        return new LibraryDelete(false, [.. refusal?.Findings.SelectMany(finding => finding.SubjectIds) ?? []]);
    }

    /// <summary>The document form of a row — the painter's theme JSON, or the stamper's house JSON — which is
    /// what a map snapshots when it binds one. Only the two kinds that <see cref="LibraryKind.Composed"/>
    /// marks answer it, and both wrap it in a one-field envelope, so the field is taken by shape rather than
    /// by name.</summary>
    public async Task<string?> DocumentAsync(LibraryKind kind, long id)
    {
        var envelope = await GetOrDefault<Dictionary<string, string>>($"api/{kind.Route}/{id}/json");
        return envelope?.Values.FirstOrDefault();
    }

    // ── shapes only one library has ─────────────────────────────────────────────
    /// <summary>The doors a house may be stamped with. Served, never restated here: the authoritative list is
    /// the table the wool-room block filter is built from.</summary>
    public async Task<IReadOnlyList<DoorOptionDto>> RoomDoorsAsync()
        => await GetOrDefault<List<DoorOptionDto>>("api/room-styles/doors") ?? [];

    /// <summary>The shell a <em>bound</em> style stamps. Previewed from the snapshot the map holds, never from
    /// the library row it came from — the row may have moved on since.</summary>
    public Task<RoomStylePreviewDto?> RoomStyleSnapshotPreviewAsync(string styleJson)
        => PostJsonDocument<RoomStylePreviewDto>("api/room-styles/preview-snapshot", styleJson);

    /// <summary>Lift a whole theme JSON into the library as one style per bucket plus a theme binding them.
    /// Returns the new theme's id, or null when the JSON was refused.</summary>
    public async Task<long?> ImportThemeAsync(string name, string themeJson)
        => (await PostOrNull<ImportedTheme>("api/themes/import", new ThemeImportRequest(name, themeJson)))?.Id;

    private sealed record ImportedTheme(long Id);

    // ── plumbing ────────────────────────────────────────────────────────────────
    // Every call answers null rather than throwing: these surfaces degrade to "no picture / no list" on a
    // failure, which is a state they can render, and a thrown request from a render path is not. The send
    // has to be inside the guard, not just the read — a request that never completes (the connection
    // dropped, the browser gave up) throws from PostAsJsonAsync itself, and one escaping an inspector's
    // OnParametersSetAsync takes down the whole app, not the picture it was drawing.
    private async Task<T?> GetOrDefault<T>(string url)
    {
        try { return await http.GetFromJsonAsync<T>(url); }
        catch { return default; }
    }

    private async Task<T?> PostOrNull<T>(string url, object body)
    {
        try { return await ReadOrNull<T>(await http.PostAsJsonAsync(url, body)); }
        catch { return default; }
    }

    private async Task<T?> PutOrNull<T>(string url, object body)
    {
        try { return await ReadOrNull<T>(await http.PutAsJsonAsync(url, body)); }
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
