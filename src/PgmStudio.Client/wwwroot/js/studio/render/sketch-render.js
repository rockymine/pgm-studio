/**
 * Stateless paint for the sketch tool — draw primitives, group/mirror result polygons, and the setup
 * overlays (bbox / chunk grid / symmetry axis). Each function takes the canvas's `CanvasPainter` + data
 * and draws; nothing is retained between frames, so the canvas owns *when* to paint and this owns *what*
 * a thing looks like — which keeps sketch-canvas.js focused on state + interaction. Reuses
 * render/shape-render for type dispatch and render/primitive-style for the treatment tiers.
 */

import { paintShape } from "./shape-render.js";
import { toRing } from "../geometry/shape.js";
import { primitiveStyle, opColors, OBJECTIVE_COLORS, BUILDING_COLORS } from "./primitive-style.js";
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
  } else if (shape.type === "polyline") {
    // The band, not the line — a path is filled by what it covers, the same as every other primitive.
    const ring = toRing(shape);
    if (ring.length >= 3) painter.ring(ring, { ...style, fillRule: "evenodd" });
  } else if ((shape.type === "polygon" || shape.type === "lasso") && (shape.vertices?.length ?? 0) >= 3) {
    painter.ring(shape.vertices, { ...style, fillRule: "evenodd" }, shape.controls || {});
  }
}

// Human-readable kind word for a structural role (S25). Wool rooms read as "wool"; spawn stays "spawn".
const STRUCT_KIND = { spawn: "spawn", woolRoom: "wool" };

// Plan-tool role colours (the canonical set is plan/plan-doc.js ROLE_COLORS — purple spawn, green wool room),
// so the surfaced pieces read in the same colours the plan drew them in. Kept as two local constants rather
// than importing the plan module into the render leaf; if the plan palette changes these, mirror them here.
const ROLE_FILL = { spawn: "#8f7bd6", woolRoom: "#3fae74" };

/**
 * The plan's structural pieces (S25) — the spawn and wool-room regions the plan already placed, projected
 * from the map intent as **locked, labelled** rectangles so they stay visible while a plan is refined. They
 * are not terrain (the rasterizer skips them; the ground under them is the fused group): a filled box in the
 * plan's role colour (purple spawn / green wool), a solid border, and a centred label sized to fit. The colour
 * carries the role, so the label carries the identity — a spawn's team, a wool's dye colour + owning team.
 * Read-only — never hit-tested or edited, so no selection chrome.
 *
 * A `building` shape is the footprint raised inside one of those regions, and is drawn as a dashed outline
 * over it rather than a second filled box: it stands inside a rectangle that is already labelled, so a second
 * label and a second fill would say the same thing twice over the same ground.
 */
export function paintStructural(painter, shapes) {
  for (const s of shapes ?? []) {
    if (s.type !== "rectangle") continue;
    const hex = ROLE_FILL[s.role] ?? "#8892a0";
    // A building is drawn as a building, in the same ink a dressed house takes, because that is what it is:
    // a room's footprint is the single-wing case of the one an author draws in the Dressing phase. It is
    // unfilled where a prop is filled, and only because it stands on a box already filled with its region's
    // colour — the ink is what says the two are one kind of thing.
    if (s.role === "building") {
      painter.rect({ min_x: s.min_x, min_z: s.min_z, max_x: s.max_x, max_z: s.max_z },
        { stroke: BUILDING_COLORS.stroke, strokeAlpha: 1, width: 2 });
      continue;
    }
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
 * Where the map's destroyables and cores stand, from the intent that places them (their anchor column). A
 * marker and nothing else: the sketch draws the ground, and these are here so an author refining it can see
 * what the ground has to carry — the plan names them, and Configure is where one is edited.
 *
 * Drawn in the objective's own colour at a fixed block size, so a board zoomed out still shows where they
 * are rather than a dot that shrinks away.
 */
export function paintObjectives(painter, objectives) {
  const half = 1.6;
  for (const { kind, x, z } of objectives ?? []) {
    if (!Number.isFinite(x) || !Number.isFinite(z)) continue;
    const color = OBJECTIVE_COLORS[kind] ?? "#888";
    painter.rect({ min_x: x - half, min_z: z - half, max_x: x + half, max_z: z + half },
      { fill: color, fillAlpha: 0.85, stroke: "var(--canvas-ink)", strokeAlpha: 0.7, width: 1.5 });
  }
}

/**
 * The computed group result polygons (exterior + holes). `filled` off draws the outline alone — what the
 * Blocks overlay needs, because the painted blocks under it *are* the group's interior and a translucent
 * fill on top would tint every block colour towards the result purple.
 */
export function paintGroups(painter, groups, { filled = true } = {}) {
  for (const group of groups ?? []) {
    if (!group?.exterior?.length) continue;
    painter.poly({ exterior: group.exterior, holes: group.holes ?? [] }, {
      ...(filled ? { fill: "var(--canvas-result-fill)", fillAlpha: 0.22 } : {}),
      stroke: "var(--canvas-result-stroke)", width: 1.5,
    });
  }
}

/** The *other* layers' group outlines, faintly — context for aligning the active layer. */
export function paintGhostGroups(painter, polys) {
  for (const poly of polys ?? []) {
    if (!poly?.exterior?.length) continue;
    painter.poly({ exterior: poly.exterior, holes: poly.holes ?? [] }, {
      fill: "var(--canvas-group)", fillAlpha: 0.07,
      stroke: "var(--canvas-group)", width: 1, dash: [2, 4],
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
 * group outline, so a curve visibly reads as the blocky cells it becomes on export. This is the geometry
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

/**
 * The solved surface as a shaded height map — one pixel per block, blitted over the group's own box.
 *
 * Contours say where the ground changes height and not which way, so reading a shape off them means counting
 * labels; a lightness ramp says it at a glance and the lines then say by how much. The two are one overlay for
 * that reason rather than two toggles: the fill is what makes the contours readable, and the contours are what
 * make the fill measurable.
 *
 * The ramp runs dark-low to light-high over the group's OWN range, so a board whose relief is four blocks and
 * one whose relief is forty each use the whole ramp — what an author is judging is the shape of their surface,
 * not how it compares to a board they are not looking at. A group with no range at all is flat and is drawn at
 * the middle of the ramp rather than at whichever end a division by zero would pick.
 */
const HEIGHT_LOW  = [ 24,  38,  54];   // deep blue-grey: the lowest ground the group holds
const HEIGHT_HIGH = [242, 232, 206];   // bone: the highest

export function heightMapBitmap(group) {
  const heights = group?.heights;
  const width = (group?.max_x ?? 0) - (group?.min_x ?? 0) + 1;
  const depth = (group?.max_z ?? 0) - (group?.min_z ?? 0) + 1;
  if (!Array.isArray(heights) || width < 1 || depth < 1 || heights.length !== width * depth) return null;

  const low = group.min ?? 0, high = group.max ?? 0;
  const span = high - low;
  const image = new ImageData(width, depth);
  for (let i = 0; i < heights.length; i++) {
    const at = i * 4;
    const height = heights[i];
    // A cell the footprint does not hold stays fully transparent: the box is rectangular and the landmass
    // drawn in it is not, and tinting the gap would draw ground that is not there.
    if (height === null || height === undefined) { image.data[at + 3] = 0; continue; }
    const t = span > 0 ? (height - low) / span : 0.5;
    for (let channel = 0; channel < 3; channel++)
      image.data[at + channel] = Math.round(HEIGHT_LOW[channel] + (HEIGHT_HIGH[channel] - HEIGHT_LOW[channel]) * t);
    image.data[at + 3] = 255;
  }
  // A canvas rather than the ImageData itself: drawImage takes a drawable, and ImageData is not one — it is
  // pixels without a surface. One block per pixel, stretched to the box at paint time.
  const surface = document.createElement("canvas");
  surface.width = width;
  surface.height = depth;
  surface.getContext("2d").putImageData(image, 0, 0);
  return surface;
}

/** Blit each group's height map over the box it was measured on. `bitmaps` is keyed by group id. */
export function paintHeightMap(painter, relief, bitmaps, { alpha = 0.88 } = {}) {
  for (const group of relief?.groups ?? []) {
    const bitmap = bitmaps?.get(group.group);
    if (!bitmap) continue;
    // max_x/max_z are inclusive cell indices, so the box the image covers reaches one block past each.
    painter.image(bitmap, { min_x: group.min_x, min_z: group.min_z,
                            max_x: group.max_x + 1, max_z: group.max_z + 1 },
                  { alpha, smooth: false });
  }
}

/**
 * The relief as contour lines — the payload `sketch/relief` returns, one entry per group:
 * `{ group, min, max, lines: [{ level, closed, points: [x, z, x, z, …] }] }`. Nothing here decides where a
 * line goes; the server traces the same field the export builds from, so what is drawn is the ground that
 * will exist rather than a client's idea of it.
 *
 * Every fifth block is an **index** line — heavier, and the only one labelled. That is the map convention and
 * it is what makes the overlay readable rather than a hatch: at a one-block interval a team board carries
 * forty levels, and forty equal lines say only "there is a slope somewhere". The label is placed at the
 * line's own widest point rather than at a fixed spacing, so a small closed ring gets exactly one and a long
 * traverse gets one where there is room for it.
 */
export function paintContours(painter, relief, { indexEvery = 5 } = {}) {
  const hair = painter.screenPx(1);
  for (const group of relief?.groups ?? []) {
    for (const line of group.lines ?? []) {
      const points = line.points ?? [];
      if (points.length < 4) continue;

      const index = Math.abs(line.level % indexEvery) < 1e-6;
      const runs = [];
      for (let i = 2; i < points.length; i += 2)
        runs.push({ x1: points[i - 2], z1: points[i - 1], x2: points[i], z2: points[i + 1] });
      painter.segments(runs, {
        stroke: index ? "var(--canvas-contour-index)" : "var(--canvas-contour)",
        width: index ? hair * 1.8 : hair,
      });
    }

    // Labels last, so no line is stroked over one.
    for (const line of group.lines ?? []) {
      if (Math.abs(line.level % indexEvery) > 1e-6) continue;
      const at = labelPoint(line.points ?? []);
      if (at) painter.text(`${Math.round(line.level)}`, at.x, at.z, {
        size: painter.screenPx(CONTOUR_LABEL_PX), fill: "var(--canvas-contour-index)",
        halo: "var(--canvas-bg)", haloWidth: 3,
      });
    }
  }
}

// Screen px. A contour label reads its level off a map legend, not off the ground, so it holds its size as
// the board is zoomed — the same rule the hairlines beside it already follow.
const CONTOUR_LABEL_PX = 10;

// Where a contour's label goes: the middle of its straightest stretch, measured as how much of a window's
// walked length it covers as the crow flies. A contour's segments are all about a block long, so the longest
// single one says nothing; a label wants the flattest run it can find, because one sitting on a bend is
// crossed by the line on both sides of it. A line too short to hold a window is labelled at its midpoint.
const LABEL_WINDOW = 6;

function labelPoint(points) {
  const count = points.length / 2;
  if (count < 2) return null;
  const at = (i) => ({ x: points[i * 2], z: points[i * 2 + 1] });
  const middle = (i, j) => ({ x: (points[i * 2] + points[j * 2]) / 2, z: (points[i * 2 + 1] + points[j * 2 + 1]) / 2 });
  if (count <= LABEL_WINDOW) return middle(0, count - 1);

  let best = -1, found = middle(0, LABEL_WINDOW);
  for (let i = 0; i + LABEL_WINDOW < count; i++) {
    let walked = 0;
    for (let step = i; step < i + LABEL_WINDOW; step++)
      walked += Math.hypot(points[(step + 1) * 2] - points[step * 2], points[(step + 1) * 2 + 1] - points[step * 2 + 1]);
    if (walked < 1e-9) continue;
    const head = at(i), tail = at(i + LABEL_WINDOW);
    const straightness = Math.hypot(tail.x - head.x, tail.z - head.z) / walked;
    if (straightness > best) { best = straightness; found = middle(i, i + LABEL_WINDOW); }
  }
  return found;
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
