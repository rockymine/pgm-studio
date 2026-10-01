using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Every write to a map's plan, sketch layout and intent is a change the map keeps: a number per slug that never
/// repeats, who wrote it, and the documents it wrote, each kept once under its hash. The changes and the
/// slug's last number are keyed by slug, so a reload — which replaces the map's row — carries its history on.
///
/// <para>Every stored map holding one of those documents is carried in as its first change, numbered at the
/// highest revision any of them, or <c>map.artifact_revision_floor</c>, has reached, and each of them is set to
/// that number: a revision is now the number of the change that last wrote the document. The floor is then
/// dropped, because the slug's last number does its work.</para>
/// </summary>
[Migration(52, "Every write to a map's documents is a change the map keeps")]
public sealed class M0052_MapChanges : Migration
{
    private const string Json = "JSON";

    public override void Up()
    {
        Create.Table("map_change_sequence")
            .WithColumn("map_slug").AsString(190).PrimaryKey()
            .WithColumn("last_number").AsInt64().NotNullable();

        Create.Table("map_change")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_slug").AsString(190).NotNullable()
            .WithColumn("number").AsInt64().NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("writer_uuid").AsString(36).Nullable()
            .WithColumn("writer_name").AsString(64).Nullable()
            .WithColumn("token_label").AsString(100).Nullable()
            .WithColumn("origin_json").AsCustom(Json).Nullable()
            .WithColumn("note").AsCustom("TEXT").Nullable();
        Create.Index("ux_map_change_number").OnTable("map_change")
            .OnColumn("map_slug").Ascending()
            .OnColumn("number").Ascending()
            .WithOptions().Unique();

        Create.Table("document_blob")
            .WithColumn("hash").AsFixedLengthString(64).PrimaryKey()
            .WithColumn("data").AsCustom("LONGBLOB").NotNullable()
            .WithColumn("length").AsInt64().NotNullable();

        Create.Table("map_change_document")
            .WithColumn("change_id").AsInt64().NotNullable().PrimaryKey()
                .ForeignKey("fk_map_change_document_change", "map_change", "id").OnDelete(Rule.Cascade)
            .WithColumn("kind").AsString(64).NotNullable().PrimaryKey()
            .WithColumn("blob_hash").AsFixedLengthString(64).NotNullable().Indexed("ix_map_change_document_blob");

        Execute.WithConnection(CarryIn);

        Delete.Column("artifact_revision_floor").FromTable("map");
    }

    public override void Down()
    {
        Create.Column("artifact_revision_floor").OnTable("map").AsInt64().NotNullable().WithDefaultValue(0);
        Execute.Sql("""
            UPDATE map m JOIN map_change_sequence s ON s.map_slug = m.slug
            SET m.artifact_revision_floor = s.last_number
            """);
        Delete.Table("map_change_document");
        Delete.Table("document_blob");
        Delete.Table("map_change");
        Delete.Table("map_change_sequence");
    }

    /// <summary>Each stored map's kept documents, as its first change.</summary>
    private static void CarryIn(IDbConnection connection, IDbTransaction transaction)
    {
        var maps = new List<(long Id, string Slug, long Floor, DateTime At)>();
        using (var select = Command(connection, transaction,
                   "SELECT id, slug, artifact_revision_floor, updated_at FROM map"))
        using (var reader = select.ExecuteReader())
            while (reader.Read())
                maps.Add((reader.GetInt64(0), reader.GetString(1), reader.GetInt64(2), reader.GetDateTime(3)));

        foreach (var map in maps)
        {
            var documents = new List<(string Kind, byte[] Data, long Revision)>();
            using (var select = Command(connection, transaction,
                       "SELECT kind, data, revision FROM map_artifact WHERE map_id = @map AND kind IN "
                       + "('plan_json', 'sketch_layout_json', 'map_intent_json')",
                       ("@map", map.Id)))
            using (var reader = select.ExecuteReader())
                while (reader.Read())
                    documents.Add((reader.GetString(0), (byte[])reader.GetValue(1), reader.GetInt64(2)));
            if (documents.Count == 0) continue;

            var number = Math.Max(map.Floor, documents.Max(document => document.Revision));
            Run(connection, transaction,
                "INSERT INTO map_change_sequence (map_slug, last_number) VALUES (@slug, @number)",
                ("@slug", map.Slug), ("@number", number));
            Run(connection, transaction,
                "INSERT INTO map_change (map_slug, number, created_at) VALUES (@slug, @number, @at)",
                ("@slug", map.Slug), ("@number", number), ("@at", map.At));
            long changeId;
            using (var id = Command(connection, transaction, "SELECT LAST_INSERT_ID()"))
                changeId = Convert.ToInt64(id.ExecuteScalar());

            foreach (var (kind, data, _) in documents)
            {
                var hash = Convert.ToHexStringLower(SHA256.HashData(data));
                Run(connection, transaction,
                    "INSERT IGNORE INTO document_blob (hash, data, length) VALUES (@hash, @data, @length)",
                    ("@hash", hash), ("@data", Gzip(data)), ("@length", (long)data.Length));
                Run(connection, transaction,
                    "INSERT INTO map_change_document (change_id, kind, blob_hash) VALUES (@change, @kind, @hash)",
                    ("@change", changeId), ("@kind", kind), ("@hash", hash));
                Run(connection, transaction,
                    "UPDATE map_artifact SET revision = @number WHERE map_id = @map AND kind = @kind",
                    ("@number", number), ("@map", map.Id), ("@kind", kind));
            }
        }
    }

    private static byte[] Gzip(byte[] data)
    {
        using var packed = new MemoryStream();
        using (var gzip = new GZipStream(packed, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(data);
        return packed.ToArray();
    }

    private static IDbCommand Command(
        IDbConnection connection, IDbTransaction transaction, string sql, params (string Name, object Value)[] values)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    private static void Run(
        IDbConnection connection, IDbTransaction transaction, string sql, params (string Name, object Value)[] values)
    {
        using var command = Command(connection, transaction, sql, values);
        command.ExecuteNonQuery();
    }
}
