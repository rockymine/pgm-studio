using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// Throwing away a sketch draft that was never worked on.
///
/// <para>Opening the Sketch tool creates a map before the author has drawn anything, so leaving without
/// drawing would otherwise litter the dashboard with untitled drafts. This is what the tool asks on the way
/// out, and it is a judgement rather than a delete: a draft is discarded only if every sign of work is
/// absent — still at the sketch stage, still under the name it was created with, credited to nobody, and
/// holding no shape in any layer.</para>
///
/// <para><b>A map that is not there is discarded, not refused.</b> Asking to throw away something that does
/// not exist has got what it wanted, and a 404 would make a caller handle a case that is already the
/// outcome it asked for.</para>
/// </summary>
public static class SketchDiscard
{
    /// <summary>The name a blank draft is created with. A draft still carrying it is one of the four signs
    /// that nothing has happened to it.</summary>
    public const string UntouchedName = "Untitled sketch";

    /// <summary>Whether the draft was discarded.</summary>
    public static async Task<bool> IfUntouchedAsync(
        MapRepository repo, PgmDb db, MapArtifactStore artifacts, string slug, CancellationToken ct)
    {
        if (await repo.GetBySlugAsync(slug, ct) is not { } map) return false;

        var untouched = map.Stage == MapStage.Sketch
            && string.Equals(map.Name?.Trim(), UntouchedName, StringComparison.Ordinal)
            && !await db.Authors.AnyAsync(author => author.MapId == map.Id, ct)
            && !await HasShapesAsync(artifacts, map.Id, ct);

        if (untouched) await repo.DeleteMapAsync(map.Id, ct);   // FK cascade removes the layout artifact
        return untouched;
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
