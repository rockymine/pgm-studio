using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using PgmStudio.Contracts;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The house-part library's HTTP surface (B71). The cases that matter are the ones the level exists for: a part
/// round-trips with its stacks, a house that binds one builds <em>that</em> part rather than its own columns, a
/// house that binds none is untouched, and a part a building still wears cannot be forgotten. Runs against
/// <c>pgm_studio_test</c>; each test resets the schema, so they run serially.
/// </summary>
[NotInParallel("api-db")]
public sealed class HousePartLibraryEndpointsTests
{
    private static RoomStyleSaveRequest House(
        string name,
        long? roofStyleId = null, long? porchStyleId = null,
        IReadOnlyList<RoomStoreyDto>? stack = null,
        params RoomCourseDto[] courses) => new(
        name, FloorDepth: 1, WallHeight: 7,
        RoofForms.Flat, Pitch: 1, Overhang: 0, RoofHole: true, RidgeCap: false,
        BorderWidth: 1, InlayInset: 2, Storeys: 1, StoreyClear: 0,
        Windows: new RoomWindowDto(WindowForms.None, Blocks.GlassPane, 0, 2, 2, 2, 3), Porch: null,
        Door: "stained-glass-pane", DoorHeight: 3,
        RoofStyleId: roofStyleId, PorchStyleId: porchStyleId, StoreyStack: stack ?? [], Courses: courses);

    private static RoofStyleSaveRequest Roof(string name, string form = RoofForms.Gable,
        params RoomCourseDto[] courses)
        => new(name, form, Pitch: 1, Overhang: 1, RoofHole: false, RidgeCap: false, Courses: courses);

    private static StoreyStyleSaveRequest Storey(string name, int clear = 3, params RoomCourseDto[] courses)
        => new(name, clear, BorderWidth: 1, InlayInset: 2,
            new RoomWindowDto(WindowForms.None, Blocks.GlassPane, 0, 2, 2, 2, 3), courses);

    private static async Task<long> StyleAsync(HttpClient client, string name, int blockId)
    {
        var saved = await (await client.PostAsJsonAsync("/api/styles", new StyleSaveRequest(
                name, MaterialKind.Solid, TerrainThemeJson.Serialize(new SolidMaterial(blockId)))))
            .Content.ReadFromJsonAsync<StyleDto>();
        return saved!.Id;
    }

    private static HashSet<string> Fills(string svg)
        => Regex.Matches(svg, "fill='(#[0-9a-f]{6})'").Select(m => m.Groups[1].Value).ToHashSet();

    private static int Height(string svg) => int.Parse(Regex.Match(svg, "height='(\\d+)'").Groups[1].Value);

    private static async Task<RoomStylePreviewDto> HousePreview(HttpClient client, RoomStyleSaveRequest draft)
        => (await (await client.PostAsJsonAsync("/api/room-styles/preview", draft))
            .Content.ReadFromJsonAsync<RoomStylePreviewDto>())!;

    // ── the parts themselves ───────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_roof_round_trips_with_its_courses_and_lists_with_a_picture()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slate = await StyleAsync(client, "slate", Blocks.Stone);
        var trim = await StyleAsync(client, "trim", Blocks.StainedClay);

        var created = await (await client.PostAsJsonAsync("/api/roof-styles", Roof("shingled", RoofForms.Gable,
                new RoomCourseDto(RoomParts.Roof, 0, slate, 1),
                new RoomCourseDto(RoomParts.Verge, 0, trim, 1))))
            .Content.ReadFromJsonAsync<RoofStyleDetail>();

        var detail = await client.GetFromJsonAsync<RoofStyleDetail>($"/api/roof-styles/{created!.Id}");
        await Assert.That(detail!.Form).IsEqualTo(RoofForms.Gable);
        await Assert.That(detail.Courses.Count).IsEqualTo(2);
        await Assert.That(detail.Courses.Single(c => c.Part == RoomParts.Verge).StyleId).IsEqualTo(trim);

        // Drawn on the sample building, so a roof card is a picture of a roof rather than of nothing.
        var listed = await client.GetFromJsonAsync<List<RoofStyleSummary>>("/api/roof-styles");
        await Assert.That(listed!.Count).IsEqualTo(1);
        await Assert.That(Fills(listed[0].Preview)).Contains(BlockPalette.Hex(Blocks.Stone, 0));
    }

    /// <summary>A roof part carries its own half-course slab, which is what lets the slab/pitch pairing run
    /// here: a slab named as a whole-block roof leaves an open half between every pair and the roof reads
    /// see-through, and a roof saved on its own is stamped exactly as one bound onto a house is.</summary>
    [Test]
    public async Task A_roof_states_its_own_slab_and_a_slab_roof_with_none_is_refused()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slabs = await StyleAsync(client, "slabs", Blocks.WoodenSlab);

        // A slab body with roofSlab unset (-1) is the see-through roof.
        var refused = await client.PostAsJsonAsync("/api/roof-styles",
            Roof("see-through", RoofForms.Gable, new RoomCourseDto(RoomParts.Roof, 0, slabs, 1)));
        await Assert.That((int)refused.StatusCode).IsEqualTo(400);
        var envelope = await refused.Content.ReadFromJsonAsync<RefusalDto>();
        await Assert.That(envelope!.Findings.Select(f => f.Rule)).Contains(HouseStyleRules.RoofMaterial);

        // The same roof with the slab named pairs correctly, and the number round-trips.
        var paired = Roof("shingled", RoofForms.Gable, new RoomCourseDto(RoomParts.Roof, 0, slabs, 1))
            with { RoofSlab = Blocks.WoodenSlab, RoofSlabData = 2 };
        var created = await (await client.PostAsJsonAsync("/api/roof-styles", paired))
            .Content.ReadFromJsonAsync<RoofStyleDetail>();

        var detail = await client.GetFromJsonAsync<RoofStyleDetail>($"/api/roof-styles/{created!.Id}");
        await Assert.That(detail!.RoofSlab).IsEqualTo(Blocks.WoodenSlab);
        await Assert.That(detail.RoofSlabData).IsEqualTo(2);
    }

    [Test]
    public async Task A_storey_round_trips_and_is_drawn_as_the_room_it_makes()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var timber = await StyleAsync(client, "timber", Blocks.Log);

        var created = await (await client.PostAsJsonAsync("/api/storey-styles", Storey("shopfront", 5,
                new RoomCourseDto(RoomParts.Wall, 0, timber, 1),
                new RoomCourseDto(RoomParts.Post, 0, timber, 1))))
            .Content.ReadFromJsonAsync<StoreyStyleDetail>();

        var detail = await client.GetFromJsonAsync<StoreyStyleDetail>($"/api/storey-styles/{created!.Id}");
        await Assert.That(detail!.Clear).IsEqualTo(5);
        await Assert.That(detail.Courses.Count).IsEqualTo(2);

        // The clear rides on the summary, because a house binding a stack has to say how tall it comes out.
        var listed = await client.GetFromJsonAsync<List<StoreyStyleSummary>>("/api/storey-styles");
        await Assert.That(listed!.Single().Clear).IsEqualTo(5);
    }

    /// <summary>A storey's ceiling is the slab it closes with, and a slab only exists under something — so a
    /// storey that names one is drawn as two of itself. Without that the knob would be a knob whose picture
    /// never moves, which is the one thing the preview is for preventing.</summary>
    [Test]
    public async Task A_storey_that_names_a_ceiling_is_drawn_with_something_standing_on_it()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var timber = await StyleAsync(client, "timber", Blocks.Log);
        var flags = await StyleAsync(client, "flags", Blocks.Bedrock);

        var plain = Storey("plain", 3, new RoomCourseDto(RoomParts.Wall, 0, timber, 1));
        var ceiled = Storey("ceiled", 3,
            new RoomCourseDto(RoomParts.Wall, 0, timber, 1),
            new RoomCourseDto(RoomParts.Deck, 0, flags, 1));

        var without = (await (await client.PostAsJsonAsync("/api/storey-styles/preview", plain))
            .Content.ReadFromJsonAsync<RoomStylePreviewDto>())!;
        var with = (await (await client.PostAsJsonAsync("/api/storey-styles/preview", ceiled))
            .Content.ReadFromJsonAsync<RoomStylePreviewDto>())!;

        // The cutaway is asked rather than the section: a section is a projection of the outside, and a slab
        // laid across the interior is behind the near wall in one. The cutaway is taken on the plane the
        // ladder stands in, which is the plane the slab and the clear under it are both on.
        await Assert.That(Fills(with.Cutaway)).Contains(BlockPalette.Hex(Blocks.Bedrock, 0));
        await Assert.That(Fills(without.Cutaway)).DoesNotContain(BlockPalette.Hex(Blocks.Bedrock, 0));
        // And the building is taller than the one-storey version, because a slab is what the second is on.
        await Assert.That(Height(with.Section)).IsGreaterThan(Height(without.Section));
    }

    /// <summary>A room has to be stood up in, so a clear under three is read as three however it is asked
    /// for — and the number handed back is the one the row stores rather than the one the request sent.</summary>
    [Test]
    public async Task A_storey_is_never_saved_shorter_than_three()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var saved = await (await client.PostAsJsonAsync("/api/storey-styles", Storey("crawlspace", 1)))
            .Content.ReadFromJsonAsync<StoreyStyleDetail>();
        await Assert.That(saved!.Clear).IsEqualTo(3);
    }

    [Test]
    public async Task A_porch_round_trips_with_its_shape()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var created = await (await client.PostAsJsonAsync("/api/porch-styles",
                new PorchStyleSaveRequest("veranda", 3, 2, PorchEdges.Front, RoofForms.Shed, 85)))
            .Content.ReadFromJsonAsync<PorchStyleDetail>();

        var detail = await client.GetFromJsonAsync<PorchStyleDetail>($"/api/porch-styles/{created!.Id}");
        await Assert.That(detail!.Depth).IsEqualTo(3);
        await Assert.That(detail.Inset).IsEqualTo(2);
        await Assert.That(detail.Roof).IsEqualTo(RoofForms.Shed);
    }

    // ── what a house does with them ────────────────────────────────────────────────────────────────
    /// <summary>The whole point of the level: a house that binds a roof builds <em>that</em> roof, and a house
    /// that binds none is exactly the building its own columns always described.</summary>
    [Test]
    public async Task A_bound_roof_replaces_the_houses_own_and_an_unbound_one_changes_nothing()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slate = await StyleAsync(client, "slate", Blocks.StainedClay);
        var roof = await (await client.PostAsJsonAsync("/api/roof-styles", Roof("gabled", RoofForms.Gable,
                new RoomCourseDto(RoomParts.Roof, 0, slate, 1))))
            .Content.ReadFromJsonAsync<RoofStyleDetail>();

        var flat = await HousePreview(client, House("plain"));
        var gabled = await HousePreview(client, House("gabled", roofStyleId: roof!.Id));

        // A gable is a different building, not a different lid — it stands taller than the flat one it
        // replaced, and it is laid in the roof style's material rather than the built-in.
        await Assert.That(Height(gabled.Section)).IsGreaterThan(Height(flat.Section));
        await Assert.That(Fills(gabled.Plan)).Contains(BlockPalette.Hex(Blocks.StainedClay, 0));

        // And binding nothing leaves the house exactly as it draws with no parts in the library at all.
        await Assert.That((await HousePreview(client, House("plain"))).Section).IsEqualTo(flat.Section);
    }

    /// <summary>A stack of bound storeys is what a house's height becomes: two storeys of three clear is 4 + 3
    /// courses of wall, taller than the seven-course single-storey house it replaced by nothing at all — so
    /// the assertion that matters is the <b>ladder</b>, which only a stacked building has.</summary>
    [Test]
    public async Task A_bound_stack_makes_the_house_a_stack_of_rooms()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var storey = await (await client.PostAsJsonAsync("/api/storey-styles", Storey("room", 3)))
            .Content.ReadFromJsonAsync<StoreyStyleDetail>();

        var one = await HousePreview(client, House("one", stack: [new RoomStoreyDto(storey!.Id, 0)]));
        var three = await HousePreview(client, House("three", stack:
            [new RoomStoreyDto(storey.Id, 0), new RoomStoreyDto(storey.Id, 0), new RoomStoreyDto(storey.Id, 0)]));

        await Assert.That(Height(three.Section)).IsGreaterThan(Height(one.Section));
        // The ladder is the thing a stack has and a single storey does not: three storeys have a way up, one
        // has nowhere to go. The cutaway is the view that shows it, being the plane the ladder stands in.
        await Assert.That(Fills(three.Cutaway)).Contains(BlockPalette.Hex(Blocks.Ladder, 3));
        await Assert.That(Fills(one.Cutaway)).DoesNotContain(BlockPalette.Hex(Blocks.Ladder, 3));
    }

    /// <summary>One preset, two heights: the house's own clear for a position overrides the storey style's, so
    /// a tall ground floor under ordinary rooms is one preset bound twice rather than two presets.</summary>
    [Test]
    public async Task A_stack_slot_can_override_the_storeys_own_clear()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var storey = await (await client.PostAsJsonAsync("/api/storey-styles", Storey("room", 3)))
            .Content.ReadFromJsonAsync<StoreyStyleDetail>();

        var even = await HousePreview(client, House("even", stack:
            [new RoomStoreyDto(storey!.Id, 0), new RoomStoreyDto(storey.Id, 0)]));
        var tallGround = await HousePreview(client, House("shop", stack:
            [new RoomStoreyDto(storey.Id, 6), new RoomStoreyDto(storey.Id, 0)]));

        await Assert.That(Height(tallGround.Section)).IsGreaterThan(Height(even.Section));
    }

    /// <summary>The stack is an order, and the order is the position in the list — so a house reads back with
    /// its storeys in the order it saved them, ground first.</summary>
    [Test]
    public async Task A_houses_stack_reads_back_in_the_order_it_was_saved()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var shop = await (await client.PostAsJsonAsync("/api/storey-styles", Storey("shop", 6)))
            .Content.ReadFromJsonAsync<StoreyStyleDetail>();
        var flat = await (await client.PostAsJsonAsync("/api/storey-styles", Storey("flat", 3)))
            .Content.ReadFromJsonAsync<StoreyStyleDetail>();

        var created = await (await client.PostAsJsonAsync("/api/room-styles", House("terrace", stack:
                [new RoomStoreyDto(shop!.Id, 0), new RoomStoreyDto(flat!.Id, 0), new RoomStoreyDto(flat.Id, 0)])))
            .Content.ReadFromJsonAsync<RoomStyleDetail>();

        var detail = await client.GetFromJsonAsync<RoomStyleDetail>($"/api/room-styles/{created!.Id}");
        await Assert.That(detail!.StoreyStack.Select(s => s.StoreyStyleId))
            .IsEquivalentTo(new[] { shop.Id, flat.Id, flat.Id });
    }

    /// <summary>A part a building still wears cannot be forgotten: deleting it would silently change every
    /// house bound to it, which is the answer a style already gives a theme.</summary>
    [Test]
    public async Task A_part_a_house_still_binds_cannot_be_deleted()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var roof = await (await client.PostAsJsonAsync("/api/roof-styles", Roof("hipped", RoofForms.Hip)))
            .Content.ReadFromJsonAsync<RoofStyleDetail>();
        var storey = await (await client.PostAsJsonAsync("/api/storey-styles", Storey("room")))
            .Content.ReadFromJsonAsync<StoreyStyleDetail>();
        await client.PostAsJsonAsync("/api/room-styles",
            House("cottage", roofStyleId: roof!.Id, stack: [new RoomStoreyDto(storey!.Id, 0)]));

        await Assert.That((await client.DeleteAsync($"/api/roof-styles/{roof.Id}")).StatusCode)
            .IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That((await client.DeleteAsync($"/api/storey-styles/{storey.Id}")).StatusCode)
            .IsEqualTo(HttpStatusCode.Conflict);

        // An unbound one goes freely.
        var spare = await (await client.PostAsJsonAsync("/api/roof-styles", Roof("spare")))
            .Content.ReadFromJsonAsync<RoofStyleDetail>();
        await Assert.That((await client.DeleteAsync($"/api/roof-styles/{spare!.Id}")).StatusCode)
            .IsEqualTo(HttpStatusCode.NoContent);
    }

    /// <summary>A draft previews exactly as a save composes, which is the discipline the whole library runs
    /// on: the picture cannot promise a part the save would not build.</summary>
    [Test]
    public async Task A_draft_part_previews_as_the_saved_one_draws()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slate = await StyleAsync(client, "slate", Blocks.StainedClay);
        var draft = Roof("hipped", RoofForms.Hip, new RoomCourseDto(RoomParts.Roof, 0, slate, 1));

        var previewed = await (await client.PostAsJsonAsync("/api/roof-styles/preview", draft))
            .Content.ReadFromJsonAsync<RoomStylePreviewDto>();
        var saved = await (await client.PostAsJsonAsync("/api/roof-styles", draft))
            .Content.ReadFromJsonAsync<RoofStyleDetail>();
        var listed = await client.GetFromJsonAsync<List<RoofStyleSummary>>("/api/roof-styles");

        // The card is the section of the same building the draft drew.
        await Assert.That(listed!.Single(entry => entry.Id == saved!.Id).Preview)
            .IsEqualTo(previewed!.Section);
    }
}
