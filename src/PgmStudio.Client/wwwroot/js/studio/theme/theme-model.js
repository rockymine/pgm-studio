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
    rimEdges: "drop",          // void | drop | boundary — which edges the rim caps
    wallOnTerrainFaces: true,
    rim: { material: { kind: "solid", id: 155 }, depth: 1, enabled: true },
    surface: { material: { kind: "layered", layers: [{ material: { kind: "solid", id: 2 }, thickness: 1 }, { material: { kind: "solid", id: 3 }, thickness: 2 }] }, depth: 3, enabled: true },
    wall: { kind: "teamTint", blockId: 159, neutral: { kind: "solid", id: 159, data: 8 } },
    wallEnabled: true,
    fill: { kind: "solid", id: 1 },
  };
}

/** A slug-safe id unique among `existing`: the trimmed base slug, then base-2, base-3, … Shared by every
 *  named scope a sketch carries — themes and dressings alike — since the slugging is the same question and a
 *  second copy would drift on the first edge case (an empty name, a name of only punctuation). */
export function uniqueScopeId(existing, base) {
  const taken = new Set(existing);
  const slug = ((base || "scope").trim().toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "")) || "scope";
  if (!taken.has(slug)) return slug;
  for (let i = 2; ; i++) { const candidate = `${slug}-${i}`; if (!taken.has(candidate)) return candidate; }
}
