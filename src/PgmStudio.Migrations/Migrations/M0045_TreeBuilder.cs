using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A copied tree records who built it, and a map's copied trees carry the name, so a map a tree stands on can
/// credit its builder.
///
/// <para>The library's copied rows gain <c>cut_builder</c>. Every tree cut from the tree showcase — the world
/// whose directory is <c>tree-showcase</c> — was built by rockymine, the author's own statement, so those rows
/// are given the name; a row cut from any other world keeps none.</para>
///
/// <para>A recipe is copied into a map rather than referenced, so the maps already stored carry their trees
/// without the name. Each copied tree in a stored sketch layout whose body is a showcase row's is given that
/// row's builder. Two bodies are one tree when they hold the same blocks at the same offsets, or when they do
/// once the planks are left out and each is moved to its own lowest corner, so a copy that carries no planks
/// still finds the row it was pulled from. A body matching no row is left alone.</para>
/// </summary>
[Migration(45, "A copied tree records who built it, and a map's copied trees carry it")]
public sealed class M0045_TreeBuilder : Migration
{
    private const int Planks = 5;

    public override void Up()
    {
        Alter.Table("tree_style").AddColumn("cut_builder").AsString(64).Nullable();
        Execute.Sql("UPDATE tree_style SET cut_builder = 'rockymine' "
                    + "WHERE form = 'copied' AND cut_world LIKE '%/tree-showcase'");
        Execute.WithConnection((connection, transaction) =>
        {
            var builders = LibraryBuilders(connection, transaction);
            if (builders.Count > 0) Rewrite(connection, transaction, json => Stamped(json, builders));
        });
    }

    /// <summary>The name taken back off every map's trees, then the column. Inverts exactly: a map's recipe
    /// carries a builder only where this migration or a later pull put one.</summary>
    public override void Down()
    {
        Execute.WithConnection((connection, transaction) => Rewrite(connection, transaction, Unstamped));
        Delete.Column("cut_builder").FromTable("tree_style");
    }

    /// <summary>Each builder-carrying library row's body, under both of the keys a stored body is matched by.</summary>
    private static Dictionary<string, string> LibraryBuilders(IDbConnection connection, IDbTransaction transaction)
    {
        var builders = new Dictionary<string, string>(StringComparer.Ordinal);
        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT body, cut_builder FROM tree_style WHERE form = 'copied' AND cut_builder IS NOT NULL";
        using var reader = select.ExecuteReader();
        while (reader.Read())
        {
            if (Rows(reader.GetString(0)) is not { Count: > 0 } body) continue;
            var builder = reader.GetString(1);
            builders.TryAdd(Exact(body), builder);
            builders.TryAdd(WithoutPlanks(body), builder);
        }
        return builders;
    }

    private static void Rewrite(IDbConnection connection, IDbTransaction transaction, Func<string, string?> convert)
    {
        var rows = new List<(long Id, string Json)>();
        using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = "SELECT id, data FROM map_artifact WHERE kind = 'sketch_layout_json'";
            using var reader = select.ExecuteReader();
            while (reader.Read())
                rows.Add((reader.GetInt64(0), reader.GetValue(1) is byte[] bytes
                    ? Encoding.UTF8.GetString(bytes)
                    : reader.GetString(1)));
        }

        foreach (var (id, json) in rows)
        {
            if (convert(json) is not { } converted) continue;
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE map_artifact SET data = @json WHERE id = @id";
            Bind(update, "@json", Encoding.UTF8.GetBytes(converted));
            Bind(update, "@id", id);
            update.ExecuteNonQuery();
        }
    }

    // Null where nothing moved: a document that will not parse, and one whose copied trees match no row.
    private static string? Stamped(string json, Dictionary<string, string> builders)
    {
        if (Layout(json) is not { } layout) return null;
        var moved = false;
        foreach (var tree in CopiedTrees(layout))
        {
            if (tree["builder"] is not null || Rows(tree["body"]?.ToJsonString()) is not { Count: > 0 } body) continue;
            if (!builders.TryGetValue(Exact(body), out var builder)
                && !builders.TryGetValue(WithoutPlanks(body), out builder)) continue;
            tree["builder"] = builder;
            moved = true;
        }
        return moved ? layout.ToJsonString() : null;
    }

    private static string? Unstamped(string json)
    {
        if (Layout(json) is not { } layout) return null;
        var moved = false;
        foreach (var tree in CopiedTrees(layout))
            moved |= tree.Remove("builder");
        return moved ? layout.ToJsonString() : null;
    }

    private static JsonObject? Layout(string json)
    {
        try { return JsonNode.Parse(json) as JsonObject; }
        catch (JsonException) { return null; }
    }

    /// <summary>Every copied tree recipe in the layout's dressing registry, which is where a pull files one.</summary>
    private static IEnumerable<JsonObject> CopiedTrees(JsonObject layout)
    {
        if (layout["dressing"]?["styles"] is not JsonObject styles) yield break;
        foreach (var (_, node) in styles)
            if (node is JsonObject style && Text(style["kind"]) == "tree" && Text(style["form"]) == "copied")
                yield return style;
    }

    private static string? Text(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>A body's five-number rows, or null where it is not a list of them.</summary>
    private static List<int[]>? Rows(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<int[]>>(json)?.Where(row => row.Length >= 5).ToList(); }
        catch (JsonException) { return null; }
    }

    private static string Exact(IEnumerable<int[]> body) => Key(body);

    private static string WithoutPlanks(List<int[]> body)
    {
        var kept = body.Where(row => row[3] != Planks).ToList();
        if (kept.Count == 0) return "";
        int x = kept.Min(row => row[0]), y = kept.Min(row => row[1]), z = kept.Min(row => row[2]);
        return "~" + Key(kept.Select(row => new[] { row[0] - x, row[1] - y, row[2] - z, row[3], row[4] }));
    }

    private static string Key(IEnumerable<int[]> body)
        => string.Join(';', body.Select(row => string.Join(',', row.Take(5))).Order(StringComparer.Ordinal));

    private static void Bind(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
