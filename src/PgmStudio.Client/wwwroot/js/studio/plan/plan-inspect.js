/**
 * Pure helpers for the plan editor's inspect layer (the derived-structure overlays) — NO DOM.
 * The block-space geometry (interfaces / gap links / frontline) is computed server-side by /api/plan/inspect;
 * this module owns only the client-side presentation glue: reading/writing the overlay-toggle preferences.
 * Node-tested alongside plan-doc.js.
 */

/**
 * The overlay layers and their default visibility. Interfaces, frontline and violations (the evaluator's fired-
 * rule evidence) show by default; labels (piece/build-area ids + gap connectors and their hop distances) stay
 * off by default to keep the canvas quiet — id text and distance lines are opt-in via one toggle.
 */
export const DEFAULT_OVERLAYS = { interfaces: true, labels: false, frontline: true, violations: true };

/**
 * Parse persisted overlay toggles. Interfaces and frontline default on (a missing key stays visible);
 * labels default off (only an explicit `true` turns them on). A blob from the earlier layout that carried a
 * `gaps` key is read cleanly — that key is ignored, its content now lives under `labels`. Garbage falls back
 * to the defaults.
 */
export function parseOverlays(raw) {
  try {
    const o = (raw && JSON.parse(raw)) || {};
    return {
      interfaces: o.interfaces !== false, labels: o.labels === true,
      frontline: o.frontline !== false, violations: o.violations !== false,
    };
  } catch {
    return { ...DEFAULT_OVERLAYS };
  }
}
