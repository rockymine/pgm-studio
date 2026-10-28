// The reader's text size (`--ui-scale`, tokens.css) and the studio's typefaces for text drawn on a canvas,
// which no stylesheet reaches.
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

const FALLBACK_STACKS = {
  "--font-sans": "Geist, ui-sans-serif, system-ui, sans-serif",
  "--font-mono": "'Geist Mono', ui-monospace, monospace",
};

/** A typeface stack the tokens name (`--font-sans`, `--font-mono`), spelled out for a canvas context or an svg
 *  attribute, neither of which reads a custom property. */
export function fontStack(token = "--font-sans") {
  const fallback = FALLBACK_STACKS[token] ?? FALLBACK_STACKS["--font-sans"];
  if (typeof document === "undefined" || typeof getComputedStyle !== "function") return fallback;
  return getComputedStyle(document.documentElement).getPropertyValue(token).trim() || fallback;
}
