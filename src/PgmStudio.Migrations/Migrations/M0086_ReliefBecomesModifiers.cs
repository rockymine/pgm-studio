using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The group-keyed <c>relief</c> a layout or a refinement states becomes the top-level <c>modifiers[]</c> the
/// ground is shaped by, and the <c>relief</c> key is deleted. Each <c>area</c> mark becomes a Level over its ring at
/// its height and each <c>point</c> mark a Level over an ellipse at its centre; each push becomes a Hill over its
/// ring at its amount; a group's grain becomes one Roughness over the whole board. <c>line</c>, <c>scarp</c> and
/// <c>rim</c> marks, and a group's base, reach, step and landform, have no modifier and are dropped. A mask fans
/// under the symmetry where the group copies onto its images, and does by default where the document names no
/// such group.
///
/// <para>A shape's <c>relief_scope</c> is one word as well: <c>exclude</c> becomes <c>hold</c>, since the modifiers
/// leave both out, and <c>follow</c> is deleted, since a shape that is neither held nor erected follows.</para>
///
/// <para>The current layout and refinement of every map are rewritten, and so is every one a change recorded,
/// under the hash of its new bytes, so a restore reads the same shape. Each map whose documents carried a relief
/// is then given a change of its own whose note says what became what and what was dropped. Forward-only.</para>
/// </summary>
[Migration(86, "A relief becomes the modifier stack")]
public sealed class M0086_ReliefBecomesModifiers : Migration
{
    private const string KindList = "'sketch_layout_json', 'refinement_json'";

    public override void Up()
    {
        Execute.WithConnection((conn, tx) =>
        {
            var current = new List<(long Id, long MapId, string Slug, string Kind, byte[] Data)>();
            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT a.id, a.map_id, m.slug, a.kind, a.data FROM map_artifact a "
                                  + $"JOIN map m ON m.id = a.map_id WHERE a.kind IN ({KindList})";
                using var reader = sel.ExecuteReader();
                while (reader.Read())
                    current.Add((reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3),
                                 (byte[])reader.GetValue(4)));
            }
            var noted = new Dictionary<string, (Carried Total, List<(long ArtifactId, string Kind, byte[] Data)> Written)>(
                StringComparer.Ordinal);
            foreach (var (id, _, slug, kind, data) in current)
            {
                if (!TryCarry(data, out var updated, out var carried)) continue;
                Run(conn, tx, "UPDATE map_artifact SET data = @data WHERE id = @id", ("@data", updated), ("@id", id));
                if (!carried.Noteworthy) continue;
                if (!noted.TryGetValue(slug, out var entry)) entry = (new Carried(), []);
                entry.Total.Add(carried);
                entry.Written.Add((id, kind, updated));
                noted[slug] = entry;
            }

            var recorded = new List<(long ChangeId, string Kind, string Hash, byte[] Packed)>();
            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT d.change_id, d.kind, d.blob_hash, b.data FROM map_change_document d "
                                  + $"JOIN document_blob b ON b.hash = d.blob_hash WHERE d.kind IN ({KindList})";
                using var reader = sel.ExecuteReader();
                while (reader.Read())
                    recorded.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2), (byte[])reader.GetValue(3)));
            }
            var carriedBlobs = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var (changeId, kind, hash, packed) in recorded)
            {
                if (!carriedBlobs.TryGetValue(hash, out var next))
                {
                    next = null;
                    if (TryCarry(Gunzip(packed), out var updated, out _))
                        next = Keep(conn, tx, updated);
                    carriedBlobs[hash] = next;
                }
                if (next is null) continue;
                Run(conn, tx, "UPDATE map_change_document SET blob_hash = @next WHERE change_id = @change AND kind = @kind",
                    ("@next", next), ("@change", changeId), ("@kind", kind));
            }

            foreach (var (slug, (total, written)) in noted)
            {
                var number = 1 + Count(conn, tx,
                    "SELECT GREATEST(COALESCE((SELECT MAX(number) FROM map_change WHERE map_slug = @slug), 0), "
                    + "COALESCE((SELECT last_number FROM map_change_sequence WHERE map_slug = @slug), 0))",
                    ("@slug", slug));
                Run(conn, tx, "INSERT INTO map_change_sequence (map_slug, last_number) VALUES (@slug, @number) "
                              + "ON DUPLICATE KEY UPDATE last_number = @number", ("@slug", slug), ("@number", number));
                Run(conn, tx, "INSERT INTO map_change (map_slug, number, created_at, note) VALUES (@slug, @number, UTC_TIMESTAMP(), @note)",
                    ("@slug", slug), ("@number", number), ("@note", total.Note()));
                var change = Count(conn, tx, "SELECT id FROM map_change WHERE map_slug = @slug AND number = @number",
                                   ("@slug", slug), ("@number", number));
                foreach (var (artifactId, kind, data) in written)
                {
                    Run(conn, tx, "INSERT INTO map_change_document (change_id, kind, blob_hash) VALUES (@change, @kind, @hash)",
                        ("@change", change), ("@kind", kind), ("@hash", Keep(conn, tx, data)));
                    Run(conn, tx, "UPDATE map_artifact SET revision = @number WHERE id = @id",
                        ("@number", number), ("@id", artifactId));
                }
            }
        });
    }

    public override void Down() { }

    /// <summary>What a document's relief became: how many modifiers of each operation were written and how many
    /// marks had none.</summary>
    internal sealed class Carried
    {
        public int Levels, Hills, Roughnesses, Lines, Scarps, Rims, Unreadable;

        public bool Noteworthy => Levels + Hills + Roughnesses + Lines + Scarps + Rims + Unreadable > 0;

        public void Add(Carried other)
        {
            Levels += other.Levels; Hills += other.Hills; Roughnesses += other.Roughnesses;
            Lines += other.Lines; Scarps += other.Scarps; Rims += other.Rims; Unreadable += other.Unreadable;
        }

        public string Note()
        {
            var made = new List<string>();
            if (Levels > 0) made.Add($"{Levels} Level");
            if (Hills > 0) made.Add($"{Hills} Hill");
            if (Roughnesses > 0) made.Add($"{Roughnesses} Roughness");
            var dropped = new List<string>();
            if (Lines > 0) dropped.Add($"{Lines} line");
            if (Scarps > 0) dropped.Add($"{Scarps} scarp");
            if (Rims > 0) dropped.Add($"{Rims} rim");
            if (Unreadable > 0) dropped.Add($"{Unreadable} unreadable");
            return "The relief became the modifier stack: "
                   + (made.Count > 0 ? string.Join(", ", made) : "no modifier")
                   + (dropped.Count > 0 ? $"; marks dropped: {string.Join(", ", dropped)}" : "")
                   + ". A group's base, reach, step and landform are no longer stated.";
        }
    }

    /// <summary>Converts a document stating a relief. Returns false, with the bytes untouched, where it states
    /// none.</summary>
    internal static bool TryCarry(byte[] data, out byte[] updated, out Carried carried)
    {
        updated = data;
        carried = new Carried();
        JsonNode? root;
        try { root = JsonNode.Parse(Encoding.UTF8.GetString(data)); }
        catch { return false; }
        if (root is not JsonObject document) return false;

        var carriedRelief = document.ContainsKey("relief");
        if (carriedRelief) CarryRelief(document, carried);
        var rescoped = Rescope(document);
        if (!carriedRelief && !rescoped) return false;
        updated = Encoding.UTF8.GetBytes(document.ToJsonString());
        return true;
    }

    private static void CarryRelief(JsonObject document, Carried carried)
    {
        var fresh = document["modifiers"] is not JsonArray;
        var modifiers = document["modifiers"] as JsonArray ?? [];
        var taken = new HashSet<string>(
            modifiers.OfType<JsonObject>().Select(modifier => Text(modifier["id"])).OfType<string>(),
            StringComparer.Ordinal);
        var mirrors = MirroringOf(document);

        if (document["relief"] is JsonObject relief)
            foreach (var (groupId, value) in relief)
                if (value is JsonObject stated)
                    CarryGroup(groupId, stated, mirrors.GetValueOrDefault(groupId, true), modifiers, taken, carried);

        document.Remove("relief");
        if (fresh && modifiers.Count > 0) document["modifiers"] = modifiers;
    }

    // Every stated relief_scope is one word: exclude is hold, and follow is the absence of the field. Walked
    // over the whole document, since a shape is stored in a layer, a room's ground, a refinement's added shape
    // and the props it merges onto one.
    private static bool Rescope(JsonNode? node)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject stated:
                if (stated["relief_scope"] is JsonValue scope && scope.TryGetValue<string>(out var word))
                {
                    if (word == "follow") { stated.Remove("relief_scope"); changed = true; }
                    else if (word == "exclude") { stated["relief_scope"] = "hold"; changed = true; }
                }
                foreach (var child in stated.Select(property => property.Value).ToList()) changed |= Rescope(child);
                break;
            case JsonArray list:
                foreach (var child in list.ToList()) changed |= Rescope(child);
                break;
        }
        return changed;
    }

    private static void CarryGroup(string groupId, JsonObject group, bool mirror, JsonArray modifiers,
                                   HashSet<string> taken, Carried carried)
    {
        var marks = group["marks"] as JsonArray ?? [];
        for (var index = 0; index < marks.Count; index++)
        {
            if (marks[index] is not JsonObject mark) { carried.Unreadable++; continue; }
            var wanted = $"{groupId}-{(Text(mark["id"]) is { Length: > 0 } named ? named : index.ToString(CultureInfo.InvariantCulture))}";
            switch (Text(mark["kind"]) ?? "point")
            {
                case "area":
                    if (Ring(mark["ring"] ?? mark["points"]) is { } ring && FirstHeight(mark["h"]) is { } areaTop)
                    {
                        modifiers.Add(Level(Unique(wanted, taken), Polygon(ring, mirror), areaTop));
                        carried.Levels++;
                    }
                    else carried.Unreadable++;
                    break;
                case "point":
                    if (Pair(mark["at"]) is { } at && FirstHeight(mark["h"]) is { } pointTop)
                    {
                        var radius = Math.Max(Number(mark["r"]) ?? 2, 2);
                        modifiers.Add(Level(Unique(wanted, taken), Ellipse(at, radius, mirror), pointTop));
                        carried.Levels++;
                    }
                    else carried.Unreadable++;
                    break;
                case "line": carried.Lines++; break;
                case "scarp": carried.Scarps++; break;
                case "rim": carried.Rims++; break;
                default: carried.Unreadable++; break;
            }
        }

        var pushes = group["pushes"] as JsonArray ?? [];
        for (var index = 0; index < pushes.Count; index++)
        {
            if (pushes[index] is not JsonObject push || Ring(push["ring"]) is not { } ring) { carried.Unreadable++; continue; }
            var wanted = $"{groupId}-{(Text(push["id"]) is { Length: > 0 } named ? named : index.ToString(CultureInfo.InvariantCulture))}";
            var amounts = (push["amounts"] as JsonArray ?? []).Select(Number).OfType<double>().ToList();
            var rise = amounts.Count > 0 ? amounts.Average() : Number(push["amount"]) ?? 0;
            modifiers.Add(new JsonObject
            {
                ["id"] = Unique(wanted, taken),
                ["op"] = "mound",
                ["mask"] = Polygon(ring, mirror),
                ["rise"] = rise,
                ["soft"] = 0.35,
                ["power"] = 4,
                ["seed"] = Seed(push["seed"]),
            });
            carried.Hills++;
        }

        if (group["grain"] is JsonObject grain && Number(grain["amplitude"]) is { } amplitude && amplitude > 0)
        {
            modifiers.Add(new JsonObject
            {
                ["id"] = Unique($"{groupId}-grain", taken),
                ["op"] = "noise",
                ["mask"] = new JsonObject
                {
                    ["areas"] = new JsonArray(new JsonObject { ["kind"] = "board" }),
                    ["mirror"] = mirror,
                },
                ["sizes"] = new JsonArray(new JsonObject
                {
                    ["cell"] = Number(grain["scale"]) ?? 9,
                    ["octaves"] = 2,
                    ["rise"] = amplitude,
                }),
                ["seed"] = Seed(grain["seed"]),
            });
            carried.Roughnesses++;
        }
    }

    private static JsonObject Level(string id, JsonObject mask, double top) => new()
    {
        ["id"] = id,
        ["op"] = "level",
        ["mask"] = mask,
        ["top"] = top,
    };

    private static JsonObject Polygon(List<(double X, double Z)> ring, bool mirror) => new()
    {
        ["areas"] = new JsonArray(new JsonObject
        {
            ["kind"] = "polygon",
            ["points"] = new JsonArray([.. ring.Select(point =>
                (JsonNode)new JsonArray(JsonValue.Create(point.X), JsonValue.Create(point.Z)))]),
        }),
        ["mirror"] = mirror,
    };

    private static JsonObject Ellipse((double X, double Z) at, double radius, bool mirror) => new()
    {
        ["areas"] = new JsonArray(new JsonObject
        {
            ["kind"] = "ellipse",
            ["at"] = new JsonArray(JsonValue.Create(at.X), JsonValue.Create(at.Z)),
            ["rx"] = radius,
            ["rz"] = radius,
        }),
        ["mirror"] = mirror,
    };

    // Whether each group copies onto its images, by id, across every layer; a group the document does not name
    // is not in it.
    private static Dictionary<string, bool> MirroringOf(JsonObject document)
    {
        var mirrors = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var layer in (document["layers"] as JsonArray ?? []).OfType<JsonObject>())
            foreach (var group in (layer["layout"]?["groups"] as JsonArray ?? []).OfType<JsonObject>())
                if (Text(group["id"]) is { Length: > 0 } id)
                    mirrors[id] = group["mirrors"] is JsonValue flag && flag.TryGetValue<bool>(out var copies) ? copies : true;
        return mirrors;
    }

    private static string Unique(string wanted, HashSet<string> taken)
    {
        var id = wanted;
        for (var suffix = 2; !taken.Add(id); suffix++) id = $"{wanted}-{suffix.ToString(CultureInfo.InvariantCulture)}";
        return id;
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static double? Number(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number) ? number : null;

    // A height stated as one number or as a list; the list's first is the one a Level holds.
    private static double? FirstHeight(JsonNode? node) =>
        node is JsonArray list ? list.Select(Number).OfType<double>().Cast<double?>().FirstOrDefault() : Number(node);

    private static int Seed(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<long>(out var seed) ? unchecked((int)seed) : 1;

    private static (double X, double Z)? Pair(JsonNode? node) =>
        node is JsonArray { Count: >= 2 } pair && Number(pair[0]) is { } x && Number(pair[1]) is { } z ? (x, z) : null;

    private static List<(double X, double Z)>? Ring(JsonNode? node)
    {
        if (node is not JsonArray points) return null;
        var ring = points.Select(Pair).OfType<(double X, double Z)>().ToList();
        return ring.Count >= 3 && ring.Count == points.Count ? ring : null;
    }

    // Stores bytes as a document blob unless their hash is held, and answers the hash.
    private static string Keep(IDbConnection conn, IDbTransaction tx, byte[] data)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(data));
        if (Count(conn, tx, "SELECT COUNT(*) FROM document_blob WHERE hash = @hash", ("@hash", hash)) == 0)
            Run(conn, tx, "INSERT INTO document_blob (hash, data, length) VALUES (@hash, @data, @length)",
                ("@hash", hash), ("@data", Gzip(data)), ("@length", (long)data.Length));
        return hash;
    }

    private static byte[] Gzip(byte[] data)
    {
        using var packed = new MemoryStream();
        using (var gzip = new GZipStream(packed, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(data);
        return packed.ToArray();
    }

    private static byte[] Gunzip(byte[] data)
    {
        using var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        using var unpacked = new MemoryStream();
        gzip.CopyTo(unpacked);
        return unpacked.ToArray();
    }

    private static void Run(IDbConnection conn, IDbTransaction tx, string sql, params (string Name, object Value)[] values)
    {
        using var cmd = Command(conn, tx, sql, values);
        cmd.ExecuteNonQuery();
    }

    private static long Count(IDbConnection conn, IDbTransaction tx, string sql, params (string Name, object Value)[] values)
    {
        using var cmd = Command(conn, tx, sql, values);
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static IDbCommand Command(IDbConnection conn, IDbTransaction tx, string sql, (string Name, object Value)[] values)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in values)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            cmd.Parameters.Add(parameter);
        }
        return cmd;
    }
}
