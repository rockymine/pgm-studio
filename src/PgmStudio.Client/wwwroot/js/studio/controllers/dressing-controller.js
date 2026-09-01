/**
 * DressingController — the placing tools of the sketch tool's Dressing phase. Same controller contract as the
 * draw and edit controllers (onMouseDown → bool, onMouseMove, onMouseUp, paint, cancel), so the canvas routes
 * to it the same way it routes to those.
 *
 * Four tools, two interactions, and the split is the model's own. A **path** and an **area** are dragged: press,
 * trace, release — where the button comes up is the last point, exactly the way the lasso already behaves, so
 * there is no separate way to finish and no way to get stuck mid-draw. A **tree** and a **boulder** are
 * markers: one click drops one, because a tree is a decision about a spot and a spot is a click.
 *
 * New props take the tool's current settings as a copy. That is what lets an author place ten oaks without
 * configuring ten oaks, while still bending any one of them afterwards — the settings are a starting point,
 * not a binding.
 *
 * A placement ends the tool: the prop is selected and `onPlaced` asks the host for select mode, the same way
 * a finished draw tool hands the canvas back. The prop an author just put down is the one they want to move,
 * nudge or tune, and a still-armed tool turns that first click into a second tree.
 *
 * A **selected** prop wears screen-space handles, the same square grips a selected sketch polygon wears: one
 * per traced point on a route or an area, one on the anchor of a marker. They are the whole reason a prop can
 * be reshaped rather than only re-traced — a path that runs two blocks wide of a bridge is a point to drag,
 * not a route to draw again — and they are what says a prop is grabbable at all. Kept as SVG in the overlay
 * above the painted surface, where a fixed pixel size and a cursor are worth having.
 */

import { paintDressingPreview, paintMarkerGhost } from "../render/dressing-render.js";
import { buildingsOverlap, defaultProp, isMarker, isRect, propAnchor, propReach, rectFootprint, rectsJoinUp,
         translateProp, wingCorners, wingRects, withCorners } from "../dressing/dressing-doc.js";
import { douglasPeucker, simplifyRing } from "../geometry/simplify.js";
import { svgEl, handleRectAttrs } from "../render/svg.js";
import { toScreen } from "../geometry/transform.js";

/** Tool name → the kind of prop it places. The canvas passes tool names through, so this is also the test for
 *  "is a dressing tool active at all". */
export const DRESSING_TOOLS = { "dress:stroke": "stroke", "dress:water": "water", "dress:flora": "flora", "dress:house": "house", "dress:tree": "tree", "dress:boulder": "boulder" };

// A dragged trace is one point per block of pointer travel — unreadable to edit and pointless to store, so it
// is simplified to the points at real bends on release. Same simplifier as the lasso, one step tighter than
// it: a lasso outlines land, where a route runs THROUGH it, so a bend the outline can afford to round off is
// a bend a path would visibly miss.
const TRACE_SIMPLIFY_TOLERANCE = 3;

// How near a prop's anchor a click counts as picking it, in blocks — a marker is small, so its own reach is
// too tight a target at a low zoom.
const PICK_SLACK = 2;

// Screen px. The point grips match the sketch editor's vertex handles, so a traced route reads as the same
// kind of editable thing a drawn polygon is.
const POINT_HALF = 4;

export class DressingController {
  #doc;
  #callbacks;
  #handlesLayer;
  #getViewport;
  #tool = null;
  #trace = null;          // the in-progress drag: { kind, points }
  #cursor = null;         // where a marker would drop
  #drag = null;           // moving an already-placed prop
  #pointDrag = null;      // reshaping one: { id, idx, wing } — idx -1 is a marker's own anchor
  #selectedId = null;
  #alsoSelected = [];     // ids selected beside the primary, in click order — what a join reads
  #settings = {};         // per-kind starting values for the next prop placed
  #onTerrain;             // (bx, bz) → is this cell on the rasterized terrain a marker can seat on?

  /**
   * @param doc          DressingDoc — the placed props (mutated through its own methods)
   * @param handlesLayer SVGGElement — screen-space handle layer for the selected prop's points
   * @param getViewport  () => { scale, panX, panY }
   * @param callbacks    { onChanged, onSelected, onPreviewChanged, onPlaced, onTerrain }
   */
  constructor(doc, handlesLayer, getViewport, callbacks = {}) {
    this.#doc = doc;
    this.#handlesLayer = handlesLayer ?? null;
    this.#getViewport = getViewport ?? (() => ({ scale: 1, panX: 0, panY: 0 }));
    this.#callbacks = callbacks;
    this.#onTerrain = callbacks.onTerrain ?? (() => true);
    for (const kind of ["stroke", "water", "flora", "house", "tree", "boulder"]) this.#settings[kind] = defaultProp(kind, seedFor(kind));
  }

  setDoc(doc) { this.#doc = doc; this.#selectedId = null; this.refreshHandles(); }
  setTool(tool) { this.#tool = DRESSING_TOOLS[tool] ? tool : null; this.cancel(); }
  get activeKind() { return DRESSING_TOOLS[this.#tool] ?? null; }
  get selectedId() { return this.#selectedId; }

  /** The starting values the next prop of a kind takes. The inspector edits these when nothing is selected. */
  settingsFor(kind) { return this.#settings[kind]; }
  setSettings(kind, patch) { this.#settings[kind] = { ...this.#settings[kind], ...patch }; }

  /** The whole selection, primary first, dropping anything since deleted. One prop is the common case and the
   *  inspector still reads `selectedId`; the rest exist so two buildings can be named at once for a join. */
  get selection() {
    const ids = [this.#selectedId, ...this.#alsoSelected];
    return ids.filter((id, at) => id && ids.indexOf(id) === at && this.#doc.byId(id));
  }

  /**
   * Pick a prop. `additive` adds it beside whatever is already picked instead of replacing it — shift-click,
   * and the only way to name the two buildings a join is asked of. Picking an already-picked prop additively
   * drops it, so a shift-click both adds and takes back.
   */
  select(id, additive = false) {
    const live = this.#doc.byId(id) ? id : null;
    if (additive && live && this.#selectedId && live !== this.#selectedId) {
      const at = this.#alsoSelected.indexOf(live);
      if (at >= 0) this.#alsoSelected.splice(at, 1);
      else this.#alsoSelected.push(live);
    } else if (additive && live && live === this.#selectedId) {
      // Taking back the primary promotes the next one picked, so the selection never loses its head.
      this.#selectedId = this.#alsoSelected.shift() ?? null;
    } else {
      this.#selectedId = live;
      this.#alsoSelected = [];
    }
    this.#callbacks.onSelected?.(this.#selectedId);
    this.refreshHandles();
    this.#callbacks.onPreviewChanged?.();
  }

  /** Delete the selection. Returns true if something went. */
  deleteSelected() {
    if (!this.#selectedId) return false;
    this.#doc.remove(this.#selectedId);
    this.select(null);
    this.#callbacks.onChanged?.();
    return true;
  }

  /**
   * Join the selected buildings into one, or take a joined one apart again — the same chord both ways, because
   * an author holding two rectangles and an author holding an L are the same author changing their mind.
   *
   * <p>Joining keeps the earliest-placed building: it holds the id, the style, the door edge, the seed and the
   * layer, and gains the others' wings in the order they were picked. Taking apart is the exact inverse — one
   * building per wing, each keeping what the whole one stated — so the pair round-trips.</p>
   *
   * <p><b>What is not decided here is whether the wings make a building.</b> Two rectangles are a hall and a
   * cross wing only if their ridges cross, and a ridge follows proportions the server resolves, so the joint
   * model is asked rather than copied — the prop's preview answers `HJ1`–`HJ5` and the inspector reads the
   * refusal. What is refused here is the one case no reading is needed for and every join would fail on
   * anyway: two buildings standing on the same ground.</p>
   *
   * <p>The result is the only announcement: the host fires one change carrying it, because two events for one
   * edit race each other and the second short-circuits the first's preview round trip on an unchanged prop.</p>
   *
   * @returns {{ done: string, wings?: number } | { refused: string }} what happened, for the host to report.
   */
  joinSelection() {
    const picked = this.selection.map(id => this.#doc.byId(id)).filter(prop => isRect(prop));
    if (picked.length === 0) return { refused: "Pick a building first — a join is two rectangles becoming one." };

    if (picked.length === 1) {
      const only = picked[0];
      const wings = only.wings ?? [];
      if (wings.length < 2) {
        return { refused: "That building is one rectangle already. Shift-click a second one to join them." };
      }
      // Apart: the first wing stays where it is so the building an author was looking at keeps its place in
      // the list, and every other wing becomes a building of its own carrying the same finish.
      this.#doc.update(only.id, { wings: [wings[0]] });
      let seed = only.seed ?? 0;
      const made = [only.id];
      for (const wing of wings.slice(1)) {
        made.push(this.#doc.add({ ...only, id: "", seed: ++seed, wings: [wing] }).id);
      }
      this.#selectedId = made[0];
      this.#alsoSelected = made.slice(1);
      this.#callbacks.onSelected?.(this.#selectedId);
      this.refreshHandles();
      return { done: "apart", wings: wings.length };
    }

    for (let a = 0; a < picked.length; a++)
      for (let b = a + 1; b < picked.length; b++)
        if (buildingsOverlap(picked[a], picked[b])) {
          return { refused: "Those buildings stand on the same ground. A plan states its ground once, so "
                          + "move one until they touch along an edge instead of sharing blocks." };
        }

    const order = this.#doc.props.filter(prop => picked.some(one => one.id === prop.id));
    const rects = order.flatMap(prop => wingRects(prop));
    if (!rectsJoinUp(rects)) {
      return { refused: "Those buildings do not touch. A building is one shell under one roof, so move them "
                      + "until they meet along an edge." };
    }

    const [keep, ...rest] = order;
    const wings = order.flatMap(prop => prop.wings ?? []);
    this.#doc.update(keep.id, { wings });
    for (const gone of rest) this.#doc.remove(gone.id);
    this.#selectedId = keep.id;
    this.#alsoSelected = [];
    this.#callbacks.onSelected?.(this.#selectedId);
    this.refreshHandles();
    return { done: "joined", wings: wings.length };
  }

  /** Change the selected prop's knobs, and remember them as the starting point for the next one of its kind —
   *  an author who widens one path usually means the next one too. */
  updateSelected(patch) {
    if (!this.#selectedId) return null;
    const updated = this.#doc.update(this.#selectedId, patch);
    if (updated) {
      this.setSettings(updated.kind, patch);
      this.refreshHandles();      // a wider path or a re-traced area moves its own grips
      this.#callbacks.onChanged?.();
    }
    return updated;
  }

  // ── pointer ────────────────────────────────────────────────────────────────
  /** Press. Returns true when the dressing phase consumed it. `additive` is shift held: it adds to the
   *  selection instead of replacing it, which is how the two buildings of a join are named. */
  onMouseDown(bx, bz, activeTool, additive = false) {
    const kind = DRESSING_TOOLS[activeTool];
    if (kind) {
      if (isMarker(kind)) { this.#place(kind, bx, bz); return true; }
      // A rectangle is a two-corner drag: the press fixes one corner and every move rewrites the other, where
      // a traced outline appends. Same trace state, one different rule about what a move does to it.
      this.#trace = { kind, points: isRect(kind) ? [[bx, bz], [bx, bz]] : [[bx, bz]] };
      this.#callbacks.onPreviewChanged?.();
      return true;
    }
    if (activeTool !== "select" && activeTool !== null) return false;

    // Select mode: pick the prop under the cursor and start dragging it.
    const hit = this.#hitTest(bx, bz);
    this.select(hit?.id ?? null, additive);
    this.#drag = hit && !additive ? { id: hit.id, fromX: bx, fromZ: bz, moved: false } : null;
    return hit !== null;
  }

  onMouseMove(bx, bz, activeTool) {
    if (this.#trace) {
      const last = this.#trace.points[this.#trace.points.length - 1];
      if (bx !== last[0] || bz !== last[1]) {
        if (isRect(this.#trace.kind)) this.#trace.points[1] = [bx, bz];
        else this.#trace.points.push([bx, bz]);
        this.#callbacks.onPreviewChanged?.();
      }
      return true;
    }
    if (this.#drag) {
      const prop = this.#doc.byId(this.#drag.id);
      if (!prop) { this.#drag = null; return false; }
      const dx = bx - this.#drag.fromX, dz = bz - this.#drag.fromZ;
      if (!this.#drag.moved && dx === 0 && dz === 0) return true;
      // A marker can only be dragged across the terrain: over the void the drag simply doesn't follow, so the
      // prop stays on the last real cell it was over rather than being carried off the map.
      if (isMarker(prop) && !this.#onTerrain(bx, bz)) return true;
      const moved = translateProp(prop, dx, dz);
      // A plan states its ground once, so a building is not carried over another: the drag stops against it
      // the way a marker's stops at the void, leaving the prop on the last legal cell rather than landing it
      // somewhere the stamp would refuse.
      if (isRect(prop) && this.#wouldOverlap(moved)) return true;
      this.#drag.moved = true;
      this.#drag.fromX = bx; this.#drag.fromZ = bz;
      this.#doc.update(prop.id, moved);
      this.refreshHandles();
      this.#callbacks.onPreviewChanged?.();
      return true;
    }
    // A marker tool shows where its prop would land, so a click is aimed rather than guessed — and whether the
    // spot will take it, so the void reads as off-limits before the click that does nothing.
    const kind = DRESSING_TOOLS[activeTool];
    if (kind && isMarker(kind)) {
      this.#cursor = { kind, x: bx, z: bz, valid: this.#onTerrain(bx, bz) };
      this.#callbacks.onPreviewChanged?.();
      return false;
    }
    if (this.#cursor) { this.#cursor = null; this.#callbacks.onPreviewChanged?.(); }
    return false;
  }

  /** Release — the drag's last point is the stroke's last point, which is the whole interaction. */
  onMouseUp() {
    if (this.#trace) {
      const { kind, points } = this.#trace;
      this.#trace = null;
      this.#finishTrace(kind, points);
      return true;
    }
    if (this.#drag) {
      const moved = this.#drag.moved;
      this.#drag = null;
      if (moved) this.#callbacks.onChanged?.();
      return moved;
    }
    return false;
  }

  cancel() {
    if (!this.#trace && !this.#cursor && !this.#drag && !this.#pointDrag) return;
    this.#trace = null; this.#cursor = null; this.#drag = null; this.#pointDrag = null;
    this.#callbacks.onPreviewChanged?.();
  }

  // ── point handles ──────────────────────────────────────────────────────────
  /**
   * Take hold of one of a prop's points — `idx` into its `points`, or -1 for a marker's own anchor. The
   * grip's mousedown is a thin adapter over this, so what a drag *does* is reachable without a DOM.
   */
  beginPointDrag(id, idx, wing = 0) {
    if (!this.#doc.byId(id)) return false;
    this.#pointDrag = { id, idx, wing, moved: false };
    return true;
  }

  /**
   * Document mousemove while a point grip is held. World coordinates, so the point lands where the cursor
   * is at any zoom. Returns true when consumed, so the canvas can hand the same hook to its other draggers.
   */
  onHandleMove(wx, wz) {
    if (!this.#pointDrag) return false;
    const prop = this.#doc.byId(this.#pointDrag.id);
    if (!prop) { this.#pointDrag = null; return false; }
    const bx = Math.round(wx), bz = Math.round(wz);
    if (this.#pointDrag.idx < 0) {
      // A marker seats on the ground, so its grip stops at the terrain edge rather than carrying it off.
      if (!this.#onTerrain(bx, bz)) return true;
      this.#doc.update(prop.id, { x: bx, z: bz });
    } else {
      // A building's grip reshapes the wing that grip belongs to, leaving its siblings where they are; every
      // other area prop reshapes its own traced points the same way it always did.
      const rect = isRect(prop);
      const at = this.#pointDrag.wing;
      const points = (rect ? (wingCorners(prop.wings?.[at]) ?? []) : (prop.points ?? [])).map(([x, z]) => [x, z]);
      if (this.#pointDrag.idx >= points.length) { this.#pointDrag = null; return false; }
      points[this.#pointDrag.idx] = [bx, bz];
      // Reshaping a wing keeps whatever else that wing states — its layers, roof, ridge and joint.
      this.#doc.update(prop.id, rect
        ? { wings: (prop.wings ?? []).map((wing, i) => i === at ? withCorners(wing, points) : wing) }
        : { points });
    }
    this.#pointDrag.moved = true;
    this.refreshHandles();
    this.#callbacks.onPreviewChanged?.();
    return true;
  }

  /** Document mouseup ending a point drag. Returns true when consumed. */
  onHandleUp() {
    if (!this.#pointDrag) return false;
    const moved = this.#pointDrag.moved;
    this.#pointDrag = null;
    if (moved) this.#callbacks.onChanged?.();
    return true;
  }

  /**
   * Redraw the selected prop's grips — one per traced point, or one on a marker's anchor. Call after a
   * selection, an edit, or any viewport change, since the handles are screen-space and the points are not.
   */
  refreshHandles() {
    const layer = this.#handlesLayer;
    if (!layer) return;
    while (layer.firstChild) layer.removeChild(layer.firstChild);
    const prop = this.#selectedId ? this.#doc.byId(this.#selectedId) : null;
    if (!prop) return;
    // A building wears grips on every wing it states, each carrying the wing it belongs to, so a joined L is
    // reshaped a rectangle at a time rather than only through the one the canvas happened to draw first.
    const grips = isMarker(prop) ? [{ point: propAnchor(prop), idx: -1, wing: 0 }]
      : isRect(prop)
        ? (prop.wings ?? []).flatMap((wing, at) =>
            (wingCorners(wing) ?? []).map((point, i) => ({ point, idx: i, wing: at })))
        : (prop.points ?? []).map((point, i) => ({ point, idx: i, wing: 0 }));
    grips.forEach(({ point: [wx, wz], idx, wing }) => {
      const sp = toScreen(wx, wz, this.#getViewport());
      const grip = svgEl("rect", {
        ...handleRectAttrs(sp.x, sp.y, POINT_HALF),
        fill: "var(--bg-deep)", stroke: "var(--accent)", "stroke-width": "1.5", style: "cursor:move",
      });
      grip.addEventListener("mousedown", (e) => {
        if (e.button !== 0) return;
        e.stopPropagation();   // the press is the grip's, not a canvas pan or a re-pick
        this.beginPointDrag(prop.id, idx, wing);
      });
      grip.addEventListener("click", (e) => e.stopPropagation());
      layer.appendChild(grip);
    });
  }

  /** Draw whatever is in flight — the traced stroke, or the ghost of a marker about to drop. */
  paint(painter) {
    if (this.#trace) {
      const radius = this.#settings[this.#trace.kind]?.radius ?? 3;
      paintDressingPreview(painter, this.#trace.kind, this.#trace.points, radius);
    }
    if (this.#cursor) {
      const settings = this.#settings[this.#cursor.kind];
      paintMarkerGhost(painter, this.#cursor.kind, this.#cursor.x, this.#cursor.z,
                       propReach(settings, this.#doc.styles), this.#cursor.valid !== false);
    }
  }

  // ── private ────────────────────────────────────────────────────────────────
  #place(kind, bx, bz) {
    // A marker seats on the ground, so it can only be dropped where there is ground. The void — a gap between
    // shapes, the space off the map — takes nothing; the click is consumed but nothing is placed.
    if (!this.#onTerrain(bx, bz)) { this.#callbacks.onPreviewChanged?.(); return; }
    const placed = this.#doc.add({ ...this.#settings[kind], x: bx, z: bz, seed: this.#nextSeed(kind) });
    this.#cursor = null;
    this.select(placed.id);
    this.#callbacks.onChanged?.();
    this.#callbacks.onPlaced?.();
  }

  #finishTrace(kind, points) {
    // A rectangle is already the two points it will be stored as — there is nothing to simplify, and a drag
    // too small to hold two walls and an inside is a misfire rather than a tiny building.
    if (isRect(kind)) {
      if (!rectFootprint({ points })) { this.#callbacks.onPreviewChanged?.(); return; }
      const placed = this.#doc.add({ ...this.#settings[kind], wings: [{ corners: points }], seed: this.#nextSeed(kind) });
      this.select(placed.id);
      this.#callbacks.onChanged?.();
      this.#callbacks.onPlaced?.();
      return;
    }

    // A route is an open line and an area is a closed one, and they simplify differently: the ring simplifier
    // splits at the two farthest points and walks both ways round, which is right for an outline and would
    // reorder a route. So the open-line props (a path, a water channel) keep their direction through the plain
    // open simplifier.
    const openLine = kind === "stroke" || kind === "water";
    const simplified = openLine
      ? douglasPeucker(points, TRACE_SIMPLIFY_TOLERANCE)
      : simplifyRing(points, TRACE_SIMPLIFY_TOLERANCE);
    // An open line needs somewhere to go; an area needs to enclose something. Below that the drag was a misfire.
    const enough = openLine ? 2 : 3;
    if (simplified.length < enough) { this.#callbacks.onPreviewChanged?.(); return; }

    const placed = this.#doc.add({ ...this.#settings[kind], points: simplified, seed: this.#nextSeed(kind) });
    this.select(placed.id);
    this.#callbacks.onChanged?.();
    this.#callbacks.onPlaced?.();
  }

  // Each prop gets its own seed, so two rocks placed with the same settings are still two different rocks.
  // Derived from the count rather than rolled, so a document re-opened places nothing new.
  #nextSeed(kind) {
    const same = this.#doc.props.filter(prop => prop.kind === kind).length;
    return seedFor(kind) + same * 7;
  }

  // The smallest prop under the cursor wins, so a tree standing inside a flora area is still clickable.
  /** Whether a building, moved to where it is now stated, would stand on another building's ground. */
  #wouldOverlap(moved) {
    for (const other of this.#doc.props) {
      if (other.id === moved.id || !isRect(other)) continue;
      if (buildingsOverlap(moved, other)) return true;
    }
    return false;
  }

  #hitTest(bx, bz) {
    let best = null, bestReach = Infinity;
    for (const prop of this.#doc.props) {
      const [ax, az] = propAnchor(prop);
      const reach = isMarker(prop) ? propReach(prop, this.#doc.styles) + PICK_SLACK : areaReach(prop);
      const distance = Math.hypot(bx - ax, bz - az);
      if (distance <= reach && reach < bestReach) { best = prop; bestReach = reach; }
    }
    return best;
  }
}

// A distinct starting seed per kind, so a map's first path and its first tree do not share a field.
function seedFor(kind) { return { path: 5, water: 11, flora: 7, house: 13, tree: 23, boulder: 17 }[kind] ?? 1; }

// How far an area prop reaches from its own middle — its bounding radius, which is what a click is measured
// against. Coarse on purpose: picking is about reaching the thing, not about its exact edge.
function areaReach(prop) {
  const points = isRect(prop) ? (prop.wings ?? []).flatMap(wing => wingCorners(wing) ?? []) : (prop.points ?? []);
  if (!points.length) return 0;
  const [ax, az] = propAnchor(prop);
  return Math.max(...points.map(([x, z]) => Math.hypot(x - ax, z - az))) + (prop.radius ?? 0);
}
