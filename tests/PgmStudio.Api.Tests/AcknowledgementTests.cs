using System.Net.Http.Json;
using System.Text.Json;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A route that answers <see cref="AppliedDto"/> answers <b>exactly</b> <c>{}</c>.
///
/// <para>Declaring a shape and sending another is the failure this catches, and nothing else does: the
/// schema tests read what an operation <i>says</i> it answers, the record tests read what the record
/// serializes to, and a handler that builds a body by hand satisfies both while sending something the
/// caller's parser has no field for. The response is compared as text rather than deserialized, because a
/// deserializer with unmapped members allowed would read an extra key as an empty record and pass.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class AcknowledgementTests
{
    /// <summary>The acknowledgement routes a bare map is enough to reach: a metadata patch, a symmetry
    /// confirmation and a document replace, which are three different operations behind one answer.</summary>
    [Test]
    public async Task A_write_that_answers_the_acknowledgement_answers_an_empty_object()
    {
        using var client = await SeedAsync("ackmap");

        await Empty(client.PatchAsJsonAsync("/api/map/ackmap/metadata", new { name = "Ack Map" }));
        await Empty(client.PatchAsJsonAsync("/api/map/ackmap/symmetry", new { status = "none" }));
        // The plan is posted under the keys the reader keeps: a document write answers the acknowledgement
        // alone only when no gate remarked, and a field the reader has nowhere to put is an RQ3 complaint
        // riding under `warnings` on the same success.
        await Empty(client.PutAsJsonAsync("/api/map/ackmap/plan",
            new { plan = 1, meta = new { name = "Ack Map" }, pieces = Array.Empty<object>() }));
    }

    private static async Task Empty(Task<HttpResponseMessage> call)
    {
        var resp = await call;
        var body = await resp.Content.ReadAsStringAsync();
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(body);
        await Assert.That(body).IsEqualTo(JsonSerializer.Serialize(new AppliedDto()));
    }

    /// <summary>Reset the test schema, seed one empty map, and return a client onto it.</summary>
    private static async Task<HttpClient> SeedAsync(string slug)
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using (var db = new PgmDb(PgmDataOptions.ForConnectionString(ApiTestFactory.ConnectionString)))
        {
            await new MapRepository(db).InsertAsync(new MapRow
            {
                Slug = slug, Name = "Ack Map", Version = "1.0.0", Gamemode = "ctw",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
        }
        return ApiTestFactory.Shared.CreateClient();
    }
}
