using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The derivation behind a destroy map's kit pickaxe: what pickaxe a map needs
/// (<see cref="DestroyKitPairing.RequiredPickaxe"/>) to match every destroyable and core it defends —
/// obsidian upgrading to diamond, anything softer staying at the corpus-default iron.
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
}
