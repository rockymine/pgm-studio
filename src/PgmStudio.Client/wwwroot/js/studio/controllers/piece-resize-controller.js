/**
 * piece-resize-controller.js — a picked room region, control point, build area or water lane, resized on the
 * Layout canvas.
 *
 * A picked one wears the transform box every rectangle in the studio wears: a grip on each corner and on the
 * middle of each edge. Dragging one moves that side or corner by whole blocks, the opposite side held, down to
 * the least span the piece takes. The piece is the intent's, so nothing is kept here: the release hands the
 * resized rectangle to the host, which asks the piece route — the studio carries the marker, the building and
 * every image with it — and draws the pieces again.
 */

import { renderTransformBox, gripSideX, gripSideZ } from "../render/canvas-chrome.js";
import { toScreen } from "../geometry/transform.js";

/** The least span a resized room region takes — the least room (`WX2`); an area or a lane takes a block. */
export const MIN_ROOM_SPAN = 4;

const GRIP_HALF = 4;

/** Whether a structural shape is one this resizes: a room's region, a control point, a build area or a water
 *  lane, authored. A control point takes the longer side it is dragged to as its size. */
export function resizable(piece) {
  if (!piece || piece.carried) return false;
  return (!piece.piece && (piece.role === "spawn" || piece.role === "woolRoom"))
    || (piece.piece === true && (piece.role === "controlPoint" || piece.role === "buildArea" || piece.role === "waterLane"));
}

/** The rectangle `orig` with the side or corner `grip` names dragged to (wx, wz): whole blocks, the opposite
 *  side held, each axis kept at least `minSpan` across. */
export function resizedRect(orig, grip, wx, wz, minSpan = 1) {
  const next = { min_x: orig.min_x, min_z: orig.min_z, max_x: orig.max_x, max_z: orig.max_z };
  const x = Math.round(wx), z = Math.round(wz);
  const sideX = gripSideX(grip), sideZ = gripSideZ(grip);
  if (sideX < 0) next.min_x = Math.min(x, orig.max_x - minSpan);
  if (sideX > 0) next.max_x = Math.max(x, orig.min_x + minSpan);
  if (sideZ < 0) next.min_z = Math.min(z, orig.max_z - minSpan);
  if (sideZ > 0) next.max_z = Math.max(z, orig.min_z + minSpan);
  return next;
}

export class PieceResizeController {
  #layer;
  #getViewport;
  #getStructural;
  #callbacks;
  #enabled = false;
  #id = null;             // the picked piece's id, while it is one this resizes
  #drag = null;           // { grip, putBack, moved }

  /**
   * @param layer         SVGGElement — screen-space layer for the box
   * @param getViewport   () => { scale, panX, panY }
   * @param getStructural () => the canvas's structural shapes
   * @param callbacks     { onResized(piece, putBack), onPreview(), isEditable() }
   */
  constructor(layer, getViewport, getStructural, callbacks = {}) {
    this.#layer = layer;
    this.#getViewport = getViewport;
    this.#getStructural = getStructural;
    this.#callbacks = callbacks;
  }

  /** Whether a piece may be resized here — Layout, on a page the caller may write. */
  setEnabled(on) { this.#enabled = !!on; this.refresh(this.#id); }

  /** Follow the canvas's picked structural shape: one this resizes wears the transform box. */
  refresh(selectedId) {
    const editable = this.#callbacks.isEditable?.() ?? true;
    const piece = this.#enabled && editable ? this.#piece(selectedId) : null;
    this.#id = piece ? piece.id : null;
    while (this.#layer?.firstChild) this.#layer.removeChild(this.#layer.firstChild);
    if (!piece || !this.#layer) return;
    const viewport = this.#getViewport();
    const a = toScreen(piece.min_x, piece.min_z, viewport), b = toScreen(piece.max_x, piece.max_z, viewport);
    renderTransformBox(this.#layer, {
      l: Math.min(a.x, b.x), r: Math.max(a.x, b.x), t: Math.min(a.y, b.y), b: Math.max(a.y, b.y),
    }, {
      outline: false, gripHalf: GRIP_HALF,
      onScale: (grip, e) => {
        if (e.button !== 0) return;
        e.stopPropagation();
        this.grab(grip);
      },
    });
  }

  /** Take hold of the picked piece by one grip of its box — a corner or an edge — as a press on it does. */
  grab(grip) {
    const piece = this.#piece(this.#id);
    if (piece) this.#drag = { grip, putBack: structuredClone(piece), moved: false };
  }

  /** Document mousemove while a grip is held: the side follows the pointer. Returns true when consumed. */
  onHandleMove(wx, wz) {
    if (!this.#drag) return false;
    const piece = this.#piece(this.#id);
    if (!piece) { this.#drag = null; return false; }
    const minSpan = piece.piece ? 1 : MIN_ROOM_SPAN;
    const next = resizedRect(this.#drag.putBack, this.#drag.grip, wx, wz, minSpan);
    if (next.min_x === piece.min_x && next.min_z === piece.min_z && next.max_x === piece.max_x && next.max_z === piece.max_z)
      return true;
    Object.assign(piece, next);
    this.#drag.moved = true;
    this.refresh(this.#id);
    this.#callbacks.onPreview?.();
    return true;
  }

  /** Document mouseup ending a resize: one that moved is handed to the host with the piece as it stood. */
  onHandleUp() {
    if (!this.#drag) return false;
    const { putBack, moved } = this.#drag;
    this.#drag = null;
    const piece = this.#piece(this.#id);
    if (moved && piece) this.#callbacks.onResized?.(piece, putBack);
    return true;
  }

  #piece(id) {
    if (!id) return null;
    const piece = this.#getStructural().find(s => s.id === id);
    return resizable(piece) ? piece : null;
  }
}
