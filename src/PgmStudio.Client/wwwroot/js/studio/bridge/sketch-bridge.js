// sketch-bridge.js — JS-interop bridge for the Sketch tool's Layout canvas. Plays the reference's
// "layout activity" role on the JS side: owns the live shape list + the island recompute loop
// (geometry/boolean.js, the hot path), drives SketchCanvas, and owns the arrow-key nudge. Blazor owns
// the toolbar/wizard chrome + persistence; it calls the handle methods and receives OnShapeSelected /
// OnDirty. getState() returns the layout for the host to PATCH (persistence wiring = S2d).

import { SketchCanvas } from "../canvas/sketch-canvas.js";
import { computeIslands, assignShapesToIslands, computeMirrorPreview, restoreIslandMeta } from "../geometry/boolean.js";

const DEFAULT_SETUP = { bbox: { min_x: -256, max_x: 256, min_z: -256, max_z: 256 }, center: { cx: 0, cz: 0 }, mirror_mode: "rot_180" };

let _seq = 0;
const genId = () => `s${Date.now()}_${_seq++}`;

export async function mount(svgEl, wrapEl, coordsEl, zoomEl, dotnetRef) {
  let setup = { ...DEFAULT_SETUP };
  let islands = [];           // latest computeIslands result (full, with metadata) — also prev for carry-over
  let savedMetas = [];        // island metadata loaded from persistence (name/mirrors/shapeIds)
  let mirrorVisible = true;

  const markDirty = () => { try { dotnetRef?.invokeMethodAsync("OnDirty", islands.length); } catch { /* host may not wire it yet */ } };

  const canvas = new SketchCanvas(svgEl, wrapEl, {
    cursorEl: coordsEl, zoomEl,
    onShapeCreated: (partial) => {
      const shape = { ...partial, id: genId(), override: partial.override ?? false };
      canvas.addShape(shape);
      recompute();
      canvas.setActiveTool("select");
      try { dotnetRef?.invokeMethodAsync("OnToolChanged", "select"); } catch { /* host may not wire it */ }
      select(shape.id);
      markDirty();
    },
    onShapeUpdated: () => { recompute(); markDirty(); },
    onShapeSelected: (id) => select(id),
    onShapeDeleted:  (id) => { canvas.removeShape(id); recompute(); select(null); markDirty(); },
  });

  function select(id) {
    canvas.selectShape(id);
    try { dotnetRef?.invokeMethodAsync("OnShapeSelected", id ?? null); } catch { /* host may not wire it */ }
  }

  // Recompute islands from the canvas's current shapes and push results back to the canvas.
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
  }

  function refreshMirror() {
    if (!mirrorVisible || !setup.mirror_mode) { canvas.setMirrorPolygons([]); return; }
    const { cx = 0, cz = 0 } = setup.center ?? {};
    canvas.setMirrorPolygons(computeMirrorPreview(islands, setup.mirror_mode, cx, cz));
  }

  function translate(shape, dx, dz) {
    if (shape.type === "rectangle") { shape.min_x += dx; shape.max_x += dx; shape.min_z += dz; shape.max_z += dz; }
    else if (shape.type === "circle") { shape.center_x += dx; shape.center_z += dz; }
    else if (shape.vertices) shape.vertices = shape.vertices.map(([x, z]) => [x + dx, z + dz]);
  }

  // Arrow-key nudge of the selected shape (Shift = 16). Activity-level, per the controller-pattern
  // split — the canvas owns Escape/Delete/dblclick; the bridge owns move.
  const onKey = (e) => {
    if (wrapEl?.offsetParent == null) return;
    if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;
    const id = canvas.selectedId;
    if (!id) return;
    const step = e.shiftKey ? 16 : 1;
    let dx = 0, dz = 0;
    if (e.key === "ArrowLeft") dx = -step; else if (e.key === "ArrowRight") dx = step;
    else if (e.key === "ArrowUp") dz = -step; else if (e.key === "ArrowDown") dz = step;
    else return;
    e.preventDefault();
    const shape = canvas.getShape(id);
    if (!shape) return;
    translate(shape, dx, dz);
    canvas.updateShape(shape);
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
