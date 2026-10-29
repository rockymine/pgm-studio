using System.Text;
using PgmStudio.Api.Services;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A library card is named before it is drawn: a list offers its route without drawing, the first ask draws it
/// once and keeps it, a card that draws nothing lists as nothing afterwards, and a name no list offered is not
/// a card.
/// </summary>
public sealed class LibraryCardsTests
{
    private static string Fresh() => Drawings.Name("library-cards-test/" + Guid.NewGuid().ToString("N"), "row");

    [Test]
    public async Task Offering_a_card_names_its_route_and_draws_nothing()
    {
        var name = Fresh();
        var draws = 0;

        var markup = LibraryCards.Offer(new LibraryCard(name, LibraryCard.Sprites, () => { draws++; return [1]; }));

        await Assert.That(markup).Contains($"src=\"/api/cards/{name}\"");
        await Assert.That(markup).Contains("loading=\"lazy\"");
        await Assert.That(markup).StartsWith("<img class=\"block-picture\"");
        await Assert.That(draws).IsEqualTo(0);
        await Assert.That(LibraryCards.Waiting).Contains(name);
    }

    [Test]
    public async Task An_offered_card_is_drawn_once_when_asked_for_and_kept()
    {
        var name = Fresh();
        var draws = 0;
        LibraryCards.Offer(new LibraryCard(name, LibraryCard.Rendered, () => { draws++; return Encoding.UTF8.GetBytes("<svg/>"); }));

        var first = await LibraryCards.FindAsync(name, CancellationToken.None);
        var second = await LibraryCards.FindAsync(name, CancellationToken.None);

        await Assert.That(first.Known && second.Known).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(second.Picture!)).IsEqualTo("<svg/>");
        await Assert.That(draws).IsEqualTo(1);
        await Assert.That(LibraryCards.Waiting).DoesNotContain(name);
    }

    [Test]
    public async Task A_card_that_cannot_draw_lists_as_nothing_once_it_has_been_tried()
    {
        var name = Fresh();
        var card = new LibraryCard(name, LibraryCard.Sprites, () => throw new FormatException("unreadable params"));
        LibraryCards.Offer(card);

        var found = await LibraryCards.FindAsync(name, CancellationToken.None);

        await Assert.That(found.Known).IsTrue();
        await Assert.That(found.Picture).IsNull();
        await Assert.That(LibraryCards.Offer(card)).IsEqualTo("");
    }

    [Test]
    public async Task A_name_no_list_offered_is_not_a_card()
    {
        var found = await LibraryCards.FindAsync(Fresh(), CancellationToken.None);
        await Assert.That(found.Known).IsFalse();
        await Assert.That(LibraryCards.IsName("not-a-card")).IsFalse();
    }

    [Test]
    public async Task A_card_is_served_as_the_picture_it_is()
    {
        await Assert.That(LibraryCards.ContentTypeOf([0x89, 0x50, 0x4E, 0x47, 0x0D])).IsEqualTo("image/png");
        await Assert.That(LibraryCards.ContentTypeOf("<svg/>"u8)).IsEqualTo("image/svg+xml");
    }
}
