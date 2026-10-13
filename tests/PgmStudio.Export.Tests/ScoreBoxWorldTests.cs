using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export.Tests;

/// <summary>
/// A score box is walked into, so every block of every box — the authored one and its images under the board's
/// symmetry — is air in the world built, and the map names each box the world cleared.
/// </summary>
public sealed class ScoreBoxWorldTests
{
    private const string Json = """
        {
          "plan": 2,
          "meta": { "name": "Box Probe" },
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
          "pieces": [
            { "id": "field", "role": "piece", "rect": [-4, -4, 4, 4], "surface": 12 }
          ]
        }
        """;

    // Sunk into the field, so the ground under the surface is what it has to clear.
    private static readonly BlockBox Sunk = new(10, 9, -2, 13, 13, 1);

    private static BuiltWorld Build()
    {
        var (layout, intent) = PlanCompiler.Compile(PlanModel.Parse(Json)!);
        var team = intent.Teams![0].Id;
        return WorldBuilder.Build(JsonSerializer.Serialize(layout, SketchLayout.Json),
            intent with { ScoreBoxes = [new ScoreBoxIntent { Team = team, Box = Sunk }] });
    }

    [Test]
    public async Task Every_box_and_its_image_is_air()
    {
        var built = Build();
        var boxes = built.ResolvedIntent.ScoreBoxes!;

        await Assert.That(boxes.Count).IsEqualTo(2).Because("rot_180 gives the other team the box's image");
        await Assert.That(boxes.Select(box => box.Team).Distinct().Count()).IsEqualTo(2);
        foreach (var box in boxes.Select(score => score.Box))
            for (var x = box.MinX; x <= box.MaxX; x++)
            for (var z = box.MinZ; z <= box.MaxZ; z++)
            for (var y = box.MinY; y <= box.MaxY; y++)
                await Assert.That(built.World.GetBlock(x, y, z).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task The_map_names_every_box_the_world_cleared()
    {
        var built = Build();
        var doc = new Dictionary<string, object?>();
        IntentGenerator.Apply(doc, built.ResolvedIntent);
        var map = MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));

        await Assert.That(map.Score!.Boxes.Count).IsEqualTo(2).Because("the map fans nothing the world did not");
    }
}
