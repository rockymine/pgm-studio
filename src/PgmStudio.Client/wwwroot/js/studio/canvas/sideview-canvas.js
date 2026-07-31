/**
 * SideviewCanvas — depth-tinted vertical cross-section of the map.
 *
 * Renders layer_segments.parquet data projected onto either the X-Y or Z-Y plane.
 * A draggable horizontal line represents the max build height.
 *
 * Data format from /api/map/<name>/segments:
 *   { axis, primary_min, primary_count, y_min, y_count, depth: int[] }
 *   depth[p_idx * y_count + y_idx] = 0-255 (0=nearest) or -1 (empty)
 *
 * This surface was always painted — it has no SVG DOM and no viewport matrix, so it never carried the
 * stale-rasterization artifact the other canvases were converted to fix. What it does share with them is
 * `CanvasPainter`, for the two things that are a trap in any 2-D surface written by hand: the backing
 * store is the CSS box times the device pixel ratio (its text and hairlines were soft on a HiDPI display
 * before), and colour tokens are resolved once and cached rather than re-read from the document on every
 * paint. `layer(name, paint)` brackets each phase in save/restore, so a setting like
 * `imageSmoothingEnabled` cannot leak out of the phase that wanted it.
 *
 * It does NOT use the painter's box primitives. Their coordinates are named for the plan's x/z axes, and
 * this surface's second axis is elevation — writing `min_z` for a Y would be a wrong-category name, so the
 * rectangle-shaped draws stay raw context calls. The primitives whose call sites take plain numbers
 * (`text`, `dot`, `line`) carry no such claim and are used.
 *
 * The viewport is its own: a fitted integer `#scale` with centring offsets, in CSS pixels, and no pan or
 * zoom — which is why it does not extend CanvasBase.
 */

import { CanvasPainter } from "../render/canvas-painter.js";

// Color stops: nearest block = light stone, farthest = very dark
const _NEAR  = [200, 195, 188];
const _FAR   = [40,  38,  35];
const _LINE_COLOR  = "var(--canvas-sideview-line, rgba(250, 110, 50, 0.9))";
const _LINE_DASH   = [5, 4];
const _HANDLE_W    = 20;
const _HANDLE_H    = 10;
const _HIT_RADIUS  = 7; // px — snap zone around the line

export class SideviewCanvas {
  #canvas;
  #painter;
  #ctx;
  #width      = 0;      // CSS pixels — the coordinates everything below is drawn and hit-tested in
  #height     = 0;
  #data       = null;   // server response object
  #buildHeight = null;  // world Y, or null
  #marker     = null;   // { p: worldPrimary, y: worldY } — the point/block cell dot, or null
  #seatOnFloor = false; // true ⇒ the Y line snaps to the marker column's floors (spawn lines)
  #dragging   = false;
  #scale      = 4;
  #offsetX    = 0;
  #offsetY    = 0;
  #onHeightChange;      // (worldY: number) => void
  #offscreen  = null;   // pre-rendered block image (rebuilt when data changes)
  #observer   = null;

  constructor(canvasEl, { onHeightChange } = {}) {
    this.#canvas  = canvasEl;
    this.#painter = new CanvasPainter(canvasEl);
    this.#ctx     = this.#painter.ctx;
    this.#onHeightChange = onHeightChange;
    this._attachPointerListeners();
    this._observeResize();
  }

  // ── Public API ─────────────────────────────────────────────────────────────

  setData(data) {
    this.#data = data;
    this.#offscreen = null;
    this._computeLayout();
    this._buildOffscreen();
    this._render();
  }

  setBuildHeight(y) {
    this.#buildHeight = (y == null) ? null : Math.round(y);
    this._render();
  }

  /** The point/block being inspected, so its cell is visible (not just the Y line). null = no marker. */
  setMarker(m) {
    this.#marker = (m && m.p != null && m.y != null) ? { p: Math.round(m.p), y: Math.round(m.y) } : null;
    this._render();
  }

  /** Constrain the Y line to the floors of the marker's column (a spawn stands on terrain). Off by
   *  default: a block region's Y may legitimately sit inside terrain. */
  setSeatOnFloor(on) {
    this.#seatOnFloor = !!on;
  }

  /**
   * Size the surface to the wrap's CONTENT box. `.svg-area` carries 12px of padding, and the canvas is a
   * `width: 100%` child of it — so measuring the padded box (`clientWidth`) describes a box 24px wider
   * than the element actually is, and the bitmap drawn for it lands stretched. CanvasBase subtracts the
   * same padding for the same reason.
   */
  resize() {
    const wrap = this.#canvas.parentElement;
    if (!wrap) return;
    const pad = getComputedStyle(wrap);
    const w = wrap.clientWidth  - parseFloat(pad.paddingLeft || 0) - parseFloat(pad.paddingRight  || 0);
    const h = wrap.clientHeight - parseFloat(pad.paddingTop  || 0) - parseFloat(pad.paddingBottom || 0);
    if (!(w > 0) || !(h > 0)) return;
    this.#painter.resize(w, h);
    this.#width  = w;
    this.#height = h;
    this._computeLayout();
    this._render();
  }

  /** Drop the resize observer and the painter's theme watcher. */
  dispose() {
    this.#observer?.disconnect();
    this.#observer = null;
    this.#painter?.dispose();
  }

  /** The phases painted in the last frame, bottom first. */
  get paintOrder() { return this.#painter?.layers ?? []; }

  // ── Layout ─────────────────────────────────────────────────────────────────

  // Follow the container without the host having to remember: this canvas is mounted inside phases that
  // are laid out after the mount, so a size arriving late is the normal case rather than the exception.
  _observeResize() {
    if (typeof ResizeObserver === "undefined") return;
    this.#observer = new ResizeObserver(() => {
      const wrap = this.#canvas.parentElement;
      if (wrap?.clientWidth > 0 && wrap?.clientHeight > 0) this.resize();
    });
    this.#observer.observe(this.#canvas.parentElement);
  }

  _computeLayout() {
    if (!this.#data) return;
    const { primary_count, y_count } = this.#data;
    const pad = 20;
    const avW = this.#width  - 2 * pad - _HANDLE_W;
    const avH = this.#height - 2 * pad;
    const s = Math.max(1, Math.min(
      Math.floor(avW / primary_count),
      Math.floor(avH / y_count),
    ));
    this.#scale   = s;
    this.#offsetX = Math.floor((this.#width  - _HANDLE_W - primary_count * s) / 2);
    this.#offsetY = Math.floor((this.#height - y_count   * s) / 2);
  }

  // ── Offscreen block image ──────────────────────────────────────────────────

  _buildOffscreen() {
    if (!this.#data) return;
    const { primary_count, y_count, depth } = this.#data;

    const tmp    = document.createElement("canvas");
    tmp.width    = primary_count;
    tmp.height   = y_count;
    const tmpCtx = tmp.getContext("2d");
    const img    = tmpCtx.createImageData(primary_count, y_count);
    const px     = img.data;

    for (let p = 0; p < primary_count; p++) {
      for (let yi = 0; yi < y_count; yi++) {
        const d = depth[p * y_count + yi];
        const row = y_count - 1 - yi; // Y-flip: world-up = canvas-up
        const i   = (row * primary_count + p) * 4;
        if (d < 0) {
          px[i + 3] = 0; // transparent
        } else {
          const t   = d / 255;
          px[i]     = (_NEAR[0] + t * (_FAR[0] - _NEAR[0])) | 0;
          px[i + 1] = (_NEAR[1] + t * (_FAR[1] - _NEAR[1])) | 0;
          px[i + 2] = (_NEAR[2] + t * (_FAR[2] - _NEAR[2])) | 0;
          px[i + 3] = 255;
        }
      }
    }
    tmpCtx.putImageData(img, 0, 0);
    this.#offscreen = tmp;
  }

  // ── Rendering ──────────────────────────────────────────────────────────────

  _render() {
    const painter = this.#painter;
    const ctx = this.#ctx;
    const W = this.#width, H = this.#height;
    if (!W || !H) return;

    // No pan or zoom here, so the frame opens at scale 1 — which leaves every draw below in CSS pixels,
    // the same units the pointer handlers hit-test in.
    painter.begin(1, 0, 0);

    painter.layer("backdrop", () => {
      ctx.fillStyle = painter.color("var(--bg-canvas, #111)");
      ctx.fillRect(0, 0, W, H);
    });

    if (!this.#data || !this.#offscreen) {
      painter.layer("empty", () => {
        painter.text("No segment data", W / 2, H / 2, {
          fill: "var(--text-muted, #888)", size: 14, font: "system-ui, sans-serif",
        });
      });
      return;
    }

    const { primary_count, y_count } = this.#data;
    const s  = this.#scale;
    const ox = this.#offsetX;
    const oy = this.#offsetY;

    painter.layer("blocks", () => {
      ctx.imageSmoothingEnabled = false;   // restored with the phase — the depth map is per-block pixels
      ctx.drawImage(this.#offscreen, ox, oy, primary_count * s, y_count * s);
    });

    painter.layer("height", () => {
      if (this.#buildHeight === null) return;
      const lineY = this._lineCanvasY(this.#buildHeight);
      if (lineY === null) return;
      const x1 = ox;
      const x2 = ox + primary_count * s;

      painter.line(x1, lineY, x2, lineY, { stroke: _LINE_COLOR, width: 2, dash: _LINE_DASH });

      // Drag handle tab on right side
      ctx.fillStyle = painter.color(_LINE_COLOR);
      ctx.fillRect(x2 + 4, lineY - _HANDLE_H / 2, _HANDLE_W, _HANDLE_H);

      painter.text(`Y ${this.#buildHeight}`, x1 - 4, lineY - 2, {
        fill: _LINE_COLOR, size: 11, font: "system-ui, sans-serif",
        align: "right", baseline: "bottom",
      });
    });

    // Point/block marker — the actual region cell, so you can see WHAT you're setting the Y of (not just
    // the level). It sits on the draggable line when editable (tracks buildHeight), else at its own Y.
    painter.layer("marker", () => {
      if (!this.#marker) return;
      const { primary_min, y_min } = this.#data;
      const pIdx = this.#marker.p - primary_min;
      const my   = (this.#buildHeight !== null) ? this.#buildHeight : this.#marker.y;
      const yi   = my - y_min;
      if (pIdx < 0 || pIdx >= primary_count || yi < 0 || yi >= y_count) return;
      painter.dot(ox + (pIdx + 0.5) * s, oy + (y_count - 1 - yi) * s + s / 2, {
        radius: Math.max(4, s * 0.55),
        fill: "var(--accent, #5b9cff)", stroke: "#fff", width: 1.5,
      });
    });
  }

  // ── Coordinate helpers ─────────────────────────────────────────────────────

  /** Canvas Y for the TOP of the build height block (the ceiling line). */
  _lineCanvasY(worldY) {
    if (!this.#data) return null;
    const { y_min, y_count } = this.#data;
    const yi = worldY - y_min; // 0-based index from bottom
    if (yi < 0 || yi > y_count) return null;
    // Y-flip: yi=0 is at bottom of canvas area
    return this.#offsetY + (y_count - yi) * this.#scale;
  }

  _worldYFromCanvasY(canvasY) {
    if (!this.#data) return null;
    const { y_min, y_count } = this.#data;
    const yi = y_count - (canvasY - this.#offsetY) / this.#scale;
    return Math.round(yi) + y_min;
  }

  _isNearLine(canvasY) {
    if (this.#buildHeight === null) return false;
    const lineY = this._lineCanvasY(this.#buildHeight);
    return lineY !== null && Math.abs(canvasY - lineY) <= _HIT_RADIUS;
  }

  // ── Pointer interaction ────────────────────────────────────────────────────

  _attachPointerListeners() {
    const canvas = this.#canvas;

    canvas.addEventListener("mousemove", (e) => {
      const cy = this._relY(e);
      if (this.#dragging) {
        this._applyDrag(cy);
        return;
      }
      canvas.style.cursor = this._isNearLine(cy) ? "ns-resize" : "default";
    });

    canvas.addEventListener("mousedown", (e) => {
      const cy = this._relY(e);
      if (this._isNearLine(cy)) {
        this.#dragging = true;
        e.preventDefault();
      } else if (this.#buildHeight !== null) {
        // Click anywhere in the block area → move line there
        const wy = this._worldYFromCanvasY(cy);
        if (wy !== null) this._applyHeight(wy);
        this.#dragging = true;
        e.preventDefault();
      }
    });

    window.addEventListener("mouseup", () => {
      this.#dragging = false;
      this.#canvas.style.cursor = "default";
    });

    canvas.addEventListener("mouseleave", () => {
      if (!this.#dragging) canvas.style.cursor = "default";
    });
  }

  _relY(e) {
    return e.clientY - this.#canvas.getBoundingClientRect().top;
  }

  _applyDrag(canvasY) {
    const wy = this._worldYFromCanvasY(canvasY);
    if (wy === null) return;
    this._applyHeight(wy);
  }

  _applyHeight(wy) {
    const { y_min, y_count } = this.#data ?? {};
    const min = y_min ?? 0;
    const max = (y_min != null && y_count != null) ? y_min + y_count : 255;
    let clamped = Math.max(min, Math.min(max, wy));

    // A spawn line rests on a floor: snap to the nearest one rather than letting the drag settle inside
    // terrain or in mid-air. Every floor in the column stays reachable, so a run with several levels
    // (an overhang, a platform above) is still selectable by dragging near it.
    const levels = this._floorLevels();
    if (levels.length > 0)
      clamped = levels.reduce((a, b) => (Math.abs(b - clamped) < Math.abs(a - clamped) ? b : a));

    if (clamped === this.#buildHeight) return;
    this.#buildHeight = clamped;
    this._render();
    this.#onHeightChange?.(clamped);
  }

  // The Y values a spawn can stand at in the marker's column: an empty cell with a solid one directly
  // below. Empty when seat-on-floor is off, there is no marker/data, or the column has no floor at all
  // (pure void) — the caller then keeps the plain range clamp.
  _floorLevels() {
    if (!this.#seatOnFloor || !this.#marker || !this.#data) return [];
    const { primary_min, primary_count, y_min, y_count, depth } = this.#data;
    if (!depth || primary_min == null || y_min == null || !y_count) return [];
    const pIdx = this.#marker.p - primary_min;
    if (pIdx < 0 || pIdx >= primary_count) return [];
    const levels = [];
    for (let i = 1; i < y_count; i++)
      if (depth[pIdx * y_count + i] === -1 && depth[pIdx * y_count + i - 1] >= 0) levels.push(y_min + i);
    return levels;
  }
}
