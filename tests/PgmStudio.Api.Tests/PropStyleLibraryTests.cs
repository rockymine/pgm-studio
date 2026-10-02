using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A tree or boulder recipe's card is kept by what draws it (<see cref="Drawings"/>), and what draws it is the
/// recipe as much as the sample prop standing it up — so two recipes that differ never share a card.
/// </summary>
public sealed class PropStyleLibraryTests
{
    private const string Stone = """{"kind":"solid","id":1,"data":0}""";
    private const string Cobblestone = """{"kind":"solid","id":4,"data":0}""";

    [Test]
    public async Task Two_tree_recipes_draw_two_cards()
    {
        var oak = PropStyleLibrary.CardOf(new TreeStyleSaveRequest("oak", TreeForms.Template, "oak", Height: 10));
        var spruce = PropStyleLibrary.CardOf(new TreeStyleSaveRequest("spruce", TreeForms.Template, "spruce", Height: 10));
        var tall = PropStyleLibrary.CardOf(new TreeStyleSaveRequest("tall", TreeForms.Template, "oak", Height: 16));

        await Assert.That(spruce).IsNotEqualTo(oak);
        await Assert.That(tall).IsNotEqualTo(oak);
    }

    [Test]
    public async Task Two_boulder_recipes_draw_two_cards()
    {
        var round = PropStyleLibrary.CardOf(new BoulderStyleSaveRequest("a", BoulderForms.Round, 5, false, Stone));
        var angular = PropStyleLibrary.CardOf(new BoulderStyleSaveRequest("b", BoulderForms.Angular, 5, false, Stone));
        var cobble = PropStyleLibrary.CardOf(new BoulderStyleSaveRequest("c", BoulderForms.Round, 5, false, Cobblestone));

        await Assert.That(angular).IsNotEqualTo(round);
        await Assert.That(cobble).IsNotEqualTo(round);
    }
}
