/**
 * How placed dressing looks on the sketch canvas. Stateless paint, like the rest of `render/` — it owns *what*
 * a prop looks like and the canvas owns *when* to draw it.
 *
 * The canvas draws each prop as the footprint it will actually occupy rather than as an icon: a path as the
 * band its stroke paves, an area as the ring that was drawn, a marker as the circle its crown or its rock will
 * fill. An icon would be readable and would also be a lie about how much of the map the thing takes, which is
 * the one question an author placing cover is asking.
 *
 * Every prop is also drawn at each of its **mirror images**, faded. A prop is fanned across the symmetry orbit
 * at export, so an author who cannot see the other half is authoring blind — the ghost is the same affordance
 * the shape mirror preview already gives.
 */

import { BUILDING_COLORS } from "./primitive-style.js";
import { pathRing, pathCenterline } from "../geometry/path.js";
import { isMarker, isRect, MAX_FOOTPRINT, propAnchor, propReach, rectFootprint, rectPlan, wingCorners }
  from "../dressing/dressing-doc.js";

// One colour family per kind, so a glance separates a route from a stand of trees without reading a label. A
// house takes the shared building ink, since a room's shell is drawn in it too.
const KIND_STYLE = {
  path:    { fill: "#8d8378", stroke: "#6f6459" },
  water:   { fill: "#4a86c4", stroke: "#2f5f92" },
  flora:   { fill: "#5aa64a", stroke: "#3f7f33" },
  house:   BUILDING_COLORS,
  tree:    { fill: "#2f7d46", stroke: "#1f5a31" },
  boulder: { fill: "#8a8f96", stroke: "#5f656d" },
};

const FILL_ALPHA = 0.34;
const GHOST_ALPHA = 0.16;      // a mirror image is context, not a thing to click
const SELECTED_WIDTH = 2.5;
const CIRCLE_POINTS = 24;      // a marker's footprint is a disc; this many points read as round at any zoom

/**
 * Paint every prop, plus the mirror images of each. `mirrorPoint(x, z, k)` gives the k-th image of a point and
 * `order` how many images there are — the canvas already owns the map's symmetry, so this asks rather than
 * re-deriving it.
 */
export function paintDressing(painter, props, { selectedId = null, mirrorPoint = null, order = 1 } = {}) {
  for (const prop of props ?? []) {
    for (let k = order - 1; k >= 0; k--) {          // images first, so the real prop draws over them
      // A building of several wings draws one ring per wing rather than one outline of the whole plan — an L
      // or a T reads as two touching rectangles instead of the traced silhouette a build actually stamps, which
      // is the honest picture of what this layer can draw without walking the plan's own outline (G177).
      for (const ring of footprints(prop, k, mirrorPoint)) {
        if (ring.length < 3) continue;
        const kind = KIND_STYLE[prop.kind] ?? KIND_STYLE.boulder;
        const selected = k === 0 && prop.id === selectedId;
        painter.ring(ring, {
          fill: kind.fill,
          fillAlpha: k === 0 ? FILL_ALPHA : GHOST_ALPHA,
          fillRule: "evenodd",
          stroke: kind.stroke,
          width: selected ? SELECTED_WIDTH : 1,
          alpha: k === 0 ? 1 : 0.7,
        });
      }
    }
    // The line an author dragged, over the band it implies — a path and a water channel are edited as their
    // route, so the route has to stay visible inside its own band.
    if ((prop.kind === "stroke" || prop.kind === "water") && (prop.points?.length ?? 0) >= 2) {
      const curve = pathCenterline(prop.points);
      const runs = [];
      for (let i = 1; i < curve.length; i++)
        runs.push({ x1: curve[i - 1][0], z1: curve[i - 1][1], x2: curve[i][0], z2: curve[i][1] });
      painter.segments(runs, { stroke: (KIND_STYLE[prop.kind] ?? KIND_STYLE.path).stroke, width: 1 });
    }
  }
}

/** The in-progress stroke or outline, dashed — the same treatment a shape being drawn gets. */
export function paintDressingPreview(painter, kind, points, radius) {
  if (!points || points.length < 2) return;
  const kindStyle = KIND_STYLE[kind] ?? KIND_STYLE.boulder;
  const style = { fill: kindStyle.fill, fillAlpha: 0.2, stroke: kindStyle.stroke, width: 1, dash: [5, 3], fillRule: "evenodd" };
  if (isRect(kind)) {
    // A drag that has overshot the largest building reads as refused, the way a marker over the void does.
    // Only overshoot: every rectangle starts too small to be a house, so painting that red would flicker on
    // the first pixel of every drag — where being too big is a thing the author kept dragging to do.
    const plan = rectPlan({ points });
    if (plan && plan.width * plan.depth > MAX_FOOTPRINT) {
      Object.assign(style, { fill: "#c0392b", fillAlpha: 0.12, stroke: "#c0392b", width: 1.5 });
    }
    const rect = rectRing(points);
    if (rect.length >= 3) painter.ring(rect, style);
    return;
  }
  const ring = (kind === "stroke" || kind === "water") ? pathRing({ points, radius }) : [...points, points[0]];
  if (ring.length >= 3) painter.ring(ring.slice(0, -1), style);
}

/** Where a marker will be dropped, before the click that drops it. Over the void it reads as refused — a red
 *  ring — because a marker seats on the ground and there is none there to take it. */
export function paintMarkerGhost(painter, kind, x, z, reach, valid = true) {
  const kindStyle = KIND_STYLE[kind] ?? KIND_STYLE.boulder;
  const colour = valid ? kindStyle : { fill: "#c0392b", stroke: "#c0392b" };
  painter.ring(disc(x, z, reach), {
    fill: colour.fill, fillAlpha: valid ? 0.2 : 0.12, stroke: colour.stroke, width: valid ? 1 : 1.5, dash: [5, 3],
  });
}

/** Every ring one image of a prop draws — more than one only for a building of several wings. */
function footprints(prop, image, mirrorPoint) {
  const mirror = (x, z) => (image === 0 || !mirrorPoint ? [x, z] : mirrorPoint(x, z, image));

  if (isMarker(prop)) {
    const [ax, az] = mirror(...propAnchor(prop));
    return [disc(ax, az, propReach(prop))];
  }
  if (prop.kind === "stroke" || prop.kind === "water") {
    const ring = pathRing(prop);
    return [ring.length ? ring.slice(0, -1).map(([x, z]) => mirror(x, z)) : []];
  }
  // A rectangle is stored as two opposite corners, so each wing is opened into four before being mirrored —
  // turning the corners is what makes a quarter-turn image swap a wing's width and depth, exactly as the
  // export does when it fans the same two points.
  if (isRect(prop)) return wingRings(prop).map(ring => ring.map(([x, z]) => mirror(x, z)));
  return [(prop.points ?? []).map(([x, z]) => mirror(x, z))];
}

/** The four corners of one rect prop's wing, spanning the whole blocks it covers — so the drawn outline is the
 *  footprint the stamp takes rather than the two cells the pointer happened to be over. */
function rectRing(points) {
  const plan = rectFootprint({ points });
  if (!plan) {
    if (points.length < 2) return [];
    const [x0, z0] = points[0], [x1, z1] = points[1];
    const [ax, bx] = [Math.min(x0, x1), Math.max(x0, x1) + 1];
    const [az, bz] = [Math.min(z0, z1), Math.max(z0, z1) + 1];
    return [[ax, az], [bx, az], [bx, bz], [ax, bz]];
  }
  const { minX, minZ, width, depth } = plan;
  return [[minX, minZ], [minX + width, minZ], [minX + width, minZ + depth], [minX, minZ + depth]];
}

/** Every wing of a rect prop, each as its own four-corner ring — one entry for the plain building every board
 *  carries, more for an L, a T or a U (G177). */
function wingRings(prop) {
  const wings = prop?.wings ?? (prop?.points ? [prop.points] : []);
  return wings.map(wing => rectRing(wingCorners(wing) ?? [])).filter(ring => ring.length >= 3);
}

function disc(cx, cz, radius) {
  const points = [];
  for (let i = 0; i < CIRCLE_POINTS; i++) {
    const angle = (2 * Math.PI * i) / CIRCLE_POINTS;
    points.push([cx + radius * Math.cos(angle), cz + radius * Math.sin(angle)]);
  }
  return points;
}
