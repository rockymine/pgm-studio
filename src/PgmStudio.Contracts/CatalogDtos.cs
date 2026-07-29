namespace PgmStudio.Contracts;

/// <summary>One catalog card: a shape the pipeline can build, its parameters, and how far it actually gets.
/// <paramref name="Tier"/> is <c>in-mix</c> (a sampler draws it), <c>reachable</c> (the filler accepts it but
/// nothing asks) or <c>emitter-only</c> (only a direct emitter call builds it); <paramref name="Note"/> says
/// why for the latter two, so a badge is never bare. <paramref name="Knobs"/> are display tokens
/// (<c>flipped</c>, <c>side-tuck</c>, <c>attach 4</c>). <paramref name="Svg"/> is ready to inject.</summary>
public sealed record CatalogShapeDto(
    string Id,
    string Kind,
    string Family,
    string Tier,
    int BoxW,
    int BoxH,
    int CorridorCells,
    IReadOnlyList<string> Knobs,
    string? Note,
    string Svg);

/// <summary>The catalog as one page — it is a bounded set, so there is no cursor. <paramref name="ByTier"/>
/// and <paramref name="ByFamily"/> count the <b>whole</b> catalog, not the filtered slice, so a chip can say
/// what it would show before it is picked and a filter never hides what it filters against (the same rule the
/// browse feed's observed census follows).</summary>
public sealed record CatalogPage(
    IReadOnlyList<CatalogShapeDto> Shapes,
    int Total,
    IReadOnlyDictionary<string, int> ByTier,
    IReadOnlyDictionary<string, int> ByFamily,
    IReadOnlyDictionary<string, int> ByKind);
