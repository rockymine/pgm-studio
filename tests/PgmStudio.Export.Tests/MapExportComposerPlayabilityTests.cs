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

    // ── OB27/OB28: the two ways a capture board does not play as written ───────────────────────────────

    /// <summary>A board of shown capture points, each carrying what the test names.</summary>
    private static Dict Hills(params (string Key, object? Value)[] stated)
    {
        var doc = Doc(("spawns", 2), ("teams", 2));
        var point = new Dict { ["id"] = "hill", ["capture_region"] = "hill-capture" };
        foreach (var (key, value) in stated) point[key] = value;
        doc["control_points"] = new List<object?> { point };
        return doc;
    }

    /// <summary><b>A point that leaves <c>required</c> off ends the match on the first capture.</b> PGM reads
    /// the attribute as true at proto 1.4.0 and above, which is every map the studio supports, and
    /// <c>GoalsVictoryCondition</c> finishes the match the instant a competitor holds all of its required
    /// goals. 66 points across 20 corpus maps leave it off.</summary>
    [Test]
    public async Task A_point_with_no_required_is_OB27()
    {
        var findings = MapExportComposer.Playable(Intent(spawns: 2), Hills(("points", 1d)));

        var ends = findings.Single(finding => finding.Rule == ObjectiveRules.PointEndsTheMatch);
        await Assert.That(ends.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(ends.Message).Contains("1 of the map's 1 capture point(s) state no `required`");
    }

    /// <summary>And the convention silences it — which is what every point the studio authors writes.</summary>
    [Test]
    public async Task A_point_stating_required_false_is_not_OB27()
    {
        var doc = Hills(("required", false), ("points", 1d));
        doc["score"] = new Dict { ["limit"] = 750L };

        await Assert.That(MapExportComposer.Playable(Intent(spawns: 2), doc)
            .Any(finding => finding.Rule == ObjectiveRules.PointEndsTheMatch)).IsFalse();
    }

    /// <summary>A point PGM never registers as a goal cannot end anything: <c>show="false"</c> clears every
    /// show option including <c>stats</c>, and <c>GoalMatchModule.addGoal</c> returns early without it. That
    /// is why an arcade grid of hidden cubes does not win on first touch.</summary>
    [Test]
    public async Task A_hidden_point_is_asked_neither_question()
    {
        var doc = Hills(("show", false), ("points", 1d));

        await Assert.That(MapExportComposer.Playable(Intent(spawns: 2), doc)
            .Any(finding => finding.Rule == ObjectiveRules.PointEndsTheMatch
                         || finding.Rule == ObjectiveRules.PointScoresIntoNothing)).IsFalse();
    }

    /// <summary><b>A point that pays into no score module pays nothing.</b> <c>ControlPoint.tickScore</c>
    /// looks up <c>ScoreMatchModule</c> every tick and PGM builds one only for a document carrying a
    /// <c>&lt;score&gt;</c> element, so the whole match scores zero with no error anywhere. 20 points across
    /// 11 corpus maps do it.</summary>
    [Test]
    public async Task A_paying_point_with_no_score_element_is_OB28()
    {
        var findings = MapExportComposer.Playable(Intent(spawns: 2), Hills(("required", false), ("points", 1d)));

        var paid = findings.Single(finding => finding.Rule == ObjectiveRules.PointScoresIntoNothing);
        await Assert.That(paid.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(paid.Field).IsEqualTo("score");
    }

    /// <summary>The element is what matters rather than anything in it — which is why corpus authors carry an
    /// empty one as a placeholder.</summary>
    [Test]
    public async Task An_empty_score_element_is_enough()
    {
        var doc = Hills(("required", false), ("points", 1d));
        doc["score"] = new Dict { ["kills"] = 0L, ["deaths"] = 0L };

        await Assert.That(MapExportComposer.Playable(Intent(spawns: 2), doc)
            .Any(finding => finding.Rule == ObjectiveRules.PointScoresIntoNothing)).IsFalse();
    }

    /// <summary>A point that pays nothing is not asked: <c>points="0"</c> is a payload's shape, and scoring is
    /// not what it is for.</summary>
    [Test]
    public async Task A_point_that_pays_nothing_needs_no_score()
    {
        var doc = Hills(("required", false), ("points", 0d));

        await Assert.That(MapExportComposer.Playable(Intent(spawns: 2), doc)
            .Any(finding => finding.Rule == ObjectiveRules.PointScoresIntoNothing)).IsFalse();
    }

    // ── SH1 — a shop reference the document does not define ─────────────────────────────────────────────
    // A board that sells things: one menu, one tab, one icon, and one keeper that opens it. Every reference
    // resolves, which is what a studio-authored board always produces.
    private static Dict Selling(Dict? icon = null, Dict? keeper = null)
    {
        var doc = Doc(("spawns", 2), ("teams", 2));
        doc["shops"] = new List<object?>
        {
            new Dict
            {
                ["id"] = "item-shop",
                ["categories"] = new List<object?>
                {
                    new Dict
                    {
                        ["id"] = "blocks",
                        ["icon"] = new Dict { ["material"] = "hard clay" },
                        ["icons"] = new List<object?> { icon ?? new Dict { ["item"] = new Dict { ["material"] = "wood" } } },
                    },
                },
            },
        };
        doc["shopkeepers"] = new List<object?> { keeper ?? new Dict { ["shop"] = "item-shop" } };
        return doc;
    }

    /// <summary>The board the studio writes says nothing: every id it names it also defines.</summary>
    [Test]
    public async Task A_board_whose_shop_references_all_resolve_is_silent()
    {
        await Assert.That(MapExportComposer.Playable(Intent(spawns: 2), Selling())
            .Any(finding => finding.Rule == ShopRules.ReferenceNotDefined)).IsFalse();
    }

    /// <summary><b>A keeper naming a menu nothing defines is a map PGM will not load</b> —
    /// <c>ShopModule.parse</c> throws <i>"No shop with id '…' could be found"</i> — so it is refused rather
    /// than complained about, beside the other questions of whether this is a map at all.</summary>
    [Test]
    public async Task A_keeper_naming_no_menu_refuses_the_export()
    {
        var findings = MapExportComposer.Playable(
            Intent(spawns: 2), Selling(keeper: new Dict { ["shop"] = "upgrade-shop" }));

        var dangling = findings.Single(finding => finding.Rule == ShopRules.ReferenceNotDefined);
        await Assert.That(dangling.Refuses).IsTrue();
        await Assert.That(dangling.SubjectIds).IsEquivalentTo(new[] { "upgrade-shop" });
    }

    /// <summary>And the place it stands in is the same kind of reference: PGM resolves it against the map's
    /// own regions and refuses when nothing answers.</summary>
    [Test]
    public async Task A_keeper_naming_no_region_refuses_the_export()
    {
        var doc = Selling(keeper: new Dict { ["shop"] = "item-shop", ["region"] = "market-floor" });

        var dangling = MapExportComposer.Playable(Intent(spawns: 2), doc)
            .Single(finding => finding.Rule == ShopRules.ReferenceNotDefined);
        await Assert.That(dangling.SubjectIds).IsEquivalentTo(new[] { "market-floor" });
    }

    /// <summary>A region the map holds answers it, so a keeper standing in one says nothing.</summary>
    [Test]
    public async Task A_keeper_standing_in_a_region_the_map_holds_is_silent()
    {
        var doc = Selling(keeper: new Dict { ["shop"] = "item-shop", ["region"] = "market-floor" });
        doc["regions"] = new Dict { ["market-floor"] = new Dict { ["id"] = "market-floor", ["type"] = "everywhere" } };

        await Assert.That(MapExportComposer.Playable(Intent(spawns: 2), doc)
            .Any(finding => finding.Rule == ShopRules.ReferenceNotDefined)).IsFalse();
    }

    /// <summary>An icon's action is a feature reference, and the studio authors no <c>&lt;actions&gt;</c>
    /// block — so any id there names nothing on a studio-built board and the map would not load.</summary>
    [Test]
    public async Task An_icon_naming_an_action_nothing_defines_refuses_the_export()
    {
        var upgrade = new Dict
        {
            ["item"] = new Dict { ["material"] = "anvil" },
            ["action"] = "add-protection",
        };

        var dangling = MapExportComposer.Playable(Intent(spawns: 2), Selling(icon: upgrade))
            .Single(finding => finding.Rule == ShopRules.ReferenceNotDefined);
        await Assert.That(dangling.Refuses).IsTrue();
        await Assert.That(dangling.SubjectIds).IsEquivalentTo(new[] { "add-protection" });
    }

    /// <summary>A board with no shop at all is not asked, so the walk costs nothing on every other map.</summary>
    [Test]
    public async Task A_board_that_sells_nothing_is_not_asked()
    {
        await Assert.That(MapExportComposer.Playable(Intent(spawns: 2), Doc(("spawns", 2), ("teams", 2)))
            .Any(finding => finding.Rule == ShopRules.ReferenceNotDefined)).IsFalse();
    }
}
