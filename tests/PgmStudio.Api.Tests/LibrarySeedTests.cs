using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PgmStudio.Api.Endpoints;
using PgmStudio.Api.Services;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Library;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The seed (<see cref="LibrarySeed"/>): a fresh library is exactly the seed folder — each pattern under its name,
/// each house composing back to its file, each theme to its file, each copied tree to its cut — no single block
/// is a pattern, a part many houses share is one row, and seeding again changes nothing. Runs against
/// <c>pgm_studio_test</c>; resets the schema, so it runs serially with the rest.
/// </summary>
[NotInParallel("api-db")]
public sealed class LibrarySeedTests
{
    /// <summary>The seeder over the host's own stores, so it reads the database the host just seeded rather
    /// than a second connection with its own idea of what is there.</summary>
    private static LibrarySeed Seed(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<LibrarySeed>();

    [Test]
    public async Task The_seeded_library_is_the_seed_folder()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        await Seed(scope).SeedAsync();
        var styles = scope.ServiceProvider.GetRequiredService<ThemeStore>();

        var patterns = (await styles.ListStylesAsync()).Select(row => (row.Name, row.Params)).ToList();
        await Assert.That(patterns).IsEquivalentTo(
            SeedFolder.Patterns.Select(entry => (entry.Name, TerrainThemeJson.Serialize(entry.Material))));
        await Assert.That(patterns.Where(row => Slots.IsBlockKind(TerrainThemeComposer.KindOf(
            TerrainThemeJson.DeserializeMaterial(row.Params))))).IsEmpty().Because("a single block is never a pattern");

        var rooms = scope.ServiceProvider.GetRequiredService<RoomStyleStore>();
        var library = new RoomStyleLibrary(rooms, scope.ServiceProvider.GetRequiredService<HousePartStore>(), styles);
        var stored = (await rooms.ListAsync()).ToDictionary(room => room.Name, room => room.Id, StringComparer.OrdinalIgnoreCase);
        await Assert.That(stored.Keys).IsEquivalentTo(SeedFolder.Houses.Select(house => house.Name));
        foreach (var (name, style) in SeedFolder.Houses)
        {
            var back = await library.ComposeAsync(stored[name]);
            await Assert.That(HouseStyleJson.Serialize(back!)).IsEqualTo(HouseStyleJson.Serialize(style))
                .Because($"{name} came back from the store as another building");
        }

        var themes = new ThemeLibrary(styles);
        var finishes = (await styles.ListThemesAsync()).ToDictionary(theme => theme.Name, theme => theme.Id);
        await Assert.That(finishes.Keys).IsEquivalentTo(SeedFolder.Themes.Select(theme => theme.Name));
        foreach (var (name, theme) in SeedFolder.Themes)
            await Assert.That(await themes.ComposeJsonAsync(finishes[name])).IsEqualTo(TerrainThemeJson.Serialize(theme))
                .Because($"{name} came back from the store as another finish");

        var props = scope.ServiceProvider.GetRequiredService<PropStyleStore>();
        var trees = await props.ListTreesAsync();
        foreach (var tree in SeedFolder.Trees.Trees)
        {
            var row = trees.SingleOrDefault(row => row.CutX == tree.Foot.X && row.CutY == tree.Foot.Y && row.CutZ == tree.Foot.Z);
            await Assert.That(row?.Name).IsEqualTo(tree.Name);
            await Assert.That(row!.Body).IsEqualTo(JsonSerializer.Serialize(tree.Style.Body));
        }
        await Assert.That((await props.ListBouldersAsync()).Select(row => row.Name))
            .IsEquivalentTo(SeedFolder.Boulders.Select(boulder => boulder.Name));
    }

    /// <summary><b>Every house composes back to the building it went in as</b>, field by field, so a house that
    /// starts losing a knob fails here whichever knob it is.</summary>
    [Test]
    public async Task No_house_loses_anything_through_the_store()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var seed = Seed(scope);
        await seed.SeedAsync();

        foreach (var (house, lost) in await seed.VerifyAsync())
            await Assert.That(lost).IsEmpty()
                .Because($"{house} lost {string.Join(", ", lost)} through the store");
    }

    /// <summary>A storey or a roof many houses share is one row, which every house stacking it binds.</summary>
    [Test]
    public async Task A_part_many_houses_share_is_one_row()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        await Seed(scope).SeedAsync();
        var parts = scope.ServiceProvider.GetRequiredService<HousePartStore>();

        var storeyCourses = (await parts.GetAllStoreyCoursesAsync()).ToLookup(course => course.StoreyStyleId);
        var storeys = (await parts.ListStoreysAsync())
            .Select(row => Content(row, storeyCourses[row.Id].Select(course => (course.Part, course.Ordinal,
                course.StyleId, course.BlockId, course.BlockData, course.BlockLaid, course.Height))))
            .ToList();
        await Assert.That(storeys.Distinct().Count()).IsEqualTo(storeys.Count);
        await Assert.That(storeys.Count).IsLessThan(SeedFolder.Houses.Sum(house => house.Style.Storeys.Count))
            .Because("the seeded houses share storeys, and a shared storey is stored once");

        var roofCourses = (await parts.GetAllRoofCoursesAsync()).ToLookup(course => course.RoofStyleId);
        var roofs = (await parts.ListRoofsAsync())
            .Select(row => Content(row, roofCourses[row.Id].Select(course => (course.Part, course.Ordinal,
                course.StyleId, course.BlockId, course.BlockData, course.BlockLaid, course.Height))))
            .ToList();
        await Assert.That(roofs.Distinct().Count()).IsEqualTo(roofs.Count);
        await Assert.That(roofs.Count).IsLessThan(SeedFolder.Houses.Count);
    }

    /// <summary>Seeding twice changes nothing the second time. A library is something an author edits, so a seeder
    /// that created a second copy of every row on every start would bury their work in duplicates.</summary>
    [Test]
    public async Task Seeding_twice_changes_nothing_the_second_time()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var seed = Seed(scope);
        await seed.SeedAsync();

        var again = await seed.SeedAsync();
        await Assert.That((again.PatternsAdded, again.PatternsUpdated, again.PartsAdded, again.PartsUpdated,
                again.HousesAdded, again.ThemesAdded, again.RecipesAdded, again.RecipesUpdated, again.Retired,
                again.Released))
            .IsEqualTo((0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    /// <summary>Every row a fresh seed puts down carries the key of the entry it holds, and no two rows of a kind
    /// carry one key.</summary>
    [Test]
    public async Task Every_seeded_row_carries_its_entrys_key()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        await Seed(scope).SeedAsync();
        var styles = scope.ServiceProvider.GetRequiredService<ThemeStore>();
        var parts = scope.ServiceProvider.GetRequiredService<HousePartStore>();
        var props = scope.ServiceProvider.GetRequiredService<PropStyleStore>();

        List<string?>[] kinds =
        [
            [.. (await styles.ListStylesAsync()).Select(row => row.SeedKey)],
            [.. (await styles.ListThemesAsync()).Select(row => row.SeedKey)],
            [.. (await styles.ListBiomesAsync()).Select(row => row.SeedKey)],
            [.. (await parts.ListRoofsAsync()).Select(row => row.SeedKey)],
            [.. (await parts.ListStoreysAsync()).Select(row => row.SeedKey)],
            [.. (await parts.ListPorchesAsync()).Select(row => row.SeedKey)],
            [.. (await scope.ServiceProvider.GetRequiredService<RoomStyleStore>().ListAsync()).Select(row => row.SeedKey)],
            [.. (await props.ListTreesAsync()).Select(row => row.SeedKey)],
            [.. (await props.ListBouldersAsync()).Select(row => row.SeedKey)],
        ];
        foreach (var seedKeys in kinds)
        {
            await Assert.That(seedKeys).DoesNotContain((string?)null);
            await Assert.That(seedKeys.Distinct().Count()).IsEqualTo(seedKeys.Count);
        }
    }

    /// <summary>A keyed row whose entry has left the folder is deleted where nothing binds it, and handed to its
    /// author — key cleared, content kept — where a theme still binds it.</summary>
    [Test]
    public async Task A_row_whose_entry_left_is_deleted_or_handed_to_its_author()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var styles = scope.ServiceProvider.GetRequiredService<ThemeStore>();
        var props = scope.ServiceProvider.GetRequiredService<PropStyleStore>();
        const string Gone = """{"kind":"noise","seed":91,"scale":2,"octaves":1,"stops":[{"kind":"solid","id":1,"data":0},{"kind":"solid","id":4,"data":0}],"rise":0}""";
        const string Bound = """{"kind":"noise","seed":92,"scale":2,"octaves":1,"stops":[{"kind":"solid","id":1,"data":0},{"kind":"solid","id":4,"data":0}],"rise":0}""";

        var loose = await styles.CreateStyleAsync(new StyleRow { Name = "gone", SeedKey = "gone", Kind = "noise", Params = Gone });
        var held = await styles.CreateStyleAsync(new StyleRow { Name = "kept", SeedKey = "kept", Kind = "noise", Params = Bound });
        await styles.CreateThemeAsync(new ThemeRow { Name = "mine" }, [new ThemeBucketRow { Bucket = "wall", StyleId = held }]);
        var boulder = await props.CreateBoulderAsync(new BoulderStyleRow { Name = "retired-rock", SeedKey = "retired-rock" });

        var tally = await Seed(scope).SeedAsync();

        await Assert.That(await styles.GetStyleAsync(loose)).IsNull();
        await Assert.That(await props.GetBoulderAsync(boulder)).IsNull();
        var kept = await styles.GetStyleAsync(held);
        await Assert.That(kept?.SeedKey).IsNull();
        await Assert.That(kept!.Params).IsEqualTo(Bound);
        await Assert.That((tally.Retired, tally.Released)).IsEqualTo((2, 1));
    }

    /// <summary>An author's row holding what a seeded entry holds is not taken once the entry's own row carries its
    /// key: the key is held, so the author's row stays theirs.</summary>
    [Test]
    public async Task An_authors_row_is_not_taken_while_the_entrys_row_stands()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var styles = scope.ServiceProvider.GetRequiredService<ThemeStore>();
        await Seed(scope).SeedAsync();

        var (_, material) = SeedFolder.Patterns.First();
        var mine = await styles.CreateStyleAsync(new StyleRow
        {
            Name = "mine", Kind = TerrainThemeComposer.KindOf(material), Params = TerrainThemeJson.Serialize(material),
        });
        await Seed(scope).SeedAsync();

        var row = await styles.GetStyleAsync(mine);
        await Assert.That(row?.SeedKey).IsNull();
        await Assert.That(row!.Name).IsEqualTo("mine");
    }

    /// <summary>A row already holding a seeded pattern is that pattern: it takes the seeded name and the entry's key
    /// rather than the seed adding a second row beside it.</summary>
    [Test]
    public async Task A_row_holding_a_seeded_pattern_takes_its_name()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var styles = scope.ServiceProvider.GetRequiredService<ThemeStore>();

        var (name, material) = SeedFolder.Patterns.Last();
        var id = await styles.CreateStyleAsync(new StyleRow
        {
            Name = "my field", Kind = TerrainThemeComposer.KindOf(material), Params = TerrainThemeJson.Serialize(material),
        });
        await Seed(scope).SeedAsync();

        var stored = await styles.ListStylesAsync();
        await Assert.That(stored.Count).IsEqualTo(SeedFolder.Patterns.Count);
        await Assert.That(stored.Single(row => row.Id == id).Name).IsEqualTo(name);
        await Assert.That(stored.Single(row => row.Id == id).SeedKey).IsEqualTo(name);
    }

    /// <summary>Every row the seed puts down carries a library name (<see cref="LibraryNaming.Valid"/>).</summary>
    [Test]
    public async Task Every_seeded_name_is_a_library_name()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        await Seed(scope).SeedAsync();
        var styles = scope.ServiceProvider.GetRequiredService<ThemeStore>();
        var parts = scope.ServiceProvider.GetRequiredService<HousePartStore>();
        var props = scope.ServiceProvider.GetRequiredService<PropStyleStore>();

        List<string> names =
        [
            .. (await styles.ListStylesAsync()).Select(row => row.Name),
            .. (await styles.ListThemesAsync()).Select(row => row.Name),
            .. (await styles.ListBiomesAsync()).Select(row => row.Name),
            .. (await parts.ListRoofsAsync()).Select(row => row.Name),
            .. (await parts.ListStoreysAsync()).Select(row => row.Name),
            .. (await parts.ListPorchesAsync()).Select(row => row.Name),
            .. (await scope.ServiceProvider.GetRequiredService<RoomStyleStore>().ListAsync()).Select(row => row.Name),
            .. (await props.ListTreesAsync()).Select(row => row.Name),
            .. (await props.ListBouldersAsync()).Select(row => row.Name),
        ];
        await Assert.That(names.Where(name => !LibraryNaming.Valid(name))).IsEmpty();
    }

    /// <summary>A name an author's row already carries is theirs: the seeded part that would have taken it takes
    /// the first free count after it, and the author's row keeps its name and what it holds.</summary>
    [Test]
    public async Task A_seeded_name_an_authors_row_carries_is_counted_on()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var parts = scope.ServiceProvider.GetRequiredService<HousePartStore>();
        var mine = await parts.CreateRoofAsync(new RoofStyleRow { Name = "andesite-gable-roof", Pitch = 3 }, []);

        await Seed(scope).SeedAsync();

        var roofs = await parts.ListRoofsAsync();
        await Assert.That(roofs.Single(row => row.Id == mine).Name).IsEqualTo("andesite-gable-roof");
        await Assert.That(roofs.Select(row => row.Name)).Contains("andesite-gable-roof-2");
    }

    /// <summary>What a part row holds, as one string: every column but its id, name, seed key and creation time, and its
    /// courses in stack order.</summary>
    private static string Content<T>(T row, IEnumerable<(string, int, long?, int?, int, bool, int)> courses)
    {
        var columns = JsonSerializer.SerializeToNode(row)!.AsObject();
        foreach (var identity in new[] { "Id", "Name", "SeedKey", "CreatedAt" }) columns.Remove(identity);
        return columns.ToJsonString() + string.Join(";", courses.Order());
    }
}
