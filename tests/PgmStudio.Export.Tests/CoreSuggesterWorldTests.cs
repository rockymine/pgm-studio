using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export.Tests;

/// <summary>
/// Core detection scored against ground truth that exists <b>by construction</b>: a plan states where a core
/// is, the pipeline builds the world, and the detector has to find it there. The corpus can only ever say
/// "83% of candidates matched a declared core"; this says "the core we placed at this anchor, with the casing
/// we asked for, is proposed with those exact parameters" — which is the claim the suggestion UI relies on.
/// </summary>
public sealed class CoreSuggesterWorldTests
{
    private static string Plan(string cores, string extra = "") => $$"""
        {
          "plan": 1,
          "meta": { "name": "Core Detection Probe" },
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
          "pieces": [ { "id": "bar-w", "role": "piece", "rect": [1, -2, 4, 4], "surface": 12 } ],
          "placements": { "cores": [ {{cores}} ] }
          {{extra}}
        }
        """;

    private static (List<CoreSuggestion> Found, MapIntent Resolved) Detect(string json)
    {
        var plan = PlanModel.Parse(json)!;
        var (layout, intent) = PlanCompiler.Compile(plan);
        var built = SketchWorldBuilder.Build(JsonSerializer.Serialize(layout, SketchLayout.Json), intent);

        // The plan's world is small and bounded, so sampling it is cheaper than teaching VoxelWorld to enumerate.
        var world = new Dictionary<(int X, int Y, int Z), int>();
        for (var x = -120; x <= 120; x++)
            for (var z = -120; z <= 120; z++)
                for (var y = 0; y < 64; y++)
                {
                    var id = built.World.GetBlock(x, y, z).Id;
                    if (id != 0) world[(x, y, z)] = id;
                }
        return (CoreSuggester.Gather(world), built.ResolvedIntent);
    }

    [Test]
    public async Task Every_core_the_plan_placed_is_proposed_where_it_was_placed()
    {
        var (found, resolved) = Detect(Plan("""{ "piece": "bar-w", "at": [2, 2] }"""));

        // rot_180 fans one authored marker into two cores; both must be found and neither invented.
        await Assert.That(resolved.Cores!.Count).IsEqualTo(2);
        await Assert.That(found.Count).IsEqualTo(2);

        foreach (var core in resolved.Cores!)
        {
            var box = core.Box!.Value;
            var match = found.SingleOrDefault(suggestion => suggestion.Casing == box);
            await Assert.That(match).IsNotNull()
                .Because($"no core proposed at the casing the plan built ({box}); proposed "
                         + string.Join(" | ", found.Select(f => f.Casing.ToString())));
        }
    }

    [Test]
    public async Task The_proposed_parameters_are_the_ones_the_plan_asked_for()
    {
        var (found, _) = Detect(Plan("""{ "piece": "bar-w", "at": [2, 2] }"""));
        var core = found[0];

        // The compiler's defaults: 5×5×5 casing, shell 1, a 3×3×3 lava interior, capped.
        await Assert.That((core.Casing.Width, core.Casing.Height, core.Casing.Depth)).IsEqualTo((5, 5, 5));
        await Assert.That(core.LavaBlocks).IsEqualTo(27);
        await Assert.That(core.Shell).IsEqualTo(1);
        await Assert.That(core.OpenTop).IsFalse();
    }

    [Test]
    public async Task An_open_top_core_is_proposed_as_open_top()
    {
        // The flag has to survive the round trip, because it is the difference between a goal players can see
        // into and one they must breach.
        var (found, _) = Detect(Plan("""{ "piece": "bar-w", "at": [2, 2], "openTop": true, "float": 6, "leak": 5 }"""));

        await Assert.That(found).IsNotEmpty();
        await Assert.That(found.All(core => core.OpenTop)).IsTrue();
    }

    [Test]
    public async Task A_thicker_shell_is_measured_not_assumed()
    {
        var (found, _) = Detect(Plan("""{ "piece": "bar-w", "at": [2, 2], "size": 7, "height": 7, "shell": 2 }"""));

        await Assert.That(found).IsNotEmpty();
        await Assert.That(found[0].Shell).IsEqualTo(2);
        await Assert.That((found[0].Casing.Width, found[0].Casing.Depth)).IsEqualTo((7, 7));
    }

    [Test]
    public async Task A_plan_with_no_core_yields_no_proposal()
    {
        // The terrain, spawn platforms and structures a plan builds must not read as a sealed lava container.
        var plan = PlanModel.Parse("""
            {
              "plan": 1,
              "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
              "pieces": [ { "id": "bar-w", "role": "piece", "rect": [1, -2, 4, 4] } ],
              "placements": { "spawns": [ { "piece": "bar-w", "at": [2, 2], "facing": "front" } ] }
            }
            """)!;
        var (layout, intent) = PlanCompiler.Compile(plan);
        var built = SketchWorldBuilder.Build(JsonSerializer.Serialize(layout, SketchLayout.Json), intent);

        var world = new Dictionary<(int X, int Y, int Z), int>();
        for (var x = -120; x <= 120; x++)
            for (var z = -120; z <= 120; z++)
                for (var y = 0; y < 64; y++)
                {
                    var id = built.World.GetBlock(x, y, z).Id;
                    if (id != 0) world[(x, y, z)] = id;
                }

        await Assert.That(CoreSuggester.Gather(world)).IsEmpty();
    }
}
