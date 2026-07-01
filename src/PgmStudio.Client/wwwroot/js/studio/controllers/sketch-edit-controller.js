/**
 * SketchEditController — 8-point rectangle resize, polygon/lasso vertex drag, cubic-Bézier tangent
 * handles, and midpoint-insert for SketchCanvas. Mirrors the editor edit controller's contract
 * (onResizeMove/onResizeUp consume hooks + onPointerMove for the edge-ghost). Mutates the shape and
 * reports via onShapeUpdated; the host triggers the island recompute.
 *
 * Constructor:
 *   handlesLayer  SVGGElement              — screen-space handle layer
 *   getViewport   () => { scale, panX, panY }
 *   getShape      (id) => shape | undefined
 *   callbacks     { onShapeUpdated, onVertexSelected, snapEdges }
 *     snapEdges(id, {x,z}, alt) → {x,z}    — snap the dragged resize edge(s) to canvas targets + draw a
 *                                            guide (the resize counterpart of the canvas's move snapping)
 */

import { svgEl, handleRectAttrs } from "../render/svg.js";

const HANDLE_HALF        = 5;
const VERTEX_HALF        = 4;
const GHOST_R            = 4;
const EDGE_THRESHOLD     = 10;  // screen px — hover distance to show midpoint ghost
const BEZIER_R           = 3;   // bezier tangent handle radius (px)
const BEZIER_COLLAPSE_PX = 5;   // screen px — collapse the handle when this close to the vertex

function distToSegment(px, py, ax, ay, bx, by) {
  const dx = bx - ax, dy = by - ay;
  const lenSq = dx * dx + dy * dy;
  if (lenSq === 0) return Math.hypot(px - ax, py - ay);
  const t = Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / lenSq));
  return Math.hypot(px - (ax + t * dx), py - (ay + t * dy));
}

const HANDLE_DEFS = [
  { key: "nw", pos: b => [b.l,  b.t],  cursor: "nw-resize", xf: "min_x", zf: "min_z" },
  { key: "n",  pos: b => [b.mx, b.t],  cursor: "n-resize",  xf: null,    zf: "min_z" },
  { key: "ne", pos: b => [b.r,  b.t],  cursor: "ne-resize", xf: "max_x", zf: "min_z" },
  { key: "w",  pos: b => [b.l,  b.my], cursor: "w-resize",  xf: "min_x", zf: null    },
  { key: "e",  pos: b => [b.r,  b.my], cursor: "e-resize",  xf: "max_x", zf: null    },
  { key: "sw", pos: b => [b.l,  b.b],  cursor: "sw-resize", xf: "min_x", zf: "max_z" },
  { key: "s",  pos: b => [b.mx, b.b],  cursor: "s-resize",  xf: null,    zf: "max_z" },
  { key: "se", pos: b => [b.r,  b.b],  cursor: "se-resize", xf: "max_x", zf: "max_z" },
];

export class SketchEditController {
  #handlesLayer;
  #getViewport;
  #getShape;
  #callbacks;

  #selectedId      = null;
  #selectedVertex  = -1;     // index of the click-selected vertex (for per-anchor height editing, S5b)
  #rectResizeState = null;
  #vertexDragState = null;
  #bezierDragState = null;
  #ghostEl         = null;
  #hoveredEdgeIdx  = -1;

  constructor(handlesLayer, getViewport, getShape, { onShapeUpdated, onVertexSelected, snapEdges } = {}) {
    this.#handlesLayer = handlesLayer;
    this.#getViewport  = getViewport;
    this.#getShape     = getShape;
    this.#callbacks    = { onShapeUpdated, onVertexSelected, snapEdges };
  }

  setSelected(id) { if (id !== this.#selectedId) this.#selectedVertex = -1; this.#selectedId = id; }

  /** Redraw handles for the selected shape (call after viewport changes too). */
  refresh() {
    if (!this.#handlesLayer) return;
    while (this.#handlesLayer.firstChild) this.#handlesLayer.removeChild(this.#handlesLayer.firstChild);
    this.#ghostEl = null;
    this.#hoveredEdgeIdx = -1;
    if (!this.#selectedId) return;
    const shape = this.#getShape(this.#selectedId);
    if (!shape) return;
    if (shape.type === "rectangle") this.#renderRectHandles(shape);
    else if (shape.type === "polygon" || shape.type === "lasso") this.#renderVertexHandles(shape);
  }

  /** Document mousemove during a resize / vertex / bezier drag. Returns true if consumed. */
  onResizeMove(wx, wz, altKey = false) {
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
      if (shape?.type === "polygon" || shape?.type === "lasso") {
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

  /** Canvas mousemove (select/move mode only): show a midpoint ghost near a polygon edge. */
  onPointerMove(wx, wz, activeTool) {
    const isEditMode = !activeTool || activeTool === "select";
    if (!isEditMode || this.#vertexDragState || this.#bezierDragState || this.#rectResizeState || !this.#selectedId) {
      this.#clearGhost();
      return;
    }
    const shape = this.#getShape(this.#selectedId);
    if (!shape || (shape.type !== "polygon" && shape.type !== "lasso") || !shape.vertices?.length) {
      this.#clearGhost();
      return;
    }
    const sp    = this.#toScreen(wx, wz);
    const verts = shape.vertices;
    const n     = verts.length;
    let bestDist = EDGE_THRESHOLD, bestIdx = -1, bestMx = 0, bestMy = 0;
    for (let i = 0; i < n; i++) {
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
    if (this.#bezierDragState) {
      const { shapeId } = this.#bezierDragState;
      this.#bezierDragState = null;
      this.refresh();
      const shape = this.#getShape(shapeId);
      if (shape) this.#callbacks.onShapeUpdated?.(shape);
      return true;
    }
    if (this.#rectResizeState) { this.#rectResizeState = null; this.refresh(); return true; }
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

  #toScreen(wx, wz) {
    const { scale, panX, panY } = this.#getViewport();
    return { x: wx * scale + panX, y: wz * scale + panY };
  }

  #clearGhost() {
    if (this.#ghostEl) this.#ghostEl.style.display = "none";
    this.#hoveredEdgeIdx = -1;
  }

  #showGhost(sx, sy, edgeIdx) {
    this.#hoveredEdgeIdx = edgeIdx;
    if (!this.#ghostEl) {
      const el = svgEl("circle", {
        r: String(GHOST_R), fill: "var(--bg-deep)", stroke: "var(--accent-light)",
        "stroke-width": "1.5", style: "cursor:copy",
      });
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
    this.#callbacks.onShapeUpdated?.(shape);
    this.#vertexDragState = { shapeId, vertexIdx: j };
    this.#ghostEl = null;
    this.#hoveredEdgeIdx = -1;
    this.refresh();
  }

  #renderRectHandles(shape) {
    const tl = this.#toScreen(shape.min_x, shape.min_z);
    const br = this.#toScreen(shape.max_x, shape.max_z);
    const b  = { l: Math.min(tl.x, br.x), r: Math.max(tl.x, br.x), t: Math.min(tl.y, br.y), b: Math.max(tl.y, br.y) };
    b.mx = (b.l + b.r) / 2;
    b.my = (b.t + b.b) / 2;
    for (const hd of HANDLE_DEFS) {
      const [hx, hy] = hd.pos(b);
      const h = svgEl("rect", {
        ...handleRectAttrs(hx, hy, HANDLE_HALF),
        fill: "var(--bg-deep)", stroke: "var(--text-muted)", "stroke-width": "1", style: `cursor:${hd.cursor}`,
      });
      h.addEventListener("mousedown", (e) => {
        if (e.button !== 0) return;
        e.stopPropagation();
        this.#rectResizeState = { shapeId: shape.id, xf: hd.xf, zf: hd.zf };
      });
      this.#handlesLayer.appendChild(h);
    }
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
          fill: "var(--accent-light)", stroke: "var(--bg-deep)", "stroke-width": "1", style: "cursor:move",
        });
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

    // Vertex handles on top.
    shape.vertices.forEach(([wx, wz], idx) => {
      const sp = this.#toScreen(wx, wz);
      const selected = idx === this.#selectedVertex;
      const h = svgEl("rect", {
        ...handleRectAttrs(sp.x, sp.y, VERTEX_HALF),
        fill: selected ? "var(--accent)" : "var(--bg-deep)", stroke: selected ? "var(--accent)" : "var(--text-muted)",
        "stroke-width": "1", style: "cursor:move",
      });
      h.addEventListener("mousedown", (e) => {
        if (e.button !== 0) return;
        e.stopPropagation();
        if (e.ctrlKey) {
          if (!shape.controls) shape.controls = {};
          this.#bezierDragState = { shapeId: shape.id, vertexIdx: idx, handle: "out" };
        } else {
          // Track start + a movement flag so a click (no drag) selects the vertex for height editing.
          this.#vertexDragState = { shapeId: shape.id, vertexIdx: idx, sx: wx, sz: wz, moved: false };
        }
      });
      h.addEventListener("click", (e) => e.stopPropagation());
      this.#handlesLayer.appendChild(h);
    });
  }
}
