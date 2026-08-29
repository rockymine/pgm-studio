/**
 * How placed relief marks look on the sketch canvas. Stateless paint, like the rest of `render/` — it owns
 * *what* a mark looks like and the canvas owns *when* to draw it.
 *
 * A mark is drawn as **the ground it holds**, not as an icon: a spot height as the disc its radius pins, a
 * ridgeline as the band its width holds, an area as the ring that was drawn, a scarp as its face with the
 * band either side of it. An icon would be readable and would also be a lie about how much of the group the
 * statement covers, which is the one question an author placing terrain is asking.
 *
 * Colour carries the **height**, not the kind. That is the opposite of the dressing overlay's rule and it is
 * the right way round here: every mark does the same thing to the ground and differs only in where and how
 * high, so a glance should answer "is this the high one or the low one" rather than "is this a line or an
 * area" — which the drawn shape already says. The ramp is the contour amber at full strength for a high mark
 * through to a cold blue for a low one, read against the group's own base.
 */

import { markAnchor, markPoints, markReach, pushAmounts, isSpot, isRing, isPush, FALLBACK_BASE }
  from "../relief/relief-doc.js";

// Screen px, both. A label is chrome rather than ground: it says what a mark states, and what a mark states
// does not get bigger as the board is zoomed into. LABEL_MIN_PX is the mark's own on-screen reach, so a mark
// under about eighteen pixels across wears no number — it is a dot at that zoom, and a number wider than the
// thing it labels reads as floating on the board rather than naming anything.
const LABEL_SIZE_PX = 11;
const LABEL_MIN_PX = 9;

const FILL_ALPHA = 0.28;
const GHOST_ALPHA = 0.12;      // a mirror image is context, not a thing to click
const SELECTED_WIDTH = 2.5;
const CIRCLE_POINTS = 24;

/** Where a mark's height puts it on the cold-to-warm ramp: below the base is a cut, above it is a rise, and
 *  the span either way is what a relief realistically covers rather than the field's own extremes — a single
 *  outlier should not wash every other mark to the middle of the scale. */
const SPAN = 12;

export function heightColor(height, base = FALLBACK_BASE) {
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
 * Where a push's *lift* puts it on the same ramp. A push states a relative move rather than a level, so it
 * cannot be read against the group's base the way a mark is: five blocks up is five blocks up wherever it is
 * drawn. Reading it as a move from zero puts a lift and a mark at the same height in the same colour, which
 * is the reading an author wants — warm is higher ground either way.
 */
export const liftColor = (amount) => heightColor(amount, 0);

/**
 * Paint every mark, plus the mirror images of each. `mirrorPoint(x, z, k)` gives the k-th image of a point and
 * `orderOf(groupId)` how many images that group's marks have — the canvas already owns the map's symmetry,
 * so this asks rather than re-deriving it. `baseOf(groupId)` supplies the level each group's marks are read
 * against.
 *
 * The order is asked **per group** rather than taken once for the map, because mirroring is a property of the
 * group: the rasterizer fans only the groups that opted in. Ghosting a mark on a group that does not
 * mirror draws ground the export will never build, which is worse than drawing nothing — it is the one thing
 * an overlay must not do.
 */
export function paintReliefMarks(painter, marks, { selectedId = null, mirrorPoint = null, orderOf = null, baseOf = null } = {}) {
  for (const mark of marks ?? []) {
    const base = baseOf?.(mark.groupId) ?? FALLBACK_BASE;
    const order = Math.max(1, orderOf?.(mark.groupId) ?? 1);
    const colour = isPush(mark) ? liftColor(topHeight(mark)) : heightColor(topHeight(mark), base);

    // A push's skirt — the ground it moves outside the ring it was drawn on — as a dashed outline at the
    // falloff distance. It is the difference between a push and a bench made visible: a bench ends at its
    // outline, where a push is still moving ground a stated distance past it. Drawn on the primary image
    // only: it is a reading aid for the thing being edited, and a dashed halo round every mirror copy would
    // crowd the board with lines nobody can grab.
    if (isPush(mark) && (mark.falloff ?? 0) > 0) {
      const skirt = offsetRing(markPoints(mark), mark.falloff);
      if (skirt.length >= 3) painter.ring(skirt, { stroke: colour, strokeAlpha: 0.5, width: 1, dash: [4, 4] });
    }
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
    // board of unlabelled patches says only that something was stated somewhere — but a mark drawn smaller
    // than its own number is better read from its colour and the list.
    const reach = markReach(mark);
    if (reach >= painter.screenPx(LABEL_MIN_PX)) {
      const [ax, az] = markAnchor(mark);
      // A spot's anchor is also where its point grip sits, so its number rides just clear of the disc
      // rather than under the grip. Every other kind is anchored on ground it covers, and reads centred.
      const lift = isSpot(mark) ? reach + painter.screenPx(LABEL_SIZE_PX) : 0;
      painter.text(label(mark), ax, az, {
        dy: -lift, size: painter.screenPx(LABEL_SIZE_PX), weight: 600, fill: colour,
        halo: "var(--canvas-bg)", haloWidth: 3,
      });
    }
  }
}

/** The in-progress trace, dashed — the same treatment a shape being drawn gets. */
export function paintMarkPreview(painter, kind, points, height, base = FALLBACK_BASE) {
  if (!points || points.length < 2) return;
  const colour = heightColor(height, base);
  const style = { fill: colour, fillAlpha: 0.18, stroke: colour, width: 1, dash: [5, 3], fillRule: "evenodd" };
  if (isRing(kind)) { painter.ring(points, style); return; }
  const runs = [];
  for (let i = 1; i < points.length; i++)
    runs.push({ x1: points[i - 1][0], z1: points[i - 1][1], x2: points[i][0], z2: points[i][1] });
  painter.segments(runs, { stroke: colour, width: 2, dash: [5, 3] });
}

/** Where a spot height will land, before the click that places it. Off any group it reads as refused — a
 *  relief is stated *inside* a group, so there is nothing off one for a mark to belong to. */
export function paintSpotGhost(painter, x, z, radius, height, base = FALLBACK_BASE, valid = true) {
  const colour = valid ? heightColor(height, base) : "#c0392b";
  painter.ring(disc(x, z, radius), {
    fill: colour, fillAlpha: valid ? 0.18 : 0.12, stroke: colour, width: valid ? 1 : 1.5, dash: [5, 3],
  });
}

// What a statement's swatch is coloured by: the height it states, a scarp's shelf — a scarp is drawn from the
// top down, so the shelf is the height it reads as — or, for a push, its largest lift.
function topHeight(mark) {
  if (isPush(mark)) {
    const amounts = pushAmounts(mark);
    return amounts.length ? amounts.reduce((a, b) => (Math.abs(b) > Math.abs(a) ? b : a)) : (mark.amount ?? 0);
  }
  if (mark.kind === "scarp") return mark.high ?? 0;
  return Array.isArray(mark.h) ? Math.max(...mark.h) : (mark.h ?? 0);
}

// The number a statement wears. A falling ridgeline wears both ends, a scarp wears its drop, and a push wears
// its lift with a sign — because one bare number would hide exactly what each of those exists to state, and a
// push showing "5" where a mark shows "5" would read as a height rather than as five blocks of move.
function label(mark) {
  if (isPush(mark)) {
    const amounts = pushAmounts(mark);
    const [first, last] = [amounts[0] ?? 0, amounts[amounts.length - 1] ?? 0];
    const varies = amounts.some(amount => amount !== first);
    return varies ? `${signed(first)}→${signed(last)}` : signed(first);
  }
  if (mark.kind === "scarp") return `${round(mark.high)}↓${round(mark.low)}`;
  if (Array.isArray(mark.h) && mark.h.length > 1 && mark.h[0] !== mark.h[mark.h.length - 1])
    return `${round(mark.h[0])}–${round(mark.h[mark.h.length - 1])}`;
  return `${round(Array.isArray(mark.h) ? mark.h[0] : mark.h)}`;
}

const signed = (value) => `${round(value) > 0 ? "+" : ""}${round(value)}`;

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
  const width = mark.kind === "scarp" ? (mark.face ?? 2) : (mark.r ?? 2);
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

/**
 * A ring pushed outward by `distance`, for drawing a push's skirt. Each vertex moves along the bisector of
 * its two edges, scaled so the offset edges land the full distance out rather than short of it on a sharp
 * corner. Approximate on purpose: the solver measures the skirt as distance **across the land** from the ring
 * — which bends round a notch where this cannot — so this is the outline's honest shape and not a second
 * implementation of the falloff.
 */
function offsetRing(ring, distance) {
  const count = ring.length;
  if (count < 3 || !(distance > 0)) return [];
  // Which side "out" is depends on the winding, and an author traces a ring either way — so the sign comes
  // from the ring's own signed area rather than from an assumption about how it was drawn. Traced the other
  // way round, an unflipped offset would draw the skirt INSIDE the push, which reads as a smaller push.
  const side = signedArea(ring) < 0 ? -1 : 1;
  const out = [];
  for (let i = 0; i < count; i++) {
    const prev = ring[(i - 1 + count) % count], here = ring[i], next = ring[(i + 1) % count];
    const inNormal = normal(prev, here, side), outNormal = normal(here, next, side);
    let bx = inNormal[0] + outNormal[0], bz = inNormal[1] + outNormal[1];
    const length = Math.hypot(bx, bz);
    if (length < 1e-9) { out.push([here[0] + inNormal[0] * distance, here[1] + inNormal[1] * distance]); continue; }
    bx /= length; bz /= length;
    // 1/cos(half the corner angle): how much farther a corner has to move for its two edges to sit the full
    // distance out. Capped, or a near-reversal would fling the corner off the map.
    const scale = Math.min(4, 1 / Math.max(0.25, (bx * inNormal[0] + bz * inNormal[1])));
    out.push([here[0] + bx * distance * scale, here[1] + bz * distance * scale]);
  }
  return out;
}

// The unit normal of the edge from `from` to `to`, on the side the ring's winding says is outward.
function normal(from, to, side) {
  const dx = to[0] - from[0], dz = to[1] - from[1];
  const length = Math.hypot(dx, dz) || 1;
  return [(dz / length) * side, (-dx / length) * side];
}

// Twice the ring's signed area — positive one way round, negative the other. Only the sign is read.
function signedArea(ring) {
  let total = 0;
  for (let i = 0; i < ring.length; i++) {
    const [x1, z1] = ring[i], [x2, z2] = ring[(i + 1) % ring.length];
    total += x1 * z2 - x2 * z1;
  }
  return total;
}

function disc(cx, cz, radius) {
  const points = [];
  for (let i = 0; i < CIRCLE_POINTS; i++) {
    const angle = (2 * Math.PI * i) / CIRCLE_POINTS;
    points.push([cx + radius * Math.cos(angle), cz + radius * Math.sin(angle)]);
  }
  return points;
}
