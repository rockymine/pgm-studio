using System.Net;
using System.Net.Http.Json;
using PgmStudio.Contracts;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The two routes the library's pictures come from: <c>GET /api/cards/{name}</c> answers the picture a list
/// row names, cached for good because its name is what drew it, and <c>GET /api/library</c> answers each
/// library's count and its newest cards — the front page, without any library's rows.
/// </summary>
[NotInParallel("api-db")]
public sealed class LibraryCardEndpointsTests
{
    private static async Task SaveStyleAsync(HttpClient client, string name, int block)
    {
        var saved = await client.PostAsJsonAsync("/api/styles", new StyleSaveRequest(name, MaterialKind.Layered,
            TerrainThemeJson.Serialize(new LayeredMaterial(new BandStack(
                [new Band(new SolidMaterial(block), 1), new Band(new SolidMaterial(Blocks.Dirt), 2)])))));
        await Assert.That(saved.IsSuccessStatusCode).IsTrue().Because(await saved.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task A_listed_card_is_served_at_its_route_with_a_cache_that_never_asks_again()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        await SaveStyleAsync(client, "plain stone", Blocks.Stone);

        var listed = await client.GetFromJsonAsync<List<StyleDto>>("/api/styles");
        var picture = await client.GetAsync(CardPictures.RouteOf(listed![0].Preview));

        await Assert.That(picture.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(picture.Content.Headers.ContentType?.MediaType).IsEqualTo("image/svg+xml");
        await Assert.That(picture.Headers.CacheControl?.ToString()).IsEqualTo("public, max-age=31536000, immutable");
        await Assert.That(await picture.Content.ReadAsStringAsync()).Contains(BlockPalette.Hex(Blocks.Stone, 0));
    }

    [Test]
    public async Task A_name_that_is_no_card_is_not_found()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        await Assert.That((await client.GetAsync("/api/cards/" + new string('0', 64))).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That((await client.GetAsync("/api/cards/not-a-card")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task The_front_page_counts_every_library_and_carries_its_newest_three_cards()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        foreach (var (name, block) in new[] { ("a", Blocks.Stone), ("b", Blocks.Gravel), ("c", Blocks.Grass), ("d", Blocks.Sand) })
            await SaveStyleAsync(client, $"style {name}", block);

        var counts = (await client.GetFromJsonAsync<List<LibraryCount>>("/api/library"))!.ToDictionary(count => count.List);
        var styles = await client.GetFromJsonAsync<List<StyleDto>>("/api/styles");

        await Assert.That(counts.Keys).IsEquivalentTo(new[]
        {
            "styles", "themes", "biome-patterns", "room-styles", "roof-styles", "storey-styles", "porch-styles",
            "tree-styles", "boulder-styles",
        });
        await Assert.That(counts["styles"].Count).IsEqualTo(styles!.Count);
        await Assert.That(counts["styles"].Newest).IsEquivalentTo(styles.Take(3).Select(style => style.Preview));
    }
}
