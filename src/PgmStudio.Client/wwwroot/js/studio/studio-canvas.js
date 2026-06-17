// studio-canvas.js — JS-interop bridge for the reused EditorCanvas (the hybrid editor canvas).
// Blazor owns the sidebar/inspector/state in C#; this drives the proven canvas JS:
//   window.studioCanvas.mount(svgEl, wrapEl, dotnetRef, slug) → a handle Blazor calls
//   (load / setTool / setSelection / resize). Selection + cursor + zoom call back into C#.
import { EditorCanvas } from "./canvas/editor-canvas.js";

// Set-operation / transform region types — excluded from the category filter; their primitive children
// carry the category and render on their own.
const COMPOUND_TYPES = new Set(["union", "complement", "negative", "intersect", "mirror", "translate"]);

function geojsonToSimplified(polygon) {
  if (!polygon?.coordinates?.length) return null;
  return { exterior: polygon.coordinates[0] || [], holes: polygon.coordinates.slice(1) };
}
function normalizeIslands(islands) {
  return (islands ?? []).map(isl => ({
    ...isl,
    simplified_polygon: isl.simplified_polygon ?? geojsonToSimplified(isl.polygon),
  }));
}

async function fetchJson(url) {
  const r = await fetch(url, { cache: "no-store" });
  if (r.status === 404) return null;
  if (!r.ok) throw new Error(`${url} → ${r.status}`);
  return r.json();
}

/** Create an EditorCanvas on the given elements, load the map, and return a handle.
 *  Cursor/zoom labels are updated in JS (per-mousemove, hot path); only selection calls C#.
 *  Imported on demand from Blazor (await JS.import) — no global, no load-order race. */
export async function mount(svgEl, wrapEl, coordsEl, zoomEl, dotnetRef, slug, category, draftStep) {
  const canvas = new EditorCanvas(svgEl, wrapEl, {
    onCanvasClick: (node) => dotnetRef.invokeMethodAsync("OnCanvasSelect", node?.id ?? null),
    onCoords: (x, z) => { if (coordsEl) coordsEl.textContent = (x === null || x === undefined) ? "" : `X ${x}  Z ${z}`; },
    onZoom: (scale) => { if (zoomEl) zoomEl.textContent = `${Math.round(scale * 100)}%`; },
    // Draw-tool region creation (C5): a completed shape → C#, which POSTs /regions then reloads.
    onRegionDraw: (drawResult) => dotnetRef.invokeMethodAsync("OnRegionDraw", drawResult),
    // Island pick (World authoring step): a click in island-select mode → C# (null = clicked empty space).
    onIslandClick: (id) => dotnetRef.invokeMethodAsync("OnCanvasIslandSelect", id ?? null),
  });
  canvas.setActiveTool("move");
  let blockData = null;   // cached top-surface layer (C6), fetched on first toggle-on

  const handle = {
    async load(slugName) {
      const tree = await fetchJson(`/api/map/${encodeURIComponent(slugName)}/regions/tree`);
      if (!tree) return;
      const islands = await fetchJson(`/api/map/${encodeURIComponent(slugName)}/islands`).catch(() => null);
      // Category filter (comma-separated). We render the PRIMITIVE regions whose own derived category is
      // wanted, found anywhere in the tree — the same regions the activity sidebar lists. Objective/spawn/
      // build regions nest inside rule-containers in the "other" group, so a group-name filter misses them;
      // collecting by per-node category (compounds excluded — their primitive children carry the category)
      // gives exactly the sidebar's primitives. No filter (Regions activity) → render the whole tree.
      const wanted = category ? new Set(category.split(",")) : null;
      let groups;
      if (wanted) {
        const prims = [];
        const walk = (n) => {
          // a region's derived category matches the activity, OR it's a still-unwired draft drawn in this
          // step (category "other" until wired — E10). Both render as their primitive geometry.
          const matches = wanted.has(n.category)
            || (draftStep && n.draft_step === draftStep && n.category === "other");
          if (n.id && matches && !COMPOUND_TYPES.has(n.type)) prims.push(n);
          (n.children ?? []).forEach(walk);
          if (n.source) walk(n.source);
        };
        (tree.groups ?? []).forEach(g => (g.regions ?? []).forEach(walk));
        groups = [{ name: "filtered", label: "", regions: prims }];
      } else {
        groups = tree.groups;
      }
      canvas.render(
        { bounding_box: tree.bounding_box, islands: normalizeIslands(islands ?? []) },
        groups,
      );
    },
    setTool(tool) { canvas.setActiveTool(tool === "select" ? null : tool); },
    setSelection(ids) { canvas.setSelectedRegions(ids ?? []); },
    // Block-colour overlay (C6): lazily fetch the top-surface layer (B4), then toggle visibility.
    // Returns false when no scan data is available, so the caller can leave the toggle off.
    async setBlocks(visible) {
      if (visible && !blockData) {
        blockData = await fetchJson(`/api/map/${encodeURIComponent(slug)}/layers/top-surface`).catch(() => null);
        if (!blockData) return false;
        canvas.loadBlockLayer(blockData);
      }
      canvas.setBlocksVisible(visible);
      return true;
    },
    resize() { canvas.resize(); },
    // Island selection (World authoring step).
    setIslandSelect(on) { canvas.setIslandSelect(on); },
    setSelectedIsland(id) { canvas.setSelectedIsland(id ?? null); },
    setExcludedIslands(ids) { canvas.setExcludedIslands(ids ?? []); },
    setIslandTeams(map) { canvas.setIslandTeams(map ?? {}); },
    setSymmetry(type, cx, cz) { canvas.setSymmetry(type ?? null, cx, cz); },
    fitIsland(id) { canvas.fitIsland(id); },
    fitBounds(minX, minZ, maxX, maxZ) { canvas.fitBounds({ min_x: minX, min_z: minZ, max_x: maxX, max_z: maxZ }); },
    resetView() { canvas.resetView(); },
    dispose() { /* no explicit teardown; dropping the reference is enough */ },
  };

  await handle.load(slug);
  return handle;
}
