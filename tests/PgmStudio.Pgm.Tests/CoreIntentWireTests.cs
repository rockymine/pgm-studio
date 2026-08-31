using System.Text.Json;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The JSON contract between the Configure wizard's Cores phase and <see cref="CoreIntent"/>. The wizard
/// edits the intent as raw JSON in the browser and PUTs the whole object back, so nothing in the type system
/// connects the keys it writes to the properties read here — a renamed or mis-cased key deserializes to a
/// default in silence, and the failure surfaces only as a core that exports at the wrong height or does not
/// export at all.
///
/// <para>The literal below is the object <c>CoreAuthoring.WriteCores</c> emits, so this fails the moment the
/// two sides drift.</para>
/// </summary>
public sealed class CoreIntentWireTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string ConfigureWrites = """
        {
          "cores": [
            {
              "owner": "red",
              "name": "Red Core",
              "anchor": { "x": 10.5, "y": 12, "z": 20.5 },
              "lava": 5,
              "lavaHeight": 2,
              "float": 6,
              "leak": 9,
              "openTop": true,
              "box": { "minX": 8, "minY": 18, "minZ": 18, "maxX": 14, "maxY": 21, "maxZ": 24 }
            }
          ]
        }
        """;

    private static CoreIntent Parse()
        => JsonSerializer.Deserialize<MapIntent>(ConfigureWrites, Web)!.Cores!.Single();

    [Test]
    public async Task Every_casing_field_the_wizard_writes_arrives_on_the_intent()
    {
        var core = Parse();
        await Assert.That(core.Owner).IsEqualTo("red");
        await Assert.That(core.Name).IsEqualTo("Red Core");
        await Assert.That((core.Anchor.X, core.Anchor.Y, core.Anchor.Z)).IsEqualTo((10.5, 12d, 20.5));
        await Assert.That(core.Lava).IsEqualTo(5);
        await Assert.That(core.LavaHeight).IsEqualTo(2);
        // and the casing they imply, which is what the stamper fills: an open top gives up the cap course,
        // so two courses of lava stand under three of obsidian rather than four.
        await Assert.That((core.Size, core.Height, core.Shell)).IsEqualTo((7, 3, 1));
        await Assert.That(core.Float).IsEqualTo(6);
        await Assert.That(core.Leak).IsEqualTo(9);
        await Assert.That(core.OpenTop).IsTrue();
        // leak 9 over float 6 — capturing this core means digging four blocks out from under it (DC2).
        await Assert.That(core.DigDepth).IsEqualTo(4);
    }

    [Test]
    public async Task The_confirmed_casing_box_becomes_the_cores_region()
    {
        // A confirmed suggestion carries the volume the detector measured, and that volume is what scopes the
        // goal (OB8). The max is one past the last block, so the casing's far face is inside its own region.
        var doc = new Dict();
        CoreGenerator.Apply(doc, new MapIntent { Cores = [Parse()] });

        var core = (Dict)((List<object?>)doc["cores"]!)[0]!;
        var region = (Dict)((Dict)doc["regions"]!)[(string)core["region"]!]!;
        var min = (Dict)region["min"]!;
        var max = (Dict)region["max"]!;

        await Assert.That(((double)min["x"]!, (double)min["y"]!, (double)min["z"]!)).IsEqualTo((8d, 18d, 18d));
        await Assert.That(((double)max["x"]!, (double)max["y"]!, (double)max["z"]!)).IsEqualTo((15d, 22d, 25d));
    }

    [Test]
    public async Task A_core_the_world_build_has_not_stamped_yet_emits_nothing()
    {
        // The wizard writes box:null for a plan-authored core, whose casing is placed against terrain that
        // does not exist until the world is built. Emitting a core with no region would scope its goal to
        // nothing, which PGM accepts and then reports as a zero-health objective.
        var intent = JsonSerializer.Deserialize<MapIntent>(
            """{ "cores": [ { "owner": "blue", "anchor": { "x": 1, "y": 2, "z": 3 }, "box": null } ] }""", Web)!;
        await Assert.That(intent.Cores!.Single().Box).IsNull();

        var doc = new Dict();
        CoreGenerator.Apply(doc, intent);
        await Assert.That((List<object?>)doc["cores"]!).IsEmpty();
    }

    [Test]
    public async Task Wools_and_cores_ride_on_one_intent_together()
    {
        // A map may carry more than one gamemode, so the two slices are independent: reading cores must not
        // depend on there being wools, and vice versa.
        var intent = JsonSerializer.Deserialize<MapIntent>("""
            {
              "wools": [ { "owner": "red", "color": "blue", "spawn": { "x": 1, "y": 2, "z": 3 } } ],
              "cores": [ { "owner": "blue", "anchor": { "x": 4, "y": 5, "z": 6 }, "lava": 3, "lavaHeight": 3,
                           "box": { "minX": 0, "minY": 10, "minZ": 0, "maxX": 4, "maxY": 14, "maxZ": 4 } } ]
            }
            """, Web)!;

        await Assert.That(intent.Wools!.Single().Color).IsEqualTo("blue");
        await Assert.That(intent.Cores!.Single().Owner).IsEqualTo("blue");
    }
}
