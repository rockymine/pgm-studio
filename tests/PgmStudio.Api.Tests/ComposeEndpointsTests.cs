using System.Net;
using System.Net.Http.Json;
using PgmStudio.Contracts;

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
        await Assert.That(c.Descriptor.ComposerVersion).IsEqualTo("box-1");
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
    }

    private sealed record SvgDto(string Svg);
}
