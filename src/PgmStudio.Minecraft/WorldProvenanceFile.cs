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
/// thousands of columns costs a few hundred rows' worth of runs rather than a dictionary entry per cell. A
/// run now breaks on an owner change as well as a layer change — two buildings that stand wall to wall are
/// two runs even though both are <see cref="ProvenanceLayer.Structure"/> — and the owner itself is written
/// once into a small id table rather than once per run: every distinct owner string in the record gets one
/// slot, and a run carries a slot number rather than the string, so an identity per column costs an int per
/// run rather than a string per cell.</para>
/// </summary>
public static class WorldProvenanceFile
{
    private const string FileName = "provenance.json";

    // OwnerId 0 is reserved for WorldProvenance.NoOwner and never appears in the id table — every claim
    // shares that reading without needing a table slot, since it is one value rather than one per pass.
    private sealed record Run(int Z, int MinX, int MaxX, [property: JsonPropertyName("structure")] bool Structure,
        [property: JsonPropertyName("owner"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int OwnerId);

    private sealed record Sidecar(
        [property: JsonPropertyName("owners")] List<string> Owners,
        [property: JsonPropertyName("runs")] List<Run> Runs);

    /// <summary>Write the sidecar into <paramref name="regionDir"/>, creating it if needed.</summary>
    public static void Write(WorldProvenance provenance, string regionDir)
    {
        Directory.CreateDirectory(regionDir);
        File.WriteAllText(Path.Combine(regionDir, FileName), JsonSerializer.Serialize(Encode(provenance)));
    }

    /// <summary>Read the sidecar back, or null when the region carries none a renderer can use — a world the
    /// studio scanned rather than built, one written before this recording existed, or one written by an
    /// earlier revision of this same file whose shape has since moved on (the sidecar has no version field of
    /// its own to gate on, so an incompatible shape is read the same way a missing file is: as no record). A
    /// renderer reading null falls back to the material estimate rather than treating either as an error.</summary>
    public static WorldProvenance? TryRead(string regionDir)
    {
        var path = Path.Combine(regionDir, FileName);
        if (!File.Exists(path)) return null;

        // The record has no version field to gate on, so an incompatible shape is read the same way a missing
        // file is: as no record. That covers both a parse failure outright (the pre-owner sidecar this file
        // wrote before B139 was a bare JSON array, which fails to convert to this object shape and raises
        // JsonException rather than returning null the way a mismatched shape normally would) and a shape that
        // parses but is missing the one field this reader cannot proceed without.
        Sidecar? sidecar;
        try { sidecar = JsonSerializer.Deserialize<Sidecar>(File.ReadAllText(path)); }
        catch (JsonException) { return null; }
        if (sidecar?.Runs is null) return null;

        var owners = sidecar.Owners ?? [];
        var provenance = new WorldProvenance();
        foreach (var run in sidecar.Runs)
        {
            var owner = run.OwnerId > 0 && run.OwnerId <= owners.Count
                ? owners[run.OwnerId - 1] : WorldProvenance.NoOwner;
            provenance.ClaimRect(run.MinX, run.Z, run.MaxX, run.Z,
                run.Structure ? ProvenanceLayer.Structure : ProvenanceLayer.Ground, owner);
        }
        return provenance;
    }

    /// <summary>Every claimed column, grouped into maximal same-layer, same-owner runs along X within each Z
    /// row, with every distinct owner string collapsed to one id-table slot.</summary>
    private static Sidecar Encode(WorldProvenance provenance)
    {
        var ownerIds = new Dictionary<string, int>();
        int OwnerId(string owner)
        {
            if (owner == WorldProvenance.NoOwner) return 0;
            if (ownerIds.TryGetValue(owner, out var id)) return id;
            id = ownerIds.Count + 1;
            ownerIds[owner] = id;
            return id;
        }

        var runs = new List<Run>();
        foreach (var row in provenance.Claims.GroupBy(claim => claim.Cell.Z).OrderBy(group => group.Key))
        {
            var sorted = row.OrderBy(claim => claim.Cell.X).ToList();
            var runStartX = sorted[0].Cell.X;
            var runLayer = sorted[0].Layer;
            var runOwner = sorted[0].Owner;
            var previousX = runStartX;

            for (var i = 1; i < sorted.Count; i++)
            {
                var (cell, layer, owner) = sorted[i];
                if (cell.X == previousX + 1 && layer == runLayer && owner == runOwner) { previousX = cell.X; continue; }
                runs.Add(new Run(row.Key, runStartX, previousX, runLayer == ProvenanceLayer.Structure, OwnerId(runOwner)));
                runStartX = cell.X;
                runLayer = layer;
                runOwner = owner;
                previousX = cell.X;
            }
            runs.Add(new Run(row.Key, runStartX, previousX, runLayer == ProvenanceLayer.Structure, OwnerId(runOwner)));
        }

        var owners = new string[ownerIds.Count];
        foreach (var (owner, id) in ownerIds) owners[id - 1] = owner;
        return new Sidecar([.. owners], runs);
    }
}
