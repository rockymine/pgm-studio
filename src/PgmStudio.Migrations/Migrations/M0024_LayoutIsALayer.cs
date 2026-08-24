using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Moves every stored sketch layout's top-level <c>layout</c> object into a single <c>ground</c> layer.
///
/// <para>A layout is composed of layers, and the ground is one of them. The document used to keep the ground
/// shapes under <c>layout</c> and anything stacked over them under <c>layers</c>, so the ground layer was the
/// one layer that was not in the stack — which is why seven readers disagreed about what a document carrying
/// both meant. The reader now takes <c>layers</c> and nothing else, so a row still holding <c>layout</c>
/// would rasterize to an empty board.</para>
///
/// <para>Rows already carrying a <c>layers</c> array are left alone, and so is a row whose JSON will not
/// parse — <c>JSON_VALID</c> guards the update rather than the statement failing on one bad blob and taking
/// the migration with it.</para>
/// </summary>
[Migration(24, "Move each stored layout's top-level shapes into a ground layer")]
public sealed class M0024_LayoutIsALayer : Migration
{
    public override void Up() => Execute.Sql(
        """
        UPDATE map_artifact
           SET data = CAST(
                 JSON_REMOVE(
                   JSON_SET(
                     CAST(data AS CHAR),
                     '$.layers',
                     JSON_ARRAY(JSON_OBJECT(
                       'id',     'ground',
                       'name',   'Ground',
                       'base_y', 0,
                       'layout', JSON_EXTRACT(CAST(data AS CHAR), '$.layout')))),
                   '$.layout')
               AS BINARY)
         WHERE kind = 'sketch_layout_json'
           AND JSON_VALID(CAST(data AS CHAR))
           AND JSON_EXTRACT(CAST(data AS CHAR), '$.layout') IS NOT NULL
           AND JSON_EXTRACT(CAST(data AS CHAR), '$.layers') IS NULL;
        """);

    /// <summary>Puts the ground layer's shapes back at the top level, for a document that has exactly one
    /// layer and can therefore be expressed the old way. A genuinely stacked document has no old form to go
    /// back to, so it is left as it is rather than flattened into a board the previous reader would have
    /// drawn wrongly.</summary>
    public override void Down() => Execute.Sql(
        """
        UPDATE map_artifact
           SET data = CAST(
                 JSON_REMOVE(
                   JSON_SET(
                     CAST(data AS CHAR),
                     '$.layout',
                     JSON_EXTRACT(CAST(data AS CHAR), '$.layers[0].layout')),
                   '$.layers')
               AS BINARY)
         WHERE kind = 'sketch_layout_json'
           AND JSON_VALID(CAST(data AS CHAR))
           AND JSON_LENGTH(CAST(data AS CHAR), '$.layers') = 1;
        """);
}
