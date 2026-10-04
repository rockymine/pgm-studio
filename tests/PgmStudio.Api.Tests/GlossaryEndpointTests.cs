using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary><c>GET /api/glossary</c> — the words a rule uses, served for a reader to look up before a run.</summary>
[NotInParallel("api-db")]
public sealed class GlossaryEndpointTests
{
    private sealed record Row(string Term, string Definition, List<string> AlsoCalled, List<string> Related);

    private static async Task<List<Row>> GlossaryAsync(string query = "")
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var resp = await client.GetAsync($"/api/glossary{query}");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<List<Row>>(new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
    }

    [Test]
    public async Task Every_term_is_served()
    {
        var served = await GlossaryAsync();

        await Assert.That(served.Select(row => row.Term)).IsEquivalentTo(PgmStudio.Vocabulary.Glossary.Terms.Select(term => term.Term));
    }

    /// <summary>A reader meets the older name in a field or a document and asks for that; the answer is the
    /// term it now goes by.</summary>
    [Test]
    public async Task Another_name_answers_its_term()
    {
        var row = (await GlossaryAsync("?term=build%20zone")).Single();

        await Assert.That(row.Term).IsEqualTo("build region");
        await Assert.That(row.AlsoCalled).Contains("build zone");
    }

    [Test]
    public async Task A_word_that_is_not_defined_is_an_empty_list()
    {
        await Assert.That(await GlossaryAsync("?term=zzz")).IsEmpty();
    }
}
