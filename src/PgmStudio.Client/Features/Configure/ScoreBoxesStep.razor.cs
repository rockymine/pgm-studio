using System.Net.Http;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Client.Models;
using PgmStudio.Geom;

namespace PgmStudio.Client.Features.Configure;

using Ctx = AuthoringContext;

// Score · boxes: the intent's killPoints and scoreBoxes. A box is drawn as a rectangle and stands on the floor under
// its middle nearest the height the spawns stand at — the board's playing level, not a roof over it — BoxHeight
// blocks tall, so a player walks into it; its bottom is the inspector's to move; its team, its points and whether it sends
// the scorer home are the inspector's. The generator fans each box onto every other team's image under the
// map's symmetry, and the step lists and draws those copies in their own team's colour, read-only, as
// SymmetryExpander.FillScoreBoxes makes them. Only the drawn boxes are stored.
public partial class ScoreBoxesStep
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>How many blocks tall a drawn box is, from the first air over the ground: a player stands in it
    /// whether they walk or jump in (corpus boxes are mostly three and four tall).</summary>
    private const int BoxHeight = 3;

    private sealed class Box
    {
        public int Id;
        public string Team = "";
        public int MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
        public int Points = 1;
        public bool Home = true;
        public Box? Source;   // the drawn box a symmetry copy is made from; null for a drawn box

        public bool Authored => Source is null;
    }

    private readonly List<Box> boxes = new();
    private List<Box> copies = new();
    private List<Ctx.Team> teams = new();
    private int? killPoints;
    private int? playLevel;
    private string? symMode; private double symCx, symCz;
    private int nextId = 1;
    private int? selectedId;
    private WorldCanvas? canvas;


    private string Slug => Wizard.Slug;
    private IEnumerable<Box> Rows => boxes.Concat(copies);
    private int RowCount => boxes.Count + copies.Count;
    private Box? Selected => Rows.FirstOrDefault(box => box.Id == selectedId);
    private string TeamName(string id) => teams.FirstOrDefault(team => team.Id == id)?.Name is { Length: > 0 } name ? name : id;
    private string Hex(string id) => GameColors.ChatHex(teams.FirstOrDefault(team => team.Id == id)?.Color ?? "");

    protected override void OnInitialized()
    {
        var intent = Wizard.Intent;
        teams = Ctx.LoadTeams(intent);
        (symMode, symCx, symCz) = Ctx.Sym(intent);
        killPoints = intent["killPoints"] is null ? null : Ctx.I(intent, "killPoints");
        var spawnHeights = (intent["spawns"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(spawn => Ctx.D(spawn["point"] as JsonObject, "y")).ToList();
        playLevel = spawnHeights.Count > 0 ? (int)Math.Round(spawnHeights.Average()) : null;
        foreach (var o in (intent["scoreBoxes"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var b = o["box"] as JsonObject;
            boxes.Add(new Box
            {
                Id = nextId++, Team = Ctx.S(o, "team"),
                MinX = Ctx.I(b, "minX"), MinY = Ctx.I(b, "minY"), MinZ = Ctx.I(b, "minZ"),
                MaxX = Ctx.I(b, "maxX"), MaxY = Ctx.I(b, "maxY"), MaxZ = Ctx.I(b, "maxZ"),
                Points = Ctx.I(o, "points", 1),
                Home = o["home"]?.GetValue<bool>() ?? true,
            });
        }
        if (boxes.Count > 0) selectedId = boxes[0].Id;
        Fan();
    }

    /// <summary>Every other team's image of each drawn box, as the generator fans it: a team holding a box of
    /// its own keeps that one.</summary>
    private void Fan()
    {
        copies = new();
        var order = Symmetry.Order(symMode);
        if (order <= 1 || teams.Count <= 1) return;
        var have = new HashSet<string>(boxes.Select(box => box.Team));
        foreach (var source in boxes)
        {
            var index = teams.FindIndex(team => team.Id == source.Team);
            if (index < 0) continue;
            for (var k = 1; k < order; k++)
            {
                var team = teams[(index + k) % teams.Count].Id;
                if (!have.Add(team)) continue;
                var (minX, minZ, maxX, maxZ) =
                    Symmetry.CellRect(source.MinX, source.MinZ, source.MaxX, source.MaxZ, symMode, symCx, symCz, k);
                copies.Add(new Box
                {
                    Id = -(copies.Count + 1), Team = team, Source = source,
                    MinX = minX, MinY = source.MinY, MinZ = minZ,
                    MaxX = maxX, MaxY = source.MaxY, MaxZ = maxZ,
                    Points = source.Points, Home = source.Home,
                });
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private Task OnCanvasReady() => Paint();

    // A rectangle's edges are block edges, so the box holds the blocks from its min up to just short of its max.
    private async Task OnRectDrawn((double MinX, double MinZ, double MaxX, double MaxZ) r)
    {
        var (minX, minZ) = ((int)Math.Floor(r.MinX), (int)Math.Floor(r.MinZ));
        var (maxX, maxZ) = (Math.Max(minX, (int)Math.Ceiling(r.MaxX) - 1), Math.Max(minZ, (int)Math.Ceiling(r.MaxZ) - 1));
        if (await ColumnFloor.RestingYAsync(Http, Slug, (minX + maxX + 1) / 2.0, (minZ + maxZ + 1) / 2.0, playLevel) is not { } y) return;
        var box = new Box
        {
            Id = nextId++, Team = teams.FirstOrDefault()?.Id ?? "",
            MinX = minX, MinY = y, MinZ = minZ, MaxX = maxX, MaxY = y + BoxHeight - 1, MaxZ = maxZ,
        };
        boxes.Add(box);
        selectedId = box.Id;
        Write();
        await Paint();
    }

    private async Task OnGeometrySaved((string Id, double MinX, double MinZ, double MaxX, double MaxZ) e)
    {
        if (BoxFromRegion(e.Id) is not { Authored: true } box) return;
        box.MinX = (int)Math.Floor(e.MinX); box.MinZ = (int)Math.Floor(e.MinZ);
        box.MaxX = Math.Max(box.MinX, (int)Math.Ceiling(e.MaxX) - 1); box.MaxZ = Math.Max(box.MinZ, (int)Math.Ceiling(e.MaxZ) - 1);
        Write();
        await Paint();
    }

    private async Task OnCanvasSelect(string? id)
    {
        selectedId = BoxFromRegion(id) is { Authored: true } box ? box.Id : null;
        if (canvas is not null) await canvas.SetSelectionAsync(selectedId is { } sel ? [RegionId(sel)] : Array.Empty<string>());
        StateHasChanged();
    }

    private async Task SelectBox(int id)
    {
        selectedId = id;
        if (canvas is not null) await canvas.SetSelectionAsync([RegionId(id)]);
    }

    private void SetKillPoints(int? points) { killPoints = points; Write(); }
    private void SetTeam(Box box, string team) { box.Team = team; Write(); _ = Paint(); }
    private void SetPoints(Box box, int points) { box.Points = points; Write(); }
    private void SetHome(Box box, bool home) { box.Home = home; Write(); }

    private void SetBottom(Box box, int bottom)
    {
        box.MaxY = bottom + (box.MaxY - box.MinY);
        box.MinY = bottom;
        Write();
    }

    private async Task Remove(Box box)
    {
        boxes.Remove(box);
        selectedId = boxes.FirstOrDefault()?.Id;
        Write();
        await Paint();
    }

    private void Write()
    {
        Fan();
        if (selectedId is { } id && Rows.All(box => box.Id != id)) selectedId = null;
        var intent = Wizard.Intent;
        if (killPoints is > 0) intent["killPoints"] = killPoints; else intent.Remove("killPoints");
        intent["scoreBoxes"] = new JsonArray(boxes.Select(box => (JsonNode)new JsonObject
        {
            ["team"] = box.Team,
            ["box"] = new JsonObject
            {
                ["minX"] = box.MinX, ["minY"] = box.MinY, ["minZ"] = box.MinZ,
                ["maxX"] = box.MaxX, ["maxY"] = box.MaxY, ["maxZ"] = box.MaxZ,
            },
            ["points"] = box.Points,
            ["home"] = box.Home,
        }).ToArray());
        Wizard.MarkDirty();
    }

    private async Task Paint()
    {
        if (canvas is null) return;
        await canvas.SetAuthorRegionsAsync(Rows.Select(box => (object)new
        {
            id = RegionId(box.Id),
            type = "rectangle",
            ghost = !box.Authored,
            label = $"{TeamName(box.Team)}'s score box",
            color = Hex(box.Team),
            bounds = new { min_x = (double)box.MinX, min_z = (double)box.MinZ, max_x = box.MaxX + 1.0, max_z = box.MaxZ + 1.0 },
        }));
    }

    private static string RegionId(int id) => id > 0 ? $"scorebox-{id}" : $"scorebox-copy-{-id}";
    private Box? BoxFromRegion(string? regionId) => Rows.FirstOrDefault(box => RegionId(box.Id) == regionId);
}
