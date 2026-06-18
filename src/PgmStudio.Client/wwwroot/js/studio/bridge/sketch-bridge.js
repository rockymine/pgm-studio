// sketch-bridge.js — JS-interop bridge for the Sketch tool's Layout canvas. Plays the reference's
// "layout activity" role on the JS side: owns the live shape list + the island recompute loop
// (geometry/boolean.js, the hot path), drives SketchCanvas, owns the arrow-key nudge, and pushes the
// island→shape tree to the Blazor panel. Blazor owns the toolbar/panel chrome + persistence; it calls
// the handle methods and receives OnShapeSelected / OnIslandSelected / OnLayout / OnDirty / OnToolChanged.
// getState() returns the layout for the host to PATCH (persistence wiring = S2d).

import { SketchCanvas } from "../canvas/sketch-canvas.js";
import { computeIslands, assignShapesToIslands, computeMirrorPreview, restoreIslandMeta } from "../geometry/boolean.js";

const DEFAULT_SETUP = { bbox: { min_x: -256, max_x: 256, min_z: -256, max_z: 256 }, center: { cx: 0, cz: 0 }, mirror_mode: "rot_180" };

let _seq = 0;
const genId = () => `s${Date.now()}_${_seq++}`;

function dimLabel(s) {
  if (s.type === "rectangle") return `${s.max_x - s.min_x}×${s.max_z - s.min_z}`;
  if (s.type === "circle")    return `r=${s.radius}`;
  return `${s.vertices?.length ?? 0} v`;
}

export async function mount(svgEl, wrapEl, coordsEl, zoomEl, dotnetRef) {
  let setup = { ...DEFAULT_SETUP };
  let islands = [];            // latest computeIslands result (full, with metadata) — also prev for carry-over
  let savedMetas = [];         // island metadata loaded from persistence (name/mirrors/shapeIds)
  let mirrorVisible = true;
  let selectedIslandId = null; // panel island selection (drives arrow-move of the whole island)

  const fire = (name, ...args) => { try { dotnetRef?.invokeMethodAsync(name, ...args); } catch { /* host may not wire it */ } };
  const markDirty = () => fire("OnDirty", islands.length);

  const canvas = new SketchCanvas(svgEl, wrapEl, {
    cursorEl: coordsEl, zoomEl,
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
  });

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
  }

  function refreshMirror() {
    if (!mirrorVisible || !setup.mirror_mode) { canvas.setMirrorPolygons([]); return; }
    const { cx = 0, cz = 0 } = setup.center ?? {};
    canvas.setMirrorPolygons(computeMirrorPreview(islands, setup.mirror_mode, cx, cz));
  }

  // Push the island→shape tree to the Blazor panel (compact — render fields + a precomputed dim label).
  function pushLayout() {
    const shapes = canvas.getShapes().map(s => ({ id: s.id, type: s.type, operation: s.operation, override: !!s.override, dim: dimLabel(s) }));
    const isl = islands.map(i => ({ id: i.id, name: i.name, mirrors: i.mirrors, shapeIds: i.shapeIds }));
    fire("OnLayout", JSON.stringify({ islands: isl, shapes }));
  }

  function translate(shape, dx, dz) {
    if (shape.type === "rectangle") { shape.min_x += dx; shape.max_x += dx; shape.min_z += dz; shape.max_z += dz; }
    else if (shape.type === "circle") { shape.center_x += dx; shape.center_z += dz; }
    else if (shape.vertices) shape.vertices = shape.vertices.map(([x, z]) => [x + dx, z + dz]);
  }

  // Arrow-key nudge (Shift = 16) of the selected island (all its shapes) or the selected shape.
  const onKey = (e) => {
    if (wrapEl?.offsetParent == null) return;
    if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;
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
        if (s) { translate(s, dx, dz); canvas.updateShape(s); moved = true; }
      }
    } else if (canvas.selectedId) {
      const s = canvas.getShape(canvas.selectedId);
      if (s) { translate(s, dx, dz); canvas.updateShape(s); moved = true; }
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
    setTool(tool)      { canvas.setActiveTool(tool === "select" ? "select" : tool); },
    setOperation(op)   { canvas.setOperation(op); },
    setMode(mode)      { applySetup({ mirror_mode: mode }); },
    setCenter(cx, cz)  { applySetup({ center: { cx, cz } }); },
    setBbox(b)         { applySetup({ bbox: b }); },
    setShapesVisible(v){ canvas.setShapesVisible(v); },
    setMirrorVisible(v){ mirrorVisible = v; canvas.setMirrorVisible(v); refreshMirror(); },
    setChunkVisible(v) { canvas.setChunkVisible(v); },

    // Panel-driven edits.
    selectShape(id)    { selectShape(id ?? null); },
    selectIsland(id)   { selectIsland(id ?? null); },
    deleteShape(id)    { canvas.removeShape(id); recompute(); selectShape(null); markDirty(); },
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
