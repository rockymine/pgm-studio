// configure-canvas.js — JS-interop bridge for the Configure wizard's dedicated preview (E8).
// Drives the reused ConfigureRenderer (layer pixels / island outlines / symmetry overlay) — a
// read-only preview, distinct from the editor canvas. The C# activity owns the wizard state and
// calls these methods; the bridge fetches per-step data and forwards it to the renderer.
import { ConfigureRenderer } from "./canvas/configure-renderer.js";
import { fetchJson } from "./shared/fetch-json.js";

export async function mount(svgEl, wrapEl, slug) {
  const enc = encodeURIComponent(slug);
  const renderer = new ConfigureRenderer(svgEl, wrapEl);

  // Seed the bounds so islands/symmetry can render before any layer pixels load.
  const tree = await fetchJson(`/api/map/${enc}/regions/tree`);
  if (tree?.bounding_box) renderer.setBounds(tree.bounding_box);

  return {
    setMode(mode) { renderer.setMode(mode); },
    async showLayer(type) { const d = await fetchJson(`/api/configure/${enc}/layers/${encodeURIComponent(type)}/pixels`); if (d) renderer.loadBlockLayer(d); },
    async showIslands() { const d = await fetchJson(`/api/map/${enc}/islands`); if (d) renderer.loadIslands(d); },
    setExcludedIds(ids) { renderer.setExcludedIds(ids ?? []); },
    async showSymmetry() { const d = await fetchJson(`/api/map/${enc}/symmetry`); if (d) renderer.loadSymmetry(d); },
    setSymmetryType(type) { renderer.setSymmetryType(type); },
    setCenter(cx, cz) { renderer.setCenter(cx, cz); },
    resize() { renderer.resize(); },
    dispose() { /* dropping the reference is enough */ },
  };
}
