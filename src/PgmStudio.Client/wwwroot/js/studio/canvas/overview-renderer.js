import { buildTransform, svgEl as makeEl } from "./transform.js";
import { blockDataToDataUrl } from "../shared/block-render.js";
import { renderSymmetryOverlay } from "../shared/symmetry-render.js";

const SYMMETRY_SKIPPED = 0.25;

export class OverviewRenderer {
  #svg;
  #wrap;
  #bbox           = null;
  #toSvg          = null;
  #blockLayerEl   = null;
  #symmetryLayerEl= null;
  #blockData      = null;
  #showBlocks     = true;
  #symmetryData   = null;
  #symmetryStatus = null;

  constructor(svgElement, wrapEl) {
    this.#svg  = svgElement;
    this.#wrap = wrapEl;
  }

  /** @param {{min_x,min_z,max_x,max_z}} bbox */
  render(bbox) {
    this.#bbox  = bbox;
    this.#toSvg = null;
    this.#build();
  }

  loadBlockLayer(data) {
    this.#blockData = data;
    if (this.#blockLayerEl && this.#toSvg) this.#renderBlocks();
  }

  setBlocksVisible(visible) {
    this.#showBlocks = visible;
    if (this.#blockLayerEl) this.#blockLayerEl.style.display = visible ? "" : "none";
  }

  setSymmetryOverlay(symmetryData, status) {
    this.#symmetryData   = symmetryData;
    this.#symmetryStatus = status;
    if (!this.#symmetryLayerEl || !this.#toSvg) return;
    this.#renderSymmetry();
  }

  resize() {
    if (!this.#bbox || !this.#wrap.clientWidth) return;
    this.#build();
  }

  // ── private ──────────────────────────────────────────────────────────────

  #build() {
    while (this.#svg.firstChild) this.#svg.removeChild(this.#svg.firstChild);
    const svgW = this.#wrap.clientWidth  || 400;
    const svgH = this.#wrap.clientHeight || 400;
    this.#svg.setAttribute("viewBox", `0 0 ${svgW} ${svgH}`);
    this.#svg.setAttribute("width",   svgW);
    this.#svg.setAttribute("height",  svgH);

    this.#toSvg = buildTransform(this.#bbox, svgW, svgH);

    const viewportG = makeEl("g", { id: "ov-viewport" });
    this.#blockLayerEl    = makeEl("g", { id: "ov-layer-blocks" });
    this.#symmetryLayerEl = makeEl("g", { id: "ov-layer-symmetry" });

    viewportG.appendChild(this.#blockLayerEl);
    viewportG.appendChild(this.#symmetryLayerEl);
    this.#svg.appendChild(viewportG);

    if (this.#blockData) this.#renderBlocks();
    this.#blockLayerEl.style.display = this.#showBlocks ? "" : "none";
    if (this.#symmetryData) this.#renderSymmetry();
  }

  #renderBlocks() {
    const { min_x, min_z, max_x, max_z } = this.#blockData;
    const p1  = this.#toSvg(min_x,     min_z);
    const p2  = this.#toSvg(max_x + 1, max_z + 1);
    const img = makeEl("image");
    img.setAttribute("href",           blockDataToDataUrl(this.#blockData));
    img.setAttribute("x",              Math.min(p1.x, p2.x));
    img.setAttribute("y",              Math.min(p1.y, p2.y));
    img.setAttribute("width",          Math.abs(p2.x - p1.x));
    img.setAttribute("height",         Math.abs(p2.y - p1.y));
    img.setAttribute("pointer-events", "none");
    img.style.imageRendering = "pixelated";
    while (this.#blockLayerEl.firstChild)
      this.#blockLayerEl.removeChild(this.#blockLayerEl.firstChild);
    this.#blockLayerEl.appendChild(img);
  }

  #renderSymmetry() {
    const { center, modes, global_symmetry } = this.#symmetryData;
    const primary = center
      ? [...(modes ?? global_symmetry ?? [])].filter(e => e.detected).sort((a, b) => b.confidence - a.confidence)[0]
      : null;
    const opacity = this.#symmetryStatus === "skipped" ? SYMMETRY_SKIPPED : 1.0;
    renderSymmetryOverlay(this.#symmetryLayerEl, primary?.type, center?.cx, center?.cz, this.#bbox, this.#toSvg,
      { lineOpacity: opacity, dotOpacity: opacity });
  }
}
