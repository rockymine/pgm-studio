using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Pages.EditorActivities;
using PgmStudio.Geom;

namespace PgmStudio.Client.Pages.Configure;

// Build · buildable-layer step: draw over-void bridges (areas) and no-build holes with the rectangle tool;
// each is a dummy region on the reused canvas (selectable + resizable). Writes the build slice's areas/holes;
// BuildGenerator unions the areas, subtracts the holes (complement), and wires the void-enforcement negative.
public partial class BuildLayerPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private const string AreaColor = "#22c55e";   // bridge — green
    private const string HoleColor = "#ef4444";   // hole — red

    private sealed class Box { public int Id; public bool Hole; public double MinX, MinZ, MaxX, MaxZ; }

    private readonly List<Box> boxes = new();
    private int nextId = 1;
    private string drawMode = "area";   // area | hole
    private int? selectedId;
    private bool overlayOn;   // the Buildable heatmap toggle — gates the legend (shown only when on)
    private EditorCanvas? canvas;
    private string? symMode; private double symCx, symCz;

    private string Slug => Wizard.Slug;
    private Box? Selected => selectedId is { } id ? boxes.FirstOrDefault(b => b.Id == id) : null;
    private string Label(Box b) => $"{(b.Hole ? "hole" : "bridge")}-{boxes.Where(x => x.Hole == b.Hole).ToList().IndexOf(b) + 1}";

    protected override void OnInitialized() => LoadFromIntent();

    // The canvas raises this when the Buildable chip flips; the legend is shown only while the overlay is on.
    private void OnBuildableToggled(bool on) => overlayOn = on;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private void LoadFromIntent()
    {
        boxes.Clear(); nextId = 1;
        if (Wizard.Intent["build"] is JsonObject b)
        {
            AddBoxes(b["areas"] as JsonArray, hole: false);
            AddBoxes(b["holes"] as JsonArray, hole: true);
        }
        symMode = null;
        if (Wizard.Intent["symmetry"] is JsonObject sym)
        {
            symMode = sym["mode"]?.GetValue<string>();
            symCx = D(sym, "centerX"); symCz = D(sym, "centerZ");
        }
        selectedId = null;
    }

    private bool MirrorActive => SymmetryOrder > 1;
    private int SymmetryOrder => Symmetry.Order(symMode);

    private void AddBoxes(JsonArray? arr, bool hole)
    {
        if (arr is null) return;
        foreach (var r in arr.OfType<JsonObject>())
            boxes.Add(new Box { Id = nextId++, Hole = hole, MinX = D(r, "minX"), MinZ = D(r, "minZ"), MaxX = D(r, "maxX"), MaxZ = D(r, "maxZ") });
    }

    private async Task OnCanvasReady()
    {
        await Paint();
        // The canvas mirrors the authored rects into ghost previews itself (shared geometry/symmetry.js),
        // and re-derives them on every subsequent SetAuthorRegions — so this is set once.
        if (canvas is not null) await canvas.SetAuthorMirrorAsync(symMode, symCx, symCz);
    }

    private async Task OnRectDrawn((double MinX, double MinZ, double MaxX, double MaxZ) r)
    {
        var b = new Box
        {
            Id = nextId++, Hole = drawMode == "hole",
            MinX = Math.Round(r.MinX), MinZ = Math.Round(r.MinZ), MaxX = Math.Round(r.MaxX), MaxZ = Math.Round(r.MaxZ),
        };
        boxes.Add(b);
        selectedId = b.Id;
        WriteLayer();
        await Paint();
        await SelectOnCanvas(b.Id);
    }

    private async Task OnGeometrySaved((string Id, double MinX, double MinZ, double MaxX, double MaxZ) e)
    {
        if (BoxFromRegion(e.Id) is not { } b) return;
        b.MinX = e.MinX; b.MinZ = e.MinZ; b.MaxX = e.MaxX; b.MaxZ = e.MaxZ;
        WriteLayer();
        await Paint();
        await SelectOnCanvas(b.Id);
    }

    private async Task OnCanvasSelect(string? id)
    {
        var b = BoxFromRegion(id);
        selectedId = b?.Id;
        if (b is not null) await SelectOnCanvas(b.Id);
        else { if (canvas is not null) await canvas.SetSelectionAsync(Array.Empty<string>()); StateHasChanged(); }
    }

    private async Task SelectBox(int id) { selectedId = id; await SelectOnCanvas(id); }

    private async Task SelectOnCanvas(int id)
    {
        if (canvas is not null) await canvas.SetSelectionAsync(new[] { RegionId(id) });
        StateHasChanged();
    }

    private async Task Remove(int id)
    {
        boxes.RemoveAll(b => b.Id == id);
        if (selectedId == id) selectedId = null;
        WriteLayer();
        await Paint();
    }

    // Repaint the authored bridges/holes (editable, bridges green / holes red). The canvas adds the
    // symmetry-orbited ghost copies itself from these (setAuthorMirror), so they're not computed here.
    private async Task Paint()
    {
        if (canvas is null) return;
        await canvas.SetAuthorRegionsAsync(boxes.Select(b => (object)new
        {
            id = RegionId(b.Id),
            type = "rectangle",
            label = b.Hole ? "No-build hole" : "Buildable bridge",
            color = b.Hole ? HoleColor : AreaColor,
            bounds = new { min_x = b.MinX, min_z = b.MinZ, max_x = b.MaxX, max_z = b.MaxZ },
        }));
    }

    // Persist the areas/holes into the build slice without disturbing the height (maxHeight).
    private void WriteLayer()
    {
        var b = Wizard.Intent["build"] as JsonObject;
        if (b is null) { b = new JsonObject(); Wizard.Intent["build"] = b; }
        b["areas"] = new JsonArray(boxes.Where(x => !x.Hole).Select(RectNode).ToArray());
        b["holes"] = new JsonArray(boxes.Where(x => x.Hole).Select(RectNode).ToArray());
        Wizard.MarkDirty();
    }

    private static JsonNode RectNode(Box b) =>
        new JsonObject { ["minX"] = b.MinX, ["minZ"] = b.MinZ, ["maxX"] = b.MaxX, ["maxZ"] = b.MaxZ };

    private static string RegionId(int id) => $"bld-{id}";
    private Box? BoxFromRegion(string? regionId)
        => regionId is { } s && s.StartsWith("bld-") && int.TryParse(s[4..], out var id)
            ? boxes.FirstOrDefault(b => b.Id == id) : null;

    private static double D(JsonObject? o, string k)
    {
        if (o?[k] is JsonValue v) { if (v.TryGetValue(out double d)) return d; if (v.TryGetValue(out int i)) return i; }
        return 0;
    }
}
