using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Minecraft;

/// <summary>
/// Persists a <see cref="WorldProvenance"/> beside a built world's region files — the "survives the build"
/// half of the record (docs/world-export/decoration.md). A block carries no provenance byte of its own, so the
/// record has to live somewhere the voxels do not; this writes it into the same folder the <c>.mca</c> files
/// land in, one JSON file per region directory, so the two travel together and a render reading a region back
/// off disk — the corpus harness, a second look at a map already built — finds the same answer a freshly built
/// world would have given it in memory.
///
/// <para>Encoded as one run per contiguous stretch of a Z row rather than one entry per column: a stamped
/// footprint and the terrain around it are each one run far more often than not, so a board of tens of
/// thousands of columns costs a few hundred rows' worth of runs rather than a dictionary entry per cell.</para>
/// </summary>
public static class WorldProvenanceFile
{
    private const string FileName = "provenance.json";

    private sealed record Run(int Z, int MinX, int MaxX, [property: JsonPropertyName("structure")] bool Structure);

    /// <summary>Write the sidecar into <paramref name="regionDir"/>, creating it if needed.</summary>
    public static void Write(WorldProvenance provenance, string regionDir)
    {
        Directory.CreateDirectory(regionDir);
        File.WriteAllText(Path.Combine(regionDir, FileName), JsonSerializer.Serialize(Encode(provenance)));
    }

    /// <summary>Read the sidecar back, or null when the region carries none — a world the studio scanned
    /// rather than built, or one written before this recording existed. A renderer reading null falls back to
    /// the material estimate rather than treating an absent file as an error.</summary>
    public static WorldProvenance? TryRead(string regionDir)
    {
        var path = Path.Combine(regionDir, FileName);
        if (!File.Exists(path)) return null;

        var runs = JsonSerializer.Deserialize<List<Run>>(File.ReadAllText(path));
        if (runs is null) return null;

        var provenance = new WorldProvenance();
        foreach (var run in runs)
            provenance.ClaimRect(run.MinX, run.Z, run.MaxX, run.Z,
                run.Structure ? ProvenanceLayer.Structure : ProvenanceLayer.Ground);
        return provenance;
    }

    /// <summary>Every claimed column, grouped into maximal same-layer runs along X within each Z row.</summary>
    private static List<Run> Encode(WorldProvenance provenance)
    {
        var runs = new List<Run>();
        foreach (var row in provenance.Claims.GroupBy(claim => claim.Cell.Z).OrderBy(group => group.Key))
        {
            var sorted = row.OrderBy(claim => claim.Cell.X).ToList();
            var runStartX = sorted[0].Cell.X;
            var runLayer = sorted[0].Layer;
            var previousX = runStartX;

            for (var i = 1; i < sorted.Count; i++)
            {
                var (cell, layer) = sorted[i];
                if (cell.X == previousX + 1 && layer == runLayer) { previousX = cell.X; continue; }
                runs.Add(new Run(row.Key, runStartX, previousX, runLayer == ProvenanceLayer.Structure));
                runStartX = cell.X;
                runLayer = layer;
                previousX = cell.X;
            }
            runs.Add(new Run(row.Key, runStartX, previousX, runLayer == ProvenanceLayer.Structure));
        }
        return runs;
    }
}
