// studio.js — small JS-interop helpers for the studio UI.
// Lucide icons come from `<i data-lucide="...">` placeholders; the factory re-runs after Blazor renders
// so new/updated icons appear.
window.studio = {
  // The width/height here are a pre-paint fallback, not the source of truth: the stylesheet sizes every
  // glyph through `svg.lucide { width: var(--icon-md) }`, and these attributes only stop an icon being
  // briefly lucide's own 24px before it lands. They must stay equal to --icon-md (tokens.css) — a
  // presentation attribute loses to any CSS rule, so a mismatch shows only as that first-paint jump.
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

  // Copy a string to the clipboard; returns true on success. Falls back to a hidden textarea +
  // execCommand where the async Clipboard API is unavailable (older / insecure contexts).
  async copyText(text) {
    try {
      if (navigator.clipboard && window.isSecureContext) { await navigator.clipboard.writeText(text); return true; }
    } catch { /* fall through to the legacy path */ }
    try {
      const ta = document.createElement("textarea");
      ta.value = text;
      ta.style.position = "fixed";
      ta.style.opacity = "0";
      document.body.appendChild(ta);
      ta.select();
      const ok = document.execCommand("copy");
      ta.remove();
      return ok;
    } catch { return false; }
  },

  // Infinite scroll: invoke dotnetRef.LoadMore() ([JSInvokable]) whenever `sentinelEl` scrolls near view.
  // Returns the IntersectionObserver as a JS reference; call .disconnect() on it to stop. The C# side guards
  // against overlapping loads, so a burst of intersections is safe.
  onScrollEnd(sentinelEl, dotnetRef) {
    if (!sentinelEl) return null;
    const obs = new IntersectionObserver(
      entries => { if (entries.some(e => e.isIntersecting)) dotnetRef.invokeMethodAsync("LoadMore"); },
      { rootMargin: "300px" });
    obs.observe(sentinelEl);
    return obs;
  },

  // Mount the hybrid world canvas. Uses a native dynamic import (absolute URL) so it bypasses
  // Blazor's fingerprinting import map (which 404s for arbitrary wwwroot modules under the dev host).
  async mountCanvas(svgEl, wrapEl, coordsEl, zoomEl, dotnetRef, slug, category, draftStep) {
    const mod = await import("/js/studio/bridge/world-bridge.js");
    return mod.mount(svgEl, wrapEl, coordsEl, zoomEl, dotnetRef, slug, category, draftStep);
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
  async mountSketch(svgEl, wrapEl, coordsEl, zoomEl, dimEl, dotnetRef, slug) {
    const mod = await import("/js/studio/bridge/sketch-bridge.js");
    return mod.mount(svgEl, wrapEl, coordsEl, zoomEl, dimEl, dotnetRef, slug);
  },

  // Mount the plan editor's canvas (the seed studio): a cell grid with rect pieces/zones + markers and a
  // live symmetry mirror ghost. dotnetRef receives OnSelect / OnTool / OnZoom / OnMeta; the handle drives
  // the tools, globals, inspector edits and plan import/export.
  async mountPlan(svgEl, wrapEl, cursorEl, dotnetRef) {
    const mod = await import("/js/studio/bridge/plan-bridge.js");
    return mod.mount(svgEl, wrapEl, cursorEl, dotnetRef);
  },

  // Mount the library editor's 3-D view of the building it has open. Answers null when WebGL cannot run, so
  // the editor can say why instead of showing an empty box. The handle draws a preview's `columns` node.
  async mountHouseIso(wrapEl) {
    const mod = await import("/js/studio/bridge/house-iso-bridge.js");
    return mod.mount(wrapEl);
  },

  // Bring an element into the scrolling column it sits in. A flat library document draws every section at
  // once, so its outline reaches a section rather than choosing which one exists.
  scrollIntoView(id) {
    document.getElementById(id)?.scrollIntoView({ block: "nearest", behavior: "smooth" });
  },

  // ── the keyboard ──────────────────────────────────────────────────────────
  // One registry owns every binding in the app (shared/keys.js); this is how Blazor reaches it. A host
  // registers a named set whose entries each call back into .NET, and drops the set by name when it
  // unmounts, so a chord can never outlive the component that answers it.
  //
  // `label` and `group` are required by the registry itself, which is what keeps the `?` sheet and the
  // Ctrl/Cmd+K palette — both rendered from it — complete without anyone maintaining a list.
  async registerKeys(ownerId, dotnetRef, entriesJson) {
    const keys = await import("/js/studio/shared/keys.js");
    const spec = typeof entriesJson === "string" ? JSON.parse(entriesJson) : entriesJson;
    keys.register(ownerId, (spec ?? []).map(entry => ({
      id: entry.id, keys: entry.keys, label: entry.label, group: entry.group,
      inField: !!entry.inField, priority: entry.priority ?? 0,
      run: () => dotnetRef.invokeMethodAsync("OnShortcut", entry.id),
    })));
  },
  async unregisterKeys(ownerId) {
    const keys = await import("/js/studio/shared/keys.js");
    keys.unregister(ownerId);
  },
  async showKeys() {
    const overlay = await import("/js/studio/shared/keys-overlay.js");
    overlay.openSheet();
  },
};

// ── Panel resize (C8) ───────────────────────────────────────────────────────
// Install the delegated `.sidebar-handle` drag-to-resize once at load. A native dynamic import (absolute
// URL) bypasses Blazor's fingerprinting import map, matching the mount* helpers above; the module installs a
// single document-level listener that serves every editor's panels.
import("/js/studio/shared/panel-resize.js").catch((e) => console.warn("[studio] panel-resize unavailable:", e?.message ?? e));

// ── The keyboard ────────────────────────────────────────────────────────────
// The registry installs its one listener on import; the overlay registers the two chords that open the
// sheet and the palette, so both are listed by the sheet they open. Every other binding is a tool's, and
// arrives when that tool mounts.
import("/js/studio/shared/keys-overlay.js")
  .then((mod) => mod.registerOverlayKeys())
  .catch((e) => console.warn("[studio] keyboard unavailable:", e?.message ?? e));

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
