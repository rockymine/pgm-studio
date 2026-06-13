import { buildTransform, svgEl as makeEl } from "./transform.js";
import { blockDataToDataUrl } from "../shared/block-render.js";

const SYMMETRY_COLOR   = "#a855f7";
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
    while (this.#symmetryLayerEl.firstChild)
      this.#symmetryLayerEl.removeChild(this.#symmetryLayerEl.firstChild);

    const { center, modes, global_symmetry } = this.#symmetryData;
    if (!center) return;

    const opacity = this.#symmetryStatus === "skipped" ? SYMMETRY_SKIPPED : 1.0;
    const { min_x, max_x, min_z, max_z } = this.#bbox;

    const primary = [...(modes ?? global_symmetry ?? [])]
      .filter(e => e.detected)
      .sort((a, b) => b.confidence - a.confidence)[0];

    if (primary) {
      const cx = center.cx, cz = center.cz;
      let lineStart, lineEnd;
      if (primary.type === "mirror_x") {
        lineStart = this.#toSvg(cx, min_z);
        lineEnd   = this.#toSvg(cx, max_z);
      } else if (primary.type === "mirror_z") {
        lineStart = this.#toSvg(min_x, cz);
        lineEnd   = this.#toSvg(max_x, cz);
      }
      if (lineStart && lineEnd) {
        this.#symmetryLayerEl.appendChild(makeEl("line", {
          x1: lineStart.x, y1: lineStart.y,
          x2: lineEnd.x,   y2: lineEnd.y,
          stroke: SYMMETRY_COLOR, "stroke-width": "1.5",
          "stroke-dasharray": "6 3", opacity,
        }));
      }
    }

    const pt = this.#toSvg(center.cx, center.cz);
    this.#symmetryLayerEl.appendChild(makeEl("circle", {
      cx: pt.x, cy: pt.y, r: 5,
      fill: SYMMETRY_COLOR, stroke: "#fff", "stroke-width": "1.5", opacity,
    }));
  }
}
