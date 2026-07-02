/**
 * Pure helpers for the plan editor's inspect layer (the derived-structure overlays + lint panel) — NO DOM.
 * The block-space geometry (interfaces / gap links / frontline) is computed server-side by /api/plan/inspect;
 * this module owns only the client-side presentation glue: ordering findings for the lint panel and
 * reading/writing the overlay-toggle preferences. Node-tested alongside plan-doc.js.
 */

/** The three overlay layers, all on by default. */
export const DEFAULT_OVERLAYS = { interfaces: true, gaps: true, frontline: true };

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
 * Parse persisted overlay toggles, defaulting any missing key to on (so a first-run or partially-written
 * blob still shows every overlay). A garbage value falls back to all-on.
 */
export function parseOverlays(raw) {
  try {
    const o = (raw && JSON.parse(raw)) || {};
    return { interfaces: o.interfaces !== false, gaps: o.gaps !== false, frontline: o.frontline !== false };
  } catch {
    return { ...DEFAULT_OVERLAYS };
  }
}
