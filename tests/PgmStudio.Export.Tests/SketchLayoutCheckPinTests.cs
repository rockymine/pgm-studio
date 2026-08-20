using PgmStudio.Minecraft.Anvil;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The one number <see cref="SketchLayoutCheck"/> restates rather than reads. <c>Pgm</c> cannot see
/// <c>VoxelWorld</c>, so the world's height is written down twice; this is where the two meet, and it fails
/// the day one of them moves — the same drift pin <c>DR-ROAD</c>'s catalogue sentence carries against the
/// standoffs the props actually keep.
/// </summary>
public sealed class SketchLayoutCheckPinTests
{
    [Test]
    public async Task The_height_the_document_gate_judges_against_is_the_world_the_builder_writes_into()
    {
        await Assert.That(SketchLayoutCheck.WorldHeight).IsEqualTo(VoxelWorld.MaxHeight);
    }
}
