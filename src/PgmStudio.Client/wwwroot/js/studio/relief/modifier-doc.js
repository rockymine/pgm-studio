/**
 * The sketch's modifier stack: the terraform operations run over the built ground in list order, each inside its
 * mask. Pure state and math, NO DOM. The list is exactly `SketchLayout.Modifiers`, the array the rasterizer runs,
 * so a modifier added here and one written by hand are the same modifier.
 */
import { applySymmetry, orbitAxes } from "../geometry/symmetry.js";

/** The operations, with what the screen calls each. */
export const MODIFIER_OPS = [
  { op: "mound", label: "Hill" },
  { op: "crater", label: "Hollow" },
  { op: "level", label: "Level" },
  { op: "noise", label: "Roughness" },
];

/** What a fresh modifier of each operation states besides its mask. */
const FRESH = {
  mound: { rise: 8, power: 1.6, soft: 0.35 },
  crater: { floor: 4, flat: 2, slope: 1, melt: 3 },
  level: { top: 12, inner: 1, outer: 1.5 },
  noise: { sizes: [{ cell: 12, octaves: 2, rise: 1 }] },
};

/** What the screen calls an operation. */
export function opLabel(op) {
  return MODIFIER_OPS.find(entry => entry.op === op)?.label ?? op;
}

/** An id for a new modifier of `op`, unique among `modifiers`: the operation's name, numbered past the first. */
export function freshModifierId(modifiers, op) {
  const taken = new Set((modifiers ?? []).map(modifier => modifier.id));
  const stem = opLabel(op).toLowerCase();
  if (!taken.has(stem)) return stem;
  for (let n = 2; ; n++) if (!taken.has(`${stem}-${n}`)) return `${stem}-${n}`;
}

/** A new modifier of `op`, its mask an ellipse at `at` — or the whole board for a Roughness. */
export function newModifier(modifiers, op, at, radius = 12) {
  const centre = [Math.round(at?.[0] ?? 0), Math.round(at?.[1] ?? 0)];
  const area = op === "noise" ? { kind: "board" } : { kind: "ellipse", at: centre, rx: radius, rz: radius };
  return {
    id: freshModifierId(modifiers, op),
    op,
    mask: { areas: [area], mirror: true },
    ...structuredClone(FRESH[op] ?? {}),
    seed: 1 + Math.floor(Math.random() * 98),
  };
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

/** One area's outline as world points, or null for the whole board. */
export function areaRing(area) {
  if (area?.kind === "polygon") return (area.points ?? []).length >= 3 ? area.points : null;
  if (area?.kind !== "ellipse" || !area.at) return null;
  const turn = (area.angle ?? 0) * Math.PI / 180;
  const [cx, cz] = area.at, rx = area.rx ?? 0, rz = area.rz ?? 0;
  return Array.from({ length: ELLIPSE_POINTS }, (_, k) => {
    const t = k / ELLIPSE_POINTS * Math.PI * 2, u = Math.cos(t) * rx, v = Math.sin(t) * rz;
    return [cx + u * Math.cos(turn) - v * Math.sin(turn), cz + u * Math.sin(turn) + v * Math.cos(turn)];
  });
}

/** Every outline a modifier works in — its own and, where its mask mirrors, their images under the map's
 *  symmetry — as `{ points, image }`. */
export function maskRings(modifier, mirrorMode, cx = 0, cz = 0) {
  const rings = [];
  for (const area of modifier?.mask?.areas ?? []) {
    const points = areaRing(area);
    if (!points) continue;
    rings.push({ points, image: false });
    if (modifier.mask.mirror === false) continue;
    for (const axis of orbitAxes(mirrorMode))
      rings.push({ points: points.map(([x, z]) => applySymmetry(x, z, axis, cx, cz)), image: true });
  }
  return rings;
}
