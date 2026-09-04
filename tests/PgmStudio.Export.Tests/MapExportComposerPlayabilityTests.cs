using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// EX2/EX3/EX4 — the export gate's questions about whether the document is a map: can anyone enter it, is it
/// the map the intent stated, and is there anyone to contest what it puts up for winning.
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
    /// <summary>A document with the lists named, each entry an id and nothing else — plus, where it carries a
    /// destroy objective, the mode ladder and the opt-in a generated map always writes, so these tests read
    /// what the generator makes rather than a shape it never emits. <c>OB26</c>'s own tests build the
    /// ladderless case explicitly.</summary>
    private static Dict Doc(params (string Key, int Count)[] lists)
    {
        var doc = new Dict { ["name"] = "m", ["version"] = "1.0.0", ["gamemode"] = new List<object?>() };
        foreach (var (key, count) in lists)
            doc[key] = Enumerable.Range(0, count).Select(i => (object?)new Dict
            {
                ["id"] = $"{key}-{i}",
                ["mode_changes"] = key is "destroyables" or "cores" ? true : null,
            }).ToList();
        if (doc.ContainsKey("destroyables") || doc.ContainsKey("cores")) doc["modes"] = Ladder();
        return doc;
    }

    private static List<object?> Ladder() =>
        [new Dict { ["id"] = "mode-gold-block", ["after"] = "15m", ["material"] = "gold block" }];

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
        var doc = Doc(("spawns", 2), ("wools", 2), ("teams", 2));

        await Assert.That(MapExportComposer.Playable(Intent(spawns: 2), doc)).IsEmpty();
    }

    /// <summary>The author's ruling, and the only one in this gate: the three gamemodes the studio authors —
    /// CTW, DTM, DTC — are played by teams, so a map that states something to win must state who contests it.
    /// It is asked of the objectives rather than of the <c>&lt;gamemode&gt;</c> element, which is derived from
    /// exactly those three lists and which PGM does not read to decide what runs.</summary>
    [Test]
    public async Task An_objective_with_no_team_has_nobody_to_contest_it()
    {
        var findings = MapExportComposer.Playable(null, Doc(("spawns", 2), ("wools", 2)));

        var orphaned = findings.Single(finding => finding.Rule == "EX4");
        await Assert.That(orphaned.Field).IsEqualTo("teams");
        await Assert.That(orphaned.Message).Contains("2 objective");
    }

    /// <summary>A board with no objective at all is not asked. It is unfinished rather than wrong — which is
    /// <c>PL3</c>'s to say, as a complaint — and refusing it here would refuse every map mid-authoring.
    /// Asserted because the obvious way to write the rule above ("a map needs teams") would.</summary>
    [Test]
    public async Task A_board_with_nothing_to_win_is_not_asked_who_would_win_it()
    {
        await Assert.That(MapExportComposer.Playable(null, Doc(("spawns", 2)))).IsEmpty();
    }

    /// <summary>A corpus map is exempt, for the reason the traversability gate exempts one: <b>281 of the
    /// 1,616 maps</b> in the two corpora declare no team, and three declare no spawn in their own file because
    /// an <c>&lt;include&gt;</c> carries it. The gate is about what the studio writes, and refusing a map the
    /// studio only read would be making the format fit.</summary>
    [Test]
    public async Task A_map_the_studio_did_not_author_exports_unchecked()
    {
        var result = MapExportComposer.Compose(Doc(), null, isIntent: false, null, null, null, []);

        await Assert.That(result.Refusal).IsNull();
    }

    /// <summary>And the same document does not, once the studio is the one that wrote it.</summary>
    [Test]
    public async Task An_intent_authored_map_with_no_spawn_is_refused_at_the_composer()
    {
        var result = MapExportComposer.Compose(Doc(), null, isIntent: true, null, null, null, []);

        await Assert.That(result.Refusal).IsNotNull();
        await Assert.That(result.Refusal!.Status).IsEqualTo(409);
        await Assert.That(result.Refusal!.Error).IsEqualTo("not a playable map");
        await Assert.That(result.Refusal!.Findings.Single().Rule).IsEqualTo("EX2");
    }

    // ── OB26: a destroy map with no way to end ────────────────────────────────────────────────────────

    /// <summary><b>A monument that stays obsidian is a monument the defending team can hold.</b> PGM lets the
    /// owner repair unless the map says otherwise, and the obsidian an attacker's pick drops is what they
    /// repair with; a core cannot even say otherwise, having no <c>repairable</c> at all. The map's answer is
    /// the mode ladder, and a destroy map with none does not end.</summary>
    [Test]
    public async Task A_destroy_map_with_no_mode_ladder_is_OB26()
    {
        var doc = Doc(("spawns", 2), ("destroyables", 2), ("teams", 2));
        doc.Remove("modes");

        var findings = MapExportComposer.Playable(Intent(spawns: 2, destroyables: 2), doc);
        var ladder = findings.Single(finding => finding.Rule == ObjectiveRules.NoModeLadder);
        await Assert.That(ladder.Message).Contains("no mode ladder");
    }

    /// <summary>The other half, and the one a document can get wrong while looking right: PGM affects an
    /// objective by <em>no</em> mode unless the objective says so, so a ladder nothing opts into is the same
    /// map as no ladder. 171 of the 173 mode-carrying corpus maps opt in; the two that do not are the
    /// case.</summary>
    [Test]
    public async Task A_ladder_no_objective_takes_is_OB26()
    {
        var doc = Doc(("spawns", 2), ("cores", 2), ("teams", 2));
        foreach (var core in doc["cores"] as List<object?> ?? []) ((Dict)core!).Remove("mode_changes");

        var findings = MapExportComposer.Playable(Intent(spawns: 2), doc);
        var ladder = findings.Single(finding => finding.Rule == ObjectiveRules.NoModeLadder);
        await Assert.That(ladder.Message).Contains("2 of its 2");
    }

    /// <summary>And a CTW map is never asked. A wool is carried rather than broken, so a mode has nothing to
    /// do to it however long the match runs.</summary>
    [Test]
    public async Task A_wool_map_needs_no_ladder()
    {
        var doc = Doc(("spawns", 2), ("wools", 2), ("teams", 2));

        await Assert.That(MapExportComposer.Playable(Intent(spawns: 2), doc)
                                           .Any(f => f.Rule == ObjectiveRules.NoModeLadder)).IsFalse();
    }
}
