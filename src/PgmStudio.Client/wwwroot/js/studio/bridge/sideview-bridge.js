// sideview-bridge.js — JS-interop bridge for the Build-Regions side-view canvas (C7).
// Drives the reused SideviewCanvas: fetch the /segments depth map for an axis and paint it; a
// draggable max-build-height line calls back into C# (dotnetRef.OnHeightChanged). Returns a handle
// Blazor calls (setBuildHeight / loadAxis / resize / dispose).
import { SideviewCanvas } from "../canvas/sideview-canvas.js";

export async function mount(canvasEl, dotnetRef, slug, axis) {
  const enc = encodeURIComponent(slug);
  const canvas = new SideviewCanvas(canvasEl, {
    onHeightChange: (y) => dotnetRef.invokeMethodAsync("OnHeightChanged", y),
  });

  async function load(ax) {
    try {
      const r = await fetch(`/api/map/${enc}/segments?axis=${encodeURIComponent(ax)}`, { cache: "no-store" });
      canvas.setData(r.ok ? await r.json() : null);
    } catch { canvas.setData(null); }
  }

  await load(axis ?? "z");
  // Size the canvas bitmap to its laid-out box so pointer coords map 1:1 to the render (otherwise
  // the default 300×150 bitmap is CSS-stretched and the drag-line hit-test is off).
  canvas.resize();

  return {
    setBuildHeight(y) { canvas.setBuildHeight(y); },
    async loadAxis(ax) { await load(ax); },
    resize() { canvas.resize(); },
    dispose() { /* dropping the reference is enough */ },
  };
}

/**
 * Mount a *localised* side-view slice for the region inspector. Fetches the windowed /segments depth
 * map (a point's column + neighbours, or a rectangle's footprint) and paints it in a mini canvas. For
 * point/block regions a draggable Y line (markerY) calls back into C# (dotnetRef.OnSliceY); for
 * rectangles markerY is null → display only. Returns a handle Blazor drives (update / resize / dispose).
 */
export async function mountSlice(canvasEl, dotnetRef, slug) {
  const enc = encodeURIComponent(slug);
  // The line fires onHeightChange on every drag move; debounce so we persist (a server PATCH) once the
  // drag settles rather than per-pixel (avoids spamming the API and racing reloads). The canvas already
  // updates the line visually live.
  let commitTimer = null;
  const canvas = new SideviewCanvas(canvasEl, {
    onHeightChange: (y) => {
      clearTimeout(commitTimer);
      commitTimer = setTimeout(() => dotnetRef?.invokeMethodAsync("OnSliceY", y), 150);
    },
  });
  let cur = { axis: "z", xmin: null, xmax: null, zmin: null, zmax: null, markerY: null, markerP: null, markerMy: null };

  async function load(opts) {
    cur = { ...cur, ...opts };
    const qs = new URLSearchParams({ axis: cur.axis });
    for (const k of ["xmin", "xmax", "zmin", "zmax"]) if (cur[k] != null) qs.set(k, String(cur[k]));
    try {
      const r = await fetch(`/api/map/${enc}/segments?${qs}`, { cache: "no-store" });
      canvas.setData(r.ok ? await r.json() : null);
    } catch { canvas.setData(null); }
    canvas.setBuildHeight(cur.markerY);   // null = no draggable line (rectangle = display only)
    canvas.setMarker(cur.markerP != null && cur.markerMy != null ? { p: cur.markerP, y: cur.markerMy } : null);
    canvas.resize();
  }

  return {
    async update(opts) { await load(opts); },
    resize() { canvas.resize(); },
    dispose() { /* dropping the reference is enough */ },
  };
}
