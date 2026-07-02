// studio.js — small JS-interop helpers for the ported studio UI.
// The reference renders Lucide icons from `<i data-lucide="...">` placeholders; we re-run the
// icon factory after Blazor renders so new/updated icons appear. Matches the reference attrs.
window.studio = {
  icons() {
    if (window.lucide && typeof window.lucide.createIcons === "function") {
      window.lucide.createIcons({ attrs: { "stroke-width": "1.5", width: "16", height: "16" } });
    }
  },

  // Smooth-scroll an in-page section into view by id. Used by the /authoring concept page's left
  // nav: plain `<a href="#id">` anchors get intercepted by Blazor's router (they resolve to the app
  // root), so the nav calls this with preventDefault instead.
  scrollToId(id) {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: "smooth", block: "start" });
  },

  // Download a string as a file (the Configure wizard's XML export). Creates a Blob + a temporary
  // anchor and clicks it, so the bytes shown in the preview are exactly what lands on disk.
  downloadText(filename, text, mime) {
    const blob = new Blob([text], { type: mime || "application/octet-stream" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },

  // Save an already-fetched response body (a .NET stream reference) as a file. The caller checks the HTTP
  // status first, so a 409/500 error body is never written to disk — only a real 2xx export (a ZIP for
  // sketch maps, or map.xml otherwise) reaches here.
  async downloadStream(filename, streamRef, mime) {
    const buffer = await streamRef.arrayBuffer();
    const blob = new Blob([buffer], { type: mime || "application/octet-stream" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },

  // Mount the hybrid editor canvas. Uses a native dynamic import (absolute URL) so it bypasses
  // Blazor's fingerprinting import map (which 404s for arbitrary wwwroot modules under the dev host).
  async mountCanvas(svgEl, wrapEl, coordsEl, zoomEl, dotnetRef, slug, category, draftStep) {
    const mod = await import("/js/studio/bridge/editor-bridge.js");
    return mod.mount(svgEl, wrapEl, coordsEl, zoomEl, dotnetRef, slug, category, draftStep);
  },

  // Mount the Overview static map render (E7).
  async mountOverview(svgEl, wrapEl, slug) {
    const mod = await import("/js/studio/bridge/overview-bridge.js");
    return mod.mount(svgEl, wrapEl, slug);
  },

  // Mount the Build-Regions side-view canvas (C7). dotnetRef.OnHeightChanged(y) fires on drag.
  async mountSideview(canvasEl, dotnetRef, slug, axis) {
    const mod = await import("/js/studio/bridge/sideview-bridge.js");
    return mod.mount(canvasEl, dotnetRef, slug, axis);
  },

  // Mount the inspector's localised side-view slice. dotnetRef.OnSliceY(y) fires when the Y line is dragged.
  async mountSliceView(canvasEl, dotnetRef, slug) {
    const mod = await import("/js/studio/bridge/sideview-bridge.js");
    return mod.mountSlice(canvasEl, dotnetRef, slug);
  },

  // Mount the new-map landing's "Found" preview (NS): reuses the editor ConfigureRenderer over the
  // cached scan artifacts (works for an xml-less world with no regions tree).
  async mountScan(svgEl, wrapEl, slug) {
    const mod = await import("/js/studio/bridge/scan-bridge.js");
    return mod.mount(svgEl, wrapEl, slug);
  },

  // Mount the Sketch tool's Layout canvas (S2): draw 2-D shapes → live island computation + mirror
  // preview. dotnetRef receives OnShapeSelected(id) / OnDirty(); the handle drives tool/operation/mode.
  async mountSketch(svgEl, wrapEl, coordsEl, zoomEl, dimEl, dotnetRef) {
    const mod = await import("/js/studio/bridge/sketch-bridge.js");
    return mod.mount(svgEl, wrapEl, coordsEl, zoomEl, dimEl, dotnetRef);
  },

  // Mount the plan editor's canvas (the seed studio): a cell grid with rect pieces/zones + markers and a
  // live symmetry mirror ghost. dotnetRef receives OnSelect / OnTool / OnZoom / OnMeta; the handle drives
  // the tools, globals, inspector edits and plan import/export.
  async mountPlan(svgEl, wrapEl, cursorEl, dotnetRef) {
    const mod = await import("/js/studio/bridge/plan-bridge.js");
    return mod.mount(svgEl, wrapEl, cursorEl, dotnetRef);
  },

  // Paint the Organic-generation demo stages (/concepts/organic) into the #gen-stage-* svgs. Stateless —
  // the page POSTs a seed to /api/sketch/generate/stages and hands the payload here on load + each re-roll.
  async renderGenStages(stages) {
    const mod = await import("/js/studio/render/gen-stages.js");
    mod.renderStages(stages);
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

// ── Theme (dark default / light) ────────────────────────────────────────────
// The initial value is set by the inline no-flash script in index.html before any CSS
// loads; the <ThemeToggle> topbar button calls toggle(). The active sun/moon icon and all
// colours are driven by `data-theme` on <html> via CSS, so no JS-side icon sync is needed.
// SVG canvases re-resolve their var(--*) fills live on attribute change; the 2D side-view
// viewport stays dark (--bg-canvas) in every theme, so no canvas redraw is required —
// we still emit `pgm:themechange` for any listener that wants it.
window.studioTheme = {
  KEY: "pgm-theme",
  get() { return document.documentElement.getAttribute("data-theme") || "dark"; },
  set(t) {
    document.documentElement.setAttribute("data-theme", t);
    try { localStorage.setItem(this.KEY, t); } catch (e) { /* private mode */ }
    window.dispatchEvent(new CustomEvent("pgm:themechange", { detail: { theme: t } }));
  },
  toggle() { this.set(this.get() === "light" ? "dark" : "light"); },
};
