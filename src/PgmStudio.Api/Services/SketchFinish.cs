using System.Text;
using PgmStudio.Analysis.Footprint;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>What rasterizing a stored drawing came to: either a refusal — the status it is answered under, the
/// gate's short label and the findings — or the geometry that was written, with whatever the board complained
/// about on the way. The complaints ride on the success because this is the last stage that can still say what
/// the board names and does not have.</summary>
/// <param name="Cells">How many ground columns the drawing rasterized to.</param>
/// <param name="Islands">How many landmasses those columns fall into.</param>
public sealed record SketchFinished(
    Refusal? Refusal, int Cells = 0, int Islands = 0, Findings? Complaints = null);

/// <summary>
/// Declaring a drawing done: rasterize the stored layout into world geometry, detect its landmasses, write
/// both, and move the map to Configure. The one stage transition the studio performs at runtime.
///
/// <para>An operation rather than a handler, because two callers reach it — the route an author's canvas
/// posts to, and the load that reconstitutes a map from the documents it was built from. The gates are here
/// rather than in front of it, so which caller came through cannot change what the drawing is judged by.</para>
///
/// <para>It answers findings instead of writing a response: the layer that speaks HTTP is the one that
/// renders the envelope and hands the complaints on.</para>
/// </summary>
public static class SketchFinish
{
    public static async Task<SketchFinished> RunAsync(
        long mapId, MapRepository repo, MapArtifactStore artifacts, WorldFeatureWriter writer,
        CancellationToken ct)
    {
        var data = await artifacts.LoadAsync(mapId, ArtifactKind.SketchLayoutJson, ct);
        if (data is null)
            return Refuse(422, "nothing to finish", new Finding(SketchRules.NothingStored,
                "this map has no stored sketch layout, so there is no drawing to rasterize"));

        // The document's own gate, run where the drawing is declared done: what the board names and does not
        // have contributes no ground, so an island the author expected can be missing from the artifacts this
        // writes.
        var layoutJson = Encoding.UTF8.GetString(data);
        var stated = SketchLayout.Stated(layoutJson);
        var checkedBoard = SketchLayoutCheck.Check(stated);
        if (checkedBoard.Refuses) return Refuse(422, "board too large", [.. checkedBoard.Refusals]);

        // A board carrying no finish at all is the one thing the earlier gates cannot see: each of them needs
        // something stated to disagree with, and a board that states none of it slips between them.
        if (SketchLayoutCheck.Unfinished(stated) is { } bare)
            checkedBoard = new Findings([.. checkedBoard, bare]);

        var cells = SketchRasterizer.RasterizeColumns(layoutJson);
        var islands = IslandDetector.Detect(cells.Select(cell => (cell.X, cell.Z)), minIslandSize: 1);
        if (islands.Count == 0)
            return Refuse(422, "nothing is drawn", new Finding(SketchRules.NothingDrawn,
                "the stored layout rasterizes to no ground at all — draw a shape that encloses some"));

        await writer.WriteSketchAsync(mapId, cells, islands, ct);
        await repo.SetStageAsync(mapId, MapStage.Configure, ct);   // the draft has geometry → ready to configure
        return new(null, cells.Count, islands.Count, checkedBoard);
    }

    private static SketchFinished Refuse(int status, string error, params Finding[] findings) =>
        new(Refusal.At(status, error, findings));
}
