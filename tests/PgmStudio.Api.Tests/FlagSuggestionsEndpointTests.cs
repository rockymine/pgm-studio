using System.Net.Http.Json;
using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.Extensions.DependencyInjection;
using PgmStudio.Data.Features;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Api.Tests;

/// <summary>
/// GET /api/map/{slug}/flag-suggestions — the configure tool's read of the banners the ingest scan found. The
/// world is gone after import, so the stored rows are all the tier sees: every banner comes back where it
/// stands, facing the way it faces, in its dye, with the flag defaults beside them.
/// </summary>
[NotInParallel("api-db")]
public sealed class FlagSuggestionsEndpointTests
{
    [Test]
    public async Task Every_banner_comes_back_where_it_stands_facing_its_way()
    {
        await ApiTestFactory.ResetSchemaAsync();
        var client = ApiTestFactory.Shared.CreateClient();
        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Flag Map" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
        using (var scope = ApiTestFactory.Shared.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PgmDb>();
            var mapId = await db.Maps.Where(m => m.Slug == slug).Select(m => m.Id).FirstAsync();
            await FlagCandidateStore.WriteAsync(db, mapId,
            [
                new BannerFeature(10, 20, 30, Wall: false, Facing: 4, "yellow", ["bs:white"]),
                new BannerFeature(-5, 22, 7, Wall: true, Facing: 5, "red", []),
            ]);
        }

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/flag-suggestions");

        var defaults = body.GetProperty("defaults");
        await Assert.That(defaults.GetProperty("pointsRate").GetDouble()).IsEqualTo(ObjectiveDefaults.FlagPointsRate);
        await Assert.That(defaults.GetProperty("carrierHearts").GetInt32()).IsEqualTo(ObjectiveDefaults.FlagCarrierHearts);
        var banners = body.GetProperty("banners").EnumerateArray().ToList();
        await Assert.That(banners.Count).IsEqualTo(2);
        var standing = banners.Single(banner => !banner.GetProperty("wall").GetBoolean());
        await Assert.That((standing.GetProperty("x").GetInt32(), standing.GetProperty("y").GetInt32(), standing.GetProperty("z").GetInt32()))
            .IsEqualTo((10, 20, 30));
        await Assert.That(standing.GetProperty("yaw").GetDouble()).IsEqualTo(90.0).Because("a quarter turn from south is west");
        await Assert.That(standing.GetProperty("color").GetString()).IsEqualTo("yellow");
        await Assert.That(standing.GetProperty("patterns").EnumerateArray().Single().GetString()).IsEqualTo("bs:white");
        var wall = banners.Single(banner => banner.GetProperty("wall").GetBoolean());
        await Assert.That(wall.GetProperty("yaw").GetDouble()).IsEqualTo(270.0).Because("a wall banner on data 5 faces east");
    }
}
