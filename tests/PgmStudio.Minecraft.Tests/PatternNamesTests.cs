using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests;

/// <summary>A pattern's name is what it contains: the blocks it lays in the order it first lays them, then the
/// kind laying them (<see cref="PatternNames"/>).</summary>
public sealed class PatternNamesTests
{
    [Test]
    public async Task A_field_names_its_blocks_then_its_kind()
        => await Assert.That(PatternNames.Describe(new NoiseMaterial(7, 2, 1,
                [new SolidMaterial(Blocks.Cobblestone), new SolidMaterial(Blocks.Stone, 5), new SolidMaterial(Blocks.Cobblestone)])))
            .IsEqualTo("cobblestone-andesite-noise");

    [Test]
    public async Task A_team_tint_names_its_block_and_the_colour_of_its_neutral()
        => await Assert.That(PatternNames.Describe(
                new TeamTintedMaterial(Blocks.StainedClay, new SolidMaterial(Blocks.StainedClay, 8))))
            .IsEqualTo("team-stained-clay-light-gray-neutral");

    [Test]
    public async Task Past_three_blocks_a_name_says_mix()
        => await Assert.That(PatternNames.Describe(new CellMaterial(1, 3, 1, 1,
                [new SolidMaterial(98), new SolidMaterial(1, 6), new SolidMaterial(1, 5), new SolidMaterial(1)])))
            .IsEqualTo("stone-bricks-polished-andesite-andesite-mix-cells");

    [Test]
    public async Task A_taken_name_takes_the_first_free_count()
        => await Assert.That(PatternNames.Unique("stone-noise", new HashSet<string> { "stone-noise", "stone-noise-2" }))
            .IsEqualTo("stone-noise-3");
}
