/**
 * The sketch's modifier stack: the terraform operations run over the built ground in list order, each inside its
 * mask. Pure state and math, NO DOM. The list is exactly `SketchLayout.Modifiers`, the array the rasterizer runs,
 * so a modifier added here and one written by hand are the same modifier.
 */
import { applySymmetry, orbitAxes } from "../geometry/symmetry.js";
import { distToSegment } from "../geometry/shape.js";

/** The width a traced line starts with, in blocks: the band either side of it is half this. An operation that
 *  states its own `width` below starts there instead. */
export const LINE_WIDTH = 6;

/**
 * The operations, with what the screen calls each and what the board traces for it: a `ring` outlines an area,
 * a `line` is an open run with a width. The words mirror `PgmStudio.Vocabulary.ModifierOps`.
 */
export const MODIFIER_OPS = [
  { op: "mound", label: "Hill", trace: "ring" },
  { op: "crater", label: "Hollow", trace: "ring" },
  { op: "level", label: "Level", trace: "ring" },
  { op: "butte", label: "Butte", trace: "ring" },
  { op: "spire", label: "Spire", trace: "ring" },
  { op: "ridge", label: "Ridge", trace: "line" },
  { op: "scarp", label: "Scarp", trace: "line" },
  { op: "canyon", label: "Canyon", trace: "line" },
  { op: "profile", label: "Profile", trace: "line", width: 32 },
  { op: "road", label: "Road", trace: "line" },
  { op: "river", label: "River", trace: "line" },
  { op: "lake", label: "Lake", trace: "ring" },
  { op: "coast", label: "Coast", trace: "ring" },
  { op: "noise", label: "Roughness", trace: "ring" },
  { op: "lumps", label: "Lumps", trace: "ring" },
  { op: "shatter", label: "Shatter", trace: "ring" },
  { op: "gullies", label: "Gullies", trace: "ring" },
  { op: "warp", label: "Warp", trace: "ring" },
];

/** What a fresh modifier of each operation states besides its mask. An absolute height left unstated (a
 *  butte's or spire's `top`, a lake's `level`) is the server's reading of the ground under the mask. */
const FRESH = {
  mound: { rise: 8, power: 1.6, soft: 0.35 },
  crater: { floor: 4, flat: 2, slope: 1, melt: 3 },
  level: { top: 12, inner: 1, outer: 1.5 },
  noise: { sizes: [{ cell: 12, octaves: 2, rise: 1 }] },
  ridge: { rise: 8, wander: 2 },
  butte: { cliff: 1.5, talus: 6, apron: 0.3, jag: 0.12 },
  spire: { taper: 1.6, jag: 0.15 },
  scarp: { height: 6, side: 1, cliff: 2, talus: 5 },
  canyon: { depth: 12, bed: 0.35, wall: 2.5, ledges: 0 },
  river: { depth: 2, water: 1, bank: 4 },
  lake: { depth: 3, shore: 3 },
  road: { grade: 0.25, shoulder: 4 },
  gullies: { count: 8, depth: 3, wander: 0.9, sharpness: 3 },
  warp: { amount: 6, cell: 24 },
  profile: { steps: [{ start: 4, end: 7, rise: 6 }, { start: 10, end: 13, rise: 12 }], jag: 1.5 },
  coast: { shelf: 10, depth: 6, slope: 0.35 },
  lumps: { count: 12, across: [6, 16], heights: [2, 5], jag: 0.25 },
  shatter: { cell: 10, amount: 3, tilt: 0.25, gap: 1, depth: 4, wander: 3 },
};

/** What the screen calls an operation. */
export function opLabel(op) {
  return MODIFIER_OPS.find(entry => entry.op === op)?.label ?? op;
}

/** Whether an operation is traced as an open line rather than an outline. */
export const isLineOp = (op) => MODIFIER_OPS.find(entry => entry.op === op)?.trace === "line";

/** An id for a new modifier of `op`, unique among `modifiers`: the operation's name, numbered past the first. */
export function freshModifierId(modifiers, op) {
  const taken = new Set((modifiers ?? []).map(modifier => modifier.id));
  const stem = opLabel(op).toLowerCase();
  if (!taken.has(stem)) return stem;
  for (let n = 2; ; n++) if (!taken.has(`${stem}-${n}`)) return `${stem}-${n}`;
}

/** The mask area an operation makes of a trace: a line for a line operation, else a polygon. */
export function traceArea(op, points) {
  const copy = points.map(([x, z]) => [x, z]);
  const width = MODIFIER_OPS.find(entry => entry.op === op)?.width ?? LINE_WIDTH;
  return isLineOp(op) ? { kind: "line", points: copy, width } : { kind: "polygon", points: copy };
}

/** A new modifier of `op` whose mask is the traced `points`. */
export function newModifier(modifiers, op, points) {
  return {
    id: freshModifierId(modifiers, op),
    op,
    mask: { areas: [traceArea(op, points)], mirror: true },
    ...structuredClone(FRESH[op] ?? {}),
    seed: 1 + Math.floor(Math.random() * 98),
  };
}

/** `modifier` with one more area in its mask, traced the way its operation traces. */
export function addArea(modifier, points) {
  const areas = [...(modifier.mask?.areas ?? []), traceArea(modifier.op, points)];
  return { ...modifier, mask: { ...modifier.mask, areas } };
}

/** `modifiers` with the one named `id` patched; `mask` is replaced whole, and a number set to null is dropped. */
export function patchModifier(modifiers, id, patch) {
  return modifiers.map(modifier => {
    if (modifier.id !== id) return modifier;
    const next = { ...modifier, ...patch };
    for (const [key, value] of Object.entries(patch)) if (value === null) delete next[key];
    if (next.off === false) delete next.off;
    return next;
  });
}

/** `modifiers` with the one named `id` moved `by` places, held inside the list. */
export function moveModifier(modifiers, id, by) {
  const from = modifiers.findIndex(modifier => modifier.id === id);
  if (from < 0) return modifiers;
  const to = Math.max(0, Math.min(modifiers.length - 1, from + by));
  if (to === from) return modifiers;
  const next = [...modifiers];
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  return next;
}

const ELLIPSE_POINTS = 48;

/** Whether an area is an open line with a width. */
export const isLineArea = (area) => area?.kind === "line";

/** One area's points as world points — an outline's ring, a line's run — or null for the whole board. */
export function areaPoints(area) {
  if (area?.kind === "polygon") return (area.points ?? []).length >= 3 ? area.points : null;
  if (area?.kind === "line") return (area.points ?? []).length >= 2 ? area.points : null;
  if (area?.kind !== "ellipse" || !area.at) return null;
  const turn = (area.angle ?? 0) * Math.PI / 180;
  const [cx, cz] = area.at, rx = area.rx ?? 0, rz = area.rz ?? 0;
  return Array.from({ length: ELLIPSE_POINTS }, (_, k) => {
    const t = k / ELLIPSE_POINTS * Math.PI * 2, u = Math.cos(t) * rx, v = Math.sin(t) * rz;
    return [cx + u * Math.cos(turn) - v * Math.sin(turn), cz + u * Math.sin(turn) + v * Math.cos(turn)];
  });
}

/** How far (x, z) is from the run `points`, in blocks. */
export function distanceToLine(points, x, z) {
  let nearest = Infinity;
  for (let i = 1; i < points.length; i++)
    nearest = Math.min(nearest, distToSegment(x, z, points[i - 1][0], points[i - 1][1], points[i][0], points[i][1]));
  return nearest;
}

/** `modifier` with area `index` given `points`: a line stays a line of its width, and an ellipse reshaped
 *  becomes the polygon. */
export function withAreaPoints(modifier, index, points) {
  const areas = (modifier.mask?.areas ?? []).map((area, at) =>
    at !== index ? area : isLineArea(area) ? { ...area, points } : { kind: "polygon", points });
  return { ...modifier, mask: { ...modifier.mask, areas } };
}

/** `modifier` with its whole mask moved by `(dx, dz)`. */
export function translateModifier(modifier, dx, dz) {
  const areas = (modifier.mask?.areas ?? []).map(area =>
    area.kind === "polygon" || area.kind === "line" ? { ...area, points: area.points.map(([x, z]) => [x + dx, z + dz]) }
    : area.kind === "ellipse" && area.at ? { ...area, at: [area.at[0] + dx, area.at[1] + dz] }
    : area);
  return { ...modifier, mask: { ...modifier.mask, areas } };
}

/** Every area a modifier works in — its own and, where its mask mirrors, their images under the map's
 *  symmetry — as `{ points, image, line, width }`. */
export function maskRings(modifier, mirrorMode, cx = 0, cz = 0) {
  const rings = [];
  for (const area of modifier?.mask?.areas ?? []) {
    const points = areaPoints(area);
    if (!points) continue;
    const line = isLineArea(area), width = line ? area.width ?? LINE_WIDTH : 0;
    rings.push({ points, image: false, line, width });
    if (modifier.mask.mirror === false) continue;
    for (const axis of orbitAxes(mirrorMode))
      rings.push({ points: points.map(([x, z]) => applySymmetry(x, z, axis, cx, cz)), image: true, line, width });
  }
  return rings;
}
