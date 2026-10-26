using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Geom;

namespace PgmStudio.Client.Features.Configure;

// Build · buildable-layer step: draw over-void bridges (areas) and no-build holes with the rectangle tool;
// each is a dummy region on the reused canvas (selectable + resizable). Writes the build slice's areas/holes;
// BuildGenerator unions the areas, subtracts the holes (complement), and wires the not-build-area negative.
// The generator orbits each rectangle across the map's symmetry; the step lists and draws those copies
// read-only, as SymmetryExpander.OrbitRects makes them, and stores only the drawn ones.
public partial class BuildLayerStep
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private const string AreaColor = "#22c55e";   // bridge — green
    private const string HoleColor = "#ef4444";   // hole — red

    private sealed class Box
    {
        public int Id; public bool Hole; public double MinX, MinZ, MaxX, MaxZ;
        public string Piece = "";   // the stored area's id, kept so a save does not rename it
        public Box? Source;   // the drawn rectangle a symmetry copy is made from; null for a drawn one

        public bool Authored => Source is null;
    }

    private readonly List<Box> boxes = new();
    private List<Box> copies = new();
    private int nextId = 1;
    private string drawMode = "area";   // area | hole
    private int? selectedId;
    private bool overlayOn;   // the Buildable heatmap toggle — gates the legend (shown only when on)
    private WorldCanvas? canvas;
    private string? symMode; private double symCx, symCz;

    /// <summary>Whether the next rectangle carves rather than bridges. The mode is armed on the canvas dock,
    /// where the tool it decides for is.</summary>
    private bool HoleMode => drawMode == "hole";

    // The button already prints the mode it is in, so its tooltip has only the other half to give: what the
    // click does.
    private string DrawModeTitle => HoleMode ? "Switch to bridge" : "Switch to hole";

    private void ToggleDrawMode() => drawMode = HoleMode ? "area" : "hole";

    private string Slug => Wizard.Slug;
    private IEnumerable<Box> Rows => boxes.Concat(copies);
    private Box? Selected => selectedId is { } id ? Rows.FirstOrDefault(b => b.Id == id) : null;
    private string Label(Box b)
    {
        var drawn = b.Source ?? b;
        return $"{(drawn.Hole ? "Hole" : "Bridge")} {boxes.Where(x => x.Hole == drawn.Hole).ToList().IndexOf(drawn) + 1}";
    }

    protected override void OnInitialized() => LoadFromIntent();

    // The canvas raises this when the Buildable chip flips; the legend is shown only while the overlay is on.
    private void OnEditZonesToggled(bool on) => overlayOn = on;

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
        Fan();
    }

    /// <summary>Each drawn rectangle's images under the symmetry, snapped to whole blocks; an image that lands
    /// on a rectangle already there is one rectangle.</summary>
    private void Fan()
    {
        copies = new();
        var order = Symmetry.Order(symMode);
        var seen = new HashSet<(bool, double, double, double, double)>(boxes.Select(b => (b.Hole, b.MinX, b.MinZ, b.MaxX, b.MaxZ)));
        foreach (var source in boxes)
            for (var k = 1; k < order; k++)
            {
                var (minX, minZ, maxX, maxZ) = Symmetry.Rect(source.MinX, source.MinZ, source.MaxX, source.MaxZ, symMode, symCx, symCz, k);
                var image = (source.Hole, Math.Round(minX), Math.Round(minZ), Math.Round(maxX), Math.Round(maxZ));
                if (!seen.Add(image)) continue;
                copies.Add(new Box
                {
                    Id = -(copies.Count + 1), Hole = source.Hole, Source = source,
                    MinX = image.Item2, MinZ = image.Item3, MaxX = image.Item4, MaxZ = image.Item5,
                });
            }
    }

    private void AddBoxes(JsonArray? arr, bool hole)
    {
        if (arr is null) return;
        foreach (var r in arr.OfType<JsonObject>())
            boxes.Add(new Box { Id = nextId++, Hole = hole, Piece = r["id"]?.GetValue<string>() ?? "", MinX = D(r, "minX"), MinZ = D(r, "minZ"), MaxX = D(r, "maxX"), MaxZ = D(r, "maxZ") });
    }

    private Task OnCanvasReady() => Paint();

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
        if (BoxFromRegion(e.Id) is not { Authored: true } b) return;
        b.MinX = e.MinX; b.MinZ = e.MinZ; b.MaxX = e.MaxX; b.MaxZ = e.MaxZ;
        WriteLayer();
        await Paint();
        await SelectOnCanvas(b.Id);
    }

    private async Task OnCanvasSelect(string? id)
    {
        var b = BoxFromRegion(id) is { Authored: true } drawn ? drawn : null;
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

    // Repaint the bridges/holes (bridges green / holes red): the drawn ones editable, their copies ghosted.
    private async Task Paint()
    {
        if (canvas is null) return;
        await canvas.SetAuthorRegionsAsync(Rows.Select(b => (object)new
        {
            id = RegionId(b.Id),
            type = "rectangle",
            ghost = !b.Authored,
            label = b.Hole ? "No-build hole" : "Buildable bridge",
            color = b.Hole ? HoleColor : AreaColor,
            bounds = new { min_x = b.MinX, min_z = b.MinZ, max_x = b.MaxX, max_z = b.MaxZ },
        }));
    }

    // Persist the areas/holes into the build slice without disturbing the height (maxHeight).
    private void WriteLayer()
    {
        Fan();
        if (selectedId is { } id && Rows.All(box => box.Id != id)) selectedId = null;
        var b = Wizard.Intent["build"] as JsonObject;
        if (b is null) { b = new JsonObject(); Wizard.Intent["build"] = b; }
        b["areas"] = new JsonArray(boxes.Where(x => !x.Hole).Select(AreaNode).ToArray());
        b["holes"] = new JsonArray(boxes.Where(x => x.Hole).Select(RectNode).ToArray());
        Wizard.MarkDirty();
    }

    private static JsonNode AreaNode(Box b)
    {
        var node = (JsonObject)RectNode(b);
        if (b.Piece.Length > 0) node["id"] = b.Piece;
        return node;
    }

    private static JsonNode RectNode(Box b) =>
        new JsonObject { ["minX"] = b.MinX, ["minZ"] = b.MinZ, ["maxX"] = b.MaxX, ["maxZ"] = b.MaxZ };

    private static string RegionId(int id) => id > 0 ? $"bld-{id}" : $"bld-copy-{-id}";
    private Box? BoxFromRegion(string? regionId) => Rows.FirstOrDefault(b => RegionId(b.Id) == regionId);

    private static double D(JsonObject? o, string k)
    {
        if (o?[k] is JsonValue v) { if (v.TryGetValue(out double d)) return d; if (v.TryGetValue(out int i)) return i; }
        return 0;
    }
}
