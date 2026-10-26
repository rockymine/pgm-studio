using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// An approach wall's line, by the wall's id: its corners set whole. The named image takes them as posted, and unless
/// <c>alone</c> every other image of its unit takes them carried through the symmetry step between them, each corner
/// to the block corner it lands on. A corner dragged, a corner added on a leg and a corner taken out are each the
/// whole line written again. Refused where fewer than two corners are posted, a corner is not two whole numbers, or
/// two corners in a row are one point.
/// </summary>
public static class PlayPieceLine
{
    /// <summary>The intent with the wall <paramref name="id"/> run through <paramref name="points"/>, or the findings
    /// that stopped it and a null intent.</summary>
    public static (MapIntent? Intent, Findings Refused) Apply(
        MapIntent intent, string id, int[][] points, bool alone = false, SymmetryIntent? board = null)
    {
        var walls = intent.Structures?.Walls ?? [];
        if (walls.FindIndex(wall => wall.Id == id) is not (var at and >= 0))
            return Refused(PlayPieces.Of(intent).Any(piece => piece.Id == id) ? RequestRules.Unreadable : RequestRules.NoSuchSubject,
                "id", PlayPieces.Of(intent).Any(piece => piece.Id == id)
                    ? $"'{id}' is not a wall, and only a wall runs along a line"
                    : $"the map has no play piece with the id '{id}'", id);
        if (points.Length < 2 || points.Any(point => point is not { Length: 2 }))
            return Refused(RequestRules.Unreadable, "points", "a wall runs through two or more corners, each [x, z]", id);
        for (var i = 1; i < points.Length; i++)
            if (points[i][0] == points[i - 1][0] && points[i][1] == points[i - 1][1])
                return Refused(RequestRules.Unreadable, "points", $"corners {i} and {i + 1} of the wall are one point", id);

        var symmetry = intent.Symmetry ?? board;
        var lined = PlayPieceMove.Images(walls, at, wall => wall.Stamp, symmetry?.Mode, alone, (wall, step) =>
            wall with { Points = step == 0 || symmetry is null ? points : PlayPieceAdd.ImageOf(points, symmetry, step) });
        return (intent with { Structures = intent.Structures! with { Walls = lined } }, Findings.None);
    }

    private static (MapIntent? Intent, Findings Refused) Refused(string rule, string field, string message, string id) =>
        (null, Findings.Of(new Finding(rule, message, Field: field, Subjects: [id])));
}
