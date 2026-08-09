using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Features.Configure;

using C = CoreAuthoring;
using Ctx = AuthoringContext;

// Cores · casing — what the structure is, and the one thing about it this tool may change.
//
// Configure writes map.xml and never touches the world. That is what decides which of a core's fields are
// editable here. Footprint, height, wall thickness, capped-or-flush lava and float describe blocks: on an
// imported map they are measurements of obsidian that already exists, and editing them would recompute a
// region that no longer scopes it — the exact failure OB8 exists to prevent, since the goal is built from the
// blocks matching the material INSIDE the region. On a plan-built map they are what the world-export stamper
// is about to place, which is the plan's statement to make, not this tool's. So they are read out, not set;
// the plan tool's marker panel is where a casing is shaped.
//
// Leak is the exception, and it is the exception for a reason: it is a rule, not a structure. Nothing in the
// world says how far the lava must fall before the core counts as breached — it is an attribute on the
// <core> element and nowhere else, so it belongs to the tool that authors the element. Paired with the
// measured float it gives the dig: leak greater than float means breaching the casing is not enough and
// players must cut the ground out from under it.
public partial class CoreCasingStep
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly List<Ctx.Team> teams = new();
    private List<C.Core> cores = new();
    private int selected = -1;
    private WorldCanvas? canvas;

    private string Slug => Wizard.Slug;
    private C.Core? Selected => selected >= 0 && selected < cores.Count ? cores[selected] : null;
    private Ctx.Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string TeamName(string id) => TeamOf(id)?.Name ?? id;
    private string TeamHex(string id) => GameColors.DyeHex(TeamOf(id)?.Color ?? "");

    protected override void OnInitialized()
    {
        teams.AddRange(Ctx.LoadTeams(Wizard.Intent));
        cores = C.ParseCores(Wizard.Intent);
        if (cores.Count > 0) selected = 0;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    private Task OnCanvasReady() => Paint();

    private void OnCanvasSelect(string? id)
    {
        const string prefix = "core-";
        if (id is { } s && s.StartsWith(prefix) && int.TryParse(s[prefix.Length..], out var i) && i < cores.Count)
            selected = i;
    }

    private void Select(int i) => selected = i;

    /// <summary>How far the lava must fall to count as leaked — the one field here that is a rule rather than
    /// a structure, and so the one this step may set.</summary>
    private void SetLeak(C.Core core, int value)
    {
        core.Leak = Math.Clamp(value, 0, 64);
        C.WriteCores(Wizard.Intent, cores);
        Wizard.MarkDirty();
    }

    private async Task Paint()
    {
        if (canvas is null) return;
        var shapes = new List<object>();
        for (var i = 0; i < cores.Count; i++)
        {
            if (cores[i].Volume is not { } volume) continue;
            shapes.Add(new
            {
                id = $"core-{i}",
                type = "cuboid", primary = i == selected, color = TeamHex(cores[i].Owner),
                label = $"core · {(string.IsNullOrEmpty(cores[i].Owner) ? "no team" : TeamName(cores[i].Owner))}",
                bounds = new { min_x = (double)volume.MinX, min_z = (double)volume.MinZ, max_x = volume.MaxX + 1.0, max_z = volume.MaxZ + 1.0 },
            });
        }
        await canvas.SetAuthorRegionsAsync(shapes);
    }
}
