using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Whether a theme's ground says whose land it is (<see cref="Materials.TintsByTeam"/>) — the question
/// <c>PT5</c> asks before it asks who walks there.
///
/// <para>A tint can sit at any depth of a material tree, because every pattern carries materials and one of
/// them can be the tint. What the answer is read for is a bucket the painter writes, so a bucket its own
/// toggle turns off states no colour however it is built.</para>
/// </summary>
public sealed class TintsByTeamTests
{
    private const int Clay = Palette.Blocks.StainedClay;
    private const int Grass = Palette.Blocks.Grass;
    private const int Stone = Palette.Blocks.Stone;

    private static TeamTintedMaterial Tint => new(Clay, new SolidMaterial(Stone));

    [Test]
    public async Task A_plain_material_states_no_team()
    {
        await Assert.That(Materials.TintsByTeam(new SolidMaterial(Grass))).IsFalse();
        await Assert.That(Materials.TintsByTeam((TerrainMaterial?)null)).IsFalse();
    }

    [Test]
    public async Task A_tint_at_the_root_states_one()
    {
        await Assert.That(Materials.TintsByTeam(Tint)).IsTrue();
    }

    /// <summary>The ordinary shape: grass over a tinted course, which is a stack and not a root.</summary>
    [Test]
    public async Task A_tint_inside_a_stack_states_one()
    {
        var surface = new LayeredMaterial(new BandStack(
            [new Band(new SolidMaterial(Grass), 1), new Band(Tint, 2)], BandEnding.Repeat));

        await Assert.That(Materials.TintsByTeam(surface)).IsTrue();
    }

    /// <summary>And a tint a pattern picks, which is the case a walk stopping at the root would miss: the
    /// cell pattern answers one of its palette per cell and one of them is the tint.</summary>
    [Test]
    public async Task A_tint_a_pattern_picks_states_one()
    {
        var mottled = new CellMaterial(Seed: 1, CellSize: 6, Jitter: 40, Warp: 0,
                                       Palette: [new SolidMaterial(Grass), Tint]);

        await Assert.That(Materials.TintsByTeam(mottled)).IsTrue();
    }

    /// <summary>What a stack hands its unclaimed ground to is painted too, so it is asked.</summary>
    [Test]
    public async Task A_tint_a_stack_hands_over_to_states_one()
    {
        var ringed = new LayeredMaterial(
            new BandStack([new Band(new SolidMaterial(Stone), 3)], BandEnding.HandOver),
            BandAxis.Inward, Beyond: Tint);

        await Assert.That(Materials.TintsByTeam(ringed)).IsTrue();
    }

    /// <summary>A theme is asked bucket by bucket, and the fill alone is enough — it claims every block the
    /// rest left.</summary>
    [Test]
    public async Task A_theme_tints_when_any_bucket_it_paints_does()
    {
        await Assert.That(Materials.TintsByTeam(TerrainTheme.Default)).IsFalse();
        await Assert.That(Materials.TintsByTeam(TerrainTheme.Default with { Fill = Tint })).IsTrue();
        await Assert.That(Materials.TintsByTeam(TerrainTheme.Default with { Wall = Tint })).IsTrue();
        await Assert.That(Materials.TintsByTeam(
            TerrainTheme.Default with { Surface = new TopBand(Tint, Depth: 1) })).IsTrue();
    }

    /// <summary>A bucket its own toggle turns off writes nothing, so the colour it would have stated is a
    /// colour no player sees.</summary>
    [Test]
    public async Task A_bucket_that_is_switched_off_states_no_colour()
    {
        await Assert.That(Materials.TintsByTeam(
            TerrainTheme.Default with { Wall = Tint, WallEnabled = false })).IsFalse();
        await Assert.That(Materials.TintsByTeam(
            TerrainTheme.Default with { Rim = new TopBand(Tint, Depth: 1, Enabled: false) })).IsFalse();
    }
}
