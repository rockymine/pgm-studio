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
            "Beams", "Door", "DoorHead", "DoorHeight", "DoorWidth", "Floor", "Form", "Gable",
            "GableWindows", "Overhang", "Pitch",
            "Porch", "Post", "RidgeCap", "Roof", "RoofHole", "RoofSlab", "RoofSlabData", "Sill", "Storeys",
            "Surface", "Verge", "Wall", "Windows",
        }.Order().ToList());
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
            Overhang = 1, Door = DoorMaterial.Web, RoofHole = false,
        });

        await Assert.That(json).Contains("\"overhang\":1");
        await Assert.That(json).Contains("\"door\":\"web\"");
        await Assert.That(json).Contains("\"roofHole\":false");
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
