using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The derivation behind a destroy map's kit pickaxe, and the guard that checks it held: what pickaxe a
/// map needs (<see cref="DestroyKitPairing.RequiredPickaxe"/>) and which of its goals nothing in a given
/// kit can break (<see cref="DestroyKitPairing.Unwinnable"/>).
/// </summary>
public sealed class DestroyKitPairingTests
{
    private static DestroyableIntent Destroyable(string materials, string owner = "red", string name = "") =>
        new() { Owner = owner, Name = name, Materials = materials, Anchor = new Pt(0, 8, 0), Float = 4 };

    private static CoreIntent Core(string owner = "red", string name = "") =>
        new() { Owner = owner, Name = name, Anchor = new Pt(0, 8, 0), Size = 5, Height = 5, Shell = 1, Float = 6, Leak = 5 };

    [Test]
    public async Task A_map_with_no_destroy_goal_takes_the_corpus_default_iron_pickaxe()
    {
        await Assert.That(DestroyKitPairing.RequiredPickaxe(new MapIntent())).IsEqualTo("iron pickaxe");
    }

    [Test]
    public async Task An_obsidian_destroyable_upgrades_the_pickaxe_to_diamond()
    {
        var intent = new MapIntent { Destroyables = [Destroyable("obsidian")] };
        await Assert.That(DestroyKitPairing.RequiredPickaxe(intent)).IsEqualTo("diamond pickaxe");
    }

    [Test]
    public async Task A_gold_or_emerald_destroyable_needs_only_iron()
    {
        var intent = new MapIntent { Destroyables = [Destroyable("gold block")] };
        await Assert.That(DestroyKitPairing.RequiredPickaxe(intent)).IsEqualTo("iron pickaxe");
    }

    [Test]
    public async Task A_core_always_upgrades_to_diamond()
    {
        // A core's casing is always obsidian — it is not a knob (docs/pgm/destroyables-and-cores.md
        // DC1) — so a map with one always needs a diamond pickaxe outright.
        var intent = new MapIntent { Cores = [Core()] };
        await Assert.That(DestroyKitPairing.RequiredPickaxe(intent)).IsEqualTo("diamond pickaxe");
    }

    [Test]
    public async Task The_hardest_of_several_goals_wins()
    {
        var intent = new MapIntent { Destroyables = [Destroyable("gold block"), Destroyable("obsidian", name: "second")] };
        await Assert.That(DestroyKitPairing.RequiredPickaxe(intent)).IsEqualTo("diamond pickaxe");
    }

    [Test]
    public async Task Unwinnable_is_empty_when_the_kit_pickaxe_meets_every_goal()
    {
        var intent = new MapIntent { Destroyables = [Destroyable("obsidian")] };
        var findings = DestroyKitPairing.Unwinnable(intent, ["diamond pickaxe"]);
        await Assert.That(findings).IsEmpty();
    }

    [Test]
    public async Task Unwinnable_names_a_destroyable_an_iron_pickaxe_cannot_break()
    {
        var intent = new MapIntent { Destroyables = [Destroyable("obsidian", owner: "red", name: "Red Monument")] };
        var findings = DestroyKitPairing.Unwinnable(intent, ["iron pickaxe"]);
        await Assert.That(findings).IsEquivalentTo(["Red Monument"]);
    }

    [Test]
    public async Task Unwinnable_falls_back_to_the_owner_when_the_destroyable_has_no_name()
    {
        var intent = new MapIntent { Destroyables = [Destroyable("obsidian", owner: "red", name: "")] };
        var findings = DestroyKitPairing.Unwinnable(intent, ["iron pickaxe"]);
        await Assert.That(findings).IsEquivalentTo(["red"]);
    }

    [Test]
    public async Task Unwinnable_flags_a_core_with_no_diamond_pickaxe_in_the_kit()
    {
        var intent = new MapIntent { Cores = [Core(owner: "blue", name: "")] };
        var findings = DestroyKitPairing.Unwinnable(intent, ["iron pickaxe"]);
        await Assert.That(findings).IsEquivalentTo(["blue"]);
    }

    [Test]
    public async Task Unwinnable_is_empty_for_a_kitless_ctw_map_with_no_destroy_goal()
    {
        var findings = DestroyKitPairing.Unwinnable(new MapIntent(), []);
        await Assert.That(findings).IsEmpty();
    }

    [Test]
    public async Task A_kitless_destroy_map_is_flagged_unwinnable()
    {
        // No pickaxe material at all reaches Unwinnable the same way an insufficient one does.
        var intent = new MapIntent { Destroyables = [Destroyable("obsidian", name: "Red Monument")] };
        var findings = DestroyKitPairing.Unwinnable(intent, []);
        await Assert.That(findings).IsEquivalentTo(["Red Monument"]);
    }
}
