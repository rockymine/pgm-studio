/**
 * SideviewCanvas — depth-tinted vertical cross-section of the map.
 *
 * Renders layer_segments.parquet data projected onto either the X-Y or Z-Y plane.
 * A draggable horizontal line represents the max build height.
 *
 * Data format from /api/map/<name>/segments:
 *   { axis, primary_min, primary_count, y_min, y_count, depth: int[] }
 *   depth[p_idx * y_count + y_idx] = 0-255 (0=nearest) or -1 (empty)
 */

// Color stops: nearest block = light stone, farthest = very dark
const _NEAR  = [200, 195, 188];
const _FAR   = [40,  38,  35];
const _LINE_COLOR  = "rgba(250, 110, 50, 0.9)";   // fallback; real value from --canvas-sideview-line
const _LINE_DASH   = [5, 4];
const _HANDLE_W    = 20;
const _HANDLE_H    = 10;
const _HIT_RADIUS  = 7; // px — snap zone around the line

export class SideviewCanvas {
  #canvas;
  #ctx;
  #data       = null;   // server response object
  #buildHeight = null;  // world Y, or null
  #dragging   = false;
  #scale      = 4;
  #offsetX    = 0;
  #offsetY    = 0;
  #onHeightChange;      // (worldY: number) => void
  #offscreen  = null;   // pre-rendered block image (rebuilt when data changes)

  constructor(canvasEl, { onHeightChange } = {}) {
    this.#canvas = canvasEl;
    this.#ctx    = canvasEl.getContext("2d");
    this.#onHeightChange = onHeightChange;
    this._attachPointerListeners();
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

  resize() {
    const wrap = this.#canvas.parentElement;
    this.#canvas.width  = wrap.clientWidth;
    this.#canvas.height = wrap.clientHeight;
    this._computeLayout();
    this._render();
  }

  // ── Layout ─────────────────────────────────────────────────────────────────

  _computeLayout() {
    if (!this.#data) return;
    const { primary_count, y_count } = this.#data;
    const pad = 20;
    const avW = this.#canvas.width  - 2 * pad - _HANDLE_W;
    const avH = this.#canvas.height - 2 * pad;
    const s = Math.max(1, Math.min(
      Math.floor(avW / primary_count),
      Math.floor(avH / y_count),
    ));
    this.#scale   = s;
    this.#offsetX = Math.floor((this.#canvas.width  - _HANDLE_W - primary_count * s) / 2);
    this.#offsetY = Math.floor((this.#canvas.height - y_count   * s) / 2);
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
    const ctx = this.#ctx;
    const W   = this.#canvas.width;
    const H   = this.#canvas.height;

    ctx.clearRect(0, 0, W, H);
    ctx.fillStyle = getComputedStyle(document.documentElement)
      .getPropertyValue("--bg-canvas").trim() || "#111";
    ctx.fillRect(0, 0, W, H);

    if (!this.#data || !this.#offscreen) {
      ctx.fillStyle = getComputedStyle(document.documentElement)
        .getPropertyValue("--text-muted").trim() || "#888";
      ctx.font      = "14px system-ui,sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("No segment data", W / 2, H / 2);
      return;
    }

    const { primary_count, y_count } = this.#data;
    const s  = this.#scale;
    const ox = this.#offsetX;
    const oy = this.#offsetY;

    // Block image
    ctx.imageSmoothingEnabled = false;
    ctx.drawImage(this.#offscreen, ox, oy, primary_count * s, y_count * s);

    // Build height line
    if (this.#buildHeight !== null) {
      const lineY = this._lineCanvasY(this.#buildHeight);
      if (lineY !== null) {
        const x1 = ox;
        const x2 = ox + primary_count * s;
        const lineColor = getComputedStyle(document.documentElement)
          .getPropertyValue("--canvas-sideview-line").trim() || _LINE_COLOR;

        ctx.strokeStyle = lineColor;
        ctx.lineWidth   = 2;
        ctx.setLineDash(_LINE_DASH);
        ctx.beginPath();
        ctx.moveTo(x1, lineY);
        ctx.lineTo(x2, lineY);
        ctx.stroke();
        ctx.setLineDash([]);

        // Drag handle tab on right side
        const hx = x2 + 4;
        ctx.fillStyle = lineColor;
        ctx.fillRect(hx, lineY - _HANDLE_H / 2, _HANDLE_W, _HANDLE_H);

        // Y label
        ctx.fillStyle  = lineColor;
        ctx.font       = "11px system-ui,sans-serif";
        ctx.textAlign  = "right";
        ctx.textBaseline = "bottom";
        ctx.fillText(`Y ${this.#buildHeight}`, x1 - 4, lineY - 2);
      }
    }
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
    const max = (y_min != null && y_count != null) ? y_min + y_count - 1 : 255;
    const clamped = Math.max(min, Math.min(max, wy));
    if (clamped === this.#buildHeight) return;
    this.#buildHeight = clamped;
    this._render();
    this.#onHeightChange?.(clamped);
  }
}
