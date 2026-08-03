/**
 * Stateless paint for the sketch tool — draw primitives, island/mirror result polygons, and the setup
 * overlays (bbox / chunk grid / symmetry axis). Each function takes the canvas's `CanvasPainter` + data
 * and draws; nothing is retained between frames, so the canvas owns *when* to paint and this owns *what*
 * a thing looks like — which keeps sketch-canvas.js focused on state + interaction. Reuses
 * render/shape-render for type dispatch and render/primitive-style for the treatment tiers.
 */

import { paintShape } from "./shape-render.js";
import { primitiveStyle, opColors } from "./primitive-style.js";
import { dyeColorLabel } from "./palette.js";

function shapeStyle(shape, selected) {
  const { fill, stroke } = opColors(shape.operation);
  const style = primitiveStyle("sketch", { fill, stroke, override: shape.override });
  // Selection chrome without a class to toggle: a heavier stroke and a denser fill on the shape itself.
  return selected ? { ...style, width: 2.5, fillAlpha: 0.4 } : style;
}

/** One draw primitive: add/subtract coloured, dashed when it overrides the normal boolean order. */
export function paintSketchShape(painter, shape, { selected = false, alpha = 1 } = {}) {
  const style = { ...shapeStyle(shape, selected), alpha };
  if (shape.type === "rectangle") {
    paintShape(painter, "rectangle", shape, style);
  } else if (shape.type === "circle") {
    paintShape(painter, "circle", {
      min_x: shape.center_x - shape.radius, max_x: shape.center_x + shape.radius,
      min_z: shape.center_z - shape.radius, max_z: shape.center_z + shape.radius,
    }, style);
  } else if ((shape.type === "polygon" || shape.type === "lasso") && (shape.vertices?.length ?? 0) >= 3) {
    painter.ring(shape.vertices, { ...style, fillRule: "evenodd" }, shape.controls || {});
  }
}

/** Ghost preview of a library item being placed — the (already world-positioned) shape specs, faded. */
export function paintPlaceGhost(painter, specs) {
  for (const spec of specs ?? []) paintSketchShape(painter, spec, { alpha: 0.55 });
}

// Human-readable kind word for a structural role (S25). Wool rooms read as "wool"; spawn stays "spawn".
const STRUCT_KIND = { spawn: "spawn", woolRoom: "wool" };

// Plan-tool role colours (the canonical set is plan/plan-doc.js ROLE_COLORS — purple spawn, green wool room),
// so the surfaced pieces read in the same colours the plan drew them in. Kept as two local constants rather
// than importing the plan module into the render leaf; if the plan palette changes these, mirror them here.
const ROLE_FILL = { spawn: "#8f7bd6", woolRoom: "#3fae74" };

/**
 * The plan's structural pieces (S25) — the spawn buildings and wool rooms the plan already placed, projected
 * from the map intent as **locked, labelled** rectangles so they stay visible while a plan is refined. They
 * are not terrain (the rasterizer skips them; the ground under them is the fused island): a filled box in the
 * plan's role colour (purple spawn / green wool), a solid border, and a centred label sized to fit. The colour
 * carries the role, so the label carries the identity — a spawn's team, a wool's dye colour + owning team.
 * Read-only — never hit-tested or edited, so no selection chrome.
 */
export function paintStructural(painter, shapes) {
  for (const s of shapes ?? []) {
    if (s.type !== "rectangle") continue;
    const hex = ROLE_FILL[s.role] ?? "#8892a0";
    painter.rect({ min_x: s.min_x, min_z: s.min_z, max_x: s.max_x, max_z: s.max_z },
      { fill: hex, fillAlpha: 0.32, stroke: hex, strokeAlpha: 0.95, width: 2 });

    const kind = STRUCT_KIND[s.role] ?? s.role ?? "";
    // The box colour carries the role, so the label carries the identity as "<who> <role>": a spawn by its
    // team ("blue spawn"), a wool by its dye colour ("blue wool", "light blue wool"). A spawn's intentRef is
    // the team id; a wool's colour slug is its dye.
    const who = s.role === "spawn" ? (s.intentRef ?? "") : dyeColorLabel(s.color).toLowerCase();
    const text = `${who} ${kind}`;
    // World-space label, sized to fit inside the box in either axis so it never spills the footprint.
    const w = s.max_x - s.min_x, h = s.max_z - s.min_z;
    const size = Math.max(2, Math.min(h * 0.4, w / (0.6 * Math.max(1, text.length))));
    painter.text(text, (s.min_x + s.max_x) / 2, (s.min_z + s.max_z) / 2,
      { fill: "var(--canvas-ink)", halo: "var(--bg-canvas)", haloWidth: 3, size, weight: 600 });
  }
}

/**
 * The computed island result polygons (exterior + holes). `filled` off draws the outline alone — what the
 * Blocks overlay needs, because the painted blocks under it *are* the island's interior and a translucent
 * fill on top would tint every block colour towards the result purple.
 */
export function paintIslands(painter, islands, { filled = true } = {}) {
  for (const island of islands ?? []) {
    if (!island?.exterior?.length) continue;
    painter.poly({ exterior: island.exterior, holes: island.holes ?? [] }, {
      ...(filled ? { fill: "var(--canvas-result-fill)", fillAlpha: 0.22 } : {}),
      stroke: "var(--canvas-result-stroke)", width: 1.5,
    });
  }
}

/** The *other* layers' island outlines, faintly — context for aligning the active layer. */
export function paintGhostIslands(painter, polys) {
  for (const poly of polys ?? []) {
    if (!poly?.exterior?.length) continue;
    painter.poly({ exterior: poly.exterior, holes: poly.holes ?? [] }, {
      fill: "var(--canvas-island)", fillAlpha: 0.07,
      stroke: "var(--canvas-island)", width: 1, dash: [2, 4],
    });
  }
}

/** The live mirror-preview polygons. */
export function paintMirror(painter, polys) {
  for (const poly of polys ?? []) {
    if (!poly?.exterior?.length) continue;
    painter.poly({ exterior: poly.exterior, holes: poly.holes ?? [] }, {
      fill: "var(--canvas-mirror-fill)", stroke: "var(--canvas-mirror-stroke)", width: 1,
    });
  }
}

/**
 * The rasterized block footprint (S23) — the exact cells the shapes voxelize into, as merged horizontal
 * runs `{ x, z, w }` (see geometry/rasterize.cellRuns). Painted as a faint stone fill beneath the smooth
 * island outline, so a curve visibly reads as the blocky cells it becomes on export. This is the geometry
 * preview only; the *paint* those cells receive is the block bitmap the canvas blits over it.
 */
export function paintRaster(painter, runs) {
  // A faint fill plus a hairline round each run — the run edges are exactly the stair-stepped cell
  // boundaries, so the voxelization reads as blocks — in the neutral stone a sketch is built from.
  const stone = "var(--canvas-result-stroke)";
  const line = painter.screenPx(1);
  for (const { x, z, w } of runs ?? []) {
    painter.rect({ min_x: x, max_x: x + w, min_z: z, max_z: z + 1 },
      { fill: stone, fillAlpha: 0.20, stroke: stone, strokeAlpha: 0.55, width: line });
  }
}

/** The working-bounds rectangle — the tight world bound of what a finish would rasterize. */
export function paintBbox(painter, bbox) {
  if (!bbox) return;
  painter.rect(bbox, { stroke: "var(--border)", width: 1 });
}

/**
 * The chunk grid across the visible extent. `step` is a multiple of the chunk: 1 draws every chunk line,
 * higher values thin the grid out when a chunk is only a few pixels across (see canvas-chrome's
 * gridStep), so zooming out cannot grow the line count without bound. Every line is one path.
 */
export function paintChunkGrid(painter, bbox, step = 1) {
  if (!bbox) return;
  const { min_x, max_x, min_z, max_z } = bbox;
  const span = 16 * Math.max(1, step);
  const runs = [];
  for (let x = Math.ceil(min_x / span) * span; x <= max_x; x += span) runs.push({ x1: x, z1: min_z, x2: x, z2: max_z });
  for (let z = Math.ceil(min_z / span) * span; z <= max_z; z += span) runs.push({ x1: min_x, z1: z, x2: max_x, z2: z });
  painter.segments(runs, { stroke: "var(--canvas-chunk)", width: 1, dash: [3, 3] });
}

/** The symmetry axis line(s) for the current mirror mode, through the centre, clipped to the bbox. */
export function paintAxis(painter, bbox, center, mode) {
  if (!bbox) return;
  const { min_x, max_x, min_z, max_z } = bbox;
  const cx = center?.cx ?? 0, cz = center?.cz ?? 0;
  const style = { stroke: "var(--canvas-axis)", width: 1, dash: [6, 4], alpha: 0.75 };
  const runs = [];
  if (mode !== "mirror_z") runs.push({ x1: cx, z1: min_z, x2: cx, z2: max_z });
  if (mode !== "mirror_x") runs.push({ x1: min_x, z1: cz, x2: max_x, z2: cz });
  painter.segments(runs, style);
}
