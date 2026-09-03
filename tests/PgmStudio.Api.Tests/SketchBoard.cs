using System.Net.Http.Json;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>A map with geometry and no finish, which is where every test of an addressable sketch part
/// starts: the board exists, and the part being written is the first thing on it.</summary>
internal static class SketchBoard
{
    private const string Layout = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[{"base_y":0,"layout":{
           "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                      "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12}],
           "groups":[{"id":"i","name":"I","shapeIds":["s1"]}]}}]}
        """;

    public const string Slug = "dressed";

    public static async Task<HttpClient> FreshAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        var client = ApiTestFactory.Shared.CreateClient();
        var made = await client.PostAsJsonAsync("/api/map/from-documents", new
        {
            plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
            layout = JsonDocument.Parse(Layout).RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Dressed","authors":[],"contributors":[]}}""").RootElement,
            name = "Dressed",
        });
        made.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>A theme whose four buckets all resolve — the clean shape every case that is about something
    /// else starts from.</summary>
    public static object Theme(int surface = 2) => new
    {
        rim = new { material = new { kind = "solid", id = 4 }, depth = 1 },
        surface = new { material = new { kind = "solid", id = surface }, depth = 1 },
        wall = new { kind = "solid", id = 1 },
        fill = new { kind = "solid", id = 1 },
    };
}
