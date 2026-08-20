using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Anvil;

/// <summary>
/// The dressing pass's decline report as a sidecar beside the region files — every whole prop the pass
/// declined to place, as the finding that says why. Written only when something was dropped: absence means
/// everything authored stood, so a reader never has to tell an empty report from a world built before the
/// report existed. The provenance sidecar answers what <em>landed</em>; this one answers what did not, which
/// is the half of the census the silent declines hid.
/// <para>The entries are <see cref="Finding"/> itself — the shape every other refusal and complaint in the
/// studio arrives in — so a reader parses one thing whether it took the report off disk or off the build
/// endpoint that returns the same findings beside its payload.</para>
/// </summary>
public static class DressingReportFile
{
    private const string FileName = "dressing-report.json";

    private sealed record Sidecar(
        [property: JsonPropertyName("dropped")] IReadOnlyList<Finding> Dropped);

    /// <summary>Web defaults, so the keys are the camelCase ones the same findings carry over HTTP.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Write the report into <paramref name="regionDir"/> when anything was dropped; remove a stale
    /// one when nothing was, so a rebuild that fixed every drop does not leave last build's report standing.</summary>
    public static void Write(IReadOnlyList<Finding>? declines, string regionDir)
    {
        var path = Path.Combine(regionDir, FileName);
        if (declines is not { Count: > 0 })
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        Directory.CreateDirectory(regionDir);
        File.WriteAllText(path, JsonSerializer.Serialize(new Sidecar(declines), Json));
    }
}
