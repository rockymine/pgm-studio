/**
 * Stage renderers for the Organic-generation demonstration page (/concepts/organic). Each function paints
 * one stage of the real generator's pipeline into its own <svg id="gen-stage-…"> panel, from the per-stage
 * intermediates the POST /api/sketch/generate/stages endpoint emits (OrganicLane.GrowStages). Pure render —
 * no interaction. Reuses the geometry + render layers (transform / svg / boolean / symmetry / sketch-render)
 * so the demo draws the same polygons the rest of the studio does. Documented in
 * docs/contracts/organic-lane-generation.md.
 */

import { buildTransform } from "../geometry/transform.js";
import { svgEl, ringToPath, polyToPath } from "./svg.js";
import { renderSketchShape } from "./sketch-render.js";
import { computeIslands, computeMirrorPreview } from "../geometry/boolean.js";
import { applySymmetry } from "../geometry/symmetry.js";

const PANEL_H = 460;                       // svg viewBox height; width follows the board aspect

// spine stroke per role (the kinds OrganicLane tags its centerlines with)
const SPINE = {
  trunk: "var(--canvas-result-stroke)",
  lane: "var(--accent)",
  spawn: "var(--color-error)",
  "fork-primary": "var(--accent)",
  "fork-child": "var(--canvas-mirror-stroke)",
};

// ── tiny SVG helpers ────────────────────────────────────────────────────────
function prep(id, W, H) {
  const svg = document.getElementById(id);
  if (!svg) return null;
  const vw = Math.round(PANEL_H * (W / H));
  svg.setAttribute("viewBox", `0 0 ${vw} ${PANEL_H}`);
  svg.setAttribute("preserveAspectRatio", "xMidYMid meet");
  while (svg.firstChild) svg.removeChild(svg.firstChild);
  return { svg, toSvg: buildTransform({ min_x: 0, min_z: 0, max_x: W, max_z: H }, vw, PANEL_H) };
}

const scaleOf = (toSvg) => { const a = toSvg(0, 0), b = toSvg(1, 0); return Math.hypot(b.x - a.x, b.y - a.y); };

function board(svg, toSvg, W, H) {
  svg.appendChild(svgEl("path", {
    d: ringToPath([[0, 0], [W, 0], [W, H], [0, H]], toSvg),
    fill: "none", stroke: "var(--border)", "stroke-width": "1", "vector-effect": "non-scaling-stroke",
  }));
}

function seg(svg, toSvg, a, b, attrs) {
  const p = toSvg(a[0], a[1]), q = toSvg(b[0], b[1]);
  svg.appendChild(svgEl("line", { x1: p.x, y1: p.y, x2: q.x, y2: q.y, "vector-effect": "non-scaling-stroke", ...attrs }));
}

function polyline(svg, toSvg, pts, attrs) {
  const d = pts.map((p, i) => { const s = toSvg(p[0], p[1]); return `${i ? "L" : "M"}${s.x.toFixed(1)},${s.y.toFixed(1)}`; }).join(" ");
  svg.appendChild(svgEl("path", { d, fill: "none", "vector-effect": "non-scaling-stroke", ...attrs }));
}

function dot(svg, toSvg, p, r, fill, stroke) {
  const c = toSvg(p[0], p[1]);
  svg.appendChild(svgEl("circle", { cx: c.x, cy: c.y, r, fill, ...(stroke ? { stroke, "stroke-width": "1", "vector-effect": "non-scaling-stroke" } : {}) }));
}

function square(svg, toSvg, p, half, fill) {
  const c = toSvg(p[0], p[1]);
  svg.appendChild(svgEl("rect", { x: c.x - half, y: c.y - half, width: half * 2, height: half * 2, fill, stroke: "var(--bg-canvas)", "stroke-width": "1", "vector-effect": "non-scaling-stroke" }));
}

function diamond(svg, toSvg, p, half, fill) {
  const c = toSvg(p[0], p[1]);
  svg.appendChild(svgEl("path", { d: `M${c.x},${c.y - half} L${c.x + half},${c.y} L${c.x},${c.y + half} L${c.x - half},${c.y} Z`, fill, stroke: "var(--bg-canvas)", "stroke-width": "1", "vector-effect": "non-scaling-stroke" }));
}

function hubGlyph(svg, toSvg, hub) {
  const s = scaleOf(toSvg), c = toSvg(hub.x, hub.z);
  svg.appendChild(svgEl("circle", { cx: c.x, cy: c.y, r: hub.r * s, fill: "var(--text-muted)", "fill-opacity": "0.12", stroke: "var(--text-muted)", "stroke-width": "1", "vector-effect": "non-scaling-stroke" }));
  if (hub.holeR > 0)
    svg.appendChild(svgEl("circle", { cx: c.x, cy: c.y, r: hub.holeR * s, fill: "var(--bg-canvas)", stroke: "var(--text-muted)", "stroke-dasharray": "4 3", "stroke-width": "1", "vector-effect": "non-scaling-stroke" }));
  svg.appendChild(svgEl("circle", { cx: c.x, cy: c.y, r: 3, fill: "var(--text-muted)" }));
}

// ── stage 1: the value-noise field ──────────────────────────────────────────
function renderNoise(st) {
  const r = prep("gen-stage-noise", st.width, st.height); if (!r) return;
  const { svg, toSvg } = r, { x0, z0, step, cols, rows, values } = st.noise;
  for (let row = 0; row < rows; row++)
    for (let col = 0; col < cols; col++) {
      const v = values[row * cols + col];
      const a = toSvg(x0 + col * step, z0 + row * step), b = toSvg(x0 + (col + 1) * step, z0 + (row + 1) * step);
      svg.appendChild(svgEl("rect", {
        x: Math.min(a.x, b.x), y: Math.min(a.y, b.y),
        width: Math.abs(b.x - a.x) + 0.5, height: Math.abs(b.y - a.y) + 0.5,
        fill: "var(--accent)", "fill-opacity": (0.08 + 0.88 * v).toFixed(3),
      }));
    }
  board(svg, toSvg, st.width, st.height);
}

// ── stage 2: anchor sampling (hub + trunk + far-spread wool tips) ────────────
function renderAnchors(st) {
  const r = prep("gen-stage-anchors", st.width, st.height); if (!r) return;
  const { svg, toSvg } = r, hub = st.hub, m = st.margin, lw = st.laneWidth;
  board(svg, toSvg, st.width, st.height);
  // the far band the wool tips are farthest-point sampled from
  svg.appendChild(svgEl("path", {
    d: ringToPath([[m + lw, m], [st.width - m - lw, m], [st.width - m - lw, st.height * 0.24], [m + lw, st.height * 0.24]], toSvg),
    fill: "var(--accent)", "fill-opacity": "0.05", stroke: "var(--accent)", "stroke-dasharray": "5 4", "stroke-width": "1", "vector-effect": "non-scaling-stroke",
  }));
  seg(svg, toSvg, [0, st.height / 2], [st.width, st.height / 2], { stroke: "var(--canvas-axis)", "stroke-dasharray": "6 4", "stroke-width": "1", opacity: "0.6" });
  // the hub fan — a line to every branch, so the ≥MinHubAngle spacing reads
  for (const t of st.trunkTips) seg(svg, toSvg, [hub.x, hub.z], t, { stroke: "var(--canvas-result-stroke)", "stroke-width": "1", opacity: "0.45" });
  for (const t of st.woolTips) seg(svg, toSvg, [hub.x, hub.z], t, { stroke: "var(--canvas-mirror-stroke)", "stroke-width": "1", opacity: "0.45" });
  hubGlyph(svg, toSvg, hub);
  for (const t of st.trunkTips) square(svg, toSvg, t, 4, "var(--canvas-result-stroke)");
  for (const t of st.woolTips) dot(svg, toSvg, t, 4, "var(--canvas-mirror-stroke)", "var(--bg-canvas)");
}

// ── stage 3: lane spines (Catmull-Rom centerlines) ──────────────────────────
function renderSpines(st) {
  const r = prep("gen-stage-spines", st.width, st.height); if (!r) return;
  const { svg, toSvg } = r;
  board(svg, toSvg, st.width, st.height);
  hubGlyph(svg, toSvg, st.hub);
  for (const sp of st.spines) {
    const colour = SPINE[sp.kind] ?? "var(--text-muted)";
    polyline(svg, toSvg, sp.points, { stroke: colour, "stroke-width": "2", ...(sp.kind === "fork-child" ? { "stroke-dasharray": "5 4" } : {}) });
    dot(svg, toSvg, sp.points[sp.points.length - 1], 3, colour);
  }
}

// ── stage 4: variable-width ribbon hulls (+ hub plaza + holes) ──────────────
function renderRibbons(st) {
  const r = prep("gen-stage-ribbons", st.width, st.height); if (!r) return;
  const { svg, toSvg } = r;
  board(svg, toSvg, st.width, st.height);
  for (const sp of st.spines) polyline(svg, toSvg, sp.points, { stroke: "var(--text-muted)", "stroke-width": "1", opacity: "0.35" });
  for (const shape of st.shapes) svg.appendChild(renderSketchShape(shape, toSvg));
  for (const w of st.woolObjs) diamond(svg, toSvg, w, 5, "var(--accent)");
  square(svg, toSvg, st.spawn, 4, "var(--color-error)");
}

// ── stage 5: assembled island + the symmetry mirror ─────────────────────────
function renderAssembled(st) {
  const r = prep("gen-stage-assembled", st.width, st.height); if (!r) return;
  const { svg, toSvg } = r;
  board(svg, toSvg, st.width, st.height);
  seg(svg, toSvg, [0, st.cz], [st.width, st.cz], { stroke: "var(--canvas-axis)", "stroke-dasharray": "6 4", "stroke-width": "1", opacity: "0.7" });

  const { islands } = computeIslands(st.shapes);
  for (const i of islands) i.mirrors = true;                 // the team unit always fans to the opponent
  const mirror = computeMirrorPreview(islands, st.mirrorMode, st.cx, st.cz);

  for (const poly of mirror)
    svg.appendChild(svgEl("path", {
      d: polyToPath({ exterior: poly.exterior, holes: poly.holes ?? [] }, toSvg),
      fill: "var(--canvas-mirror-fill)", stroke: "var(--canvas-mirror-stroke)", "stroke-width": "1.5",
      "fill-rule": "evenodd", "fill-opacity": "0.3", "vector-effect": "non-scaling-stroke",
    }));
  for (const isl of islands) {
    if (!isl.exterior?.length) continue;
    svg.appendChild(svgEl("path", {
      d: polyToPath({ exterior: isl.exterior, holes: isl.holes ?? [] }, toSvg),
      fill: "var(--canvas-result-fill)", stroke: "var(--canvas-result-stroke)", "stroke-width": "1.5",
      "fill-rule": "evenodd", "fill-opacity": "0.28", "vector-effect": "non-scaling-stroke",
    }));
  }

  // each trunk tip is a bridge anchor spanned across the void to its mirror
  for (const t of st.trunkTips)
    seg(svg, toSvg, t, applySymmetry(t[0], t[1], st.mirrorMode, st.cx, st.cz), { stroke: "var(--canvas-result-stroke)", "stroke-dasharray": "3 4", "stroke-width": "1.5", opacity: "0.8" });
  for (const w of st.woolObjs) { diamond(svg, toSvg, w, 5, "var(--accent)"); diamond(svg, toSvg, applySymmetry(w[0], w[1], st.mirrorMode, st.cx, st.cz), 5, "var(--accent)"); }
  square(svg, toSvg, st.spawn, 4, "var(--color-error)");
  square(svg, toSvg, applySymmetry(st.spawn[0], st.spawn[1], st.mirrorMode, st.cx, st.cz), 4, "var(--color-error)");
}

/** Render all five pipeline stages from one stages payload. Called on load + every seed change. */
export function renderStages(stages) {
  if (!stages) return;
  renderNoise(stages);
  renderAnchors(stages);
  renderSpines(stages);
  renderRibbons(stages);
  renderAssembled(stages);
}
