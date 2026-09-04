using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using PgmStudio.Pgm.Sketch;

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

    // ── what a write reads, and what it leaves for a read ────────────────────────────

    /// <summary>A write takes the sketch gate's <b>document</b> reading. The seven rules read off the
    /// rasterized spans walk every column of the board's extent, so answering them on every moved vertex
    /// costs a whole rasterize per edit; the write names them instead and a read asks for them.
    ///
    /// <para>The two headers are deliberately not one. <c>Pgm-Warnings</c> means <em>these were found</em>
    /// and its absence means <em>nothing was</em>, which is what makes it readable at all — so a rule that
    /// was never asked rides on its own key.</para></summary>
    [Test]
    public async Task A_write_says_which_rules_it_did_not_walk_the_ground_for()
    {
        using var client = await RingAsync();

        var moved = await client.PatchAsync($"{Sketch}/shapes/coast/vertices/0", Body("""{"x":-4,"z":-4}"""));
        moved.EnsureSuccessStatusCode();

        await Assert.That(moved.Headers.TryGetValues("Pgm-Unwalked", out var unwalked)).IsTrue()
            .Because("a write that did not walk the ground says so");
        var named = string.Join(" ", unwalked!).Split(' ');
        await Assert.That(named.Order(StringComparer.Ordinal))
            .IsEquivalentTo(SketchLayoutCheck.GroundRules.Order(StringComparer.Ordinal));
    }

    /// <summary>And the rules a write leaves out are answered where it says they are. The stack below is
    /// `SK9` — a roof drawn over a floor on one layer — which no write reports and `GET …/findings`
    /// does.</summary>
    [Test]
    public async Task The_rules_a_write_leaves_out_are_answered_by_the_findings_read()
    {
        using var client = await SketchBoard.FreshAsync();
        foreach (var shape in new[]
                 {
                     """{"id":"floor","type":"rectangle","operation":"add","min_x":-20,"max_x":20,"min_z":-8,"max_z":8,"floor":0,"base_height":4}""",
                     """{"id":"roof","type":"rectangle","operation":"add","min_x":-20,"max_x":20,"min_z":-8,"max_z":8,"floor":16,"base_height":6}""",
                 })
        {
            var drawn = await client.PostAsync($"{Sketch}/layers/layer0/shapes?group=i", Body(shape));
            drawn.EnsureSuccessStatusCode();
            await Assert.That(drawn.Headers.TryGetValues("Pgm-Warnings", out var warned)
                              && string.Join(" ", warned!).Contains("SK9")).IsFalse()
                .Because("a write does not walk the ground, so it cannot have found SK9");
        }

        var findings = await client.GetFromJsonAsync<JsonElement>($"/api/map/{SketchBoard.Slug}/findings");
        var rules = findings.GetProperty("findings").EnumerateArray()
            .Select(finding => finding.GetProperty("rule").GetString()).ToList();
        await Assert.That(rules).Contains("SK9");
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

    /// <summary>The rule a bend is safe under, over the wire — the outline's own vertices are all still
    /// there — and the side a caller gets without asking, which is the bloat that reads as land.</summary>
    [Test]
    public async Task A_bend_keeps_every_vertex_and_bloats_the_outline_by_default()
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
        await Assert.That(Area(after.GetProperty("vertices")))
            .IsGreaterThan(Area(before.GetProperty("vertices")));
        await Assert.That(after.GetProperty("controls").EnumerateObject().Any()).IsTrue();

        foreach (var corner in new[] { (0.0, 0.0), (100.0, 0.0), (100.0, 100.0), (0.0, 100.0) })
            await Assert.That(after.GetProperty("vertices").EnumerateArray()
                .Any(v => v[0].GetDouble() == corner.Item1 && v[1].GetDouble() == corner.Item2))
                .IsTrue().Because($"({corner.Item1}, {corner.Item2}) is the outline's own and may not move");
    }

    /// <summary>The side is the caller's, and asking for the other one takes back exactly what the first
    /// gave: the reach at a point is the wander's and only its sign is the side's.</summary>
    [Test]
    public async Task The_side_a_bend_states_is_the_side_it_takes()
    {
        using var client = await RingAsync();
        var plan = Area((await ShapeAsync(client, "coast")).GetProperty("vertices"));

        (await client.PostAsync($"{Sketch}/shapes/coast/bend",
            Body("""{"wander":3,"step":10,"seed":5,"side":"in"}"""))).EnsureSuccessStatusCode();
        var inward = Area((await ShapeAsync(client, "coast")).GetProperty("vertices"));

        (await client.PatchAsync($"{Sketch}/shapes/coast",
            Body("""{"vertices":[[0,0],[100,0],[100,100],[0,100]],"controls":null}"""))).EnsureSuccessStatusCode();
        (await client.PostAsync($"{Sketch}/shapes/coast/bend",
            Body("""{"wander":3,"step":10,"seed":5,"side":"out"}"""))).EnsureSuccessStatusCode();
        var outward = Area((await ShapeAsync(client, "coast")).GetProperty("vertices"));

        await Assert.That(inward).IsLessThan(plan);
        await Assert.That(outward).IsGreaterThan(plan);
        await Assert.That(outward - plan).IsEqualTo(plan - inward).Within(0.5);
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
    /// shape is left exactly as it was. A convex ring folds on the inward side, where the two walls of the
    /// square move toward each other; outward from one there is always room.</summary>
    [Test]
    public async Task A_fold_is_refused_and_the_outline_is_untouched()
    {
        using var client = await RingAsync();
        var before = await ShapeAsync(client, "coast");

        var refused = await client.PostAsync($"{Sketch}/shapes/coast/bend",
            Body("""{"wander":90,"step":6,"seed":2,"side":"in"}"""));
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

    // ── the outline, one point at a time ─────────────────────────────────────────────

    private static (double X, double Z)[] Ring(JsonElement shape) =>
        [.. shape.GetProperty("vertices").EnumerateArray().Select(v => (v[0].GetDouble(), v[1].GetDouble()))];

    /// <summary>What a hand does in the browser and what a whole-ring transform cannot: one corner is pulled
    /// and every other point is exactly where it was drawn. A board's shapes abut, so the points that did not
    /// move are the whole of what keeps two of them flush.</summary>
    [Test]
    public async Task Moving_one_vertex_leaves_every_other_where_it_was()
    {
        using var client = await RingAsync();
        var before = Ring(await ShapeAsync(client, "coast"));

        var moved = await client.PatchAsync($"{Sketch}/shapes/coast/vertices/0",
            Body("""{"x":-25,"z":-15}"""));
        await Assert.That(moved.IsSuccessStatusCode).IsTrue().Because(await moved.Content.ReadAsStringAsync());
        var wrote = await moved.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(wrote.GetProperty("index").GetInt32()).IsEqualTo(0);
        await Assert.That(wrote.GetProperty("vertices").GetInt32()).IsEqualTo(4);

        var after = Ring(await ShapeAsync(client, "coast"));
        await Assert.That(after[0]).IsEqualTo((-25.0, -15.0));
        await Assert.That(after[1..]).IsEquivalentTo(before[1..]);
    }

    /// <summary>Adding a point with no <c>x</c>/<c>z</c> puts it at the midpoint of the edge it follows —
    /// the anchor a hand reaches for when it wants a new corner half way along a wall — and the answer says
    /// where it landed, which is the address the move route then takes.</summary>
    [Test]
    public async Task A_vertex_added_with_no_point_lands_on_the_midpoint_of_its_edge()
    {
        using var client = await RingAsync();

        var added = await client.PostAsync($"{Sketch}/shapes/coast/vertices", Body("""{"after":1}"""));
        await Assert.That(added.IsSuccessStatusCode).IsTrue().Because(await added.Content.ReadAsStringAsync());
        var wrote = await added.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(wrote.GetProperty("index").GetInt32()).IsEqualTo(2);
        await Assert.That(wrote.GetProperty("vertices").GetInt32()).IsEqualTo(5);

        var after = Ring(await ShapeAsync(client, "coast"));
        await Assert.That(after[2]).IsEqualTo((100.0, 50.0));

        var pulled = await client.PatchAsync($"{Sketch}/shapes/coast/vertices/2", Body("""{"x":140,"z":50}"""));
        pulled.EnsureSuccessStatusCode();
        await Assert.That(Ring(await ShapeAsync(client, "coast"))[2]).IsEqualTo((140.0, 50.0));
    }

    /// <summary>A point taken out leaves the outline shorter and every other point where it was, and the ring
    /// is refused below three since two points draw no ground.</summary>
    [Test]
    public async Task A_vertex_is_taken_out_and_the_last_three_are_kept()
    {
        using var client = await RingAsync();

        var dropped = await client.DeleteAsync($"{Sketch}/shapes/coast/vertices/1");
        dropped.EnsureSuccessStatusCode();
        var after = Ring(await ShapeAsync(client, "coast"));
        await Assert.That(after.Length).IsEqualTo(3);
        await Assert.That(after).IsEquivalentTo([(0.0, 0.0), (100.0, 100.0), (0.0, 100.0)]);

        var refused = await client.DeleteAsync($"{Sketch}/shapes/coast/vertices/1");
        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That((await refused.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("findings")[0].GetProperty("field").GetString()).IsEqualTo("index");
    }

    /// <summary>An index the outline does not carry is refused with the range it does carry, since a caller
    /// reading a stale copy of the shape needs to know how long it is now.</summary>
    [Test]
    public async Task An_index_the_outline_does_not_carry_is_refused_with_its_range()
    {
        using var client = await RingAsync();
        var refused = await client.PatchAsync($"{Sketch}/shapes/coast/vertices/9", Body("""{"x":0,"z":0}"""));

        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var finding = (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("index");
        await Assert.That(finding.GetProperty("message").GetString()).Contains("numbered 0 to 3");
    }

    /// <summary>A move that folds the outline across its own far side is refused, not clamped, and the shape
    /// is left exactly as it was.</summary>
    [Test]
    public async Task A_move_that_folds_the_outline_is_refused()
    {
        using var client = await RingAsync();
        var before = Ring(await ShapeAsync(client, "coast"));

        var refused = await client.PatchAsync($"{Sketch}/shapes/coast/vertices/0",
            Body("""{"x":150,"z":50}"""));
        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        await Assert.That(Ring(await ShapeAsync(client, "coast"))).IsEquivalentTo(before);
    }

    /// <summary>The plan compiles a room's rectangle and a recompile redraws it, so its points are the
    /// compiler's rather than a caller's.</summary>
    [Test]
    public async Task A_role_shapes_vertices_are_the_compilers()
    {
        using var client = await RingAsync();
        (await client.PatchAsync($"{Sketch}/shapes/coast", Body("""{"height_mode":"flat"}"""))).EnsureSuccessStatusCode();

        var drawn = await client.PostAsync($"{Sketch}/layers/layer0/shapes?group=i", Body("""
            {"id":"hall","type":"polygon","role":"room","vertices":[[0,0],[10,0],[10,10],[0,10]]}
            """));
        drawn.EnsureSuccessStatusCode();

        var refused = await client.PatchAsync($"{Sketch}/shapes/hall/vertices/0", Body("""{"x":1,"z":1}"""));
        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That((await refused.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("findings")[0].GetProperty("field").GetString()).IsEqualTo("role");
    }

    /// <summary>A vertex edit on an id that names nothing is a 404, the same as every other shape route.</summary>
    [Test]
    public async Task A_vertex_edit_on_an_id_that_names_nothing_is_a_404()
    {
        using var client = await RingAsync();
        var missing = await client.PatchAsync($"{Sketch}/shapes/nobody/vertices/0", Body("""{"x":0,"z":0}"""));
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
