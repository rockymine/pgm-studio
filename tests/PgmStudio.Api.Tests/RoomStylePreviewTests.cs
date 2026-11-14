using PgmStudio.Api.Services;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Vocabulary;
using PgmStudio.Minecraft.Library;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The sample a house style is drawn on. What has to hold is that every offered footprint <b>resolves</b> —
/// a shell is refused outright below 6×6 (<c>WX2</c>) and where the piece's two axes differ in parity
/// (<c>WX3</c>), so a set of proportions is only offerable if each member survives both.
/// </summary>
public sealed class RoomStylePreviewTests
{
    [Test]
    [MethodDataSource(nameof(Footprints))]
    public async Task Every_offered_footprint_draws_a_shell(string footprint)
    {
        var views = RoomStylePreview.Views(SeedFolder.Houses[0].Style, footprint: footprint);
        await Assert.That(views.Plan).IsNotEmpty();
        await Assert.That(views.Section).IsNotEmpty();
    }

    /// <summary>A style is stamped over a rectangle it says nothing about, so two proportions of one style
    /// are two buildings — which is the whole reason the sample is a parameter.</summary>
    [Test]
    public async Task A_long_shell_and_a_square_one_draw_different_buildings()
    {
        var style = SeedFolder.Houses[0].Style;
        var square = RoomStylePreview.Views(style, footprint: HouseFootprints.Square);
        var oblong = RoomStylePreview.Views(style, footprint: HouseFootprints.Long);

        await Assert.That(oblong.Plan).IsNotEqualTo(square.Plan);
    }

    /// <summary>A word outside the set costs the wrong proportion and never the picture: a footprint is how a
    /// style is looked at rather than part of the question.</summary>
    [Test]
    public async Task An_unknown_footprint_draws_the_default_rather_than_refusing()
    {
        var style = SeedFolder.Houses[0].Style;
        await Assert.That(RoomStylePreview.Views(style, footprint: "nonsense").Plan)
            .IsEqualTo(RoomStylePreview.Views(style, footprint: HouseFootprints.Default).Plan);
    }

    /// <summary>A card is the building on the shell its row states, under a name of its own for each shell, so a
    /// row moved to another shell is a different picture and never the old one served from the cache.</summary>
    [Test]
    public async Task A_card_is_drawn_on_the_footprint_it_is_asked_for()
    {
        var style = SeedFolder.Houses[0].Style;
        var names = new HashSet<string>();
        foreach (var (footprint, _, _) in HouseFootprints.All)
        {
            var card = RoomStylePreview.CardOf(style, PictureSprites.Flat, footprint: footprint);
            await Assert.That(Text(card.Draw())).IsEqualTo(RoomStylePreview.Card(style, footprint: footprint));
            names.Add(card.Name);
        }
        await Assert.That(names.Count).IsEqualTo(HouseFootprints.All.Length);
        await Assert.That(RoomStylePreview.CardOf(style, PictureSprites.Flat).Name)
            .IsEqualTo(RoomStylePreview.CardOf(style, PictureSprites.Flat, footprint: HouseFootprints.Default).Name);
    }

    private static string Text(byte[]? drawn) => System.Text.Encoding.UTF8.GetString(drawn ?? []);

    public static IEnumerable<Func<string>> Footprints()
        => HouseFootprints.All.Select(one => (Func<string>)(() => one.Id));
}
