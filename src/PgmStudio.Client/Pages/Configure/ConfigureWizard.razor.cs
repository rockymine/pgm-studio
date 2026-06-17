using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.Configure;

public partial class ConfigureWizard
{
    [Parameter] public string Slug { get; set; } = "";

    private int phaseIndex;
    private int subStep;
    private int furthest;            // highest phase reached — gates the rail (later = locked)
    private string? mapName;
    private bool loaded;

    // The stored authoring intent (GET /map/{slug}/intent) is the source of truth for both gating and
    // persistence. Held as a mutable JsonObject so a phase body can patch its own slice and mark dirty;
    // the wizard re-PUTs the whole object on phase-advance (save model, new-map-authoring.md §12).
    private JsonObject? intent;
    private bool dirty;
    private SaveState save = SaveState.Saved;
    private enum SaveState { Saved, Saving, Unsaved }

    private ConfigurePhase Phase => ConfigurePhases.All[phaseIndex];
    private int LastStep => Math.Max(0, Phase.SubSteps.Length - 1);
    private bool AtStart => phaseIndex == 0 && subStep == 0;
    private bool AtEnd => phaseIndex == ConfigurePhases.All.Length - 1 && subStep == LastStep;

    private bool BackEnabled => !AtStart;
    private string NextLabel => AtEnd ? "Export" : "Next";
    private string SubLabel => Phase.SubSteps.Length == 0 ? "Map Info" : Phase.SubSteps[subStep];

    // Topbar indicator — Saved · Saving… · Unsaved (no icons); blank until the intent has loaded.
    private string? SaveStatus => !loaded ? null : save switch
    {
        SaveState.Saving => "Saving…",
        SaveState.Unsaved => "Unsaved",
        _ => "Saved",
    };

    // A phase is "done" once its intent slice is present — the rail's green dot, and the prerequisite that
    // unlocks the next phase. Slices are per-phase (new-map-authoring.md §12): meta · symmetry · teams ·
    // build · wools; Review has no slice of its own.
    private bool PhaseDone(int i) => i switch
    {
        0 => Obj("meta") is { } m && !string.IsNullOrWhiteSpace(Str(m, "name")),
        1 => Obj("symmetry") is not null,
        2 => NonEmptyArray("teams"),
        3 => Obj("build") is not null,
        4 => NonEmptyArray("wools"),
        _ => false,
    };

    // The unlocked range derives from the intent: every phase whose prerequisites (all earlier slices) are
    // present, plus the first not-yet-done phase to work on next.
    private int SliceFurthest()
    {
        var n = 0;
        while (n < ConfigurePhases.All.Length && PhaseDone(n)) n++;
        return n;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (loaded) return;   // Slug is a fixed route param — load identity + intent once
        loaded = true;
        await Task.WhenAll(LoadNameAsync(), LoadIntentAsync());
        furthest = Math.Max(furthest, SliceFurthest());
    }

    private async Task LoadNameAsync()
    {
        // Best-effort: the shell renders regardless of whether the map has a DB row yet
        // (a freshly-imported/sketched world may not). The name is just for the breadcrumb.
        try
        {
            var resp = await Http.GetAsync($"api/map/{Slug}");
            if (resp.IsSuccessStatusCode)
            {
                var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
                mapName = doc.TryGetProperty("name", out var n) ? n.GetString() : null;
            }
        }
        catch { /* shell is independent of map data */ }
    }

    private async Task LoadIntentAsync()
    {
        try
        {
            var json = await Http.GetStringAsync($"api/map/{Slug}/intent");
            intent = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch { intent = new JsonObject(); }   // no row / not authored yet — start from empty intent
    }

    /// <summary>The working intent a phase body edits; call <see cref="MarkDirty"/> after mutating it so
    /// the slice persists on the next phase-advance.</summary>
    public JsonObject Intent => intent ??= new JsonObject();

    public void MarkDirty()
    {
        dirty = true;
        if (save != SaveState.Unsaved) { save = SaveState.Unsaved; StateHasChanged(); }
    }

    // Persist the whole intent (one idempotent regenerate, §3) when a dirty phase is left; a clean phase
    // is a no-op so we don't regenerate for nothing. After saving, a fresh slice may unlock the next phase.
    private async Task SaveIfDirtyAsync()
    {
        if (!dirty || intent is null) return;
        save = SaveState.Saving; StateHasChanged();
        try
        {
            var body = new StringContent(intent.ToJsonString(), Encoding.UTF8, "application/json");
            var resp = await Http.PutAsync($"api/map/{Slug}/intent", body);
            if (resp.IsSuccessStatusCode) { dirty = false; save = SaveState.Saved; }
            else save = SaveState.Unsaved;
        }
        catch { save = SaveState.Unsaved; }
        furthest = Math.Max(furthest, SliceFurthest());
    }

    private async Task JumpPhase(string id)
    {
        var i = ConfigurePhases.IndexOf(id);
        if (i < 0 || i > furthest || i == phaseIndex) return;   // locked / no-op
        await SaveIfDirtyAsync();   // leaving the current phase
        phaseIndex = i;
        subStep = 0;
    }

    private void JumpStep(int j)
    {
        if (j >= 0 && j <= LastStep) subStep = j;
    }

    private async Task Back()
    {
        if (subStep > 0) { subStep--; return; }
        if (phaseIndex > 0)
        {
            await SaveIfDirtyAsync();   // crossing a phase boundary
            phaseIndex--;
            subStep = LastStep;
        }
    }

    private async Task Next()
    {
        if (subStep < LastStep) { subStep++; return; }
        if (phaseIndex < ConfigurePhases.All.Length - 1)
        {
            await SaveIfDirtyAsync();   // crossing a phase boundary
            phaseIndex++;
            subStep = 0;
            if (phaseIndex > furthest) furthest = phaseIndex;
        }
        // AtEnd: "Export" is a no-op stub here — the gated export lands with N05/N06.
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    // ── intent slice helpers (presence checks over the camelCase intent JSON) ──
    private JsonObject? Obj(string key) => intent?[key] as JsonObject;
    private bool NonEmptyArray(string key) => intent?[key] is JsonArray a && a.Count > 0;
    private static string Str(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue(out string? s) ? s ?? "" : "";
}
