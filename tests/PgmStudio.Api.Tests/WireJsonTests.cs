using System.Text.RegularExpressions;
using NJsonSchema;
using NSwag;

namespace PgmStudio.Api.Tests;

/// <summary>
/// What a route answers is what the document says it answers (<c>PgmStudio.Api.Endpoints.WireJson</c>).
///
/// <para>A client generated from <c>/api/openapi/v1.json</c> trusts two things: that every value it is sent
/// validates against the schema the route names, and that every key it is sent is one the schema declares.
/// Both are asserted here over the answers themselves rather than over the document, because the document
/// can be complete and still describe a different wire — a closed set listed as words and written as numbers,
/// a field a record computes and the generator never lists.</para>
///
/// <para>The board states one of everything that crosses as a closed set: every prop kind, a recipe of each
/// kind, both room shells, a theme and a biome. Every GET route that a map's slug reaches, or that takes no
/// address at all, is called on it, and every answer that is a JSON object or list is held to its
/// schema.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class WireJsonTests
{
    private const string Slug = "wire";

    /// <summary>The count of things an answer says that its schema does not, and it only moves down.</summary>
    private const int StillUntrue = 0;

    private const string Board = """
        {"layout":{"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
          "layers":[{"id":"ground","base_y":0,"layout":{
            "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                       "min_x":-30,"max_x":30,"min_z":-30,"max_z":30,"floor":8,"base_height":12}],
            "groups":[{"id":"i","name":"I","shapeIds":["s1"]}]}}],
          "themes":{"heath":{"surface":{"material":{"kind":"solid","id":2},"depth":1},
                             "wall":{"kind":"solid","id":1},"fill":{"kind":"solid","id":1}}},
          "mapTheme":"heath",
          "biome":{"kind":"solid","id":4},
          "roomStyles":{"wool":{},"spawn":{}},
          "dressing":{"props":[
            {"id":"road","kind":"stroke","seed":1,"points":[[-20,-20],[-10,-20]],"radius":1.5,"style":"rough"},
            {"id":"beck","kind":"fluid","seed":2,"shape":"channel","form":"natural","fluid":"water",
             "points":[[-25,10],[25,12]],"radius":2,"depth":1},
            {"id":"bothy","kind":"house","seed":3,"front":"posX","style":"croft",
             "wings":[{"corners":[[10,-25],[17,-19]],"spec":{"storeysHigh":1,"form":"gable","ridge":"alongX"}}]},
            {"id":"chest","kind":"chest","seed":4,"x":0,"z":0,"facing":"negZ"},
            {"id":"tree-1","kind":"tree","seed":5,"x":20,"z":20,"style":"oak"},
            {"id":"stone-1","kind":"boulder","seed":6,"x":-20,"z":20,"style":"stone"}],
           "styles":{"oak":{"kind":"tree","form":"template"},"stone":{"kind":"boulder","form":"round"},
                     "croft":{"kind":"house","shell":{}}}}},
         "plan":{"pieces":[]},
         "intent":{"meta":{"name":"Wire","authors":[],"contributors":[]}},
         "name":"Wire"}
        """;

    [Test]
    public async Task Every_answer_is_what_its_schema_says()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var stored = await client.PutAsync($"/api/map/{Slug}/source",
            new StringContent(Board, System.Text.Encoding.UTF8, "application/json"));
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());

        var document = await OpenApiDocument.FromJsonAsync(await client.GetStringAsync("/api/openapi/v1.json"));
        var untrue = new List<string>();
        var answered = 0;
        foreach (var (path, item) in document.Paths)
        {
            if (!item.TryGetValue("get", out var operation)) continue;
            if (Regex.Matches(path, @"\{(\w+)\}").Any(parameter => parameter.Groups[1].Value != "slug")) continue;
            if (!operation.Responses.TryGetValue("200", out var ok)
                || !ok.Content.TryGetValue("application/json", out var media)) continue;

            var response = await client.GetAsync(path.Replace("{slug}", Slug));
            if (!response.IsSuccessStatusCode
                || response.Content.Headers.ContentType?.MediaType?.Contains("json") != true) continue;
            answered++;

            var body = await response.Content.ReadAsStringAsync();
            untrue.AddRange(SchemaTruth.Untrue(body, media.Schema)
                .Select(said => $"{path} {said}"));
        }

        await Assert.That(answered).IsGreaterThan(60).Because("a board that answers nothing proves nothing");
        await Assert.That(untrue.Count).IsLessThanOrEqualTo(StillUntrue)
            .Because($"{untrue.Count} thing(s) answered are not what the schema says:{Environment.NewLine}  "
                     + string.Join($"{Environment.NewLine}  ", untrue.Take(80)));
    }
}
