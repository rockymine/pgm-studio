using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// EX2/EX3 (B140) — the export gate's two arithmetic questions: is the document about to be written a map at
/// all, and is it the map the intent stated.
///
/// <para>The case that motivated them is a board with <b>nothing on it</b>. Every other gate here quantifies
/// over a collection — a goal in void, a prop in a clearance, spawn and wool points reaching each other — and
/// each is vacuously satisfied by an empty list, so two authoring-trial boards exported a ten-line
/// <c>map.xml</c> and every stage answered 200.</para>
///
/// <para>The other half is the harder one and is what most of this file is about: their plan carried two
/// spawns and two destroyables, so nothing was un-authored — it was lost between the plan and the export. That
/// is invisible to any gate that reads one side, which is why the comparison happens here and nowhere
/// earlier.</para>
/// </summary>
public sealed class MapExportComposerPlayabilityTests
{
    private static Dict Doc(params (string Key, int Count)[] lists)
    {
        var doc = new Dict { ["name"] = "m", ["version"] = "1.0.0", ["gamemode"] = new List<object?>() };
        foreach (var (key, count) in lists)
            doc[key] = Enumerable.Range(0, count).Select(i => (object?)new Dict { ["id"] = $"{key}-{i}" }).ToList();
        return doc;
    }

    private static MapIntent Intent(int spawns = 0, int destroyables = 0) => new()
    {
        Spawns = [.. Enumerable.Range(0, spawns).Select(i => new SpawnIntent { Team = $"t{i}" })],
        Destroyables = destroyables == 0
            ? null
            : [.. Enumerable.Range(0, destroyables).Select(i => new DestroyableIntent { Owner = $"t{i}" })],
    };

    /// <summary>The whole entry in one assertion: a document nobody can enter is refused, where before it was
    /// written out and called a map.</summary>
    [Test]
    public async Task A_document_with_no_spawn_cannot_be_entered()
    {
        var findings = MapExportComposer.Playable(null, Doc());

        await Assert.That(findings.Refuses).IsTrue();
        await Assert.That(findings.Single().Rule).IsEqualTo("EX2");
        await Assert.That(findings.Single().Field).IsEqualTo("spawns");
    }

    /// <summary>What the boards actually needed. The author stated two spawns and two destroyables; the
    /// document carries neither, and the numbers are in the sentence because "something is missing" does not
    /// tell anyone which stage to look at.</summary>
    [Test]
    public async Task What_the_intent_stated_and_the_document_lost_is_named_per_kind()
    {
        var findings = MapExportComposer.Playable(Intent(spawns: 2, destroyables: 2), Doc());

        var lost = findings.Where(finding => finding.Rule == "EX3").ToList();
        await Assert.That(lost.Select(finding => finding.Field)).IsEquivalentTo(new[] { "spawns", "destroyables" });
        await Assert.That(lost.First(finding => finding.Field == "spawns").Message).Contains("states 2 spawns");
    }

    /// <summary>A map that carries what its intent stated is silent. Asserted because a gate that fired on
    /// every export would pass the tests above and refuse every real map.</summary>
    [Test]
    public async Task A_map_carrying_what_it_stated_says_nothing()
    {
        var doc = Doc(("spawns", 2), ("destroyables", 2), ("teams", 2));

        await Assert.That(MapExportComposer.Playable(Intent(spawns: 2, destroyables: 2), doc)).IsEmpty();
    }

    /// <summary>Only the kinds the intent actually states are compared. A CTW map carries no destroyables and
    /// no cores, and reading a zero as a loss would refuse every map of one gamemode for lacking another's
    /// objective.</summary>
    [Test]
    public async Task A_kind_the_intent_never_stated_is_not_a_loss()
    {
        var doc = Doc(("spawns", 2), ("wools", 2));

        await Assert.That(MapExportComposer.Playable(Intent(spawns: 2), doc)).IsEmpty();
    }

    /// <summary>A corpus map is exempt, for the reason the traversability gate exempts one: <b>281 of the
    /// 1,616 maps</b> in the two corpora declare no team, and three declare no spawn in their own file because
    /// an <c>&lt;include&gt;</c> carries it. The gate is about what the studio writes, and refusing a map the
    /// studio only read would be making the format fit.</summary>
    [Test]
    public async Task A_map_the_studio_did_not_author_exports_unchecked()
    {
        var result = MapExportComposer.Compose(Doc(), null, isIntent: false, null, null, null, []);

        await Assert.That(result.IsError).IsFalse();
    }

    /// <summary>And the same document does not, once the studio is the one that wrote it.</summary>
    [Test]
    public async Task An_intent_authored_map_with_no_spawn_is_refused_at_the_composer()
    {
        var result = MapExportComposer.Compose(Doc(), null, isIntent: true, null, null, null, []);

        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.ErrorStatus).IsEqualTo(409);
        await Assert.That(result.ErrorBody!["error"]).IsEqualTo("not a playable map");
        var findings = (List<Dictionary<string, object?>>)result.ErrorBody!["findings"]!;
        await Assert.That(findings.Single()["rule"]).IsEqualTo("EX2");
    }
}
