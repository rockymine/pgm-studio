using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The destroyable slice of the declarative generator: a resolved intent projects to a
/// <c>&lt;destroyable&gt;</c> plus the cuboid region that scopes it — the structure's own box, so the goal's
/// blocks and the region containing them cannot disagree.
/// </summary>
public sealed class DestroyableGeneratorTests
{
    private static DestroyableIntent Sample(BlockBox? box) => new()
    {
        Owner = "red", Name = "Red Monument", Style = "pillar-3", Materials = "obsidian",
        Anchor = new Pt(10, 12, 20), Float = 4, Box = box,
    };

    private static Dict Generate(params DestroyableIntent[] destroyables)
    {
        var doc = new Dict();
        DestroyableGenerator.Apply(doc, new MapIntent { Destroyables = [.. destroyables] });
        return doc;
    }

    private static List<object?> Destroyables(Dict doc) => (List<object?>)doc["destroyables"]!;
    private static Dict Regions(Dict doc) => (Dict)doc["regions"]!;

    [Test]
    public async Task The_region_is_the_structures_own_box_with_a_max_one_past_the_last_block()
    {
        // A 1×3×1 pillar occupying x=10, y=16..18, z=20.
        var doc = Generate(Sample(new BlockBox(10, 16, 20, 10, 18, 20)));

        var d = (Dict)Destroyables(doc)[0]!;
        var region = (Dict)Regions(doc)[(string)d["region"]!]!;
        await Assert.That((string)region["type"]!).IsEqualTo("cuboid");

        var min = (Dict)region["min"]!;
        var max = (Dict)region["max"]!;
        await Assert.That(((double)min["x"]!, (double)min["y"]!, (double)min["z"]!)).IsEqualTo((10d, 16d, 20d));
        // A PGM cuboid spans [min, max), so the max is one past the pillar on every axis — a max of
        // (10,18,20) would scope a region the pillar's top two blocks fall outside of.
        await Assert.That(((double)max["x"]!, (double)max["y"]!, (double)max["z"]!)).IsEqualTo((11d, 19d, 21d));
    }

    [Test]
    public async Task The_destroyable_carries_its_owner_name_materials_and_region()
    {
        var doc = Generate(Sample(new BlockBox(10, 16, 20, 10, 18, 20)));
        var d = (Dict)Destroyables(doc)[0]!;
        await Assert.That((string)d["owner"]!).IsEqualTo("red");
        await Assert.That((string)d["name"]!).IsEqualTo("Red Monument");
        await Assert.That((string)d["materials"]!).IsEqualTo("obsidian");
        await Assert.That((string)d["region"]!).IsEqualTo($"{(string)d["id"]!}-region");
    }

    [Test]
    public async Task An_unresolved_box_emits_nothing_rather_than_a_guessed_region()
    {
        // A region that misses its structure is a zero-health goal PGM accepts with only a warning, so a
        // destroyable that never reached the world build must not be emitted at all.
        var doc = Generate(Sample(box: null));
        await Assert.That(Destroyables(doc)).IsEmpty();
    }

    [Test]
    public async Task Regenerating_replaces_rather_than_appends()
    {
        var doc = new Dict();
        var intent = new MapIntent { Destroyables = [Sample(new BlockBox(10, 16, 20, 10, 18, 20))] };
        DestroyableGenerator.Apply(doc, intent);
        DestroyableGenerator.Apply(doc, intent);
        await Assert.That(Destroyables(doc).Count).IsEqualTo(1);
        await Assert.That(Regions(doc).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Two_destroyables_named_the_same_still_get_distinct_ids()
    {
        // The compiler's owner-and-index naming rules this out, but an authored name can collide — and a
        // duplicate id silently re-points every reference to whichever won.
        var box = new BlockBox(10, 16, 20, 10, 18, 20);
        var doc = Generate(Sample(box), Sample(box));
        var ids = Destroyables(doc).Cast<Dict>().Select(d => (string)d["id"]!).ToList();
        await Assert.That(ids.Distinct().Count()).IsEqualTo(2);
        await Assert.That(Regions(doc).Count).IsEqualTo(2);
    }
}
