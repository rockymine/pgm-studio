using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The dressing's own half of the map surface: a placement is a resource with an id, so a caller adds, edits
/// or removes one without holding the board it stands on — the shape the objectives already answer in.
///
/// <para>The routes exist for what they publish as much as for what they do. A body typed as
/// <c>PlacedProp</c> puts the six kinds, their knobs and their recipes in <c>/api/openapi/v1.json</c>, which
/// is where an agent is told to look before it asks for something the studio cannot do.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class SketchPropEndpointsTests
{
    private static Task<HttpClient> BoardAsync() => SketchBoard.FreshAsync();

    private static string Props => "/api/map/dressed/sketch/props";

    private static async Task<string> AddAsync(HttpClient client, object prop)
    {
        var resp = await client.PostAsJsonAsync(Props, prop);
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> ListAsync(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>(Props);

    /// <summary>A placement is added, listed and addressed without the caller ever sending the layout it
    /// lives in — which is the whole of what the route is for.</summary>
    [Test]
    public async Task A_prop_is_placed_and_read_back_without_the_board()
    {
        using var client = await BoardAsync();

        await Assert.That((await ListAsync(client)).GetProperty("props").GetArrayLength()).IsEqualTo(0);

        var id = await AddAsync(client, new { kind = "tree", x = 10, z = -14, seed = 7, style = "oak-1" });
        await Assert.That(id).IsEqualTo("tree-1");

        var props = (await ListAsync(client)).GetProperty("props");
        await Assert.That(props.GetArrayLength()).IsEqualTo(1);
        await Assert.That(props[0].GetProperty("kind").GetString()).IsEqualTo("tree");
        await Assert.That(props[0].GetProperty("x").GetInt32()).IsEqualTo(10);
    }

    /// <summary>Every write is against the stored document, so placements accumulate rather than replacing
    /// each other — the fault a partial write is most likely to have.</summary>
    [Test]
    public async Task Placements_accumulate_across_writes()
    {
        using var client = await BoardAsync();

        await AddAsync(client, new { kind = "tree", x = 1, z = 1, seed = 1 });
        await AddAsync(client, new { kind = "boulder", x = 2, z = 2, seed = 2, size = 4 });
        await AddAsync(client, new { kind = "tree", x = 3, z = 3, seed = 3 });

        var ids = (await ListAsync(client)).GetProperty("props").EnumerateArray()
            .Select(prop => prop.GetProperty("id").GetString()).ToList();
        await Assert.That(ids).IsEquivalentTo(new[] { "tree-1", "boulder-1", "tree-2" });
    }

    /// <summary>An edit keeps the placement's position in the pass's order, and a removal takes only the one
    /// addressed.</summary>
    [Test]
    public async Task A_prop_is_edited_in_place_and_removed_by_id()
    {
        using var client = await BoardAsync();
        await AddAsync(client, new { kind = "tree", x = 1, z = 1, seed = 1 });
        await AddAsync(client, new { kind = "boulder", x = 2, z = 2, seed = 2, size = 4 });
        await AddAsync(client, new { kind = "flora", x = 3, z = 3, seed = 3 });

        var patched = await client.PatchAsJsonAsync($"{Props}/boulder-1",
            new { kind = "boulder", x = 40, z = 40, seed = 9, size = 5 });
        await Assert.That(patched.IsSuccessStatusCode).IsTrue().Because(await patched.Content.ReadAsStringAsync());

        var after = (await ListAsync(client)).GetProperty("props");
        await Assert.That(after[1].GetProperty("id").GetString()).IsEqualTo("boulder-1");
        await Assert.That(after[1].GetProperty("x").GetInt32()).IsEqualTo(40);

        var deleted = await client.DeleteAsync($"{Props}/tree-1");
        await Assert.That(deleted.IsSuccessStatusCode).IsTrue();

        var left = (await ListAsync(client)).GetProperty("props").EnumerateArray()
            .Select(prop => prop.GetProperty("id").GetString()).ToList();
        await Assert.That(left).IsEquivalentTo(new[] { "boulder-1", "flora-1" });
    }

    /// <summary>An id no placement holds is a 404 rather than a silent no-op, so a client editing against a
    /// stale document is told rather than left believing the edit landed.</summary>
    [Test]
    public async Task An_id_that_names_no_placement_is_not_found()
    {
        using var client = await BoardAsync();
        await AddAsync(client, new { kind = "tree", x = 1, z = 1, seed = 1 });

        var patched = await client.PatchAsJsonAsync($"{Props}/tree-9", new { kind = "tree", x = 2, z = 2 });
        await Assert.That(patched.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        var deleted = await client.DeleteAsync($"{Props}/tree-9");
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    /// <summary>A kind the reader does not know is refused where it is posted, naming the field and every
    /// kind there is — the answer that makes the surface discoverable from a refusal as well as a schema.</summary>
    [Test]
    public async Task An_unknown_kind_is_refused_and_names_the_kinds()
    {
        using var client = await BoardAsync();

        var resp = await client.PostAsJsonAsync(Props, new { kind = "gazebo", x = 1, z = 1 });
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var finding = body.GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ1");
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("kind");
        await Assert.That(finding.GetProperty("message").GetString()).Contains("boulder");
    }
}
