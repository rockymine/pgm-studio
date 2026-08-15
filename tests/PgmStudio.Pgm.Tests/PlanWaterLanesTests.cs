using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The water-lane zone kind (docs/pgm/water-lanes.md): a zone that opens mid-match rather than at the
/// first tick. The tests hold the line that separates it from a build zone — it compiles to its own intent,
/// stays out of the buildable region, and takes no part in the starting board's connectivity.
/// </summary>
public sealed class PlanWaterLanesTests
{
    private static PlanModel Plan(string json) => PlanModel.Parse(json)!;

    private const string Unit = """
        "pieces":[ {"id":"lane","rect":[1,5,2,6]}, {"id":"wr","role":"wool-room","rect":[-3,5,2,2]} ],
        "placements":{ "spawns":[ {"piece":"lane","at":[1,5],"facing":"front"} ],
                       "wools":[ {"piece":"wr","at":[1,1]} ] }
        """;

    private static PlanModel WithZones(string zones) =>
        Plan($$"""{ "plan":1, "globals":{"symmetry":"rot_180"}, "zones":{{zones}}, {{Unit}} }""");

    [Test]
    public async Task A_zone_with_no_kind_is_a_build_zone()
    {
        // Every zone authored before the kind existed has to keep reading exactly as it did.
        var plan = WithZones("""[ {"id":"mid","rect":[-3,-5,6,10]} ]""");

        await Assert.That(plan.Zones.Single().KindOrDefault).IsEqualTo(PlanZoneKinds.Build);
        await Assert.That(plan.BuildZones.Count()).IsEqualTo(1);
        await Assert.That(plan.WaterLanes.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task An_unknown_kind_folds_to_build()
    {
        var plan = WithZones("""[ {"id":"mid","kind":"trapdoor","rect":[-3,-5,6,10]} ]""");
        await Assert.That(plan.Zones.Single().KindOrDefault).IsEqualTo(PlanZoneKinds.Build);
    }

    [Test]
    public async Task A_water_lane_zone_round_trips_its_kind()
    {
        var plan = WithZones("""[ {"id":"far","kind":"water-lane","rect":[3,-2,2,4]} ]""");
        var reloaded = Plan(plan.ToJson());

        await Assert.That(reloaded.Zones.Single().KindOrDefault).IsEqualTo(PlanZoneKinds.WaterLane);
        await Assert.That(reloaded.WaterLanes.Single().Id).IsEqualTo("far");
    }

    [Test]
    public async Task A_water_lane_compiles_to_the_lane_intent_and_not_the_build_intent()
    {
        var plan = WithZones("""
            [ {"id":"mid","rect":[-3,-5,6,10]}, {"id":"far","kind":"water-lane","rect":[3,-2,2,4]} ]
            """);
        var (_, intent) = PlanCompiler.Compile(plan);

        // The build areas are the mid band's two orbit images; the lane is nowhere among them.
        await Assert.That(intent.Build!.Areas.Count).IsEqualTo(1);   // the band straddles the centre, so it self-maps
        await Assert.That(intent.WaterLanes!.Rects.Count).IsEqualTo(2);
        await Assert.That(intent.Build.Areas.Any(a => a.MinX >= 15)).IsFalse();
    }

    [Test]
    public async Task A_plan_with_no_lane_carries_no_lane_intent()
    {
        var (_, intent) = PlanCompiler.Compile(WithZones("""[ {"id":"mid","rect":[-3,-5,6,10]} ]"""));
        await Assert.That(intent.WaterLanes).IsNull();
    }

    [Test]
    public async Task A_lane_fans_by_symmetry_like_a_build_area()
    {
        var (_, intent) = PlanCompiler.Compile(WithZones("""[ {"id":"far","kind":"water-lane","rect":[3,-2,2,4]} ]"""));

        // rect [3,-2,2,4] at cell 5 → blocks x 15..25, z -10..10; rotated 180° → x -25..-15, z -10..10.
        var lanes = intent.WaterLanes!.Rects.OrderBy(r => r.MinX).ToList();
        await Assert.That(lanes.Count).IsEqualTo(2);
        await Assert.That((lanes[0].MinX, lanes[0].MaxX)).IsEqualTo((-25d, -15d));
        await Assert.That((lanes[1].MinX, lanes[1].MaxX)).IsEqualTo((15d, 25d));
    }

    [Test]
    public async Task A_lane_over_terrain_lints_WL1()
    {
        // A lane opens void. Over a piece the columns already hold terrain, so that part of the rect adds
        // nothing and the drawing overstates the route.
        var plan = WithZones("""[ {"id":"far","kind":"water-lane","rect":[1,5,2,6]} ]""");
        var findings = PlanValidator.Check(plan);

        await Assert.That(findings.Any(f => f.Rule == "WL1" && f.SubjectIds.Contains("far"))).IsTrue();
    }

    [Test]
    public async Task A_build_zone_over_terrain_does_not_lint_WL1()
    {
        // Build areas may overlap terrain by design, so the rule is the lane's alone.
        var plan = WithZones("""[ {"id":"mid","rect":[1,5,2,6]} ]""");
        await Assert.That(PlanValidator.Check(plan).Any(f => f.Rule == "WL1")).IsFalse();
    }
}
