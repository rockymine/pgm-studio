using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Analysis.Region;
using PgmStudio.Contracts;
using PgmStudio.Domain;

namespace PgmStudio.Api.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// <c>RegionTreeDto</c> says what <c>GET /map/{slug}/regions/tree</c> answers, and the tree it describes is
/// built one project down in <c>Analysis</c>, which cannot see <c>Contracts</c>. Nothing in the compiler
/// connects the two, so this does: the encoder's real output is deserialized into the record with unmapped
/// members <b>disallowed</b>, which fails the moment the encoder writes a key the record has no field for.
///
/// <para>The board is built to exercise the shape's awkward corners rather than to be realistic — a nested
/// child so <c>children</c> recurses, a mirror so <c>source</c> is a node rather than an id, a half-space so
/// <c>bounds</c> carries the string <c>oo</c> that a numeric field would have thrown on, and a wired region
/// so <c>wiring</c> and <c>polygon_2d</c> are populated.</para>
/// </summary>
public sealed class RegionTreeShapeTests
{
    private static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static Dict Box(double minX, double minZ, double maxX, double maxZ) => new()
    {
        ["min"] = new Dict { ["x"] = minX, ["z"] = minZ },
        ["max"] = new Dict { ["x"] = maxX, ["z"] = maxZ },
    };

    private static (Dict Regions, Dictionary<string, string> Categories,
                    Dictionary<string, RegionFacet> Facets, Dictionary<string, string> Drafts) Board()
    {
        var regions = new Dict
        {
            ["pad"] = new Dict
            {
                ["id"] = "pad", ["type"] = "rectangle", ["bounds_2d"] = Box(0, 0, 10, 10),
                ["min"] = new Dict { ["x"] = 0.0, ["z"] = 0.0 }, ["max"] = new Dict { ["x"] = 10.0, ["z"] = 10.0 },
            },
            ["zone"] = new Dict
            {
                ["id"] = "zone", ["type"] = "union", ["children"] = new List<object?> { "pad" },
                ["bounds_2d"] = Box(0, 0, 10, 10),
            },
            ["far-side"] = new Dict
            {
                ["id"] = "far-side", ["type"] = "mirror", ["source_id"] = "pad",
                ["origin"] = new Dict { ["x"] = 0.0, ["y"] = 0.0, ["z"] = 0.0 },
                ["normal"] = new Dict { ["x"] = 1.0, ["y"] = 0.0, ["z"] = 0.0 },
            },
            // A half-space is unbounded on one side, and states it as the string the map.xml attribute uses.
            ["north"] = new Dict
            {
                ["id"] = "north", ["type"] = "half",
                ["bounds_2d"] = new Dict
                {
                    ["min"] = new Dict { ["x"] = "-oo", ["z"] = "-oo" },
                    ["max"] = new Dict { ["x"] = "oo", ["z"] = 0.0 },
                },
                ["origin"] = new Dict { ["x"] = 0.0, ["y"] = 0.0, ["z"] = 0.0 },
                ["normal"] = new Dict { ["x"] = 0.0, ["y"] = 0.0, ["z"] = 1.0 },
            },
        };
        var categories = new Dictionary<string, string>
        {
            ["zone"] = "build", ["far-side"] = "build", ["north"] = "other",
        };
        var facets = new Dictionary<string, RegionFacet>
        {
            ["zone"] = new("build", ["enter=only-blue", "rule_container"]),
            ["pad"] = new("build", []),
            ["far-side"] = new("build", [], "protection"),
            ["north"] = new("other", []),
        };
        var drafts = new Dictionary<string, string> { ["far-side"] = "teams" };
        return (regions, categories, facets, drafts);
    }

    /// <summary>Every key the encoder writes has a field on the record. An added key fails here rather than
    /// arriving on the wire under a schema that does not mention it.</summary>
    [Test]
    public async Task The_record_carries_every_key_the_encoder_writes()
    {
        var (regions, categories, facets, drafts) = Board();
        var groups = RegionAuthoringEncoder.EncodeTree(regions, categories, (0, 0, 10, 10), facets, drafts);
        var body = new Dict
        {
            ["groups"] = groups,
            ["bounding_box"] = new Dict { ["min_x"] = 0.0, ["min_z"] = 0.0, ["max_x"] = 10.0, ["max_z"] = 10.0 },
        };

        var tree = JsonSerializer.Deserialize<RegionTreeDto>(JsonSerializer.Serialize(body, Strict), Strict);

        await Assert.That(tree).IsNotNull();
        await Assert.That(tree!.BoundingBox!.MaxX).IsEqualTo(10.0);
        await Assert.That(tree.Groups.Select(g => g.Name)).Contains("build");
    }

    /// <summary>And the corners the record exists to get right: a nested child, a transform source encoded as
    /// a node, an unbounded side crossing as a string, and the wiring a region is reached through.</summary>
    [Test]
    public async Task The_awkward_corners_read_back_as_the_record_describes_them()
    {
        var (regions, categories, facets, drafts) = Board();
        var groups = RegionAuthoringEncoder.EncodeTree(regions, categories, (0, 0, 10, 10), facets, drafts);
        var body = new Dict { ["groups"] = groups, ["bounding_box"] = null };

        var tree = JsonSerializer.Deserialize<RegionTreeDto>(JsonSerializer.Serialize(body, Strict), Strict)!;
        var nodes = tree.Groups.SelectMany(g => g.Regions).ToList();

        var zone = nodes.Single(n => n.Id == "zone");
        await Assert.That(zone.Children.Single().Id).IsEqualTo("pad");
        await Assert.That(zone.Wiring).IsEquivalentTo(new[] { "enter=only-blue" });

        var mirror = nodes.Single(n => n.Id == "far-side");
        await Assert.That(mirror.Source!.Id).IsEqualTo("pad");
        await Assert.That(mirror.Subtype).IsEqualTo("protection");
        await Assert.That(mirror.DraftStep).IsEqualTo("teams");

        // The half-space's unbounded side is the string the attribute spells, not a number.
        var half = nodes.Single(n => n.Id == "north");
        await Assert.That(half.Bounds!.MinX.ValueKind).IsEqualTo(JsonValueKind.String);
        await Assert.That(half.Bounds.MinX.GetString()).IsEqualTo("-oo");

        // coords is keyed by what the type is made of, which is why it is an open object.
        await Assert.That(zone.Coords).IsNull();
        await Assert.That(mirror.Coords!.Keys).Contains("normal_x");
    }
}
