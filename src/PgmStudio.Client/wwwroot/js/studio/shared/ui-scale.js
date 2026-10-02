// The reader's text size (`--ui-scale`, tokens.css) for text drawn on a canvas, which no stylesheet reaches.
// A screen-space label is a fixed pixel size at any zoom, so it is sized here once per redraw.

/** The current `--ui-scale`: 1 at the default text size. */
export function uiScale() {
  if (typeof document === "undefined" || typeof getComputedStyle !== "function") return 1;
  const raw = getComputedStyle(document.documentElement).getPropertyValue("--ui-scale");
  const scale = parseFloat(raw);
  return Number.isFinite(scale) && scale > 0 ? scale : 1;
}

/** A canvas label's size in pixels: the design size, a step up so labels read on a large display, times the scale. */
export function labelPx(px) {
  return Math.round(px * 1.2 * uiScale());
}
