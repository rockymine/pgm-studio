/**
 * How placed relief marks look on the sketch canvas. Stateless paint, like the rest of `render/` — it owns
 * *what* a mark looks like and the canvas owns *when* to draw it.
 *
 * A mark is drawn as **the ground it holds**, not as an icon: a spot height as the disc its radius pins, a
 * ridgeline as the band its width holds, an area as the ring that was drawn, a scarp as its face with the
 * band either side of it. An icon would be readable and would also be a lie about how much of the island the
 * statement covers, which is the one question an author placing terrain is asking.
 *
 * Colour carries the **height**, not the kind. That is the opposite of the dressing overlay's rule and it is
 * the right way round here: every mark does the same thing to the ground and differs only in where and how
 * high, so a glance should answer "is this the high one or the low one" rather than "is this a line or an
 * area" — which the drawn shape already says. The ramp is the contour amber at full strength for a high mark
 * through to a cold blue for a low one, read against the island's own base.
 */

import { markAnchor, markPoints, isSpot, isRing } from "../relief/relief-doc.js";

const FILL_ALPHA = 0.28;
const GHOST_ALPHA = 0.12;      // a mirror image is context, not a thing to click
const SELECTED_WIDTH = 2.5;
const CIRCLE_POINTS = 24;

/** Where a mark's height puts it on the cold-to-warm ramp: below the base is a cut, above it is a rise, and
 *  the span either way is what a relief realistically covers rather than the field's own extremes — a single
 *  outlier should not wash every other mark to the middle of the scale. */
const SPAN = 12;

export function heightColor(height, base = 8) {
  const t = Math.max(-1, Math.min(1, (height - base) / SPAN));
  if (t >= 0) {
    // base → summit: slate through amber.
    const mix = t;
    return `rgb(${Math.round(120 + 131 * mix)},${Math.round(130 + 61 * mix)},${Math.round(150 - 114 * mix)})`;
  }
  // base → hollow: slate through a cold blue, so a dip never reads as merely "a paler rise".
  const mix = -t;
  return `rgb(${Math.round(120 - 66 * mix)},${Math.round(130 - 30 * mix)},${Math.round(150 + 55 * mix)})`;
}

/**
 * Paint every mark, plus the mirror images of each. `mirrorPoint(x, z, k)` gives the k-th image of a point and
 * `order` how many images there are — the canvas already owns the map's symmetry, so this asks rather than
 * re-deriving it. `baseOf(islandId)` supplies the level each island's marks are read against.
 */
export function paintReliefMarks(painter, marks, { selectedId = null, mirrorPoint = null, order = 1, baseOf = null } = {}) {
  for (const mark of marks ?? []) {
    const base = baseOf?.(mark.islandId) ?? 8;
    const colour = heightColor(topHeight(mark), base);
    for (let k = order - 1; k >= 0; k--) {          // images first, so the real mark draws over them
      const ring = footprint(mark, k, mirrorPoint);
      if (ring.length < 3) continue;
      const selected = k === 0 && mark.id === selectedId;
      painter.ring(ring, {
        fill: colour,
        fillAlpha: k === 0 ? FILL_ALPHA : GHOST_ALPHA,
        fillRule: "evenodd",
        stroke: colour,
        width: selected ? SELECTED_WIDTH : 1,
        alpha: k === 0 ? 1 : 0.7,
      });
    }
    // The line an author drew, over the band it implies. A ridgeline and a scarp are edited as their line, so
    // the line has to stay visible inside the ground it holds — and for a scarp it is the face itself, which
    // is the thing the band is arranged around.
    const points = markPoints(mark);
    if (!isSpot(mark) && !isRing(mark) && points.length >= 2) {
      const runs = [];
      for (let i = 1; i < points.length; i++)
        runs.push({ x1: points[i - 1][0], z1: points[i - 1][1], x2: points[i][0], z2: points[i][1] });
      painter.segments(runs, { stroke: colour, width: mark.kind === "scarp" ? 2.5 : 1.5 });
    }
    // The height, at the mark's own anchor. A relief is a set of numbers before it is a set of shapes, and a
    // board of unlabelled patches says only that something was stated somewhere.
    const [ax, az] = markAnchor(mark);
    painter.text(label(mark), ax, az, {
      size: 11, weight: 600, fill: colour, halo: "var(--canvas-bg)", haloWidth: 3,
    });
  }
}

/** The in-progress trace, dashed — the same treatment a shape being drawn gets. */
export function paintMarkPreview(painter, kind, points, height, base = 8) {
  if (!points || points.length < 2) return;
  const colour = heightColor(height, base);
  const style = { fill: colour, fillAlpha: 0.18, stroke: colour, width: 1, dash: [5, 3], fillRule: "evenodd" };
  if (isRing(kind)) { painter.ring(points, style); return; }
  const runs = [];
  for (let i = 1; i < points.length; i++)
    runs.push({ x1: points[i - 1][0], z1: points[i - 1][1], x2: points[i][0], z2: points[i][1] });
  painter.segments(runs, { stroke: colour, width: 2, dash: [5, 3] });
}

/** Where a spot height will land, before the click that places it. Off any island it reads as refused — a
 *  relief is stated *inside* an island, so there is nothing off one for a mark to belong to. */
export function paintSpotGhost(painter, x, z, radius, height, base = 8, valid = true) {
  const colour = valid ? heightColor(height, base) : "#c0392b";
  painter.ring(disc(x, z, radius), {
    fill: colour, fillAlpha: valid ? 0.18 : 0.12, stroke: colour, width: valid ? 1 : 1.5, dash: [5, 3],
  });
}

// What a mark's swatch is coloured by: the height it states, or a scarp's shelf — a scarp is drawn from the
// top down, so the shelf is the height it reads as.
function topHeight(mark) {
  if (mark.kind === "scarp") return mark.high ?? 0;
  return Array.isArray(mark.h) ? Math.max(...mark.h) : (mark.h ?? 0);
}

// The number a mark wears. A falling ridgeline wears both ends and a scarp wears its drop, because one number
// would hide exactly what those two kinds exist to state.
function label(mark) {
  if (mark.kind === "scarp") return `${round(mark.high)}↓${round(mark.low)}`;
  if (Array.isArray(mark.h) && mark.h.length > 1 && mark.h[0] !== mark.h[mark.h.length - 1])
    return `${round(mark.h[0])}–${round(mark.h[mark.h.length - 1])}`;
  return `${round(Array.isArray(mark.h) ? mark.h[0] : mark.h)}`;
}

const round = (value) => Math.round(Number(value) || 0);

/** The ground one image of a mark holds, as an open ring the painter can fill. */
function footprint(mark, image, mirrorPoint) {
  const mirror = (x, z) => (image === 0 || !mirrorPoint ? [x, z] : mirrorPoint(x, z, image));
  const points = markPoints(mark);

  if (isSpot(mark)) {
    const [ax, az] = mirror(...markAnchor(mark));
    return disc(ax, az, Math.max(1, mark.r ?? 4));
  }
  if (isRing(mark)) return points.map(([x, z]) => mirror(x, z));
  // A line and a scarp hold a band around their run, so the drawn shape is the band — the offset ring of the
  // polyline, which is what the solver actually pins.
  const width = mark.kind === "scarp" ? (mark.face ?? 2) : (mark.width ?? 1.5);
  return band(points, width).map(([x, z]) => mirror(x, z));
}

/** The ring a polyline's band covers: one side out, the other side back. Blunt ends, because a mark's band
 *  stops where its line stops — rounding them would close the gap a scarp is drawn to leave. */
function band(points, halfWidth) {
  if (points.length < 2) return [];
  const left = [], right = [];
  for (let i = 0; i < points.length; i++) {
    const from = points[Math.max(0, i - 1)], to = points[Math.min(points.length - 1, i + 1)];
    const dx = to[0] - from[0], dz = to[1] - from[1];
    const length = Math.hypot(dx, dz) || 1;
    const nx = (-dz / length) * halfWidth, nz = (dx / length) * halfWidth;
    left.push([points[i][0] + nx, points[i][1] + nz]);
    right.push([points[i][0] - nx, points[i][1] - nz]);
  }
  return [...left, ...right.reverse()];
}

function disc(cx, cz, radius) {
  const points = [];
  for (let i = 0; i < CIRCLE_POINTS; i++) {
    const angle = (2 * Math.PI * i) / CIRCLE_POINTS;
    points.push([cx + radius * Math.cos(angle), cz + radius * Math.sin(angle)]);
  }
  return points;
}
