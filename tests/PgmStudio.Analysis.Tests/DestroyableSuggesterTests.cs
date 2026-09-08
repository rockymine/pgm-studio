using PgmStudio.Analysis.Suggest;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// The two neighbourhood readings are the whole detector, so each is tested by the world that isolates it:
/// a mass that is alone and raised, one that is alone and level, one that is raised and surrounded.
/// </summary>
public class DestroyableSuggesterTests
{
    private const int Obsidian = 49;
    private const int Stone = 1;

    /// <summary>Flat stone terrain at y=10 over the square, so every column has a surface to measure
    /// against.</summary>
    private static Dictionary<(int X, int Y, int Z), int> Ground(int half = 25)
    {
        var world = new Dictionary<(int X, int Y, int Z), int>();
        for (var x = -half; x <= half; x++)
            for (var z = -half; z <= half; z++)
                world[(x, 10, z)] = Stone;
        return world;
    }

    [Test]
    public async Task An_isolated_mass_standing_above_the_ring_is_proposed()
    {
        var world = Ground();
        for (var y = 15; y <= 17; y++) world[(0, y, 0)] = Obsidian;   // a 3-block pillar, +5 over the ground

        var found = DestroyableSuggester.Gather(world);

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].Materials).IsEqualTo("obsidian");
        await Assert.That(found[0].Blocks).IsEqualTo(3);
        await Assert.That(found[0].SameNearby).IsEqualTo(0);
        await Assert.That(found[0].Elevation).IsEqualTo(5);
        await Assert.That((found[0].Structure.MinY, found[0].Structure.MaxY)).IsEqualTo((15, 17));
    }

    [Test]
    public async Task A_mass_level_with_the_ground_around_it_is_not_proposed()
    {
        var world = Ground();
        // Sitting on the surface rather than over it: elevation +1, under the floor the reading asks for.
        world[(0, 11, 0)] = Obsidian;

        await Assert.That(DestroyableSuggester.Gather(world)).IsEmpty();
    }

    [Test]
    public async Task A_material_repeated_around_it_is_not_proposed_however_high_it_stands()
    {
        var world = Ground();
        for (var y = 15; y <= 17; y++) world[(0, y, 0)] = Obsidian;
        // Decoration of the same material within the neighbourhood — the isolation reading is what a goal
        // fails when its material is what the place is built from.
        for (var x = 3; x <= 8; x++)
            for (var z = 3; z <= 8; z++)
                world[(x, 11, z)] = Obsidian;

        await Assert.That(DestroyableSuggester.Gather(world)).IsEmpty();
    }

    [Test]
    public async Task Each_of_the_four_buildable_materials_is_read_and_nothing_else_is()
    {
        var world = Ground(40);
        var at = -30;
        foreach (var material in PgmStudio.Domain.DestroyableMaterials.All)
        {
            var id = PgmStudio.Domain.DestroyableMaterials.BlockId(material);
            for (var y = 15; y <= 17; y++) world[(at, y, 0)] = id;
            at += 20;   // far enough apart that neither isolation nor the ring sees the next one
        }
        // Wool is excluded by design: a CTW map is largely made of it, so admitting it floods the candidate
        // set. A raised, isolated wool mass must still not be proposed.
        for (var y = 15; y <= 17; y++) world[(at, y, 0)] = 35;

        var found = DestroyableSuggester.Gather(world);

        await Assert.That(found.Count).IsEqualTo(4);
        await Assert.That(found.Select(f => f.Materials).OrderBy(m => m).ToList())
            .IsEquivalentTo(PgmStudio.Domain.DestroyableMaterials.All.OrderBy(m => m).ToList());
    }

    [Test]
    public async Task Masses_of_one_material_within_the_separation_are_one_proposal()
    {
        var world = Ground(40);
        // Three pillars in a row, 6 apart: one structure that clustered into three, not three goals. The
        // list a person confirms from should offer it once.
        foreach (var x in new[] { 0, 6, 12 })
            for (var y = 15; y <= 17; y++)
                world[(x, y, 0)] = Obsidian;
        // And one well outside the separation, which is a second proposal rather than a duplicate.
        for (var y = 15; y <= 17; y++) world[(35, y, 0)] = Obsidian;

        var found = DestroyableSuggester.Gather(world);

        await Assert.That(found.Count).IsEqualTo(2);
        await Assert.That(found.Select(f => f.Structure.MinX).Order().ToList()).IsEquivalentTo(new List<int> { 0, 35 });
    }

    [Test]
    public async Task A_mass_of_any_size_is_read_because_the_corpus_spans_four_orders_of_magnitude()
    {
        var world = Ground(40);
        // 11x11x3 — far past any cap an earlier reading would have applied, and a real declared size.
        for (var x = -5; x <= 5; x++)
            for (var z = -5; z <= 5; z++)
                for (var y = 15; y <= 17; y++)
                    world[(x, y, z)] = Obsidian;

        var found = DestroyableSuggester.Gather(world);

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].Blocks).IsEqualTo(11 * 11 * 3);
        await Assert.That(found[0].SameNearby).IsEqualTo(0);   // the mass is not counted against itself
    }
}
