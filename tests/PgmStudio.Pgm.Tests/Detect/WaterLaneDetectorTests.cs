using PgmStudio.Pgm.Detect;

namespace PgmStudio.Pgm.Tests.Detect;

/// <summary>
/// Water-lane detection (docs/contracts/water-lanes.md): the four wirings, the <c>y=0</c> test that separates
/// a lane from any other water, and the ranking that reports one lane once.
/// <para>Every fixture is a synthetic <c>map.xml</c> pushed through the real parser, because two of the four
/// wirings are only visible through parser surface (<c>&lt;include&gt;</c>, <c>&lt;fill&gt;</c>) and a
/// hand-built registry would not exercise it.</para>
/// </summary>
public sealed class WaterLaneDetectorTests
{
    // A minimal loadable CTW map: proto in range, one wool with a monument, plus whatever the test adds.
    private static string Map(string body) => $"""
        <map proto="1.5.0">
          <name>Lane Fixture</name>
          <version>1.0.0</version>
          {body}
        </map>
        """;

    private static IReadOnlyList<WaterLane> Detect(string body) =>
        WaterLaneDetector.Detect(MapParser.ParseXmlString(Map(body)));

    [Test]
    public async Task An_include_with_the_matching_region_is_the_newest_form()
    {
        var lanes = Detect("""
          <include id="gapple-kill-reward"/>
          <include id="water-lanes"/>
          <regions>
            <union id="water-lanes">
              <cuboid id="blue-lane" min="20,0,10" max="40,1,25"/>
              <cuboid id="red-lane" min="20,0,-25" max="40,1,-10"/>
            </union>
          </regions>
          """);

        await Assert.That(lanes.Count).IsEqualTo(1);
        await Assert.That(lanes[0].Form).IsEqualTo(WaterLaneForm.Include);
        await Assert.That(lanes[0].RegionId).IsEqualTo("water-lanes");
        await Assert.That(lanes[0].Footprint.Count).IsEqualTo(2);
        // The fragment owns the timing but exposes it as a fallback constant, so a map that names none
        // still has a knowable answer: the fragment's default.
        await Assert.That(lanes[0].Trigger).IsEqualTo(WaterLaneDetector.LaneTimerDefault);
    }

    [Test]
    public async Task A_map_that_sets_the_lane_timer_overrides_the_fragment_default()
    {
        // `water-lane-timer` is declared `fallback` by the fragment, so the map's own value wins — and the
        // map never interpolates it, which is why the constant has to survive substitution to be seen at all.
        var lanes = Detect("""
          <include id="water-lanes"/>
          <constants><constant id="water-lane-timer">30m</constant></constants>
          <regions><cuboid id="water-lanes" min="0,0,0" max="10,1,10"/></regions>
          """);

        await Assert.That(lanes.Single().Trigger).IsEqualTo("30m");
    }

    [Test]
    public async Task The_include_alone_applies_to_nothing_and_is_not_a_lane()
    {
        var lanes = Detect("""
          <include id="water-lanes"/>
          <regions><cuboid id="somewhere-else" min="0,60,0" max="10,61,10"/></regions>
          """);

        await Assert.That(lanes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_water_fill_over_the_void_layer_is_a_lane_and_reports_when_it_fires()
    {
        var lanes = Detect("""
          <regions>
            <union id="lanes">
              <cuboid id="west" min="42,0,-65" max="62,1,-56"/>
              <cuboid id="east" min="42,0,59" max="62,1,68"/>
            </union>
          </regions>
          <actions>
            <trigger scope="match" filter="add-side-lanes">
              <action><fill region="lanes" material="water" filter="only-air"/></action>
            </trigger>
          </actions>
          """);

        await Assert.That(lanes.Count).IsEqualTo(1);
        await Assert.That(lanes[0].Form).IsEqualTo(WaterLaneForm.Fill);
        // The trigger's condition, not the fill's own block guard — `only-air` says what may be overwritten.
        await Assert.That(lanes[0].Trigger).IsEqualTo("add-side-lanes");
    }

    [Test]
    public async Task A_fill_declared_apart_from_its_trigger_still_reports_the_trigger()
    {
        var lanes = Detect("""
          <regions><cuboid id="lanes" min="0,0,0" max="10,1,10"/></regions>
          <actions>
            <action id="open-lanes"><fill region="lanes" material="water"/></action>
            <trigger scope="match" action="open-lanes" filter="half-time"/>
          </actions>
          """);

        await Assert.That(lanes.Count).IsEqualTo(1);
        await Assert.That(lanes[0].Trigger).IsEqualTo("half-time");
    }

    [Test]
    public async Task Water_above_the_void_layer_is_not_a_lane()
    {
        // A decorative pool or a flag-status indicator fills with water and opens no route: the columns under
        // it were never void, so the void rule never applied to them.
        var lanes = Detect("""
          <regions><union id="eyes"><block location="-52,21,-2"/><block location="-52,21,1"/></union></regions>
          <actions><fill id="wet" region="eyes" material="water"/></actions>
          """);

        await Assert.That(lanes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_hidden_destroyable_swapped_to_water_by_a_mode_is_the_oldest_form()
    {
        var lanes = Detect("""
          <regions><cuboid id="mid-gap" min="115,0,-73" max="122,1,-65"/></regions>
          <destroyables materials="air" completion="0%" required="false" show="false" modes="water-mode">
            <destroyable name="water-lane" owner="yellow-team" region="mid-gap"/>
          </destroyables>
          <modes>
            <mode id="air-mode" after="0s" material="air"/>
            <mode id="water-mode" after="15m" material="water"/>
          </modes>
          """);

        await Assert.That(lanes.Count).IsEqualTo(1);
        await Assert.That(lanes[0].Form).IsEqualTo(WaterLaneForm.Phantom);
        await Assert.That(lanes[0].Trigger).IsEqualTo("15m");
    }

    [Test]
    public async Task A_hidden_destroyable_swapped_to_something_else_is_not_a_lane()
    {
        // The same element scripts other world changes — a build floor crumbling to air is not a route.
        var lanes = Detect("""
          <regions><cuboid id="floor" min="86,0,-169" max="232,1,-23"/></regions>
          <destroyables materials="stained glass" show="false" modes="air-mode">
            <destroyable name="build-regions" owner="yellow-team" region="floor"/>
          </destroyables>
          <modes><mode id="air-mode" after="0s" material="air"/></modes>
          """);

        await Assert.That(lanes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_visible_destroyable_is_a_goal_not_a_lane()
    {
        var lanes = Detect("""
          <regions><cuboid id="pillar" min="0,0,0" max="3,1,3"/></regions>
          <destroyables materials="obsidian" modes="water-mode">
            <destroyable name="Red Monument" owner="red-team" region="pillar"/>
          </destroyables>
          <modes><mode id="water-mode" after="15m" material="water"/></modes>
          """);

        await Assert.That(lanes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_lane_named_region_that_nothing_drives_is_the_static_form()
    {
        // Water already stands in these columns; the id keeps the void rule off them so draining does not make
        // the ground unbuildable. Nothing opens, so it is its own form rather than one of the three that do.
        var lanes = Detect("""
          <include id="gapple-kill-reward"/>
          <regions>
            <union id="water-lanes">
              <rectangle id="cyan-left" min="-34,32" max="-26,20"/>
              <rectangle id="cyan-right" min="27,32" max="35,20"/>
            </union>
            <negative id="void-area"><region id="water-lanes"/></negative>
            <apply block="deny(void)" region="void-area"/>
          </regions>
          """);

        await Assert.That(lanes.Count).IsEqualTo(1);
        await Assert.That(lanes[0].Form).IsEqualTo(WaterLaneForm.Static);
        await Assert.That(lanes[0].Footprint.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_lane_union_and_its_lane_named_leaves_report_once()
    {
        var lanes = Detect("""
          <regions>
            <union id="water-lanes">
              <union id="blue-water-lanes">
                <cuboid id="lime-water-lane" min="-80,0,-28" max="-95,1,-47"/>
                <cuboid id="cyan-water-lane" min="-80,0,29" max="-95,1,48"/>
              </union>
            </union>
          </regions>
          """);

        await Assert.That(lanes.Count).IsEqualTo(1);
        await Assert.That(lanes[0].RegionId).IsEqualTo("water-lanes");
        await Assert.That(lanes[0].Footprint.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_filled_lane_is_not_also_reported_as_static()
    {
        // The strongest wiring claims the ground: the same region carries both the convention's name and the
        // fill that drives it, and it is one lane.
        var lanes = Detect("""
          <regions>
            <union id="water-lanes"><cuboid id="west-water-lane" min="0,0,0" max="10,1,10"/></union>
          </regions>
          <actions><trigger scope="match" filter="go"><action><fill region="water-lanes" material="water"/></action></trigger></actions>
          """);

        await Assert.That(lanes.Count).IsEqualTo(1);
        await Assert.That(lanes[0].Form).IsEqualTo(WaterLaneForm.Fill);
    }

    [Test]
    public async Task A_flat_companion_region_over_the_same_ground_is_not_a_second_lane()
    {
        // A lane commonly carries a 2-D twin used to grant building there; it is the same lane described twice.
        var lanes = Detect("""
          <regions>
            <union id="water-lanes"><cuboid id="lime-water-lane" min="-80,0,-28" max="-95,1,-47"/></union>
            <union id="building-water-lanes"><rectangle id="lime-building-water-lane" min="-80,-28" max="-95,-47"/></union>
          </regions>
          <actions><trigger scope="match" filter="go"><action><fill region="water-lanes" material="water"/></action></trigger></actions>
          """);

        await Assert.That(lanes.Count).IsEqualTo(1);
        await Assert.That(lanes[0].RegionId).IsEqualTo("water-lanes");
    }

    [Test]
    public async Task A_mirrored_lane_is_a_separate_place()
    {
        // Reflecting a lane produces a second lane elsewhere, so the footprint counts both — following the
        // mirror back to its source and stopping there would report one.
        var lanes = Detect("""
          <include id="water-lanes"/>
          <regions>
            <union id="water-lanes">
              <cuboid id="orange-water-lane" min="-185,0,-169" max="-171,1,-116"/>
              <mirror id="yellow-water-lane" region="orange-water-lane" origin="-113.5,0,-49.5" normal="1,0,0"/>
            </union>
          </regions>
          """);

        await Assert.That(lanes[0].Footprint.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_map_with_no_lanes_reports_none()
    {
        var lanes = Detect("""
          <include id="gapple-kill-reward"/>
          <regions><rectangle id="middle" min="35,12" max="-34,-11"/></regions>
          """);

        await Assert.That(lanes.Count).IsEqualTo(0);
        await Assert.That(WaterLaneDetector.Any(MapParser.ParseXmlString(Map("<regions/>")))).IsFalse();
    }
}
