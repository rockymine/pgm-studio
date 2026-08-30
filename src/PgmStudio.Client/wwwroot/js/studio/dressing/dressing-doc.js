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
export const PROP_KINDS = ["stroke", "water", "flora", "house", "tree", "boulder"];

/** A fresh prop of each kind, before the author has touched a knob. The numbers mirror the C# record defaults,
 *  so a prop drawn on the canvas and one deserialized from an empty object are the same prop. */
export function defaultProp(kind, seed) {
  const base = { kind, id: "", seed: seed ?? 0 };
  switch (kind) {
    case "stroke":
      // Gravel, three blocks to a side, clean-edged, and paint rather than a route: the plainest band, which
      // every other style is a variation on. Route is declared, because it is what other props stand off.
      return { ...base, points: [], radius: 3, style: "solid", coverage: 0.7, route: false,
               pave: { kind: "solid", id: 13, data: 0 } };
    case "water":
      // A three-block-wide canal, cut two deep, meeting the land through a shore beach, over a bank of
      // cellular gravel / coarse dirt / sand. The bank is a full terrain material, not one block — the same
      // pattern the painter tiles. Numbers + the material mirror the C# WaterProp defaults.
      return { ...base, points: [], radius: 3, depth: 2, form: "canal", edge: 0.8, shore: 2, shoreWander: true, bank: {
        kind: "voronoi", seed: 1, cellSize: 5, rise: 0, bands: [
          { material: { kind: "solid", id: 13, data: 0 }, depth: 2 },
          { material: { kind: "solid", id: 3, data: 1 }, depth: 1 },
          { material: { kind: "solid", id: 12, data: 0 }, depth: 1 },
        ] } };
    case "flora":
      return { ...base, points: [], spec: { coverage: 0.45, scale: 12, octaves: 3, fernShare: 0.25, flowerShare: 0.18, flowerScale: 18, tallShare: 0 } };
    case "tree":
      // Vanilla, because that is the tree a map is mostly made of; the grown one is what an author reaches
      // for when a spot wants a shape no vanilla generator makes.
      return { ...base, x: 0, z: 0, form: "template", species: "oak", wood: "oak", height: 12,
               stems: 1, leader: 0.55, flow: 0.45, branchAngle: 0.55, levels: 2, leafSize: 0.6 };
    case "boulder":
      // An erratic: the rock a glacier left, big enough to take cover behind rather than step over.
      return { ...base, x: 0, z: 0, form: "round", size: 4, mossy: true,
               rock: { kind: "solid", id: 1, data: 0 } };
    case "house":
      // No style of its own until one is picked from the library: an empty object deserializes to the C#
      // HouseStyle defaults, which is the built-in shell — so a building drawn and never dressed is still a
      // building rather than nothing. `wings` is a list of rectangles rather than one — the canvas only ever
      // drags the first, but the shape carries more the day something else authors one (G177).
      return { ...base, wings: [], front: null, style: {} };
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

/** Whether a kind is placed by dragging a rectangle rather than by tracing an outline. A building is the only
 *  one: its footprint is what the stamper takes, and a stamper takes a box — one or more of them. Stored as
 *  `wings`, a list of rectangles each as two opposite corners, so a wing moves, mirrors and reshapes through
 *  the same per-rectangle code the single box always used; the canvas drags only the first one today. */
export const isRect = (propOrKind) =>
  (typeof propOrKind === "string" ? propOrKind : propOrKind?.kind) === "house";

/** One wing's two opposite corners. A stored wing is `AuthoredWing` — `{corners, spec?}` — because a wing
 *  states its own layers, roof and ridge alongside its rectangle; everything here reads its corners through
 *  this one accessor so the rest of the canvas never has to know the wrapper is there, and so the wrapper can
 *  gain fields without every reader learning about them. Anything that is not a pair of corners reads as no
 *  wing at all rather than as a crash: a drawn layer must not be taken down by one malformed prop. */
export const wingCorners = (wing) => {
  const corners = wing?.corners ?? wing;
  return Array.isArray(corners) && corners.length >= 2
      && Array.isArray(corners[0]) && Array.isArray(corners[1]) ? corners : null;
};

/** A wing carrying the given corners, keeping whatever else it states. */
export const withCorners = (wing, corners) => ({ ...(wing ?? {}), corners });

/** The largest footprint a placed building may cover, in blocks — three times the 8x8 shell a wool cage is
 *  stamped in, so a 12x16 house is buildable and a 20x30 one is not. Restated here rather than served because
 *  it is a rule the *canvas* has to apply while a drag is still in the pointer, before anything could be asked
 *  of the server; it is the same number `HouseProp.MaxFootprint` holds, and the same reason the 3-block minimum
 *  is restated below it. */
export const MAX_FOOTPRINT = 192;

/** A rect prop's footprint in whole blocks, or null when it is no building — too small to hold two walls and
 *  an inside, or past `MAX_FOOTPRINT`. The same floor and ceiling `HouseProp.Footprint` holds, so the canvas
 *  refuses exactly what the stamp would. Reads the wing being dragged or edited — the canvas only ever offers
 *  one at a time — so this is the live-drag and single-wing check; the server is the authority on a whole
 *  multi-wing plan's own covered area (G177). */
export function rectFootprint(prop) {
  const plan = rectPlan(prop);
  if (!plan) return null;
  return plan.width < 3 || plan.depth < 3 || plan.width * plan.depth > MAX_FOOTPRINT ? null : plan;
}

/** The rectangle two corners bound, whatever its size — what a drag in progress is, before it is judged. Takes
 *  either an in-progress trace (`{points: [c1, c2]}`, the shape a rectangle drag builds before it is placed) or
 *  a stored building (`{wings: [{corners: [c1, c2]}, ...]}`), reading its first wing — the one the canvas
 *  actually drags. */
export function rectPlan(prop) {
  const points = prop?.wings ? wingCorners(prop.wings[0]) : wingCorners(prop?.points);
  if (!points) return null;
  const minX = Math.floor(Math.min(points[0][0], points[1][0]));
  const minZ = Math.floor(Math.min(points[0][1], points[1][1]));
  const maxX = Math.floor(Math.max(points[0][0], points[1][0]));
  const maxZ = Math.floor(Math.max(points[0][1], points[1][1]));
  return { minX, minZ, width: maxX - minX + 1, depth: maxZ - minZ + 1 };
}

/** A prop's position, as one point: a marker's own cell, else the middle of what it covers. What a label is
 *  anchored to and what a click is measured against. A building's middle is measured across every wing's own
 *  corners, so a multi-wing plan anchors in the middle of the whole shape rather than of its first rectangle
 *  alone. */
export function propAnchor(prop) {
  if (isMarker(prop)) return [prop.x, prop.z];
  const points = isRect(prop)
    ? (prop?.wings ?? []).flatMap(wing => wingCorners(wing) ?? [])
    : (prop?.points ?? []);
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

/** Move a prop by (dx, dz), returning a new prop. Areas move point by point, and a building's every wing moves
 *  the same delta together, so a drag keeps every part's shape and the wings' own relative positions. */
export function translateProp(prop, dx, dz) {
  if (isMarker(prop)) return { ...prop, x: Math.round(prop.x + dx), z: Math.round(prop.z + dz) };
  const shift = ([x, z]) => [Math.round(x + dx), Math.round(z + dz)];
  if (isRect(prop)) {
    // A wing moves by its corners and keeps everything else it states.
    return { ...prop, wings: (prop.wings ?? []).map(wing => {
      const corners = wingCorners(wing);
      return corners ? withCorners(wing, corners.map(shift)) : wing;
    }) };
  }
  return { ...prop, points: (prop.points ?? []).map(shift) };
}

/**
 * The dressing document: props in the order they were placed, with ids minted here so a canvas can select,
 * move and delete one among many. Mutating methods return the document so a caller can chain; the caller owns
 * when to persist.
 */
export class DressingDoc {
  #props = [];
  #nextId = 1;

  // The layer a placement lands on — the layer the board is being drawn on, not a property of the prop
  // being placed. Empty means the board is flat and the export resolves the top surface, which is what an
  // unstacked board has always meant.
  #layer = "";

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

  /** Which layer a prop placed from now on rests on. */
  setLayer(id) { this.#layer = id || ""; return this; }

  get layer() { return this.#layer; }

  /** Place a prop. It takes the active layer unless it names one already — an edit that carries a layer
   *  keeps it, and a re-placed prop is not silently moved to whichever layer is being looked at. */
  add(prop) {
    const placed = { ...prop, id: prop.id || this.#mintId() };
    if (this.#layer && !placed.layer) placed.layer = this.#layer;
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
