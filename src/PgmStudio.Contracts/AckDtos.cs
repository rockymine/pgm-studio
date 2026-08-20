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
public sealed record SketchFromPlanDto(bool Ok, IReadOnlyList<string> Orphaned);

/// <summary>A rendered board, as the SVG a card draws inline.</summary>
public sealed record SvgDto(string Svg);

/// <summary>A room style as the stamper's own JSON — the form the export consumes and the form a map
/// snapshots when it binds one.</summary>
public sealed record StyleJsonDto(string StyleJson);

/// <summary>A terrain theme as the painter's own JSON.</summary>
public sealed record ThemeJsonDto(string ThemeJson);

/// <summary>A whole map painted with a theme, as one SVG over the footprint it covers.</summary>
public sealed record ThemeMapPreviewDto(string Svg, int MinX, int MinZ, int SpanX, int SpanZ);

/// <summary>Whether the map came from a sketch — which is what drops the Monuments step.</summary>
public sealed record MapOriginDto(bool Sketch);

/// <summary>A Minecraft account, resolved either way round from a name or a uuid.</summary>
public sealed record PlayerDto(string Uuid, string Name);
