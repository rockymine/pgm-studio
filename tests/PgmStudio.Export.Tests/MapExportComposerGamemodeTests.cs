using PgmStudio.Domain;
namespace PgmStudio.Export.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// OB20 (B155) — the export composer refuses a declared <c>&lt;gamemode&gt;</c> id PGM's own closed enum
/// would reject, the same shape as OB17/OB19 (<c>docs/tools/configure.md</c>). Unlike those two it needs no
/// world and no resolved intent — it reads only the document's own <c>"gamemode"</c> list — so it is checked
/// first, against every map regardless of origin.
/// </summary>
public sealed class MapExportComposerGamemodeTests
{
    private static Dict Doc(params string[] gamemodes) => new()
    {
        ["name"] = "m", ["version"] = "1.0.0", ["objective"] = "o",
        ["gamemode"] = gamemodes.Cast<object?>().ToList(),
    };

    [Test]
    public async Task An_unrecognized_gamemode_id_refuses_OB20()
    {
        var result = MapExportComposer.Compose(Doc("not-a-real-mode"), null, isIntent: false, null, null, null, []);

        await Assert.That(result.Refusal).IsNotNull();
        await Assert.That(result.Refusal!.Status).IsEqualTo(409);
        await Assert.That(result.Refusal!.Error).IsEqualTo("unknown gamemode");

        // Every gate answers in the one finding shape: the rule, the sentence, and what it indicts.
        var finding = result.Refusal!.Findings.Single();
        await Assert.That(finding.Rule).IsEqualTo(ObjectiveRules.UnknownGamemode);
        await Assert.That(finding.Refuses).IsTrue();
        await Assert.That(finding.SubjectIds).IsEquivalentTo(new[] { "not-a-real-mode" });
    }

    // The exact B155 regression: two real modes must round-trip as two separate <gamemode> elements, neither
    // of which is "dtm dtc" — the joined value the old writer produced and PGM's enum could never resolve.
    [Test]
    public async Task Every_id_PGM_recognizes_exports_clean_even_mixed()
    {
        var result = MapExportComposer.Compose(Doc("ctw", "dtm"), null, isIntent: false, null, null, null, []);

        await Assert.That(result.Refusal).IsNull();
        await Assert.That(result.Xml).Contains("<gamemode>ctw</gamemode>");
        await Assert.That(result.Xml).Contains("<gamemode>dtm</gamemode>");
        await Assert.That(result.Xml).DoesNotContain("ctw dtm");
    }

    [Test]
    public async Task No_declared_gamemode_is_not_a_refusal()
    {
        var result = MapExportComposer.Compose(Doc(), null, isIntent: false, null, null, null, []);

        await Assert.That(result.Refusal).IsNull();
        await Assert.That(result.Xml).DoesNotContain("<gamemode>");
    }
}
