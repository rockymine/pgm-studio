/**
 * ModifierController — the Terraform phase's canvas tools. Same controller contract as the draw, edit and
 * dressing controllers (onMouseDown → bool, onMouseMove, onMouseUp, paint, cancel), so the canvas routes to it
 * the same way it routes to those.
 *
 * A modifier's mask is traced the way the lasso traces ground: press with a modifier's tool in hand, drag the
 * outline, release, and the trace is simplified to the points at its real bends. A line operation (a ridge, a
 * river) traces an open run instead, simplified the same way and kept with a width. With Select in hand a press
 * on a mask picks its modifier and drags the whole mask; the picked outline carries a grip per point and offers
 * a new point on the edge under the pointer, as a drawn polygon does.
 *
 * The controller holds no document. It reads the stack the bridge holds (`getModifiers`) and hands every
 * change back as a new stack (`onChanged`) or a finished trace (`onTraced`).
 */

import { simplifyRing, douglasPeucker } from "../geometry/simplify.js";
import { distToSegment } from "../geometry/shape.js";
import { pointInRing } from "../geometry/polygon.js";
import { svgEl, handleRectAttrs, insertGhost } from "../render/svg.js";
import { toScreen } from "../geometry/transform.js";
import { areaPoints, withAreaPoints, translateModifier, isLineOp, isLineArea, distanceToLine, LINE_WIDTH }
  from "../relief/modifier-doc.js";

/** The dock tool that traces a modifier of an operation is `modifier:<op>`. */
export const MODIFIER_TOOL_PREFIX = "modifier:";

/** The operation a tool traces, or null for a tool that traces none. */
export const opOfTool = (tool) => typeof tool === "string" && tool.startsWith(MODIFIER_TOOL_PREFIX)
  ? tool.slice(MODIFIER_TOOL_PREFIX.length) : null;

// The dressing traces' tolerance rather than the lasso's looser one: a mask's outline is the form of the hill
// seen from above, so a bend the lasso can afford to round off is one the ground would visibly miss.
const TRACE_SIMPLIFY_TOLERANCE = 2;

// A line shorter than this, in blocks, is a click that moved, not a run.
const MIN_LINE_LENGTH = 3;

// How near an outline a press counts as on it, in blocks.
const PICK_SLACK = 2;

// Screen px. The grips match the sketch editor's vertex handles; the edge threshold is the draw stage's.
const POINT_HALF = 4;
const EDGE_THRESHOLD = 10;

export class ModifierController {
  #callbacks;
  #handlesLayer;
  #getViewport;
  #getModifiers;
  #editable;
  #trace = null;          // the in-progress outline or run: { op, points, line }
  #drag = null;           // moving a picked mask: { id, fromX, fromZ, moved }
  #pointDrag = null;      // reshaping one outline: { id, area, idx, moved }
  #hoveredEdge = -1;
  #ghostEl = null;
  #selectedId = null;
  #selectedArea = 0;      // which of the picked modifier's outlines carries the grips

  /**
   * @param handlesLayer SVGGElement — screen-space layer for the picked outline's grips
   * @param getViewport  () => { scale, panX, panY }
   * @param callbacks    { getModifiers, onTraced(op, points), onChanged(modifiers), onSelected(id),
   *                       onPreviewChanged, onPlaced, isEditable }
   */
  constructor(handlesLayer, getViewport, callbacks = {}) {
    this.#handlesLayer = handlesLayer ?? null;
    this.#getViewport = getViewport ?? (() => ({ scale: 1, panX: 0, panY: 0 }));
    this.#callbacks = callbacks;
    this.#getModifiers = callbacks.getModifiers ?? (() => []);
    this.#editable = callbacks.isEditable ?? (() => true);
  }

  get selectedId() { return this.#selectedId; }

  #byId(id) { return this.#getModifiers().find(modifier => modifier.id === id) ?? null; }

  /** Pick a modifier, or null. Announced through `onSelected` only when it changes. */
  select(id, area = 0) {
    const next = id && this.#byId(id) ? id : null;
    const changed = next !== this.#selectedId;
    this.#selectedId = next;
    this.#selectedArea = area;
    if (changed) this.#callbacks.onSelected?.(next);
    this.refreshHandles();
    this.#callbacks.onPreviewChanged?.();
  }

  /** Delete the picked modifier. Returns true if one went. */
  deleteSelected() {
    if (!this.#selectedId) return false;
    const id = this.#selectedId;
    this.select(null);
    this.#callbacks.onChanged?.(this.#getModifiers().filter(modifier => modifier.id !== id));
    return true;
  }

  // ── pointer ────────────────────────────────────────────────────────────────
  /** Press. Returns true when Terraform consumed it. */
  onMouseDown(bx, bz, activeTool) {
    const op = opOfTool(activeTool);
    if (op) {
      if (!this.#editable()) return true;
      this.#trace = { op, points: [[bx, bz]], line: isLineOp(op) };
      this.#callbacks.onPreviewChanged?.();
      return true;
    }
    if (activeTool !== "select" && activeTool !== null) return false;

    const hit = this.#hitTest(bx, bz);
    if (hit) {
      this.select(hit.id, hit.area);
      this.#drag = this.#editable() ? { id: hit.id, fromX: bx, fromZ: bz, moved: false } : null;
      return true;
    }
    this.select(null);
    this.#drag = null;
    return false;
  }

  onMouseMove(bx, bz, activeTool) {
    if (this.#trace) {
      const last = this.#trace.points[this.#trace.points.length - 1];
      if (bx !== last[0] || bz !== last[1]) {
        this.#trace.points.push([bx, bz]);
        this.#callbacks.onPreviewChanged?.();
      }
      return true;
    }
    if (this.#drag) {
      const modifier = this.#byId(this.#drag.id);
      if (!modifier) { this.#drag = null; return false; }
      const dx = bx - this.#drag.fromX, dz = bz - this.#drag.fromZ;
      if (dx === 0 && dz === 0) return true;
      this.#drag.moved = true;
      this.#drag.fromX = bx; this.#drag.fromZ = bz;
      this.#replace(translateModifier(modifier, dx, dz), true);
      return true;
    }
    this.#offerInsert(bx, bz, activeTool);
    return false;
  }

  /** Release: the trace's last point is where the button came up, so there is no other way to finish. */
  onMouseUp() {
    if (this.#trace) {
      const { op, points, line } = this.#trace;
      this.#trace = null;
      // An outline has to enclose something and a run has to go somewhere; below that the drag was a misfire.
      const kept = line ? douglasPeucker(points, TRACE_SIMPLIFY_TOLERANCE) : simplifyRing(points, TRACE_SIMPLIFY_TOLERANCE);
      if (line ? kept.length < 2 || runLength(kept) < MIN_LINE_LENGTH : kept.length < 3) {
        this.#callbacks.onPreviewChanged?.();
        return true;
      }
      this.#callbacks.onTraced?.(op, kept);
      this.#callbacks.onPlaced?.();
      return true;
    }
    if (this.#drag) {
      const moved = this.#drag.moved;
      this.#drag = null;
      if (moved) this.#callbacks.onChanged?.(this.#getModifiers());
      return moved;
    }
    return false;
  }

  cancel() {
    if (!this.#trace && !this.#drag && !this.#pointDrag) return;
    this.#trace = null; this.#drag = null; this.#pointDrag = null;
    this.#callbacks.onPreviewChanged?.();
  }

  // ── the picked outline's points ────────────────────────────────────────────
  #pickedPoints() {
    const modifier = this.#selectedId ? this.#byId(this.#selectedId) : null;
    return modifier ? areaPoints(modifier.mask?.areas?.[this.#selectedArea]) : null;
  }

  /** Whether the picked outline is an open run, whose last point has no edge back to its first. */
  #pickedIsLine() {
    const modifier = this.#selectedId ? this.#byId(this.#selectedId) : null;
    return isLineArea(modifier?.mask?.areas?.[this.#selectedArea]);
  }

  #offerInsert(bx, bz, activeTool) {
    const points = (!activeTool || activeTool === "select") && this.#editable() ? this.#pickedPoints() : null;
    if (!points || points.length < 2) { this.#clearGhost(); return; }
    const view = this.#getViewport();
    const at = toScreen(bx, bz, view);
    const edges = this.#pickedIsLine() ? points.length - 1 : points.length;
    let best = EDGE_THRESHOLD, index = -1, mx = 0, my = 0;
    for (let i = 0; i < edges; i++) {
      const j = (i + 1) % points.length;
      const a = toScreen(points[i][0], points[i][1], view);
      const b = toScreen(points[j][0], points[j][1], view);
      const away = distToSegment(at.x, at.y, a.x, a.y, b.x, b.y);
      if (away < best) { best = away; index = i; mx = (a.x + b.x) / 2; my = (a.y + b.y) / 2; }
    }
    if (index < 0) { this.#clearGhost(); return; }
    this.#hoveredEdge = index;
    if (!this.#handlesLayer) return;
    if (!this.#ghostEl || !this.#ghostEl.isConnected) {
      this.#ghostEl = insertGhost(() => this.insertPoint(this.#hoveredEdge));
      this.#handlesLayer.appendChild(this.#ghostEl);
    }
    this.#ghostEl.setAttribute("cx", String(mx));
    this.#ghostEl.setAttribute("cy", String(my));
    this.#ghostEl.style.display = "";
  }

  #clearGhost() {
    this.#hoveredEdge = -1;
    if (this.#ghostEl) this.#ghostEl.style.display = "none";
  }

  /** Put a point at the middle of one edge of the picked outline and leave it in the hand that pressed for it,
   *  so the press that inserts is the drag that places. Returns true if it went in. */
  insertPoint(edge) {
    const points = this.#pickedPoints()?.map(point => [...point]);
    if (!points || edge < 0 || edge >= (this.#pickedIsLine() ? points.length - 1 : points.length)) return false;
    const next = (edge + 1) % points.length;
    points.splice(next, 0, [Math.round((points[edge][0] + points[next][0]) / 2),
                            Math.round((points[edge][1] + points[next][1]) / 2)]);
    this.#replace(withAreaPoints(this.#byId(this.#selectedId), this.#selectedArea, points), false);
    this.#clearGhost();
    this.#pointDrag = { id: this.#selectedId, area: this.#selectedArea, idx: next, moved: true };
    return true;
  }

  /** Document mousemove while a grip is held, in world coordinates. Returns true when consumed. */
  onHandleMove(wx, wz) {
    if (!this.#pointDrag) return false;
    const modifier = this.#byId(this.#pointDrag.id);
    const points = modifier ? areaPoints(modifier.mask?.areas?.[this.#pointDrag.area])?.map(point => [...point]) : null;
    if (!points || this.#pointDrag.idx >= points.length) { this.#pointDrag = null; return false; }
    points[this.#pointDrag.idx] = [Math.round(wx), Math.round(wz)];
    this.#pointDrag.moved = true;
    this.#replace(withAreaPoints(modifier, this.#pointDrag.area, points), true);
    return true;
  }

  /** Document mouseup ending a grip drag. Returns true when consumed. */
  onHandleUp() {
    if (!this.#pointDrag) return false;
    const moved = this.#pointDrag.moved;
    this.#pointDrag = null;
    if (moved) this.#callbacks.onChanged?.(this.#getModifiers());
    return true;
  }

  /** Swap one modifier for its edited self — as a preview while a drag is live, as a change once it lands. */
  #replace(edited, live) {
    const next = this.#getModifiers().map(modifier => modifier.id === edited.id ? edited : modifier);
    if (live) this.#callbacks.onPreview?.(next);
    else this.#callbacks.onChanged?.(next);
    this.refreshHandles();
    this.#callbacks.onPreviewChanged?.();
  }

  /** Redraw the picked outline's grips; call after a pick, an edit or a viewport change. */
  refreshHandles() {
    const layer = this.#handlesLayer;
    if (!layer) return;
    while (layer.firstChild) layer.removeChild(layer.firstChild);
    this.#ghostEl = null;
    this.#hoveredEdge = -1;
    const points = this.#editable() ? this.#pickedPoints() : null;
    if (!points) return;
    const id = this.#selectedId, area = this.#selectedArea;
    points.forEach(([wx, wz], idx) => {
      const at = toScreen(wx, wz, this.#getViewport());
      const grip = svgEl("rect", {
        ...handleRectAttrs(at.x, at.y, POINT_HALF),
        fill: "var(--bg-deep)", stroke: "var(--accent)", "stroke-width": "1.5", style: "cursor:move",
      });
      grip.addEventListener("mousedown", (e) => {
        if (e.button !== 0) return;
        e.stopPropagation();
        this.#pointDrag = { id, area, idx, moved: false };
      });
      grip.addEventListener("click", (e) => e.stopPropagation());
      layer.appendChild(grip);
    });
  }

  /** The outline or run being traced, as it will be kept. */
  paint(painter) {
    if (!this.#trace || this.#trace.points.length < 2) return;
    if (this.#trace.line) {
      painter.polyline(this.#trace.points, { stroke: "var(--accent)", strokeAlpha: 0.15, cap: "round", join: "round",
                                             width: LINE_WIDTH / painter.screenPx(1) });
      painter.polyline(this.#trace.points, { stroke: "var(--accent)", strokeAlpha: 0.95, width: 2, cap: "round", join: "round" });
      return;
    }
    painter.ring(this.#trace.points, { fill: "var(--accent)", fillAlpha: 0.12, stroke: "var(--accent)",
                                       strokeAlpha: 0.95, width: 2 });
  }

  /** The modifier whose outline a press lands in, the smallest outline first, so a hill drawn inside a broad
   *  roughness is what a click on it reaches. */
  #hitTest(bx, bz) {
    let best = null, bestFit = Infinity;
    for (const modifier of this.#getModifiers()) {
      (modifier.mask?.areas ?? []).forEach((area, index) => {
        const points = areaPoints(area);
        if (isLineArea(area)) {
          // A run is picked within its band, and a thin one still within the slack, so it is never unreachable.
          const reach = Math.max((area.width ?? LINE_WIDTH) / 2, PICK_SLACK);
          if (points && bestFit > 0 && distanceToLine(points, bx + 0.5, bz + 0.5) <= reach) {
            best = { id: modifier.id, area: index };
            bestFit = 0;
          }
          return;
        }
        if (!points || points.length < 3) return;
        const inside = pointInRing(bx + 0.5, bz + 0.5, points);
        const fit = inside ? ringArea(points) : nearestEdge(points, bx + 0.5, bz + 0.5) <= PICK_SLACK ? 0 : null;
        if (fit !== null && fit < bestFit) { best = { id: modifier.id, area: index }; bestFit = fit; }
      });
    }
    return best;
  }
}

function runLength(points) {
  let length = 0;
  for (let i = 1; i < points.length; i++) length += Math.hypot(points[i][0] - points[i - 1][0], points[i][1] - points[i - 1][1]);
  return length;
}

function ringArea(points) {
  let twice = 0;
  for (let i = 0; i < points.length; i++) {
    const [ax, az] = points[i], [bx, bz] = points[(i + 1) % points.length];
    twice += ax * bz - bx * az;
  }
  return Math.abs(twice) / 2;
}

function nearestEdge(points, x, z) {
  let nearest = Infinity;
  for (let i = 0; i < points.length; i++) {
    const [ax, az] = points[i], [bx, bz] = points[(i + 1) % points.length];
    nearest = Math.min(nearest, distToSegment(x, z, ax, az, bx, bz));
  }
  return nearest;
}
