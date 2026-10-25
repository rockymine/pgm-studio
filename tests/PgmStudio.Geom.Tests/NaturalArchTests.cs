using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>A natural arch's deck is a surface that rises to a crown and falls to its feet, with rock hanging
/// thin at the crown and heavy at the feet.</summary>
public sealed class NaturalArchTests
{
    private static readonly NaturalArch Arch = NaturalArch.Of(4, [10, 4], [4, 12, 1], 0, 0);

    [Test]
    public async Task The_deck_stands_at_the_crown_in_the_middle_and_at_the_end_at_the_feet()
    {
        await Assert.That(Arch.Run(0.5, 0)).IsEqualTo((6, 11)).Because("crown 10, four of rock under it");
        await Assert.That(Arch.Run(0, 0)).IsEqualTo((-8, 5)).Because("end 4, twelve of rock under it");
        await Assert.That(Arch.Run(0.25, 0)).IsEqualTo((-1, 8)).Because("half way down the span");
    }

    [Test]
    public async Task The_two_halves_of_the_span_are_alike()
    {
        await Assert.That(Arch.Run(0.2, 1)).IsEqualTo(Arch.Run(0.8, 1));
    }

    [Test]
    public async Task Camber_lowers_the_deck_towards_the_sides()
    {
        var cambered = NaturalArch.Of(4, [10, 4], [4, 12, 1], 2, 0);

        await Assert.That(cambered.Run(0.5, 4).Top).IsEqualTo(9);
        await Assert.That(cambered.Run(0.5, 0).Top).IsEqualTo(11);
    }

    [Test]
    public async Task A_deck_not_stated_is_flat_at_the_height_given()
    {
        var flat = NaturalArch.Of(3, null, null, 0, 7);

        await Assert.That(flat.Run(0.5, 0).Top).IsEqualTo(8);
        await Assert.That(flat.Run(0, 0).Top).IsEqualTo(8);
    }
}
