// scan-canvas.js — JS-interop bridge for the new-map landing's "Found" preview (NS / ND3).
// Reuses the existing editor ConfigureRenderer (island base + surface overlay) — no new canvas — but
// feeds it the already-cached scan artifacts (top-surface / islands / symmetry) instead of the regions
// tree, so it renders an xml-less world that has no regions yet. The bounding box is seeded from the
// top-surface payload itself (loadBlockLayer self-seeds when none is set).
import { ConfigureRenderer } from "./canvas/configure-renderer.js";
import { fetchJson } from "./shared/fetch-json.js";

export async function mount(svgEl, wrapEl, slug) {
  const enc = encodeURIComponent(slug);
  const renderer = new ConfigureRenderer(svgEl, wrapEl);
  renderer.setMode("islands");   // surface pixels + island outlines — the "cleaned base" view

  const [topSurface, islands, symmetry] = await Promise.all([
    fetchJson(`/api/map/${enc}/layers/top-surface`),
    fetchJson(`/api/map/${enc}/islands`),
    fetchJson(`/api/map/${enc}/symmetry`),
  ]);

  if (topSurface) renderer.loadBlockLayer(topSurface);   // surface overlay (+ seeds the bbox)
  if (islands)    renderer.loadIslands(islands);         // detected island base outlines
  if (symmetry)   renderer.loadSymmetry(symmetry);       // axis + centre over the islands

  // Re-fit on container size changes (window resize, panel drags, layout settling after mount) — the
  // viewBox is computed from the wrap's client size, so without this it would freeze at its mount size.
  const ro = new ResizeObserver(() => renderer.resize());
  ro.observe(wrapEl);

  return {
    resize() { renderer.resize(); },
    dispose() { ro.disconnect(); },
  };
}
