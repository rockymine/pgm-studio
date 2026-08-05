// sketch-bridge.js — JS-interop bridge for the Sketch tool's Layout canvas. Plays the reference's
// "layout activity" role on the JS side: owns the live shape list + the island recompute loop
// (geometry/boolean.js, the hot path), drives SketchCanvas, owns the arrow-key nudge, and pushes the
// island→shape tree to the Blazor panel. Blazor owns the toolbar/panel chrome + persistence; it calls
// the handle methods and receives OnShapeSelected / OnIslandSelected / OnLayout / OnDirty / OnToolChanged.
// getState() returns the layout for the host to PATCH (persistence wiring = S2d).

import { SketchCanvas } from "../canvas/sketch-canvas.js";
import { computeIslands, assignShapesToIslands, computeMirrorPreview, restoreIslandMeta, shapeToMultiPoly } from "../geometry/boolean.js";
import { rectToPolygon, translateShape, rotateShape, boundsOfShapes, splitShape } from "../geometry/shape.js";
import { surfaceHeights } from "../geometry/slope.js";
import { LIBRARY, instantiate, libraryMeta } from "../geometry/shape-library.js";
import { applySymmetry, orbitAxes } from "../geometry/symmetry.js";
import { defaultThemeJson, uniqueScopeId } from "../theme/theme-model.js";
import { fireTo } from "./fire.js";
import polygonClipping from "../vendor/polygon-clipping.js";

// Default footprint = 2-team landscape (120×80), framed about the origin. CTW maps fit a ~120-block long
// axis with 10–15-wide lanes; a tight default keeps the canvas at a scale where those read true.
const DEFAULT_SETUP = { bbox: { min_x: -60, max_x: 60, min_z: -40, max_z: 40 }, center: { cx: 0, cz: 0 }, mirror_mode: "rot_180" };

let _seq = 0;
const genId = () => `s${Date.now()}_${_seq++}`;

// Height invariants: every shape is at least one block tall (height >= 1, default 1) and its floor never
// dips below 0. clampHeight/clampFloor coerce an unset or out-of-range value to the nearest valid one.
const MIN_HEIGHT = 1;
const clampHeight = (h) => Math.max(MIN_HEIGHT, h ?? MIN_HEIGHT);
const clampFloor  = (f) => Math.max(0, f ?? 0);

function dimLabel(s) {
  if (s.type === "rectangle") return `${s.max_x - s.min_x}×${s.max_z - s.min_z}`;
  if (s.type === "circle")    return `r=${s.radius}`;
  // A path reads as how wide it is, not how many points it took to draw — the width is the knob an author
  // reaches for, and the point count is already visible as handles on the canvas.
  if (s.type === "path")      return `${(s.radius ?? 0) * 2} wide`;
  return `${s.vertices?.length ?? 0} v`;
}

export async function mount(svgEl, wrapEl, coordsEl, zoomEl, dimEl, dotnetRef, slug) {
  let setup = { ...DEFAULT_SETUP };
  // Stacked layers (S7b): each holds its own shapes/islands at a base_y. The canvas always edits the
  // ACTIVE layer's shapes; other layers keep cached shapes+islands for ghosting (2-D) and stacking (iso).
  let layers = [{ id: genId(), name: "Ground", baseY: 0, shapes: [], structural: [], islands: [], savedMetas: [] }];
  let active = 0;
  let islands = [];            // alias of layers[active].islands — kept current by recompute()
  let mirrorVisible = true;
  let selectedIslandId = null; // panel island selection (drives arrow-move of the whole island)
  let view = "2d";             // "2d" | "iso" — the read-only isometric height preview (S6)
  let isoYaw = 30;
  // Terrain-paint theming (finishing-model.md §4): a map-global registry + default; a shape's own override
  // rides on the shape (`shape.theme`), assigned via the Theme phase and resolved at export.
  let themes = {};
  let mapTheme = "";
  // The two room-style snapshots the map binds (structures.md §9): the shell every wool cage is stamped with
  // and the one every spawn cube is. Snapshots rather than library ids, so a library edit never rebuilds a
  // shipped map's rooms — and map-wide rather than per room, because a cage that differed between teams would
  // be a sightline that differed between teams.
  let roomStyles = { cage: null, spawn: null };
  // Dressing (decoration.md) does NOT ride beside theming. A theme is a recipe named once and applied to many
  // footprints; a prop was put somewhere, so the canvas owns the placements and this owns only the load/save.
  // Theme phase: the canvas is a selection surface only. Geometry is the Draw phase's to edit.
  let selectOnly = false;

  const fire = (name, ...args) => fireTo(dotnetRef, name, ...args);
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
      const shape = { ...partial, id: genId(), override: partial.override ?? false, base_height: clampHeight(partial.base_height), floor: clampFloor(partial.floor) };
      canvas.addShape(shape);
      recompute();
      canvas.setActiveTool("select");
      fire("OnToolChanged", "select");
      selectShape(shape.id);
      markDirty();
    },
    onShapeUpdated: () => { recompute(); markDirty(); },
    onShapeSelected: (id) => selectShape(id),
    onIslandSelected: (id) => selectIsland(id),
    // Placing, moving and picking a prop all happen on the canvas; the bridge only has to relay the result.
    onDressingChanged: () => afterDressingChange(),
    onPropSelected:    () => fire("OnDressing", dressingState()),
    onShapeDeleted:  (id) => { canvas.removeShape(id); recompute(); selectShape(null); markDirty(); },
    onShapePromote:  (id) => promoteShape(id),
    onSplit:         (a, b) => splitAt(a, b),
    onPlace:         (bx, bz) => placeAt(bx, bz),
    onVertexSelected: (shapeId, idx) => {
      const s = canvas.getShape(shapeId);
      const h = s ? clampHeight(s.anchor_heights?.[idx] ?? s.base_height) : MIN_HEIGHT;
      fire("OnVertexSelected", shapeId ?? null, idx, h);
    },
    // The shift-marked surface-slope control set changed — send each control's index + its current height
    // so the inspector can offer a height box per marked vertex + Apply.
    onSlopeControls: (shapeId, indices) => {
      const s = canvas.getShape(shapeId);
      if (!s?.vertices) { fire("OnSlopeControls", shapeId ?? null, "[]"); return; }
      const base = clampHeight(s.base_height);
      const controls = indices.map(idx => ({ idx, height: clampHeight(s.anchor_heights?.[idx] ?? base) }));
      fire("OnSlopeControls", shapeId ?? null, JSON.stringify(controls));
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
      const shape = { ...translateShape(spec, bx, bz), id: genId(), override: spec.override ?? false, base_height: clampHeight(spec.base_height), floor: clampFloor(spec.floor) };
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

  // Split tool (S14): the slice a→b cuts the topmost shape it crosses into two, in place. Try shapes
  // top-first; the first that yields a clean two-way cut is replaced by its two halves.
  function splitAt(a, b) {
    for (const s of [...canvas.getShapes()].reverse()) {
      const halves = splitShape(s, a, b);
      if (!halves) continue;
      canvas.removeShape(s.id);
      for (const h of halves)
        canvas.addShape({ ...h, id: genId(), override: h.override ?? false, base_height: clampHeight(h.base_height), floor: clampFloor(h.floor) });
      recompute();
      canvas.setActiveTool("select");        // a completed cut drops back to select (like the draw tools)
      fire("OnToolChanged", "select");
      selectShape(null);
      markDirty();
      return;
    }
  }

  function selectShape(id) {
    selectedIslandId = null;
    canvas.selectShape(id);
    fire("OnShapeSelected", id ?? null);
    fire("OnIslandSelected", null);
  }

  function selectIsland(id) {
    selectedIslandId = id ?? null;
    canvas.selectIsland(selectedIslandId);
    // A single-member island shows the shape inspector (its member) — set height / convert / op without
    // drilling; a multi-shape island shows the island inspector. Either way selectedIslandId stays set, so
    // arrow-nudge (and later rotate) act on the whole island.
    const isl = selectedIslandId ? islands.find(i => i.id === selectedIslandId) : null;
    const single = isl && isl.shapeIds.length === 1 ? isl.shapeIds[0] : null;
    fire("OnShapeSelected", single);
    fire("OnIslandSelected", single ? null : selectedIslandId);
  }

  // Rotate the current selection by `deg` degrees about its bbox centre (the inspector's numeric field; the
  // canvas owns the drag-handle path). Island selected → all its members; a drilled shape → just that shape.
  function rotateSelected(deg) {
    const rad = (deg || 0) * Math.PI / 180;
    if (!rad) return;
    let ids;
    if (selectedIslandId) { const isl = islands.find(i => i.id === selectedIslandId); ids = isl?.shapeIds ?? []; }
    else if (canvas.selectedId) ids = [canvas.selectedId];
    else return;
    const shapes = ids.map(id => canvas.getShape(id)).filter(Boolean);
    if (!shapes.length) return;
    const b = boundsOfShapes(shapes);
    const pivot = [(b.min_x + b.max_x) / 2, (b.min_z + b.max_z) / 2];
    for (const s of shapes) canvas.updateShape(rotateShape(s, rad, pivot));
    recompute();
    markDirty();
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
    canvas.setIslands(next.map(i => ({ id: i.id, shapeIds: i.shapeIds, exterior: i.exterior, holes: i.holes })));
    canvas.setGhostIslands(ghostPolys());
    refreshMirror();
    pushLayout();
    pushLayers();
    refreshIso();
    refreshPaint();   // the geometry moved, so the paint on it has too (no-op unless the overlay is on)
  }

  // Build the iso "solids" for every layer: one solid PER SHAPE so per-shape heights are visible (a
  // per-island prism would collapse to the island's tallest shape and hide the rest). Each add shape
  // becomes a flat prism spanning [floor, floor + height] (floor = elevation, height = thickness) —
  // carved by the layer's subtract shapes so holes/moats
  // show (subtracts are not solids themselves) — or, if it carries per-vertex anchor_heights, sloped
  // terrain (S5c). Carving follows the rasterizer's order: a normal subtract cuts normal adds, an
  // override subtract cuts everything. All shifted by the layer's base_y, with a mirror copy per orbit
  // axis for shapes whose island opts in (default: mirror). The renderer depth-buffers them on the GPU,
  // so where shapes overlap the taller one occludes — matching the rasterizer's taller-surface-wins rule.
  // (Per-anchor terrain shapes aren't carved — a TIN-with-holes top isn't modelled in the preview yet.)
  function solidsForIso() {
    syncActive();
    const { cx = 0, cz = 0 } = setup.center ?? {};
    const axes = (mirrorVisible && setup.mirror_mode) ? orbitAxes(setup.mirror_mode) : [];
    const out = [];
    const hasAnchors = s => Array.isArray(s.anchor_heights) && s.vertices && s.anchor_heights.length === s.vertices.length;
    const mirrorRing = (ring, axis) => ring.map(([x, z]) => applySymmetry(x, z, axis, cx, cz));

    for (const L of layers) {
      // A shape mirrors unless its island says otherwise; ungrouped shapes default to mirroring.
      const mirrorOf = new Map();
      for (const isl of (L.islands ?? [])) for (const sid of (isl.shapeIds ?? [])) mirrorOf.set(sid, isl.mirrors !== false);

      // Subtract footprints, split by override (normal subs spare override adds; override subs cut all).
      const subs = L.shapes.filter(s => s.operation === "subtract");
      const normalSubMP   = subs.filter(s => !s.override).map(shapeToMultiPoly).filter(p => p.length);
      const overrideSubMP = subs.filter(s =>  s.override).map(shapeToMultiPoly).filter(p => p.length);

      // floor = elevation (base_y + shape floor); a vertex's top = floor + its thickness (anchor_heights).
      const terrainOf = (s, verts, mirror) => {
        const fl = L.baseY + clampFloor(s.floor);
        return { vertices: verts, heights: s.anchor_heights.map(hh => fl + hh), floor: fl, mirror };
      };

      for (const s of L.shapes) {
        if (s.operation === "subtract") continue;            // carves land; not a solid
        const doMirror = mirrorOf.get(s.id) !== false;
        if (hasAnchors(s)) {
          out.push(terrainOf(s, s.vertices.map(v => [v[0], v[1]]), false));
          if (doMirror) for (const axis of axes) out.push(terrainOf(s, mirrorRing(s.vertices, axis), true));
          continue;
        }
        const floor = L.baseY + clampFloor(s.floor), top = floor + clampHeight(s.base_height);
        const clippers = s.override ? overrideSubMP : normalSubMP.concat(overrideSubMP);
        for (const { exterior, holes } of carveFootprint(s, clippers)) {     // add − subs → exterior + holes
          out.push({ exterior, holes, top, floor, mirror: false });
          if (doMirror) for (const axis of axes)
            out.push({ exterior: mirrorRing(exterior, axis), holes: holes.map(h => mirrorRing(h, axis)), top, floor, mirror: true });
        }
      }
    }
    return out;
  }

  // Carve an add shape's footprint with the given subtract MultiPolygons (reusing the same boolean the
  // 2-D islands use). Returns one {exterior, holes} per resulting polygon (a subtract can split or hole it).
  function carveFootprint(shape, clippers) {
    const mp = shapeToMultiPoly(shape);
    if (!mp.length) return [];
    let result = mp;
    if (clippers.length) { try { result = polygonClipping.difference(mp, ...clippers); } catch { result = mp; } }
    return result.map(poly => ({ exterior: poly[0], holes: poly.slice(1) }));
  }

  function refreshIso() { if (view === "iso") canvas.showIso(solidsForIso(), isoYaw, setup.bbox); }

  function refreshMirror() {
    if (!mirrorVisible || !setup.mirror_mode) { canvas.setMirrorPolygons([]); return; }
    const { cx = 0, cz = 0 } = setup.center ?? {};
    canvas.setMirrorPolygons(computeMirrorPreview(islands, setup.mirror_mode, cx, cz));
  }

  // Push the island→shape tree to the Blazor panel (compact — render fields + a precomputed dim label).
  function pushLayout() {
    const shapes = canvas.getShapes().map(s => ({
      id: s.id, type: s.type, operation: s.operation, override: !!s.override, dim: dimLabel(s),
      baseHeight: clampHeight(s.base_height), floor: clampFloor(s.floor),
      radius: s.radius ?? 0, pathEdge: s.path_edge ?? "", pathSeed: s.path_seed ?? 0,
    }));
    const isl = islands.map(i => ({ id: i.id, name: i.name, mirrors: i.mirrors, shapeIds: i.shapeIds }));
    fire("OnLayout", JSON.stringify({ islands: isl, shapes }));
  }

  // Push the layer list (id/name/base_y + active) to the Blazor layer panel.
  function pushLayers() {
    fire("OnLayers", JSON.stringify({ active: layers[active].id, layers: layers.map(L => ({ id: L.id, name: L.name, baseY: L.baseY })) }));
  }

  // Load the active layer's shapes onto the canvas (after a switch/delete) and recompute. The active layer's
  // locked plan pieces (S25) ride alongside as a render-only overlay — never a drawn/edited shape.
  function loadActiveToCanvas() {
    canvas.clearShapes();
    for (const sh of layers[active].shapes) canvas.addShape({ ...sh });
    canvas.setStructural(layers[active].structural ?? []);
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
    layers.push({ id: genId(), name: `Layer ${layers.length + 1}`, baseY, shapes: [], structural: [], islands: [], savedMetas: [] });
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
    if (selectOnly) return;   // Theme phase: the arrows are a move, and moving belongs to Draw
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

  // ── terrain-paint themes (finishing-model.md §4) ────────────────────────────
  // The theme state the Theme phase reads: the registry, the map default, and the resolved per-shape override
  // (every shape carrying a `theme`). The island tree is the selection surface, so the phase derives an
  // island's theme from its member shapes (uniform → that theme, else mixed).
  function themesState() {
    const shapeThemes = {};
    syncActive();
    for (const L of layers) for (const s of (L.shapes || [])) if (s.theme) shapeThemes[s.id] = s.theme;
    return JSON.stringify({ themes, mapTheme: mapTheme || "", shapeThemes });
  }
  // A theme edit is a discrete action the author is waiting on the result of, so it repaints at once; only
  // the continuous geometry stream (drag, resize) is worth coalescing.
  function roomStylesState() { return JSON.stringify(roomStyles); }

  function afterThemeChange() { syncActive(); markDirty(); fire("OnThemes", themesState()); refreshPaint({ now: true }); }

  // ── dressing (decoration.md) ────────────────────────────────────────────────
  // Placed props live on the canvas (it is where they are put, moved and picked); the bridge announces changes
  // and carries them in and out of the stored document. Unlike a theme edit this does not repaint the Blocks
  // overlay: that shows the painter's surface colours, and dressing adds blocks *above* the surface.
  function dressingState() {
    const tools = canvas.dressingTools;
    const selectedId = tools?.selectedId ?? null;
    return JSON.stringify({
      props: canvas.dressing.props,
      selectedId,
      selected: selectedId ? canvas.dressing.byId(selectedId) : null,
    });
  }

  function afterDressingChange() { markDirty(); fire("OnDressing", dressingState()); }

  // ── the painted Blocks overlay ─────────────────────────────────────────────
  // The Blocks toggle shows the blocks the export places, not an approximation of them: the live layout goes
  // to `sketch/paint`, which runs the real painter over it and returns one colour per footprint cell, and the
  // canvas blits that as a bitmap. So a voronoi reads as its cells and a noise field as its patches — a
  // client-side "one representative colour per theme" could never show either. Only fetched while the overlay
  // is on, and coalesced: geometry edits fire continuously, and a paint is worth one round-trip per settle.
  // The wait is short because the paint itself is: a typical board repaints in tens of milliseconds, so this
  // is what a drag's trailing edge costs, not a cushion for slow work.
  //
  // It takes BOTH the Blocks toggle and the Theme phase. Theming is a finishing pass over a finished sketch,
  // so while the shapes are still being drawn the overlay shows the plain voxelization and nothing else —
  // that is what Blocks is for there, the exact cells an export would fill. Not painting during Draw also
  // costs the drawing loop nothing: no round-trip fires at all.
  const PAINT_DEBOUNCE_MS = 120;
  let paintTimer = null, paintSeq = 0, blocksOn = false, paintPhase = false;
  const paintWanted = () => blocksOn && paintPhase;

  function refreshPaint({ now = false } = {}) {
    if (!paintWanted() || !slug) return;
    clearTimeout(paintTimer);
    paintTimer = setTimeout(fetchPaint, now ? 0 : PAINT_DEBOUNCE_MS);
  }

  // Bring the overlay in line with the toggle + the phase: paint when both want it, drop the bitmap when
  // either stops, so re-entering can't flash a stale one.
  function syncPaint() {
    clearTimeout(paintTimer);
    if (paintWanted()) refreshPaint({ now: true });
    else canvas.loadPaintLayer(null);
  }

  async function fetchPaint() {
    const seq = ++paintSeq;
    try {
      const res = await fetch(`/api/map/${encodeURIComponent(slug)}/sketch/paint`, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(handle.getState()),
      });
      if (!res.ok) return;
      const data = await res.json();
      // Two wire forms, both palette-indexed: row-major runs (what a painted board almost always is), or a
      // per-cell list. The bitmap decoder reads runs directly; the cell list needs its colours expanded.
      if (!data.runs && data.color_idx) data.colors = data.color_idx.map(i => data.palette[i]);
      if (seq === paintSeq) canvas.loadPaintLayer(data);   // ignore a reply overtaken by a newer edit
    } catch { /* offline or mid-navigation — the overlay keeps the stone footprint */ }
  }

  // Set (or clear, with a falsy themeId) a shape's theme override — the live canvas shape so it persists on sync.
  function setShapeTheme(shapeId, themeId) {
    const s = canvas.getShape(shapeId);
    if (!s) return;
    if (themeId && themes[themeId]) s.theme = themeId; else delete s.theme;
  }

  // Start in the default "move" (pan) tool — matches the Blazor toolbar default. Without this the canvas
  // sits at CanvasBase's null tool, which the base treats as click-to-select, so a click on first load
  // would select a shape/island even though the move tool is shown (only the select tool should select).
  canvas.setActiveTool("move");

  // Seed the default working bounds so drawing + the mirror preview work immediately.
  applySetup(setup);
  canvas.resize();

  const handle = {
    setTool(tool)      {
      canvas.setActiveTool(tool === "select" ? "select" : tool);
      // Leaving select mode clears the selection — otherwise arrow-nudge keeps moving a shape that's no
      // longer visibly selected (you've switched to panning/drawing). Arrow-move is a select-mode action.
      // Not so in select-only mode: nothing there moves the selection, and the selection is what the phase
      // is *for*, so reaching for the hand tool to pan must not throw away what you picked.
      if (tool !== "select" && !selectOnly) selectShape(null);
    },
    // Selection-only: the Theme phase picks islands and shapes to paint but edits none of them. Forcing the
    // select tool is part of the restriction — a draw tool left armed would add geometry, which is equally
    // the Draw phase's job. Lifting it only lifts it; the tool stays where it is, which is what the Draw
    // toolbar is already showing.
    setSelectOnly(on)  {
      selectOnly = !!on;
      canvas.setSelectOnly(selectOnly);
      // An armed library item survives a tool change on its own, so disarm it rather than leave a placement
      // primed to drop a shape on the first click into the phase.
      if (selectOnly) { cancelPlace(); canvas.setActiveTool("select"); }
    },
    setOperation(op)   { canvas.setOperation(op); },
    setMode(mode)      { applySetup({ mirror_mode: mode }); markDirty(); },
    setCenter(cx, cz)  { applySetup({ center: { cx, cz } }); markDirty(); },
    setBbox(b)         { applySetup({ bbox: b }); markDirty(); },
    setShapesVisible(v){ canvas.setShapesVisible(v); },
    setMirrorVisible(v){ mirrorVisible = v; canvas.setMirrorVisible(v); refreshMirror(); },
    setChunkVisible(v) { canvas.setChunkVisible(v); },
    setBlocksVisible(v){
      blocksOn = !!v;
      canvas.setBlocksVisible(blocksOn);
      syncPaint();
    },
    // Whether this phase previews the finished paint. Only Theme does: Draw wants the raw voxelization while
    // the shapes are still moving, and painting the layout is server work worth not doing there at all.
    setPaintPreview(on) { paintPhase = !!on; syncPaint(); },
    setSnap(v)         { canvas.setSnapEnabled(v); },
    setView(v)         {
      if (v !== "iso") { view = "2d"; canvas.hideIso(); return; }
      // showIso resolves false if the WebGL preview can't initialise — stay in 2-D and tell the host
      // so it can disable the toggle (this also keeps recompute()'s refreshIso from retrying).
      canvas.showIso(solidsForIso(), isoYaw, setup.bbox).then(ok => {
        view = ok === false ? "2d" : "iso";
        if (ok === false) fire("OnIsoUnavailable");
      });
    },
    rotateIso()        { isoYaw = (isoYaw + 90) % 360; refreshIso(); },
    setHeight(id, base, floor) {
      const s = canvas.getShape(id); if (!s) return;
      if (base  !== null && base  !== undefined) s.base_height = clampHeight(base);   // >= 1
      if (floor !== null && floor !== undefined) s.floor = clampFloor(floor);         // >= 0
      canvas.updateShape(s);   // refresh vertex labels (default = base height)
      pushLayout(); refreshIso(); markDirty();
    },
    // Set one vertex's height (S5b). Materialises anchor_heights (length = vertices, default = base) on first use.
    setVertexHeight(id, idx, h) {
      const s = canvas.getShape(id);
      if (!s?.vertices || idx < 0 || idx >= s.vertices.length) return;
      const base = clampHeight(s.base_height);
      if (!Array.isArray(s.anchor_heights) || s.anchor_heights.length !== s.vertices.length)
        s.anchor_heights = s.vertices.map((_, i) => clampHeight(s.anchor_heights?.[i] ?? base));
      s.anchor_heights[idx] = clampHeight(h);   // a vertex is a height too — never below 1
      canvas.updateShape(s);   // re-render the vertex labels
      pushLayout(); refreshIso(); markDirty();
    },
    // Fit a tilted plane through the 2–3 control vertices (each `{idx, height}`) and read every vertex's
    // height off it → the shape's whole top becomes a flat slope (2 controls = a ramp, 3 = an aimed plane).
    // Heights round to blocks, so a slope reads as the neat straight steps of a staircase.
    applySlope(id, samplesJson) {
      const s = canvas.getShape(id);
      if (!s?.vertices) return;
      let samples;
      try { samples = JSON.parse(samplesJson); } catch { return; }
      const pts = samples
        .filter(c => c.idx >= 0 && c.idx < s.vertices.length)
        .map(c => ({ x: s.vertices[c.idx][0], z: s.vertices[c.idx][1], h: clampHeight(c.height) }));
      const heights = surfaceHeights(s.vertices, pts);
      if (!heights) return;   // fewer than 2 distinct control positions — nothing to fit
      s.anchor_heights = heights.map(clampHeight);
      canvas.updateShape(s);
      pushLayout(); refreshIso(); markDirty();
    },

    // The band a path stands for: its half-width, how its edges are drawn, and the seed a rough edge wanders
    // by. All three are stored on the shape and the ring is derived, so each lands as a reshape.
    setPathBand(id, radius, edge, seed) {
      const s = canvas.getShape(id);
      if (s?.type !== "path") return;
      if (radius !== null && radius !== undefined) s.radius = Math.max(1, Math.round(radius));
      if (edge) s.path_edge = edge;
      if (seed !== null && seed !== undefined) s.path_seed = Math.max(0, Math.round(seed));
      canvas.updateShape(s);
      recompute(); pushLayout(); refreshIso(); markDirty();
    },

    // Panel-driven edits.
    selectShape(id)    { selectShape(id ?? null); },
    selectIsland(id)   { selectIsland(id ?? null); },
    rotateSelected(deg){ rotateSelected(deg); },
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

    // ── terrain-paint themes (finishing-model.md §4) ──
    getThemes() { return themesState(); },
    getRoomStyles() { return roomStylesState(); },
    // A snapshot as its JSON text, or null/"" to fall back to that kind's built-in shell.
    setRoomStyle(kind, styleJson) {
      if (kind !== "cage" && kind !== "spawn") return;
      let parsed = null;
      if (styleJson) { try { parsed = JSON.parse(styleJson); } catch { parsed = null; } }
      roomStyles = { ...roomStyles, [kind]: parsed };
      markDirty();
      fire("OnRoomStyles", roomStylesState());
    },
    defineTheme(name) {
      const id = uniqueScopeId(Object.keys(themes), name || "theme");
      themes[id] = defaultThemeJson();
      afterThemeChange(); return id;
    },
    renameTheme(oldId, newId) {
      if (!themes[oldId]) return oldId;
      const id = uniqueScopeId(Object.keys(themes).filter(k => k !== oldId), newId || oldId);
      if (id === oldId) return oldId;
      themes[id] = themes[oldId]; delete themes[oldId];
      if (mapTheme === oldId) mapTheme = id;
      for (const L of layers) for (const s of (L.shapes || [])) if (s.theme === oldId) s.theme = id;
      afterThemeChange(); return id;
    },
    deleteTheme(id) {
      if (!themes[id]) return;
      delete themes[id];
      if (mapTheme === id) mapTheme = "";
      for (const L of layers) for (const s of (L.shapes || [])) if (s.theme === id) delete s.theme;
      afterThemeChange();
    },
    // Replace a theme's material JSON (the raw TerrainTheme). Returns an error string on invalid JSON, else null.
    setThemeJson(id, text) {
      if (!themes[id]) return "No such theme.";
      let parsed; try { parsed = JSON.parse(text); } catch (e) { return e?.message || "Invalid JSON"; }
      themes[id] = parsed; afterThemeChange(); return null;
    },
    setMapTheme(id) { mapTheme = (id && themes[id]) ? id : ""; afterThemeChange(); },
    // Assign (or clear, with an empty themeId) a theme to one shape — a per-shape override.
    assignShape(shapeId, themeId) { setShapeTheme(shapeId, themeId); afterThemeChange(); },
    // Assign (or clear) a theme to every shape of an island — the coarse scope, written per member shape.
    assignIsland(islandId, themeId) {
      const isl = islandById(islandId); if (!isl) return;
      for (const sid of (isl.shapeIds || [])) setShapeTheme(sid, themeId);
      afterThemeChange();
    },

    // ── dressing (decoration.md) ──
    // Placing is the canvas's; the bridge exposes reading the document, editing the selection, and the
    // per-kind settings a newly placed prop starts from.
    getDressing() { return dressingState(); },
    setDressingMode(on) { canvas.setDressingMode(!!on); if (on) fire("OnDressing", dressingState()); },
    selectProp(id) { canvas.dressingTools?.select(id || null); },
    deleteProp() { if (canvas.dressingTools?.deleteSelected()) afterDressingChange(); },
    /** Patch the selected prop. `patchJson` is a partial prop; returns an error string on bad JSON, else null. */
    updateProp(patchJson) {
      let patch; try { patch = JSON.parse(patchJson); } catch (e) { return e?.message || "Invalid JSON"; }
      canvas.dressingTools?.updateSelected(patch);
      afterDressingChange(); return null;
    },
    /** The starting values the next prop of a kind takes — what the inspector edits with nothing selected. */
    getPropSettings(kind) { return JSON.stringify(canvas.dressingTools?.settingsFor(kind) ?? {}); },
    setPropSettings(kind, patchJson) {
      let patch; try { patch = JSON.parse(patchJson); } catch (e) { return e?.message || "Invalid JSON"; }
      canvas.dressingTools?.setSettings(kind, patch);
      fire("OnDressing", dressingState()); return null;
    },

    // Load a persisted layout: setup + the layers[] array (or a legacy single layout → one layer at base_y 0).
    load(state) {
      const s = state ?? {};
      if (s.setup) applySetup(s.setup);
      themes = (s.themes && typeof s.themes === "object") ? s.themes : {};
      mapTheme = (s.mapTheme && themes[s.mapTheme]) ? s.mapTheme : "";
      roomStyles = {
        cage: s.roomStyles?.cage ?? null,
        spawn: s.roomStyles?.spawn ?? null,
      };
      canvas.setDressing(s.dressing && typeof s.dressing === "object" ? s.dressing : null);
      const raw = (s.layers && s.layers.length) ? s.layers : (s.layout ? [{ base_y: 0, layout: s.layout }] : []);
      // A layer's stored shapes are partitioned on load: role-tagged shapes are the plan's structural pieces
      // (S25) — carried as a locked render-only overlay, kept out of the drawn-shape pipeline (islands, raster,
      // mirror, edit) so they can neither be reshaped nor double-cover the ground. Everything else is terrain.
      layers = raw.map((L, i) => {
        const all = (L.layout?.shapes ?? []).map(sh => ({ ...sh }));
        return {
          id: L.id || genId(),
          name: L.name || (i === 0 ? "Ground" : `Layer ${i + 1}`),
          baseY: L.base_y ?? 0,
          shapes: all.filter(sh => !sh.role),
          structural: all.filter(sh => sh.role),
          islands: [],
          savedMetas: L.layout?.islands ?? [],
        };
      });
      if (!layers.length) layers = [{ id: genId(), name: "Ground", baseY: 0, shapes: [], structural: [], islands: [], savedMetas: [] }];
      active = 0;
      // Cache the non-active layers' islands (for ghosts/iso); the active one is computed by recompute(true).
      for (let i = 0; i < layers.length; i++) if (i !== active) layers[i].islands = computeLayerIslands(layers[i].shapes, layers[i].savedMetas);
      canvas.clearShapes();
      for (const sh of layers[active].shapes) canvas.addShape({ ...sh });
      canvas.setStructural(layers[active].structural ?? []);
      recompute(true);
      // Frame what was loaded. applySetup's fit above ran before the shapes existed, so on its own it
      // would open a saved sketch on the blank working area instead of on the drawing.
      canvas.fitToBbox();
    },
    // The layout for the host to persist (the SketchLayoutJson shape — now layers[]).
    getState() {
      syncActive();
      return {
        setup,
        // Terrain-paint theming (finishing-model.md §4): the registry + default ride the layout; each shape's
        // own override rides on the shape below. Omitted when empty so an unthemed sketch serialises as before.
        themes: Object.keys(themes).length ? themes : undefined,
        mapTheme: mapTheme || undefined,
        // The bound room shells, omitted when neither is picked so a sketch that never opened the step
        // serialises exactly as it did before it existed.
        roomStyles: (roomStyles.cage || roomStyles.spawn)
          ? { cage: roomStyles.cage ?? undefined, spawn: roomStyles.spawn ?? undefined }
          : undefined,
        // Dressing rides the same way, and is likewise omitted when empty so an undressed sketch serialises
        // exactly as it did before the phase existed.
        dressing: canvas.dressing.isEmpty ? undefined : canvas.dressing.toJSON(),
        layers: layers.map(L => ({
          id: L.id, name: L.name, base_y: L.baseY,
          layout: {
            // Merge the locked plan pieces (S25) back in so they persist with the terrain they annotate.
            shapes: [...L.shapes, ...(L.structural ?? [])],
            islands: (L.islands ?? []).map(i => ({ id: i.id, name: i.name, mirrors: i.mirrors, shapeIds: i.shapeIds })),
          },
        })),
      };
    },
    islandCount() { return islands.length; },
    fitToBbox() { canvas.fitToBbox(); },
    resize() { canvas.resize(); },
    dispose() { clearTimeout(paintTimer); document.removeEventListener("keydown", onKey); canvas.dispose(); },
  };
  return handle;
}
