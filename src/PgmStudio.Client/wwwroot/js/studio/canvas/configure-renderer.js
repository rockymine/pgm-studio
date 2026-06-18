/**
 * ConfigureRenderer — lightweight SVG renderer for the Configure wizard.
 *
 * Three display modes controlled by the host:
 *   "layer"    — block pixels only (step 1)
 *   "islands"  — block pixels + island polygon outlines (step 2)
 *   "symmetry" — island outlines + symmetry axis + center point (step 3)
 *
 * A fixed-fit preview (extends StaticSvgRenderer): no pan/zoom — the image fills
 * the viewport and is scaled via the bbox transform. Keeps the wizard feeling like a
 * read-only preview rather than an interactive editor.
 */

import { svgEl, polyToPath } from "../render/svg.js";
import { renderBlockImage } from "../render/block-render.js";
import { renderSymmetryOverlay } from "../render/symmetry-render.js";
import { StaticSvgRenderer } from "./static-renderer.js";

const ISLAND_INCLUDED_COLOR = "var(--canvas-result-fill)";   // indigo-500
const ISLAND_EXCLUDED_COLOR = "var(--canvas-island)";        // gray-500
const ISLAND_STROKE_WIDTH   = 1.5;

export class ConfigureRenderer extends StaticSvgRenderer {
  #mode      = "layer";   // "layer" | "islands" | "symmetry"

  #blockData    = null;
  #islandsData  = null;
  #excludedIds  = new Set();
  #symmetryData = null;

  // SVG layer groups
  #blockLayerEl    = null;
  #islandLayerEl   = null;
  #symmetryLayerEl = null;

  /** @param {"layer"|"islands"|"symmetry"} mode */
  setMode(mode) {
    this.#mode = mode;
    this._build();
  }

  /** @param {{min_x,min_z,max_x,max_z}} bbox */
  setBounds(bbox) {
    this._bbox = bbox;
    this._build();
  }

  /** Block pixel data from /api/map/<name>/layers/top-surface */
  loadBlockLayer(data) {
    this.#blockData = data;
    if (!this._bbox) {
      this._bbox = {
        min_x: data.min_x, min_z: data.min_z,
        max_x: data.max_x, max_z: data.max_z,
      };
    }
    this._build();
  }

  /** Islands array from /api/map/<name>/islands */
  loadIslands(islands) {
    this.#islandsData = islands;
    this._build();
  }

  /** Set which island IDs are excluded (shown dimmed) */
  setExcludedIds(ids) {
    this.#excludedIds = new Set(ids);
    this.#renderIslands();
  }

  /** Symmetry data from /api/map/<name>/symmetry */
  loadSymmetry(sym) {
    this.#symmetryData = sym;
    this.#renderSymmetry();
  }

  /** Set the symmetry primary type for axis drawing (may differ from detected) */
  setSymmetryType(type) {
    if (this.#symmetryData) {
      this.#symmetryData = { ...this.#symmetryData, _override_type: type };
    }
    this.#renderSymmetry();
  }

  /** Update center point without a full reload */
  setCenter(cx, cz) {
    if (this.#symmetryData) {
      this.#symmetryData = {
        ...this.#symmetryData,
        center: { cx, cz },
      };
    }
    this.#renderSymmetry();
  }

  // ── private ──────────────────────────────────────────────────────────────

  _build() {
    const viewport = this._resetViewport();
    if (!viewport) return;

    this.#blockLayerEl    = svgEl("g");
    this.#islandLayerEl   = svgEl("g");
    this.#symmetryLayerEl = svgEl("g");
    viewport.appendChild(this.#blockLayerEl);
    viewport.appendChild(this.#islandLayerEl);
    viewport.appendChild(this.#symmetryLayerEl);

    if (this.#mode === "layer" || this.#mode === "islands") {
      if (this.#blockData) this.#renderBlocks();
    }
    if (this.#mode === "islands" || this.#mode === "symmetry") {
      if (this.#islandsData) this.#renderIslands();
    }
    if (this.#mode === "symmetry") {
      if (this.#symmetryData) this.#renderSymmetry();
    }
  }

  #renderBlocks() {
    renderBlockImage(this.#blockLayerEl, this.#blockData, this._toSvg);
  }

  #renderIslands() {
    if (!this.#islandLayerEl || !this._toSvg || !this.#islandsData) return;
    while (this.#islandLayerEl.firstChild)
      this.#islandLayerEl.removeChild(this.#islandLayerEl.firstChild);

    for (const island of this.#islandsData) {
      const excluded = this.#excludedIds.has(island.id);
      const color    = excluded ? ISLAND_EXCLUDED_COLOR : ISLAND_INCLUDED_COLOR;
      const opacity  = excluded ? 0.4 : 0.85;
      const poly     = island.polygon;
      if (!poly?.coordinates?.length) continue;

      const d = polyToPath({ exterior: poly.coordinates[0], holes: poly.coordinates.slice(1) }, this._toSvg);
      const path = svgEl("path");
      path.setAttribute("d",             d);
      path.setAttribute("fill-rule",     "evenodd");
      path.setAttribute("fill",          color);
      path.setAttribute("fill-opacity",  excluded ? 0.1 : 0.15);
      path.setAttribute("stroke",        color);
      path.setAttribute("stroke-width",  ISLAND_STROKE_WIDTH);
      path.setAttribute("stroke-opacity", opacity);
      this.#islandLayerEl.appendChild(path);
    }
  }

  #renderSymmetry() {
    if (!this.#symmetryLayerEl || !this._toSvg) return;
    const sym  = this.#symmetryData;
    const type = sym ? (sym._override_type ?? sym.primary?.type ?? null) : null;
    renderSymmetryOverlay(this.#symmetryLayerEl, type, sym?.center?.cx, sym?.center?.cz, this._bbox, this._toSvg);
  }
}
