// sketch-bridge.js — JS-interop bridge for the Sketch tool's Layout canvas. Plays the reference's
// "layout activity" role on the JS side: owns the live shape list + the island recompute loop
// (geometry/boolean.js, the hot path), drives SketchCanvas, owns the arrow-key nudge, and pushes the
// island→shape tree to the Blazor panel. Blazor owns the toolbar/panel chrome + persistence; it calls
// the handle methods and receives OnShapeSelected / OnIslandSelected / OnLayout / OnDirty / OnToolChanged.
// getState() returns the layout for the host to PATCH (persistence wiring = S2d).

import { SketchCanvas } from "../canvas/sketch-canvas.js";
import { computeIslands, assignShapesToIslands, computeMirrorPreview, restoreIslandMeta } from "../geometry/boolean.js";
import { rectToPolygon, translateShape } from "../geometry/shape.js";
import { LIBRARY, instantiate, libraryMeta } from "../geometry/shape-library.js";

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
  let islands = [];            // latest computeIslands result (full, with metadata) — also prev for carry-over
  let savedMetas = [];         // island metadata loaded from persistence (name/mirrors/shapeIds)
  let mirrorVisible = true;
  let selectedIslandId = null; // panel island selection (drives arrow-move of the whole island)
  let view = "2d";             // "2d" | "iso" — the read-only isometric height preview (S6)
  let isoYaw = 30;

  const fire = (name, ...args) => { try { dotnetRef?.invokeMethodAsync(name, ...args); } catch { /* host may not wire it */ } };
  const markDirty = () => fire("OnDirty", islands.length);

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
    if (restoreFromSaved && savedMetas.length) restoreIslandMeta(next, savedMetas, ["id", "name", "mirrors"]);
    islands = next;
    canvas.setIslands(next.map(i => ({ exterior: i.exterior, holes: i.holes })));
    refreshMirror();
    pushLayout();
    refreshIso();
  }

  // Per-island extrusion column for the iso preview: top = the tallest shape in the island, floor = the
  // lowest. Mirror copies (when shown) reuse their source island's column.
  function islandsForIso() {
    const byId = new Map(canvas.getShapes().map(s => [s.id, s]));
    const columnOf = (isl) => {
      let top = 0, floor = 0, any = false;
      for (const sid of (isl.shapeIds ?? [])) {
        const s = byId.get(sid); if (!s) continue;
        any = true;
        top = Math.max(top, s.base_height ?? 0);
        floor = Math.min(floor, s.floor ?? 0);
      }
      return { top: any ? top : 0, floor };
    };
    const out = islands.map(i => ({ exterior: i.exterior, holes: i.holes, ...columnOf(i), mirror: false }));
    if (mirrorVisible && setup.mirror_mode) {
      const { cx = 0, cz = 0 } = setup.center ?? {};
      for (const m of computeMirrorPreview(islands, setup.mirror_mode, cx, cz)) {
        const src = islands.find(i => i.id === m.sourceId);
        out.push({ exterior: m.exterior, holes: m.holes, ...(src ? columnOf(src) : { top: 0, floor: 0 }), mirror: true });
      }
    }
    return out;
  }

  function refreshIso() { if (view === "iso") canvas.showIso(islandsForIso(), isoYaw); }

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
    setView(v)         { view = v === "iso" ? "iso" : "2d"; if (view === "iso") canvas.showIso(islandsForIso(), isoYaw); else canvas.hideIso(); },
    rotateIso()        { isoYaw = (isoYaw + 90) % 360; refreshIso(); },
    setHeight(id, base, floor) {
      const s = canvas.getShape(id); if (!s) return;
      if (base  !== null && base  !== undefined) s.base_height = base;
      if (floor !== null && floor !== undefined) s.floor = floor;
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

    // Load a persisted layout (S2d): setup + shapes + island metadata → render + recompute.
    load(state) {
      const s = state ?? {};
      if (s.setup) applySetup(s.setup);
      savedMetas = s.layout?.islands ?? [];
      canvas.clearShapes();
      for (const shape of (s.layout?.shapes ?? [])) canvas.addShape({ ...shape });
      recompute(true);
    },
    // The layout for the host to persist (the SketchLayoutJson shape).
    getState() {
      return {
        setup,
        layout: {
          shapes: canvas.getShapes(),
          islands: islands.map(i => ({ id: i.id, name: i.name, mirrors: i.mirrors, shapeIds: i.shapeIds })),
        },
      };
    },
    islandCount() { return islands.length; },
    fitToBbox() { canvas.fitToBbox(); },
    resize() { canvas.resize(); },
    dispose() { document.removeEventListener("keydown", onKey); },
  };
}
