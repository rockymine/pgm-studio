using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The three edits a dressing document takes one placement at a time. Each is asserted as the property it
/// has to hold rather than against a document a particular scene produced: an addition goes on the end
/// because the pass runs in placement order, a replacement keeps its position for the same reason, and every
/// id a document hands out is one no other placement holds.
/// </summary>
public sealed class DressingEditTests
{
    private static DressingDoc Doc(params PlacedProp[] props) => new() { Props = [.. props] };

    private static DressingDoc AddAll(params PlacedProp[] props)
    {
        var doc = DressingDoc.Empty;
        foreach (var prop in props) doc = DressingEdit.Add(doc, prop).Doc!;
        return doc;
    }

    /// <summary>An id is minted from the kind, so a document read by hand says what each placement is, and
    /// the number is the lowest one free rather than a count — a document that has lost a prop reuses the
    /// hole rather than climbing forever.</summary>
    [Test]
    public async Task A_placement_with_no_id_is_named_for_its_kind()
    {
        var doc = AddAll(new TreeProp(), new TreeProp(), new BoulderProp());

        await Assert.That(doc.Props.Select(prop => prop.Id).ToList())
            .IsEquivalentTo(new[] { "tree-1", "tree-2", "boulder-1" });
    }

    /// <summary>A stated id is kept, because a client that minted one on a canvas is holding it already. A
    /// stated id another placement holds is not: two props answering to one name is a document where an edit
    /// addresses both.</summary>
    [Test]
    public async Task A_stated_id_is_kept_unless_it_is_taken()
    {
        var doc = AddAll(new TreeProp { Id = "oak-by-the-gate" }, new TreeProp { Id = "oak-by-the-gate" });

        await Assert.That(doc.Props[0].Id).IsEqualTo("oak-by-the-gate");
        await Assert.That(doc.Props[1].Id).IsEqualTo("tree-1");
        await Assert.That(doc.Props.Select(prop => prop.Id).Distinct().Count()).IsEqualTo(2);
    }

    /// <summary>An addition goes on the end. The pass places a group at a time in placement order, so where a
    /// prop sits in the list is what it may stand on.</summary>
    [Test]
    public async Task An_addition_goes_on_the_end()
    {
        var doc = AddAll(new TreeProp(), new BoulderProp());
        var grown = DressingEdit.Add(doc, new FloraProp()).Doc!;

        await Assert.That(grown.Props[^1].Id).IsEqualTo("flora-1");
        await Assert.That(grown.Props.Count).IsEqualTo(3);
    }

    /// <summary>A replacement keeps its position and its id. Moving an edited prop to the end would move it
    /// past everything the pass places after it, so editing a tree could change what it is allowed to stand
    /// on.</summary>
    [Test]
    public async Task A_replacement_keeps_its_place_in_the_order()
    {
        var doc = AddAll(new TreeProp(), new BoulderProp(), new FloraProp());
        var edited = DressingEdit.Replace(doc, "boulder-1", new BoulderProp { X = 40, Z = 40 }).Doc!;

        await Assert.That(edited.Props[1].Id).IsEqualTo("boulder-1");
        await Assert.That(((BoulderProp)edited.Props[1]).X).IsEqualTo(40);
        await Assert.That(edited.Props.Select(prop => prop.Id).ToList())
            .IsEquivalentTo(new[] { "tree-1", "boulder-1", "flora-1" });
    }

    /// <summary>A removal takes the placement and leaves the recipe: a key is shared by every placement
    /// wearing it, so dropping it with the last one would strip a style the author is still using
    /// elsewhere.</summary>
    [Test]
    public async Task A_removal_leaves_the_recipes_alone()
    {
        var doc = Doc(new TreeProp { Id = "tree-1", StyleKey = "oak" }) with
        {
            Styles = new Dictionary<string, PropStyle> { ["oak"] = new TreeStyle() },
        };

        var pruned = DressingEdit.Remove(doc, "tree-1").Doc!;

        await Assert.That(pruned.Props).IsEmpty();
        await Assert.That(pruned.Styles.ContainsKey("oak")).IsTrue();
    }

    /// <summary>An id no placement holds is answered rather than thrown on: a stale id is the ordinary case
    /// for a client that read the document a moment ago, and it is what the route turns into a 404.</summary>
    [Test]
    public async Task An_id_that_names_nothing_does_not_apply()
    {
        var doc = AddAll(new TreeProp());

        await Assert.That(DressingEdit.Replace(doc, "tree-9", new TreeProp()).Applied).IsFalse();
        await Assert.That(DressingEdit.Remove(doc, "tree-9").Applied).IsFalse();
        await Assert.That(DressingEdit.Remove(doc, "tree-1").Applied).IsTrue();
    }
}
