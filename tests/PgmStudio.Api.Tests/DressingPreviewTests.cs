using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PgmStudio.Api.Services;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The dressing stage's preview half (G161): what a prop looks like, asserted by what it <em>placed</em>
/// rather than by the bytes it drew. <see cref="PgmStudio.Export.Tests.DressingScopeTests"/> is the sibling
/// for what an author placed, what the pass must leave bare, and how the map is mirrored.
/// </summary>
[NotInParallel("api-db")]
public sealed class DressingPreviewTests
{
    private static HashSet<string> Fills(string svg)
        => Regex.Matches(svg, "fill='(#[0-9a-f]{6})'").Select(m => m.Groups[1].Value).ToHashSet();

    private static int Height(string svg)
        => int.Parse(Regex.Match(svg, "height='(\\d+)'").Groups[1].Value);

    // A theme whose surface is grass — what flora needs underfoot to place at all, since the built-in default
    // no longer paints anything organic.
    private static readonly TerrainTheme Grassed = TerrainTheme.Default with
    {
        Surface = new TopBand(new SolidMaterial(Blocks.Grass), 1),
    };

    [Test]
    public async Task The_preview_places_the_prop_rather_than_drawing_an_impression_of_it()
    {
        var views = DressingPreview.Views(
            new FloraProp { Points = [[0, 0], [40, 0], [40, 40], [0, 40]], Spec = new FloraSpec(Coverage: 0.9, FlowerShare: 0.5), Seed = 7 },
            Grassed);

        await Assert.That(views.Counts.Plants).IsGreaterThan(100);
        await Assert.That(views.Counts.Trees).IsEqualTo(0);
        // Flowers are the point of a flower field, so the picture has to carry their colours and not one
        // generic plant green.
        await Assert.That(Fills(views.Plan)).Contains(BlockPalette.Hex(DressingPalette.RedFlower, 0));
        await Assert.That(Fills(views.Plan)).Contains(BlockPalette.Hex(DressingPalette.YellowFlower, 0));
    }

    [Test]
    public async Task A_preview_can_be_asked_for_as_a_png_of_either_view()
    {
        // format=png answers one named view as image/png bytes — the form an agent saves and looks at —
        // while the JSON-of-SVGs default stays what the client renders inline. The bytes are checked by the
        // PNG signature, which is what says an image and not an error envelope came back.
        using var client = ApiTestFactory.Shared.CreateClient();
        const string prop = """
        {"propJson": "{\"kind\":\"tree\",\"id\":\"t\",\"seed\":5,\"x\":0,\"z\":0,\"species\":\"oak\",\"height\":14}"}
        """;

        foreach (var view in (string[])["plan", "section"])
        {
            var resp = await client.PostAsync($"/api/terrain/prop-preview?format=png&view={view}",
                new StringContent(prop, System.Text.Encoding.UTF8, "application/json"));
            await Assert.That(resp.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
            await Assert.That(resp.Content.Headers.ContentType!.MediaType).IsEqualTo("image/png");
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            await Assert.That(bytes.Take(4)).IsEquivalentTo(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' });
        }

        // a view the endpoint does not have is a 400, not a guess
        var bad = await client.PostAsync("/api/terrain/prop-preview?format=png&view=sideways",
            new StringContent(prop, System.Text.Encoding.UTF8, "application/json"));
        await Assert.That(bad.StatusCode).IsEqualTo(System.Net.HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task A_picture_can_be_asked_for_larger_and_a_bad_scale_costs_only_the_size()
    {
        // The same view at more pixels, so what a preview chose to show is what a caller gets more of — a
        // house section is 72x108 unasked, which is not a picture a roof idiom can be read off. A scale is
        // how the answer is looked at rather than part of the question, so one outside the range clamps and
        // one that is not a number falls back: a bad scale costs a bigger picture, never the picture.
        using var client = ApiTestFactory.Shared.CreateClient();
        async Task<(int Width, int Height)> SizeAsync(string query)
        {
            var resp = await client.PostAsync($"/api/terrain/material-preview?format=png&view=plan{query}",
                new StringContent("""{"kind":"solid","id":1,"data":0}""",
                    System.Text.Encoding.UTF8, "application/json"));
            await Assert.That(resp.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
            var png = await resp.Content.ReadAsByteArrayAsync();
            // IHDR's width and height, big-endian, at the fixed offset every PNG puts them
            return (System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)),
                    System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        }

        var (width, height) = await SizeAsync("");
        await Assert.That(await SizeAsync("&scale=4")).IsEqualTo((width * 4, height * 4));
        // past the ceiling, and not a number at all
        await Assert.That(await SizeAsync("&scale=99")).IsEqualTo((width * 8, height * 8));
        await Assert.That(await SizeAsync("&scale=nope")).IsEqualTo((width, height));
    }

    [Test]
    public async Task A_view_the_preview_does_not_draw_is_refused_by_naming_the_ones_it_does()
    {
        // The names in the sentence are the array the schema publishes as the view enum, so a caller reading
        // the document and a caller reading a refusal are told the same set.
        using var client = ApiTestFactory.Shared.CreateClient();
        var resp = await client.PostAsync("/api/terrain/theme-preview?format=png&view=elevation",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        await Assert.That(resp.StatusCode).IsEqualTo(System.Net.HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        foreach (var view in PgmStudio.Api.Services.StylePreview.ThemePngViews)
            await Assert.That(body).Contains(view);
    }

    [Test]
    public async Task A_material_preview_can_be_asked_for_as_a_png()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var resp = await client.PostAsync("/api/terrain/material-preview?format=png&view=section",
            new StringContent("""{"kind":"solid","id":1,"data":0}""", System.Text.Encoding.UTF8, "application/json"));

        await Assert.That(resp.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
        await Assert.That(resp.Content.Headers.ContentType!.MediaType).IsEqualTo("image/png");
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        await Assert.That(bytes.Take(4)).IsEquivalentTo(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' });
    }

    [Test]
    public async Task A_house_the_build_would_refuse_is_refused_by_the_preview_with_the_same_finding()
    {
        // Two wings sharing a row are HJ1 — the dressing pass drops that prop silently, so the preview
        // certifying it as a drawing would be worse than no preview at all. Both wings clear the least span
        // any footprint takes, so the joint is the only thing left to refuse them for.
        using var client = ApiTestFactory.Shared.CreateClient();
        var prop = """
        {"propJson": "{\"kind\":\"house\",\"id\":\"h\",\"seed\":1,\"wings\":[{\"corners\":[[0,0],[7,7]]},{\"corners\":[[0,7],[3,10]]}],\"style\":{}}"}
        """;
        var resp = await client.PostAsync("/api/terrain/prop-preview",
            new StringContent(prop, System.Text.Encoding.UTF8, "application/json"));

        await Assert.That((int)resp.StatusCode).IsEqualTo(400);
        var body = await resp.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("HJ1");
    }

    [Test]
    public async Task A_prop_is_re_centred_on_the_sample_so_a_card_shows_it_wherever_it_was_placed()
    {
        // A tree placed at (900, -400) on a real map must still be the thing in the middle of its own card.
        var far = DressingPreview.Views(new TreeProp { X = 900, Z = -400, Seed = 5, Style = new TreeStyle { Species = "oak" } }, TerrainTheme.Default);
        await Assert.That(far.Counts.Trees).IsEqualTo(1);
    }

    [Test]
    public async Task The_theme_underneath_is_what_decides_whether_anything_grows()
    {
        var meadow = new FloraProp { Points = [[0, 0], [40, 0], [40, 40], [0, 40]], Spec = new FloraSpec(Coverage: 0.8), Seed = 7 };
        var paved = TerrainTheme.Default with { Surface = new TopBand(new SolidMaterial(Blocks.QuartzBlock), 1) };

        await Assert.That(DressingPreview.Views(meadow, Grassed).Counts.Plants).IsGreaterThan(0);
        await Assert.That(DressingPreview.Views(meadow, paved).Counts.Plants).IsEqualTo(0);
    }

    [Test]
    public async Task The_section_crops_to_what_is_there_so_a_path_and_a_tree_read_at_the_same_scale()
    {
        // A fixed sky would draw a path as one grey line under forty courses of nothing.
        var path = DressingPreview.Views(new StrokeProp
        {
            Points = [[0, 20], [40, 20]], Radius = 3, Seed = 5, Pave = new SolidMaterial(Blocks.Gravel),
        }, TerrainTheme.Default);
        var tree = DressingPreview.Views(new TreeProp { Seed = 5, Style = new TreeStyle { Species = "spruce", Height = 24 } }, TerrainTheme.Default);

        await Assert.That(path.Counts.PathCells).IsGreaterThan(50);
        await Assert.That(Height(tree.Section)).IsGreaterThan(Height(path.Section));
    }

    [Test]
    public async Task Every_card_in_a_set_is_cropped_the_same_way_so_their_floors_line_up()
    {
        // Cropping each card to its own content puts every floor at a different height once they sit in a
        // grid, and makes a spruce and an acacia look the same size. One crop per set fixes both: the tallest
        // option decides the top, the ground decides the bottom, and the heights compare honestly.
        var species = DressingPreview.SpeciesCards(TerrainTheme.Default);
        var forms = DressingPreview.BoulderFormCards(new BoulderProp { Seed = 3, Style = new BoulderStyle { Size = 3 } }, TerrainTheme.Default);

        await Assert.That(species.Select(card => Height(card.Svg)).Distinct().Count()).IsEqualTo(1);
        await Assert.That(forms.Select(card => Height(card.Svg)).Distinct().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task The_side_view_is_a_projection_so_a_crown_reads_whole_rather_than_speckled()
    {
        // A cut through one row meets a crown wherever that row falls — as often through the air between leaf
        // clusters as through them. Projecting every row is what makes the silhouette the shape it is.
        var tree = DressingPreview.Views(new TreeProp { Seed = 5, Style = new TreeStyle { Species = "oak", Height = 20 } }, TerrainTheme.Default);

        // Depth shades a block without repainting it, so a projected crown is many shades of the one leaf
        // colour — where a cut would give one flat shade with holes through it.
        //
        // A crown fill is recognised by asking the palette to produce it: a shade the view can emit is
        // Hex(leaf, depth) for some depth, so reproducing the fill from the shade function IS the membership
        // test. Guessing instead — "green-dominant, same channels at zero" — reads the ground as crown the
        // moment grass and oak leaves are near hues, which is what a colour table sharpened against the real
        // textures made them.
        var front = BlockPalette.Hex(Blocks.Leaves, DressingPalette.LeafNoDecay, 0);
        var leafShades = Enumerable.Range(0, 1001)
            .Select(step => BlockPalette.Hex(Blocks.Leaves, DressingPalette.LeafNoDecay, step / 1000.0))
            .ToHashSet();

        var crown = Fills(tree.Section).Where(leafShades.Contains).ToList();
        await Assert.That(crown.Count).IsGreaterThan(3);
        // And every one of them is still that leaf, darkened: no shade is brighter than the block's own.
        await Assert.That(crown.All(hex => Green(hex) <= Green(front))).IsTrue();
        // The ground is not the crown: grass shares the leaf's hue but no shade of a leaf is ever that bright.
        await Assert.That(crown).DoesNotContain(BlockPalette.Hex(Blocks.Grass, 0));

        static int Green(string hex) => Convert.ToInt32(hex[3..5], 16);
    }

    [Test]
    public async Task Both_views_look_inside_the_sample_rather_than_at_the_wall_around_it()
    {
        // The painter finishes a footprint's perimeter as an edge — a rim course over a wall — so the sample's
        // outermost ring is its own boundary, not ground. Seen from the side that ring is the entire front
        // face, and a preview that included it showed a tree standing behind a wall instead of in the grass
        // the plan view plainly draws under it. Both views carry the same ground.
        var tree = DressingPreview.Views(new TreeProp { Seed = 5, Style = new TreeStyle { Species = "oak", Height = 20 } }, Grassed);

        var ground = BlockPalette.Hex(Blocks.Grass, 0);
        await Assert.That(Fills(tree.Plan)).Contains(ground);
        await Assert.That(Fills(tree.Section)).Contains(ground);
    }

    [Test]
    public async Task The_wood_cards_change_the_colour_and_nothing_else()
    {
        // A wood is a material, so six wood cards are one tree six times. If they differed in shape the picker
        // would be claiming the wood decides the silhouette, which is the claim the species picker makes and
        // this one must not.
        var woods = DressingPreview.WoodCards(new TreeProp { Seed = 5, Style = new TreeStyle { Form = TreeForm.Grown, Height = 18 } }, TerrainTheme.Default);

        await Assert.That(woods.Select(card => card.Key)).IsEquivalentTo(DressingPalette.Woods.Select(wood => wood.Name));
        await Assert.That(woods.Select(card => Cells(card.Svg)).Distinct().Count()).IsEqualTo(1);
        await Assert.That(woods.Select(card => card.Svg).Distinct().Count()).IsEqualTo(woods.Count);

        // How many blocks a card draws — the same tree in another wood fills the same cells.
        static int Cells(string svg) => Regex.Matches(svg, "<rect").Count;
    }

    [Test]
    public async Task A_species_is_a_silhouette_and_not_a_colour()
    {
        // The inverse claim, and the reason the two pickers are two pickers: the species cards must differ in
        // shape, or six of them are one tree wearing six palettes.
        var species = DressingPreview.SpeciesCards(TerrainTheme.Default);

        await Assert.That(species.Select(card => Regex.Matches(card.Svg, "<rect").Count).Distinct().Count())
            .IsGreaterThan(4);
    }

    [Test]
    public async Task Every_picker_offers_exactly_what_the_pass_can_build()
    {
        // The cards are the real algorithm at card size, so a picker can never promise a look the export does
        // not produce — which is only true if every option actually draws something.
        var styles = DressingPreview.StrokeStyleCards(
            new StrokeProp { Radius = 3, Seed = 5, Pave = new SolidMaterial(Blocks.Gravel) }, TerrainTheme.Default);
        var forms = DressingPreview.BoulderFormCards(new BoulderProp { Seed = 3, Style = new BoulderStyle { Size = 3 } }, TerrainTheme.Default);
        var species = DressingPreview.SpeciesCards(TerrainTheme.Default);

        await Assert.That(styles.Select(card => card.Key))
            .IsEquivalentTo(Enum.GetValues<StrokeStyle>().Select(style => style.ToString().ToLowerInvariant()));
        await Assert.That(forms.Count).IsEqualTo(4);
        await Assert.That(species.Select(card => card.Key)).IsEquivalentTo(DressingPalette.Species.Select(row => row.Name));

        foreach (var card in styles.Concat(forms).Concat(species))
        {
            await Assert.That(card.Svg).Contains("<rect");
            await Assert.That(card.Label).IsNotEmpty();
        }
    }

    // ── the claims raster (TS81) ───────────────────────────────────────────────────────────────────
    // A flat board wide enough to carry a stroke off to one side and a goal well clear of it, so the two
    // classes never overlap and each can be read in isolation.
    private const string ClaimsBoard = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layers":[{"id":"ground","base_y":0,"layout":{"shapes":[
            {"id":"a","type":"rectangle","operation":"add","min_x":-60,"max_x":60,"min_z":-60,"max_z":60,"base_height":10}],
          "groups":[{"id":"i1","name":"Ground","mirrors":false,"shapeIds":["a"]}]} }],
         "dressing":{"props":[
            {"kind":"stroke","id":"p1","points":[[-40,0],[40,0]],"radius":2,"seed":5}]}}
        """;

    private static async Task<string> MapAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync("/api/sketch", new { name = $"TS81 {Guid.NewGuid():N}" });
        return (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
    }

    private static Task<HttpResponseMessage> PostDressingAsync(HttpClient client, string slug, string query = "") =>
        client.PostAsync($"/api/map/{slug}/sketch/dressing{query}",
            new StringContent(ClaimsBoard, Encoding.UTF8, "application/json"));

    private static char At(JsonElement claims, int x, int z)
    {
        var bounds = claims.GetProperty("bounds");
        int minX = (int)bounds.GetProperty("min_x").GetDouble(), minZ = (int)bounds.GetProperty("min_z").GetDouble();
        return claims.GetProperty("rows")[z - minZ].GetString()![x - minX];
    }

    [Test]
    public async Task The_claims_rasters_rows_span_exactly_its_own_width_and_height()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await MapAsync(client);

        var resp = await PostDressingAsync(client, slug);
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(await resp.Content.ReadAsStringAsync());
        var claims = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("claims");

        var width = claims.GetProperty("width").GetInt32();
        var height = claims.GetProperty("height").GetInt32();
        var bounds = claims.GetProperty("bounds");
        // The bounds name edges, both inclusive, so the width is one more than their difference.
        await Assert.That(width).IsEqualTo((int)(bounds.GetProperty("max_x").GetDouble() - bounds.GetProperty("min_x").GetDouble()) + 1);
        await Assert.That(height).IsEqualTo((int)(bounds.GetProperty("max_z").GetDouble() - bounds.GetProperty("min_z").GetDouble()) + 1);

        var rows = claims.GetProperty("rows");
        await Assert.That(rows.GetArrayLength()).IsEqualTo(height);
        foreach (var row in rows.EnumerateArray()) await Assert.That(row.GetString()!.Length).IsEqualTo(width);
    }

    [Test]
    public async Task A_strokes_own_covered_cells_read_as_route_on_the_raster()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await MapAsync(client);

        var body = await (await PostDressingAsync(client, slug)).Content.ReadFromJsonAsync<JsonElement>();
        var claims = body.GetProperty("claims");
        var stroke = body.GetProperty("props").EnumerateArray()
            .First(prop => prop.GetProperty("kind").GetString() == "stroke");

        foreach (var cell in stroke.GetProperty("covered").EnumerateArray())
            await Assert.That(At(claims, cell.GetProperty("x").GetInt32(), cell.GetProperty("z").GetInt32()))
                .IsEqualTo('2');
    }

    [Test]
    public async Task A_goals_clearance_reads_around_its_anchor_on_the_raster()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await MapAsync(client);

        var intent = await client.PutAsync($"/api/map/{slug}/intent", new StringContent("""
            {"destroyables":[{"owner":"red","name":"reds monument","style":"pillar-1","materials":"obsidian",
                              "anchor":{"x":30,"y":8,"z":30}}]}
            """, Encoding.UTF8, "application/json"));
        await Assert.That(intent.IsSuccessStatusCode).IsTrue().Because(await intent.Content.ReadAsStringAsync());

        var body = await (await PostDressingAsync(client, slug)).Content.ReadFromJsonAsync<JsonElement>();
        var claims = body.GetProperty("claims");

        // Two blocks off the marker is inside the clearance however the structure resolved, since the
        // clearance reaches at least GoalClearance (4) past the smallest footprint and GoalStandoff (10)
        // past the marker either way.
        await Assert.That(At(claims, 32, 30)).IsEqualTo('9');
    }

    [Test]
    public async Task Format_text_answers_a_key_naming_every_character_the_rows_use()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await MapAsync(client);

        var resp = await PostDressingAsync(client, slug, "?format=text");
        await Assert.That(resp.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
        await Assert.That(resp.Content.Headers.ContentType!.MediaType).IsEqualTo("text/plain");

        var text = await resp.Content.ReadAsStringAsync();
        var lines = text.Split('\n');
        await Assert.That(lines[0]).Contains("1 char = 1 block");
        var key = lines[1];
        await Assert.That(key).StartsWith("KEY");

        // Each row opens with its z, right-aligned in four characters and a space (the shared grid
        // convention) — stripped off before reading the raster characters, so the sign of a negative z
        // is never mistaken for one of them.
        var used = lines.Skip(3).TakeWhile(line => !line.StartsWith("decline") && !line.StartsWith("placed"))
            .SelectMany(line => line.Length > 5 ? line[5..] : "").Distinct().Where(ch => ch != ' ');
        foreach (var digit in used) await Assert.That(key).Contains(digit);

        await Assert.That(text).Contains("placed 1, declined 0");
    }

    // ── the raster read forwards (WE34) ────────────────────────────────────────────────────────────────

    private static Task<HttpResponseMessage> PostSeatsAsync(HttpClient client, string slug, string query) =>
        client.PostAsync($"/api/map/{slug}/sketch/seats{query}",
            new StringContent(ClaimsBoard, Encoding.UTF8, "application/json"));

    [Test]
    public async Task The_seat_read_answers_where_a_prop_of_its_kind_and_footprint_may_stand()
    {
        // The board carries one stroke along z=0. A tree keeps three blocks off a route, so the pavement
        // and the two cells either side of it refuse and open ground away from it seats.
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await MapAsync(client);

        var resp = await PostSeatsAsync(client, slug, "?kind=tree");
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(await resp.Content.ReadAsStringAsync());
        var seats = await resp.Content.ReadFromJsonAsync<JsonElement>();

        await Assert.That(seats.GetProperty("kind").GetString()).IsEqualTo("tree");
        await Assert.That(seats.GetProperty("standoff").GetInt32()).IsEqualTo(3);
        await Assert.That(seats.GetProperty("seats").GetInt32()).IsGreaterThan(0);
        await Assert.That(At(seats, 0, 0)).IsEqualTo('0').Because("the stroke's own pavement is claimed");
        await Assert.That(At(seats, 0, 40)).IsEqualTo('1').Because("open ground well off the road seats");
        await Assert.That(seats.GetProperty("refused").EnumerateArray()
                .Any(because => because.GetProperty("rule").GetString() == "DR-ROAD")).IsTrue();
    }

    [Test]
    public async Task A_footprint_wider_than_the_gap_it_is_asked_about_does_not_seat_in_it()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await MapAsync(client);

        var one = await (await PostSeatsAsync(client, slug, "?kind=house&width=1")).Content
            .ReadFromJsonAsync<JsonElement>();
        var wide = await (await PostSeatsAsync(client, slug, "?kind=house&width=20")).Content
            .ReadFromJsonAsync<JsonElement>();

        await Assert.That(wide.GetProperty("footprintDepth").GetInt32()).IsEqualTo(20)
            .Because("one number asks about a square");
        await Assert.That(wide.GetProperty("seats").GetInt32())
            .IsLessThan(one.GetProperty("seats").GetInt32());
    }

    [Test]
    public async Task The_seat_read_answers_text_and_refuses_a_kind_no_document_names()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await MapAsync(client);

        var text = await PostSeatsAsync(client, slug, "?kind=boulder&format=text");
        await Assert.That(text.Content.Headers.ContentType!.MediaType).IsEqualTo("text/plain");
        var body = await text.Content.ReadAsStringAsync();
        await Assert.That(body).StartsWith("SEATS  boulder, footprint 1x1, 2 blocks off a route");
        await Assert.That(body).Contains("KEY  1 the footprint seats");

        var nonsense = await PostSeatsAsync(client, slug, "?kind=obelisk");
        await Assert.That((int)nonsense.StatusCode).IsEqualTo(422);
        await Assert.That(await nonsense.Content.ReadAsStringAsync()).Contains("boulder");
    }
}
