namespace PgmStudio.Contracts;

/// <summary>A rendered board, as the SVG a card draws inline.</summary>
/// <param name="Svg">The picture itself, ready to inject into the page.</param>
public sealed record SvgDto(string Svg);

/// <summary>A whole map painted with a theme, as one SVG over the footprint it covers.</summary>
/// <param name="Svg">The painted map, ready to inject.</param>
/// <param name="MinX">The west edge of the ground it covers, in blocks.</param>
/// <param name="MinZ">The north edge.</param>
/// <param name="SpanX">How far east it runs from <paramref name="MinX"/>, in blocks — what turns a point on
/// the picture back into a block coordinate.</param>
/// <param name="SpanZ">How far south it runs from <paramref name="MinZ"/>.</param>
public sealed record ThemeMapPreviewDto(string Svg, int MinX, int MinZ, int SpanX, int SpanZ);
