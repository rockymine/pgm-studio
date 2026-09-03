using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The last two parts of the finish to get an address: the shells a map's rooms are stamped in, and the biome
/// its columns carry.
///
/// <para>The room binding is the one part with <b>three</b> states rather than two, and the routes have to
/// keep them apart: absent falls back to the built-in shell, an explicit null asked for open ground, and a
/// stated style is itself. A pad on a plateau the plan already shaped is a real thing to want, and it is not
/// the same as never having answered.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class SketchShellBiomeEndpointsTests
{
    private static string Shells => $"/api/map/{SketchBoard.Slug}/sketch/room-styles";
    private static string Biome => $"/api/map/{SketchBoard.Slug}/sketch/biome";

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static async Task<(bool Cage, bool Spawn)> ShellsAsync(HttpClient client)
    {
        var d = await client.GetFromJsonAsync<JsonElement>(Shells);
        return (d.GetProperty("cage").ValueKind != JsonValueKind.Null,
                d.GetProperty("spawn").ValueKind != JsonValueKind.Null);
    }

    /// <summary>All three states of a binding are reachable and tell each other apart: a board that bound
    /// nothing resolves both shells to the built-in ones, a null binds open ground, and a delete puts the
    /// built-in one back rather than leaving open ground behind.</summary>
    [Test]
    public async Task A_binding_tells_open_ground_from_never_having_asked()
    {
        using var client = await SketchBoard.FreshAsync();

        await Assert.That(await ShellsAsync(client)).IsEqualTo((true, true))
            .Because("a board that bound nothing stamps the built-in shells");

        await Assert.That((await client.DeleteAsync($"{Shells}/spawn")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound).Because("there is no binding to remove yet");

        var open = await client.PutAsync($"{Shells}/spawn", Json("null"));
        await Assert.That(open.IsSuccessStatusCode).IsTrue().Because(await open.Content.ReadAsStringAsync());
        await Assert.That(await ShellsAsync(client)).IsEqualTo((true, false))
            .Because("null is a statement — a pad rather than a building over it");

        await Assert.That((await client.DeleteAsync($"{Shells}/spawn")).StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await ShellsAsync(client)).IsEqualTo((true, true))
            .Because("unbinding restores the built-in shell, which is not the same as open ground");
    }

    [Test]
    public async Task A_room_part_the_layout_does_not_have_is_refused()
    {
        using var client = await SketchBoard.FreshAsync();

        var resp = await client.PutAsync($"{Shells}/turret", Json("null"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var finding = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("part");
        await Assert.That(finding.GetProperty("message").GetString()).Contains("cage");
    }

    /// <summary>The biome field: absent is plains everywhere, which is why nothing stated answers 404 rather
    /// than a default nobody wrote.</summary>
    [Test]
    public async Task A_biome_field_is_stated_replaced_and_taken_away()
    {
        using var client = await SketchBoard.FreshAsync();

        await Assert.That((await client.GetAsync(Biome)).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That((await client.DeleteAsync(Biome)).StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        var put = await client.PutAsync(Biome, Json("""{"kind":"cell","seed":7,"cellSize":48,"palette":[1,4,6]}"""));
        await Assert.That(put.IsSuccessStatusCode).IsTrue().Because(await put.Content.ReadAsStringAsync());

        var field = await client.GetFromJsonAsync<JsonElement>(Biome);
        await Assert.That(field.GetProperty("kind").GetString()).IsEqualTo("cell");
        await Assert.That(field.GetProperty("cellSize").GetInt32()).IsEqualTo(48);

        await client.PutAsync(Biome, Json("""{"kind":"solid","id":2}"""));
        var replaced = await client.GetFromJsonAsync<JsonElement>(Biome);
        await Assert.That(replaced.GetProperty("kind").GetString()).IsEqualTo("solid");

        await Assert.That((await client.DeleteAsync(Biome)).StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await client.GetAsync(Biome)).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_biome_kind_the_reader_does_not_know_is_refused()
    {
        using var client = await SketchBoard.FreshAsync();

        var resp = await client.PutAsync(Biome, Json("""{"kind":"marble","id":2}"""));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var finding = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ1");
        await Assert.That(finding.GetProperty("message").GetString()).Contains("noise");
    }
}
