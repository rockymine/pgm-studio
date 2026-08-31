using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PgmStudio.Api.Services;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The seed's own round trip. A seeder is only as good as it, and <see cref="LibrarySeed.VerifyAsync"/> was
/// written to ask the question and had nothing asking it: a preset that stores cleanly today grows a beam or a
/// laid-log roof tomorrow and starts arriving back as a quieter building than it left, with nothing failing.
/// Runs against <c>pgm_studio_test</c>; resets the schema, so it runs serially with the rest.
/// </summary>
[NotInParallel("api-db")]
public sealed class LibrarySeedTests
{
    /// <summary>The seeder over the host's own stores, so it reads the database the host just seeded rather
    /// than a second connection with its own idea of what is there.</summary>
    private static LibrarySeed Seed(IServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<ThemeStore>(),
        scope.ServiceProvider.GetRequiredService<RoomStyleStore>(),
        scope.ServiceProvider.GetRequiredService<HousePartStore>());

    /// <summary>
    /// What the store loses, house by house — pinned rather than asserted empty, because four of the shipped
    /// presets lose window and door knobs today and this is the check that finally says which
    /// (<c>TL12</c>). Pinning it is what stops the gap spreading: a preset that starts losing something new,
    /// or a house that joins the list, fails here.
    /// </summary>
    [Test]
    public async Task No_preset_loses_more_through_the_store_than_it_does_today()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var seed = Seed(scope);
        await seed.SeedAsync();

        // The row model lags the stamper by exactly these: a window seated per storey and a doorway's width.
        var known = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["cottage"] = ["windows", "storey 1 windows"],
            ["longhouse"] = ["windows", "doorWidth", "storey 1 windows"],
            ["terrace"] = ["storey 1 windows"],
            ["counting house"] = ["storey 1 windows", "storey 3 windows"],
            ["workshop"] = ["doorWidth"],
        };

        foreach (var (house, lost) in await seed.VerifyAsync())
            await Assert.That(lost).IsEquivalentTo(known.GetValueOrDefault(house, []))
                .Because($"{house} lost {(lost.Count == 0 ? "nothing" : string.Join(", ", lost))} through the store");
    }

    /// <summary>The hand-authored houses compose back to exactly the buildings they went in as. Stated apart
    /// from the pin above because this is the claim that must never soften: a generated preset can be
    /// regenerated, and these three cannot.</summary>
    [Test]
    public async Task Every_authored_house_composes_back_to_its_preset()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var seed = Seed(scope);
        await seed.SeedAsync();

        var authored = HousePresets.Authored.Select(house => house.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (house, lost) in (await seed.VerifyAsync()).Where(entry => authored.Contains(entry.House)))
            await Assert.That(lost).IsEmpty().Because($"{house} did not survive the store");
    }

    /// <summary>The hand-authored set is in the library after a seed, under the names it was authored as.
    /// It is the one part of the seed that is somebody's work rather than a generated preset, so losing a name
    /// is losing the thing — there is nothing to re-derive it from.</summary>
    [Test]
    public async Task The_authored_set_is_seeded_under_its_own_names()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        await Seed(scope).SeedAsync();

        var styles = await client.GetFromJsonAsync<List<PgmStudio.Contracts.StyleDto>>("/api/styles");
        var names = styles!.Select(style => style.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, _) in StylePresets.All)
            await Assert.That(names.Contains(name)).IsTrue().Because($"{name} is not in the seeded library");

        var houses = await client.GetFromJsonAsync<List<PgmStudio.Contracts.RoomStyleSummary>>("/api/room-styles");
        var built = houses!.Select(house => house.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var house in HousePresets.Authored)
            await Assert.That(built.Contains(house.Name)).IsTrue().Because($"{house.Name} is not in the seeded library");
    }

    /// <summary>Seeding twice adds nothing the second time. A library is something an author edits, so a seeder
    /// that created a second copy of every row on every start would bury their work in duplicates.</summary>
    [Test]
    public async Task Seeding_twice_adds_nothing_the_second_time()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var seed = Seed(scope);
        await seed.SeedAsync();

        var again = await seed.SeedAsync();
        await Assert.That(again.StylesAdded).IsEqualTo(0);
        await Assert.That(again.RoomsAdded).IsEqualTo(0);
        await Assert.That(again.ThemesAdded).IsEqualTo(0);
    }

    /// <summary>Two library rows whose names differ only by case seed without throwing. The seeder matches a
    /// name case-insensitively, so the grouping that decides which row is already there has to read a name the
    /// same way the lookup does — a pair that groups as two keys and collides as one takes the whole startup
    /// down with it, since the seed runs at app start (<c>RP61</c>).</summary>
    [Test]
    public async Task Two_rows_named_alike_but_for_case_do_not_collide()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var _ = ApiTestFactory.Shared.CreateClient();
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var themes = scope.ServiceProvider.GetRequiredService<ThemeStore>();

        var (name, material) = StylePresets.All.First();
        await themes.CreateStyleAsync(new StyleRow { Name = name.ToLowerInvariant(), Kind = "solid", Params = "{}" });
        await themes.CreateStyleAsync(new StyleRow { Name = name.ToUpperInvariant(), Kind = "solid", Params = "{}" });

        await Seed(scope).SeedAsync();

        var stored = await themes.ListStylesAsync(ct: default);
        await Assert.That(stored.Count(style => string.Equals(style.Name, name, StringComparison.OrdinalIgnoreCase)))
            .IsEqualTo(2).Because("the seeder binds one of the two and adds no third");
    }
}
