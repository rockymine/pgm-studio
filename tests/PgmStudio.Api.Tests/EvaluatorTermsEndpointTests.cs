using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>GET /api/rules/terms</c> — the evaluator's numeric surface: every term, with the band it is scoring
/// against right now and where that band came from. The catalogue beside it answers what a rule means; this
/// is what stops the numbers being reachable only by reading source.
/// </summary>
[NotInParallel("api-db")]
public sealed class EvaluatorTermsEndpointTests
{
    private sealed record Row(string Term, string Rule, string Kind, double[]? Band, string BandSource, bool? LearnsFromTraced);

    private static async Task<List<Row>> TermsAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var resp = await client.GetAsync("/api/rules/terms");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<List<Row>>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
    }

    [Test]
    public async Task An_authored_band_is_served_as_the_terms_own_ruling()
    {
        // GO1's band is stated on the term, not learned — the endpoint says so and serves the numbers the
        // scorer actually uses.
        var goalRatio = (await TermsAsync()).Single(term => term.Term == "goal-spawn-ratio");

        await Assert.That(goalRatio.Rule).IsEqualTo("GO1");
        await Assert.That(goalRatio.Kind).IsEqualTo("soft");
        await Assert.That(goalRatio.BandSource).IsEqualTo("authored");
        await Assert.That(goalRatio.Band).IsEquivalentTo(new[] { 3.0, 4.0 });
        await Assert.That(goalRatio.LearnsFromTraced).IsEqualTo(false);
    }

    [Test]
    public async Task A_learned_band_is_served_from_the_envelopes_the_scorer_reads()
    {
        var fillRatio = (await TermsAsync()).Single(term => term.Term == "fill-ratio");

        await Assert.That(fillRatio.BandSource).IsEqualTo("envelope");
        await Assert.That(fillRatio.Band).IsNotNull();
        await Assert.That(fillRatio.Band![0]).IsLessThan(fillRatio.Band![1]);
    }

    [Test]
    public async Task A_hard_term_has_no_band_because_it_refuses_rather_than_scores()
    {
        var terms = await TermsAsync();
        var hard = terms.Where(term => term.Kind == "hard").ToList();

        await Assert.That(hard).IsNotEmpty();
        await Assert.That(hard.All(term => term.Band is null && term.BandSource == "none")).IsTrue();
        // and every term of either kind cites a rule the catalogue can answer for
        await Assert.That(terms.All(term => term.Rule.Length > 0)).IsTrue();
    }
}
