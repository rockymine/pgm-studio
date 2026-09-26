using System.Text.Json.Nodes;
using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// The refusal contract: a plan missing something a map cannot do without is turned away with a reason, at
/// the earliest gate that can see the problem — never compiled into a half-map and never silently finished.
///
/// Each case corrupts a real composed plan in exactly one way, so a refusal can be attributed to it. A case
/// that stops refusing is the interesting result: it means a gate moved.
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class PlanRefusalsSpec(E2eSession session)
{
    /// <summary>
    /// One slice stripped out of a good plan. <c>Refused</c> means the compile gate blocks it (422 + findings);
    /// otherwise it compiles today and is asserted as-is, so a gate that starts refusing it fails loudly. The
    /// gate asks whether the plan is coherent and whether it carries what a map cannot exist without (land, a
    /// spawn); a missing objective is the author's to set later, so it is a warning on the 200 instead.
    /// </summary>
    private static readonly (string Label, bool Refused, Action<JsonNode> Mutate)[] Cases =
    [
        ("no pieces at all", true, plan => { plan["pieces"] = new JsonArray(); plan["zones"] = new JsonArray(); plan["boxes"] = new JsonArray(); }),
        ("no build zones", true, plan => plan["zones"] = new JsonArray()),
        ("a marker on a piece that isn't there", true, plan =>
        {
            if (plan["placements"]?["wools"]?[0] is JsonObject wool) wool["piece"] = "no-such-piece";
        }),
        ("no spawns", true, plan => plan["placements"]!["spawns"] = new JsonArray()),

        // still compiles: incomplete but not un-buildable
        ("no wools", false, plan => plan["placements"]!["wools"] = new JsonArray()),
        ("a piece with a blank id", false, plan =>
        {
            if (plan["pieces"]?[0] is JsonObject piece) piece["id"] = "";
        }),
    ];

    [Test, NotInParallel(Order = 12)]
    public async Task APlanMissingWhatAMapNeedsIsRefusedWithAReason()
    {
        var api = session.Api;
        var good = JsonNode.Parse(session.Seed.PlanJson)!;
        JsonNode Mutated(Action<JsonNode> mutate)
        {
            var plan = good.DeepClone();
            mutate(plan);
            return plan;
        }
        static string Clip(string text, int length) => text.Length > length ? text[..length] : text;

        var checks = new Checks("plan refusals");

        checks.Section("a good plan still compiles");
        var okCompile = await api.Raw(HttpMethod.Post, "/plan/compile", good);
        checks.Add("the composed plan compiles", okCompile.Status == 200,
            okCompile.Status == 200 ? "200" : $"{okCompile.Status}: {Clip(okCompile.Text, 160)}");

        checks.Section("a structurally broken plan is refused, with findings");
        foreach (var (label, _, mutate) in Cases.Where(item => item.Refused))
        {
            var answer = await api.Raw(HttpMethod.Post, "/plan/compile", Mutated(mutate));
            var refused = answer.Status == 422;
            var findings = answer.Json.Items("findings");
            checks.Add($"refuses: {label}", refused, refused ? "422" : $"got {answer.Status}");
            if (refused)
            {
                var why = findings.Select(finding => finding.Text("message")).FirstOrDefault(message => !string.IsNullOrEmpty(message)) ?? "";
                checks.Add($"  …and says why: {label}", findings.Count > 0, Clip(why, 90));
            }
        }

        checks.Section("an incomplete-but-buildable plan still compiles");
        foreach (var (label, _, mutate) in Cases.Where(item => !item.Refused))
        {
            var answer = await api.Raw(HttpMethod.Post, "/plan/compile", Mutated(mutate));
            checks.Add($"still compiles: {label}", answer.Status == 200,
                answer.Status == 200 ? "200" : $"now {answer.Status}: a gate landed, flip this case");
        }

        checks.Section("a goalless plan compiles, but says so");
        {
            var goalless = Mutated(plan =>
            {
                plan["placements"]!["wools"] = new JsonArray();
                plan["placements"]!["destroyables"] = new JsonArray();
                plan["placements"]!["cores"] = new JsonArray();
            });
            var answer = await api.Raw(HttpMethod.Post, "/plan/compile", goalless);
            var warnings = answer.Json.Items("warnings");
            var warned = warnings.Any(warning => (warning.Text("message") ?? "").Contains("objective", StringComparison.OrdinalIgnoreCase));
            checks.Add("compiles", answer.Status == 200, $"{answer.Status}");
            var said = string.Join("; ", warnings.Select(warning => warning.Text("message")));
            checks.Add("…and warns about the missing objective", warned, said.Length > 0 ? Clip(said, 100) : "no warnings");

            // The warning must not be noise on a plan that does have a goal.
            var withGoal = await api.Raw(HttpMethod.Post, "/plan/compile", good);
            var goalWarnings = withGoal.Json?["warnings"] as JsonArray ?? [];
            checks.Add("no warning on a plan with an objective", goalWarnings.Count == 0, Clip(goalWarnings.ToJsonString(), 100));
        }

        checks.Section("an empty plan is refused, not answered with an empty map");
        {
            var empty = new JsonObject { ["plan"] = 2, ["globals"] = new JsonObject { ["cell"] = 5 } };
            var compiled = await api.Raw(HttpMethod.Post, "/plan/compile", empty);
            checks.Add("empty plan → 422", compiled.Status == 422, $"{compiled.Status}");
            var why = compiled.Json.Items("findings").Select(finding => finding.Text("message")).FirstOrDefault() ?? "";
            checks.Add("…naming the missing land", why.Contains("no pieces", StringComparison.Ordinal), Clip(why, 90));

            // Its sibling endpoints must survive the same document — an empty plan is well-formed, just empty.
            var evaluated = await api.Raw(HttpMethod.Post, "/plan/evaluate", empty);
            checks.Add("evaluate answers an empty plan with an empty score", evaluated.Status == 200,
                $"{evaluated.Status}: {Clip(evaluated.Text, 120)}");
            var inspected = await api.Raw(HttpMethod.Post, "/plan/inspect", empty);
            checks.Add("inspect answers an empty plan", inspected.Status == 200, $"{inspected.Status}");
        }

        checks.Section("malformed input is answered, not 500");
        foreach (var (label, body) in new[] { ("not json", "{{{"), ("empty", ""), ("an array", "[]") })
        {
            var answer = await api.Raw(HttpMethod.Post, "/plan/compile", body);
            checks.Add($"{label} → 4xx, never 5xx", answer.Status is >= 400 and < 500, $"{answer.Status}");
        }

        checks.Section("finish takes one island, and refuses only bare ground");
        // One connected landmass is a map, not a half-drawn one, so Finish must accept it and refuse only a
        // layout that rasterizes to no ground at all.
        var draft = await api.Raw(HttpMethod.Post, "/sketch", new { name = "E2E refusal draft" });
        if (draft.Status == 200 && draft.Json.Text("slug") is { } slug)
        {
            var setup = new JsonObject
            {
                ["bbox"] = new JsonObject { ["min_x"] = -32, ["max_x"] = 32, ["min_z"] = -32, ["max_z"] = 32 },
                ["center"] = new JsonObject { ["cx"] = 0, ["cz"] = 0 },
                ["mirror_mode"] = "none",
            };
            JsonObject Layout(JsonArray shapes) => new()
            {
                ["setup"] = setup.DeepClone(),
                ["layers"] = new JsonArray(new JsonObject
                {
                    ["id"] = "l1", ["name"] = "Ground", ["base_y"] = 0,
                    ["layout"] = new JsonObject { ["shapes"] = shapes, ["islands"] = new JsonArray() },
                }),
            };
            var oneShape = Layout(new JsonArray(new JsonObject
            {
                ["id"] = "s1", ["type"] = "rectangle", ["operation"] = "add",
                ["min_x"] = 0, ["max_x"] = 4, ["min_z"] = 0, ["max_z"] = 4, ["base_height"] = 1, ["floor"] = 0,
            }));
            await api.Raw(HttpMethod.Put, $"/map/{slug}/sketch", oneShape);
            var finished = await api.Raw(HttpMethod.Post, $"/map/{slug}/sketch/finish");
            checks.Add("finish accepts a one-island layout", finished.Status == 200,
                $"{finished.Status}: {Clip(finished.Json.Text("error") ?? finished.Text, 120)}");

            // Nothing drawn is the one thing left to refuse.
            var bare = await api.Raw(HttpMethod.Post, "/sketch", new { name = "E2E bare draft" });
            if (bare.Status == 200 && bare.Json.Text("slug") is { } bareSlug)
            {
                await api.Raw(HttpMethod.Put, $"/map/{bareSlug}/sketch", Layout(new JsonArray()));
                var bareFinished = await api.Raw(HttpMethod.Post, $"/map/{bareSlug}/sketch/finish");
                checks.Add("finish refuses a layout with no ground", bareFinished.Status == 422,
                    $"{bareFinished.Status}: {Clip(bareFinished.Json.Text("error") ?? bareFinished.Text, 120)}");
                await api.Raw(HttpMethod.Delete, $"/map/{bareSlug}/discard-if-empty");
            }
        }
        else
        {
            checks.Add("finish accepts a one-island layout", false, $"could not create a draft: {draft.Status}");
        }

        checks.Finish();
    }
}
