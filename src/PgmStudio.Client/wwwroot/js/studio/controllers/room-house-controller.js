/**
 * room-house-controller.js — a room's house, shaped on the Layout canvas the way Decoration shapes a dressed one.
 *
 * A picked room building wears square grips on the corners of every wing of its house; dragging one resizes that
 * wing, held to the least span a wing takes and to the room's region. Near the middle of a free wall the pointer
 * shows the insert marker, and a press there adds a wing along that wall, five deep and shrunk to fit the region
 * (`shared/building.js`). The house is the intent's, so nothing is kept here: a finished resize or an added wing
 * hands the room's wing rectangles to the host, which writes the house and draws the rooms again.
 *
 * Wings are read off the canvas's structural shapes — `{room}-building` for the room's own wing, then
 * `{room}-building-2` on — whose bounds are max-exclusive, and handed back the same way.
 */

import { MIN_FOOTPRINT_SPAN, wallNear, wingOffWall } from "../shared/building.js";
import { svgEl, handleRectAttrs, insertGhost } from "../render/svg.js";
import { toScreen } from "../geometry/transform.js";

// Screen px: the corner grips match a dressed building's, and the marker shows at the outline editor's distance.
const GRIP_HALF = 4;
const WALL_REACH = 10;

/** The room a building shape is a wing of, and which wing: `spawn-red-building-2` → `{ room: "spawn-red", wing: 1 }`. */
export function wingOf(id) {
  const found = /-building(?:-(\d+))?$/.exec(id ?? "");
  return found ? { room: id.slice(0, found.index), wing: found[1] ? Number(found[1]) - 1 : 0 } : null;
}

export class RoomHouseController {
  #layer;
  #getViewport;
  #getStructural;
  #callbacks;
  #enabled = false;
  #room = null;           // the picked room's id, while one of its wings is picked
  #drag = null;           // { wing, corner, moved } — corner 0 the min corner, 3 the max, 1 and 2 the others
  #wall = null;
  #ghost = null;

  /**
   * @param layer         SVGGElement — screen-space layer for the grips and the marker
   * @param getViewport   () => { scale, panX, panY }
   * @param getStructural () => the canvas's structural shapes, rooms and their wings among them
   * @param callbacks     { onEdited(room, wings), onRefused(sentence), onPreview(), isEditable() }
   */
  constructor(layer, getViewport, getStructural, callbacks = {}) {
    this.#layer = layer;
    this.#getViewport = getViewport;
    this.#getStructural = getStructural;
    this.#callbacks = callbacks;
  }

  /** Whether a room's house may be shaped here — Layout, on a page the caller may write. */
  setEnabled(on) { this.#enabled = !!on; this.refresh(this.#room ? `${this.#room}-building` : null); }

  /** Follow the canvas's picked structural shape: a wing of a room picks that room's house. */
  refresh(selectedId) {
    const editable = this.#callbacks.isEditable?.() ?? true;
    this.#room = this.#enabled && editable ? wingOf(selectedId)?.room ?? null : null;
    this.#wall = null;
    this.#ghost = null;
    while (this.#layer?.firstChild) this.#layer.removeChild(this.#layer.firstChild);
    if (!this.#room || !this.#layer) return;
    this.#wings().forEach((wing, at) => {
      corners(wing).forEach(([wx, wz], corner) => {
        const sp = toScreen(wx, wz, this.#getViewport());
        const grip = svgEl("rect", {
          ...handleRectAttrs(sp.x, sp.y, GRIP_HALF),
          fill: "var(--bg-deep)", stroke: "var(--accent)", "stroke-width": "1.5", style: "cursor:nwse-resize",
        });
        grip.addEventListener("mousedown", (e) => {
          if (e.button !== 0) return;
          e.stopPropagation();
          this.#drag = { wing: at, corner, moved: false };
        });
        grip.addEventListener("click", (e) => e.stopPropagation());
        this.#layer.appendChild(grip);
      });
    });
  }

  /** Pointer move in world coordinates: show the marker on the free wall the pointer is near, or hide it. */
  onPointerHover(wx, wz) {
    if (!this.#room || this.#drag) return;
    const viewport = this.#getViewport();
    const wall = wallNear(this.#wings().map(inclusive), wx, wz, WALL_REACH / (viewport.scale || 1));
    this.#wall = wall;
    if (!this.#layer) return;
    if (!wall) { if (this.#ghost) this.#ghost.style.display = "none"; return; }
    if (!this.#ghost || !this.#ghost.isConnected) {
      this.#ghost = insertGhost(() => this.addWing());
      this.#layer.appendChild(this.#ghost);
    }
    const sp = toScreen(wall.mid[0], wall.mid[1], viewport);
    this.#ghost.setAttribute("cx", String(sp.x));
    this.#ghost.setAttribute("cy", String(sp.y));
    this.#ghost.style.display = "";
  }

  /** Add a wing off the wall the marker stands on, shrunk to the room's region; say why where none fits. */
  addWing() {
    if (!this.#room || !this.#wall) return;
    const wings = this.#wings();
    const region = this.#region();
    const rect = wingOffWall(wings.map(inclusive), this.#wall, region ? inclusive(region) : null);
    if (!rect) { this.#callbacks.onRefused?.("There is no room for a wing off that wall."); return; }
    this.#callbacks.onEdited?.(this.#room, [...wings, exclusive(rect)]);
  }

  /** Document mousemove while a corner grip is held. Returns true when consumed. */
  onHandleMove(wx, wz) {
    if (!this.#drag) return false;
    const piece = this.#piece(this.#drag.wing);
    if (!piece) { this.#drag = null; return false; }
    const x = Math.round(wx), z = Math.round(wz);
    const region = this.#region();
    const left = this.#drag.corner % 2 === 0, top = this.#drag.corner < 2;
    const next = { ...piece };
    if (left) next.min_x = Math.min(x, piece.max_x - MIN_FOOTPRINT_SPAN); else next.max_x = Math.max(x, piece.min_x + MIN_FOOTPRINT_SPAN);
    if (top) next.min_z = Math.min(z, piece.max_z - MIN_FOOTPRINT_SPAN); else next.max_z = Math.max(z, piece.min_z + MIN_FOOTPRINT_SPAN);
    if (region) {
      next.min_x = Math.max(next.min_x, region.min_x); next.max_x = Math.min(next.max_x, region.max_x);
      next.min_z = Math.max(next.min_z, region.min_z); next.max_z = Math.min(next.max_z, region.max_z);
    }
    if (next.max_x - next.min_x < MIN_FOOTPRINT_SPAN || next.max_z - next.min_z < MIN_FOOTPRINT_SPAN) return true;
    Object.assign(piece, { min_x: next.min_x, min_z: next.min_z, max_x: next.max_x, max_z: next.max_z });
    this.#drag.moved = true;
    this.refresh(`${this.#room}-building`);
    this.#callbacks.onPreview?.();
    return true;
  }

  /** Document mouseup ending a corner drag: a resize that moved is handed to the host. */
  onHandleUp() {
    if (!this.#drag) return false;
    const moved = this.#drag.moved;
    this.#drag = null;
    if (moved) this.#callbacks.onEdited?.(this.#room, this.#wings());
    return true;
  }

  #piece(wing) {
    const id = wing === 0 ? `${this.#room}-building` : `${this.#room}-building-${wing + 1}`;
    return this.#getStructural().find(s => s.id === id) ?? null;
  }

  #wings() {
    const wings = [];
    for (let at = 0; ; at++) {
      const piece = this.#piece(at);
      if (!piece) return wings;
      wings.push({ min_x: piece.min_x, min_z: piece.min_z, max_x: piece.max_x, max_z: piece.max_z });
    }
  }

  #region() {
    const region = this.#getStructural().find(s => s.id === this.#room);
    return region ? { min_x: region.min_x, min_z: region.min_z, max_x: region.max_x, max_z: region.max_z } : null;
  }
}

function corners(r) {
  return [[r.min_x, r.min_z], [r.max_x, r.min_z], [r.min_x, r.max_z], [r.max_x, r.max_z]];
}

/** A max-exclusive shape rectangle as the inclusive cell rect the building law reads. */
function inclusive(r) {
  return { minX: r.min_x, minZ: r.min_z, maxX: r.max_x - 1, maxZ: r.max_z - 1 };
}

function exclusive(r) {
  return { min_x: r.minX, min_z: r.minZ, max_x: r.maxX + 1, max_z: r.maxZ + 1 };
}
