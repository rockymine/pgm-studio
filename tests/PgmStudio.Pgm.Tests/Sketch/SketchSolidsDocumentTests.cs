using System.Text.RegularExpressions;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>The worked board in <c>docs/tools/sketch.md</c>'s <i>Solids</i> section, read out of the document: it
/// parses, the gate names none of its solids, and it builds the ground the prose says it does.</summary>
public sealed class SketchSolidsDocumentTests
{
    private static string DocumentedBoard()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docs", "tools", "sketch.md")))
            directory = directory.Parent;
        var markdown = File.ReadAllText(Path.Combine(directory!.FullName, "docs", "tools", "sketch.md"));
        var section = markdown[markdown.IndexOf("### Solids: shapes that stand in the block grid", StringComparison.Ordinal)..];
        return Regex.Match(section, @"```json PUT /api/map/\{slug\}/sketch\r?\n(?<body>.*?)\r?\n```", RegexOptions.Singleline)
                    .Groups["body"].Value;
    }

    [Test]
    public async Task The_documented_board_parses_and_the_gate_names_none_of_its_solids()
    {
        var layout = SketchLayout.Parse(DocumentedBoard())!;

        await Assert.That(layout.Layers!.SelectMany(layer => layer.Shapes).Select(shape => shape.Type).Distinct())
            .IsEquivalentTo(["rectangle", "box", "sphere", "torus", "wedge", "cylinder", "arch"]);
        var findings = SketchLayoutCheck.Check(layout);
        await Assert.That(findings.Where(finding => finding.Rule is SketchRules.NamesNothing or SketchRules.DrawsNothing
                                                    or SketchRules.DrawsNoArea or SketchRules.UnbuildableHeight)).IsEmpty();
    }

    [Test]
    public async Task The_documented_board_builds_what_the_prose_says()
    {
        var columns = SketchRasterizer.RasterizeColumns(SketchLayout.Parse(DocumentedBoard()));

        var ground = columns.Where(segment => segment.Layer == "ground").ToList();
        await Assert.That(ground.Single(segment => segment.X == 8 && segment.Z == 0).YTop).IsEqualTo(22)
            .Because("the monolith stands from 8 to 22, one mass with the plate under it");
        await Assert.That(ground.Max(segment => segment.YTop)).IsEqualTo(26).Because("the ring is its 16 blocks from the plate");
        await Assert.That(ground.GroupBy(segment => segment.Cell).Any(column => column.Count() > 1)).IsTrue()
            .Because("a column through the ring's hole has two runs");
        var span = columns.Where(segment => segment.Layer == "span").ToList();
        await Assert.That(span.Max(segment => segment.YTop)).IsEqualTo(23).Because("the deck at 22 and a block of surface");
        await Assert.That(span.Where(segment => segment.YFloor < 10).Any()).IsTrue().Because("the feet reach the plate at 10");
    }
}
