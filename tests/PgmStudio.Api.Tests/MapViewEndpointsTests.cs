using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The views of a board from a player's eye: every board keeps its own straight-down view, the studio suggests
/// others from the built board, an author keeps more, and a kept one is changed or let go by its id. A view is only where to stand and what to look at, so what is
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
        await Assert.That(views.Where(view => view.GetProperty("kept").GetBoolean()).Select(view => view.GetProperty("id").GetString()!))
            .IsEquivalentTo(["above"]);
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
        await Assert.That((await ListAsync(client)).Where(entry => entry.GetProperty("kept").GetBoolean())
            .Select(entry => entry.GetProperty("id").GetString()!)).IsEquivalentTo(["above"]);
    }

    [Test]
    public async Task Every_board_keeps_its_straight_down_view_first_and_it_is_changed_rather_than_let_go()
    {
        using var client = await SketchBoard.FreshAsync();

        var first = (await ListAsync(client))[0];
        await Assert.That(first.GetProperty("id").GetString()).IsEqualTo("above");
        await Assert.That(first.GetProperty("kept").GetBoolean()).IsTrue();
        await Assert.That(first.GetProperty("own").GetBoolean()).IsTrue();
        await Assert.That(first.GetProperty("pitch").GetDouble()).IsEqualTo(90.0);
        var refused = await client.DeleteAsync($"{Views}/above");
        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.Conflict);

        var changed = await client.PutAsJsonAsync($"{Views}/above", new { lookX = 0, lookZ = 0, fromX = 0, fromZ = 1, y = 150, pitch = 80 });
        await Assert.That(changed.IsSuccessStatusCode).IsTrue().Because(await changed.Content.ReadAsStringAsync());
        var adjusted = (await ListAsync(client))[0];
        await Assert.That(adjusted.GetProperty("name").GetString()).IsEqualTo("Straight down");
        await Assert.That(adjusted.GetProperty("pitch").GetDouble()).IsEqualTo(80.0);

        await Assert.That((await client.DeleteAsync($"{Views}/above")).StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await ListAsync(client))[0].GetProperty("pitch").GetDouble()).IsEqualTo(90.0);
    }

    [Test]
    public async Task A_kept_view_is_changed_in_place_by_its_id_and_an_unknown_one_is_not_found()
    {
        using var client = await SketchBoard.FreshAsync();
        await client.PostAsJsonAsync(Views, new { name = "Porch", lookX = 3, lookZ = 4, fromX = 10, fromZ = 10 });

        var changed = await client.PutAsJsonAsync($"{Views}/view-1", new { lookX = 5, lookZ = 5, fromX = 12, fromZ = 12, y = 40 });
        await Assert.That(changed.IsSuccessStatusCode).IsTrue();
        var view = (await ListAsync(client)).Single(entry => entry.GetProperty("id").GetString() == "view-1");
        await Assert.That(view.GetProperty("name").GetString()).IsEqualTo("Porch");
        await Assert.That(view.GetProperty("query").GetString()).IsEqualTo("look=5,5&from=12,12&y=40");

        var missing = await client.PutAsJsonAsync($"{Views}/view-9", new { lookX = 0, lookZ = 0 });
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        var suggestion = await client.PutAsJsonAsync($"{Views}/overview", new { lookX = 0, lookZ = 0 });
        await Assert.That(suggestion.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
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
