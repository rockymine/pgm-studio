/**
 * The relief a map carries: what an author has stated about the ground inside each island, and what a fresh
 * statement of each kind starts as. Pure state and math, NO DOM.
 *
 * The shape differs from the dressing document in one way that decides everything else about this file: a prop
 * is placed *on the map* and a mark is placed *in an island*. A relief is solved over one island's fused
 * footprint, so the document is keyed by island id — `{ "i3": { base, reach, step, grain, marks, pushes } }` —
 * and it is exactly `SketchLayout.Relief`, the same object the rasterizer reads and the solver takes. There is
 * no second model of a mark.
 *
 * Marks carry an id for the same reason props do: a placed thing has to survive being selected, moved and
 * edited among its neighbours. The id rides on the wire rather than being minted per session, because a relief
 * that renumbered itself on every load would move the selection under the author's hands.
 */

/** The mark kinds that are *placed*, in the order their tools sit on the dock. */
export const MARK_KINDS = ["point", "line", "area", "scarp"];

/** A rim is a mark, but it is not placed: it holds the island's whole outline, so there is nowhere to put it
 *  and nothing to drag. It rides as a property of the island's relief instead — one height and a depth. */
export const RIM_KIND = "rim";

/** What a relief starts as before an author has stated anything. Base 8 is a working ground level rather than
 *  a claim; a reach of 0 means the marks decide the whole surface, which is what a single island wants. */
export function defaultRelief() {
  return { base: 8, reach: 0, step: 1, grain: { amplitude: 1.2, scale: 12, seed: 1 }, marks: [], pushes: [] };
}

/**
 * A fresh mark of each kind, before the author has touched a knob. Every number mirrors the C# record default,
 * so a mark drawn on the canvas and one deserialized from a hand-written relief are the same mark.
 *
 * `h` is the one field a placement has to decide rather than inherit, because a mark with no height states
 * nothing. It arrives from the tool's settings, seeded from the island's own base — a first mark placed on
 * flat ground reads as flat ground, which is honest, rather than jumping to a height nobody asked for.
 */
export function defaultMark(kind, height = 8) {
  const base = { kind, id: "" };
  switch (kind) {
    case "point": return { ...base, at: [0, 0], h: height, r: 4 };
    case "line":  return { ...base, points: [], h: height, width: 3 };
    case "area":  return { ...base, ring: [], h: height };
    // A scarp is the one mark stating two heights: the shelf above its line and the ground below it. The
    // defaults put a 6-block drop over a 2-block face, which is a barrier a player builds over rather than
    // walks, and a 5-block band either side for the land to arrive through.
    case "scarp": return { ...base, points: [], high: height + 3, low: Math.max(0, height - 3), face: 2, band: 5 };
    case "rim":   return { ...base, h: height, depth: 1 };
    default: throw new Error(`Unknown mark kind: ${kind}`);
  }
}

/** Whether a kind is placed at a point rather than traced. Only a spot height is: a summit is a decision about
 *  a place, and a place is a click. */
export const isSpot = (markOrKind) =>
  (typeof markOrKind === "string" ? markOrKind : markOrKind?.kind) === "point";

/** Whether a kind's trace closes on itself. An area encloses ground; a line and a scarp run through it, and
 *  closing either would turn a ridgeline into a loop and a scarp into a moat. */
export const isRing = (markOrKind) =>
  (typeof markOrKind === "string" ? markOrKind : markOrKind?.kind) === "area";

/** A mark's traced points, whatever field its kind keeps them in — `ring` for an area, `points` for the rest.
 *  Two names because the wire format says what a thing IS: an area's points close, a line's do not. */
export function markPoints(mark) {
  if (isSpot(mark)) return mark?.at ? [mark.at] : [];
  return (isRing(mark) ? mark?.ring : mark?.points) ?? [];
}

/** The patch that writes points back to whichever field this kind keeps them in. */
export function pointsPatch(mark, points) {
  if (isSpot(mark)) return { at: points[0] ?? [0, 0] };
  return isRing(mark) ? { ring: points } : { points };
}

/** A mark's position as one point: a spot's own cell, else the middle of what it covers. What a label is
 *  anchored to and what a click is measured against. */
export function markAnchor(mark) {
  const points = markPoints(mark);
  if (!points.length) return [0, 0];
  const xs = points.map(([x]) => x), zs = points.map(([, z]) => z);
  return [(Math.min(...xs) + Math.max(...xs)) / 2, (Math.min(...zs) + Math.max(...zs)) / 2];
}

/** How far a mark reaches from its anchor, in blocks — what a hit test measures against. A spot reaches its
 *  own radius; a traced mark reaches its farthest point plus whatever band it holds. */
export function markReach(mark) {
  if (isSpot(mark)) return Math.max(2, mark?.r ?? 4);
  const points = markPoints(mark);
  if (!points.length) return 0;
  const [ax, az] = markAnchor(mark);
  const band = mark?.kind === "line" ? (mark.width ?? 1.5) : mark?.kind === "scarp" ? (mark.band ?? 5) : 0;
  return Math.max(...points.map(([x, z]) => Math.hypot(x - ax, z - az))) + band;
}

/** Move a mark by (dx, dz), returning a new mark. Points move one by one, so a drag keeps the shape. */
export function translateMark(mark, dx, dz) {
  const moved = markPoints(mark).map(([x, z]) => [Math.round(x + dx), Math.round(z + dz)]);
  return { ...mark, ...pointsPatch(mark, moved) };
}

/** The heights a mark states, as an array — a line may carry one per vertex, everything else carries one.
 *  Reading them through one function is what lets an inspector show "the heights" without asking the kind. */
export function markHeights(mark) {
  if (mark?.kind === "scarp") return [mark.high ?? 0, mark.low ?? 0];
  const stated = mark?.h;
  return Array.isArray(stated) ? stated : [stated ?? 0];
}

/**
 * The relief document: one relief per island, and the marks inside each. Mutating methods return the affected
 * mark so a caller can select what it just made; the caller owns when to persist.
 *
 * Ids are unique across the WHOLE document rather than per island, because a mark can be dragged out of one
 * island and into another and an id that changed on the way would break the selection mid-drag.
 */
export class ReliefDoc {
  #byIsland = new Map();
  #nextId = 1;

  /** Read a stored document. A mark whose kind the client cannot draw is dropped rather than carried as a
   *  shape nothing can edit — the same rule the dressing document follows, for the same reason. */
  static from(stored) {
    const doc = new ReliefDoc();
    for (const [islandId, relief] of Object.entries(stored ?? {})) {
      if (!islandId || !relief || typeof relief !== "object") continue;
      const kept = { ...defaultRelief(), ...relief };
      kept.marks = (relief.marks ?? []).filter(mark => MARK_KINDS.includes(mark?.kind) || mark?.kind === RIM_KIND)
                                       .map(mark => ({ ...mark, id: mark.id || doc.#mintId() }));
      kept.pushes = (relief.pushes ?? []).map(push => ({ ...push, id: push.id || doc.#mintId() }));
      doc.#byIsland.set(islandId, kept);
    }
    doc.#nextId = Math.max(doc.#nextId, ...doc.#allIds().map(id => (parseInt(String(id).replace(/\D/g, ""), 10) || 0) + 1));
    return doc;
  }

  /** The stored form — exactly what `SketchLayout.Relief` deserializes. An island whose relief states nothing
   *  is left out entirely, so opening the phase and closing it again cannot add a key to the layout. */
  toJSON() {
    const out = {};
    for (const [islandId, relief] of this.#byIsland)
      if (relief.marks.length || relief.pushes.length) out[islandId] = relief;
    return out;
  }

  get isEmpty() { return Object.keys(this.toJSON()).length === 0; }

  /** Every island that states something, with its relief. */
  get islands() { return [...this.#byIsland.entries()].map(([id, relief]) => ({ id, relief })); }

  /** One island's relief, created on demand — asking for it is how a first mark gets somewhere to live. */
  reliefOf(islandId) {
    if (!this.#byIsland.has(islandId)) this.#byIsland.set(islandId, defaultRelief());
    return this.#byIsland.get(islandId);
  }

  /** One island's relief if it has one, without creating it — what a reader asks. */
  peek(islandId) { return this.#byIsland.get(islandId) ?? null; }

  /** Every mark in the document, each tagged with the island stating it. */
  get marks() {
    return [...this.#byIsland.entries()].flatMap(([islandId, relief]) =>
      relief.marks.map(mark => ({ ...mark, islandId })));
  }

  byId(id) { return this.marks.find(mark => mark.id === id) ?? null; }

  /** Which island states a mark — the question every edit has to answer first, since the marks live inside
   *  their island rather than in one flat list. */
  islandOf(id) {
    for (const [islandId, relief] of this.#byIsland)
      if (relief.marks.some(mark => mark.id === id)) return islandId;
    return null;
  }

  add(islandId, mark) {
    const placed = { ...mark, id: mark.id || this.#mintId() };
    this.reliefOf(islandId).marks.push(placed);
    return { ...placed, islandId };
  }

  update(id, patch) {
    const islandId = this.islandOf(id);
    if (!islandId) return null;
    const marks = this.#byIsland.get(islandId).marks;
    const at = marks.findIndex(mark => mark.id === id);
    marks[at] = { ...marks[at], ...patch, id, kind: marks[at].kind };
    return { ...marks[at], islandId };
  }

  remove(id) {
    const islandId = this.islandOf(id);
    if (!islandId) return;
    const relief = this.#byIsland.get(islandId);
    relief.marks = relief.marks.filter(mark => mark.id !== id);
  }

  /** Patch an island's own settings — base, reach, step, grain. Not a mark: these are what the marks are
   *  stated against. */
  updateRelief(islandId, patch) {
    const relief = this.reliefOf(islandId);
    Object.assign(relief, patch);
    return relief;
  }

  #allIds() {
    return [...this.#byIsland.values()].flatMap(relief =>
      [...relief.marks, ...relief.pushes].map(entry => entry.id));
  }

  #mintId() { return `r${this.#nextId++}`; }
}
