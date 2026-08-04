namespace PgmStudio.Contracts;

/// <summary>What a sample patch grew, so an author can tell a recipe that is doing nothing from one whose
/// props are simply off-screen — a picture at card size cannot always say.</summary>
public sealed record DressingCountsDto(int Plants, int Boulders, int Trees);

/// <summary>Both views of a dressing recipe (POST /api/terrain/dressing-preview), grown by the real pass over a
/// sample patch. <paramref name="Plan"/> is the patch from above, where the density fields read — clearings,
/// flower patches, how far apart the groves fall; <paramref name="Section"/> is the same patch cut open, the
/// only view a tree's silhouette or a boulder's half-buried profile is visible in.</summary>
public sealed record DressingPreviewDto(string Plan, string Section, DressingCountsDto Counts);

/// <summary>A dressing previewed against a theme (POST /api/terrain/dressing-preview): the recipe, and the
/// terrain finish it grows on — what the paint leaves on top is exactly what decides whether flora grows at
/// all, so a preview that ignored the theme would promise a meadow on a quartz plaza.</summary>
public sealed record DressingPreviewRequest(string DressingJson, string? ThemeJson);

/// <summary>One tree species the dressing editor offers (GET /api/terrain/species) — the palette's rows, so
/// the picker cannot name a species the grower does not know.</summary>
public sealed record TreeSpeciesDto(string Name);
