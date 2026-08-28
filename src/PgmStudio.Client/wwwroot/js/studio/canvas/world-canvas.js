/**
 * WorldCanvas — the world rendering engine shared by the Edit page (/maps/{id}/edit) and the Configure
 * wizard (/maps/{id}/configure). Extends CanvasBase for pan/zoom/transform + the drag FSM (via the
 * _on* hooks below), and delegates every interaction mode to a plain controller:
 *   WorldDrawController     new-region drawing (the draw tools)
 *   WorldEditController     8-handle resize + arrow-key move of the selected region
 *   SelectController        click-select modes (region / island) — one registered picker each
 *
 * A hybrid surface: the world layers are **painted** to a 2-D `<canvas>` under the svg and redrawn each
 * frame at the current scale, so no rasterization is kept for a zoom to stretch; the screen-space overlay
 * (the region label, the dimension pill, the resize handles) stays in the svg, which also remains the
 * single pointer target. Nothing world-side is retained between frames: `#nodeMap` and `#ctx.islands`
 * ARE the picture, and every setter is a state change plus a repaint rather than an element edit.
 *
 * Unlike the plan and sketch canvases, world coordinates are NOT the surface's: `buildTransform` fits the
 * map's bounding box to the viewport and the painter maps through it (`painter.toSurface`), which is why
 * that transform is rebuilt on every repaint and handed over in one place.
 *
 * Public surface (grouped; the bridge (bridge/world-bridge.js) forwards the subset Blazor drives):
 *   Render / lifecycle
 *     render(ctx, groups)              full repaint + zoom reset
 *     refreshRegions(groups)           re-take the region set without resetting zoom
 *     refreshRegionBounds(id, bounds)  repaint one region after an inspector/move edit
 *     resize()                         re-render at new dimensions (preserves zoom)
 *     dispose()                        drop the painted surface + the base's observers
 *   Selection / editing
 *     setSelectedRegions(ids)          highlight the id set; shows resize anchors when exactly one
 *                                      resizable region is selected
 *     updateRegionBounds(node, bounds) live footprint update during a drag/resize (edit controller)
 *     showAnchors(node) / clearAnchors()  8-handle resize anchors for the focused region
 *     setActiveTool(tool)              null | "move" | "rectangle" | "cylinder" | "circle" | "point" | "block"
 *     addRegion(node) / removeRegion(id) / renameNode(old,new)  mutate the region set
 *     setRegionVisible(id, v)          per-region show/hide
 *     setAuthorRegions(nodes)          render intent-backed "dummy" regions (Configure spawns / protection)
 *   Overlays / visibility
 *     setBlocksVisible(v) / loadBlockLayer(data)               top-surface block overlay
 *     connectBlocksToggle(cb,label,fetch) / autoLoadBlocks() / reloadBlocks()  block-toggle wiring
 *     setPoisVisible(v) / setBuildVisible(v) / setResolvedMode(v)  layer toggles
 *     setSymmetry(type, cx, cz)        symmetry axis/centre overlay
 *   Islands
 *     setIslandSelect(on)              switch the select controller to island mode
 *     setSelectedIsland(id) / setExcludedIslands(ids) / setIslandTeams(map)  highlight / exclude / team-tint
 *     fitIsland(id)                    pan+zoom to an island
 *   Point pick
 *     setPointPick(on)                 arm the point-drop tool (Configure spawn step)
 *   View
 *     focusRegion(node)                pan+zoom to fit a region
 *     fitBounds(bbox) / resetView()    frame a box / reset to the full extent
 *
 * ctx shape: { bounding_box: {min_x,min_z,max_x,max_z}, islands: [...] }
 * groups shape: [{name, label, color, regions: [node, ...]}]
 * node shape: { id, type, label, color, bounds?: {min_x,min_z,max_x,max_z}, children?, polygon_2d? }
 */

import { buildTransform, buildInverseTransform } from "../geometry/transform.js";
import { translateBounds } from "../geometry/shape.js";
import { svgEl, polyToPath } from "../render/svg.js";
import { renderDimensionPill } from "../render/canvas-chrome.js";
import { layerStack, clearLayer } from "../render/layer-stack.js";
import { CanvasPainter } from "../render/canvas-painter.js";
import { CanvasBase, ZOOM_MIN, ZOOM_MAX } from "./canvas-base.js";
import { WorldDrawController } from "../controllers/world-draw-controller.js";
import { WorldEditController, RESIZABLE_TYPES } from "../controllers/world-edit-controller.js";
import { SelectController } from "../controllers/select-controller.js";
import { chatColorHex, dyeColorHex } from "../render/palette.js";
import { blockToExtentBounds } from "../geometry/region-convert.js";
import { pointInRing } from "../geometry/polygon.js";
import { applySymmetryToBounds, orbitAxes } from "../geometry/symmetry.js";
import { paintShape, paintAnchorBlock } from "../render/shape-render.js";
import { primitiveStyle } from "../render/primitive-style.js";
import { paintSymmetryOverlay } from "../render/symmetry-render.js";
import { loadBlockImage, blockImageBounds } from "../render/block-render.js";
import { geojsonToSimplified } from "../geometry/islands.js";

const COMPOSITE_TYPES = new Set(["union", "intersect", "negative", "complement"]);

// The points of interest, as the glyph each is drawn with and where its colour comes from. One table, so
// three layers that differ only in those two things are three lines rather than three near-copies.
const POI_LAYERS = [
  { key: "spawns",    source: "spawns",    glyph: "★", size: 12, weight: "bold", color: (poi) => chatColorHex(poi.team_color ?? "") },
  { key: "wools",     source: "wools",     glyph: "◆", size: 11, weight: null,   color: (poi) => dyeColorHex(poi.color ?? "") },
  { key: "monuments", source: "monuments", glyph: "⊕", size: 13, weight: null,   color: (poi) => dyeColorHex(poi.color ?? "") },
];

export class WorldCanvas extends CanvasBase {
  #ctx    = null;
  #groups = [];
  #toSvg  = null;
  #toWorld= null;
  #callbacks;

  // The painted world surface + the screen-space svg layers above it.
  #painter  = null;
  #canvasEl = null;
  #screen = {};

  // The region set: `#nodeMap` for lookup (hit-test, visibility, selection), `#regionNodes` for paint
  // order. Together they are what the region layer draws — there is no element cache beside them.
  #nodeMap        = new Map();
  #regionNodes    = [];
  #addedNodes     = [];
  #authorRegionIds = [];   // ids of "dummy" authoring regions (e.g. intent-backed spawn-protection rects)
  #authorRegionNodes = []; // the authored nodes themselves, kept so the mirror preview can re-derive
  #authorMirror = null;    // {type, cx, cz} — when set, authored rects get symmetry-orbited ghost copies

  // draw controller (instantiated in constructor)
  #drawCtrl = null;

  // resize (8-handle drag) + arrow-key move, extracted into a controller
  #editCtrl = null;
  // click-select modes (region / island / spawn), extracted into a controller
  #selectCtrl = null;

  // visibility/selection
  #visibilityMap      = new Map();
  #currentSelectedIds = new Set();
  #resolvedMode       = false;

  // Set while a batch of region mutations is in flight, so a run of them costs one repaint instead of one
  // each — the authored mirror preview replaces its whole set at once, and every step of it would
  // otherwise redraw the world.
  #batching = false;

  // island selection (World authoring step): when on, canvas clicks pick an island instead of a region;
  // the selected island gets an accent border and excluded islands are dimmed.
  #selectedIslandId  = null;
  // The island outlines as path data, rebuilt only when the world or the fit moves. Islands are the one
  // world layer whose geometry is large and static — a few hundred points each, unchanged by a drag — so
  // rebuilding their paths per frame would be the only real cost in a repaint.
  #islandPaths = [];
  #excludedIslandIds = new Set();
  #islandTeamColors  = new Map();   // island id → team colour hex (World · Teams island assignment)

  // symmetry overlay (World · Symmetry step): a dashed axis line (or two, for rot_90) + a centre marker.
  #symType           = null;
  #symCx             = 0;
  #symCz             = 0;

  // spawn-point authoring (Teams · Spawn step): point-pick reports the clicked world point; author spawns
  // are rendered as team-coloured markers (the placed one bright, orbit ones faint).
  #pointPick         = false;

  // layer state
  #showPois   = false;
  #showBuild  = false;
  #showBlocks = false;
  #blockData  = null;
  #blockImage = null;          // the decoded bitmap — a canvas blits an image, not a data URL
  #showBuildability = false;   // N03 buildability overlay visibility
  #buildabilityData = null;    // cached block-image payload so a render() reset re-paints it
  #buildabilityImage = null;
  #selectedNode = null;

  // blocks toggle wiring (set by connectBlocksToggle)
  #blocksCbEl      = null;
  #blocksLabelEl   = null;
  #blocksFetchFn   = null;
  #blockFetchId    = 0;
  #blockFetchPromise = null;

  constructor(svgEl_, wrapEl, callbacks = {}) {
    super(svgEl_, wrapEl);
    this.#callbacks = callbacks;
    // The painted surface is created here rather than in the host markup, so neither the Blazor component
    // nor the bridge signature is changed by what the world layers are drawn with — which matters more
    // here than anywhere else, since sixteen hosts mount this one canvas.
    this.#canvasEl = document.createElement("canvas");
    this.#canvasEl.className = "world-canvas-2d";
    this._svg.parentNode.insertBefore(this.#canvasEl, this._svg);
    this.#painter = new CanvasPainter(this.#canvasEl);
    this.#drawCtrl  = new WorldDrawController({
      onRegionDraw: (r) => this.#callbacks.onRegionDraw?.(r),
      onPreviewChanged: () => this.#paintWorld(),
    });
    this.#editCtrl  = new WorldEditController(
      {
        getSelected: () => this.#selectedNode,
        getOverlay:  () => this.#screen.overlay,
        getToWorld:  () => this.#toWorld,
        getToSvg:    () => this.#toSvg,
        getViewport: () => ({ scale: this._scale, panX: this._panX, panY: this._panY }),
        clientToSvg: (x, y) => this._clientToSvg(x, y),
        isVisible:   () => this._wrap?.offsetParent != null,
      },
      {
        applyBounds: (node, nb) => this.updateRegionBounds(node, nb),
        saveBounds:  (node, b)  => this.#callbacks.onBoundsSave?.(node, b),
        setCursor:   (c) => { this._svg.style.cursor = c; },
        afterResize: () => { this.#refreshCursor(); this.#updateOverlay(); },
      },
    );
    this.#selectCtrl = new SelectController()
      .register("region", (w) => this.#callbacks.onCanvasClick?.(this.#hitTest(w.x, w.z)))
      .register("island", (w) => this.#callbacks.onIslandClick?.(this.#hitTestIsland(w.x, w.z)));
    this.#selectCtrl.setMode("region");
  }

  // ── CanvasBase hook overrides ──────────────────────────────────────────────

  _onViewportChanged() {
    this.#paintWorld();
    this.#updateOverlay();
  }

  _onZoom(scale) {
    this.#callbacks.onZoom?.(scale);
  }

  _onToolMousedown(e, svgPt) {
    if (!this.#toWorld) return;
    const world = this.#toWorld(svgPt.x, svgPt.y);
    const bx = Math.floor(world.x), bz = Math.floor(world.z);
    if (this._activeTool === "move" || !this._activeTool) return;
    // Spawn placement: the point tool drops a spawn at the clicked world point (host orbits the rest).
    if (this.#pointPick && this._activeTool === "point") {
      this.#callbacks.onPointPick?.(world.x, world.z);
      return;
    }
    if (!this.#drawCtrl.onMouseDown(bx, bz)) {
      if (this._activeTool === "point" || this._activeTool === "block") {
        this.#callbacks.onRegionDraw?.({ ...blockToExtentBounds(bx, bz), type: this._activeTool });
      }
    }
  }

  _onPointerMove(e, svgPt) {
    if (!this.#toWorld) return;
    const world = this.#toWorld(svgPt.x, svgPt.y);
    const bx = Math.floor(world.x), bz = Math.floor(world.z);
    this.#callbacks.onCoords?.(bx, bz);
    this.#drawCtrl.onMouseMove(bx, bz);
  }

  _onToolMouseup(e, svgPt) {
    this.#drawCtrl.onMouseUp();
  }

  _onCanvasClick(e, svgPt) {
    if (!this.#toWorld) return;
    // Region-select (default) or island-select — the active mode owns its hit-test.
    this.#selectCtrl.click(this.#toWorld(svgPt.x, svgPt.y));
  }

  _onMouseleave() {
    this.#callbacks.onCoords?.(null, null);
  }

  _onResizeMove(e) { return this.#editCtrl?.onResizeMove(e) ?? false; }
  _onResizeUp(e)   { return this.#editCtrl?.onResizeUp(e) ?? false; }

  // Body-drag (CV10): drag the selected region's body to move it. The editor fits through a transform,
  // so map SVG→world with the inverse; only resizable, non-ghost AABB regions are movable.
  _toWorld(svgPt) { return this.#toWorld ? this.#toWorld(svgPt.x, svgPt.y) : null; }
  _hitMovable(world) {
    const n = this.#selectedNode;
    if (!n?.bounds || !RESIZABLE_TYPES.has(n.type) || n.ghost) return null;
    const { min_x, min_z, max_x, max_z } = n.bounds;
    return (world.x >= min_x && world.x <= max_x && world.z >= min_z && world.z <= max_z) ? n : null;
  }
  _moveBy(node, dx, dz) { this.updateRegionBounds(node, translateBounds(node.bounds, dx, dz)); }
  _commitMove(node)     { this.#callbacks.onBoundsSave?.(node, { ...node.bounds }); }

  // ── public API ─────────────────────────────────────────────────────────────

  render(ctx, groups) {
    this.#ctx    = ctx;
    this.#groups = groups || [];
    this.#addedNodes = [];
    this.#selectedNode = null;
    this.#nodeMap.clear();
    this.#regionNodes = [];
    this.#visibilityMap.clear();
    this.#currentSelectedIds.clear();
    this.#selectedIslandId = null;
    this.#blockData        = null;
    this.#blockImage       = null;
    this.#blockFetchId++;
    this.#blockFetchPromise = null;
    this._scale = 1;
    this._panX  = 0;
    this._panY  = 0;
    this.#rebuild();
    this._onZoom(this._scale);
  }

  showAnchors(node) {
    this.#selectedNode = node;
    this.#paintWorld();
    this.#updateOverlay();
  }

  clearAnchors() {
    this.#selectedNode = null;
    this.#paintWorld();
    this.#updateOverlay();
  }

  setPoisVisible(v) {
    this.#showPois = v;
    this.#paintWorld();
  }

  // The `build` layer has a switch and a z-order slot but nothing that paints into it — see CV21, which
  // owns the question of whether a Build phase was meant to fill it or whether the whole thing goes.
  setBuildVisible(v) {
    this.#showBuild = v;
    this.#paintWorld();
  }

  setResolvedMode(v) {
    this.#resolvedMode = v;
    this.refreshRegions(this.#groups);
  }

  focusRegion(node) {
    let bbox = node.bounds ?? null;
    if (!bbox && node.polygon_2d?.exterior?.length) {
      const pts = node.polygon_2d.polygons
        ? node.polygon_2d.polygons.flatMap(p => p.exterior)
        : node.polygon_2d.exterior;
      const xs = pts.map(([x]) => x), zs = pts.map(([, z]) => z);
      bbox = { min_x: Math.min(...xs), max_x: Math.max(...xs), min_z: Math.min(...zs), max_z: Math.max(...zs) };
    }
    if (bbox) this.fitBounds(bbox, 0.75);
  }

  /** Reset pan/zoom to the default whole-map view (the base transform). */
  resetView() {
    this._scale = 1;
    this._panX  = 0;
    this._panY  = 0;
    this._applyViewportTransform();
    this._onZoom(this._scale);
  }

  /** Pan+zoom so an island's bbox fills the viewport (a little padding). */
  fitIsland(id, fillFrac = 0.9) {
    const isl = (this.#ctx?.islands ?? []).find(i => i.id === id);
    if (isl && Array.isArray(isl.bounds) && isl.bounds.length === 4) {
      const [min_x, min_z, max_x, max_z] = isl.bounds;
      this.fitBounds({ min_x, min_z, max_x, max_z }, fillFrac);
    }
  }

  /** Pan+zoom so a world bbox {min_x,min_z,max_x,max_z} fills the viewport, scaled to fillFrac. */
  fitBounds(bbox, fillFrac = 0.9) {
    if (!this.#toSvg || !bbox) return;
    const { min_x, max_x, min_z, max_z } = bbox;
    if (![min_x, max_x, min_z, max_z].every(Number.isFinite)) return;
    const { w, h } = this._size();
    const p1 = this.#toSvg(min_x, min_z);
    const p2 = this.#toSvg(max_x, max_z);
    const sx1 = Math.min(p1.x, p2.x), sx2 = Math.max(p1.x, p2.x);
    const sy1 = Math.min(p1.y, p2.y), sy2 = Math.max(p1.y, p2.y);
    const bw = sx2 - sx1, bh = sy2 - sy1;
    const newScale = (bw > 0 || bh > 0)
      ? Math.max(ZOOM_MIN, Math.min(ZOOM_MAX, Math.min(bw > 0 ? w / bw : Infinity, bh > 0 ? h / bh : Infinity) * fillFrac))
      : this._scale;
    this._scale = newScale;
    this._panX  = w / 2 - ((sx1 + sx2) / 2) * newScale;
    this._panY  = h / 2 - ((sy1 + sy2) / 2) * newScale;
    this._applyViewportTransform();
    this._onZoom(this._scale);
  }

  setBlocksVisible(v) {
    this.#showBlocks = v;
    this.#paintWorld();
  }

  loadBlockLayer(data) {
    this.#blockData = data;
    this.#blockImage = null;
    if (!data) { this.#paintWorld(); return; }
    loadBlockImage(data, (image) => {
      if (this.#blockData !== data) return;   // a newer payload arrived while this one decoded
      this.#blockImage = image;
      this.#paintWorld();
    });
  }

  // The buildability heatmap uses the same rasterize-once machinery as the block overlay (`data` is the
  // block-image payload {xs,zs,colors,min_x,min_z,max_x,max_z} the bridge builds from /buildability).
  setBuildabilityVisible(v) {
    this.#showBuildability = v;
    this.#paintWorld();
  }

  loadBuildabilityLayer(data) {
    this.#buildabilityData = data;
    this.#buildabilityImage = null;
    if (!data) { this.#paintWorld(); return; }
    loadBlockImage(data, (image) => {
      if (this.#buildabilityData !== data) return;
      this.#buildabilityImage = image;
      this.#paintWorld();
    });
  }

  /**
   * Wire a filter-chip checkbox + label to the block layer.
   * fetchFn: async () => blockLayerData  (should close over mapName)
   * Call this once in the activity constructor; call autoLoadBlocks() after each render().
   */
  connectBlocksToggle(cbEl, labelEl, fetchFn) {
    this.#blocksCbEl    = cbEl;
    this.#blocksLabelEl = labelEl;
    this.#blocksFetchFn = fetchFn;
    cbEl?.addEventListener("change", async (e) => {
      if (!e.target.checked) {
        this.setBlocksVisible(false);
        labelEl?.classList.remove("filter-chip--active");
        return;
      }
      try {
        await this.#ensureBlockData();
        this.setBlocksVisible(true);
        labelEl?.classList.add("filter-chip--active");
      } catch {
        e.target.checked = false;
        labelEl?.classList.remove("filter-chip--active");
      }
    });
  }

  /** Fetch (if needed) and show the block layer; pre-checks the toggle chip. */
  async autoLoadBlocks() {
    if (!this.#blocksFetchFn) return;
    try {
      await this.#ensureBlockData();
      this.setBlocksVisible(true);
      if (this.#blocksCbEl)    this.#blocksCbEl.checked = true;
      this.#blocksLabelEl?.classList.add("filter-chip--active");
    } catch {
      // top-surface not available — stay in island view
    }
  }

  /** Re-fetch block data after a render() reset, but only if the user had blocks on. */
  async reloadBlocks() {
    if (!this.#blocksFetchFn || !this.#showBlocks) return;
    try {
      await this.#ensureBlockData();
    } catch { /* stay in island view */ }
  }

  async #ensureBlockData() {
    if (this.#blockData) return;
    if (!this.#blockFetchPromise) {
      const fetchId = this.#blockFetchId;
      this.#blockFetchPromise = this.#blocksFetchFn()
        .then(data => { if (fetchId === this.#blockFetchId) this.loadBlockLayer(data); })
        .finally(() => { this.#blockFetchPromise = null; });
    }
    await this.#blockFetchPromise;
  }

  setSelectedRegions(ids) {
    this.#currentSelectedIds = new Set(ids);
    // Show the resize overlay (dimension pill + drag handles) when the selection resolves to a single
    // resizable region; clear it for empty, multi, or non-resizable selections. Both paths repaint, which
    // is what puts the new selection's own styling on the surface.
    const resizable = [...this.#currentSelectedIds]
      .map(id => this.#nodeMap.get(id))
      .filter(n => n?.bounds && RESIZABLE_TYPES.has(n.type) && !n.ghost);
    if (resizable.length === 1) this.showAnchors(resizable[0]);
    else                        this.clearAnchors();
  }

  setRegionVisible(id, visible) {
    if (visible) this.#visibilityMap.delete(id);
    else         this.#visibilityMap.set(id, false);
    this.#paintWorld();
  }

  updateRegionBounds(node, newBounds) {
    Object.assign(node.bounds, newBounds);
    this.#paintWorld();
    if (this.#selectedNode?.id === node.id) this.#updateOverlay();
  }

  setActiveTool(tool) {
    this.#drawCtrl.cancel();
    this.#drawCtrl.setTool(tool);
    this._activeTool = tool;
    this.#refreshCursor();
  }

  addRegion(node) {
    const stale = this.#regionNodes.findIndex(n => n.id === node.id);
    if (stale >= 0) this.#regionNodes.splice(stale, 1);
    this.#regionNodes.push(node);
    this.#nodeMap.set(node.id, node);
    if (!this.#addedNodes.some(n => n.id === node.id)) this.#addedNodes.push(node);
    this.#paintWorld();
  }

  removeRegion(id) {
    this.#regionNodes = this.#regionNodes.filter(n => n.id !== id);
    this.#nodeMap.delete(id);
    this.#visibilityMap.delete(id);
    this.#currentSelectedIds.delete(id);
    this.#addedNodes = this.#addedNodes.filter(n => n.id !== id);
    if (this.#selectedNode?.id === id) { this.#selectedNode = null; this.#updateOverlay(); }
    this.#paintWorld();
  }

  // Render a set of authoring-only "dummy" regions — geometry that lives in the intent, not the loaded
  // tree (e.g. spawn-protection rectangles). They go into the same maps as real regions, so they select,
  // resize, and report edits via onBoundsSave exactly like tree regions (the host routes those to intent).
  // Replaces the previous author-region set.
  setAuthorRegions(nodes) {
    this.#authorRegionNodes = nodes ?? [];
    this.#renderAuthorRegions();
  }

  // When set, every authored rectangle gets its symmetry-orbited copies rendered as non-editable ghosts
  // (reusing the shared geometry/symmetry.js — the same math the sketch tool's mirror preview uses).
  setAuthorMirror(type, cx, cz) {
    this.#authorMirror = type ? { type, cx, cz } : null;
    this.#renderAuthorRegions();
  }

  #renderAuthorRegions() {
    this.#batching = true;
    try {
      for (const id of this.#authorRegionIds) this.removeRegion(id);
      const all = [...this.#authorRegionNodes, ...this.#mirrorGhosts(this.#authorRegionNodes)];
      this.#authorRegionIds = all.map(n => n.id);
      for (const node of all) this.addRegion(node);
    } finally {
      this.#batching = false;
    }
    this.#paintWorld();
  }

  #mirrorGhosts(nodes) {
    if (!this.#authorMirror) return [];
    const { type, cx, cz } = this.#authorMirror;
    const axes = orbitAxes(type);
    const out = [];
    for (const n of nodes) {
      if (!n.bounds || n.marker || n.ghost) continue;   // mirror real rectangles only (not markers/ghosts)
      axes.forEach((ax, j) => out.push({
        id: `${n.id}~m${j}`, type: "rectangle", ghost: true,
        color: n.color, label: `${n.label ?? n.id} (mirror)`,
        bounds: applySymmetryToBounds(n.bounds, ax, cx, cz),
      }));
    }
    return out;
  }

  renameNode(oldId, newId) {
    const node = this.#nodeMap.get(oldId);
    if (node) {
      this.#nodeMap.delete(oldId);
      node.id = newId;
      this.#nodeMap.set(newId, node);
    }
    if (this.#visibilityMap.has(oldId)) {
      this.#visibilityMap.set(newId, this.#visibilityMap.get(oldId));
      this.#visibilityMap.delete(oldId);
    }
    if (this.#currentSelectedIds.has(oldId)) { this.#currentSelectedIds.delete(oldId); this.#currentSelectedIds.add(newId); }
    this.#paintWorld();
  }

  resize() {
    if (!this.#ctx) return;
    const nW = this._wrap.clientWidth  - 24;
    const nH = this._wrap.clientHeight - 24;
    if (nW <= 0 || nH <= 0) return;
    if (nW === +this._svg.getAttribute("width") && nH === +this._svg.getAttribute("height")) return;
    this.#rebuild();
  }

  /** Drop the painted surface and the base's observers. The sixteen hosts each mount their own. */
  dispose() {
    this.#painter?.dispose();
    this.#canvasEl?.remove();
    this._disposeCanvasBase();
  }

  refreshRegionBounds(nodeId, newBounds) {
    const node = this.#nodeMap.get(nodeId);
    if (!node) return;
    node.bounds = newBounds;
    this.#paintWorld();
    this.#updateOverlay();
  }

  refreshRegions(groups) {
    this.#groups = groups || [];
    this.#addedNodes = [];
    this.#selectedNode = null;
    this.#currentSelectedIds.clear();
    this.#visibilityMap.clear();
    this.#collectRegions();
    this.#paintWorld();
    this.#updateOverlay();
  }

  // ── rendering ──────────────────────────────────────────────────────────────

  /**
   * Re-take the surface: size the svg and the backing store, rebuild the world→surface transform for the
   * new box, and repaint. Called on load and on every size change — the transform is bbox-fitted, so it
   * is only correct for the box it was built for.
   */
  #rebuild() {
    this.#drawCtrl.cancel();
    // Through the base's own measurement, which carries the fallback for a wrap that has no box yet.
    // Measuring here directly wrote width="-24" (0 minus the padding) whenever a host mounted the canvas
    // before layout settled, and the browser rejects a negative width/height/viewBox outright.
    const { w, h } = this._resizeSvg();
    this.#painter.resize(w, h);

    // An xml-only / not-fully-pipelined map has no bounding_box yet: there is nothing to fit a
    // transform to, so degrade gracefully with an empty-canvas hint instead of rendering garbage.
    if (!this.#ctx.bounding_box) {
      while (this._svg.firstChild) this._svg.removeChild(this._svg.firstChild);
      this.#screen = {};
      this.#toSvg = null;
      this.#toWorld = null;
      this.#painter.begin(1, 0, 0);   // clear whatever the previous map painted
      const hint = svgEl("text", {
        x: w / 2, y: h / 2, "text-anchor": "middle", "dominant-baseline": "middle",
        "font-size": "13", fill: "#888",
      });
      hint.textContent = "No map bounds yet — run the pipeline to view this map.";
      this._svg.appendChild(hint);
      return;
    }

    this.#toSvg   = buildTransform(this.#ctx.bounding_box, w, h);
    this.#toWorld = buildInverseTransform(this.#ctx.bounding_box, w, h);
    this.#painter.toSurface = this.#toSvg;

    while (this._svg.firstChild) this._svg.removeChild(this._svg.firstChild);

    // `_viewportG` stays as an empty group because CanvasBase treats it as the "surface is ready" flag its
    // pointer handlers guard on, and puts the viewport matrix there; nothing hangs off it now.
    this._viewportG = svgEl("g");
    this._applyViewportTransform();
    this._svg.appendChild(this._viewportG);
    this.#screen = layerStack(this._svg, { overlay: null });

    this.#traceIslands();
    this.#collectRegions();
    this.#paintWorld();
    this.#updateOverlay();
  }

  /**
   * Paint every world layer, bottom first. This is the whole of the world's z-order, and unlike a group
   * stack it is also the whole of its cost: a layer that draws nothing costs a function call, and nothing
   * survives between frames to be stretched by a later transform.
   */
  #paintWorld() {
    if (!this.#painter || !this.#toSvg || this.#batching) return;
    const painter = this.#painter;
    painter.begin(this._scale, this._panX, this._panY);
    painter.layer("build",        () => {});   // declared but unfed — CV21
    painter.layer("blocks",       () => this.#paintBlockLayer());
    painter.layer("islands",      () => this.#paintIslands());
    painter.layer("buildability", () => this.#paintBuildabilityLayer());
    painter.layer("symmetry",     () => paintSymmetryOverlay(painter, this.#symType, this.#symCx, this.#symCz, this.#ctx?.bounding_box));
    painter.layer("spawns",       () => this.#paintPoiLayer(POI_LAYERS[0]));
    painter.layer("regions",      () => this.#paintRegions());
    painter.layer("wools",        () => this.#paintPoiLayer(POI_LAYERS[1]));
    painter.layer("monuments",    () => this.#paintPoiLayer(POI_LAYERS[2]));
    painter.layer("anchors",      () => this.#paintAnchors());
    painter.layer("draw",         () => this.#drawCtrl.paint(painter));
  }

  /** The painted layer names of the last frame, bottom first — what `data-layer` offered on a group stack. */
  get paintOrder() { return this.#painter?.layers ?? []; }

  #refreshCursor() {
    if (this._activeTool === "move")    this._svg.style.cursor = "grab";
    else if (this._activeTool !== null) this._svg.style.cursor = "crosshair";
    else                               this._svg.style.cursor = "default";
  }

  // ── hit test ──────────────────────────────────────────────────────────────

  // Smallest region whose bounds contain the click; if none does, the nearest region within a small
  // margin — so 1-block primitives (points, spawns) are forgiving to click, the same select rule for all.
  #hitTest(worldX, worldZ) {
    const MARGIN = 2;
    let best = null, bestArea = Infinity;
    let near = null, nearD = Infinity;
    for (const [id, node] of this.#nodeMap) {
      if (!node.bounds) continue;
      if (node.ghost) continue;   // derived previews (orbit copies) aren't selectable
      if (this.#visibilityMap.get(id) === false) continue;
      const { min_x, min_z, max_x, max_z } = node.bounds;
      if (worldX >= min_x && worldX <= max_x && worldZ >= min_z && worldZ <= max_z) {
        const area = (max_x - min_x) * (max_z - min_z);
        if (area < bestArea) { bestArea = area; best = node; }
      } else {
        const dx = Math.max(min_x - worldX, 0, worldX - max_x);   // distance from the point to the AABB
        const dz = Math.max(min_z - worldZ, 0, worldZ - max_z);
        const d = dx * dx + dz * dz;
        if (d < nearD) { nearD = d; near = node; }
      }
    }
    return best ?? (nearD <= MARGIN * MARGIN ? near : null);
  }

  // ── layers ────────────────────────────────────────────────────────────────

  #paintBlockLayer() {
    if (!this.#showBlocks || !this.#blockImage) return;
    this.#painter.image(this.#blockImage, blockImageBounds(this.#blockData));
  }

  // The buildability heatmap is translucent so the terrain reads through it.
  #paintBuildabilityLayer() {
    if (!this.#showBuildability || !this.#buildabilityImage) return;
    this.#painter.image(this.#buildabilityImage, blockImageBounds(this.#buildabilityData), { alpha: 0.5 });
  }

  // Trace each island once, into surface coordinates. Called whenever the world or the fit changes, and
  // only then: nothing a selection, an exclusion or a drag does moves an island outline.
  #traceIslands() {
    this.#islandPaths = [];
    if (!this.#toSvg) return;
    for (const island of (this.#ctx?.islands || [])) {
      const poly = island.simplified_polygon ?? geojsonToSimplified(island.polygon);
      if (!poly?.exterior?.length) continue;
      this.#islandPaths.push({ id: island.id, data: polyToPath(poly, this.#toSvg) });
    }
  }

  // The islands, with their selection / exclusion / team-tint state applied as they are drawn: a
  // team-assigned island is tinted that team's colour, the selected one gets an accent border, an excluded
  // one is dimmed, and the fill drops out entirely while the block overlay is showing the real terrain.
  #paintIslands() {
    for (const { id, data } of this.#islandPaths) {
      const selected = this.#selectedIslandId === id;
      const team     = this.#islandTeamColors.get(id);
      this.#painter.path(data, {
        fill: team || "var(--canvas-island)",
        fillAlpha: this.#showBlocks ? 0 : 0.25,
        fillRule: "evenodd",
        stroke: selected ? "var(--accent)" : (team || "var(--canvas-island-stroke)"),
        width: selected ? 2.5 : 1.2,
        alpha: this.#excludedIslandIds.has(id) ? 0.35 : 1,
      });
    }
  }

  // The island whose polygon actually contains the world point. Bounds alone are ambiguous — island
  // bounding boxes overlap on radial maps — so this point-in-polygon test picks the real island clicked.
  #hitTestIsland(worldX, worldZ) {
    for (const isl of (this.#ctx?.islands ?? [])) {
      if (isl.id == null) continue;
      const b = isl.bounds;   // [min_x, min_z, max_x, max_z] — quick reject before the polygon test
      if (b && (worldX < b[0] || worldX > b[2] + 1 || worldZ < b[1] || worldZ > b[3] + 1)) continue;
      const ring = (isl.simplified_polygon ?? geojsonToSimplified(isl.polygon))?.exterior;
      if (ring?.length && pointInRing(worldX, worldZ, ring)) return isl.id;
    }
    return null;
  }

  setIslandSelect(on) { this.#selectCtrl.setMode(on ? "island" : "region"); }

  /** Show the symmetry overlay for the given type + centre (type null clears it). */
  setSymmetry(type, cx, cz) {
    this.#symType = type || null;
    this.#symCx = cx ?? 0;
    this.#symCz = cz ?? 0;
    this.#paintWorld();
  }

  // Point-tool placement mode: the point tool drops a spawn via onPointPick (in _onToolMousedown) rather
  // than drawing a region. The placed spawn is a point dummy region, picked by the normal select hit-test.
  setPointPick(on) { this.#pointPick = !!on; }

  setSelectedIsland(id) {
    this.#selectedIslandId = (id === null || id === undefined) ? null : id;
    this.#paintWorld();
  }

  setExcludedIslands(ids) {
    this.#excludedIslandIds = new Set(ids || []);
    this.#paintWorld();
  }

  // map: { islandId: teamColourHex }. Tints each assigned island; unassigned stay neutral.
  setIslandTeams(map) {
    this.#islandTeamColors = new Map(Object.entries(map || {}).map(([k, v]) => [Number(k), v]));
    this.#paintWorld();
  }

  // A point-of-interest layer: one glyph per entry, in the colour its kind derives (see POI_LAYERS).
  #paintPoiLayer({ source, glyph, size, weight, color }) {
    if (!this.#showPois) return;
    for (const poi of (this.#ctx[source] || [])) {
      if (!poi.x && poi.x !== 0) continue;
      this.#painter.text(glyph, poi.x, poi.z, { fill: color(poi), size, weight });
    }
  }

  // ── overlay ────────────────────────────────────────────────────────────────

  #updateOverlay() {
    if (!this.#screen.overlay) return;
    clearLayer(this.#screen.overlay);

    const node = this.#selectedNode;
    if (!node?.bounds || node.is_negative || !this.#toSvg) return;

    const { min_x, min_z, max_x, max_z } = node.bounds;
    const color = node.color || "var(--canvas-region)";

    const p1b = this.#toSvg(min_x, min_z);
    const p2b = this.#toSvg(max_x, max_z);
    const toScr = (bx, by) => this._toScreen(bx, by);
    const s1 = toScr(p1b.x, p1b.y), s2 = toScr(p2b.x, p2b.y);
    const left = Math.min(s1.x, s2.x), right = Math.max(s1.x, s2.x);
    const top  = Math.min(s1.y, s2.y), bottom = Math.max(s1.y, s2.y);

    const maxChars = 36;
    const label = node.label ?? node.id ?? "";
    const labelText = label.length > maxChars ? label.slice(0, maxChars-1) + "…" : label;
    const nameEl = svgEl("text", {
      x: left, y: top - 5,
      "text-anchor": "start", "dominant-baseline": "alphabetic",
      "font-size": "11", "font-family": "ui-monospace, monospace",
      fill: color, "pointer-events": "none",
    });
    nameEl.textContent = labelText;
    this.#screen.overlay.appendChild(nameEl);

    renderDimensionPill(this.#screen.overlay, {
      left, right, bottom, width: max_x - min_x, depth: max_z - min_z, color,
    });

    if (RESIZABLE_TYPES.has(node.type)) this.#editCtrl.renderHandles(node);
  }

  // ── anchors ────────────────────────────────────────────────────────────────

  // The 1×1 block anchors of the selected region — its corners in block terms, so an extent is legible
  // even where the shape itself is a few pixels across.
  #paintAnchors() {
    const node = this.#selectedNode;
    if (!node?.bounds || node.is_negative || COMPOSITE_TYPES.has(node.type)) return;
    const { min_x, min_z, max_x, max_z } = node.bounds;
    const color = node.color ?? "var(--canvas-region)";
    if (["cylinder", "circle", "sphere"].includes(node.type)) {
      paintAnchorBlock(this.#painter, Math.floor((min_x + max_x) / 2), Math.floor((min_z + max_z) / 2), color);
      return;
    }
    const bMinX = Math.floor(min_x), bMinZ = Math.floor(min_z);
    const bMaxX = Math.ceil(max_x) - 1, bMaxZ = Math.ceil(max_z) - 1;
    paintAnchorBlock(this.#painter, bMinX, bMinZ, color);
    if (bMaxX !== bMinX || bMaxZ !== bMinZ) paintAnchorBlock(this.#painter, bMaxX, bMaxZ, color);
  }

  // ── region rendering ──────────────────────────────────────────────────────

  // The region set in paint order: the named primitives of the loaded tree, then anything added since
  // (drawn regions, authored dummies) that the tree does not already carry.
  #collectRegions() {
    this.#nodeMap.clear();
    this.#regionNodes = [];
    for (const region of this.#flattenNamed(this.#groups)) {
      this.#nodeMap.set(region.id, region);
      this.#regionNodes.push(region);
    }
    for (const node of this.#addedNodes) {
      if (this.#nodeMap.has(node.id)) continue;
      this.#nodeMap.set(node.id, node);
      this.#regionNodes.push(node);
    }
  }

  /**
   * Every region, styled by what it currently is rather than by an attribute patched onto it after the
   * fact: a marker keeps the marker treatment, a derived preview (`ghost`) keeps its faint one whatever
   * the selection does, and a hidden region is simply not drawn unless it is the selected one.
   */
  #paintRegions() {
    for (const node of this.#regionNodes) {
      const selected = this.#currentSelectedIds.has(node.id);
      if (this.#visibilityMap.get(node.id) === false && !selected) continue;
      const color = node.color ?? "var(--canvas-region)";
      const style = node.marker
        ? primitiveStyle("marker", { color, primary: node.primary })
        : primitiveStyle("region", { color, state: node.ghost ? "ghost" : (selected ? "selected" : "normal") });
      paintShape(this.#painter, node.marker ? "point" : node.type, node.polygon_2d ?? node.bounds, style);
    }
  }

  // ── flatten helpers ────────────────────────────────────────────────────────

  #flattenNamed(groupsOrNodes, out = []) {
    for (const item of groupsOrNodes) {
      if (item.regions) { this.#flattenNamed(item.regions, out); continue; }
      if (this.#resolvedMode && item.id && item.polygon_2d && this.#visibilityMap.get(item.id) !== false) {
        out.push(item); continue;
      }
      if (item.id && !COMPOSITE_TYPES.has(item.type) && (item.bounds || item.polygon_2d)) {
        out.push(item);
        if (!item.bounds && item.polygon_2d) continue;
      }
      this.#flattenNamed(item.children || [], out);
      if (item.source && !item.source.id) this.#flattenNamed([item.source], out);
    }
    return out;
  }
}
