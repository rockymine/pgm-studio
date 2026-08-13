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
}
