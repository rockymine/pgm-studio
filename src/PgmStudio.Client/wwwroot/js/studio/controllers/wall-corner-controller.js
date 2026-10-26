/**
 * wall-corner-controller.js — a picked approach wall's corners, dragged, added and taken out on the Layout canvas.
 *
 * A picked wall wears a square grip on each corner of its line. Dragging one moves that corner alone, by block
 * corners, and it snaps to the nearest point on a drawn shape's outline within `END_SNAP_BLOCKS`, so a wall run from
 * shore to shore meets both. A shift-press on a leg adds a corner there, and one on a corner takes it out while two
 * stay. The wall is the intent's, so nothing is kept here: every change hands the host the whole line and the wall
 * as it stood, and the host asks the piece route and draws the pieces again.
 */

import { svgEl, handleRectAttrs } from "../render/svg.js";
import { toScreen } from "../geometry/transform.js";

/** How near a shape's outline, in blocks, a dragged corner has to come to land on it. */
export const END_SNAP_BLOCKS = 3;

/** How near a leg, in blocks, a shift-press has to land to add a corner to it. */
export const LEG_REACH_BLOCKS = 1.5;

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
      if (distance <= reach && (!best || distance < best.distance)) best = { x: px, z: pz, distance, edge: i };
    }
  }
  return best;
}

/** Where a wall's corner dragged to (x, z) lands: the block corner nearest the shape edge it comes within reach
 *  of, else the block corner nearest the pointer. */
export function wallEndAt(x, z, rings, reach = END_SNAP_BLOCKS) {
  const edge = nearestOnEdges(x, z, rings, reach);
  return { x: Math.round(edge ? edge.x : x), z: Math.round(edge ? edge.z : z), snapped: !!edge };
}

/** The line `points` with a corner put into the leg a press at (x, z) lands within `reach` of, at the block corner
 *  nearest the point on the leg under it, or null where it lands on none or on a corner already there. */
export function withCorner(points, x, z, reach = LEG_REACH_BLOCKS) {
  const leg = nearestOnEdges(x, z, [points], reach);
  if (!leg) return null;
  const corner = [Math.round(leg.x), Math.round(leg.z)];
  if (points.some(([px, pz]) => px === corner[0] && pz === corner[1])) return null;
  return [...points.slice(0, leg.edge), corner, ...points.slice(leg.edge)];
}

export class WallCornerController {
  #layer;
  #getViewport;
  #getStructural;
  #getRings;
  #callbacks;
  #enabled = false;
  #wall = null;           // the picked wall's id
  #drag = null;           // { corner, putBack, moved }

  /**
   * @param layer         SVGGElement — screen-space layer for the grips
   * @param getViewport   () => { scale, panX, panY }
   * @param getStructural () => the canvas's structural shapes, the walls among them carrying `line`
   * @param getRings      () => the closed outline rings of the drawn shapes a corner snaps to
   * @param callbacks     { onEdited(piece, points, putBack), onPreview(), isEditable() }
   */
  constructor(layer, getViewport, getStructural, getRings, callbacks = {}) {
    this.#layer = layer;
    this.#getViewport = getViewport;
    this.#getStructural = getStructural;
    this.#getRings = getRings;
    this.#callbacks = callbacks;
  }

  /** Whether a wall's corners may be changed here — Layout, on a page the caller may write. */
  setEnabled(on) { this.#enabled = !!on; this.refresh(this.#wall); }

  /** Follow the canvas's picked structural shape: a wall shows a grip on every corner. */
  refresh(selectedId) {
    const editable = this.#callbacks.isEditable?.() ?? true;
    const piece = this.#enabled && editable ? this.#piece(selectedId) : null;
    this.#wall = piece ? piece.id : null;
    while (this.#layer?.firstChild) this.#layer.removeChild(this.#layer.firstChild);
    if (!piece || !this.#layer) return;
    piece.line.forEach(([wx, wz], corner) => {
      const sp = toScreen(wx, wz, this.#getViewport());
      const grip = svgEl("rect", {
        ...handleRectAttrs(sp.x, sp.y, GRIP_HALF),
        fill: "var(--bg-deep)", stroke: "var(--accent)", "stroke-width": "1.5", style: "cursor:move",
      });
      grip.addEventListener("mousedown", (e) => {
        if (e.button !== 0) return;
        e.stopPropagation();
        if (e.shiftKey) this.remove(corner);
        else this.grab(corner);
      });
      grip.addEventListener("click", (e) => e.stopPropagation());
      this.#layer.appendChild(grip);
    });
  }

  /** Take hold of the picked wall's corner `corner`, counted from its start, as a press on its grip does. */
  grab(corner) {
    const piece = this.#piece(this.#wall);
    if (piece) this.#drag = { corner, putBack: structuredClone(piece), moved: false };
  }

  /** Take the picked wall's corner `corner` out, as a shift-press on its grip does, while two stay. */
  remove(corner) {
    const piece = this.#piece(this.#wall);
    if (!piece || piece.line.length <= 2) return false;
    const putBack = structuredClone(piece);
    this.#reshape(piece, piece.line.filter((_, at) => at !== corner));
    this.#callbacks.onEdited?.(piece, piece.line, putBack);
    return true;
  }

  /** A shift-press at (x, z): a corner added to the picked wall's leg it lands on. Returns true when it did. */
  addAt(x, z) {
    const piece = this.#piece(this.#wall);
    const line = piece ? withCorner(piece.line, x, z) : null;
    if (!line) return false;
    const putBack = structuredClone(piece);
    this.#reshape(piece, line);
    this.#callbacks.onEdited?.(piece, piece.line, putBack);
    return true;
  }

  /** Document mousemove while a corner is held: the corner follows the pointer, snapped. Returns true when
   *  consumed. */
  onHandleMove(wx, wz) {
    if (!this.#drag) return false;
    const piece = this.#piece(this.#wall);
    if (!piece) { this.#drag = null; return false; }
    const { x, z } = wallEndAt(wx, wz, this.#getRings());
    const { corner } = this.#drag;
    const line = piece.line.map((point, at) => at === corner ? [x, z] : point);
    const beside = [line[corner - 1], line[corner + 1]].filter(Boolean);
    if (beside.some(([px, pz]) => px === x && pz === z)) return true;
    if (line[corner][0] === piece.line[corner][0] && line[corner][1] === piece.line[corner][1]) return true;
    this.#reshape(piece, line);
    this.#drag.moved = true;
    this.#callbacks.onPreview?.();
    return true;
  }

  /** Document mouseup ending a corner drag: one that moved is handed to the host with the wall as it stood. */
  onHandleUp() {
    if (!this.#drag) return false;
    const { putBack, moved } = this.#drag;
    this.#drag = null;
    const piece = this.#piece(this.#wall);
    if (moved && piece) this.#callbacks.onEdited?.(piece, piece.line, putBack);
    return true;
  }

  // The picked wall drawn through `line`, its box the band's bounds, and its grips where its corners now stand.
  #reshape(piece, line) {
    piece.line = line;
    const xs = line.map(([x]) => x), zs = line.map(([, z]) => z);
    Object.assign(piece, {
      min_x: Math.min(...xs) - 1, max_x: Math.max(...xs) + 1, min_z: Math.min(...zs) - 1, max_z: Math.max(...zs) + 1,
    });
    this.refresh(this.#wall);
  }

  #piece(id) {
    if (!id) return null;
    const piece = this.#getStructural().find(s => s.id === id);
    return piece && Array.isArray(piece.line) && !piece.carried ? piece : null;
  }
}
