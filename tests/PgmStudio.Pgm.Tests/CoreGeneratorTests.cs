using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The core slice of the declarative generator: a resolved intent projects to a <c>&lt;core&gt;</c> plus the
/// cuboid region scoping it — the casing's own box, so the lava and the region containing it agree.
/// </summary>
public sealed class CoreGeneratorTests
{
    private static CoreIntent Sample(BlockBox? box, string owner = "red", string name = "", int leak = 5) => new()
    {
        Owner = owner, Name = name, Anchor = new Pt(10, 12, 20),
        Size = 5, Height = 5, Shell = 1, Float = 6, Leak = leak, Box = box,
    };

    private static readonly BlockBox Box = new(8, 18, 18, 12, 22, 22);

    private static Dict Generate(params CoreIntent[] cores)
    {
        var doc = new Dict();
        CoreGenerator.Apply(doc, new MapIntent { Cores = [.. cores] });
        return doc;
    }

    private static List<object?> Cores(Dict doc) => (List<object?>)doc["cores"]!;
    private static Dict Regions(Dict doc) => (Dict)doc["regions"]!;

    [Test]
    public async Task The_region_is_the_casings_own_box_with_a_max_one_past_the_last_block()
    {
        var doc = Generate(Sample(Box));
        var region = (Dict)Regions(doc)[(string)((Dict)Cores(doc)[0]!)["region"]!]!;
        var min = (Dict)region["min"]!;
        var max = (Dict)region["max"]!;
        await Assert.That(((double)min["x"]!, (double)min["y"]!, (double)min["z"]!)).IsEqualTo((8d, 18d, 18d));
        // [min, max) — an inclusive max would leave the casing's far face outside its own region (OB13).
        await Assert.That(((double)max["x"]!, (double)max["y"]!, (double)max["z"]!)).IsEqualTo((13d, 23d, 23d));
    }

    [Test]
    public async Task The_owner_is_spelled_owner_in_the_doc_tree_and_team_only_in_the_XML()
    {
        // OB1's `team` spelling is the XML's alone; the doc tree says `owner` like everything else, and the
        // writer translates at the boundary. Emitting `team` here parses back as an unowned core.
        var d = (Dict)Cores(Generate(Sample(Box)))[0]!;
        await Assert.That((string)d["owner"]!).IsEqualTo("red");
        await Assert.That(d.ContainsKey("team")).IsFalse();

        // The claim that matters: the owner survives the whole doc → MapXml → XML trip.
        var xml = Pgm.XmlWriter.ToXml(Pgm.Deserializer.FromDict(Generate(Sample(Box))));
        await Assert.That(xml).Contains("team=\"red\"");
        await Assert.That(Pgm.MapParser.ParseXmlString(xml).Cores[0].Owner).IsEqualTo("red");
    }

    [Test]
    public async Task A_nameless_core_emits_no_name_so_PGM_names_it()
    {
        var d = (Dict)Cores(Generate(Sample(Box)))[0]!;
        await Assert.That(d.ContainsKey("name")).IsFalse();

        var named = (Dict)Cores(Generate(Sample(Box, name: "The Heart")))[0]!;
        await Assert.That((string)named["name"]!).IsEqualTo("The Heart");
    }

    [Test]
    public async Task No_material_is_emitted_because_PGM_defaults_to_obsidian()
    {
        await Assert.That(((Dict)Cores(Generate(Sample(Box)))[0]!).ContainsKey("material")).IsFalse();
    }

    [Test]
    public async Task Leak_is_emitted_only_when_it_differs_from_PGMs_own_default()
    {
        await Assert.That(((Dict)Cores(Generate(Sample(Box)))[0]!).ContainsKey("leak")).IsFalse();
        var custom = (Dict)Cores(Generate(Sample(Box, leak: 3)))[0]!;
        await Assert.That((long)custom["leak"]!).IsEqualTo(3L);
    }

    [Test]
    public async Task A_nameless_core_ids_off_its_owner_so_the_teams_do_not_collide()
    {
        var doc = Generate(Sample(Box, owner: "red"), Sample(Box, owner: "blue"));
        var ids = Cores(doc).Cast<Dict>().Select(d => (string)d["id"]!).ToList();
        await Assert.That(ids).IsEquivalentTo(new[] { "red-core", "blue-core" });
        await Assert.That(Regions(doc).Count).IsEqualTo(2);
    }

    [Test]
    public async Task An_unresolved_box_emits_nothing_rather_than_a_guessed_region()
    {
        await Assert.That(Cores(Generate(Sample(box: null)))).IsEmpty();
    }

    [Test]
    public async Task Regenerating_replaces_rather_than_appends()
    {
        var doc = new Dict();
        var intent = new MapIntent { Cores = [Sample(Box)] };
        CoreGenerator.Apply(doc, intent);
        CoreGenerator.Apply(doc, intent);
        await Assert.That(Cores(doc).Count).IsEqualTo(1);
        await Assert.That(Regions(doc).Count).IsEqualTo(1);
    }
}
