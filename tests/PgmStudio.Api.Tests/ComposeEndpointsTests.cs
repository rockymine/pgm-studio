using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PgmStudio.Contracts;
using PgmStudio.Data.Compose;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Compose;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The browse feed's HTTP surface over the composed-board library: GET /api/compose reads a page of the
/// boards the library holds, best score first, filters them as queries and ends where the library does, and
/// composes nothing on request; POST /api/compose/map makes a map of the stored board a card names, labelled for the
/// player count asked, as a sketch with a shape per box. Runs against <c>pgm_studio_test</c>; each test resets the
/// schema and fills the few boards it reads with the library's own fill.
/// </summary>
[NotInParallel("api-db")]
public sealed class ComposeEndpointsTests
{
    private static async Task FillAsync(string band, int perBand, string symmetry = "rot_180")
    {
        await using var db = new PgmDb(PgmDataOptions.ForConnectionString(ApiTestFactory.ConnectionString));
        await ComposedBoardLibrary.FillAsync(new ComposedBoardStore(db), perBand, [band], [symmetry], _ => { });
    }

    [Test]
    public async Task A_page_is_read_from_the_library_best_score_first_and_ends_where_the_library_does()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await FillAsync("nano", 5);
        using var client = ApiTestFactory.Shared.CreateClient();

        var first = await client.GetFromJsonAsync<ComposePage>("/api/compose?players=10&symmetry=rot_180&count=3");
        await Assert.That(first!.Cards.Count).IsEqualTo(3);
        await Assert.That(first.Matching).IsEqualTo(5);
        await Assert.That(first.Next).IsEqualTo(3);
        await Assert.That(first.End).IsFalse();

        var rest = await client.GetFromJsonAsync<ComposePage>($"/api/compose?players=10&symmetry=rot_180&from={first.Next}&count=3");
        await Assert.That(rest!.Cards.Count).IsEqualTo(2);
        await Assert.That(rest.End).IsTrue().Because("the library holds five boards and nothing is composed past them");

        var cards = first.Cards.Concat(rest.Cards).ToList();
        var ordered = cards.OrderBy(card => card.Score).ThenBy(card => card.Descriptor.Seed).ToList();
        await Assert.That(cards.Select(card => card.Descriptor.Seed)).IsEquivalentTo(ordered.Select(card => card.Descriptor.Seed))
            .Because("best score first, the seed breaking ties");

        var card = cards[0];
        await Assert.That(card.Svg).Contains("<svg");
        await Assert.That(first.Key!.Select(entry => entry.Label)).Contains("Build zone")
            .Because("a card's picture carries no key, so the page carries it");
        await Assert.That(card.Descriptor.Players).IsEqualTo(10).Because("a card is labelled for the players asked");
        await Assert.That(card.Descriptor.ComposerVersion).IsEqualTo(ComposerVersion.Current)
            .Because("the card is stamped with the composer that made it, not a literal");
        await Assert.That(card.WoolCount).IsGreaterThan(0);
    }

    [Test]
    public async Task An_empty_library_answers_an_empty_page_and_composes_nothing()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var page = await client.GetFromJsonAsync<ComposePage>("/api/compose?players=12&symmetry=rot_180");
        await Assert.That(page!.Cards).IsEmpty();
        await Assert.That(page.End).IsTrue();
        await Assert.That(page.Observed!.Boards).IsEqualTo(0);

        await using var db = new PgmDb(PgmDataOptions.ForConnectionString(ApiTestFactory.ConnectionString));
        await Assert.That(db.ComposedBoards.Count()).IsEqualTo(0).Because("a browse is a read");
    }

    [Test]
    public async Task Filling_again_composes_only_what_is_missing()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var db = new PgmDb(PgmDataOptions.ForConnectionString(ApiTestFactory.ConnectionString));
        var store = new ComposedBoardStore(db);

        var first = await ComposedBoardLibrary.FillAsync(store, 3, ["nano"], ["rot_180"], _ => { });
        var again = await ComposedBoardLibrary.FillAsync(store, 4, ["nano"], ["rot_180"], _ => { });

        await Assert.That(first).IsEqualTo(3);
        await Assert.That(again).IsEqualTo(1);
        await Assert.That(db.ComposedBoards.Count()).IsEqualTo(4);
    }

    [Test]
    public async Task Unsupported_symmetry_is_400()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.GetAsync("/api/compose?players=12&symmetry=rot_90");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [Arguments(1)]
    [Arguments(3)]
    [Arguments(4)]
    public async Task A_team_count_the_library_does_not_hold_is_400(int teams)
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.GetAsync($"/api/compose?players=16&teams={teams}&symmetry=rot_180&count=1");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest)
            .Because("a team count the library does not hold is refused, not answered with a two-team board");

        var body = await resp.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("teams")
            .Because("the refusal names the field it is about, the way the symmetry refusal does");
    }

    /// <summary>A filter is a query over the library: every card it answers has the form asked for, and it
    /// counts exactly the boards the census says hold that form.</summary>
    [Test]
    public async Task Filters_answer_the_boards_that_hold_the_form_and_count_them()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await FillAsync("micro", 12);
        using var client = ApiTestFactory.Shared.CreateClient();

        var all = await client.GetFromJsonAsync<ComposePage>("/api/compose?players=16&symmetry=rot_180&count=48");
        var census = all!.Observed!;
        var hub = census.Hubs.OrderBy(pair => pair.Value).First().Key;
        var family = census.Wools.OrderBy(pair => pair.Value).First().Key;

        var byHub = await client.GetFromJsonAsync<ComposePage>($"/api/compose?players=16&symmetry=rot_180&count=48&hub={hub}");
        await Assert.That(byHub!.Cards.All(card => card.Structure.Hub == hub)).IsTrue();
        await Assert.That(byHub.Matching).IsEqualTo(census.Hubs[hub]);

        var byWool = await client.GetFromJsonAsync<ComposePage>($"/api/compose?players=16&symmetry=rot_180&count=48&wools={family}");
        await Assert.That(byWool!.Cards.All(card => card.Structure.Wools.Contains(family))).IsTrue();
        await Assert.That(byWool.Matching).IsEqualTo(census.Wools[family]);

        var median = all.Cards.Select(card => card.Score).Order().ElementAt(all.Cards.Count / 2);
        var byScore = await client.GetFromJsonAsync<ComposePage>(
            $"/api/compose?players=16&symmetry=rot_180&count=48&maxScore={median.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        await Assert.That(byScore!.Cards.All(card => card.Score <= median)).IsTrue();
        await Assert.That(byScore.Matching).IsEqualTo(all.Cards.Count(card => card.Score <= median));
    }

    /// <summary>The census the filter chips read counts every board the library holds for the band and symmetry,
    /// before the filters, so picking one form never hides the others.</summary>
    [Test]
    public async Task The_census_counts_every_board_held_not_the_ones_matched()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await FillAsync("micro", 12);
        using var client = ApiTestFactory.Shared.CreateClient();

        var survey = await client.GetFromJsonAsync<ComposePage>("/api/compose?players=16&symmetry=rot_180&count=1");
        var minority = survey!.Observed!.Hubs.OrderBy(pair => pair.Value).ThenBy(pair => pair.Key).First().Key;
        var page = await client.GetFromJsonAsync<ComposePage>($"/api/compose?players=16&symmetry=rot_180&count=1&hub={minority}");
        var observed = page!.Observed!;

        await Assert.That(observed.Boards).IsEqualTo(12);
        await Assert.That(page.Matching).IsLessThan(observed.Boards);
        await Assert.That(observed.Hubs.Values.Sum()).IsEqualTo(observed.Boards)
            .Because("every board contributes exactly one hub form");
        await Assert.That(observed.Frontlines.Values.Sum()).IsEqualTo(observed.Boards);
    }

    /// <summary>A family the composer never builds stays at zero in the census, which is what lets a chip say
    /// "these settings do not make one" rather than "none yet".</summary>
    [Test]
    public async Task A_form_the_settings_do_not_make_stays_absent_from_the_census()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await FillAsync("nano", 20);
        using var client = ApiTestFactory.Shared.CreateClient();

        var page = await client.GetFromJsonAsync<ComposePage>("/api/compose?players=8&symmetry=rot_180&count=8&wools=scythe");
        var observed = page!.Observed!;

        await Assert.That(page.Cards).IsEmpty();
        await Assert.That(observed.Boards).IsEqualTo(20);
        await Assert.That(observed.Wools.GetValueOrDefault("scythe")).IsEqualTo(0);
        await Assert.That(observed.Wools.Values.Sum()).IsGreaterThan(0);
    }

    /// <summary>While the running composer's library is still being filled, the feed shows the boards the
    /// version before it made, and a card from them keeps the board it showed.</summary>
    [Test]
    public async Task The_feed_shows_the_previous_version_while_the_current_one_is_filled()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await FillAsync("nano", 3);
        await ApiTestFactory.ExecuteAsync("UPDATE composed_board SET composer_version = 'earlier-1'");
        await FillAsync("nano", 1);
        using var client = ApiTestFactory.Shared.CreateClient();

        var page = await client.GetFromJsonAsync<ComposePage>("/api/compose?players=12&symmetry=rot_180&count=12");
        await Assert.That(page!.Cards.Count).IsEqualTo(3);
        await Assert.That(page.Cards.All(card => card.Descriptor.ComposerVersion == "earlier-1")).IsTrue();

        var made = await client.PostAsJsonAsync("/api/compose/map", page.Cards[0].Descriptor);
        await Assert.That(made.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    /// <summary>A map made of a card is the stored board as a sketch: one shape per box of the team unit, named for
    /// the box, all in the one mirrored group; the intent labelled for the players asked; and the boxes as places,
    /// one per box at every orbit image.</summary>
    [Test]
    public async Task A_map_made_of_a_card_is_the_board_as_a_sketch_with_a_shape_per_box()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await FillAsync("nano", 1);
        using var client = ApiTestFactory.Shared.CreateClient();

        var card = (await client.GetFromJsonAsync<ComposePage>("/api/compose?players=10&symmetry=rot_180&count=1"))!.Cards[0];
        var made = await (await client.PostAsJsonAsync("/api/compose/map", card.Descriptor)).Content.ReadFromJsonAsync<OriginatedDto>();
        await Assert.That(made!.Slug).IsEqualTo($"composed-p10-t2-{card.Descriptor.Seed}");

        var layout = JsonDocument.Parse(await client.GetStringAsync($"/api/map/{made.Slug}/sketch")).RootElement;
        var ground = layout.GetProperty("layers")[0].GetProperty("layout");
        var added = ground.GetProperty("shapes").EnumerateArray()
            .Where(shape => shape.GetProperty("operation").GetString() == "add"
                            && !(shape.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.String))
            .Select(shape => shape.GetProperty("id").GetString()!).ToList();
        foreach (var box in new[] { "hub-", "spawn-", "wool-a-" })
            await Assert.That(added.Count(id => id.StartsWith(box, StringComparison.Ordinal))).IsGreaterThanOrEqualTo(1)
                .Because($"the {box.TrimEnd('-')} box is a shape of its own");
        await Assert.That(added.Any(id => id.StartsWith("hub-t", StringComparison.Ordinal))).IsFalse()
            .Because("a shape is named for its box, not for the first of its pieces");
        var team = ground.GetProperty("groups").EnumerateArray().Single(group => group.GetProperty("id").GetString() == "team");
        var grouped = team.GetProperty("shapeIds").EnumerateArray().Select(id => id.GetString()).ToHashSet();
        await Assert.That(added.All(grouped.Contains)).IsTrue()
            .Because($"the board's shapes stay one group, so the unit moves as a whole: {string.Join(", ", added)}");

        var intent = JsonDocument.Parse(await client.GetStringAsync($"/api/map/{made.Slug}/intent")).RootElement;
        await Assert.That(intent.GetProperty("maxPlayers").GetInt32()).IsEqualTo(10);
        await Assert.That(intent.GetProperty("meta").GetProperty("name").GetString()).IsEqualTo($"Composed p10 t2 #{card.Descriptor.Seed}");

        var places = (await client.GetFromJsonAsync<PlacesDocument>($"/api/map/{made.Slug}/places"))!.Places!;
        await Assert.That(places.Select(place => place.Id)).Contains("red-hub");
        await Assert.That(places.Select(place => place.Id)).Contains("blue-hub");
        await Assert.That(places.All(place => place.Shape == "box" && place.Holds is null)).IsTrue();

        var second = await (await client.PostAsJsonAsync("/api/compose/map", card.Descriptor)).Content.ReadFromJsonAsync<OriginatedDto>();
        await Assert.That(second!.Slug).IsEqualTo($"{made.Slug}-2").Because("a second map of one board is a second map");
    }

    [Test]
    public async Task A_map_of_a_board_the_library_does_not_hold_is_404()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await FillAsync("nano", 1);
        using var client = ApiTestFactory.Shared.CreateClient();

        var descriptor = new ComposeRequestDto(12, 2, "rot_180", ComposeRequest.DefaultCell, 999, ComposerVersion.Current,
            ComposeDescriptor.CurrentSchema);
        var resp = await client.PostAsJsonAsync("/api/compose/map", descriptor);
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
