using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Vocabulary;

namespace PgmStudio.Contracts;

/// <summary>Where a map's documents were built from: the repository, the commit and the folder of the script
/// that wrote them, and whether the working tree differed from the commit.</summary>
/// <param name="Repo">The repository, as <c>owner/name</c>.</param>
/// <param name="Commit">The commit the documents were built at.</param>
/// <param name="Path">The folder in the repository the documents and their script sit in.</param>
/// <param name="Dirty">Whether the working tree held changes the commit does not, so the commit alone does not
/// rebuild these documents.</param>
public sealed record ChangeOrigin(string? Repo = null, string? Commit = null, string? Path = null, bool? Dirty = null);

/// <summary>A map's source applied: the change it landed as, what it changed in the documents the map held, and
/// what its drawing turned out to hold.</summary>
/// <param name="Slug">What every later route names the map by.</param>
/// <param name="Change">The change the source landed as. Null on a dry run, which stores nothing.</param>
/// <param name="Replaced">Whether a map was stored under the slug before, which the source replaces.</param>
/// <param name="Edits">What the source changes in the documents the map held, in the shape a diff answers —
/// every document stated for the first time where no map was stored.</param>
/// <param name="Cells">Ground columns the layout rasterizes to.</param>
/// <param name="Islands">Landmasses those columns fall into.</param>
/// <param name="ConfigureUrl">The page that continues it, ready to navigate to.</param>
/// <param name="Layout">On a dry run, the layout the source would store: the base with the refinement applied.
/// Absent on a store, whose layout <c>GET /map/{slug}/sketch</c> reads.</param>
/// <param name="Intent">On a dry run, the intent the source would store. Absent on a store, whose intent
/// <c>GET /map/{slug}/intent</c> reads.</param>
public sealed record MapSourceDto(
    string Slug,
    long? Change,
    bool Replaced,
    IReadOnlyList<DocumentEdit> Edits,
    int Cells,
    int Islands,
    string ConfigureUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Layout = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Intent = null);
