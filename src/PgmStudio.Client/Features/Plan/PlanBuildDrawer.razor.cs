using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Plan;

public partial class PlanBuildDrawer
{
    /// <summary>The bridge that holds the plan document.</summary>
    [Parameter] public IJSObjectReference? Handle { get; set; }

    /// <summary>The map the plan is stored on, or null for a plan row, whose build originates the map.</summary>
    [Parameter] public string? Slug { get; set; }

    [Parameter] public string PlanName { get; set; } = "";

    /// <summary>The write of the map's plan artifact, which the build states the loaded revision on.</summary>
    [Parameter, EditorRequired] public DocumentSave Document { get; set; } = default!;

    private bool MapBacked => Slug is { Length: > 0 };

    // The compiled pair (pretty-printed for preview, raw for the draft chain), the structural errors when
    // the compile is blocked, and the per-step state of the build.
    private bool open;

    private bool compiling;
    private string compileTab = PlanTabId;
    private bool copied;
    private string? compiledPlan;                     // the plan document this compile was run on
    private string? compiledLayout, compiledIntent;   // pretty-printed for the preview panes
    private string? compiledLayoutRaw, compiledIntentRaw;   // verbatim, posted to the draft pipeline
    private string? compileError;                     // a malformed / transport failure message
    private List<Finding> compileErrors = [];  // 422 structural findings (compile blocked)
    private List<Finding> compileWarnings = [];  // completeness complaints that did not block the compile

    private string? draftSlug;
    private bool draftBusy;
    private string draftStep = "";
    private string? draftError;

    // What the open map already holds, so the build can tell an origination from a rebuild before it runs.
    // A map with a sketch or a world has downstream work that the build replaces, which is worth saying
    // out loud once rather than discovering afterwards; a plan that has never been built has nothing to
    // lose and gets no interruption. Null until the fetch lands, and on a plan row, where there is no map to
    // ask about.
    private MapState? state;
    private bool confirmingRebuild;

    // The groups whose relief the rebuilt board has no island for, as the layout write refused them (409) —
    // offered back as a choice, since discarding hand-drawn terrain is the author's call and not the build's.
    private IReadOnlyList<string>? orphanedRelief;

    // The sketch-drawn shapes the last rebuild did not keep, as the layout write answered them.
    private IReadOnlyList<string> droppedShapes = [];

    private bool Rebuilds => state is { Artifacts: { } held } && (held.Sketch || held.World);
    private string BuildLabel => Rebuilds ? "Rebuild this map" : MapBacked ? "Build the map" : "Create draft";

    /// <summary>What the drawer's footer button says. <see cref="BuildLabel"/> answers only whether the map is
    /// built, which is the right word for the one state where the button can act; every other state is about
    /// the compile, and naming it is what keeps a disabled control from promising the build it cannot do. A
    /// refusal count points at the findings the drawer already lists above the footer.</summary>
    private string DraftLabel
        => compiling ? "Compiling…"
         : compileErrors.Count > 0
             ? $"Fix {compileErrors.Count} problem{(compileErrors.Count == 1 ? "" : "s")} first"
         : compileError is not null ? "Couldn't compile"
         : compiledLayout is null ? "Compile first"
         : BuildLabel;

    private async Task LoadStateAsync()
    {
        if (!MapBacked) { state = null; return; }
        try { state = await Http.GetFromJsonAsync<MapState>($"api/map/{Slug}/state"); }
        catch { state = null; }   // unreachable / not a map row → treat as a first build, never block one
    }


    // ── compile and build ────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    /// <summary>Open the drawer and compile the plan the bridge holds.</summary>
    public async Task OpenAsync()
    {
        open = true;
        confirmingRebuild = false;
        StateHasChanged();
        await LoadStateAsync();   // re-read on open: a build in this session changes the answer
        await Compile();
        StateHasChanged();
    }

    /// <summary>Close the drawer, keeping what it compiled for the next opening.</summary>
    public void Close()
    {
        open = false;
        confirmingRebuild = false;
        StateHasChanged();
    }

    /// <summary>Drop everything read off the previous binding: the drawer closes and the map state is
    /// forgotten, so the next opening asks again.</summary>
    public void Reset()
    {
        open = false;
        confirmingRebuild = false;
        state = null;
    }

    // A finding can point at what it is about only if it named something: a rule about the plan as a whole
    // (or one whose subject the canvas cannot draw) has nothing to show.
    private static bool HasSubjects(Finding finding) => finding.SubjectIds.Count > 0;

    private static string? ShowFindingTitle(Finding finding)
        => HasSubjects(finding) ? "Show on the canvas" : null;

    // Click a compile finding to see what it is about. A finding names its subjects — pieces, zones, markers —
    // and the canvas can pulse them, but the compile drawer is modal and dims the board behind it, so the
    // drawer closes first: the click means "show me", and the answer is on the canvas rather than in the list.
    private async Task ShowFinding(Finding finding)
    {
        if (Handle is null || !HasSubjects(finding)) return;
        Close();
        // Render the closed drawer before the pulse starts, so the whole 1.6s ramp plays on a board the
        // author can actually see rather than beginning under the backdrop.
        StateHasChanged();
        await Task.Yield();
        await Handle.InvokeVoidAsync("highlightSubjects", JsonSerializer.Serialize(finding.SubjectIds));
    }

    // The build button. On a map that already holds a sketch or a world it asks first, because the same
    // click means two different things — originating the map, or replacing a board someone has since been
    // working on — and only the second is worth a sentence.
    private async Task BuildRequested()
    {
        if (Rebuilds && !confirmingRebuild) { confirmingRebuild = true; return; }
        confirmingRebuild = false;
        await CreateDraft();
    }

    private void CancelRebuild() => confirmingRebuild = false;

    private void KeepRelief() => orphanedRelief = null;

    // The author accepted the loss the layout write refused over: the same build, with ?force=true.
    private async Task DiscardReliefAndRebuild()
    {
        orphanedRelief = null;
        await CreateDraft(discardRelief: true);
    }

    // Post the current plan to /api/plan/compile. A 422 renders its structural findings in place of the JSON;
    // a 400 (malformed) / transport failure shows a message. A 200 stores the compiled pair for preview + the
    // draft chain. Compiling resets any prior draft so a fresh compile starts the loop over.
    private async Task Compile()
    {
        if (Handle is null) return;
        compiling = true;
        compileError = null;
        compileErrors = [];
        compileWarnings = [];
        compiledPlan = compiledLayout = compiledIntent = compiledLayoutRaw = compiledIntentRaw = null;
        compileTab = PlanTabId;
        draftSlug = null; draftError = null; draftBusy = false;
        orphanedRelief = null; droppedShapes = [];
        StateHasChanged();

        try
        {
            var planJson = await Handle.InvokeAsync<string>("exportJson");
            // Kept whatever the compile answers: the plan is the one file that exists either way, and a plan
            // that will not compile is the one an author most needs a copy of.
            compiledPlan = Reindent(planJson);
            using var resp = await Http.PostAsync("api/plan/compile", new StringContent(planJson, Encoding.UTF8, "application/json"));
            if (resp.IsSuccessStatusCode)
            {
                // The compiled pair and what the compile remarked on come out of one read: `warnings` is a
                // member of the success rather than a wrapper around it, and the body is a stream.
                var (compiled, warnings) = await ServerWarnings.AnsweredAsync<CompiledPlanDto>(resp);
                compiledLayoutRaw = compiled?.Layout.GetRawText();
                compiledIntentRaw = compiled?.Intent.GetRawText();
                compiledLayout = compiled is null ? null : JsonSerializer.Serialize(compiled.Layout, Pretty);
                compiledIntent = compiled is null ? null : JsonSerializer.Serialize(compiled.Intent, Pretty);
                compileWarnings = [.. warnings];
                compileTab = LayoutTabId;
            }
            else if ((int)resp.StatusCode == 422)
            {
                var refusal = await resp.Content.ReadFromJsonAsync<RefusalDto>();
                compileErrors = [.. refusal?.Findings ?? []];
            }
            else
            {
                compileError = $"Couldn't compile the plan (HTTP {(int)resp.StatusCode}). {Trunc(await resp.Content.ReadAsStringAsync())}";
            }
        }
        catch (Exception ex) { compileError = ex.Message; }
        compiling = false;
        StateHasChanged();
    }

    // ── the drawer's files ───────────────────────────────────────────────────────
    // Every file a plan can be read out of is offered from the one drawer: the plan document, and the pair
    // it compiled into once there is one. The tab id doubles as the downloaded file's middle extension
    // (`<name>.plan.json`, `.layout.json`, `.intent.json`).

    private const string PlanTabId = "plan";
    private const string LayoutTabId = "layout";
    private const string IntentTabId = "intent";

    private IEnumerable<(string Id, string Label)> CompileTabs
    {
        get
        {
            yield return (PlanTabId, "Plan");
            if (compiledLayout is null) yield break;
            yield return (LayoutTabId, "Layout");
            yield return (IntentTabId, "Game settings");
        }
    }

    private string? TabText => compileTab switch
    {
        LayoutTabId => compiledLayout,
        IntentTabId => compiledIntent,
        _ => compiledPlan,
    };

    /// <summary>The plan document as the panes show every other file — the bridge exports it compact.</summary>
    private static string Reindent(string json)
    {
        try { return JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement, Pretty); }
        catch (JsonException) { return json; }
    }

    private async Task CopyTab()
    {
        if (TabText is not { } text) return;
        copied = await JS.InvokeAsync<bool>("studio.copyText", text);
        StateHasChanged();
    }

    private async Task DownloadTab()
    {
        if (TabText is not { } text) return;
        var slug = string.IsNullOrWhiteSpace(PlanName) ? "plan" : PlanName.Trim().ToLowerInvariant().Replace(' ', '-');
        await JS.InvokeVoidAsync("studio.downloadText", $"{slug}.{compileTab}.json", text, "application/json");
    }

    // Drive the draft pipeline from a successful compile: push the compiled layout, rasterize it, then push
    // the compiled intent. Any non-2xx step aborts with a message naming that step.
    //
    // A map-backed plan builds onto its OWN row: one map carries plan → sketch → configure, keeps its plan
    // blob beside the layout it compiled into, and re-running refreshes it in place instead of leaving a
    // trail of near-identical maps. The intent write carries the plan's name, so the map's identity follows
    // the plan without a second call. A plan row has no map, so building one originates the map: there the
    // build IS the map's creation.
    //
    // Either way the plan itself is written to the map first, so the map carries the document its layout was
    // compiled from and opens in the plan editor on the board it was built from.
    //
    // Both writes go through their from-plan route rather than the plain PUT, for the same reason: a
    // compiled pair states what the plan states and nothing else, so a straight replace deleted everything
    // the map had accumulated on top. The layout carries its finish across (SketchLayout.CarryFinish — the
    // themes, room shells and dressing) and the intent carries its authored slices (IntentCarry — the
    // authors, island team assignments and confirmed symmetry). Rebuilding changes the board and the
    // structure the plan describes; it is not an answer to anything else about the map.
    private async Task CreateDraft(bool discardRelief = false)
    {
        if (Handle is null || compiledLayoutRaw is null || compiledIntentRaw is null) return;
        draftBusy = true; draftError = null; draftSlug = null; orphanedRelief = null; droppedShapes = [];

        try
        {
            var slug = Slug;
            if (!MapBacked)
            {
                draftStep = "Creating draft"; StateHasChanged();
                using var createResp = await Http.PostAsJsonAsync("api/sketch", new { name = PlanName });
                if (!await Ok(createResp, "create the draft")) return;
                slug = (await createResp.Content.ReadFromJsonAsync<OriginatedDto>())?.Slug;
                if (string.IsNullOrEmpty(slug)) { draftError = "Couldn't create the draft. The server didn't return its name."; return; }
            }

            draftStep = "Saving the plan"; StateHasChanged();
            if (MapBacked)
            {
                var outcome = await Document.PutAsync($"api/map/{slug}/plan", () => PlanExport.BodyAsync(Handle!));
                if (!outcome.Landed) { draftError = outcome.Message; return; }
            }
            else
            {
                using var planResp = await Http.PutAsync($"api/map/{slug}/plan", await PlanExport.BodyAsync(Handle));
                if (!await Ok(planResp, "save the plan")) return;
            }

            draftStep = "Saving the layout"; StateHasChanged();
            using var layoutResp = await Http.PutAsync(
                discardRelief ? $"api/map/{slug}/sketch/from-plan?force=true" : $"api/map/{slug}/sketch/from-plan",
                new StringContent(compiledLayoutRaw, Encoding.UTF8, "application/json"));
            // A 409 here is relief the rebuilt board has no island for, one finding per group: asked back
            // rather than reported as a failure, since the way through is a choice the author makes.
            if (layoutResp.StatusCode == System.Net.HttpStatusCode.Conflict
                && await layoutResp.Content.ReadFromJsonAsync<RefusalDto>() is { } refusal
                && refusal.Findings.SelectMany(finding => finding.SubjectIds).ToList() is { Count: > 0 } groups)
            {
                orphanedRelief = groups;
                return;
            }
            if (!await Ok(layoutResp, "save the layout")) return;
            droppedShapes = (await layoutResp.Content.ReadFromJsonAsync<SketchFromPlanDto>())?.Dropped ?? [];

            draftStep = "Building the world"; StateHasChanged();
            using var finishResp = await Http.PostAsync($"api/map/{slug}/sketch/finish", null);
            if (!await Ok(finishResp, "build the world")) return;

            draftStep = "Applying game settings"; StateHasChanged();
            using var intentResp = await Http.PutAsync($"api/map/{slug}/intent/from-plan", new StringContent(compiledIntentRaw, Encoding.UTF8, "application/json"));
            if (!await Ok(intentResp, "apply the game settings")) return;

            draftSlug = slug;
            await LoadStateAsync();   // the map now holds a sketch and a world — the next build is a rebuild
        }
        catch (Exception ex) { draftError = ex.Message; }
        finally { draftBusy = false; StateHasChanged(); }
    }

    private async Task<bool> Ok(HttpResponseMessage resp, string step)
    {
        if (resp.IsSuccessStatusCode) return true;
        draftError = $"Couldn't {step} (HTTP {(int)resp.StatusCode}). {Trunc(await resp.Content.ReadAsStringAsync())}";
        return false;
    }

    // Save the draft's world export, or say why it was refused.
    private async Task DownloadWorld()
    {
        if (draftSlug is null) return;
        draftError = await MapDownload.SaveAsync(Http, JS, draftSlug);
        StateHasChanged();
    }

    private static string Trunc(string s) => s.Length > 200 ? s[..200] + "…" : s;
}
