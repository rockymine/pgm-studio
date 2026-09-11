using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A role shape's <c>relief_scope</c> says which of the two things it does.
///
/// <para>One word covered both: a shape tagged <c>hold</c> was pinned at the top it stated when the author
/// had corrected its height and seated on whatever surface the relief settled on when they had not, and the
/// field that chose between them was <c>height_authored</c> — a fact about who last edited the number, doing
/// duty as a mode. The words are now <c>follow</c> and <c>hold</c> and each does one thing, so a stored
/// <c>hold</c> is rewritten to the word for the behaviour it was getting: <c>follow</c> where no corrected
/// height stands behind it, and <c>hold</c> where one does.</para>
///
/// <para>Every other shape crosses untouched — <c>exclude</c>, a shape with no scope, and a shape with no
/// role, which the rasterizer never read a scope on.</para>
/// </summary>
[Migration(36, "A role shape's relief scope says whether it follows or holds")]
public sealed class M0036_ReliefScopeSaysWhichItIs : Migration
{
    public override void Up() => Rewrite(toFollow: true);

    /// <summary>Both words back to the one that covered them. It inverts exactly: the reader that read
    /// <c>hold</c> took <c>height_authored</c> to decide, and that field is untouched here.</summary>
    public override void Down() => Rewrite(toFollow: false);

    private void Rewrite(bool toFollow) => Execute.WithConnection((connection, transaction) =>
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
            if (Moved(json, toFollow) is not { } converted) continue;
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE map_artifact SET data = @json WHERE id = @id";
            Bind(update, "@json", Encoding.UTF8.GetBytes(converted));
            Bind(update, "@id", id);
            update.ExecuteNonQuery();
        }
    });

    // Null where nothing moved: a document that will not parse, and one carrying no role shape whose scope
    // this reads. A shape is walked wherever it sits, since a layout nests its shapes per layer.
    private static string? Moved(string json, bool toFollow)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return null; }
        if (root is not JsonObject layout) return null;

        var moved = false;
        foreach (var shape in Shapes(layout))
        {
            if (shape["role"] is not JsonValue) continue;
            var scope = shape["relief_scope"]?.GetValue<string?>();
            var authored = shape["height_authored"]?.GetValue<bool?>() == true;

            if (toFollow)
            {
                if (scope != "hold" || authored) continue;
                shape["relief_scope"] = JsonValue.Create("follow");
            }
            else
            {
                if (scope != "follow") continue;
                shape["relief_scope"] = JsonValue.Create("hold");
            }
            moved = true;
        }
        return moved ? layout.ToJsonString() : null;
    }

    /// <summary>Every shape object the document holds, over every layer and over the one a flat document
    /// keeps at its root — the same two shapes a layout is stored in.</summary>
    private static IEnumerable<JsonObject> Shapes(JsonObject layout)
    {
        foreach (var array in ShapeArrays(layout))
            foreach (var node in array)
                if (node is JsonObject shape) yield return shape;
    }

    private static IEnumerable<JsonArray> ShapeArrays(JsonObject layout)
    {
        if (layout["layout"]?["shapes"] is JsonArray flat) yield return flat;
        if (layout["shapes"] is JsonArray bare) yield return bare;
        if (layout["layers"] is not JsonArray layers) yield break;
        foreach (var layer in layers)
            if (layer?["layout"]?["shapes"] is JsonArray nested) yield return nested;
    }

    private static void Bind(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
