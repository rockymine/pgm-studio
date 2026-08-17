using PgmStudio.Minecraft.Houses;
using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// What the dressing pass records about the things it puts down. It used to record its buildings and nothing
/// else, on the argument that a tree separates from built ground by material. It does — what material cannot
/// say is that a pass put it there, or which prop it belonged to, so a flora prop that landed nothing looked
/// exactly like one that was never authored. Every prop now answers for its own columns.
/// </summary>
public sealed class DressingProvenanceTests
{
    private static (VoxelWorld World, Dictionary<(int X, int Z), int> SurfaceTop) Plateau(int size = 48)
    {
        var world = new VoxelWorld();
        var top = new Dictionary<(int X, int Z), int>();
        for (var z = -size / 2; z < size / 2; z++)
            for (var x = -size / 2; x < size / 2; x++)
            {
                for (var y = 0; y < 7; y++) world.SetBlock(x, y, z, Blocks.Stone);
                world.SetBlock(x, 7, z, Blocks.Grass);
                top[(x, z)] = 8;
            }
        return (world, top);
    }

    private static DressingContext Context(
        Dictionary<(int X, int Z), int> top, IReadOnlyList<PlacedProp> props, string? symmetry = null)
        => new(top, props, (_, _) => null, new DressingSymmetry(symmetry, 0, 0));

    private static DressingPlacement Dress(IReadOnlyList<PlacedProp> props, string? symmetry = null)
    {
        var (world, top) = Plateau();
        return Decorator.Decorate(world, Context(top, props, symmetry));
    }

    [Test]
    [Arguments("tree")] [Arguments("boulder")] [Arguments("path")] [Arguments("flora")]
    public async Task Every_kind_of_prop_claims_the_columns_it_covered(string kind)
    {
        var placed = Dress([
            new TreeProp { Id = "t1", X = 6, Z = 6, Seed = 5 },
            new BoulderProp { Id = "b1", X = -8, Z = 8, Seed = 3 },
            new PathProp { Id = "p1", Points = [[-10, -10], [10, -10]], Radius = 2, Seed = 5 },
            new FloraProp { Id = "f1", Points = [[2, 2], [20, 2], [20, 20], [2, 20]], Spec = new FloraSpec(), Seed = 7 },
        ]);

        var claims = placed.Placements.Where(entry => entry.Owner.Kind == kind).ToList();

        await Assert.That(claims).IsNotEmpty();
        await Assert.That(claims.Sum(claim => claim.Cells.Count)).IsGreaterThan(0);
        await Assert.That(claims.Select(claim => claim.Layer).Distinct()).IsEquivalentTo(new[] { ProvenanceLayer.Prop });
    }

    [Test]
    public async Task A_prop_carries_the_id_its_author_gave_it()
    {
        // The whole point of recording it: a read-back can name which prop a column belonged to, which is
        // what makes "this prop landed nothing" a thing the record can prove rather than a thing nobody sees.
        var placed = Dress([new TreeProp { Id = "oak-by-the-gate", X = 4, Z = 4, Seed = 1 }]);

        await Assert.That(placed.Placements.Select(claim => claim.Owner))
                    .Contains(new StampId("tree", "oak-by-the-gate", 0));
    }

    [Test]
    public async Task A_building_is_a_structure_and_a_tree_beside_it_is_not()
    {
        // Both are the dressing pass's, and a material test would call the house's spruce posts a grove — the
        // layer is what keeps a reader asking for buildings from being handed the tree.
        var placed = Dress([
            new HouseProp
            {
                Id = "h1",
                Wings = [new AuthoredWing([[10, 10], [18, 18]])],
                Style = new HouseStyle { Doorway = new Doorway { Door = DoorMaterial.Air } },
            },
            new TreeProp { Id = "t1", X = -10, Z = -10, Seed = 5 },
        ]);

        await Assert.That(placed.Placements.Single(c => c.Owner.Kind == "house").Layer)
                    .IsEqualTo(ProvenanceLayer.Structure);
        await Assert.That(placed.Placements.Single(c => c.Owner.Kind == "tree").Layer)
                    .IsEqualTo(ProvenanceLayer.Prop);
        await Assert.That(placed.Structures.Select(c => c.Owner.Kind)).IsEquivalentTo(new[] { "house" });
    }

    [Test]
    public async Task A_prop_that_landed_nothing_claims_nothing()
    {
        // The case the record exists to make visible: no claim at all, rather than a claim over bare ground.
        var (world, top) = Plateau();
        var blocked = new DressingContext(top, [new TreeProp { Id = "t1", X = 4, Z = 4, Seed = 5 }],
                                          (_, _) => KeepOut.Spawn, DressingSymmetry.None);

        var placed = Decorator.Decorate(world, blocked);

        await Assert.That(placed.Trees).IsEqualTo(0);
        await Assert.That(placed.Placements).IsEmpty();
        await Assert.That(placed.Declines.Single().Rule).IsEqualTo(DressingRules.KeptClear);
    }

    [Test]
    public async Task A_mirrored_prop_claims_each_of_its_images_apart()
    {
        // One authored thing, two images — and the record says which half is which, exactly as a building's
        // does. Merging them would say the prop landed somewhere without saying on which side.
        var placed = Dress([new TreeProp { Id = "t1", X = 8, Z = 8, Seed = 5 }], symmetry: "rot_180");

        await Assert.That(placed.Trees).IsEqualTo(2);
        await Assert.That(placed.Placements.Count).IsEqualTo(2);
        await Assert.That(placed.Placements.Select(claim => claim.Owner.Identity).Distinct().Count()).IsEqualTo(1);
        await Assert.That(placed.Placements.Select(claim => claim.Owner.Image).Order())
                    .IsEquivalentTo(new[] { 0, 1 });

        var authored = placed.Placements.Single(claim => claim.Owner.Image == 0);
        var mirror = placed.Placements.Single(claim => claim.Owner.Image == 1);
        await Assert.That(authored.Cells.All(cell => cell is { X: > 0, Z: > 0 })).IsTrue();
        await Assert.That(mirror.Cells.All(cell => cell is { X: < 0, Z: < 0 })).IsTrue();
    }

    [Test]
    public async Task A_claimed_prop_still_reads_as_what_its_blocks_are()
    {
        // The layer is a provenance answer, not a colour one: a tree's leaves stay foliage and a road's
        // gravel stays ground, so recording them changes no picture.
        await Assert.That(RenderCategories.Of(Blocks.Leaves, ProvenanceLayer.Prop))
                    .IsEqualTo(RenderCategory.Foliage);
        await Assert.That(RenderCategories.Of(Blocks.Gravel, ProvenanceLayer.Prop))
                    .IsEqualTo(RenderCategory.Ground);
    }
}
