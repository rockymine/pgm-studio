// studio.js — small JS-interop helpers for the ported studio UI.
// The reference renders Lucide icons from `<i data-lucide="...">` placeholders; we re-run the
// icon factory after Blazor renders so new/updated icons appear. Matches the reference attrs.
window.studio = {
  icons() {
    if (window.lucide && typeof window.lucide.createIcons === "function") {
      window.lucide.createIcons({ attrs: { "stroke-width": "1.5", width: "16", height: "16" } });
    }
  },

  // Mount the hybrid editor canvas. Uses a native dynamic import (absolute URL) so it bypasses
  // Blazor's fingerprinting import map (which 404s for arbitrary wwwroot modules under the dev host).
  async mountCanvas(svgEl, wrapEl, coordsEl, zoomEl, dotnetRef, slug, category, draftStep) {
    const mod = await import("/js/studio/studio-canvas.js");
    return mod.mount(svgEl, wrapEl, coordsEl, zoomEl, dotnetRef, slug, category, draftStep);
  },

  // Mount the Overview static map render (E7).
  async mountOverview(svgEl, wrapEl, slug) {
    const mod = await import("/js/studio/overview-canvas.js");
    return mod.mount(svgEl, wrapEl, slug);
  },

  // Mount the Build-Regions side-view canvas (C7). dotnetRef.OnHeightChanged(y) fires on drag.
  async mountSideview(canvasEl, dotnetRef, slug, axis) {
    const mod = await import("/js/studio/sideview-canvas-bridge.js");
    return mod.mount(canvasEl, dotnetRef, slug, axis);
  },

  // Mount the Configure wizard preview (E8): layer / islands / symmetry modes.
  async mountConfigure(svgEl, wrapEl, slug) {
    const mod = await import("/js/studio/configure-canvas.js");
    return mod.mount(svgEl, wrapEl, slug);
  },
};
