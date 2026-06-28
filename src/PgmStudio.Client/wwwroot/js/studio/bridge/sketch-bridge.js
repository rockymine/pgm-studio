// sketch-bridge.js — JS-interop bridge for the Sketch tool's Layout canvas. Plays the reference's
// "layout activity" role on the JS side: owns the live shape list + the island recompute loop
// (geometry/boolean.js, the hot path), drives SketchCanvas, owns the arrow-key nudge, and pushes the
// island→shape tree to the Blazor panel. Blazor owns the toolbar/panel chrome + persistence; it calls
// the handle methods and receives OnShapeSelected / OnIslandSelected / OnLayout / OnDirty / OnToolChanged.
// getState() returns the layout for the host to PATCH (persistence wiring = S2d).

import { SketchCanvas } from "../canvas/sketch-canvas.js";
import { computeIslands, assignShapesToIslands, computeMirrorPreview, restoreIslandMeta } from "../geometry/boolean.js";
import { rectToPolygon, translateShape, toRing } from "../geometry/shape.js";
import { LIBRARY, instantiate, libraryMeta } from "../geometry/shape-library.js";
import { applySymmetry, orbitAxes } from "../geometry/symmetry.js";

// Default footprint = 2-team landscape (120×80), framed about the origin. CTW maps fit a ~120-block long
// axis with 10–15-wide lanes; a tight default keeps the canvas at a scale where those read true.
const DEFAULT_SETUP = { bbox: { min_x: -60, max_x: 60, min_z: -40, max_z: 40 }, center: { cx: 0, cz: 0 }, mirror_mode: "rot_180" };

let _seq = 0;
const genId = () => `s${Date.now()}_${_seq++}`;

function dimLabel(s) {
  if (s.type === "rectangle") return `${s.max_x - s.min_x}×${s.max_z - s.min_z}`;
  if (s.type === "circle")    return `r=${s.radius}`;
  return `${s.vertices?.length ?? 0} v`;
}

export async function mount(svgEl, wrapEl, coordsEl, zoomEl, dimEl, dotnetRef) {
  let setup = { ...DEFAULT_SETUP };
  // Stacked layers (S7b): each holds its own shapes/islands at a base_y. The canvas always edits the
  // ACTIVE layer's shapes; other layers keep cached shapes+islands for ghosting (2-D) and stacking (iso).
  let layers = [{ id: genId(), name: "Ground", baseY: 0, shapes: [], islands: [], savedMetas: [] }];
  let active = 0;
  let islands = [];            // alias of layers[active].islands — kept current by recompute()
  let mirrorVisible = true;
  let selectedIslandId = null; // panel island selection (drives arrow-move of the whole island)
  let view = "2d";             // "2d" | "iso" — the read-only isometric height preview (S6)
  let isoYaw = 30;

  const fire = (name, ...args) => { try { dotnetRef?.invokeMethodAsync(name, ...args); } catch { /* host may not wire it */ } };
  const markDirty = () => fire("OnDirty", islands.length);
  const syncActive = () => { if (layers[active]) layers[active].shapes = canvas.getShapes(); };

  // Compute a layer's islands from its shapes (used for non-active layers at load + on switch).
  function computeLayerIslands(shapes, savedMetas) {
    const { islands: next, addUnion, afterSub, overrideAddUnion } = computeIslands(shapes, []);
    assignShapesToIslands(shapes, next, addUnion, overrideAddUnion, afterSub);
    if (savedMetas?.length) restoreIslandMeta(next, savedMetas, ["id", "name", "mirrors"]);
    return next;
  }

  // The other layers' island outlines (for the 2-D ghost render).
  const ghostPolys = () => layers.flatMap((L, i) => i === active ? [] : L.islands.map(o => ({ exterior: o.exterior, holes: o.holes })));

  const canvas = new SketchCanvas(svgEl, wrapEl, {
    cursorEl: coordsEl, zoomEl, dimEl,
    onShapeCreated: (partial) => {
      const shape = { ...partial, id: genId(), override: partial.override ?? false };
      canvas.addShape(shape);
      recompute();
      canvas.setActiveTool("select");
      fire("OnToolChanged", "select");
      selectShape(shape.id);
      markDirty();
    },
    onShapeUpdated: () => { recompute(); markDirty(); },
    onShapeSelected: (id) => selectShape(id),
    onShapeDeleted:  (id) => { canvas.removeShape(id); recompute(); selectShape(null); markDirty(); },
    onShapePromote:  (id) => promoteShape(id),
    onPlace:         (bx, bz) => placeAt(bx, bz),
    onVertexSelected: (shapeId, idx) => {
      const s = canvas.getShape(shapeId);
      const h = s ? (s.anchor_heights?.[idx] ?? s.base_height ?? 0) : 0;
      fire("OnVertexSelected", shapeId ?? null, idx, h);
    },
  });

  let placeSpecs = null;   // the armed library item's shapes (centred at origin), awaiting a drop point

  // Arm a library item for placement: instantiate it centred at origin, hand the ghost to the canvas.
  function armPlace(itemId) {
    const item = LIBRARY.find(i => i.id === itemId);
    if (!item) return;
    placeSpecs = instantiate(item, 0, 0);
    canvas.armPlace(placeSpecs);
  }

  // Drop the armed item at (bx,bz): translate each spec there, add as a real shape, then return to select.
  function placeAt(bx, bz) {
    if (!placeSpecs) return;
    const created = [];
    for (const spec of placeSpecs) {
      const shape = { ...translateShape(spec, bx, bz), id: genId(), override: spec.override ?? false };
      canvas.addShape(shape);
      created.push(shape.id);
    }
    placeSpecs = null;
    canvas.disarmPlace();
    recompute();
    canvas.setActiveTool("select");
    fire("OnToolChanged", "select");
    if (created.length) selectShape(created[created.length - 1]);
    markDirty();
  }

  function cancelPlace() {
    if (!placeSpecs) return false;
    placeSpecs = null;
    canvas.disarmPlace();
    canvas.setActiveTool("select");
    fire("OnToolChanged", "select");
    return true;
  }

  // Promote a rectangle to a polygon (keeps id, so its island membership + selection survive); a no-op
  // for any other type. After promotion the shape edits as a polygon (vertex/midpoint/Bézier).
  function promoteShape(id) {
    const s = canvas.getShape(id);
    if (!s || s.type !== "rectangle") return;
    canvas.updateShape(rectToPolygon(s));
    recompute();
    selectShape(id);
    markDirty();
  }

  function selectShape(id) {
    selectedIslandId = null;
    canvas.selectShape(id);
    fire("OnShapeSelected", id ?? null);
    fire("OnIslandSelected", null);
  }

  function selectIsland(id) {
    selectedIslandId = id ?? null;
    canvas.selectShape(null);
    fire("OnShapeSelected", null);
    fire("OnIslandSelected", selectedIslandId);
  }

  // Recompute islands from the canvas's current shapes and push results to the canvas + panel.
  // `restoreFromSaved` (load only) seeds metadata from persisted records; live edits carry metadata
  // over via the previous islands (centroid match inside computeIslands).
  function recompute(restoreFromSaved = false) {
    const shapes = canvas.getShapes();
    const prev = restoreFromSaved ? [] : islands;
    const { islands: next, addUnion, afterSub, overrideAddUnion } = computeIslands(shapes, prev);
    assignShapesToIslands(shapes, next, addUnion, overrideAddUnion, afterSub);
    const sm = layers[active].savedMetas;
    if (restoreFromSaved && sm?.length) restoreIslandMeta(next, sm, ["id", "name", "mirrors"]);
    islands = next;
    layers[active].islands = next;
    layers[active].shapes = shapes;
    canvas.setIslands(next.map(i => ({ exterior: i.exterior, holes: i.holes })));
    canvas.setGhostIslands(ghostPolys());
    refreshMirror();
    pushLayout();
    pushLayers();
    refreshIso();
  }

  // Build the iso "solids" for every layer: one solid PER SHAPE so per-shape heights are visible (a
  // per-island prism would collapse to the island's tallest shape and hide the rest). Each add shape
  // becomes a flat prism at its own base_height, or — if it carries per-vertex anchor_heights — sloped
  // terrain (S5c); subtract shapes carve and aren't solids. All shifted by the layer's base_y, with a
  // mirror copy per orbit axis for shapes whose island opts in (default: mirror). The renderer
  // depth-buffers them on the GPU, so where shapes overlap the taller one occludes — matching the
  // rasterizer's taller-surface-wins rule.
  function solidsForIso() {
    syncActive();
    const { cx = 0, cz = 0 } = setup.center ?? {};
    const axes = (mirrorVisible && setup.mirror_mode) ? orbitAxes(setup.mirror_mode) : [];
    const out = [];
    const hasAnchors = s => Array.isArray(s.anchor_heights) && s.vertices && s.anchor_heights.length === s.vertices.length;

    for (const L of layers) {
      // A shape mirrors unless its island says otherwise; ungrouped shapes default to mirroring.
      const mirrorOf = new Map();
      for (const isl of (L.islands ?? [])) for (const sid of (isl.shapeIds ?? [])) mirrorOf.set(sid, isl.mirrors !== false);

      const prismOf  = (s, ring, mirror) => ({ exterior: ring, holes: [], top: L.baseY + (s.base_height ?? 0), floor: L.baseY + (s.floor ?? 0), mirror });
      const terrainOf = (s, verts, mirror) => ({ vertices: verts, heights: s.anchor_heights.map(hh => L.baseY + hh), floor: L.baseY + (s.floor ?? 0), mirror });

      for (const s of L.shapes) {
        if (s.operation === "subtract") continue;            // carves land; not a solid
        const doMirror = mirrorOf.get(s.id) !== false;
        if (hasAnchors(s)) {
          out.push(terrainOf(s, s.vertices.map(v => [v[0], v[1]]), false));
          if (doMirror) for (const axis of axes) out.push(terrainOf(s, s.vertices.map(([x, z]) => applySymmetry(x, z, axis, cx, cz)), true));
        } else {
          const ring = toRing(s);
          if (ring.length < 4) continue;
          out.push(prismOf(s, ring, false));
          if (doMirror) for (const axis of axes) out.push(prismOf(s, ring.map(([x, z]) => applySymmetry(x, z, axis, cx, cz)), true));
        }
      }
    }
    return out;
  }

  function refreshIso() { if (view === "iso") canvas.showIso(solidsForIso(), isoYaw, setup.bbox); }

  function refreshMirror() {
    if (!mirrorVisible || !setup.mirror_mode) { canvas.setMirrorPolygons([]); return; }
    const { cx = 0, cz = 0 } = setup.center ?? {};
    canvas.setMirrorPolygons(computeMirrorPreview(islands, setup.mirror_mode, cx, cz));
  }

  // Push the island→shape tree to the Blazor panel (compact — render fields + a precomputed dim label).
  function pushLayout() {
    const shapes = canvas.getShapes().map(s => ({ id: s.id, type: s.type, operation: s.operation, override: !!s.override, dim: dimLabel(s), baseHeight: s.base_height ?? 0, floor: s.floor ?? 0 }));
    const isl = islands.map(i => ({ id: i.id, name: i.name, mirrors: i.mirrors, shapeIds: i.shapeIds }));
    fire("OnLayout", JSON.stringify({ islands: isl, shapes }));
  }

  // Push the layer list (id/name/base_y + active) to the Blazor layer panel.
  function pushLayers() {
    fire("OnLayers", JSON.stringify({ active: layers[active].id, layers: layers.map(L => ({ id: L.id, name: L.name, baseY: L.baseY })) }));
  }

  // Load the active layer's shapes onto the canvas (after a switch/delete) and recompute.
  function loadActiveToCanvas() {
    canvas.clearShapes();
    for (const sh of layers[active].shapes) canvas.addShape({ ...sh });
    selectShape(null);
    recompute();
  }

  function switchLayer(id) {
    const i = layers.findIndex(L => L.id === id);
    if (i < 0 || i === active) return;
    syncActive();
    active = i;
    loadActiveToCanvas();
    markDirty();
  }

  function addLayer() {
    syncActive();
    const baseY = Math.max(0, ...layers.map(L => L.baseY)) + 10;   // stack the new slab above by default
    layers.push({ id: genId(), name: `Layer ${layers.length + 1}`, baseY, shapes: [], islands: [], savedMetas: [] });
    active = layers.length - 1;
    loadActiveToCanvas();
    markDirty();
  }

  function deleteLayer(id) {
    if (layers.length <= 1) return;             // always keep one layer
    const i = layers.findIndex(L => L.id === id);
    if (i < 0) return;
    syncActive();
    layers.splice(i, 1);
    if (active > i) active--;
    if (active >= layers.length) active = layers.length - 1;
    loadActiveToCanvas();
    markDirty();
  }

  function renameLayer(id, name) { const L = layers.find(l => l.id === id); if (!L) return; L.name = name; pushLayers(); markDirty(); }
  function setLayerBaseY(id, y) { const L = layers.find(l => l.id === id); if (!L) return; L.baseY = y; pushLayers(); refreshIso(); markDirty(); }

  // Arrow-key nudge (Shift = 16) of the selected island (all its shapes) or the selected shape.
  const onKey = (e) => {
    if (wrapEl?.offsetParent == null) return;
    if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;
    if (e.key === "Escape" && cancelPlace()) { e.preventDefault(); return; }
    const step = e.shiftKey ? 16 : 1;
    let dx = 0, dz = 0;
    if (e.key === "ArrowLeft") dx = -step; else if (e.key === "ArrowRight") dx = step;
    else if (e.key === "ArrowUp") dz = -step; else if (e.key === "ArrowDown") dz = step;
    else return;
    let moved = false;
    if (selectedIslandId) {
      const isl = islands.find(i => i.id === selectedIslandId);
      for (const sid of (isl?.shapeIds ?? [])) {
        const s = canvas.getShape(sid);
        if (s) { canvas.updateShape(translateShape(s, dx, dz)); moved = true; }
      }
    } else if (canvas.selectedId) {
      const s = canvas.getShape(canvas.selectedId);
      if (s) { canvas.updateShape(translateShape(s, dx, dz)); moved = true; }
    }
    if (!moved) return;
    e.preventDefault();
    recompute();
    markDirty();
  };
  document.addEventListener("keydown", onKey);

  function applySetup(s) {
    setup = { bbox: s.bbox ?? setup.bbox, center: s.center ?? setup.center, mirror_mode: s.mirror_mode ?? setup.mirror_mode };
    if (s.bbox) { canvas.setBbox(setup.bbox); canvas.fitToBbox(); }
    if (s.center !== undefined) canvas.setCenter(setup.center.cx ?? 0, setup.center.cz ?? 0);
    if (s.mirror_mode !== undefined) canvas.setMode(setup.mirror_mode);
    refreshMirror();
  }

  function islandById(id) { return islands.find(i => i.id === id); }

  // Seed the default working bounds so drawing + the mirror preview work immediately.
  applySetup(setup);
  canvas.resize();

  return {
    setTool(tool)      {
      canvas.setActiveTool(tool === "select" ? "select" : tool);
      // Leaving select mode clears the selection — otherwise arrow-nudge keeps moving a shape that's no
      // longer visibly selected (you've switched to panning/drawing). Arrow-move is a select-mode action.
      if (tool !== "select") selectShape(null);
    },
    setOperation(op)   { canvas.setOperation(op); },
    setMode(mode)      { applySetup({ mirror_mode: mode }); markDirty(); },
    setCenter(cx, cz)  { applySetup({ center: { cx, cz } }); markDirty(); },
    setBbox(b)         { applySetup({ bbox: b }); markDirty(); },
    setShapesVisible(v){ canvas.setShapesVisible(v); },
    setMirrorVisible(v){ mirrorVisible = v; canvas.setMirrorVisible(v); refreshMirror(); },
    setChunkVisible(v) { canvas.setChunkVisible(v); },
    setSnap(v)         { canvas.setSnapEnabled(v); },
    setView(v)         { view = v === "iso" ? "iso" : "2d"; if (view === "iso") canvas.showIso(solidsForIso(), isoYaw, setup.bbox); else canvas.hideIso(); },
    rotateIso()        { isoYaw = (isoYaw + 90) % 360; refreshIso(); },
    setHeight(id, base, floor) {
      const s = canvas.getShape(id); if (!s) return;
      if (base  !== null && base  !== undefined) s.base_height = base;
      if (floor !== null && floor !== undefined) s.floor = floor;
      canvas.updateShape(s);   // refresh vertex labels (default = base height)
      pushLayout(); refreshIso(); markDirty();
    },
    // Set one vertex's height (S5b). Materialises anchor_heights (length = vertices, default = base) on first use.
    setVertexHeight(id, idx, h) {
      const s = canvas.getShape(id);
      if (!s?.vertices || idx < 0 || idx >= s.vertices.length) return;
      const base = s.base_height ?? 0;
      if (!Array.isArray(s.anchor_heights) || s.anchor_heights.length !== s.vertices.length)
        s.anchor_heights = s.vertices.map((_, i) => s.anchor_heights?.[i] ?? base);
      s.anchor_heights[idx] = h;
      canvas.updateShape(s);   // re-render the vertex labels
      pushLayout(); refreshIso(); markDirty();
    },

    // Panel-driven edits.
    selectShape(id)    { selectShape(id ?? null); },
    selectIsland(id)   { selectIsland(id ?? null); },
    deleteShape(id)    { canvas.removeShape(id); recompute(); selectShape(null); markDirty(); },
    promoteShape(id)   { promoteShape(id ?? canvas.selectedId); },
    getLibrary()       { return libraryMeta(); },
    armPlace(itemId)   { armPlace(itemId); },
    toggleOp(id)       { const s = canvas.getShape(id); if (!s) return; s.operation = s.operation === "subtract" ? "add" : "subtract"; canvas.updateShape(s); recompute(); markDirty(); },
    toggleOverride(id) { const s = canvas.getShape(id); if (!s) return; s.override = !s.override; canvas.updateShape(s); recompute(); markDirty(); },
    toggleMirrors(islandId) { const i = islandById(islandId); if (!i) return; i.mirrors = !i.mirrors; refreshMirror(); pushLayout(); markDirty(); },
    renameIsland(islandId, name) { const i = islandById(islandId); if (!i) return; i.name = name; pushLayout(); markDirty(); },

    // Layer ops (S7b).
    addLayer()              { addLayer(); },
    switchLayer(id)         { switchLayer(id); },
    deleteLayer(id)         { deleteLayer(id); },
    renameLayer(id, name)   { renameLayer(id, name); },
    setLayerBaseY(id, y)    { setLayerBaseY(id, y); },

    // Load a persisted layout: setup + the layers[] array (or a legacy single layout → one layer at base_y 0).
    load(state) {
      const s = state ?? {};
      if (s.setup) applySetup(s.setup);
      const raw = (s.layers && s.layers.length) ? s.layers : (s.layout ? [{ base_y: 0, layout: s.layout }] : []);
      layers = raw.map((L, i) => ({
        id: L.id || genId(),
        name: L.name || (i === 0 ? "Ground" : `Layer ${i + 1}`),
        baseY: L.base_y ?? 0,
        shapes: (L.layout?.shapes ?? []).map(sh => ({ ...sh })),
        islands: [],
        savedMetas: L.layout?.islands ?? [],
      }));
      if (!layers.length) layers = [{ id: genId(), name: "Ground", baseY: 0, shapes: [], islands: [], savedMetas: [] }];
      active = 0;
      // Cache the non-active layers' islands (for ghosts/iso); the active one is computed by recompute(true).
      for (let i = 0; i < layers.length; i++) if (i !== active) layers[i].islands = computeLayerIslands(layers[i].shapes, layers[i].savedMetas);
      canvas.clearShapes();
      for (const sh of layers[active].shapes) canvas.addShape({ ...sh });
      recompute(true);
    },
    // The layout for the host to persist (the SketchLayoutJson shape — now layers[]).
    getState() {
      syncActive();
      return {
        setup,
        layers: layers.map(L => ({
          id: L.id, name: L.name, base_y: L.baseY,
          layout: {
            shapes: L.shapes,
            islands: (L.islands ?? []).map(i => ({ id: i.id, name: i.name, mirrors: i.mirrors, shapeIds: i.shapeIds })),
          },
        })),
      };
    },
    islandCount() { return islands.length; },
    fitToBbox() { canvas.fitToBbox(); },
    resize() { canvas.resize(); },
    dispose() { document.removeEventListener("keydown", onKey); },
  };
}
