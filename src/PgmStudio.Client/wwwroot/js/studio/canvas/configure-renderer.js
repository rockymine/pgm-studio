/**
 * ConfigureRenderer — lightweight SVG renderer for the Configure wizard.
 *
 * Three display modes controlled by the host:
 *   "layer"    — block pixels only (step 1)
 *   "islands"  — block pixels + island polygon outlines (step 2)
 *   "symmetry" — island outlines + symmetry axis + center point (step 3)
 *
 * No pan/zoom in this canvas — the image fills the viewport and is scaled
 * via SVG viewBox to fit the bounding box. Keeps the wizard feeling like a
 * read-only preview rather than an interactive editor.
 */

import { buildTransform, svgEl } from "./transform.js";
import { blockDataToDataUrl } from "../shared/block-render.js";

const ISLAND_INCLUDED_COLOR = "var(--canvas-result-fill)";   // indigo-500
const ISLAND_EXCLUDED_COLOR = "var(--canvas-island)";        // gray-500
const ISLAND_STROKE_WIDTH   = 1.5;
const SYMMETRY_COLOR        = "var(--canvas-symmetry)";      // purple-500
const CENTER_RADIUS         = 5;

export class ConfigureRenderer {
  #svg;
  #wrap;
  #bbox      = null;
  #toSvg     = null;
  #mode      = "layer";   // "layer" | "islands" | "symmetry"

  #blockData    = null;
  #islandsData  = null;
  #excludedIds  = new Set();
  #symmetryData = null;

  // SVG layer groups
  #blockLayerEl    = null;
  #islandLayerEl   = null;
  #symmetryLayerEl = null;

  constructor(svgEl_, wrapEl) {
    this.#svg  = svgEl_;
    this.#wrap = wrapEl;
  }

  /** @param {"layer"|"islands"|"symmetry"} mode */
  setMode(mode) {
    this.#mode = mode;
    this.#rebuild();
  }

  /** @param {{min_x,min_z,max_x,max_z}} bbox */
  setBounds(bbox) {
    this.#bbox = bbox;
    this.#rebuild();
  }

  /** Block pixel data from /api/map/<name>/layers/top-surface */
  loadBlockLayer(data) {
    this.#blockData = data;
    if (!this.#bbox) {
      this.#bbox = {
        min_x: data.min_x, min_z: data.min_z,
        max_x: data.max_x, max_z: data.max_z,
      };
    }
    this.#rebuild();
  }

  /** Islands array from /api/map/<name>/islands */
  loadIslands(islands) {
    this.#islandsData = islands;
    this.#rebuild();
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

  resize() {
    if (this.#bbox) this.#rebuild();
  }

  // ── private ──────────────────────────────────────────────────────────────

  #rebuild() {
    while (this.#svg.firstChild) this.#svg.removeChild(this.#svg.firstChild);
    if (!this.#bbox) return;

    const W = this.#wrap.clientWidth  || 400;
    const H = this.#wrap.clientHeight || 400;
    this.#svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
    this.#svg.setAttribute("width",   W);
    this.#svg.setAttribute("height",  H);

    this.#toSvg = buildTransform(this.#bbox, W, H);

    const viewport = svgEl("g");
    this.#blockLayerEl    = svgEl("g");
    this.#islandLayerEl   = svgEl("g");
    this.#symmetryLayerEl = svgEl("g");

    viewport.appendChild(this.#blockLayerEl);
    viewport.appendChild(this.#islandLayerEl);
    viewport.appendChild(this.#symmetryLayerEl);
    this.#svg.appendChild(viewport);

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
    const d = this.#blockData;
    const p1 = this.#toSvg(d.min_x,     d.min_z);
    const p2 = this.#toSvg(d.max_x + 1, d.max_z + 1);
    const img = svgEl("image");
    img.setAttribute("href",   blockDataToDataUrl(d));
    img.setAttribute("x",      Math.min(p1.x, p2.x));
    img.setAttribute("y",      Math.min(p1.y, p2.y));
    img.setAttribute("width",  Math.abs(p2.x - p1.x));
    img.setAttribute("height", Math.abs(p2.y - p1.y));
    img.setAttribute("pointer-events", "none");
    img.style.imageRendering = "pixelated";
    this.#blockLayerEl.appendChild(img);
  }

  #renderIslands() {
    if (!this.#islandLayerEl || !this.#toSvg || !this.#islandsData) return;
    while (this.#islandLayerEl.firstChild)
      this.#islandLayerEl.removeChild(this.#islandLayerEl.firstChild);

    for (const island of this.#islandsData) {
      const excluded = this.#excludedIds.has(island.id);
      const color    = excluded ? ISLAND_EXCLUDED_COLOR : ISLAND_INCLUDED_COLOR;
      const opacity  = excluded ? 0.4 : 0.85;
      const poly     = island.polygon;
      if (!poly?.coordinates?.length) continue;

      const d = poly.coordinates.map(ring => {
        const pts = ring.map(([x, z]) => {
          const p = this.#toSvg(x, z);
          return `${p.x},${p.y}`;
        });
        return `M ${pts.join(" L ")} Z`;
      }).join(" ");
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
    if (!this.#symmetryLayerEl || !this.#toSvg || !this.#symmetryData) return;
    while (this.#symmetryLayerEl.firstChild)
      this.#symmetryLayerEl.removeChild(this.#symmetryLayerEl.firstChild);

    const sym     = this.#symmetryData;
    const center  = sym.center;
    const bbox    = this.#bbox;
    if (!center || !bbox) return;

    const cx = center.cx;
    const cz = center.cz;

    // Determine which type to draw axis for
    const type = sym._override_type
      ?? sym.primary?.type
      ?? null;

    if (type === "mirror_x" || type === "rot_90") {
      // vertical axis at cx
      const s = this.#toSvg(cx, bbox.min_z - 10);
      const e = this.#toSvg(cx, bbox.max_z + 10);
      this.#symmetryLayerEl.appendChild(svgEl("line", {
        x1: s.x, y1: s.y, x2: e.x, y2: e.y,
        stroke: SYMMETRY_COLOR, "stroke-width": "1.5",
        "stroke-dasharray": "6 3", opacity: 0.8,
      }));
    }
    if (type === "mirror_z" || type === "rot_90" || type === "rot_180") {
      // horizontal axis at cz
      const s = this.#toSvg(bbox.min_x - 10, cz);
      const e = this.#toSvg(bbox.max_x + 10, cz);
      this.#symmetryLayerEl.appendChild(svgEl("line", {
        x1: s.x, y1: s.y, x2: e.x, y2: e.y,
        stroke: SYMMETRY_COLOR, "stroke-width": "1.5",
        "stroke-dasharray": "6 3", opacity: 0.8,
      }));
    }

    // Center point
    const pt = this.#toSvg(cx, cz);
    this.#symmetryLayerEl.appendChild(svgEl("circle", {
      cx: pt.x, cy: pt.y, r: CENTER_RADIUS,
      fill: SYMMETRY_COLOR, stroke: "var(--canvas-marker-stroke)", "stroke-width": "1.5", opacity: 0.9,
    }));
  }
}
