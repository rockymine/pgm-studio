/**
 * PlanCanvas — the drawing surface for the plan editor (the seed studio). Extends CanvasBase for
 * pan/zoom/drag and renders the cell grid, rect pieces (role-coloured, tinted by surface height),
 * translucent dashed zones, objective markers (spawn/wool/iron/destroyable/core), and the dimmed non-editable symmetry
 * mirror ghost. Pointer tools draw / move / resize pieces and zones and drop markers; all snapping,
 * hit-testing and mirror math live in plan/plan-doc.js (pure). World coordinates ARE the SVG base
 * coordinates (identity transform, like SketchCanvas); `fit()` frames the content bounds.
 *
 * The document is owned by the host bridge and shared by reference — the canvas mutates it in place for
 * live drags and reports via onChange/onSelect; the bridge persists and syncs the Blazor panels.
 */

import { CanvasBase } from "./canvas-base.js";
import { svgEl } from "../render/svg.js";
import { layerStack, INERT } from "../render/layer-stack.js";
import { CanvasPainter } from "../render/canvas-painter.js";
import { blockDataToDataUrl } from "../render/block-render.js";
import {
  ROLE_COLORS, BOX_COLORS, ZONE_COLORS, isWaterLane, canonicalZoneKind, FACING_DIR, nextFacing, rectCellsToBlocks, cellOfWorld, rectFromCells,
  markerCell, attachMarker, markerAt, markerList, MARKER_KINDS, allMarkers, viewBounds, pickAtWorld, sameSelection,
  pieceSurface, surfaceRange, surfaceFraction, isAnnotationRole, boxById, boxMembers, boxOfPiece, rectContainsCell,
  pieceMirrorImages, zoneMirrorImages, boxMirrorImages, markerMirrorImages, nearestInterface,
} from "../plan/plan-doc.js";
import { viewportWorldRect, snapOut, unionRect, gridStep, renderScaleBar } from "../render/canvas-chrome.js";

const FIT_MARGIN = 0.82;

// How long the lint-finding highlight pulse runs, in ms.
const PULSE_MS = 1600;

// The **working area** — the tinted region that anchors how big a board should be. It exists at a default
// size on a blank plan and grows to enclose the content (plus its symmetry ghosts) with a cell buffer.
// The default is expressed in BLOCKS and converted to whole cells, so the anchor stays ~60 blocks across
// whatever `globals.cell` is set to (12×12 cells at the default cell size of 5).
const DEFAULT_AREA_BLOCKS = 60;
const AREA_BUFFER_CELLS = 3;

// The cell grid spans the VISIBLE VIEWPORT, not the working area, so the surface is never fenced in.
// GRID_SNAP_CELLS is how far out the visible extent snaps: coarse enough that panning rebuilds the lines
// every few cells instead of every one.
const GRID_SNAP_CELLS = 4;


// fit() frames the working area with this much of it again added on each side, so the tinted region sits
// inside a visible margin of grid rather than filling the surface edge to edge.
const FIT_PAD_FRACTION = 0.2;
const MARKER_COLORS = { spawn: "#e0b13c", wool: "#e6e6e6", iron: "#9aa7b4", destroyable: "#6b4f9e", core: "#d4622a" };

// The 8 resize handles of a rect: ex/ez pick which cell edge each drags (−1 = min side, 1 = max, 0 = none);
// nx/nz are the handle's normalised position on the block-bounds box (for placing it in screen space).
const HANDLES = [
  { ex: -1, ez: -1, nx: 0, nz: 0, cur: "nwse-resize" },
  { ex: 1, ez: -1, nx: 1, nz: 0, cur: "nesw-resize" },
  { ex: 1, ez: 1, nx: 1, nz: 1, cur: "nwse-resize" },
  { ex: -1, ez: 1, nx: 0, nz: 1, cur: "nesw-resize" },
  { ex: 0, ez: -1, nx: 0.5, nz: 0, cur: "ns-resize" },
  { ex: 1, ez: 0, nx: 1, nz: 0.5, cur: "ew-resize" },
  { ex: 0, ez: 1, nx: 0.5, nz: 1, cur: "ns-resize" },
  { ex: -1, ez: 0, nx: 0, nz: 0.5, cur: "ew-resize" },
];

// Lerp a #rrggbb colour toward white by t∈[0,1] — a higher surface tints the fill lighter.
function tint(hex, t) {
  const n = parseInt(hex.slice(1), 16);
  const r = (n >> 16) & 255, g = (n >> 8) & 255, b = n & 255;
  const m = (c) => Math.round(c + (255 - c) * t);
  return `rgb(${m(r)},${m(g)},${m(b)})`;
}

// Evaluator-evidence styling table (tag → stroke): one place maps a rule's evidence semantics to colour, so a
// generic overlay paints every term with no per-term drawing code. `offender` is the geometry that broke the
// rule (red), `bound` the limit it should have respected (amber, dashed), `measure` a dimension line (accent,
// dashed), `context` framing geometry (dim). Unknown tags (e.g. the `slot:*` convention) fall back to context.
const EVIDENCE_STYLE = {
  offender: { stroke: "#e5534b", width: 3, dash: null },
  bound: { stroke: "#e8b923", width: 2.5, dash: "6 4" },
  measure: { stroke: "var(--accent)", width: 2, dash: "5 4" },
  context: { stroke: "var(--canvas-axis)", width: 2, dash: "2 3" },
};
const evidenceStyle = (tag) => EVIDENCE_STYLE[tag] || EVIDENCE_STYLE.context;

// Height-map ramp: monotonically-lightening deep-blue → teal → pale-gold stops. Lowest surface → darkest,
// highest → lightest, so island heights read at a glance on the dark canvas in either theme.
const HEIGHT_STOPS = [[0, [26, 44, 74]], [0.5, [46, 128, 130]], [1, [232, 214, 128]]];
function heightColor(t) {
  const v = Math.max(0, Math.min(1, t));
  let lo = HEIGHT_STOPS[0], hi = HEIGHT_STOPS[HEIGHT_STOPS.length - 1];
  for (let i = 1; i < HEIGHT_STOPS.length; i++) { if (v <= HEIGHT_STOPS[i][0]) { lo = HEIGHT_STOPS[i - 1]; hi = HEIGHT_STOPS[i]; break; } }
  const span = hi[0] - lo[0] || 1, f = (v - lo[0]) / span;
  const m = (i) => Math.round(lo[1][i] + (hi[1][i] - lo[1][i]) * f);
  return `rgb(${m(0)},${m(1)},${m(2)})`;
}

export class PlanCanvas extends CanvasBase {
  #doc = null;
  #tool = "select";                 // select | pan | piece | zone | box | spawn | wool | iron | destroyable | core | wall
  #pieceRole = "piece";             // role armed for the piece tool
  #boxKind = "hub";                 // kind armed for the box tool
  #sel = null;                      // { kind:'piece'|'zone'|'box', id } | { kind:'marker', markerKind, index }
  #zoneKind = "build";              // which kind the zone tool draws — build (open now) | water-lane (opens later)
  #drag = null;                     // { mode:'move'|'draw', ... } live pointer op
  #resize = null;                   // { handle, id, kind } while dragging a resize handle
  #cb = {};
  #cursorEl = null;

  // Derived-structure overlay (block coords from /api/plan/inspect) + which layers are visible.
  #inspect = { interfaces: [], gapLinks: [], frontline: [] };
  // Evaluator violations (cell-space evidence from /api/plan/evaluate) — the fired rules drawn on the grid.
  #violations = [];
  // Producibility evidence (from /api/plan/feasibility): the cells by which one box's geometry misses the
  // nearest thing the emitters can build. Only ever one box's at a time — the author picks it in the panel.
  #nearestMiss = null;
  // Which violation's evidence to isolate: -1 draws every violation (the default), an index draws only that one.
  #focusedViolation = -1;
  // Labels off by default keeps the canvas quiet: no piece/zone id text, no gap connectors or hop numbers.
  #overlayOn = { interfaces: true, labels: false, frontline: true, violations: true };
  #heightMap = false;               // fill pieces by a surface-height ramp + show the height inside each

  // The world layers are PAINTED, not retained: `#painter` owns a <canvas> under the svg and redraws the
  // whole world each frame at the current scale. That is what keeps a zoom sharp — there is no cached
  // rasterization for the browser to stretch — and it is why `_onViewportChanged` repaints rather than
  // just nudging a matrix. Screen-space chrome stays in the svg (`#screen`), where DOM semantics are
  // worth having and nothing is transformed.
  #painter = null;
  #canvasEl = null;
  #screen = {};                     // screen-space layers (outside the viewport transform)
  #hatch = {};                      // role → CanvasPattern for the annotation fills, rebuilt per cell size
  #hatchCell = null;                // the cell size the patterns were built for
  #pulseUntil = 0;                  // timestamp the pulse animation runs to (see pulseSubjects)
  #pulseIds = [];
  #pulseFrame = 0;
  // reference (tracing backdrop) state: the fetched block payload, its world bbox, the placement cfg,
  // and the decoded bitmap the painter blits (an <img>, since a canvas draws images, not data URLs).
  #refData = null; #refBounds = null; #refCfg = null; #refImage = null;
  // screen-space overlay (labels + selection box + resize handles)
  #overlay;

  constructor(svgEl_, wrapEl, { cursorEl, ...cb } = {}) {
    super(svgEl_, wrapEl);
    this.#cb = cb;
    this.#cursorEl = cursorEl ?? null;
    // The painted surface is created here rather than in the host markup, so the Blazor component and the
    // bridge signature are unchanged by what the world layers are drawn with.
    this.#canvasEl = document.createElement("canvas");
    this.#canvasEl.className = "world-canvas-2d";
    this._svg.parentNode.insertBefore(this.#canvasEl, this._svg);
    this.#painter = new CanvasPainter(this.#canvasEl);
    this.#build();
  }

  // ── public API ──────────────────────────────────────────────────────────────

  setDoc(doc) { this.#doc = doc; this.render(); }
  setTool(tool) {
    this.#tool = tool;
    this._activeTool = tool === "pan" ? "move" : tool;
    const draws = tool === "piece" || tool === "zone" || tool === "box" || tool === "wall" || MARKER_KINDS.includes(tool);
    this._svg.style.cursor = draws ? "crosshair" : (tool === "select" ? "default" : "");
  }
  setPieceRole(role) { this.#pieceRole = role; }
  setBoxKind(kind) { this.#boxKind = kind; }

  /** Arm which kind the zone tool draws — build (open now) or water-lane (opens mid-match). */
  setZoneKind(kind) { this.#zoneKind = canonicalZoneKind(kind); }

  // Derived-structure feed (block coords, already fanned-out excluded — authored unit only). Redraw the layer.
  setInspect(data) {
    this.#inspect = { interfaces: data.interfaces || [], gapLinks: data.gapLinks || [], frontline: data.frontline || [] };
    this.#paintWorld();
    this.#refreshOverlay();
  }
  // Evaluator-violation feed (cell-space evidence). A new feed is a fresh rule set, so any isolate-focus is
  // dropped (a stale index would point at the wrong rule). Redraw the evidence layer + screen-space measure labels.
  setViolations(violations) {
    this.#violations = Array.isArray(violations) ? violations : [];
    this.#focusedViolation = -1;
    this.#paintWorld();
    this.#refreshOverlay();
  }
  // Isolate one fired rule's evidence (from the Score panel): -1 restores the all-violations overlay.
  focusViolation(index) {
    this.#focusedViolation = Number.isInteger(index) ? index : -1;
    this.#paintWorld();
    this.#refreshOverlay();
  }
  // The violations to draw right now: the single focused one, or all when nothing is isolated.
  #shownViolations() {
    if (this.#focusedViolation < 0) return this.#violations;
    const v = this.#violations[this.#focusedViolation];
    return v ? [v] : [];
  }
  /**
   * Show one box's nearest-miss evidence, or clear it with null. `miss` is { extra, missing } — cell rects the
   * nearest producible candidate emits that the box lacks (extra) and cells the box has that it does not
   * (missing). Painted with the same offender/bound styling the evaluator evidence uses, so "8 cells differ"
   * becomes a place on the grid rather than a number.
   */
  setNearestMiss(miss) {
    this.#nearestMiss = miss && (miss.extra?.length || miss.missing?.length) ? miss : null;
    this.#paintWorld();
  }

  setOverlayVisible(key, on) {
    if (!(key in this.#overlayOn)) return;
    this.#overlayOn[key] = !!on;
    this.#paintWorld();
    this.#refreshOverlay();
  }
  // Height-map mode: pieces fill by a min..max surface ramp and carry their height number. Re-render the
  // pieces (fill) and the screen-space overlay (labels).
  setHeightMap(on) {
    this.#heightMap = !!on;
    if (!this.#doc) return;
    this.#paintWorld();
    this.#refreshOverlay();
  }

  // ── isometric preview ───────────────────────────────────────────────────────
  // Swap the top-down viewport for a read-only 3-D render of the world the plan compiles to — the ground it
  // builds and the structures standing on it, not an extrusion of the pieces. Same renderer as the sketch
  // tool's (render/iso-webgl.js), lazily loaded on first use. Entering and drawing are two steps because the
  // world comes from the server: `enterIso` returns false (leaving the 2-D viewport untouched) if the module
  // can't load or WebGL is unavailable, so the host can fall back gracefully.
  enterIso() { return this._enterIso(); }
  drawIso(mesh, yawDeg, bbox) { this._drawIso(mesh, yawDeg, bbox); }
  hideIso() { this._hideIso(); }

  _isoTag() { return "plan"; }
  _onIsoEnter() { this.#drag = null; this.#resize = null; }
  _isoLayers() { return [this.#canvasEl, this._viewportG, this.#overlay]; }

  // ── reference (tracing) backdrop ────────────────────────────────────────────
  // A real map's top-down block render, painted behind the grid so the author can trace over it. `data` is the
  // /api/map/{slug}/top-surface payload ({xs,zs,colors,min_x,min_z,max_x,max_z}); `cfg` is
  // {offset:[x,z] cells, scale, opacity}. The <image> sits at its natural world/block coords; a group transform
  // auto-centres its bbox on the symmetry origin, then applies the author's offset (cells → blocks) and scale, so
  // it shares the plan's block frame — a real 10-block lane reads as 2 cells at scale 1. The PNG is rebuilt only
  // when the payload changes; nudging offset/scale/opacity just re-applies the group transform.
  setReference(data, cfg) {
    this.#refData = data || null;
    this.#refBounds = data ? { min_x: data.min_x, min_z: data.min_z, max_x: data.max_x, max_z: data.max_z } : null;
    this.#refCfg = cfg || null;
    this.#refImage = null;
    if (data) {
      // A canvas draws an image, not a data URL, so the PNG is decoded once here and blitted thereafter.
      const img = new Image();
      img.onload = () => { this.#refImage = img; this.#paintWorld(); };
      img.src = blockDataToDataUrl(data);
    }
    this.render();
  }

  // Re-place an already-loaded backdrop after an offset/scale/opacity change (no PNG rebuild).
  updateReference(cfg) { this.#refCfg = cfg || null; this.#paintWorld(); }
  clearReference() { this.setReference(null, null); }

  /**
   * The tracing backdrop: the block PNG placed in the plan's own frame. The SVG version carried the
   * centring, offset and scale on a group transform; here the same numbers go on the context, and
   * `imageSmoothingEnabled = false` is the `image-rendering: pixelated` that kept blocks crisp.
   */
  #paintReference(ctx) {
    if (!this.#refImage || !this.#refBounds || !this.#refCfg) return;
    const b = this.#refWorldTranslate();
    ctx.globalAlpha = this.#refCfg.opacity == null ? 0.5 : Number(this.#refCfg.opacity);
    ctx.imageSmoothingEnabled = false;
    ctx.scale(b.s, b.s);
    ctx.translate(b.tx, b.tz);
    const d = this.#refBounds;
    ctx.drawImage(this.#refImage, d.min_x, d.min_z, d.max_x - d.min_x + 1, d.max_z - d.min_z + 1);
  }

  // The scale + pre-scale translation that centres the backdrop bbox on the origin, then applies the cell-space
  // offset (× cell → blocks). Shared by the transform and the fit-bounds computation so they never diverge.
  #refWorldTranslate() {
    const cell = this.#doc?.globals.cell || 5;
    const b = this.#refBounds;
    const cx = (b.min_x + b.max_x + 1) / 2, cz = (b.min_z + b.max_z + 1) / 2;   // bbox centre, blocks
    const off = this.#refCfg.offset || [0, 0];
    const s = this.#refCfg.scale > 0 ? this.#refCfg.scale : 1;
    return { s, tx: -cx + (Number(off[0]) || 0) * cell, tz: -cz + (Number(off[1]) || 0) * cell };
  }

  // The backdrop's world/block AABB after its centre/offset/scale transform — folded into fit() so loading a
  // map frames it even before any piece is drawn.
  #refWorldBounds() {
    if (!this.#refBounds || !this.#refCfg) return null;
    const b = this.#refBounds, t = this.#refWorldTranslate();
    return {
      min_x: t.s * (b.min_x + t.tx), min_z: t.s * (b.min_z + t.tz),
      max_x: t.s * (b.max_x + 1 + t.tx), max_z: t.s * (b.max_z + 1 + t.tz),
    };
  }

  getSelection() { return this.#sel; }
  select(sel) { this.#sel = sel; this.#refreshOverlay(); this.#fireSelect(); }
  clearSelection() { this.#sel = null; this.#refreshOverlay(); this.#fireSelect(); }

  // Frame the working area (union'd with any tracing backdrop), leaving a margin of grid visible around
  // it. Deferred when the wrap has no layout box yet — the tool mounts this canvas while the Draw phase is
  // still display:none, and #size()'s fallback would frame the view for a viewport that isn't the real one.
  fit() {
    if (!this._hasBox()) { this._fitPending = true; return; }
    this._fitWorldBox(unionRect(this.#workArea(), this.#refWorldBounds()),
                      { margin: FIT_MARGIN, pad: FIT_PAD_FRACTION });
  }
  _fit() { this.fit(); }

  resize() {
    const { w, h } = this._resizeSvg();
    this.#painter.resize(w, h);          // the backing store follows the box, and re-reads the pixel ratio
    if (this.#doc) this.#paintWorld();   // a bigger/smaller viewport shows more/less grid
    this.#refreshOverlay();
  }

  // ── render ────────────────────────────────────────────────────────────────────

  render() {
    if (!this.#doc) return;
    this.#paintWorld();
    this.#refreshOverlay();
    this.#cb.onChange?.();
  }

  /**
   * Paint every world layer, bottom first. This is the whole of the world's z-order, and unlike a group
   * stack it is also the whole of its cost: a layer that draws nothing costs a function call, and nothing
   * survives between frames to be stretched by a later transform.
   */
  #paintWorld() {
    if (!this.#doc || !this.#painter) return;
    this.#ensureHatch();
    const painter = this.#painter;
    painter.begin(this._scale, this._panX, this._panY);
    painter.layer("ref",       (ctx) => this.#paintReference(ctx));
    painter.layer("work",      () => this.#paintWorkArea());
    painter.layer("grid",      () => this.#paintGrid());
    painter.layer("center",    () => this.#paintCenter());
    painter.layer("ghost",     () => this.#paintGhost());
    painter.layer("zone",      () => this.#paintZones());
    painter.layer("piece",     () => this.#paintPieces());
    painter.layer("box",       () => this.#paintBoxes());
    painter.layer("inspect",   () => this.#paintInspect());
    painter.layer("violation", () => this.#paintViolations());
    painter.layer("marker",    () => this.#paintMarkers());
    painter.layer("preview",   () => this.#paintPreview());
    painter.layer("pulse",     () => this.#paintPulse());
    // The scale bar is screen-space chrome and stays in the svg, but it reads the zoom, so it is
    // refreshed on the same beat as the world.
    const { w, h } = this._viewSize ?? this._size();
    renderScaleBar(this.#screen.scale, { w, h, scale: this._scale });
  }

  /** The painted layer names of the last frame, bottom first — what `data-layer` offered on a group stack. */
  get paintOrder() { return this.#painter?.layers ?? []; }

  #clear(layer) { while (layer.firstChild) layer.removeChild(layer.firstChild); }

  // ── painting helpers ──────────────────────────────────────────────────────────

  /**
   * The diagonal hatch behind the annotation fills, as a `CanvasPattern` per role. An SVG `<pattern>` in
   * `userSpaceOnUse` tiles in world units and rotates by an attribute; a canvas pattern tiles in the
   * coordinate space in force and is rotated by a matrix on the pattern itself, so the tile is drawn
   * axis-aligned and turned 45° through `setTransform`. Sized to the cell, like the SVG version, so the
   * hatch stays proportional to pieces — and rebuilt only when the cell moves, since building it
   * rasterizes a tile.
   *
   * Buffer is a single diagonal — a reserved gap, clearly "not terrain".
   */
  #ensureHatch() {
    const cell = this.#doc?.globals.cell || 5;
    if (this.#hatchCell === cell && this.#hatch.buffer) return;
    this.#hatchCell = cell;
    const ctx = this.#painter.ctx;
    const step = Math.max(2, cell * 0.7);
    const strokeWidth = Math.max(0.6, cell * 0.14);
    // The tile is rasterized at a fixed pixel size and scaled back to world units by the pattern matrix,
    // so the hatch keeps its density however far the board is zoomed.
    const TILE_PX = 16;
    const build = (color, backdropAlpha) => {
      const tile = document.createElement("canvas");
      tile.width = TILE_PX; tile.height = TILE_PX;
      const tctx = tile.getContext("2d");
      tctx.globalAlpha = backdropAlpha;
      tctx.fillStyle = color;
      tctx.fillRect(0, 0, TILE_PX, TILE_PX);
      tctx.globalAlpha = 1;
      tctx.strokeStyle = color;
      tctx.lineWidth = Math.max(1, (strokeWidth / step) * TILE_PX);
      tctx.beginPath();
      tctx.moveTo(0, 0); tctx.lineTo(0, TILE_PX);
      tctx.stroke();
      const pattern = ctx.createPattern(tile, "repeat");
      // World units per tile pixel, then the 45° turn the SVG version got from patternTransform.
      const unit = step / TILE_PX;
      const cos = Math.cos(Math.PI / 4) * unit, sin = Math.sin(Math.PI / 4) * unit;
      pattern?.setTransform?.({ a: cos, b: sin, c: -sin, d: cos, e: 0, f: 0 });
      return pattern;
    };
    this.#hatch = { buffer: build(ROLE_COLORS.buffer, 0.12) };
  }

  // The working area, in blocks: the default region (never absent, so a blank plan still says how big a
  // board should be) grown to enclose the content and its symmetry ghosts plus a cell buffer, snapped out
  // to whole cells. Drawn as the tinted region and framed by fit().
  #workArea() {
    const cell = this.#doc?.globals.cell || 5;
    const halfCells = Math.max(1, Math.round(DEFAULT_AREA_BLOCKS / cell / 2));
    const half = halfCells * cell;
    let area = { min_x: -half, min_z: -half, max_x: half, max_z: half };
    const b = this.#doc ? viewBounds(this.#doc) : null;
    if (b) {
      const pad = AREA_BUFFER_CELLS * cell;
      area = unionRect(area, snapOut(
        { min_x: b.min_x - pad, min_z: b.min_z - pad, max_x: b.max_x + pad, max_z: b.max_z + pad }, cell));
    }
    return area;
  }

  // The world rect on screen, snapped out to whole grid steps — what the cell grid and the origin axes
  // span, so wherever the author pans or zooms there is grid to draw on.
  #gridBounds() {
    const cell = this.#doc?.globals.cell || 5;
    const { w, h } = this._viewSize ?? this._size();
    return snapOut(viewportWorldRect(w, h, this._scale, this._panX, this._panY), cell * GRID_SNAP_CELLS);
  }

  // The working-area tint: a low-opacity lift that reads on either theme, framed by a solid border so the
  // region sets itself apart from the dashed grid above it.
  #paintWorkArea() {
    const area = this.#workArea();
    if (!area) return;
    this.#painter.rect(area, {
      fill: "var(--canvas-ink, #ffffff)", fillAlpha: 0.05,
      stroke: "var(--canvas-axis, #a78bfa)", width: 1.5,
    });
  }

  /**
   * The cell grid, spanning the visible viewport rather than the content, so the surface is never fenced
   * in. The `gridStep` ladder keeps the line count flat as the board is zoomed out — the memo that used to
   * guard a DOM rebuild is gone with the DOM, but the ladder still matters, because past a few pixels
   * apart the lines are noise whatever they cost to draw.
   */
  #paintGrid() {
    const cell = this.#doc.globals.cell;
    const g = this.#gridBounds();
    // Snap the drawn range to whole steps so the lines stay on the same world coordinates as the step
    // changes — otherwise the grid shifts under the drawing at every threshold.
    const step = gridStep(cell * this._scale);
    const cx0 = Math.floor(g.min_x / cell / step) * step, cz0 = Math.floor(g.min_z / cell / step) * step;
    const cx1 = Math.ceil(g.max_x / cell / step) * step, cz1 = Math.ceil(g.max_z / cell / step) * step;

    // One faint dashed line per cell, in one path — the whole grid is a single stroke.
    const runs = [];
    for (let c = cx0; c <= cx1; c += step) runs.push({ x1: c * cell, z1: cz0 * cell, x2: c * cell, z2: cz1 * cell });
    for (let c = cz0; c <= cz1; c += step) runs.push({ x1: cx0 * cell, z1: c * cell, x2: cx1 * cell, z2: c * cell });
    this.#painter.segments(runs, { stroke: "var(--canvas-chunk, rgba(167,139,250,0.38))", width: 1, dash: [3, 3] });

    // Heavier gridlines along the origin axes, drawn atop the cell grid.
    const axis = { stroke: "var(--canvas-axis, #a78bfa)", width: 2 };
    if (0 >= cx0 && 0 <= cx1) this.#painter.line(0, cz0 * cell, 0, cz1 * cell, axis);
    if (0 >= cz0 && 0 <= cz1) this.#painter.line(cx0 * cell, 0, cx1 * cell, 0, axis);
  }

  // Origin marker — the centre crosshair + ring, in the axis colour.
  #paintCenter() {
    const cell = this.#doc.globals.cell;
    const arm = cell * 0.6, ringRadius = cell * 0.32;
    const axis = { stroke: "var(--canvas-axis, #a78bfa)", width: 1.5 };
    this.#painter.line(-arm, 0, arm, 0, axis);
    this.#painter.line(0, -arm, 0, arm, axis);
    this.#painter.circle(0, 0, ringRadius, axis);
  }

  /** The hatch fill for an annotation role, or its flat colour if the pattern could not be built. */
  #hatchFill(role) { return this.#hatch[role] || ROLE_COLORS[role] || "#888"; }

  // The symmetry mirror: every piece, zone, box and marker fanned to its orbit images, dimmed and
  // non-editable, so a pinwheel's centre tiling is visible while authoring.
  #paintGhost() {
    const painter = this.#painter;
    for (const img of pieceMirrorImages(this.#doc)) {
      const style = isAnnotationRole(img.role)
        ? { fill: this.#hatchFill(img.role), fillAlpha: 0.35, stroke: ROLE_COLORS[img.role], width: 1, dash: [6, 4] }
        : { fill: ROLE_COLORS[img.role] || "#888", fillAlpha: 0.28, stroke: ROLE_COLORS[img.role] || "#888", width: 1 };
      painter.rect(img.bounds, style);
    }
    const accent = "var(--accent, #5b9cff)";
    for (const img of zoneMirrorImages(this.#doc)) {
      painter.rect(img.bounds, { fill: accent, fillAlpha: 0.06, stroke: accent, width: 1, dash: [4, 3] });
      for (const hole of img.holes)
        painter.rect(hole, { fill: "var(--bg-canvas, #080f1a)", fillAlpha: 0.5, stroke: accent, width: 0.8, dash: [3, 3] });
    }
    for (const img of boxMirrorImages(this.#doc))
      painter.rect(img.bounds, { stroke: BOX_COLORS[img.kind] || "#9aa7b4", width: 1.5, dash: [8, 5], alpha: 0.35 });
    const cell = this.#doc.globals.cell;
    for (const m of markerMirrorImages(this.#doc))
      painter.circle(m.x, m.z, cell * 0.28, { fill: MARKER_COLORS[m.kind] || "#888", fillAlpha: 0.3 });
  }

  #paintZones() {
    const cell = this.#doc.globals.cell;
    const accent = "var(--accent, #5b9cff)";
    for (const z of this.#doc.zones) {
      // A water lane reads as a denser, tighter-dashed version of the build zone it sits beside: the same
      // kind of thing (a gap players cross) drawn as the closed one, since on a still canvas the only
      // difference between them is which is open yet.
      const lane = isWaterLane(z);
      const color = lane ? ZONE_COLORS["water-lane"] : accent;
      this.#painter.rect(rectCellsToBlocks(z.rect, cell),
        { fill: color, fillAlpha: lane ? 0.3 : 0.12, stroke: color, width: 1.4, dash: lane ? [2, 3] : [5, 4] });
      for (const h of z.holes)
        this.#painter.rect(rectCellsToBlocks(h, cell),
          { fill: "var(--bg-canvas, #080f1a)", fillAlpha: 0.6, stroke: color, width: 0.8, dash: [3, 3] });
    }
  }

  #paintPieces() {
    const cell = this.#doc.globals.cell, base = this.#doc.globals.surface;
    const range = this.#heightMap ? surfaceRange(this.#doc) : null;   // ramp domain for height-map mode
    for (const p of this.#doc.pieces) {
      const b = rectCellsToBlocks(p.rect, cell);
      if (isAnnotationRole(p.role)) {
        // Non-generating annotation: a hatched fill + dashed same-colour stroke, no solid terrain — reads as
        // "not buildable ground" and distinct from the dashed build-zone accent.
        this.#painter.rect(b, { fill: this.#hatchFill(p.role), stroke: ROLE_COLORS[p.role], width: 1.2, dash: [6, 4] });
        continue;
      }
      const surf = pieceSurface(this.#doc, p);
      let fill, stroke;
      if (this.#heightMap) {
        fill = heightColor(surfaceFraction(surf, range));
        stroke = fill;
      } else {
        const lift = Math.max(0, Math.min(0.6, (surf - base) / 16));   // higher surface → lighter fill
        fill = tint(ROLE_COLORS[p.role] || "#888", lift);
        stroke = ROLE_COLORS[p.role] || "#888";
      }
      this.#painter.rect(b, { fill, stroke, width: 1.2 });
    }
  }

  // Box annotations: an unfilled dashed envelope per box, kind-coloured, drawn above the pieces it groups
  // so the border stays visible over terrain. Unfilled by design — a box marks an extent, it never covers
  // what is inside it.
  #paintBoxes() {
    const cell = this.#doc.globals.cell;
    for (const b of this.#doc.boxes || [])
      this.#painter.rect(rectCellsToBlocks(b.rect, cell),
        { stroke: BOX_COLORS[b.kind] || "#9aa7b4", width: 2, dash: [8, 5] });
  }

  #paintMarkers() {
    const cell = this.#doc.globals.cell;
    for (const { kind, marker } of allMarkers(this.#doc)) {
      const c = markerCell(this.#doc, marker);
      if (!c) continue;
      const cx = c[0] * cell, cz = c[1] * cell, r = cell * 0.34;
      const color = MARKER_COLORS[kind] || "#888";
      if (kind === "spawn") {
        this.#painter.circle(cx, cz, r, { fill: color, fillAlpha: 0.85, stroke: "#222", width: 1 });
        const [dx, dz] = FACING_DIR[marker.facing] || FACING_DIR.front;
        this.#painter.line(cx, cz, cx + dx * r * 1.7, cz + dz * r * 1.7, { stroke: "#222", width: 2 });
      } else {
        const side = r * 1.5;
        this.#painter.rect({ min_x: cx - side / 2, min_z: cz - side / 2, max_x: cx + side / 2, max_z: cz + side / 2 },
          { fill: color, fillAlpha: 0.85, stroke: "#222", width: 1 });
      }
    }
  }

  // Derived-structure overlay (world space, non-interactive): land interfaces (solid green — narrow seams a
  // slimmer green core, still connected) vs corner point contacts (red warning), zone gap connectors (purple
  // ruler dashes), and frontline edges (accent-tinted highlight). Drawn above pieces, below markers; the hop
  // labels ride the screen-space overlay so they stay legible.
  #paintInspect() {
    if (!this.#doc) return;
    const cell = this.#doc.globals.cell;
    const painter = this.#painter;

    if (this.#overlayOn.frontline)
      for (const f of this.#inspect.frontline)
        painter.line(f.x1, f.z1, f.x2, f.z2,
          { stroke: "var(--accent, #5b9cff)", width: 6, cap: "round", alpha: 0.4 });

    if (this.#overlayOn.labels) {
      const axis = "var(--canvas-axis, #a78bfa)";
      for (const g of this.#inspect.gapLinks) {
        painter.line(g.x1, g.z1, g.x2, g.z2, { stroke: axis, width: 2.5, dash: [4, 3], cap: "round" });
        for (const [px, pz] of [[g.x1, g.z1], [g.x2, g.z2]])
          painter.circle(px, pz, cell * 0.12, { fill: axis });
      }
    }

    if (this.#overlayOn.interfaces)
      for (const it of this.#inspect.interfaces) {
        if (it.x1 === it.x2 && it.z1 === it.z2) {
          painter.circle(it.x1, it.z1, cell * 0.22, { stroke: "#d9534f", width: 2.5 });
          continue;
        }
        // A land/narrow segment sits exactly on a piece seam, where the piece strokes (or a same-green
        // wool-room fill) would swallow a plain line — a dark casing under a bright core reads on any fill.
        // A wall mark renders as a heavy near-black bar; a terrain↔wool-room seam renders red; other land
        // is green. A narrow seam connects too, with a slimmer core so it reads as "connected but thin".
        const narrow = it.kind === "narrow";
        const casing = narrow ? 5 : 7, core = narrow ? 2 : 3.5;
        const [casingColor, coreColor, casingWidth] = it.wall
          ? ["#000000", "#3b3b44", narrow ? 8 : 11]
          : it.woolRoom ? ["#4a1211", "#e5534b", casing]
                        : ["#123d26", "#4ade80", casing];
        painter.line(it.x1, it.z1, it.x2, it.z2, { stroke: casingColor, width: casingWidth, cap: "round" });
        painter.line(it.x1, it.z1, it.x2, it.z2, { stroke: coreColor, width: core, cap: "round" });
        if (it.wall) this.#paintWallChestFace(it, cell);
      }
  }

  // The open face of an approach wall: a bar drawn just off the seam, on the side the defence chests are set
  // into. Only one of a wall's two faces opens, so the side is the author's single decision about it and the
  // one thing the heavy black bar cannot show — without the tick, clicking the wall tool again looks like it
  // did nothing. The side comes from the feed as the piece the face looks out at, which is what survives the
  // symmetry orbit; the offset direction is read off that piece's own rect.
  #paintWallChestFace(it, cell) {
    const piece = (this.#doc?.pieces || []).find(p => p.id === it.wallChest);
    if (!piece) return;
    const [px, pz, pw, ph] = piece.rect;
    const vertical = it.x1 === it.x2;
    const centre = vertical ? (px + pw / 2) * cell : (pz + ph / 2) * cell;
    const away = Math.sign(centre - (vertical ? it.x1 : it.z1)) || 1;
    const offset = away * cell * 0.28;
    const [dx, dz] = vertical ? [offset, 0] : [0, offset];
    this.#painter.line(it.x1 + dx, it.z1 + dz, it.x2 + dx, it.z2 + dz,
      { stroke: "#f0a63a", width: 3, cap: "round" });
  }

  // Evaluator-evidence overlay (world space, non-interactive): every fired rule's cell-space evidence painted
  // on the grid so a broken rule is seen, not only read. The four primitives (rect / segment / marker / measure)
  // are drawn generically off the tag→style table; a measure's label rides the screen-space overlay (below) so
  // it stays legible at any zoom. Coordinates are cell-space — scale by the cell size to reach block/world space.
  #paintViolations() {
    if (!this.#doc) return;
    const cell = this.#doc.globals.cell;
    const painter = this.#painter;

    // Producibility evidence rides this layer too — it is the same kind of thing (geometry indicted by a check)
    // and follows its own panel selection rather than the Rules overlay toggle. `missing` cells are the box's own
    // geometry that no candidate emits (the offender); `extra` are cells the nearest candidate would have had
    // (the bound it should have respected) — the same semantics the evidence styling table already encodes.
    if (this.#nearestMiss) {
      const paint = (rects, tag) => {
        const st = evidenceStyle(tag);
        for (const r of rects || [])
          painter.rect(rectCellsToBlocks(r, cell),
            { fill: st.stroke, fillAlpha: 0.18, stroke: st.stroke, width: st.width, dash: st.dash });
      };
      paint(this.#nearestMiss.missing, "offender");
      paint(this.#nearestMiss.extra, "bound");
    }

    if (!this.#overlayOn.violations) return;
    for (const v of this.#shownViolations())
      for (const e of v.evidence || []) {
        const st = evidenceStyle(e.tag);
        const style = { stroke: st.stroke, width: st.width, dash: st.dash };
        if (e.kind === "rect" && Array.isArray(e.rect)) {
          painter.rect(rectCellsToBlocks(e.rect, cell), style);
        } else if (e.kind === "segment" || e.kind === "measure") {
          painter.line(e.x1 * cell, e.z1 * cell, e.x2 * cell, e.z2 * cell, { ...style, cap: "round" });
        } else if (e.kind === "marker") {
          painter.circle(e.x * cell, e.z * cell, Math.max(2, cell * 0.24), { stroke: st.stroke, width: st.width });
        }
      }
  }

  /**
   * A transient highlight pulse on the pieces, zones and markers a clicked lint finding implicates
   * (self-clearing). A subject is matched by id against all three, so a finding about one goal outlines that
   * goal rather than the whole piece under it.
   * An SVG `<animate>` ran itself; a painted frame has no such thing, so the pulse drives its own repaints
   * for as long as it lasts and the opacity is a function of elapsed time (`#pulseAlpha`).
   */
  pulseSubjects(ids) {
    if (!this.#doc) return;
    this.#pulseIds = [...(ids || [])];
    this.#pulseUntil = performance.now() + PULSE_MS;
    if (this.#pulseFrame) return;
    const step = () => {
      this.#pulseFrame = 0;
      this.#paintWorld();
      if (performance.now() < this.#pulseUntil) this.#pulseFrame = requestAnimationFrame(step);
      else { this.#pulseIds = []; this.#paintWorld(); }
    };
    this.#pulseFrame = requestAnimationFrame(step);
  }

  /** The pulse's opacity over its life — the `1;0.2;1;0.2;0` ramp, as a function of how far through it is. */
  #pulseAlpha() {
    const left = this.#pulseUntil - performance.now();
    if (left <= 0) return 0;
    const t = 1 - left / PULSE_MS;                       // 0 → 1 across the pulse
    const stops = [1, 0.2, 1, 0.2, 0];
    const span = 1 / (stops.length - 1);
    const i = Math.min(stops.length - 2, Math.floor(t / span));
    return stops[i] + (stops[i + 1] - stops[i]) * ((t - i * span) / span);
  }

  #paintPulse() {
    if (!this.#pulseIds.length) return;
    const alpha = this.#pulseAlpha();
    if (alpha <= 0) return;
    const cell = this.#doc.globals.cell;
    for (const id of this.#pulseIds) {
      const item = this.#doc.pieces.find(p => p.id === id) || this.#doc.zones.find(z => z.id === id);
      if (item) {
        this.#painter.rect(rectCellsToBlocks(item.rect, cell),
          { stroke: "var(--accent, #5b9cff)", width: 3, alpha });
        continue;
      }
      // A finding may name a marker as readily as a piece — a goal refused for where it stands is about the
      // marker, and pulsing the whole piece under it says the wrong thing about which one is wrong. A marker
      // has no rect, so the ring is a box centred on its point, sized just outside the glyph drawn there. An
      // orphaned marker (its piece deleted) has no point at all and pulses nothing.
      const found = allMarkers(this.#doc).find(entry => entry.marker.id === id);
      const point = found && markerCell(this.#doc, found.marker);
      if (!point) continue;
      const cx = point[0] * cell, cz = point[1] * cell, reach = cell * 0.6;
      this.#painter.rect({ min_x: cx - reach, min_z: cz - reach, max_x: cx + reach, max_z: cz + reach },
        { stroke: "var(--accent, #5b9cff)", width: 3, alpha });
    }
  }

  // Screen-space overlay: piece/zone id labels, the selection box, and the resize handles. Recomputed on
  // every viewport change so labels/handles stay a fixed pixel size and legible at any zoom.
  #refreshOverlay() {
    const layer = this.#overlay; if (!layer || !this.#doc) return;
    this.#clear(layer);
    const toS = (x, z) => this._toScreen(x, z);
    const cell = this.#doc.globals.cell;

    const label = (text, bx, bz, color, size = "11") => {
      const c = toS(bx, bz);
      const t = svgEl("text", {
        x: c.x, y: c.y, "text-anchor": "middle", "dominant-baseline": "middle",
        "font-size": size, "font-family": "ui-monospace, monospace", "font-weight": "600", fill: color,
        "paint-order": "stroke", stroke: "var(--bg-canvas)", "stroke-width": "3", "stroke-linejoin": "round",
        "pointer-events": "none",
      });
      t.textContent = text;
      layer.appendChild(t);
    };
    // Id labels (piece/zone) and gap hop distances follow the Labels toggle. With labels off the currently
    // selected piece/zone still shows its own id for orientation. In height-map mode the piece's surface
    // height reads big at the centre (it is data, always shown); the id rides the top edge and follows Labels.
    const showLabels = this.#overlayOn.labels;
    const selId = this.#sel && this.#sel.kind !== "marker" ? this.#sel.id : null;
    for (const p of this.#doc.pieces) {
      const b = rectCellsToBlocks(p.rect, cell);
      const mx = (b.min_x + b.max_x) / 2, mz = (b.min_z + b.max_z) / 2;
      const showId = showLabels || p.id === selId;
      if (this.#heightMap) {
        label(String(pieceSurface(this.#doc, p)), mx, mz, "var(--canvas-ink)", "13");
        if (showId) label(p.id, mx, b.min_z, "var(--canvas-ink)", "9");
      } else if (showId) {
        label(p.id, mx, mz, "var(--canvas-ink)");
      }
    }
    for (const z of this.#doc.zones)
      if (showLabels || z.id === selId) { const b = rectCellsToBlocks(z.rect, cell); label(z.id, (b.min_x + b.max_x) / 2, b.min_z, "var(--accent-light)"); }
    // A box's id rides its top-left corner in the kind's colour, so several nested envelopes stay tellable
    // apart without their labels stacking on one another.
    for (const bx of this.#doc.boxes || [])
      if (showLabels || bx.id === selId) { const b = rectCellsToBlocks(bx.rect, cell); label(bx.id, b.min_x + (b.max_x - b.min_x) * 0.14, b.min_z, BOX_COLORS[bx.kind] || "#9aa7b4", "10"); }

    // Gap-link hop distances ride the screen-space overlay so they stay a fixed pixel size at any zoom.
    if (showLabels)
      for (const g of this.#inspect.gapLinks)
        label(String(g.hop), (g.x1 + g.x2) / 2, (g.z1 + g.z2) / 2, "var(--canvas-axis)");

    // Evaluator measure labels (e.g. "17 < 20") ride the screen-space overlay too — a dimension line's number
    // must stay readable at any zoom. Cell-space endpoints → block coords for the midpoint.
    if (this.#overlayOn.violations)
      for (const v of this.#shownViolations())
        for (const e of v.evidence || [])
          if (e.kind === "measure" && e.label)
            label(e.label, ((e.x1 + e.x2) / 2) * cell, ((e.z1 + e.z2) / 2) * cell, "var(--accent-light)");

    // The selection outline: its box plus resize handles (a marker shows just a ring).
    if (this.#sel) this.#drawSelectionOutline(this.#sel, layer, toS, cell, true);
  }

  // Draw one selection's screen-space outline: a ring for a marker, a dashed box for a piece/zone/box, with
  // resize handles only when `handles`.
  #drawSelectionOutline(sel, layer, toS, cell, handles) {
    if (!sel) return;
    if (sel.kind === "marker") {
      const m = markerAt(this.#doc, sel.markerKind, sel.index);
      const c = m && markerCell(this.#doc, m);
      if (!c) return;
      const s = toS(c[0] * cell, c[1] * cell);
      // Selected marker: a bright accent ring over a dark casing (reads on any marker colour) plus a faint halo.
      layer.appendChild(svgEl("circle", { cx: s.x, cy: s.y, r: 15, fill: "var(--accent)", "fill-opacity": "0.14", stroke: "none", "pointer-events": "none" }));
      layer.appendChild(svgEl("circle", { cx: s.x, cy: s.y, r: 13, fill: "none", stroke: "var(--bg-canvas)", "stroke-width": "4", "pointer-events": "none" }));
      layer.appendChild(svgEl("circle", { cx: s.x, cy: s.y, r: 13, fill: "none", stroke: "var(--accent)", "stroke-width": "2.5", "pointer-events": "none" }));
      return;
    }
    const item = this.#itemOf(sel);
    if (!item) return;
    const b = rectCellsToBlocks(item.rect, cell);
    const p0 = toS(b.min_x, b.min_z), p1 = toS(b.max_x, b.max_z);
    const l = Math.min(p0.x, p1.x), r = Math.max(p0.x, p1.x), t = Math.min(p0.y, p1.y), bot = Math.max(p0.y, p1.y);
    layer.appendChild(svgEl("rect", { x: l, y: t, width: r - l, height: bot - t, fill: "none", stroke: "var(--accent)", "stroke-width": "1.5", "stroke-dasharray": "5 3", "pointer-events": "none" }));
    if (!handles) return;
    const HALF = 4;
    for (const hd of HANDLES) {
      const hx = l + hd.nx * (r - l), hy = t + hd.nz * (bot - t);
      const el = svgEl("rect", { x: hx - HALF, y: hy - HALF, width: HALF * 2, height: HALF * 2, rx: 1, fill: "var(--bg-deep)", stroke: "var(--accent)", "stroke-width": "1.5" });
      el.style.cursor = hd.cur;
      el.addEventListener("mousedown", (e) => this.#startResize(e, hd));
      layer.appendChild(el);
    }
  }

  #selItem() { return this.#itemOf(this.#sel); }
  #itemOf(sel) {
    if (!sel) return null;
    if (sel.kind === "piece") return this.#doc.pieces.find(p => p.id === sel.id) || null;
    if (sel.kind === "zone") return this.#doc.zones.find(z => z.id === sel.id) || null;
    if (sel.kind === "box") return boxById(this.#doc, sel.id);
    return null;
  }

  #fireSelect() {
    const cb = this.#cb.onSelect;
    if (!cb) return;
    if (!this.#sel) { cb(null); return; }
    if (this.#sel.kind === "marker") {
      const m = markerAt(this.#doc, this.#sel.markerKind, this.#sel.index);
      const c = m && markerCell(this.#doc, m);
      // The structure fields ride along so the inspector can offer them. Each is absent on a marker whose
      // author never changed it — the host fills the default in rather than the document carrying one.
      cb({
        kind: "marker", markerKind: this.#sel.markerKind, index: this.#sel.index, id: m?.id,
        piece: m?.piece, at: m?.at, cell: c, facing: m?.facing, name: m?.name,
        style: m?.style, materials: m?.materials,
        size: m?.size, height: m?.height, shell: m?.shell,
        float: m?.float, leak: m?.leak, openTop: m?.openTop,
      });
      return;
    }
    const item = this.#selItem();
    if (!item) { cb(null); return; }
    if (this.#sel.kind === "piece") cb({ kind: "piece", id: item.id, role: item.role, rect: item.rect, surface: item.surface ?? this.#doc.globals.surface, surfaceSet: item.surface != null, mirrors: item.mirrors !== false });
    else if (this.#sel.kind === "box") cb({
      kind: "box", id: item.id, boxKind: item.kind, rect: item.rect,
      members: boxMembers(this.#doc, item).map(p => p.id), membersNamed: Array.isArray(item.members) && item.members.length > 0,
    });
    else cb({ kind: "zone", id: item.id, zoneKind: canonicalZoneKind(item.kind), rect: item.rect });
  }

  // ── CanvasBase hooks ────────────────────────────────────────────────────────

  _onViewportChanged() { if (this.#doc) this.#paintWorld(); this.#refreshOverlay(); }
  _onZoom(scale) { this.#cb.onZoom?.(Math.round(scale * 100)); }

  _onToolMousedown(e, svgPt) {
    if (this._isoOn) return;          // iso preview is read-only
    const cell = this.#doc.globals.cell;
    const [cx, cz] = cellOfWorld(svgPt.x, svgPt.y, cell);
    if (this.#tool === "select") return this.#selectDown(svgPt, cx, cz);
    if (this.#tool === "wall") return this.#cycleWallAt(svgPt.x, svgPt.y);
    if (this.#tool === "piece" || this.#tool === "zone" || this.#tool === "box") { this.#drag = { mode: "draw", kind: this.#tool, a: [cx, cz], b: [cx, cz] }; this.#paintWorld(); return; }
    // Markers snap to the half-cell lattice — feed the fractional cell coordinate, not the floored cell.
    if (MARKER_KINDS.includes(this.#tool)) this.#placeMarker(this.#tool, svgPt.x / cell, svgPt.y / cell);
  }

  _onPointerMove(e, svgPt) {
    if (this._isoOn) return;          // iso preview is read-only
    const cell = this.#doc.globals.cell;
    const [cx, cz] = cellOfWorld(svgPt.x, svgPt.y, cell);
    if (this.#cursorEl) this.#cursorEl.textContent = `cell ${cx}, ${cz}`;
    if (this.#drag?.mode === "draw") { this.#drag.b = [cx, cz]; this.#paintWorld(); return; }
    if (this.#drag?.mode === "move") { this.#moveTo(cx, cz, svgPt.x / cell, svgPt.y / cell); return; }
    this.#refreshHoverCursor(svgPt);
  }

  /**
   * A pointer cursor over something grabbable. Painted shapes are not elements, so this cannot come from a
   * per-rect `cursor:pointer` any more — it comes from the same world-point pick the click already uses,
   * which is where the answer was really coming from all along.
   */
  #refreshHoverCursor(svgPt) {
    if (this.#tool !== "select" || this.#resize) return;
    const over = pickAtWorld(this.#doc, svgPt.x, svgPt.y);
    this._svg.style.cursor = over ? "pointer" : "default";
  }

  _onToolMouseup(e, svgPt) {
    if (this.#drag?.mode === "draw") { this.#commitDraw(); return; }
    if (this.#drag?.mode === "move") {
      const { moved, reselect } = this.#drag;
      this.#drag = null;
      if (moved) { this.render(); this.#cb.onChange?.(); this.#fireSelect(); }
      else this.#clickSelect(reselect);   // a press without a drag = a plain click (select / cycle facing)
    }
  }

  _onResizeMove(e) {
    if (!this.#resize) return false;
    const p = this._clientToSvg(e.clientX, e.clientY);
    const [cx, cz] = cellOfWorld(p.x, p.y, this.#doc.globals.cell);
    this.#resizeTo(cx, cz);
    return true;
  }
  _onResizeUp(e) {
    if (!this.#resize) return false;
    if (e.button !== 0) return false;
    this.#resize = null;
    this.render(); this.#cb.onChange?.(); this.#fireSelect();
    return true;
  }

  _onMouseleave() { if (this.#cursorEl) this.#cursorEl.textContent = ""; }

  // ── interaction ───────────────────────────────────────────────────────────────

  // Press with the select tool: pick the item under the click (marker-first, then the box that groups the
  // pieces there) and begin a move drag from it. Records whether the click re-hit the already-selected item,
  // so a plain re-click on a selected spawn cycles its facing (the first click only selects).
  //
  // A piece the author drilled into stays grabbed while the press lands inside it, so drill-then-drag moves
  // that one piece instead of snapping back to its box — the precedence the sketch tool's `_hitMovable`
  // applies to a drilled shape under a selected island. Pressing anywhere else re-selects the box.
  #selectDown(svgPt, cx, cz) {
    const prev = this.#sel;
    let hit = pickAtWorld(this.#doc, svgPt.x, svgPt.y);
    if (prev?.kind === "piece" && hit?.kind === "box") {
      const drilled = this.#doc.pieces.find(p => p.id === prev.id);
      if (drilled && rectContainsCell(drilled.rect, cx, cz)) hit = prev;
    }
    this.#sel = hit;
    this.#refreshOverlay();
    this.#fireSelect();
    // A box drag carries its members — resolve them now, before the envelope starts moving.
    const carried = hit?.kind === "box" ? boxMembers(this.#doc, boxById(this.#doc, hit.id) || { rect: [0, 0, 0, 0] }) : null;
    this.#drag = { mode: "move", sel: hit, grab: [cx, cz], moved: false, reselect: sameSelection(prev, hit), carried };
  }

  #moveTo(cx, cz, fcx, fcz) {
    const d = this.#drag; if (!d?.sel) return;
    // Markers track the cursor on the half-cell lattice (absolute, snap-aware) and re-parent to the piece
    // under it; only a real position change marks the drag moved (so a plain click still cycles facing).
    if (d.sel.kind === "marker") {
      const m = markerAt(this.#doc, d.sel.markerKind, d.sel.index);
      const at = attachMarker(this.#doc, fcx, fcz);
      if (at && (at.piece !== m.piece || at.at[0] !== m.at[0] || at.at[1] !== m.at[1])) {
        m.piece = at.piece; m.at = at.at; d.moved = true; this.render();
      }
      return;
    }
    const ddx = cx - d.grab[0], ddz = cz - d.grab[1];
    if (!ddx && !ddz) return;
    d.grab = [cx, cz];
    const item = this.#selItem();
    if (item) {
      // A box carries the pieces it groups: dragging the envelope relocates the whole part it annotates,
      // which is the point of typing it. Membership is resolved once at grab time (d.carried) so a piece
      // cannot fall out of a containment-grouped box mid-drag and be left behind.
      if (d.sel.kind === "box") for (const p of d.carried || []) { p.rect[0] += ddx; p.rect[1] += ddz; }
      item.rect[0] += ddx; item.rect[1] += ddz; d.moved = true;
    }
    this.render();
  }

  // A press-release with no drag. The first click on a spawn only selects it; a re-click on the
  // already-selected spawn (reselect) cycles its facing — so placing/selecting never surprises with a
  // facing change. (Selection itself already happened on mousedown.)
  #clickSelect(reselect) {
    if (reselect && this.#sel?.kind === "marker" && this.#sel.markerKind === "spawn") {
      const m = markerAt(this.#doc, "spawn", this.#sel.index);
      if (m) { m.facing = nextFacing(m.facing); this.render(); this.#cb.onChange?.(); this.#fireSelect(); }
    } else if (!this.#sel) {
      this.#refreshOverlay();
    }
  }

  #placeMarker(kind, cx, cz) {
    const at = attachMarker(this.#doc, cx, cz);
    if (!at) return;                          // markers must ride a piece
    const rec = kind === "spawn" ? { ...at, facing: "front" } : { ...at };
    const list = markerList(this.#doc, kind);
    if (!list) return;
    list.push(rec);
    this.#sel = { kind: "marker", markerKind: kind, index: list.length - 1 };
    this.setTool("select"); this.#cb.onTool?.("select");
    this.render(); this.#cb.onChange?.(); this.#fireSelect();
  }

  // Wall tool: cycle the wall mark on the land interface nearest the click (within one cell) — none, then a
  // wall whose defence chests face the seam's first piece, then one whose chests face the second, then none
  // again. The mark rides the piece pair; the bridge mutates doc.walls and re-inspects so the heavy bar and
  // its chest-face tick render from the feed. The wall tool stays armed for repeated clicking.
  #cycleWallAt(wx, wz) {
    const cell = this.#doc.globals.cell;
    const it = nearestInterface(this.#inspect.interfaces, wx, wz, cell);
    if (it) this.#cb.onCycleWall?.(it.a, it.b);
  }

  #paintPreview() {
    if (this.#drag?.mode !== "draw") return;
    const cell = this.#doc.globals.cell;
    const b = rectCellsToBlocks(rectFromCells(...this.#drag.a, ...this.#drag.b), cell);
    // A box preview is unfilled like the box itself — it frames pieces rather than covering them.
    if (this.#drag.kind === "box") {
      this.#painter.rect(b, { stroke: BOX_COLORS[this.#boxKind] || "#9aa7b4", width: 2, dash: [8, 5] });
      return;
    }
    const isAnnotation = this.#drag.kind === "piece" && isAnnotationRole(this.#pieceRole);
    const color = this.#drag.kind === "zone"
      ? (this.#zoneKind === "water-lane" ? ZONE_COLORS["water-lane"] : "var(--accent, #5b9cff)")
      : ROLE_COLORS[this.#pieceRole];
    this.#painter.rect(b, {
      fill: isAnnotation ? this.#hatchFill(this.#pieceRole) : color,
      fillAlpha: isAnnotation ? 0.6 : 0.2,
      stroke: color, width: 1.5, dash: isAnnotation ? [6, 4] : [4, 3],
    });
  }

  #commitDraw() {
    const kind = this.#drag.kind;
    const rect = rectFromCells(...this.#drag.a, ...this.#drag.b);
    this.#drag = null;
    this.#cb.onCreate?.(kind, rect);          // the bridge mints the id, appends, and re-selects
    this.setTool("select"); this.#cb.onTool?.("select");
  }

  // Resize the selected piece/zone by dragging a handle: move the picked cell edge(s) to the cursor cell,
  // keeping each extent ≥ 1 cell.
  #startResize(e, handle) {
    if (e.button !== 0 || !this.#sel || this.#sel.kind === "marker") return;
    e.stopPropagation(); e.preventDefault();
    this.#resize = { handle, sel: this.#sel };
  }

  #resizeTo(cx, cz) {
    const item = this.#selItem(); if (!item) return;
    const h = this.#resize.handle;
    let [x, z, w, hh] = item.rect;
    let minX = x, maxX = x + w - 1, minZ = z, maxZ = z + hh - 1;   // inclusive cell edges
    if (h.ex === -1) minX = Math.min(cx, maxX);
    else if (h.ex === 1) maxX = Math.max(cx, minX);
    if (h.ez === -1) minZ = Math.min(cz, maxZ);
    else if (h.ez === 1) maxZ = Math.max(cz, minZ);
    item.rect = [minX, minZ, maxX - minX + 1, maxZ - minZ + 1];
    this.render();
  }

  // ── build ─────────────────────────────────────────────────────────────────────

  #build() {
    const { w, h } = this._size();
    this._svg.setAttribute("width", w);
    this._svg.setAttribute("height", h);
    this._svg.setAttribute("viewBox", `0 0 ${w} ${h}`);

    this.#painter.resize(w, h);

    // The world's paint order lives in #paintWorld, bottom first — one sequence of painter.layer calls,
    // and `painter.layers` reports it by name. `_viewportG` stays as an empty group because CanvasBase
    // treats it as the "surface is ready" flag its pointer handlers guard on, and puts the viewport
    // matrix there; nothing hangs off it now, so that matrix is inert.
    this._viewportG = svgEl("g");
    this._svg.appendChild(this._viewportG);

    this.#screen = layerStack(this._svg, {
      overlay: null,                    // labels + selection box + resize handles
      scale:   INERT,                   // "N blocks" bar, bottom-right
    });
    this.#overlay = this.#screen.overlay;

    this._applyViewportTransform();

    this._observeResize();

    // Double-click drills into the piece under the cursor, past the box that groups it (the group model the
    // sketch tool sets: single-click picks the group, double-click enters a member). Select tool only — a
    // draw tool owns its own double-click.
    this._svg.addEventListener("dblclick", this.#onDblClick);
    // Delete / Backspace removes the current selection; Escape pops a drilled piece back out to its box
    // (guarded by visibility + not typing in a field).
    document.addEventListener("keydown", this.#onKey);
    this.setTool("select");
  }

  #onDblClick = (e) => {
    if (this._isoOn || !this.#doc) return;
    if (this.#tool !== "select") return;
    const p = this._clientToSvg(e.clientX, e.clientY);
    const hit = pickAtWorld(this.#doc, p.x, p.y, { drill: true });
    if (!hit) return;
    this.#drag = null;
    this.#sel = hit;
    this.#refreshOverlay();
    this.#fireSelect();
  };

  #onKey = (e) => {
    if (this._wrap?.offsetParent == null) return;
    if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;
    if ((e.key === "Delete" || e.key === "Backspace") && this.#sel) { e.preventDefault(); this.#cb.onDelete?.(this.#sel); }
    // Escape walks the group model back out: a drilled piece pops to the box that groups it, anything else
    // (including a box, or a piece in no box) clears.
    if (e.key === "Escape" && this.#sel && this.#doc) {
      e.preventDefault();
      const parent = this.#sel.kind === "piece" ? boxOfPiece(this.#doc, this.#sel.id) : null;
      this.#sel = parent ? { kind: "box", id: parent.id } : null;
      this.#refreshOverlay();
      this.#fireSelect();
    }
  };

  dispose() {
    if (this.#pulseFrame) { cancelAnimationFrame(this.#pulseFrame); this.#pulseFrame = 0; }
    this.#painter?.dispose();
    this.#canvasEl?.remove();
    this._disposeCanvasBase();
    this._svg.removeEventListener("dblclick", this.#onDblClick);
    document.removeEventListener("keydown", this.#onKey);
  }
}
