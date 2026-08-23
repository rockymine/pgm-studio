using PgmStudio.Analysis.Playability;
using PgmStudio.Api.Services;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The goals an author has stated, read as places. What has to hold is that a goal the document also carries
/// is recognised as the same goal: the two are matched by kind, name and owner, so a name derived here that
/// the generators would spell differently counts one wool twice and sends every read chasing a journey to a
/// place nothing is.
/// </summary>
public sealed class DeclaredGoalsTests
{
    private static Dict TeamDoc(string id, string colour) => new()
    {
        ["teams"] = new List<object?> { new Dict { ["id"] = id, ["color"] = colour } },
    };

    [Test]
    public async Task A_wool_is_named_the_way_the_generator_names_it()
    {
        // A wool stating no colour of its own takes its team's, and a two-word colour slugs with an
        // underscore. Asserted against the generator itself rather than against a copy of its rule.
        var doc = TeamDoc("blue-team", "Light Blue");
        var wool = new WoolIntent { Owner = "blue-team", Spawn = new Pt(4, 12, 9) };

        var declared = DeclaredGoals.Of(doc, new MapIntent { Wools = [wool] }).Single();

        await Assert.That(declared.Name).IsEqualTo(WoolGenerator.ColorSlug(doc, wool));
        await Assert.That(declared.Name).IsEqualTo("light_blue");
        await Assert.That(declared.Owner).IsEqualTo("blue-team");
    }

    [Test]
    public async Task A_wool_the_document_already_carries_is_not_counted_twice()
    {
        var doc = TeamDoc("blue-team", "Light Blue");
        doc["wools"] = new List<object?>
        {
            new Dict
            {
                ["color"] = "light_blue", ["team"] = "blue-team",
                ["location"] = new Dict { ["x"] = 4.0, ["y"] = 12.0, ["z"] = 9.0 },
            },
        };
        var intent = new MapIntent { Wools = [new WoolIntent { Owner = "blue-team", Spawn = new Pt(4, 12, 9) }] };

        var points = NavPoints.Of(doc, (-64, -64, 64, 64), DeclaredGoals.Of(doc, intent));

        await Assert.That(points.Count(point => point.Kind == "wool")).IsEqualTo(1);
    }

    [Test]
    public async Task A_destroyable_and_a_core_are_declared_from_their_anchors()
    {
        var doc = TeamDoc("red-team", "Red");
        var intent = new MapIntent
        {
            Destroyables = [new DestroyableIntent { Owner = "red", Name = "Cairn", Anchor = new Pt(-20, 12, 90) }],
            Cores = [new CoreIntent { Owner = "red", Anchor = new Pt(30, 12, -40) }],
        };

        var goals = DeclaredGoals.Of(doc, intent);

        await Assert.That(goals.Single(g => g.Kind == "destroyable").Cell).IsEqualTo((-20, 90));
        await Assert.That(goals.Single(g => g.Kind == "core").Cell).IsEqualTo((30, -40));
        await Assert.That(goals.All(g => g.Owner == "red-team")).IsTrue();
    }
}
