using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The match rules an intent states reach the map: the time limit and what decides it, the map's own respawn, a
/// blitz's lives, rage, and the kill height as a portal dropping whoever falls below it into the void. A rule the
/// intent leaves unstated is left as the map has it.
/// </summary>
public sealed class MatchGeneratorTests
{
    private static readonly TeamDef[] Teams =
    [
        new() { Id = "red-team", Name = "Red", Color = "red" },
        new() { Id = "blue-team", Name = "Blue", Color = "blue" },
    ];

    private static MapXml Written(MapIntent intent, Dict? doc = null)
    {
        doc ??= new Dict();
        IntentGenerator.Apply(doc, intent with { Teams = [.. Teams] });
        return MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));
    }

    [Test]
    public async Task A_time_limit_is_written_with_what_decides_it()
    {
        var map = Written(new MapIntent { TimeLimit = new TimeLimitIntent { Duration = "10m", Result = TimeResults.Score } });

        await Assert.That((map.TimeLimit!.Duration, map.TimeLimit.Result)).IsEqualTo(("10m", "score"));
    }

    [Test]
    public async Task The_default_result_writes_no_result()
    {
        var map = Written(new MapIntent { TimeLimit = new TimeLimitIntent { Duration = "8m" } });

        await Assert.That(map.TimeLimit!.Result).IsEmpty();
    }

    [Test]
    public async Task Respawn_lives_and_rage_are_written()
    {
        var map = Written(new MapIntent
        {
            Respawn = new RespawnIntent { Delay = "3s", Auto = true },
            Lives = 2,
            Rage = true,
        });

        await Assert.That((map.Respawn!.Delay, map.Respawn.Auto, map.Respawn.Spectate)).IsEqualTo(("3s", (bool?)true, (bool?)null));
        await Assert.That(map.Blitz!.Lives).IsEqualTo(2);
        await Assert.That(map.Rage).IsTrue();
        await Assert.That(map.Gamemodes).IsEquivalentTo([Gamemodes.Blitz, Gamemodes.Rage]);
    }

    [Test]
    public async Task An_unstated_rule_is_left_as_the_map_has_it_and_an_emptied_one_is_removed()
    {
        var doc = new Dict();
        IntentGenerator.Apply(doc, new MapIntent { Teams = [.. Teams], TimeLimit = new TimeLimitIntent { Duration = "10m" }, Lives = 3 });

        var kept = Written(new MapIntent(), doc);
        await Assert.That(kept.TimeLimit!.Duration).IsEqualTo("10m");
        await Assert.That(kept.Blitz!.Lives).IsEqualTo(3);

        var emptied = Written(new MapIntent { TimeLimit = new TimeLimitIntent(), Lives = 0 }, doc);
        await Assert.That(emptied.TimeLimit).IsNull();
        await Assert.That(emptied.Blitz).IsNull();
    }

    [Test]
    public async Task A_kill_height_drops_a_player_below_it_into_the_void()
    {
        var map = Written(new MapIntent { KillHeight = 40 });

        var portal = map.Portals.Single();
        await Assert.That(map.Regions[portal.RegionId].Type).IsEqualTo("below");
        await Assert.That(map.Regions[portal.RegionId].HalfY).IsEqualTo(40);
        await Assert.That(portal.Y).IsEqualTo("@-64");
        await Assert.That(portal.ObserversFilterId).IsEqualTo("never");
    }

    [Test]
    public async Task A_rebuild_replaces_the_kill_height_and_an_unstated_one_removes_it()
    {
        var doc = new Dict();
        Written(new MapIntent { KillHeight = 40 }, doc);
        var moved = Written(new MapIntent { KillHeight = 30 }, doc);
        await Assert.That(moved.Portals.Count).IsEqualTo(1);
        await Assert.That(moved.Regions[MatchGenerator.KillHeightRegion].HalfY).IsEqualTo(30);

        var gone = Written(new MapIntent(), doc);
        await Assert.That(gone.Portals).IsEmpty();
    }

    [Test]
    public async Task A_deathmatch_stating_no_limit_ends_on_the_corpus_clock_and_reads_as_one()
    {
        var map = Written(new MapIntent { Meta = new MetaIntent { Name = "Pit" }, KillPoints = 1 });

        await Assert.That(map.TimeLimit!.Duration).IsEqualTo(ObjectiveDefaults.DeathmatchTimeLimit);
        await Assert.That(map.Score!.Kills).IsEqualTo(1);
        await Assert.That(map.DeclaredGamemode).IsEquivalentTo([Gamemodes.Tdm]);
        await Assert.That(map.Objective).IsEqualTo("Get the most kills!");
    }

    [Test]
    public async Task A_deathmatch_with_a_score_limit_or_a_stated_clock_takes_no_default_clock()
    {
        await Assert.That(Written(new MapIntent { KillPoints = 1, ScoreLimit = 50 }).TimeLimit).IsNull();
        await Assert.That(Written(new MapIntent { KillPoints = 1, TimeLimit = new TimeLimitIntent { Duration = "5m" } }).TimeLimit!.Duration)
            .IsEqualTo("5m");
    }
}
