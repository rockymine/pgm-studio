using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Vocabulary;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

using PgmStudio.Client.Features.Library;

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
/// <para>Every picker is drawn by the pass itself (<c>/api/terrain/stroke-styles</c>, <c>/boulder-forms</c>,
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
    /// <summary>The board's layers, as the canvas layer strip lists them — the storeys a selected prop may be
    /// moved to.</summary>
    [Parameter] public IReadOnlyList<SketchLayerRow> Layers { get; set; } = [];

    [Inject] public TerrainLibraryClient Library { get; set; } = default!;
    [Inject] public IJSRuntime JS { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;

    private JsonObject? prop;                 // what is being edited: the selection, else the tool's settings
    private bool editingSelection;
    private int propCount;
    private int picked;                       // how many props are selected — a join reads more than one
    private string kind = "";

    private DressingPreviewDto? preview;
    private string? refusal;                  // why the gate would not take this prop, in its own sentence
    private string? note;                     // what the last canvas operation did, or would not do
    /// <summary>The document's recipe registry, so a preview of one placement can resolve the key it names —
    /// a prop on its own has no document behind it.</summary>
    private JsonObject? styleRegistry;
    private string previewedFor = "";
    private IReadOnlyList<PropOptionDto> strokeStyles = [];
    private IReadOnlyList<PropOptionDto> fluidForms = [];
    private IReadOnlyList<PropOptionDto> boulderForms = [];
    private IReadOnlyList<PropOptionDto> species = [];
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
        styleRegistry = null;

        if (string.IsNullOrWhiteSpace(StateJson)) return;
        JsonNode? root;
        try { root = JsonNode.Parse(StateJson); } catch (JsonException) { return; }
        if (root is not JsonObject state) return;

        propCount = (state["props"] as JsonArray)?.Count ?? 0;
        styleRegistry = state["styles"] as JsonObject;
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
        if (blocks.Count == 0 && kind is PropKinds.Stroke or PropKinds.Boulder or PropKinds.Fluid) blocks = await Library.BlocksAsync();
        if (styles.Count == 0 && kind is PropKinds.Stroke or PropKinds.Boulder or PropKinds.Fluid) styles = await Library.ListAsync<StyleDto>(LibraryKinds.Styles);
        if (kind == PropKinds.Stroke && strokeStyles.Count == 0) strokeStyles = await Library.StrokeStylesAsync(Spec(PropFields.Pave));
        if (kind == PropKinds.Fluid && fluidForms.Count == 0) fluidForms = await Library.FluidFormsAsync();
        if (kind == PropKinds.House && shells.Count == 0) shells = await Library.ListAsync<RoomStyleSummary>(LibraryKinds.Houses);
        if (RecipeKind is { } recipeKind && recipesFor != recipeKind.Slug)
        {
            recipesFor = recipeKind.Slug;
            recipes = await Library.ListAsync<LibraryRow>(recipeKind);
        }
        if (!editingSelection && prop is null) await LoadToolSettings();
    }

    /// <summary>The shell a building would be raised in, offered as the library's own cards. Picking one pulls
    /// it into the document's registry and names the key the registry gave it on the placement — the same road
    /// a tree and a boulder take, so one doctrine covers all three. The registry is the document's, so editing
    /// that library row later cannot rebuild a map's scenery.</summary>
    private async Task PickShell(RoomStyleSummary shell)
    {
        if (Handle is null) return;
        if (await Library.DocumentAsync(LibraryKinds.Houses, shell.Id) is not { } json) return;
        // A room style composes to the stamper's own `HouseStyle`; the registry holds recipes, so it rides
        // under the `house` kind the way a tree's rides under `tree`.
        var recipe = new JsonObject { ["kind"] = "house", ["shell"] = JsonNode.Parse(json) };
        if (await Handle.InvokeAsync<string?>("pullRecipe", shell.Name, recipe.ToJsonString()) is not { } key) return;
        await Set(PropFields.Style, JsonValue.Create(key));
    }

    /// <summary>Which shell the building names. Read off the prop, so a reopened map shows the card in use.</summary>
    private string? shellName => Text(PropFields.Style, string.Empty) is { Length: > 0 } key ? key : null;

    /// <summary>Whether the placement's registry key came from the card carrying this name. A key is the row's
    /// name or that name numbered, so both readings are matched; two rows sharing a name both answer yes,
    /// which is the one case a name cannot tell apart and the reason the key no longer is one.</summary>
    private static bool Wears(string? key, string name)
        => key is not null && (key == name
            || (key.Length > name.Length + 1 && key.StartsWith(name, StringComparison.Ordinal)
                && key[name.Length] == '-' && key[(name.Length + 1)..].All(char.IsAsciiDigit)));
    private IReadOnlyList<RoomStyleSummary> shells = [];

    // ── the recipe a click puts down ───────────────────────────────────────────
    /// <summary>Which library a placement of this kind names a recipe from, or null for the drawn kinds — a
    /// channel and a track are traced, so what they are is stated where they are drawn.</summary>
    private LibraryKind? RecipeKind => kind switch
    {
        PropKinds.Tree => LibraryKinds.Trees,
        PropKinds.Boulder => LibraryKinds.Boulders,
        _ => null,
    };

    private IReadOnlyList<LibraryRow> recipes = [];
    private string recipesFor = "";

    /// <summary>The recipe the placement names. Read off the prop rather than remembered, so a reopened map
    /// shows the card that is actually in use.</summary>
    private string? recipeName => Text(PropFields.Style, string.Empty) is { Length: > 0 } key ? key : null;

    /// <summary>Name this recipe on the placement, and put it in the document's registry if it is not there
    /// yet. The key is the row's name where nothing else holds it, and a numbered variant where two library
    /// rows read the same way — so the placement names what it is actually made of.</summary>
    private async Task PickRecipe(LibraryRow recipe)
    {
        if (RecipeKind is not { } recipeKind || Handle is null) return;
        if (await Library.DocumentAsync(recipeKind, recipe.Id) is not { } json) return;
        if (await Handle.InvokeAsync<string?>("pullRecipe", recipe.Name, json) is not { } key) return;
        await Set(PropFields.Style, JsonValue.Create(key));
    }

    /// <summary>Open the library at this kind, so authoring another is one click from wanting one.</summary>
    private void OpenLibrary()
    {
        if (RecipeKind is { } recipeKind) Nav.NavigateTo($"/library/{recipeKind.Slug}");
    }

    /// <summary>The four sides a building's door or a chest's front may face, in the wire words <c>RoomEdge</c>
    /// serializes as. Named here rather than in the markup because a Razor markup lambda cannot hold a string
    /// literal.</summary>
    private static readonly (string Key, string Label)[] Sides =
    [
        ("negZ", "−z"), ("posZ", "+z"), ("negX", "−x"), ("posX", "+x"),
    ];

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

    private string JoinLabel => Wings > 1 && picked <= 1 ? "Split" : "Join buildings";

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
        // The prop plus the recipes it names: the preview reads a registry of one off the prop itself, which
        // is how a placement is drawn without the document it belongs to.
        var asked = (JsonObject)prop.DeepClone();
        if (styleRegistry is not null) asked["styles"] = styleRegistry.DeepClone();
        var json = asked.ToJsonString();
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

    /// <summary>The crops a flora spec sows; unstated is wheat, which is what the pass sows then.</summary>
    private IReadOnlyList<string> Crops() =>
        prop?["spec"]?[SpecFields.Crops] is JsonArray named && named.Count > 0
            ? [.. named.Select(word => word?.GetValue<string>() ?? string.Empty)]
            : [CropKinds.Wheat];

    /// <summary>Sow or stop sowing one crop. The last one left stays, since a field sows at least one.</summary>
    private Task ToggleCrop(string crop)
    {
        var crops = Crops().ToList();
        if (crops.Contains(crop)) { if (crops.Count > 1) crops.Remove(crop); }
        else crops.Add(crop);
        return SetSpec(SpecFields.Crops, new JsonArray([.. CropKinds.All.Where(crops.Contains).Select(word => JsonValue.Create(word))]));
    }

    private Task Delete() => Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("deleteProp").AsTask();

    // ── a chest's stacks ─────────────────────────────────────────────────────────
    private JsonArray ChestItems() => prop?[PropFields.Items] as JsonArray ?? [];

    /// <summary>Write the stacks back whole: the list is small, and one patch of it is the edit the canvas takes.</summary>
    private Task WriteChestItems(JsonArray items) => Set(PropFields.Items, items);

    private Task AddChestItem()
    {
        var items = (JsonArray)ChestItems().DeepClone();
        items.Add(new JsonObject { [ChestItemFields.Item] = "minecraft:arrow", [ChestItemFields.Count] = 16 });
        return WriteChestItems(items);
    }

    private Task RemoveChestItem(int index)
    {
        var items = (JsonArray)ChestItems().DeepClone();
        if (index < items.Count) items.RemoveAt(index);
        return WriteChestItems(items);
    }

    private Task SetChestItem(int index, string field, JsonNode? value)
    {
        var items = (JsonArray)ChestItems().DeepClone();
        if (index < items.Count && items[index] is JsonObject item) item[field] = value;
        return WriteChestItems(items);
    }

    private JsonObject? Stack(int index) => index < ChestItems().Count ? ChestItems()[index] as JsonObject : null;

    private string StackItem(int index) => Stack(index)?[ChestItemFields.Item]?.GetValue<string>() ?? "";

    private double StackCount(int index) =>
        Stack(index)?[ChestItemFields.Count] is { } node && double.TryParse(node.ToString(), out var count) ? count : 1;

    /// <summary>A stack's enchantments as the one line the inspector edits them in: <c>power:1, infinity:1</c>.</summary>
    private string EnchantmentText(int index) =>
        Stack(index)?[ChestItemFields.Enchantments] is JsonArray list
            ? string.Join(", ", list.OfType<JsonObject>().Select(enchantment =>
                $"{enchantment["name"]?.GetValue<string>()}:{enchantment["level"]?.GetValue<int>() ?? 1}"))
            : "";

    /// <summary>That line read back as the list the document carries; a name without a level is level 1.</summary>
    private static JsonArray Enchantments(string? line)
    {
        var list = new JsonArray();
        foreach (var part in (line ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var (name, level) = part.Split(':', 2) is [var named, var stated] && int.TryParse(stated, out var parsed)
                ? (named.Trim(), parsed) : (part, 1);
            list.Add(new JsonObject { ["name"] = name, ["level"] = level });
        }
        return list;
    }

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
    private static double Share(double percent) => Math.Clamp(percent / 100, 0, 1);

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
        if (field == PropFields.Pave) strokeStyles = await Library.StrokeStylesAsync(Spec(field));
        if (field == PropFields.Rock) boulderForms = await Library.BoulderFormsAsync(Spec(field));
        await RefreshPreview();
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
            [PropKinds.Stroke] = ("spline", "Stroke", "A band of surface along a line you draw, such as a road, a trail, or a forest floor. It replaces the ground it crosses. Make it a path to keep trees, boulders, and buildings off it."),
            [PropKinds.Fluid] = ("waves", "Fluid", "A channel or pool of water or lava. It cuts a bed into existing ground and fills it to a level line. It is mirrored across the map."),
            [PropKinds.Flora] = ("flower", "Ground cover", "Grass, ferns, and flowers on the soil inside the area you draw. Nothing grows on paved ground."),
            [PropKinds.Tree] = ("trees", "Tree", "A tree from the library, planted where you click."),
            [PropKinds.Boulder] = ("mountain", "Boulder", "A boulder from the library, set into the ground where you click."),
            [PropKinds.House] = ("home", "Building", "A building on the rectangle you drag, using a room style from the library. It settles into the ground and is mirrored so both teams get the same cover."),
            [PropKinds.Chest] = ("box", "Chest", "One chest with the items you list, on the ground or at a set height. Mirrored so both teams get the same loot."),
        };

    private (string Icon, string Title, string Blurb) Info
        => KindInfo.TryGetValue(kind, out var info) ? info : ("shapes", "Decoration", "");
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

    /// <summary>How far a path strays either side of the line through its points, in blocks. Nought runs as
    /// drawn.</summary>
    public const string Wander = "wander";
    /// <summary>How many blocks of path one bend of a wandering path takes.</summary>
    public const string WanderLength = "wanderLength";

    /// <summary>Whether a stroke is a way through rather than paint. It is what a tree's and a boulder's
    /// standoff is measured to, and the style says nothing about it.</summary>
    public const string ClaimsGround = "claimsGround";
    /// <summary>What a path is paved with — a full terrain material, not a block list.</summary>
    public const string Pave = "pave";
    public const string Depth = "depth";
    public const string Edge = "edge";
    public const string Shore = "shore";
    public const string ShoreWander = "shoreWander";
    /// <summary>A chest's front, the course it stands at, and its stacks.</summary>
    public const string Facing = "facing";
    public const string Y = "y";
    public const string Items = "items";
    /// <summary>What a fluid prop's bed is filled with, and its two words.</summary>
    public const string Fluid = "fluid";
    public const string WaterFluid = "water";
    public const string LavaFluid = "lava";
    public const string Bank = "bank";
    public const string Species = "species";
    public const string Height = "height";
    /// <summary>Which shape a prop takes — a boulder's rock family, a tree's vanilla-or-copied. One wire name
    /// because the two never share an object; the distinction lives in their C# types.</summary>
    public const string Form = "form";
    public const string Size = "size";
    /// <summary>What a boulder is cut from — a full terrain material, resolved in the rock's own frame.</summary>
    public const string Rock = "rock";
    public const string Mossy = "mossy";
    public const string Seed = "seed";
    /// <summary>The storey a prop rests on, by layer id — absent on a flat board, which resolves the top
    /// surface.</summary>
    public const string Layer = "layer";

    /// <summary>The values a fresh prop starts at, for the reads that need a fallback.</summary>
    public const string SolidStyle = "solid";
    public const string RoundForm = "round";
    public const string OakSpecies = "oak";
    public const string TemplateForm = "template";
    public const string WornStyle = "worn";
    public const string CanalForm = "canal";
}

/// <summary>A chest stack's fields.</summary>
public static class ChestItemFields
{
    public const string Item = "item";
    public const string Count = "count";
    public const string Enchantments = "enchantments";
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
    public const string DeadBushShare = "deadBushShare";
    public const string CactusShare = "cactusShare";
    public const string LilyShare = "lilyShare";
    public const string MushroomShare = "mushroomShare";
    public const string CropShare = "cropShare";
    public const string Crops = "crops";
    public const string Ripeness = "ripeness";
}

/// <summary>The dressing toolbar's tools, named once. The canvas routes on these strings, so the button, the
/// inspector and the controller all have to agree on them.</summary>
public static class DressingTools
{
    public const string Stroke = "dress:stroke";
    public const string Fluid = "dress:fluid";
    public const string Flora = "dress:flora";
    public const string House = "dress:house";
    public const string Tree = "dress:tree";
    public const string Boulder = "dress:boulder";
    public const string Chest = "dress:chest";

    /// <summary>Tool id, the prop kind it places, its glyph, and what it is called. The name names the tool
    /// and does not explain it — a dock tooltip is a label, not a manual.</summary>
    public static readonly (string Tool, string Kind, string Icon, string Name)[] All =
    [
        (Stroke, PropKinds.Stroke, "spline", "Stroke"),
        (Fluid, PropKinds.Fluid, "waves", "Fluid"),
        (Flora, PropKinds.Flora, "flower", "Ground cover"),
        (House, PropKinds.House, "home", "Building"),
        (Tree, PropKinds.Tree, "trees", "Tree"),
        (Boulder, PropKinds.Boulder, "mountain", "Boulder"),
        (Chest, PropKinds.Chest, "box", "Chest"),
    ];

    /// <summary>The kind of prop a tool places, or null when the tool places none.</summary>
    public static string? KindOf(string? tool) => All.FirstOrDefault(entry => entry.Tool == tool).Kind;
}
