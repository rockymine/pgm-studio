using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A hub's and a front line's form are stored under the one list of form words a wool's already uses: <c>bar</c>
/// reads <c>i</c>, <c>single</c> reads <c>l</c>, <c>twin</c> reads <c>u</c>, <c>ring</c> reads <c>donut</c>, and an
/// <c>arms-N</c> comb reads <c>e</c>. The words live in <c>composed_board.hub</c> and <c>.frontline</c> and inside
/// <c>plan.structure</c>, the <c>wools:…|hub:…|front:…</c> key a generated plan is kept under; every row is
/// rewritten in place. Wool words, <c>p</c>, <c>g</c>, <c>double-hole</c>, <c>two-u</c> and <c>none</c> are already
/// the merged words.
/// </summary>
[Migration(75, "FormWords")]
public sealed class M0075_FormWords : Migration
{
    private static readonly (string Old, string New)[] Renamed =
        [("bar", "i"), ("single", "l"), ("twin", "u"), ("ring", "donut")];

    public override void Up()
    {
        foreach (var (old, merged) in Renamed) Rewrite(old, merged);
        foreach (var column in new[] { "hub", "frontline" })
            Execute.Sql($"UPDATE composed_board SET `{column}` = 'e' WHERE `{column}` LIKE 'arms-%'");
        Execute.Sql(@"UPDATE plan SET structure = REGEXP_REPLACE(structure, '\\|hub:arms-[0-9]+\\|', '|hub:e|') "
                    + "WHERE structure LIKE '%|hub:arms-%'");
        Execute.Sql(@"UPDATE plan SET structure = REGEXP_REPLACE(structure, '\\|front:arms-[0-9]+$', '|front:e') "
                    + "WHERE structure LIKE '%|front:arms-%'");
    }

    public override void Down()
    {
        foreach (var (old, merged) in Renamed) Rewrite(merged, old);
        Rewrite("e", "arms-3");
    }

    /// <summary>Rewrite the form word <paramref name="from"/> to <paramref name="to"/> in both board columns and
    /// in the hub and front-line slots of every plan's structure key.</summary>
    private void Rewrite(string from, string to)
    {
        foreach (var column in new[] { "hub", "frontline" })
            Execute.Sql($"UPDATE composed_board SET `{column}` = '{to}' WHERE `{column}` = '{from}'");
        Execute.Sql($"UPDATE plan SET structure = REPLACE(structure, '|hub:{from}|', '|hub:{to}|') "
                    + $"WHERE structure LIKE '%|hub:{from}|%'");
        Execute.Sql($"UPDATE plan SET structure = CONCAT(LEFT(structure, CHAR_LENGTH(structure) - {from.Length}), '{to}') "
                    + $"WHERE structure LIKE '%|front:{from}'");
    }
}
