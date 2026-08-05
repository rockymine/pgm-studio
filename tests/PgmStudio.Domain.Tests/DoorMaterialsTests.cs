using PgmStudio.Domain;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// The door vocabulary. The case that matters is the one the table exists for: every door that places a
/// block names the PGM material the wool-room filter has to whitelist, because a door the filter does not
/// name cannot be broken and a wool room with no way in is a map that cannot be played.
/// </summary>
public sealed class DoorMaterialsTests
{
    [Test]
    public async Task Every_door_that_places_a_block_names_the_material_that_makes_it_breakable()
    {
        foreach (var choice in DoorMaterials.All)
        {
            var placesBlock = choice.BlockId != 0;
            await Assert.That(choice.PgmMaterial is not null).IsEqualTo(placesBlock);
            await Assert.That(choice.FilterId is not null).IsEqualTo(placesBlock);
        }

        // Air is the one that places nothing: an opening is already open.
        await Assert.That(DoorMaterials.Of(DoorMaterial.Air).PgmMaterial).IsNull();
        await Assert.That(DoorMaterials.Breakable.Select(choice => choice.Material))
            .IsEquivalentTo(new[] { DoorMaterial.Web, DoorMaterial.StainedGlass, DoorMaterial.StainedGlassPane });
    }

    [Test]
    public async Task Only_the_glass_doors_take_the_rooms_colour()
    {
        await Assert.That(DoorMaterials.Of(DoorMaterial.StainedGlass).Coloured).IsTrue();
        await Assert.That(DoorMaterials.Of(DoorMaterial.StainedGlassPane).Coloured).IsTrue();
        await Assert.That(DoorMaterials.Of(DoorMaterial.Web).Coloured).IsFalse();
        await Assert.That(DoorMaterials.Of(DoorMaterial.Air).Coloured).IsFalse();
    }

    [Test]
    public async Task The_table_is_in_enum_order_so_a_lookup_is_an_index()
    {
        for (var i = 0; i < DoorMaterials.All.Count; i++)
            await Assert.That(DoorMaterials.All[i].Material).IsEqualTo((DoorMaterial)i);
    }

    [Test]
    public async Task An_unknown_slug_is_reported_rather_than_guessed()
    {
        await Assert.That(DoorMaterials.TryParse("web", out var web)).IsTrue();
        await Assert.That(web).IsEqualTo(DoorMaterial.Web);
        await Assert.That(DoorMaterials.TryParse("STAINED-GLASS", out var glass)).IsTrue();
        await Assert.That(glass).IsEqualTo(DoorMaterial.StainedGlass);

        await Assert.That(DoorMaterials.IsKnown("obsidian")).IsFalse();
        await Assert.That(DoorMaterials.IsKnown(null)).IsFalse();
        await Assert.That(DoorMaterials.IsKnown("")).IsFalse();
    }
}
