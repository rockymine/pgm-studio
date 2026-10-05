using PgmStudio.Vocabulary;

namespace PgmStudio.Vocabulary.Tests;

public sealed class SymmetryModesTests
{
    [Test]
    public async Task The_sketch_offers_only_modes_a_layout_may_state()
    {
        foreach (var mode in SymmetryModes.Sketched)
            await Assert.That(SymmetryModes.All).Contains(mode);
    }

    [Test]
    public async Task The_sketch_offers_no_symmetry_as_a_choice()
    {
        await Assert.That(SymmetryModes.Sketched).Contains(SymmetryModes.None);
    }

    [Test]
    public async Task No_mode_is_listed_twice()
    {
        await Assert.That(SymmetryModes.All.Distinct().Count()).IsEqualTo(SymmetryModes.All.Length);
        await Assert.That(SymmetryModes.Sketched.Distinct().Count()).IsEqualTo(SymmetryModes.Sketched.Length);
    }
}
