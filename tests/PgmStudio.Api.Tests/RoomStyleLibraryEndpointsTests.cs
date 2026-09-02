using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using PgmStudio.Contracts;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The room-style library's HTTP surface (G34b). The cases that matter are the ones the library exists for: a
/// composed room reads back with its stacks intact, the picture is drawn by the real stamper so a knob that
/// does nothing to the export does nothing to the card, and the doors offered are the ones the wool-room filter
/// whitelists. Runs against <c>pgm_studio_test</c>; each test resets the schema, so they run serially.
/// </summary>
[NotInParallel("api-db")]
public sealed class RoomStyleLibraryEndpointsTests
{
    private static RoomStyleSaveRequest Draft(string name, params RoomCourseDto[] courses) => new(
        name, FloorDepth: 1, WallHeight: 7,
        RoofForms.Flat, Pitch: 1, Overhang: 0, RoofHole: true, RidgeCap: false,
        BorderWidth: 1, InlayInset: 2, Storeys: 1, StoreyClear: 0,
        Windows: new RoomWindowDto(WindowForms.None, Blocks.GlassPane, 0, 2, 2, 2, 3), Porch: null,
        Door: "stained-glass-pane", DoorHeight: 3,
        RoofStyleId: null, PorchStyleId: null, StoreyStack: [], Courses: courses);

    private static async Task<long> StyleAsync(HttpClient client, string name, int blockId)
    {
        var saved = await (await client.PostAsJsonAsync("/api/styles", new StyleSaveRequest(
                name, MaterialKind.Solid, TerrainThemeJson.Serialize(new SolidMaterial(blockId)))))
            .Content.ReadFromJsonAsync<StyleDto>();
        return saved!.Id;
    }

    // The raster merges same-colour runs into one rect, so a rect count measures a picture's structure rather
    // than its blocks. What a colour is in is what these read.
    private static HashSet<string> Fills(string svg)
        => Regex.Matches(svg, "fill='(#[0-9a-f]{6})'").Select(m => m.Groups[1].Value).ToHashSet();

    private static int Height(string svg) => int.Parse(Regex.Match(svg, "height='(\\d+)'").Groups[1].Value);

    /// <summary>The courses a columns payload actually carries, read out of its run stride
    /// <c>[x, z, runCount, (yTop, yBottom, colour, layer) × runCount]</c>. A picture's extent is the box it was
    /// drawn over; this is what is standing in it.</summary>
    private static (int Low, int High, int Runs) Extent(WorldColumnsDto columns)
    {
        int low = int.MaxValue, high = int.MinValue, runs = 0;
        for (var at = 0; at < columns.Cols.Count;)
        {
            var count = columns.Cols[at + 2];
            at += 3;
            for (var run = 0; run < count; run++, at += 4, runs++)
            {
                high = Math.Max(high, columns.Cols[at]);
                low = Math.Min(low, columns.Cols[at + 1]);
            }
        }
        return (low, high, runs);
    }

    /// <summary>The preview answers the building itself rather than a picture of it, and the part an editor
    /// has open cuts it: a roof row draws the roof, standing clear of the ground the whole building stands on.
    /// The three flat views are cut by the same box, so the section shortens with it while the plan — an XZ
    /// read — does not.</summary>
    [Test]
    public async Task A_preview_answers_a_world_and_the_part_cuts_it()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var draft = Draft("cut");
        var whole = await Preview(client, draft, part: null);
        var roof = await Preview(client, draft, part: RoomParts.Roof);
        var floor = await Preview(client, draft, part: RoomParts.Floor);

        // A world, not a picture: colours to draw it in and runs to draw.
        await Assert.That(whole!.Columns.Palette).IsNotEmpty();
        await Assert.That(Extent(whole.Columns).Runs).IsGreaterThan(0);

        // The bands stack in the order a building does, and neither reaches into the other's.
        await Assert.That(Extent(roof!.Columns).Low).IsGreaterThan(Extent(floor!.Columns).High);

        // Each band is a slice of the whole, and the two that end on it keep its ends: a floor is claimed
        // downward from the course players walk on, so it starts where the building does, and the roof is
        // everything over the eave, so it stops where the building stops.
        await Assert.That(Extent(floor.Columns).Low).IsEqualTo(Extent(whole.Columns).Low);
        await Assert.That(Extent(roof.Columns).High).IsEqualTo(Extent(whole.Columns).High);

        // The flat views follow the same cut — except the plan, which reads down the Y the cut is taken on.
        await Assert.That(Height(roof.Section)).IsLessThan(Height(whole.Section));
        await Assert.That(Height(roof.Plan)).IsEqualTo(Height(whole.Plan));
    }

    private static async Task<RoomStylePreviewDto?> Preview(
        HttpClient client, RoomStyleSaveRequest draft, string? part)
        => await (await client.PostAsJsonAsync(
                $"/api/room-styles/preview{(part is null ? "" : $"?part={part}")}", draft))
            .Content.ReadFromJsonAsync<RoomStylePreviewDto>();

    /// <summary>
    /// Every field the row stores comes back on the wire. The five under test here were added to the request
    /// as trailing defaulted parameters, which is exactly why they could go missing from the mapping without
    /// anything failing to compile: the building stamped off the row kept them and the DTO the editor loads
    /// and saves back did not, so opening a house and pressing save was enough to lose its door head.
    /// </summary>
    [Test]
    public async Task A_room_style_answers_back_the_parts_a_later_field_added()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        // Every pairing here is one the gates accept, because a refusal would prove nothing about the
        // mapping: a window cut from the same material as its host (HS4), a beam that is a log (HS1), and a
        // half-course slab in the material the roof body is laid in (HS3).
        const int StoneBricks = 98, StoneBrickStairs = 109, StoneBrickSlabData = 5;
        var brick = await StyleAsync(client, "stone bricks", StoneBricks);
        var draft = Draft("full house", new RoomCourseDto(RoomParts.Roof, 0, brick, 1)) with
        {
            RoofForm = RoofForms.Gable,
            Windows = new RoomWindowDto(WindowForms.StairLattice, Blocks.CobblestoneStairs, 0, 2, 2, 2, 3,
                HostBlock: Blocks.Cobblestone, HostData: 0),
            Beams = new RoomBeamDto(Blocks.Log, 2, 3),
            RoofSlab = Blocks.StoneSlab, RoofSlabData = StoneBrickSlabData,
            GableWindows = new RoomWindowDto(WindowForms.Open, 102, 0, 1, 2, 2, 3),
            DoorHead = new RoomDoorHeadDto(DoorHeadForms.Arched, StoneBrickStairs,
                DoorHeadFills.UpperSlab, Blocks.StoneSlab, StoneBrickSlabData),
        };

        var response = await client.PostAsJsonAsync("/api/room-styles", draft);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var saved = await response.Content.ReadFromJsonAsync<RoomStyleDetail>();
        var read = await client.GetFromJsonAsync<RoomStyleDetail>($"/api/room-styles/{saved!.Id}");

        foreach (var answer in new[] { saved, read! })
        {
            await Assert.That(answer.Windows.HostBlock).IsEqualTo(Blocks.Cobblestone);
            await Assert.That(answer.Beams).IsNotNull();
            await Assert.That(answer.Beams!.Block).IsEqualTo(Blocks.Log);
            await Assert.That(answer.Beams.Reach).IsEqualTo(3);
            await Assert.That(answer.RoofSlab).IsEqualTo(Blocks.StoneSlab);
            await Assert.That(answer.RoofSlabData).IsEqualTo(5);
            await Assert.That(answer.GableWindows).IsNotNull();
            await Assert.That(answer.GableWindows!.Form).IsEqualTo(WindowForms.Open);
            await Assert.That(answer.DoorHead).IsNotNull();
            await Assert.That(answer.DoorHead!.Form).IsEqualTo(DoorHeadForms.Arched);
            await Assert.That(answer.DoorHead.FillBlock).IsEqualTo(Blocks.StoneSlab);
            await Assert.That(answer.DoorHead.FillData).IsEqualTo(StoneBrickSlabData);
        }
    }

    /// <summary>
    /// The editor's own load-and-save path keeps every field, not only the ones it draws a control for.
    ///
    /// <para>An editor loads a row into a draft and PUTs the draft back, so what the load leaves out the save
    /// writes away — and a field with no control is exactly the field a hand-written load list omits.
    /// <c>RoomStyleDetail.AsSaveRequest</c> is the one place that mapping lives, and this is what fails when
    /// a later field is added outside it.</para>
    /// </summary>
    [Test]
    public async Task Loading_a_house_and_saving_it_back_unchanged_loses_nothing()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        const int StoneBricks = 98, StoneBrickStairs = 109, StoneBrickSlabData = 5;
        var brick = await StyleAsync(client, "stone bricks", StoneBricks);
        var draft = Draft("round trip", new RoomCourseDto(RoomParts.Roof, 0, brick, 1)) with
        {
            RoofForm = RoofForms.Gable,
            Windows = new RoomWindowDto(WindowForms.StairLattice, Blocks.CobblestoneStairs, 0, 2, 2, 2, 3,
                HostBlock: Blocks.Cobblestone, HostData: 0),
            Beams = new RoomBeamDto(Blocks.Log, 2, 3),
            RoofSlab = Blocks.StoneSlab, RoofSlabData = StoneBrickSlabData,
            GableWindows = new RoomWindowDto(WindowForms.Open, 102, 0, 1, 2, 2, 3),
            DoorHead = new RoomDoorHeadDto(DoorHeadForms.Arched, StoneBrickStairs,
                DoorHeadFills.UpperSlab, Blocks.StoneSlab, StoneBrickSlabData),
            DoorWidth = 3,
        };

        var created = await client.PostAsJsonAsync("/api/room-styles", draft);
        var saved = await created.Content.ReadFromJsonAsync<RoomStyleDetail>();

        // Exactly what the editor does: read the row, turn it into a request, send it straight back.
        var loaded = await client.GetFromJsonAsync<RoomStyleDetail>($"/api/room-styles/{saved!.Id}");
        var response = await client.PutAsJsonAsync($"/api/room-styles/{saved.Id}", loaded!.AsSaveRequest());
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var after = await client.GetFromJsonAsync<RoomStyleDetail>($"/api/room-styles/{saved.Id}");
        await Assert.That(after!.Beams).IsNotNull();
        await Assert.That(after.Beams!.Block).IsEqualTo(Blocks.Log);
        await Assert.That(after.RoofSlab).IsEqualTo(Blocks.StoneSlab);
        await Assert.That(after.RoofSlabData).IsEqualTo(StoneBrickSlabData);
        await Assert.That(after.GableWindows).IsNotNull();
        await Assert.That(after.DoorHead).IsNotNull();
        await Assert.That(after.DoorHead!.Form).IsEqualTo(DoorHeadForms.Arched);
        await Assert.That(after.DoorWidth).IsEqualTo(3);
        await Assert.That(after.Windows.HostBlock).IsEqualTo(Blocks.Cobblestone);
    }

    /// <summary>A house that states none of them answers absent rather than a shape meaning none, so the
    /// editor reads the absence — and saving what it read stores the same nothing back.</summary>
    [Test]
    public async Task A_room_style_stating_none_of_them_answers_absent()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var saved = await (await client.PostAsJsonAsync("/api/room-styles", Draft("plain house")))
            .Content.ReadFromJsonAsync<RoomStyleDetail>();
        var read = await client.GetFromJsonAsync<RoomStyleDetail>($"/api/room-styles/{saved!.Id}");

        await Assert.That(read!.Beams).IsNull();
        await Assert.That(read.GableWindows).IsNull();
        await Assert.That(read.DoorHead).IsNull();
        await Assert.That(read.Windows.HostBlock).IsEqualTo(-1);
    }

    [Test]
    public async Task A_room_style_round_trips_with_its_stacks_in_order()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var stone = await StyleAsync(client, "stone", Blocks.Stone);
        var clay = await StyleAsync(client, "clay", Blocks.StainedClay);

        var created = await (await client.PostAsJsonAsync("/api/room-styles", Draft("bunker",
                new RoomCourseDto(RoomParts.Wall, 0, stone, 3),
                new RoomCourseDto(RoomParts.Wall, 1, clay, 1),
                new RoomCourseDto(RoomParts.Wall, 2, stone, 1),
                new RoomCourseDto(RoomParts.Roof, 0, clay, 1))))
            .Content.ReadFromJsonAsync<RoomStyleDetail>();

        var detail = await client.GetFromJsonAsync<RoomStyleDetail>($"/api/room-styles/{created!.Id}");
        var wall = detail!.Courses.Where(c => c.Part == RoomParts.Wall).OrderBy(c => c.Ordinal).ToList();
        await Assert.That(wall.Select(c => c.StyleId)).IsEquivalentTo(new[] { stone, clay, stone });
        await Assert.That(wall[0].Height).IsEqualTo(3);
        await Assert.That(detail.Courses.Count(c => c.Part == RoomParts.Roof)).IsEqualTo(1);

        // And it lists with the shell it stamps.
        var listed = await client.GetFromJsonAsync<List<RoomStyleSummary>>("/api/room-styles");
        await Assert.That(listed!.Count).IsEqualTo(1);
        await Assert.That(listed[0].Preview).Contains("<rect");
    }

    [Test]
    public async Task An_unbound_part_keeps_the_built_in_finish()
    {
        // The reason the library is worth having for a style that only changes its roof: naming one part does
        // not blank the other two.
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var bedrock = BlockPalette.Hex(Blocks.Bedrock, 0);
        var clay = BlockPalette.Hex(Blocks.StainedClay, 0);

        var bare = await Preview(client, Draft("bare"));
        var roofed = await Preview(client, Draft("roofed",
            new RoomCourseDto(RoomParts.Roof, 0, await StyleAsync(client, "clay", Blocks.StainedClay), 1)));

        // Read from above, the roof is what a plan view shows. Unbound it is the built-in bedrock; bound it is
        // the style — and the floor seen through the roof's hole is still bedrock either way, because naming
        // the roof left the other two parts alone.
        await Assert.That(Fills(bare.Plan)).Contains(bedrock);
        await Assert.That(Fills(bare.Plan)).DoesNotContain(clay);
        await Assert.That(Fills(roofed.Plan)).Contains(clay);
        await Assert.That(Fills(roofed.Plan)).Contains(bedrock);
    }

    [Test]
    public async Task The_picture_follows_the_shell_the_knobs_build()
    {
        // Not a second copy of the geometry tests — those assert block by block in RoomStyleTests. What only
        // this can tell is that the knobs reach the drawing at all: the same request that would be saved is
        // what is composed and stamped.
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var plain = Draft("plain");
        var baseline = await Preview(client, plain);
        await Assert.That(baseline.Plan).Contains("<rect");
        await Assert.That(baseline.Section).Contains("<rect");

        // A shell that grows upward is a taller picture — the section is cropped to the shell it drew.
        await Assert.That(Height((await Preview(client, plain with { WallHeight = 11 })).Section))
            .IsGreaterThan(Height(baseline.Section));
        // And one that grows downward: a deeper floor is drawn from further down.
        await Assert.That(Height((await Preview(client, plain with { FloorDepth = 4 })).Section))
            .IsGreaterThan(Height(baseline.Section));

        // An eave and a sealed roof change what the plan shows without changing its size.
        await Assert.That((await Preview(client, plain with { Overhang = 1 })).Plan)
            .IsNotEqualTo(baseline.Plan);
        // And the shape itself: a gable is a different building, not a different lid.
        await Assert.That((await Preview(client, plain with { RoofForm = RoofForms.Gable })).Section)
            .IsNotEqualTo(baseline.Section);
        await Assert.That((await Preview(client, plain with { RoofHole = false })).Plan)
            .IsNotEqualTo(baseline.Plan);
    }

    [Test]
    public async Task Only_the_doors_the_filter_whitelists_are_offered()
    {
        // The picker cannot offer a door the wool-room block rule does not name, or the cage it stamps has an
        // entrance nobody can open. Served from the one table both sides read.
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var doors = await client.GetFromJsonAsync<List<DoorOptionDto>>("/api/room-styles/doors") ?? [];
        await Assert.That(doors.Select(d => d.Slug))
            .IsEquivalentTo(new[] { "air", "web", "stained-glass", "stained-glass-pane" });
        await Assert.That(doors.All(d => !string.IsNullOrWhiteSpace(d.Label))).IsTrue();
    }

    [Test]
    public async Task A_style_a_room_binds_cannot_be_forgotten_while_it_does()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var stone = await StyleAsync(client, "stone", Blocks.Stone);
        await client.PostAsJsonAsync("/api/room-styles", Draft("bunker", new RoomCourseDto(RoomParts.Wall, 0, stone, 1)));

        var refused = await client.DeleteAsync($"/api/styles/{stone}");
        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        var why = await refused.Content.ReadFromJsonAsync<RefusalDto>();
        await Assert.That(why!.Findings.SelectMany(finding => finding.SubjectIds)).Contains("bunker");
    }

    [Test]
    public async Task An_unknown_room_style_is_a_404_on_read_and_on_write()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        await Assert.That((await client.GetAsync("/api/room-styles/404")).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That((await client.PutAsJsonAsync("/api/room-styles/404", Draft("ghost"))).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
    }

    private static async Task<RoomStylePreviewDto> Preview(HttpClient client, RoomStyleSaveRequest draft)
        => (await (await client.PostAsJsonAsync("/api/room-styles/preview", draft))
            .Content.ReadFromJsonAsync<RoomStylePreviewDto>())!;
}
