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
    /// <summary>The board's theme ids, in registry order.</summary>
    [Parameter] public IReadOnlyList<string> Themes { get; set; } = [];
    /// <summary>The theme in hand, held by the tool because the canvas can lift one into it.</summary>
    [Parameter] public string? Brush { get; set; }
    /// <summary>The board's map default, or empty for unthemed.</summary>
    [Parameter] public string MapTheme { get; set; } = "";
    /// <summary>Every shape that carries a theme, by shape id.</summary>
    [Parameter] public IReadOnlyDictionary<string, string> ShapeThemes { get; set; } = new Dictionary<string, string>();
    /// <summary>Every shape on the board, themed or not — the denominator of the coverage line.</summary>
    [Parameter] public int ShapeCount { get; set; }
    [Parameter] public string? SelectedGroupId { get; set; }
    [Parameter] public string? SelectedShapeId { get; set; }
    /// <summary>The shape ids the current selection covers — what its theme is read back over.</summary>
    [Parameter] public IReadOnlyList<string> TargetShapeIds { get; set; } = [];
    /// <summary>Bumped whenever the registry changes. The swatch render is keyed on it as well as on the name,
    /// because a theme replaced under the name already in hand would otherwise keep the picture it had.</summary>
    [Parameter] public int Revision { get; set; }
    /// <summary>Whether the add-from-library panel is open. The strip's + toggles it.</summary>
    [Parameter] public bool AddOpen { get; set; }
    [Parameter] public EventCallback<bool> AddOpenChanged { get; set; }
    /// <summary>Put a theme in hand, or empty it — a set rather than a toggle, because a copy-in has to arm
    /// what it landed whether or not that name was already held.</summary>
    [Parameter] public EventCallback<string> OnHold { get; set; }
    /// <summary>The registry or an assignment moved; the tool re-reads it.</summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private IReadOnlyList<ThemeSummary> libraryThemes = [];
    private IReadOnlyList<RoomStyleSummary> rooms = [];
    private ThemePreviewDto? preview;
    private (string? Held, int Revision) previewedFor;
    private string? note;

    /// <summary>The room-style snapshot bound per kind — the JSON itself, which is what the document holds.</summary>
    private readonly Dictionary<string, string> boundRooms = [];
    /// <summary>Which library row each snapshot was taken from. Presentation only: it re-selects the dropdown
    /// after a reload and is never what the map exports from.</summary>
    private readonly Dictionary<string, long> pickedRooms = [];
    /// <summary>The kinds bound to <b>no building</b> — a pad on open ground with nothing over it. Held apart
    /// from <see cref="boundRooms"/> because it is a binding, not a style: the document states an explicit
    /// null for it, which is a different answer from never having asked.</summary>
    private readonly HashSet<string> openRooms = [];
    private readonly Dictionary<string, RoomStylePreviewDto> roomPreviews = [];

    private string? InHand => string.IsNullOrEmpty(Brush) ? null : Brush;
    private bool HasSelection => SelectedGroupId is not null || SelectedShapeId is not null;

    protected override async Task OnInitializedAsync()
    {
        libraryThemes = await Library.ListAsync<ThemeSummary>(LibraryKinds.Themes);
        rooms = await Library.ListAsync<RoomStyleSummary>(LibraryKinds.Houses);
        await ReadRoomBindings();
    }

    // The swatch render is one round-trip, so it is taken only when the theme it would draw actually moves.
    protected override async Task OnParametersSetAsync()
    {
        if (previewedFor == (InHand, Revision)) return;
        previewedFor = (InHand, Revision);
        preview = null;
        if (Handle is null || InHand is null) return;
        var json = await Handle.InvokeAsync<string>("getThemes");
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("themes", out var themes) && themes.TryGetProperty(InHand, out var node))
            preview = await Library.ThemePreviewAsync(node.GetRawText());
    }

    /// <summary>What the selection paints: the theme every target shape shares, empty when none carries one, or
    /// <see cref="Mixed"/> when a group's shapes disagree.</summary>
    private string SelectionTheme()
    {
        if (TargetShapeIds.Count == 0) return "";
        var first = ShapeThemes.GetValueOrDefault(TargetShapeIds[0], "");
        return TargetShapeIds.All(id => ShapeThemes.GetValueOrDefault(id, "") == first) ? first : Mixed;
    }

    /// <summary>How much of the board still falls through to the map default — the question the number keys and
    /// the walk-to-the-next-unpainted chord are both answers to.</summary>
    private string Coverage
    {
        get
        {
            if (ShapeCount == 0) return "Nothing is drawn yet.";
            var painted = ShapeThemes.Count;
            return painted == ShapeCount
                ? $"All {ShapeCount} shapes are painted."
                : $"{painted} of {ShapeCount} shapes are painted; the other {ShapeCount - painted} fall through to this.";
        }
    }

    // ── the registry ──

    private async Task CopyIn(ThemeSummary picked)
    {
        if (Handle is null) return;
        var themeJson = await Library.DocumentAsync(LibraryKinds.Themes, picked.Id);
        if (themeJson is null) { note = "That theme could not be read."; return; }

        // Copying in under a name already on the board replaces it, which is how a theme edited in the library
        // is brought up to date; a new name defines a new one.
        var id = Themes.Contains(picked.Name)
            ? picked.Name
            : await Handle.InvokeAsync<string>("defineTheme", picked.Name);
        var fault = await Handle.InvokeAsync<string?>("setThemeJson", id, themeJson);
        note = fault;
        if (fault is null)
        {
            await AddOpenChanged.InvokeAsync(false);
            await OnHold.InvokeAsync(id);
        }
        await OnChanged.InvokeAsync();
    }

    private async Task SaveToLibrary()
    {
        if (Handle is null || InHand is null) return;
        var json = await Handle.InvokeAsync<string>("getThemes");
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("themes", out var themes) || !themes.TryGetProperty(InHand, out var node))
            return;
        var id = await Library.ImportThemeAsync(InHand, node.GetRawText());
        note = id is null
            ? "The library refused this theme."
            : $"Saved “{InHand}” to the library, one style per bucket.";
        libraryThemes = await Library.ListAsync<ThemeSummary>(LibraryKinds.Themes);
        StateHasChanged();
    }

    private async Task RemoveFromBoard()
    {
        if (Handle is null || InHand is null) return;
        await Handle.InvokeVoidAsync("deleteTheme", InHand);
        note = null;
        await OnHold.InvokeAsync("");
        await OnChanged.InvokeAsync();
    }

    private async Task SetMapDefault(ChangeEventArgs e)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("setMapTheme", (string?)e.Value ?? "");
        await OnChanged.InvokeAsync();
    }

    private async Task ClearSelection()
    {
        if (Handle is null) return;
        if (SelectedGroupId is not null) await Handle.InvokeVoidAsync("assignGroup", SelectedGroupId, "");
        else if (SelectedShapeId is not null) await Handle.InvokeVoidAsync("assignShape", SelectedShapeId, "");
        await OnChanged.InvokeAsync();
    }

    // ── the room shells ──

    private string? BoundRoom(string kind) => boundRooms.GetValueOrDefault(kind);
    private long PickedRoom(string kind) => pickedRooms.GetValueOrDefault(kind);

    /// <summary>What the select shows: a row id, <c>0</c> for the built-in shell, or
    /// <see cref="NoBuilding"/>.</summary>
    private string RoomChoice(string kind) =>
        openRooms.Contains(kind) ? NoBuilding : PickedRoom(kind).ToString();

    /// <summary>Whether this kind is bound to no building.</summary>
    private bool IsOpenRoom(string kind) => openRooms.Contains(kind);
    private RoomStylePreviewDto? RoomPreview(string kind) => roomPreviews.GetValueOrDefault(kind);

    private async Task ReadRoomBindings()
    {
        if (Handle is null) return;
        JsonNode? state;
        try { state = JsonNode.Parse(await Handle.InvokeAsync<string>("getRoomStyles")); }
        catch (JsonException) { return; }

        foreach (var kind in RoomKindInfo.All)
        {
            // TryGetPropertyValue is what separates the three answers: the indexer returns null both for a key
            // that is absent and for one holding a JSON null, and those are different bindings — never asked
            // (the built-in shell) against asked for no building at all.
            JsonNode? snapshot = null;
            var present = (state as JsonObject)?.TryGetPropertyValue(kind.Id, out snapshot) is true;
            boundRooms.Remove(kind.Id);
            roomPreviews.Remove(kind.Id);
            openRooms.Remove(kind.Id);
            if (!present) continue;
            if (snapshot is null) { openRooms.Add(kind.Id); continue; }
            boundRooms[kind.Id] = snapshot.ToJsonString();
            await RedrawRoom(kind.Id);
        }
        StateHasChanged();
    }

    private async Task BindRoom(string kind, ChangeEventArgs e)
    {
        if ((string?)e.Value == NoBuilding) { await OpenRoom(kind); return; }
        if (!long.TryParse((string?)e.Value, out var id) || id == 0) { await ClearRoom(kind); return; }

        // The snapshot is taken here: from now on the board holds the style, not a pointer at the row.
        var styleJson = await Library.DocumentAsync(LibraryKinds.Houses, id);
        if (styleJson is null) { note = "That room style could not be read."; return; }

        boundRooms[kind] = styleJson;
        pickedRooms[kind] = id;
        note = null;
        if (Handle is not null) await Handle.InvokeVoidAsync("setRoomStyle", kind, styleJson);
        await RedrawRoom(kind);
    }

    private async Task ClearRoom(string kind)
    {
        boundRooms.Remove(kind);
        pickedRooms.Remove(kind);
        roomPreviews.Remove(kind);
        openRooms.Remove(kind);
        note = null;
        if (Handle is not null) await Handle.InvokeVoidAsync("setRoomStyle", kind, null);
        StateHasChanged();
    }

    /// <summary>Bind <b>no building</b>: the pad stands on open ground with nothing over it. The document
    /// states an explicit null, which the export reads as a third answer rather than as a missing one.</summary>
    private async Task OpenRoom(string kind)
    {
        boundRooms.Remove(kind);
        pickedRooms.Remove(kind);
        roomPreviews.Remove(kind);
        openRooms.Add(kind);
        note = null;
        if (Handle is not null) await Handle.InvokeVoidAsync("setRoomStyle", kind, "null");
        StateHasChanged();
    }

    private async Task RedrawRoom(string kind)
    {
        if (boundRooms.GetValueOrDefault(kind) is not { } styleJson) return;
        if (await Library.RoomStyleSnapshotPreviewAsync(styleJson) is { } views) roomPreviews[kind] = views;
        StateHasChanged();
    }
}

/// <summary>The two kinds of room a board binds a shell for. The ids are the wire keys the sketch document
/// holds them under; the words are what the inspector offers them as.</summary>
public sealed record RoomKindInfo(string Id, string Title)
{
    public static readonly IReadOnlyList<RoomKindInfo> All =
    [
        new("cage", "Wool cages"),
        new("spawn", "Spawn cubes"),
    ];
}
