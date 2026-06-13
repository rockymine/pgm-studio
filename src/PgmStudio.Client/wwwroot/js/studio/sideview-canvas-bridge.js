// sideview-canvas-bridge.js — JS-interop bridge for the Build-Regions side-view canvas (C7).
// Drives the reused SideviewCanvas: fetch the /segments depth map for an axis and paint it; a
// draggable max-build-height line calls back into C# (dotnetRef.OnHeightChanged). Returns a handle
// Blazor calls (setBuildHeight / loadAxis / resize / dispose).
import { SideviewCanvas } from "./canvas/sideview-canvas.js";

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
