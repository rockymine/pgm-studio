// overview-canvas.js — JS-interop bridge for the Overview activity's static map render (E7).
// Blazor owns the identity/authors form in C#; this drives the reused OverviewRenderer: fetch the
// map bbox + top-surface block layer (+ symmetry overlay) and paint the preview. Returns a handle
// Blazor calls (resize / dispose). Imported on demand from studio.mountOverview (no global, no race).
import { OverviewRenderer } from "./canvas/overview-renderer.js";

async function fetchJson(url) {
  const r = await fetch(url, { cache: "no-store" });
  if (!r.ok) return null;
  return r.json();
}

export async function mount(svgEl, wrapEl, slug) {
  const renderer = new OverviewRenderer(svgEl, wrapEl);
  const enc = encodeURIComponent(slug);

  const [tree, topSurface, symmetry] = await Promise.all([
    fetchJson(`/api/map/${enc}/regions/tree`),
    fetchJson(`/api/map/${enc}/layers/top-surface`),
    fetchJson(`/api/map/${enc}/symmetry`),
  ]);

  if (tree?.bounding_box) {
    renderer.render(tree.bounding_box);
    if (topSurface) { renderer.loadBlockLayer(topSurface); renderer.setBlocksVisible(true); }
    if (symmetry) renderer.setSymmetryOverlay(symmetry, symmetry.status);
  }

  return {
    resize() { renderer.resize(); },
    dispose() { /* dropping the reference is enough */ },
  };
}
