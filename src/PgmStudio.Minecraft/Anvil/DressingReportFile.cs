using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Minecraft.Anvil;

/// <summary>
/// The dressing pass's decline report as a sidecar beside the region files — every whole prop the pass
/// declined to place, with the reason (<see cref="DressingPlacement.Dropped"/>). Written only when something
/// was dropped: absence means everything authored stood, so a reader never has to tell an empty report from a
/// world built before the report existed. The provenance sidecar answers what <em>landed</em>; this one
/// answers what did not, which is the half of the census B146-class silences hid.
/// </summary>
public static class DressingReportFile
{
    private const string FileName = "dressing-report.json";

    private sealed record Entry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("reason")] string Reason);

    private sealed record Sidecar([property: JsonPropertyName("dropped")] List<Entry> Dropped);

    /// <summary>Write the report into <paramref name="regionDir"/> when anything was dropped; remove a stale
    /// one when nothing was, so a rebuild that fixed every drop does not leave last build's report standing.</summary>
    public static void Write(IReadOnlyList<DroppedProp>? dropped, string regionDir)
    {
        var path = Path.Combine(regionDir, FileName);
        if (dropped is not { Count: > 0 })
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        Directory.CreateDirectory(regionDir);
        var sidecar = new Sidecar([.. dropped.Select(drop => new Entry(drop.Id, drop.Kind, drop.Reason))]);
        File.WriteAllText(path, JsonSerializer.Serialize(sidecar));
    }
}
