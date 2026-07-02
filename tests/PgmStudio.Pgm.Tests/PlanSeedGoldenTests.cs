using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// Regression anchor: compiling each seed <c>*.plan.json</c> reproduces the checked-in <c>*.layout.json</c> +
/// <c>*.intent.json</c>. Layout shapes compare as normalised rings (winding/start-independent); islands and
/// setup compare structurally; intent fields compare as sets where the original is a set. base-2island and
/// base-4team match exactly; base-2wool matches on structure, with the two documented deviations (the
/// hand-authored wool y and second-dye strings) checked leniently.
/// </summary>
public sealed class PlanSeedGoldenTests
{
    private static (SketchLayout Layout, MapIntent Intent) Compile(string name) =>
        PlanCompiler.Compile(PlanModel.Parse(PlanTestSupport.ReadSeed($"{name}.plan.json"))!);

    // ── layout (all three: shapes + islands + setup are exact modulo ring winding/start) ────────────────

    [Test]
    [Arguments("base-2island")]
    [Arguments("base-2wool")]
    [Arguments("base-4team")]
    public async Task Compiled_layout_reproduces_the_seed(string name)
    {
        var (layout, _) = Compile(name);
        var seed = PlanTestSupport.LoadLayout($"{name}.layout.json");

        // shapes: same set of normalised rings, each with the same base height
        var got = PlanTestSupport.RingHeights(layout);
        var want = PlanTestSupport.RingHeights(seed);
        await Assert.That(got.Keys.ToHashSet().SetEquals(want.Keys)).IsTrue();
        foreach (var (ring, h) in want) await Assert.That(got[ring]).IsEqualTo(h);

        // one mirror island covering every shape
        await Assert.That(layout.Layout!.Islands.Count).IsEqualTo(1);
        var island = layout.Layout!.Islands[0];
        await Assert.That(island.Mirrors).IsEqualTo(seed.Layout!.Islands[0].Mirrors);
        var refRings = island.ShapeIds
            .Select(id => PlanTestSupport.NormRing(layout.Layout!.Shapes.Single(s => s.Id == id).Vertices ?? []))
            .ToHashSet();
        await Assert.That(refRings.SetEquals(got.Keys)).IsTrue();

        // setup
        await Assert.That(layout.Setup!.MirrorMode).IsEqualTo(seed.Setup!.MirrorMode);
        await Assert.That(layout.Setup!.Center!.Cx).IsEqualTo(seed.Setup!.Center!.Cx);
        await Assert.That(layout.Setup!.Center!.Cz).IsEqualTo(seed.Setup!.Center!.Cz);
        await Assert.That(layout.Setup!.Bbox!.MinX).IsEqualTo(seed.Setup!.Bbox!.MinX);
        await Assert.That(layout.Setup!.Bbox!.MaxX).IsEqualTo(seed.Setup!.Bbox!.MaxX);
        await Assert.That(layout.Setup!.Bbox!.MinZ).IsEqualTo(seed.Setup!.Bbox!.MinZ);
        await Assert.That(layout.Setup!.Bbox!.MaxZ).IsEqualTo(seed.Setup!.Bbox!.MaxZ);
    }

    // ── intent: teams/spawns/build/observer exact on the two exact seeds ─────────────────────────────────

    [Test]
    [Arguments("base-2island")]
    [Arguments("base-4team")]
    public async Task Compiled_intent_matches_the_seed_exactly(string name)
    {
        var (_, intent) = Compile(name);
        var seed = PlanTestSupport.LoadIntent($"{name}.intent.json");
        await AssertTeamsSpawnsBuildObserver(intent, seed);

        // wools: same set of (owner, colour, spawn point)
        var got = intent.Wools!.Select(w => (w.Owner, w.Color, PlanTestSupport.T(w.Spawn))).ToHashSet();
        var want = seed.Wools!.Select(w => (w.Owner, w.Color, PlanTestSupport.T(w.Spawn))).ToHashSet();
        await Assert.That(got.SetEquals(want)).IsTrue();
    }

    [Test]
    public async Task Compiled_2wool_intent_matches_structurally()
    {
        var (_, intent) = Compile("base-2wool");
        var seed = PlanTestSupport.LoadIntent("base-2wool.intent.json");
        await AssertTeamsSpawnsBuildObserver(intent, seed);

        // wools: owner + XZ position match as a set (the y and non-team dye strings are documented deviations)
        var got = intent.Wools!.Select(w => (w.Owner, w.Spawn.X, w.Spawn.Z)).ToHashSet();
        var want = seed.Wools!.Select(w => (w.Owner, w.Spawn.X, w.Spawn.Z)).ToHashSet();
        await Assert.That(got.SetEquals(want)).IsTrue();

        // colours are distinct (unique wool ids) and each team's first wool takes the team colour
        await Assert.That(intent.Wools!.Select(w => w.Color).Distinct().Count()).IsEqualTo(intent.Wools!.Count);
        await Assert.That(intent.Wools!.Any(w => w.Owner == "red" && w.Color == "red")).IsTrue();
        await Assert.That(intent.Wools!.Any(w => w.Owner == "blue" && w.Color == "blue")).IsTrue();
        // the concrete dye choices reproduce the seed's hand-authored orange / light_blue
        await Assert.That(intent.Wools!.Any(w => w.Color == "orange")).IsTrue();
        await Assert.That(intent.Wools!.Any(w => w.Color == "light_blue")).IsTrue();
    }

    private static async Task AssertTeamsSpawnsBuildObserver(MapIntent intent, MapIntent seed)
    {
        await Assert.That(intent.Teams!.Select(t => (t.Id, t.Name, t.Color)).ToList())
            .IsEquivalentTo(seed.Teams!.Select(t => (t.Id, t.Name, t.Color)).ToList());
        await Assert.That(intent.MaxPlayers).IsEqualTo(seed.MaxPlayers);

        var gotSpawns = intent.Spawns.Select(s => (s.Team, PlanTestSupport.T(s.Point), s.Yaw)).ToHashSet();
        var wantSpawns = seed.Spawns.Select(s => (s.Team, PlanTestSupport.T(s.Point), s.Yaw)).ToHashSet();
        await Assert.That(gotSpawns.SetEquals(wantSpawns)).IsTrue();

        await Assert.That(intent.Build!.MaxHeight).IsEqualTo(seed.Build!.MaxHeight);
        var gotAreas = intent.Build!.Areas.Select(PlanTestSupport.R).ToHashSet();
        var wantAreas = seed.Build!.Areas.Select(PlanTestSupport.R).ToHashSet();
        await Assert.That(gotAreas.SetEquals(wantAreas)).IsTrue();

        await Assert.That(PlanTestSupport.T(intent.Observer!.Point)).IsEqualTo(PlanTestSupport.T(seed.Observer!.Point));
        await Assert.That(intent.Observer!.Yaw).IsEqualTo(seed.Observer!.Yaw);
    }
}
