using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The Theme phase's Rooms step (G34c, docs/world-export/structures.md §9): which shell this map's wool cages
/// and spawn cubes are stamped with.
///
/// <para>Two bindings and no more. Rooms are fanned across the map's symmetry orbit so both sides face the
/// same building, so a per-room shell would be a sightline that differed between teams — the same fairness
/// argument the dressing stage makes about a tree. The step therefore binds by <em>kind</em>, not by room.</para>
///
/// <para>What is stored is the composed style's <b>JSON</b>, not the library id it came from. A map keeps what
/// it picked, so editing the library's copy afterwards cannot rebuild a shipped map's spawn rooms — the rule
/// the applied terrain theme already follows. The pictures are rendered from that snapshot for the same
/// reason: drawing the library row would show what the row looks like now.</para>
/// </summary>
public partial class SketchRoomStyleStep
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }
    /// <summary>The other steps of the phase, by index — 0 Create, 1 Apply.</summary>
    [Parameter] public EventCallback<int> OnGoStep { get; set; }

    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private static readonly string[] Steps = ["Create", "Apply", "Rooms"];

    private Task OnStep(int i) => i == 2 ? Task.CompletedTask : OnGoStep.InvokeAsync(i);

    private IReadOnlyList<RoomStyleSummary> rooms = [];
    private string? note;

    /// <summary>The snapshot bound per kind, keyed by <see cref="RoomKindInfo.Id"/> — the JSON itself, which is
    /// what the sketch document holds.</summary>
    private readonly Dictionary<string, string> bound = [];

    /// <summary>Which library row each snapshot was taken from. Presentation only: it re-selects the dropdown
    /// after a reload and is never what the map is exported from.</summary>
    private readonly Dictionary<string, long> pickedFrom = [];

    private readonly Dictionary<string, RoomStylePreviewDto> previews = [];

    private string? Bound(string kind) => bound.GetValueOrDefault(kind);
    private long Picked(string kind) => pickedFrom.GetValueOrDefault(kind);
    private RoomStylePreviewDto? Preview(string kind) => previews.GetValueOrDefault(kind);

    protected override async Task OnInitializedAsync()
    {
        rooms = await Library.ListAsync<RoomStyleSummary>(LibraryKinds.Houses);
        await ReadBindings();
    }

    /// <summary>What the sketch document already binds, and a picture of each.</summary>
    private async Task ReadBindings()
    {
        if (Handle is null) return;
        JsonNode? state;
        try { state = JsonNode.Parse(await Handle.InvokeAsync<string>("getRoomStyles")); }
        catch (JsonException) { return; }

        foreach (var kind in RoomKindInfo.All)
        {
            var snapshot = (state as JsonObject)?[kind.Id];
            if (snapshot is null) { bound.Remove(kind.Id); previews.Remove(kind.Id); continue; }
            bound[kind.Id] = snapshot.ToJsonString();
            await Redraw(kind.Id);
        }
        StateHasChanged();
    }

    private async Task Bind(string kind, ChangeEventArgs e)
    {
        if (!long.TryParse((string?)e.Value, out var id) || id == 0) { await Clear(kind); return; }

        // The snapshot is taken here: from now on the map holds the style, not a pointer at the row.
        var styleJson = await Library.DocumentAsync(LibraryKinds.Houses, id);
        if (styleJson is null) { note = "That room style could not be read."; return; }

        bound[kind] = styleJson;
        pickedFrom[kind] = id;
        note = null;
        await Write(kind, styleJson);
        await Redraw(kind);
    }

    private async Task Clear(string kind)
    {
        bound.Remove(kind);
        pickedFrom.Remove(kind);
        previews.Remove(kind);
        note = null;
        await Write(kind, null);
        StateHasChanged();
    }

    private async Task Write(string kind, string? styleJson)
    {
        if (Handle is not null) await Handle.InvokeVoidAsync("setRoomStyle", kind, styleJson);
    }

    private async Task Redraw(string kind)
    {
        if (bound.GetValueOrDefault(kind) is not { } styleJson) return;
        if (await Library.RoomStyleSnapshotPreviewAsync(styleJson) is { } views) previews[kind] = views;
        StateHasChanged();
    }
}

/// <summary>The two kinds of room a map binds a shell for. The ids are the wire keys the sketch document holds
/// them under; the words are what the step offers them as.</summary>
public sealed record RoomKindInfo(string Id, string Title, string Blurb)
{
    public static readonly IReadOnlyList<RoomKindInfo> All =
    [
        new("cage", "Wool cages",
            "Every wool room on the map. Its doorway is what an attacker breaks to reach the wool, so only the four breakable materials can fill one."),
        new("spawn", "Spawn cubes",
            "Every team's spawn room. Its doorway is always open whatever the style says — a player spawning in has to be able to walk straight out."),
    ];
}
