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
}
