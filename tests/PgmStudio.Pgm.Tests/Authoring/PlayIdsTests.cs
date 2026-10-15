using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>Every play piece leaves the store with an id: a stated one kept, the rest minted from what names
/// the piece, and two pieces never given the same one.</summary>
public sealed class PlayIdsTests
{
    [Test]
    public async Task A_piece_without_an_id_is_given_one_from_what_names_it()
    {
        var assigned = PlayIds.Assign(new MapIntent
        {
            Spawns = [new SpawnIntent { Team = "red-team" }, new SpawnIntent { Team = "blue-team", Id = "blue-home" }],
            Wools = [new WoolIntent { Owner = "red-team", Color = "lime" }, new WoolIntent { Owner = "blue-team" }],
            Destroyables = [new DestroyableIntent { Owner = "red-team" }, new DestroyableIntent { Owner = "red-team" }],
            Cores = [new CoreIntent { Owner = "blue-team" }],
        });

        await Assert.That(assigned.Spawns.Select(spawn => spawn.Id)).IsEquivalentTo(new[] { "spawn-red-team", "blue-home" });
        await Assert.That(assigned.Wools!.Select(wool => wool.Id))
            .IsEquivalentTo(new[] { "wool-red-team-lime", "wool-blue-team" });
        await Assert.That(assigned.Destroyables!.Select(goal => goal.Id))
            .IsEquivalentTo(new[] { "destroyable-red-team", "destroyable-red-team-2" });
        await Assert.That(assigned.Cores!.Single().Id).IsEqualTo("core-blue-team");
    }

    /// <summary>A minted id never takes one an author stated, wherever in the intent the stated one is.</summary>
    [Test]
    public async Task A_minted_id_steps_round_a_stated_one()
    {
        var assigned = PlayIds.Assign(new MapIntent
        {
            Spawns = [new SpawnIntent { Team = "red-team" }],
            Cores = [new CoreIntent { Owner = "blue-team", Id = "spawn-red-team" }],
        });

        await Assert.That(assigned.Spawns.Single().Id).IsEqualTo("spawn-red-team-2");
    }

    [Test]
    public async Task Two_pieces_stating_one_id_are_refused_under_RQ5()
    {
        var findings = PlayIds.Duplicates(new MapIntent
        {
            Spawns = [new SpawnIntent { Team = "red-team", Id = "home" }],
            Wools = [new WoolIntent { Owner = "red-team", Id = "home" }],
        }).ToList();

        await Assert.That(findings.Single().Rule).IsEqualTo(RequestRules.Taken);
        await Assert.That(findings.Single().Field).IsEqualTo("wools[0].id");
    }
}
