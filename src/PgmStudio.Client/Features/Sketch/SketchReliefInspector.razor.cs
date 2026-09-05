using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
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

    /// <summary>What the group's own settings sit under. A group already called "Group 1" does not want the
    /// word twice, and one called <c>i1</c> does — so the word is added only where the name does not carry
    /// it.</summary>
    private string GroupTitle
    {
        get
        {
            var name = groupName ?? groupId ?? "";
            return name.StartsWith("group", StringComparison.OrdinalIgnoreCase) ? name : $"Group {name}";
        }
    }

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
    /// <see cref="ReadState"/> because it is a round trip and the state push is not.
    ///
    /// <para>The answer is discarded where the panel has moved on while it was in flight. A state push
    /// carrying a selection lands synchronously and this does not, so an armed tool's defaults can come back
    /// after a mark has been picked and replace the mark's own numbers with them — the panel then shows one
    /// kind's fields under another kind's heading.</para></summary>
    private async Task LoadToolSettings()
    {
        if (Handle is null || string.IsNullOrEmpty(kind)) return;
        var asked = kind;
        var json = await Handle.InvokeAsync<string>("getMarkSettings", asked);
        if (editingSelection || kind != asked) return;
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

    /// <summary>Stop stating a field. A null in the patch deletes the key rather than writing a JSON null,
    /// so a document the editor saves carries the fields it states and nothing else.</summary>
    private async Task Remove(params string[] fields)
    {
        foreach (var field in fields) await Set(field, null);
    }

    private Task SetRadius(double value) => Set(MarkFields.Radius, JsonValue.Create(Math.Max(0, value)));
    private Task SetHigh(double value) => Set(MarkFields.High, JsonValue.Create(value));
    private Task SetLow(double value) => Set(MarkFields.Low, JsonValue.Create(value));
    private Task SetFace(double value) => Set(MarkFields.Face, JsonValue.Create(Math.Max(1, value)));
    private Task SetBand(double value) => Set(MarkFields.Band, JsonValue.Create(Math.Max(1, value)));

    private Task Delete() => Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("deleteMark").AsTask();

    // ── the name a finding uses ────────────────────────────────────────────────
    /// <summary>The mark's id, which is its name everywhere else: a seam is reported as a pair of them and
    /// <c>RL4</c> names the one that pinned nothing.</summary>
    private string MarkId => mark?[MarkFields.Id]?.ToString() ?? "";

    /// <summary>Why the last rename did not take — a name another mark already carries, or none at all.</summary>
    private string? nameError;

    /// <summary>Rename the selected mark. The id is the document's own key and the selection rides on it, so
    /// the canvas performs the swap and answers what it refused rather than the panel writing a patch.</summary>
    private async Task Rename(ChangeEventArgs e)
    {
        if (Handle is null || !editingSelection) return;
        nameError = await Handle.InvokeAsync<string?>("renameMark", (e.Value?.ToString() ?? "").Trim());
    }

    // ── a line's band ──────────────────────────────────────────────────────────
    /// <summary>How far the band reaches either side of the centerline — what a tread is measured against.</summary>
    private double Radius => Num(MarkFields.Radius, 2);

    /// <summary>Whether the line states a tread at all. Absent and present-at-zero are different statements —
    /// no tread holds the whole band flat, a tread of zero holds none of it — so this asks whether the field
    /// is there rather than what it says.</summary>
    private bool HasTread => mark?[MarkFields.Tread] is JsonValue stated
                             && double.TryParse(stated.ToString(), out _);

    private double Tread => Num(MarkFields.Tread, Radius);
    private double Batter => Num(MarkFields.Batter);

    /// <summary>What the numbers leave, stated as the shoulder they make. A tread's whole effect is the band
    /// it does <em>not</em> hold flat, and that width is what the grade between two passes is spent over.</summary>
    private string TreadReadout
    {
        get
        {
            // Kept to the half rather than rounded: a tread is stated in halves, so a rounded shoulder would
            // report a width the numbers above it do not add up to.
            var shoulder = Math.Max(0, Radius - Tread);
            var width = $"{shoulder:0.#} block{(Math.Abs(shoulder - 1) < 0.01 ? "" : "s")}";
            return shoulder <= 0 ? "No shoulder — the whole band is held flat."
                 : Batter > 0 ? $"{width} of shoulder either side, falling at {Batter:0}°."
                 : $"{width} of shoulder either side, at whatever angle the passes leave it.";
        }
    }

    /// <summary>Start or stop stating a tread. Starting takes half the band, which is a road with as much
    /// shoulder as surface; stopping drops the batter with it, since a batter is the angle of a shoulder that
    /// no longer exists.</summary>
    private Task ToggleTread(ChangeEventArgs e)
        => e.Value is true
            ? Set(MarkFields.Tread, JsonValue.Create(Math.Round(Radius / 2, 1)))
            : Remove(MarkFields.Tread, MarkFields.Batter);

    private Task SetTread(double value)
        => Set(MarkFields.Tread, JsonValue.Create(Math.Clamp(value, 0, Radius)));

    // Zero is what an unstated batter means to the solver, so it is written by removing the field rather
    // than by storing the number that stands for its absence.
    private Task SetBatter(double value)
        => value <= 0 ? Remove(MarkFields.Batter)
                      : Set(MarkFields.Batter, JsonValue.Create(Math.Clamp(value, 1, 89)));

    // ── an area's surface ──────────────────────────────────────────────────────
    /// <summary>How many corners the drawn ring has — a tilted area states one height per one of them.</summary>
    private int RingCount => mark?[MarkFields.Ring] is JsonArray ring ? ring.Count : 0;

    /// <summary>Whether the ring states a height per corner. The test is the solver's: heights that do not
    /// match the ring one for one are read as the single first height, so a document that half-states a tilt
    /// shows the level form here and means the level form there.</summary>
    private bool Tilted => HeightCount > 1 && HeightCount == RingCount;

    /// <summary>Give the ring a height at every corner, all at the level it already holds — an author tilting
    /// a pad means to bend the one they have, not to place a new one.</summary>
    private Task TiltArea()
    {
        var heights = new JsonArray();
        for (var i = 0; i < RingCount; i++) heights.Add(JsonValue.Create(Height(0)));
        return Set(MarkFields.Height, heights);
    }

    private double Bevel => Num(MarkFields.Bevel);

    private Task SetBevel(double value)
        => value <= 0 ? Remove(MarkFields.Bevel) : Set(MarkFields.Bevel, JsonValue.Create(value));

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

    /// <summary>Where one of a line's heights lands. They are spaced evenly along the run and have nothing to
    /// do with the points the line was drawn with — a two-point line can state five heights — so they are
    /// named by the position they hold rather than numbered as if they were vertices.</summary>
    private static string StationLabel(int at, int count)
        => count <= 1 ? "Height"
         : at == 0 ? "At the start"
         : at == count - 1 ? "At the end"
         : $"{100.0 * at / (count - 1):0}% along";

    /// <summary>Add a height to the profile, starting at the last one — an author adding one means to bend
    /// the run from where it already is, not to introduce a step.</summary>
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

    // ── what the ground is meant to be ─────────────────────────────────────────
    /// <summary>The word the group states about what kind of ground it is meant to be, or empty for one
    /// stating none. It shapes nothing — the readback measures the solved surface against it.</summary>
    private string Landform => relief?[ReliefFields.Landform]?.ToString() ?? "";

    /// <summary>The four words a group can claim, each under what the vocabulary says it is. The notes are
    /// the constants' own summaries rather than a second wording, so the word an author picks, the word the
    /// document uses and the word <c>RL1</c> cites are one word with one meaning.</summary>
    private static readonly SelectOption[] Landforms =
    [
        new(Vocabulary.Landform.Plain, "plain",
            "Ground a player crosses without thinking about it."),
        new(Vocabulary.Landform.Rolling, "rolling",
            "Rises and falls enough to break a sight line and shape a route."),
        new(Vocabulary.Landform.Hills, "hills",
            "Real climbs — a route goes round or over, and the choice matters."),
        new(Vocabulary.Landform.Mountain, "mountain",
            "Ground the map is built against rather than on: a range, a rim, a wall of land."),
    ];

    private Task SetLandform(string word)
        => SetRelief(ReliefFields.Landform, word.Length == 0 ? null : JsonValue.Create(word));

    // ── the rim ────────────────────────────────────────────────────────────────
    // The rim is a mark in the wire format and a setting in the interface, because it holds the group's whole
    // outline: there is nowhere to place it and nothing to drag. So it is read out of, and written back into,
    // the group's own mark list rather than being a field of its own.
    private JsonObject? Rim => (relief?[ReliefFields.Marks] as JsonArray)
        ?.OfType<JsonObject>().FirstOrDefault(entry => entry["kind"]?.GetValue<string>() == MarkKinds.Rim);

    private bool HasRim => Rim is not null;

    private double RimHeight => Rim?[MarkFields.Height] is { } stated && double.TryParse(stated.ToString(), out var value)
        ? value : Base;

    /// <summary>How many rings in from the outline the rim holds. One is the boundary cells themselves; more
    /// is a coastal shelf, which is how a group gets flat ground to stand on at its own edge.</summary>
    private double RimDepth => Rim?[MarkFields.Depth] is { } stated && double.TryParse(stated.ToString(), out var value)
        ? value : 1;

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

    private Task SetRimHeight(double value) => SetRim(MarkFields.Height, JsonValue.Create(value));

    private Task SetRimDepth(double value)
        => SetRim(MarkFields.Depth, JsonValue.Create((int)Math.Max(1, value)));

    /// <summary>Write one of the rim's fields back into the group's own mark list, since that is where the rim
    /// lives: it is a mark on the wire and a setting in the panel, and the list keeps its order.</summary>
    private async Task SetRim(string field, JsonNode? value)
    {
        if (relief?[ReliefFields.Marks] is not JsonArray marks) return;
        var updated = new JsonArray();
        foreach (var entry in marks.OfType<JsonObject>())
        {
            var copy = (JsonObject)entry.DeepClone();
            if (copy["kind"]?.GetValue<string>() == MarkKinds.Rim) copy[field] = value?.DeepClone();
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

    // ── what the numbers work out to ───────────────────────────────────────────
    // Each of these is a fact about the mark or the group as it stands. A knob doing nothing says nothing:
    // the empty string renders no line at all, so a panel is quiet until an author has stated something.

    /// <summary>A point's disc, in the width a player crosses. A radius under a cell is a spike, which is a
    /// different thing from a summit and the only case worth naming.</summary>
    private string PointReadout
    {
        get
        {
            var radius = Num(MarkFields.Radius, 4);
            return radius < 1 ? "Pins one cell — a spike, not a summit."
                              : $"Holds a disc {Span(radius * 2)} across.";
        }
    }

    /// <summary>A line's band, and whether its heights fall along it. Both are read off the fields above, so
    /// the line says what the drawing already decided rather than what a line mark is for.</summary>
    private string LineReadout
    {
        get
        {
            var band = $"A band {Span(Radius * 2)} wide";
            if (HeightCount < 2) return $"{band}, level its whole run.";
            double first = Height(0), last = Height(HeightCount - 1);
            return first.Equals(last) ? $"{band}, back at {Trim(first)} where it started."
                                      : $"{band}, running {Trim(first)} to {Trim(last)}.";
        }
    }

    /// <summary>An area's surface and its edge. The flat core is the number a bevel can eat: stated wider
    /// than half the ring's narrow span there is nothing left holding the height the pad was drawn for.</summary>
    private string AreaReadout
    {
        get
        {
            var surface = Tilted
                ? $"Falls {Trim(CornerHigh)} to {Trim(CornerLow)} across its corners"
                : $"Level at {Trim(Height(0))}";
            if (Bevel <= 0) return $"{surface}, stated to its own outline — it meets its neighbour on a step.";
            var core = RingSpan - 2 * Bevel;
            return core <= 0
                ? $"{surface}. The bevel is wider than the ring — nothing is left flat."
                : $"{surface}. Edge grades over {Span(Bevel)}; {Trim(core)} of its {Span(RingSpan)} stays flat.";
        }
    }

    /// <summary>What a scarp is actually choosing, which is the grade rather than the drop. One number spells
    /// all three answers, so the reading says which of them this face gives.</summary>
    private string ScarpReadout
    {
        get
        {
            double high = Num(MarkFields.High, 11), low = Num(MarkFields.Low, 5);
            var face = Math.Max(1, Num(MarkFields.Face, 2));
            var drop = Math.Abs(high - low);
            var grade = drop / face;
            // The three answers a grade gives, from the mark's own docstring: a ramp, a face a block gets a
            // player up, and one that is only ever descended.
            var crossing = grade <= 1 ? "walked up"
                         : grade <= 2 ? "crossed by placing a block"
                         : "only ever descended";
            var points = mark?[MarkFields.Points] is JsonArray line ? line.Count : 0;
            var run = points > 2 ? $" Held over all {points} points." : "";
            return $"Drops {Trim(drop)} over a {Trim(face)}-block face: {grade:0.#} a block, {crossing}.{run}";
        }
    }

    /// <summary>A push's two gradients, which are the two things it can disagree with itself about: the skirt
    /// climbs <c>amount / falloff</c> and the crown <c>crown</c> over the distance to the shape's spine.</summary>
    private string PushReadout
    {
        get
        {
            var amount = Num(PushFields.Amount, 5);
            var falloff = Num(PushFields.Falloff, 10);
            var crown = Num(PushFields.Crown, 2);
            var verb = amount < 0 ? "Digs" : "Lifts";
            var skirt = falloff <= 0
                ? "a sheer edge at the ring"
                : $"over {Span(falloff)} of skirt, {Math.Abs(amount) / falloff:0.#} a block";
            var top = crown == 0 ? "flat on top"
                    : crown > 0 ? $"domed {Span(crown)} at its spine"
                    : $"dished {Span(-crown)} at its spine";
            return $"{verb} {Span(Math.Abs(amount))}, {skirt} — {top}.";
        }
    }

    /// <summary>Where the corners of a push's ring disagree. Nothing where they do not, since a uniform ring
    /// is the single amount the row above already states.</summary>
    private string PushCornerReadout => !VariesAlongRing ? ""
        : $"Varies {Trim(amounts.Min())} to {Trim(amounts.Max())} around the ring.";

    /// <summary>What a block step above one costs. At one there is nothing to say — every surface is quantised
    /// to blocks — and above it every riser the ground makes is that tall, which is the one knob here that can
    /// break a map.</summary>
    private string StepReadout
    {
        get
        {
            var step = ReliefNum(ReliefFields.Step, 1);
            return step <= 1 ? "" : $"Every riser the ground makes is {Span(step)} tall.";
        }
    }

    /// <summary>What the grain adds. Off is worth saying, because a zero amplitude and no grain at all are the
    /// same surface and an author who set one and got neither would have nowhere to look.</summary>
    private string GrainReadout
    {
        get
        {
            var amount = GrainNum(GrainFields.Amplitude);
            return amount <= 0 ? "Off — the surface is exactly what the marks solved."
                 : $"Moves the solved surface up to {Span(amount)}, over features about "
                   + $"{Span(GrainNum(GrainFields.Scale, 12))} across.";
        }
    }

    /// <summary>How far in the rim reaches, once there is one. Its height is the row above; what a reader
    /// cannot see is how much of the group's edge it flattens.</summary>
    private string RimReadout => !HasRim ? ""
        : $"Held at {Trim(RimHeight)}, {Span(RimDepth)} in from the outline.";

    // ── reading the numbers a readout is built from ────────────────────────────
    /// <summary>The narrower span of the ring's own box, which is what a bevel eats into from both sides.</summary>
    private double RingSpan
    {
        get
        {
            if (mark?[MarkFields.Ring] is not JsonArray ring || ring.Count < 3) return 0;
            var xs = new List<double>();
            var zs = new List<double>();
            foreach (var point in ring.OfType<JsonArray>().Where(point => point.Count >= 2))
            {
                if (double.TryParse(point[0]?.ToString(), out var x)) xs.Add(x);
                if (double.TryParse(point[1]?.ToString(), out var z)) zs.Add(z);
            }
            return xs.Count == 0 || zs.Count == 0 ? 0
                 : Math.Min(xs.Max() - xs.Min(), zs.Max() - zs.Min());
        }
    }

    private double CornerLow => Enumerable.Range(0, HeightCount).Select(Height).Min();
    private double CornerHigh => Enumerable.Range(0, HeightCount).Select(Height).Max();

    /// <summary>A length in blocks, kept to the half a tread can be stated in and pluralised on the value it
    /// actually shows.</summary>
    private static string Span(double blocks)
        => $"{blocks:0.#} block{(Math.Abs(blocks - 1) < 0.01 ? "" : "s")}";

    /// <summary>A height with no trailing zero, since a mark states whole courses far more often than not.</summary>
    private static string Trim(double height) => $"{height:0.#}";

    private static readonly Dictionary<string, (string Icon, string Title, string Blurb)> KindInfo = new()
    {
        [MarkKinds.Point] = ("dot", "Spot height",
            "One position held at one height — a summit, a hollow, or a level the ground has to reach here."),
        [MarkKinds.Line] = ("spline", "Ridgeline",
            "A drawn line held at a height, and a band either side of it. Give it more than one height and it falls along its own length."),
        [MarkKinds.Area] = ("pentagon", "Bench",
            "A drawn ring the ground is held to — a floor, a plateau, or a shelf cut into a slope."),
        [MarkKinds.Scarp] = ("triangle", "Scarp",
            "A shelf on one side of a drawn line and open ground on the other, with the drop between them. One level each, the whole run — a scarp states a drop rather than a profile. The shelf takes the +z hand of the direction the line is drawn, and the band stops where the line stops."),
        [MarkKinds.Rim] = ("square-dashed", "Rim",
            "The group's whole outline, held at one height."),
        [MarkKinds.Push] = ("arrows-up-from-line", "Push",
            "A drawn ring the ground is lifted inside, falling away over the skirt outside it. It moves the surface rather than stating a height, so two over the same ground add."),
    };

    private (string Icon, string Title, string Blurb) Info
        => KindInfo.TryGetValue(kind, out var info) ? info : ("shapes", "Relief", "");
}

/// <summary>A mark's own fields (see <see cref="MarkKinds"/> for why these are constants).</summary>
public static class MarkFields
{
    /// <summary>The height a mark states — a number, or one per vertex on a line or an area that tilts.</summary>
    public const string Height = "h";
    public const string At = "at";

    /// <summary>The mark's own name, which is what every finding about it calls it.</summary>
    public const string Id = "id";

    /// <summary>How far a point's or a line's height reaches from what it is drawn on. One field, because it
    /// is one quantity: a line's band is twice it, the same way a point's circle is.</summary>
    public const string Radius = "r";

    /// <summary>How much of a line's band is flat. Absent holds the whole band, which is what a ridge wants;
    /// stated, the rest of the band grades between the line's own passes instead of stepping between them.</summary>
    public const string Tread = "tread";

    /// <summary>How steeply that graded shoulder falls, in degrees. Absent takes whatever run the drawing
    /// leaves.</summary>
    public const string Batter = "batter";

    /// <summary>How far inside an area's ring its height gives way to the ground around it — the tread of an
    /// area, stated from the rim inward because that is where an area's edge is.</summary>
    public const string Bevel = "bevel";

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
