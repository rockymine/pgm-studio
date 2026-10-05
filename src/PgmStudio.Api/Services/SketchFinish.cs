using System.Text;
using System.Text.Json.Nodes;
using PgmStudio.Analysis.Footprint;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>What rasterizing a stored drawing came to: either a refusal — the status it is answered under, the
/// gate's short label and the findings — or the geometry that was written, with whatever the board complained
/// about on the way. The complaints ride on the success because this is the last stage that can still say what
/// the board names and does not have. <para><b>Cells</b> — How many ground columns the drawing rasterized
/// to.</para>
/// <para><b>Islands</b> — How many landmasses those columns fall into.</para></summary>
public sealed record SketchFinished(
    Refusal? Refusal, int Cells = 0, int Islands = 0, Findings? Complaints = null);

/// <summary>A drawing judged and rasterized, before anything about it is written: either the refusal finishing
/// it would answer, or the columns and landmasses a finish writes, with what the board complained about.</summary>
public sealed record SketchPrepared(
    Refusal? Refusal, IReadOnlyList<ColumnSegment> Cells, IReadOnlyList<IslandDetector.Island> Islands,
    Findings Judged);

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
        // The revision is read before the bytes, so a write landing between the two leaves the scan behind
        // rather than ahead: the next read refreshes it.
        var revision = await artifacts.RevisionAsync(mapId, ArtifactKind.SketchLayoutJson, ct) ?? 0;
        var data = await artifacts.LoadAsync(mapId, ArtifactKind.SketchLayoutJson, ct);
        if (data is null)
            return Refuse(422, "nothing to finish", new Finding(SketchRules.NothingStored,
                "the map has no stored sketch layout"));

        var prepared = Prepare(Encoding.UTF8.GetString(data), await artifacts.LoadAsync(mapId, ArtifactKind.PlanJson, ct));
        return await WriteAsync(mapId, prepared, revision, repo, writer, ct);
    }

    /// <summary>Everything finishing a drawing can be refused for, and the geometry it would write, decided
    /// from the documents alone. A caller about to replace a stored map asks this first, so a drawing the
    /// finish would refuse never costs the board it was meant to replace.</summary>
    public static SketchPrepared Prepare(string layoutJson, byte[]? planBytes)
    {
        var checkedBoard = Judge(layoutJson, planBytes);
        if (checkedBoard.Refuses)
            return Refused(Refusal.At(422, "the board cannot be built as drawn", [.. checkedBoard.Refusals]));

        var cells = SketchRasterizer.RasterizeColumns(layoutJson);
        var islands = IslandDetector.Detect(cells.Select(cell => (cell.X, cell.Z)), minIslandSize: 1);
        if (islands.Count == 0)
            return Refused(Refusal.At(422, "nothing is drawn", new Finding(SketchRules.NothingDrawn,
                "the stored layout has no shape that draws ground")));
        return new(null, cells, islands, checkedBoard);

        static SketchPrepared Refused(Refusal refusal) => new(refusal, [], [], Findings.None);
    }

    /// <summary>Write what <see cref="Prepare"/> rasterized as the map's geometry, recording the layout
    /// revision it was drawn from, and move the map to Configure.</summary>
    public static async Task<SketchFinished> WriteAsync(
        long mapId, SketchPrepared prepared, long layoutRevision, MapRepository repo, WorldFeatureWriter writer,
        CancellationToken ct)
    {
        if (prepared.Refusal is { } refusal) return new(refusal);
        await writer.WriteSketchAsync(mapId, prepared.Cells, prepared.Islands, layoutRevision, ct);
        await repo.SetStageAsync(mapId, MapStage.Configure, ct);   // the draft has geometry → ready to configure
        return new(null, prepared.Cells.Count, prepared.Islands.Count, prepared.Judged);
    }

    /// <summary>What a drawing is judged by when it is declared done. Finish refuses on it; the findings list
    /// asks it of the layout as stored, so a board edited after Finish is judged as it is drawn now.
    ///
    /// <para>The document's own gate first: what the board names and does not have contributes no ground, so an
    /// island the author expected can be missing. Then a board carrying no finish at all, which the gate cannot
    /// see because each of its checks needs something stated to disagree with. Then the plan's straits, re-read
    /// off the ground rather than off the rectangles they were checked against — only a board drawn from a plan
    /// has any, since the pairs come from the plan's roles and build regions.</para></summary>
    public static Findings Judge(string layoutJson, byte[]? planBytes)
    {
        var stated = SketchLayout.Stated(layoutJson);
        var judged = SketchLayoutCheck.Check(stated);
        if (judged.Refuses) return judged;
        if (SketchLayoutCheck.Unfinished(stated) is { } bare) judged = new Findings([.. judged, bare]);
        if (planBytes is { Length: > 0 })
        {
            var moved = StraitReadback.Check(PlanModel.Stated(Encoding.UTF8.GetString(planBytes)), layoutJson);
            if (moved.Count > 0) judged = new Findings([.. judged, .. moved]);
        }
        return judged;
    }

    /// <summary>Rasterize a finished sketch again where the stored layout has moved past the scan: a vertex
    /// moved, a coast bent, a shape redrawn after Finish. Every read of the board's ground — editability,
    /// traversability, the pre-flight, the export — goes through the scan, so a stale one answers for a board
    /// that is no longer drawn. A map never finished, and an imported world's scan, are left alone. True where
    /// the scan was written again.</summary>
    public static async Task<bool> RefreshAsync(long mapId, MapArtifactStore artifacts, WorldFeatureWriter writer,
        CancellationToken ct)
    {
        if (await artifacts.RevisionAsync(mapId, ArtifactKind.SketchLayoutJson, ct) is not { } revision) return false;
        if (await artifacts.LoadAsync(mapId, ArtifactKind.MapConfigJson, ct) is not { } configBytes) return false;
        if (JsonNode.Parse(configBytes) is not JsonObject config
            || config["scan_read"]?.GetValue<string>() != "surface") return false;
        if (config[WorldFeatureWriter.SketchScanRevision] is JsonValue scanned
            && scanned.TryGetValue<long>(out var at) && at == revision) return false;

        if (await artifacts.LoadAsync(mapId, ArtifactKind.SketchLayoutJson, ct) is not { } data) return false;
        var cells = SketchRasterizer.RasterizeColumns(Encoding.UTF8.GetString(data));
        var islands = IslandDetector.Detect(cells.Select(cell => (cell.X, cell.Z)), minIslandSize: 1);
        if (islands.Count == 0) return false;
        await writer.WriteSketchAsync(mapId, cells, islands, revision, ct);
        return true;
    }

    private static SketchFinished Refuse(int status, string error, params Finding[] findings) =>
        new(Refusal.At(status, error, findings));
}
