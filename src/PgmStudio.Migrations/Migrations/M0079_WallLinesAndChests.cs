using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Every approach wall of a stored intent becomes the line down the middle of its box, and its defence chests
/// become markers of their own. A wall box <c>[minX, maxX) × [minZ, maxZ)</c> two blocks thick across its
/// thinner side is the line between the middles of its two short sides; its chests — one on a lane up to 10
/// blocks long, two on a longer one, evenly spaced — stand on the ground one block out from the face
/// <c>chestOnMinFace</c> named, fronting away from the wall, under ids minted <c>chest-{n}</c>. Each map's
/// current intent is rewritten, and so is every intent a change recorded, under the hash of its new bytes, so a
/// restore reads the same shape. Forward-only.
/// </summary>
[Migration(79, "Approach walls are lines, and their chests are markers")]
public sealed class M0079_WallLinesAndChests : Migration
{
    private const int SingleChestMaxLane = 10;

    public override void Up()
    {
        Execute.WithConnection((conn, tx) =>
        {
            var current = new List<(long Id, byte[] Data)>();
            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT id, data FROM map_artifact WHERE kind = 'map_intent_json'";
                using var reader = sel.ExecuteReader();
                while (reader.Read()) current.Add((reader.GetInt64(0), (byte[])reader.GetValue(1)));
            }
            foreach (var (id, data) in current)
            {
                if (!TryCarry(data, out var updated)) continue;
                Run(conn, tx, "UPDATE map_artifact SET data = @data WHERE id = @id", ("@data", updated), ("@id", id));
            }

            var recorded = new List<(long ChangeId, string Hash, byte[] Packed)>();
            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT d.change_id, d.blob_hash, b.data FROM map_change_document d "
                                  + "JOIN document_blob b ON b.hash = d.blob_hash WHERE d.kind = 'map_intent_json'";
                using var reader = sel.ExecuteReader();
                while (reader.Read())
                    recorded.Add((reader.GetInt64(0), reader.GetString(1), (byte[])reader.GetValue(2)));
            }
            var carried = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var (changeId, hash, packed) in recorded)
            {
                if (!carried.TryGetValue(hash, out var next))
                {
                    next = null;
                    if (TryCarry(Gunzip(packed), out var updated))
                    {
                        next = Convert.ToHexStringLower(SHA256.HashData(updated));
                        if (Count(conn, tx, "SELECT COUNT(*) FROM document_blob WHERE hash = @hash", ("@hash", next)) == 0)
                            Run(conn, tx, "INSERT INTO document_blob (hash, data, length) VALUES (@hash, @data, @length)",
                                ("@hash", next), ("@data", Gzip(updated)), ("@length", (long)updated.Length));
                    }
                    carried[hash] = next;
                }
                if (next is null) continue;
                Run(conn, tx, "UPDATE map_change_document SET blob_hash = @next "
                              + "WHERE change_id = @change AND kind = 'map_intent_json'",
                    ("@next", next), ("@change", changeId));
            }
        });
    }

    public override void Down() { }

    // Every list whose entries state an id, so a minted chest id never repeats any of them.
    private static readonly (string Parent, string List)[] Named =
    [
        ("", "spawns"), ("", "wools"), ("", "destroyables"), ("", "cores"), ("build", "areas"),
        ("waterLanes", "rects"), ("structures", "ironCubes"), ("structures", "walls"), ("structures", "chests"),
    ];

    // Returns the re-serialized bytes only when a wall was stored as a box, so every other blob is left as it is.
    internal static bool TryCarry(byte[] data, out byte[] updated)
    {
        updated = data;
        JsonNode? root;
        try { root = JsonNode.Parse(Encoding.UTF8.GetString(data)); }
        catch { return false; }
        if (root is not JsonObject intent || intent["structures"] is not JsonObject structures
            || structures["walls"] is not JsonArray walls) return false;
        var boxes = walls.OfType<JsonObject>().Where(wall => wall.ContainsKey("minX")).ToList();
        if (boxes.Count == 0) return false;

        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (parent, list) in Named)
            foreach (var entry in Entries(intent, parent, list))
                if (Text(entry, "id") is { Length: > 0 } stated) taken.Add(stated);
        var chests = structures["chests"] as JsonArray ?? new JsonArray();
        var minted = chests.Count;

        foreach (var wall in boxes)
        {
            int minX = Number(wall, "minX"), minZ = Number(wall, "minZ"), maxX = Number(wall, "maxX"), maxZ = Number(wall, "maxZ");
            var onMinFace = wall["chestOnMinFace"] is not JsonValue face || !face.TryGetValue<bool>(out var flag) || flag;
            var thinAlongX = maxX - minX <= maxZ - minZ;
            var (x1, z1, x2, z2) = thinAlongX
                ? ((minX + maxX) / 2, minZ, (minX + maxX) / 2, maxZ)
                : (minX, (minZ + maxZ) / 2, maxX, (minZ + maxZ) / 2);

            var stamp = wall["stamp"] as JsonObject;
            var lane = thinAlongX ? maxZ - minZ : maxX - minX;
            var count = lane <= SingleChestMaxLane ? 1 : 2;
            var facing = thinAlongX ? (onMinFace ? "-x" : "+x") : (onMinFace ? "-z" : "+z");
            var outside = thinAlongX ? (onMinFace ? minX - 1 : maxX) : (onMinFace ? minZ - 1 : maxZ);
            for (var i = 0; i < count; i++)
            {
                var along = (thinAlongX ? minZ : minX) + (int)((long)(i + 1) * lane / (count + 1));
                var name = $"chest-{++minted}";
                var id = name;
                for (var suffix = 2; !taken.Add(id); suffix++) id = $"{name}-{suffix}";
                var chest = new JsonObject
                {
                    ["x"] = thinAlongX ? outside : along,
                    ["z"] = thinAlongX ? along : outside,
                    ["facing"] = facing,
                };
                if (stamp is not null && Text(stamp, "kind").Length > 0)
                    chest["stamp"] = new JsonObject
                    {
                        ["kind"] = "chest", ["unit"] = $"{Text(stamp, "unit")}-{i}", ["image"] = Number(stamp, "image"),
                    };
                chest["id"] = id;
                chests.Add(chest);
            }

            foreach (var key in new[] { "minX", "minZ", "maxX", "maxZ", "chestOnMinFace" }) wall.Remove(key);
            wall["x1"] = x1; wall["z1"] = z1; wall["x2"] = x2; wall["z2"] = z2;
        }
        structures["chests"] = chests;
        updated = Encoding.UTF8.GetBytes(intent.ToJsonString());
        return true;
    }

    private static IEnumerable<JsonObject> Entries(JsonObject intent, string parent, string list)
    {
        var holder = parent.Length == 0 ? intent : intent[parent] as JsonObject;
        return holder?[list] is JsonArray entries ? entries.OfType<JsonObject>() : [];
    }

    private static string Text(JsonObject entry, string key) =>
        entry[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static int Number(JsonObject entry, string key) =>
        entry[key] is JsonValue value && value.TryGetValue<double>(out var number) ? (int)Math.Round(number) : 0;

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
        return Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
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
