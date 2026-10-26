/**
 * DressingController — the placing tools of the sketch tool's Dressing phase. Same controller contract as the
 * draw and edit controllers (onMouseDown → bool, onMouseMove, onMouseUp, paint, cancel), so the canvas routes
 * to it the same way it routes to those.
 *
 * Four tools, two interactions, and the split is the model's own. A **path** and an **area** are dragged: press,
 * trace, release — where the button comes up is the last point, exactly the way the lasso already behaves, so
 * there is no separate way to finish and no way to get stuck mid-draw. A **tree** and a **boulder** are
 * markers: one click drops one, because a tree is a decision about a spot and a spot is a click.
 *
 * New props take the tool's current settings as a copy. That is what lets an author place ten oaks without
 * configuring ten oaks, while still bending any one of them afterwards — the settings are a starting point,
 * not a binding.
 *
 * A placement ends the tool: the prop is selected and `onPlaced` asks the host for select mode, the same way
 * a finished draw tool hands the canvas back. The prop an author just put down is the one they want to move,
 * nudge or tune, and a still-armed tool turns that first click into a second tree.
 *
 * A **selected** prop wears screen-space handles, the same square grips a selected sketch polygon wears: one
 * per traced point on a route or an area, one on the anchor of a marker. They are the whole reason a prop can
 * be reshaped rather than only re-traced — a path that runs two blocks wide of a bridge is a point to drag,
 * not a route to draw again — and they are what says a prop is grabbable at all. Kept as SVG in the overlay
 * above the painted surface, where a fixed pixel size and a cursor are worth having.
 */

import { paintDressingPreview, paintMarkerGhost, paintPropHover } from "../render/dressing-render.js";
import { buildingsOverlap, defaultProp, isMarker, isRect, propAnchor, propReach, rectFootprint, rectsJoinUp,
         onLayer, translateProp, wingCorners, wingRects, withCorners } from "../dressing/dressing-doc.js";
import { MIN_FOOTPRINT_SPAN, wallNear, wingOffWall } from "../shared/building.js";
import { douglasPeucker, simplifyRing } from "../geometry/simplify.js";
import { svgEl, handleRectAttrs, insertGhost } from "../render/svg.js";
import { toScreen } from "../geometry/transform.js";
import { renderTransformBox } from "../render/canvas-chrome.js";
import { resizedRect } from "./piece-resize-controller.js";

/** Tool name → the kind of prop it places. The canvas passes tool names through, so this is also the test for
 *  "is a dressing tool active at all". */
export const DRESSING_TOOLS = { "dress:stroke": "stroke", "dress:fluid": "fluid", "dress:flora": "flora", "dress:house": "house", "dress:tree": "tree", "dress:boulder": "boulder", "dress:chest": "chest" };

// A dragged trace is one point per block of pointer travel — unreadable to edit and pointless to store, so it
// is simplified to the points at real bends on release. Same simplifier as the lasso, one step tighter than
// it: a lasso outlines land, where a route runs THROUGH it, so a bend the outline can afford to round off is
// a bend a path would visibly miss.
const TRACE_SIMPLIFY_TOLERANCE = 3;

// How near a free wall of a picked building the pointer shows the insert marker, in screen px — the distance the
// outline editor shows its own at.
const WALL_REACH = 10;

// How near a prop's anchor a click counts as picking it, in blocks — a marker is small, so its own reach is
// too tight a target at a low zoom.
const PICK_SLACK = 2;

// Screen px. The point grips match the sketch editor's vertex handles, so a traced route reads as the same
// kind of editable thing a drawn polygon is.
const POINT_HALF = 4;

export class DressingController {
  #doc;
  #callbacks;
  #handlesLayer;
  #getViewport;
  #tool = null;
  #trace = null;          // the in-progress drag: { kind, points }
  #cursor = null;         // where a marker would drop
  #drag = null;           // moving an already-placed prop
  #pointDrag = null;      // reshaping one: { id, idx } or { id, wing, grip, from } — idx -1 is a marker's own anchor, and a
                          // building's wing is resized by a `grip` of its box from `from`, the rect it stood at
  #selectedId = null;
  #hoveredId = null;      // the prop whose row the Placed list has the pointer on
  #selectedWing = 0;      // which wing of a picked building its settings are edited for
  #wall = null;           // the free wall of the picked building the pointer is near, where a press adds a wing
  #wallGhost = null;
  #settings = {};         // per-kind starting values for the next prop placed
  #onTerrain;             // (bx, bz) → is this cell on the rasterized terrain a marker can seat on?
  #editable;              // () → may this page change the placements at all?

  /**
   * @param doc          DressingDoc — the placed props (mutated through its own methods)
   * @param handlesLayer SVGGElement — screen-space handle layer for the selected prop's points
   * @param getViewport  () => { scale, panX, panY }
   * @param callbacks    { onChanged, onSelected, onPreviewChanged, onPlaced, onTerrain, isEditable }
   *                     `isEditable` answers false on a page the caller may not write: a prop is still picked,
   *                     and nothing is placed, dragged or given a grip.
   */
  constructor(doc, handlesLayer, getViewport, callbacks = {}) {
    this.#doc = doc;
    this.#handlesLayer = handlesLayer ?? null;
    this.#getViewport = getViewport ?? (() => ({ scale: 1, panX: 0, panY: 0 }));
    this.#callbacks = callbacks;
    this.#onTerrain = callbacks.onTerrain ?? (() => true);
    this.#editable = callbacks.isEditable ?? (() => true);
    for (const kind of ["stroke", "fluid", "flora", "house", "tree", "boulder", "chest"]) this.#settings[kind] = defaultProp(kind, seedFor(kind));
  }

  setDoc(doc) { this.#doc = doc; this.#selectedId = null; this.refreshHandles(); }
  setTool(tool) { this.#tool = DRESSING_TOOLS[tool] ? tool : null; this.cancel(); }
  get activeKind() { return DRESSING_TOOLS[this.#tool] ?? null; }
  get selectedId() { return this.#selectedId; }

  /** Which wing of the picked building is picked, counted from 0. */
  get selectedWing() { return this.#selectedWing; }

  /** The starting values the next prop of a kind takes. The inspector edits these when nothing is selected. */
  settingsFor(kind) { return this.#settings[kind]; }
  setSettings(kind, patch) { this.#settings[kind] = { ...this.#settings[kind], ...patch }; }

  /** Pick a prop, and its first wing where it is a building. */
  select(id, wing = 0) {
    const live = this.#doc.byId(id) ? id : null;
    this.#selectedId = live;
    this.#selectedWing = live ? Math.max(0, Math.min(wing, (this.#doc.byId(live).wings?.length ?? 1) - 1)) : 0;
    this.#wall = null;
    this.#callbacks.onSelected?.(this.#selectedId);
    this.refreshHandles();
    this.#callbacks.onPreviewChanged?.();
  }

  /** Pick one wing of the picked building, whose settings the inspector edits. */
  selectWing(wing) {
    const prop = this.#selectedId ? this.#doc.byId(this.#selectedId) : null;
    if (!prop || !isRect(prop) || wing < 0 || wing >= (prop.wings?.length ?? 0)) return;
    this.#selectedWing = wing;
    this.#callbacks.onSelected?.(this.#selectedId);
  }

  /** Delete the selection. Returns true if something went. */
  deleteSelected() {
    if (!this.#selectedId) return false;
    this.#doc.remove(this.#selectedId);
    this.select(null);
    this.#callbacks.onChanged?.();
    return true;
  }

  /**
   * Add a wing off the free wall the pointer is near, the wall's length and five deep, shrunk short of any
   * building it would run into, and pick it. Answers what happened, for the host to report.
   *
   * @returns {{ done: string, wings: number } | { refused: string }}
   */
  addWing() {
    const prop = this.#selectedId ? this.#doc.byId(this.#selectedId) : null;
    if (!prop || !isRect(prop) || !this.#wall || !this.#editable()) return { refused: "Point at a wall of the building." };
    const others = this.#doc.props.filter(other => other.id !== prop.id && isRect(other) && onLayer(other, prop.layer))
      .flatMap(other => wingRects(other));
    const rect = wingOffWall([...wingRects(prop), ...others], this.#wall);
    if (!rect) return { refused: "There is no room for a wing off that wall." };
    const wings = [...(prop.wings ?? []), { corners: [[rect.minX, rect.minZ], [rect.maxX, rect.maxZ]] }];
    this.#doc.update(prop.id, { wings });
    this.#wall = null;
    this.#selectedWing = wings.length - 1;
    this.#callbacks.onSelected?.(this.#selectedId);
    this.refreshHandles();
    return { done: "added", wings: wings.length };
  }

  /**
   * Take a wing out of the picked building. The last wing is the building itself, and a wing whose going
   * leaves the rest no longer joined edge to edge would leave two buildings, so both are refused.
   *
   * @returns {{ done: string, wings: number } | { refused: string }}
   */
  deleteWing(wing) {
    const prop = this.#selectedId ? this.#doc.byId(this.#selectedId) : null;
    if (!prop || !isRect(prop) || !this.#editable()) return { refused: "Select a building." };
    const wings = prop.wings ?? [];
    if (wings.length < 2) return { refused: "A building keeps at least one wing." };
    const rest = wings.filter((_, at) => at !== wing);
    if (!rectsJoinUp(wingRects({ wings: rest }))) return { refused: "The other wings would no longer meet." };
    this.#doc.update(prop.id, { wings: rest });
    this.#selectedWing = Math.min(this.#selectedWing, rest.length - 1);
    this.#callbacks.onSelected?.(this.#selectedId);
    this.refreshHandles();
    return { done: "deleted", wings: rest.length };
  }

  /** Change what one wing of the picked building states about itself — its storeys, roof, ridge and joint. */
  updateWing(wing, spec) {
    const prop = this.#selectedId ? this.#doc.byId(this.#selectedId) : null;
    if (!prop || !isRect(prop) || !prop.wings?.[wing]) return null;
    const wings = prop.wings.map((each, at) => at === wing ? { ...each, spec } : each);
    const updated = this.#doc.update(prop.id, { wings });
    this.#callbacks.onChanged?.();
    return updated;
  }

  /** Change the selected prop's knobs, and remember them as the starting point for the next one of its kind —
   *  an author who widens one path usually means the next one too. */
  updateSelected(patch) {
    if (!this.#selectedId) return null;
    const updated = this.#doc.update(this.#selectedId, patch);
    if (updated) {
      // The storey is where this one prop rests, not a starting value: the next prop placed takes the storey
      // being drawn on.
      const { layer, ...carried } = patch ?? {};
      this.setSettings(updated.kind, carried);
      this.refreshHandles();      // a wider path or a re-traced area moves its own grips
      this.#callbacks.onChanged?.();
    }
    return updated;
  }

  // ── pointer ────────────────────────────────────────────────────────────────
  /** Press. Returns true when the dressing phase consumed it. */
  onMouseDown(bx, bz, activeTool) {
    const kind = DRESSING_TOOLS[activeTool];
    if (kind) {
      if (!this.#editable()) return true;   // a page that may not write places nothing
      if (isMarker(kind)) { this.#place(kind, bx, bz); return true; }
      // A rectangle is a two-corner drag: the press fixes one corner and every move rewrites the other, where
      // a traced outline appends. Same trace state, one different rule about what a move does to it.
      this.#trace = { kind, points: isRect(kind) ? [[bx, bz], [bx, bz]] : [[bx, bz]] };
      this.#callbacks.onPreviewChanged?.();
      return true;
    }
    if (activeTool !== "select" && activeTool !== null) return false;

    // Select mode: pick the prop under the cursor, and the wing of it under the cursor, and start dragging it.
    const hit = this.#hitTest(bx, bz);
    const wing = hit && isRect(hit)
      ? Math.max(0, wingRects(hit).findIndex(r => bx >= r.minX && bx <= r.maxX && bz >= r.minZ && bz <= r.maxZ)) : 0;
    this.select(hit?.id ?? null, wing);
    this.#drag = hit && this.#editable() ? { id: hit.id, fromX: bx, fromZ: bz, moved: false } : null;
    return hit !== null;
  }

  onMouseMove(bx, bz, activeTool) {
    if (this.#trace) {
      const last = this.#trace.points[this.#trace.points.length - 1];
      if (bx !== last[0] || bz !== last[1]) {
        if (isRect(this.#trace.kind)) this.#trace.points[1] = [bx, bz];
        else this.#trace.points.push([bx, bz]);
        this.#callbacks.onPreviewChanged?.();
      }
      return true;
    }
    if (this.#drag) {
      const prop = this.#doc.byId(this.#drag.id);
      if (!prop) { this.#drag = null; return false; }
      const dx = bx - this.#drag.fromX, dz = bz - this.#drag.fromZ;
      if (!this.#drag.moved && dx === 0 && dz === 0) return true;
      // A marker can only be dragged across the terrain: over the void the drag simply doesn't follow, so the
      // prop stays on the last real cell it was over rather than being carried off the map.
      if (isMarker(prop) && !this.#onTerrain(bx, bz)) return true;
      const moved = translateProp(prop, dx, dz);
      // A plan states its ground once, so a building is not carried over another: the drag stops against it
      // the way a marker's stops at the void, leaving the prop on the last legal cell rather than landing it
      // somewhere the stamp would refuse.
      if (isRect(prop) && this.#wouldOverlap(moved)) return true;
      this.#drag.moved = true;
      this.#drag.fromX = bx; this.#drag.fromZ = bz;
      this.#doc.update(prop.id, moved);
      this.refreshHandles();
      this.#callbacks.onPreviewChanged?.();
      return true;
    }
    // A marker tool shows where its prop would land, so a click is aimed rather than guessed — and whether the
    // spot will take it, so the void reads as off-limits before the click that does nothing.
    const kind = DRESSING_TOOLS[activeTool];
    if (kind && isMarker(kind)) {
      this.#cursor = { kind, x: bx, z: bz, valid: this.#onTerrain(bx, bz) };
      this.#callbacks.onPreviewChanged?.();
      return false;
    }
    if (this.#cursor) { this.#cursor = null; this.#callbacks.onPreviewChanged?.(); }
    return false;
  }

  /** Pointer move in world coordinates: show the insert marker on the free wall of the picked building the
   *  pointer is near, or hide it. */
  onPointerHover(wx, wz, activeTool) {
    const prop = this.#selectedId ? this.#doc.byId(this.#selectedId) : null;
    const viewport = this.#getViewport();
    const wall = prop && isRect(prop) && this.#editable() && (activeTool === "select" || !activeTool)
      ? wallNear(wingRects(prop), wx, wz, WALL_REACH / (viewport.scale || 1)) : null;
    this.#wall = wall;
    if (!this.#handlesLayer) return;
    if (!wall) { if (this.#wallGhost) this.#wallGhost.style.display = "none"; return; }
    if (!this.#wallGhost || !this.#wallGhost.isConnected) {
      this.#wallGhost = insertGhost(() => this.#callbacks.onWingAdded?.(this.addWing()));
      this.#handlesLayer.appendChild(this.#wallGhost);
    }
    const sp = toScreen(wall.mid[0], wall.mid[1], viewport);
    this.#wallGhost.setAttribute("cx", String(sp.x));
    this.#wallGhost.setAttribute("cy", String(sp.y));
    this.#wallGhost.style.display = "";
  }

  /** Release — the drag's last point is the stroke's last point, which is the whole interaction. */
  onMouseUp() {
    if (this.#trace) {
      const { kind, points } = this.#trace;
      this.#trace = null;
      this.#finishTrace(kind, points);
      return true;
    }
    if (this.#drag) {
      const moved = this.#drag.moved;
      this.#drag = null;
      if (moved) this.#callbacks.onChanged?.();
      return moved;
    }
    return false;
  }

  cancel() {
    if (!this.#trace && !this.#cursor && !this.#drag && !this.#pointDrag) return;
    this.#trace = null; this.#cursor = null; this.#drag = null; this.#pointDrag = null;
    this.#callbacks.onPreviewChanged?.();
  }

  // ── point handles ──────────────────────────────────────────────────────────
  /**
   * Take hold of one of a prop's points — `idx` into its `points`, or -1 for a marker's own anchor. The
   * grip's mousedown is a thin adapter over this, so what a drag *does* is reachable without a DOM.
   */
  beginPointDrag(id, idx) {
    if (!this.#doc.byId(id)) return false;
    this.#pointDrag = { id, idx, moved: false };
    return true;
  }

  /**
   * Take hold of wing `wing` of building `id` by one grip of its transform box — a corner or an edge — as a press
   * on it does.
   */
  beginWingDrag(id, wing, grip) {
    const prop = this.#doc.byId(id);
    const rect = prop && isRect(prop) ? wingRects(prop)[wing] : null;
    if (!rect) return false;
    const from = { min_x: rect.minX, min_z: rect.minZ, max_x: rect.maxX + 1, max_z: rect.maxZ + 1 };
    this.#pointDrag = { id, wing, grip, from, moved: false };
    return true;
  }

  /**
   * Document mousemove while a point grip is held. World coordinates, so the point lands where the cursor
   * is at any zoom. Returns true when consumed, so the canvas can hand the same hook to its other draggers.
   */
  onHandleMove(wx, wz) {
    if (!this.#pointDrag) return false;
    const prop = this.#doc.byId(this.#pointDrag.id);
    if (!prop) { this.#pointDrag = null; return false; }
    const bx = Math.round(wx), bz = Math.round(wz);
    if (this.#pointDrag.grip) {
      // A wing's grip resizes that wing alone, by whole blocks, the opposite side held; its siblings stay put, and
      // the wing keeps whatever else it states — its layers, roof, ridge and joint.
      const at = this.#pointDrag.wing;
      const next = resizedRect(this.#pointDrag.from, this.#pointDrag.grip, wx, wz, MIN_FOOTPRINT_SPAN);
      const points = [[next.min_x, next.min_z], [next.max_x - 1, next.max_z - 1]];
      this.#doc.update(prop.id, { wings: (prop.wings ?? []).map((wing, i) => i === at ? withCorners(wing, points) : wing) });
    } else if (this.#pointDrag.idx < 0) {
      // A marker seats on the ground, so its grip stops at the terrain edge rather than carrying it off.
      if (!this.#onTerrain(bx, bz)) return true;
      this.#doc.update(prop.id, { x: bx, z: bz });
    } else {
      // An area prop reshapes its own traced points, one at a time.
      const points = (prop.points ?? []).map(([x, z]) => [x, z]);
      if (this.#pointDrag.idx >= points.length) { this.#pointDrag = null; return false; }
      points[this.#pointDrag.idx] = [bx, bz];
      this.#doc.update(prop.id, { points });
    }
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

  /**
   * Redraw the selected prop's grips — one per traced point, or one on a marker's anchor. Call after a
   * selection, an edit, or any viewport change, since the handles are screen-space and the points are not.
   */
  refreshHandles() {
    const layer = this.#handlesLayer;
    if (!layer) return;
    while (layer.firstChild) layer.removeChild(layer.firstChild);
    const prop = this.#selectedId ? this.#doc.byId(this.#selectedId) : null;
    if (!prop || !this.#editable()) return;
    // A building's picked wing wears the transform box every rectangle wears, so a joined L is reshaped a
    // rectangle at a time; a press inside another wing picks that one.
    if (isRect(prop)) {
      const rect = wingRects(prop)[this.#selectedWing];
      if (!rect) return;
      const viewport = this.#getViewport();
      const a = toScreen(rect.minX, rect.minZ, viewport), b = toScreen(rect.maxX + 1, rect.maxZ + 1, viewport);
      renderTransformBox(layer, { l: Math.min(a.x, b.x), r: Math.max(a.x, b.x), t: Math.min(a.y, b.y), b: Math.max(a.y, b.y) }, {
        outline: false, gripHalf: POINT_HALF,
        onScale: (grip, e) => {
          if (e.button !== 0) return;
          e.stopPropagation();   // the press is the grip's, not a canvas pan or a re-pick
          this.beginWingDrag(prop.id, this.#selectedWing, grip);
        },
      });
      return;
    }
    const grips = isMarker(prop) ? [{ point: propAnchor(prop), idx: -1 }]
      : (prop.points ?? []).map((point, i) => ({ point, idx: i }));
    grips.forEach(({ point: [wx, wz], idx }) => {
      const sp = toScreen(wx, wz, this.#getViewport());
      const grip = svgEl("rect", {
        ...handleRectAttrs(sp.x, sp.y, POINT_HALF),
        fill: "var(--bg-deep)", stroke: "var(--accent)", "stroke-width": "1.5", style: "cursor:move",
      });
      grip.addEventListener("mousedown", (e) => {
        if (e.button !== 0) return;
        e.stopPropagation();   // the press is the grip's, not a canvas pan or a re-pick
        this.beginPointDrag(prop.id, idx);
      });
      grip.addEventListener("click", (e) => e.stopPropagation());
      layer.appendChild(grip);
    });
  }

  /** Ring the prop whose list row is under the pointer, or none. A view, not a selection: nothing is picked. */
  hover(id) {
    const live = id && this.#doc.byId(id) ? id : null;
    if (live === this.#hoveredId) return;
    this.#hoveredId = live;
    this.#callbacks.onPreviewChanged?.();
  }

  /** Draw whatever is in flight — the traced stroke, or the ghost of a marker about to drop — and the ring round
   *  a prop hovered in the list. */
  paint(painter) {
    const hovered = this.#hoveredId ? this.#doc.byId(this.#hoveredId) : null;
    if (hovered) paintPropHover(painter, hovered, this.#doc.styles);
    if (this.#trace) {
      const radius = this.#settings[this.#trace.kind]?.radius ?? 3;
      paintDressingPreview(painter, this.#trace.kind, this.#trace.points, radius);
    }
    if (this.#cursor) {
      const settings = this.#settings[this.#cursor.kind];
      paintMarkerGhost(painter, this.#cursor.kind, this.#cursor.x, this.#cursor.z,
                       propReach(settings, this.#doc.styles), this.#cursor.valid !== false);
    }
  }

  // ── private ────────────────────────────────────────────────────────────────
  #place(kind, bx, bz) {
    // A marker seats on the ground, so it can only be dropped where there is ground. The void — a gap between
    // shapes, the space off the map — takes nothing; the click is consumed but nothing is placed.
    if (!this.#onTerrain(bx, bz)) { this.#callbacks.onPreviewChanged?.(); return; }
    const placed = this.#doc.add({ ...this.#settings[kind], x: bx, z: bz, seed: this.#nextSeed(kind) });
    this.#cursor = null;
    this.select(placed.id);
    this.#callbacks.onChanged?.();
    this.#callbacks.onPlaced?.();
  }

  #finishTrace(kind, points) {
    // A rectangle is already the two points it will be stored as — there is nothing to simplify, and a drag
    // too small to hold two walls and an inside is a misfire rather than a tiny building.
    if (isRect(kind)) {
      if (!rectFootprint({ points })) { this.#callbacks.onPreviewChanged?.(); return; }
      const placed = this.#doc.add({ ...this.#settings[kind], wings: [{ corners: points }], seed: this.#nextSeed(kind) });
      this.select(placed.id);
      this.#callbacks.onChanged?.();
      this.#callbacks.onPlaced?.();
      return;
    }

    // A route is an open line and an area is a closed one, and they simplify differently: the ring simplifier
    // splits at the two farthest points and walks both ways round, which is right for an outline and would
    // reorder a route. So the open-line props (a path, a fluid channel) keep their direction through the plain
    // open simplifier.
    const openLine = kind === "stroke" || kind === "fluid";
    const simplified = openLine
      ? douglasPeucker(points, TRACE_SIMPLIFY_TOLERANCE)
      : simplifyRing(points, TRACE_SIMPLIFY_TOLERANCE);
    // An open line needs somewhere to go; an area needs to enclose something. Below that the drag was a misfire.
    const enough = openLine ? 2 : 3;
    if (simplified.length < enough) { this.#callbacks.onPreviewChanged?.(); return; }

    const placed = this.#doc.add({ ...this.#settings[kind], points: simplified, seed: this.#nextSeed(kind) });
    this.select(placed.id);
    this.#callbacks.onChanged?.();
    this.#callbacks.onPlaced?.();
  }

  // Each prop gets its own seed, so two rocks placed with the same settings are still two different rocks.
  // Derived from the count rather than rolled, so a document re-opened places nothing new.
  #nextSeed(kind) {
    const same = this.#doc.props.filter(prop => prop.kind === kind).length;
    return seedFor(kind) + same * 7;
  }

  // The smallest prop under the cursor wins, so a tree standing inside a flora area is still clickable.
  /** Whether a building, moved to where it is now stated, would stand on another building's ground. */
  #wouldOverlap(moved) {
    for (const other of this.#doc.props) {
      if (other.id === moved.id || !isRect(other)) continue;
      if (buildingsOverlap(moved, other)) return true;
    }
    return false;
  }

  #hitTest(bx, bz) {
    let best = null, bestReach = Infinity;
    for (const prop of this.#doc.props) {
      // A prop on another storey is drawn dimmed as context and is not picked, the way another layer's shapes
      // ghost — a click over a stacked board reaches the floor being drawn on.
      if (!onLayer(prop, this.#doc.layer)) continue;
      const [ax, az] = propAnchor(prop);
      const reach = isMarker(prop) ? propReach(prop, this.#doc.styles) + PICK_SLACK : areaReach(prop);
      const distance = Math.hypot(bx - ax, bz - az);
      if (distance <= reach && reach < bestReach) { best = prop; bestReach = reach; }
    }
    return best;
  }
}

// A distinct starting seed per kind, so a map's first path and its first tree do not share a field.
function seedFor(kind) { return { path: 5, fluid: 11, flora: 7, house: 13, tree: 23, boulder: 17, chest: 19 }[kind] ?? 1; }

// How far an area prop reaches from its own middle — its bounding radius, which is what a click is measured
// against. Coarse on purpose: picking is about reaching the thing, not about its exact edge.
function areaReach(prop) {
  const points = isRect(prop) ? (prop.wings ?? []).flatMap(wing => wingCorners(wing) ?? []) : (prop.points ?? []);
  if (!points.length) return 0;
  const [ax, az] = propAnchor(prop);
  return Math.max(...points.map(([x, z]) => Math.hypot(x - ax, z - az))) + (prop.radius ?? 0);
}
