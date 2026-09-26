using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// A compile finding points at what it is about: the validator names the subjects of every refusal, and a
/// click on the finding's row pulses them on the canvas.
///
/// The fixture is a plan built to fail one way — a 15-block casing on the far edge of its piece, most of its
/// footprint over nothing — so the refusal names the core. The API assertion comes first, because if the
/// fixture ever stops being refused the UI checks would pass vacuously against a drawer with no rows.
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class PlanFindingsSpec(E2eSession session)
{
    /// <summary>An order-sensitive digest of the board: which colour sits at which offset.</summary>
    private const string Survey = """
        () => {
          const canvas = document.querySelector("canvas.world-canvas-2d");
          const { width, height } = canvas;
          const data = canvas.getContext("2d").getImageData(0, 0, width, height).data;
          let signature = 0;
          for (let i = 0; i < data.length; i += 4) {
            if (data[i + 3] === 0) continue;
            signature = (signature + data[i] * 3 + data[i + 1] * 5 + data[i + 2] * 7 + i) % 1_000_000_007;
          }
          return signature;
        }
        """;

    [Test, NotInParallel(Order = 9)]
    public async Task AFindingShowsWhatItIsAbout()
    {
        var api = session.Api;
        var checks = new Checks("plan · findings");

        // One 6×6-cell island, and a core on its right edge wearing a casing three cells across: centred on the
        // marker, over half of that footprint hangs past the last block of land. `at` is blocks from the piece's
        // minimum corner; the piece's own rect is in cells.
        var plan = JsonNode.Parse("""
            {
              "plan": 2,
              "globals": { "cell": 5, "symmetry": "rot_180", "maxPlayers": 12, "surface": 9, "headroom": 11 },
              "pieces": [{ "id": "land", "role": "piece", "rect": [0, 0, 6, 6] }],
              "placements": { "cores": [{ "id": "core-1", "piece": "land", "at": [30, 15], "size": 15 }] }
            }
            """)!;

        checks.Section("the plan is refused, and the refusal names the core");

        var compiled = await api.Raw(HttpMethod.Post, "/plan/compile", plan);
        checks.Add("the overhanging core blocks the compile", compiled.Status == 422, $"{compiled.Status}");

        var findings = compiled.Json.Items("findings");
        var overhang = findings.FirstOrDefault(finding => Regex.IsMatch(finding.Text("message") ?? "", "overhangs the void"));
        var messages = string.Join(" | ", findings.Select(finding => finding.Text("message")));
        checks.Add("a finding says the goal overhangs the void", overhang != null, messages.Length > 0 ? messages : "no findings");
        var subjects = overhang.Items("subjects").Select(subject => subject.GetValue<string>()).ToList();
        checks.Add("it names the marker, not only its piece", subjects.Contains("core-1"),
            subjects.Count > 0 ? string.Join(", ", subjects) : "no subjects");

        // ── the row, and what clicking it does ──
        await using var page = await session.NewPageAsync();

        var slug = (await api.Post("/plan", new { name = "e2e findings" })).Text("slug")!;
        await api.Put($"/map/{slug}/plan", plan);

        await page.GotoAsync($"/maps/{slug}/plan");
        await page.WaitForSelectorAsync("canvas.world-canvas-2d", 20000);
        await StudioPage.Pause(800);

        checks.Section("the finding is offered as something to click");

        await page.Locator("button", hasText: "Compile").ClickAsync();
        await page.WaitForSelectorAsync(".plan-compile-errors", 20000);

        var row = page.Locator(".plan-compile-errors .plan-lint-row", hasText: "overhangs the void").First();
        checks.Add("the refusal is listed in the compile drawer", await row.CountAsync() > 0);
        var rowText = await row.TextContentAsync();
        checks.Add("and it reads as the marker's problem", rowText.Contains("core 'core-1'", StringComparison.Ordinal), rowText.Trim());
        checks.Add("the row is live, not inert",
            !(await row.GetAttributeAsync("class") ?? "").Contains("plan-lint-row--static", StringComparison.Ordinal)
            && await row.IsEnabledAsync());

        checks.Section("clicking it shows the core on the board");

        // The compile this fixture exists to trigger answers 422, which the page records as a fault. It is the
        // expected answer, so it is cleared here — leaving the check below to catch anything the click raises.
        page.ClearFaults();

        var before = await page.EvaluateAsync<long>(Survey);
        await row.ClickAsync();
        await StudioPage.Pause(200);

        // The drawer is modal and dims the board behind it, so the click has to dismiss it.
        checks.Add("the drawer gets out of the way", await page.Locator(".side-drawer").CountAsync() == 0);

        var during = await page.EvaluateAsync<long>(Survey);
        checks.Add("the board is repainted with the highlight", during != before, $"{before} → {during}");

        // The pulse is transient by design (1.6s) and leaves the board exactly as it found it — which also says
        // the difference above was the highlight rather than an unrelated repaint.
        await StudioPage.Pause(2200);
        var settled = await page.EvaluateAsync<long>(Survey);
        checks.Add("the highlight clears itself", settled == before, $"{before} → {during} → {settled}");

        checks.Add("showing a finding raised nothing", page.Faults.Count == 0, string.Join(" | ", page.Faults));

        checks.Finish();
    }
}
