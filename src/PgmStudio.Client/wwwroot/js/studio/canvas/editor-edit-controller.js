/**
 * EditorEditController — region resize (8-handle drag) + arrow-key move for EditorCanvas. Extracted
 * from editor-canvas.js; mirrors EditorDrawController (state accessors + callbacks; the canvas forwards
 * its CanvasBase hooks into this controller).
 *
 * accessors:
 *   getSelected () => node | null       the currently-selected resizable region
 *   getOverlay  () => SVGGElement|null  the overlay layer (handles render here)
 *   getToWorld  () => Function|null     svg→world transform (rebuilt on repaint)
 *   getToSvg    () => Function|null     world→svg transform (rebuilt on repaint)
 *   getViewport () => {scale,panX,panY} pan/zoom for screen-space handle math
 *   clientToSvg (x,y) => {x,y}          client→svg point
 *   isVisible   () => bool              whether this canvas is the visible one (arrow-key guard)
 * callbacks:
 *   applyBounds (node, nb)     update a region's geometry live (no persist)
 *   saveBounds  (node, bounds) persist the final geometry (resize mouse-up / debounced nudge)
 *   setCursor   (cursor)       set the canvas cursor while a handle is grabbed
 *   afterResize ()             refresh cursor + overlay once a resize drag ends
 */

import { svgEl, handleRectAttrs } from "./transform.js";

const HANDLE_SIZE = 14;
const HANDLE_DEFS = [
  { key: "nw", pos: sb => [sb.left,  sb.top   ], cursor: "nw-resize" },
  { key: "n",  pos: sb => [sb.midX,  sb.top   ], cursor: "n-resize"  },
  { key: "ne", pos: sb => [sb.right, sb.top   ], cursor: "ne-resize" },
  { key: "w",  pos: sb => [sb.left,  sb.midY  ], cursor: "w-resize"  },
  { key: "e",  pos: sb => [sb.right, sb.midY  ], cursor: "e-resize"  },
  { key: "sw", pos: sb => [sb.left,  sb.bottom], cursor: "sw-resize" },
  { key: "s",  pos: sb => [sb.midX,  sb.bottom], cursor: "s-resize"  },
  { key: "se", pos: sb => [sb.right, sb.bottom], cursor: "se-resize" },
];

export const RESIZABLE_TYPES = new Set(["rectangle", "cuboid"]);

export class EditorEditController {
  #acc;
  #cb;
  #resizeState    = null;
  #nudgeSaveTimer = null;

  constructor(accessors, callbacks = {}) {
    this.#acc = accessors;
    this.#cb  = callbacks;
    this.#setupKeyboardNudge();
  }

  /** Draw the 8 resize handles for a resizable node into the overlay layer. */
  renderHandles(node) {
    const overlay = this.#acc.getOverlay();
    const sb = this.#screenBounds(node);
    if (!overlay || !sb) return;
    const hs = HANDLE_SIZE / 2;
    for (const h of HANDLE_DEFS) {
      const [cx, cy] = h.pos(sb);
      const el = svgEl("rect", {
        ...handleRectAttrs(cx, cy, hs), rx: 1,
        fill: "var(--canvas-handle-fill)", stroke: "var(--canvas-handle-stroke)", "stroke-width": "1.5", cursor: h.cursor,
      });
      el.addEventListener("mousedown", (e) => {
        if (e.button !== 0) return;
        e.stopPropagation(); e.preventDefault();
        const fields = this.#handleFields(h.key, this.#screenBounds(node));
        this.#resizeState = { node, ...fields, cursor: h.cursor };
        this.#cb.setCursor?.(h.cursor);
      });
      overlay.appendChild(el);
    }
  }

  /** Intercept mousemove during a handle drag — return true to consume the event (before pan logic). */
  onResizeMove(e) {
    if (!this.#resizeState) return false;
    this.#doResize(e.clientX, e.clientY);
    return true;
  }

  /** Intercept mouseup ending a handle drag — persist the final bounds. */
  onResizeUp(e) {
    if (!this.#resizeState || e.button !== 0) return false;
    this.#cb.saveBounds?.(this.#resizeState.node, { ...this.#resizeState.node.bounds });
    this.#resizeState = null;
    this.#cb.afterResize?.();
    return true;
  }

  /** Translate the selected region by (dx, dz) blocks — live in JS, then persist (debounced). */
  moveSelected(dx, dz) {
    const node = this.#acc.getSelected();
    if (!node?.bounds) return;
    const b = node.bounds;
    this.#cb.applyBounds?.(node, { min_x: b.min_x + dx, min_z: b.min_z + dz, max_x: b.max_x + dx, max_z: b.max_z + dz });
    clearTimeout(this.#nudgeSaveTimer);
    this.#nudgeSaveTimer = setTimeout(() => this.#cb.saveBounds?.(node, { ...node.bounds }), 200);
  }

  // ── internal ─────────────────────────────────────────────────────────────────

  // Arrow keys nudge the selected region by 1 block (Shift = 16). One document listener (this canvas is
  // shared by every host); guards keep only the visible canvas, with no text field focused, responding.
  #setupKeyboardNudge() {
    document.addEventListener("keydown", (e) => {
      if (!this.#acc.getSelected()?.bounds) return;          // only a single resizable region is selected
      if (!this.#acc.isVisible()) return;                     // this canvas isn't the visible one
      const tag = document.activeElement?.tagName;
      if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT") return;   // typing in a field
      const step = e.shiftKey ? 16 : 1;
      let dx = 0, dz = 0;
      switch (e.key) {
        case "ArrowLeft":  dx = -step; break;
        case "ArrowRight": dx =  step; break;
        case "ArrowUp":    dz = -step; break;
        case "ArrowDown":  dz =  step; break;
        default: return;
      }
      e.preventDefault();
      this.moveSelected(dx, dz);
    });
  }

  #doResize(clientX, clientY) {
    const toWorld = this.#acc.getToWorld();
    if (!this.#resizeState || !toWorld) return;
    const { node, xField, zField } = this.#resizeState;
    const svgPt = this.#acc.clientToSvg(clientX, clientY);
    const world = toWorld(svgPt.x, svgPt.y);
    const nb    = { ...node.bounds };
    if (xField) nb[xField] = Math.round(world.x);
    if (zField) nb[zField] = Math.round(world.z);
    if (xField && nb.max_x - nb.min_x < 1)
      nb[xField] = xField === "max_x" ? nb.min_x + 1 : nb.max_x - 1;
    if (zField && nb.max_z - nb.min_z < 1)
      nb[zField] = zField === "max_z" ? nb.min_z + 1 : nb.max_z - 1;
    // Live update in JS (hot path) — the shape, anchors and overlay follow the cursor; the final
    // bounds are persisted to the host only on mouse-up.
    this.#cb.applyBounds?.(node, nb);
  }

  #screenBounds(node) {
    const toSvg = this.#acc.getToSvg();
    if (!node?.bounds || !toSvg) return null;
    const { scale, panX, panY } = this.#acc.getViewport();
    const { min_x, min_z, max_x, max_z } = node.bounds;
    const toScr = (bx, by) => ({ x: bx * scale + panX, y: by * scale + panY });
    const p1b = toSvg(min_x, min_z), p2b = toSvg(max_x, max_z);
    const s1  = toScr(p1b.x, p1b.y), s2 = toScr(p2b.x, p2b.y);
    return {
      left: Math.min(s1.x, s2.x), right: Math.max(s1.x, s2.x),
      top:  Math.min(s1.y, s2.y), bottom: Math.max(s1.y, s2.y),
      midX: (s1.x + s2.x) / 2,    midY: (s1.y + s2.y) / 2,
      leftIsMinX: p1b.x <= p2b.x,  topIsMinZ: p1b.y <= p2b.y,
    };
  }

  #handleFields(key, sb) {
    const lF = sb.leftIsMinX ? "min_x" : "max_x", rF = sb.leftIsMinX ? "max_x" : "min_x";
    const tF = sb.topIsMinZ  ? "min_z" : "max_z", bF = sb.topIsMinZ  ? "max_z" : "min_z";
    return { nw:{xField:lF,zField:tF}, n:{xField:null,zField:tF}, ne:{xField:rF,zField:tF},
              w:{xField:lF,zField:null}, e:{xField:rF,zField:null},
             sw:{xField:lF,zField:bF}, s:{xField:null,zField:bF}, se:{xField:rF,zField:bF} }[key];
  }
}
