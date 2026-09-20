using System.Xml.Linq;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;
using PgmStudio.Geom;

/// <summary>
/// Build-slice generator (declarative authoring). Asserts the not-build-area structure and the
/// mirror property: the generated rectangles read back as <c>build</c>. See
/// docs/pgm/new-map-authoring.md §5 and filter-region-wiring.md template 1.
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

    /// <summary>The regression the dict-level assertions above cannot make: the fault was not in the
    /// document, it was in what the document serialised to. A `void` filter is trivial and XmlWriter never
    /// gives it an id (B15); a filter referenced by two parents is hoisted into the &lt;filters&gt; block by
    /// the >= 2 rule and every reference to it is written as &lt;filter id="..."/&gt;. A named void filter
    /// shared by the place side and the break side met both rules at once: the definition came out as a bare
    /// &lt;void/&gt; with the id stripped and two references pointed at nothing. The map is well-formed, it
    /// round-trips, and PGM refuses it at load.
    ///
    /// <para>So the assertion is about the XML and not about the ids: every filter a document references
    /// resolves to one it defines.</para></summary>
    [Test]
    public async Task Every_filter_reference_in_the_written_xml_resolves()
    {
        var doc = Map();
        BuildGenerator.Apply(doc, Intent());
        var xml = XDocument.Parse(XmlWriter.ToXml(Deserializer.FromDict(doc)));

        var defined = xml.Descendants()
            .Where(e => e.Name.LocalName != "filter" && e.Attribute("id") is not null)
            .Select(e => e.Attribute("id")!.Value).ToHashSet();
        var referenced = xml.Descendants("filter")
            .Where(e => e.Attribute("id") is not null && !e.HasElements)
            .Select(e => e.Attribute("id")!.Value).ToList();
        foreach (var rule in xml.Descendants("apply"))
            foreach (var name in new[] { "block", "block-place", "block-break" })
                if (rule.Attribute(name)?.Value is { } v && !v.StartsWith("deny(") && !v.StartsWith("allow("))
                    referenced.Add(v);

        await Assert.That(referenced).IsNotEmpty();
        foreach (var id in referenced.Distinct())
            await Assert.That(defined).Contains(id)
                .Because($"<filter id=\"{id}\"/> is referenced and nothing defines it");
    }

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

        await Assert.That(((Dict)Filters(doc)["block-place-void-filter"]!)["type"]).IsEqualTo("not");
        await Assert.That(((Dict)Filters(doc)["__bvf-void-place"]!)["type"]).IsEqualTo("void");
        await Assert.That(Filters(doc).ContainsKey("is-void")).IsFalse()
            .Because("a shared named void filter is hoisted into the block and written without its id, and both references then dangle");

        var rule = Rules(doc).OfType<Dict>().Single(r => r.GetValueOrDefault("region") as string == "not-build-area");
        await Assert.That(rule["block_place"]).IsEqualTo("block-place-void-filter");
        await Assert.That(rule["block_break"]).IsEqualTo("block-break-void-filter");
        await Assert.That(rule.ContainsKey("block")).IsFalse()
            .Because("one filter over both scopes is what seals a canopy over the void");
        await Assert.That(doc["max_build_height"]).IsEqualTo(24);
    }

    [Test]
    public async Task Caps_the_build_height_at_the_max()
    {
        var doc = Map();
        BuildGenerator.Apply(doc, Intent(maxHeight: 150));   // over-range → capped
        await Assert.That(doc["max_build_height"]).IsEqualTo(BuildGenerator.MaxBuildHeight);

        var doc2 = Map();
        BuildGenerator.Apply(doc2, Intent(maxHeight: 64));   // in-range → kept
        await Assert.That(doc2["max_build_height"]).IsEqualTo(64);
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
        var afterOne = (Regions(doc).Count, Filters(doc).Count, Rules(doc).Count);

        BuildGenerator.Apply(doc, Intent());

        await Assert.That(Regions(doc).Keys.Count(k => k.StartsWith("build-area-"))).IsEqualTo(3);
        await Assert.That(Rules(doc).Count).IsEqualTo(1);
        await Assert.That((Regions(doc).Count, Filters(doc).Count, Rules(doc).Count)).IsEqualTo(afterOne)
            .Because("a re-apply clears exactly what it wrote, however many filters that is");
    }

    /// <summary><b>Breaking over the void is not the same question as building over it.</b> A void rule that
    /// covers both scopes seals whatever the dressing stage left hanging past a coast — a canopy, a flower on
    /// a ledge — for the rest of the match, since nothing out there has a block at y=0. The break side
    /// therefore carries an exception for what decoration puts there, and for nothing that forms terrain: a
    /// crag is a shape the author built and a team may not mine it away.</summary>
    [Test]
    public async Task Breaking_over_the_void_admits_what_decoration_left_there_and_no_terrain()
    {
        var doc = Map();
        BuildGenerator.Apply(doc, Intent());

        var breakable = (Dict)Filters(doc)["block-break-void-filter"]!;
        await Assert.That(breakable["type"]).IsEqualTo("any");
        await Assert.That((List<object?>)breakable["children"]!).IsEquivalentTo(new object?[] { "__ovb-over-void", "block-place-void-filter" })
            .Because("allow over ground exactly as placing does, and over the void only what is listed");

        var overVoid = (Dict)Filters(doc)["__ovb-over-void"]!;
        await Assert.That(overVoid["type"]).IsEqualTo("all");
        await Assert.That((List<object?>)overVoid["children"]!).Contains("__bvf-void-break")
            .Because("its own inline void, not the place side's -- one filter with two parents is the one that dangles");

        var materials = Filters(doc).Where(f => f.Key.StartsWith("__ovb-") && ((Dict)f.Value!)["type"] as string == "material")
            .Select(f => ((Dict)f.Value!)["material"] as string).ToList();
        await Assert.That(materials).Contains("leaves");
        await Assert.That(materials).Contains("log");
        await Assert.That(materials).Contains("long grass").Because("the flora overlay reaches past a coast too");
        foreach (var terrain in (string[])["stone", "dirt", "grass", "sandstone", "stained clay"])
            await Assert.That(materials).DoesNotContain(terrain)
                .Because("the exception is for what decoration left over the void, not for the board itself");
    }

    [Test]
    public async Task No_build_intent_emits_nothing()
    {
        var doc = Map();
        BuildGenerator.Apply(doc, new MapIntent());

        await Assert.That(Regions(doc).Count).IsEqualTo(0);
        await Assert.That(Rules(doc).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Holes_emit_a_complement_subtracted_from_the_union()
    {
        var doc = Map();
        BuildGenerator.Apply(doc, new MapIntent
        {
            Build = new BuildIntent
            {
                Areas = [new Rect(0, 0, 50, 50), new Rect(-50, -50, 0, 0)],
                Holes = [new Rect(10, 10, 20, 20)],
            },
        });

        // buildable = complement(build-area, build-hole-1); first child is the base, rest subtracted
        var comp = (Dict)Regions(doc)["buildable"]!;
        await Assert.That(comp["type"]).IsEqualTo("complement");
        var kids = ((List<object?>)comp["children"]!).Cast<string>().ToList();
        await Assert.That(kids[0]).IsEqualTo("build-area");
        await Assert.That(kids).Contains("build-hole-1");

        // the void wrapper now wraps the complement, not the bare union
        var neg = (Dict)Regions(doc)["not-build-area"]!;
        await Assert.That(((List<object?>)neg["children"]!).Single()).IsEqualTo("buildable");
    }

    [Test]
    public async Task Holes_and_complement_read_back_as_build()   // mirror property with a cutout
    {
        var doc = Map();
        BuildGenerator.Apply(doc, new MapIntent
        {
            Build = new BuildIntent
            {
                Areas = [new Rect(0, 0, 50, 50), new Rect(-50, -50, 0, 0)],
                Holes = [new Rect(10, 10, 20, 20)],
            },
        });
        var facets = RegionCategorizer.DeriveFacets(doc);

        await Assert.That(facets["buildable"].Category).IsEqualTo("build");
        await Assert.That(facets["build-area"].Category).IsEqualTo("build");
        await Assert.That(facets["build-hole-1"].Category).IsEqualTo("build");
        await Assert.That(facets["not-build-area"].Category).IsEqualTo("other");
    }

    [Test]
    public async Task Holes_cleared_on_reapply()
    {
        var doc = Map();
        var intent = new MapIntent { Build = new BuildIntent { Areas = [new Rect(0, 0, 50, 50), new Rect(-50, -50, 0, 0)], Holes = [new Rect(10, 10, 20, 20)] } };
        BuildGenerator.Apply(doc, intent);
        BuildGenerator.Apply(doc, intent);

        await Assert.That(Regions(doc).Keys.Count(k => k.StartsWith("build-hole-"))).IsEqualTo(1);
        await Assert.That(Regions(doc).Keys.Count(k => k == "buildable")).IsEqualTo(1);
    }

    // ── what a board says about the void, and what it may not say twice ────────────────────────

    [Test]
    public async Task No_build_area_writes_no_region_and_no_rule()
    {
        // The void boundary is the edge of the buildable region, so a board that declares none states
        // nothing about the void: no region, no apply rule, and the height cap regardless.
        var doc = Map();
        BuildGenerator.Apply(doc, new MapIntent { Build = new BuildIntent { MaxHeight = 40 } });

        await Assert.That(Regions(doc).Count).IsEqualTo(0);
        await Assert.That(Rules(doc).Count).IsEqualTo(0);
        await Assert.That(doc["max_build_height"]).IsEqualTo(40);
    }

    [Test]
    public async Task A_build_area_writes_exactly_one_void_rule()
    {
        // One rule, over not-build-area, in the shape template.xml writes. A second rule scoped wider
        // would be the deciding one rather than a second opinion: PGM stops at the first that decides.
        var doc = Map();
        BuildGenerator.Apply(doc, Intent());

        var rule = Rules(doc).OfType<Dict>().Single();
        await Assert.That(rule["region"]).IsEqualTo("not-build-area");
        await Assert.That(rule["block_place"]).IsEqualTo("block-place-void-filter");
        await Assert.That(rule["block_break"]).IsEqualTo("block-break-void-filter");
    }

    [Test]
    public async Task A_stored_everywhere_void_region_is_scrubbed_on_apply()
    {
        // A document carrying the wider region keeps it otherwise, and then holds two void rules of which
        // the wider one decides — denying placement inside the very rectangles the board declared buildable.
        var doc = Map();
        Regions(doc)["void-enforcement-area"] = new Dict { ["id"] = "void-enforcement-area", ["type"] = "everywhere" };
        Regions(doc)["void-enforcement-exclusion-1"] = new Dict { ["id"] = "void-enforcement-exclusion-1", ["type"] = "rectangle" };
        Rules(doc).Add(new Dict { ["block_place"] = "deny(void)", ["region"] = "void-enforcement-area" });

        BuildGenerator.Apply(doc, Intent());

        await Assert.That(Regions(doc).ContainsKey("void-enforcement-area")).IsFalse();
        await Assert.That(Regions(doc).ContainsKey("void-enforcement-exclusion-1")).IsFalse();
        await Assert.That(Rules(doc).Count).IsEqualTo(1);
        await Assert.That(Rules(doc).OfType<Dict>().Single()["region"]).IsEqualTo("not-build-area");
    }
}
