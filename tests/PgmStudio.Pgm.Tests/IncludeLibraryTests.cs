using PgmStudio.Domain;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// Include resolution (docs/contracts/include-resolution.md): splicing the shared fragments a map references
/// so the document describes the map <b>as played</b> rather than as written. The tests hold the two lines
/// that make that safe — a resolved parse can only ever add, and it never happens unless a library is given.
/// </summary>
public sealed class IncludeLibraryTests
{
    private string _dir = "";

    [Before(Test)]
    public void CreateLibrary() => _dir = Directory.CreateTempSubdirectory("includes-").FullName;

    [After(Test)]
    public void RemoveLibrary() { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }

    private void Fragment(string id, string body) =>
        File.WriteAllText(Path.Combine(_dir, id + ".xml"), $"<map>{body}</map>");

    private static string Map(string body) => $"""
        <map proto="1.5.0"><name>Fixture</name><version>1.0.0</version>{body}</map>
        """;

    private MapXml ParseResolved(string body) => MapParser.ParseXmlString(Map(body), IncludeLibrary.Open(_dir));

    [Test]
    public async Task Without_a_library_the_map_reads_exactly_as_written()
    {
        Fragment("extras", """<filters><material id="from-fragment">stone</material></filters>""");
        var map = MapParser.ParseXmlString(Map("""<include id="extras"/><filters><material id="own">dirt</material></filters>"""));

        await Assert.That(map.Includes).Contains("extras");     // referenced …
        await Assert.That(map.ResolvedIncludes).IsEmpty();      // … but never resolved
        await Assert.That(map.Filters.ContainsKey("from-fragment")).IsFalse();
    }

    [Test]
    public async Task An_absent_directory_is_simply_no_library()
        => await Assert.That(IncludeLibrary.Open(Path.Combine(_dir, "nope"))).IsNull();

    [Test]
    public async Task A_resolved_map_carries_the_fragments_content_and_still_names_them()
    {
        Fragment("extras", """<filters><material id="from-fragment">stone</material></filters>""");
        var map = ParseResolved("""<include id="extras"/><filters><material id="own">dirt</material></filters>""");

        await Assert.That(map.Filters.ContainsKey("from-fragment")).IsTrue();
        await Assert.That(map.Filters.ContainsKey("own")).IsTrue();
        // The reference survives so the map still knows what it pulled in — which is also why a resolved
        // document must not be re-exported.
        await Assert.That(map.Includes).Contains("extras");
        await Assert.That(map.ResolvedIncludes).Contains("extras");
    }

    [Test]
    public async Task The_maps_own_blocks_survive_a_fragment_spliced_ahead_of_them()
    {
        // The failure this guards: reading only the first <regions>/<filters> block silently drops whichever
        // lost the race, and `global` guarantees a fragment is always ahead of the map's own.
        Fragment("global", """<regions><rectangle id="global-region" min="0,0" max="1,1"/></regions>""");
        Fragment("extras", """<regions><rectangle id="fragment-region" min="2,2" max="3,3"/></regions>""");
        var map = ParseResolved("""<include id="extras"/><regions><rectangle id="own-region" min="4,4" max="5,5"/></regions>""");

        await Assert.That(map.Regions.Keys).Contains("own-region");
        await Assert.That(map.Regions.Keys).Contains("fragment-region");
        await Assert.That(map.Regions.Keys).Contains("global-region");
    }

    [Test]
    public async Task Resolving_can_only_add()
    {
        const string body = """
            <include id="extras"/>
            <filters><material id="own">dirt</material></filters>
            <regions>
              <rectangle id="own-region" min="4,4" max="5,5"/>
              <apply block="own" region="own-region"/>
            </regions>
            """;
        Fragment("extras", """
            <filters><material id="from-fragment">stone</material></filters>
            <regions><rectangle id="fragment-region" min="2,2" max="3,3"/><apply block="from-fragment" region="fragment-region"/></regions>
            """);

        var written = MapParser.ParseXmlString(Map(body));
        var played = ParseResolved(body);

        await Assert.That(played.Filters.Count).IsGreaterThan(written.Filters.Count);
        await Assert.That(played.Regions.Count).IsGreaterThan(written.Regions.Count);
        await Assert.That(played.ApplyRules.Count).IsGreaterThan(written.ApplyRules.Count);
    }

    [Test]
    public async Task Fragments_resolve_recursively()
    {
        Fragment("outer", """<include id="inner"/><filters><material id="outer-filter">stone</material></filters>""");
        Fragment("inner", """<filters><material id="inner-filter">dirt</material></filters>""");
        var map = ParseResolved("""<include id="outer"/>""");

        await Assert.That(map.Filters.ContainsKey("outer-filter")).IsTrue();
        await Assert.That(map.Filters.ContainsKey("inner-filter")).IsTrue();
        await Assert.That(map.ResolvedIncludes).Contains("inner");
    }

    [Test]
    public async Task The_global_fragment_is_spliced_without_any_map_naming_it()
    {
        Fragment("global", """<filters><material id="everywhere">bedrock</material></filters>""");
        var map = ParseResolved("<regions/>");

        await Assert.That(map.Filters.ContainsKey("everywhere")).IsTrue();
        await Assert.That(map.ResolvedIncludes).Contains(IncludeLibrary.GlobalInclude);
        await Assert.That(map.Includes).IsEmpty();    // referenced by nothing in the map
    }

    [Test]
    public async Task A_cycle_resolves_to_nothing_rather_than_recursing()
    {
        Fragment("a", """<include id="b"/><filters><material id="a-filter">stone</material></filters>""");
        Fragment("b", """<include id="a"/><filters><material id="b-filter">dirt</material></filters>""");
        var map = ParseResolved("""<include id="a"/>""");

        await Assert.That(map.Filters.ContainsKey("a-filter")).IsTrue();
        await Assert.That(map.Filters.ContainsKey("b-filter")).IsTrue();
    }

    [Test]
    public async Task An_unresolvable_reference_leaves_the_document_as_it_was()
    {
        var map = ParseResolved("""<include id="never-shipped"/><filters><material id="own">dirt</material></filters>""");

        await Assert.That(map.Includes).Contains("never-shipped");
        await Assert.That(map.ResolvedIncludes).DoesNotContain("never-shipped");
        await Assert.That(map.Filters.ContainsKey("own")).IsTrue();
    }

    [Test]
    public async Task An_objective_module_from_a_fragment_does_not_reject_the_map()
    {
        // The unread-objective gate stops a goal being lost in silence on round-trip. A module arriving from a
        // fragment is not at risk of that — the export re-emits the reference — so gating after the splice
        // would reject maps that parse and re-export perfectly.
        Fragment("bridge", "<score><limit>10</limit></score>");
        var map = ParseResolved("""<include id="bridge"/><regions/>""");

        await Assert.That(map.ResolvedIncludes).Contains("bridge");
    }
}
