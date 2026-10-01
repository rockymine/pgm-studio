using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The library's house styles carry the names the author's review gave them, and the styles the review rejected
/// leave it.
///
/// <para><b>A renamed style takes its parts with it.</b> The seed files every material, roof, porch and storey a
/// house binds as <c>&lt;house&gt; · &lt;part&gt;</c>, so those rows are renamed beside the room style, and the
/// next start's seed lays the reviewed style over the renamed row. Its footing does not move with it: no style is
/// saved with one (<c>HS7</c>), so the sill course and the material only it bound leave. Where a row under the
/// new name is already there, the old one is the duplicate and leaves the way a rejected style does.</para>
///
/// <para><b>A rejected style leaves with the parts nothing else binds.</b> Its room style goes with its courses
/// and storeys; a part another row still binds — a material a theme uses, a roof another house wears — stays
/// under the name it has.</para>
///
/// <para><b>The refinement each map last stated names the row by its new name.</b> It is the document a source is
/// stated from again, so a renamed row it still called by the old name would refuse the next apply. Only the
/// current refinement is rewritten: a change keeps the document as it was written. A name of a rejected style is
/// left as it is — the map holds its copy, and the next apply says the row is gone. A refinement whose JSON will
/// not parse is left exactly as it was found.</para>
/// </summary>
[Migration(55, "The library's house styles carry the review's names, and the rejected leave")]
public sealed partial class M0055_HouseStyleNames : Migration
{
    private static readonly Dictionary<string, string> Renamed = new(StringComparer.OrdinalIgnoreCase)
    {
        // the kept styles
        ["17h-croft"] = "brick-roofed-stone-cottage",
        ["17h-hall"] = "brick-roofed-stone-and-spruce-house",
        ["17h-hall-spruce"] = "hay-roofed-stone-and-spruce-house",
        ["hw-stonehouse"] = "banded-stone-house",
        ["sb-spawn"] = "andesite-gabled-house",
        ["showcase-hall"] = "brick-roofed-stone-and-dark-oak-house",
        ["showcase-hall-hay"] = "hay-roofed-stone-and-dark-oak-house",
        ["sn-compass-well"] = "diorite-blue-clay-pyramid-house",
        ["stilts"] = "oak-stilt-house",
        ["talltimber-cottage-jungle"] = "jungle-framed-cottage",
        // the presets
        ["alpine mining"] = "acacia-log-checkered-house",
        ["desert brick"] = "brick-roofed-sandstone-house",
        ["townside"] = "oak-and-spruce-timbered-house",
        ["townside on stilts"] = "jungle-trimmed-stilt-house",
        ["cottage"] = "spruce-roofed-stone-cottage",
        ["longhouse"] = "spruce-roofed-stone-longhouse",
        ["counting house"] = "stone-and-sandstone-townhouse",
        ["darkwood"] = "black-clay-and-dark-oak-house",
    };

    private static readonly string[] Rejected =
    [
        "desert-house", "hb-cage", "hb-spawn", "hoar-longhall", "hoar-steading", "hoar-store", "hoar-watch",
        "hw-assay", "hw-minehouse", "lk-spawn", "ow-cage", "rk-spawn", "sb-blockhouse", "sb-blockhouse-lo",
        "showcase-cage", "talltimber-cottage", "talltimber-hall", "talltimber-hall-jungle", "talltimber-store",
        "stonemason", "sandy mushroom", "terrace", "workshop", "diorite pyramid",
    ];

    /// <summary>The tables a house's parts are filed in, each with what binds a row of it.</summary>
    private static readonly (string Table, string[] BoundBy)[] Parts =
    [
        ("roof_style", ["SELECT COUNT(*) FROM room_style WHERE roof_style_id = @id"]),
        ("porch_style", ["SELECT COUNT(*) FROM room_style WHERE porch_style_id = @id"]),
        ("storey_style", ["SELECT COUNT(*) FROM room_style_storey WHERE storey_style_id = @id"]),
        ("style",
        [
            "SELECT COUNT(*) FROM room_style_course WHERE style_id = @id",
            "SELECT COUNT(*) FROM theme_bucket WHERE style_id = @id",
            "SELECT COUNT(*) FROM roof_style_course WHERE style_id = @id",
            "SELECT COUNT(*) FROM storey_style_course WHERE style_id = @id",
        ]),
    ];

    private const string Separator = " · ";

    public override void Up() => Execute.WithConnection((connection, transaction) =>
    {
        var db = new Db(connection, transaction);
        foreach (var name in Rejected) Remove(db, name);
        foreach (var (old, renamed) in Renamed)
        {
            if (db.Ids("SELECT id FROM room_style WHERE name = @name", ("@name", renamed)).Count > 0)
            {
                Remove(db, old);
                continue;
            }
            db.Run("UPDATE room_style SET name = @renamed WHERE name = @old", ("@renamed", renamed), ("@old", old));
            // No style is saved with a footing, so the seeded house's sill course goes before its parts move.
            db.Run("DELETE FROM room_style_course WHERE part = 'sill' AND room_style_id IN "
                   + "(SELECT id FROM room_style WHERE name = @renamed)", ("@renamed", renamed));
            foreach (var (id, _) in db.Named("SELECT id, name FROM style WHERE name = @sill", ("@sill", old + Separator + "sill")))
                RemovePart(db, "style", Parts[^1].BoundBy, id);
            foreach (var (table, boundBy) in Parts)
                foreach (var (id, name) in db.Named($"SELECT id, name FROM {table} WHERE name LIKE @prefix",
                             ("@prefix", old + Separator + "%")))
                {
                    var target = renamed + name[old.Length..];
                    if (db.Ids($"SELECT id FROM {table} WHERE name = @name", ("@name", target)).Count > 0)
                        RemovePart(db, table, boundBy, id);
                    else db.Run($"UPDATE {table} SET name = @name WHERE id = @id", ("@name", target), ("@id", id));
                }
        }
        RenameInRefinements(db);
    });

    /// <summary>A style's room style and every part of it nothing else binds.</summary>
    private static void Remove(Db db, string house)
    {
        db.Run("DELETE FROM room_style WHERE name = @name", ("@name", house));
        foreach (var (table, boundBy) in Parts)
            foreach (var (id, _) in db.Named($"SELECT id, name FROM {table} WHERE name LIKE @prefix",
                         ("@prefix", house + Separator + "%")))
                RemovePart(db, table, boundBy, id);
    }

    private static void RemovePart(Db db, string table, string[] boundBy, long id)
    {
        if (boundBy.Any(query => db.Count(query, ("@id", id)) > 0)) return;
        db.Run($"DELETE FROM {table} WHERE id = @id", ("@id", id));
    }

    /// <summary>Every map's current refinement, with each renamed style named by its new name: a room style where
    /// a room style stands — a room's shell, a house prop's own style, a house in the dressing's registry — and a
    /// house's part wherever a material is named.</summary>
    private static void RenameInRefinements(Db db)
    {
        foreach (var (id, data) in db.Blobs("SELECT id, data FROM map_artifact WHERE kind = 'refinement_json'"))
        {
            if (Parsed(Encoding.UTF8.GetString(data)) is not JsonObject refinement) continue;
            if (!RenameNamed(refinement, "")) continue;
            db.Run("UPDATE map_artifact SET data = @data WHERE id = @id",
                ("@data", Encoding.UTF8.GetBytes(refinement.ToJsonString())), ("@id", id));
        }
    }

    /// <summary>The document, or null for JSON that will not parse — a row left exactly as it was found.</summary>
    private static JsonNode? Parsed(string json)
    {
        try { return JsonNode.Parse(json); }
        catch (JsonException) { return null; }
    }

    private static bool RenameNamed(JsonNode node, string path)
    {
        var changed = false;
        if (node is JsonObject named && named["library"] is JsonValue value && value.TryGetValue<string>(out var called))
        {
            if (IsRoomStyle(path, named) && Renamed.TryGetValue(called, out var renamed))
            {
                named["library"] = renamed;
                changed = true;
            }
            else if (called.IndexOf(Separator, StringComparison.Ordinal) is var at and > 0
                     && Renamed.TryGetValue(called[..at], out var house))
            {
                named["library"] = house + called[at..];
                changed = true;
            }
        }
        switch (node)
        {
            case JsonObject members:
                foreach (var (key, child) in members.ToList())
                    if (child is not null) changed |= RenameNamed(child, path.Length == 0 ? key : $"{path}.{key}");
                break;
            case JsonArray items:
                for (var at = 0; at < items.Count; at++)
                    if (items[at] is { } child) changed |= RenameNamed(child, $"{path}[{at}]");
                break;
        }
        return changed;
    }

    /// <summary>Whether a name at this path stands for a room style: the shell a kind of room is stamped in, a
    /// house prop's style stated in place, or a house in the dressing's registry.</summary>
    private static bool IsRoomStyle(string path, JsonObject named) =>
        RoomShell().IsMatch(path) || HouseOwnStyle().IsMatch(path)
        || (DressingStyle().IsMatch(path) && named["kind"]?.GetValue<string>() == "house");

    [GeneratedRegex(@"^roomStyles\.[^.\[]+$")]
    private static partial Regex RoomShell();

    [GeneratedRegex(@"^dressing\.props\[\d+\]\.style$")]
    private static partial Regex HouseOwnStyle();

    [GeneratedRegex(@"^dressing\.styles\.[^.\[]+$")]
    private static partial Regex DressingStyle();

    /// <summary>Nothing to undo into: a rejected style's rows are gone, and the seed files a kept style by the name
    /// it now carries, so a schema rolled back still reads these rows under their new names.</summary>
    public override void Down() { }

    /// <summary>The handful of statements the migration needs, over the runner's own connection and
    /// transaction.</summary>
    private sealed class Db(IDbConnection connection, IDbTransaction transaction)
    {
        public void Run(string sql, params (string Name, object Value)[] parameters)
        {
            using var command = Command(sql, parameters);
            command.ExecuteNonQuery();
        }

        public long Count(string sql, params (string Name, object Value)[] parameters)
        {
            using var command = Command(sql, parameters);
            return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        public List<long> Ids(string sql, params (string Name, object Value)[] parameters)
        {
            using var command = Command(sql, parameters);
            using var reader = command.ExecuteReader();
            var ids = new List<long>();
            while (reader.Read()) ids.Add(reader.GetInt64(0));
            return ids;
        }

        public List<(long Id, string Name)> Named(string sql, params (string Name, object Value)[] parameters)
        {
            using var command = Command(sql, parameters);
            using var reader = command.ExecuteReader();
            var rows = new List<(long, string)>();
            while (reader.Read()) rows.Add((reader.GetInt64(0), reader.GetString(1)));
            return rows;
        }

        public List<(long Id, byte[] Data)> Blobs(string sql)
        {
            using var command = Command(sql, []);
            using var reader = command.ExecuteReader();
            var rows = new List<(long, byte[])>();
            while (reader.Read()) rows.Add((reader.GetInt64(0), (byte[])reader.GetValue(1)));
            return rows;
        }

        private IDbCommand Command(string sql, (string Name, object Value)[] parameters)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }
            return command;
        }
    }
}
