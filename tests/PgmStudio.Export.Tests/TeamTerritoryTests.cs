using PgmStudio.Export;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The shared terrain-ownership decomposition (docs/world-export/terrain-painting.md §3): canonical
/// IslandDetector islands, owned by a spawn's team (else a wool's owner, else neutral), with a stored
/// IslandTeams value winning. Ids are the 1-based islands_json ids (largest island first).
/// </summary>
public sealed class TeamTerritoryTests
{
    // Big island A (100 cells, id 1) at the origin; small island B (25 cells, id 2) far away.
    private static List<(int X, int Z)> TwoIslands()
    {
        var f = new List<(int, int)>();
        for (var x = 0; x < 10; x++) for (var z = 0; z < 10; z++) f.Add((x, z));
        for (var x = 50; x < 55; x++) for (var z = 0; z < 5; z++) f.Add((x, z));
        return f;
    }

    private static MapIntent TwoTeams(SpawnIntent[] spawns, WoolIntent[]? wools = null) => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
        Spawns = [.. spawns],
        Wools = wools is null ? null : [.. wools],
    };

    [Test]
    public async Task A_spawn_owns_its_island_and_anchorless_land_is_neutral()
    {
        var intent = TwoTeams([new SpawnIntent { Team = "red", Point = new Pt(5, 1, 5) }]);   // in island 1
        var owners = TeamTerritory.Assign(TwoIslands(), intent);
        await Assert.That(owners["1"]).IsEqualTo("red");
        await Assert.That(owners["2"]).IsEqualTo(TeamTerritory.Neutral);
    }

    [Test]
    public async Task A_wool_owner_takes_an_island_with_no_spawn()
    {
        var intent = TwoTeams(
            [new SpawnIntent { Team = "red", Point = new Pt(5, 1, 5) }],
            [new WoolIntent { Owner = "blue", Spawn = new Pt(52, 1, 2) }]);   // in island 2
        var owners = TeamTerritory.Assign(TwoIslands(), intent);
        await Assert.That(owners["1"]).IsEqualTo("red");
        await Assert.That(owners["2"]).IsEqualTo("blue");
    }

    [Test]
    public async Task A_stored_island_team_wins_over_the_anchor()
    {
        var intent = TwoTeams([new SpawnIntent { Team = "red", Point = new Pt(5, 1, 5) }]);
        intent.IslandTeams["1"] = "blue";       // manual override (configure)
        intent.IslandTeams["2"] = "red";
        var owners = TeamTerritory.Assign(TwoIslands(), intent);
        await Assert.That(owners["1"]).IsEqualTo("blue");
        await Assert.That(owners["2"]).IsEqualTo("red");
    }

    [Test]
    public async Task DamageAt_gives_the_team_nibble_and_neutral_off_team()
    {
        var intent = TwoTeams([new SpawnIntent { Team = "red", Point = new Pt(5, 1, 5) }]);
        var dmg = TeamTerritory.DamageAt(TwoIslands(), intent);
        await Assert.That(dmg(5, 5)).IsEqualTo(14);    // red wool/clay damage, island 1
        await Assert.That(dmg(52, 2)).IsEqualTo(-1);   // neutral island 2
        await Assert.That(dmg(999, 999)).IsEqualTo(-1); // off the footprint
    }

    // ── land more than one team enters (PT5) ─────────────────────────────────────────────────────────
    /// <summary>A team per island is the arrangement the tint can state, so nothing is shared.</summary>
    [Test]
    public async Task A_board_with_a_team_on_each_island_shares_none_of_its_land()
    {
        var intent = TwoTeams(
        [
            new SpawnIntent { Team = "red", Point = new Pt(5, 1, 5) },
            new SpawnIntent { Team = "blue", Point = new Pt(52, 1, 2) },
        ]);

        await Assert.That(TeamTerritory.Shared(TwoIslands(), intent)).IsEmpty();
    }

    /// <summary>Two spawns of one team on one island is still one team's land — the question is how many
    /// teams enter it, not how many doors they come through.</summary>
    [Test]
    public async Task Two_spawns_of_one_team_do_not_share_an_island()
    {
        var intent = TwoTeams(
        [
            new SpawnIntent { Team = "red", Point = new Pt(1, 1, 1) },
            new SpawnIntent { Team = "red", Point = new Pt(8, 1, 8) },
        ]);

        await Assert.That(TeamTerritory.Shared(TwoIslands(), intent)).IsEmpty();
    }

    /// <summary>And both teams entering one landmass is the case a per-island colour cannot answer: the
    /// island comes back naming both teams, the one owner it resolved, and every cell wearing it.</summary>
    [Test]
    public async Task One_island_both_teams_enter_is_shared_and_names_them_with_its_ground()
    {
        var intent = TwoTeams(
        [
            new SpawnIntent { Team = "red", Point = new Pt(1, 1, 1) },
            new SpawnIntent { Team = "blue", Point = new Pt(8, 1, 8) },
        ]);

        var shared = TeamTerritory.Shared(TwoIslands(), intent);

        await Assert.That(shared.Count).IsEqualTo(1);
        await Assert.That(shared[0].Island).IsEqualTo("1");
        await Assert.That(shared[0].Owner).IsEqualTo("red");        // the first spawn read, which is the fault
        await Assert.That(shared[0].Teams).IsEquivalentTo(new[] { "red", "blue" });
        await Assert.That(shared[0].Cells.Count).IsEqualTo(100);
    }

    /// <summary>A stored assignment does not unshare the ground. It decides which colour the island wears,
    /// and the island is still one colour for two teams — which is the whole of what the finding says.</summary>
    [Test]
    public async Task Assigning_the_island_by_hand_still_leaves_it_shared()
    {
        var intent = TwoTeams(
        [
            new SpawnIntent { Team = "red", Point = new Pt(1, 1, 1) },
            new SpawnIntent { Team = "blue", Point = new Pt(8, 1, 8) },
        ]);
        intent.IslandTeams["1"] = "blue";

        var shared = TeamTerritory.Shared(TwoIslands(), intent);

        await Assert.That(shared.Count).IsEqualTo(1);
        await Assert.That(shared[0].Owner).IsEqualTo("blue");
    }

    /// <summary>A team the document never declared is not a team, so a spawn carrying one cannot make an
    /// island shared — the ownership itself resolves such a spawn to neutral.</summary>
    [Test]
    public async Task A_spawn_of_an_undeclared_team_does_not_share_an_island()
    {
        var intent = TwoTeams(
        [
            new SpawnIntent { Team = "red", Point = new Pt(1, 1, 1) },
            new SpawnIntent { Team = "green", Point = new Pt(8, 1, 8) },
        ]);

        await Assert.That(TeamTerritory.Shared(TwoIslands(), intent)).IsEmpty();
    }
}
