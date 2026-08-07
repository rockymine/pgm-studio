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
import { defaultProp, isMarker, propAnchor, propReach, translateProp } from "../dressing/dressing-doc.js";
import { douglasPeucker, simplifyRing } from "../geometry/simplify.js";
import { svgEl, handleRectAttrs } from "../render/svg.js";
import { toScreen } from "../geometry/transform.js";

/** Tool name → the kind of prop it places. The canvas passes tool names through, so this is also the test for
 *  "is a dressing tool active at all". */
export const DRESSING_TOOLS = { "dress:path": "path", "dress:water": "water", "dress:flora": "flora", "dress:tree": "tree", "dress:boulder": "boulder" };

// A dragged trace is one point per block of pointer travel — unreadable to edit and pointless to store, so it
// is simplified to the points at real bends on release. The same tolerance the lasso uses, for the same reason.
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
  #pointDrag = null;      // reshaping one: { id, idx } — idx -1 is a marker's own anchor
  #selectedId = null;
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
    for (const kind of ["path", "water", "flora", "tree", "boulder"]) this.#settings[kind] = defaultProp(kind, seedFor(kind));
  }

  setDoc(doc) { this.#doc = doc; this.#selectedId = null; this.refreshHandles(); }
  setTool(tool) { this.#tool = DRESSING_TOOLS[tool] ? tool : null; this.cancel(); }
  get activeKind() { return DRESSING_TOOLS[this.#tool] ?? null; }
  get selectedId() { return this.#selectedId; }

  /** The starting values the next prop of a kind takes. The inspector edits these when nothing is selected. */
  settingsFor(kind) { return this.#settings[kind]; }
  setSettings(kind, patch) { this.#settings[kind] = { ...this.#settings[kind], ...patch }; }

  select(id) {
    this.#selectedId = this.#doc.byId(id) ? id : null;
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
  /** Press. Returns true when the dressing phase consumed it. */
  onMouseDown(bx, bz, activeTool) {
    const kind = DRESSING_TOOLS[activeTool];
    if (kind) {
      if (isMarker(kind)) { this.#place(kind, bx, bz); return true; }
      this.#trace = { kind, points: [[bx, bz]] };
      this.#callbacks.onPreviewChanged?.();
      return true;
    }
    if (activeTool !== "select" && activeTool !== null) return false;

    // Select mode: pick the prop under the cursor and start dragging it.
    const hit = this.#hitTest(bx, bz);
    this.select(hit?.id ?? null);
    this.#drag = hit ? { id: hit.id, fromX: bx, fromZ: bz, moved: false } : null;
    return hit !== null;
  }

  onMouseMove(bx, bz, activeTool) {
    if (this.#trace) {
      const last = this.#trace.points[this.#trace.points.length - 1];
      if (bx !== last[0] || bz !== last[1]) {
        this.#trace.points.push([bx, bz]);
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
      this.#drag.moved = true;
      this.#drag.fromX = bx; this.#drag.fromZ = bz;
      this.#doc.update(prop.id, translateProp(prop, dx, dz));
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
  beginPointDrag(id, idx) {
    if (!this.#doc.byId(id)) return false;
    this.#pointDrag = { id, idx, moved: false };
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
      const points = (prop.points ?? []).map(([x, z]) => [x, z]);
      if (this.#pointDrag.idx >= points.length) { this.#pointDrag = null; return false; }
      points[this.#pointDrag.idx] = [bx, bz];
      this.#doc.update(prop.id, { points });
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
    const points = isMarker(prop) ? [propAnchor(prop)] : (prop.points ?? []);
    points.forEach(([wx, wz], i) => {
      const idx = isMarker(prop) ? -1 : i;
      const sp = toScreen(wx, wz, this.#getViewport());
      const grip = svgEl("rect", {
        ...handleRectAttrs(sp.x, sp.y, POINT_HALF),
        fill: "var(--bg-deep)", stroke: "var(--accent)", "stroke-width": "1.5", style: "cursor:move",
      });
      grip.addEventListener("mousedown", (e) => {
        if (e.button !== 0) return;
        e.stopPropagation();   // the press is the grip's, not a canvas pan or a re-pick
        this.beginPointDrag(prop.id, idx);
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
      paintMarkerGhost(painter, this.#cursor.kind, this.#cursor.x, this.#cursor.z, propReach(settings), this.#cursor.valid !== false);
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
    // A route is an open line and an area is a closed one, and they simplify differently: the ring simplifier
    // splits at the two farthest points and walks both ways round, which is right for an outline and would
    // reorder a route. So the open-line props (a path, a water channel) keep their direction through the plain
    // open simplifier.
    const openLine = kind === "path" || kind === "water";
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
  #hitTest(bx, bz) {
    let best = null, bestReach = Infinity;
    for (const prop of this.#doc.props) {
      const [ax, az] = propAnchor(prop);
      const reach = isMarker(prop) ? propReach(prop) + PICK_SLACK : areaReach(prop);
      const distance = Math.hypot(bx - ax, bz - az);
      if (distance <= reach && reach < bestReach) { best = prop; bestReach = reach; }
    }
    return best;
  }
}

// A distinct starting seed per kind, so a map's first path and its first tree do not share a field.
function seedFor(kind) { return { path: 5, water: 11, flora: 7, tree: 23, boulder: 17 }[kind] ?? 1; }

// How far an area prop reaches from its own middle — its bounding radius, which is what a click is measured
// against. Coarse on purpose: picking is about reaching the thing, not about its exact edge.
function areaReach(prop) {
  const points = prop.points ?? [];
  if (!points.length) return 0;
  const [ax, az] = propAnchor(prop);
  return Math.max(...points.map(([x, z]) => Math.hypot(x - ax, z - az))) + (prop.radius ?? 0);
}
