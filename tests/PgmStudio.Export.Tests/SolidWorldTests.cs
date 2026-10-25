using System.Text.Json.Nodes;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The shapes that stand in the block grid reach the built world: a sphere is stone under the theme's surface, and
/// an arch's deck is a surface a player walks, with the gap under it a place to stand.
/// </summary>
public sealed class SolidWorldTests
{
    private const int Quartz = 155;

    /// <summary>A plate ten thick for the ground, and the solids given.</summary>
    private static string Board(string solids, int plate = 10)
    {
        var doc = JsonNode.Parse($$$"""
            {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},"layers": [
              { "id": "ground", "base_y": 0, "layout":{"shapes":[
                {"id":"plate","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":{{{plate}}}}],
               "groups":[]} },
              { "id": "solids", "base_y": 0, "layout":{"shapes":[ {{{solids}}} ],"groups":[]} } ]}
            """)!.AsObject();
        var theme = TerrainTheme.Default with
        {
            Surface = TerrainTheme.Default.Surface with { Material = new SolidMaterial(Quartz), Depth = 1 },
        };
        doc["themes"] = new JsonObject { ["crest"] = JsonNode.Parse(TerrainThemeJson.Serialize(theme))! };
        doc["mapTheme"] = "crest";
        return doc.ToJsonString();
    }

    private static MapIntent Intent() => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
        Spawns = [new SpawnIntent { Team = "red", Point = new Pt(-30, 11, -30), Yaw = 0 }],
        Wools = [new WoolIntent { Owner = "blue", Color = "blue", Spawn = new Pt(30, 11, 30) }],
    };

    [Test]
    public async Task A_sphere_stands_in_the_world_as_stone_under_the_themes_surface()
    {
        var built = BuiltWorlds.Of(Board(
            """{"id":"ball","type":"sphere","center_x":0.5,"center_z":0.5,"radius":5,"floor":10}"""), Intent());

        await Assert.That(built.World.GetBlock(0, 10, 0).Id).IsNotEqualTo(Blocks.Air).Because("the ball's lowest course");
        await Assert.That(built.World.GetBlock(0, 14, 0)).IsEqualTo((Blocks.Stone, 0)).Because("its inside is stone");
        await Assert.That(built.World.GetBlock(0, 19, 0)).IsEqualTo((Quartz, 0)).Because("its top is the theme's surface");
        await Assert.That(built.World.GetBlock(0, 20, 0).Id).IsEqualTo(Blocks.Air);
        await Assert.That(built.World.GetBlock(5, 14, 0).Id).IsEqualTo(Blocks.Air).Because("a column five out is past its rim");
    }

    [Test]
    public async Task An_arch_stands_on_its_feet_over_a_gap_and_its_deck_is_walked()
    {
        var built = BuiltWorlds.Of(Board(
            """{"id":"arch","type":"arch","vertices":[[-30,0],[30,0]],"radius":3,"deck":[18,10,1],"thick":[3,8,1]}""",
            plate: 10), Intent());
        var ground = BuiltWalk.Ground(built, null);

        await Assert.That(built.World.GetBlock(0, 18, 0).Id).IsNotEqualTo(Blocks.Air).Because("the deck at the crown");
        await Assert.That(built.World.GetBlock(0, 12, 0).Id).IsEqualTo(Blocks.Air).Because("the gap under the crown, between the ground and the rock");

        var column = ground.Stacks[(0, 0)].OrderBy(place => place.Y).ToList();
        await Assert.That(column.Count).IsGreaterThan(1).Because("a player can stand under the arch and on it");
        await Assert.That(column[0].Y).IsEqualTo(10);
        await Assert.That(column[^1].Y).IsEqualTo(19);

        WalkPlace OnDeck(int x) => ground.Stacks[(x, 0)].OrderBy(place => place.Y).Last();
        var across = Walk.Between(OnDeck(-25), OnDeck(25), ground);
        await Assert.That(across).IsNotNull();
        await Assert.That(across!.Cost.Blocks).IsEqualTo(0).Because("the deck is walked, no block placed");
        await Assert.That(across.Places.Max(place => place.Y)).IsEqualTo(19).Because("the route goes over the crown");
        await Assert.That(across.Places.Where(place => place.X is > -20 and < 20).All(place => place.Y > 12)).IsTrue()
            .Because("it stays on the deck rather than dropping under it");
    }
}
