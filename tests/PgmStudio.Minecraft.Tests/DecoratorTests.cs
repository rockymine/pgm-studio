using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The dressing pass (G161, docs/world-export/decoration.md): the third walk over a realized world, adding the
/// terrain's life on top of the paint. The cases that matter are the ones a preview cannot show — that the
/// paint underneath is really what gates flora, that protected ground is really left alone, that leaves carry
/// the no-decay bit, and above all that gameplay-affecting props land identically for every team.
/// </summary>
public sealed class DecoratorTests
{
    /// <summary>A flat painted plateau: <paramref name="size"/> square of grass over dirt over stone, its first
    /// air course at y = 8 — the same shape the painter hands the pass in a real export.</summary>
    private static (VoxelWorld World, Dictionary<(int X, int Z), int> SurfaceTop) Plateau(
        int size = 40, int surfaceBlock = Blocks.Grass, int from = 0)
    {
        var world = new VoxelWorld();
        var top = new Dictionary<(int X, int Z), int>();
        for (var z = from; z < from + size; z++)
        for (var x = from; x < from + size; x++)
        {
            for (var y = 0; y < 7; y++) world.SetBlock(x, y, z, Blocks.Stone);
            world.SetBlock(x, 7, z, surfaceBlock);
            top[(x, z)] = 8;
        }
        return (world, top);
    }

    private static DressingContext Context(
        Dictionary<(int X, int Z), int> top, DressingRecipe recipe,
        Func<int, int, bool>? isProtected = null, string? symmetry = null, double centerX = 0, double centerZ = 0)
        => new(top, (_, _) => recipe, isProtected ?? ((_, _) => false),
            new DressingSymmetry(symmetry, centerX, centerZ));

    private static IEnumerable<(int X, int Y, int Z, int Id, int Data)> Placed(
        VoxelWorld world, IEnumerable<(int X, int Z)> cells, int fromY, int toY)
    {
        foreach (var (x, z) in cells)
        for (var y = fromY; y <= toY; y++)
        {
            var (id, data) = world.GetBlock(x, y, z);
            if (id != Blocks.Air) yield return (x, y, z, id, data);
        }
    }

    // ── nothing by default ─────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_bare_recipe_places_nothing_at_all()
    {
        var (world, top) = Plateau();
        var tally = Decorator.Decorate(world, Context(top, DressingRecipe.Bare));

        await Assert.That(tally).IsEqualTo(new DressingTally(0, 0, 0));
        // The map a author never dressed exports exactly as it did before the stage existed.
        await Assert.That(Placed(world, top.Keys, 8, 40).Any()).IsFalse();
    }

    // ── flora (DR-FL) ──────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Flora_grows_in_the_air_above_the_soil_and_leaves_the_ground_alone()
    {
        var (world, top) = Plateau();
        var tally = Decorator.Decorate(world, Context(top, new DressingRecipe { Flora = new FloraSpec(Coverage: 0.6) }));

        await Assert.That(tally.Plants).IsGreaterThan(50);
        foreach (var (x, _, z, _, _) in Placed(world, top.Keys, 8, 12))
            await Assert.That(world.GetBlock(x, 7, z).Id).IsEqualTo(Blocks.Grass);   // the ground is untouched
    }

    [Test]
    public async Task The_paint_underneath_is_what_decides_whether_flora_grows()
    {
        // The reason the pass runs after the painter at all: a plaza's quartz takes nothing, and no separate
        // mask had to be derived to know that.
        var flora = new DressingRecipe { Flora = new FloraSpec(Coverage: 0.9) };

        var (grass, grassTop) = Plateau(surfaceBlock: Blocks.Grass);
        var (quartz, quartzTop) = Plateau(surfaceBlock: Blocks.QuartzBlock);
        var (sand, sandTop) = Plateau(surfaceBlock: 12);

        var onGrass = Decorator.Decorate(grass, Context(grassTop, flora)).Plants;
        var onQuartz = Decorator.Decorate(quartz, Context(quartzTop, flora)).Plants;
        var onSand = Decorator.Decorate(sand, Context(sandTop, flora)).Plants;

        await Assert.That(onQuartz).IsEqualTo(0);
        await Assert.That(onSand).IsGreaterThan(0);
        await Assert.That(onSand).IsLessThan(onGrass);   // sand accepts flora, but sparsely
    }

    [Test]
    public async Task A_two_block_plant_is_placed_as_a_pair_with_its_top_half_flagged()
    {
        // Placed without the upper-half flag, a double plant drops as an item on the first block update.
        var (world, top) = Plateau();
        Decorator.Decorate(world, Context(top, new DressingRecipe
        { Flora = new FloraSpec(Coverage: 0.9, TallShare: 1.0) }));

        var lower = Placed(world, top.Keys, 8, 8).Where(b => b.Id == DressingPalette.DoublePlant).ToList();
        await Assert.That(lower.Count).IsGreaterThan(20);
        foreach (var (x, _, z, _, _) in lower)
            await Assert.That(world.GetBlock(x, 9, z)).IsEqualTo((DressingPalette.DoublePlant, DressingPalette.DoublePlantUpper));
    }

    [Test]
    public async Task Flora_never_lands_on_protected_ground()
    {
        var (world, top) = Plateau();
        // A spawn box in one corner — nothing may grow inside it.
        Decorator.Decorate(world, Context(top, new DressingRecipe { Flora = new FloraSpec(Coverage: 0.95) },
            isProtected: (x, z) => x < 10 && z < 10));

        await Assert.That(Placed(world, top.Keys.Where(c => c.X < 10 && c.Z < 10), 8, 12).Any()).IsFalse();
        await Assert.That(Placed(world, top.Keys.Where(c => c.X >= 10), 8, 12).Any()).IsTrue();
    }

    // ── boulders (DR-SC) ───────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_boulder_is_half_buried_so_it_reads_as_emerging_from_the_ground()
    {
        var (world, top) = Plateau(60);
        var tally = Decorator.Decorate(world, Context(top, new DressingRecipe
        { Boulders = new BoulderSpec(Density: 0.9, SizeSpread: 0.2, Form: BoulderForm.Round) }));

        await Assert.That(tally.Boulders).IsGreaterThan(3);
        // Rock stands above the old surface …
        await Assert.That(Placed(world, top.Keys, 8, 20).Any(b => b.Id is Blocks.Stone or 48)).IsTrue();
        // … and has replaced ground below it, which is what "buried" means.
        var buried = top.Keys.Count(cell => world.GetBlock(cell.X, 7, cell.Z).Id != Blocks.Grass);
        await Assert.That(buried).IsGreaterThan(0);
    }

    [Test]
    public async Task Boulders_do_not_pile_on_top_of_one_another()
    {
        var (world, top) = Plateau(60);
        Decorator.Decorate(world, Context(top, new DressingRecipe { Boulders = new BoulderSpec(Density: 1.0) }));

        // The scatter is a local maximum over a disc, so however dense the spec asks for, sites stay spaced.
        var rock = Placed(world, top.Keys, 9, 30).Select(b => (b.X, b.Z)).Distinct().Count();
        await Assert.That(rock).IsLessThan(top.Count / 2);
    }

    [Test]
    public async Task A_boulder_that_would_overhang_the_void_is_refused_rather_than_clipped()
    {
        // A strip one cell wide: no boulder's footprint fits on it, so none is placed. A prop half over the
        // edge would read as floating.
        var world = new VoxelWorld();
        var top = new Dictionary<(int X, int Z), int>();
        for (var x = 0; x < 60; x++) { world.SetBlock(x, 7, 0, Blocks.Grass); top[(x, 0)] = 8; }

        var tally = Decorator.Decorate(world, Context(top, new DressingRecipe { Boulders = new BoulderSpec(Density: 1.0) }));
        await Assert.That(tally.Boulders).IsEqualTo(0);
    }

    // ── trees (DR-TR) ──────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_tree_stands_on_the_surface_with_a_trunk_under_its_leaves()
    {
        var (world, top) = Plateau(60);
        var tally = Decorator.Decorate(world, Context(top, new DressingRecipe
        { Trees = new TreeSpec(Density: 0.9, GroveThreshold: 1.0, Species: ["oak"]) }));

        await Assert.That(tally.Trees).IsGreaterThan(2);
        var logs = Placed(world, top.Keys, 8, 60).Where(b => b.Id == DressingPalette.Log).ToList();
        var leaves = Placed(world, top.Keys, 8, 60).Where(b => b.Id == DressingPalette.Leaves).ToList();
        await Assert.That(logs.Count).IsGreaterThan(10);
        await Assert.That(leaves.Count).IsGreaterThan(logs.Count);          // a crown outweighs its wood
        await Assert.That(logs.Min(b => b.Y)).IsEqualTo(8);                 // seated on the surface, not sunk
    }

    [Test]
    public async Task Every_leaf_carries_the_no_decay_bit()
    {
        // A built map has no growing tree behind its leaves; without this flag the whole crown vanishes
        // shortly after the map loads.
        var (world, top) = Plateau(60);
        Decorator.Decorate(world, Context(top, new DressingRecipe
        { Trees = new TreeSpec(Density: 0.9, GroveThreshold: 1.0) }));

        var leaves = Placed(world, top.Keys, 8, 60)
            .Where(b => b.Id is DressingPalette.Leaves or DressingPalette.Leaves2).ToList();
        await Assert.That(leaves.Count).IsGreaterThan(0);
        foreach (var leaf in leaves)
            await Assert.That(leaf.Data & DressingPalette.LeafNoDecay).IsEqualTo(DressingPalette.LeafNoDecay);
    }

    [Test]
    public async Task Groves_clump_rather_than_covering_the_map_evenly()
    {
        var (world, top) = Plateau(80);
        var sparse = Decorator.Decorate(world, Context(top, new DressingRecipe
        { Trees = new TreeSpec(Density: 0.8, GroveThreshold: 0.25) })).Trees;

        var (world2, top2) = Plateau(80);
        var full = Decorator.Decorate(world2, Context(top2, new DressingRecipe
        { Trees = new TreeSpec(Density: 0.8, GroveThreshold: 1.0) })).Trees;

        await Assert.That(sparse).IsLessThan(full);
    }

    // ── fairness (G162) ────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Gameplay_props_land_identically_for_every_team()
    {
        // The correctness rule the whole stage is gated on: cover a team has, its mirror has too. The plateau
        // is centred on the origin so rot_180 maps it onto itself.
        var (world, top) = Plateau(60, from: -30);
        Decorator.Decorate(world, Context(top,
            new DressingRecipe { Boulders = new BoulderSpec(Density: 0.8), Trees = new TreeSpec(Density: 0.6, GroveThreshold: 1.0) },
            symmetry: "rot_180"));

        var cover = top.Keys.Where(cell => world.GetBlock(cell.X, 9, cell.Z).Id != Blocks.Air).ToHashSet();
        await Assert.That(cover.Count).IsGreaterThan(20);

        var unmirrored = cover.Count(cell => !cover.Contains((-cell.X - 1, -cell.Z - 1)));
        await Assert.That(unmirrored).IsEqualTo(0);
    }

    [Test]
    public async Task Without_symmetry_the_same_scatter_is_free()
    {
        // The same recipe on an unmirrored map is not forced into pairs — the fan is a property of the map's
        // symmetry, not a cost every dressing pays.
        var (world, top) = Plateau(60, from: -30);
        Decorator.Decorate(world, Context(top, new DressingRecipe { Boulders = new BoulderSpec(Density: 0.8) }));

        var cover = top.Keys.Where(cell => world.GetBlock(cell.X, 9, cell.Z).Id != Blocks.Air).ToHashSet();
        await Assert.That(cover.Count(cell => !cover.Contains((-cell.X - 1, -cell.Z - 1)))).IsGreaterThan(0);
    }

    [Test]
    public async Task Cosmetic_flora_stays_free_even_on_a_mirrored_map()
    {
        // Two identical flower beds read as a rendering artefact, and a flower decides nothing — so the orbit
        // fan deliberately does not reach them.
        var (world, top) = Plateau(60, from: -30);
        Decorator.Decorate(world, Context(top,
            new DressingRecipe { Flora = new FloraSpec(Coverage: 0.5) }, symmetry: "rot_180"));

        var plants = top.Keys.Where(cell => world.GetBlock(cell.X, 8, cell.Z).Id != Blocks.Air).ToHashSet();
        await Assert.That(plants.Count(cell => !plants.Contains((-cell.X - 1, -cell.Z - 1)))).IsGreaterThan(0);
    }

    // ── determinism + serialization ────────────────────────────────────────────────────────────────
    [Test]
    public async Task The_same_recipe_dresses_the_same_world_the_same_way()
    {
        var recipe = new DressingRecipe
        { Flora = new FloraSpec(), Boulders = new BoulderSpec(), Trees = new TreeSpec(GroveThreshold: 0.8) };

        var (first, firstTop) = Plateau(50);
        var (again, againTop) = Plateau(50);
        Decorator.Decorate(first, Context(firstTop, recipe));
        Decorator.Decorate(again, Context(againTop, recipe));

        foreach (var cell in firstTop.Keys)
        for (var y = 7; y < 40; y++)
            await Assert.That(first.GetBlock(cell.X, y, cell.Z)).IsEqualTo(again.GetBlock(cell.X, y, cell.Z));
    }

    [Test]
    public async Task A_recipe_round_trips_through_its_json()
    {
        var recipe = new DressingRecipe
        {
            Flora = new FloraSpec(Coverage: 0.6, TallShare: 0.2) { Seed = 99 },
            Boulders = new BoulderSpec(Form: BoulderForm.Cairn, BlockId: 1, BlockData: 5),
            Trees = new TreeSpec(GroveScale: 40, Species: ["birch", "spruce"]),
        };
        var json = DressingJson.Serialize(recipe);

        await Assert.That(json).Contains("cairn");    // enums read as names, not ordinals
        await Assert.That(DressingJson.Deserialize(json)).IsEqualTo(recipe);
        await Assert.That(DressingJson.Serialize(DressingJson.Deserialize(json))).IsEqualTo(json);
        await Assert.That(DressingJson.Deserialize(DressingJson.Serialize(DressingRecipe.Bare)).IsBare).IsTrue();
    }
}
