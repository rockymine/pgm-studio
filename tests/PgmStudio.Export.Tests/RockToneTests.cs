using System.Text.Json;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

/// <summary>
/// DR-TONE (<see cref="RockTone"/>): a boulder cut from nothing but the tone families of the ground it stands
/// on has no silhouette. The ground here is real theme JSON read back through the scope the export paints
/// with, and the rocks are real <see cref="BoulderStyle"/> recipes, so what the rule is asked is what the
/// world would be built from.
/// </summary>
public sealed class RockToneTests
{
    private const int Sand = Minecraft.Palette.Blocks.Sand;          // "sand"
    private const int Sandstone = 24;                                // "sand"
    private const int Stone = Minecraft.Palette.Blocks.Stone;        // "grey stone"
    private const int Cobblestone = Minecraft.Palette.Blocks.Cobblestone; // "cobble"
    private const int Grass = Minecraft.Palette.Blocks.Grass;        // "verdant"
    private const int Dirt = Minecraft.Palette.Blocks.Dirt;          // "dirt"

    private static JsonElement Surfaced(TerrainMaterial material) => JsonSerializer.Deserialize<JsonElement>(
        TerrainThemeJson.Serialize(TerrainTheme.Default with
        {
            Surface = TerrainTheme.Default.Surface with { Material = material, Depth = 1 },
        }));

    /// <summary>A board of one square of ground under one theme, with one boulder standing on it.</summary>
    private static string Board(TerrainMaterial ground, TerrainMaterial? rock)
    {
        var layout = new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "rot_180", Center = new SketchCenter { Cx = 0, Cz = 0 } },
            Themes = new Dictionary<string, JsonElement> { ["field"] = Surfaced(ground) },
            MapTheme = "field",
            Layers =
            [
                SketchLayer.Ground(
                    [new SketchShape
                    {
                        Id = "ground", Type = "rectangle", Operation = "add",
                        MinX = -20, MinZ = -20, MaxX = 20, MaxZ = 20, Theme = "field",
                    }],
                    [new SketchGroup { Id = "i", Mirrors = false, ShapeIds = ["ground"] }]),
            ],
        };
        var doc = new DressingDoc
        {
            Props = [new BoulderProp
            {
                Id = "rock-1", X = 4, Z = 4, Layer = "ground",
                Style = rock is null ? new BoulderStyle() : new BoulderStyle { Rock = rock },
            }],
        };
        layout.Dressing = JsonSerializer.Deserialize<JsonElement>(DressingJson.Serialize(doc));
        return layout.ToJson();
    }

    /// <summary>The author's case: a sandstone rock on sand. Two different blocks, one tone, no rock.</summary>
    [Test]
    public async Task A_sandstone_rock_on_sand_is_a_patch_of_sand_standing_up()
    {
        var findings = RockTone.Check(Board(new SolidMaterial(Sand), new SolidMaterial(Sandstone)));

        var finding = findings.Single();
        await Assert.That(finding.Rule).IsEqualTo(DressingRules.RockInTheGroundsTone);
        await Assert.That(finding.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(finding.Message).Contains("boulder 'rock-1' at (4, 4)");
        await Assert.That(finding.Message).Contains("cut from sand");
        await Assert.That(finding.SubjectIds).IsEquivalentTo(new[] { "rock-1" });
    }

    /// <summary>The rule's own remedy, tested as one: the rock a placement naming no recipe is cut from reads
    /// against the four grounds the rule names it for.</summary>
    [Test]
    [Arguments(Sand, 0)]
    [Arguments(Grass, 0)]
    [Arguments(Dirt, 0)]
    [Arguments(Sand, 1)]   // red sand — the "rust" family, not sand's
    public async Task The_default_rock_reads_against_ordinary_ground(int id, int data)
    {
        await Assert.That(RockTone.Check(Board(new SolidMaterial(id, data), rock: null))).IsEmpty();
    }

    /// <summary>And it does not read against grey stone, which is what it is made of — the one ground the
    /// rule's remedy says to take the rock the other way on.</summary>
    [Test]
    public async Task The_default_rock_on_stone_and_cobble_ground_is_the_ground()
    {
        var ground = new CellMaterial(1, 6, 20, 2,
            [new SolidMaterial(Stone), new SolidMaterial(Cobblestone)]);

        await Assert.That(RockTone.Check(Board(ground, rock: null)).Single().Message)
            .Contains("cobble and grey stone");
    }

    /// <summary>A rock keeping one family the ground does not have is a rock, however much else it shares:
    /// what disappears is the one built wholly from the field.</summary>
    [Test]
    public async Task A_rock_holding_one_tone_the_ground_lacks_still_reads_as_a_rock()
    {
        var rock = new CellMaterial(1, 4, 2, 3, [new SolidMaterial(Sand), new SolidMaterial(Stone)]);

        await Assert.That(RockTone.Check(Board(new SolidMaterial(Sand), rock))).IsEmpty();
    }

    /// <summary>The ground a rock is judged against is the ground it can rest on. A meadow whose steep faces
    /// are bare stone is a meadow, so a stone rock standing on it stands out — and asking the whole slope
    /// stack instead would fire on the recipe the rule itself recommends.</summary>
    [Test]
    public async Task A_meadow_whose_cliffs_are_stone_is_still_a_meadow_under_a_stone_rock()
    {
        var slope = new LayeredMaterial(
            new BandStack([new Band(new SolidMaterial(Grass), 20),
                           new Band(new SolidMaterial(Dirt), 15)], BandEnding.HandOver),
            BandAxis.Slope, Beyond: new SolidMaterial(Stone));

        await Assert.That(RockTone.Check(Board(slope, new SolidMaterial(Stone)))).IsEmpty();
    }

    /// <summary>A board with no boulder on it is not asked, whatever its ground.</summary>
    [Test]
    public async Task A_board_with_no_rock_on_it_says_nothing()
    {
        var layout = new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "rot_180", Center = new SketchCenter { Cx = 0, Cz = 0 } },
            Layers = [SketchLayer.Ground([new SketchShape
            {
                Id = "ground", Type = "rectangle", Operation = "add",
                MinX = -20, MinZ = -20, MaxX = 20, MaxZ = 20,
            }])],
        };

        await Assert.That(RockTone.Check(layout.ToJson())).IsEmpty();
    }
}
