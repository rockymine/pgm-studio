/**
 * CanvasBase — shared pan/zoom machinery for editor and sketch canvases.
 *
 * Subclasses extend this and override the hook methods below.
 * All shared state is _-prefixed (convention: protected).
 *
 * See docs/client/canvas-interaction.md §3 (the shared base) for the contract.
 */

import { toScreen } from "../geometry/transform.js";
import { showLayers } from "../render/layer-stack.js";

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
  #ro           = null;   // ResizeObserver on the wrap (see _observeResize)
  #chromeFrame  = 0;      // pending rAF for the viewport-chrome repaint (see _applyViewportTransform)
  #pendingZoom  = null;   // scale awaiting report on that frame (see _reportZoom)
  #iso          = null;   // lazily-created WebGL iso renderer (see _enterIso)
  #isoBusy      = null;   // the "still building" overlay, created with it
  _isoOn        = false;
  _fitPending   = false;  // a fit asked for while the wrap had no layout box; run once it gets one
  _viewSize     = null;   // last measurement (see _size) — per-frame paths read this, not the DOM

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

  /**
   * Push the viewport matrix, and repaint the chrome that depends on it — but only **once per frame**.
   *
   * The matrix goes on synchronously: it is one attribute, it is what makes the surface feel attached to
   * the pointer, and it must not lag the input. The chrome (`_onViewportChanged` — grid, work area, scale
   * bar, overlays) is a DOM rebuild, and doing that inside the input handler is what stalls a zoom: a wheel
   * burst is many events per frame, so the work is repeated several times over for a single painted frame.
   * With the main thread behind, the compositor has nothing new to raster and re-uses the previous one
   * scaled by the new matrix — which reads as the picture going soft until it catches up.
   *
   * Coalescing to one rAF makes a burst cost one rebuild instead of N, and rAF fires before paint, so the
   * chrome is still correct in the frame the user sees. Panning never showed this because its grid memo
   * usually hits; zoom moves the snapped extent almost every tick, so nothing absorbed the repetition.
   */
  _applyViewportTransform() {
    if (!this._viewportG) return;
    this._viewportG.setAttribute(
      "transform",
      `matrix(${this._scale},0,0,${this._scale},${this._panX},${this._panY})`,
    );
    this.#scheduleChrome();
  }

  #scheduleChrome() {
    if (this.#chromeFrame) return;
    this.#chromeFrame = requestAnimationFrame(() => {
      this.#chromeFrame = 0;
      this._onViewportChanged();
      if (this.#pendingZoom !== null) { const z = this.#pendingZoom; this.#pendingZoom = null; this._onZoom(z); }
    });
  }

  /**
   * Report a new zoom scale — on the next frame, not now. The readout is chrome like the rest, and on the
   * plan canvas it crosses into .NET (`plan-bridge`'s `fire("OnZoom")`), so a per-tick call marshals an
   * interop message and re-renders a component for every wheel event. Measured, that was the larger half of
   * the zoom stall once the grid stopped rebuilding — the grid was only the visible part.
   */
  _reportZoom(scale) {
    this.#pendingZoom = scale;
    this.#scheduleChrome();
  }

  /** Run any pending chrome repaint now. For callers that must read the chrome in the same tick they
   * moved the viewport (a fit followed by a measurement, a test asserting on the grid). */
  _flushViewportChrome() {
    if (!this.#chromeFrame) return;
    cancelAnimationFrame(this.#chromeFrame);
    this.#chromeFrame = 0;
    this._onViewportChanged();
    if (this.#pendingZoom !== null) { const z = this.#pendingZoom; this.#pendingZoom = null; this._onZoom(z); }
  }

  _clientToSvg(clientX, clientY) {
    const rect = this._svg.getBoundingClientRect();
    return {
      x: (clientX - rect.left - this._panX) / this._scale,
      y: (clientY - rect.top  - this._panY) / this._scale,
    };
  }

  /** The viewport as plain data — what the controllers' `getViewport` closures hand around. */
  _viewport() { return { scale: this._scale, panX: this._panX, panY: this._panY }; }

  /** World → screen/SVG. The forward direction of `_clientToSvg`. */
  _toScreen(wx, wz) { return toScreen(wx, wz, this._viewport()); }

  /**
   * The drawing surface's size. Subtracts the `.svg-area` padding (12px each side) so the svg's viewBox
   * equals its rendered size — otherwise `max-width:100%` shrinks the svg while the viewBox stays larger
   * and the client→world mapping drifts, worse toward the right/bottom.
   *
   * Measuring forces a layout, so the result is cached in `_viewSize`: per-frame paths (grids, overlays)
   * read the cache; only a resize re-measures.
   */
  _size() {
    this._viewSize = { w: (this._wrap.clientWidth || 600) - 24, h: (this._wrap.clientHeight || 600) - 24 };
    return this._viewSize;
  }

  /** False while the canvas sits in a `display:none` phase, where `_size()` returns its fallback. */
  _hasBox() { return this._wrap.clientWidth > 0 && this._wrap.clientHeight > 0; }

  /** Size the `<svg>` to its wrap. Returns the size so callers can carry on with it. */
  _resizeSvg() {
    const { w, h } = this._size();
    this._svg.setAttribute("width", w);
    this._svg.setAttribute("height", h);
    this._svg.setAttribute("viewBox", `0 0 ${w} ${h}`);
    return { w, h };
  }

  /**
   * Frame a world box (identity-projected canvases). `margin` is the fraction of the surface the box may
   * fill; `pad` grows the box by that fraction of itself on each side first, so the framed thing sits
   * inside visible surroundings rather than edge to edge. Returns false when there is nothing to frame.
   */
  _fitWorldBox(box, { margin = 0.85, pad = 0 } = {}) {
    if (!box) return false;
    const { w, h } = this._size();
    const bw = (box.max_x - box.min_x) * (1 + 2 * pad), bh = (box.max_z - box.min_z) * (1 + 2 * pad);
    if (!(bw > 0) || !(bh > 0)) return false;
    this._scale = Math.min(w / bw, h / bh) * margin;
    this._panX = w / 2 - ((box.min_x + box.max_x) / 2) * this._scale;
    this._panY = h / 2 - ((box.min_z + box.max_z) / 2) * this._scale;
    this._applyViewportTransform();
    this._onZoom(this._scale);
    return true;
  }

  /**
   * Re-measure whenever the wrap's box changes. Both tools mount their canvas inside a `display:none`
   * phase, and the workspace sidebars are resizable — so the canvas owns this rather than each host
   * nudging it (a nudge from a phase switch runs before the phase div is re-rendered, and measures the
   * still-hidden element). A fit deferred for want of a layout box runs the first time a real one arrives.
   */
  _observeResize() {
    if (typeof ResizeObserver === "undefined") return;
    this.#ro = new ResizeObserver(() => {
      if (!this._hasBox()) return;
      this.resize();
      if (this._fitPending) { this._fitPending = false; this._fit(); }
    });
    this.#ro.observe(this._wrap);
  }

  /** Frame the canvas's own default extent. Overridden by surfaces that support a deferred fit. */
  _fit() {}

  // ── isometric preview ────────────────────────────────────────────────────────
  // Swap the 2-D surface for a read-only WebGL render. The renderer is imported lazily so a missing or
  // blocked WebGL stack — or any failure to load that module — degrades to "no 3-D preview" instead of
  // breaking the editor at page load; `false` tells the caller to stay in 2-D and disable the toggle.

  /** The elements the preview covers — everything drawn in 2-D. Subclasses list their own. */
  _isoLayers() { return [this._viewportG]; }

  /** Log tag for the "unavailable" warning. */
  _isoTag() { return "canvas"; }

  /** Called just before entering the preview, for whatever in-progress interaction to drop. */
  _onIsoEnter() {}

  /**
   * Swap to the 3-D surface and wait there. The world it draws comes from the server, so the view is entered
   * before there is anything to put in it: the click is answered at once and the picture arrives after.
   * Resolves false when the preview cannot run at all, which is the caller's cue to stay in 2-D.
   */
  async _enterIso() {
    if (!this.#iso) {
      try {
        const { IsoScene } = await import("../render/iso-webgl.js");
        this.#iso = new IsoScene(this._wrap);
      } catch (e) {
        console.warn(`[${this._isoTag()}] 3-D preview unavailable:`, e?.message ?? e);
        return false;
      }
    }
    this._isoOn = true;
    this._onIsoEnter();
    showLayers(this._isoLayers(), false);
    this.#iso.show();
    this._isoBusy(true);
    return true;
  }

  /** Draw a meshed world into the preview, which stops it waiting. */
  _drawIso(mesh, yawDeg, bbox) {
    if (!this.#iso || !this._isoOn) return;
    this._isoBusy(false);
    const { w, h } = this._size();
    this.#iso.render(mesh, w, h, yawDeg, bbox);
  }

  /** The "still building" overlay, created on first use and left in the wrap thereafter. */
  _isoBusy(on) {
    if (!this.#isoBusy) {
      if (!on) return;
      this.#isoBusy = document.createElement("div");
      this.#isoBusy.className = "canvas-iso-busy";
      this.#isoBusy.textContent = "Building the world…";
      this._wrap.appendChild(this.#isoBusy);
    }
    this.#isoBusy.style.display = on ? "" : "none";
  }

  _hideIso() {
    this._isoOn = false;
    this._isoBusy(false);
    this.#iso?.hide();
    showLayers(this._isoLayers(), true);
  }

  /** Release the observer + the WebGL context. Subclasses call this from their own `dispose`. */
  _disposeCanvasBase() {
    if (this.#chromeFrame) { cancelAnimationFrame(this.#chromeFrame); this.#chromeFrame = 0; }
    this.#ro?.disconnect(); this.#ro = null;
    this.#iso?.dispose(); this.#iso = null;
    this.#isoBusy?.remove(); this.#isoBusy = null;
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
      this._reportZoom(this._scale);
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
