/**
 * The one place every editor's primitive drawing style lives. A drawable primitive (region, sketch
 * shape, plan piece, point marker) is styled by its semantic *treatment*, not by which editor drew it —
 * so the same tier looks the same everywhere and a recolour/retune happens in one spot.
 *
 * `primitiveStyle(treatment, opts)` returns a style object in the painter's vocabulary
 * (`render/canvas-painter.js`): `fill`/`fillAlpha`, `stroke`/`strokeAlpha`/`width`/`dash`, `alpha`, and
 * `radiusPx` for the treatments that draw a fixed-size dot. Colour is always supplied by the caller
 * (`color`, and optionally a distinct `stroke`/`fill`) — this module never decides colour, only the
 * opacity / stroke / dash knobs of each tier. A width is in screen pixels at every zoom, which is what
 * `vector-effect: non-scaling-stroke` used to buy and the painter now gives for nothing.
 *
 * Treatments:
 *   region     translucent dashed outline — Edit/Configure regions (rect/point). States: normal/ghost/selected.
 *   marker     a fixed-size point dot — a spawn / wool source. `primary` = the authored one (brighter, larger).
 *   sketch     a terrain shape in the boolean vocabulary — add/subtract colour supplied; `override` → dashed.
 *   terrain    solid opaque fill — a plan generating piece (colour tinted by surface by the caller). Ghost variant.
 *   technical  hatched fill — a plan buffer annotation (the hatch pattern supplied as `fill`). Ghost variant.
 *   zone       translucent accent — a plan build zone. Ghost variant.
 */

/** Fill/stroke colour tokens for the sketch boolean vocabulary — single source (shape + draw-preview render). */
export const OP_COLORS = {
  add:      { fill: "var(--canvas-add-fill)", stroke: "var(--canvas-add-stroke)" },
  subtract: { fill: "var(--canvas-sub-fill)", stroke: "var(--canvas-sub-stroke)" },
};
export const opColors = (operation) => (operation === "subtract" ? OP_COLORS.subtract : OP_COLORS.add);

/** What each objective is drawn in, wherever one is drawn — the plan places them and the sketch shows where
 *  they landed, so the two read as the same thing rather than as two conventions. */
export const OBJECTIVE_COLORS = {
  spawn: "#e0b13c", wool: "#e6e6e6", iron: "#9aa7b4", destroyable: "#6b4f9e", core: "#d4622a",
};

/** What a building is drawn in, wherever it came from. A room's footprint is the single-wing case of the
 *  building an author draws in the Dressing phase, so a house dressed onto the ground and the shell raised on
 *  a spawn or wool room read as one kind of thing and not as two. */
export const BUILDING_COLORS = { fill: "#b08050", stroke: "#7d5732" };

/**
 * @param {string} treatment  — one of the treatments above.
 * @param {object} opts
 * @param {string} [opts.color]    — the primitive's colour (fill + stroke default to it).
 * @param {string} [opts.fill]     — explicit fill override (e.g. a hatch pattern for `technical`).
 * @param {string} [opts.stroke]   — explicit stroke override (e.g. sketch's darker stroke shade).
 * @param {"normal"|"ghost"|"selected"} [opts.state="normal"]
 * @param {boolean} [opts.primary] — `marker` only: the authored marker (vs a faint orbit copy).
 * @param {boolean} [opts.override]— `sketch` only: an add/subtract that overrides normal boolean order.
 * @param {boolean} [opts.heightMap] — `terrain` only: the height-map ramp mode (slightly more opaque).
 * @returns {object} a painter style object.
 */
export function primitiveStyle(treatment, opts = {}) {
  const color = opts.color;
  const fill = opts.fill ?? color;
  const stroke = opts.stroke ?? color;
  const state = opts.state ?? "normal";

  switch (treatment) {
    case "region":
      if (state === "ghost")
        return { fill: color, fillAlpha: 0.06, stroke: color, strokeAlpha: 0.30, width: 1.5, dash: [2, 3] };
      if (state === "selected")
        return { fill: color, fillAlpha: 0.22, stroke: color, strokeAlpha: 0.85, width: 2.5 };
      return { fill: color, fillAlpha: 0.20, stroke: color, strokeAlpha: 0.55, width: 1.5, dash: [4, 2] };

    case "marker":
      return { radius: opts.primary ? 6 : 5, fill: color, stroke: "var(--canvas-marker-stroke)",
               width: opts.primary ? 2 : 1, alpha: opts.primary ? 1 : 0.55 };

    case "sketch":
      return { fill, fillAlpha: 0.28, stroke, width: 1.2, ...(opts.override ? { dash: [6, 3] } : {}) };

    case "terrain":
      if (state === "ghost")
        return { fill: color, fillAlpha: 0.08, stroke: color, strokeAlpha: 0.5, width: 1, dash: [5, 4] };
      return { fill: color, fillAlpha: opts.heightMap ? 0.85 : 0.7, stroke, width: 1.5 };

    case "technical":
      if (state === "ghost")
        return { fill, fillAlpha: 0.3, stroke: color, strokeAlpha: 0.4, width: 1, dash: [5, 4] };
      return { fill, fillAlpha: 0.9, stroke: color, strokeAlpha: 0.85, width: 1.4, dash: [5, 4] };

    case "zone":
      if (state === "ghost")
        return { fill: color, fillAlpha: 0.07, stroke: color, strokeAlpha: 0.5, width: 1.2, dash: [7, 4] };
      return { fill: color, fillAlpha: 0.22, stroke: color, width: 1.4, dash: [7, 4] };

    default:
      return { fill: color, stroke: color };
  }
}
