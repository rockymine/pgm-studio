using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The views of a board from a player's eye: the studio suggests some from the built board, an author keeps
/// others, and a kept one is let go by its id. A view is only where to stand and what to look at, so what is
/// held is the list and its query words — the picture is <c>render/eye</c>'s.
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class MapViewEndpointsTests
{
    private static string Views => $"/api/map/{SketchBoard.Slug}/views";

    private static async Task<List<JsonElement>> ListAsync(HttpClient client) =>
        [.. (await client.GetFromJsonAsync<JsonElement>(Views)).GetProperty("views").EnumerateArray()];

    [Test]
    public async Task A_built_board_suggests_its_whole_and_what_stands_on_it()
    {
        using var client = await SketchBoard.FreshAsync();
        var placed = await client.PostAsJsonAsync($"/api/map/{SketchBoard.Slug}/sketch/props",
            new { kind = "boulder", x = 8, z = 8, seed = 2, size = 4 });
        await Assert.That(placed.IsSuccessStatusCode).IsTrue().Because(await placed.Content.ReadAsStringAsync());

        var views = await ListAsync(client);

        await Assert.That(views.Any(view => view.GetProperty("id").GetString() == "overview")).IsTrue();
        await Assert.That(views.Any(view => view.GetProperty("name").GetString() == "Boulder 1")).IsTrue();
        await Assert.That(views.All(view => !view.GetProperty("kept").GetBoolean())).IsTrue();
    }

    [Test]
    public async Task A_kept_view_is_listed_after_the_suggestions_and_let_go_by_its_id()
    {
        using var client = await SketchBoard.FreshAsync();

        var kept = await client.PostAsJsonAsync(Views, new { name = "Porch", lookX = 3, lookZ = 4, fromX = 10, fromZ = 10 });
        await Assert.That(kept.IsSuccessStatusCode).IsTrue().Because(await kept.Content.ReadAsStringAsync());
        var view = await kept.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(view.GetProperty("id").GetString()).IsEqualTo("view-1");
        await Assert.That(view.GetProperty("query").GetString()).IsEqualTo("look=3,4&from=10,10");

        var last = (await ListAsync(client))[^1];
        await Assert.That(last.GetProperty("id").GetString()).IsEqualTo("view-1");
        await Assert.That(last.GetProperty("kept").GetBoolean()).IsTrue();

        await Assert.That((await client.DeleteAsync($"{Views}/view-1")).StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await client.DeleteAsync($"{Views}/view-1")).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That((await ListAsync(client)).Any(entry => entry.GetProperty("kept").GetBoolean())).IsFalse();
    }

    [Test]
    public async Task A_stand_point_with_one_coordinate_is_refused_naming_it()
    {
        using var client = await SketchBoard.FreshAsync();

        var refused = await client.PostAsJsonAsync(Views, new { lookX = 0, lookZ = 0, fromX = 5 });

        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var finding = (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("fromX");
    }
}
