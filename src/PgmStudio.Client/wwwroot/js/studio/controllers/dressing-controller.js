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
 */

import { paintDressingPreview, paintMarkerGhost } from "../render/dressing-render.js";
import { defaultProp, isMarker, propAnchor, propReach, translateProp } from "../dressing/dressing-doc.js";
import { douglasPeucker, simplifyRing } from "../geometry/simplify.js";

/** Tool name → the kind of prop it places. The canvas passes tool names through, so this is also the test for
 *  "is a dressing tool active at all". */
export const DRESSING_TOOLS = { "dress:path": "path", "dress:water": "water", "dress:flora": "flora", "dress:tree": "tree", "dress:boulder": "boulder" };

// A dragged trace is one point per block of pointer travel — unreadable to edit and pointless to store, so it
// is simplified to the points at real bends on release. The same tolerance the lasso uses, for the same reason.
const TRACE_SIMPLIFY_TOLERANCE = 3;

// How near a prop's anchor a click counts as picking it, in blocks — a marker is small, so its own reach is
// too tight a target at a low zoom.
const PICK_SLACK = 2;

export class DressingController {
  #doc;
  #callbacks;
  #tool = null;
  #trace = null;          // the in-progress drag: { kind, points }
  #cursor = null;         // where a marker would drop
  #drag = null;           // moving an already-placed prop
  #selectedId = null;
  #settings = {};         // per-kind starting values for the next prop placed

  /**
   * @param doc        DressingDoc — the placed props (mutated through its own methods)
   * @param callbacks  { onChanged, onSelected, onPreviewChanged }
   */
  constructor(doc, callbacks = {}) {
    this.#doc = doc;
    this.#callbacks = callbacks;
    for (const kind of ["path", "water", "flora", "tree", "boulder"]) this.#settings[kind] = defaultProp(kind, seedFor(kind));
  }

  setDoc(doc) { this.#doc = doc; this.#selectedId = null; }
  setTool(tool) { this.#tool = DRESSING_TOOLS[tool] ? tool : null; this.cancel(); }
  get activeKind() { return DRESSING_TOOLS[this.#tool] ?? null; }
  get selectedId() { return this.#selectedId; }

  /** The starting values the next prop of a kind takes. The inspector edits these when nothing is selected. */
  settingsFor(kind) { return this.#settings[kind]; }
  setSettings(kind, patch) { this.#settings[kind] = { ...this.#settings[kind], ...patch }; }

  select(id) {
    this.#selectedId = this.#doc.byId(id) ? id : null;
    this.#callbacks.onSelected?.(this.#selectedId);
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
      this.#drag.moved = true;
      this.#drag.fromX = bx; this.#drag.fromZ = bz;
      this.#doc.update(prop.id, translateProp(prop, dx, dz));
      this.#callbacks.onPreviewChanged?.();
      return true;
    }
    // A marker tool shows where its prop would land, so a click is aimed rather than guessed.
    const kind = DRESSING_TOOLS[activeTool];
    if (kind && isMarker(kind)) {
      this.#cursor = { kind, x: bx, z: bz };
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
    if (!this.#trace && !this.#cursor && !this.#drag) return;
    this.#trace = null; this.#cursor = null; this.#drag = null;
    this.#callbacks.onPreviewChanged?.();
  }

  /** Draw whatever is in flight — the traced stroke, or the ghost of a marker about to drop. */
  paint(painter) {
    if (this.#trace) {
      const radius = this.#settings[this.#trace.kind]?.radius ?? 3;
      paintDressingPreview(painter, this.#trace.kind, this.#trace.points, radius);
    }
    if (this.#cursor) {
      const settings = this.#settings[this.#cursor.kind];
      paintMarkerGhost(painter, this.#cursor.kind, this.#cursor.x, this.#cursor.z, propReach(settings));
    }
  }

  // ── private ────────────────────────────────────────────────────────────────
  #place(kind, bx, bz) {
    const placed = this.#doc.add({ ...this.#settings[kind], x: bx, z: bz, seed: this.#nextSeed(kind) });
    this.#cursor = null;
    this.select(placed.id);
    this.#callbacks.onChanged?.();
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
