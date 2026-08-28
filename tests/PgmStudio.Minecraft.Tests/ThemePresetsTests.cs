using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The shipped named finishes (docs/world-export/terrain-painting.md §5): each is a real theme, picked instead
/// of the unthemed built-in default rather than a restatement of it.
/// </summary>
public sealed class ThemePresetsTests
{
    [Test]
    public async Task Meadow_is_not_the_default()
    {
        // Rim and Surface, not Wall or Fill: those two are what an author actually sees change when they pick
        // Meadow over painting nothing, so a silent re-convergence would show here first.
        await Assert.That(ThemePresets.Meadow.Rim.Material).IsNotEqualTo(TerrainTheme.Default.Rim.Material);
        await Assert.That(ThemePresets.Meadow.Surface.Material).IsNotEqualTo(TerrainTheme.Default.Surface.Material);
    }
}
