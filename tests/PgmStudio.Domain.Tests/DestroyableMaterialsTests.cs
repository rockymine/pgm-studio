using PgmStudio.Domain;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// The materials a generated destroyable can be built from. The list is the authoring vocabulary <i>and</i>
/// the stamper's capability, which is why it is one list: a picker offering a material the stamper cannot
/// place would build obsidian while the map.xml says emerald, and the goal would be unbreakable by the
/// material its own rule names.
/// </summary>
public sealed class DestroyableMaterialsTests
{
    [Test]
    public async Task Each_offered_material_resolves_to_its_own_block()
    {
        await Assert.That(DestroyableMaterials.BlockId("obsidian")).IsEqualTo(49);
        await Assert.That(DestroyableMaterials.BlockId("emerald block")).IsEqualTo(133);
        await Assert.That(DestroyableMaterials.BlockId("gold block")).IsEqualTo(41);
        await Assert.That(DestroyableMaterials.BlockId("ender stone")).IsEqualTo(121);
    }

    [Test]
    public async Task The_spellings_PGM_treats_as_one_name_are_one_material_here_too()
    {
        // PGM's material lookup is case- and separator-insensitive, so an author who writes end_stone means
        // the same block as one who writes "Ender Stone"; resolving only the literal offered string would
        // silently drop an imported plan to obsidian.
        await Assert.That(DestroyableMaterials.BlockId("end_stone")).IsEqualTo(121);
        await Assert.That(DestroyableMaterials.BlockId("ENDER-STONE")).IsEqualTo(121);
        await Assert.That(DestroyableMaterials.BlockId("emerald_block")).IsEqualTo(133);
    }

    [Test]
    public async Task A_material_the_stamper_cannot_build_falls_back_to_obsidian()
    {
        // Both directions of "cannot build": a real block outside the vocabulary, and a name nothing knows.
        await Assert.That(DestroyableMaterials.BlockId("diamond block")).IsEqualTo(49);
        await Assert.That(DestroyableMaterials.BlockId("not a block")).IsEqualTo(49);
        await Assert.That(DestroyableMaterials.BlockId("")).IsEqualTo(49);
        await Assert.That(DestroyableMaterials.BlockId(null)).IsEqualTo(49);
    }

    [Test]
    public async Task The_default_material_leads_the_offered_list()
    {
        // A picker takes its order from this list, and the value a bare marker already has should be the one
        // it opens on rather than something the author has to scroll back to.
        await Assert.That(DestroyableMaterials.All[0]).IsEqualTo(ObjectiveDefaults.Materials);
        await Assert.That(DestroyableMaterials.All.Count).IsEqualTo(4);
    }

    [Test]
    public async Task Every_offered_material_is_one_the_id_table_can_name()
    {
        // The guard on the list itself: adding a row here that MaterialIds cannot resolve would offer a
        // material that silently builds obsidian, which is the failure this vocabulary exists to prevent.
        foreach (var material in DestroyableMaterials.All)
            await Assert.That(MaterialIds.ResolvesFully(material)).IsTrue();
    }

    [Test]
    public async Task Every_offered_material_is_buildable()
    {
        foreach (var material in DestroyableMaterials.All)
            await Assert.That(DestroyableMaterials.IsBuildable(material)).IsTrue();
    }

    [Test]
    public async Task A_name_the_stamper_cannot_build_is_not_buildable()
    {
        // The whole point of IsBuildable: catch, before authoring, exactly the case BlockId's own fallback
        // hides — a declared material that would silently become obsidian in the world.
        await Assert.That(DestroyableMaterials.IsBuildable("diamond block")).IsFalse();
        await Assert.That(DestroyableMaterials.IsBuildable("not a block")).IsFalse();
    }

    [Test]
    public async Task Empty_is_buildable_the_obsidian_default()
    {
        await Assert.That(DestroyableMaterials.IsBuildable(null)).IsTrue();
        await Assert.That(DestroyableMaterials.IsBuildable("")).IsTrue();
    }

    [Test]
    public async Task A_compound_match_is_buildable_if_any_pattern_is()
    {
        // Mirrors BlockId's own "first buildable pattern wins" reading of a ; separated match.
        await Assert.That(DestroyableMaterials.IsBuildable("gold block;iron block")).IsTrue();
        await Assert.That(DestroyableMaterials.IsBuildable("iron block;coal block")).IsFalse();
    }

    // ── what a style may be built in ───────────────────────────────────────────────────────────────
    [Test]
    [Arguments(DestroyableStyle.Pillar1)]
    [Arguments(DestroyableStyle.Pillar2)]
    [Arguments(DestroyableStyle.Pillar3)]
    public async Task A_pillar_keeps_the_obsidian_it_asked_for(DestroyableStyle style)
    {
        // One to three blocks is what obsidian is for, and what half the corpus builds.
        var (materials, correction) = DestroyableMaterials.Resolve(style, "obsidian");
        await Assert.That(materials).IsEqualTo("obsidian");
        await Assert.That(correction).IsNull();
    }

    [Test]
    [Arguments(DestroyableStyle.Cube3, 27)]
    [Arguments(DestroyableStyle.Cube4, 64)]
    [Arguments(DestroyableStyle.ColumnPlus, 15)]
    public async Task A_goal_bigger_than_obsidian_is_worth_is_built_in_ender_stone(
        DestroyableStyle style, int blocks)
    {
        var (materials, correction) = DestroyableMaterials.Resolve(style, "obsidian");
        await Assert.That(materials).IsEqualTo(DestroyableMaterials.Bulk);
        await Assert.That(correction).IsNotNull();
        await Assert.That(correction!).Contains(blocks.ToString());
        await Assert.That(correction!).Contains(DestroyableMaterials.ObsidianLimit.ToString());
    }

    [Test]
    public async Task A_cube_in_a_material_it_may_carry_is_left_alone()
    {
        // The cap is obsidian's, not the cube's: emerald and gold are exactly what the corpus builds them in.
        await Assert.That(DestroyableMaterials.Resolve(DestroyableStyle.Cube3, "emerald block").Correction)
            .IsNull();
        await Assert.That(DestroyableMaterials.Resolve(DestroyableStyle.Cube4, "gold block").Materials)
            .IsEqualTo("gold block");
    }

    [Test]
    public async Task A_name_the_stamper_cannot_build_is_corrected_and_said_so()
    {
        // The silence this closes: BlockId falls back to obsidian while the name goes into the map.xml
        // verbatim, so the declared material matches nothing in its own region and the goal loads at zero
        // health. The correction is what the blocks were always going to be.
        var (materials, correction) = DestroyableMaterials.Resolve(DestroyableStyle.Pillar3, "diamond block");
        await Assert.That(materials).IsEqualTo("obsidian");
        await Assert.That(correction!).Contains("diamond block");
    }

    [Test]
    public async Task An_unbuildable_name_on_a_cube_takes_both_corrections_at_once()
    {
        // Correcting the first fault produces the second — obsidian on a 27-block cube — so one resolve
        // answers both and the caller writes one complaint.
        var (materials, correction) = DestroyableMaterials.Resolve(DestroyableStyle.Cube3, "diamond block");
        await Assert.That(materials).IsEqualTo(DestroyableMaterials.Bulk);
        await Assert.That(correction!).Contains("diamond block");
        await Assert.That(correction!).Contains("27 blocks");
    }

    [Test]
    public async Task A_compound_match_is_judged_on_the_block_it_would_actually_lay()
    {
        // BlockId takes the first buildable pattern, so `gold block;obsidian` builds gold — correcting it
        // would be correcting something that never happens. The other order really does build obsidian.
        await Assert.That(DestroyableMaterials.Resolve(DestroyableStyle.Cube3, "gold block;obsidian").Correction)
            .IsNull();
        await Assert.That(DestroyableMaterials.Resolve(DestroyableStyle.Cube3, "obsidian;gold block").Materials)
            .IsEqualTo(DestroyableMaterials.Bulk);
    }

    [Test]
    public async Task An_unstated_material_is_the_default_and_no_complaint_on_a_pillar()
    {
        await Assert.That(DestroyableMaterials.Resolve(DestroyableStyle.Pillar3, null).Correction).IsNull();
        await Assert.That(DestroyableMaterials.Resolve(DestroyableStyle.Pillar3, null).Materials)
            .IsEqualTo("obsidian");
        // …and on a cube it is the default that is over the cap, so the correction is still made.
        await Assert.That(DestroyableMaterials.Resolve(DestroyableStyle.Cube3, null).Materials)
            .IsEqualTo(DestroyableMaterials.Bulk);
    }
}
