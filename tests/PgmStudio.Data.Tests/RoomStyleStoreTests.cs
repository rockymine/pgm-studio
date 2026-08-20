using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Vocabulary;

namespace PgmStudio.Data.Tests;

/// <summary>
/// The room-style library store (M0012, G34b). What separates it from its theme sibling is the one thing worth
/// asserting: a part takes an <em>ordered stack</em> of styles rather than a single one, so the stack has to
/// come back in the order it went in and a rewrite has to replace it wholesale. Runs against
/// <c>pgm_studio_test</c>; each test resets the schema.
/// </summary>
[NotInParallel]
public sealed class RoomStyleStoreTests
{
    private static RoomStyleCourseRow Course(string part, int ordinal, long styleId, int height = 1)
        => new() { Part = part, Ordinal = ordinal, StyleId = styleId, Height = height };

    [Test]
    public async Task A_parts_courses_come_back_in_stack_order()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var styles = new ThemeStore(db);
        var store = new RoomStyleStore(db);

        var bedrock = await styles.CreateStyleAsync(new StyleRow { Name = "bedrock", Kind = MaterialKind.Solid, Params = "{}" });
        var band = await styles.CreateStyleAsync(new StyleRow { Name = "band", Kind = MaterialKind.TeamTint, Params = "{}" });

        var id = await store.CreateAsync(new RoomStyleRow { Name = "banded", WallHeight = 7 },
        [
            Course(RoomParts.Wall, 0, bedrock, 3),
            Course(RoomParts.Wall, 1, band),
            Course(RoomParts.Wall, 2, bedrock),
            Course(RoomParts.Roof, 0, bedrock),
        ]);

        var wall = (await store.GetCoursesAsync(id)).Where(c => c.Part == RoomParts.Wall).ToList();
        await Assert.That(wall.Select(c => c.Ordinal)).IsEquivalentTo(new[] { 0, 1, 2 });
        await Assert.That(wall[0].StyleId).IsEqualTo(bedrock);
        await Assert.That(wall[0].Height).IsEqualTo(3);
        await Assert.That(wall[1].StyleId).IsEqualTo(band);
    }

    [Test]
    public async Task Saving_replaces_a_stack_rather_than_adding_to_it()
    {
        // The stack is what the author edited, so a save is a replacement. Merging would leave the courses a
        // removed one used to occupy, and the unique (room, part, ordinal) index would refuse the next save.
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var styles = new ThemeStore(db);
        var store = new RoomStyleStore(db);

        var stone = await styles.CreateStyleAsync(new StyleRow { Name = "stone", Kind = MaterialKind.Solid, Params = "{}" });
        var id = await store.CreateAsync(new RoomStyleRow { Name = "plain" },
            [Course(RoomParts.Wall, 0, stone), Course(RoomParts.Wall, 1, stone), Course(RoomParts.Wall, 2, stone)]);

        var replaced = await store.UpdateAsync(id, new RoomStyleRow { Name = "plainer", WallHeight = 9 },
            [Course(RoomParts.Wall, 0, stone)]);

        await Assert.That(replaced).IsTrue();
        await Assert.That((await store.GetCoursesAsync(id)).Count).IsEqualTo(1);
        await Assert.That((await store.GetAsync(id))!.WallHeight).IsEqualTo(9);
        await Assert.That((await store.GetAsync(id))!.Name).IsEqualTo("plainer");
    }

    [Test]
    public async Task Updating_an_unknown_room_style_reports_it_rather_than_creating_one()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();

        await Assert.That(await new RoomStyleStore(db).UpdateAsync(404, new RoomStyleRow { Name = "ghost" }, []))
            .IsFalse();
    }

    [Test]
    public async Task Deleting_a_room_style_takes_its_courses_and_leaves_the_styles()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var styles = new ThemeStore(db);
        var store = new RoomStyleStore(db);

        var stone = await styles.CreateStyleAsync(new StyleRow { Name = "stone", Kind = MaterialKind.Solid, Params = "{}" });
        var id = await store.CreateAsync(new RoomStyleRow { Name = "plain" }, [Course(RoomParts.Wall, 0, stone)]);

        await store.DeleteAsync(id);
        await Assert.That((await store.GetCoursesAsync(id)).Count).IsEqualTo(0);   // courses cascaded
        await Assert.That((await styles.ListStylesAsync()).Count).IsEqualTo(1);    // the style survives
    }

    [Test]
    public async Task A_style_a_room_binds_is_named_before_it_can_be_forgotten()
    {
        // A style is shared by both libraries, so "what would break" has to include the rooms — otherwise the
        // theme half reports nothing and the delete fails on the foreign key instead.
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var styles = new ThemeStore(db);
        var store = new RoomStyleStore(db);

        var stone = await styles.CreateStyleAsync(new StyleRow { Name = "stone", Kind = MaterialKind.Solid, Params = "{}" });
        await store.CreateAsync(new RoomStyleRow { Name = "bunker" }, [Course(RoomParts.Wall, 0, stone)]);

        await Assert.That(await store.UsingStyleAsync(stone)).IsEquivalentTo(new[] { "bunker" });
        await Assert.That(await styles.ThemesUsingStyleAsync(stone)).IsEmpty();
    }
}
