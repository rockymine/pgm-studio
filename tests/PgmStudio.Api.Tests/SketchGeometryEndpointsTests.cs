using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The drawing as addressable parts: a layer, a group and a shape each read and written without the caller
/// holding the board they are on.
///
/// <para>Two judgements are pinned here. A layer that already groups its shapes takes no shape that names
/// none, because the orbit fan and the relief are both read off a group's list and an ungrouped shape on a
/// grouped layer is built once, where it was drawn, on flat ground. And a shape patch refuses the three
/// fields the plan compiler owns, because <c>role</c> and <c>intentRef</c> are the identity a recompile
/// matches by and <c>height_authored</c> is a claim that a floor was corrected by hand.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class SketchGeometryEndpointsTests
{
    private static string Sketch => $"/api/map/{SketchBoard.Slug}/sketch";

    private static StringContent Body(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ShapeAsync(HttpClient client, string id) =>
        await client.GetFromJsonAsync<JsonElement>($"{Sketch}/shapes/{id}");

    [Test]
    public async Task The_stack_reads_back_with_the_shapes_and_groups_drawn_on_it()
    {
        using var client = await SketchBoard.FreshAsync();

        var layers = await client.GetFromJsonAsync<JsonElement>($"{Sketch}/layers");
        await Assert.That(layers.GetArrayLength()).IsEqualTo(1);
        var ground = layers[0];
        await Assert.That(ground.GetProperty("id").GetString()).IsEqualTo("layer0");
        await Assert.That(ground.GetProperty("layout").GetProperty("shapes").GetArrayLength()).IsEqualTo(1);

        var one = await client.GetFromJsonAsync<JsonElement>($"{Sketch}/layers/layer0");
        await Assert.That(one.GetProperty("base_y").GetDouble()).IsEqualTo(0);

        var shapes = await client.GetFromJsonAsync<JsonElement>($"{Sketch}/layers/layer0/shapes");
        await Assert.That(shapes.GetArrayLength()).IsEqualTo(1);

        await Assert.That((await client.GetAsync($"{Sketch}/layers/nobody")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_layer_is_raised_added_and_taken_off_without_restating_the_board()
    {
        using var client = await SketchBoard.FreshAsync();

        var raised = await client.PutAsync($"{Sketch}/layers/layer0", Body("""{"base_y":4,"name":"Ground"}"""));
        await Assert.That(raised.IsSuccessStatusCode).IsTrue().Because(await raised.Content.ReadAsStringAsync());

        var ground = await client.GetFromJsonAsync<JsonElement>($"{Sketch}/layers/layer0");
        await Assert.That(ground.GetProperty("base_y").GetDouble()).IsEqualTo(4);
        await Assert.That(ground.GetProperty("layout").GetProperty("shapes").GetArrayLength()).IsEqualTo(1);

        var made = await client.PutAsync($"{Sketch}/layers/lid", Body("""{"base_y":20,"kind":"made"}"""));
        await Assert.That(made.IsSuccessStatusCode).IsTrue();
        await Assert.That((await client.GetFromJsonAsync<JsonElement>($"{Sketch}/layers")).GetArrayLength())
            .IsEqualTo(2);

        await Assert.That((await client.DeleteAsync($"{Sketch}/layers/lid")).StatusCode)
            .IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await client.DeleteAsync($"{Sketch}/layers/lid")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_shape_is_drawn_into_a_group_edited_and_rubbed_out()
    {
        using var client = await SketchBoard.FreshAsync();

        var drawn = await client.PostAsync($"{Sketch}/layers/layer0/shapes?group=i",
            Body("""{"type":"circle","center_x":30,"center_z":0,"radius":8,"floor":8,"base_height":10}"""));
        await Assert.That(drawn.IsSuccessStatusCode).IsTrue().Because(await drawn.Content.ReadAsStringAsync());
        var id = (await drawn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        await Assert.That(id).IsEqualTo("circle-1");

        var groups = await client.GetFromJsonAsync<JsonElement>($"{Sketch}/groups");
        var island = groups.EnumerateArray().Single(group => group.GetProperty("id").GetString() == "i");
        await Assert.That(island.GetProperty("layer").GetString()).IsEqualTo("layer0");
        await Assert.That(island.GetProperty("shapeIds").GetArrayLength()).IsEqualTo(2);
        await Assert.That(island.GetProperty("hasRelief").GetBoolean()).IsFalse();

        var moved = await client.PatchAsync($"{Sketch}/shapes/{id}", Body("""{"radius":12}"""));
        await Assert.That(moved.IsSuccessStatusCode).IsTrue();
        var shape = await ShapeAsync(client, id);
        await Assert.That(shape.GetProperty("radius").GetDouble()).IsEqualTo(12);
        await Assert.That(shape.GetProperty("center_x").GetDouble()).IsEqualTo(30);

        await Assert.That((await client.DeleteAsync($"{Sketch}/shapes/{id}")).StatusCode)
            .IsEqualTo(HttpStatusCode.OK);
        var after = await client.GetFromJsonAsync<JsonElement>($"{Sketch}/groups");
        await Assert.That(after.EnumerateArray().Single().GetProperty("shapeIds").GetArrayLength()).IsEqualTo(1);
        await Assert.That((await client.DeleteAsync($"{Sketch}/shapes/{id}")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
    }

    /// <summary>A shape naming no group on a layer that groups its shapes is refused rather than stored and
    /// complained about — the complaint (`SK17`) only ever arrives after the board is saved.</summary>
    [Test]
    public async Task A_shape_naming_no_group_on_a_grouped_layer_is_refused()
    {
        using var client = await SketchBoard.FreshAsync();

        var drawn = await client.PostAsync($"{Sketch}/layers/layer0/shapes",
            Body("""{"type":"rectangle","min_x":30,"max_x":40,"min_z":0,"max_z":10}"""));
        await Assert.That(drawn.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var refusal = await drawn.Content.ReadFromJsonAsync<JsonElement>();
        var finding = refusal.GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ1");
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("group");

        var shapes = await client.GetFromJsonAsync<JsonElement>($"{Sketch}/layers/layer0/shapes");
        await Assert.That(shapes.GetArrayLength()).IsEqualTo(1);
    }

    [Test]
    [Arguments("role")]
    [Arguments("intentRef")]
    [Arguments("height_authored")]
    public async Task A_shape_patch_refuses_the_fields_the_compiler_owns(string field)
    {
        using var client = await SketchBoard.FreshAsync();

        var value = field == "height_authored" ? "true" : "\"wool\"";
        var patched = await client.PatchAsync($"{Sketch}/shapes/s1", Body($$"""{"{{field}}":{{value}}}"""));
        await Assert.That(patched.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var refusal = await patched.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(refusal.GetProperty("findings")[0].GetProperty("field").GetString()).IsEqualTo(field);
    }

    /// <summary>A stated null takes the field off, which is the half of a patch a replace cannot do.</summary>
    [Test]
    public async Task A_stated_null_clears_the_field_it_names()
    {
        using var client = await SketchBoard.FreshAsync();

        await client.PatchAsync($"{Sketch}/shapes/s1", Body("""{"relief_scope":"exclude"}"""));
        await Assert.That((await ShapeAsync(client, "s1")).GetProperty("relief_scope").GetString())
            .IsEqualTo("exclude");

        await client.PatchAsync($"{Sketch}/shapes/s1", Body("""{"relief_scope":null}"""));
        var cleared = await ShapeAsync(client, "s1");
        await Assert.That(cleared.TryGetProperty("relief_scope", out var scope) && scope.ValueKind != JsonValueKind.Null)
            .IsFalse();
    }

    /// <summary>A group is stated and ungrouped without its shapes moving, and the relief keyed over it goes
    /// with the group rather than being left naming nothing.</summary>
    [Test]
    public async Task A_group_is_stated_and_ungrouped_and_its_relief_goes_with_it()
    {
        using var client = await SketchBoard.FreshAsync();

        var put = await client.PutAsync($"{Sketch}/layers/layer0/groups/east",
            Body("""{"name":"East","mirrors":false,"shapeIds":[]}"""));
        await Assert.That(put.IsSuccessStatusCode).IsTrue().Because(await put.Content.ReadAsStringAsync());

        var relief = await client.PutAsJsonAsync($"{Sketch}/relief/east", new
        {
            @base = 6.0, reach = 40, step = 1,
            marks = new[] { new { kind = "point", at = new[] { 0.0, 0.0 }, h = 14.0, r = 6.0 } },
        });
        await Assert.That(relief.IsSuccessStatusCode).IsTrue();

        var groups = await client.GetFromJsonAsync<JsonElement>($"{Sketch}/groups");
        var east = groups.EnumerateArray().Single(group => group.GetProperty("id").GetString() == "east");
        await Assert.That(east.GetProperty("mirrors").GetBoolean()).IsFalse();
        await Assert.That(east.GetProperty("hasRelief").GetBoolean()).IsTrue();

        await Assert.That((await client.DeleteAsync($"{Sketch}/layers/layer0/groups/east")).StatusCode)
            .IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await client.GetAsync($"{Sketch}/relief/east")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That((await client.GetFromJsonAsync<JsonElement>($"{Sketch}/layers/layer0/shapes"))
            .GetArrayLength()).IsEqualTo(1);
    }

    /// <summary>A stack whose list order and base_y disagree is stored and complained about (`SK20`), not
    /// refused: the world is built from base_y and comes out exactly as stated.</summary>
    [Test]
    public async Task A_layer_raised_above_the_one_after_it_complains_rather_than_refusing()
    {
        using var client = await SketchBoard.FreshAsync();

        await client.PutAsync($"{Sketch}/layers/terrace", Body("""{"base_y":20}"""));
        var span = await client.PutAsync($"{Sketch}/layers/span", Body("""{"base_y":14}"""));

        await Assert.That(span.IsSuccessStatusCode).IsTrue();
        await Assert.That(span.Headers.TryGetValues("Pgm-Warnings", out var warnings)).IsTrue();
        await Assert.That(string.Join(" ", warnings!)).Contains("SK20");
    }

    // ── the bend: a compiled outline redrawn as a coast ──────────────────────────────

    /// <summary>A polygon on the board's own layer, so the bend has an outline to resample.</summary>
    private static async Task<HttpClient> RingAsync()
    {
        var client = await SketchBoard.FreshAsync();
        var drawn = await client.PostAsync($"{Sketch}/layers/layer0/shapes?group=i", Body("""
            {"id":"coast","type":"polygon","operation":"add","base_height":12,
             "vertices":[[0,0],[100,0],[100,100],[0,100]]}
            """));
        drawn.EnsureSuccessStatusCode();
        return client;
    }

    private static double Area(JsonElement vertices)
    {
        double twice = 0;
        var ring = vertices.EnumerateArray().Select(v => (v[0].GetDouble(), v[1].GetDouble())).ToList();
        for (var i = 0; i < ring.Count; i++)
        {
            var next = ring[(i + 1) % ring.Count];
            twice += ring[i].Item1 * next.Item2 - next.Item1 * ring[i].Item2;
        }
        return Math.Abs(twice) / 2;
    }

    /// <summary>The two rules a bend is safe under, over the wire: the outline's own vertices are all still
    /// there, and the ground only ever got smaller.</summary>
    [Test]
    public async Task A_bend_keeps_every_vertex_and_only_ever_takes_land_away()
    {
        using var client = await RingAsync();
        var before = await ShapeAsync(client, "coast");

        var bent = await client.PostAsync($"{Sketch}/shapes/coast/bend",
            Body("""{"wander":3,"step":10,"seed":5}"""));
        await Assert.That(bent.IsSuccessStatusCode).IsTrue().Because(await bent.Content.ReadAsStringAsync());
        var drew = await bent.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(drew.GetProperty("held").GetInt32()).IsEqualTo(0);
        await Assert.That(drew.GetProperty("vertices").GetInt32()).IsGreaterThan(4);

        var after = await ShapeAsync(client, "coast");
        await Assert.That(Area(after.GetProperty("vertices"))).IsLessThan(Area(before.GetProperty("vertices")));
        await Assert.That(after.GetProperty("controls").EnumerateObject().Any()).IsTrue();

        foreach (var corner in new[] { (0.0, 0.0), (100.0, 0.0), (100.0, 100.0), (0.0, 100.0) })
            await Assert.That(after.GetProperty("vertices").EnumerateArray()
                .Any(v => v[0].GetDouble() == corner.Item1 && v[1].GetDouble() == corner.Item2))
                .IsTrue().Because($"({corner.Item1}, {corner.Item2}) is the outline's own and may not move");
    }

    /// <summary>A bend of nought is not a bend: both numbers are asked for, and stating neither is refused
    /// rather than answered with the outline unchanged.</summary>
    [Test]
    public async Task A_wander_of_nought_is_refused()
    {
        using var client = await RingAsync();
        var refused = await client.PostAsync($"{Sketch}/shapes/coast/bend", Body("""{"wander":0,"step":10}"""));

        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var finding = (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ1");
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("wander");
    }

    /// <summary>A rectangle states its bounds rather than an outline, so there is nothing to resample.</summary>
    [Test]
    public async Task A_shape_with_no_outline_is_refused()
    {
        using var client = await RingAsync();
        var refused = await client.PostAsync($"{Sketch}/shapes/s1/bend", Body("""{"wander":3,"step":10}"""));
        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var finding = (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("vertices");
    }

    /// <summary>A wander that folds the outline across its own far side is refused, not clamped — and the
    /// shape is left exactly as it was.</summary>
    [Test]
    public async Task A_fold_is_refused_and_the_outline_is_untouched()
    {
        using var client = await RingAsync();
        var before = await ShapeAsync(client, "coast");

        var refused = await client.PostAsync($"{Sketch}/shapes/coast/bend",
            Body("""{"wander":90,"step":6,"seed":2}"""));
        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That((await refused.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("findings")[0].GetProperty("field").GetString()).IsEqualTo("wander");

        var after = await ShapeAsync(client, "coast");
        await Assert.That(after.GetProperty("vertices").GetArrayLength())
            .IsEqualTo(before.GetProperty("vertices").GetArrayLength());
    }

    [Test]
    public async Task Bending_an_id_that_names_nothing_is_a_404()
    {
        using var client = await RingAsync();
        var missing = await client.PostAsync($"{Sketch}/shapes/nobody/bend", Body("""{"wander":3,"step":10}"""));
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
