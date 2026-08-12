/**
 * ReliefController — the placing tools of the sketch tool's Relief phase. Same controller contract as the
 * draw, edit and dressing controllers (onMouseDown → bool, onMouseMove, onMouseUp, paint, cancel), so the
 * canvas routes to it the same way it routes to those.
 *
 * Four tools, two interactions, and the split is the model's own. A **spot height** is a click, because a
 * summit is a decision about a place and a place is a click. A **ridgeline**, an **area** and a **scarp** are
 * dragged: press, trace, release — where the button comes up is the last point, exactly the way the lasso
 * already behaves, so there is no separate way to finish and no way to get stuck mid-draw.
 *
 * One thing here has no counterpart in dressing, and it is the whole difference between the two phases: a
 * prop is placed **on the map**, a mark is placed **in an island**. A relief is solved over one island's fused
 * footprint, so every mark has to belong to one, and the island is decided by where the trace *starts* — not
 * by where most of it lands. A mark is clipped, not confined: one placed past an edge raises the ground into
 * a corner and stops, which is how a spawn hill is authored with no wasted strip behind it. Judging ownership
 * by coverage would make that authoring gesture pick whichever island the overhang happened to cross.
 *
 * New marks take the tool's current settings as a copy, so an author can state six spot heights without
 * configuring six of them, while still bending any one afterwards. The exception is the first mark in an
 * island: its height starts at that island's own base, because a mark carried over from another island at
 * another base would state a cliff nobody asked for.
 */

import { paintMarkPreview, paintSpotGhost } from "../render/relief-render.js";
import { defaultMark, isSpot, isRing, markAnchor, markPoints, markReach, pointsPatch, translateMark }
  from "../relief/relief-doc.js";
import { douglasPeucker, simplifyRing } from "../geometry/simplify.js";
import { svgEl, handleRectAttrs } from "../render/svg.js";
import { toScreen } from "../geometry/transform.js";

/** Tool name → the kind of mark it places. The canvas passes tool names through, so this is also the test for
 *  "is a relief tool active at all". */
export const RELIEF_TOOLS = {
  "relief:point": "point", "relief:line": "line", "relief:area": "area", "relief:scarp": "scarp",
};

// A dragged trace is one point per block of pointer travel — unreadable to edit and pointless to store, so it
// is simplified to the points at real bends on release. The same tolerance the lasso and the dressing traces
// use, for the same reason.
const TRACE_SIMPLIFY_TOLERANCE = 3;

// How near a mark's anchor a click counts as picking it, in blocks.
const PICK_SLACK = 2;

// Screen px. The point grips match the sketch editor's vertex handles, so a traced mark reads as the same
// kind of editable thing a drawn polygon is.
const POINT_HALF = 4;

export class ReliefController {
  #doc;
  #callbacks;
  #handlesLayer;
  #getViewport;
  #tool = null;
  #trace = null;          // the in-progress drag: { kind, islandId, points }
  #cursor = null;         // where a spot height would drop
  #drag = null;           // moving an already-placed mark
  #pointDrag = null;      // reshaping one: { id, idx }
  #selectedId = null;
  #settings = {};         // per-kind starting values for the next mark placed
  #islandAt;              // (bx, bz) → the id of the island covering this cell, or null

  /**
   * @param doc          ReliefDoc — the stated relief (mutated through its own methods)
   * @param handlesLayer SVGGElement — screen-space handle layer for the selected mark's points
   * @param getViewport  () => { scale, panX, panY }
   * @param callbacks    { onChanged, onSelected, onPreviewChanged, onPlaced, onIslandAt }
   */
  constructor(doc, handlesLayer, getViewport, callbacks = {}) {
    this.#doc = doc;
    this.#handlesLayer = handlesLayer ?? null;
    this.#getViewport = getViewport ?? (() => ({ scale: 1, panX: 0, panY: 0 }));
    this.#callbacks = callbacks;
    this.#islandAt = callbacks.onIslandAt ?? (() => null);
    for (const kind of ["point", "line", "area", "scarp"]) this.#settings[kind] = defaultMark(kind);
  }

  setDoc(doc) { this.#doc = doc; this.#selectedId = null; this.refreshHandles(); }
  setTool(tool) { this.#tool = RELIEF_TOOLS[tool] ? tool : null; this.cancel(); }
  get activeKind() { return RELIEF_TOOLS[this.#tool] ?? null; }
  get selectedId() { return this.#selectedId; }

  /** The starting values the next mark of a kind takes. The inspector edits these when nothing is selected. */
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

  /** Change the selected mark's numbers, and remember them as the starting point for the next one of its kind
   *  — an author who widens one ridgeline usually means the next one too. */
  updateSelected(patch) {
    if (!this.#selectedId) return null;
    const updated = this.#doc.update(this.#selectedId, patch);
    if (updated) {
      this.setSettings(updated.kind, patch);
      this.refreshHandles();      // a wider band or a re-traced ring moves its own grips
      this.#callbacks.onChanged?.();
    }
    return updated;
  }

  /** Change an island's own relief settings — base, reach, step, grain, and the rim it carries. Not a mark:
   *  these are what the marks are stated against. */
  updateRelief(islandId, patch) {
    if (!islandId) return null;
    const relief = this.#doc.updateRelief(islandId, patch);
    this.#callbacks.onChanged?.();
    return relief;
  }

  // ── pointer ────────────────────────────────────────────────────────────────
  /** Press. Returns true when the relief phase consumed it. */
  onMouseDown(bx, bz, activeTool) {
    const kind = RELIEF_TOOLS[activeTool];
    if (kind) {
      const islandId = this.#islandAt(bx, bz);
      // A mark states something about an island's ground. Off every island there is no ground and no island
      // to state it about, so the press is consumed and nothing is begun.
      if (!islandId) { this.#callbacks.onPreviewChanged?.(); return true; }
      if (isSpot(kind)) { this.#place(kind, islandId, bx, bz); return true; }
      this.#trace = { kind, islandId, points: [[bx, bz]] };
      this.#callbacks.onPreviewChanged?.();
      return true;
    }
    if (activeTool !== "select" && activeTool !== null) return false;

    // Select mode: pick the mark under the cursor and start dragging it.
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
      const mark = this.#doc.byId(this.#drag.id);
      if (!mark) { this.#drag = null; return false; }
      const dx = bx - this.#drag.fromX, dz = bz - this.#drag.fromZ;
      if (!this.#drag.moved && dx === 0 && dz === 0) return true;
      this.#drag.moved = true;
      this.#drag.fromX = bx; this.#drag.fromZ = bz;
      // Dragged freely, including off the island. A mark past an edge is clipped rather than refused, and it
      // is the gesture that puts a hill in a corner with no wasted ground behind it — so the drag must not
      // stop at the outline the way a prop's does at the void.
      this.#doc.update(mark.id, translateMark(mark, dx, dz));
      this.refreshHandles();
      this.#callbacks.onPreviewChanged?.();
      return true;
    }
    // The spot tool shows where its mark would land, and whether the spot will take one — so a click off
    // every island reads as off-limits before the click that does nothing.
    const kind = RELIEF_TOOLS[activeTool];
    if (kind && isSpot(kind)) {
      this.#cursor = { kind, x: bx, z: bz, islandId: this.#islandAt(bx, bz) };
      this.#callbacks.onPreviewChanged?.();
      return false;
    }
    if (this.#cursor) { this.#cursor = null; this.#callbacks.onPreviewChanged?.(); }
    return false;
  }

  /** Release — the drag's last point is the trace's last point, which is the whole interaction. */
  onMouseUp() {
    if (this.#trace) {
      const { kind, islandId, points } = this.#trace;
      this.#trace = null;
      this.#finishTrace(kind, islandId, points);
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
  /** Take hold of one of a mark's points — `idx` into its traced points, or 0 for a spot's own cell. The
   *  grip's mousedown is a thin adapter over this, so what a drag *does* is reachable without a DOM. */
  beginPointDrag(id, idx) {
    if (!this.#doc.byId(id)) return false;
    this.#pointDrag = { id, idx, moved: false };
    return true;
  }

  /** Document mousemove while a point grip is held. World coordinates, so the point lands where the cursor
   *  is at any zoom. Returns true when consumed. */
  onHandleMove(wx, wz) {
    if (!this.#pointDrag) return false;
    const mark = this.#doc.byId(this.#pointDrag.id);
    if (!mark) { this.#pointDrag = null; return false; }
    const points = markPoints(mark).map(([x, z]) => [x, z]);
    if (this.#pointDrag.idx >= points.length) { this.#pointDrag = null; return false; }
    points[this.#pointDrag.idx] = [Math.round(wx), Math.round(wz)];
    this.#doc.update(mark.id, pointsPatch(mark, points));
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

  /** Redraw the selected mark's grips — one per traced point, or one on a spot's own cell. Call after a
   *  selection, an edit, or any viewport change, since the handles are screen-space and the points are not. */
  refreshHandles() {
    const layer = this.#handlesLayer;
    if (!layer) return;
    while (layer.firstChild) layer.removeChild(layer.firstChild);
    const mark = this.#selectedId ? this.#doc.byId(this.#selectedId) : null;
    if (!mark) return;
    markPoints(mark).forEach(([wx, wz], idx) => {
      const at = toScreen(wx, wz, this.#getViewport());
      const grip = svgEl("rect", {
        ...handleRectAttrs(at.x, at.y, POINT_HALF),
        fill: "var(--bg-deep)", stroke: "var(--accent)", "stroke-width": "1.5", style: "cursor:move",
      });
      grip.addEventListener("mousedown", (e) => {
        if (e.button !== 0) return;
        e.stopPropagation();   // the press is the grip's, not a canvas pan or a re-pick
        this.beginPointDrag(mark.id, idx);
      });
      grip.addEventListener("click", (e) => e.stopPropagation());
      layer.appendChild(grip);
    });
  }

  /** Draw whatever is in flight — the traced line, or the ghost of a spot height about to drop. */
  paint(painter) {
    if (this.#trace) {
      const settings = this.#settings[this.#trace.kind];
      paintMarkPreview(painter, this.#trace.kind, this.#trace.points, this.#heightOf(settings),
                       this.#baseOf(this.#trace.islandId));
    }
    if (this.#cursor) {
      const settings = this.#settings[this.#cursor.kind];
      const base = this.#baseOf(this.#cursor.islandId);
      paintSpotGhost(painter, this.#cursor.x, this.#cursor.z, Math.max(1, settings.r ?? 4),
                     this.#heightOf(settings), base, this.#cursor.islandId !== null);
    }
  }

  // ── private ────────────────────────────────────────────────────────────────
  #place(kind, islandId, bx, bz) {
    const placed = this.#doc.add(islandId, { ...this.#freshMark(kind, islandId), at: [bx, bz] });
    this.#cursor = null;
    this.select(placed.id);
    this.#callbacks.onChanged?.();
    this.#callbacks.onPlaced?.();
  }

  #finishTrace(kind, islandId, points) {
    // A ring simplifier splits at the two farthest points and walks both ways round, which is right for an
    // outline and would reorder a line. So a ridgeline and a scarp keep their direction through the plain
    // open simplifier — and a scarp's direction is load-bearing, since it says which side the shelf is on.
    const simplified = isRing(kind)
      ? simplifyRing(points, TRACE_SIMPLIFY_TOLERANCE)
      : douglasPeucker(points, TRACE_SIMPLIFY_TOLERANCE);
    // A line needs somewhere to go; an area needs to enclose something. Below that the drag was a misfire.
    if (simplified.length < (isRing(kind) ? 3 : 2)) { this.#callbacks.onPreviewChanged?.(); return; }

    const fresh = this.#freshMark(kind, islandId);
    const placed = this.#doc.add(islandId, { ...fresh, ...pointsPatch(fresh, simplified) });
    this.select(placed.id);
    this.#callbacks.onChanged?.();
    this.#callbacks.onPlaced?.();
  }

  /** A new mark of a kind: the tool's settings, but re-based when this island has nothing stated yet. A
   *  height carried over from another island at another base would state a cliff nobody asked for. */
  #freshMark(kind, islandId) {
    const relief = this.#doc.peek(islandId);
    if (relief?.marks?.length) return { ...this.#settings[kind] };
    return defaultMark(kind, relief?.base ?? this.#doc.reliefOf(islandId).base);
  }

  #baseOf(islandId) { return this.#doc.peek(islandId)?.base ?? 8; }

  // What a preview is coloured by — a scarp reads as its shelf, everything else as the height it states.
  #heightOf(settings) {
    if (settings?.kind === "scarp") return settings.high ?? 0;
    return Array.isArray(settings?.h) ? settings.h[0] : (settings?.h ?? 0);
  }

  // The smallest mark under the cursor wins, so a spot height inside a broad area is still clickable.
  #hitTest(bx, bz) {
    let best = null, bestReach = Infinity;
    for (const mark of this.#doc.marks) {
      if (!markPoints(mark).length) continue;
      const [ax, az] = markAnchor(mark);
      const reach = markReach(mark) + (isSpot(mark) ? PICK_SLACK : 0);
      const distance = Math.hypot(bx - ax, bz - az);
      if (distance <= reach && reach < bestReach) { best = mark; bestReach = reach; }
    }
    return best;
  }
}
