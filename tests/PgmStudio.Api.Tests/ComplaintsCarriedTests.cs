using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A complaint a gate produced reaches the caller on every surface that ran the gate, and it does so without
/// the endpoint having decided to pass it on.
///
/// <para>A dropped complaint fails nothing on its own — the status is the same, the body is still valid, and
/// the board simply reads as cleaner than it is — so the guarantee has to be asserted over the whole set
/// rather than route by route. A sketch endpoint added tomorrow gets it from the pipeline; one that somehow
/// answers outside the pipeline fails here.</para>
///
/// <para>The board is one good rectangle plus one shape stating a kind nobody draws. It builds — that is the
/// point of a complaint — and the drawing is one shape smaller than the document asked for.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class ComplaintsCarriedTests
{
    private const string Layout = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[{"base_y":0,"layout":{
           "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                      "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12},
                     {"id":"s2","type":"blob","operation":"add",
                      "min_x":-5,"max_x":5,"min_z":-5,"max_z":5,"floor":8,"base_height":12}],
           "groups":[{"id":"i","name":"I","shapeIds":["s1","s2"]}]}}]}
        """;

    [Test]
    public async Task Every_sketch_surface_that_runs_the_document_gate_answers_its_complaints()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Named nothing" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var stored = await client.PutAsync($"/api/map/{slug}/sketch", Body());
        await Assert.That(stored.IsSuccessStatusCode).IsTrue();
        await Rules(stored, "PUT /sketch");

        foreach (var route in (string[])["paint", "columns", "relief", "relief/read"])
            await Rules(await client.PostAsync($"/api/map/{slug}/sketch/{route}", Body()), route);

        // Finish reads the stored layout rather than a posted one, and is the stage that says the drawing is
        // done — the last point at which "one of your shapes drew nothing" is still worth hearing.
        await Rules(await client.PostAsync($"/api/map/{slug}/sketch/finish", null), "finish");
    }

    /// <summary>The complaint is on the <b>success</b>, under one key, whatever the body around it is —
    /// a dict of pixels, a list of islands, a slug and a URL.</summary>
    private static async Task Rules(HttpResponseMessage response, string route)
    {
        await Assert.That(response.IsSuccessStatusCode).IsTrue()
            .Because($"{route} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.TryGetProperty("warnings", out var warnings)).IsTrue()
            .Because($"{route} ran the document gate and the board states a kind nobody draws");
        await Assert.That(warnings.EnumerateArray()
                .Any(finding => finding.GetProperty("rule").GetString() == "SK3"
                             && finding.GetProperty("severity").GetString() == "complaint"))
            .IsTrue().Because($"{route} answered {warnings}");
    }

    private static StringContent Body() => new(Layout, Encoding.UTF8, "application/json");
}
