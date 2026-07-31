/**
 * CanvasPainter — the 2-D surface a canvas's world layers paint onto, and the vocabulary they paint in.
 *
 * The counterpart to the SVG layer stack: where `layer-stack.js` hands out retained `<g>` groups that
 * hold their content between frames, this hands out a context that is redrawn each frame at the current
 * scale. Four things it owns, because each of them is a trap when written per canvas:
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
 *     move. Every primitive below resolves its own colours through it, so a layer keeps writing
 *     `var(--accent)` and never has to know the trap exists.
 *   • **The primitives.** `rect`/`line`/`circle`/`poly`/`text`/`image` and the rest, in **world**
 *     coordinates: each maps through `toSurface` (identity on the surfaces whose world coordinates are
 *     already the surface's) and applies one style vocabulary. They live here rather than privately on a
 *     canvas because all three drawing surfaces need the same ones, and because two of the three knobs a
 *     primitive sets — a screen-space stroke width and a resolved colour — are the painter's to divide
 *     out and resolve, not the caller's to remember.
 *
 * The style vocabulary is one object, so a layer says what it wants once and never in two dialects:
 *
 *     fill · fillAlpha · fillRule     the filled half; omit `fill` to skip it
 *     stroke · strokeAlpha · width    the stroked half; `width` is in SCREEN pixels at every zoom
 *     dash · cap · join               dash lengths are screen pixels too (an array, or "6 4")
 *     alpha                           the whole primitive, fill and stroke together
 *
 * Paint order is the order the caller draws in; `layer` exists so a surface can name the phases it paints
 * in and expose that list, since a canvas offers nothing to query the way `data-layer` does.
 */

import { ringToPath, polyToPath } from "./svg.js";

/** The device pixel ratio to render at. Clamped: past 2 the buffer grows faster than the sharpness. */
const MAX_DPR = 2;

/** World coordinates are the surface's own unless a canvas fits its world through a transform. */
const IDENTITY = (x, z) => ({ x, y: z });

/**
 * Canvas text needs a family named outright — an SVG `<text>` inherits one from the page, a context has
 * nothing to inherit from. This is the page's own stack (`css/app.css`), so a painted label matches the
 * one beside it in the svg overlay; `style.font` overrides it where a caller wants figures to line up.
 */
const FONT_STACK = "'Helvetica Neue', Helvetica, Arial, sans-serif";

export class CanvasPainter {
  #canvas;
  #ctx;
  #dpr    = 1;
  #width  = 0;   // CSS pixels
  #height = 0;
  #scale  = 1;   // the viewport scale in force, so screenPx can divide it back out
  #tokens = new Map();
  #themeWatch = null;
  #probe  = null;  // context used to ask whether a colour parses (see #accepts)
  #layers = [];    // the names painted this frame, in paint order
  #toSurface = IDENTITY;

  constructor(canvasEl, toSurface = IDENTITY) {
    this.#canvas = canvasEl;
    this.#ctx = canvasEl.getContext("2d");
    this.#toSurface = toSurface ?? IDENTITY;
    this.#watchTheme();
  }

  get ctx() { return this.#ctx; }
  get canvas() { return this.#canvas; }
  get dpr() { return this.#dpr; }
  /** The phases painted in the last frame, bottom first — the queryable form of a layer stack. */
  get layers() { return this.#layers.slice(); }

  /**
   * The world→surface map. Identity where a canvas draws in the surface's own coordinates (plan, sketch);
   * a bbox fit where it does not (the world canvas), which is re-derived whenever the viewport is resized.
   * Set it, and every primitive follows — the stroke widths do not, because the viewport scale is the only
   * part of the chain a screen width has to survive.
   */
  get toSurface() { return this.#toSurface; }
  set toSurface(fn) { this.#toSurface = fn ?? IDENTITY; }

  /** A world point in surface coordinates — what a caller needs when it builds a path itself. */
  point(x, z) { return this.#toSurface(x, z); }

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
   * unevaluated from `getComputedStyle` — a custom property computes to its own text — and the context
   * parses colour syntax itself, so the value normally goes straight through.
   *
   * Normally, but not provably: canvas colour parsing is specified as "parse as a CSS `<color>`", yet it
   * has historically been a *different* parser from the one that styles the page, so a token the
   * stylesheet accepts is not guaranteed to be a token the context accepts. Rather than pin that to a
   * support matrix that varies by engine and version, each resolved value is tried against the context
   * once and demoted to `fallback` if it does not take — which also covers a token that is simply
   * absent. Both cases would otherwise paint in whatever colour the previous draw call left behind.
   */
  token(name, fallback = "#888") {
    const hit = this.#tokens.get(name);
    if (hit !== undefined) return hit;
    const raw = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    let value = raw || fallback;
    if (raw && !this.#accepts(value)) {
      console.warn(`[painter] ${name} = "${raw}" is not a colour this context parses; using ${fallback}`);
      value = fallback;
    }
    this.#tokens.set(name, value);
    return value;
  }

  /**
   * Does the context parse this colour? A rejected assignment leaves the property at its previous value,
   * so the test is "did it move" — against two sentinels, since the value under test may itself be one.
   */
  #accepts(value) {
    const probe = this.#probe ??= this.#canvas.getContext("2d");
    if (!probe) return true;   // no context to ask (a stubbed surface) — take the value as given
    for (const sentinel of ["#000000", "#ffffff"]) {
      probe.fillStyle = sentinel;
      probe.fillStyle = value;
      if (probe.fillStyle !== sentinel) return true;
    }
    return false;
  }

  /** Drop the resolved tokens — the theme moved, so every colour is stale. */
  invalidateTokens() { this.#tokens.clear(); }

  /**
   * A style's colour, ready for the context. A layer writes the same `var(--accent)` it wrote as an SVG
   * attribute and this resolves it (with `var(--x, #fallback)` honoured); anything else — a hex, an
   * `rgb()`, a `CanvasPattern` — passes through untouched, since only custom properties are the trap.
   */
  color(value, fallback = "#888") {
    if (!value || typeof value !== "string") return value || null;
    if (!value.startsWith("var(")) return value;
    const inner = value.slice(4, -1);
    const comma = inner.indexOf(",");
    if (comma < 0) return this.token(inner.trim(), fallback);
    return this.token(inner.slice(0, comma).trim(), inner.slice(comma + 1).trim());
  }

  // ── primitives ────────────────────────────────────────────────────────────────
  // Every one takes WORLD coordinates and one style object (see the header). They are the painted twin of
  // render/svg.js's element factory, and share its path builders outright: `new Path2D(d)` takes SVG path
  // data, so `ringToPath`/`polyToPath` serve both dialects and the geometry cannot drift between them.

  /** Fill and/or stroke a world box `{min_x, min_z, max_x, max_z}`. */
  rect(box, style = {}) {
    const p1 = this.#toSurface(box.min_x, box.min_z), p2 = this.#toSurface(box.max_x, box.max_z);
    const x = Math.min(p1.x, p2.x), y = Math.min(p1.y, p2.y);
    const width = Math.abs(p2.x - p1.x), height = Math.abs(p2.y - p1.y);
    const ctx = this.#ctx, prev = ctx.globalAlpha;
    if (this.#useFill(style))   { ctx.globalAlpha = this.#alphaOf(style, style.fillAlpha);   ctx.fillRect(x, y, width, height); }
    if (this.#useStroke(style)) { ctx.globalAlpha = this.#alphaOf(style, style.strokeAlpha); ctx.strokeRect(x, y, width, height); }
    ctx.globalAlpha = prev;
  }

  /** A straight run between two world points. */
  line(x1, z1, x2, z2, style = {}) {
    const from = this.#toSurface(x1, z1), to = this.#toSurface(x2, z2);
    this.#ctx.beginPath();
    this.#ctx.moveTo(from.x, from.y);
    this.#ctx.lineTo(to.x, to.y);
    this.#strokeCurrent(style);
  }

  /**
   * Many runs as ONE path — the shape a grid wants. A canvas's cost is per draw call rather than per
   * element, so a few hundred gridlines in one `stroke()` is the difference the SVG version could not make.
   */
  segments(runs, style = {}) {
    const ctx = this.#ctx;
    ctx.beginPath();
    for (const run of runs) {
      const from = this.#toSurface(run.x1, run.z1), to = this.#toSurface(run.x2, run.z2);
      ctx.moveTo(from.x, from.y);
      ctx.lineTo(to.x, to.y);
    }
    this.#strokeCurrent(style);
  }

  /** A circle of `radius` WORLD units, so it grows with the zoom the way a drawn thing does. */
  circle(centreX, centreZ, radius, style = {}) {
    const centre = this.#toSurface(centreX, centreZ);
    const edge = this.#toSurface(centreX + radius, centreZ);
    this.#arc(centre.x, centre.y, Math.hypot(edge.x - centre.x, edge.y - centre.y), style);
  }

  /**
   * A dot whose radius is NOT in world units — the primitive for anything that must stay findable when
   * the thing it marks is one block across. `radius` is in surface units (what an SVG `r` was, so it
   * still grows with the zoom but never shrinks with the map); `radiusPx` pins it to screen pixels.
   */
  dot(centreX, centreZ, style = {}) {
    const centre = this.#toSurface(centreX, centreZ);
    const radius = style.radiusPx != null ? this.screenPx(style.radiusPx) : (style.radius ?? 5);
    this.#arc(centre.x, centre.y, radius, style);
  }

  /** The radial region types (cylinder / circle / sphere): the ellipse inscribed in a world box. */
  ellipse(box, style = {}) {
    const p1 = this.#toSurface(box.min_x, box.min_z), p2 = this.#toSurface(box.max_x, box.max_z);
    this.#ctx.beginPath();
    this.#ctx.ellipse((p1.x + p2.x) / 2, (p1.y + p2.y) / 2,
                      Math.abs(p2.x - p1.x) / 2, Math.abs(p2.y - p1.y) / 2, 0, 0, Math.PI * 2);
    this.#paintCurrent(style);
  }

  /** SVG path data already in surface coordinates — the seam the shared path builders come through. */
  path(data, style = {}) {
    if (!data) return;
    const shape = new Path2D(data);
    const ctx = this.#ctx, prev = ctx.globalAlpha;
    if (this.#useFill(style))   { ctx.globalAlpha = this.#alphaOf(style, style.fillAlpha);   ctx.fill(shape, style.fillRule ?? "nonzero"); }
    if (this.#useStroke(style)) { ctx.globalAlpha = this.#alphaOf(style, style.strokeAlpha); ctx.stroke(shape); }
    ctx.globalAlpha = prev;
  }

  /** A closed ring of world points, with the same optional Bézier `controls` an authored shape carries. */
  ring(points, style = {}, controls = {}) {
    if (!points?.length) return;
    this.path(ringToPath(points, this.#toSurface, controls), style);
  }

  /** A polygon with holes (`{exterior, holes}` or `{polygons}`) — even-odd, so the holes read as holes. */
  poly(polygon, style = {}) {
    if (!polygon) return;
    this.path(polyToPath(polygon, this.#toSurface), { fillRule: "evenodd", ...style });
  }

  /**
   * Text at a world point. `size` is in world units, so it scales with the map exactly as an SVG `<text>`
   * inside the viewport group did; screen-space labels stay in the svg overlay, where they always were.
   * `halo` is the canvas spelling of `paint-order: stroke` — the casing that keeps a label readable over
   * whatever it lands on.
   */
  text(content, x, z, style = {}) {
    const point = this.#toSurface(x, z);
    const at = { x: point.x + (style.dx ?? 0), y: point.y + (style.dy ?? 0) };
    const ctx = this.#ctx, prev = ctx.globalAlpha;
    ctx.font = `${style.weight ? `${style.weight} ` : ""}${style.size ?? 12}px ${style.font ?? FONT_STACK}`;
    ctx.textAlign = style.align ?? "center";
    ctx.textBaseline = style.baseline ?? "middle";
    ctx.globalAlpha = this.#alphaOf(style, 1);
    if (style.halo) {
      ctx.strokeStyle = this.color(style.halo);
      ctx.lineWidth = this.screenPx(style.haloWidth ?? 3);
      ctx.setLineDash([]);
      ctx.lineJoin = "round";
      ctx.strokeText(content, at.x, at.y);
    }
    ctx.fillStyle = this.color(style.fill ?? style.color);
    ctx.fillText(content, at.x, at.y);
    ctx.globalAlpha = prev;
  }

  /**
   * A decoded bitmap stretched over a world box. `smooth` off is the `image-rendering: pixelated` that
   * keeps a block layer's cells square — the one case where interpolation is the defect, not the polish.
   */
  image(bitmap, box, style = {}) {
    if (!bitmap) return;
    const p1 = this.#toSurface(box.min_x, box.min_z), p2 = this.#toSurface(box.max_x, box.max_z);
    const ctx = this.#ctx, prev = ctx.globalAlpha, prevSmooth = ctx.imageSmoothingEnabled;
    ctx.globalAlpha = this.#alphaOf(style, 1);
    ctx.imageSmoothingEnabled = style.smooth ?? false;
    ctx.drawImage(bitmap, Math.min(p1.x, p2.x), Math.min(p1.y, p2.y),
                  Math.abs(p2.x - p1.x), Math.abs(p2.y - p1.y));
    ctx.imageSmoothingEnabled = prevSmooth;
    ctx.globalAlpha = prev;
  }

  // ── style application ─────────────────────────────────────────────────────────

  #arc(x, y, radius, style) {
    this.#ctx.beginPath();
    this.#ctx.arc(x, y, radius, 0, Math.PI * 2);
    this.#paintCurrent(style);
  }

  /** Fill then stroke the path the caller just built, each at its own alpha. */
  #paintCurrent(style) {
    const ctx = this.#ctx, prev = ctx.globalAlpha;
    if (this.#useFill(style))   { ctx.globalAlpha = this.#alphaOf(style, style.fillAlpha);   ctx.fill(style.fillRule ?? "nonzero"); }
    if (this.#useStroke(style)) { ctx.globalAlpha = this.#alphaOf(style, style.strokeAlpha); ctx.stroke(); }
    ctx.globalAlpha = prev;
  }

  #strokeCurrent(style) {
    const ctx = this.#ctx, prev = ctx.globalAlpha;
    if (this.#useStroke(style)) { ctx.globalAlpha = this.#alphaOf(style, style.strokeAlpha); ctx.stroke(); }
    ctx.globalAlpha = prev;
  }

  /** Alphas multiply: a primitive-wide `alpha` scales the half-specific one rather than replacing it. */
  #alphaOf(style, half) { return (style.alpha ?? 1) * (half ?? 1); }

  #useFill(style) {
    if (!style.fill) return false;
    this.#ctx.fillStyle = this.color(style.fill);
    return true;
  }

  #useStroke(style) {
    if (!style.stroke) return false;
    const ctx = this.#ctx;
    ctx.strokeStyle = this.color(style.stroke);
    ctx.lineWidth = this.screenPx(style.width ?? 1);
    ctx.setLineDash(this.#dash(style.dash));
    ctx.lineCap = style.cap ?? "butt";
    ctx.lineJoin = style.join ?? "miter";
    return true;
  }

  /** Dash lengths are screen pixels like the width, and take either an array or an SVG dasharray string. */
  #dash(dash) {
    if (!dash) return [];
    const lengths = Array.isArray(dash) ? dash : String(dash).trim().split(/[\s,]+/).map(Number);
    return lengths.map(length => this.screenPx(length));
  }

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
