/**
 * A canvas's **screen-space** layer <g> stack, declared once.
 *
 * Each canvas keeps a fixed stack of SVG groups above its painted world — labels, selection chrome,
 * handles, the scale bar — and the stack's order IS its z-order. Declared by hand that order gets stated
 * **twice** (once by the field declarations, once by the append loop) and the two can silently drift;
 * `layerStack` states it once: the key order in the spec is the paint order, bottom first.
 *
 * Each group carries `data-layer="<name>"`, so a layer can be addressed by name from the outside
 * (dev tools, an end-to-end probe) instead of by its index among its siblings — an index that moves
 * whenever a layer is inserted. The world half is painted rather than retained and has no elements to
 * address, so it reports its own order instead: `painter.layers`.
 */

import { svgEl } from "./svg.js";

/** Attributes for a layer that is painted but never hit-tested — the common case. */
export const INERT = { "pointer-events": "none" };

/**
 * Create the groups named by `spec` under `parent`, in order (first = bottom), and return them keyed by
 * name. A spec value is the group's attributes, or `null` for a plain interactive group.
 */
export function layerStack(parent, spec) {
  const layers = {};
  for (const [name, attrs] of Object.entries(spec)) {
    const g = svgEl("g", { "data-layer": name, ...(attrs ?? {}) });
    parent.appendChild(g);
    layers[name] = g;
  }
  return layers;
}

/** Show or hide several elements at once — the shape the 3-D preview's enter/leave needs. */
export function showLayers(els, on) {
  for (const el of els) if (el) el.style.display = on ? "" : "none";
}

/** Remove every child of a layer, leaving the group in place. */
export function clearLayer(el) {
  while (el?.firstChild) el.removeChild(el.firstChild);
}
