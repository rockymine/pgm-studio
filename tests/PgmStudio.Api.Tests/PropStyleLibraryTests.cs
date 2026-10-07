using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Minecraft.Render;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Library;
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
        var oak = PropStyleLibrary.CardOf(new TreeStyleSaveRequest("oak", TreeForms.Template, "oak", Height: 10), PictureSprites.Flat);
        var spruce = PropStyleLibrary.CardOf(new TreeStyleSaveRequest("spruce", TreeForms.Template, "spruce", Height: 10), PictureSprites.Flat);
        var tall = PropStyleLibrary.CardOf(new TreeStyleSaveRequest("tall", TreeForms.Template, "oak", Height: 16), PictureSprites.Flat);

        await Assert.That(spruce).IsNotEqualTo(oak);
        await Assert.That(tall).IsNotEqualTo(oak);
    }

    [Test]
    public async Task Two_boulder_recipes_draw_two_cards()
    {
        var round = PropStyleLibrary.CardOf(new BoulderStyleSaveRequest("a", BoulderForms.Round, 5, false, Stone), PictureSprites.Flat);
        var angular = PropStyleLibrary.CardOf(new BoulderStyleSaveRequest("b", BoulderForms.Angular, 5, false, Stone), PictureSprites.Flat);
        var cobble = PropStyleLibrary.CardOf(new BoulderStyleSaveRequest("c", BoulderForms.Round, 5, false, Cobblestone), PictureSprites.Flat);

        await Assert.That(angular).IsNotEqualTo(round);
        await Assert.That(cobble).IsNotEqualTo(round);
    }

    /// <summary>The sprites a studio with a texture jar draws with: two flat ones is enough for a card to be the
    /// textured kind, since a block with no sprite takes its palette colour.</summary>
    private static PictureSprites Textured() => new(
        BlockTextureSet.Of(new Dictionary<string, BlockSprite>
        {
            ["stone"] = new(2, [120, 120, 120, 255, 120, 120, 120, 255, 120, 120, 120, 255, 120, 120, 120, 255]),
        }), "test-jar");

    [Test]
    public async Task A_studio_with_textures_draws_a_tree_and_a_boulder_as_a_picture_and_without_them_as_a_flat_card()
    {
        var tree = new TreeStyleSaveRequest("oak", TreeForms.Template, "oak", Height: 10);
        var boulder = new BoulderStyleSaveRequest("a", BoulderForms.Round, 5, false, Stone);

        foreach (var card in new[]
                 {
                     PropStyleLibrary.CardOf(tree, Textured()), PropStyleLibrary.CardOf(boulder, Textured()),
                 })
        {
            await Assert.That(card).StartsWith("<img class=\"block-render\"");
            await Assert.That(card).Contains("data:image/png;base64,");
        }
        await Assert.That(PropStyleLibrary.CardOf(tree, PictureSprites.Flat)).StartsWith("<svg");
        await Assert.That(PropStyleLibrary.CardOf(boulder, PictureSprites.Flat)).StartsWith("<svg");
    }

    [Test]
    public async Task A_house_and_each_of_its_parts_draw_as_a_picture_where_there_are_textures()
    {
        var style = SeedFolder.Houses[0].Style;

        foreach (var part in new string?[] { null, RoomParts.Roof, RoomParts.Wall })
        {
            await Assert.That(RoomStylePreview.CardOnce(style, Textured(), part)).StartsWith("<img class=\"block-render\"");
            await Assert.That(RoomStylePreview.CardOnce(style, PictureSprites.Flat, part)).StartsWith("<svg");
        }
    }

    [Test]
    public async Task Two_tree_recipes_draw_two_textured_cards()
    {
        var oak = PropStyleLibrary.CardOf(new TreeStyleSaveRequest("oak", TreeForms.Template, "oak", Height: 10), Textured());
        var spruce = PropStyleLibrary.CardOf(new TreeStyleSaveRequest("spruce", TreeForms.Template, "spruce", Height: 10), Textured());

        await Assert.That(spruce).IsNotEqualTo(oak);
    }
}
