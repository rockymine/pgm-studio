using PgmStudio.Analysis;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Analysis.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Build-slice generator (declarative authoring). Asserts the void-enforcement structure and the
/// mirror property: the generated rectangles read back as <c>build</c>. See
/// docs/contracts/new-map-authoring.md §5 and filter-region-wiring.md template 1.
/// </summary>
public sealed class BuildGeneratorTests
{
    private static Dict Map() => new()
    {
        ["regions"] = new Dict(), ["filters"] = new Dict(), ["apply_rules"] = new List<object?>(),
    };

    private static MapIntent Intent(int? maxHeight = 24) => new()
    {
        Build = new BuildIntent
        {
            MaxHeight = maxHeight,
            Areas = [new Rect(0, 0, 50, 50), new Rect(-50, -50, 0, 0), new Rect(-5, 0, 5, 50)],
        },
    };

    private static Dict Regions(Dict d) => (Dict)d["regions"]!;
    private static Dict Filters(Dict d) => (Dict)d["filters"]!;
    private static List<object?> Rules(Dict d) => (List<object?>)d["apply_rules"]!;

    [Test]
    public async Task Builds_union_negative_voidfilter_and_rule()
    {
        var doc = Map();
        BuildGenerator.Apply(doc, Intent());

        await Assert.That(Regions(doc).Keys.Count(k => k.StartsWith("build-area-"))).IsEqualTo(3);   // 3 build rectangles
        var union = (Dict)Regions(doc)["build-area"]!;
        await Assert.That(union["type"]).IsEqualTo("union");
        await Assert.That(((List<object?>)union["children"]!).Count).IsEqualTo(3);

        var neg = (Dict)Regions(doc)["not-build-area"]!;
        await Assert.That(neg["type"]).IsEqualTo("negative");
        await Assert.That(((List<object?>)neg["children"]!).Single()).IsEqualTo("build-area");

        await Assert.That(((Dict)Filters(doc)["no-void"]!)["type"]).IsEqualTo("not");
        await Assert.That(((Dict)Filters(doc)["is-void"]!)["type"]).IsEqualTo("void");

        var rule = Rules(doc).OfType<Dict>().Single(r => r.GetValueOrDefault("region") as string == "not-build-area");
        await Assert.That(rule["block"]).IsEqualTo("no-void");
        await Assert.That(doc["max_build_height"]).IsEqualTo(24);
    }

    [Test]
    public async Task Generated_rectangles_read_back_as_build()   // mirror property
    {
        var doc = Map();
        BuildGenerator.Apply(doc, Intent());
        var facets = RegionCategorizer.DeriveFacets(doc);

        await Assert.That(facets["build-area-1"].Category).IsEqualTo("build");
        await Assert.That(facets["build-area-3"].Category).IsEqualTo("build");
        await Assert.That(facets["not-build-area"].Category).IsEqualTo("other");
        await Assert.That(facets["not-build-area"].Roles).Contains("rule_container");
    }

    [Test]
    public async Task Single_rectangle_skips_the_union()
    {
        var doc = Map();
        BuildGenerator.Apply(doc, new MapIntent { Build = new BuildIntent { Areas = [new Rect(0, 0, 10, 10)] } });

        await Assert.That(Regions(doc).ContainsKey("build-area")).IsFalse();   // no union for a lone rect
        var neg = (Dict)Regions(doc)["not-build-area"]!;
        await Assert.That(((List<object?>)neg["children"]!).Single()).IsEqualTo("build-area-1");
        await Assert.That(RegionCategorizer.DeriveFacets(doc)["build-area-1"].Category).IsEqualTo("build");
    }

    [Test]
    public async Task Reapplying_is_idempotent()
    {
        var doc = Map();
        BuildGenerator.Apply(doc, Intent());
        BuildGenerator.Apply(doc, Intent());

        await Assert.That(Regions(doc).Keys.Count(k => k.StartsWith("build-area-"))).IsEqualTo(3);
        await Assert.That(Filters(doc).Count).IsEqualTo(2);
        await Assert.That(Rules(doc).Count).IsEqualTo(1);
    }

    [Test]
    public async Task No_build_intent_emits_nothing()
    {
        var doc = Map();
        BuildGenerator.Apply(doc, new MapIntent());

        await Assert.That(Regions(doc).Count).IsEqualTo(0);
        await Assert.That(Rules(doc).Count).IsEqualTo(0);
    }
}
