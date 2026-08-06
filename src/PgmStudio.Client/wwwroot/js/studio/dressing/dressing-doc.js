/**
 * The dressing a map carries: the list of things an author placed, and what a fresh one of each starts as.
 * Pure state and math, NO DOM.
 *
 * This is deliberately not a registry. A theme is a recipe named once and applied to many footprints, so it is
 * stored by name and referenced; a prop was *put somewhere*, so what is stored is the placement. The document
 * below is therefore a flat list of placed things — the wire format `PgmStudio.Minecraft.Dressing.DressingDoc`
 * deserializes, with the same `kind` discriminator, so there is no second model of a prop.
 */

/** The things that can be placed, in the order their tools sit on the toolbar. */
export const PROP_KINDS = ["path", "water", "flora", "tree", "boulder"];

/** A fresh prop of each kind, before the author has touched a knob. The numbers mirror the C# record defaults,
 *  so a prop drawn on the canvas and one deserialized from an empty object are the same prop. */
export function defaultProp(kind, seed) {
  const base = { kind, id: "", seed: seed ?? 0 };
  switch (kind) {
    case "path":
      // Gravel, three blocks to a side, clean-edged: a plain route, which is the one every other style is a
      // variation on.
      return { ...base, points: [], radius: 3, style: "solid", coverage: 0.7, blocks: [{ id: 13, data: 0 }] };
    case "water":
      // A three-block-wide canal, cut two deep, meeting the land through a shore beach, over a bank of
      // jittered sand / gravel / coarse dirt. The bank is a full terrain material, not one block — the same
      // pattern the painter tiles. Numbers + the material mirror the C# WaterProp defaults.
      return { ...base, points: [], radius: 3, depth: 2, form: "canal", shore: 2, bank: {
        kind: "voronoi", seed: 1, cellSize: 5, palette: [
          { kind: "solid", id: 12, data: 0 }, { kind: "solid", id: 13, data: 0 }, { kind: "solid", id: 3, data: 1 },
        ] } };
    case "flora":
      return { ...base, points: [], spec: { coverage: 0.45, scale: 12, octaves: 3, fernShare: 0.25, flowerShare: 0.18, flowerScale: 18, tallShare: 0 } };
    case "tree":
      // Vanilla, because that is the tree a map is mostly made of; the grown one is what an author reaches
      // for when a spot wants a shape no vanilla generator makes.
      return { ...base, x: 0, z: 0, form: "template", species: "oak", wood: "oak", height: 12,
               stems: 1, leader: 0.55, flow: 0.45, branchAngle: 0.55, levels: 2, leafSize: 0.6 };
    case "boulder":
      return { ...base, x: 0, z: 0, form: "round", size: 2.5, blockId: 1, blockData: 0, mossy: true };
    default:
      throw new Error(`Unknown prop kind: ${kind}`);
  }
}

/** Whether a kind is placed at a point (a marker) rather than over a drawn area. The two are placed and
 *  edited differently — a marker is clicked and dragged, an area is traced — so the distinction is worth a
 *  name. Takes a kind or a prop, since callers have one or the other and neither should have to unwrap. */
export const isMarker = (propOrKind) => {
  const kind = typeof propOrKind === "string" ? propOrKind : propOrKind?.kind;
  return kind === "tree" || kind === "boulder";
};

/** A prop's position, as one point: a marker's own cell, else the middle of what it covers. What a label is
 *  anchored to and what a click is measured against. */
export function propAnchor(prop) {
  if (isMarker(prop)) return [prop.x, prop.z];
  const points = prop?.points ?? [];
  if (!points.length) return [0, 0];
  const xs = points.map(([x]) => x), zs = points.map(([, z]) => z);
  return [(Math.min(...xs) + Math.max(...xs)) / 2, (Math.min(...zs) + Math.max(...zs)) / 2];
}

/** How far from its anchor a prop reaches, in blocks — the radius a hit test and a mirror ghost use. */
export function propReach(prop) {
  if (prop?.kind === "tree") return Math.max(3, prop.height * 0.35);
  if (prop?.kind === "boulder") return Math.max(2, prop.size);
  return 0;
}

/** Move a prop by (dx, dz), returning a new prop. Areas move point by point, so a drag keeps their shape. */
export function translateProp(prop, dx, dz) {
  if (isMarker(prop)) return { ...prop, x: Math.round(prop.x + dx), z: Math.round(prop.z + dz) };
  return { ...prop, points: (prop.points ?? []).map(([x, z]) => [Math.round(x + dx), Math.round(z + dz)]) };
}

/**
 * The dressing document: props in the order they were placed, with ids minted here so a canvas can select,
 * move and delete one among many. Mutating methods return the document so a caller can chain; the caller owns
 * when to persist.
 */
export class DressingDoc {
  #props = [];
  #nextId = 1;

  /** Read a stored document. Anything unrecognised is dropped rather than carried as a shape nothing can
   *  edit — a prop kind the client does not know is a prop the client cannot draw. */
  static from(stored) {
    const doc = new DressingDoc();
    for (const prop of stored?.props ?? []) {
      if (!PROP_KINDS.includes(prop?.kind)) continue;
      doc.#props.push({ ...prop, id: prop.id || doc.#mintId() });
    }
    doc.#nextId = Math.max(doc.#nextId, ...doc.#props.map(p => (parseInt(String(p.id).replace(/\D/g, ""), 10) || 0) + 1));
    return doc;
  }

  /** The stored form — exactly what the pass deserializes. */
  toJSON() { return { props: this.#props }; }

  get props() { return this.#props; }
  get isEmpty() { return this.#props.length === 0; }

  byId(id) { return this.#props.find(prop => prop.id === id) ?? null; }

  add(prop) {
    const placed = { ...prop, id: prop.id || this.#mintId() };
    this.#props.push(placed);
    return placed;
  }

  update(id, patch) {
    const at = this.#props.findIndex(prop => prop.id === id);
    if (at < 0) return null;
    this.#props[at] = { ...this.#props[at], ...patch, id, kind: this.#props[at].kind };
    return this.#props[at];
  }

  remove(id) { this.#props = this.#props.filter(prop => prop.id !== id); }

  #mintId() { return `d${this.#nextId++}`; }
}
