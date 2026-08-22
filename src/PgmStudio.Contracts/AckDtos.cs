namespace PgmStudio.Contracts;

/// <summary>
/// What a write answers when the answer is that it happened — and, where the caller now needs a handle it
/// did not send, that handle and nothing else.
///
/// <para><b>A success carries no restatement of its own status.</b> The body is an empty object rather than
/// a field saying so: a 200 already says the write landed, and a flag repeating it is a second place for the
/// same fact to be read from. It is an object rather than <b>204 No Content</b> because a success is where
/// complaints ride — <c>Complaints</c>'s rule is that a 2xx JSON object answers <c>warnings</c> when a gate
/// remarked on something and carries no such key when none did, and a body-less response has nowhere to put
/// it. The deletes that answer a true 204 are the library rows, which run no gate and have nothing to
/// remark.</para>
/// </summary>
/// <remarks>The write was applied and there is nothing to hand back — the caller posted what is stored, and
/// the read that answers the stored form is a route of its own. The document replaces, the map edits, the
/// symmetry confirmation and the metadata patch all answer this.</remarks>
public sealed record AppliedDto;

/// <summary>A row was made, and this is the id every later route names it by.</summary>
public sealed record CreatedDto(long Id);

/// <summary>A map was originated, and this is the slug every later route names it by.</summary>
public sealed record OriginatedDto(string Slug);

/// <summary>Whether a still-pristine draft was dropped. False is the ordinary answer — a draft with any real
/// work in it is left alone, and so is a slug no map is stored under.</summary>
public sealed record DiscardedDto(bool Discarded);

/// <summary>The sketch is rasterized and the map has moved to Configure, with the page that continues it.</summary>
public sealed record SketchFinishedDto(string Slug, string ConfigureUrl);

/// <summary>The stored layout was replaced by one a plan compiled, and the terrain that had nowhere to land
/// on the new board. Empty unless <c>?force=true</c> accepted the loss.</summary>
public sealed record SketchFromPlanDto(IReadOnlyList<string> Orphaned);
