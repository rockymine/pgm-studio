using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The contributors the export adds to a map: the builder of every copied tree standing in the world — by
/// account where the caller resolved one, and never twice for a person the map already credits — and nobody
/// else, the studio included.
/// </summary>
public sealed class TreeBuilderCreditsTests
{
    private static Dict Person(string name, string role, string uuid = "", string contribution = "")
        => new() { ["uuid"] = uuid, ["name"] = name, ["role"] = role, ["contribution"] = contribution };

    private static List<Dict> People(Dict doc) => [.. ((IEnumerable<object?>)doc["authors"]!).Cast<Dict>()];

    [Test]
    public async Task Each_tree_builder_is_credited_after_the_stated_people()
    {
        var doc = new Dict { ["authors"] = new List<object?> { Person("Opus 5.5", "author") } };
        TreeBuilderCredits.Apply(doc, ["rockymine"],
            new Dictionary<string, (string Uuid, string Name)> { ["rockymine"] = ("0f00-uuid", "rockymine") });

        var people = People(doc);
        await Assert.That(people.Select(person => (person["name"], person["role"], person["contribution"], person["uuid"])))
            .IsEquivalentTo(new (object?, object?, object?, object?)[]
            {
                ("Opus 5.5", "author", "", ""),
                ("rockymine", "contributor", TreeBuilderCredits.TreeContribution, "0f00-uuid"),
            });
    }

    /// <summary>A builder no account answers for is credited by the name they were stated under, which
    /// <c>map.xml</c> writes as a pseudonym.</summary>
    [Test]
    public async Task A_builder_nobody_resolved_is_credited_by_name()
    {
        var doc = new Dict();
        TreeBuilderCredits.Apply(doc, ["rockymine"]);
        var builder = People(doc).Single(person => (string?)person["contribution"] == TreeBuilderCredits.TreeContribution);
        await Assert.That((builder["name"], builder["uuid"])).IsEqualTo(((object?)"rockymine", (object?)""));
    }

    /// <summary>A person the map already credits keeps the credit they were given, whichever role it is in and
    /// however it was spelled — by name or by the account behind it.</summary>
    [Test]
    public async Task A_person_the_map_already_credits_is_not_credited_again()
    {
        var doc = new Dict
        {
            ["authors"] = new List<object?>
            {
                Person("RockyMine", "author", uuid: "0f00-uuid"),
                Person("Opus 5.5", "contributor", contribution: "Everything"),
            },
        };
        TreeBuilderCredits.Apply(doc, ["rockymine", "OPUS 5.5"],
            new Dictionary<string, (string Uuid, string Name)> { ["rockymine"] = ("0f00-uuid", "rockymine") });
        await Assert.That(People(doc).Count).IsEqualTo(2);
    }

    /// <summary>A map may name as many builders as its recipes like, and anyone may ask for its export, so the
    /// names one export resolves to accounts are bounded.</summary>
    [Test]
    public async Task An_export_resolves_at_most_a_bounded_number_of_builders()
    {
        var styles = string.Join(",", Enumerable.Range(1, TreeBuilderCredits.MaxResolved + 2).Select(n =>
            $$$"""
            "t{{{n}}}":{"kind":"tree","form":"copied","body":[[0,0,0,17,0]],"builder":"builder{{{n}}}"}
            """));
        var layout = """{"dressing":{"props":[],"styles":{""" + styles + "}}}";
        var named = TreeBuilderCredits.Named(layout);
        await Assert.That(named.Count).IsEqualTo(TreeBuilderCredits.MaxResolved);
        await Assert.That(named.Distinct().Count()).IsEqualTo(named.Count);
    }

    // ── on the way to map.xml ─────────────────────────────────────────────────────────────────────────

    /// <summary>One plate, with a copied tree standing on it whose recipe names <paramref name="builder"/>, or
    /// names nobody.</summary>
    private static string Board(string? builder) =>
        """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
          {"id":"a","type":"rectangle","operation":"add","min_x":-60,"min_z":-20,"max_x":60,"max_z":20,"base_height":10}],
         "groups":[]} }],
         "dressing":{"styles":{"cut":{"kind":"tree","form":"copied","body":[[0,0,0,17,0],[0,1,0,17,0],[0,2,0,18,0]]
        """ + (builder is null ? "" : $",\"builder\":\"{builder}\"") + """
        }},"props":[{"kind":"tree","id":"t","x":0,"z":14,"seed":3,"style":"cut"}]}}
        """;

    private static MapIntent Intent() => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
        Spawns = [new SpawnIntent { Team = "red", Point = new Pt(-40, 11, 0), Yaw = 0 }],
        Wools = [new WoolIntent { Owner = "blue", Color = "blue", Spawn = new Pt(40, 11, 0) }],
    };

    private static Dict Doc() => new() { ["name"] = "m", ["version"] = "1.0.0", ["gamemode"] = new List<object?>() };

    /// <summary>A built map's <c>map.xml</c> credits the builder of a copied tree standing on it; the same
    /// board with a tree naming nobody credits nobody, and neither names the studio.</summary>
    [Test]
    public async Task A_built_map_credits_the_builder_of_its_trees_and_not_the_studio()
    {
        var credited = MapExportComposer.BuildAndCompose(Doc(), Board("rockymine"), Intent());
        await Assert.That(credited.Refusal).IsNull().Because($"it answered {credited.Refusal?.Message}");
        await Assert.That(credited.Xml!).Contains($"<contributor contribution=\"{TreeBuilderCredits.TreeContribution}\">rockymine</contributor>");
        await Assert.That(credited.Xml!).DoesNotContain("pgmstudio");

        var anonymous = MapExportComposer.BuildAndCompose(Doc(), Board(null), Intent());
        await Assert.That(anonymous.Xml!).DoesNotContain("<contributors>");
    }

    /// <summary>A map the studio did not author is left as its authors credited it.</summary>
    [Test]
    public async Task A_corpus_map_is_left_as_its_authors_credited_it()
    {
        var result = MapExportComposer.Compose(Doc(), null, isIntent: false, null, null, null, []);
        await Assert.That(result.Xml!).DoesNotContain("<contributors>");
    }
}
