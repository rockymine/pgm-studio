using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A group's interior elevation as an addressable part. The relief is the one finish key the layout already
/// declared a model for, so what these routes add is not a schema but the ability to state one island's
/// terrain without restating the board's.
///
/// <para>The judgement pinned here is that a write does <b>not</b> check the group exists. A relief is
/// authored against a fusion the compiler produces, and whether the id still names one is <c>SK1</c>'s
/// question on the compile path — where losing hand-authored terrain is the risk worth refusing over.
/// Answering it here would refuse a relief written before the geometry it belongs to, which is an order an
/// author works in.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class SketchReliefEndpointsTests
{
    private static string Relief => $"/api/map/{SketchBoard.Slug}/sketch/relief";

    private static object Field(double at = 6) => new
    {
        @base = at,
        reach = 40,
        step = 1,
        marks = new[] { new { kind = "point", at = new[] { 0.0, 0.0 }, h = 14.0, r = 6.0 } },
    };

    [Test]
    public async Task A_relief_is_stated_read_back_and_removed()
    {
        using var client = await SketchBoard.FreshAsync();

        var empty = await client.GetFromJsonAsync<JsonElement>(Relief);
        await Assert.That(empty.EnumerateObject().Any()).IsFalse();

        var put = await client.PutAsJsonAsync($"{Relief}/i", Field());
        await Assert.That(put.IsSuccessStatusCode).IsTrue().Because(await put.Content.ReadAsStringAsync());

        var one = await client.GetFromJsonAsync<JsonElement>($"{Relief}/i");
        await Assert.That(one.GetProperty("base").GetDouble()).IsEqualTo(6);
        await Assert.That(one.GetProperty("reach").GetDouble()).IsEqualTo(40);

        var listed = await client.GetFromJsonAsync<JsonElement>(Relief);
        await Assert.That(listed.EnumerateObject().Count()).IsEqualTo(1);

        await Assert.That((await client.DeleteAsync($"{Relief}/i")).StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await client.GetAsync($"{Relief}/i")).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That((await client.DeleteAsync($"{Relief}/i")).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    /// <summary>Writing the same group twice replaces its field rather than keeping both.</summary>
    [Test]
    public async Task Writing_the_same_group_twice_replaces_its_field()
    {
        using var client = await SketchBoard.FreshAsync();

        await client.PutAsJsonAsync($"{Relief}/i", Field(at: 6));
        await client.PutAsJsonAsync($"{Relief}/i", Field(at: 11));

        var listed = await client.GetFromJsonAsync<JsonElement>(Relief);
        await Assert.That(listed.EnumerateObject().Count()).IsEqualTo(1);
        await Assert.That(listed.GetProperty("i").GetProperty("base").GetDouble()).IsEqualTo(11);
    }

    /// <summary>A relief for a group the board does not carry is stored and complained about (<c>SK3</c>),
    /// not refused: the geometry it belongs to may not be drawn yet.</summary>
    [Test]
    public async Task A_relief_for_an_unknown_group_is_stored_with_a_complaint()
    {
        using var client = await SketchBoard.FreshAsync();

        var put = await client.PutAsJsonAsync($"{Relief}/not-a-group", Field());
        await Assert.That(put.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var warnings = (await put.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("warnings").EnumerateArray().ToList();
        await Assert.That(warnings.Any(w => w.GetProperty("rule").GetString() == "SK3")).IsTrue();

        await Assert.That((await client.GetAsync($"{Relief}/not-a-group")).StatusCode)
            .IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task A_body_that_is_not_a_relief_is_refused()
    {
        using var client = await SketchBoard.FreshAsync();

        var resp = await client.PutAsync($"{Relief}/i",
            new StringContent("\"not a relief\"", System.Text.Encoding.UTF8, "application/json"));

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var finding = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ1");
    }
}
