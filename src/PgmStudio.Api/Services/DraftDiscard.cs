using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// Throwing away a draft that was never worked on.
///
/// <para>"New sketch" and "New plan" create a map before the author has done anything, so leaving without
/// doing anything would otherwise litter the dashboard with untitled drafts. This is what either tool asks on
/// the way out, and it is a judgement rather than a delete: a draft is discarded only if every sign of work is
/// absent — still at the stage it was made at, still under the name it was made with, credited to nobody but
/// the person who originated it, and holding nothing its tool writes. The originator's own credit is written
/// with the map (<see cref="MapOrigin"/>), so it is no sign of work. A sketch holds nothing while no layer
/// holds a shape; a plan while its document is the empty one it was created with and it was not forked from a
/// generator candidate.</para>
///
/// <para><b>A map that is not there is discarded, not refused.</b> Asking to throw away something that does
/// not exist has got what it wanted, and a 404 would make a caller handle a case that is already the
/// outcome it asked for.</para>
/// </summary>
public static class DraftDiscard
{
    /// <summary>The name a blank sketch draft is created with.</summary>
    public const string SketchName = "Untitled sketch";

    /// <summary>The name a blank plan draft is created with.</summary>
    public const string PlanName = "Untitled plan";

    /// <summary>Whether the draft was discarded.</summary>
    public static async Task<bool> IfUntouchedAsync(
        MapRepository repo, PgmDb db, MapArtifactStore artifacts, string slug, CancellationToken ct)
    {
        if (await repo.GetBySlugAsync(slug, ct) is not { } map) return false;

        var untouched = map.Stage switch
        {
            MapStage.Sketch => IsNamed(map, SketchName) && !await HasShapesAsync(artifacts, map.Id, ct),
            MapStage.Plan => IsNamed(map, PlanName) && map.PlanSourceId is null
                             && await IsEmptyPlanAsync(artifacts, map.Id, ct),
            _ => false,
        };
        untouched = untouched
            && !await db.Authors.AnyAsync(author => author.MapId == map.Id && author.Uuid != map.OwnerUuid, ct);

        if (untouched)
        {
            await repo.DeleteMapAsync(map.Id, ct);   // FK cascade removes the artifacts
            await new MapNoteStore(db).DeleteMapAsync(map.Slug, ct);
        }
        return untouched;
    }

    private static bool IsNamed(MapRow map, string name) =>
        string.Equals(map.Name?.Trim(), name, StringComparison.Ordinal);

    // A plan draft is created holding `{}`; the editor's first save writes its whole document.
    private static async Task<bool> IsEmptyPlanAsync(MapArtifactStore artifacts, long mapId, CancellationToken ct)
    {
        var data = await artifacts.LoadAsync(mapId, ArtifactKind.PlanJson, ct);
        if (data is null || data.Length == 0) return true;
        try
        {
            using var doc = JsonDocument.Parse(data);
            return doc.RootElement.ValueKind == JsonValueKind.Object && !doc.RootElement.EnumerateObject().Any();
        }
        catch (JsonException) { return false; }
    }

    // The layout blob is {setup?, layers:[{layout:{shapes,groups}}]} (or a legacy single {layout:{…}}, or
    // {} / setup-only for a fresh draft). "Drawn on" = a shape in any layer.
    private static async Task<bool> HasShapesAsync(MapArtifactStore artifacts, long mapId, CancellationToken ct)
    {
        var data = await artifacts.LoadAsync(mapId, ArtifactKind.SketchLayoutJson, ct);
        if (data is null || data.Length == 0) return false;
        try { using var doc = JsonDocument.Parse(data); return HasShapes(doc.RootElement); }
        catch { return false; }
    }

    private static bool HasShapes(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
        if (root.TryGetProperty("layers", out var layers) && layers.ValueKind == JsonValueKind.Array)
            foreach (var layer in layers.EnumerateArray())
                if (LayoutHasShapes(layer)) return true;
        return LayoutHasShapes(root);   // legacy top-level {layout:{shapes}}
    }

    private static bool LayoutHasShapes(JsonElement element)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty("layout", out var layout) && layout.ValueKind == JsonValueKind.Object
           && layout.TryGetProperty("shapes", out var shapes) && shapes.ValueKind == JsonValueKind.Array
           && shapes.GetArrayLength() > 0;
}
