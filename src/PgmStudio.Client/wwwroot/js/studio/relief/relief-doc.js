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

/**
 * A push is placed exactly the way a bench is — a ring traced on the ground — so it travels the whole placed-
 * thing pipeline with the marks: one id, one selection, one set of grips, one body drag, one list row. What
 * separates it is not how it is drawn but what it *does*, and the difference is worth being plain about,
 * because it is the reason both exist.
 *
 * A mark is a **constraint**: the ground here IS twelve. Two marks over the same ground have to argue, and the
 * solver settles it by order. A push is applied to the solved surface **afterwards** and is a **relative**
 * lift, so two over the same ground simply add — which is what makes a spur on the flank of a hill one
 * operation rather than a re-statement of the hill.
 *
 * It lives in the relief's own `pushes` array rather than among the marks, and it carries no `kind` on the
 * wire: the array it is in already says what it is, and a stored field that only repeated that would be one
 * more thing able to disagree. The word is added back when the document hands a push out, so everything
 * downstream can ask a statement what it is without first asking where it came from.
 */
export const PUSH_KIND = "push";

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
    case PUSH_KIND: return defaultPush();
    default: throw new Error(`Unknown mark kind: ${kind}`);
  }
}

/**
 * A fresh push. The numbers mirror the C# record defaults, with one exception that is a UI decision rather
 * than a model one: `crown` ships at **2** here where the record's default is 0.
 *
 * A crown of zero gives a drawn ring a flat top, and a flat top is the least natural of the three settings —
 * it is a plateau, and a plateau is the thing an author reaches for a push to stop making. Two is a gentle
 * dome on a round ring and a gentle ridge on a long one, from the same number, because the middle is measured
 * inward and a shape's inward middle is a point when it is round and a line when it is long.
 */
export function defaultPush() {
  return { kind: PUSH_KIND, id: "", ring: [], amount: 5, falloff: 10, roughness: 0.3, crown: 2, seed: 1 };
}

/** Whether a kind is placed at a point rather than traced. Only a spot height is: a summit is a decision about
 *  a place, and a place is a click. */
export const isSpot = (markOrKind) =>
  (typeof markOrKind === "string" ? markOrKind : markOrKind?.kind) === "point";

/** Whether a kind's trace closes on itself. A bench and a push enclose ground; a line and a scarp run through
 *  it, and closing either would turn a ridgeline into a loop and a scarp into a moat. */
export const isRing = (markOrKind) => {
  const kind = typeof markOrKind === "string" ? markOrKind : markOrKind?.kind;
  return kind === "area" || kind === PUSH_KIND;
};

/** Whether a statement is a push — a relative lift applied after the solve — rather than a mark, which pins
 *  the ground to a stated height. Everything about placing and editing the two is shared; what they do to the
 *  surface is not, and every caller that cares asks here. */
export const isPush = (markOrKind) =>
  (typeof markOrKind === "string" ? markOrKind : markOrKind?.kind) === PUSH_KIND;

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
  const band = mark?.kind === "line" ? (mark.width ?? 1.5)
             : mark?.kind === "scarp" ? (mark.band ?? 5)
             // A push's skirt is ground it moves, so a click on the slope reaches the push that made it.
             : isPush(mark) ? (mark.falloff ?? 10)
             : 0;
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
 * A push's lift at each of its ring's vertices — its `amounts` if it states them, else its single `amount`
 * repeated. What makes a drawn ridge fall along its length instead of holding level, and the reason the
 * expanded form is what an editor shows: an author who wants one end lower needs a number to change, and
 * "the amount, except at that corner" is not a number.
 */
export function pushAmounts(push) {
  const ring = markPoints(push);
  const stated = push?.amounts;
  if (Array.isArray(stated) && stated.length)
    return ring.map((_, i) => Number(stated[Math.min(i, stated.length - 1)]) || 0);
  return ring.map(() => Number(push?.amount ?? 0) || 0);
}

/**
 * The patch that states one vertex's lift. Writing a per-vertex array collapses back to the single `amount`
 * when every vertex agrees — so a push edited to a uniform lift and one that never had per-vertex numbers are
 * the same document, and an author who undoes a variation is left with what they started from rather than
 * with an array that happens to be flat.
 */
export function pushAmountPatch(push, index, value) {
  const amounts = pushAmounts(push);
  if (index < 0 || index >= amounts.length) return {};
  amounts[index] = Number(value) || 0;
  const uniform = amounts.every(amount => amount === amounts[0]);
  return uniform ? { amount: amounts[0], amounts: undefined } : { amount: amounts[0], amounts };
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

  /** Every push, each tagged with its island and with the word for what it is. The `kind` is added here
   *  rather than stored, because the array it sits in already says it. */
  get pushes() {
    return [...this.#byIsland.entries()].flatMap(([islandId, relief]) =>
      relief.pushes.map(push => ({ ...push, kind: PUSH_KIND, islandId })));
  }

  /** Everything an author placed, marks and pushes together, in the one list the canvas selects from and the
   *  sidebar lists. They differ in what they do to the surface, not in how they are picked up. */
  get statements() { return [...this.marks, ...this.pushes]; }

  byId(id) { return this.statements.find(entry => entry.id === id) ?? null; }

  /** Which island states an entry, and which of its two arrays holds it — the question every edit has to
   *  answer first, since a statement lives inside its island rather than in one flat list. */
  #locate(id) {
    for (const [islandId, relief] of this.#byIsland) {
      if (relief.marks.some(mark => mark.id === id)) return { islandId, list: relief.marks, kind: null };
      if (relief.pushes.some(push => push.id === id)) return { islandId, list: relief.pushes, kind: PUSH_KIND };
    }
    return null;
  }

  islandOf(id) { return this.#locate(id)?.islandId ?? null; }

  add(islandId, entry) {
    const placed = { ...entry, id: entry.id || this.#mintId() };
    if (isPush(entry)) {
      // Stored without the word: the pushes array is what says it is a push, and a field repeating that
      // would be one more thing able to disagree with it.
      const { kind, ...stored } = placed;
      this.reliefOf(islandId).pushes.push(stored);
      return { ...stored, kind: PUSH_KIND, islandId };
    }
    this.reliefOf(islandId).marks.push(placed);
    return { ...placed, islandId };
  }

  update(id, patch) {
    const found = this.#locate(id);
    if (!found) return null;
    if (found.kind === PUSH_KIND) {
      const at = found.list.findIndex(push => push.id === id);
      // `undefined` in a patch means "state this no longer" — how a per-vertex lift collapses back to one
      // number — so it deletes the key rather than writing an undefined into the document.
      const merged = { ...found.list[at], ...patch, id };
      for (const [key, value] of Object.entries(patch)) if (value === undefined) delete merged[key];
      delete merged.kind;
      found.list[at] = merged;
      return { ...merged, kind: PUSH_KIND, islandId: found.islandId };
    }
    const islandId = found.islandId;
    const marks = this.#byIsland.get(islandId).marks;
    const at = marks.findIndex(mark => mark.id === id);
    marks[at] = { ...marks[at], ...patch, id, kind: marks[at].kind };
    return { ...marks[at], islandId };
  }

  remove(id) {
    const found = this.#locate(id);
    if (!found) return;
    const relief = this.#byIsland.get(found.islandId);
    if (found.kind === PUSH_KIND) relief.pushes = relief.pushes.filter(push => push.id !== id);
    else relief.marks = relief.marks.filter(mark => mark.id !== id);
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
