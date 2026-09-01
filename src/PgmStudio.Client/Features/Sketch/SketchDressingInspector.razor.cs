using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The Dressing phase's inspector: the knobs of the thing under the cursor, and a picture of what they make.
///
/// <para>There is no separate step where dressing is defined. A tree is a decision about a spot on the map, so
/// it is placed on the map and configured where it stands — the same way a spawn or an iron cube is placed in
/// the plan. What this shows therefore follows the canvas: the selected prop's own knobs when one is selected,
/// and the active tool's <em>starting</em> knobs when none is, so the next thing placed can be aimed before it
/// is placed rather than corrected after.</para>
///
/// <para>Every picker is drawn by the pass itself (<c>/api/terrain/path-styles</c>, <c>/boulder-forms</c>,
/// <c>/species</c>) rather than described in words or icons. A path's six styles differ in ways no label
/// captures — where the gaps fall, how the edge wanders — and a picker that could offer a look the export does
/// not produce is worse than no picker.</para>
/// </summary>
public partial class SketchDressingInspector
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    /// <summary>The tool the toolbar has armed (<c>dress:tree</c>, …), which is whose starting knobs are shown
    /// when nothing is selected.</summary>
    [Parameter] public string? ActiveTool { get; set; }
    /// <summary>The dressing state pushed by the bridge (<c>OnDressing</c>) — props, the selection, and the
    /// selected prop itself.</summary>
    [Parameter] public string? StateJson { get; set; }

    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    private JsonObject? prop;                 // what is being edited: the selection, else the tool's settings
    private bool editingSelection;
    private int propCount;
    private int picked;                       // how many props are selected — a join reads more than one
    private string kind = "";

    private DressingPreviewDto? preview;
    private string? refusal;                  // why the gate would not take this prop, in its own sentence
    private string? note;                     // what the last canvas operation did, or would not do
    private string previewedFor = "";
    private IReadOnlyList<PropOptionDto> pathStyles = [];
    private IReadOnlyList<PropOptionDto> waterForms = [];
    private IReadOnlyList<PropOptionDto> boulderForms = [];
    private IReadOnlyList<PropOptionDto> species = [];
    private IReadOnlyList<PropOptionDto> woods = [];
    private string woodedFor = "";
    private IReadOnlyList<PaintBlockDto> blocks = [];
    // The library's styles, so a prop's paving, bank or rock can be filled from one the same way a theme's
    // can. Loaded beside the blocks, since the surfaces that offer one offer the other.
    private IReadOnlyList<StyleDto> styles = [];
    // The map's own default finish. A preview grown on unthemed stone would show ground no themed map paints,
    // so the picture is grown on what this map actually paints.
    private string? themeJson;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    protected override async Task OnParametersSetAsync()
    {
        ReadState();
        await LoadTheme();
        await LoadOptions();
        await RefreshPreview();
    }

    // The bridge pushes one document; which half of it is being edited depends on whether anything is selected.
    private void ReadState()
    {
        prop = null;
        editingSelection = false;
        kind = DressingTools.KindOf(ActiveTool) ?? "";
        propCount = 0;
        picked = 0;
        note = null;

        if (string.IsNullOrWhiteSpace(StateJson)) return;
        JsonNode? root;
        try { root = JsonNode.Parse(StateJson); } catch (JsonException) { return; }
        if (root is not JsonObject state) return;

        propCount = (state["props"] as JsonArray)?.Count ?? 0;
        picked = (state["selection"] as JsonArray)?.Count ?? 0;
        note = state["note"]?.GetValue<string>();
        if (state["selected"] is JsonObject selected)
        {
            prop = (JsonObject)selected.DeepClone();
            kind = prop["kind"]?.GetValue<string>() ?? "";
            editingSelection = true;
        }
    }

    /// <summary>The tool's starting values, fetched when nothing is selected. Kept out of
    /// <see cref="ReadState"/> because it is a round trip and the state push is not.</summary>
    private async Task LoadToolSettings()
    {
        if (Handle is null || string.IsNullOrEmpty(kind)) return;
        var json = await Handle.InvokeAsync<string>("getPropSettings", kind);
        try { prop = JsonNode.Parse(json) as JsonObject; } catch (JsonException) { prop = null; }
    }

    private async Task LoadTheme()
    {
        if (Handle is null || themeJson is not null) return;
        try
        {
            var json = await Handle.InvokeAsync<string>("getThemes");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var mapTheme = root.TryGetProperty("mapTheme", out var name) ? name.GetString() : null;
            themeJson = !string.IsNullOrEmpty(mapTheme)
                && root.TryGetProperty("themes", out var themes)
                && themes.TryGetProperty(mapTheme, out var theme)
                ? theme.GetRawText() : "";
        }
        catch (JsonException) { themeJson = ""; }
    }

    private async Task LoadOptions()
    {
        // The block picker's offered list is the export's own palette, so a path and a rock cannot be paved
        // with something the painter has no colour for.
        if (blocks.Count == 0 && kind is PropKinds.Stroke or PropKinds.Boulder or PropKinds.Water) blocks = await Library.BlocksAsync();
        if (styles.Count == 0 && kind is PropKinds.Stroke or PropKinds.Boulder or PropKinds.Water) styles = await Library.ListAsync<StyleDto>(LibraryKinds.Styles);
        if (kind == PropKinds.Stroke && pathStyles.Count == 0) pathStyles = await Library.PathStylesAsync(Spec(PropFields.Pave));
        if (kind == PropKinds.Water && waterForms.Count == 0) waterForms = await Library.WaterFormsAsync();
        if (kind == PropKinds.Boulder && boulderForms.Count == 0) boulderForms = await Library.BoulderFormsAsync(Spec(PropFields.Rock));
        if (kind == PropKinds.Tree && species.Count == 0) species = await Library.SpeciesAsync();
        if (kind == PropKinds.House && shells.Count == 0) shells = await Library.ListAsync<RoomStyleSummary>(LibraryKinds.Houses);
        if (!editingSelection && prop is null) await LoadToolSettings();
        if (kind == PropKinds.Tree && IsGrown) await LoadWoods();
    }

    /// <summary>The shell a building would be raised in, offered as the library's own cards. Picking one
    /// copies its JSON onto the prop rather than storing its id — a snapshot, the rule a map's bound room
    /// styles follow, so editing that library row later cannot rebuild a map's scenery.</summary>
    private async Task PickShell(RoomStyleSummary shell)
    {
        var json = await Library.DocumentAsync(LibraryKinds.Houses, shell.Id);
        if (json is null) return;
        await Set(PropFields.Style, JsonNode.Parse(json));
        shellName = shell.Name;
    }

    private string? shellName;
    private IReadOnlyList<RoomStyleSummary> shells = [];

    /// <summary>The four walls a door may be cut through, in the wire words <c>RoomEdge</c> serializes as.
    /// Named here rather than in the markup because a Razor markup lambda cannot hold a string literal.</summary>
    private static readonly (string Key, string Label)[] HouseFronts =
    [
        ("negZ", "−z"), ("posZ", "+z"), ("negX", "−x"), ("posX", "+x"),
    ];

    /// <summary>Whether the tree being edited is the grown one rather than a vanilla template.</summary>
    private bool IsGrown => Text(PropFields.Form, PropFields.TemplateForm) == PropFields.GrownForm;

    /// <summary>The wood cards, drawn on the tree the author is actually shaping — so the picker answers "what
    /// would <em>mine</em> look like in that wood". Refetched only when the shape changes, for the same reason
    /// the preview is: every card is a real grow.</summary>
    private async Task LoadWoods()
    {
        var knobs = KnobSpec();
        if (knobs == woodedFor && woods.Count > 0) return;
        woodedFor = knobs;
        woods = await Library.WoodsAsync(knobs);
    }

    /// <summary>The tree's shape as query parameters. Formatted invariantly rather than by the ambient
    /// culture: this is a wire format, and a comma-decimal locale would write "leader=0,55" — where the comma
    /// separates parameters, not digits.</summary>
    private string KnobSpec()
        => $"height={Knob(PropFields.Height, 12)}&stems={Knob(PropFields.Stems, 1, "0")}" +
           $"&leader={Knob(PropFields.Leader, 0.55)}&flow={Knob(PropFields.Flow, 0.45)}" +
           $"&branchAngle={Knob(PropFields.BranchAngle, 1.1)}&levels={Knob(PropFields.Levels, 2, "0")}" +
           $"&leafSize={Knob(PropFields.LeafSize, 0.6)}&whorled={Flag(PropFields.Whorled)}";

    private string Knob(string field, double fallback, string format = "0.##")
        => Num(field, fallback).ToString(format, CultureInfo.InvariantCulture);

    /// <summary>One of the prop's materials as a query parameter, so a shape card is drawn in the material the
    /// author actually chose rather than a stock one.</summary>
    private string? Spec(string field) => Material(field)?.ToJsonString();

    // ── wings ──────────────────────────────────────────────────────────────────
    /// <summary>How many rectangles the selected building states. One is the plain house every board carries;
    /// more is an L, a T or a U stamped under one roof.</summary>
    private int Wings => (prop?["wings"] as JsonArray)?.Count ?? 0;

    /// <summary>Whether the chord has anything to do: two buildings to join, or one joined one to take
    /// apart. The canvas answers the same question for the keyboard, and this is the button's half of it.</summary>
    private bool CanJoin => editingSelection && (picked > 1 || Wings > 1);

    private string JoinLabel => Wings > 1 && picked <= 1 ? "Take apart" : "Join into one building";

    /// <summary>The chord as the platform spells it, so the sentence naming it cannot disagree with the key
    /// that runs it.</summary>
    private string JoinChord => OperatingSystem.IsMacOS() ? "⌘G" : "Ctrl+G";

    private async Task Join()
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("joinDressing");
    }

    /// <summary>Redraw the picture, but only when the prop actually changed — the preview is a round trip that
    /// runs the real pass, so re-issuing it on every render would make a slider feel like treacle.</summary>
    private async Task RefreshPreview()
    {
        if (prop is null) { preview = null; previewedFor = ""; return; }
        var json = prop.ToJsonString();
        if (json == previewedFor) return;
        previewedFor = json;
        var answered = await Library.PropPreviewAsync(json, themeJson);
        preview = answered.Pictures;
        refusal = answered.Refusal;
        StateHasChanged();
    }

    // ── editing ────────────────────────────────────────────────────────────────
    /// <summary>Write one field and push it. A selected prop is patched in place; with nothing selected the
    /// same edit lands on the tool's starting values, so the next prop placed already carries it.</summary>
    private async Task Set(string field, JsonNode? value)
    {
        if (prop is null || Handle is null) return;
        prop[field] = value;
        var patch = new JsonObject { [field] = value?.DeepClone() };
        if (editingSelection) await Handle.InvokeVoidAsync("updateProp", patch.ToJsonString());
        else await Handle.InvokeVoidAsync("setPropSettings", kind, patch.ToJsonString());
        await RefreshPreview();
    }

    /// <summary>Write one field of the flora spec — the one prop whose knobs live a level down, because the
    /// spec is the shared recipe the pass and the preview both read.</summary>
    private async Task SetSpec(string field, JsonNode? value)
    {
        if (prop is null || Handle is null) return;
        if (prop["spec"] is not JsonObject spec) prop["spec"] = spec = new JsonObject();
        spec[field] = value;
        var patch = new JsonObject { ["spec"] = spec.DeepClone() };
        if (editingSelection) await Handle.InvokeVoidAsync("updateProp", patch.ToJsonString());
        else await Handle.InvokeVoidAsync("setPropSettings", kind, patch.ToJsonString());
        await RefreshPreview();
    }

    private Task Delete() => Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("deleteProp").AsTask();

    /// <summary>A new seed for the same knobs — the one control that changes the result without changing the
    /// recipe, so an author who likes the shape but not this particular rock can roll again.</summary>
    private Task Reroll() => Set(PropFields.Seed, JsonValue.Create(Number(prop) + 1));

    private static int Number(JsonObject? prop)
        => prop?["seed"]?.GetValue<int>() ?? 0;

    // ── reading ────────────────────────────────────────────────────────────────
    private double Num(string field, double fallback = 0)
        => prop?[field] is { } node && double.TryParse(node.ToString(), out var value) ? value : fallback;

    private double Spec(string field, double fallback = 0)
        => prop?["spec"]?[field] is { } node && double.TryParse(node.ToString(), out var value) ? value : fallback;

    private string Text(string field, string fallback = "")
        => prop?[field]?.GetValue<string>() ?? fallback;

    private bool Flag(string field, bool fallback = false)
        => prop?[field]?.GetValue<bool>() ?? fallback;

    // A slider stores 0–100 and the model stores 0–1, so every share crosses here rather than at each caller.
    private static double Share(ChangeEventArgs e)
        => double.TryParse(e.Value?.ToString(), out var value) ? Math.Clamp(value / 100, 0, 1) : 0;

    private static double Whole(ChangeEventArgs e)
        => double.TryParse(e.Value?.ToString(), out var value) ? value : 0;

    /// <summary>One of this prop's material nodes — a path's paving, a boulder's rock, a channel's bank. Each
    /// is a full terrain material edited by the same <c>MaterialEditor</c> the theme phase uses, and the editor
    /// mutates the node in place, so persisting it is pushing the node back as a patch.</summary>
    private JsonObject? Material(string field) => prop?[field] as JsonObject;

    private async Task MaterialChanged(string field)
    {
        if (prop is null || Handle is null || Material(field) is not { } material) return;
        var patch = new JsonObject { [field] = material.DeepClone() };
        if (editingSelection) await Handle.InvokeVoidAsync("updateProp", patch.ToJsonString());
        else await Handle.InvokeVoidAsync("setPropSettings", kind, patch.ToJsonString());
        // The shape cards are drawn in the prop's own material, so they are stale the moment it changes.
        if (field == PropFields.Pave) pathStyles = await Library.PathStylesAsync(Spec(field));
        if (field == PropFields.Rock) boulderForms = await Library.BoulderFormsAsync(Spec(field));
        await RefreshPreview();
    }

    /// <summary>Switch a tree between its two forms. The wood cards are drawn on the tree being shaped, so
    /// they are fetched after the switch rather than before it.</summary>
    private async Task SetForm(string form)
    {
        await Set(PropFields.Form, JsonValue.Create(form));
        if (form == PropFields.GrownForm) await LoadWoods();
    }

    private async Task Pick(string field, PropOptionDto option)
    {
        await Set(field, JsonValue.Create(option.Key));
        if (string.IsNullOrWhiteSpace(option.Defaults) || prop is null || Handle is null) return;

        JsonNode? implied;
        try { implied = JsonNode.Parse(option.Defaults); } catch (JsonException) { return; }
        if (implied is not JsonObject patch) return;

        foreach (var entry in patch) prop[entry.Key] = entry.Value?.DeepClone();
        if (editingSelection) await Handle.InvokeVoidAsync("updateProp", patch.ToJsonString());
        else await Handle.InvokeVoidAsync("setPropSettings", kind, patch.ToJsonString());
        await RefreshPreview();
    }

    private static readonly IReadOnlyDictionary<string, (string Icon, string Title, string Blurb)> KindInfo =
        new Dictionary<string, (string, string, string)>
        {
            [PropKinds.Stroke] = ("spline", "Stroke", "A band of surface along a line you draw. It swaps the ground it crosses rather than building on it — a road, a worn trail, a smear of dirt or a painted forest floor, depending on the brush and what it lays. Mark it a route and trees and boulders will keep clear of it."),
            [PropKinds.Water] = ("waves", "Water", "A channel of water. It cuts a bed into the ground and fills it to a level line — the one prop that takes terrain away rather than standing on it. Only existing ground is cut, and it is mirrored across the map's symmetry."),
            [PropKinds.Flora] = ("flower", "Cover", "Grass, fern and flowers over the soil inside the area you drew. Masked by the paint beneath — nothing grows on a plaza's quartz."),
            [PropKinds.Tree] = ("trees", "Tree", "One tree, standing where you put it. Mirrored across the map's symmetry, so both teams get the same cover."),
            [PropKinds.Boulder] = ("mountain", "Boulder", "One erratic, standing where you put it and bedded into the ground. Mirrored across the map's symmetry, so both teams get the same cover."),
            [PropKinds.House] = ("home", "Building", "A building on the rectangle you dragged, raised in a shell from the room-style library. It settles into the ground it covers, and it is mirrored across the map's symmetry, so both teams get the same cover."),
        };

    private (string Icon, string Title, string Blurb) Info
    {
        get
        {
            if (!KindInfo.TryGetValue(kind, out var info)) return ("shapes", "Dressing", "");
            // A tree is two trees, and which one is being edited changes what the sentence should say.
            return kind == PropKinds.Tree
                ? info with { Blurb = info.Blurb + (IsGrown
                    ? " Grown from a branch skeleton you shape, in the wood you choose."
                    : " A vanilla tree of its species: trunk, canopy, proportions.") }
                : info;
        }
    }
}

/// <summary>The prop field names, as constants. Two reasons rather than one: a Razor markup lambda cannot
/// contain a string literal at all, and naming the wire fields once means the form and the model cannot drift
/// apart over a typo that would silently write a field nothing reads.</summary>
public static class PropKinds
{
    public const string Stroke = "stroke";
    public const string Water = "water";
    public const string Flora = "flora";
    public const string Tree = "tree";
    public const string Boulder = "boulder";
    public const string House = "house";
}

/// <summary>A prop's own fields (see <see cref="PropKinds"/> for why these are constants).</summary>
public static class PropFields
{
    public const string Radius = "radius";
    /// <summary>Which wall a building's door is cut through — <c>null</c> lets it pick a long side.</summary>
    public const string Front = "front";
    /// <summary>A path's band style, and a building's whole shell. One wire name, two prop kinds: the field is
    /// named once here because it is one field name, whatever the prop it sits on means by it.</summary>
    public const string Style = "style";
    public const string Coverage = "coverage";

    /// <summary>Whether a stroke is a way through rather than paint. It is what a tree's and a boulder's
    /// standoff is measured to, and the style says nothing about it.</summary>
    public const string Route = "route";
    /// <summary>What a path is paved with — a full terrain material, not a block list.</summary>
    public const string Pave = "pave";
    public const string Depth = "depth";
    public const string Edge = "edge";
    public const string Shore = "shore";
    public const string ShoreWander = "shoreWander";
    public const string Bank = "bank";
    public const string Species = "species";
    public const string Height = "height";
    public const string Stems = "stems";
    public const string Leader = "leader";
    public const string Flow = "flow";
    public const string BranchAngle = "branchAngle";
    public const string Levels = "levels";
    public const string Whorled = "whorled";
    public const string LeafSize = "leafSize";
    public const string Wood = "wood";
    /// <summary>Which shape a prop takes — a boulder's rock family, a tree's vanilla-or-grown. One wire name
    /// because the two never share an object; the distinction lives in their C# types.</summary>
    public const string Form = "form";
    public const string Size = "size";
    /// <summary>What a boulder is cut from — a full terrain material, resolved in the rock's own frame.</summary>
    public const string Rock = "rock";
    public const string Mossy = "mossy";
    public const string Seed = "seed";

    /// <summary>The values a fresh prop starts at, for the reads that need a fallback.</summary>
    public const string SolidStyle = "solid";
    public const string RoundForm = "round";
    public const string OakSpecies = "oak";
    public const string OakWood = "oak";
    public const string TemplateForm = "template";
    public const string GrownForm = "grown";
    public const string WornStyle = "worn";
    public const string CanalForm = "canal";
}

/// <summary>The flora spec's fields — one level down from a prop, because the spec is the shared recipe the
/// pass and the preview both read.</summary>
public static class SpecFields
{
    public const string Coverage = "coverage";
    public const string Scale = "scale";
    public const string FernShare = "fernShare";
    public const string FlowerShare = "flowerShare";
    public const string TallShare = "tallShare";
}

/// <summary>The dressing toolbar's tools, named once. The canvas routes on these strings, so the button, the
/// inspector and the controller all have to agree on them.</summary>
public static class DressingTools
{
    public const string Stroke = "dress:stroke";
    public const string Water = "dress:water";
    public const string Flora = "dress:flora";
    public const string House = "dress:house";
    public const string Tree = "dress:tree";
    public const string Boulder = "dress:boulder";

    /// <summary>Tool id, the prop kind it places, its glyph, and what it is called. The name names the tool
    /// and does not explain it — a dock tooltip is a label, not a manual.</summary>
    public static readonly (string Tool, string Kind, string Icon, string Name)[] All =
    [
        (Stroke, PropKinds.Stroke, "spline", "Stroke"),
        (Water, PropKinds.Water, "waves", "Water"),
        (Flora, PropKinds.Flora, "flower", "Ground cover"),
        (House, PropKinds.House, "home", "Building"),
        (Tree, PropKinds.Tree, "trees", "Tree"),
        (Boulder, PropKinds.Boulder, "mountain", "Boulder"),
    ];

    /// <summary>The kind of prop a tool places, or null when the tool places none.</summary>
    public static string? KindOf(string? tool) => All.FirstOrDefault(entry => entry.Tool == tool).Kind;
}
