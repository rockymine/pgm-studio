using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The tree showcase's copied trees are named for what they are rather than where they stood.
///
/// <para>A showcase row was filed as <c>&lt;world&gt;-r&lt;row&gt;-&lt;place&gt;</c>, the band it stood in and its
/// place along x. What each band is was the author's statement, and it is now the name: <c>willow-1</c> for the
/// first tree of band 17. A kind held by several bands is counted through them in band order, and the one tree
/// its band does not describe (band 7's fourth, a sequoia) takes its own kind. The table is the one
/// <c>corpus/tree-showcase/kinds.json</c> in <c>pgm-studio-mapgen</c> states; the seeder reads that file and
/// matches a row by its cut, so a relabel is the seeder's to carry.</para>
///
/// <para>Only rows cut from the showcase — <c>cut_world</c> ending in <c>/tree-showcase</c> — whose name still
/// carries its band and place are touched. A map's copied trees are copies keyed in its own registry, so no
/// stored map changes.</para>
/// </summary>
[Migration(49, "The tree showcase's copied trees are named for what they are")]
public sealed partial class M0049_ShowcaseTreesNamedByKind : Migration
{
    private static readonly Dictionary<int, string> Bands = new()
    {
        [1] = "small-olive", [2] = "large-pine", [3] = "large-pine", [4] = "tiny-spruce", [5] = "dark-oak",
        [6] = "tiny-oak", [7] = "tall-spruce", [8] = "acacia", [9] = "oak", [10] = "olive", [11] = "dense-oak",
        [12] = "oak", [13] = "birch", [14] = "oak", [15] = "wool-tree", [16] = "jungle", [17] = "willow",
    };

    private static readonly Dictionary<(int Band, int Place), string> Trees = new() { [(7, 4)] = "sequoia" };

    [GeneratedRegex(@"-r(\d+)-(\d+)$")]
    private static partial Regex BandAndPlace();

    public override void Up() => Execute.WithConnection((connection, transaction) =>
    {
        var rows = new List<(long Id, int Band, int Place)>();
        using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = "SELECT id, name FROM tree_style WHERE form = 'copied' AND cut_world LIKE '%/tree-showcase'";
            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                var match = BandAndPlace().Match(reader.GetString(1));
                if (!match.Success) continue;
                rows.Add((reader.GetInt64(0), int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                          int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)));
            }
        }

        var counted = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (id, band, place) in rows.OrderBy(row => row.Band).ThenBy(row => row.Place))
        {
            if (!Trees.TryGetValue((band, place), out var kind) && !Bands.TryGetValue(band, out kind)) continue;
            counted[kind] = counted.GetValueOrDefault(kind) + 1;
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE tree_style SET name = @name WHERE id = @id";
            Bind(update, "@name", $"{kind}-{counted[kind]}");
            Bind(update, "@id", id);
            update.ExecuteNonQuery();
        }
    });

    /// <summary>Nothing to undo into: a band and place are not kept beside the new name, and the seeder files by
    /// kind, so a schema rolled back still reads these rows under the names they now carry.</summary>
    public override void Down() { }

    private static void Bind(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
