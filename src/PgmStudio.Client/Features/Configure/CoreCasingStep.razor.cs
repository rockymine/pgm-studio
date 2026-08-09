using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Features.Configure;

using C = CoreAuthoring;
using Ctx = AuthoringContext;

// Cores · casing. What the Objectives step settles is which team defends a core and roughly where; this step
// settles what the structure is: the obsidian box's footprint and height, how thick its wall is, whether the
// lava is capped or flush with the rim, and the pair that decides how it is captured — float (air under the
// casing) and leak (how far the lava must fall to count). Their difference is the dig: leak greater than
// float means breaching the casing is not enough and players must cut the ground out from under it.
//
// Every edit re-derives the casing volume from the anchor, so the numbers and the box can never disagree —
// the region the generator emits is that box (OB8), and a box that drifted from the numbers would scope a
// goal the world does not contain. A plan-authored core keeps no volume here: the world-export path stamps
// it against terrain that does not exist until the build runs.
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

    // ── the casing numbers ─────────────────────────────────────────────────────────────
    private void SetSize(C.Core core, int value) => Apply(core, () => core.Size = Math.Clamp(value, 1, 64));
    private void SetHeight(C.Core core, int value) => Apply(core, () => core.Height = Math.Clamp(value, 1, 64));
    private void SetShell(C.Core core, int value) => Apply(core, () => core.Shell = Math.Clamp(value, 1, 16));
    private void SetFloat(C.Core core, int value) => Apply(core, () => core.Float = Math.Clamp(value, 0, 64));
    private void SetLeak(C.Core core, int value) => Apply(core, () => core.Leak = Math.Clamp(value, 0, 64));
    private void SetOpenTop(C.Core core, bool value) => Apply(core, () => core.OpenTop = value);
    private void SetBaseY(C.Core core, int value) => Apply(core, () => core.AnchorY = Math.Clamp(value, 0, 255) - core.Float);

    private void Apply(C.Core core, Action edit)
    {
        edit();
        Resolve(core);
        C.WriteCores(Wizard.Intent, cores);
        Wizard.MarkDirty();
        _ = Paint();
    }

    /// <summary>The casing base — <c>float</c> blocks of air above the ground the anchor rests on, which is
    /// the height the world-export stamper builds at. Null while the core has no resolved volume.</summary>
    private static int? BaseY(C.Core core) => core.Volume?.MinY;

    // Re-derive the volume from the anchor and the numbers. A core with no resolved volume stays that way:
    // its box belongs to the world build, and inventing one here would export a region over terrain nobody
    // has generated yet.
    private static void Resolve(C.Core core)
    {
        if (core.Volume is null) return;
        var minX = (int)Math.Floor(core.AnchorX) - (core.Size - 1) / 2;
        var minZ = (int)Math.Floor(core.AnchorZ) - (core.Size - 1) / 2;
        var minY = Math.Max(0, (int)Math.Round(core.AnchorY) + core.Float);
        core.Volume = new C.Box(minX, minY, minZ, minX + core.Size - 1, minY + core.Height - 1, minZ + core.Size - 1);
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
