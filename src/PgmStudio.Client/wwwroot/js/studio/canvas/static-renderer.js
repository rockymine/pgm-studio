/**
 * StaticSvgRenderer — base for fixed-fit, non-interactive SVG previews
 * (ConfigureRenderer, OverviewRenderer). The counterpart to CanvasBase: where
 * CanvasBase owns pan/zoom/drag for the interactive canvases, this owns the
 * build-once-fit-to-viewport machinery the preview renderers share — svg sizing,
 * the world→SVG transform, the viewport <g>, and re-fit on container resize.
 *
 * No pan/zoom: the image fills the viewport and is scaled via the bbox transform.
 * Subclasses implement _build() — call _resetViewport() at its top to (re)build the
 * svg shell, then create their layer <g>s into the returned viewport and paint them.
 *
 * Protected (subclass-visible) state is _-prefixed: _svg, _wrap, _bbox, _toSvg.
 */

import { buildTransform } from "../geometry/transform.js";
import { svgEl } from "../render/svg.js";

export class StaticSvgRenderer {
  _svg   = null;
  _wrap  = null;
  _bbox  = null;
  _toSvg = null;

  constructor(svgEl_, wrapEl) {
    this._svg  = svgEl_;
    this._wrap = wrapEl;
  }

  /**
   * Clear + size the svg to its wrap, build _toSvg from _bbox, append a fresh
   * viewport <g>, and return it. Returns null when there's no bbox yet (the
   * subclass _build() should bail in that case).
   */
  _resetViewport() {
    while (this._svg.firstChild) this._svg.removeChild(this._svg.firstChild);
    if (!this._bbox) return null;
    const W = this._wrap.clientWidth  || 400;
    const H = this._wrap.clientHeight || 400;
    this._svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
    this._svg.setAttribute("width",   W);
    this._svg.setAttribute("height",  H);
    this._toSvg = buildTransform(this._bbox, W, H);
    const viewport = svgEl("g");
    this._svg.appendChild(viewport);
    return viewport;
  }

  /** Subclasses override: _resetViewport() then build + paint their layers. */
  _build() {}

  /** Re-fit on container size change (window resize, panel drag, layout settle). */
  resize() {
    if (this._bbox) this._build();
  }
}
