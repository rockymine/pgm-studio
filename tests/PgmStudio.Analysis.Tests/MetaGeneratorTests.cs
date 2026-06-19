using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Analysis.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Map-identity slice. Asserts name + the auto-derived version/gamemode/objective. (Author username→uuid
/// resolution is async via MojangClient and lives in the intent endpoint, not here.)
/// </summary>
public sealed class MetaGeneratorTests
{
    [Test]
    public async Task Sets_name_and_fixed_version_gamemode()
    {
        var doc = new Dict();
        MetaGenerator.Apply(doc, new MapIntent { Meta = new MetaIntent { Name = "Thunder Blank" } });

        await Assert.That(doc["name"]).IsEqualTo("Thunder Blank");
        await Assert.That(doc["version"]).IsEqualTo("1.0.0");
        await Assert.That(doc["gamemode"]).IsEqualTo("ctw");
    }

    [Test]
    public async Task Objective_is_singular_for_one_wool()
    {
        var doc = new Dict();
        MetaGenerator.Apply(doc, new MapIntent
        {
            Meta = new MetaIntent { Name = "M" },
            Wools = [new WoolIntent { Owner = "red-team" }],
        });
        await Assert.That(doc["objective"]).IsEqualTo("Capture the wool!");
    }

    [Test]
    public async Task Objective_is_plural_for_multiple_wools()
    {
        var doc = new Dict();
        MetaGenerator.Apply(doc, new MapIntent
        {
            Meta = new MetaIntent { Name = "M" },
            Wools = [new WoolIntent { Owner = "red-team" }, new WoolIntent { Owner = "blue-team" }],
        });
        await Assert.That(doc["objective"]).IsEqualTo("Capture the enemies' wools!");
    }

    [Test]
    public async Task No_meta_leaves_doc_untouched()
    {
        var doc = new Dict { ["name"] = "existing" };
        MetaGenerator.Apply(doc, new MapIntent());
        await Assert.That(doc["name"]).IsEqualTo("existing");
        await Assert.That(doc.ContainsKey("version")).IsFalse();
    }
}
