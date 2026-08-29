/**
 * Grabbing a contour and moving it. Pure geometry and state — no DOM, no canvas — so what a drag *means* is
 * testable without one.
 *
 * A contour is a line of constant height, so moving one says the ground reaches that height **here** now. That
 * makes the gesture a `line` mark at the contour's own level, along the line's new position: the reading of a
 * surface and the way it is edited become the same object, rather than a picture beside a form. It is the one
 * interaction in the phase that starts from what the solver produced instead of from an empty tool.
 *
 * Two decisions are worth stating because neither is forced.
 *
 * **Which contour a press grabs.** Several run close together on a steep face — that is what steep means — so
 * a plain nearest-line test hands an author whichever one the cursor happened to fall on the near side of.
 * Index lines win ties inside a slack: they are the ones drawn heavily enough to aim at, and an author
 * reaching for a contour on a busy slope is reaching for one of those.
 *
 * **What the drag writes.** The whole traced line is moved by the pointer's delta rather than reshaped under
 * it. A contour has hundreds of points and a drag has one; bending it locally would need a brush radius, a
 * falloff and a decision about what happens at the ends — three settings to express "this height is over
 * here", which is one statement. The mark that lands is simplified like every other traced mark, so what an
 * author gets back is a handful of draggable points rather than the tracer's own polyline.
 */

import { douglasPeucker } from "../geometry/simplify.js";

/** How near a contour a press has to be to grab it, in blocks. Generous, because a contour is a hairline and
 *  the thing being aimed at is a line of height rather than a pixel. */
export const GRAB_SLACK = 2.5;

/** How much closer a plain contour has to be than an index line to win the press. Inside this the index line
 *  takes it: at a one-block interval a steep face carries a line per block, and the labelled ones are the only
 *  ones an author can actually aim at. */
const INDEX_PREFERENCE = 1.5;

/** The simplify tolerance a dragged contour's mark is written at — the tracer's polyline is one point per
 *  cell boundary, which is a mark nobody can edit. Matches the traced-mark tolerance elsewhere in the phase. */
const SIMPLIFY_TOLERANCE = 3;

/** A dragged contour is only worth a mark if it moved. Below this the press was a click on a line. */
const MOVED_ENOUGH = 1;

/**
 * The contour under a point, or null. `relief` is the payload `sketch/relief` returns; `indexEvery` matches
 * what the overlay draws heavily, since that is what an author sees to aim at.
 */
export function contourAt(relief, x, z, { slack = GRAB_SLACK, indexEvery = 5 } = {}) {
  let best = null;
  for (const group of relief?.groups ?? []) {
    for (const line of group.lines ?? []) {
      const distance = distanceToLine(line.points ?? [], x, z);
      if (distance > slack) continue;
      const index = Math.abs(line.level % indexEvery) < 1e-6;
      // An index line is judged as if it were nearer than it is, so it wins anything within the preference.
      const score = index ? distance - INDEX_PREFERENCE : distance;
      if (!best || score < best.score) best = { score, distance, group: group.group, line, index };
    }
  }
  return best && { groupId: best.group, level: best.line.level, points: best.line.points, index: best.index };
}

/**
 * The mark a finished contour drag states, or null when the contour did not move far enough to mean anything.
 * The level is the contour's own — a contour dragged somewhere else is still that height, which is the whole
 * reading — and the width is the band the mark holds, narrow because a contour is a line and not a shoulder.
 */
export function markFromDrag(grabbed, dx, dz, { width = 2 } = {}) {
  if (!grabbed || Math.hypot(dx, dz) < MOVED_ENOUGH) return null;
  const moved = [];
  for (let i = 0; i + 1 < grabbed.points.length; i += 2)
    moved.push([Math.round(grabbed.points[i] + dx), Math.round(grabbed.points[i + 1] + dz)]);

  const simplified = douglasPeucker(moved, SIMPLIFY_TOLERANCE);
  if (simplified.length < 2) return null;
  return {
    groupId: grabbed.groupId,
    mark: { kind: "line", id: "", points: simplified, h: Math.round(grabbed.level), width },
  };
}

/** The nearest distance from a point to a flat [x, z, x, z, …] polyline. */
function distanceToLine(points, x, z) {
  let best = Infinity;
  for (let i = 0; i + 3 < points.length; i += 2)
    best = Math.min(best, distanceToSegment(x, z, points[i], points[i + 1], points[i + 2], points[i + 3]));
  return best;
}

function distanceToSegment(x, z, x1, z1, x2, z2) {
  const dx = x2 - x1, dz = z2 - z1;
  const length = dx * dx + dz * dz;
  if (length < 1e-12) return Math.hypot(x - x1, z - z1);
  const along = Math.max(0, Math.min(1, ((x - x1) * dx + (z - z1) * dz) / length));
  return Math.hypot(x - (x1 + along * dx), z - (z1 + along * dz));
}
