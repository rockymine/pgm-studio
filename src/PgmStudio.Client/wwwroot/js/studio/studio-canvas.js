// studio-canvas.js — JS-interop bridge for the reused EditorCanvas (the hybrid editor canvas).
// Blazor owns the sidebar/inspector/state in C#; this drives the proven canvas JS:
//   window.studioCanvas.mount(svgEl, wrapEl, dotnetRef, slug) → a handle Blazor calls
//   (load / setTool / setSelection / resize). Selection + cursor + zoom call back into C#.
import { EditorCanvas } from "./canvas/editor-canvas.js";

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
export async function mount(svgEl, wrapEl, coordsEl, zoomEl, dotnetRef, slug, category) {
  const canvas = new EditorCanvas(svgEl, wrapEl, {
    onCanvasClick: (node) => dotnetRef.invokeMethodAsync("OnCanvasSelect", node?.id ?? null),
    onCoords: (x, z) => { if (coordsEl) coordsEl.textContent = (x === null || x === undefined) ? "" : `X ${x}  Z ${z}`; },
    onZoom: (scale) => { if (zoomEl) zoomEl.textContent = `${Math.round(scale * 100)}%`; },
  });
  canvas.setActiveTool("move");
  let blockData = null;   // cached top-surface layer (C6), fetched on first toggle-on

  const handle = {
    async load(slugName) {
      const tree = await fetchJson(`/api/map/${encodeURIComponent(slugName)}/regions/tree`);
      if (!tree) return;
      const islands = await fetchJson(`/api/map/${encodeURIComponent(slugName)}/islands`).catch(() => null);
      // Optional category filter (comma-separated, e.g. "wool_room,monument") — port of getRegionGroups.
      const wanted = category ? new Set(category.split(",")) : null;
      const groups = wanted ? (tree.groups ?? []).filter(g => wanted.has(g.name)) : tree.groups;
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
    dispose() { /* no explicit teardown; dropping the reference is enough */ },
  };

  await handle.load(slug);
  return handle;
}
