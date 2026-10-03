using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The Theme phase's inspector (docs/tools/sketch.md, the Theme phase): what is in hand, what the selection is
/// painted with, and — with nothing selected — what the board falls back to.
/// <para>It reads the registry the tool holds and writes through the sketch-bridge <see cref="Handle"/>. The
/// previews and the library lists are its own, because they are HTTP and the tool has no use for them.</para>
/// </summary>
public partial class SketchThemeInspector
{
    /// <summary>The value <see cref="SelectionTheme"/> answers when a group's shapes disagree.</summary>
    private const string Mixed = "~mixed~";

    /// <summary>The room select's value for the third answer. A style is picked by row id and the built-in by
    /// <c>0</c>, so no building needs a word rather than a number.</summary>
    private const string NoBuilding = "none";

    [Parameter] public IJSObjectReference? Handle { get; set; }
    /// <summary>The board's finish, as the bridge last announced it: the registry, the map default, which
    /// shape carries which theme, where each copy came from, the room shells and the biome.</summary>
    [Parameter] public SketchThemes Themes { get; set; } = SketchThemes.Empty;
    /// <summary>The theme in hand, held by the tool because the canvas can lift one into it.</summary>
    [Parameter] public string? Brush { get; set; }
    [Parameter] public string? SelectedGroupId { get; set; }
    [Parameter] public string? SelectedShapeId { get; set; }
    /// <summary>The shape ids the current selection covers — what its theme is read back over.</summary>
    [Parameter] public IReadOnlyList<string> TargetShapeIds { get; set; } = [];
    /// <summary>Whether the add-from-library panel is open. The strip's + toggles it.</summary>
    [Parameter] public bool AddOpen { get; set; }
    [Parameter] public EventCallback<bool> AddOpenChanged { get; set; }
    /// <summary>Put a theme in hand, or empty it — a set rather than a toggle, because a copy-in has to arm
    /// what it landed whether or not that name was already held.</summary>
    [Parameter] public EventCallback<string> OnHold { get; set; }

    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private IReadOnlyList<ThemeSummary> libraryThemes = [];

    /// <summary>The biome library's rows, each with the patch of ground it tints — what the map's biome is
    /// picked from. A field is authored in the library and only chosen here, so this select is the whole of
    /// the phase's biome surface.</summary>
    private IReadOnlyList<BiomePatternSummary> biomePatterns = [];

    private string BiomeChoice => (HeldBiome?.Id ?? 0).ToString();

    /// <summary>
    /// The library row the board's field is, or null where it is none of them.
    ///
    /// <para>The recorded row first, and the field's own <b>content</b> after it: a field written over HTTP
    /// records no row, and one of the flat presets is what most boards state, so matching the document is what
    /// stops the select reading "none" over a board that plainly has a field. Compared as parsed values rather
    /// than as text, since key order and whitespace are serialization.</para></summary>
    private BiomePatternSummary? HeldBiome =>
        biomePatterns.FirstOrDefault(pattern => pattern.Id == Themes.BiomeSource)
        ?? (string.IsNullOrWhiteSpace(Themes.BiomeJson)
            ? null
            : biomePatterns.FirstOrDefault(pattern => SameDocument(pattern.Params, Themes.BiomeJson)));

    /// <summary>Whether the board states a field the library does not hold — what the select says instead of
    /// claiming the board has none.</summary>
    private bool BiomeOffLibrary => !string.IsNullOrWhiteSpace(Themes.BiomeJson) && HeldBiome is null;

    private IReadOnlyList<SelectOption> BiomePatterns =>
        [.. biomePatterns.Select(pattern => new SelectOption(pattern.Id.ToString(), pattern.Name))];

    /// <summary>Copy a library pattern onto the board, or take the field off with the unbound row. The field
    /// itself is snapshotted the way a theme is — editing the library row afterwards retints nothing — and the
    /// row it came from is recorded beside it so the select can say which one is held.</summary>
    private async Task SetMapBiome(string value)
    {
        if (Handle is null) return;
        var row = long.TryParse(value, out var id) ? id : 0;
        var picked = biomePatterns.FirstOrDefault(pattern => pattern.Id == row);
        await Handle.InvokeAsync<string?>("setBiome", picked?.Params ?? "", picked?.Id ?? 0);
    }
    private IReadOnlyList<RoomStyleSummary> rooms = [];
    private ThemePreviewDto? preview;

    /// <summary>What the swatch render and the library comparison were last taken of: the theme in hand, its
    /// document and the library row it was copied from. A theme replaced under the name already in hand is a
    /// different document, so it is drawn again.</summary>
    private (string? Held, string? Document, long Source) previewedFor;
    private string? note;

    private string? InHand => string.IsNullOrEmpty(Brush) ? null : Brush;
    private bool HasSelection => SelectedGroupId is not null || SelectedShapeId is not null;

    protected override async Task OnInitializedAsync()
    {
        libraryThemes = await Library.ListAsync<ThemeSummary>(LibraryKinds.Themes);
        rooms = await Library.ListAsync<RoomStyleSummary>(LibraryKinds.Houses);
        roomOfSnapshot.Clear();
        biomePatterns = await Library.ListAsync<BiomePatternSummary>(LibraryKinds.Biomes);
    }

    /// <summary>The material document of the theme in hand, or null where none is held or the board no longer
    /// has it.</summary>
    private string? HeldDocument => InHand is { } held ? Themes.Documents.GetValueOrDefault(held) : null;

    // The swatch render is one round-trip, so it is taken only when the theme it would draw actually moves.
    protected override async Task OnParametersSetAsync()
    {
        var taken = (InHand, HeldDocument, InHand is { } held ? Themes.Sources.GetValueOrDefault(held) : 0);
        if (previewedFor == taken) return;
        previewedFor = taken;
        preview = null;
        heldMatchesSource = null;
        if (HeldDocument is not { } document) return;
        preview = await Library.ThemePreviewAsync(document);
        await ReadHeldSource();
    }

    /// <summary>What the selection paints: the theme every target shape shares, empty when none carries one, or
    /// <see cref="Mixed"/> when a group's shapes disagree.</summary>
    private string SelectionTheme()
    {
        if (TargetShapeIds.Count == 0) return "";
        var first = Themes.ShapeThemes.GetValueOrDefault(TargetShapeIds[0], "");
        return TargetShapeIds.All(id => Themes.ShapeThemes.GetValueOrDefault(id, "") == first) ? first : Mixed;
    }

    // ── the registry ──

    private async Task CopyIn(ThemeSummary picked)
    {
        if (Handle is null) return;
        var themeJson = await Library.DocumentAsync(LibraryKinds.Themes, picked.Id);
        if (themeJson is null) { note = "Couldn't read that palette."; return; }

        // The copy this row already made is what a second copy refreshes, whatever either side has since been
        // renamed to. Only a row nothing on the board came from defines a theme, and that theme records where
        // it came from — a name is the author's word and two of them may be the same.
        var already = await Handle.InvokeAsync<string>("themeFromLibrary", picked.Id);
        var id = already.Length > 0 ? already : await Handle.InvokeAsync<string>("defineTheme", picked.Name);
        var fault = await Handle.InvokeAsync<string?>("setThemeJson", id, themeJson);
        note = fault;
        if (fault is null)
        {
            await Handle.InvokeVoidAsync("setThemeSource", id, picked.Id);
            librarySnapshots[id] = themeJson;
            await AddOpenChanged.InvokeAsync(false);
            await OnHold.InvokeAsync(id);
        }
    }

    /// <summary>The board theme copied from a library row, or null where nothing on the board came from
    /// it — what the add panel says instead of matching the row's name against a board theme's.</summary>
    private string? CopiedAs(long row) =>
        Themes.Sources.FirstOrDefault(entry => entry.Value == row && Themes.Documents.ContainsKey(entry.Key)).Key;

    /// <summary>The library row the theme in hand was copied from, or null for one authored on the board or
    /// copied from a row the library has since forgotten.</summary>
    private ThemeSummary? HeldSource =>
        InHand is { } held && Themes.Sources.TryGetValue(held, out var row)
            ? libraryThemes.FirstOrDefault(theme => theme.Id == row)
            : null;

    /// <summary>The library row's own document, per board theme, as it read when it was last compared — what
    /// says whether the snapshot is still that row or has moved on from it.</summary>
    private readonly Dictionary<string, string> librarySnapshots = [];

    /// <summary>Whether the theme in hand still says what its library row says: true where the two documents
    /// match, false where the copy has been edited or the row has moved on, and null where the row has not
    /// been read.</summary>
    private bool? heldMatchesSource;

    /// <summary>Read the row behind the theme in hand and compare, so the phase can say whether the snapshot
    /// is behind. One round trip per theme, taken with the swatch render.</summary>
    private async Task ReadHeldSource()
    {
        heldMatchesSource = null;
        if (InHand is not { } held || HeldSource is not { } source) return;
        if (!librarySnapshots.TryGetValue(held, out var rowJson))
        {
            rowJson = await Library.DocumentAsync(LibraryKinds.Themes, source.Id);
            if (rowJson is null) return;
            librarySnapshots[held] = rowJson;
        }
        if (HeldDocument is not { } document) return;
        heldMatchesSource = SameDocument(document, rowJson);
    }

    /// <summary>Whether two theme documents say the same thing. Compared as parsed values rather than as text:
    /// key order and whitespace are serialization, and a copy that differs only in those is the row.</summary>
    private static bool SameDocument(string left, string right)
    {
        try { return JsonNode.DeepEquals(JsonNode.Parse(left), JsonNode.Parse(right)); }
        catch (JsonException) { return false; }
    }

    private async Task RemoveFromBoard()
    {
        if (Handle is null || InHand is null) return;
        librarySnapshots.Remove(InHand);
        await Handle.InvokeVoidAsync("deleteTheme", InHand);
        note = null;
        await OnHold.InvokeAsync("");
    }

    private async Task SetMapDefault(string theme)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("setMapTheme", theme);
    }

    /// <summary>The board's own themes as options. The map default is the lowest layer — what paints a cell
    /// no shape claims — so the row standing for none says what falls through instead of naming nothing.</summary>
    private IReadOnlyList<SelectOption> MapThemes =>
        [.. Themes.Ids.Select(id => new SelectOption(id, id))];

    /// <summary>What a room kind's select offers: no building at all, then every style the library holds. The
    /// built-in shell is the placeholder rather than an option, because it is also what the select falls back
    /// to for a snapshot no row matches, and those two read differently.</summary>
    private IReadOnlyList<SelectOption> RoomOptions =>
    [
        new(NoBuilding, "(no building)"),
        .. rooms.Select(room => new SelectOption(room.Id.ToString(), room.Name)),
    ];

    private async Task ClearSelection()
    {
        if (Handle is null) return;
        if (SelectedGroupId is not null) await Handle.InvokeVoidAsync("assignGroup", SelectedGroupId, "");
        else if (SelectedShapeId is not null) await Handle.InvokeVoidAsync("assignShape", SelectedShapeId, "");
    }

    // ── the room shells ──

    private string? BoundRoom(string kind) => Themes.RoomStyles.GetValueOrDefault(kind);

    /// <summary>The library row a kind's bound shell is, or null where the board binds nothing — or binds a
    /// shell no row holds.
    ///
    /// <para>Matched on the snapshot's own <b>content</b>, because that is the only thing there is to match
    /// on: the binding is a snapshot and records no row id, so a board read back after a reload, and one
    /// written straight to <c>PUT …/sketch/room-styles/{part}</c>, would otherwise resolve to nothing.
    /// Compared as parsed values rather than as text, since key order and whitespace are
    /// serialization.</para></summary>
    private RoomStyleSummary? HeldRoom(string kind)
    {
        if (Themes.RoomStyles.GetValueOrDefault(kind) is not { } snapshot) return null;
        if (roomOfSnapshot.TryGetValue(snapshot, out var held)) return held;
        return roomOfSnapshot[snapshot] = rooms.FirstOrDefault(room => SameDocument(room.Style, snapshot));
    }

    /// <summary>The row each snapshot resolves to, keyed by the snapshot itself so the match survives a
    /// re-render without being re-derived: two selects are drawn per render and each row costs a parse of
    /// both documents. The board's snapshot can arrive before the library list does, so the memo is cleared
    /// when the list arrives; a match made against no rows is not kept.</summary>
    private readonly Dictionary<string, RoomStyleSummary?> roomOfSnapshot = [];

    /// <summary>Whether the board binds a shell the library does not hold — what the select says instead of
    /// claiming the room is on its built-in one.</summary>
    private bool RoomOffLibrary(string kind) => Themes.RoomStyles.ContainsKey(kind) && HeldRoom(kind) is null;

    /// <summary>What the select shows: a row id, <c>0</c> for the built-in shell, or
    /// <see cref="NoBuilding"/>.</summary>
    private string RoomChoice(string kind) =>
        Themes.OpenRooms.Contains(kind) ? NoBuilding : (HeldRoom(kind)?.Id ?? 0).ToString();

    /// <summary>What the select reads where it holds no row: the built-in shell, or a shell the library
    /// cannot name.</summary>
    private string RoomPlaceholder(string kind) =>
        RoomOffLibrary(kind) ? "(not in the library)" : "(built-in shell)";

    /// <summary>Whether this kind is bound to no building.</summary>
    private bool IsOpenRoom(string kind) => Themes.OpenRooms.Contains(kind);

    private async Task BindRoom(string kind, string choice)
    {
        if (choice == NoBuilding) { await OpenRoom(kind); return; }
        if (!long.TryParse(choice, out var id) || id == 0) { await ClearRoom(kind); return; }

        // The snapshot is taken here: from now on the board holds the style, not a pointer at the row.
        var styleJson = await Library.DocumentAsync(LibraryKinds.Houses, id);
        if (styleJson is null) { note = "Couldn't read that room style."; return; }

        note = null;
        if (Handle is not null) await Handle.InvokeVoidAsync("setRoomStyle", kind, styleJson);
    }

    private async Task ClearRoom(string kind)
    {
        note = null;
        if (Handle is not null) await Handle.InvokeVoidAsync("setRoomStyle", kind, null);
    }

    /// <summary>Bind <b>no building</b>: the pad stands on open ground with nothing over it. The document
    /// states an explicit null, which the export reads as a third answer rather than as a missing one.</summary>
    private async Task OpenRoom(string kind)
    {
        note = null;
        if (Handle is not null) await Handle.InvokeVoidAsync("setRoomStyle", kind, "null");
    }
}

/// <summary>The two kinds of room a board binds a shell for. The ids are the wire keys the sketch document
/// holds them under; the words are what the inspector offers them as.</summary>
public sealed record RoomKindInfo(string Id, string Title)
{
    public static readonly IReadOnlyList<RoomKindInfo> All =
    [
        new("wool", "Wool rooms"),
        new("spawn", "Spawn rooms"),
    ];
}
