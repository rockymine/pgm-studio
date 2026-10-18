/**
 * room-house-controller.js — a room's house, shaped on the Layout canvas the way Decoration shapes a dressed one.
 *
 * A picked room building wears square grips on the corners of every wing of its house; dragging one resizes that
 * wing, held to the least span a wing takes and to the room's region. Near the middle of a free wall the pointer
 * shows the insert marker, and a press there adds a wing along that wall, five deep and shrunk to fit the region
 * (`shared/building.js`). The house is the intent's, so nothing is kept here: a finished resize or an added wing
 * hands the room's wing rectangles to the host, which writes the house and draws the rooms again.
 *
 * The room's doors are drawn on the outline, each a short bar where it is cut, and a shift-press on a free wall
 * adds one there or takes away the one already there; the whole list goes to the host, which writes the doors. The
 * host hands the doors in as the room is built with them, so a wool stating none shows the one it is given.
 *
 * Wings are read off the canvas's structural shapes — `{room}-building` for the room's own wing, then
 * `{room}-building-2` on — whose bounds are max-exclusive, and handed back the same way.
 */

import { MIN_FOOTPRINT_SPAN, freeWalls, wallNear, wingOffWall } from "../shared/building.js";
import { svgEl, handleRectAttrs, insertGhost } from "../render/svg.js";
import { toScreen } from "../geometry/transform.js";

// Screen px: the corner grips match a dressed building's, and the marker shows at the outline editor's distance.
const GRIP_HALF = 4;
const WALL_REACH = 10;
// Blocks: a door's bar is about a door wide, and a shift-press this close along its wall takes the door away.
const DOOR_HALF = 1.5;
const DOOR_REACH = 2;

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
  #hover = null;          // the world point the pointer last stood at
  #doors = null;          // { room, doors } — the doors the picked room is built with, as the host read them

  /**
   * @param layer         SVGGElement — screen-space layer for the grips and the marker
   * @param getViewport   () => { scale, panX, panY }
   * @param getStructural () => the canvas's structural shapes, rooms and their wings among them
   * @param callbacks     { onEdited(room, wings), onDoorsEdited(room, doors), onRefused(sentence), onPreview(),
   *                        isEditable() }
   */
  constructor(layer, getViewport, getStructural, callbacks = {}) {
    this.#layer = layer;
    this.#getViewport = getViewport;
    this.#getStructural = getStructural;
    this.#callbacks = callbacks;
  }

  /** Whether a room's house may be shaped here — Layout, on a page the caller may write. */
  setEnabled(on) { this.#enabled = !!on; this.refresh(this.#room ? `${this.#room}-building` : null); }

  /** The doors `room` is built with — `[{edge, x?, z?}]`, a door with no point centred on its wall. */
  setDoors(room, doors) {
    this.#doors = room ? { room, doors: doors ?? [] } : null;
    this.refresh(this.#room ? `${this.#room}-building` : null);
  }

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
    const wings = this.#wings();
    if (this.#doors?.room !== this.#room || wings.length === 0) return;
    for (const door of this.#doors.doors) {
      const at = doorPoint(door, wings);
      if (!at) continue;
      const alongX = !door.edge.endsWith("x");
      const [ax, az] = alongX ? [at.x - DOOR_HALF, at.z] : [at.x, at.z - DOOR_HALF];
      const [bx, bz] = alongX ? [at.x + DOOR_HALF, at.z] : [at.x, at.z + DOOR_HALF];
      const a = toScreen(ax, az, this.#getViewport()), b = toScreen(bx, bz, this.#getViewport());
      const bar = svgEl("line", {
        x1: a.x, y1: a.y, x2: b.x, y2: b.y, stroke: "var(--accent)", "stroke-width": "4", "stroke-linecap": "round",
        "pointer-events": "none",
      });
      bar.dataset.door = door.edge;
      this.#layer.appendChild(bar);
    }
  }

  /** A shift-press at (wx, wz): add a door on the free wall there, or take away the one already there. Returns
   *  true when the press was about a door. */
  toggleDoorAt(wx, wz) {
    if (!this.#room || this.#doors?.room !== this.#room) return false;
    const viewport = this.#getViewport();
    const wings = this.#wings();
    const wall = wallNear(wings.map(inclusive), wx, wz, WALL_REACH / (viewport.scale || 1));
    if (!wall) return false;
    const next = doorToggled(this.#doors.doors, doorOnWall(wall, wx, wz), wings);
    if (next.length === 0) { this.#callbacks.onRefused?.("A room keeps at least one door."); return true; }
    this.#callbacks.onDoorsEdited?.(this.#room, next);
    return true;
  }

  /** Pointer move in world coordinates: show the marker on the free wall the pointer is near, or hide it. */
  onPointerHover(wx, wz) {
    this.#hover = [wx, wz];
    if (!this.#room || this.#drag) return;
    const viewport = this.#getViewport();
    const wall = wallNear(this.#wings().map(inclusive), wx, wz, WALL_REACH / (viewport.scale || 1));
    this.#wall = wall;
    if (!this.#layer) return;
    if (!wall) { if (this.#ghost) this.#ghost.style.display = "none"; return; }
    if (!this.#ghost || !this.#ghost.isConnected) {
      this.#ghost = insertGhost((e) => (e?.shiftKey && this.#hover ? this.toggleDoorAt(...this.#hover) : this.addWing()));
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

/** The door a press at (wx, wz) on `wall` states: on the wall's outer edge, centred on the block pressed. */
export function doorOnWall(wall, wx, wz) {
  const alongX = !wall.facing.endsWith("x");
  const edge = wall.facing[0] === "-" ? wall.line + 1 : wall.line;
  const along = Math.min(Math.max(Math.floor(alongX ? wx : wz), wall.lo), wall.hi) + 0.5;
  return alongX ? { edge: wall.facing, x: along, z: edge } : { edge: wall.facing, x: edge, z: along };
}

/** Where a door is cut on the outline of a house of `wings` (max-exclusive, the room's first): on the free wall
 *  facing its way whose run holds its place along it, the one nearest the line it was stated on, as the stamper
 *  takes it. A door stating no point stands at the middle of the room's wing's side; one no free wall holds stands
 *  where it was stated. */
export function doorPoint(door, wings) {
  const room = wings[0];
  const stated = door.x != null && door.z != null ? { x: door.x, z: door.z }
    : door.edge === "-z" ? { x: (room.min_x + room.max_x) / 2, z: room.min_z }
    : door.edge === "+z" ? { x: (room.min_x + room.max_x) / 2, z: room.max_z }
    : door.edge === "-x" ? { x: room.min_x, z: (room.min_z + room.max_z) / 2 }
    : door.edge === "+x" ? { x: room.max_x, z: (room.min_z + room.max_z) / 2 }
    : null;
  if (!stated) return null;
  const alongX = !door.edge.endsWith("x");
  const along = alongX ? stated.x : stated.z, across = alongX ? stated.z : stated.x;
  let best = null, bestDistance = Infinity;
  for (const wall of freeWalls(wings.map(inclusive))) {
    if (wall.facing !== door.edge || along < wall.lo || along > wall.hi + 1) continue;
    const line = wall.facing[0] === "-" ? wall.line + 1 : wall.line;
    if (Math.abs(line - across) < bestDistance) { best = line; bestDistance = Math.abs(line - across); }
  }
  if (best === null) return stated;
  return alongX ? { x: along, z: best } : { x: best, z: along };
}

/** The doors with `door` added, or with the one cut on its wall within `DOOR_REACH` blocks of it taken away. */
export function doorToggled(doors, door, wings) {
  const alongX = !door.edge.endsWith("x");
  const pressed = doorPoint(door, wings);
  const near = doors.findIndex(other => {
    if (other.edge !== door.edge) return false;
    const at = doorPoint(other, wings);
    if (!at || !pressed) return false;
    const across = alongX ? Math.abs(at.z - pressed.z) : Math.abs(at.x - pressed.x);
    const along = alongX ? Math.abs(at.x - pressed.x) : Math.abs(at.z - pressed.z);
    return across < 0.5 && along <= DOOR_REACH;
  });
  return near >= 0 ? doors.filter((_, at) => at !== near) : [...doors, door];
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
