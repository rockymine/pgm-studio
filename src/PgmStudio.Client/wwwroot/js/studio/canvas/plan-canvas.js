/**
 * PlanCanvas — the drawing surface for the plan editor (the seed studio). Extends CanvasBase for
 * pan/zoom/drag and renders the cell grid, rect pieces (role-coloured, tinted by surface height),
 * translucent dashed zones, objective markers (spawn/wool/iron), and the dimmed non-editable symmetry
 * mirror ghost. Pointer tools draw / move / resize pieces and zones and drop markers; all snapping,
 * hit-testing and mirror math live in plan/plan-doc.js (pure). World coordinates ARE the SVG base
 * coordinates (identity transform, like SketchCanvas); `fit()` frames the content bounds.
 *
 * The document is owned by the host bridge and shared by reference — the canvas mutates it in place for
 * live drags and reports via onChange/onSelect; the bridge persists and syncs the Blazor panels.
 */

import { CanvasBase } from "./canvas-base.js";
import { svgEl } from "../render/svg.js";
import {
  ROLE_COLORS, FACING_DIR, nextFacing, rectCellsToBlocks, cellOfWorld, rectFromCells,
  markerCell, attachMarker, markerAt, allMarkers, viewBounds, pickAtWorld, sameSelection,
  pieceSurface, surfaceRange, surfaceFraction,
  pieceMirrorImages, zoneMirrorImages, markerMirrorImages, nearestInterface,
} from "../plan/plan-doc.js";

const FIT_MARGIN = 0.82;
const MARKER_COLORS = { spawn: "#e0b13c", wool: "#e6e6e6", iron: "#9aa7b4" };

// The 8 resize handles of a rect: ex/ez pick which cell edge each drags (−1 = min side, 1 = max, 0 = none);
// nx/nz are the handle's normalised position on the block-bounds box (for placing it in screen space).
const HANDLES = [
  { ex: -1, ez: -1, nx: 0, nz: 0, cur: "nwse-resize" },
  { ex: 1, ez: -1, nx: 1, nz: 0, cur: "nesw-resize" },
  { ex: 1, ez: 1, nx: 1, nz: 1, cur: "nwse-resize" },
  { ex: -1, ez: 1, nx: 0, nz: 1, cur: "nesw-resize" },
  { ex: 0, ez: -1, nx: 0.5, nz: 0, cur: "ns-resize" },
  { ex: 1, ez: 0, nx: 1, nz: 0.5, cur: "ew-resize" },
  { ex: 0, ez: 1, nx: 0.5, nz: 1, cur: "ns-resize" },
  { ex: -1, ez: 0, nx: 0, nz: 0.5, cur: "ew-resize" },
];

// Lerp a #rrggbb colour toward white by t∈[0,1] — a higher surface tints the fill lighter.
function tint(hex, t) {
  const n = parseInt(hex.slice(1), 16);
  const r = (n >> 16) & 255, g = (n >> 8) & 255, b = n & 255;
  const m = (c) => Math.round(c + (255 - c) * t);
  return `rgb(${m(r)},${m(g)},${m(b)})`;
}

// Height-map ramp: monotonically-lightening deep-blue → teal → pale-gold stops. Lowest surface → darkest,
// highest → lightest, so island heights read at a glance on the dark canvas in either theme.
const HEIGHT_STOPS = [[0, [26, 44, 74]], [0.5, [46, 128, 130]], [1, [232, 214, 128]]];
function heightColor(t) {
  const v = Math.max(0, Math.min(1, t));
  let lo = HEIGHT_STOPS[0], hi = HEIGHT_STOPS[HEIGHT_STOPS.length - 1];
  for (let i = 1; i < HEIGHT_STOPS.length; i++) { if (v <= HEIGHT_STOPS[i][0]) { lo = HEIGHT_STOPS[i - 1]; hi = HEIGHT_STOPS[i]; break; } }
  const span = hi[0] - lo[0] || 1, f = (v - lo[0]) / span;
  const m = (i) => Math.round(lo[1][i] + (hi[1][i] - lo[1][i]) * f);
  return `rgb(${m(0)},${m(1)},${m(2)})`;
}

export class PlanCanvas extends CanvasBase {
  #doc = null;
  #tool = "select";                 // select | pan | piece | zone | spawn | wool | iron | wall
  #pieceRole = "piece";             // role armed for the piece tool
  #sel = null;                      // { kind:'piece'|'zone', id } | { kind:'marker', markerKind, index }
  #drag = null;                     // { mode:'move'|'draw', ... } live pointer op
  #resize = null;                   // { handle, id, kind } while dragging a resize handle
  #cb = {};
  #cursorEl = null;

  // Derived-structure overlay (block coords from /api/plan/inspect) + which layers are visible.
  #inspect = { interfaces: [], gapLinks: [], frontline: [] };
  // Labels off by default keeps the canvas quiet: no piece/zone id text, no gap connectors or hop numbers.
  #overlayOn = { interfaces: true, labels: false, frontline: true };
  #heightMap = false;               // fill pieces by a surface-height ramp + show the height inside each
  #pulseTimer = null;

  // viewport layers (world space)
  #gridLayer; #ghostLayer; #zoneLayer; #pieceLayer; #inspectLayer; #markerLayer; #previewLayer; #centerLayer; #pulseLayer;
  // screen-space overlay (labels + selection box + resize handles)
  #overlay;

  constructor(svgEl_, wrapEl, { cursorEl, ...cb } = {}) {
    super(svgEl_, wrapEl);
    this.#cb = cb;
    this.#cursorEl = cursorEl ?? null;
    this.#build();
  }

  // ── public API ──────────────────────────────────────────────────────────────

  setDoc(doc) { this.#doc = doc; this.render(); }
  setTool(tool) {
    this.#tool = tool;
    this._activeTool = tool === "pan" ? "move" : tool;
    const draws = tool === "piece" || tool === "zone" || tool === "spawn" || tool === "wool" || tool === "iron" || tool === "wall";
    this._svg.style.cursor = draws ? "crosshair" : (tool === "select" ? "default" : "");
  }
  setPieceRole(role) { this.#pieceRole = role; }

  // Derived-structure feed (block coords, already fanned-out excluded — authored unit only). Redraw the layer.
  setInspect(data) {
    this.#inspect = { interfaces: data.interfaces || [], gapLinks: data.gapLinks || [], frontline: data.frontline || [] };
    this.#renderInspect();
    this.#refreshOverlay();
  }
  setOverlayVisible(key, on) {
    if (!(key in this.#overlayOn)) return;
    this.#overlayOn[key] = !!on;
    this.#renderInspect();
    this.#refreshOverlay();
  }
  // Height-map mode: pieces fill by a min..max surface ramp and carry their height number. Re-render the
  // pieces (fill) and the screen-space overlay (labels).
  setHeightMap(on) {
    this.#heightMap = !!on;
    if (!this.#doc) return;
    this.#renderPieces();
    this.#refreshOverlay();
  }

  getSelection() { return this.#sel; }
  select(sel) { this.#sel = sel; this.#refreshOverlay(); this.#fireSelect(); }
  clearSelection() { this.#sel = null; this.#refreshOverlay(); this.#fireSelect(); }

  fit() {
    const b = this.#doc ? viewBounds(this.#doc) : null;
    const box = b ?? { min_x: -40, min_z: -40, max_x: 40, max_z: 40 };
    const { w, h } = this.#size();
    const bw = Math.max(box.max_x - box.min_x, 1), bh = Math.max(box.max_z - box.min_z, 1);
    this._scale = Math.min(w / bw, h / bh) * FIT_MARGIN;
    this._panX = w / 2 - ((box.min_x + box.max_x) / 2) * this._scale;
    this._panY = h / 2 - ((box.min_z + box.max_z) / 2) * this._scale;
    this._applyViewportTransform();
    this._onZoom(this._scale);
  }

  resize() {
    const { w, h } = this.#size();
    this._svg.setAttribute("width", w);
    this._svg.setAttribute("height", h);
    this._svg.setAttribute("viewBox", `0 0 ${w} ${h}`);
    this.#refreshOverlay();
  }

  // ── render ────────────────────────────────────────────────────────────────────

  render() {
    if (!this.#doc) return;
    this.#renderGrid();
    this.#renderGhost();
    this.#renderZones();
    this.#renderPieces();
    this.#renderMarkers();
    this.#refreshOverlay();
    this.#cb.onChange?.();
  }

  #clear(layer) { while (layer.firstChild) layer.removeChild(layer.firstChild); }

  #renderGrid() {
    const layer = this.#gridLayer;
    this.#clear(layer);
    const cell = this.#doc.globals.cell;
    const b = viewBounds(this.#doc);
    // Cell-index extent: the view bounds (content + its symmetry ghost images) padded by 3 cells,
    // with a sensible minimum span so a blank (or tiny) plan still shows a workable grid.
    let cx0 = -8, cz0 = -8, cx1 = 8, cz1 = 8;
    if (b) {
      cx0 = Math.floor(b.min_x / cell) - 3; cz0 = Math.floor(b.min_z / cell) - 3;
      cx1 = Math.ceil(b.max_x / cell) + 3; cz1 = Math.ceil(b.max_z / cell) + 3;
    }
    // Cell grid — the sketch tool's purple chunk-grid look: one faint dashed line per cell (no heavier
    // interval; the only emphasis is the origin axes below).
    const cellLine = (x1, y1, x2, y2) => layer.appendChild(svgEl("line", {
      x1, y1, x2, y2, stroke: "var(--canvas-chunk)", "stroke-width": "1",
      "stroke-dasharray": "3 3", "vector-effect": "non-scaling-stroke",
    }));
    for (let c = cx0; c <= cx1; c++) cellLine(c * cell, cz0 * cell, c * cell, cz1 * cell);
    for (let c = cz0; c <= cz1; c++) cellLine(cx0 * cell, c * cell, cx1 * cell, c * cell);

    // Heavier gridlines along the origin axes (x=0 and z=0), drawn atop the cell grid.
    const axis = (x1, y1, x2, y2) => layer.appendChild(svgEl("line", {
      x1, y1, x2, y2, stroke: "var(--canvas-axis)", "stroke-width": "2", "vector-effect": "non-scaling-stroke",
    }));
    if (0 >= cx0 && 0 <= cx1) axis(0, cz0 * cell, 0, cz1 * cell);
    if (0 >= cz0 && 0 <= cz1) axis(cx0 * cell, 0, cx1 * cell, 0);

    // Origin marker — the sketch tool's centre crosshair + ring, in the axis colour.
    const cl = this.#centerLayer; this.#clear(cl);
    const arm = cell * 0.6, mr = cell * 0.32;
    const mark = (a) => cl.appendChild(svgEl("line", { stroke: "var(--canvas-axis)", "stroke-width": "1.5", "vector-effect": "non-scaling-stroke", ...a }));
    mark({ x1: -arm, y1: 0, x2: arm, y2: 0 });
    mark({ x1: 0, y1: -arm, x2: 0, y2: arm });
    cl.appendChild(svgEl("circle", { cx: 0, cy: 0, r: mr, fill: "none", stroke: "var(--canvas-axis)", "stroke-width": "1.5", "vector-effect": "non-scaling-stroke" }));
  }

  #renderGhost() {
    const layer = this.#ghostLayer; this.#clear(layer);
    for (const img of pieceMirrorImages(this.#doc)) {
      const { min_x, min_z, max_x, max_z } = img.bounds;
      layer.appendChild(svgEl("rect", {
        x: min_x, y: min_z, width: max_x - min_x, height: max_z - min_z,
        fill: ROLE_COLORS[img.role] || "#888", "fill-opacity": "0.08",
        stroke: ROLE_COLORS[img.role] || "#888", "stroke-opacity": "0.5", "stroke-width": "1",
        "stroke-dasharray": "5 4", "vector-effect": "non-scaling-stroke",
      }));
    }
    // Zones fan into the ghost like pieces — dimmed, dashed, non-editable — so a pinwheel's centre tiling is
    // visible while authoring. Holes ghost too (a faint cut-out over their zone image).
    for (const img of zoneMirrorImages(this.#doc)) {
      const { min_x, min_z, max_x, max_z } = img.bounds;
      layer.appendChild(svgEl("rect", {
        x: min_x, y: min_z, width: max_x - min_x, height: max_z - min_z,
        fill: "var(--accent)", "fill-opacity": "0.07", stroke: "var(--accent)", "stroke-opacity": "0.5",
        "stroke-width": "1.2", "stroke-dasharray": "7 4", "vector-effect": "non-scaling-stroke",
      }));
      for (const h of img.holes)
        layer.appendChild(svgEl("rect", {
          x: h.min_x, y: h.min_z, width: h.max_x - h.min_x, height: h.max_z - h.min_z,
          fill: "var(--bg-canvas)", "fill-opacity": "0.5", stroke: "var(--accent)", "stroke-opacity": "0.4",
          "stroke-width": "0.8", "stroke-dasharray": "3 3", "vector-effect": "non-scaling-stroke",
        }));
    }
    const cell = this.#doc.globals.cell;
    for (const m of markerMirrorImages(this.#doc))
      layer.appendChild(svgEl("circle", { cx: m.x, cy: m.z, r: cell * 0.28, fill: MARKER_COLORS[m.kind] || "#888", "fill-opacity": "0.3" }));
  }

  #renderZones() {
    const layer = this.#zoneLayer; this.#clear(layer);
    const cell = this.#doc.globals.cell;
    for (const z of this.#doc.zones) {
      const b = rectCellsToBlocks(z.rect, cell);
      layer.appendChild(svgEl("rect", {
        x: b.min_x, y: b.min_z, width: b.max_x - b.min_x, height: b.max_z - b.min_z,
        fill: "var(--accent)", "fill-opacity": "0.22", stroke: "var(--accent)", "stroke-width": "1.4",
        "stroke-dasharray": "7 4", "vector-effect": "non-scaling-stroke", "data-zone": z.id, style: "cursor:pointer",
      }));
      for (const h of z.holes) {
        const hb = rectCellsToBlocks(h, cell);
        layer.appendChild(svgEl("rect", {
          x: hb.min_x, y: hb.min_z, width: hb.max_x - hb.min_x, height: hb.max_z - hb.min_z,
          fill: "var(--bg-canvas)", "fill-opacity": "0.6", stroke: "var(--accent)", "stroke-width": "0.8",
          "stroke-dasharray": "3 3", "vector-effect": "non-scaling-stroke", "pointer-events": "none",
        }));
      }
    }
  }

  #renderPieces() {
    const layer = this.#pieceLayer; this.#clear(layer);
    const cell = this.#doc.globals.cell, base = this.#doc.globals.surface;
    const range = this.#heightMap ? surfaceRange(this.#doc) : null;   // ramp domain for height-map mode
    for (const p of this.#doc.pieces) {
      const b = rectCellsToBlocks(p.rect, cell);
      const surf = pieceSurface(this.#doc, p);
      let fill, stroke;
      if (this.#heightMap) {
        fill = heightColor(surfaceFraction(surf, range));
        stroke = fill;
      } else {
        const t = Math.max(0, Math.min(0.6, (surf - base) / 16));   // higher surface → lighter fill
        fill = tint(ROLE_COLORS[p.role] || "#888", t);
        stroke = ROLE_COLORS[p.role] || "#888";
      }
      layer.appendChild(svgEl("rect", {
        x: b.min_x, y: b.min_z, width: b.max_x - b.min_x, height: b.max_z - b.min_z,
        fill, "fill-opacity": this.#heightMap ? "0.85" : "0.7",
        stroke, "stroke-width": "1.5", "vector-effect": "non-scaling-stroke",
        "data-piece": p.id, style: "cursor:pointer",
      }));
    }
  }

  #renderMarkers() {
    const layer = this.#markerLayer; this.#clear(layer);
    const cell = this.#doc.globals.cell;
    for (const { kind, marker } of allMarkers(this.#doc)) {
      const c = markerCell(this.#doc, marker);
      if (!c) continue;
      const cx = (c[0] + 0.5) * cell, cz = (c[1] + 0.5) * cell, r = cell * 0.34;
      const col = MARKER_COLORS[kind] || "#888";
      if (kind === "spawn") {
        layer.appendChild(svgEl("circle", { cx, cy: cz, r, fill: col, "fill-opacity": "0.85", stroke: "#222", "stroke-width": "1", "vector-effect": "non-scaling-stroke", "pointer-events": "none" }));
        const [dx, dz] = FACING_DIR[marker.facing] || FACING_DIR.front;
        layer.appendChild(svgEl("line", { x1: cx, y1: cz, x2: cx + dx * r * 1.7, y2: cz + dz * r * 1.7, stroke: "#222", "stroke-width": "2", "vector-effect": "non-scaling-stroke", "pointer-events": "none" }));
      } else {
        const s = r * 1.5;
        layer.appendChild(svgEl("rect", { x: cx - s / 2, y: cz - s / 2, width: s, height: s, rx: cell * 0.08, fill: col, "fill-opacity": "0.85", stroke: "#222", "stroke-width": "1", "vector-effect": "non-scaling-stroke", "pointer-events": "none" }));
      }
    }
  }

  // Derived-structure overlay (world space, non-interactive): land interfaces (solid green — narrow seams a
  // slimmer green core, still connected) vs corner point contacts (red warning), zone gap connectors (purple
  // ruler dashes), and frontline edges (accent-tinted highlight). Drawn above pieces, below markers; the hop
  // labels ride the screen-space overlay so they stay legible.
  #renderInspect() {
    const layer = this.#inspectLayer; if (!layer) return;
    this.#clear(layer);
    if (!this.#doc) return;

    if (this.#overlayOn.frontline)
      for (const f of this.#inspect.frontline)
        layer.appendChild(svgEl("line", {
          x1: f.x1, y1: f.z1, x2: f.x2, y2: f.z2, stroke: "var(--accent)", "stroke-width": "6",
          "stroke-opacity": "0.4", "stroke-linecap": "round", "vector-effect": "non-scaling-stroke",
        }));

    if (this.#overlayOn.labels)
      for (const g of this.#inspect.gapLinks) {
        layer.appendChild(svgEl("line", {
          x1: g.x1, y1: g.z1, x2: g.x2, y2: g.z2, stroke: "var(--canvas-axis)", "stroke-width": "2.5",
          "stroke-dasharray": "4 3", "stroke-linecap": "round", "vector-effect": "non-scaling-stroke",
        }));
        for (const [px, pz] of [[g.x1, g.z1], [g.x2, g.z2]])
          layer.appendChild(svgEl("circle", { cx: px, cy: pz, r: this.#doc.globals.cell * 0.12, fill: "var(--canvas-axis)" }));
      }

    if (this.#overlayOn.interfaces)
      for (const it of this.#inspect.interfaces) {
        if (it.x1 === it.x2 && it.z1 === it.z2) {
          layer.appendChild(svgEl("circle", { cx: it.x1, cy: it.z1, r: this.#doc.globals.cell * 0.22, fill: "none", stroke: "#d9534f", "stroke-width": "2.5", "vector-effect": "non-scaling-stroke" }));
          continue;
        }
        // A land/narrow segment sits exactly on a piece seam, where the piece strokes (or a same-green
        // wool-room fill) would swallow a plain line — a dark casing under a bright core reads on any fill. A
        // wall mark renders as a heavy near-black bar; a terrain↔wool-room seam renders red (ST1); other land
        // is green. A narrow seam connects too, drawn with a slimmer core so it reads as "connected but thin".
        const seg = { x1: it.x1, y1: it.z1, x2: it.x2, y2: it.z2, "stroke-linecap": "round", "vector-effect": "non-scaling-stroke" };
        const narrow = it.kind === "narrow";
        const casing = narrow ? "5" : "7", core = narrow ? "2" : "3.5";
        if (it.wall) {
          layer.appendChild(svgEl("line", { ...seg, stroke: "#000000", "stroke-width": narrow ? "8" : "11" }));
          layer.appendChild(svgEl("line", { ...seg, stroke: "#3b3b44", "stroke-width": core }));
        } else if (it.woolRoom) {
          layer.appendChild(svgEl("line", { ...seg, stroke: "#4a1211", "stroke-width": casing }));
          layer.appendChild(svgEl("line", { ...seg, stroke: "#e5534b", "stroke-width": core }));
        } else {
          layer.appendChild(svgEl("line", { ...seg, stroke: "#123d26", "stroke-width": casing }));
          layer.appendChild(svgEl("line", { ...seg, stroke: "#4ade80", "stroke-width": core }));
        }
      }
  }

  // A transient highlight pulse on the pieces/zones a clicked lint finding implicates (self-clearing).
  pulseSubjects(ids) {
    const layer = this.#pulseLayer; if (!layer || !this.#doc) return;
    this.#clear(layer);
    const cell = this.#doc.globals.cell;
    for (const id of ids || []) {
      const item = this.#doc.pieces.find(p => p.id === id) || this.#doc.zones.find(z => z.id === id);
      if (!item) continue;
      const b = rectCellsToBlocks(item.rect, cell);
      const rect = svgEl("rect", {
        x: b.min_x, y: b.min_z, width: b.max_x - b.min_x, height: b.max_z - b.min_z, fill: "none",
        stroke: "var(--accent)", "stroke-width": "3", "vector-effect": "non-scaling-stroke", "pointer-events": "none",
      });
      const anim = document.createElementNS("http://www.w3.org/2000/svg", "animate");
      anim.setAttribute("attributeName", "opacity");
      anim.setAttribute("values", "1;0.2;1;0.2;0");
      anim.setAttribute("dur", "1.6s");
      anim.setAttribute("repeatCount", "1");
      anim.setAttribute("fill", "freeze");
      rect.appendChild(anim);
      layer.appendChild(rect);
    }
    if (this.#pulseTimer) clearTimeout(this.#pulseTimer);
    this.#pulseTimer = setTimeout(() => this.#clear(layer), 1700);
  }

  // Screen-space overlay: piece/zone id labels, the selection box, and the resize handles. Recomputed on
  // every viewport change so labels/handles stay a fixed pixel size and legible at any zoom.
  #refreshOverlay() {
    const layer = this.#overlay; if (!layer || !this.#doc) return;
    this.#clear(layer);
    const toS = (x, z) => ({ x: x * this._scale + this._panX, y: z * this._scale + this._panY });
    const cell = this.#doc.globals.cell;

    const label = (text, bx, bz, color, size = "11") => {
      const c = toS(bx, bz);
      const t = svgEl("text", {
        x: c.x, y: c.y, "text-anchor": "middle", "dominant-baseline": "middle",
        "font-size": size, "font-family": "ui-monospace, monospace", "font-weight": "600", fill: color,
        "paint-order": "stroke", stroke: "var(--bg-canvas)", "stroke-width": "3", "stroke-linejoin": "round",
        "pointer-events": "none",
      });
      t.textContent = text;
      layer.appendChild(t);
    };
    // Id labels (piece/zone) and gap hop distances follow the Labels toggle. With labels off the currently
    // selected piece/zone still shows its own id for orientation. In height-map mode the piece's surface
    // height reads big at the centre (it is data, always shown); the id rides the top edge and follows Labels.
    const showLabels = this.#overlayOn.labels;
    const selId = this.#sel && (this.#sel.kind === "piece" || this.#sel.kind === "zone") ? this.#sel.id : null;
    for (const p of this.#doc.pieces) {
      const b = rectCellsToBlocks(p.rect, cell);
      const mx = (b.min_x + b.max_x) / 2, mz = (b.min_z + b.max_z) / 2;
      const showId = showLabels || p.id === selId;
      if (this.#heightMap) {
        label(String(pieceSurface(this.#doc, p)), mx, mz, "var(--canvas-ink)", "13");
        if (showId) label(p.id, mx, b.min_z, "var(--canvas-ink)", "9");
      } else if (showId) {
        label(p.id, mx, mz, "var(--canvas-ink)");
      }
    }
    for (const z of this.#doc.zones)
      if (showLabels || z.id === selId) { const b = rectCellsToBlocks(z.rect, cell); label(z.id, (b.min_x + b.max_x) / 2, b.min_z, "var(--accent-light)"); }

    // Gap-link hop distances ride the screen-space overlay so they stay a fixed pixel size at any zoom.
    if (showLabels)
      for (const g of this.#inspect.gapLinks)
        label(String(g.hop), (g.x1 + g.x2) / 2, (g.z1 + g.z2) / 2, "var(--canvas-axis)");

    // Selection box + resize handles for a piece/zone (markers show just a ring).
    if (!this.#sel) return;
    if (this.#sel.kind === "marker") {
      const m = markerAt(this.#doc, this.#sel.markerKind, this.#sel.index);
      const c = m && markerCell(this.#doc, m);
      if (!c) return;
      const s = toS((c[0] + 0.5) * cell, (c[1] + 0.5) * cell);
      // Selected marker: a bright accent ring over a dark casing (reads on any marker colour) plus a faint halo.
      layer.appendChild(svgEl("circle", { cx: s.x, cy: s.y, r: 15, fill: "var(--accent)", "fill-opacity": "0.14", stroke: "none", "pointer-events": "none" }));
      layer.appendChild(svgEl("circle", { cx: s.x, cy: s.y, r: 13, fill: "none", stroke: "var(--bg-canvas)", "stroke-width": "4", "pointer-events": "none" }));
      layer.appendChild(svgEl("circle", { cx: s.x, cy: s.y, r: 13, fill: "none", stroke: "var(--accent)", "stroke-width": "2.5", "pointer-events": "none" }));
      return;
    }
    const item = this.#selItem();
    if (!item) return;
    const b = rectCellsToBlocks(item.rect, cell);
    const p0 = toS(b.min_x, b.min_z), p1 = toS(b.max_x, b.max_z);
    const l = Math.min(p0.x, p1.x), r = Math.max(p0.x, p1.x), t = Math.min(p0.y, p1.y), bot = Math.max(p0.y, p1.y);
    layer.appendChild(svgEl("rect", { x: l, y: t, width: r - l, height: bot - t, fill: "none", stroke: "var(--accent)", "stroke-width": "1.5", "stroke-dasharray": "5 3", "pointer-events": "none" }));
    const HALF = 4;
    for (const hd of HANDLES) {
      const hx = l + hd.nx * (r - l), hy = t + hd.nz * (bot - t);
      const el = svgEl("rect", { x: hx - HALF, y: hy - HALF, width: HALF * 2, height: HALF * 2, rx: 1, fill: "var(--bg-deep)", stroke: "var(--accent)", "stroke-width": "1.5" });
      el.style.cursor = hd.cur;
      el.addEventListener("mousedown", (e) => this.#startResize(e, hd));
      layer.appendChild(el);
    }
  }

  #selItem() {
    if (!this.#sel) return null;
    if (this.#sel.kind === "piece") return this.#doc.pieces.find(p => p.id === this.#sel.id) || null;
    if (this.#sel.kind === "zone") return this.#doc.zones.find(z => z.id === this.#sel.id) || null;
    return null;
  }

  #fireSelect() {
    if (!this.#sel) { this.#cb.onSelect?.(null); return; }
    if (this.#sel.kind === "marker") {
      const m = markerAt(this.#doc, this.#sel.markerKind, this.#sel.index);
      const c = m && markerCell(this.#doc, m);
      this.#cb.onSelect?.({ kind: "marker", markerKind: this.#sel.markerKind, index: this.#sel.index, piece: m?.piece, at: m?.at, cell: c, facing: m?.facing });
      return;
    }
    const item = this.#selItem();
    if (!item) { this.#cb.onSelect?.(null); return; }
    if (this.#sel.kind === "piece") this.#cb.onSelect?.({ kind: "piece", id: item.id, role: item.role, rect: item.rect, surface: item.surface ?? this.#doc.globals.surface, surfaceSet: item.surface != null, mirrors: item.mirrors !== false });
    else this.#cb.onSelect?.({ kind: "zone", id: item.id, rect: item.rect });
  }

  // ── CanvasBase hooks ────────────────────────────────────────────────────────

  _onViewportChanged() { this.#refreshOverlay(); }
  _onZoom(scale) { this.#cb.onZoom?.(Math.round(scale * 100)); }

  _onToolMousedown(e, svgPt) {
    const cell = this.#doc.globals.cell;
    const [cx, cz] = cellOfWorld(svgPt.x, svgPt.y, cell);
    if (this.#tool === "select") return this.#selectDown(svgPt, cx, cz);
    if (this.#tool === "wall") return this.#toggleWallAt(svgPt.x, svgPt.y);
    if (this.#tool === "piece" || this.#tool === "zone") { this.#drag = { mode: "draw", kind: this.#tool, a: [cx, cz], b: [cx, cz] }; this.#renderPreview(); return; }
    // Markers snap to the half-cell lattice — feed the fractional cell coordinate, not the floored cell.
    if (this.#tool === "spawn" || this.#tool === "wool" || this.#tool === "iron") this.#placeMarker(this.#tool, svgPt.x / cell, svgPt.y / cell);
  }

  _onPointerMove(e, svgPt) {
    const cell = this.#doc.globals.cell;
    const [cx, cz] = cellOfWorld(svgPt.x, svgPt.y, cell);
    if (this.#cursorEl) this.#cursorEl.textContent = `cell ${cx}, ${cz}`;
    if (this.#drag?.mode === "draw") { this.#drag.b = [cx, cz]; this.#renderPreview(); return; }
    if (this.#drag?.mode === "move") this.#moveTo(cx, cz, svgPt.x / cell, svgPt.y / cell);
  }

  _onToolMouseup(e, svgPt) {
    if (this.#drag?.mode === "draw") { this.#commitDraw(); return; }
    if (this.#drag?.mode === "move") {
      const { moved, reselect } = this.#drag;
      this.#drag = null;
      if (moved) { this.render(); this.#cb.onChange?.(); this.#fireSelect(); }
      else this.#clickSelect(reselect);   // a press without a drag = a plain click (select / cycle facing)
    }
  }

  _onResizeMove(e) {
    if (!this.#resize) return false;
    const p = this._clientToSvg(e.clientX, e.clientY);
    const [cx, cz] = cellOfWorld(p.x, p.y, this.#doc.globals.cell);
    this.#resizeTo(cx, cz);
    return true;
  }
  _onResizeUp(e) {
    if (!this.#resize) return false;
    if (e.button !== 0) return false;
    this.#resize = null;
    this.render(); this.#cb.onChange?.(); this.#fireSelect();
    return true;
  }

  _onMouseleave() { if (this.#cursorEl) this.#cursorEl.textContent = ""; }

  // ── interaction ───────────────────────────────────────────────────────────────

  // Press with the select tool: pick the item under the click (marker-first, by paint order) and begin a
  // move drag from it. Records whether the click re-hit the already-selected item, so a plain re-click on a
  // selected spawn cycles its facing (the first click only selects).
  #selectDown(svgPt, cx, cz) {
    const prev = this.#sel;
    const hit = pickAtWorld(this.#doc, svgPt.x, svgPt.y);
    this.#sel = hit;
    this.#refreshOverlay();
    this.#fireSelect();
    this.#drag = { mode: "move", sel: hit, grab: [cx, cz], moved: false, reselect: sameSelection(prev, hit) };
  }

  #moveTo(cx, cz, fcx, fcz) {
    const d = this.#drag; if (!d?.sel) return;
    // Markers track the cursor on the half-cell lattice (absolute, snap-aware) and re-parent to the piece
    // under it; only a real position change marks the drag moved (so a plain click still cycles facing).
    if (d.sel.kind === "marker") {
      const m = markerAt(this.#doc, d.sel.markerKind, d.sel.index);
      const at = attachMarker(this.#doc, fcx, fcz);
      if (at && (at.piece !== m.piece || at.at[0] !== m.at[0] || at.at[1] !== m.at[1])) {
        m.piece = at.piece; m.at = at.at; d.moved = true; this.render();
      }
      return;
    }
    const ddx = cx - d.grab[0], ddz = cz - d.grab[1];
    if (!ddx && !ddz) return;
    d.grab = [cx, cz];
    const item = this.#selItem();
    if (item) { item.rect[0] += ddx; item.rect[1] += ddz; d.moved = true; }
    this.render();
  }

  // A press-release with no drag. The first click on a spawn only selects it; a re-click on the
  // already-selected spawn (reselect) cycles its facing — so placing/selecting never surprises with a
  // facing change. (Selection itself already happened on mousedown.)
  #clickSelect(reselect) {
    if (reselect && this.#sel?.kind === "marker" && this.#sel.markerKind === "spawn") {
      const m = markerAt(this.#doc, "spawn", this.#sel.index);
      if (m) { m.facing = nextFacing(m.facing); this.render(); this.#cb.onChange?.(); this.#fireSelect(); }
    } else if (!this.#sel) {
      this.#refreshOverlay();
    }
  }

  #placeMarker(kind, cx, cz) {
    const at = attachMarker(this.#doc, cx, cz);
    if (!at) return;                          // markers must ride a piece
    const rec = kind === "spawn" ? { ...at, facing: "front" } : { ...at };
    const list = kind === "spawn" ? this.#doc.placements.spawns : kind === "wool" ? this.#doc.placements.wools : this.#doc.placements.iron;
    list.push(rec);
    this.#sel = { kind: "marker", markerKind: kind, index: list.length - 1 };
    this.setTool("select"); this.#cb.onTool?.("select");
    this.render(); this.#cb.onChange?.(); this.#fireSelect();
  }

  // Wall tool: toggle a wall mark on the land interface nearest the click (within one cell). The mark rides
  // the piece pair; the bridge mutates doc.walls and re-inspects so the heavy bar renders from the feed. The
  // wall tool stays armed for repeated toggling.
  #toggleWallAt(wx, wz) {
    const cell = this.#doc.globals.cell;
    const it = nearestInterface(this.#inspect.interfaces, wx, wz, cell);
    if (it) this.#cb.onToggleWall?.(it.a, it.b);
  }

  #renderPreview() {
    const layer = this.#previewLayer; this.#clear(layer);
    if (this.#drag?.mode !== "draw") return;
    const cell = this.#doc.globals.cell;
    const rect = rectFromCells(...this.#drag.a, ...this.#drag.b);
    const b = rectCellsToBlocks(rect, cell);
    const color = this.#drag.kind === "zone" ? "var(--accent)" : ROLE_COLORS[this.#pieceRole];
    layer.appendChild(svgEl("rect", {
      x: b.min_x, y: b.min_z, width: b.max_x - b.min_x, height: b.max_z - b.min_z,
      fill: color, "fill-opacity": "0.2", stroke: color, "stroke-width": "1.5",
      "stroke-dasharray": "4 3", "vector-effect": "non-scaling-stroke", "pointer-events": "none",
    }));
  }

  #commitDraw() {
    const kind = this.#drag.kind;
    const rect = rectFromCells(...this.#drag.a, ...this.#drag.b);
    this.#drag = null;
    this.#clear(this.#previewLayer);
    this.#cb.onCreate?.(kind, rect);          // the bridge mints the id, appends, and re-selects
    this.setTool("select"); this.#cb.onTool?.("select");
  }

  // Resize the selected piece/zone by dragging a handle: move the picked cell edge(s) to the cursor cell,
  // keeping each extent ≥ 1 cell.
  #startResize(e, handle) {
    if (e.button !== 0 || !this.#sel || this.#sel.kind === "marker") return;
    e.stopPropagation(); e.preventDefault();
    this.#resize = { handle, sel: this.#sel };
  }

  #resizeTo(cx, cz) {
    const item = this.#selItem(); if (!item) return;
    const h = this.#resize.handle;
    let [x, z, w, hh] = item.rect;
    let minX = x, maxX = x + w - 1, minZ = z, maxZ = z + hh - 1;   // inclusive cell edges
    if (h.ex === -1) minX = Math.min(cx, maxX);
    else if (h.ex === 1) maxX = Math.max(cx, minX);
    if (h.ez === -1) minZ = Math.min(cz, maxZ);
    else if (h.ez === 1) maxZ = Math.max(cz, minZ);
    item.rect = [minX, minZ, maxX - minX + 1, maxZ - minZ + 1];
    this.render();
  }

  // ── build ─────────────────────────────────────────────────────────────────────

  #size() { return { w: (this._wrap.clientWidth || 600) - 24, h: (this._wrap.clientHeight || 600) - 24 }; }

  #build() {
    const { w, h } = this.#size();
    this._svg.setAttribute("width", w);
    this._svg.setAttribute("height", h);
    this._svg.setAttribute("viewBox", `0 0 ${w} ${h}`);

    this._viewportG = svgEl("g");
    this.#gridLayer = svgEl("g", { "pointer-events": "none" });
    this.#centerLayer = svgEl("g", { "pointer-events": "none" });
    this.#ghostLayer = svgEl("g", { "pointer-events": "none" });
    this.#zoneLayer = svgEl("g");
    this.#pieceLayer = svgEl("g");
    this.#inspectLayer = svgEl("g", { "pointer-events": "none" });
    this.#markerLayer = svgEl("g");
    this.#previewLayer = svgEl("g", { "pointer-events": "none" });
    this.#pulseLayer = svgEl("g", { "pointer-events": "none" });
    for (const g of [this.#gridLayer, this.#centerLayer, this.#ghostLayer, this.#zoneLayer, this.#pieceLayer, this.#inspectLayer, this.#markerLayer, this.#previewLayer, this.#pulseLayer]) this._viewportG.appendChild(g);
    this._svg.appendChild(this._viewportG);

    this.#overlay = svgEl("g");
    this._svg.appendChild(this.#overlay);
    this._applyViewportTransform();

    // Delete / Backspace removes the current selection (guarded by visibility + not typing in a field).
    document.addEventListener("keydown", this.#onKey);
    this.setTool("select");
  }

  #onKey = (e) => {
    if (this._wrap?.offsetParent == null) return;
    if (["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName)) return;
    if ((e.key === "Delete" || e.key === "Backspace") && this.#sel) { e.preventDefault(); this.#cb.onDelete?.(this.#sel); }
  };

  dispose() { if (this.#pulseTimer) clearTimeout(this.#pulseTimer); document.removeEventListener("keydown", this.#onKey); }
}
