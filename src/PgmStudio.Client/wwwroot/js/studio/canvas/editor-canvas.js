/**
 * EditorCanvas — SVG rendering engine for the editor activities.
 * Extends CanvasBase for shared pan/zoom/transform machinery.
 *
 * Public surface:
 *   render(ctx, groups)           full repaint + zoom reset
 *   refreshRegions(groups)        swap region layer without resetting zoom
 *   setSelectedRegions(ids)       highlight the given id set
 *   showAnchors(node)             highlight anchor blocks for focused region
 *   clearAnchors()                remove anchor highlights
 *   setBlocksVisible(v)           toggle block layer
 *   setPoisVisible(v)             toggle POI markers
 *   loadBlockLayer(data)          load top-surface block data
 *   addRegion(node)               add a region without repaint
 *   removeRegion(id)              remove a region
 *   setActiveTool(tool)           null | "move" | "rectangle" | "cylinder" | "circle" | "point" | "block"
 *   focusRegion(node)             pan+zoom to fit region in viewport
 *   resize()                      re-render at new dimensions (preserves zoom)
 *
 * ctx shape: { bounding_box: {min_x,min_z,max_x,max_z}, islands: [...] }
 * groups shape: [{name, label, color, regions: [node, ...]}]
 * node shape: { id, type, label, color, bounds?: {min_x,min_z,max_x,max_z}, children?, polygon_2d? }
 */

import { buildTransform, buildInverseTransform, svgEl, polyToPath, handleRectAttrs, anchorBlockEl } from "./transform.js";
import { CanvasBase, ZOOM_MIN, ZOOM_MAX } from "./canvas-base.js";
import { EditorDrawController } from "./editor-draw-controller.js";
import { chatColorHex, dyeColorHex } from "../shared/game-colors.js";
import { blockToExtentBounds } from "../shared/converters.js";
import { renderShape } from "../shared/shape-render.js";

const HANDLE_SIZE = 7;
const HANDLE_DEFS = [
  { key: "nw", pos: sb => [sb.left,  sb.top   ], cursor: "nw-resize" },
  { key: "n",  pos: sb => [sb.midX,  sb.top   ], cursor: "n-resize"  },
  { key: "ne", pos: sb => [sb.right, sb.top   ], cursor: "ne-resize" },
  { key: "w",  pos: sb => [sb.left,  sb.midY  ], cursor: "w-resize"  },
  { key: "e",  pos: sb => [sb.right, sb.midY  ], cursor: "e-resize"  },
  { key: "sw", pos: sb => [sb.left,  sb.bottom], cursor: "sw-resize" },
  { key: "s",  pos: sb => [sb.midX,  sb.bottom], cursor: "s-resize"  },
  { key: "se", pos: sb => [sb.right, sb.bottom], cursor: "se-resize" },
];

const COMPOSITE_TYPES = new Set(["union", "intersect", "negative", "complement"]);
const RESIZABLE_TYPES = new Set(["rectangle", "cuboid"]);

function geojsonToSimplified(polygon) {
  if (!polygon?.coordinates?.length) return null;
  return {
    exterior: polygon.coordinates[0] || [],
    holes:    polygon.coordinates.slice(1),
  };
}

export class EditorCanvas extends CanvasBase {
  #ctx    = null;
  #groups = [];
  #toSvg  = null;
  #toWorld= null;
  #callbacks;

  // DOM caches
  #regionGroupMap = new Map();
  #shapeMap       = new Map();
  #nodeMap        = new Map();
  #overlayLayer   = null;
  #highlightRect  = null;
  #anchorLayer    = null;
  #buildLayerEl   = null;
  #blockLayerEl   = null;
  #islandLayerEl  = null;
  #spawnLayerEl   = null;
  #woolLayerEl    = null;
  #monumentLayerEl= null;
  #regionsLayerEl = null;
  #drawLayerEl    = null;
  #addedNodes     = [];

  // draw controller (instantiated in constructor)
  #drawCtrl = null;

  // resize
  #resizeState = null;

  // visibility/selection
  #visibilityMap      = new Map();
  #currentSelectedIds = new Set();
  #resolvedMode       = false;

  // island selection (World authoring step): when on, canvas clicks pick an island instead of a region;
  // the selected island gets an accent border and excluded islands are dimmed.
  #islandPathMap     = new Map();   // island id → <path>
  #selectedIslandId  = null;
  #excludedIslandIds = new Set();
  #islandTeamColors  = new Map();   // island id → team colour hex (World · Teams island assignment)
  #islandSelect      = false;

  // symmetry overlay (World · Symmetry step): a dashed axis line (or two, for rot_90) + a centre marker.
  #symmetryLayerEl   = null;
  #symType           = null;
  #symCx             = 0;
  #symCz             = 0;

  // layer state
  #showPois   = false;
  #showBuild  = false;
  #showBlocks = false;
  #blockData  = null;
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
    this.#drawCtrl  = new EditorDrawController(
      () => this.#drawLayerEl,
      () => this.#toSvg,
      { onRegionDraw: (r) => this.#callbacks.onRegionDraw?.(r) },
    );
  }

  // ── CanvasBase hook overrides ──────────────────────────────────────────────

  _onViewportChanged() {
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
    const world = this.#toWorld(svgPt.x, svgPt.y);
    if (this.#islandSelect) {
      this.#callbacks.onIslandClick?.(this.#hitTestIsland(world.x, world.z));
      return;
    }
    this.#callbacks.onCanvasClick?.(this.#hitTest(world.x, world.z));
  }

  _onMouseleave() {
    this.#callbacks.onCoords?.(null, null);
  }

  _onResizeMove(e) {
    if (!this.#resizeState) return false;
    this.#doResize(e.clientX, e.clientY);
    return true;
  }

  _onResizeUp(e) {
    if (!this.#resizeState || e.button !== 0) return false;
    this.#callbacks.onBoundsSave?.(this.#resizeState.node, { ...this.#resizeState.node.bounds });
    this.#resizeState = null;
    this.#refreshCursor();
    this.#updateOverlay();
    return true;
  }

  // ── public API ─────────────────────────────────────────────────────────────

  render(ctx, groups) {
    this.#ctx    = ctx;
    this.#groups = groups || [];
    this.#addedNodes = [];
    this.#selectedNode = null;
    this.#regionGroupMap.clear();
    this.#shapeMap.clear();
    this.#nodeMap.clear();
    this.#visibilityMap.clear();
    this.#currentSelectedIds.clear();
    this.#selectedIslandId = null;
    this.#blockData        = null;
    this.#blockFetchId++;
    this.#blockFetchPromise = null;
    this._scale = 1;
    this._panX  = 0;
    this._panY  = 0;
    this.#repaint();
    this._onZoom(this._scale);
  }

  showAnchors(node) {
    this.#selectedNode = node;
    if (this.#anchorLayer) {
      while (this.#anchorLayer.firstChild) this.#anchorLayer.removeChild(this.#anchorLayer.firstChild);
      this.#renderAnchors();
    }
    this.#updateOverlay();
  }

  clearAnchors() {
    this.#selectedNode = null;
    if (this.#anchorLayer) {
      while (this.#anchorLayer.firstChild) this.#anchorLayer.removeChild(this.#anchorLayer.firstChild);
    }
    this.#updateOverlay();
  }

  setPoisVisible(v) {
    this.#showPois = v;
    if (this.#spawnLayerEl)    this.#spawnLayerEl.style.display    = v ? "" : "none";
    if (this.#woolLayerEl)     this.#woolLayerEl.style.display     = v ? "" : "none";
    if (this.#monumentLayerEl) this.#monumentLayerEl.style.display = v ? "" : "none";
  }

  setBuildVisible(v) {
    this.#showBuild = v;
    if (this.#buildLayerEl) this.#buildLayerEl.style.display = v ? "" : "none";
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
    const w  = this._wrap.clientWidth  - 24;
    const h  = this._wrap.clientHeight - 24;
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
    if (this.#blockLayerEl) this.#blockLayerEl.style.display = v ? "" : "none";
    if (this.#islandLayerEl) this.#islandLayerEl.setAttribute("fill-opacity", v ? "0" : "0.25");
  }

  loadBlockLayer(data) {
    this.#blockData = data;
    if (this.#blockLayerEl && this.#toSvg) {
      while (this.#blockLayerEl.firstChild) this.#blockLayerEl.removeChild(this.#blockLayerEl.firstChild);
      this.#renderBlockImage(this.#blockLayerEl);
      if (this.#showBlocks) this.#blockLayerEl.style.display = "";
    }
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
    for (const id of this.#regionGroupMap.keys()) this.#refreshRegionDisplay(id);
  }

  setRegionVisible(id, visible) {
    if (visible) this.#visibilityMap.delete(id);
    else         this.#visibilityMap.set(id, false);
    this.#refreshRegionDisplay(id);
  }

  updateRegionBounds(node, newBounds) {
    Object.assign(node.bounds, newBounds);
    const entry = this.#shapeMap.get(node.id);
    if (entry && this.#toSvg) {
      const { min_x, min_z, max_x, max_z } = node.bounds;
      const p1 = this.#toSvg(min_x, min_z);
      const p2 = this.#toSvg(max_x, max_z);
      if (["cylinder", "circle", "sphere"].includes(entry.type)) {
        const cx = (p1.x + p2.x) / 2, cy = (p1.y + p2.y) / 2;
        entry.shape.setAttribute("cx", cx);
        entry.shape.setAttribute("cy", cy);
        entry.shape.setAttribute("rx", Math.abs(p2.x - p1.x) / 2);
        entry.shape.setAttribute("ry", Math.abs(p2.y - p1.y) / 2);
      } else {
        entry.shape.setAttribute("x",      Math.min(p1.x, p2.x));
        entry.shape.setAttribute("y",      Math.min(p1.y, p2.y));
        entry.shape.setAttribute("width",  Math.abs(p2.x - p1.x));
        entry.shape.setAttribute("height", Math.abs(p2.y - p1.y));
      }
    }
    if (this.#selectedNode?.id === node.id) {
      if (this.#anchorLayer) {
        while (this.#anchorLayer.firstChild) this.#anchorLayer.removeChild(this.#anchorLayer.firstChild);
        this.#renderAnchors();
      }
      this.#updateOverlay();
    }
  }

  setActiveTool(tool) {
    this.#drawCtrl.cancel();
    this.#drawCtrl.setTool(tool);
    this._activeTool = tool;
    this.#refreshCursor();
  }

  addRegion(node) {
    if (!this.#regionsLayerEl || !this.#toSvg) return;
    const stale = this.#regionGroupMap.get(node.id);
    if (stale?.parentNode) stale.parentNode.removeChild(stale);
    const regionG = this.#regionGroup(node);
    this.#regionGroupMap.set(node.id, regionG);
    this.#nodeMap.set(node.id, node);
    this.#regionsLayerEl.appendChild(regionG);
    if (!this.#addedNodes.some(n => n.id === node.id)) this.#addedNodes.push(node);
  }

  removeRegion(id) {
    const g = this.#regionGroupMap.get(id) ?? this._svg.querySelector(`[id="region-${id}"]`);
    if (g?.parentNode) g.parentNode.removeChild(g);
    this.#regionGroupMap.delete(id);
    this.#shapeMap.delete(id);
    this.#nodeMap.delete(id);
    this.#visibilityMap.delete(id);
    this.#currentSelectedIds.delete(id);
    this.#addedNodes = this.#addedNodes.filter(n => n.id !== id);
    if (this.#selectedNode?.id === id) { this.#selectedNode = null; this.#updateOverlay(); }
  }

  renameNode(oldId, newId) {
    const g = this.#regionGroupMap.get(oldId);
    if (g) {
      this.#regionGroupMap.delete(oldId);
      this.#regionGroupMap.set(newId, g);
      g.setAttribute("id", `region-${newId}`);
      const titleEl = g.querySelector("title");
      if (titleEl) {
        const type = titleEl.textContent.replace(/^.*\(/, "").replace(/\)$/, "");
        titleEl.textContent = `${newId} (${type})`;
      }
    }
    const shape = this.#shapeMap.get(oldId);
    if (shape) { this.#shapeMap.delete(oldId); this.#shapeMap.set(newId, shape); }
    const node = this.#nodeMap.get(oldId);
    if (node) { this.#nodeMap.delete(oldId); this.#nodeMap.set(newId, node); }
    if (this.#visibilityMap.has(oldId)) {
      this.#visibilityMap.set(newId, this.#visibilityMap.get(oldId));
      this.#visibilityMap.delete(oldId);
    }
    if (this.#currentSelectedIds.has(oldId)) { this.#currentSelectedIds.delete(oldId); this.#currentSelectedIds.add(newId); }
  }

  resize() {
    if (!this.#ctx) return;
    const nW = this._wrap.clientWidth  - 24;
    const nH = this._wrap.clientHeight - 24;
    if (nW <= 0 || nH <= 0) return;
    if (nW === +this._svg.getAttribute("width") && nH === +this._svg.getAttribute("height")) return;
    this.#repaint();
  }

  refreshRegionBounds(nodeId, newBounds) {
    const node = this.#nodeMap.get(nodeId);
    if (!node || !this.#toSvg) return;
    node.bounds = newBounds;
    const entry = this.#shapeMap.get(nodeId);
    const groupEl = this.#regionGroupMap.get(nodeId);
    if (!entry || !groupEl) return;
    const color = node.color ?? "var(--canvas-region)";
    const newShape = renderShape(node.type, newBounds, this.#toSvg, this.#regionAttrs(color));
    if (newShape) {
      groupEl.replaceChild(newShape, entry.shape);
      this.#shapeMap.set(nodeId, { shape: newShape, type: node.type });
    }
    this.#updateOverlay();
  }

  refreshRegions(groups) {
    this.#groups = groups || [];
    this.#addedNodes = [];
    this.#selectedNode = null;
    this.#currentSelectedIds.clear();
    this.#visibilityMap.clear();
    const oldLayer = this.#regionsLayerEl;
    const newLayer = this.#buildXmlRegions();
    if (oldLayer?.parentNode) oldLayer.parentNode.replaceChild(newLayer, oldLayer);
    this.#updateOverlay();
  }

  // ── rendering ──────────────────────────────────────────────────────────────

  #repaint() {
    this.#drawCtrl.cancel();
    const w = this._wrap.clientWidth  - 24;
    const h = this._wrap.clientHeight - 24;
    this._svg.setAttribute("width",   w);
    this._svg.setAttribute("height",  h);
    this._svg.setAttribute("viewBox", `0 0 ${w} ${h}`);
    this.#toSvg   = buildTransform(this.#ctx.bounding_box, w, h);
    this.#toWorld = buildInverseTransform(this.#ctx.bounding_box, w, h);

    while (this._svg.firstChild) this._svg.removeChild(this._svg.firstChild);

    const viewport = svgEl("g");
    this._viewportG = viewport;
    this._applyViewportTransform();

    viewport.appendChild(this.#buildBuildRegion());
    viewport.appendChild(this.#buildBlockLayer());
    viewport.appendChild(this.#buildIslands());
    viewport.appendChild(this.#buildSymmetryLayer());
    viewport.appendChild(this.#buildSpawnLayer());
    viewport.appendChild(this.#buildXmlRegions());
    viewport.appendChild(this.#buildWoolLayer());
    viewport.appendChild(this.#buildMonumentLayer());
    viewport.appendChild(this.#buildAnchorLayer());
    viewport.appendChild(this.#buildDrawLayer());
    viewport.appendChild(this.#buildBlockHighlight());

    const overlayG = svgEl("g", { id: "layer-overlay" });
    this.#overlayLayer = overlayG;

    this._svg.appendChild(viewport);
    this._svg.appendChild(overlayG);
    this.#updateOverlay();
  }

  #refreshCursor() {
    if (this._activeTool === "move")    this._svg.style.cursor = "grab";
    else if (this._activeTool !== null) this._svg.style.cursor = "crosshair";
    else                               this._svg.style.cursor = "default";
  }

  // ── hit test ──────────────────────────────────────────────────────────────

  #hitTest(worldX, worldZ) {
    let best = null;
    let bestArea = Infinity;
    for (const [id, node] of this.#nodeMap) {
      if (!node.bounds) continue;
      if (this.#visibilityMap.get(id) === false) continue;
      const { min_x, min_z, max_x, max_z } = node.bounds;
      if (worldX >= min_x && worldX <= max_x && worldZ >= min_z && worldZ <= max_z) {
        const area = (max_x - min_x) * (max_z - min_z);
        if (area < bestArea) { bestArea = area; best = node; }
      }
    }
    return best;
  }

  // ── layers ────────────────────────────────────────────────────────────────

  #buildBuildRegion() {
    const g = svgEl("g", { id: "layer-build" });
    this.#buildLayerEl = g;
    if (!this.#showBuild) g.style.display = "none";
    return g;
  }

  #buildBlockLayer() {
    const g = svgEl("g", { id: "layer-blocks" });
    this.#blockLayerEl = g;
    if (!this.#showBlocks || !this.#blockData) g.style.display = "none";
    if (this.#blockData && this.#toSvg) this.#renderBlockImage(g);
    return g;
  }

  #renderBlockImage(g) {
    const { xs, zs, colors, min_x, min_z, max_x, max_z } = this.#blockData;
    const imgW = max_x - min_x + 1;
    const imgH = max_z - min_z + 1;
    const offscreen = document.createElement("canvas");
    offscreen.width  = imgW;
    offscreen.height = imgH;
    const ctx2d  = offscreen.getContext("2d");
    const pixels = ctx2d.createImageData(imgW, imgH);
    const data   = pixels.data;
    for (let i = 0; i < xs.length; i++) {
      const rgb = parseInt(colors[i].slice(1), 16);
      const idx = ((zs[i] - min_z) * imgW + (xs[i] - min_x)) * 4;
      data[idx] = (rgb >> 16) & 0xff; data[idx+1] = (rgb >> 8) & 0xff;
      data[idx+2] = rgb & 0xff;       data[idx+3] = 255;
    }
    ctx2d.putImageData(pixels, 0, 0);
    const p1 = this.#toSvg(min_x,     min_z);
    const p2 = this.#toSvg(max_x + 1, max_z + 1);
    const img = svgEl("image");
    img.setAttribute("href",   offscreen.toDataURL("image/png"));
    img.setAttribute("x",      Math.min(p1.x, p2.x));
    img.setAttribute("y",      Math.min(p1.y, p2.y));
    img.setAttribute("width",  Math.abs(p2.x - p1.x));
    img.setAttribute("height", Math.abs(p2.y - p1.y));
    img.setAttribute("pointer-events", "none");
    img.style.imageRendering = "pixelated";
    g.appendChild(img);
  }

  #buildIslands() {
    const g = svgEl("g", { id: "layer-islands", "fill-opacity": "0.25" });
    this.#islandLayerEl = g;
    this.#islandPathMap.clear();
    if (this.#showBlocks) g.setAttribute("fill-opacity", "0");
    for (const island of (this.#ctx.islands || [])) {
      const poly = island.simplified_polygon ?? geojsonToSimplified(island.polygon);
      if (!poly?.exterior?.length) continue;
      const path = svgEl("path", {
        d: polyToPath(poly, this.#toSvg),
        fill: "var(--canvas-island)", stroke: "var(--canvas-island-stroke)", "stroke-width": "1.2", "fill-rule": "evenodd",
      });
      if (island.id != null) this.#islandPathMap.set(island.id, path);
      g.appendChild(path);
    }
    this.#paintIslandStates();
    return g;
  }

  // Repaint island fill/border/opacity for the current selection, exclusions, and team tints (no full
  // re-render). A team-assigned island is tinted that team's colour; the selected one gets an accent border.
  #paintIslandStates() {
    for (const [id, path] of this.#islandPathMap) {
      const selected = this.#selectedIslandId === id;
      const excluded = this.#excludedIslandIds.has(id);
      const team     = this.#islandTeamColors.get(id);
      path.setAttribute("fill", team || "var(--canvas-island)");
      path.setAttribute("stroke", selected ? "var(--accent)" : (team || "var(--canvas-island-stroke)"));
      path.setAttribute("stroke-width", selected ? "2.5" : "1.2");
      path.setAttribute("opacity", excluded ? "0.35" : "1");
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
      if (ring?.length && EditorCanvas.#pointInRing(worldX, worldZ, ring)) return isl.id;
    }
    return null;
  }

  // Ray-cast point-in-polygon on a [[x,z], …] ring.
  static #pointInRing(x, z, ring) {
    let inside = false;
    for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
      const xi = ring[i][0], zi = ring[i][1], xj = ring[j][0], zj = ring[j][1];
      if (((zi > z) !== (zj > z)) && (x < (xj - xi) * (z - zi) / (zj - zi) + xi)) inside = !inside;
    }
    return inside;
  }

  setIslandSelect(on) { this.#islandSelect = !!on; }

  #buildSymmetryLayer() {
    const g = svgEl("g", { id: "layer-symmetry" });
    this.#symmetryLayerEl = g;
    this.#renderSymmetry();
    return g;
  }

  // Dashed axis line(s) + a centre marker for the confirmed symmetry (mirrors ConfigureRenderer):
  // mirror_x / rot_90 → vertical axis at cx; mirror_z / rot_90 / rot_180 → horizontal axis at cz;
  // mirror_d1 / mirror_d2 → a diagonal through the centre. Always a centre dot.
  #renderSymmetry() {
    const g = this.#symmetryLayerEl;
    if (!g) return;
    while (g.firstChild) g.removeChild(g.firstChild);
    const bbox = this.#ctx?.bounding_box;
    if (!this.#symType || !bbox || !this.#toSvg) return;
    const cx = this.#symCx, cz = this.#symCz, t = this.#symType;
    const axis = (x1, z1, x2, z2) => {
      const a = this.#toSvg(x1, z1), b = this.#toSvg(x2, z2);
      g.appendChild(svgEl("line", {
        x1: a.x, y1: a.y, x2: b.x, y2: b.y,
        stroke: "var(--canvas-symmetry)", "stroke-width": "1.5", "stroke-dasharray": "6 3", opacity: 0.8,
      }));
    };
    const span = Math.max(bbox.max_x - bbox.min_x, bbox.max_z - bbox.min_z) + 20;
    if (t === "mirror_x" || t === "rot_90") axis(cx, bbox.min_z - 10, cx, bbox.max_z + 10);
    if (t === "mirror_z" || t === "rot_90" || t === "rot_180") axis(bbox.min_x - 10, cz, bbox.max_x + 10, cz);
    if (t === "mirror_d1") axis(cx - span, cz - span, cx + span, cz + span);
    if (t === "mirror_d2") axis(cx - span, cz + span, cx + span, cz - span);
    const p = this.#toSvg(cx, cz);
    g.appendChild(svgEl("circle", {
      cx: p.x, cy: p.y, r: 5,
      fill: "var(--canvas-symmetry)", stroke: "var(--canvas-marker-stroke)", "stroke-width": "1.5", opacity: 0.9,
    }));
  }

  /** Show the symmetry overlay for the given type + centre (type null clears it). */
  setSymmetry(type, cx, cz) {
    this.#symType = type || null;
    this.#symCx = cx ?? 0;
    this.#symCz = cz ?? 0;
    this.#renderSymmetry();
  }

  setSelectedIsland(id) {
    this.#selectedIslandId = (id === null || id === undefined) ? null : id;
    this.#paintIslandStates();
  }

  setExcludedIslands(ids) {
    this.#excludedIslandIds = new Set(ids || []);
    this.#paintIslandStates();
  }

  // map: { islandId: teamColourHex }. Tints each assigned island; unassigned stay neutral.
  setIslandTeams(map) {
    this.#islandTeamColors = new Map(Object.entries(map || {}).map(([k, v]) => [Number(k), v]));
    this.#paintIslandStates();
  }

  #buildSpawnLayer() {
    const g = svgEl("g", { id: "layer-spawns" });
    this.#spawnLayerEl = g;
    if (!this.#showPois) g.style.display = "none";
    for (const spawn of (this.#ctx.spawns || [])) {
      if (!spawn.x && spawn.x !== 0) continue;
      const p = this.#toSvg(spawn.x, spawn.z);
      const t = svgEl("text", {
        x: p.x, y: p.y, "text-anchor": "middle", "dominant-baseline": "middle",
        "font-size": "12", fill: chatColorHex(spawn.team_color ?? ""), "font-weight": "bold",
      });
      t.textContent = "★";
      g.appendChild(t);
    }
    return g;
  }

  #buildWoolLayer() {
    const g = svgEl("g", { id: "layer-wools" });
    this.#woolLayerEl = g;
    if (!this.#showPois) g.style.display = "none";
    for (const wool of (this.#ctx.wools || [])) {
      if (!wool.x && wool.x !== 0) continue;
      const p = this.#toSvg(wool.x, wool.z);
      const t = svgEl("text", {
        x: p.x, y: p.y, "text-anchor": "middle", "dominant-baseline": "middle",
        "font-size": "11", fill: dyeColorHex(wool.color ?? ""),
      });
      t.textContent = "◆";
      g.appendChild(t);
    }
    return g;
  }

  #buildMonumentLayer() {
    const g = svgEl("g", { id: "layer-monuments" });
    this.#monumentLayerEl = g;
    if (!this.#showPois) g.style.display = "none";
    for (const mon of (this.#ctx.monuments || [])) {
      if (!mon.x && mon.x !== 0) continue;
      const p = this.#toSvg(mon.x, mon.z);
      const t = svgEl("text", {
        x: p.x, y: p.y, "text-anchor": "middle", "dominant-baseline": "middle",
        "font-size": "13", fill: dyeColorHex(mon.color ?? ""),
      });
      t.textContent = "⊕";
      g.appendChild(t);
    }
    return g;
  }

  #buildAnchorLayer() {
    const g = svgEl("g", { id: "layer-anchors" });
    this.#anchorLayer = g;
    this.#renderAnchors();
    return g;
  }

  #buildBlockHighlight() {
    const rect = svgEl("rect", {
      id: "block-highlight",
      x: 0, y: 0, width: 0, height: 0, rx: 1, visibility: "hidden",
      fill: "var(--canvas-ink)", "fill-opacity": "0.06",
      stroke: "var(--canvas-ink)", "stroke-opacity": "0.4", "stroke-width": "1",
      "vector-effect": "non-scaling-stroke", "pointer-events": "none",
    });
    this.#highlightRect = rect;
    return rect;
  }

  // ── overlay ────────────────────────────────────────────────────────────────

  #updateOverlay() {
    if (!this.#overlayLayer) return;
    while (this.#overlayLayer.firstChild) this.#overlayLayer.removeChild(this.#overlayLayer.firstChild);

    const node = this.#selectedNode;
    if (!node?.bounds || node.is_negative || !this.#toSvg) return;

    const { min_x, min_z, max_x, max_z } = node.bounds;
    const color = node.color || "var(--canvas-region)";

    const p1b = this.#toSvg(min_x, min_z);
    const p2b = this.#toSvg(max_x, max_z);
    const toScr = (bx, by) => ({ x: bx * this._scale + this._panX, y: by * this._scale + this._panY });
    const s1 = toScr(p1b.x, p1b.y), s2 = toScr(p2b.x, p2b.y);
    const left = Math.min(s1.x, s2.x), right = Math.max(s1.x, s2.x);
    const top  = Math.min(s1.y, s2.y), bottom = Math.max(s1.y, s2.y);
    const mid  = (left + right) / 2;

    const maxChars = 36;
    const labelText = node.label.length > maxChars ? node.label.slice(0, maxChars-1) + "…" : node.label;
    const nameEl = svgEl("text", {
      x: left, y: top - 5,
      "text-anchor": "start", "dominant-baseline": "alphabetic",
      "font-size": "11", "font-family": "ui-monospace, monospace",
      fill: color, "pointer-events": "none",
    });
    nameEl.textContent = labelText;
    this.#overlayLayer.appendChild(nameEl);

    const fmtDim = v => Number.isInteger(v) ? String(v) : v.toFixed(1);
    const dimText = `${fmtDim(max_x - min_x)} × ${fmtDim(max_z - min_z)}`;
    const FONT_SZ = 10, PAD_X = 6, PAD_Y = 3;
    const pillH = FONT_SZ + PAD_Y * 2;
    const pillW = dimText.length * (FONT_SZ * 0.6) + PAD_X * 2;
    const pillX = mid - pillW / 2;
    const pillY = bottom + 5;
    this.#overlayLayer.appendChild(svgEl("rect", {
      x: pillX, y: pillY, width: pillW, height: pillH, rx: 3,
      fill: color, "fill-opacity": "0.85", "pointer-events": "none",
    }));
    const dimEl = svgEl("text", {
      x: mid, y: pillY + PAD_Y + FONT_SZ - 1,
      "text-anchor": "middle", "dominant-baseline": "auto",
      "font-size": FONT_SZ, "font-family": "ui-monospace, monospace",
      // dark ink — the pill is always a bright region colour, so stay dark in both themes
      fill: "var(--canvas-handle-fill)", "font-weight": "600", "pointer-events": "none",
    });
    dimEl.textContent = dimText;
    this.#overlayLayer.appendChild(dimEl);

    if (RESIZABLE_TYPES.has(node.type)) this.#renderHandles(node);
  }

  // ── anchors ────────────────────────────────────────────────────────────────

  #renderAnchors() {
    const node = this.#selectedNode;
    if (!node?.bounds || !this.#toSvg || node.is_negative || COMPOSITE_TYPES.has(node.type)) return;
    const { min_x, min_z, max_x, max_z } = node.bounds;
    const color = node.color ?? "var(--canvas-region)";
    const isCircular = ["cylinder", "circle", "sphere"].includes(node.type);
    if (isCircular) {
      const cx = (min_x + max_x) / 2, cz = (min_z + max_z) / 2;
      this.#anchorLayer.appendChild(anchorBlockEl(this.#toSvg, Math.floor(cx), Math.floor(cz), color));
    } else {
      const bMinX = Math.floor(min_x), bMinZ = Math.floor(min_z);
      const bMaxX = Math.ceil(max_x) - 1, bMaxZ = Math.ceil(max_z) - 1;
      this.#anchorLayer.appendChild(anchorBlockEl(this.#toSvg, bMinX, bMinZ, color));
      if (bMaxX !== bMinX || bMaxZ !== bMinZ) this.#anchorLayer.appendChild(anchorBlockEl(this.#toSvg, bMaxX, bMaxZ, color));
    }
  }

  // ── region rendering ──────────────────────────────────────────────────────

  #buildXmlRegions() {
    this.#regionGroupMap.clear();
    this.#shapeMap.clear();
    this.#nodeMap.clear();
    const g = svgEl("g", { id: "layer-regions" });
    this.#regionsLayerEl = g;
    for (const region of this.#flattenNamed(this.#groups)) {
      const regionG = this.#regionGroup(region);
      this.#regionGroupMap.set(region.id, regionG);
      this.#nodeMap.set(region.id, region);
      g.appendChild(regionG);
    }
    for (const node of this.#addedNodes) {
      if (!this.#regionGroupMap.has(node.id)) {
        const regionG = this.#regionGroup(node);
        this.#regionGroupMap.set(node.id, regionG);
        this.#nodeMap.set(node.id, node);
        g.appendChild(regionG);
      }
    }
    return g;
  }

  #regionGroup(region) {
    const { id, type } = region;
    const color = region.color ?? "var(--canvas-region)";
    const g = svgEl("g", { id: `region-${id}` });
    const title = svgEl("title");
    title.textContent = `${id} (${type})`;
    g.appendChild(title);

    const boundsOrPoly = region.polygon_2d ?? region.bounds;
    const shape = renderShape(type, boundsOrPoly, this.#toSvg, this.#regionAttrs(color));
    if (shape) { g.appendChild(shape); this.#shapeMap.set(id, { shape, type }); }
    return g;
  }

  #regionAttrs(color) {
    return {
      fill: color, "fill-opacity": "0.20",
      stroke: color, "stroke-opacity": "0.55", "stroke-width": "1.5", "stroke-dasharray": "4,2",
      "vector-effect": "non-scaling-stroke",
    };
  }

  #refreshRegionDisplay(id) {
    const g = this.#regionGroupMap.get(id);
    if (!g) return;
    const isSelected = this.#currentSelectedIds.has(id);
    const isVisible  = this.#visibilityMap.get(id) !== false;
    g.style.display  = (isVisible || isSelected) ? "" : "none";
    const entry = this.#shapeMap.get(id);
    if (!entry) return;
    if (isSelected) {
      entry.shape.setAttribute("stroke-width",   "2.5");
      entry.shape.setAttribute("stroke-opacity", "0.85");
      entry.shape.setAttribute("fill-opacity",   "0.22");
      entry.shape.removeAttribute("stroke-dasharray");
    } else {
      entry.shape.setAttribute("stroke-width",   "1.5");
      entry.shape.setAttribute("stroke-opacity", "0.55");
      entry.shape.setAttribute("fill-opacity",   "0.20");
      entry.shape.setAttribute("stroke-dasharray", "4,2");
    }
  }

  // ── resize ────────────────────────────────────────────────────────────────

  #screenBounds(node) {
    if (!node?.bounds || !this.#toSvg) return null;
    const { min_x, min_z, max_x, max_z } = node.bounds;
    const toScr = (bx, by) => ({ x: bx * this._scale + this._panX, y: by * this._scale + this._panY });
    const p1b = this.#toSvg(min_x, min_z), p2b = this.#toSvg(max_x, max_z);
    const s1  = toScr(p1b.x, p1b.y), s2 = toScr(p2b.x, p2b.y);
    return {
      left: Math.min(s1.x, s2.x), right: Math.max(s1.x, s2.x),
      top:  Math.min(s1.y, s2.y), bottom: Math.max(s1.y, s2.y),
      midX: (s1.x + s2.x) / 2,   midY: (s1.y + s2.y) / 2,
      leftIsMinX: p1b.x <= p2b.x, topIsMinZ: p1b.y <= p2b.y,
    };
  }

  #handleFields(key, sb) {
    const lF = sb.leftIsMinX ? "min_x" : "max_x", rF = sb.leftIsMinX ? "max_x" : "min_x";
    const tF = sb.topIsMinZ  ? "min_z" : "max_z", bF = sb.topIsMinZ  ? "max_z" : "min_z";
    return { nw:{xField:lF,zField:tF}, n:{xField:null,zField:tF}, ne:{xField:rF,zField:tF},
              w:{xField:lF,zField:null}, e:{xField:rF,zField:null},
             sw:{xField:lF,zField:bF}, s:{xField:null,zField:bF}, se:{xField:rF,zField:bF} }[key];
  }

  #renderHandles(node) {
    const sb = this.#screenBounds(node);
    if (!sb) return;
    const hs = HANDLE_SIZE / 2;
    for (const h of HANDLE_DEFS) {
      const [cx, cy] = h.pos(sb);
      const el = svgEl("rect", {
        ...handleRectAttrs(cx, cy, hs), rx: 1,
        fill: "var(--canvas-handle-fill)", stroke: "var(--canvas-handle-stroke)", "stroke-width": "1.5", cursor: h.cursor,
      });
      el.addEventListener("mousedown", (e) => {
        if (e.button !== 0) return;
        e.stopPropagation(); e.preventDefault();
        const fields = this.#handleFields(h.key, this.#screenBounds(node));
        this.#resizeState = { node, ...fields, cursor: h.cursor };
        this._svg.style.cursor = h.cursor;
      });
      this.#overlayLayer.appendChild(el);
    }
  }

  #doResize(clientX, clientY) {
    if (!this.#resizeState || !this.#toWorld) return;
    const { node, xField, zField } = this.#resizeState;
    const svgPt = this._clientToSvg(clientX, clientY);
    const world = this.#toWorld(svgPt.x, svgPt.y);
    const nb    = { ...node.bounds };
    if (xField) nb[xField] = Math.round(world.x);
    if (zField) nb[zField] = Math.round(world.z);
    if (xField && nb.max_x - nb.min_x < 1)
      nb[xField] = xField === "max_x" ? nb.min_x + 1 : nb.max_x - 1;
    if (zField && nb.max_z - nb.min_z < 1)
      nb[zField] = zField === "max_z" ? nb.min_z + 1 : nb.max_z - 1;
    this.#callbacks.onBoundsChange?.(node, nb);
  }

  // ── draw tools ────────────────────────────────────────────────────────────

  #buildDrawLayer() {
    const g = svgEl("g", { id: "layer-draw" });
    this.#drawLayerEl = g;
    return g;
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
