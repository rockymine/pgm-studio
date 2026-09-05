/**
 * SketchEditController — the chrome for the shape levels of the sketch's selection ladder, and the drags it
 * opens. At the **shape** level it draws one transform box on the shape's own bounds: a rectangle moves the
 * bound a grip names, with snapping; anything stored as vertices (polygon/lasso, and a path's centerline)
 * has every point and Bézier handle scaled proportionally. At the **points** level it draws a handle per
 * vertex, the cubic-Bézier tangent handles, and the midpoint-insert ghost — and no box, because the box's
 * grips and the points would land on each other. Mirrors the editor edit controller's contract
 * (onResizeMove/onResizeUp consume hooks + onPointerMove for the edge-ghost). Mutates the shape and
 * reports via onShapeUpdated; the host triggers the group recompute.
 *
 * Constructor:
 *   handlesLayer  SVGGElement              — screen-space handle layer
 *   getViewport   () => { scale, panX, panY }
 *   getShape      (id) => shape | undefined
 *   callbacks     { onShapeUpdated, onVertexSelected, onSlopeControls, onRotateStart, snapEdges }
 *     snapEdges(id, {x,z}, alt) → {x,z}    — snap the dragged resize edge(s) to canvas targets + draw a
 *                                            guide (the resize counterpart of the canvas's move snapping)
 *     onRotateStart(event)                 — a press on a rotate zone; the canvas owns rotation, because it
 *                                            holds the snapshots and applies the same turn to a whole group
 */

import { svgEl } from "../render/svg.js";
import { renderTransformBox, gripSideX, gripSideZ } from "../render/canvas-chrome.js";
import { toScreen } from "../geometry/transform.js";
import { toBounds, distToSegment } from "../geometry/shape.js";

const VERTEX_R           = 4;   // a point of an outline, drawn as a disc the size of the insert ghost
const GHOST_R            = 4;
const EDGE_THRESHOLD     = 10;  // screen px — hover distance to show midpoint ghost
const BEZIER_R           = 3;   // bezier tangent handle radius (px)
const BEZIER_COLLAPSE_PX = 5;   // screen px — collapse the handle when this close to the vertex
const MIN_SPAN           = 1;   // blocks — an outline is never scaled thinner than this on either axis

// The shapes an author edits point by point. A path joins them because it is stored as the line it was
// drawn as — dragging one of its points moves the line, and the band follows. Its line is **open**, so the
// wrap-around edge every closed ring has does not exist for it; `closedVertices` is what says so.
const vertexEdited = (shape) => shape?.type === "polygon" || shape?.type === "lasso" || shape?.type === "polyline";
const closedVertices = (shape) => shape?.type !== "polyline";

export class SketchEditController {
  #handlesLayer;
  #getViewport;
  #getShape;
  #callbacks;

  #polyScaleState  = null;   // { shapeId, xf, zf, from } — an outline being stretched by its box
  #enabled         = true;   // off in the Theme phase: selection only, no editing affordance at all
  #selectedId      = null;
  // Which rung of the selection ladder the selected shape is on, and the only thing that decides what is
  // drawn: "group" means a whole landmass is selected and the canvas draws its box, so a member draws
  // nothing under it; "shape" draws the transform box; "points" draws the vertices. Exactly one of them
  // draws, which is what keeps two grips off one spot.
  #level           = "group";
  #selectedVertex  = -1;     // index of the click-selected vertex (for per-anchor height editing, S5b)
  #slopeControls   = [];     // vertex indices shift-clicked as surface-slope controls (2–3), insertion order
  #rectResizeState = null;
  #vertexDragState = null;
  #bezierDragState = null;
  #ghostEl         = null;
  #hoveredEdgeIdx  = -1;

  constructor(handlesLayer, getViewport, getShape, { onShapeUpdated, onVertexSelected, onSlopeControls, onRotateStart, snapEdges } = {}) {
    this.#handlesLayer = handlesLayer;
    this.#getViewport  = getViewport;
    this.#getShape     = getShape;
    this.#callbacks    = { onShapeUpdated, onVertexSelected, onSlopeControls, onRotateStart, snapEdges };
  }

  /**
   * The shape being edited and the rung it is on, in one call — the two are one fact, and a controller told
   * them separately can draw a level's chrome for the wrong shape between the two.
   *
   * Any (re)selection or level change drops the slope-control marks and tells the host, so clicking out,
   * stepping back up a rung or hitting Esc clears them just like it clears the shape selection.
   */
  setSelected(id, level = "shape") {
    if (id !== this.#selectedId) this.#selectedVertex = -1;
    this.#clearSlopeControls(id);
    this.#selectedId = id;
    this.#level = id ? level : "group";
  }

  /** Which rung the controller is drawing — the canvas asks before deciding whether to draw its own box. */
  get level() { return this.#level; }

  // Toggle a vertex in the surface-slope control set (shift-click), capped at 3 — the plane fit needs 2 or 3
  // points. Clears the single-vertex height selection so the two modes don't fight, and reports the set up so
  // the inspector can offer a height per control + Apply.
  #toggleSlopeControl(shapeId, idx) {
    const at = this.#slopeControls.indexOf(idx);
    if (at >= 0) this.#slopeControls.splice(at, 1);
    else if (this.#slopeControls.length < 3) this.#slopeControls.push(idx);
    else return;   // already 3 marked — ignore until one is removed
    this.#selectedVertex = -1;
    this.refresh();
    this.#callbacks.onSlopeControls?.(shapeId, [...this.#slopeControls]);
  }

  // Drop the slope control set (a plain vertex click, or clearing) and tell the host it's empty.
  #clearSlopeControls(shapeId) {
    if (!this.#slopeControls.length) return;
    this.#slopeControls = [];
    this.#callbacks.onSlopeControls?.(shapeId ?? this.#selectedId, []);
  }

  /**
   * Turn every editing affordance off (the Theme phase, where the canvas is a selection surface and shapes
   * are read-only). Disabled means the handle layer stays empty and the pointer hooks decline everything —
   * there is no resize handle, vertex, tangent or midpoint ghost to grab, so nothing can start a drag. Any
   * drag already in flight is dropped rather than left mid-edit.
   */
  setEnabled(on) {
    this.#enabled = !!on;
    if (!this.#enabled) {
      this.#rectResizeState = null; this.#vertexDragState = null; this.#bezierDragState = null;
      this.#polyScaleState = null;
    }
    this.refresh();
  }

  /** Redraw the selected shape's chrome for the rung it is on (call after viewport changes too). */
  refresh() {
    if (!this.#handlesLayer) return;
    while (this.#handlesLayer.firstChild) this.#handlesLayer.removeChild(this.#handlesLayer.firstChild);
    this.#ghostEl = null;
    this.#hoveredEdgeIdx = -1;
    if (!this.#enabled) return;
    if (this.#level === "group" || !this.#selectedId) return;
    const shape = this.#getShape(this.#selectedId);
    if (!shape) return;
    if (this.#level === "points" && vertexEdited(shape)) this.#renderVertexHandles(shape);
    else this.#renderShapeBox(shape);
  }

  /** Document mousemove during a resize / vertex / bezier drag. Returns true if consumed. */
  onResizeMove(wx, wz, altKey = false) {
    if (!this.#enabled) return false;
    if (this.#bezierDragState) {
      const { shapeId, vertexIdx, handle } = this.#bezierDragState;
      const shape = this.#getShape(shapeId);
      if (shape?.type === "polygon" || shape?.type === "lasso") {
        const [vx, vz] = shape.vertices[vertexIdx];
        const { scale } = this.#getViewport();
        const screenDist = Math.hypot(wx - vx, wz - vz) * scale;
        if (!shape.controls) shape.controls = {};
        const key = String(vertexIdx);
        if (screenDist < BEZIER_COLLAPSE_PX) {
          delete shape.controls[key];
          if (!Object.keys(shape.controls).length) delete shape.controls;
        } else {
          if (!shape.controls[key]) shape.controls[key] = {};
          shape.controls[key][handle] = [wx, wz];
          if (!altKey) {
            const other = handle === "out" ? "in" : "out";
            shape.controls[key][other] = [2 * vx - wx, 2 * vz - wz];
          }
        }
        this.#callbacks.onShapeUpdated?.(shape);
      }
      return true;
    }
    if (this.#vertexDragState) {
      const st = this.#vertexDragState;
      if (!st.moved && Math.hypot(wx - st.sx, wz - st.sz) < 0.5) return true;   // jitter — keep it a click
      st.moved = true;
      const { shapeId, vertexIdx } = st;
      const shape = this.#getShape(shapeId);
      if (vertexEdited(shape)) {
        const [oldX, oldZ] = shape.vertices[vertexIdx];
        const dx = wx - oldX, dz = wz - oldZ;
        shape.vertices[vertexIdx] = [wx, wz];
        const ctrl = shape.controls?.[String(vertexIdx)];
        if (ctrl) {
          if (ctrl.in)  ctrl.in  = [ctrl.in[0]  + dx, ctrl.in[1]  + dz];
          if (ctrl.out) ctrl.out = [ctrl.out[0] + dx, ctrl.out[1] + dz];
        }
        this.#callbacks.onShapeUpdated?.(shape);
      }
      return true;
    }
    if (this.#polyScaleState) {
      this.#scaleOutline(wx, wz);
      return true;
    }
    if (this.#rectResizeState) {
      const { shapeId, xf, zf } = this.#rectResizeState;
      const shape = this.#getShape(shapeId);
      if (shape?.type === "rectangle") {
        const bx = Math.floor(wx), bz = Math.floor(wz);
        // Proposed dragged-edge coords, then snap them to the canvas's targets (edges/centres + symmetry
        // centre) — same snapping the move path gets. Alt / the Snap toggle are honoured inside the hook.
        let ex = xf ? (xf === "max_x" ? bx + 1 : bx) : null;
        let ez = zf ? (zf === "max_z" ? bz + 1 : bz) : null;
        const snapped = this.#callbacks.snapEdges?.(shapeId, { x: ex, z: ez }, altKey);
        if (snapped) { ex = snapped.x; ez = snapped.z; }
        if (xf) shape[xf] = Math.round(ex);
        if (zf) shape[zf] = Math.round(ez);
        if (shape.min_x >= shape.max_x) {
          if (xf === "min_x") shape.min_x = shape.max_x - 1; else shape.max_x = shape.min_x + 1;
        }
        if (shape.min_z >= shape.max_z) {
          if (zf === "min_z") shape.min_z = shape.max_z - 1; else shape.max_z = shape.min_z + 1;
        }
        this.#callbacks.onShapeUpdated?.(shape);
      }
      return true;
    }
    return false;
  }

  /** Canvas mousemove: show a midpoint ghost near an edge — only where points are being edited, since that
   *  is the only rung whose gesture inserts one. */
  onPointerMove(wx, wz, activeTool) {
    const isEditMode = this.#enabled && this.#level === "points" && (!activeTool || activeTool === "select");
    if (!isEditMode || this.#vertexDragState || this.#bezierDragState || this.#rectResizeState || !this.#selectedId) {
      this.#clearGhost();
      return;
    }
    const shape = this.#getShape(this.#selectedId);
    if (!vertexEdited(shape) || !shape.vertices?.length) {
      this.#clearGhost();
      return;
    }
    const sp    = this.#toScreen(wx, wz);
    const verts = shape.vertices;
    const n     = verts.length;
    const edges = closedVertices(shape) ? n : n - 1;   // an open line has no wrap-around edge to insert into
    let bestDist = EDGE_THRESHOLD, bestIdx = -1, bestMx = 0, bestMy = 0;
    for (let i = 0; i < edges; i++) {
      const j = (i + 1) % n;
      const a = this.#toScreen(verts[i][0], verts[i][1]);
      const b = this.#toScreen(verts[j][0], verts[j][1]);
      const dist = distToSegment(sp.x, sp.y, a.x, a.y, b.x, b.y);
      if (dist < bestDist) { bestDist = dist; bestIdx = i; bestMx = (a.x + b.x) / 2; bestMy = (a.y + b.y) / 2; }
    }
    if (bestIdx >= 0) this.#showGhost(bestMx, bestMy, bestIdx);
    else this.#clearGhost();
  }

  /** Document mouseup to finish a resize / vertex / bezier drag. Returns true if consumed. */
  onResizeUp() {
    if (!this.#enabled) return false;
    if (this.#bezierDragState) {
      const { shapeId } = this.#bezierDragState;
      this.#bezierDragState = null;
      this.refresh();
      const shape = this.#getShape(shapeId);
      if (shape) this.#callbacks.onShapeUpdated?.(shape);
      return true;
    }
    if (this.#rectResizeState) { this.#rectResizeState = null; this.refresh(); return true; }
    if (this.#polyScaleState) { this.#polyScaleState = null; this.refresh(); return true; }
    if (this.#vertexDragState) {
      const { shapeId, vertexIdx, moved } = this.#vertexDragState;
      this.#vertexDragState = null;
      if (!moved) { this.#selectedVertex = vertexIdx; this.#callbacks.onVertexSelected?.(shapeId, vertexIdx); }
      this.refresh();
      if (moved) { const shape = this.#getShape(shapeId); if (shape) this.#callbacks.onShapeUpdated?.(shape); }
      return true;
    }
    return false;
  }

  // ── private ────────────────────────────────────────────────────────────────

  #toScreen(wx, wz) { return toScreen(wx, wz, this.#getViewport()); }

  #clearGhost() {
    if (this.#ghostEl) this.#ghostEl.style.display = "none";
    this.#hoveredEdgeIdx = -1;
  }

  #showGhost(sx, sy, edgeIdx) {
    this.#hoveredEdgeIdx = edgeIdx;
    if (!this.#ghostEl) {
      const el = svgEl("circle", {
        r: String(GHOST_R), fill: "var(--bg-deep)", stroke: "var(--accent-light)", "stroke-width": "1.5",
      });
      el.style.cursor = "copy";
      el.addEventListener("mousedown", (e) => {
        if (e.button !== 0) return;
        e.stopPropagation();
        this.#insertEdgeVertex(this.#selectedId, this.#hoveredEdgeIdx);
      });
      el.addEventListener("click", (e) => e.stopPropagation());
      this.#handlesLayer.appendChild(el);
      this.#ghostEl = el;
    }
    this.#ghostEl.setAttribute("cx", String(sx));
    this.#ghostEl.setAttribute("cy", String(sy));
    this.#ghostEl.style.display = "";
  }

  #insertEdgeVertex(shapeId, edgeIdx) {
    if (!shapeId || edgeIdx < 0) return;
    const shape = this.#getShape(shapeId);
    if (!shape?.vertices) return;
    const verts = shape.vertices;
    const n = verts.length, i = edgeIdx, j = (i + 1) % n;
    const mx = (verts[i][0] + verts[j][0]) / 2;
    const mz = (verts[i][1] + verts[j][1]) / 2;
    verts.splice(j, 0, [mx, mz]);
    if (shape.controls && Object.keys(shape.controls).length) {
      const shifted = {};
      for (const [k, v] of Object.entries(shape.controls)) {
        const ki = parseInt(k);
        shifted[String(ki >= j ? ki + 1 : ki)] = v;
      }
      shape.controls = shifted;
    }
    // Keep per-vertex heights aligned: splice the new vertex's height (the mid of its two neighbours) in at
    // the same index the vertex and Bézier controls shifted, so a sloped surface survives the insert instead
    // of falling back to a uniform height on the now-mismatched array.
    if (Array.isArray(shape.anchor_heights) && shape.anchor_heights.length === n) {
      const midHeight = Math.max(1, Math.round((shape.anchor_heights[i] + shape.anchor_heights[j]) / 2));
      shape.anchor_heights.splice(j, 0, midHeight);
    }
    this.#callbacks.onShapeUpdated?.(shape);
    this.#vertexDragState = { shapeId, vertexIdx: j };
    this.#ghostEl = null;
    this.#hoveredEdgeIdx = -1;
    this.refresh();
  }

  /**
   * The box that transforms one shape — the same eight grips and four rotate zones a whole group wears,
   * on the shape's own bounds. Where a grip sits is not the shape's business and is drawn by the one
   * emission both levels call; what a grip *does* is, and the two differ: a rectangle states bounds, so it
   * moves the bound the grip names and snaps it to the other shapes' edges, while anything stored as
   * vertices has every point and Bézier handle scaled proportionally.
   */
  #renderShapeBox(shape) {
    const world = toBounds(shape);
    if (!world) return;
    const tl = this.#toScreen(world.min_x, world.min_z);
    const br = this.#toScreen(world.max_x, world.max_z);
    const box = {
      l: Math.min(tl.x, br.x), r: Math.max(tl.x, br.x),
      t: Math.min(tl.y, br.y), b: Math.max(tl.y, br.y),
    };
    const scalable = shape.type === "rectangle" || (vertexEdited(shape) && (shape.vertices?.length ?? 0) >= 2);
    renderTransformBox(this.#handlesLayer, box, {
      onScale: scalable ? (grip, e) => {
        if (e.button !== 0) return;
        e.stopPropagation();
        this.#beginScale(shape, grip, world);
      } : null,
      onRotate: (e) => this.#callbacks.onRotateStart?.(e),
    });
  }

  /** Open a scale drag on one grip. The outline path freezes the shape as it stood when the drag opened, so
   *  every frame scales from that rather than from the last one and a drag back and forth lands where it
   *  started instead of compounding. */
  #beginScale(shape, grip, world) {
    // Which bound the grip drags: a corner names one on each axis, an edge band one on its own axis only.
    const side = (s, min, max) => (s === 0 ? null : s < 0 ? min : max);
    const xf = side(gripSideX(grip), "min_x", "max_x");
    const zf = side(gripSideZ(grip), "min_z", "max_z");
    if (shape.type === "rectangle") { this.#rectResizeState = { shapeId: shape.id, xf, zf }; return; }
    this.#polyScaleState = {
      shapeId: shape.id, xf, zf, from: world,
      vertices: (shape.vertices ?? []).map(([x, z]) => [x, z]),
      controls: JSON.parse(JSON.stringify(shape.controls ?? {})),
    };
  }

  /** Scale the outline so the dragged edge of its box lands under the pointer, every point moving with it. */
  #scaleOutline(wx, wz) {
    const st = this.#polyScaleState;
    const shape = this.#getShape(st.shapeId);
    if (!vertexEdited(shape)) return;

    const axis = (dragged, toward, minKey, maxKey) => {
      if (!dragged) return null;
      const anchor = dragged === maxKey ? st.from[minKey] : st.from[maxKey];
      const span = Math.abs(st.from[maxKey] - st.from[minKey]);
      if (span === 0) return null;
      const wanted = Math.max(MIN_SPAN, Math.abs(Math.round(toward) - anchor));
      return { anchor, factor: wanted / span };
    };
    const sx = axis(st.xf, wx, "min_x", "max_x");
    const sz = axis(st.zf, wz, "min_z", "max_z");
    const put = ([x, z]) => [
      sx ? sx.anchor + (x - sx.anchor) * sx.factor : x,
      sz ? sz.anchor + (z - sz.anchor) * sz.factor : z,
    ];

    shape.vertices = st.vertices.map(put);
    if (Object.keys(st.controls).length) {
      shape.controls = {};
      for (const [key, ctrl] of Object.entries(st.controls)) {
        shape.controls[key] = {};
        for (const side of ["in", "out"]) if (ctrl[side]) shape.controls[key][side] = put(ctrl[side]);
      }
    }
    this.#callbacks.onShapeUpdated?.(shape);
    this.refresh();
  }

  #renderVertexHandles(shape) {
    if (!shape.vertices?.length) return;
    const controls = shape.controls || {};

    // Bézier tangent lines + circles beneath the vertex handles.
    for (const [key, ctrl] of Object.entries(controls)) {
      const idx = parseInt(key);
      if (idx >= shape.vertices.length) continue;
      const vp = this.#toScreen(shape.vertices[idx][0], shape.vertices[idx][1]);
      for (const side of ["in", "out"]) {
        if (!ctrl[side]) continue;
        const cp = this.#toScreen(ctrl[side][0], ctrl[side][1]);
        this.#handlesLayer.appendChild(svgEl("line", {
          x1: vp.x, y1: vp.y, x2: cp.x, y2: cp.y,
          stroke: "var(--accent-light)", "stroke-width": "1", opacity: "0.7", "pointer-events": "none",
        }));
        const circle = svgEl("circle", {
          cx: cp.x, cy: cp.y, r: BEZIER_R,
          fill: "var(--accent-light)", stroke: "var(--bg-deep)", "stroke-width": "1",
        });
        circle.style.cursor = "move";
        circle.addEventListener("mousedown", (e) => {
          if (e.button !== 0) return;
          e.stopPropagation();
          this.#bezierDragState = { shapeId: shape.id, vertexIdx: idx, handle: side };
        });
        circle.addEventListener("click", (e) => e.stopPropagation());
        this.#handlesLayer.appendChild(circle);
      }
    }

    // Per-vertex height labels (the shape's height profile — anchor height, else its base height).
    const base = shape.base_height ?? 1;   // a shape is never zero-height (default 1)
    shape.vertices.forEach(([wx, wz], idx) => {
      const sp = this.#toScreen(wx, wz);
      const hh = shape.anchor_heights?.[idx] ?? base;
      const label = svgEl("text", {
        x: sp.x + 7, y: sp.y - 5, "font-size": "9", "font-weight": "600",
        fill: idx === this.#selectedVertex ? "var(--accent)" : "var(--text-muted)", "pointer-events": "none",
      });
      label.textContent = `${Math.round(hh)}`;
      this.#handlesLayer.appendChild(label);
    });

    // Vertex handles on top — **round**, where the transform box's anchors are square. The shape of a grip
    // says which rung the ladder is on: a square scales the whole thing, a circle is one point of it, and
    // the midpoint-insert ghost is round because what it adds is another point. Colour is left to say what
    // a point IS — plain, picked for its own height, or shift-marked as a slope control — so the two
    // questions never share an answer.
    shape.vertices.forEach(([wx, wz], idx) => {
      const sp = this.#toScreen(wx, wz);
      const selected = idx === this.#selectedVertex;
      const control = this.#slopeControls.includes(idx);   // a shift-marked surface-slope control
      const h = svgEl("circle", {
        cx: sp.x, cy: sp.y, r: control ? VERTEX_R + 1 : VERTEX_R,
        fill: control ? "var(--warning)" : (selected ? "var(--accent)" : "var(--bg-deep)"),
        stroke: control ? "var(--warning)" : "var(--accent)",
        "stroke-width": control ? "2" : "1.5",
      });
      h.style.cursor = "move";
      h.addEventListener("mousedown", (e) => {
        if (e.button !== 0) return;
        e.stopPropagation();
        if (e.shiftKey) {
          this.#toggleSlopeControl(shape.id, idx);   // mark/unmark a slope control (no drag, no single-select)
        } else if (e.ctrlKey) {
          this.#clearSlopeControls(shape.id);
          if (!shape.controls) shape.controls = {};
          this.#bezierDragState = { shapeId: shape.id, vertexIdx: idx, handle: "out" };
        } else {
          // Track start + a movement flag so a click (no drag) selects the vertex for height editing.
          this.#clearSlopeControls(shape.id);
          this.#vertexDragState = { shapeId: shape.id, vertexIdx: idx, sx: wx, sz: wz, moved: false };
        }
      });
      h.addEventListener("click", (e) => e.stopPropagation());
      this.#handlesLayer.appendChild(h);
    });
  }
}
