using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The terrain-theme registry as addressable entries. A theme is a place rather than a board-wide blob, so an
/// author registers, replaces and removes one without restating the other twelve.
///
/// <para>The two behaviours worth pinning are the ones that could each have gone the other way: a partial
/// write runs the <b>whole layout's</b> gate, so the small route cannot be the way past the big route's
/// refusal; and a delete <b>does not</b> refuse over what still names the id, because a dangling name is
/// already a complaint on a working document and refusing would make an entry undeletable until every shape
/// naming it had been found by hand.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class SketchThemeEndpointsTests
{
    private static string Themes => $"/api/map/{SketchBoard.Slug}/sketch/themes";
    private static string MapTheme => $"/api/map/{SketchBoard.Slug}/sketch/map-theme";

    [Test]
    public async Task A_theme_is_registered_read_back_and_made_the_map_default()
    {
        using var client = await SketchBoard.FreshAsync();

        var empty = await client.GetFromJsonAsync<JsonElement>(Themes);
        await Assert.That(empty.GetProperty("themes").EnumerateObject().Any()).IsFalse();
        await Assert.That(empty.GetProperty("mapTheme").ValueKind).IsEqualTo(JsonValueKind.Null);

        var put = await client.PutAsJsonAsync($"{Themes}/meadow", SketchBoard.Theme());
        await Assert.That(put.IsSuccessStatusCode).IsTrue().Because(await put.Content.ReadAsStringAsync());

        var defaulted = await client.PutAsJsonAsync(MapTheme, new { theme = "meadow" });
        await Assert.That(defaulted.IsSuccessStatusCode).IsTrue();

        var listed = await client.GetFromJsonAsync<JsonElement>(Themes);
        await Assert.That(listed.GetProperty("mapTheme").GetString()).IsEqualTo("meadow");
        await Assert.That(listed.GetProperty("themes").GetProperty("meadow")
            .GetProperty("surface").GetProperty("depth").GetInt32()).IsEqualTo(1);

        var one = await client.GetFromJsonAsync<JsonElement>($"{Themes}/meadow");
        await Assert.That(one.GetProperty("rim").GetProperty("material").GetProperty("id").GetInt32()).IsEqualTo(4);
    }

    /// <summary>A registry entry is addressed by the name an author gave it, so the write that creates one and
    /// the write that replaces it are the same verb on the same address.</summary>
    [Test]
    public async Task Writing_the_same_id_twice_replaces_rather_than_doubles()
    {
        using var client = await SketchBoard.FreshAsync();

        await client.PutAsJsonAsync($"{Themes}/meadow", SketchBoard.Theme(surface: 2));
        await client.PutAsJsonAsync($"{Themes}/meadow", SketchBoard.Theme(surface: 12));

        var listed = await client.GetFromJsonAsync<JsonElement>(Themes);
        await Assert.That(listed.GetProperty("themes").EnumerateObject().Count()).IsEqualTo(1);
        await Assert.That(listed.GetProperty("themes").GetProperty("meadow")
            .GetProperty("surface").GetProperty("material").GetProperty("id").GetInt32()).IsEqualTo(12);
    }

    /// <summary>A theme a shape or the map default still names is removed and complained about rather than
    /// refused — the answer a working document gets everywhere else in the sketch.</summary>
    [Test]
    public async Task A_theme_the_map_default_names_is_removed_with_a_complaint()
    {
        using var client = await SketchBoard.FreshAsync();
        await client.PutAsJsonAsync($"{Themes}/meadow", SketchBoard.Theme());
        await client.PutAsJsonAsync(MapTheme, new { theme = "meadow" });

        var deleted = await client.DeleteAsync($"{Themes}/meadow");
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await deleted.Content.ReadFromJsonAsync<JsonElement>();
        var warnings = body.GetProperty("warnings").EnumerateArray().ToList();
        await Assert.That(warnings.Any(w => w.GetProperty("rule").GetString() == "SK3"
                                         && w.GetProperty("field").GetString() == "mapTheme")).IsTrue();

        await Assert.That((await client.DeleteAsync($"{Themes}/meadow")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
    }

    /// <summary><b>The small route does not get past the big route's gate.</b> A voronoi band that binds with
    /// no material in it is <c>PT2</c>, and it refuses here exactly as it does on a whole-layout PUT — the
    /// field naming the path all the way down to the band.</summary>
    [Test]
    public async Task A_theme_whose_pattern_carries_no_material_is_refused()
    {
        using var client = await SketchBoard.FreshAsync();

        var resp = await client.PutAsJsonAsync($"{Themes}/bad", new
        {
            rim = new { material = new { kind = "solid", id = 4 }, depth = 1 },
            surface = new { material = new { kind = "solid", id = 2 }, depth = 1 },
            wall = new { kind = "solid", id = 1 },
            fill = new { kind = "voronoi", seed = 1, cellSize = 7, bands = new[] { new { kind = "solid", id = 1 } } },
        });

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var finding = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("PT2");
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("themes.bad.fill.bands[0]");
    }

    [Test]
    public async Task An_id_the_registry_does_not_carry_is_not_found()
    {
        using var client = await SketchBoard.FreshAsync();

        await Assert.That((await client.GetAsync($"{Themes}/nope")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That((await client.DeleteAsync($"{Themes}/nope")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
    }
}
