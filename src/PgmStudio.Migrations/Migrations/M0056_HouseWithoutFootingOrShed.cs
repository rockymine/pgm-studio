using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Every stored house is carried to the shape a house is saved in: no footing (<c>HS7</c>), and no shed for its
/// roof, a wing's roof or its porch's canopy (<c>HS14</c>). Left as they were, each would be refused the next
/// time it is saved.
///
/// <para><b>A shed becomes a gable</b>, the form a porch states when it names none: a room style's roof and porch
/// canopy, a porch style's canopy and a roof style's form. A porch row that names no canopy defaults to a
/// gable.</para>
///
/// <para><b>A footing is taken off.</b> The course binding one leaves every room style; the material it bound
/// stays in the library.</para>
///
/// <para><b>Each map's current documents say the same.</b> Its sketch layout and its refinement are walked for
/// houses: a foundation's footing is stated as none, and a shed where a house roof, a porch or a wing's spec
/// names its form is a gable. A change keeps the document as it was written, and JSON that will not parse is
/// left exactly as it was found.</para>
/// </summary>
[Migration(56, "A house stores no footing and no shed")]
public sealed class M0056_HouseWithoutFootingOrShed : Migration
{
    private const string Shed = "shed", Gable = "gable";

    private static readonly (string Table, string Column)[] Forms =
        [("room_style", "roof_form"), ("room_style", "porch_roof"), ("porch_style", "roof_form"), ("roof_style", "form")];

    public override void Up()
    {
        foreach (var (table, column) in Forms)
            Execute.Sql($"UPDATE {table} SET {column} = '{Gable}' WHERE LOWER({column}) = '{Shed}'");
        Execute.Sql("DELETE FROM room_style_course WHERE part = 'sill'");
        Alter.Table("room_style").AlterColumn("porch_roof").AsString(16).NotNullable().WithDefaultValue(Gable);
        Alter.Table("porch_style").AlterColumn("roof_form").AsString(16).NotNullable().WithDefaultValue(Gable);
        Execute.WithConnection(CarryDocuments);
    }

    /// <summary>The two defaults go back to a shed. What was carried stays carried: a gable this migration wrote
    /// cannot be told from one that was stored as a gable.</summary>
    public override void Down()
    {
        Alter.Table("room_style").AlterColumn("porch_roof").AsString(16).NotNullable().WithDefaultValue(Shed);
        Alter.Table("porch_style").AlterColumn("roof_form").AsString(16).NotNullable().WithDefaultValue(Shed);
    }

    private static void CarryDocuments(IDbConnection connection, IDbTransaction transaction)
    {
        var rows = new List<(long Id, string Json)>();
        using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText =
                "SELECT id, data FROM map_artifact WHERE kind IN ('sketch_layout_json', 'refinement_json')";
            using var reader = select.ExecuteReader();
            while (reader.Read())
                rows.Add((reader.GetInt64(0), reader.GetValue(1) is byte[] bytes
                    ? Encoding.UTF8.GetString(bytes)
                    : reader.GetString(1)));
        }

        foreach (var (id, json) in rows)
        {
            if (Carried(json) is not { } carried) continue;
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE map_artifact SET data = @json WHERE id = @id";
            Bind(update, "@json", Encoding.UTF8.GetBytes(carried));
            Bind(update, "@id", id);
            update.ExecuteNonQuery();
        }
    }

    // Null where nothing moved.
    private static string? Carried(string json)
    {
        JsonNode? document;
        try { document = JsonNode.Parse(json); }
        catch (JsonException) { return null; }
        return document is not null && Carry(document, key: null) ? document.ToJsonString() : null;
    }

    /// <summary>One node, reached under <paramref name="key"/>, and everything below it: a foundation's footing
    /// stated as none, a porch's shed canopy and a house roof's or a wing spec's shed form stated as a
    /// gable.</summary>
    private static bool Carry(JsonNode node, string? key)
    {
        var moved = false;
        switch (node)
        {
            case JsonObject members:
                if (key == "foundation" && members["footing"] is not null)
                {
                    members["footing"] = null;
                    moved = true;
                }
                else if (key == "porch" && IsShed(members["roof"]))
                {
                    members["roof"] = Gable;
                    moved = true;
                }
                else if (key is "roof" or "spec" && IsShed(members["form"]))
                {
                    members["form"] = Gable;
                    moved = true;
                }
                foreach (var (childKey, child) in members.ToList())
                    if (child is not null) moved |= Carry(child, childKey);
                break;
            case JsonArray items:
                foreach (var item in items)
                    if (item is not null) moved |= Carry(item, key: null);
                break;
        }
        return moved;
    }

    private static bool IsShed(JsonNode? form) =>
        form is JsonValue value && value.TryGetValue<string>(out var word)
        && string.Equals(word, Shed, StringComparison.OrdinalIgnoreCase);

    private static void Bind(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
