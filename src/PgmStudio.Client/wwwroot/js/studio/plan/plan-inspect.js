/**
 * Pure helpers for the plan editor's inspect layer (the derived-structure overlays + lint panel) — NO DOM.
 * The block-space geometry (interfaces / gap links / frontline) is computed server-side by /api/plan/inspect;
 * this module owns only the client-side presentation glue: ordering findings for the lint panel and
 * reading/writing the overlay-toggle preferences. Node-tested alongside plan-doc.js.
 */

/**
 * The overlay layers and their default visibility. Interfaces and frontline show by default; labels
 * (piece/build-area ids + gap connectors and their hop distances) stay off by default to keep the canvas
 * quiet — id text and distance lines are opt-in via one toggle.
 */
export const DEFAULT_OVERLAYS = { interfaces: true, labels: false, frontline: true };

/**
 * Order findings for the lint panel: structural errors first, then rule lint, stable within each group so a
 * finding keeps its server order (and its highlight target stays predictable across re-inspects).
 */
export function sortFindings(findings) {
  const rank = (s) => (s === "error" ? 0 : 1);
  return (findings || [])
    .map((f, i) => ({ f, i }))
    .sort((a, b) => rank(a.f.severity) - rank(b.f.severity) || a.i - b.i)
    .map((x) => x.f);
}

/**
 * Parse persisted overlay toggles. Interfaces and frontline default on (a missing key stays visible);
 * labels default off (only an explicit `true` turns them on). A blob from the earlier layout that carried a
 * `gaps` key is read cleanly — that key is ignored, its content now lives under `labels`. Garbage falls back
 * to the defaults.
 */
export function parseOverlays(raw) {
  try {
    const o = (raw && JSON.parse(raw)) || {};
    return { interfaces: o.interfaces !== false, labels: o.labels === true, frontline: o.frontline !== false };
  } catch {
    return { ...DEFAULT_OVERLAYS };
  }
}
