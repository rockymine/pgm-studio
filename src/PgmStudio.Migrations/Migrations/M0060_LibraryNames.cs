using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A library row's name is letters, digits, spaces, dashes and underscores, and one name is one row of its kind.
///
/// <para><b>A name holding anything else is made one.</b> A <c>+</c> is spelled <c>plus</c>, any other character a
/// name may not hold becomes a space, and spaces are collapsed and trimmed — <c>Mesa (Bryce)</c> is
/// <c>Mesa Bryce</c>, <c>Extreme hills+ M</c> is <c>Extreme hills plus M</c>, a <c>house · part</c> name is
/// <c>house part</c>. A name left empty is <c>unnamed</c>.</para>
///
/// <para><b>A name two rows of one kind carry, compared without case, stays with the lower id</b>, and each later
/// row takes the first free count after it, <c>name-2</c>. Every table then holds a unique index over its names,
/// which is what keeps one name one row.</para>
///
/// <para><b>The refinement each map last stated follows a renamed row</b>, where it names one by its name: a theme
/// where a theme stands, a room style where a room style stands, a recipe in the dressing's registry by its kind,
/// the biome, and a pattern anywhere else. A name two rows carried could not be resolved and is left as it was.
/// Only the current refinement is rewritten, and one whose JSON will not parse is left exactly as it was found.</para>
/// </summary>
[Migration(60, "A library name is letters, digits, spaces, dashes and underscores, and names one row")]
public sealed partial class M0060_LibraryNames : Migration
{
    private static readonly string[] Tables =
    [
        "style", "theme", "roof_style", "storey_style", "porch_style", "room_style", "tree_style", "boulder_style",
        "biome_pattern",
    ];

    public override void Up()
    {
        Execute.WithConnection((connection, transaction) =>
        {
            var db = new Db(connection, transaction);
            var renamed = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (var table in Tables)
            {
                var rows = db.Rows($"SELECT id, name FROM {table} ORDER BY id");
                var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var shared = rows.GroupBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1).Select(group => group.Key)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var names = renamed[table] = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (id, name) in rows)
                {
                    var tidy = Tidy(name);
                    var free = tidy;
                    for (var count = 2; taken.Contains(free); count++) free = $"{tidy}-{count}";
                    taken.Add(free);
                    if (free == name) continue;
                    db.Run($"UPDATE {table} SET name = @name WHERE id = @id", ("@name", free), ("@id", id));
                    if (!shared.Contains(name)) names[name] = free;
                }
            }
            Follow(db, renamed);
        });
        foreach (var table in Tables)
            Create.Index($"ux_{table}_name").OnTable(table).OnColumn("name").Ascending().WithOptions().Unique();
    }

    /// <summary>The indexes go; the names stay as they were made, since every one of them is still a name.</summary>
    public override void Down()
    {
        foreach (var table in Tables) Delete.Index($"ux_{table}_name").OnTable(table);
    }

    private static string Tidy(string text)
    {
        var spelled = new StringBuilder(text.Length);
        foreach (var character in text)
            spelled.Append(character == '+' ? " plus "
                : char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character.ToString() : " ");
        var tidy = string.Join(' ', spelled.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return tidy.Length == 0 ? "unnamed" : tidy;
    }

    /// <summary>Every map's current refinement, each name it states following the row it named.</summary>
    private static void Follow(Db db, IReadOnlyDictionary<string, Dictionary<string, string>> renamed)
    {
        foreach (var (id, data) in db.Blobs("SELECT id, data FROM map_artifact WHERE kind = 'refinement_json'"))
        {
            JsonNode? refinement;
            try { refinement = JsonNode.Parse(Encoding.UTF8.GetString(data)); }
            catch (JsonException) { continue; }
            if (refinement is not JsonObject members || !Walk(members, "", renamed)) continue;
            db.Run("UPDATE map_artifact SET data = @data WHERE id = @id",
                ("@data", Encoding.UTF8.GetBytes(members.ToJsonString(Written))), ("@id", id));
        }
    }

    private static readonly JsonSerializerOptions Written = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static bool Walk(JsonNode node, string path, IReadOnlyDictionary<string, Dictionary<string, string>> renamed)
    {
        var changed = false;
        if (node is JsonObject stated && stated["library"] is JsonValue value && value.TryGetValue<string>(out var called)
            && renamed[TableAt(path, stated)].TryGetValue(called, out var name))
        {
            stated["library"] = name;
            changed = true;
        }
        switch (node)
        {
            case JsonObject members:
                foreach (var (key, child) in members.ToList())
                    if (child is not null) changed |= Walk(child, path.Length == 0 ? key : $"{path}.{key}", renamed);
                break;
            case JsonArray items:
                for (var at = 0; at < items.Count; at++)
                    if (items[at] is { } child) changed |= Walk(child, $"{path}[{at}]", renamed);
                break;
        }
        return changed;
    }

    /// <summary>The table a name at this path names a row of.</summary>
    private static string TableAt(string path, JsonObject stated)
    {
        if (path == "biome") return "biome_pattern";
        if (Theme().IsMatch(path)) return "theme";
        if (RoomShell().IsMatch(path) || HouseOwnStyle().IsMatch(path)) return "room_style";
        if (DressingStyle().IsMatch(path))
            return stated["kind"]?.GetValue<string>() switch
            {
                "tree" => "tree_style",
                "boulder" => "boulder_style",
                _ => "room_style",
            };
        return "style";
    }

    [GeneratedRegex(@"^themes\.[^.\[]+$")]
    private static partial Regex Theme();

    [GeneratedRegex(@"^roomStyles\.[^.\[]+$")]
    private static partial Regex RoomShell();

    [GeneratedRegex(@"^dressing\.props\[\d+\]\.style$")]
    private static partial Regex HouseOwnStyle();

    [GeneratedRegex(@"^dressing\.styles\.[^.\[]+$")]
    private static partial Regex DressingStyle();

    /// <summary>The handful of statements the migration needs, over the runner's own connection and
    /// transaction.</summary>
    private sealed class Db(IDbConnection connection, IDbTransaction transaction)
    {
        public void Run(string sql, params (string Name, object Value)[] parameters)
        {
            using var command = Command(sql, parameters);
            command.ExecuteNonQuery();
        }

        public List<(long Id, string Name)> Rows(string sql)
        {
            using var command = Command(sql, []);
            using var reader = command.ExecuteReader();
            var rows = new List<(long, string)>();
            while (reader.Read())
                rows.Add((Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1)));
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
