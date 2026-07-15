/**
 * The one place every editor's primitive drawing style lives. A drawable primitive (region, sketch
 * shape, plan piece, point marker) is styled by its semantic *treatment*, not by which editor drew it —
 * so the same tier looks the same everywhere and a recolour/retune happens in one spot.
 *
 * `primitiveStyle(treatment, opts)` returns an attrs object ready to spread onto an SVG element. Colour
 * is always supplied by the caller (`color`, and optionally a distinct `stroke`/`fill`) — this module
 * never decides colour, only the opacity / stroke / dash knobs of each tier.
 *
 * Treatments:
 *   region     translucent dashed outline — Edit/Configure regions (rect/point). States: normal/ghost/selected.
 *   marker     a fixed-size point dot — a spawn / wool source. `primary` = the authored one (brighter, larger).
 *   sketch     a terrain shape in the boolean vocabulary — add/subtract colour supplied; `override` → dashed.
 *   terrain    solid opaque fill — a plan generating piece (colour tinted by surface by the caller). Ghost variant.
 *   technical  hatched fill — a plan buffer/connector annotation (hatch pattern url supplied as `fill`). Ghost variant.
 *   zone       translucent accent — a plan build zone. Ghost variant.
 */

const NSS = "non-scaling-stroke";

/** Fill/stroke colour tokens for the sketch boolean vocabulary — single source (shape + draw-preview render). */
export const OP_COLORS = {
  add:      { fill: "var(--canvas-add-fill)", stroke: "var(--canvas-add-stroke)" },
  subtract: { fill: "var(--canvas-sub-fill)", stroke: "var(--canvas-sub-stroke)" },
};
export const opColors = (operation) => (operation === "subtract" ? OP_COLORS.subtract : OP_COLORS.add);

/**
 * @param {string} treatment  — one of the treatments above.
 * @param {object} opts
 * @param {string} [opts.color]    — the primitive's colour (fill + stroke default to it).
 * @param {string} [opts.fill]     — explicit fill override (e.g. a hatch-pattern url for `technical`).
 * @param {string} [opts.stroke]   — explicit stroke override (e.g. sketch's darker stroke shade).
 * @param {"normal"|"ghost"|"selected"} [opts.state="normal"]
 * @param {boolean} [opts.primary] — `marker` only: the authored marker (vs a faint orbit copy).
 * @param {boolean} [opts.override]— `sketch` only: an add/subtract that overrides normal boolean order.
 * @param {boolean} [opts.heightMap] — `terrain` only: the height-map ramp mode (slightly more opaque).
 * @returns {object} SVG attrs to spread onto the element.
 */
export function primitiveStyle(treatment, opts = {}) {
  const c = opts.color;
  const fill = opts.fill ?? c;
  const stroke = opts.stroke ?? c;
  const state = opts.state ?? "normal";

  switch (treatment) {
    case "region":
      if (state === "ghost")
        return { fill: c, "fill-opacity": "0.06", stroke: c, "stroke-opacity": "0.30",
                 "stroke-width": "1.5", "stroke-dasharray": "2 3", "vector-effect": NSS };
      if (state === "selected")
        return { fill: c, "fill-opacity": "0.22", stroke: c, "stroke-opacity": "0.85",
                 "stroke-width": "2.5", "vector-effect": NSS };
      return { fill: c, "fill-opacity": "0.20", stroke: c, "stroke-opacity": "0.55",
               "stroke-width": "1.5", "stroke-dasharray": "4 2", "vector-effect": NSS };

    case "marker":
      return { r: opts.primary ? 6 : 5, fill: c, stroke: "var(--canvas-marker-stroke)",
               "stroke-width": opts.primary ? "2" : "1", opacity: opts.primary ? "1" : "0.55" };

    case "sketch":
      return { fill, stroke, "stroke-width": "1.2", "fill-opacity": "0.28", "vector-effect": NSS,
               ...(opts.override ? { "stroke-dasharray": "6 3" } : {}) };

    case "terrain":
      if (state === "ghost")
        return { fill: c, "fill-opacity": "0.08", stroke: c, "stroke-opacity": "0.5",
                 "stroke-width": "1", "stroke-dasharray": "5 4", "vector-effect": NSS };
      return { fill: c, "fill-opacity": opts.heightMap ? "0.85" : "0.7", stroke,
               "stroke-width": "1.5", "vector-effect": NSS };

    case "technical":
      if (state === "ghost")
        return { fill, "fill-opacity": "0.3", stroke: c, "stroke-opacity": "0.4",
                 "stroke-width": "1", "stroke-dasharray": "5 4", "vector-effect": NSS };
      return { fill, "fill-opacity": "0.9", stroke: c, "stroke-opacity": "0.85",
               "stroke-width": "1.4", "stroke-dasharray": "5 4", "vector-effect": NSS };

    case "zone":
      if (state === "ghost")
        return { fill: c, "fill-opacity": "0.07", stroke: c, "stroke-opacity": "0.5",
                 "stroke-width": "1.2", "stroke-dasharray": "7 4", "vector-effect": NSS };
      return { fill: c, "fill-opacity": "0.22", stroke: c,
               "stroke-width": "1.4", "stroke-dasharray": "7 4", "vector-effect": NSS };

    default:
      return { fill: c, stroke: c, "vector-effect": NSS };
  }
}
