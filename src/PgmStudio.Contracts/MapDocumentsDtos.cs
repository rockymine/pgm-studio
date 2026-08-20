using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>
/// The three documents a studio-authored map is made of, handed over together
/// (<c>POST /map/from-documents</c>) so a map built somewhere else can be loaded whole.
///
/// <para>Each is passed through verbatim to the route that stores it: the plan is a <c>PlanModel</c>, the
/// layout a <c>SketchLayout</c>, the intent a <c>MapIntent</c>. They are the documents themselves rather than
/// shapes restated here — naming them again would be a second copy free to disagree with the readers.</para>
/// </summary>
/// <param name="Plan">The board as cell rectangles. Stored as the map's plan, which is what makes it
/// re-plannable rather than only re-buildable.</param>
/// <param name="Layout">The drawing the plan compiled to, with everything a plan cannot state already on it.
/// Stored verbatim, so the map is loaded as it was built rather than as it would recompile.</param>
/// <param name="Intent">What the map is played for. Stored and projected into the document.</param>
/// <param name="Name">What to call the map. Falls back to the intent's own <c>meta.name</c>.</param>
/// <param name="Slug">What to store it under. Falls back to the name, slugified. A map already stored under
/// it is <b>replaced</b>.</param>
/// <param name="Authors">Who to credit, as a bare pseudonym or as <c>{uuid, name, role, contribution}</c>.
/// Stated separately because a compiled intent carries an empty <c>meta.authors</c>, and applied after the
/// intent for the reason that projection exists.</param>
public sealed record MapFromDocumentsRequest(
    JsonElement Plan,
    JsonElement Layout,
    JsonElement Intent,
    string? Name = null,
    string? Slug = null,
    IReadOnlyList<JsonElement>? Authors = null);

/// <summary>A map loaded from its documents, and what the drawing turned out to hold.</summary>
/// <param name="Replaced">Whether a map stored under this slug was dropped to make room. False is an
/// origination, true is a reload of a map that had been loaded before.</param>
/// <param name="Cells">Ground columns the layout rasterized to.</param>
/// <param name="Islands">Landmasses those columns fall into.</param>
public sealed record MapLoadedDto(
    string Slug,
    bool Replaced,
    int Cells,
    int Islands,
    [property: JsonPropertyName("configureUrl")] string ConfigureUrl);
