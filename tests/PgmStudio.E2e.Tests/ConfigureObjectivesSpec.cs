using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// The Configure wizard's objective phases, which are a group rather than a choice.
///
/// A PGM map may carry wools, destroyables and cores at once, so Cores is its own phase beside Wools and both
/// are offered on every map. Two claims are held down here: the Cores phase renders what the intent states,
/// and the objective phases share ONE completeness gate — a map whose only objective is a core is not held
/// behind an empty wool slice.
///
/// The seed's configure-stage map arrives with teams, build and wools authored; the plan compiler leaves
/// identity and symmetry empty (it owns neither), and the rail unlocks off those, so the spec fills them in.
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class ConfigureObjectivesSpec(E2eSession session)
{
    private static readonly (int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ) Casing = (40, 20, 40, 44, 24, 44);

    /// <summary>The core the Cores phase writes: a confirmed suggestion, casing volume and all.</summary>
    private static JsonObject Core(string owner) => new()
    {
        ["owner"] = owner, ["name"] = "Probe Core",
        ["anchor"] = new JsonObject { ["x"] = 42.5, ["y"] = 14, ["z"] = 42.5 },
        ["size"] = 5, ["height"] = 5, ["shell"] = 1, ["float"] = 6, ["leak"] = 9, ["openTop"] = false,
        ["box"] = new JsonObject
        {
            ["minX"] = Casing.MinX, ["minY"] = Casing.MinY, ["minZ"] = Casing.MinZ,
            ["maxX"] = Casing.MaxX, ["maxY"] = Casing.MaxY, ["maxZ"] = Casing.MaxZ,
        },
    };

    [Test, NotInParallel(Order = 2)]
    public async Task TheObjectivePhasesAreOneGroup()
    {
        var api = session.Api;
        var slug = session.Seed.MapSlug;
        var checks = new Checks("configure · objectives");
        await using var page = await session.NewPageAsync();

        var intent = (await api.Get($"/map/{slug}/intent"))!;
        var owner = intent["teams"]?[0].Text("id") ?? "";

        // Stores the map's objectives, with the identity and symmetry the rail unlocks off filled in.
        async Task Arrange(params (string Key, JsonNode Value)[] objectives)
        {
            var body = intent.DeepClone().AsObject();
            body["meta"]!["authors"] = new JsonArray(new JsonObject { ["name"] = "Notch" });
            body["symmetry"] = new JsonObject { ["mode"] = "rot_180", ["centerX"] = 0, ["centerZ"] = 0 };
            foreach (var (key, value) in objectives) body[key] = value;
            await api.Put($"/map/{slug}/intent", body);
        }

        // The author invented to complete Identity sends the topbar off to resolve a Minecraft username, which
        // answers 404 with no network — an artifact of the arrangement, nothing to do with the objective phases.
        List<string> FeatureFaults() => [.. page.Faults.Where(fault => !fault.Contains("/api/minecraft/", StringComparison.Ordinal))];

        async Task OpenPhase(string title)
        {
            page.ClearFaults();
            await page.GotoAsync($"/maps/{slug}/configure");
            await page.WaitForSelectorAsync(".nav-btn", 20000);
            await page.ClickAsync($".nav-btn[title=\"{title}\"]");
            await page.WaitForSelectorAsync(".flow-bar-phase", 20000);
        }

        // ── the Cores phase exists beside Wools, and renders the map's cores ──
        checks.Section("a map carrying wools AND a core shows both phases");

        await Arrange(("cores", new JsonArray(Core(owner))));

        await OpenPhase("Cores");
        var phaseTitle = (await page.TextContentAsync(".flow-bar-phase span")).Trim();
        checks.Add("the Cores phase opens", phaseTitle == "Cores", phaseTitle);

        var railTitles = await page.Locator(".nav-btn").EvaluateAllAsync<string?[]>("els => els.map(e => e.getAttribute('title'))");
        checks.Add("Wools and Cores are both on the rail", railTitles.Contains("Wools") && railTitles.Contains("Cores"),
            string.Join(" · ", railTitles));

        var steps = await page.Locator(".flow-steps .flow-step").EvaluateAllAsync<string[]>("els => els.map(e => e.textContent.trim())");
        checks.Add("the phase walks Objectives → Casing", string.Join(",", steps) == "Objectives,Casing", string.Join(" · ", steps));

        var listed = await page.TextContentAsync(".workspace");
        checks.Add("the stored core is listed", listed.Contains("Probe Core", StringComparison.Ordinal));

        // The casing volume is the core's region, so the step must show the box it will export, not a guess.
        await page.ClickAsync(".flow-steps .flow-step:nth-child(2)");
        await page.WaitForSelectorAsync(".workspace", 20000);
        var casing = await page.TextContentAsync(".workspace");
        checks.Add("the Casing step shows the region it will export",
            casing.Contains($"{Casing.MinX}, {Casing.MinY}, {Casing.MinZ}", StringComparison.Ordinal));
        // leak 9 over float 6 — four blocks of digging, stated rather than left to arithmetic. The lava
        // free-falls to the first air cell over the terrain and PGM tests its centre, so the core leaks one
        // course lower than the number reads.
        checks.Add("the dig depth is spelled out", Regex.IsMatch(casing, "digging 4 blocks"));

        checks.Add("the Cores phase raised nothing", FeatureFaults().Count == 0, string.Join(" | ", FeatureFaults()));

        // ── one gate across the objective phases ──
        checks.Section("a core-only map is not held behind an empty wool slice");

        await Arrange(("wools", new JsonArray()), ("cores", new JsonArray(Core(owner))));

        await OpenPhase("Review & Export");
        var reviewTitle = (await page.TextContentAsync(".flow-bar-phase span")).Trim();
        checks.Add("Review is reachable with no wools at all", reviewTitle == "Review & Export", reviewTitle);

        var locked = await page.Locator(".nav-btn").EvaluateAllAsync<string?[]>(
            "els => els.filter(e => e.disabled).map(e => e.getAttribute('title'))");
        checks.Add("no phase is locked", locked.Length == 0, string.Join(" · ", locked));

        checks.Add("the core-only map raised nothing", FeatureFaults().Count == 0, string.Join(" | ", FeatureFaults()));

        // Put the seed map back the way the other specs expect it.
        await api.Put($"/map/{slug}/intent", intent);

        checks.Finish();
    }
}
