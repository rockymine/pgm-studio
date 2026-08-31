using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A plan marker's <c>at</c> becomes an offset in <b>blocks</b> from its piece's minimum corner, where it was
/// an offset in cells. The numbers change and nothing moves: <c>at × globals.cell</c> is exact on the
/// half-block lattice both units land on, so every marker keeps the block it named and the parity that sizes
/// its pad.
///
/// <para>A plan document lives in two places — the <c>plan</c> corpus table and a map's <c>plan_json</c>
/// artifact — and both are rewritten here, because the reader refuses a document that states the earlier
/// version rather than guessing which unit a coordinate is in.</para>
/// </summary>
[Migration(29, "A plan marker's offset is in blocks")]
public sealed class M0029_PlanMarkersInBlocks : Migration
{
    private static readonly string[] MarkerKinds = ["spawns", "wools", "iron", "destroyables", "cores"];

    public override void Up() => Execute.WithConnection((connection, transaction) =>
    {
        Rewrite(connection, transaction, "SELECT id, plan_json FROM `plan`",
            "UPDATE `plan` SET plan_json = @json WHERE id = @id", asText: true);
        Rewrite(connection, transaction,
            "SELECT id, data FROM map_artifact WHERE kind = 'plan_json'",
            "UPDATE map_artifact SET data = @json WHERE id = @id", asText: false);
    });

    /// <summary>Down is deliberately empty. The conversion is a multiply and its inverse is a divide, but a
    /// document written after this migration states markers a cell grid cannot express — a 7-block offset on
    /// a cell-5 board — so dividing back would move them. A rollback keeps the documents it finds.</summary>
    public override void Down() { }

    private static void Rewrite(IDbConnection connection, IDbTransaction transaction, string read, string write, bool asText)
    {
        var rows = new List<(long Id, string Json)>();
        using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = read;
            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var json = asText || reader.GetValue(1) is not byte[] bytes
                    ? reader.GetString(1)
                    : Encoding.UTF8.GetString(bytes);
                rows.Add((id, json));
            }
        }

        foreach (var (id, json) in rows)
        {
            if (Converted(json) is not { } converted) continue;
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = write;
            Bind(update, "@json", asText ? converted : Encoding.UTF8.GetBytes(converted));
            Bind(update, "@id", id);
            update.ExecuteNonQuery();
        }
    }

    // Null where the document is not a version-1 plan: anything already converted, and anything that will not
    // parse, is left exactly as it was found.
    private static string? Converted(string json)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return null; }
        if (root is not JsonObject plan) return null;
        if (plan["plan"]?.GetValue<int>() is not 1 and not null) return null;

        var cell = plan["globals"]?["cell"]?.GetValue<int>() ?? 5;
        if (plan["placements"] is JsonObject placements)
            foreach (var kind in MarkerKinds)
                if (placements[kind] is JsonArray markers)
                    foreach (var marker in markers)
                        if (marker?["at"] is JsonArray at && at.Count >= 2)
                            for (var axis = 0; axis < 2; axis++)
                                at[axis] = JsonValue.Create(at[axis]!.GetValue<double>() * cell);

        plan["plan"] = 2;
        return plan.ToJsonString();
    }

    private static void Bind(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
