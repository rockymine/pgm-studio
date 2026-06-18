import { svgEl as makeEl } from "../render/svg.js";
import { renderBlockImage } from "../render/block-render.js";
import { renderSymmetryOverlay } from "../render/symmetry-render.js";
import { StaticSvgRenderer } from "./static-renderer.js";

const SYMMETRY_SKIPPED = 0.25;

export class OverviewRenderer extends StaticSvgRenderer {
  #blockLayerEl   = null;
  #symmetryLayerEl= null;
  #blockData      = null;
  #showBlocks     = true;
  #symmetryData   = null;
  #symmetryStatus = null;

  /** @param {{min_x,min_z,max_x,max_z}} bbox */
  render(bbox) {
    this._bbox = bbox;
    this._build();
  }

  loadBlockLayer(data) {
    this.#blockData = data;
    if (this.#blockLayerEl && this._toSvg) this.#renderBlocks();
  }

  setBlocksVisible(visible) {
    this.#showBlocks = visible;
    if (this.#blockLayerEl) this.#blockLayerEl.style.display = visible ? "" : "none";
  }

  setSymmetryOverlay(symmetryData, status) {
    this.#symmetryData   = symmetryData;
    this.#symmetryStatus = status;
    if (!this.#symmetryLayerEl || !this._toSvg) return;
    this.#renderSymmetry();
  }

  // ── private ──────────────────────────────────────────────────────────────

  _build() {
    const viewport = this._resetViewport();
    if (!viewport) return;

    this.#blockLayerEl    = makeEl("g", { id: "ov-layer-blocks" });
    this.#symmetryLayerEl = makeEl("g", { id: "ov-layer-symmetry" });
    viewport.appendChild(this.#blockLayerEl);
    viewport.appendChild(this.#symmetryLayerEl);

    if (this.#blockData) this.#renderBlocks();
    this.#blockLayerEl.style.display = this.#showBlocks ? "" : "none";
    if (this.#symmetryData) this.#renderSymmetry();
  }

  #renderBlocks() {
    renderBlockImage(this.#blockLayerEl, this.#blockData, this._toSvg);
  }

  #renderSymmetry() {
    const { center, modes, global_symmetry } = this.#symmetryData;
    const primary = center
      ? [...(modes ?? global_symmetry ?? [])].filter(e => e.detected).sort((a, b) => b.confidence - a.confidence)[0]
      : null;
    const opacity = this.#symmetryStatus === "skipped" ? SYMMETRY_SKIPPED : 1.0;
    renderSymmetryOverlay(this.#symmetryLayerEl, primary?.type, center?.cx, center?.cz, this._bbox, this._toSvg,
      { lineOpacity: opacity, dotOpacity: opacity });
  }
}
