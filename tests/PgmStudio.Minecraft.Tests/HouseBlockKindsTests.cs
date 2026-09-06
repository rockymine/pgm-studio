using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The catalogue against the gate that refuses from it. Asserted as agreement rather than as a transcription:
/// a copy of the table would pass against a table copied wrong, which is the failure one catalogue exists to
/// prevent.
/// </summary>
public sealed class HouseBlockKindsTests
{
    [Test]
    public async Task Every_field_names_a_kind_that_has_blocks()
    {
        foreach (var field in HouseBlockKinds.Fields)
        {
            await Assert.That(BlockKinds.All).Contains(field.Kind);
            await Assert.That(HouseBlockKinds.BlocksOf(field.Kind)).IsNotEmpty();
        }
    }

    /// <summary>A block the catalogue offers is a block the gate takes, and one it does not offer is one the
    /// gate refuses. Read off the gate itself, so a family that grew an id the catalogue cannot see fails.</summary>
    [Test]
    public async Task Every_offered_block_is_one_the_gate_accepts()
    {
        foreach (var kind in BlockKinds.All)
            foreach (var block in HouseBlockKinds.BlocksOf(kind))
                await Assert.That(HouseBlockKinds.Accepts(kind, block.Id)).IsTrue();
    }

    /// <summary>A double slab is a full cube wearing a slab's name, so it is never offered where a slab is
    /// asked for — the one mistake <c>HS1</c>'s slab fields exist to name.</summary>
    [Test]
    public async Task No_double_slab_is_offered_as_a_slab()
    {
        foreach (var block in HouseBlockKinds.BlocksOf(BlockKinds.Slab))
            await Assert.That(BlockFamilies.IsDoubleSlab(block.Id)).IsFalse();
    }

    /// <summary>Every offered block reads back as a material, which is what <c>HS4</c> pairs two fields of one
    /// part by — a stair offered with no material could never be matched to its slab.</summary>
    [Test]
    public async Task Every_offered_block_names_the_material_the_pairing_rule_reads()
    {
        foreach (var kind in BlockKinds.All)
            foreach (var block in HouseBlockKinds.BlocksOf(kind))
                await Assert.That(block.Material).IsEqualTo(BlockMaterials.Of(block.Id, block.Data));
    }

    /// <summary>The refusal is the catalogue's own row: the field it names and the sentence it carries are the
    /// ones served, so an author reading the catalogue reads what the gate would say.</summary>
    [Test]
    public async Task A_beam_of_the_wrong_kind_is_refused_in_the_catalogues_own_words()
    {
        var style = new HouseStyle { Beams = new BeamStyle { Block = Blocks.Stone, Reach = 1 } };

        var finding = HouseStyleValidation.Check(style)
            .Single(f => f.Rule == HouseStyleRules.BlockKind);

        await Assert.That(finding.Field).IsEqualTo(HouseBlockKinds.Beams.Field);
        await Assert.That(finding.Message).Contains(HouseBlockKinds.Beams.Means);
    }

    /// <summary>Every log the catalogue offers passes the beam field, which is the round trip the catalogue is
    /// for: pick from it and the gate does not refuse.</summary>
    [Test]
    public async Task Every_offered_log_passes_the_beam_field()
    {
        foreach (var block in HouseBlockKinds.BlocksOf(BlockKinds.Log))
        {
            var style = new HouseStyle { Beams = new BeamStyle { Block = block.Id, Data = block.Data, Reach = 1 } };
            await Assert.That(HouseStyleValidation.Check(style)
                .Any(f => f.Rule == HouseStyleRules.BlockKind && f.Field == HouseBlockKinds.Beams.Field)).IsFalse();
        }
    }
}
