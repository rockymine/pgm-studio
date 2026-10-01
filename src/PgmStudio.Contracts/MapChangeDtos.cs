using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Vocabulary;

namespace PgmStudio.Contracts;

/// <summary>GET /api/map/{slug}/changes — every change to a map's documents, oldest first.</summary>
/// <param name="Slug">The map.</param>
/// <param name="Changes">Its changes, or those after <c>since</c> where the request names one.</param>
public sealed record MapChangesDto(string Slug, IReadOnlyList<MapChangeDto> Changes);

/// <summary>One change to a map's documents: one request's writes to its plan, refinement, layout and
/// intent.</summary>
/// <param name="Number">Which change it is. Numbers count up per map and never repeat, and a kept document's
/// revision is the number of the change that last wrote it.</param>
/// <param name="At">When it landed, in UTC.</param>
/// <param name="Writer">The name of the person it was written as, where anyone signed it.</param>
/// <param name="WriterUuid">Their account, where anyone signed it.</param>
/// <param name="Token">The label of the token it was written with, where a token wrote it.</param>
/// <param name="Origin">Where its documents were built from, where the writer said.</param>
/// <param name="Note">What the writer said the change is.</param>
/// <param name="Documents">The documents it wrote.</param>
/// <param name="Discarded">The earlier changes it dropped: a source applied over changes it had not seen names
/// the ones it replaces. Empty where it dropped none.</param>
public sealed record MapChangeDto(
    long Number, DateTime At, string? Writer, string? WriterUuid, string? Token, ChangeOrigin? Origin, string? Note,
    [property: WordSet(typeof(MapDocuments))] IReadOnlyList<string> Documents, IReadOnlyList<long> Discarded);

/// <summary>GET /api/map/{slug}/changes/{number} — a map's documents as they stood at one change: for each, what
/// the latest change at or before it wrote. A document no change had written by then is absent.</summary>
/// <param name="Number">The change.</param>
/// <param name="Plan">The plan, where one was stated by then.</param>
/// <param name="Refinement">The refinement the map's source stated, where a source had been applied by then.</param>
/// <param name="Layout">The sketch layout.</param>
/// <param name="Intent">The intent.</param>
public sealed record MapChangeDocumentsDto(
    long Number, JsonElement? Plan, JsonElement? Refinement, JsonElement? Layout, JsonElement? Intent);

/// <summary>GET /api/map/{slug}/diff — what changed between two of a map's changes: every edit to its four
/// documents, and with <c>world=true</c> the columns whose ground, surface block or structure the edits
/// moved.</summary>
/// <param name="From">The change compared from. Zero is before the map's first change, where nothing was
/// stated.</param>
/// <param name="To">The change compared to.</param>
/// <param name="Edits">The edits taking the documents at <paramref name="From"/> to those at
/// <paramref name="To"/>, in the order each document states what they touch — plan, then layout, then
/// intent.</param>
/// <param name="World">The columns the two builds disagree on, where the request asked for them, and absent
/// where it did not.</param>
public sealed record MapDiffDto(
    long From, long To, IReadOnlyList<DocumentEdit> Edits,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorldChangesDto? World = null);

/// <summary>The columns two builds of a board disagree on, sorted by what changed in each.</summary>
/// <param name="Ground">Columns whose ground rose, fell, came or went.</param>
/// <param name="Surface">Columns whose ground held its height and whose top block is another.</param>
/// <param name="Structure">Columns whose ground and top block held and where something else changed — a
/// building, a prop, a room, a made thing, a course under the surface.</param>
public sealed record WorldChangesDto(ColumnChangesDto Ground, ColumnChangesDto Surface, ColumnChangesDto Structure);

/// <summary>The columns of one kind of change.</summary>
/// <param name="Columns">How many columns changed this way.</param>
/// <param name="Runs">The largest twelve 4-connected runs they form, largest first.</param>
public sealed record ColumnChangesDto(int Columns, IReadOnlyList<CellRunDto> Runs);

/// <summary>POST /api/map/{slug}/changes/{number}/restore — what to say about the restore.</summary>
/// <param name="Note">Kept on the change the restore lands as, at most 1,000 characters. Absent says which
/// change it restores.</param>
public sealed record MapRestoreRequest(string? Note = null);

/// <summary>A map's documents written back as they stood at an earlier change.</summary>
/// <param name="Restored">The change whose documents were written back.</param>
/// <param name="Change">The change the restore landed as, or absent where every document already stood as it
/// did then and nothing was written.</param>
/// <param name="Documents">The documents written back.</param>
public sealed record MapRestoredDto(
    long Restored, long? Change, [property: WordSet(typeof(MapDocuments))] IReadOnlyList<string> Documents);
