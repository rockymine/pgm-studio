namespace PgmStudio.Contracts;

/// <summary>
/// What a write answers when the whole answer is "it happened".
///
/// <para>A replace has nothing to hand back — the caller posted the document and the document is what is
/// stored — so the body is one field, and it is a field rather than an empty object because a caller reading
/// a response needs something to have asserted on.</para>
/// </summary>
public sealed record OkDto(bool Ok = true);

/// <summary>A map was originated, and this is the slug every later route names it by.</summary>
public sealed record OriginatedDto(string Slug);

/// <summary>The sketch is rasterized and the map has moved to Configure, with the page that continues it.</summary>
public sealed record SketchFinishedDto(string Slug, string ConfigureUrl);

/// <summary>Whether a still-pristine draft was dropped. False is the ordinary answer — a draft with any real
/// work in it is left alone, and so is a slug no map is stored under.</summary>
public sealed record DiscardedDto(bool Discarded);

/// <summary>The stored layout was replaced by one a plan compiled, and the terrain that had nowhere to land
/// on the new board. Empty unless <c>?force=true</c> accepted the loss.</summary>
public sealed record SketchFromPlanDto(IReadOnlyList<string> Orphaned, bool Ok = true);
