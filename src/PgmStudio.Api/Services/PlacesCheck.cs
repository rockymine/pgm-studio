using System.Text.Json;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// What refuses a map's places document: one that does not bind, a place with no id, two places stating one id,
/// a shape that is not one or that carries the wrong number of points, and a place holding a play piece the map
/// has none of. A name may be empty and a place may hold nothing; the document stores what an author is still
/// drawing.
/// </summary>
public static class PlacesCheck
{
    /// <summary>The document read out of <paramref name="json"/>, or the findings that refuse it, checked against
    /// the play pieces of <paramref name="intent"/>.</summary>
    public static (PlacesDocument? Places, Findings Refused) Check(string json, MapIntent intent)
    {
        if (DocumentBinding.Unbindable("places", json, text => JsonSerializer.Deserialize<PlacesDocument>(text, MapArtifactStore.Json))
            is { } unbound) return (null, Findings.Of(unbound));
        var document = JsonSerializer.Deserialize<PlacesDocument>(json, MapArtifactStore.Json) ?? new PlacesDocument();

        var refused = new List<Finding>();
        var pieces = new HashSet<string>(PlayPieces.Of(intent).Select(piece => piece.Id), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var places = document.Places ?? [];
        for (var at = 0; at < places.Count; at++)
        {
            var place = places[at];
            var field = $"places[{at}]";
            if (string.IsNullOrEmpty(place.Id))
                refused.Add(new Finding(RequestRules.Unreadable, $"the place at {field} has no `id`", Field: $"{field}.id"));
            else if (!seen.Add(place.Id))
                refused.Add(new Finding(RequestRules.Taken, $"the id '{place.Id}' at {field} is already a place's",
                    Field: $"{field}.id", Subjects: [place.Id]));

            var count = place.Points?.Length ?? 0;
            if (PlaceShapes.Points(place.Shape) is not var (least, most))
                refused.Add(new Finding(RequestRules.Unreadable,
                    $"the `shape` of {field} is '{place.Shape}', not one of {string.Join(", ", PlaceShapes.All)}",
                    Field: $"{field}.shape"));
            else if (count < least || count > most)
                refused.Add(new Finding(RequestRules.Unreadable,
                    $"the {place.Shape} at {field} has {count} points, and a {place.Shape} takes "
                    + (least == most ? $"{least}" : $"{least} or more"), Field: $"{field}.points"));
            if (place.Points?.Any(point => point is not { Length: 2 }) is true)
                refused.Add(new Finding(RequestRules.Unreadable, $"a point of {field} is not [x, z]", Field: $"{field}.points"));

            if (place.Holds is { Length: > 0 } held && !pieces.Contains(held))
                refused.Add(new Finding(RequestRules.NoSuchSubject,
                    $"the place at {field} holds '{held}', and the map has no play piece with that id",
                    Field: $"{field}.holds", Subjects: [held]));
        }
        if (document.Verdict is { } verdict && !PlanVerdicts.All.Contains(verdict))
            refused.Add(new Finding(RequestRules.Unreadable,
                $"the `verdict` is '{verdict}', not one of {string.Join(", ", PlanVerdicts.All)}", Field: "verdict"));
        if (document.Area is { } area && (area.Length != 4 || area[2] <= area[0] || area[3] <= area[1]))
            refused.Add(new Finding(RequestRules.Unreadable,
                "the `area` is not [minX, minZ, maxX, maxZ] with each max past its min", Field: "area"));
        return refused.Count > 0 ? (null, Findings.Of([.. refused])) : (document, Findings.None);
    }
}
