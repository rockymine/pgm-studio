using System.Net.Http.Json;
using System.Text.Json;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The tree and boulder cards over HTTP: with a texture jar a recipe is drawn as a picture, and without one it
/// is the flat card — the same two paths a pattern's card takes.
/// </summary>
[NotInParallel("api-db")]
public sealed class StructureCardEndpointsTests
{
    private const string Stone = """{"kind":"solid","id":1,"data":0}""";

    private static async Task<string> CardAsync(HttpClient client, string route, object draft)
    {
        var response = await client.PostAsJsonAsync(route, draft);
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("card").GetString()!;
    }

    [Test]
    public async Task With_a_jar_named_a_tree_and_a_boulder_draw_as_pictures()
    {
        using var client = TexturedFactory.Shared.CreateClient();

        var tree = await CardAsync(client, "/api/tree-styles/preview",
            new TreeStyleSaveRequest("oak", TreeForms.Template, "oak", Height: 10));
        var boulder = await CardAsync(client, "/api/boulder-styles/preview",
            new BoulderStyleSaveRequest("rock", BoulderForms.Round, 5, false, Stone));

        await Assert.That(tree).StartsWith("<img class=\"block-render\"");
        await Assert.That(boulder).StartsWith("<img class=\"block-render\"");
    }

    [Test]
    public async Task Without_textures_a_tree_and_a_boulder_draw_as_flat_cards()
    {
        using var client = ApiTestFactory.Shared.CreateClient();

        var tree = await CardAsync(client, "/api/tree-styles/preview",
            new TreeStyleSaveRequest("oak", TreeForms.Template, "oak", Height: 10));
        var boulder = await CardAsync(client, "/api/boulder-styles/preview",
            new BoulderStyleSaveRequest("rock", BoulderForms.Round, 5, false, Stone));

        await Assert.That(tree).StartsWith("<svg");
        await Assert.That(boulder).StartsWith("<svg");
    }

    [Test]
    public async Task With_a_jar_named_the_house_part_libraries_list_pictures()
    {
        using var client = TexturedFactory.Shared.CreateClient();

        foreach (var route in new[] { "/api/room-styles", "/api/roof-styles", "/api/storey-styles", "/api/porch-styles" })
        {
            var rows = await client.GetFromJsonAsync<JsonElement>(route);
            foreach (var row in rows.EnumerateArray().Take(2))
                await Assert.That(row.GetProperty("preview").GetString()!).StartsWith("<img class=\"block-render\"");
        }
    }
}
