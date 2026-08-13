using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Detect;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Water-lane slice of the declarative generator (docs/pgm/water-lanes.md): the authored footprints
/// become one <c>y=0</c> union under the agreed id, and the map pulls in the fragment that gives it
/// behaviour. The closing assertion is the round trip — what the generator emits is what the detector reads
/// back as the newest form.
/// </summary>
public sealed class WaterLaneGeneratorTests
{
    private static Dict Map() => new()
    {
        ["regions"] = new Dict(), ["filters"] = new Dict(), ["apply_rules"] = new List<object?>(),
    };

    private static MapIntent Intent(params Rect[] rects) => new()
    {
        WaterLanes = new WaterLaneIntent { Rects = [.. rects] },
    };

    private static Dict Regions(Dict doc) => (Dict)doc["regions"]!;

    [Test]
    public async Task Emits_a_union_of_void_layer_cuboids_under_the_agreed_id()
    {
        var doc = Map();
        WaterLaneGenerator.Apply(doc, Intent(new Rect(20, 10, 40, 25), new Rect(20, -25, 40, -10)));

        var union = (Dict)Regions(doc)["water-lanes"]!;
        await Assert.That(union["type"]).IsEqualTo("union");
        await Assert.That(((List<object?>)union["children"]!).Count).IsEqualTo(2);

        var lane = (Dict)Regions(doc)["water-lanes-1"]!;
        await Assert.That(lane["type"]).IsEqualTo("cuboid");
        // The single block layer the void filter reads: PGM cuboid bounds are half-open, so 0..1 is y=0 alone.
        await Assert.That(((Dict)lane["min"]!)["y"]).IsEqualTo(0d);
        await Assert.That(((Dict)lane["max"]!)["y"]).IsEqualTo(1d);
    }

    [Test]
    public async Task A_single_lane_is_the_cuboid_itself_under_the_agreed_id()
    {
        // The fragment resolves an id, not a type, so one lane needs no union to wrap it.
        var doc = Map();
        WaterLaneGenerator.Apply(doc, Intent(new Rect(0, 0, 10, 10)));

        var lane = (Dict)Regions(doc)["water-lanes"]!;
        await Assert.That(lane["type"]).IsEqualTo("cuboid");
        await Assert.That(Regions(doc).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Emits_nothing_when_no_lane_is_authored()
    {
        var doc = Map();
        WaterLaneGenerator.Apply(doc, new MapIntent());

        await Assert.That(Regions(doc).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Re_applying_replaces_rather_than_accumulates()
    {
        var doc = Map();
        WaterLaneGenerator.Apply(doc, Intent(new Rect(0, 0, 10, 10), new Rect(20, 20, 30, 30)));
        WaterLaneGenerator.Apply(doc, Intent(new Rect(0, 0, 10, 10)));

        // Two lanes down to one: the numbered cuboids and the union that grouped them are both gone, leaving
        // the lone lane under the bare id.
        await Assert.That(Regions(doc).Keys).IsEquivalentTo(new[] { "water-lanes" });
        await Assert.That(((Dict)Regions(doc)["water-lanes"]!)["type"]).IsEqualTo("cuboid");
    }

    [Test]
    public async Task The_include_follows_the_region_in_both_directions()
    {
        var map = new MapXml();
        map.Regions["water-lanes"] = new Region { Id = "water-lanes", Type = "union", Children = ["water-lanes-1"] };
        WaterLaneGenerator.EnsureInclude(map);
        await Assert.That(map.Includes).Contains("water-lanes");

        // A region with no include is inert geometry, and an include with no region resolves to nothing — so
        // dropping the lanes drops the include with them.
        map.Regions.Remove("water-lanes");
        WaterLaneGenerator.EnsureInclude(map);
        await Assert.That(map.Includes).DoesNotContain("water-lanes");
    }

    [Test]
    public async Task A_lane_is_never_added_to_the_buildable_region()
    {
        // The lane must stay outside the build area or the void rule opens it from the first tick, which is
        // the one thing a lane must not be.
        var doc = Map();
        var intent = new MapIntent
        {
            Build = new BuildIntent { MaxHeight = 24, Areas = [new Rect(-10, -10, 10, 10)] },
            WaterLanes = new WaterLaneIntent { Rects = [new Rect(40, 40, 60, 60)] },
        };
        WaterLaneGenerator.Apply(doc, intent);
        BuildGenerator.Apply(doc, intent);

        // The lane's ground is nowhere in the buildable region, so the void rule still covers it at kickoff.
        await Assert.That(Regions(doc).Keys.Count(k => k.StartsWith("build-area-"))).IsEqualTo(1);
        var buildArea = (Dict)Regions(doc)["build-area-1"]!;
        await Assert.That(((Dict)((Dict)buildArea["bounds_2d"]!)["min"]!)["x"]).IsEqualTo(-10d);

        var lane = (Dict)Regions(doc)["water-lanes"]!;
        await Assert.That(((Dict)lane["min"]!)["x"]).IsEqualTo(40d);
    }

    [Test]
    public async Task What_the_generator_emits_reads_back_as_the_newest_form()
    {
        var doc = Map();
        WaterLaneGenerator.Apply(doc, Intent(new Rect(20, 10, 40, 25), new Rect(20, -25, 40, -10)));

        var map = Deserializer.FromDict(doc);
        WaterLaneGenerator.EnsureInclude(map);
        var reparsed = MapParser.ParseXmlString(XmlWriter.ToXml(map));

        var lanes = WaterLaneDetector.Detect(reparsed);
        await Assert.That(lanes.Count).IsEqualTo(1);
        await Assert.That(lanes[0].Form).IsEqualTo(WaterLaneForm.Include);
        await Assert.That(lanes[0].Footprint.Count).IsEqualTo(2);
    }
}
