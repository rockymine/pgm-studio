namespace PgmStudio.Contracts;

/// <summary>A rendered board, as the SVG a card draws inline.</summary>
public sealed record SvgDto(string Svg);

/// <summary>A whole map painted with a theme, as one SVG over the footprint it covers.</summary>
public sealed record ThemeMapPreviewDto(string Svg, int MinX, int MinZ, int SpanX, int SpanZ);
