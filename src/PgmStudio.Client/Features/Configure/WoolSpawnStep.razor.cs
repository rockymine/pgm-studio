using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Configure;

using W = WoolAuthoring;
using Ctx = AuthoringContext;

// Wools · spawn step: confirm/adjust where each wool dispenses (the <wool location> + spawner spawn point),
// seeded by the objectives step's detected source centroid. The point tool moves the selected wool's
// source; editing a wool owned by the anchor team re-derives its symmetric partners (position orbits, like
// the team-spawn step), while editing an orbit copy nudges it alone. The side-view sets the Y.
//
// A scanned world can already hold the wool in a chest in its room. Where a chest of the wool's colour stands
// there the step offers it as the supply, and takes it by itself for a wool no step has stated a supply for: the
// wool's location is then the chest, and no spawner is written (PGM refills a wool chest on its own).
public partial class WoolSpawnStep
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly List<Ctx.Team> teams = new();
    private List<W.Wool> wools = new();
    private string? symMode; private double symCx, symCz;
    private string anchorTeam = "";
    private string? selectedColor;
    private WorldCanvas? canvas;

    /// <summary>Per wool colour, the chest the wool can be taken from (<see cref="ChestForAsync"/>); absent where
    /// there is none, or the world has no scan.</summary>
    private readonly Dictionary<string, WoolSourceDto> chests = new();

    private static readonly IReadOnlyList<SelectOption> SupplyOptions =
    [
        new(WoolSupplies.Spawner, "Spawner", "Drops the wool while an attacker is in the room."),
        new(WoolSupplies.Chest, "Chest", "The chest in the room, which PGM refills."),
    ];

    private string Slug => Wizard.Slug;
    private W.Wool? Selected => wools.FirstOrDefault(w => w.Color == selectedColor);
    private Ctx.Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string TeamName(string id) => TeamOf(id)?.Name ?? id;
    private string TeamColorOf(string id) => TeamOf(id)?.Color ?? "";
    private bool IsAuthored(W.Wool w) => w.Owner == anchorTeam;

    protected override async Task OnInitializedAsync()
    {
        teams.AddRange(Ctx.LoadTeams(Wizard.Intent));
        (symMode, symCx, symCz) = Ctx.Sym(Wizard.Intent);
        anchorTeam = teams.FirstOrDefault()?.Id ?? "";
        wools = W.ParseWools(Wizard.Intent);
        selectedColor = wools.FirstOrDefault()?.Color;

        foreach (var w in wools)
            if (await ChestForAsync(w) is { } chest) chests[W.NormColor(w.Color)] = chest;
        var stated = false;
        foreach (var w in wools.Where(w => w.Supply is null && ChestOf(w) is not null))
        {
            UseChest(w, ChestOf(w)!);
            stated = true;
        }
        if (stated) Write();
    }

    /// <summary>How far from a roomless wool's spawn point a chest of its colour is looked for, in blocks.</summary>
    private const int ChestReach = 16;

    /// <summary>The chest holding <paramref name="w"/>'s colour, read off the scan: inside its room once the room is
    /// drawn, holding the most of it; before that the nearest within <see cref="ChestReach"/> of its spawn point,
    /// which the objectives step seeds on the wool found in the world.</summary>
    private async Task<WoolSourceDto?> ChestForAsync(W.Wool w)
    {
        var bounds = w.HasRoom
            ? new JsonObject
            {
                ["min_x"] = w.Rooms.Min(r => r.MinX), ["min_z"] = w.Rooms.Min(r => r.MinZ),
                ["max_x"] = w.Rooms.Max(r => r.MaxX), ["max_z"] = w.Rooms.Max(r => r.MaxZ),
            }
            : new JsonObject
            {
                ["min_x"] = w.SpawnX - ChestReach, ["min_z"] = w.SpawnZ - ChestReach,
                ["max_x"] = w.SpawnX + ChestReach, ["max_z"] = w.SpawnZ + ChestReach,
            };
        try
        {
            using var body = new StringContent(new JsonObject { ["bounds"] = bounds }.ToJsonString(), Encoding.UTF8, "application/json");
            using var resp = await Http.PostAsync($"api/map/{Slug}/wool-sources", body);
            if (!resp.IsSuccessStatusCode) return null;
            var found = await resp.Content.ReadFromJsonAsync<WoolSourcesResponseDto>();
            // The scan reports a chest slot by slot, so one chest is the sum of the slots at its block.
            var held = (found?.Colors ?? [])
                .Where(c => W.NormColor(c.Color) == W.NormColor(w.Color))
                .SelectMany(c => c.Sources)
                .Where(source => source.Type == WoolSupplies.Chest)
                .GroupBy(source => (source.X, source.Y, source.Z))
                .Select(slots => slots.First() with { Count = slots.Sum(slot => slot.Count) });
            return w.HasRoom
                ? held.Where(source => w.Rooms.Any(r => r.MinX <= source.X + 0.5 && source.X + 0.5 <= r.MaxX
                                                     && r.MinZ <= source.Z + 0.5 && source.Z + 0.5 <= r.MaxZ))
                      .OrderByDescending(source => source.Count).FirstOrDefault()
                : held.OrderBy(source => Math.Pow(source.X + 0.5 - w.SpawnX, 2) + Math.Pow(source.Z + 0.5 - w.SpawnZ, 2))
                      .FirstOrDefault();
        }
        catch (HttpRequestException) { return null; }
    }

    private WoolSourceDto? ChestOf(W.Wool w) => chests.GetValueOrDefault(W.NormColor(w.Color));

    private bool FromChest(W.Wool w) => w.Supply == WoolSupplies.Chest;

    /// <summary>The wool is taken from <paramref name="chest"/>: its location is the chest's block.</summary>
    private static void UseChest(W.Wool w, WoolSourceDto chest)
    {
        w.Supply = WoolSupplies.Chest;
        (w.SpawnX, w.SpawnY, w.SpawnZ) = (chest.X + 0.5, chest.Y, chest.Z + 0.5);
    }

    /// <summary>Switch a wool's supply. An authored wool takes its copies with it, each to the chest in its own
    /// room where it has one.</summary>
    private async Task SetSupply(W.Wool w, string supply)
    {
        foreach (var target in IsAuthored(w) ? [w, .. Partners(w)] : new[] { w })
        {
            if (supply == WoolSupplies.Chest && ChestOf(target) is { } chest) UseChest(target, chest);
            else if (supply == WoolSupplies.Spawner) target.Supply = WoolSupplies.Spawner;
        }
        Write();
        await Paint();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    private Task OnCanvasReady() => Paint();

    private async Task OnPointPick((double X, double Z) p)
    {
        if (Selected is not { } w || FromChest(w)) return;
        // The wool's own level anchors the search: it usually sits in a covered room, whose roof is the
        // column's topmost surface. Read before X/Z move, and re-seat before the partners are re-derived,
        // since they copy the authored wool's Y.
        var refY = (int)Math.Floor(w.SpawnY);
        w.SpawnX = Ctx.Snap(p.X); w.SpawnZ = Ctx.Snap(p.Z);
        if (await ColumnFloor.RestingYAsync(Http, Slug, w.SpawnX, w.SpawnZ, refY) is { } y) w.SpawnY = y;
        if (IsAuthored(w)) ReDerivePartners(w);
        Write();
        await Paint();
    }

    private void OnCanvasSelect(string? id)
    {
        const string pfx = "wool-spawn-";
        if (id is { } s && s.StartsWith(pfx) && wools.Any(w => w.Color == s[pfx.Length..])) selectedColor = s[pfx.Length..];
    }

    private void SelectWool(string color) => selectedColor = color;

    /// <summary>Write one coordinate of a wool's source point. A move across the ground <b>re-seats</b> it
    /// on the column it lands in, the same rule the point tool places one by — the wool's own level anchors
    /// the search, since it usually sits in a covered room whose roof would otherwise be the column's
    /// topmost surface. A Y typed here is the author's and stands.</summary>
    private async Task SetCoord(W.Wool w, string axis, ChangeEventArgs e)
    {
        if (!double.TryParse(e.Value?.ToString(), out var v)) return;
        // Read before the X/Z move, and re-seat before the partners are re-derived, since they copy the
        // authored wool's Y.
        var refY = (int)Math.Floor(w.SpawnY);
        switch (axis)
        {
            case "x": w.SpawnX = v; break;
            case "z": w.SpawnZ = v; break;
            case "y": w.SpawnY = v; break;
        }
        if (axis is "x" or "z" && await ColumnFloor.RestingYAsync(Http, Slug, w.SpawnX, w.SpawnZ, refY) is { } seated)
            w.SpawnY = seated;
        if (IsAuthored(w))
        {
            if (axis is "x" or "z") ReDerivePartners(w);
            if (axis == "y") foreach (var p in Partners(w)) p.SpawnY = v;
        }
        Write();
        await Paint();
    }

    private void SetY(W.Wool w, int y)
    {
        w.SpawnY = y;
        if (IsAuthored(w)) foreach (var p in Partners(w)) p.SpawnY = y;
        Write();
    }

    // The orbit partners of an authored wool: for each symmetry step the wool of another owner nearest the
    // mirrored point. Position-only — colour/owner are untouched (green's mirror stays yellow, never "blue").
    private IEnumerable<W.Wool> Partners(W.Wool authored)
    {
        var order = Ctx.OrbitOrder(symMode);
        for (var k = 1; k < order; k++)
        {
            var (mx, mz) = Ctx.Orbit(authored.SpawnX, authored.SpawnZ, symMode, symCx, symCz, k);
            var partner = wools.Where(o => o != authored && o.Owner != anchorTeam)
                .OrderBy(o => (o.SpawnX - mx) * (o.SpawnX - mx) + (o.SpawnZ - mz) * (o.SpawnZ - mz))
                .FirstOrDefault();
            if (partner is not null) yield return partner;
        }
    }

    private void ReDerivePartners(W.Wool authored)
    {
        var order = Ctx.OrbitOrder(symMode);
        for (var k = 1; k < order; k++)
        {
            var (mx, mz) = Ctx.Orbit(authored.SpawnX, authored.SpawnZ, symMode, symCx, symCz, k);
            var partner = wools.Where(o => o != authored && o.Owner != anchorTeam)
                .OrderBy(o => (o.SpawnX - mx) * (o.SpawnX - mx) + (o.SpawnZ - mz) * (o.SpawnZ - mz))
                .FirstOrDefault();
            if (partner is not null) { partner.SpawnX = mx; partner.SpawnZ = mz; partner.SpawnY = authored.SpawnY; }
        }
    }

    private RegionNode SpawnNode(W.Wool w) => new()
    {
        Id = $"{w.Color}-wool-spawn@{w.SpawnX},{w.SpawnZ}", Type = "point",
        Coords = new() { ["x"] = w.SpawnX, ["y"] = w.SpawnY, ["z"] = w.SpawnZ },
    };

    private void Write() { W.WriteWools(Wizard.Intent, wools); Wizard.MarkDirty(); }

    private async Task Paint()
    {
        if (canvas is null) return;
        await canvas.SetAuthorRegionsAsync(wools.Select(w => (object)new
        {
            id = $"wool-spawn-{w.Color}",
            type = "point",
            marker = true,
            primary = w.Color == selectedColor,
            color = W.Hex(w.Color),
            label = FromChest(w) ? $"{W.Name(w.Color)} wool chest" : $"{W.Name(w.Color)} wool",
            bounds = new { min_x = w.SpawnX - 0.5, min_z = w.SpawnZ - 0.5, max_x = w.SpawnX + 0.5, max_z = w.SpawnZ + 0.5 },
        }));
    }
}
