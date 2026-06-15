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

  // Mount the inspector's localised side-view slice. dotnetRef.OnSliceY(y) fires when the Y line is dragged.
  async mountSliceView(canvasEl, dotnetRef, slug) {
    const mod = await import("/js/studio/sideview-canvas-bridge.js");
    return mod.mountSlice(canvasEl, dotnetRef, slug);
  },

  // Mount the Configure wizard preview (E8): layer / islands / symmetry modes.
  async mountConfigure(svgEl, wrapEl, slug) {
    const mod = await import("/js/studio/configure-canvas.js");
    return mod.mount(svgEl, wrapEl, slug);
  },

  // R1a: a minimal editor keyboard layer — Ctrl/Cmd+G → dotnetRef.OnGroupKey() (group/ungroup the
  // current selection). One active listener at a time (the visible activity owns it). preventDefault
  // so the browser's "find next" doesn't fire. Ignored while typing in a field. (Seed of B6's command
  // system; deliberately just this one binding, no undo stack yet.)
  _shortcutRef: null,
  _shortcutHandler: null,
  registerShortcuts(dotnetRef) {
    this.clearShortcuts();
    this._shortcutRef = dotnetRef;
    this._shortcutHandler = (e) => {
      const t = e.target;
      const tag = (t && t.tagName || "").toLowerCase();
      if (tag === "input" || tag === "textarea" || tag === "select" || (t && t.isContentEditable)) return;
      if ((e.ctrlKey || e.metaKey) && !e.shiftKey && !e.altKey && (e.key === "g" || e.key === "G")) {
        e.preventDefault();
        this._shortcutRef && this._shortcutRef.invokeMethodAsync("OnGroupKey");
      }
    };
    document.addEventListener("keydown", this._shortcutHandler);
  },
  clearShortcuts() {
    if (this._shortcutHandler) { document.removeEventListener("keydown", this._shortcutHandler); this._shortcutHandler = null; }
    this._shortcutRef = null;
  },
};
