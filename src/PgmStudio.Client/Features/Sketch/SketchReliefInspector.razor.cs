using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The Relief phase's inspector: the numbers of the mark under the cursor, and the group settings those
/// numbers are stated against.
///
/// <para>What it shows follows the canvas, the same way the Dressing inspector's does: the selected mark's own
/// numbers when one is selected, and the active tool's <em>starting</em> numbers when none is, so the next
/// mark can be aimed before it is placed rather than corrected after.</para>
///
/// <para>The group's own settings sit below and are shown whether or not a mark is selected. That is not a
/// layout preference: a height means nothing without the base it is read against, and the base, the reach and
/// the block step move every mark in the group at once. There is no separate place to reach them, because
/// there is no moment when they are not what the numbers above mean.</para>
/// </summary>
public partial class SketchReliefInspector
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    /// <summary>The tool the dock has armed (<c>relief:point</c>, …), which is whose starting numbers are
    /// shown when nothing is selected.</summary>
    [Parameter] public string? ActiveTool { get; set; }
    /// <summary>The relief state pushed by the bridge (<c>OnRelief</c>) — the marks, the selection, and the
    /// group in play with its own settings.</summary>
    [Parameter] public string? StateJson { get; set; }

    [Inject] public IJSRuntime JS { get; set; } = default!;

    private JsonObject? mark;          // what is being edited: the selection, else the tool's settings
    private JsonObject? relief;        // the group in play's own settings
    private bool editingSelection;
    private int markCount;
    private List<double> amounts = [];   // a selected push's lift at each ring vertex, expanded
    private string kind = "";
    private string? groupId;
    private string? groupName;
    private double? groupTop;      // the level the group's ground already stands at, or null where nothing
                                    // can be read from it — every height in the panel is stated against it
    private string? groupError;    // what the bridge refused the last group edit with

    private string GroupTitle => $"Group {groupName ?? groupId}";

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    protected override async Task OnParametersSetAsync()
    {
        ReadState();
        if (mark is null) await LoadToolSettings();
    }

    // The bridge pushes one document; which half of it is being edited depends on whether anything is selected.
    private void ReadState()
    {
        mark = null;
        relief = null;
        editingSelection = false;
        kind = ReliefTools.KindOf(ActiveTool) ?? "";
        markCount = 0;
        groupId = null;
        groupName = null;
        groupError = null;

        if (string.IsNullOrWhiteSpace(StateJson)) return;
        JsonNode? root;
        try { root = JsonNode.Parse(StateJson); } catch (JsonException) { return; }
        if (root is not JsonObject state) return;

        markCount = (state["marks"] as JsonArray)?.Count ?? 0;
        amounts = state["amounts"] is JsonArray lifts
            ? [.. lifts.Select(lift => double.TryParse(lift?.ToString(), out var one) ? one : 0)]
            : [];
        groupId = state["groupId"]?.GetValue<string>();
        groupName = state["groupName"]?.GetValue<string>();
        groupTop = state["groupTop"] is { } top && double.TryParse(top.ToString(), out var level) ? level : null;
        if (state["relief"] is JsonObject stated) relief = (JsonObject)stated.DeepClone();
        if (state["selected"] is JsonObject selected)
        {
            mark = (JsonObject)selected.DeepClone();
            kind = mark["kind"]?.GetValue<string>() ?? "";
            editingSelection = true;
        }
    }

    /// <summary>The tool's starting values, fetched when nothing is selected. Kept out of
    /// <see cref="ReadState"/> because it is a round trip and the state push is not.</summary>
    private async Task LoadToolSettings()
    {
        if (Handle is null || string.IsNullOrEmpty(kind)) return;
        var json = await Handle.InvokeAsync<string>("getMarkSettings", kind);
        try { mark = JsonNode.Parse(json) as JsonObject; } catch (JsonException) { mark = null; }
    }

    // ── editing a mark ─────────────────────────────────────────────────────────
    /// <summary>Write one field and push it. A selected mark is patched in place; with nothing selected the
    /// same edit lands on the tool's starting values, so the next mark placed already carries it.</summary>
    private async Task Set(string field, JsonNode? value)
    {
        if (mark is null || Handle is null) return;
        mark[field] = value;
        var patch = new JsonObject { [field] = value?.DeepClone() };
        if (editingSelection) await Handle.InvokeVoidAsync("updateMark", patch.ToJsonString());
        else await Handle.InvokeVoidAsync("setMarkSettings", kind, patch.ToJsonString());
    }

    private Task SetRadius(double value) => Set(MarkFields.Radius, JsonValue.Create(Math.Max(0, value)));
    private Task SetHigh(double value) => Set(MarkFields.High, JsonValue.Create(value));
    private Task SetLow(double value) => Set(MarkFields.Low, JsonValue.Create(value));
    private Task SetFace(double value) => Set(MarkFields.Face, JsonValue.Create(Math.Max(1, value)));
    private Task SetBand(double value) => Set(MarkFields.Band, JsonValue.Create(Math.Max(1, value)));

    private Task Delete() => Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("deleteMark").AsTask();

    // ── a push ─────────────────────────────────────────────────────────────────
    private Task SetAmount(double value) => Set(PushFields.Amount, JsonValue.Create(value));
    private Task SetFalloff(double value) => Set(PushFields.Falloff, JsonValue.Create(Math.Max(0, value)));
    private Task SetCrown(double value) => Set(PushFields.Crown, JsonValue.Create(value));
    private Task SetRoughness(double value) => Set(PushFields.Roughness, JsonValue.Create(Math.Max(0, value)));

    /// <summary>Whether the ring's corners state different lifts — which is the whole reason the per-corner
    /// numbers exist, and the test for whether there is anything to level back out.</summary>
    private bool VariesAlongRing => amounts.Count > 1 && amounts.Any(lift => lift != amounts[0]);

    /// <summary>State one corner's lift. The document collapses the array back to a single amount when every
    /// corner agrees, so this is also the way out: level them and the push is the one an author started
    /// from rather than an array that happens to be flat.</summary>
    private Task SetPushAmount(int at, double value)
    {
        if (Handle is null || at < 0 || at >= amounts.Count) return Task.CompletedTask;
        amounts[at] = value;
        return Handle.InvokeVoidAsync("setPushAmount", at, value).AsTask();
    }

    private async Task LevelAmounts()
    {
        // Level to the FIRST corner rather than to the average: an author levelling a ridge is undoing the
        // fall, and the height they mean is the one they set at the end they started from.
        for (var at = 1; at < amounts.Count; at++) await SetPushAmount(at, amounts[0]);
    }

    // ── heights ────────────────────────────────────────────────────────────────
    /// <summary>How many heights this mark states. One is the common case; a ridgeline gains more as an
    /// author asks it to fall, and the wire format reads a number or an array for exactly that reason.</summary>
    private int HeightCount => mark?[MarkFields.Height] is JsonArray heights ? Math.Max(1, heights.Count) : 1;

    private double Height(int at)
    {
        if (mark?[MarkFields.Height] is JsonArray heights)
            return at < heights.Count && double.TryParse(heights[at]?.ToString(), out var one) ? one : 0;
        return Num(MarkFields.Height);
    }

    /// <summary>Write one of the heights. A single-height mark stays a single number rather than becoming a
    /// one-element array — the document an author hand-writes and the one the editor saves are the same
    /// document, and `"h": 9` is what a person writes.</summary>
    private Task SetHeight(int at, double value)
    {
        if (HeightCount == 1 && at == 0) return Set(MarkFields.Height, JsonValue.Create(value));
        var heights = new JsonArray();
        for (var i = 0; i < HeightCount; i++) heights.Add(JsonValue.Create(i == at ? value : Height(i)));
        return Set(MarkFields.Height, heights);
    }

    /// <summary>Add a point to the ridgeline's height profile, starting at the last one — an author adding a
    /// point means to bend the line from where it already is, not to introduce a step.</summary>
    private Task AddHeight()
    {
        var heights = new JsonArray();
        for (var i = 0; i < HeightCount; i++) heights.Add(JsonValue.Create(Height(i)));
        heights.Add(JsonValue.Create(Height(HeightCount - 1)));
        return Set(MarkFields.Height, heights);
    }

    /// <summary>Collapse the profile back to one height — the way out of a falling ridge, and the reason the
    /// single-number form has to stay reachable rather than being a special case of an array.</summary>
    private Task LevelHeights() => Set(MarkFields.Height, JsonValue.Create(Height(0)));

    // ── editing the group ─────────────────────────────────────────────────────
    /// <summary>Write one of the group's own settings. The bridge answers a sentence when it cannot place
    /// the edit, and that sentence is shown rather than dropped: an edit that lands nowhere and says nothing
    /// is indistinguishable from one that worked.</summary>
    private async Task SetRelief(string field, JsonNode? value)
    {
        if (Handle is null || groupId is null) return;
        var patch = new JsonObject { [field] = value?.DeepClone() };
        groupError = await Handle.InvokeAsync<string?>("updateGroupRelief", patch.ToJsonString());
        if (groupError is null) (relief ??= [])[field] = value;
    }

    /// <summary>The base the group states, falling back to the level it is already drawn at — a relief
    /// replaces the top of every column of its group, so where nothing has been stated the ground is
    /// exactly where the shapes put it.</summary>
    private double Base => ReliefNum(ReliefFields.Base, groupTop ?? FallbackBase);

    /// <summary>What an absent <c>base</c> means, matching the C# record's own default so a document the
    /// editor seeds and one a hand writes mean the same thing by an absent field.</summary>
    private const double FallbackBase = 4;

    private static string Blocks(double count)
        => $"{Math.Round(count)} block{(Math.Abs(Math.Round(count)) == 1 ? "" : "s")}";

    private Task SetBase(double value) => SetRelief(ReliefFields.Base, JsonValue.Create(value));
    private Task SetReach(double value) => SetRelief(ReliefFields.Reach, JsonValue.Create(Math.Max(0, value)));
    private Task SetStep(double value) => SetRelief(ReliefFields.Step, JsonValue.Create((int)Math.Max(1, value)));

    private Task SetGrain(string field, JsonNode? value)
    {
        var grain = relief?[ReliefFields.Grain] is JsonObject stated ? (JsonObject)stated.DeepClone() : [];
        grain[field] = value;
        return SetRelief(ReliefFields.Grain, grain);
    }

    private Task SetGrainAmount(double value) => SetGrain(GrainFields.Amplitude, JsonValue.Create(Math.Max(0, value)));
    private Task SetGrainScale(double value) => SetGrain(GrainFields.Scale, JsonValue.Create((int)Math.Max(1, value)));
    private Task SetGrainSeed(double value) => SetGrain(GrainFields.Seed, JsonValue.Create((int)Math.Max(0, value)));

    // ── what the step strands, and what the ground is meant to be ──────────────
    /// <summary>Whether the block step's stair repair is asked for. Worth asking for whenever the step is
    /// more than one, since that is the setting that turns a riser into a wall.</summary>
    private bool HasStairs => relief?[ReliefFields.Stairs] is { } node && bool.TryParse(node.ToString(), out var on) && on;

    private Task ToggleStairs(ChangeEventArgs e)
        => SetRelief(ReliefFields.Stairs, JsonValue.Create(e.Value is true));

    /// <summary>The word the group states about what kind of ground it is meant to be, or empty for one
    /// stating none. It shapes nothing — the readback measures the solved surface against it.</summary>
    private string Landform => relief?[ReliefFields.Landform]?.ToString() ?? "";

    private Task SetLandform(ChangeEventArgs e)
    {
        var word = e.Value?.ToString() ?? "";
        return SetRelief(ReliefFields.Landform, word.Length == 0 ? null : JsonValue.Create(word));
    }

    // ── the rim ────────────────────────────────────────────────────────────────
    // The rim is a mark in the wire format and a setting in the interface, because it holds the group's whole
    // outline: there is nowhere to place it and nothing to drag. So it is read out of, and written back into,
    // the group's own mark list rather than being a field of its own.
    private JsonObject? Rim => (relief?[ReliefFields.Marks] as JsonArray)
        ?.OfType<JsonObject>().FirstOrDefault(entry => entry["kind"]?.GetValue<string>() == MarkKinds.Rim);

    private bool HasRim => Rim is not null;

    private double RimHeight => Rim?[MarkFields.Height] is { } stated && double.TryParse(stated.ToString(), out var value)
        ? value : Base;

    private async Task ToggleRim(ChangeEventArgs e)
    {
        if (Handle is null || groupId is null || relief is null) return;
        var marks = relief[ReliefFields.Marks] as JsonArray ?? [];
        var kept = new JsonArray();
        foreach (var entry in marks.OfType<JsonObject>())
            if (entry["kind"]?.GetValue<string>() != MarkKinds.Rim) kept.Add(entry.DeepClone());

        // A rim goes on FIRST, so any mark stated later still wins the cells it covers. Written the other way
        // round, a rim would cut a doorway through both ends of every ridge that reaches the outline.
        if (e.Value is true)
        {
            var rim = new JsonObject
            {
                ["kind"] = MarkKinds.Rim,
                [MarkFields.Height] = JsonValue.Create(Base),
                [MarkFields.Depth] = JsonValue.Create(1),
            };
            var reordered = new JsonArray { rim };
            foreach (var entry in kept.OfType<JsonObject>()) reordered.Add(entry.DeepClone());
            await SetRelief(ReliefFields.Marks, reordered);
            return;
        }
        await SetRelief(ReliefFields.Marks, kept);
    }

    private async Task SetRimHeight(double value)
    {
        if (relief?[ReliefFields.Marks] is not JsonArray marks) return;
        var updated = new JsonArray();
        foreach (var entry in marks.OfType<JsonObject>())
        {
            var copy = (JsonObject)entry.DeepClone();
            if (copy["kind"]?.GetValue<string>() == MarkKinds.Rim) copy[MarkFields.Height] = JsonValue.Create(value);
            updated.Add(copy);
        }
        await SetRelief(ReliefFields.Marks, updated);
    }

    // ── reading ────────────────────────────────────────────────────────────────
    private double Num(string field, double fallback = 0)
        => mark?[field] is { } node && double.TryParse(node.ToString(), out var value) ? value : fallback;

    private double ReliefNum(string field, double fallback = 0)
        => relief?[field] is { } node && double.TryParse(node.ToString(), out var value) ? value : fallback;

    private double GrainNum(string field, double fallback = 0)
        => relief?[ReliefFields.Grain]?[field] is { } node && double.TryParse(node.ToString(), out var value)
            ? value : fallback;

    private static readonly Dictionary<string, (string Icon, string Title, string Blurb)> KindInfo = new()
    {
        [MarkKinds.Point] = ("dot", "Spot height",
            "One position held at one height — a summit, a hollow, or a level the ground has to reach here."),
        [MarkKinds.Line] = ("spline", "Ridgeline",
            "A drawn line held at a height, and a band either side of it. Give it more than one height and it falls along its own length."),
        [MarkKinds.Area] = ("pentagon", "Bench",
            "A drawn ring held flat — a floor, a plateau, the ground a building stands on."),
        [MarkKinds.Scarp] = ("triangle", "Scarp",
            "A shelf on one side of a line and open ground on the other — the shelf is on the +z hand of the direction the line is drawn. The face is the drop; where the line stops is where the shelf can be crossed."),
        [MarkKinds.Rim] = ("square-dashed", "Rim",
            "The group's whole outline, held at one height."),
        [MarkKinds.Push] = ("arrows-up-from-line", "Push",
            "A drawn ring the ground is lifted inside, falling away outside it over the skirt. It moves the surface rather than stating a height, so two over the same ground add."),
    };

    private (string Icon, string Title, string Blurb) Info
        => KindInfo.TryGetValue(kind, out var info) ? info : ("shapes", "Relief", "");
}

/// <summary>A mark's own fields (see <see cref="MarkKinds"/> for why these are constants).</summary>
public static class MarkFields
{
    /// <summary>The height a mark states — a number, or one per vertex on a line that falls.</summary>
    public const string Height = "h";
    public const string At = "at";

    /// <summary>How far a point's or a line's height reaches from what it is drawn on. One field, because it
    /// is one quantity: a line's band is twice it, the same way a point's circle is.</summary>
    public const string Radius = "r";
    public const string Points = "points";
    public const string Ring = "ring";
    public const string Depth = "depth";
    public const string High = "high";
    public const string Low = "low";
    public const string Face = "face";
    public const string Band = "band";
}

/// <summary>A push's own fields. <c>Amount</c> is the lift the whole ring takes and <c>Amounts</c> one per
/// ring vertex; the two are not alternatives an author picks between, but the same number before and after it
/// stops being uniform, which is why the inspector edits vertices and the document collapses them back.</summary>
public static class PushFields
{
    public const string Amount = "amount";
    public const string Amounts = "amounts";
    public const string Falloff = "falloff";
    public const string Roughness = "roughness";
    public const string Crown = "crown";
    public const string Ring = "ring";
}

/// <summary>One group's relief settings — what its marks are stated against.</summary>
public static class ReliefFields
{
    public const string Base = "base";
    public const string Reach = "reach";
    public const string Step = "step";
    public const string Stairs = "stairs";
    public const string Landform = "landform";
    public const string Grain = "grain";
    public const string Marks = "marks";
    public const string Pushes = "pushes";
}

/// <summary>The grain's fields, one level down from the relief.</summary>
public static class GrainFields
{
    public const string Amplitude = "amplitude";
    public const string Scale = "scale";
    public const string Seed = "seed";
}

/// <summary>The relief dock's tools, named once. The canvas routes on these strings, so the button, the
/// inspector and the controller all have to agree on them.</summary>
public static class ReliefTools
{
    public const string Point = "relief:point";
    public const string Line = "relief:line";
    public const string Area = "relief:area";
    public const string Scarp = "relief:scarp";
    public const string Push = "relief:push";

    /// <summary>Tool id, the mark kind it places, its glyph, and what it is called. The name names the tool
    /// and does not explain it — a dock tooltip is a label, not a manual.</summary>
    public static readonly (string Tool, string Kind, string Icon, string Name)[] All =
    [
        (Point, MarkKinds.Point, "dot", "Spot height"),
        (Line, MarkKinds.Line, "spline", "Ridgeline"),
        (Area, MarkKinds.Area, "pentagon", "Bench"),
        (Scarp, MarkKinds.Scarp, "triangle", "Scarp"),
        (Push, MarkKinds.Push, "arrows-up-from-line", "Push"),
    ];

    /// <summary>The kind of mark a tool places, or null when the tool places none.</summary>
    public static string? KindOf(string? tool) => All.FirstOrDefault(entry => entry.Tool == tool).Kind;
}
