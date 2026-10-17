/**
 * wall-end-controller.js — a picked approach wall's two ends, dragged apart on the Layout canvas.
 *
 * A picked wall wears a square grip on each end of its line. Dragging one moves that end alone, by block
 * corners, and the end snaps to the nearest point on a drawn shape's outline within `END_SNAP_BLOCKS`, so a wall
 * run from shore to shore meets both. The wall is the intent's, so nothing is kept here: the release hands the
 * end and where it landed to the host, which asks the piece route and draws the pieces again.
 */

import { svgEl, handleRectAttrs } from "../render/svg.js";
import { toScreen } from "../geometry/transform.js";

/** How near a shape's outline, in blocks, a dragged end has to come to land on it. */
export const END_SNAP_BLOCKS = 3;

const GRIP_HALF = 4;

/** The nearest point to (x, z) on any of `rings`' edges within `reach`, or null where none is that near. */
export function nearestOnEdges(x, z, rings, reach) {
  let best = null;
  for (const ring of rings ?? []) {
    for (let i = 1; i < ring.length; i++) {
      const [ax, az] = ring[i - 1], [bx, bz] = ring[i];
      const dx = bx - ax, dz = bz - az;
      const lengthSq = dx * dx + dz * dz;
      const t = lengthSq === 0 ? 0 : Math.max(0, Math.min(1, ((x - ax) * dx + (z - az) * dz) / lengthSq));
      const px = ax + t * dx, pz = az + t * dz;
      const distance = Math.hypot(x - px, z - pz);
      if (distance <= reach && (!best || distance < best.distance)) best = { x: px, z: pz, distance };
    }
  }
  return best;
}

/** Where a wall's end dragged to (x, z) lands: the block corner nearest the shape edge it comes within reach
 *  of, else the block corner nearest the pointer. */
export function wallEndAt(x, z, rings, reach = END_SNAP_BLOCKS) {
  const edge = nearestOnEdges(x, z, rings, reach);
  return { x: Math.round(edge ? edge.x : x), z: Math.round(edge ? edge.z : z), snapped: !!edge };
}

export class WallEndController {
  #layer;
  #getViewport;
  #getStructural;
  #getRings;
  #callbacks;
  #enabled = false;
  #wall = null;           // the picked wall's id
  #drag = null;           // { second, putBack, moved }

  /**
   * @param layer         SVGGElement — screen-space layer for the grips
   * @param getViewport   () => { scale, panX, panY }
   * @param getStructural () => the canvas's structural shapes, the walls among them carrying `line`
   * @param getRings      () => the closed outline rings of the drawn shapes an end snaps to
   * @param callbacks     { onEdited(piece, part, x, z, putBack), onPreview(), isEditable() }
   */
  constructor(layer, getViewport, getStructural, getRings, callbacks = {}) {
    this.#layer = layer;
    this.#getViewport = getViewport;
    this.#getStructural = getStructural;
    this.#getRings = getRings;
    this.#callbacks = callbacks;
  }

  /** Whether a wall's ends may be dragged here — Layout, on a page the caller may write. */
  setEnabled(on) { this.#enabled = !!on; this.refresh(this.#wall); }

  /** Follow the canvas's picked structural shape: a wall shows its two end grips. */
  refresh(selectedId) {
    const editable = this.#callbacks.isEditable?.() ?? true;
    const piece = this.#enabled && editable ? this.#piece(selectedId) : null;
    this.#wall = piece ? piece.id : null;
    while (this.#layer?.firstChild) this.#layer.removeChild(this.#layer.firstChild);
    if (!piece || !this.#layer) return;
    const [x1, z1, x2, z2] = piece.line;
    [[x1, z1], [x2, z2]].forEach(([wx, wz], end) => {
      const sp = toScreen(wx, wz, this.#getViewport());
      const grip = svgEl("rect", {
        ...handleRectAttrs(sp.x, sp.y, GRIP_HALF),
        fill: "var(--bg-deep)", stroke: "var(--accent)", "stroke-width": "1.5", style: "cursor:move",
      });
      grip.addEventListener("mousedown", (e) => {
        if (e.button !== 0) return;
        e.stopPropagation();
        this.grab(end === 1);
      });
      grip.addEventListener("click", (e) => e.stopPropagation());
      this.#layer.appendChild(grip);
    });
  }

  /** Take hold of one end of the picked wall — its second where `second` — as a press on its grip does. */
  grab(second) {
    const piece = this.#piece(this.#wall);
    if (piece) this.#drag = { second, putBack: structuredClone(piece), moved: false };
  }

  /** Document mousemove while an end is held: the end follows the pointer, snapped. Returns true when consumed. */
  onHandleMove(wx, wz) {
    if (!this.#drag) return false;
    const piece = this.#piece(this.#wall);
    if (!piece) { this.#drag = null; return false; }
    const { x, z } = wallEndAt(wx, wz, this.#getRings());
    const [x1, z1, x2, z2] = piece.line;
    const line = this.#drag.second ? [x1, z1, x, z] : [x, z, x2, z2];
    if (line[0] === line[2] && line[1] === line[3]) return true;
    if (line.every((value, at) => value === piece.line[at])) return true;
    piece.line = line;
    Object.assign(piece, {
      min_x: Math.min(line[0], line[2]) - 1, max_x: Math.max(line[0], line[2]) + 1,
      min_z: Math.min(line[1], line[3]) - 1, max_z: Math.max(line[1], line[3]) + 1,
    });
    this.#drag.moved = true;
    this.refresh(this.#wall);
    this.#callbacks.onPreview?.();
    return true;
  }

  /** Document mouseup ending an end drag: one that moved is handed to the host with the wall as it stood. */
  onHandleUp() {
    if (!this.#drag) return false;
    const { second, putBack, moved } = this.#drag;
    this.#drag = null;
    const piece = this.#piece(this.#wall);
    if (moved && piece) {
      const x = second ? piece.line[2] : piece.line[0], z = second ? piece.line[3] : piece.line[1];
      this.#callbacks.onEdited?.(piece, second ? "end" : "start", x, z, putBack);
    }
    return true;
  }

  #piece(id) {
    if (!id) return null;
    const piece = this.#getStructural().find(s => s.id === id);
    return piece && Array.isArray(piece.line) && !piece.carried ? piece : null;
  }
}
