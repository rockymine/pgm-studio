using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

/// <summary>
/// <b>A made thing and a built thing do not read each other, so the world holds one inside the other.</b> A
/// layer stating <c>kind: "made"</c> is laid by the rasterizer before anything is stamped, and every stamper
/// seats on the terrain's surface — which is every column's top with the made things taken out. Neither half
/// asks about the other, their blocks interleave in the columns they share, and nothing declines.
///
/// <para><b>A shared column is not the fault; a shared course is.</b> A gantry over a shed and a beacon frame
/// forty courses above a monument both stand in the monument's columns, and only one of them is in its way —
/// so the read is the made thing's own span against the solid run rising off the terrain.</para>
///
/// <para>Driven over the committed 2-island seed, whose spawns stand at (10, 50) and (−10, −50): a made thing
/// drawn down into one of them is `SK18`, the same thing drawn clear above it is not, and neither is one out
/// over the water.</para>
/// </summary>
public sealed class MadeThingInBuiltTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static (string Layout, MapIntent Intent) Seed(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "seeds", $"{name}.layout.json")))
            dir = dir.Parent;
        if (dir is null) throw new FileNotFoundException($"{name} seed not found above " + AppContext.BaseDirectory);
        var seeds = Path.Combine(dir.FullName, "tools", "seeds");
        return (File.ReadAllText(Path.Combine(seeds, $"{name}.layout.json")),
                JsonSerializer.Deserialize<MapIntent>(File.ReadAllText(Path.Combine(seeds, $"{name}.intent.json")), Web)!);
    }

    /// <summary>The seed's layout with an eight-block envelope over (x, z) at <paramref name="floor"/> — a
    /// balloon, drawn the way `opus5-slipway` draws one: its own layer, `kind: "made"`, at an absolute
    /// floor.</summary>
    private static string WithBalloon(string layout, int x, int z, int floor)
    {
        var doc = JsonNode.Parse(layout)!.AsObject();
        doc["layers"]!.AsArray().Add(new JsonObject
        {
            ["id"] = "balloon",
            ["name"] = "balloon",
            ["kind"] = SketchLayer.MadeKind,
            ["base_y"] = 0,
            ["layout"] = new JsonObject
            {
                ["shapes"] = new JsonArray(new JsonObject
                {
                    ["id"] = "envelope",
                    ["type"] = "rectangle",
                    ["operation"] = "add",
                    ["min_x"] = x - 4, ["min_z"] = z - 4, ["max_x"] = x + 4, ["max_z"] = z + 4,
                    ["floor"] = floor,
                    ["base_height"] = 8,
                }),
            },
        });
        return doc.ToJsonString();
    }

    /// <summary>The island's terrain tops out at y8 and the spawn standing on it reaches y16, so an envelope
    /// floored at y10 holds courses the structure holds.</summary>
    [Test]
    public async Task A_made_thing_standing_in_a_stamped_structure_is_named()
    {
        var (layout, intent) = Seed("base-2island");
        var built = WorldBuilder.Build(WithBalloon(layout, 10, 50, floor: 10), intent);

        var shared = built.Declines.Where(finding => finding.Rule == SketchRules.MadeThingInBuilt).ToList();
        await Assert.That(shared).IsNotEmpty();
        await Assert.That(shared[0].Severity).IsEqualTo(Severity.Complaint);   // the world exists, as it built
        await Assert.That(shared[0].Message).Contains("balloon");
        await Assert.That(shared[0].Subjects!).Contains("balloon");
    }

    /// <summary><b>The one this rule was getting wrong.</b> The same envelope over the same spawn, floored at
    /// y40 — clear of the y16 the structure reaches. Every column is shared and no course is, so the board is
    /// a balloon flying over a spawn and the read says nothing. Read by the column it raised one complaint per
    /// layer of the thing, every one of them about blocks tens of courses apart.</summary>
    [Test]
    public async Task A_made_thing_drawn_clear_above_what_it_passes_over_is_not()
    {
        var (layout, intent) = Seed("base-2island");
        var built = WorldBuilder.Build(WithBalloon(layout, 10, 50, floor: 40), intent);

        var shared = built.Declines.Where(finding => finding.Rule == SketchRules.MadeThingInBuilt).ToList();
        await Assert.That(shared).IsEmpty().Because(string.Join(" | ", shared.Select(f => f.Message)));
    }

    [Test]
    public async Task A_made_thing_clear_of_everything_built_is_not()
    {
        // The same envelope, out past the island the spawn stands on.
        var (layout, intent) = Seed("base-2island");
        var built = WorldBuilder.Build(WithBalloon(layout, 120, 120, floor: 30), intent);

        await Assert.That(built.Declines.Any(finding => finding.Rule == SketchRules.MadeThingInBuilt)).IsFalse();
    }

    /// <summary>And the seed as committed carries none, which is what makes the two above a signal rather than
    /// a property of the fixture.</summary>
    [Test]
    [Arguments("base-2island")]
    [Arguments("base-4team")]
    [Arguments("base-2wool")]
    public async Task A_seed_with_no_made_thing_on_it_says_nothing(string name)
    {
        var (layout, intent) = Seed(name);
        var built = WorldBuilder.Build(layout, intent);

        await Assert.That(built.Declines.Any(finding => finding.Rule == SketchRules.MadeThingInBuilt)).IsFalse();
    }
}
