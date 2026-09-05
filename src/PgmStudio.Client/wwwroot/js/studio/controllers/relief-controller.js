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
 * prop is placed **on the map**, a mark is placed **in a group**. A relief is solved over one group's fused
 * footprint, so every mark has to belong to one, and the group is decided by where the trace *starts* — not
 * by where most of it lands. A mark is clipped, not confined: one placed past an edge raises the ground into
 * a corner and stops, which is how a spawn hill is authored with no wasted strip behind it. Judging ownership
 * by coverage would make that authoring gesture pick whichever group the overhang happened to cross.
 *
 * New marks take the tool's current settings as a copy, so an author can state six spot heights without
 * configuring six of them, while still bending any one afterwards. The exception is the first mark in an
 * group: its height starts at that group's own base, because a mark carried over from another group at
 * another base would state a cliff nobody asked for.
 */

import { paintMarkPreview, paintSpotGhost } from "../render/relief-render.js";
import { defaultMark, defaultPush, isSpot, isRing, isPush, markPoints, pointsPatch,
         translateMark, FALLBACK_BASE, PUSH_KIND } from "../relief/relief-doc.js";
import { contourAt, markFromDrag } from "../relief/contour-drag.js";
import { douglasPeucker, simplifyRing } from "../geometry/simplify.js";
import { distToSegment } from "../geometry/shape.js";
import { pointInRing } from "../geometry/polygon.js";
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
// Screen px: how near an edge the pointer has to come for the insert ghost to offer itself, and
// how big that ghost is. Both are the draw stage's, since it is the same gesture on the same kind
// of outline and an author should not have to learn it twice.
const EDGE_THRESHOLD = 10;
const GHOST_R = 4;

export class ReliefController {
  #doc;
  #callbacks;
  #handlesLayer;
  #getViewport;
  #tool = null;
  #trace = null;          // the in-progress drag: { kind, groupId, points }
  #cursor = null;         // where a spot height would drop
  #drag = null;           // moving an already-placed mark
  #pointDrag = null;      // reshaping one: { id, idx }
  #contourDrag = null;    // moving a traced contour: { grabbed, fromX, fromZ, dx, dz }
  #hoveredEdge = -1;      // the selected mark's edge the insert ghost is offering itself on
  #ghostEl = null;        // that ghost, kept so a move repositions it rather than rebuilding it
  #selectedId = null;
  #settings = {};         // per-kind starting values for the next mark placed
  #groupAt;              // (bx, bz) → the id of the group covering this cell, or null
  #groupTop;             // (groupId) → the level that group's ground already stands at, or null
  #contours;              // () → the traced contour payload on screen, or null

  /**
   * @param doc          ReliefDoc — the stated relief (mutated through its own methods)
   * @param handlesLayer SVGGElement — screen-space handle layer for the selected mark's points
   * @param getViewport  () => { scale, panX, panY }
   * @param callbacks    { onChanged, onSelected, onPreviewChanged, onPlaced, onGroupAt, onGroupTop }
   */
  constructor(doc, handlesLayer, getViewport, callbacks = {}) {
    this.#doc = doc;
    this.#handlesLayer = handlesLayer ?? null;
    this.#getViewport = getViewport ?? (() => ({ scale: 1, panX: 0, panY: 0 }));
    this.#callbacks = callbacks;
    this.#groupAt = callbacks.onGroupAt ?? (() => null);
    this.#groupTop = callbacks.onGroupTop ?? (() => null);
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

  /** Rename the selection and follow it, since the id is what the selection is. Returns the complaint the
   *  document refused with, or null. */
  renameSelected(next) {
    if (!this.#selectedId) return "nothing selected";
    const refused = this.#doc.rename(this.#selectedId, next);
    if (refused) return refused;
    this.#selectedId = String(next).trim();
    this.#callbacks.onSelected?.(this.#selectedId);
    this.#callbacks.onChanged?.();
    return null;
  }

  /** Change a group's own relief settings — base, reach, step, grain, and the rim it carries. Not a mark:
   *  these are what the marks are stated against. */
  updateRelief(groupId, patch) {
    if (!groupId) return null;
    this.#reliefFor(groupId);
    const relief = this.#doc.updateRelief(groupId, patch);
    this.#callbacks.onChanged?.();
    return relief;
  }

  // ── pointer ────────────────────────────────────────────────────────────────
  /** Press. Returns true when the relief phase consumed it. */
  onMouseDown(bx, bz, activeTool) {
    const kind = RELIEF_TOOLS[activeTool];
    if (kind) {
      const groupId = this.#groupAt(bx, bz);
      // A mark states something about a group's ground. Off every group there is no ground and no group
      // to state it about, so the press is consumed and nothing is begun.
      if (!groupId) { this.#callbacks.onPreviewChanged?.(); return true; }
      if (isSpot(kind)) { this.#place(kind, groupId, bx, bz); return true; }
      this.#trace = { kind, groupId, points: [[bx, bz]] };
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
      // Dragged freely, including off the group. A mark past an edge is clipped rather than refused, and it
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
    // every group reads as off-limits before the click that does nothing.
    const kind = RELIEF_TOOLS[activeTool];
    if (kind && isSpot(kind)) {
      this.#cursor = { kind, x: bx, z: bz, groupId: this.#groupAt(bx, bz) };
      this.#callbacks.onPreviewChanged?.();
      return false;
    }
    if (this.#cursor) { this.#cursor = null; this.#callbacks.onPreviewChanged?.(); }
    // Nothing else wanted the pointer, so it is free to offer a point on the selected mark's own outline.
    this.#offerInsert(bx, bz, activeTool);
    return false;
  }

  /**
   * Show a midpoint ghost where the pointer is near an edge of the selected mark, and insert a point there
   * when it is pressed — the draw stage's gesture, on the outline a mark is drawn as.
   *
   * A mark's points are the shape of what it states, and until now the only way to change that shape was to
   * redraw the mark: the grips move the points a trace happened to leave and nothing adds one. The heights
   * beside them are a different quantity and are spaced along the run, so adding a height cannot add a point
   * and never could.
   */
  #offerInsert(bx, bz, activeTool) {
    const mark = this.#selectedId && (!activeTool || activeTool === "select")
      ? this.#doc.byId(this.#selectedId) : null;
    const points = mark && !isSpot(mark) ? markPoints(mark) : [];
    if (points.length < 2) { this.#clearGhost(); return; }

    const view = this.#getViewport();
    const at = toScreen(bx, bz, view);
    // A ring wraps and an open line does not, so the last-to-first edge exists for one and not the other.
    const edges = isRing(mark) || isPush(mark) ? points.length : points.length - 1;
    let best = EDGE_THRESHOLD, index = -1, mx = 0, my = 0;
    for (let i = 0; i < edges; i++) {
      const j = (i + 1) % points.length;
      const a = toScreen(points[i][0], points[i][1], view);
      const b = toScreen(points[j][0], points[j][1], view);
      const away = distToSegment(at.x, at.y, a.x, a.y, b.x, b.y);
      if (away < best) { best = away; index = i; mx = (a.x + b.x) / 2; my = (a.y + b.y) / 2; }
    }
    if (index < 0) { this.#clearGhost(); return; }
    this.#showGhost(mx, my, index);
  }

  #clearGhost() {
    this.#hoveredEdge = -1;
    if (this.#ghostEl) this.#ghostEl.style.display = "none";
  }

  #showGhost(sx, sy, edge) {
    if (!this.#handlesLayer) return;
    this.#hoveredEdge = edge;
    if (!this.#ghostEl || !this.#ghostEl.isConnected) {
      const el = svgEl("circle", {
        r: String(GHOST_R), fill: "var(--bg-deep)", stroke: "var(--accent-light)", "stroke-width": "1.5",
      });
      el.style.cursor = "copy";
      el.addEventListener("mousedown", (event) => {
        if (event.button !== 0) return;
        event.stopPropagation();   // the press adds a point rather than starting a pan or a re-pick
        this.insertPoint(this.#hoveredEdge);
      });
      el.addEventListener("click", (event) => event.stopPropagation());
      this.#handlesLayer.appendChild(el);
      this.#ghostEl = el;
    }
    this.#ghostEl.setAttribute("cx", String(sx));
    this.#ghostEl.setAttribute("cy", String(sy));
    this.#ghostEl.style.display = "";
  }

  /**
   * Put a point at the middle of one of the selected mark's edges, and leave it in the hand that pressed for
   * it. Returns true if it went in.
   *
   * The press that inserts becomes the drag: the point arrives under the cursor that asked for it and the
   * same gesture places it, which is what the draw stage's insert has always done. Dropping it at the
   * midpoint instead would make one gesture into two — press to add, re-aim, press again to move — for a
   * point whose whole purpose is to go somewhere the midpoint is not.
   */
  insertPoint(edge) {
    const mark = this.#selectedId ? this.#doc.byId(this.#selectedId) : null;
    if (!mark || edge < 0) return false;
    const points = markPoints(mark).map(point => [...point]);
    if (points.length < 2 || edge >= points.length) return false;
    // A ring is cyclic, so inserting at index 0 splits the closing edge — the new point lands between the
    // last and the first exactly as it does between any other pair.
    const next = (edge + 1) % points.length;
    points.splice(next, 0, [Math.round((points[edge][0] + points[next][0]) / 2),
                            Math.round((points[edge][1] + points[next][1]) / 2)]);
    this.#doc.update(mark.id, pointsPatch(mark, points));
    this.#clearGhost();
    this.beginPointDrag(mark.id, next);
    this.refreshHandles();
    this.#callbacks.onChanged?.();
    return true;
  }

  /** Release — the drag's last point is the trace's last point, which is the whole interaction. */
  onMouseUp() {
    if (this.#trace) {
      const { kind, groupId, points } = this.#trace;
      this.#trace = null;
      this.#finishTrace(kind, groupId, points);
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
      this.#reliefFor(stated.groupId);
      const placed = this.#doc.add(stated.groupId, stated.mark);
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
    this.#ghostEl = null;
    this.#hoveredEdge = -1;
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
      // A push is coloured by its lift read from zero, a mark by its height read from the group's base —
      // which is the same ramp asked the same question, since what both want to say is "higher" or "lower".
      paintMarkPreview(painter, this.#trace.kind, this.#trace.points, this.#heightOf(settings),
                       isPush(this.#trace.kind) ? 0 : this.#baseOf(this.#trace.groupId));
    }
    // A grabbed contour, at the height it states, following the pointer — so what the drag will say is on
    // screen before the release that says it.
    if (this.#contourDrag) {
      const { grabbed, dx, dz } = this.#contourDrag;
      const moved = [];
      for (let i = 0; i + 1 < grabbed.points.length; i += 2)
        moved.push([grabbed.points[i] + dx, grabbed.points[i + 1] + dz]);
      paintMarkPreview(painter, "line", moved, grabbed.level, this.#baseOf(grabbed.groupId));
    }
    if (this.#cursor) {
      const settings = this.#settings[this.#cursor.kind];
      const base = this.#baseOf(this.#cursor.groupId);
      paintSpotGhost(painter, this.#cursor.x, this.#cursor.z, Math.max(1, settings.r ?? 4),
                     this.#heightOf(settings), base, this.#cursor.groupId !== null);
    }
  }

  // ── private ────────────────────────────────────────────────────────────────
  #place(kind, groupId, bx, bz) {
    const placed = this.#doc.add(groupId, { ...this.#freshMark(kind, groupId), at: [bx, bz] });
    this.#cursor = null;
    this.select(placed.id);
    this.#callbacks.onChanged?.();
    this.#callbacks.onPlaced?.();
  }

  #finishTrace(kind, groupId, points) {
    // A ring simplifier splits at the two farthest points and walks both ways round, which is right for an
    // outline and would reorder a line. So a ridgeline and a scarp keep their direction through the plain
    // open simplifier — and a scarp's direction is load-bearing, since it says which side the shelf is on. A
    // push's ring closes, so it takes the ring simplifier with the bench.
    const simplified = isRing(kind)
      ? simplifyRing(points, TRACE_SIMPLIFY_TOLERANCE)
      : douglasPeucker(points, TRACE_SIMPLIFY_TOLERANCE);
    // A line needs somewhere to go; an area needs to enclose something. Below that the drag was a misfire.
    if (simplified.length < (isRing(kind) ? 3 : 2)) { this.#callbacks.onPreviewChanged?.(); return; }

    const fresh = this.#freshMark(kind, groupId);
    const placed = this.#doc.add(groupId, { ...fresh, ...pointsPatch(fresh, simplified) });
    this.select(placed.id);
    this.#callbacks.onChanged?.();
    this.#callbacks.onPlaced?.();
  }

  /** A new statement of a kind: the tool's settings, but re-based when this group has nothing stated yet. A
   *  height carried over from another group at another base would state a cliff nobody asked for.
   *
   *  A push needs no re-basing, and that is not an exception so much as the point of it: a push states a
   *  lift, not a level, so five blocks up is five blocks up wherever it is drawn. */
  #freshMark(kind, groupId) {
    // A push gets its own seed, so two drawn with the same roughness are still two different slopes. Derived
    // from how many there already are rather than rolled, so re-opening a document places nothing new.
    this.#reliefFor(groupId);
    if (isPush(kind)) return { ...this.#settings[kind], seed: 1 + this.#doc.pushes.length * 7 };
    const relief = this.#doc.peek(groupId);
    if (relief.marks.length) return { ...this.#settings[kind] };
    return defaultMark(kind, relief.base);
  }

  /** The group's relief, created against the level its ground already stands at. A relief REPLACES the top
   *  of every column of its group, so a base read from anywhere but the group moves the whole landmass the
   *  moment the first mark lands. */
  #reliefFor(groupId) { return this.#doc.reliefOf(groupId, this.#groupTop(groupId)); }

  #baseOf(groupId) { return this.#doc.peek(groupId)?.base ?? this.#groupTop(groupId) ?? FALLBACK_BASE; }

  // What a preview is coloured by — a scarp reads as its shelf, a push as its lift, everything else as the
  // height it states.
  #heightOf(settings) {
    if (isPush(settings)) return settings.amount ?? 0;
    if (settings?.kind === "scarp") return settings.high ?? 0;
    return Array.isArray(settings?.h) ? settings.h[0] : (settings?.h ?? 0);
  }

  /**
   * The statement under the cursor, measured against the geometry it was drawn as rather than against a
   * circle around it. A line is a band along a polyline and a bench is the ground inside a ring; a radius
   * from the anchor covers both, plus everything in the corners a long mark never reaches — so a press
   * anywhere in that circle picked the mark, and a press genuinely on the line lost to whichever other mark
   * happened to have the smaller circle.
   *
   * The tightest fit wins, so a spot height inside a broad bench is still what a click on it reaches. Pushes
   * are searched with the marks: they are picked up the same way and differ only in what they do.
   */
  #hitTest(bx, bz) {
    let best = null, bestFit = Infinity;
    for (const mark of this.#doc.statements) {
      const fit = this.#pickFit(mark, bx, bz);
      if (fit !== null && fit < bestFit) { best = mark; bestFit = fit; }
    }
    return best;
  }

  /**
   * How tightly a statement covers a point, or null where it does not cover it at all. The number is the
   * reach the point was caught by, so comparing two is comparing how specific each mark's claim is.
   */
  #pickFit(mark, bx, bz) {
    const points = markPoints(mark);
    if (!points.length) return null;

    if (isSpot(mark)) {
      const reach = Math.max(2, mark.r ?? 4) + PICK_SLACK;
      return Math.hypot(bx - points[0][0], bz - points[0][1]) <= reach ? reach : null;
    }

    // A ring's ground is what it states, so inside it counts — and so does the skirt a push moves outside its
    // own outline, since a press on the slope should reach the push that made it.
    if (isRing(mark) || isPush(mark)) {
      const skirt = isPush(mark) ? Math.max(0, mark.falloff ?? 10) : 0;
      if (points.length >= 3 && pointInRing(bx, bz, points)) return 0;
      const away = this.#distToOutline(points, bx, bz, true);
      const reach = skirt + PICK_SLACK;
      return away <= reach ? reach : null;
    }

    // A line and a scarp are bands along an open polyline: the reach is half the face plus the band either
    // side, which is the ground each of them actually states.
    const band = mark.kind === "scarp"
      ? Math.max(0.5, mark.face ?? 2) / 2 + Math.max(1, mark.band ?? 5)
      : Math.max(0, mark.r ?? 2);
    const reach = band + PICK_SLACK;
    return this.#distToOutline(points, bx, bz, false) <= reach ? reach : null;
  }

  /** How far a point lies from a run of points, closing it back on itself where the run is a ring. */
  #distToOutline(points, bx, bz, closed) {
    if (points.length === 1) return Math.hypot(bx - points[0][0], bz - points[0][1]);
    let nearest = Infinity;
    const edges = closed ? points.length : points.length - 1;
    for (let i = 0; i < edges; i++) {
      const j = (i + 1) % points.length;
      nearest = Math.min(nearest,
        distToSegment(bx, bz, points[i][0], points[i][1], points[j][0], points[j][1]));
    }
    return nearest;
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
  const { points, ring, at, amounts, id, ...rest } = patch ?? {};
  if (Array.isArray(rest.h)) delete rest.h;
  // A null in a patch says "state this no longer", which is a fact about the mark being edited rather than a
  // starting value for the next one — carried forward it would write the key back as an explicit null.
  for (const [key, value] of Object.entries(rest)) if (value === null) delete rest[key];
  return rest;
}
