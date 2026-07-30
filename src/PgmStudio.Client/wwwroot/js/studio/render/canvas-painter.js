/**
 * CanvasPainter — the 2-D surface a canvas's world layers paint onto.
 *
 * The counterpart to the SVG layer stack: where `layer-stack.js` hands out retained `<g>` groups that
 * hold their content between frames, this hands out a context that is redrawn each frame at the current
 * scale. Three things it owns, because each of them is a trap when written per canvas:
 *
 *   • **The backing store.** A canvas has two sizes — its CSS box and its pixel buffer — and text and
 *     hairlines are only as sharp as the buffer. `resize` sets the buffer to the box times the device
 *     pixel ratio and bakes that ratio into the base transform, so every draw call afterwards is in CSS
 *     pixels and the sharpness comes for free.
 *   • **The viewport.** `begin` applies scale and pan to the context itself, which is what makes a
 *     screen-space stroke width just a width: `screenPx` divides by the scale in force, so a line asked
 *     to be 1px wide is 1px wide at every zoom. Nothing needs to opt into that.
 *   • **Colour tokens.** The canvas palette lives in CSS custom properties, and a 2-D context cannot
 *     read them: assigning `var(--canvas-axis)` to `strokeStyle` is a parse failure that silently keeps
 *     the previous colour, so a missed token paints in whatever the last draw used. `token` resolves one
 *     against the document and caches it — resolving forces a style recalc, so doing it per draw call is
 *     the shape to avoid. The cache is dropped when the theme flips, which is the only time the values
 *     move.
 *
 * Paint order is the order the caller draws in; `layer` exists so a surface can name the phases it paints
 * in and expose that list, since a canvas offers nothing to query the way `data-layer` does.
 */

/** The device pixel ratio to render at. Clamped: past 2 the buffer grows faster than the sharpness. */
const MAX_DPR = 2;

export class CanvasPainter {
  #canvas;
  #ctx;
  #dpr    = 1;
  #width  = 0;   // CSS pixels
  #height = 0;
  #scale  = 1;   // the viewport scale in force, so screenPx can divide it back out
  #tokens = new Map();
  #themeWatch = null;
  #layers = [];  // the names painted this frame, in paint order

  constructor(canvasEl) {
    this.#canvas = canvasEl;
    this.#ctx = canvasEl.getContext("2d");
    this.#watchTheme();
  }

  get ctx() { return this.#ctx; }
  get canvas() { return this.#canvas; }
  get dpr() { return this.#dpr; }
  /** The phases painted in the last frame, bottom first — the queryable form of a layer stack. */
  get layers() { return this.#layers.slice(); }

  /**
   * Size the surface to a CSS box. The buffer is that box times the pixel ratio; the element keeps the
   * box, so layout is unaffected. Returns the size so callers can carry on with it.
   */
  resize(width, height) {
    this.#dpr = Math.min(globalThis.devicePixelRatio || 1, MAX_DPR);
    this.#width = width;
    this.#height = height;
    this.#canvas.width  = Math.max(1, Math.round(width  * this.#dpr));
    this.#canvas.height = Math.max(1, Math.round(height * this.#dpr));
    this.#canvas.style.width  = `${width}px`;
    this.#canvas.style.height = `${height}px`;
    return { w: width, h: height };
  }

  /**
   * Start a frame: clear the surface and apply the viewport, so draw calls that follow are in world
   * coordinates. Every frame begins here — there is no retained content to leave behind.
   */
  begin(scale, panX, panY) {
    const ctx = this.#ctx;
    this.#scale = scale || 1;
    this.#layers = [];
    ctx.setTransform(this.#dpr, 0, 0, this.#dpr, 0, 0);
    ctx.clearRect(0, 0, this.#width, this.#height);
    ctx.setTransform(this.#dpr * this.#scale, 0, 0, this.#dpr * this.#scale,
                     this.#dpr * panX,        this.#dpr * panY);
  }

  /**
   * Paint one named phase. The name is recorded so the surface can report what it drew; the context is
   * saved around the callback so a phase cannot leak a style into the next one — the failure mode a
   * retained `<g>` does not have.
   */
  layer(name, paint) {
    this.#layers.push(name);
    this.#ctx.save();
    try { paint(this.#ctx); } finally { this.#ctx.restore(); }
  }

  /** Run a callback in screen space (no viewport), for anything measured in pixels rather than blocks. */
  screenSpace(paint) {
    const ctx = this.#ctx;
    ctx.save();
    ctx.setTransform(this.#dpr, 0, 0, this.#dpr, 0, 0);
    try { paint(ctx, this.#width, this.#height); } finally { ctx.restore(); }
  }

  /** A width in screen pixels, expressed in the world units currently in force. */
  screenPx(px) { return px / this.#scale; }

  /**
   * Resolve a CSS custom property to something the context accepts. `color-mix()` and the rest arrive
   * unevaluated from `getComputedStyle` — a custom property computes to its own text — but the context
   * parses colour syntax itself, so the value goes straight through. `fallback` covers a token that is
   * absent, since an unparseable value would silently keep the previous colour.
   */
  token(name, fallback = "#888") {
    const hit = this.#tokens.get(name);
    if (hit !== undefined) return hit;
    const raw = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    const value = raw || fallback;
    this.#tokens.set(name, value);
    return value;
  }

  /** Drop the resolved tokens — the theme moved, so every colour is stale. */
  invalidateTokens() { this.#tokens.clear(); }

  #watchTheme() {
    if (typeof MutationObserver === "undefined") return;
    this.#themeWatch = new MutationObserver(() => this.invalidateTokens());
    this.#themeWatch.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });
  }

  dispose() {
    this.#themeWatch?.disconnect();
    this.#themeWatch = null;
    this.#tokens.clear();
  }
}
