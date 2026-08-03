/**
 * Terrain-paint theme model shared by the sketch tool's Theme phase (finishing-model.md §4). A theme is the
 * wire JSON the painter (`TerrainThemeJson`/`TerrainTheme`) deserialises — no second model — so this holds only
 * the built-in default a new theme clones and a small id uniquifier. Pure, no DOM.
 */

/** The built-in terrain-paint theme: the whole-map default when no theme is authored. Mirrors
 *  TerrainTheme.Default's shipping look — a quartz rim, a team-tinted clay wall, a grass-over-dirt surface. A
 *  new theme starts as a clone of this and the author edits its JSON; kept client-side so the phase can seed
 *  and preview a theme without a round-trip. */
export function defaultThemeJson() {
  return {
    bedrock: { relative: false, value: 1 },
    closed: false,
    wallOnTerrainFaces: true,
    rim: { material: { kind: "solid", id: 155 }, depth: 1, enabled: true },
    surface: { material: { kind: "layered", layers: [{ material: { kind: "solid", id: 2 }, thickness: 1 }, { material: { kind: "solid", id: 3 }, thickness: 2 }] }, depth: 3, enabled: true },
    wall: { kind: "teamTint", blockId: 159, neutral: { kind: "solid", id: 159, data: 8 } },
    wallEnabled: true,
    fill: { kind: "solid", id: 1 },
  };
}

/** The representative block id of a material — what a top-down cell reads as. Recurses composites to their
 *  first/base entry; falls back to stone (1). Used to colour the Blocks overlay by a theme's surface. */
export function materialBlockId(mat) {
  if (!mat || typeof mat !== "object") return 1;
  switch (mat.kind) {
    case "solid":    return mat.id ?? 1;
    case "layered":  return materialBlockId(mat.layers?.[0]?.material);
    case "teamTint": return mat.blockId ?? materialBlockId(mat.neutral);
    case "voronoi":  return materialBlockId(mat.palette?.[0]?.material ?? mat.palette?.[0]);
    case "noise":    return materialBlockId(mat.stops?.[0]?.material ?? mat.stops?.[0]);
    case "wallRun":  return materialBlockId(mat.stripes?.[0]?.material ?? mat.stripes?.[0]);
    default:         return mat.id ?? mat.blockId ?? 1;
  }
}

/** A theme's surface block id — the material a cell shows from the top (surface bucket, else the whole node). */
export function surfaceBlockId(theme) {
  const surface = theme?.surface;
  return materialBlockId(surface?.material ?? surface);
}

/** A slug-safe id unique among `existing`: the trimmed base slug, then base-2, base-3, … */
export function uniqueThemeId(existing, base) {
  const taken = new Set(existing);
  const slug = ((base || "theme").trim().toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "")) || "theme";
  if (!taken.has(slug)) return slug;
  for (let i = 2; ; i++) { const candidate = `${slug}-${i}`; if (!taken.has(candidate)) return candidate; }
}
