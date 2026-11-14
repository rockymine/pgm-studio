/**
 * outline-points.js — the point edits every outline the canvas draws takes, whatever owns it: a modifier's area
 * or run, a dressing route or area, an approach wall. A point drags; a press on the ghost at an edge's middle puts
 * a point there; a picked point is taken out by the delete key while the outline keeps enough to stay one. Pure,
 * so each controller asks the same arithmetic and a test asks it without a DOM.
 */

import { toScreen } from "./transform.js";
import { distToSegment } from "./shape.js";

/** The fewest points an outline keeps: a closed ring encloses something with three, an open run goes somewhere
 *  with two. */
export const leastPoints = (closed) => (closed ? 3 : 2);

/** How many edges an outline of `points` has: a closed ring's last point joins its first, an open run's does not. */
export const edgeCount = (points, closed) => (points?.length ?? 0) < 2 ? 0 : closed ? points.length : points.length - 1;

/**
 * The edge of `points` nearest the screen point `at`, within `threshold` screen px, and the screen middle of that
 * edge, where the insert ghost stands — or null where none is that near.
 */
export function edgeNear(points, closed, at, viewport, threshold) {
  const edges = edgeCount(points, closed);
  let best = threshold, found = null;
  for (let i = 0; i < edges; i++) {
    const j = (i + 1) % points.length;
    const a = toScreen(points[i][0], points[i][1], viewport);
    const b = toScreen(points[j][0], points[j][1], viewport);
    const away = distToSegment(at.x, at.y, a.x, a.y, b.x, b.y);
    if (away < best) { best = away; found = { edge: i, x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 }; }
  }
  return found;
}

/** `points` with a point put at the middle of edge `edge`, on the nearest block, and that point's index — or null
 *  for an edge the outline does not have. */
export function withMidpoint(points, edge, closed) {
  if (!points || edge < 0 || edge >= edgeCount(points, closed)) return null;
  const next = (edge + 1) % points.length;
  const middle = [Math.round((points[edge][0] + points[next][0]) / 2), Math.round((points[edge][1] + points[next][1]) / 2)];
  const index = edge + 1;
  return { points: [...points.slice(0, index).map(point => [...point]), middle, ...points.slice(index).map(point => [...point])], index };
}

/** `points` without the point at `index`, or null where taking it out would leave fewer than the outline keeps. */
export function withoutPoint(points, index, closed) {
  if (!points || index < 0 || index >= points.length || points.length - 1 < leastPoints(closed)) return null;
  return points.filter((_, at) => at !== index).map(point => [...point]);
}
