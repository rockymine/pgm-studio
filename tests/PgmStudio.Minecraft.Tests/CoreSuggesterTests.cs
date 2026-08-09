using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The core signature (<see cref="CoreSuggester"/>) on synthetic worlds — the enclosure rule and the things it
/// must refuse. A pool of lava with obsidian near it is not a core; a sealed container is.
/// </summary>
public sealed class CoreSuggesterTests
{
    private const int Obsidian = 49;
    private const int StationaryLava = 11;
    private const int Stone = 1;

    /// <summary>A casing of <paramref name="size"/>³ with a lava interior, its floor at <paramref name="baseY"/>.</summary>
    private static Dictionary<(int X, int Y, int Z), int> Core(int originX = 0, int baseY = 20, int originZ = 0,
                                                              int size = 5, int shell = 1, bool openTop = false)
    {
        var world = new Dictionary<(int X, int Y, int Z), int>();
        for (var x = 0; x < size; x++)
            for (var y = 0; y < size; y++)
                for (var z = 0; z < size; z++)
                {
                    var inner = x >= shell && x < size - shell && z >= shell && z < size - shell
                                && y >= shell && (openTop ? y < size : y < size - shell);
                    if (openTop && y >= size - shell && inner) continue;   // no cap: leave the rim open
                    world[(originX + x, baseY + y, originZ + z)] = inner ? StationaryLava : Obsidian;
                }
        return world;
    }

    [Test]
    public async Task A_sealed_obsidian_casing_around_lava_is_a_core()
    {
        var found = CoreSuggester.Gather(Core());

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].LavaBlocks).IsEqualTo(27);          // 3×3×3
        await Assert.That(found[0].Shell).IsEqualTo(1);
        await Assert.That((found[0].Casing.Width, found[0].Casing.Height, found[0].Casing.Depth)).IsEqualTo((5, 5, 5));
        await Assert.That(found[0].OpenTop).IsFalse();
    }

    [Test]
    public async Task An_unsealed_casing_is_not_a_core()
    {
        // One block of the seal removed and the lava is a pool, which is what most map lava is. It has to be a
        // block the lava actually touches: the casing's floor corners are not part of the seal.
        var world = Core();
        world.Remove((0, 21, 2));
        await Assert.That(CoreSuggester.Gather(world)).IsEmpty();
    }

    [Test]
    public async Task A_casing_of_the_wrong_material_is_not_a_core()
    {
        var world = Core();
        foreach (var key in world.Where(entry => entry.Value == Obsidian).Select(entry => entry.Key).ToList())
            world[key] = Stone;
        await Assert.That(CoreSuggester.Gather(world)).IsEmpty();
    }

    [Test]
    public async Task Lava_with_no_casing_at_all_is_not_a_core()
    {
        var world = new Dictionary<(int X, int Y, int Z), int>();
        for (var x = 0; x < 8; x++)
            for (var z = 0; z < 8; z++)
                world[(x, 20, z)] = StationaryLava;
        await Assert.That(CoreSuggester.Gather(world)).IsEmpty();
    }

    [Test]
    public async Task A_cased_lava_lake_is_refused_on_volume()
    {
        // The enclosure test alone would accept an arbitrarily large sealed reservoir; a casing is a container.
        var world = new Dictionary<(int X, int Y, int Z), int>();
        const int span = 12;   // 10×10×10 = 1000 lava, past MaxLavaBlocks
        for (var x = 0; x < span; x++)
            for (var y = 0; y < span; y++)
                for (var z = 0; z < span; z++)
                {
                    var inner = x > 0 && y > 0 && z > 0 && x < span - 1 && y < span - 1 && z < span - 1;
                    world[(x, 20 + y, z)] = inner ? StationaryLava : Obsidian;
                }

        await Assert.That(world.Count(entry => entry.Value == StationaryLava)).IsGreaterThan(CoreSuggester.MaxLavaBlocks);
        await Assert.That(CoreSuggester.Gather(world)).IsEmpty();
    }

    [Test]
    public async Task Two_cores_are_two_proposals()
    {
        var world = Core();
        foreach (var (cell, id) in Core(originX: 60, originZ: 60)) world[cell] = id;

        await Assert.That(CoreSuggester.Gather(world).Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_thicker_shell_is_measured()
    {
        var found = CoreSuggester.Gather(Core(size: 7, shell: 2));

        await Assert.That(found.Count).IsEqualTo(1);
        await Assert.That(found[0].Shell).IsEqualTo(2);
        await Assert.That(found[0].LavaBlocks).IsEqualTo(27);          // 7 − 2·2 = 3 across
    }

    [Test]
    public async Task Float_is_the_air_under_the_casing()
    {
        // A core resting on ground floats 0; the same casing over void floats the whole way down.
        var floating = CoreSuggester.Gather(Core(baseY: 20));
        await Assert.That(floating[0].Float).IsGreaterThan(0);

        var resting = Core(baseY: 20);
        for (var x = 0; x < 5; x++)
            for (var z = 0; z < 5; z++)
                resting[(x, 19, z)] = Stone;
        await Assert.That(CoreSuggester.Gather(resting)[0].Float).IsEqualTo(0);
    }

    [Test]
    public async Task An_empty_world_yields_nothing()
        => await Assert.That(CoreSuggester.Gather(new Dictionary<(int X, int Y, int Z), int>())).IsEmpty();
}
