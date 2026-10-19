namespace PgmStudio.Contracts;

/// <summary>A rendered board, as the SVG a card draws inline.</summary>
/// <param name="Svg">The picture itself, ready to inject into the page.</param>
public sealed record SvgDto(string Svg);

