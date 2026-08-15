namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The layer between a tone family and a theme — a material asked for by name. What is worth asserting is the
/// part a caller cannot see: that a family read here never yields a block which only reads from above, and that
/// a name it does not know is a refusal naming what it does.
/// </summary>
public sealed class MaterialRecipesTests
{
    [Test]
    public async Task A_family_never_yields_a_block_that_only_reads_from_above()
    {
        // Grass, podzol and mycelium sit inside verdant, loam and mauve, and a pattern filled from a whole
        // family would scatter them down a riser as banded dirt. Checked over every family rather than the
        // three, so a fourth top-faced block added to any of them fails here.
        foreach (var family in TerrainPalette.Families)
            foreach (var material in MaterialRecipes.Family(family.Name))
            {
                var (id, data) = material.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Surface, 0, -1));
                await Assert.That((family.Name, id, data, MaterialRecipes.IsTopFaced(id, data)))
                    .IsEqualTo((family.Name, id, data, false));
            }
    }

    [Test]
    public async Task Grass_is_one_course_over_dirt_and_only_one()
    {
        // The most-repeated authoring mistake in this repository is a surface that repeats grass down every
        // course, which reads as a wall of turf. The recipe answers grass at the top and dirt below it.
        var top = MaterialRecipes.Grass.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Surface, DepthFromTop: 0, -1));
        var under = MaterialRecipes.Grass.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Surface, DepthFromTop: 1, -1));

        await Assert.That(top).IsEqualTo((2, 0));
        await Assert.That(under).IsEqualTo((3, 0));
    }

    [Test]
    public async Task Every_named_pattern_resolves_and_an_unnamed_one_says_what_it_has()
    {
        foreach (var name in MaterialRecipes.AreaPatterns)
            await Assert.That(MaterialRecipes.Pattern(name, "cobble")).IsNotNull();

        var refused = Assert.Throws<ArgumentException>(() => MaterialRecipes.Pattern("marble", "cobble"));
        await Assert.That(refused!.Message).Contains("turbulence");
    }

    [Test]
    public async Task An_unknown_family_names_the_ones_there_are()
    {
        var refused = Assert.Throws<ArgumentException>(() => MaterialRecipes.Family("chartreuse"));
        await Assert.That(refused!.Message).Contains("verdant");
    }

    [Test]
    public async Task Asking_past_a_familys_end_answers_its_last_block_rather_than_refusing()
    {
        // A family is a curated set whose length is not the caller's business, so an index past it is a
        // request for the last block. The clamp is what lets "the second stone" be written without counting.
        var family = MaterialRecipes.Family("cobble");
        await Assert.That(MaterialRecipes.One("cobble", 99)).IsEqualTo(family[^1]);
        await Assert.That(MaterialRecipes.One("cobble", -3)).IsEqualTo(family[0]);
    }

    [Test]
    public async Task Stripes_carry_the_families_blocks_at_one_width()
    {
        var stripes = MaterialRecipes.Stripes("azure", 3);
        await Assert.That(stripes.Count).IsEqualTo(Math.Min(3, MaterialRecipes.Family("azure").Count));
        await Assert.That(stripes.All(stripe => stripe.Width == 3)).IsTrue();
    }
}
