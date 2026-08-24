using System.Text.Json;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// The seam where an authored relief becomes terrain: the rasterizer takes a relief-bearing island's column
/// tops from the solved field instead of from the shape's own outline heights, and a mirrored copy takes them
/// from the same field read back through the same transform.
/// </summary>
public sealed class SketchReliefTests
{
    // One island, one square shape, and whatever relief the caller wants over it.
    private static string Layout(string? relief, bool mirrors = false, string mirrorMode = "rot_180") => $$"""
    {
      "setup": { "mirror_mode": "{{mirrorMode}}", "center": { "cx": 40, "cz": 10 } },
      "layers": [{ "id": "ground", "base_y": 0, "layout": {
        "shapes": [
          { "id": "s1", "type": "rectangle", "operation": "add",
            "min_x": 0, "min_z": 0, "max_x": 20, "max_z": 20, "base_height": 3, "floor": 0 }
        ],
        "islands": [ { "id": "i1", "name": "board", "mirrors": {{(mirrors ? "true" : "false")}}, "shapeIds": ["s1"] } ]
      } }]
      {{(relief is null ? "" : $", \"relief\": {relief}")}}
    }
    """;

    private static Dictionary<(int X, int Z), int> Tops(string layoutJson)
    {
        var tops = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z, _, top, _) in SketchRasterizer.RasterizeColumns(layoutJson))
            if (!tops.TryGetValue((x, z), out var current) || top > current) tops[(x, z)] = top;
        return tops;
    }

    [Test]
    public async Task Without_a_relief_a_shape_keeps_its_own_height()
    {
        var tops = Tops(Layout(null));
        await Assert.That(tops[(10, 10)]).IsEqualTo(3);
    }

    [Test]
    public async Task A_relief_replaces_the_islands_column_tops()
    {
        const string relief = """
        { "i1": { "base": 5, "marks": [ { "kind": "point", "at": [10, 10], "h": 12, "r": 3 } ] } }
        """;
        var tops = Tops(Layout(relief));

        // One mark and no rim decides the whole surface, so the island stands at the height that was stated —
        // not the 3 the shape's own base_height asked for.
        await Assert.That(tops[(10, 10)]).IsEqualTo(12);
        await Assert.That(tops[(2, 2)]).IsEqualTo(12);
    }

    [Test]
    public async Task A_stored_line_mark_keeps_the_band_it_was_drawn_with()
    {
        // The reach either side of a line is one quantity under one name; a document stored under the other
        // reads into the same field, so a relief authored before the rename holds the same ground. Seventeen
        // committed layouts carry it, and the failure is silent: the mark falls back to the default reach and
        // the terrain under it quietly flattens.
        const string stored = """
        { "i1": { "base": 3, "reach": 12, "marks": [
          { "id": "m", "kind": "line", "points": [[0, 10], [20, 10]], "h": 12, "width": 6 },
          { "id": "edge", "kind": "rim", "h": 3, "depth": 1 } ] } }
        """;
        const string renamed = """
        { "i1": { "base": 3, "reach": 12, "marks": [
          { "id": "m", "kind": "line", "points": [[0, 10], [20, 10]], "h": 12, "r": 6 },
          { "id": "edge", "kind": "rim", "h": 3, "depth": 1 } ] } }
        """;
        const string defaulted = """
        { "i1": { "base": 3, "reach": 12, "marks": [
          { "id": "m", "kind": "line", "points": [[0, 10], [20, 10]], "h": 12 },
          { "id": "edge", "kind": "rim", "h": 3, "depth": 1 } ] } }
        """;

        var byOldName = SketchRasterizer.ReliefFields(Layout(stored))["i1"];
        var byNewName = SketchRasterizer.ReliefFields(Layout(renamed))["i1"];
        var byDefault = SketchRasterizer.ReliefFields(Layout(defaulted))["i1"];

        // The two spellings are the same surface, cell for cell.
        foreach (var (x, z) in byNewName.Footprint.Land())
            await Assert.That(byOldName.At(x, z)).IsEqualTo(byNewName.At(x, z));

        // And the stored name is genuinely read: a mark falling back to the default reach holds less ground,
        // which is the silent flattening this exists to stop.
        await Assert.That(byOldName.At(10, 15)).IsEqualTo(12);
        await Assert.That(byDefault.At(10, 15)).IsLessThan(12);
    }

    [Test]
    public async Task A_rim_keeps_the_hill_inside_the_shape()
    {
        const string relief = """
        {
          "i1": {
            "base": 5,
            "marks": [
              { "kind": "rim",   "h": 5, "depth": 1 },
              { "kind": "point", "at": [10, 10], "h": 14, "r": 3 }
            ]
          }
        }
        """;
        var tops = Tops(Layout(relief));

        await Assert.That(tops[(10, 10)]).IsEqualTo(14);
        await Assert.That(tops[(0, 0)]).IsEqualTo(5);
        await Assert.That(tops[(19, 19)]).IsEqualTo(5);
    }

    [Test]
    public async Task A_relief_never_pushes_a_column_below_its_own_floor()
    {
        // A relief says where the ground is, not how thick the slab under it is — but a surface at or under
        // the floor would be a column of no height at all.
        const string relief = """
        { "i1": { "base": 0, "marks": [ { "kind": "point", "at": [10, 10], "h": -4, "r": 3 } ] } }
        """;
        var tops = Tops(Layout(relief));
        await Assert.That(tops[(10, 10)]).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task A_mirrored_island_carries_the_same_relief_cell_for_cell()
    {
        // The mirror copy reads the island's own solved surface back through the same transform, so the two
        // sides are identical by construction rather than to within a second solve's tolerance.
        const string relief = """
        {
          "i1": {
            "base": 5,
            "grain": { "amplitude": 1.5, "scale": 7, "seed": 4 },
            "marks": [
              { "kind": "rim",   "h": 5, "depth": 1 },
              { "kind": "point", "at": [6, 6], "h": 15, "r": 3 }
            ]
          }
        }
        """;
        var tops = Tops(Layout(relief, mirrors: true, mirrorMode: "mirror_x"));

        // The centre is cx 40, so a cell at x mirrors to 79 - x.
        var worst = 0;
        for (var x = 0; x <= 20; x++)
            for (var z = 0; z <= 20; z++)
                if (tops.TryGetValue((x, z), out var here) && tops.TryGetValue((79 - x, z), out var there))
                    worst = Math.Max(worst, Math.Abs(here - there));

        await Assert.That(worst).IsEqualTo(0);
    }

    [Test]
    public async Task A_relief_naming_no_island_places_nothing()
    {
        const string relief = """
        { "somewhere-else": { "base": 5, "marks": [ { "kind": "point", "at": [10, 10], "h": 12, "r": 3 } ] } }
        """;
        var tops = Tops(Layout(relief));
        await Assert.That(tops[(10, 10)]).IsEqualTo(3);
    }

    [Test]
    public async Task The_preview_reads_the_same_field_the_build_does()
    {
        // The property the whole preview rests on: what an author sees on the canvas is the surface that
        // gets built, not a second computation that agrees with it most of the time.
        const string relief = """
        {
          "i1": {
            "base": 5,
            "grain": { "amplitude": 1.1, "scale": 6, "seed": 3 },
            "marks": [ { "kind": "rim", "h": 5 }, { "kind": "point", "at": [10, 10], "h": 16, "r": 3 } ]
          }
        }
        """;
        var layout = Layout(relief);
        var field = SketchRasterizer.ReliefFields(layout)["i1"];
        var tops = Tops(layout);

        foreach (var (x, z) in field.Footprint.Land())
            await Assert.That(tops[(x, z)]).IsEqualTo(field.At(x, z));
    }

    [Test]
    public async Task A_layer_offset_moves_the_previewed_field_with_the_ground()
    {
        // A relief on a stacked layer is solved in layer-local Y, so a preview reading it raw would draw a
        // sky bridge's contours down at the ground's levels.
        const string layout = """
        {
          "setup": { "mirror_mode": "rot_180", "center": { "cx": 40, "cz": 10 } },
          "layers": [
            { "id": "l1", "base_y": 30, "layout": {
              "shapes": [ { "id": "s1", "type": "rectangle", "operation": "add",
                            "min_x": 0, "min_z": 0, "max_x": 20, "max_z": 20, "base_height": 3, "floor": 0 } ],
              "islands": [ { "id": "i1", "mirrors": false, "shapeIds": ["s1"] } ] } }
          ],
          "relief": { "i1": { "base": 5, "marks": [ { "kind": "point", "at": [10, 10], "h": 12, "r": 3 } ] } }
        }
        """;
        var field = SketchRasterizer.ReliefFields(layout)["i1"];
        await Assert.That(field.At(10, 10)).IsEqualTo(42);
        await Assert.That(field.Continuous[field.Footprint.Index(10, 10)]).IsBetween(41.5, 42.5);
    }

    [Test]
    public async Task A_layout_with_no_relief_previews_nothing()
    {
        await Assert.That(SketchRasterizer.ReliefFields(Layout(null)).Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_marks_id_survives_a_round_trip_and_is_absent_when_it_has_none()
    {
        // The editor's handle on a mark. It has to come back out of the document unchanged, or a stored relief
        // would renumber its marks on load and move the selection under the author's hands. A hand-written
        // relief carries none, and must not gain one silently — which would look like an edit nobody made.
        const string json = """
        {
          "base": 5,
          "marks": [
            { "id": "r3", "kind": "point", "at": [4, 4], "h": 12 },
            { "kind": "area", "ring": [[0, 0], [8, 0], [8, 8]], "h": 7 }
          ]
        }
        """;
        var relief = JsonSerializer.Deserialize<SketchReliefJson>(json, SketchLayout.Json)!;
        await Assert.That(relief.Marks![0].Id).IsEqualTo("r3");
        await Assert.That(relief.Marks[1].Id).IsNull();

        var written = JsonSerializer.Serialize(relief, SketchLayout.Json);
        await Assert.That(written).Contains("\"id\":\"r3\"");
        await Assert.That(written.Split("{\"kind\":\"area\"")[^1]).DoesNotContain("\"id\"");
    }

    [Test]
    public async Task A_push_lifts_the_solved_surface_and_two_of_them_add()
    {
        // The property that separates a push from a mark: it is applied AFTER the solve as a relative move,
        // so two over the same ground add where two constraints would have to argue over it.
        static string WithPushes(string pushes) => Layout($$"""
        {
          "i1": {
            "base": 6,
            "marks": [ { "kind": "rim", "h": 6 } ],
            "pushes": {{pushes}}
          }
        }
        """);

        const string ring = """[[6,6],[14,6],[14,14],[6,14]]""";
        var one = Tops(WithPushes($$"""[ { "ring": {{ring}}, "amount": 5, "falloff": 3, "crown": 0 } ]"""));
        var two = Tops(WithPushes($$"""
        [ { "ring": {{ring}}, "amount": 5, "falloff": 3, "crown": 0 },
          { "ring": {{ring}}, "amount": 5, "falloff": 3, "crown": 0 } ]
        """));

        await Assert.That(two[(10, 10)] - one[(10, 10)]).IsEqualTo(one[(10, 10)] - 6);
    }

    [Test]
    public async Task A_per_vertex_lift_falls_along_the_ring()
    {
        // What makes a drawn ridge fall along its length instead of holding level — and the reason the
        // editor expands the single amount into one number per corner before showing it.
        const string relief = """
        {
          "i1": {
            "base": 4,
            "marks": [ { "kind": "rim", "h": 4 } ],
            "pushes": [ { "ring": [[2,8],[10,8],[18,8],[18,12],[10,12],[2,12]],
                          "amount": 10, "amounts": [10, 6, 2, 2, 6, 10], "falloff": 2, "crown": 0 } ]
          }
        }
        """;
        var tops = Tops(Layout(relief));
        await Assert.That(tops[(3, 10)]).IsGreaterThan(tops[(17, 10)]);
    }

    [Test]
    public async Task An_id_and_a_per_vertex_lift_survive_the_wire()
    {
        // The editor's handle on a placed thing, and the array that makes a ridge fall. Both are dropped by a
        // round trip that does not know them, which would silently move the selection and level the ridge.
        const string relief = """
        {
          "i1": {
            "marks": [ { "id": "r4", "kind": "point", "at": [5, 5], "h": 9, "r": 2 } ],
            "pushes": [ { "id": "r5", "ring": [[0,0],[8,0],[8,8]], "amount": 6, "amounts": [6, 3, 1] } ]
          }
        }
        """;
        var state = SketchLayout.Parse(Layout(relief));
        var stated = state!.Relief!["i1"];

        await Assert.That(stated.Marks![0].Id).IsEqualTo("r4");
        await Assert.That(stated.Pushes![0].Id).IsEqualTo("r5");
        await Assert.That(stated.Pushes[0].Amounts).IsEquivalentTo(new[] { 6d, 3d, 1d });

        // And back out again, through the carry a recompile runs.
        var carried = SketchLayout.CarryRelief(Layout(null), Layout(relief));
        var round = SketchLayout.Parse(carried)!.Relief!["i1"];
        await Assert.That(round.Marks![0].Id).IsEqualTo("r4");
        await Assert.That(round.Pushes![0].Amounts).IsEquivalentTo(new[] { 6d, 3d, 1d });
    }

    [Test]
    public async Task A_half_written_mark_is_dropped_rather_than_guessed_at()
    {
        // A point with no position cannot place terrain, and inventing one would put ground where nobody
        // asked for it.
        var mark = new ReliefMarkJson { Kind = "point", Heights = [9] };
        await Assert.That(mark.ToMark()).IsNull();

        var line = new ReliefMarkJson { Kind = "line", Points = [[0, 0], [4, 4]], Heights = [7, 9] };
        await Assert.That(line.ToMark()).IsNotNull();
    }
}
