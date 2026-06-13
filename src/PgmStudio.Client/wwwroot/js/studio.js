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
  async mountCanvas(svgEl, wrapEl, coordsEl, zoomEl, dotnetRef, slug, category) {
    const mod = await import("/js/studio/studio-canvas.js");
    return mod.mount(svgEl, wrapEl, coordsEl, zoomEl, dotnetRef, slug, category);
  },
};
