/**
 * room-house-controller.js — a room's house, shaped on the Layout canvas the way Decoration shapes a dressed one.
 *
 * A picked wing of a room's house wears the transform box every rectangle in the studio wears — a grip on each
 * corner and a handle along each edge — and dragging one resizes that wing alone, held to the least span a wing
 * takes and to the room's region; a press inside another wing picks it. Near the middle of a free wall the pointer
 * shows the insert marker, and a press there adds a wing along that wall, five deep and shrunk to fit the region
 * (`shared/building.js`). The house is the intent's, so nothing is kept here: a finished resize or an added wing
 * hands the room's wing rectangles to the host, which writes the house and draws the rooms again.
 *
 * The room's doors are drawn on the outline, each a short bar where it is cut, and a shift-press on a free wall
 * adds one there or takes away the one already there; the whole list goes to the host, which writes the doors. The
 * host hands the doors in as the room is built with them, so a wool stating none shows the one it is given.
 *
 * The room's chests (a wool room) and monument seats (a spawn room) are drawn inside its first wing as the host reads
 * them built, a small square or ring on each block, and the seats past the wools the room seats are drawn hollow.
 * Each drags by whole blocks, held to the floor inside the walls and off the pad and off the others; the release
 * hands the whole list, as offsets from the first wing's lowest corner, to the host, which writes it on the house —
 * so a room stating none states every derived cell, the dragged one moved.
 *
 * Wings are read off the canvas's structural shapes — `{room}-building` for the room's own wing, then
 * `{room}-building-2` on — whose bounds are max-exclusive, and handed back the same way.
 */

import { MIN_FOOTPRINT_SPAN, freeWalls, wallNear, wingOffWall } from "../shared/building.js";
import { svgEl, insertGhost } from "../render/svg.js";
import { renderTransformBox } from "../render/canvas-chrome.js";
import { toScreen } from "../geometry/transform.js";
import { resizedRect } from "./piece-resize-controller.js";
import { wingOf } from "../shared/pick.js";

// Screen px: the corner grips match a dressed building's, and the marker shows at the outline editor's distance.
const GRIP_HALF = 4;
const WALL_REACH = 10;
// Blocks: a door's bar is about a door wide, and a shift-press this close along its wall takes the door away.
const DOOR_HALF = 1.5;
const DOOR_REACH = 2;
// Screen px: the least a chest's or a seat's marker is drawn across, however far out the board is zoomed.
const CELL_MARKER_MIN = 6;

export class RoomHouseController {
  #layer;
  #getViewport;
  #getStructural;
  #callbacks;
  #enabled = false;
  #room = null;           // the picked room's id, while one of its wings is picked
  #wing = 0;              // which wing of it is picked, counted from 0
  #drag = null;           // { wing, grip, putBack, moved } — grip a corner or an edge of the transform box
  #wall = null;
  #ghost = null;
  #hover = null;          // the world point the pointer last stood at
  #doors = null;          // { room, doors } — the doors the picked room is built with, as the host read them
  #interior = null;       // { room, corner, floor, pad, chests, monuments, seated } — its chests and seats, as built

  /**
   * @param layer         SVGGElement — screen-space layer for the grips and the marker
   * @param getViewport   () => { scale, panX, panY }
   * @param getStructural () => the canvas's structural shapes, rooms and their wings among them
   * @param callbacks     { onEdited(room, wings), onDoorsEdited(room, doors), onInteriorEdited(room, field, cells,
   *                        wings), onRefused(sentence), onPreview(), isEditable() }
   */
  constructor(layer, getViewport, getStructural, callbacks = {}) {
    this.#layer = layer;
    this.#getViewport = getViewport;
    this.#getStructural = getStructural;
    this.#callbacks = callbacks;
  }

  /** Whether a room's house may be shaped here — Layout, on a page the caller may write. */
  setEnabled(on) { this.#enabled = !!on; this.refresh(this.#picked()); }

  /** The doors `room` is built with — `[{edge, x?, z?}]`, a door with no point centred on its wall. */
  setDoors(room, doors) {
    this.#doors = room ? { room, doors: doors ?? [] } : null;
    this.refresh(this.#picked());
  }

  /** The chests and seats `room` is built with — the interior read `{corner, floor, pad, chests, monuments, seated}`,
   *  each cell `[dx, dz]` from `corner`. */
  setInterior(room, interior) {
    this.#interior = room && interior ? { room, ...structuredClone(interior) } : null;
    this.refresh(this.#picked());
  }

  /** Follow the canvas's picked structural shape: a wing of a room picks that room's house. */
  refresh(selectedId) {
    const editable = this.#callbacks.isEditable?.() ?? true;
    const picked = this.#enabled && editable ? wingOf(selectedId) : null;
    this.#room = picked?.room ?? null;
    this.#wing = picked?.wing ?? 0;
    this.#wall = null;
    this.#ghost = null;
    while (this.#layer?.firstChild) this.#layer.removeChild(this.#layer.firstChild);
    if (!this.#room || !this.#layer) return;
    const wings = this.#wings();
    const wing = wings[this.#wing];
    if (wing) {
      const viewport = this.#getViewport();
      const a = toScreen(wing.min_x, wing.min_z, viewport), b = toScreen(wing.max_x, wing.max_z, viewport);
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
    this.#renderInterior();
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

  /** Each chest and seat of the picked room, a marker on its block that a press takes hold of. */
  #renderInterior() {
    const interior = this.#interior;
    if (!interior || interior.room !== this.#room || !this.#layer) return;
    const viewport = this.#getViewport();
    const half = Math.max(CELL_MARKER_MIN / 2, (viewport.scale || 1) * 0.35);
    for (const field of INTERIOR_FIELDS) {
      interior[field].forEach((cell, index) => {
        const at = toScreen(interior.corner[0] + cell[0] + 0.5, interior.corner[1] + cell[1] + 0.5, viewport);
        const seated = field === "chests" || index < interior.seated;
        const marker = field === "chests"
          ? svgEl("rect", { x: at.x - half, y: at.y - half, width: 2 * half, height: 2 * half, rx: "1" })
          : svgEl("circle", { cx: at.x, cy: at.y, r: half });
        marker.setAttribute("fill", seated ? "var(--accent)" : "none");
        marker.setAttribute("stroke", "var(--accent)");
        marker.setAttribute("stroke-width", "1.5");
        // A hollow seat is pressed anywhere inside its ring, not on the ring's stroke alone.
        marker.setAttribute("pointer-events", "all");
        marker.style.cursor = "move";
        marker.dataset[field === "chests" ? "chest" : "seat"] = String(index);
        marker.addEventListener("mousedown", (e) => {
          if (e.button !== 0) return;
          e.stopPropagation();
          this.grabCell(field, index);
        });
        marker.addEventListener("click", (e) => e.stopPropagation());
        this.#layer.appendChild(marker);
      });
    }
  }

  /** Take hold of chest or seat `index` of the picked room — `field` is `chests` or `monuments`. */
  grabCell(field, index) {
    const interior = this.#interior;
    if (!interior || interior.room !== this.#room || !interior[field]?.[index]) return;
    this.#drag = { cell: { field, index }, putBack: structuredClone(interior[field]), moved: false };
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

  /** Take hold of the picked wing by one grip of its box — a corner or an edge — as a press on it does. */
  grab(grip) {
    const piece = this.#piece(this.#wing);
    if (piece) this.#drag = { wing: this.#wing, grip, putBack: structuredClone(piece), moved: false };
  }

  /** Document mousemove while a grip is held. Returns true when consumed. */
  onHandleMove(wx, wz) {
    if (!this.#drag) return false;
    if (this.#drag.cell) {
      const { field, index } = this.#drag.cell;
      const next = cellMoved(this.#interior, field, index, wx, wz);
      if (next) {
        this.#interior[field] = next;
        this.#drag.moved = true;
        this.refresh(this.#picked());
      }
      return true;
    }
    const piece = this.#piece(this.#drag.wing);
    if (!piece) { this.#drag = null; return false; }
    const region = this.#region();
    const next = resizedRect(this.#drag.putBack, this.#drag.grip, wx, wz, MIN_FOOTPRINT_SPAN);
    if (region) {
      next.min_x = Math.max(next.min_x, region.min_x); next.max_x = Math.min(next.max_x, region.max_x);
      next.min_z = Math.max(next.min_z, region.min_z); next.max_z = Math.min(next.max_z, region.max_z);
    }
    if (next.max_x - next.min_x < MIN_FOOTPRINT_SPAN || next.max_z - next.min_z < MIN_FOOTPRINT_SPAN) return true;
    Object.assign(piece, { min_x: next.min_x, min_z: next.min_z, max_x: next.max_x, max_z: next.max_z });
    this.#drag.moved = true;
    this.refresh(this.#picked());
    this.#callbacks.onPreview?.();
    return true;
  }

  /** Document mouseup ending a corner drag: a resize that moved is handed to the host. */
  onHandleUp() {
    if (!this.#drag) return false;
    const { moved, cell } = this.#drag;
    this.#drag = null;
    if (moved && cell) this.#callbacks.onInteriorEdited?.(this.#room, cell.field, structuredClone(this.#interior[cell.field]), this.#wings());
    else if (moved) this.#callbacks.onEdited?.(this.#room, this.#wings());
    return true;
  }

  /** The id of the picked wing's shape, or null where none is picked. */
  #picked() {
    if (!this.#room) return null;
    return this.#wing === 0 ? `${this.#room}-building` : `${this.#room}-building-${this.#wing + 1}`;
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

/** The two lists of cells a room's house states, as its interior read names them. */
const INTERIOR_FIELDS = ["chests", "monuments"];

/** The cells of `field` with cell `index` moved onto the block under (wx, wz), or null where that block is off the
 *  floor inside the walls, on the pad, already holds another of the list, or is where the cell already stands. */
export function cellMoved(interior, field, index, wx, wz) {
  const dx = Math.floor(wx) - interior.corner[0], dz = Math.floor(wz) - interior.corner[1];
  const [floorMinX, floorMinZ, floorMaxX, floorMaxZ] = interior.floor;
  const [padMinX, padMinZ, padMaxX, padMaxZ] = interior.pad;
  if (dx < floorMinX || dx >= floorMaxX || dz < floorMinZ || dz >= floorMaxZ) return null;
  if (dx >= padMinX && dx < padMaxX && dz >= padMinZ && dz < padMaxZ) return null;
  const cells = interior[field];
  if (cells.some(cell => cell[0] === dx && cell[1] === dz)) return null;
  return cells.map((cell, at) => (at === index ? [dx, dz] : [...cell]));
}

/** The cells a house states carried through a change of its room's wing from `from` to `to` (max-exclusive): each
 *  keeps its block, stated again from the new corner and pushed back onto the floor `floor` — `[minDx, minDz, maxDx,
 *  maxDz]` from the old corner — keeps inside the walls. */
export function cellsCarried(cells, from, to, floor) {
  const [lowX, lowZ] = [floor[0], floor[1]];
  const [highX, highZ] = [(from.max_x - from.min_x) - floor[2], (from.max_z - from.min_z) - floor[3]];
  const [spanX, spanZ] = [to.max_x - to.min_x, to.max_z - to.min_z];
  return cells.map(([dx, dz]) => [
    Math.min(Math.max(from.min_x + dx - to.min_x, lowX), spanX - highX - 1),
    Math.min(Math.max(from.min_z + dz - to.min_z, lowZ), spanZ - highZ - 1),
  ]);
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

/** A max-exclusive shape rectangle as the inclusive cell rect the building law reads. */
function inclusive(r) {
  return { minX: r.min_x, minZ: r.min_z, maxX: r.max_x - 1, maxZ: r.max_z - 1 };
}

function exclusive(r) {
  return { min_x: r.minX, min_z: r.minZ, max_x: r.maxX + 1, max_z: r.maxZ + 1 };
}
