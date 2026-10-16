using System.Text.Json.Serialization;
using PgmStudio.Vocabulary;

namespace PgmStudio.Contracts;

/// <summary>A map's places — the document <c>GET</c> and <c>PUT /map/{slug}/places</c> answer and take: what the
/// map is for, drawn over its ground as named shapes that make no terrain. Each write is a change in the map's
/// history.</summary>
/// <param name="Places">The places, in the order they are listed.</param>
/// <param name="Area">The ground the board is meant to fill, <c>[minX, minZ, maxX, maxZ]</c> in blocks, where the
/// one drawing it states one; absent leaves the board the size of what is drawn.</param>
public sealed record PlacesDocument(
    List<Place>? Places = null,
    double[]? Area = null);

/// <summary>One place: a name, the shape it is drawn as, and the play piece it holds.</summary>
/// <param name="Id">Its id, unique among the map's places, which a note on it names.</param>
/// <param name="Name">What it is called, as the list and the canvas label it.</param>
/// <param name="Shape">What it is drawn as.</param>
/// <param name="Points">Its points, <c>[[x, z], …]</c> in blocks: one for a point, two corners for a box, three or
/// more for a ring and two or more for a line.</param>
/// <param name="Holds">The id of the play piece it holds, as <c>GET /map/{slug}/intent/pieces</c> names it;
/// absent for a place holding none.</param>
public sealed record Place(
    string Id,
    string Name,
    [property: WordSet(typeof(PlaceShapes))] string Shape,
    double[][] Points,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Holds = null);
