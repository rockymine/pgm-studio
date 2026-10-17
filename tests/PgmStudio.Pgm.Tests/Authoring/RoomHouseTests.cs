using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>
/// A room's house: wings whose corners are their first and last cells, the first of them the room. A house over
/// a max-exclusive rectangle reads back as that rectangle, the bounds run round every wing, and a mapping moves
/// each wing while keeping what it states about itself.
/// </summary>
public sealed class RoomHouseTests
{
    [Test]
    public async Task A_house_over_a_rectangle_is_one_wing_of_its_first_and_last_cells()
    {
        var house = RoomHouse.Over(new Rect(2, 3, 8, 9));

        await Assert.That(house.Wings.Single().Corners.Select(corner => (corner[0], corner[1])))
            .IsEquivalentTo(new[] { (2.0, 3.0), (7.0, 8.0) });
        await Assert.That(house.Room).IsEqualTo(new Rect(2, 3, 8, 9));
        await Assert.That(house.Bounds).IsEqualTo(new Rect(2, 3, 8, 9));
    }

    [Test]
    public async Task The_room_is_the_first_wing_and_the_bounds_run_round_them_all()
    {
        var house = new RoomHouse { Wings = [.. RoomHouse.Over(new Rect(0, 0, 6, 6)).Wings, .. RoomHouse.Over(new Rect(6, 2, 10, 6)).Wings] };

        await Assert.That(house.Room).IsEqualTo(new Rect(0, 0, 6, 6));
        await Assert.That(house.Bounds).IsEqualTo(new Rect(0, 0, 10, 6));
        await Assert.That(new RoomHouse().Room).IsNull();
    }

    [Test]
    public async Task A_mapping_moves_every_wing_and_keeps_its_spec()
    {
        var tall = RoomHouse.Over(new Rect(0, 0, 6, 6)).Wings[0] with { Spec = new Domain.WingSpec(StoreysHigh: 2) };
        var house = new RoomHouse { Wings = [tall] };

        var moved = house.Mapped(rect => new Rect(rect.MinX + 3, rect.MinZ - 1, rect.MaxX + 3, rect.MaxZ - 1));

        await Assert.That(moved.Room).IsEqualTo(new Rect(3, -1, 9, 5));
        await Assert.That(moved.Wings[0].Spec.StoreysHigh).IsEqualTo(2);
    }

    [Test]
    public async Task A_wings_words_are_written_as_words_whatever_the_options()
    {
        var wing = RoomHouse.Over(new Rect(0, 0, 6, 6)).Wings[0]
            with { Spec = new Domain.WingSpec(Form: Domain.RoofForm.Hip, Ridge: Domain.RidgeAxis.AlongX) };
        var json = System.Text.Json.JsonSerializer.Serialize(new RoomHouse { Wings = [wing] },
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        await Assert.That(json).Contains("\"ridge\":\"alongX\"");
        await Assert.That(json).Contains("\"form\":\"hip\"");
    }

    [Test]
    public async Task A_stated_null_style_reads_back_as_null_and_an_absent_one_stays_absent()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        var open = System.Text.Json.JsonSerializer.Deserialize<RoomHouse>("""{"wings":[],"style":null}""", options)!;
        var unstated = System.Text.Json.JsonSerializer.Deserialize<RoomHouse>("""{"wings":[]}""", options)!;

        await Assert.That(open.Style.ValueKind).IsEqualTo(System.Text.Json.JsonValueKind.Null);
        await Assert.That(System.Text.Json.JsonSerializer.Serialize(open, options)).Contains("\"style\":null");
        await Assert.That(unstated.Style.ValueKind).IsEqualTo(System.Text.Json.JsonValueKind.Undefined);
        await Assert.That(System.Text.Json.JsonSerializer.Serialize(unstated, options)).DoesNotContain("style");
    }
}
