using PgmStudio.Pgm;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// What <c>FilterEditor.CreateFilter</c> refuses. The intent generators are its only callers, so every filter
/// a map carries is built here — a dangling reference would be a rule that silently never fires, which is
/// the one failure a map cannot show its author.
/// </summary>
public sealed class FilterEditorTests
{
    private static Dict Map() => Serializer.ToDict(MapParser.ParseXmlString("""
        <?xml version="1.0"?>
        <map proto="1.4.0"><name>t</name><version>1</version><objective>o</objective>
        <regions><cuboid id="zone" min="0,0,0" max="4,4,4"/></regions>
        <filters><material id="is-stone">stone</material></filters></map>
        """));

    private static async Task RefusesWith(string rule, Func<object> edit)
    {
        var fault = Assert.Throws<EditException>(() => edit());
        await Assert.That(fault!.Finding.Rule).IsEqualTo(rule);
    }

    [Test]
    public async Task A_type_pgm_does_not_know_is_unreadable() =>
        await RefusesWith("RQ1", () => FilterEditor.CreateFilter(Map(), new Dict { ["id"] = "f", ["type"] = "sideways" }));

    [Test]
    public async Task An_id_already_in_the_registry_conflicts() =>
        await RefusesWith("RQ5", () => FilterEditor.CreateFilter(Map(), new Dict { ["id"] = "is-stone", ["type"] = "void" }));

    [Test]
    public async Task A_filter_naming_one_that_is_not_there_is_unresolved() =>
        await RefusesWith("ED1", () => FilterEditor.CreateFilter(Map(),
            new Dict { ["id"] = "f", ["type"] = "all", ["children"] = new List<object?> { "ghost" } }));

    [Test]
    public async Task A_filter_naming_itself_is_unresolved() =>
        await RefusesWith("ED1", () => FilterEditor.CreateFilter(Map(),
            new Dict { ["id"] = "loop", ["type"] = "not", ["child"] = "loop" }));

    [Test]
    public async Task A_region_filter_naming_no_region_is_unresolved() =>
        await RefusesWith("ED1", () => FilterEditor.CreateFilter(Map(),
            new Dict { ["id"] = "f", ["type"] = "region", ["region"] = "ghost" }));

    /// <summary>And the builtins resolve without being in the registry, which is what lets a generated rule
    /// wrap <c>never</c> or <c>always</c> without declaring them first.</summary>
    [Test]
    public async Task A_builtin_resolves_without_being_declared()
    {
        var doc = Map();
        FilterEditor.CreateFilter(doc, new Dict { ["id"] = "no-one", ["type"] = "not", ["child"] = "always" });
        await Assert.That((doc["filters"] as Dict)!.Keys).Contains("no-one");
    }

    /// <summary>An unnamed filter is given the first free <c>&lt;type&gt;_n</c>, so a generator can add one
    /// without inventing an id.</summary>
    [Test]
    public async Task An_unnamed_filter_is_numbered_by_its_type()
    {
        var doc = Map();
        var first = FilterEditor.CreateFilter(doc, new Dict { ["type"] = "void" });
        var second = FilterEditor.CreateFilter(doc, new Dict { ["type"] = "void" });
        await Assert.That(first["id"]).IsEqualTo("void_1");
        await Assert.That(second["id"]).IsEqualTo("void_2");
    }
}
