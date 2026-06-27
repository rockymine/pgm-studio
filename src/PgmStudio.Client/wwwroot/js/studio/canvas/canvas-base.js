/**
 * CanvasBase — shared pan/zoom machinery for editor and sketch canvases.
 *
 * Subclasses extend this and override the hook methods below.
 * All shared state is _-prefixed (convention: protected).
 *
 * See docs/contracts/geometry.md §4 (shared canvas base) for the contract.
 */

export const ZOOM_FACTOR = 1.15;
export const ZOOM_MIN    = 0.5;
export const ZOOM_MAX    = 200;

export class CanvasBase {
  _svg        = null;
  _wrap       = null;
  _scale      = 1;
  _panX       = 0;
  _panY       = 0;
  _viewportG  = null;
  _activeTool = null;

  #isDragging   = false;
  #midDragging  = false;
  #dragAnchor   = null;
  #didDrag      = false;
  #clickWasDrag = false;
  #moveState    = null;   // body-drag: { handle, lastBx, lastBz, moved } while dragging a shape/region

  constructor(svgEl, wrapEl) {
    this._svg  = svgEl;
    this._wrap = wrapEl;
    this._setupBaseEvents();
  }

  // ── subclass hooks ─────────────────────────────────────────────────────────

  /** Called after every viewport matrix update. Override to reposition overlays. */
  _onViewportChanged() {}

  /** Called after zoom scale changes (e.g. wheel zoom or focusRegion). */
  _onZoom(scale) {}

  /**
   * Called on left-mousedown for tool modes (i.e. not "move" and not null).
   * Also called for null/"move" so the subclass can inspect activeTool and bx/bz
   * without duplicating logic — it's safe to check activeTool here.
   */
  _onToolMousedown(e, svgPt) {}

  /** Called on every mousemove, after base pan logic. For coord display, draw previews. */
  _onPointerMove(e, svgPt) {}

  /** Called on left-mouseup, after drag state is reset. For draw completion etc. */
  _onToolMouseup(e, svgPt) {}

  /** Called on canvas click when null or move tool, after drag suppression check. */
  _onCanvasClick(e, svgPt) {}

  /** Called on mouseleave. */
  _onMouseleave() {}

  /**
   * Override to intercept mousemove before pan logic.
   * Return true to consume the event (prevents pan from running).
   */
  _onResizeMove(e) { return false; }

  /**
   * Override to intercept mouseup before drag-reset logic.
   * Return true to consume the event.
   */
  _onResizeUp(e) { return false; }

  // ── body-drag move (shared affordance; see CV10) ─────────────────────────────
  // Map an SVG point to world coords {x,z}. Default = identity (sketch: world == svg base coords);
  // canvases that fit through a transform (editor) override with their inverse.
  _toWorld(svgPt) { return { x: svgPt.x, z: svgPt.y }; }

  /** With the select tool, return a movable handle (shape id / region node) at world {x,z}, or null. */
  _hitMovable(world) { return null; }

  /** Called once when a body-drag begins, with the grabbed handle + the world point grabbed. */
  _moveStart(handle, world) {}

  /** Absolute move (snap-aware, S9): place the handle at start + (dx,dz) world units from the grab point.
   *  Return true if handled (skips the incremental path); default false → fall back to _moveBy. */
  _moveTo(handle, dx, dz) { return false; }

  /** Translate the grabbed handle by (dx,dz) blocks — live (no persist). The incremental fallback. */
  _moveBy(handle, dx, dz) {}

  /** The drag ended — persist the grabbed handle's final position. */
  _commitMove(handle) {}

  // ── shared API ─────────────────────────────────────────────────────────────

  _applyViewportTransform() {
    if (!this._viewportG) return;
    this._viewportG.setAttribute(
      "transform",
      `matrix(${this._scale},0,0,${this._scale},${this._panX},${this._panY})`,
    );
    this._onViewportChanged();
  }

  _clientToSvg(clientX, clientY) {
    const rect = this._svg.getBoundingClientRect();
    return {
      x: (clientX - rect.left - this._panX) / this._scale,
      y: (clientY - rect.top  - this._panY) / this._scale,
    };
  }

  // ── event setup ────────────────────────────────────────────────────────────

  _setupBaseEvents() {
    // Zoom wheel
    this._svg.addEventListener("wheel", (e) => {
      if (!this._viewportG) return;
      e.preventDefault();
      const factor   = e.deltaY < 0 ? ZOOM_FACTOR : 1 / ZOOM_FACTOR;
      const newScale = Math.max(ZOOM_MIN, Math.min(ZOOM_MAX, this._scale * factor));
      const rect     = this._svg.getBoundingClientRect();
      const mx = e.clientX - rect.left;
      const my = e.clientY - rect.top;
      this._panX  = mx - (mx - this._panX) * (newScale / this._scale);
      this._panY  = my - (my - this._panY) * (newScale / this._scale);
      this._scale = newScale;
      this._applyViewportTransform();
      this._onZoom(this._scale);
    }, { passive: false });

    // Pan / tool start
    this._svg.addEventListener("mousedown", (e) => {
      if (!this._viewportG) return;
      if (e.button === 1) {
        e.preventDefault();
        this.#midDragging = true;
        this.#dragAnchor  = { x: e.clientX, y: e.clientY, panX: this._panX, panY: this._panY };
        return;
      }
      if (e.button !== 0) return;
      if (this._activeTool === "move" || this._activeTool === null) e.preventDefault();
      const svgPt = this._clientToSvg(e.clientX, e.clientY);
      this.#isDragging = true;
      this.#didDrag    = false;
      this.#dragAnchor = { x: e.clientX, y: e.clientY, panX: this._panX, panY: this._panY };
      // Body-drag: with the select tool, grabbing a movable shape/region drags it instead of panning.
      this.#moveState = null;
      if (this._activeTool === "select") {
        const world  = this._toWorld(svgPt);
        const handle = world ? this._hitMovable(world) : null;
        if (handle != null) {
          this.#moveState = { handle, grab: world, lastBx: Math.floor(world.x), lastBz: Math.floor(world.z), moved: false };
          this._moveStart(handle, world);
        }
      }
      this._onToolMousedown(e, svgPt);
    });

    // Drag / pointer move
    document.addEventListener("mousemove", (e) => {
      if (this._onResizeMove(e)) return;
      if (!this._viewportG) return;

      if (this.#midDragging && this.#dragAnchor) {
        this._panX = this.#dragAnchor.panX + (e.clientX - this.#dragAnchor.x);
        this._panY = this.#dragAnchor.panY + (e.clientY - this.#dragAnchor.y);
        this._applyViewportTransform();
      } else if (this.#isDragging && this.#dragAnchor) {
        const dx = e.clientX - this.#dragAnchor.x;
        const dy = e.clientY - this.#dragAnchor.y;
        if (!this.#didDrag && Math.sqrt(dx * dx + dy * dy) > 4) this.#didDrag = true;
        if (this.#didDrag && this.#moveState) {
          const world = this._toWorld(this._clientToSvg(e.clientX, e.clientY));
          if (world) {
            const ms = this.#moveState;
            // Absolute, snap-aware path (S9) if the subclass handles it; else incremental block deltas.
            if (this._moveTo(ms.handle, world.x - ms.grab.x, world.z - ms.grab.z, e.altKey)) {
              ms.moved = true; this._svg.style.cursor = "grabbing";
            } else {
              const bx = Math.floor(world.x), bz = Math.floor(world.z);
              const mdx = bx - ms.lastBx, mdz = bz - ms.lastBz;
              if (mdx || mdz) { this._moveBy(ms.handle, mdx, mdz); ms.lastBx = bx; ms.lastBz = bz; ms.moved = true; this._svg.style.cursor = "grabbing"; }
            }
          }
        } else if (this.#didDrag && this._activeTool === "move") {
          this._panX = this.#dragAnchor.panX + dx;
          this._panY = this.#dragAnchor.panY + dy;
          this._applyViewportTransform();
        }
      }

      this._onPointerMove(e, this._clientToSvg(e.clientX, e.clientY));
    });

    // Release
    document.addEventListener("mouseup", (e) => {
      if (this._onResizeUp(e)) return;
      if (e.button === 1) { this.#midDragging = false; this.#dragAnchor = null; return; }
      if (e.button !== 0) return;
      if (this.#isDragging) {
        this.#clickWasDrag = this.#didDrag;
        this.#isDragging   = false;
        this.#didDrag      = false;
        this.#dragAnchor   = null;
      }
      if (this.#moveState) {
        if (this.#moveState.moved) { this._commitMove(this.#moveState.handle); this._svg.style.cursor = ""; }
        this.#moveState = null;
      }
      this._onToolMouseup(e, this._clientToSvg(e.clientX, e.clientY));
    });

    // Click (select / inspect)
    this._svg.addEventListener("click", (e) => {
      if (this.#clickWasDrag) { this.#clickWasDrag = false; return; }
      if (this._activeTool !== null && this._activeTool !== "select") return;
      this._onCanvasClick(e, this._clientToSvg(e.clientX, e.clientY));
    });

    // Mouseleave
    this._svg.addEventListener("mouseleave", () => this._onMouseleave());
  }
}
