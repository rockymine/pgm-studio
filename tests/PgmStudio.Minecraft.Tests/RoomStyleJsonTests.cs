using PgmStudio.Domain;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// A room style as the JSON a map snapshots (docs/world-export/structures.md §9). The case that matters is the
/// round trip through the whole graph: a course's material is polymorphic, and a stack that lost its tint or
/// its air course on the way out would stamp a different building than the one that was picked.
/// </summary>
public sealed class RoomStyleJsonTests
{
    /// <summary>The round trip is only as good as the equality it is asserted with, and
    /// <see cref="HouseStyle.Equals(HouseStyle)"/> is written out by hand — a member added to the style and
    /// not to it would drop out of every comparison in this file without failing one of them. So the members
    /// are named here too: adding one fails this test, which is the reminder to go and add it there.</summary>
    [Test]
    public async Task The_style_has_no_member_the_hand_written_equality_forgot()
    {
        var members = typeof(HouseStyle)
            .GetProperties()
            .Where(property => property.SetMethod is not null)
            .Select(property => property.Name)
            .Order()
            .ToList();

        await Assert.That(members).IsEquivalentTo(new[]
        {
            "Beams", "Doorway", "Front", "Foundation",
            "Porch", "Post", "Roof", "Storeys", "Wall", "Windows",
        }.Order().ToList());
    }

    /// <summary>
    /// <b>A snapshot outlives the shape it was written in.</b> A map keeps its bound style rather than a key
    /// into the library, so every style ever stored is still out there in a layout blob — and the reader falls
    /// back to the built-in shell on anything it cannot make sense of, which turns a shape change without an
    /// upgrade into a map that quietly stops looking like itself rather than into an error.
    ///
    /// <para>The sill, the floor and its zoning were three fields beside everything else; they are the one
    /// thing a building stands on. A sill resolving to air was how "no footing" was said before it was a
    /// state, so it reads forward as one.</para>
    /// </summary>
    [Test]
    public async Task A_style_stored_before_the_foundation_had_a_name_reads_forward()
    {
        var stored = """
            {"sill":{"kind":"solid","id":4,"data":0},
             "floor":{"courses":[{"material":{"kind":"solid","id":5,"data":0},"height":1}],"extent":3},
             "surface":{"border":{"kind":"solid","id":98,"data":0},"borderWidth":2},
             "wall":{"courses":[{"material":{"kind":"solid","id":5,"data":1},"height":1}],"extent":5}}
            """;

        var style = HouseStyleJson.Deserialize(stored);

        await Assert.That(style.Foundation.Depth).IsEqualTo(3);
        await Assert.That(style.Foundation.Deck).IsEqualTo(new SolidMaterial(5));
        await Assert.That(style.Foundation.Footing).IsEqualTo(new SolidMaterial(Blocks.Cobblestone));
        await Assert.That(style.Foundation.Surface.BorderWidth).IsEqualTo(2);
        await Assert.That(style.Wall.Extent).IsEqualTo(5);          // everything beside it is untouched
    }

    /// <summary>The roof's statements were eleven fields beside everything else, and the one named <c>roof</c>
    /// held the material the other ten describe the shape of — so the key changes meaning as well as depth,
    /// from a material to the part that material is the body of. A style stored under the old shape keeps every
    /// one of them, and a field it never named keeps taking the part's own default rather than the record's
    /// old one.</summary>
    [Test]
    public async Task A_style_stored_before_the_roof_was_one_part_reads_forward()
    {
        var stored = """
            {"form":"hip","pitch":2,"overhang":0,"roofHole":true,"ridgeCap":true,
             "roofSlab":44,"roofSlabData":5,
             "roof":{"kind":"solid","id":98,"data":0},
             "verge":{"kind":"solid","id":4,"data":0},
             "gable":{"kind":"solid","id":5,"data":5},
             "gableWindows":{"form":"pane","block":102,"width":2,"height":2,"sill":1},
             "wall":{"courses":[{"material":{"kind":"solid","id":5,"data":1},"height":1}],"extent":5}}
            """;

        var style = HouseStyleJson.Deserialize(stored);

        await Assert.That(style.Roof.Form).IsEqualTo(RoofForm.Hip);
        await Assert.That(style.Roof.Pitch).IsEqualTo(2);
        await Assert.That(style.Roof.Overhang).IsEqualTo(0);
        await Assert.That(style.Roof.Hole).IsTrue();
        await Assert.That(style.Roof.RidgeCap).IsTrue();
        await Assert.That(style.Roof.Slab).IsEqualTo(44);
        await Assert.That(style.Roof.SlabData).IsEqualTo(5);
        await Assert.That(style.Roof.Body).IsEqualTo(new SolidMaterial(98));
        await Assert.That(style.Roof.Verge).IsEqualTo(new SolidMaterial(Blocks.Cobblestone));
        await Assert.That(style.Roof.Gable).IsEqualTo(new SolidMaterial(5, 5));
        await Assert.That(style.Roof.GableWindows.Width).IsEqualTo(2);
        await Assert.That(style.Wall.Extent).IsEqualTo(5);          // everything beside it is untouched
    }

    /// <summary>A roof stored with none of its numbers named keeps the part's defaults rather than a zero for
    /// each — the failure a blanket move would have: a pitch of nothing is a roof with no slope at all.
    /// </summary>
    [Test]
    public async Task A_stored_roof_naming_only_its_material_keeps_the_rest_of_the_parts_defaults()
    {
        var style = HouseStyleJson.Deserialize("""{"roof":{"kind":"solid","id":98,"data":0}}""");

        await Assert.That(style.Roof.Body).IsEqualTo(new SolidMaterial(98));
        await Assert.That(style.Roof.Pitch).IsEqualTo(new RoofStyle().Pitch);
        await Assert.That(style.Roof.Overhang).IsEqualTo(new RoofStyle().Overhang);
        await Assert.That(style.Roof.Form).IsEqualTo(new RoofStyle().Form);
        await Assert.That(style.Roof.Verge).IsEqualTo(new RoofStyle().Verge);
    }

    /// <summary>The way in was four fields beside everything else, and a fifth that only looked like one:
    /// <c>doorEdge</c> is the wall the whole building fronts on — the one a shed roof falls toward and a porch
    /// stands against — so it reads forward as the style's own <c>front</c> rather than into the doorway. A
    /// stored style keeps both.</summary>
    [Test]
    public async Task A_style_stored_before_the_doorway_was_one_part_reads_forward()
    {
        var stored = """
            {"door":"web","doorWidth":3,"doorHeight":4,"doorEdge":"negX",
             "doorHead":{"form":"arched","block":135,"fill":"upperSlab","fillBlock":126,"fillData":2},
             "wall":{"courses":[{"material":{"kind":"solid","id":5,"data":1},"height":1}],"extent":5}}
            """;

        var style = HouseStyleJson.Deserialize(stored);

        await Assert.That(style.Doorway.Door).IsEqualTo(DoorMaterial.Web);
        await Assert.That(style.Doorway.Width).IsEqualTo(3);
        await Assert.That(style.Doorway.Height).IsEqualTo(4);
        await Assert.That(style.Doorway.Head.Form).IsEqualTo(DoorHeadForm.Arched);
        await Assert.That(style.Doorway.Head.FillBlock).IsEqualTo(126);
        await Assert.That(style.Front).IsEqualTo(RoomEdge.NegX);    // the building's, not the doorway's
        await Assert.That(style.Wall.Extent).IsEqualTo(5);
    }

    /// <summary>The air material that used to stand in for no footing reads forward as the state it meant, so
    /// a building seated into terrain still meets the ground flush rather than on a course of air.</summary>
    [Test]
    public async Task A_stored_air_sill_reads_forward_as_no_footing()
    {
        var style = HouseStyleJson.Deserialize(
            """{"sill":{"kind":"solid","id":0,"data":0},"floor":{"courses":[],"extent":1}}""");

        await Assert.That(style.Foundation.Footing).IsNull();
    }

    [Test]
    public async Task A_shipped_style_round_trips_through_its_json()
    {
        foreach (var style in (HouseStyle[])[HouseStyle.Wool, HouseStyle.Spawn])
        {
            var json = HouseStyleJson.Serialize(style);
            var back = HouseStyleJson.Deserialize(json);

            await Assert.That(back).IsEqualTo(style);
            // And it is stable: a re-serialize is the same text, so a save cannot churn a map's layout.
            await Assert.That(HouseStyleJson.Serialize(back)).IsEqualTo(json);
        }
    }

    [Test]
    public async Task Every_course_of_every_part_survives_including_its_material()
    {
        // The wall is the one that would tell: three bedrock, a tint, bedrock, air, bedrock. A stack that came
        // back as one flat course would still stamp a shell, just not the one that was picked.
        var json = HouseStyleJson.Serialize(HouseStyle.Wool);
        var wall = HouseStyleJson.Deserialize(json).Wall;

        await Assert.That(wall.Courses.Count).IsEqualTo(HouseStyle.Wool.Wall.Courses.Count);
        await Assert.That(wall.Extent).IsEqualTo(7);
        await Assert.That(wall.At(3).Material).IsTypeOf<TeamTintedMaterial>();
        await Assert.That(wall.At(5).Material).IsEqualTo(new SolidMaterial(Blocks.Air));
    }

    [Test]
    public async Task The_knobs_read_as_names_rather_than_numbers()
    {
        // A snapshot lives inside a map's layout, where it is read and diffed by people. An ordinal would say
        // nothing about which door a room has.
        var json = HouseStyleJson.Serialize(HouseStyle.Wool with
        {
            Doorway = HouseStyle.Wool.Doorway with { Door = DoorMaterial.Web },
            Roof = HouseStyle.Wool.Roof with { Overhang = 1, Hole = false },
        });

        await Assert.That(json).Contains("\"overhang\":1");
        await Assert.That(json).Contains("\"door\":\"web\"");
        await Assert.That(json).Contains("\"hole\":false");
        // And the derived height is not stored — it is the extents added up.
        await Assert.That(json).DoesNotContain("topLayer");
    }

    [Test]
    public async Task A_style_built_twice_is_the_same_style_collections_included()
    {
        // Record equality compares a member by reference when the member is a collection, so a part built here
        // and a part read back from JSON would never be equal without the walk both now do — and neither would
        // a layer stack or a pattern palette inside one of its courses.
        var once = Stacked();
        var again = Stacked();

        await Assert.That(once).IsEqualTo(again);
        await Assert.That(once.GetHashCode()).IsEqualTo(again.GetHashCode());
        await Assert.That(HouseStyleJson.Deserialize(HouseStyleJson.Serialize(once))).IsEqualTo(once);

        // And a difference inside a collection is still a difference.
        await Assert.That(once with { Wall = RoomPart.Of(new SolidMaterial(Blocks.Stone), 7) }).IsNotEqualTo(once);

        static HouseStyle Stacked() => HouseStyle.Wool with
        {
            Wall = new RoomPart(
            [
                new RoomCourse(new LayeredMaterial(
                [
                    new MaterialLayer(new SolidMaterial(Blocks.Stone), 1),
                    new MaterialLayer(new SolidMaterial(Blocks.Dirt), 2),
                ]), 3),
                new RoomCourse(new WallRunMaterial(
                [
                    new WallStripe(new SolidMaterial(Blocks.QuartzBlock), 2),
                    new WallStripe(new SolidMaterial(Blocks.Bedrock), 1),
                ])),
            ], Extent: 7),
        };
    }

    [Test]
    public async Task An_absent_or_unreadable_snapshot_falls_back_rather_than_throwing()
    {
        // The snapshot is a hand-editable leaf inside a map's layout. A malformed one should cost that map its
        // chosen shell, not its export.
        await Assert.That(HouseStyleJson.DeserializeOr(null, HouseStyle.Spawn)).IsEqualTo(HouseStyle.Spawn);
        await Assert.That(HouseStyleJson.DeserializeOr("", HouseStyle.Spawn)).IsEqualTo(HouseStyle.Spawn);
        await Assert.That(HouseStyleJson.DeserializeOr("{ not json", HouseStyle.Wool)).IsEqualTo(HouseStyle.Wool);
    }
}
