// What a change did, drawn over the board: the shapes and props two versions of a layout disagree on, and the
// runs of columns the two builds disagree on. The overlay is read off the two documents themselves, by id, so
// it draws what was stored rather than what an edit list says about it.

import { toRing } from "../geometry/shape.js";

/** The fields that make a shape's outline; a change to any other field leaves the outline where it was. */
const OUTLINE = ["type", "operation", "min_x", "min_z", "max_x", "max_z", "center_x", "center_z", "radius",
  "vertices", "controls", "stroke_edge", "stroke_seed"];

const SHAPE_COLOURS = {
  added: "var(--canvas-diff-added)",
  removed: "var(--canvas-diff-removed)",
  reshaped: "var(--canvas-diff-reshaped)",
  changed: "var(--canvas-diff-changed)",
};

const PROP_COLOURS = {
  placed: SHAPE_COLOURS.added,
  removed: SHAPE_COLOURS.removed,
  moved: SHAPE_COLOURS.reshaped,
  changed: SHAPE_COLOURS.changed,
};

const RUN_COLOURS = {
  ground: "var(--canvas-diff-ground)",
  surface: "var(--canvas-diff-surface)",
  structure: "var(--canvas-diff-structure)",
};

const same = (a, b) => JSON.stringify(a ?? null) === JSON.stringify(b ?? null);

function shapesOf(layout) {
  const byId = new Map();
  for (const layer of layout?.layers ?? [])
    for (const shape of layer?.layout?.shapes ?? []) if (shape?.id) byId.set(shape.id, shape);
  return byId;
}

function propsOf(layout) {
  const byId = new Map();
  for (const prop of layout?.dressing?.props ?? []) if (prop?.id) byId.set(prop.id, prop);
  return byId;
}

function outlineOf(shape) {
  const outline = {};
  for (const key of OUTLINE) if (shape[key] !== undefined) outline[key] = shape[key];
  return outline;
}

function ringOf(shape) {
  try {
    const ring = toRing(shape);
    return ring.length ? ring : null;
  } catch {
    return null;
  }
}

/** Where a prop stands: its own `x` and `z`, or the middle of the points it is drawn along. */
function anchorOf(prop) {
  if (Number.isFinite(prop?.x) && Number.isFinite(prop?.z)) return [prop.x, prop.z];
  const points = (prop?.points ?? []).filter(point => Array.isArray(point) && point.length >= 2);
  if (!points.length) return null;
  return [points.reduce((sum, point) => sum + point[0], 0) / points.length,
          points.reduce((sum, point) => sum + point[1], 0) / points.length];
}

/**
 * The overlay between two layouts: every shape added, removed, reshaped (its outline moved) or otherwise
 * changed, with the outline it had and the one it has; every prop placed, removed, moved or otherwise changed,
 * with where it stood and where it stands; and the world diff's runs, one box each.
 */
export function diffOverlay(before, after, world) {
  const shapes = [];
  const was = shapesOf(before), now = shapesOf(after);
  for (const [id, shape] of now) {
    const held = was.get(id);
    if (!held) shapes.push({ id, state: "added", before: null, after: ringOf(shape) });
    else if (!same(outlineOf(held), outlineOf(shape)))
      shapes.push({ id, state: "reshaped", before: ringOf(held), after: ringOf(shape) });
    else if (!same(held, shape)) shapes.push({ id, state: "changed", before: null, after: ringOf(shape) });
  }
  for (const [id, held] of was) if (!now.has(id)) shapes.push({ id, state: "removed", before: ringOf(held), after: null });

  const props = [];
  const placedBefore = propsOf(before), placedNow = propsOf(after);
  for (const [id, prop] of placedNow) {
    const held = placedBefore.get(id);
    if (!held) { props.push({ id, state: "placed", from: null, to: anchorOf(prop) }); continue; }
    const from = anchorOf(held), to = anchorOf(prop);
    if (!same(from, to)) props.push({ id, state: "moved", from, to });
    else if (!same(held, prop)) props.push({ id, state: "changed", from: null, to });
  }
  for (const [id, held] of placedBefore)
    if (!placedNow.has(id)) props.push({ id, state: "removed", from: anchorOf(held), to: null });

  const runs = Object.keys(RUN_COLOURS).flatMap(kind =>
    (world?.[kind]?.runs ?? []).map(run => ({ kind, ...run })));
  return { shapes, props, runs };
}

/** Draw an overlay: the column runs under everything, then the outlines — where a shape was dashed, where it
 *  is solid — then the props, a hollow dot where one stood and a filled one where it stands. */
export function paintDiff(painter, overlay) {
  if (!overlay) return;
  for (const run of overlay.runs ?? []) {
    const colour = RUN_COLOURS[run.kind];
    painter.rect({ min_x: run.minX, min_z: run.minZ, max_x: run.maxX + 1, max_z: run.maxZ + 1 },
      { fill: colour, fillAlpha: 0.16, stroke: colour, width: 1, dash: [3, 3] });
  }
  for (const shape of overlay.shapes ?? []) {
    const colour = SHAPE_COLOURS[shape.state];
    if (shape.before) painter.ring(shape.before, { stroke: colour, width: 2, dash: [5, 4] });
    if (shape.after) painter.ring(shape.after, { stroke: colour, width: 2.5, fill: colour, fillAlpha: 0.1 });
  }
  for (const prop of overlay.props ?? []) {
    const colour = PROP_COLOURS[prop.state];
    if (prop.from && prop.to) painter.line(prop.from[0], prop.from[1], prop.to[0], prop.to[1], { stroke: colour, width: 2, dash: [3, 2] });
    if (prop.from) painter.dot(prop.from[0], prop.from[1], { stroke: colour, width: 2, radiusPx: 5 });
    if (prop.to) painter.dot(prop.to[0], prop.to[1], { fill: colour, radiusPx: 5 });
  }
}
