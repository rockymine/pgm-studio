using System.Net;
using System.Net.Http.Json;
using PgmStudio.Contracts;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Compose;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The browse feed's HTTP surface (G117): GET /api/compose composes boards ahead, sieves them, renders each
/// to SVG and advances a seed cursor; POST /api/compose/pin re-composes a card from its descriptor and stores
/// it as a generated row (idempotent), which the G119 tray endpoint then lists; GET /api/plans/{id}/svg
/// re-renders a stored plan. Runs against <c>pgm_studio_test</c>; each test resets the schema.
/// </summary>
[NotInParallel("api-db")]
public sealed class ComposeEndpointsTests
{
    [Test]
    public async Task Browse_returns_cards_with_svg_and_advances_the_cursor()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<ComposePage>("/api/compose?players=12&symmetry=rot_180&seedStart=0&count=3");
        await Assert.That(page!.Cards.Count).IsEqualTo(3);
        await Assert.That(page.NextSeed).IsGreaterThan(0);

        var c = page.Cards[0];
        await Assert.That(c.Svg).Contains("<svg");
        await Assert.That(c.Descriptor.ComposerVersion).IsEqualTo(ComposerVersion.Current)
            .Because("the card is stamped with the composer that made it, not a literal");
        await Assert.That(c.WoolCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Unsupported_symmetry_is_400()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/compose?players=12&symmetry=rot_90");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Structural_sieve_wools_must_include_hub_any_of()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        // wools=donut → every card includes a donut wool (must-include); the page reports the scan honestly.
        var donut = await client.GetFromJsonAsync<ComposePage>(
            "/api/compose?players=20&symmetry=rot_180&seedStart=0&count=3&wools=donut");
        await Assert.That(donut!.Cards.All(c => c.Structure.Wools.Contains("donut"))).IsTrue();
        await Assert.That(donut.Scanned).IsGreaterThanOrEqualTo(donut.Cards.Count);

        // hub=ring → every card's hub form is ring (any-of over a single value).
        var ring = await client.GetFromJsonAsync<ComposePage>(
            "/api/compose?players=20&symmetry=rot_180&seedStart=0&count=3&hub=ring");
        await Assert.That(ring!.Cards.All(c => c.Structure.Hub == "ring")).IsTrue();
    }

    /// <summary>The census the filter chips read: counted over the boards a page <b>composed</b>, not the ones
    /// it matched, so picking one form never hides the others. Without that the chips would report every
    /// alternative as unavailable the moment a filter was on.</summary>
    [Test]
    public async Task The_structural_census_counts_composed_boards_not_matched_ones()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<ComposePage>(
            "/api/compose?players=20&symmetry=rot_180&seedStart=0&count=3&hub=ring");
        var observed = page!.Observed;

        await Assert.That(observed).IsNotNull();
        await Assert.That(observed!.Boards).IsGreaterThan(page.Cards.Count)
            .Because("the sieve rejected boards, and the census counted them anyway");
        await Assert.That(observed.Hubs.Keys.Count).IsGreaterThan(1)
            .Because("filtering to ring must not erase the other hub forms from the census");
        await Assert.That(observed.Hubs.Values.Sum()).IsEqualTo(observed.Boards)
            .Because("every board contributes exactly one hub form");
        await Assert.That(observed.Frontlines.Values.Sum()).IsEqualTo(observed.Boards);
    }

    /// <summary>The census is what lets a chip say "this request does not make one". An 8-player board's hub is
    /// too small for the wide forms, so they never appear however many seeds are scanned — which is a different
    /// answer from an unlucky run, and only the census can tell them apart.</summary>
    [Test]
    public async Task A_form_the_request_cannot_produce_stays_absent_from_the_census()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<ComposePage>(
            "/api/compose?players=8&symmetry=rot_180&seedStart=0&count=8&hub=twin");
        var observed = page!.Observed!;

        await Assert.That(page.Cards).IsEmpty();
        await Assert.That(observed.Boards).IsGreaterThan(100).Because("the structural scan budget ran");
        await Assert.That(observed.Hubs.GetValueOrDefault("twin")).IsEqualTo(0)
            .Because($"an 8-player hub has no room for two legs and a bay; saw {string.Join(", ",
                observed.Hubs.Select(h => $"{h.Key}:{h.Value}"))}");
        await Assert.That(observed.Hubs.GetValueOrDefault("single")).IsGreaterThan(0)
            .Because("the one-legged branch is the form that size does produce");
    }

    [Test]
    public async Task MaxScore_sieve_excludes_higher_scored_boards()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<ComposePage>(
            "/api/compose?players=12&symmetry=rot_180&seedStart=0&count=8&maxScore=0.5");
        await Assert.That(page!.Cards.All(card => card.Score <= 0.5)).IsTrue();
    }

    [Test]
    public async Task Pin_persists_a_generated_row_is_idempotent_and_renders()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<ComposePage>("/api/compose?players=12&symmetry=rot_180&seedStart=0&count=1");
        var card = page!.Cards[0];

        var pinned1 = await (await client.PostAsJsonAsync("/api/compose/pin", card.Descriptor)).Content.ReadFromJsonAsync<PlanDetail>();
        await Assert.That(pinned1!.Origin).IsEqualTo("generated");

        var pinned2 = await (await client.PostAsJsonAsync("/api/compose/pin", card.Descriptor)).Content.ReadFromJsonAsync<PlanDetail>();
        await Assert.That(pinned2!.Id).IsEqualTo(pinned1.Id);   // dedup by content hash

        var tray = await client.GetFromJsonAsync<List<PlanSummary>>("/api/plans?origin=generated");
        await Assert.That(tray!.Count).IsEqualTo(1);
        await Assert.That(tray[0].Descriptor).IsNotNull();       // parsed descriptor exposed for the tray
        await Assert.That(tray[0].Descriptor!.Seed).IsEqualTo(card.Descriptor.Seed);

        var svg = await client.GetFromJsonAsync<SvgDto>($"/api/plans/{pinned1.Id}/svg");
        await Assert.That(svg!.Svg).Contains("<svg");

        await Assert.That(tray[0].StaleComposer).IsFalse()
            .Because("it was pinned by the composer that is running");
    }

    /// <summary>A row an older composer made is flagged. Its stored plan is untouched — geometry is stored, not
    /// recomputed — but its descriptor has stopped reproducing it, and nothing said so before.</summary>
    [Test]
    public async Task A_row_from_an_older_composer_reads_stale()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<ComposePage>("/api/compose?players=12&symmetry=rot_180&seedStart=0&count=1");
        var current = page!.Cards[0].Descriptor;
        var pinned = await (await client.PostAsJsonAsync("/api/compose/pin", current)).Content.ReadFromJsonAsync<PlanDetail>();

        // age the stored row's stamp — the same state a row pinned before a pipeline change is left in
        await ApiTestFactory.ExecuteAsync($"UPDATE plan SET composer_version = 'box-0' WHERE id = {pinned!.Id}");

        var listed = (await client.GetFromJsonAsync<List<PlanSummary>>("/api/plans?origin=generated"))!.Single();
        await Assert.That(listed.ComposerVersion).IsEqualTo("box-0");
        await Assert.That(listed.StaleComposer).IsTrue();

        // an authored row has no descriptor to go stale, whatever it is stamped with
        var authoredJson = new PlanModel { Meta = new PlanMeta { Name = "hand-drawn" } }.ToJson();
        await client.PostAsJsonAsync("/api/plans", new PlanSaveRequest(authoredJson, null));
        var authored = (await client.GetFromJsonAsync<List<PlanSummary>>("/api/plans?origin=authored"))!.Single();
        await Assert.That(authored.StaleComposer).IsFalse();
    }

    private sealed record SvgDto(string Svg);
}
