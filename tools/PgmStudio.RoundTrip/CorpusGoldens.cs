using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Analysis.Layer;
using PgmStudio.Analysis.Playability;
using PgmStudio.Domain;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;
using Dict = System.Collections.Generic.Dictionary<string, object?>;

/// <summary>
/// A recorded digest of what the four map-level derivations make of the corpus, one entry per map: the region
/// categorizer, buildability, traversability, and wool availability with its resource summary.
///
/// <para>It answers a question the unit suites cannot — <b>did a change move a verdict on a real map, and
/// which ones</b> — because those run on synthetic fixtures chosen to isolate one rule. A corpus map exercises
/// combinations nobody would write a fixture for, and these four derivations are the ones a refactor is most
/// likely to move without noticing: each reads a whole map document and answers in aggregate.</para>
///
/// <para>It is <b>not</b> a claim that the answers may not change, and it is meant to be re-recorded whenever a
/// change is deliberate. What it buys is that the change has to be looked at first, map by map, rather than
/// arriving unannounced.</para>
///
/// <para>Each entry reads <c>&lt;digest&gt; &lt;summary&gt;</c>. The digest covers everything the derivation
/// answered; the summary is the part a reader can act on, so a <c>git diff</c> of the record says what moved
/// before anyone re-runs anything.</para>
/// </summary>
public static class CorpusGoldens
{
    /// <summary>The recorded digest, beside the harness that writes it.</summary>
    public static readonly string[] FilePath = ["tools", "PgmStudio.RoundTrip", "corpus-goldens.json"];

    // The record is read as a diff, so it is written to be read: indented, and without the \uXXXX escaping
    // that would turn every summary separator into six characters of noise.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>What one map digests to. A derivation whose inputs are absent — no world-scan output for that
    /// map — records null rather than an empty string, so "not measured" never reads as "measured and
    /// empty".</summary>
    public sealed record MapEntry(
        [property: JsonPropertyName("regions")] string? Regions,
        [property: JsonPropertyName("build")] string? Build,
        [property: JsonPropertyName("trav")] string? Trav,
        [property: JsonPropertyName("wool")] string? Wool)
    {
        public string? this[string derivation] => derivation switch
        {
            "regions" => Regions, "build" => Build, "trav" => Trav, "wool" => Wool, _ => null,
        };
    }

    public static readonly string[] Derivations = ["regions", "build", "trav", "wool"];

    public sealed record Record([property: JsonPropertyName("maps")] IReadOnlyDictionary<string, MapEntry> Maps)
    {
        public string ToJson() => JsonSerializer.Serialize(this, Json);
        public static Record? Parse(string json) => JsonSerializer.Deserialize<Record>(json, Json);
    }

    /// <summary>
    /// Run every derivation that has its inputs, over every map given.
    /// <paramref name="featureRoot"/> is a directory of <c>&lt;slug&gt;/*.parquet</c> world-scan output; without
    /// one only the region categorizer runs, since the other three read the terrain.
    /// </summary>
    public static async Task<Record> Compute(
        IReadOnlyList<(string Slug, string XmlPath)> maps, string? featureRoot, Action<string>? note = null)
    {
        var entries = new SortedDictionary<string, MapEntry>(StringComparer.Ordinal);
        foreach (var (slug, xmlPath) in maps)
        {
            Dict doc;
            try
            {
                doc = Serializer.ToDict(MapParser.Parse(xmlPath));
            }
            catch (UnsupportedMapException ex)
            {
                // A map entering or leaving the supported range is itself a change worth catching, so it is
                // recorded rather than skipped — and the reason is the whole of the answer.
                entries[slug] = new MapEntry(Digest($"unsupported: {ex.Message}", "unsupported"), null, null, null);
                continue;
            }
            catch (Exception ex)
            {
                entries[slug] = new MapEntry(Digest(ex.Message, $"unparsed:{ex.GetType().Name}"), null, null, null);
                note?.Invoke($"  {slug}: {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            var regions = DescribeRegions(doc);
            string? build = null, trav = null, wool = null;

            var dir = featureRoot is null ? null : Path.Combine(featureRoot, slug);
            var segments = dir is null ? null : Path.Combine(dir, "layer_segments.parquet");
            if (segments is not null && File.Exists(segments))
            {
                var index = new SegmentIndex(await FeatureData.ReadSegments(segments));
                var y0 = index.Y0Columns();
                var surface = index.StandingColumns();
                build = DescribeBuildability(Buildability.Compute(doc, y0));
                trav = DescribeTraversability(Traversability.Check(doc, surface, y0));
            }
            if (dir is not null && Directory.Exists(dir)) wool = await DescribeWool(doc, dir);

            entries[slug] = new MapEntry(regions, build, trav, wool);
        }
        return new Record(entries);
    }

    // ── what each derivation is asked to state ──────────────────────────────────────────────────────

    private static string DescribeRegions(Dict doc)
    {
        var facets = RegionCategorizer.DeriveFacets(doc);
        var detail = facets.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv =>
            $"{kv.Key}\t{kv.Value.Category}/{kv.Value.Subtype ?? "-"}\t{string.Join(",", kv.Value.Roles)}");
        var byCategory = facets.Values.GroupBy(f => f.Category).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}={g.Count()}");
        return Digest(string.Join("\n", detail), $"{facets.Count} regions · {string.Join(" ", byCategory)}");
    }

    private static string DescribeBuildability(Buildability.Result r)
    {
        // The verdict grid is the answer itself, so it is hashed rather than summarized: a per-cell change is
        // exactly what this record exists to catch, and no count would show one.
        var grid = Convert.ToHexString(SHA256.HashData(r.Verdict))[..16];
        var counts = r.Counts.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}");
        return Digest($"{r.MinX},{r.MinZ},{r.MaxX},{r.MaxZ}\ny0={r.HasY0}\n{grid}",
            $"{r.Width}x{r.Height} · {string.Join(" ", counts)}");
    }

    private static string DescribeTraversability(Traversability.Result r)
    {
        var detail = r.Points.Select(p => $"{p.Point.Kind}\t{p.Point.Name}\t{p.Point.X},{p.Point.Z}\tc{p.Component}")
            .Concat(r.Isolated.Select(p => p.For is { } team
                ? $"isolated\t{p.Kind}\t{p.Name}\tfor:{team}"
                : $"isolated\t{p.Kind}\t{p.Name}"))
            .Append($"layers={r.HaveLayers}").Append(r.Message);
        return Digest(string.Join("\n", detail),
            $"connected={r.Connected} components={r.ComponentCount} points={r.Points.Count} {r.Severity}");
    }

    private static async Task<string> DescribeWool(Dict doc, string dir)
    {
        var sources = await FeatureData.WoolSourceRows(dir);
        sources.AddRange(WoolSources.PgmSpawnerSources(doc));
        var availability = WoolSources.CheckAvailability(doc, sources);
        var resources = ResourceSources.Summarize(
            await FeatureData.ResourceBlocks(dir), null, ResourceSources.RenewableRegions(doc));

        var detail = availability
            .Select(a => $"{a.Color}\t{a.Obtainable}/{a.Repeatable}/{a.OneTime}\t{a.Severity}\t{string.Join(",", a.SourceTypes)}")
            .Concat(resources.Select(t => $"{t.Type}\t{t.Total}\t{t.Renewable}\t{t.AllRenewable}"));
        return Digest(string.Join("\n", detail),
            $"{availability.Count(a => a.Obtainable)}/{availability.Count} obtainable · {resources.Count} resources");
    }

    /// <summary>One recorded field: the exact digest of everything the derivation answered, then the part of it
    /// a reader can act on.</summary>
    private static string Digest(string detail, string summary) =>
        $"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(detail)))[..12]} {summary}";
}

/// <summary>The world-scan parquet a derivation reads, in the shape the scan writes it.</summary>
public static class FeatureData
{
    public static async Task<List<Dict>> ReadParquet(string path)
    {
        await using var stream = File.OpenRead(path);
        var result = await Parquet.Serialization.ParquetSerializer.DeserializeUntypedAsync(stream);
        return [.. result.Data.Select(d => d.ToDictionary(kv => kv.Key, kv => (object?)kv.Value))];
    }

    public static async Task<List<(int, int, int, int)>> ReadSegments(string path) =>
        [.. (await ReadParquet(path)).Select(r => (Convert.ToInt32(r["world_x"]), Convert.ToInt32(r["world_z"]),
            Convert.ToInt32(r["world_y_start"]), Convert.ToInt32(r["world_y_end"])))];

    /// <summary>Placed wool, wool in chests, and wool-spawning spawners — the three ways a map hands a team a
    /// colour, as the scan recorded them.</summary>
    public static async Task<List<WoolSources.Source>> WoolSourceRows(string dir)
    {
        var sources = new List<WoolSources.Source>();
        var wools = Path.Combine(dir, "wools.parquet");
        if (File.Exists(wools))
            foreach (var r in await ReadParquet(wools))
                sources.Add(new("block", BlockColors.Normalize(r["color"]?.ToString() ?? ""),
                    Convert.ToInt32(r["world_x"]), Convert.ToInt32(r["world_y"]), Convert.ToInt32(r["world_z"]), 1));

        var chests = Path.Combine(dir, "chests.parquet");
        if (File.Exists(chests))
            foreach (var r in await ReadParquet(chests))
            {
                if (!(r["item_id"]?.ToString() ?? "").Contains("wool", StringComparison.OrdinalIgnoreCase)) continue;
                if (!BlockColors.BlockDamageToColor.TryGetValue(Convert.ToInt32(r["item_damage"]), out var color)) continue;
                sources.Add(new("chest", color, Convert.ToInt32(r["world_x"]), Convert.ToInt32(r["world_y"]),
                    Convert.ToInt32(r["world_z"]), Convert.ToInt32(r["count"])));
            }

        var spawners = Path.Combine(dir, "spawners.parquet");
        if (File.Exists(spawners))
            foreach (var r in await ReadParquet(spawners))
            {
                if (r.GetValueOrDefault("spawns_wool") is not true) continue;
                if (r.GetValueOrDefault("spawn_item_damage") is null
                    || !BlockColors.BlockDamageToColor.TryGetValue(Convert.ToInt32(r["spawn_item_damage"]), out var color)) continue;
                var count = r.GetValueOrDefault("spawn_count") is { } sc ? Convert.ToInt32(sc) : 1;
                sources.Add(new("spawner", color, Convert.ToInt32(r["world_x"]), Convert.ToInt32(r["world_y"]),
                    Convert.ToInt32(r["world_z"]), count == 0 ? 1 : count));
            }
        return sources;
    }

    public static async Task<List<ResourceSources.Block>> ResourceBlocks(string dir)
    {
        var path = Path.Combine(dir, "resources.parquet");
        if (!File.Exists(path)) return [];
        return [.. (await ReadParquet(path)).Select(r => new ResourceSources.Block(
            r["resource_type"]?.ToString() ?? "", Convert.ToInt32(r["world_x"]),
            Convert.ToInt32(r["world_y"]), Convert.ToInt32(r["world_z"])))];
    }
}
