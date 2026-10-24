/**
 * Stateless paint for the sketch tool — draw primitives, group/mirror result polygons, and the setup
 * overlays (bbox / chunk grid / symmetry axis). Each function takes the canvas's `CanvasPainter` + data
 * and draws; nothing is retained between frames, so the canvas owns *when* to paint and this owns *what*
 * a thing looks like — which keeps sketch-canvas.js focused on state + interaction. Reuses
 * render/shape-render for type dispatch and render/primitive-style for the treatment tiers.
 */

import { paintShape } from "./shape-render.js";
import { toRing } from "../geometry/shape.js";
import { primitiveStyle, opColors, OBJECTIVE_COLORS, BUILDING_COLORS, UNKNOWN_KIND_COLOR } from "./primitive-style.js";

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

// The room roles' tokens, so a spawn and a wool room read in the colours every surface draws them in.
const ROLE_FILL = { spawn: "var(--canvas-role-spawn)", woolRoom: "var(--canvas-role-wool-room)" };

// Every other play piece, in the colour the plan draws the same thing in. A goal, a cube and the observer are
// small and solid; an area and a wall are ground a player crosses, so they are washed in rather than filled.
const PIECE_FILL = {
  destroyable: { color: OBJECTIVE_COLORS.destroyable, alpha: 0.85 },
  core:        { color: OBJECTIVE_COLORS.core, alpha: 0.85 },
  ironCube:    { color: OBJECTIVE_COLORS.iron, alpha: 0.85 },
  observer:    { color: OBJECTIVE_COLORS.spawn, alpha: 0.85 },
  spawnPoint:  { color: OBJECTIVE_COLORS.spawn, alpha: 0.95 },
  woolSpawn:   { color: OBJECTIVE_COLORS.wool, alpha: 0.95 },
  buildArea:   { color: "var(--accent)", alpha: 0.12, dash: [6, 4] },
  waterLane:   { color: "var(--canvas-zone-water-lane)", alpha: 0.3 },
  wall:        { color: "var(--canvas-seam-wall)", alpha: 0.85 },
  chest:       { color: OBJECTIVE_COLORS.wool, alpha: 0.85 },
};

/**
 * The play pieces the intent places, drawn over the board: a spawn and a wool room as a filled box in its role
 * colour (purple spawn / green wool) with a solid border, and every other piece in its kind's colour. They are not
 * terrain (the rasterizer skips them; the ground under them is the fused group). No piece carries a name on the
 * board: the colour says what it is, and the picked one is named above its box (`renderSelectionMarks`).
 * `selectedId` is the one a click has picked, ringed so an author can see which piece the rail is talking about.
 *
 * A `building` shape is the footprint raised inside one of those regions, and is drawn as an outline over it
 * rather than a second filled box.
 */
export function paintStructural(painter, shapes, selectedId = null) {
  for (const s of shapes ?? []) {
    if (s.type !== "rectangle") continue;
    const colour = ROLE_FILL[s.role] ?? UNKNOWN_KIND_COLOR;
    const box = { min_x: s.min_x, min_z: s.min_z, max_x: s.max_x, max_z: s.max_z };
    // The ring goes outside whatever the piece draws for itself, in the selection ink every other picked
    // thing on this canvas wears, so a picked region and a picked island read as the same kind of state.
    // A wall is the band its line is laid in, at whatever angle the line runs.
    const band = s.line ? wallBand(s.line) : null;
    if (s.id === selectedId) {
      if (band) painter.ring(band, { stroke: "var(--accent)", strokeAlpha: 1, width: 4 });
      else painter.rect(box, { stroke: "var(--accent)", strokeAlpha: 1, width: 4 });
    }
    // A piece that is not a room is drawn in its kind's colour and carries no label: what it is shows by its
    // colour and its shape. One that moves only with something else is dashed and fainter.
    if (s.piece) {
      const look = PIECE_FILL[s.role] ?? { color: UNKNOWN_KIND_COLOR, alpha: 0.5 };
      const dash = s.carried ? [3, 3] : look.dash;
      const style = {
        fill: look.color, fillAlpha: s.carried ? look.alpha / 2 : look.alpha,
        stroke: look.color, strokeAlpha: 0.95, width: 2, ...(dash ? { dash } : {}),
      };
      if (band) painter.ring(band, style);
      else painter.rect(box, style);
      continue;
    }
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
      { fill: colour, fillAlpha: 0.32, stroke: colour, strokeAlpha: 0.95, width: 2 });
  }
}

/**
 * The map's places: a name over a point, a box, a ring or a line, drawn over the board in the one ink a place
 * takes and labelled at its first point. They make no ground, so none is filled more than a tint; the picked one
 * is drawn in the selection ink.
 */
export function paintPlaces(painter, places, selectedId = null) {
  for (const place of places ?? []) {
    const points = place.points ?? [];
    if (points.length === 0) continue;
    const picked = place.id === selectedId;
    const ink = picked ? "var(--accent)" : "var(--canvas-place)";
    const style = { fill: ink, fillAlpha: 0.12, stroke: ink, strokeAlpha: 0.95, width: picked ? 3 : 2, dash: picked ? null : [6, 4] };
    if (place.shape === "point") painter.dot(points[0][0], points[0][1], { fill: ink, radiusPx: picked ? 6 : 5 });
    else if (place.shape === "box" && points.length === 2)
      painter.rect({ min_x: Math.min(points[0][0], points[1][0]), min_z: Math.min(points[0][1], points[1][1]),
                     max_x: Math.max(points[0][0], points[1][0]), max_z: Math.max(points[0][1], points[1][1]) }, style);
    else if (place.shape === "ring") painter.ring(points, style);
    else if (place.shape === "line")
      for (let i = 1; i < points.length; i++)
        painter.line(points[i - 1][0], points[i - 1][1], points[i][0], points[i][1], { stroke: ink, strokeAlpha: 0.95, width: picked ? 4 : 3 });
    if (place.name)
      painter.text(place.name, points[0][0], points[0][1],
        { fill: "var(--canvas-ink)", halo: "var(--bg-canvas)", haloWidth: 3, size: 4, weight: 600, dy: -8 });
  }
}

/** The modifiers' masks: every outline thin, the picked modifier's bold with its images under the symmetry
 *  dashed, and a switched-off modifier's faint. A line is stroked open with its band beside it. */
export function paintMasks(painter, masks) {
  for (const mask of masks ?? []) {
    const strong = mask.picked && !mask.image;
    const strokeAlpha = mask.off ? 0.3 : strong ? 0.95 : 0.6;
    const dash = mask.image || mask.off ? [5, 4] : null;
    if (mask.line) {
      painter.polyline(mask.points, { stroke: "var(--accent)", strokeAlpha: strong ? 0.18 : mask.picked ? 0.08 : 0.05,
                                      width: (mask.width ?? 0) / painter.screenPx(1), cap: "round", join: "round" });
      painter.polyline(mask.points, { stroke: "var(--accent)", strokeAlpha, width: strong ? 2 : 1.5, dash, cap: "round", join: "round" });
      continue;
    }
    painter.ring(mask.points, {
      fill: "var(--accent)", fillAlpha: strong ? 0.1 : mask.picked ? 0.04 : 0.02, stroke: "var(--accent)",
      strokeAlpha, width: strong ? 2 : 1.5, dash,
    });
  }
}

/** The place under (wx, wz), the last drawn first — inside a box or a ring, within `reach` blocks of a point or
 *  a line — or null. */
export function placeAt(places, wx, wz, reach) {
  const near = (ax, az, bx, bz) => {
    const dx = bx - ax, dz = bz - az, length = dx * dx + dz * dz;
    const t = length === 0 ? 0 : Math.max(0, Math.min(1, ((wx - ax) * dx + (wz - az) * dz) / length));
    return Math.hypot(wx - (ax + t * dx), wz - (az + t * dz)) <= reach;
  };
  for (let i = (places ?? []).length - 1; i >= 0; i--) {
    const place = places[i], points = place.points ?? [];
    if (place.shape === "point" && points.length && Math.hypot(wx - points[0][0], wz - points[0][1]) <= reach) return place;
    if (place.shape === "box" && points.length === 2) {
      const [a, b] = points;
      if (wx >= Math.min(a[0], b[0]) && wx <= Math.max(a[0], b[0]) && wz >= Math.min(a[1], b[1]) && wz <= Math.max(a[1], b[1])) return place;
    }
    if (place.shape === "ring" && points.length >= 3) {
      let inside = false;
      for (let j = 0, k = points.length - 1; j < points.length; k = j++) {
        const [xj, zj] = points[j], [xk, zk] = points[k];
        if ((zj > wz) !== (zk > wz) && wx < (xk - xj) * (wz - zj) / (zk - zj) + xj) inside = !inside;
      }
      if (inside) return place;
    }
    if (place.shape === "line")
      for (let j = 1; j < points.length; j++)
        if (near(points[j - 1][0], points[j - 1][1], points[j][0], points[j][1])) return place;
  }
  return null;
}

/** The four corners of the band a wall from (x1, z1) to (x2, z2) stands in: two blocks across, square at the ends. */
export function wallBand([x1, z1, x2, z2]) {
  const length = Math.hypot(x2 - x1, z2 - z1) || 1;
  const nx = -(z2 - z1) / length, nz = (x2 - x1) / length;
  return [[x1 + nx, z1 + nz], [x2 + nx, z2 + nz], [x2 - nx, z2 - nz], [x1 - nx, z1 - nz]];
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
 * The rasterized block footprint — the exact cells the shapes voxelize into, as merged horizontal
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
  if (!bbox || !mode || mode === "none") return;
  const { min_x, max_x, min_z, max_z } = bbox;
  const cx = center?.cx ?? 0, cz = center?.cz ?? 0;
  const style = { stroke: "var(--canvas-axis)", width: 1, dash: [6, 4], alpha: 0.75 };
  const runs = [];
  if (mode !== "mirror_z") runs.push({ x1: cx, z1: min_z, x2: cx, z2: max_z });
  if (mode !== "mirror_x") runs.push({ x1: min_x, z1: cz, x2: max_x, z2: cz });
  painter.segments(runs, style);
}
