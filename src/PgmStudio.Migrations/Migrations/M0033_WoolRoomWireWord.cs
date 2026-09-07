using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The wool room's binding moves from <c>roomStyles.cage</c> to <c>roomStyles.wool</c> in every stored sketch
/// layout. A board binds a shell for a wool room and a spawn room, and the reader takes the new key alone, so
/// a row still holding the old one would stamp the built-in shell over a board that bound a style.
///
/// <para>The binding's three states cross unchanged, because they are three different answers: an object is
/// the bound style, an explicit null is open ground with no building over it, and an absent key is that
/// kind's built-in shell. A row that already states the new key, one with no <c>roomStyles</c> object, and
/// one whose JSON will not parse are each left exactly as they were found.</para>
/// </summary>
[Migration(33, "The wool room's binding is keyed 'wool'")]
public sealed class M0033_WoolRoomWireWord : Migration
{
    public override void Up() => Rewrite("cage", "wool");

    /// <summary>The move in reverse. Renaming a key loses nothing, so it inverts exactly.</summary>
    public override void Down() => Rewrite("wool", "cage");

    private void Rewrite(string from, string to) => Execute.WithConnection((connection, transaction) =>
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
            if (Moved(json, from, to) is not { } converted) continue;
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE map_artifact SET data = @json WHERE id = @id";
            Bind(update, "@json", Encoding.UTF8.GetBytes(converted));
            Bind(update, "@id", id);
            update.ExecuteNonQuery();
        }
    });

    // Null where there is nothing to move: a document that will not parse, one with no roomStyles object, and
    // one that does not state the key being moved. TryGetPropertyValue is what separates a key holding a JSON
    // null from a key that is absent, which the indexer alone answers null for both of.
    private static string? Moved(string json, string from, string to)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return null; }
        if (root is not JsonObject layout) return null;
        if (layout["roomStyles"] is not JsonObject styles) return null;
        if (!styles.TryGetPropertyValue(from, out var bound)) return null;

        var carried = bound?.DeepClone();
        styles.Remove(from);
        styles.Remove(to);
        styles[to] = carried;
        return layout.ToJsonString();
    }

    private static void Bind(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
