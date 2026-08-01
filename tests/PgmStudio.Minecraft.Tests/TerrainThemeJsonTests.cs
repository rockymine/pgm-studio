namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The theme JSON (docs/world-export/terrain-painting.md §3): a theme — including patterns and a nested team
/// tint — round-trips through the tagged-union serialization verbatim. Records with list fields don't compare
/// by value, so the round-trip oracle is canonical re-serialization (serialize ∘ deserialize ∘ serialize is a
/// fixed point) plus a spot check that the rebuilt graph resolves like the original.
/// </summary>
public sealed class TerrainThemeJsonTests
{
    [Test]
    public async Task Default_theme_round_trips()
    {
        var json = TerrainThemeJson.Serialize(TerrainTheme.Default);
        await Assert.That(TerrainThemeJson.Serialize(TerrainThemeJson.Deserialize(json))).IsEqualTo(json);
    }

    [Test]
    public async Task A_theme_of_patterns_round_trips_and_keeps_its_resolution()
    {
        var theme = TerrainTheme.Default with
        {
            // a 3-material wall-run, the last stripe a team tint
            Wall = new WallRunMaterial([
                new WallStripe(new SolidMaterial(Blocks.StainedClay, 14), 5),
                new WallStripe(new SolidMaterial(Blocks.QuartzBlock), 3),
                new WallStripe(new TeamTintedMaterial(Blocks.StainedClay, new SolidMaterial(Blocks.StainedClay, 8)), 5),
            ]),
            // a fractal-noise surface ramp
            Surface = TerrainTheme.Default.Surface with { Material = new NoiseMaterial(42u, 12, 4, [new SolidMaterial(Blocks.Grass), new SolidMaterial(Blocks.Dirt)]) },
            // a voronoi rim
            Rim = TerrainTheme.Default.Rim with { Material = new VoronoiMaterial(7u, 6, [new SolidMaterial(Blocks.QuartzBlock), new SolidMaterial(Blocks.Wool, 0)]) },
            Bedrock = BedrockSpec.TerrainRelative(4),
        };

        var json = TerrainThemeJson.Serialize(theme);
        var back = TerrainThemeJson.Deserialize(json);
        await Assert.That(TerrainThemeJson.Serialize(back)).IsEqualTo(json);   // canonical fixed point

        // the rebuilt graph resolves identically on a sample of each patterned bucket
        var wall = new BucketContext(3, 5, 9, TerrainBucket.Wall, 0, TeamData: 12, PerimeterArc: 7);
        var surf = new BucketContext(11, 8, 4, TerrainBucket.Surface, 1);
        var rimc = new BucketContext(2, 8, 6, TerrainBucket.Rim, 0);
        await Assert.That(back.Wall.Resolve(wall)).IsEqualTo(theme.Wall.Resolve(wall));
        await Assert.That(back.Surface.Material.Resolve(surf)).IsEqualTo(theme.Surface.Material.Resolve(surf));
        await Assert.That(back.Rim.Material.Resolve(rimc)).IsEqualTo(theme.Rim.Material.Resolve(rimc));
    }

    [Test]
    public async Task Material_carries_its_kind_discriminator()
    {
        var json = TerrainThemeJson.Serialize(new WallRunMaterial([new WallStripe(new SolidMaterial(1), 2)]));
        await Assert.That(json).Contains("\"kind\":\"wallRun\"");
        await Assert.That(json).Contains("\"kind\":\"solid\"");   // the nested stripe material tags too
    }
}
