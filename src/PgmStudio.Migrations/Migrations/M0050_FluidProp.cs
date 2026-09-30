using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The dressing prop that carves a bed and fills it is kind <c>fluid</c> in every stored sketch layout, because
/// it fills with water or lava. The reader takes the new word alone, so a row still holding <c>water</c> would
/// refuse to load its dressing.
///
/// <para>Only a prop's <c>kind</c> moves. Its <c>fluid</c> field keeps <c>water</c> or <c>lava</c>, and a row
/// with no dressing, no <c>water</c> prop, or JSON that will not parse is left exactly as it was found.</para>
/// </summary>
[Migration(50, "The water prop is kind 'fluid'")]
public sealed class M0050_FluidProp : Migration
{
    public override void Up() => Rewrite("water", "fluid");

    /// <summary>The rename in reverse. No stored prop is kind <c>fluid</c> before this migration, so it inverts
    /// exactly.</summary>
    public override void Down() => Rewrite("fluid", "water");

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
            if (Renamed(json, from, to) is not { } converted) continue;
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE map_artifact SET data = @json WHERE id = @id";
            Bind(update, "@json", Encoding.UTF8.GetBytes(converted));
            Bind(update, "@id", id);
            update.ExecuteNonQuery();
        }
    });

    // Null where nothing moved.
    private static string? Renamed(string json, string from, string to)
    {
        JsonObject? layout;
        try { layout = JsonNode.Parse(json) as JsonObject; }
        catch (JsonException) { return null; }
        if (layout?["dressing"]?["props"] is not JsonArray props) return null;

        var moved = false;
        foreach (var node in props)
        {
            if (node is not JsonObject prop || prop["kind"] is not JsonValue kind
                || !kind.TryGetValue<string>(out var word) || word != from) continue;
            prop["kind"] = to;
            moved = true;
        }
        return moved ? layout.ToJsonString() : null;
    }

    private static void Bind(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
