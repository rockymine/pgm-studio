using PgmStudio.Analysis.Playability;
using PgmStudio.Api.Services;
using PgmStudio.Export;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// <see cref="CoverageReads"/> — a built board's coverage walked once for the world it was built from and the
/// document it is judged by, so the JSON and the picture of one board are one walk, and a changed document is
/// walked again.
/// </summary>
public sealed class CoverageReadsTests
{
    private const string Plate = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
            {"id":"a","type":"rectangle","operation":"add","min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"base_height":6}],
          "groups":[{"id":"i1","name":"Plate","mirrors":false,"shapeIds":["a"]}]} }]}
        """;

    [Test]
    public async Task A_world_and_its_document_are_walked_once_and_a_changed_document_again()
    {
        var world = BuiltWorlds.Of(Plate, new MapIntent());
        Dict doc = new() { ["name"] = "m", ["version"] = "1.0.0" };
        var walks = 0;
        GroundCoverage.Result Walk(Dict judged)
        {
            walks++;
            return GroundCoverage.Read(judged, BuiltWalk.Ground(world, judged), [], []);
        }

        var first = CoverageReads.Of(world, doc, () => Walk(doc));
        var again = CoverageReads.Of(world, new Dict { ["name"] = "m", ["version"] = "1.0.0" }, () => Walk(doc));
        await Assert.That(walks).IsEqualTo(1).Because("the same world judged by the same document is one walk");
        await Assert.That(again).IsSameReferenceAs(first);

        Dict renamed = new() { ["name"] = "n", ["version"] = "1.0.0" };
        CoverageReads.Of(world, renamed, () => Walk(renamed));
        await Assert.That(walks).IsEqualTo(2).Because("a changed document is another key");
    }
}
