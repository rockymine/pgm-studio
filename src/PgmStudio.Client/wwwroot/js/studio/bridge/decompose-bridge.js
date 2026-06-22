// decompose-bridge.js — the lane-decomposition canvas. Loads a map's simplified island outlines, lets the
// user lasso a region (→ enclosed vertices + lasso∩edge markers), pick two seam points (existing nodes or
// markers), and cut the piece into a lane + remainder (iterative peeling). Roles are tagged per piece in the
// Blazor panel. Undo + getState() (a SketchLayout-format blob with a role per add shape) for the host to PUT.

import { buildTransform, buildInverseTransform } from "../geometry/transform.js";
import { svgEl, ringToPath } from "../render/svg.js";
import { renderBlockImage } from "../render/block-render.js";
import { enclosedVertices, edgeMarkers, splitPiece, centroid, pointInRing } from "../geometry/decompose-cut.js";
import { applySymmetry, orbitAxes } from "../geometry/symmetry.js";

const ROLE_COLORS = {
  spawn: "var(--color-error)", wool: "var(--accent)", frontline: "var(--canvas-result-stroke)",
  hub: "var(--text-muted)", other: "var(--canvas-add-stroke)",
  "stepping-stone": "var(--color-success)", mid: "var(--color-warning)", decorative: "var(--canvas-mirror-stroke)",
};

// island role (from /island-roles) → pre-filled whole-island category: decorative islands and neutral
// stepping-stones get tagged for free; team/objective islands are left "other" for the human to cut into lanes.
const ROLE_PREFILL = { decorative: "decorative", neutral: "stepping-stone" };

export async function mount(svgEl_, wrapEl, coordsEl, zoomEl, dotnetRef) {
  let pieces = [];          // [{id, exterior:[[x,z]], holes:[[[x,z]]], role}]
  let tool = "lasso";       // "lasso" | "pan"
  let view = null;          // {min_x,min_z,max_x,max_z}
  let lasso = null;         // [[x,z]] while dragging
  let target = null;        // piece being cut
  let markers = [];         // edge markers from the last lasso
  let seam = [];            // up to 2 picked seam points
  let laneRep = null;       // lasso centroid (the lane side)
  let selectedId = null;    // panel-selected piece
  let sym = null;           // {mode, cx, cz} of the map (for one-side dedup + getState mirror)
  let blockData = null;     // top-surface block-colour overlay payload (reference, below the pieces)
  let showBlocks = false;
  let anchors = [];         // [{kind:"spawn"|"wool", x, z}] objective markers (from /island-roles)
  let showAnchors = false;
  let buildRegion = null;   // GeoJSON {type, coordinates} of the declared build space
  let showBuild = false;
  let baseScale = 0;        // px-per-block at the first fit (zoom % baseline)
  const undo = [];          // snapshots of `pieces`
  let seq = 0;

  const fire = (n, ...a) => { try { dotnetRef?.invokeMethodAsync(n, ...a); } catch { /* host may not wire it */ } };
  const clone = (ps) => ps.map(p => ({ id: p.id, role: p.role, exterior: p.exterior.map(v => [...v]), holes: p.holes.map(h => h.map(v => [...v])) }));
  const W = () => wrapEl.clientWidth || 800, H = () => wrapEl.clientHeight || 600;
  const toSvg = () => buildTransform(view, W(), H());
  const toWorld = () => buildInverseTransform(view, W(), H());

  function fitView() {
    let mnx = 1e9, mnz = 1e9, mxx = -1e9, mxz = -1e9;
    for (const p of pieces) for (const v of p.exterior) { mnx = Math.min(mnx, v[0]); mxx = Math.max(mxx, v[0]); mnz = Math.min(mnz, v[1]); mxz = Math.max(mxz, v[1]); }
    if (mnx > mxx) { mnx = mnz = -50; mxx = mxz = 50; }
    const pad = Math.max(8, (mxx - mnx) * 0.06);
    view = { min_x: mnx - pad, min_z: mnz - pad, max_x: mxx + pad, max_z: mxz + pad };
  }

  function pushUndo() { undo.push(clone(pieces)); if (undo.length > 50) undo.shift(); }

  function zoomBy(f) {
    if (!view) return;
    const cx = (view.min_x + view.max_x) / 2, cz = (view.min_z + view.max_z) / 2;
    const hw = (view.max_x - view.min_x) / 2 / f, hh = (view.max_z - view.min_z) / 2 / f;
    view = { min_x: cx - hw, max_x: cx + hw, min_z: cz - hh, max_z: cz + hh };
    render();
  }

  // ── render ──────────────────────────────────────────────────────────────
  function clear() { while (svgEl_.firstChild) svgEl_.removeChild(svgEl_.firstChild); }
  function render() {
    if (!view) fitView();
    svgEl_.setAttribute("width", W());
    svgEl_.setAttribute("height", H());
    clear();
    const T = toSvg();
    const scale = T(1, 0).x - T(0, 0).x;
    if (!baseScale) baseScale = scale;
    if (zoomEl) zoomEl.textContent = `${Math.round((scale / baseScale) * 100)}%`;
    // reference block-colour overlay, painted first so the lane pieces draw on top of it
    if (showBlocks && blockData) {
      const bg = svgEl_.appendChild(svgEl("g", { "data-layer": "blocks", opacity: "0.6" }));
      renderBlockImage(bg, blockData, T);
    }
    // declared build space (G8c): a dashed outline + faint fill under the pieces — the contested middle
    if (showBuild && buildRegion) {
      const d = geoToPath(buildRegion, T);
      if (d) svgEl_.appendChild(svgEl("path", {
        d, "fill-rule": "evenodd", fill: "var(--color-success)", "fill-opacity": "0.07",
        stroke: "var(--color-success)", "stroke-opacity": "0.75", "stroke-dasharray": "5 4",
        "stroke-width": "1.5", "vector-effect": "non-scaling-stroke", "data-layer": "build",
      }));
    }
    const polyPath = (p) => ringToPath(p.exterior, T) + (p.holes || []).map(h => " " + ringToPath(h, T)).join("");
    for (const p of pieces) {
      const sel = p.id === selectedId || (target && p.id === target.id);
      svgEl_.appendChild(svgEl("path", {
        d: polyPath(p), "fill-rule": "evenodd", fill: ROLE_COLORS[p.role] || ROLE_COLORS.other,
        "fill-opacity": sel ? "0.42" : "0.24", stroke: ROLE_COLORS[p.role] || ROLE_COLORS.other,
        "stroke-width": sel ? "2" : "1.2", "vector-effect": "non-scaling-stroke", "data-piece": p.id,
      }));
    }
    // candidate vertices of the target piece (or all pieces before a lasso)
    const showVerts = target ? [target] : pieces;
    for (const p of showVerts)
      p.exterior.forEach((v, i) => {
        const s = T(v[0], v[1]);
        const picked = seam.some(k => k.kind === "vertex" && k.pieceId === p.id && k.index === i);
        svgEl_.appendChild(svgEl("circle", {
          cx: s.x, cy: s.y, r: picked ? 5 : 3, fill: picked ? "var(--accent)" : "var(--bg-panel)",
          stroke: "var(--text-strong)", "stroke-width": "1", "vector-effect": "non-scaling-stroke",
        }));
      });
    // lasso∩edge markers
    for (const m of markers) {
      const s = T(m.point[0], m.point[1]);
      const picked = seam.some(k => k.kind === "marker" && k.key === m.key);
      svgEl_.appendChild(svgEl("circle", { cx: s.x, cy: s.y, r: picked ? 5 : 4, fill: picked ? "var(--accent)" : "var(--canvas-mirror-stroke)", stroke: "var(--bg-canvas)", "stroke-width": "1", "vector-effect": "non-scaling-stroke" }));
    }
    // live lasso path
    if (lasso && lasso.length > 1) {
      const d = lasso.map((p, i) => { const s = T(p[0], p[1]); return `${i ? "L" : "M"}${s.x.toFixed(1)},${s.y.toFixed(1)}`; }).join(" ");
      svgEl_.appendChild(svgEl("path", { d: d + " Z", fill: "var(--accent)", "fill-opacity": "0.08", stroke: "var(--accent)", "stroke-dasharray": "4 3", "stroke-width": "1", "vector-effect": "non-scaling-stroke" }));
    }
    // seam preview
    if (seam.length === 2) {
      const a = seamPoint(seam[0]), b = seamPoint(seam[1]);
      const sa = T(a[0], a[1]), sb = T(b[0], b[1]);
      svgEl_.appendChild(svgEl("line", { x1: sa.x, y1: sa.y, x2: sb.x, y2: sb.y, stroke: "var(--accent)", "stroke-width": "2", "vector-effect": "non-scaling-stroke" }));
    }
    // objective anchors (G8b): spawn spurs (red) + wool tips (accent) as ringed target markers, drawn last
    if (showAnchors)
      for (const a of anchors) {
        const s = T(a.x, a.z);
        const color = a.kind === "spawn" ? "var(--color-error)" : "var(--accent)";
        svgEl_.appendChild(svgEl("circle", { cx: s.x, cy: s.y, r: 7, fill: "none", stroke: color, "stroke-width": "2", "vector-effect": "non-scaling-stroke" }));
        svgEl_.appendChild(svgEl("circle", { cx: s.x, cy: s.y, r: 3, fill: color, stroke: "var(--bg-canvas)", "stroke-width": "1", "vector-effect": "non-scaling-stroke" }));
      }
  }

  // GeoJSON Polygon / MultiPolygon → an SVG path string (evenodd handles holes) in the current transform.
  function geoToPath(geo, T) {
    if (!geo) return "";
    const polys = geo.type === "MultiPolygon" ? geo.coordinates : geo.type === "Polygon" ? [geo.coordinates] : [];
    return polys.map(rings => rings.map(r => ringToPath(r, T)).join(" ")).join(" ");
  }

  function seamPoint(k) {
    if (k.kind === "marker") return markers.find(m => m.key === k.key)?.point ?? k.point;
    return target.exterior[k.index];
  }

  // ── piece list → panel ──────────────────────────────────────────────────
  function pushPanel() {
    fire("OnPieces", JSON.stringify(pieces.map(p => ({ id: p.id, role: p.role, vertices: p.exterior.length, holes: p.holes.length }))));
  }

  // ── interaction ───────────────────────────────────────────────────────────
  const evWorld = (e) => { const r = svgEl_.getBoundingClientRect(); return toWorld()(e.clientX - r.left, e.clientY - r.top); };
  let down = null, moved = false, panStart = null;

  // Topmost piece whose exterior contains the point and none of whose holes do (for the select tool).
  function pieceAt(world) {
    for (let i = pieces.length - 1; i >= 0; i--) {
      const p = pieces[i];
      if (pointInRing(world.x, world.z, p.exterior) && !(p.holes || []).some(h => pointInRing(world.x, world.z, h)))
        return p;
    }
    return null;
  }

  function hitCandidate(world) {
    const T = toSvg(), s = T(world.x, world.z), R = 9;
    for (const m of markers) { const p = T(m.point[0], m.point[1]); if (Math.hypot(p.x - s.x, p.y - s.y) < R) return { kind: "marker", key: m.key, point: m.point }; }
    const ps = target ? [target] : pieces;
    for (const p of ps) for (let i = 0; i < p.exterior.length; i++) { const q = T(p.exterior[i][0], p.exterior[i][1]); if (Math.hypot(q.x - s.x, q.y - s.y) < R) return { kind: "vertex", pieceId: p.id, index: i, point: p.exterior[i] }; }
    return null;
  }

  function pickSeam(c) {
    // restrict picks to a single target piece (vertex picks lock the target)
    if (c.kind === "vertex") { if (!target) target = pieces.find(p => p.id === c.pieceId); else if (c.pieceId !== target.id) return; }
    const key = c.kind === "marker" ? "m:" + c.key : "v:" + c.pieceId + ":" + c.index;
    const exists = seam.findIndex(k => (k.kind === "marker" ? "m:" + k.key : "v:" + k.pieceId + ":" + k.index) === key);
    if (exists >= 0) seam.splice(exists, 1);            // toggle off
    else if (seam.length < 2) seam.push(c.kind === "marker" ? { kind: "marker", key: c.key, point: c.point } : { kind: "vertex", pieceId: c.pieceId, index: c.index });
    if (seam.length === 2) doCut();
    render();
  }

  function doCut() {
    if (!target || seam.length !== 2) return;
    const rep = laneRep || centroid(target.exterior);
    const a = seam[0].kind === "marker" ? markers.find(m => m.key === seam[0].key) : { kind: "vertex", index: seam[0].index };
    const b = seam[1].kind === "marker" ? markers.find(m => m.key === seam[1].key) : { kind: "vertex", index: seam[1].index };
    const res = splitPiece(target, a, b, rep);
    seam = []; markers = []; lasso = null; laneRep = null;
    if (!res) { target = null; render(); return; }
    pushUndo();
    const i = pieces.findIndex(p => p.id === target.id);
    const [lane, rem] = res;
    pieces.splice(i, 1,
      { id: `p${seq++}`, role: lane.role, exterior: lane.exterior, holes: lane.holes },
      { id: `p${seq++}`, role: rem.role, exterior: rem.exterior, holes: rem.holes });
    target = null;
    render(); pushPanel(); fire("OnDirty");
  }

  function finishLasso() {
    if (!lasso || lasso.length < 3) { lasso = null; render(); return; }
    // target = the piece with the most enclosed exterior vertices
    let best = null, bestN = 0;
    for (const p of pieces) { const n = enclosedVertices(p.exterior, lasso).length; if (n > bestN) { bestN = n; best = p; } }
    if (!best) { // none enclosed → target the piece whose interior contains the lasso centroid
      const c = centroid(lasso); best = pieces.find(p => pointInRing(c[0], c[1], p.exterior));
    }
    target = best || null;
    markers = target ? edgeMarkers(target.exterior, lasso) : [];
    laneRep = centroid(lasso);
    seam = [];
    render();
  }

  svgEl_.addEventListener("pointerdown", (e) => {
    try { svgEl_.setPointerCapture(e.pointerId); } catch { /* not a captureable pointer */ }
    down = { x: e.clientX, y: e.clientY }; moved = false;
    if (tool === "pan" || e.button === 1) { panStart = { ...view, sx: e.clientX, sy: e.clientY }; return; }
    if (tool === "select") return;   // select picks a piece on pointerup; nothing to start here
    // lasso mode: a click on a candidate picks a seam; a drag starts a lasso
    const w = evWorld(e);
    const c = hitCandidate(w);
    if (c) { down._pick = c; return; }
    lasso = [[w.x, w.z]];
  });
  svgEl_.addEventListener("pointermove", (e) => {
    if (coordsEl && view) { const w = evWorld(e); coordsEl.textContent = `${Math.round(w.x)}, ${Math.round(w.z)}`; }
    if (!down) return;
    if (Math.hypot(e.clientX - down.x, e.clientY - down.y) > 3) moved = true;
    if (panStart) {
      const T0 = buildTransform({ min_x: panStart.min_x, min_z: panStart.min_z, max_x: panStart.max_x, max_z: panStart.max_z }, W(), H());
      const scale = (T0(1, 0).x - T0(0, 0).x) || 1;
      const dx = (e.clientX - panStart.sx) / scale, dz = (e.clientY - panStart.sy) / scale;
      view = { min_x: panStart.min_x - dx, max_x: panStart.max_x - dx, min_z: panStart.min_z - dz, max_z: panStart.max_z - dz };
      render(); return;
    }
    if (lasso && !down._pick) { const w = evWorld(e); lasso.push([w.x, w.z]); render(); }
  });
  svgEl_.addEventListener("pointerup", (e) => {
    if (!down) return;
    const wasPick = down._pick, wasLasso = lasso && !wasPick;
    const didMove = moved; down = null; panStart = null;
    if (tool === "select") {   // click a piece to select it (no drag); empty space clears the selection
      if (!didMove) { const p = pieceAt(evWorld(e)); selectedId = p?.id ?? null; render(); fire("OnSelect", selectedId); }
      return;
    }
    if (wasPick && !didMove) { pickSeam(wasPick); return; }
    if (wasLasso) { if (didMove) finishLasso(); else lasso = null; render(); }
  });
  svgEl_.addEventListener("wheel", (e) => {
    e.preventDefault();
    const r = svgEl_.getBoundingClientRect(); const w = toWorld()(e.clientX - r.left, e.clientY - r.top);
    const f = e.deltaY > 0 ? 1.12 : 1 / 1.12;
    view = { min_x: w.x + (view.min_x - w.x) * f, max_x: w.x + (view.max_x - w.x) * f, min_z: w.z + (view.min_z - w.z) * f, max_z: w.z + (view.max_z - w.z) * f };
    render();
  }, { passive: false });

  // keep the canvas filled + redrawn when its container resizes
  const ro = new ResizeObserver(() => { if (view) render(); });
  ro.observe(wrapEl);

  // Keep one island per symmetry orbit so the author cuts a single team's set (not both mirrored copies).
  // Greedy over a deterministic order: keep a piece, drop any other piece whose centroid matches one of this
  // piece's symmetry images. A self-mapping piece (a centred mid island) keeps itself; an unpaired piece stays.
  function dedupBySymmetry(ps, mode, cx, cz) {
    const axes = orbitAxes(mode);
    const items = ps.map(p => ({ p, c: centroid(p.exterior) })).sort((a, b) => a.c[1] - b.c[1] || a.c[0] - b.c[0]);
    const tol = 12, removed = new Set(), kept = [];
    for (const it of items) {
      if (removed.has(it.p.id)) continue;
      kept.push(it.p);
      for (const ax of axes) {
        const [ix, iz] = applySymmetry(it.c[0], it.c[1], ax, cx, cz);
        for (const other of items)
          if (other.p.id !== it.p.id && !removed.has(other.p.id)
            && Math.hypot(other.c[0] - ix, other.c[1] - iz) < tol) removed.add(other.p.id);
      }
    }
    return kept;
  }

  // ── seed + handle ─────────────────────────────────────────────────────────
  // `roles` (optional) is the per-island gameplay role from /island-roles, index-aligned to the sketch
  // islands — used to pre-fill whole-island categories when seeding from the bare outline (no saved cuts).
  function loadLayout(state, symmetry, roles) {
    pieces = []; seq = 0; undo.length = 0; lasso = null; target = null; markers = []; seam = [];
    blockData = null; showBlocks = false;   // new map: drop the previous map's overlays; the host re-applies
    anchors = []; showAnchors = false; buildRegion = null; showBuild = false;
    selectedId = null;
    sym = symmetry && symmetry.mode && symmetry.mode !== "none" ? symmetry : null;
    const layout = state?.layout;
    if (layout?.islands?.length) {
      layout.islands.forEach((isl, idx) => {
        const shapes = (isl.shapeIds || []).map(id => layout.shapes.find(s => s.id === id)).filter(Boolean);
        const ext = shapes.find(s => s.operation !== "subtract");
        if (!ext?.vertices?.length) return;
        const prefill = roles ? ROLE_PREFILL[roles[idx]] : null;
        pieces.push({ id: `p${seq++}`, role: ext.role || prefill || "other", exterior: ext.vertices.map(v => [v[0], v[1]]),
          holes: shapes.filter(s => s.operation === "subtract" && s.vertices?.length).map(s => s.vertices.map(v => [v[0], v[1]])) });
      });
    }
    if (sym && pieces.length > 1) pieces = dedupBySymmetry(pieces, sym.mode, sym.cx, sym.cz);
    view = null; baseScale = 0; render(); pushPanel();
  }

  return {
    load(state, symmetry, roles) { loadLayout(state, symmetry, roles); },
    // Reference top-surface block overlay: the host passes the payload on enable (cached after), null to toggle
    // an already-loaded layer; visibility is independent so it persists as the host re-feeds it per map.
    setBlocks(data, visible) { if (data) blockData = data; showBlocks = !!visible && !!blockData; render(); },
    // Objective anchors (G8b) + declared build region (G8c) — both from /island-roles, fed + toggled per map.
    setAnchors(data, visible) { if (data) anchors = data; showAnchors = !!visible && anchors.length > 0; render(); },
    setBuild(data, visible) { if (data) buildRegion = data; showBuild = !!visible && !!buildRegion; render(); },
    setTool(t) { tool = (t === "pan" || t === "select") ? t : "lasso"; if (t !== "lasso") { lasso = null; } render(); },
    selectPiece(id) { selectedId = id ?? null; render(); },
    setRole(id, role) { const p = pieces.find(x => x.id === id); if (p) { p.role = role; render(); pushPanel(); fire("OnDirty"); } },
    undo() { if (undo.length) { pieces = undo.pop(); target = null; seam = []; markers = []; lasso = null; render(); pushPanel(); fire("OnDirty"); } },
    fit() { view = null; render(); },
    fitPiece(id) {
      const p = pieces.find(x => x.id === id); if (!p) return;
      let mnx = 1e9, mnz = 1e9, mxx = -1e9, mxz = -1e9;
      for (const v of p.exterior) { mnx = Math.min(mnx, v[0]); mxx = Math.max(mxx, v[0]); mnz = Math.min(mnz, v[1]); mxz = Math.max(mxz, v[1]); }
      const pad = Math.max(6, (mxx - mnx) * 0.12);
      view = { min_x: mnx - pad, min_z: mnz - pad, max_x: mxx + pad, max_z: mxz + pad };
      selectedId = id; render();
    },
    zoomIn() { zoomBy(1.25); },
    zoomOut() { zoomBy(1 / 1.25); },
    resize() { render(); },
    dispose() { try { ro.disconnect(); } catch { /* already gone */ } },
    getState() {
      const shapes = [], islands = [];
      for (const p of pieces) {
        const ids = [];
        const ex = { id: `${p.id}_ext`, type: "polygon", operation: "add", role: p.role, vertices: p.exterior };
        shapes.push(ex); ids.push(ex.id);
        p.holes.forEach((h, k) => { const hid = `${p.id}_h${k}`; shapes.push({ id: hid, type: "polygon", operation: "subtract", vertices: h }); ids.push(hid); });
        islands.push({ id: p.id, name: p.role, role: p.role, mirrors: false, shapeIds: ids });
      }
      // one-side decomposition: record the map symmetry so the full board = these lanes mirrored
      const setup = { mirror_mode: sym?.mode ?? "none", center: { cx: sym?.cx ?? 0, cz: sym?.cz ?? 0 } };
      return { setup, layout: { shapes, islands } };
    },
    pieceCount() { return pieces.length; },
  };
}
