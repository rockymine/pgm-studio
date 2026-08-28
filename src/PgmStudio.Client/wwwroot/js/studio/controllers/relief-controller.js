/**
 * ReliefController — the placing tools of the sketch tool's Relief phase. Same controller contract as the
 * draw, edit and dressing controllers (onMouseDown → bool, onMouseMove, onMouseUp, paint, cancel), so the
 * canvas routes to it the same way it routes to those.
 *
 * Five tools, two interactions, and the split is the model's own. A **spot height** is a click, because a
 * summit is a decision about a place and a place is a click. A **ridgeline**, a **bench**, a **scarp** and a
 * **push** are dragged: press, trace, release — where the button comes up is the last point, exactly the way
 * the lasso already behaves, so there is no separate way to finish and no way to get stuck mid-draw.
 *
 * Four of the five state a **constraint** — the ground here IS twelve — and the fifth, the push, states a
 * **relative lift** applied to the solved surface afterwards. Everything about placing and editing them is
 * shared, and this file is that sharing; what differs is only what the solver does with the result, which is
 * why a push travels the same pipeline instead of getting a phase of its own.
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
import { defaultMark, defaultPush, isSpot, isRing, isPush, markAnchor, markPoints, markReach, pointsPatch,
         translateMark, FALLBACK_BASE, PUSH_KIND } from "../relief/relief-doc.js";
import { contourAt, markFromDrag } from "../relief/contour-drag.js";
import { douglasPeucker, simplifyRing } from "../geometry/simplify.js";
import { svgEl, handleRectAttrs } from "../render/svg.js";
import { toScreen } from "../geometry/transform.js";

/** Tool name → the kind of mark it places. The canvas passes tool names through, so this is also the test for
 *  "is a relief tool active at all". */
export const RELIEF_TOOLS = {
  "relief:point": "point", "relief:line": "line", "relief:area": "area", "relief:scarp": "scarp",
  "relief:push": PUSH_KIND,
};

// A dragged trace is one point per block of pointer travel — unreadable to edit and pointless to store, so it
// is simplified to the points at real bends on release. Same simplifier as the lasso and the dressing traces,
// and the dressing tolerance rather than the lasso's looser one, for the same reason: a lasso outlines land,
// where a ridgeline and a scarp run THROUGH it, and a bend an outline can afford to round off is a bend the
// terrain would visibly miss.
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
  #contourDrag = null;    // moving a traced contour: { grabbed, fromX, fromZ, dx, dz }
  #selectedId = null;
  #settings = {};         // per-kind starting values for the next mark placed
  #islandAt;              // (bx, bz) → the id of the island covering this cell, or null
  #islandTop;             // (islandId) → the level that island's ground already stands at, or null
  #contours;              // () → the traced contour payload on screen, or null

  /**
   * @param doc          ReliefDoc — the stated relief (mutated through its own methods)
   * @param handlesLayer SVGGElement — screen-space handle layer for the selected mark's points
   * @param getViewport  () => { scale, panX, panY }
   * @param callbacks    { onChanged, onSelected, onPreviewChanged, onPlaced, onIslandAt, onIslandTop }
   */
  constructor(doc, handlesLayer, getViewport, callbacks = {}) {
    this.#doc = doc;
    this.#handlesLayer = handlesLayer ?? null;
    this.#getViewport = getViewport ?? (() => ({ scale: 1, panX: 0, panY: 0 }));
    this.#callbacks = callbacks;
    this.#islandAt = callbacks.onIslandAt ?? (() => null);
    this.#islandTop = callbacks.onIslandTop ?? (() => null);
    this.#contours = callbacks.onContours ?? (() => null);
    for (const kind of ["point", "line", "area", "scarp", PUSH_KIND]) this.#settings[kind] = defaultMark(kind);
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
      this.setSettings(updated.kind, carryable(patch));
      this.refreshHandles();      // a wider band or a re-traced ring moves its own grips
      this.#callbacks.onChanged?.();
    }
    return updated;
  }

  /** Change an island's own relief settings — base, reach, step, grain, and the rim it carries. Not a mark:
   *  these are what the marks are stated against. */
  updateRelief(islandId, patch) {
    if (!islandId) return null;
    this.#reliefFor(islandId);
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
    if (hit) {
      this.select(hit.id);
      this.#drag = { id: hit.id, fromX: bx, fromZ: bz, moved: false };
      return true;
    }

    // Nothing placed under the cursor — but a CONTOUR may be, and a contour is grabbable. It is a line of
    // constant height, so moving one says the ground reaches that height here now, which is a line mark at
    // that level. Marks win the press: one is a thing an author put there, and the contours are what the
    // solver made of them.
    const grabbed = contourAt(this.#contours(), bx, bz);
    if (grabbed) {
      this.select(null);
      this.#contourDrag = { grabbed, fromX: bx, fromZ: bz, dx: 0, dz: 0 };
      return true;
    }

    this.select(null);
    this.#drag = null;
    return false;
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
    if (this.#contourDrag) {
      this.#contourDrag.dx = bx - this.#contourDrag.fromX;
      this.#contourDrag.dz = bz - this.#contourDrag.fromZ;
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
    if (this.#contourDrag) {
      const { grabbed, dx, dz } = this.#contourDrag;
      this.#contourDrag = null;
      const stated = markFromDrag(grabbed, dx, dz);
      // A contour pressed and released without moving is a click on a line, not a statement about the ground.
      if (!stated) { this.#callbacks.onPreviewChanged?.(); return true; }
      this.#reliefFor(stated.islandId);
      const placed = this.#doc.add(stated.islandId, stated.mark);
      this.select(placed.id);
      this.#callbacks.onChanged?.();
      return true;
    }
    return false;
  }

  cancel() {
    if (!this.#trace && !this.#cursor && !this.#drag && !this.#pointDrag && !this.#contourDrag) return;
    this.#trace = null; this.#cursor = null; this.#drag = null; this.#pointDrag = null; this.#contourDrag = null;
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
      // A push is coloured by its lift read from zero, a mark by its height read from the island's base —
      // which is the same ramp asked the same question, since what both want to say is "higher" or "lower".
      paintMarkPreview(painter, this.#trace.kind, this.#trace.points, this.#heightOf(settings),
                       isPush(this.#trace.kind) ? 0 : this.#baseOf(this.#trace.islandId));
    }
    // A grabbed contour, at the height it states, following the pointer — so what the drag will say is on
    // screen before the release that says it.
    if (this.#contourDrag) {
      const { grabbed, dx, dz } = this.#contourDrag;
      const moved = [];
      for (let i = 0; i + 1 < grabbed.points.length; i += 2)
        moved.push([grabbed.points[i] + dx, grabbed.points[i + 1] + dz]);
      paintMarkPreview(painter, "line", moved, grabbed.level, this.#baseOf(grabbed.islandId));
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
    // open simplifier — and a scarp's direction is load-bearing, since it says which side the shelf is on. A
    // push's ring closes, so it takes the ring simplifier with the bench.
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

  /** A new statement of a kind: the tool's settings, but re-based when this island has nothing stated yet. A
   *  height carried over from another island at another base would state a cliff nobody asked for.
   *
   *  A push needs no re-basing, and that is not an exception so much as the point of it: a push states a
   *  lift, not a level, so five blocks up is five blocks up wherever it is drawn. */
  #freshMark(kind, islandId) {
    // A push gets its own seed, so two drawn with the same roughness are still two different slopes. Derived
    // from how many there already are rather than rolled, so re-opening a document places nothing new.
    this.#reliefFor(islandId);
    if (isPush(kind)) return { ...this.#settings[kind], seed: 1 + this.#doc.pushes.length * 7 };
    const relief = this.#doc.peek(islandId);
    if (relief.marks.length) return { ...this.#settings[kind] };
    return defaultMark(kind, relief.base);
  }

  /** The island's relief, created against the level its ground already stands at. A relief REPLACES the top
   *  of every column of its island, so a base read from anywhere but the island moves the whole landmass the
   *  moment the first mark lands. */
  #reliefFor(islandId) { return this.#doc.reliefOf(islandId, this.#islandTop(islandId)); }

  #baseOf(islandId) { return this.#doc.peek(islandId)?.base ?? this.#islandTop(islandId) ?? FALLBACK_BASE; }

  // What a preview is coloured by — a scarp reads as its shelf, a push as its lift, everything else as the
  // height it states.
  #heightOf(settings) {
    if (isPush(settings)) return settings.amount ?? 0;
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

/**
 * The part of an edit worth carrying to the next statement of the same kind. Widening one ridgeline usually
 * means the next one too; where that one was drawn never does.
 *
 * Geometry is the obvious exclusion. The one that is not obvious, and the reason this function exists rather
 * than a `delete patch.points`: a **per-vertex** number is sized to the ring it was stated on. Carried
 * forward, a push whose six corners each hold a different lift would hand a freshly drawn four-corner push an
 * array it cannot mean — and the same goes for a ridgeline whose `h` has become one height per vertex.
 */
function carryable(patch) {
  const { points, ring, at, amounts, ...rest } = patch ?? {};
  if (Array.isArray(rest.h)) delete rest.h;
  return rest;
}
