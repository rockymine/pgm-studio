using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Map-identity slice. Asserts name + the auto-derived version/gamemode/objective — both of the latter
/// following which objective modules the intent actually carries, not a fixed CTW label (B131: a destroy or
/// core board used to ship "Capture the wool(s)!" regardless of what it actually defended). (Author
/// username→uuid resolution is async via MojangClient and lives in the intent endpoint, not here.)
/// </summary>
public sealed class MetaGeneratorTests
{
    private static DestroyableIntent Destroyable(string owner = "red", string name = "") =>
        new() { Owner = owner, Name = name, Materials = "obsidian", Anchor = new Pt(0, 8, 0), Float = 4 };

    private static CoreIntent Core(string owner = "red", string name = "") =>
        new() { Owner = owner, Name = name, Anchor = new Pt(0, 8, 0), Size = 5, Height = 5, Shell = 1, Float = 6, Leak = 5 };

    // doc["gamemode"] is a list, one id per element — never a single joined string (B155: PGM parses
    // <gamemode> as a repeated element and cannot resolve several ids written into one).
    private static List<string> Gamemode(Dict doc) => ((List<object?>)doc["gamemode"]!).Select(g => (string)g!).ToList();

    [Test]
    public async Task Sets_name_and_fixed_version()
    {
        var doc = new Dict();
        MetaGenerator.Apply(doc, new MapIntent { Meta = new MetaIntent { Name = "Thunder Blank" } });

        await Assert.That(doc["name"]).IsEqualTo("Thunder Blank");
        await Assert.That(doc["version"]).IsEqualTo("1.0.0");
    }

    [Test]
    public async Task A_single_wool_declares_ctw_and_the_singular_capture_line()
    {
        var doc = new Dict();
        MetaGenerator.Apply(doc, new MapIntent
        {
            Meta = new MetaIntent { Name = "M" },
            Wools = [new WoolIntent { Owner = "red-team" }],
        });
        await Assert.That(Gamemode(doc)).IsEquivalentTo(new[] { "ctw" });
        await Assert.That(doc["objective"]).IsEqualTo("Capture the wool!");
    }

    [Test]
    public async Task Several_wools_declare_the_plural_capture_line()
    {
        var doc = new Dict();
        MetaGenerator.Apply(doc, new MapIntent
        {
            Meta = new MetaIntent { Name = "M" },
            Wools = [new WoolIntent { Owner = "red-team" }, new WoolIntent { Owner = "blue-team" }],
        });
        await Assert.That(Gamemode(doc)).IsEquivalentTo(new[] { "ctw" });
        await Assert.That(doc["objective"]).IsEqualTo("Capture the enemies' wools!");
    }

    // B131 — the fault itself: a destroy board with no wool at all used to still ship "Capture the enemies'
    // wools!" because Objective(intent) only branched on the wool count, defaulting the zero case to the
    // plural line. This is the case that fails on the old behaviour.
    [Test]
    public async Task A_destroy_board_with_no_wool_declares_dtm_and_the_destroy_line_not_capture()
    {
        var doc = new Dict();
        MetaGenerator.Apply(doc, new MapIntent
        {
            Meta = new MetaIntent { Name = "M" },
            Destroyables = [Destroyable(name: "Red Monument")],
        });
        await Assert.That(Gamemode(doc)).IsEquivalentTo(new[] { "dtm" });
        await Assert.That(doc["objective"]).IsEqualTo("Destroy the enemy's monument!");
        await Assert.That((string)doc["objective"]!).DoesNotContain("wool");
    }

    [Test]
    public async Task Several_destroyables_declare_the_plural_destroy_line()
    {
        var doc = new Dict();
        MetaGenerator.Apply(doc, new MapIntent
        {
            Meta = new MetaIntent { Name = "M" },
            Destroyables = [Destroyable(name: "one"), Destroyable(name: "two")],
        });
        await Assert.That(doc["objective"]).IsEqualTo("Destroy the enemy's monuments!");
    }

    [Test]
    public async Task A_core_board_declares_dtc_and_the_leak_line()
    {
        var doc = new Dict();
        MetaGenerator.Apply(doc, new MapIntent
        {
            Meta = new MetaIntent { Name = "M" },
            Cores = [Core(name: "Red Core")],
        });
        await Assert.That(Gamemode(doc)).IsEquivalentTo(new[] { "dtc" });
        await Assert.That(doc["objective"]).IsEqualTo("Leak the enemy's core!");
    }

    [Test]
    public async Task Several_cores_declare_the_plural_leak_line()
    {
        var doc = new Dict();
        MetaGenerator.Apply(doc, new MapIntent
        {
            Meta = new MetaIntent { Name = "M" },
            Cores = [Core(name: "one"), Core(name: "two")],
        });
        await Assert.That(doc["objective"]).IsEqualTo("Leak the enemy's cores!");
    }

    // B155 — the fault itself: this used to assert doc["gamemode"] equalled the single joined string
    // "ctw dtm", which XmlWriter then wrote as one <gamemode>ctw dtm</gamemode> element — a value PGM's
    // closed enum cannot resolve (Gamemode.byId matches one id verbatim, never a space-separated pair), so
    // MapInfoImpl.parseGamemodes threw and the map failed to load. This is the case that fails on the old
    // behaviour: one element per mode, never several ids joined into one.
    [Test]
    public async Task A_mixed_wool_and_destroy_board_declares_both_gamemodes_as_separate_entries()
    {
        var doc = new Dict();
        MetaGenerator.Apply(doc, new MapIntent
        {
            Meta = new MetaIntent { Name = "M" },
            Wools = [new WoolIntent { Owner = "red-team" }],
            Destroyables = [Destroyable(name: "Red Monument")],
        });
        await Assert.That(Gamemode(doc)).IsEquivalentTo(new[] { "ctw", "dtm" });
        await Assert.That(doc["objective"]).IsEqualTo("Capture the wool and destroy the enemy's monument!");
    }

    [Test]
    public async Task A_board_with_no_objective_module_declares_neither_gamemode_nor_objective()
    {
        var doc = new Dict();
        MetaGenerator.Apply(doc, new MapIntent { Meta = new MetaIntent { Name = "M" } });
        await Assert.That(Gamemode(doc)).IsEmpty();
        await Assert.That(doc["objective"]).IsEqualTo("");
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
