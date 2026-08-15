using System.Text.Json.Nodes;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Export.Tests;

/// <summary>
/// <b>A seed teaches the shape it is written in.</b> <c>ruediger.layout.json</c> is the worked example
/// <c>docs/tools/plan.md</c> calls "the one to read first", so a retired field in it is not a stale file —
/// it is a lesson in a model that no longer exists, given to every reader and every agent that opens it.
///
/// <para>The readers upgrade a stored document in place, which is what makes the rot silent: the seed kept
/// loading, kept painting something, and nothing anywhere said that what it painted was not what it asked
/// for. Its voronoi asked for three fills and got one flat band, because a <c>palette</c> of fills to pick
/// between became a <c>bands</c> ramp measured inward from the cell edge and the upgrade can only carry the
/// first entry across. The map never showed the pattern it was written to show.</para>
///
/// <para>So this asserts the seed needs <b>no upgrade at all</b>, using the same unread-field walk
/// <c>RQ3</c> reports on the wire (<c>B214</c>): after the reader has taken everything it understands, and
/// after every upgrade has claimed the names it carries forward, nothing may be left over. A field that is
/// left over is one the author wrote and nothing read.</para>
/// </summary>
public sealed class SeedThemeShapeTests
{
    private static JsonObject Themes()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "tools", "seeds", "ruediger.layout.json");
            if (File.Exists(candidate))
                return (JsonObject)JsonNode.Parse(File.ReadAllText(candidate))!["themes"]!;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException("ruediger.layout.json not found above the test binary");
    }

    [Test]
    public async Task Every_theme_in_the_worked_example_is_written_in_the_current_model()
    {
        var stale = new List<string>();
        foreach (var (name, node) in Themes())
        {
            TerrainThemeJson.Deserialize(node!.ToJsonString(), out var unread);
            stale.AddRange(unread.Select(field => $"{name}.{field}"));
        }

        await Assert.That(stale).IsEmpty();
    }

    /// <summary>And the pattern it asks for is one that can be seen. A voronoi's bands are a ramp inward from
    /// the cell boundary, so a cell has to be wide enough to reach past its own rim before the ramp means
    /// anything — at the cell size this seed carried, every band was one block and the surface came out as
    /// noise. Three bands over twelve-block cells is a rim, a ring and a middle, and the rim is two blocks
    /// deep so a cut through the surface reads as cells rather than as a hairline.</summary>
    [Test]
    public async Task The_voronoi_draws_cells_rather_than_one_flat_fill()
    {
        var surface = TerrainThemeJson.Deserialize(Themes()["ruediger"]!.ToJsonString()).Surface.Material;

        var voronoi = (VoronoiMaterial)surface;
        await Assert.That(voronoi.Bands.Count).IsEqualTo(3);
        await Assert.That(voronoi.Bands[0].Depth).IsGreaterThanOrEqualTo(2);

        // Every band actually reaches ground: a ramp whose inner bands never show is a flat fill wearing a
        // list, which is exactly what the upgrade had left behind.
        var drawn = new HashSet<int>();
        for (var x = 0; x < 120; x++)
            for (var z = 0; z < 120; z++)
                drawn.Add(voronoi.Resolve(new BucketContext { X = x, Y = 64, Z = z }).Id);

        await Assert.That(drawn.Count).IsEqualTo(3);
    }
}
