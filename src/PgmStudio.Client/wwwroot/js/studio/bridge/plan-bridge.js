// plan-bridge.js — JS-interop bridge for the plan editor (the seed studio). Owns the plan document and
// drives PlanCanvas; Blazor owns the toolbar / globals form / inspector chrome and persistence UI. The
// canvas reports selection + edits back here (onSelect / onCreate / onDelete / onChange); this bridge
// mutates the document, mints ids, debounces a localStorage autosave, and pushes the selection JSON to
// the Blazor inspector. Import/export round-trip the plan wire format via plan-doc.

import { PlanCanvas } from "../canvas/plan-canvas.js";
import {
  emptyDoc, normalizeDoc, fromJson, toJson, uniqueId, toggleWall, defaultReference, ROLES,
} from "../plan/plan-doc.js";
import { parseOverlays, sortFindings } from "../plan/plan-inspect.js";

const STORAGE_KEY = "pgm-plan-editor";
const OVERLAY_KEY = "pgm-plan-overlays";
const HEIGHTMAP_KEY = "pgm-plan-heightmap";
const SURFACESTEP_KEY = "pgm-plan-surface-step";

export async function mount(svgEl, wrapEl, cursorEl, dotnetRef) {
  let doc = emptyDoc();
  const fire = (name, ...args) => { try { dotnetRef?.invokeMethodAsync(name, ...args); } catch { /* host may not wire it */ } };

  const canvas = new PlanCanvas(svgEl, wrapEl, {
    cursorEl,
    onSelect: (sel) => fire("OnSelect", sel ? JSON.stringify(sel) : null),
    onZoom: (pct) => fire("OnZoom", pct),
    onTool: (t) => fire("OnTool", t),
    onChange: () => scheduleSave(),
    onCreate: (kind, rect) => createRect(kind, rect),
    onDelete: (sel) => deleteSelection(sel),
    onToggleWall: (a, b) => toggleWallMark(a, b),
  });

  // ── document mutations (canvas + inspector edits funnel here) ───────────────

  function createRect(kind, rect) {
    if (kind === "zone") {
      const id = uniqueId(doc.zones.map(z => z.id), "zone");
      doc.zones.push({ id, rect, holes: [] });
      canvas.setDoc(doc);
      canvas.select({ kind: "zone", id });
    } else {
      const role = canvasRole;
      const base = role === "wool-room" ? "wool" : role === "spawn" ? "spawn"
        : role === "buffer" ? "buffer" : role === "connector" ? "connector" : "piece";
      const id = uniqueId(doc.pieces.map(p => p.id), base);
      doc.pieces.push({ id, role, rect });
      canvas.setDoc(doc);
      canvas.select({ kind: "piece", id });
    }
    scheduleSave();
  }

  // Toggle a wall mark on a land-interface piece pair, then re-inspect immediately so the heavy bar (and the
  // "not an interface" error, if the pair stops sharing a seam) refreshes without waiting on the debounce.
  function toggleWallMark(a, b) {
    toggleWall(doc, a, b);
    canvas.setDoc(doc);
    scheduleSave();
    runInspect();
  }

  function deleteSelection(sel) {
    if (!sel) return;
    if (sel.kind === "piece") {
      const p = doc.pieces.find(x => x.id === sel.id);
      doc.pieces = doc.pieces.filter(x => x.id !== sel.id);
      if (p) {   // drop any markers that rode the removed piece + any cliff/wall marks referencing it
        doc.placements.spawns = doc.placements.spawns.filter(m => m.piece !== sel.id);
        doc.placements.wools = doc.placements.wools.filter(m => m.piece !== sel.id);
        doc.placements.iron = doc.placements.iron.filter(m => m.piece !== sel.id);
        doc.cliffs = (doc.cliffs || []).filter(c => c.a !== sel.id && c.b !== sel.id);
        doc.walls = (doc.walls || []).filter(w => w.a !== sel.id && w.b !== sel.id);
      }
    } else if (sel.kind === "zone") {
      doc.zones = doc.zones.filter(x => x.id !== sel.id);
    } else if (sel.kind === "marker") {
      const list = sel.markerKind === "spawn" ? doc.placements.spawns : sel.markerKind === "wool" ? doc.placements.wools : doc.placements.iron;
      list.splice(sel.index, 1);
    }
    canvas.clearSelection();
    canvas.setDoc(doc);
    scheduleSave();
  }

  // Role armed for the next drawn piece (mirrored in the canvas so its preview colour matches).
  let canvasRole = "piece";

  // ── autosave (debounced localStorage) ───────────────────────────────────────

  let saveTimer = null;
  function scheduleSave() {
    if (saveTimer) clearTimeout(saveTimer);
    saveTimer = setTimeout(() => { try { localStorage.setItem(STORAGE_KEY, toJson(doc)); } catch { /* private mode */ } }, 600);
    scheduleInspect();
  }

  // ── live inspect (debounced POST to /api/plan/inspect; stale responses ignored) ──

  let overlays = { interfaces: true, labels: false, frontline: true };
  try { overlays = parseOverlays(localStorage.getItem(OVERLAY_KEY)); } catch { /* default */ }

  // Height-map fill mode (off by default) — persisted like the overlay chips, under its own key.
  let heightMap = false;
  try { heightMap = localStorage.getItem(HEIGHTMAP_KEY) === "1"; } catch { /* default off */ }

  // Surface-stepper increment (blocks per ± click on a piece's surface; default 2 per EL1). An authoring
  // preference — persisted per browser, never written to the plan; the host reads it to size its ± buttons.
  let surfaceStep = 2;
  try { const v = parseInt(localStorage.getItem(SURFACESTEP_KEY), 10); if (v >= 1) surfaceStep = v; } catch { /* default 2 */ }

  let inspectTimer = null, inspectSeq = 0;
  function scheduleInspect() {
    if (inspectTimer) clearTimeout(inspectTimer);
    inspectTimer = setTimeout(runInspect, 300);
  }
  async function runInspect() {
    const seq = ++inspectSeq;
    let res;
    try {
      res = await fetch("/api/plan/inspect", { method: "POST", headers: { "Content-Type": "application/json" }, body: toJson(doc) });
    } catch { return; }                       // offline / transient — keep the last good overlay
    if (seq !== inspectSeq) return;            // a newer edit already fired
    if (!res.ok) {                             // malformed plan (400) — clear the derived layer + panel
      canvas.setInspect({ interfaces: [], gapLinks: [], frontline: [] });
      fire("OnFindings", "[]");
      return;
    }
    let data;
    try { data = await res.json(); } catch { return; }
    if (seq !== inspectSeq) return;            // re-check after the awaited body
    canvas.setInspect({ interfaces: data.interfaces || [], gapLinks: data.gapLinks || [], frontline: data.frontline || [] });
    fire("OnFindings", JSON.stringify(sortFindings(data.findings || [])));
  }

  // ── reference (tracing) backdrop ─────────────────────────────────────────────
  // The plan may carry a `reference` block (the real map it was traced over + placement); it round-trips in the
  // file but the compiler ignores it. Fetch the map's top-down render and paint it behind the grid.

  const metaJson = () => JSON.stringify({ name: doc.meta.name, globals: doc.globals, reference: doc.reference || null });

  async function fetchSurface(slug) {
    let res;
    try { res = await fetch(`/api/map/${encodeURIComponent(slug)}/layers/top-surface`); } catch { return null; }
    if (!res.ok) return null;
    try { return await res.json(); } catch { return null; }
  }

  // Paint whatever `doc.reference` currently names (or clear the backdrop when there is none). Keeps the
  // reference block even if the render can't be fetched (offline / unscanned map) so provenance survives.
  async function paintReference() {
    const ref = doc.reference;
    if (!ref?.map) { canvas.setReference(null, null); return false; }
    const data = await fetchSurface(ref.map);
    canvas.setReference(data, { offset: ref.offset, scale: ref.scale, opacity: ref.opacity });
    return !!data;
  }

  function load(next, { fit = true } = {}) {
    doc = normalizeDoc(next);
    canvas.clearSelection();
    canvas.setDoc(doc);
    if (fit) canvas.fit();
    fire("OnMeta", metaJson());
    scheduleSave();
    paintReference().then(painted => { if (fit && painted) canvas.fit(); });
  }

  function persistOverlays() { try { localStorage.setItem(OVERLAY_KEY, JSON.stringify(overlays)); } catch { /* private mode */ } }

  // Restore the last autosaved plan on open; fall back to a blank document.
  try { const saved = localStorage.getItem(STORAGE_KEY); if (saved) doc = fromJson(saved); } catch { doc = emptyDoc(); }
  for (const k of Object.keys(overlays)) canvas.setOverlayVisible(k, overlays[k]);
  canvas.setHeightMap(heightMap);
  canvas.setDoc(doc);
  canvas.fit();
  canvas.resize();
  paintReference().then(painted => { if (painted) canvas.fit(); });
  scheduleInspect();

  return {
    setTool(tool) { canvas.setTool(tool); },
    setRole(role) { canvasRole = ROLES.includes(role) ? role : "piece"; canvas.setPieceRole(canvasRole); },
    fit() { canvas.fit(); },
    resize() { canvas.resize(); },

    newDoc() { load(emptyDoc()); },
    importJson(text) { try { load(fromJson(text)); return null; } catch (e) { return e?.message || "Invalid plan JSON"; } },
    exportJson() { return toJson(doc); },
    getMeta() { return metaJson(); },

    // Reference (tracing) backdrop: pick a real map to trace over, nudge its placement, or clear it.
    async setReferenceMap(slug) {
      if (!slug) { delete doc.reference; canvas.setReference(null, null); fire("OnMeta", metaJson()); scheduleSave(); return null; }
      const data = await fetchSurface(slug);
      if (!data) return "That map has no cached surface render.";
      doc.reference = defaultReference(slug);
      canvas.setReference(data, { offset: doc.reference.offset, scale: doc.reference.scale, opacity: doc.reference.opacity });
      canvas.fit();
      fire("OnMeta", metaJson());
      scheduleSave();
      return null;
    },
    setReferenceParam(key, value) {
      const ref = doc.reference; if (!ref) return;
      if (key === "opacity") ref.opacity = Math.max(0, Math.min(1, Number(value)));
      else if (key === "scale") { const s = Number(value); if (s > 0) ref.scale = s; }
      else if (key === "offsetX") ref.offset[0] = Number(value) || 0;
      else if (key === "offsetZ") ref.offset[1] = Number(value) || 0;
      else return;
      canvas.updateReference({ offset: ref.offset, scale: ref.scale, opacity: ref.opacity });
      scheduleSave();
    },
    recenterReference() {
      const ref = doc.reference; if (!ref) return;
      ref.offset = [0, 0]; ref.scale = 1;
      canvas.updateReference({ offset: ref.offset, scale: ref.scale, opacity: ref.opacity });
      canvas.fit();
      fire("OnMeta", metaJson());
      scheduleSave();
    },
    clearReference() { delete doc.reference; canvas.setReference(null, null); fire("OnMeta", metaJson()); scheduleSave(); },

    setName(name) { doc.meta.name = name || "Untitled plan"; scheduleSave(); },
    setGlobal(key, value) {
      const g = doc.globals;
      if (key === "symmetry") g.symmetry = value;
      else g[key] = Number(value);
      canvas.setDoc(doc);
      if (key === "cell") canvas.fit();
      scheduleSave();
    },

    // Inspector edits on the current selection.
    setPieceId(oldId, newId) {
      const p = doc.pieces.find(x => x.id === oldId); if (!p || !newId || newId === oldId) return;
      const id = uniqueId(doc.pieces.filter(x => x !== p).map(x => x.id), newId);
      for (const m of [...doc.placements.spawns, ...doc.placements.wools, ...doc.placements.iron]) if (m.piece === oldId) m.piece = id;
      for (const c of [...(doc.cliffs || []), ...(doc.walls || [])]) { if (c.a === oldId) c.a = id; if (c.b === oldId) c.b = id; }
      p.id = id;
      canvas.setDoc(doc); canvas.select({ kind: "piece", id }); scheduleSave();
    },
    setPieceRole(id, role) { const p = doc.pieces.find(x => x.id === id); if (!p) return; p.role = role; canvas.setDoc(doc); canvas.select({ kind: "piece", id }); scheduleSave(); },
    stepPieceSurface(id, delta) {
      const p = doc.pieces.find(x => x.id === id); if (!p) return;
      const next = (p.surface ?? doc.globals.surface) + delta;
      if (next === doc.globals.surface) delete p.surface; else p.surface = next;
      canvas.setDoc(doc); canvas.select({ kind: "piece", id }); scheduleSave();
    },
    togglePieceMirrors(id) { const p = doc.pieces.find(x => x.id === id); if (!p) return; if (p.mirrors === false) delete p.mirrors; else p.mirrors = false; canvas.setDoc(doc); canvas.select({ kind: "piece", id }); scheduleSave(); },
    setZoneId(oldId, newId) {
      const z = doc.zones.find(x => x.id === oldId); if (!z || !newId || newId === oldId) return;
      z.id = uniqueId(doc.zones.filter(x => x !== z).map(x => x.id), newId);
      canvas.setDoc(doc); canvas.select({ kind: "zone", id: z.id }); scheduleSave();
    },
    cycleFacing(index) { const m = doc.placements.spawns[index]; if (!m) return; const o = ["front", "right", "back", "left"]; m.facing = o[(o.indexOf(m.facing) + 1) % 4]; canvas.setDoc(doc); canvas.select({ kind: "marker", markerKind: "spawn", index }); scheduleSave(); },
    deleteSelected() { deleteSelection(canvas.getSelection()); },

    // Derived-structure overlays: toggle a layer (persisted) and pulse a finding's subjects on click.
    getOverlays() { return JSON.stringify(overlays); },
    setOverlay(key, on) { if (!(key in overlays)) return; overlays[key] = !!on; persistOverlays(); canvas.setOverlayVisible(key, overlays[key]); },
    getHeightMap() { return heightMap; },
    setHeightMap(on) { heightMap = !!on; try { localStorage.setItem(HEIGHTMAP_KEY, heightMap ? "1" : "0"); } catch { /* private mode */ } canvas.setHeightMap(heightMap); },
    getSurfaceStep() { return surfaceStep; },
    setSurfaceStep(v) { surfaceStep = Math.max(1, Math.round(Number(v) || 2)); try { localStorage.setItem(SURFACESTEP_KEY, String(surfaceStep)); } catch { /* private mode */ } return surfaceStep; },
    highlightSubjects(idsJson) { try { canvas.pulseSubjects(JSON.parse(idsJson) || []); } catch { /* ignore */ } },

    dispose() { if (saveTimer) clearTimeout(saveTimer); if (inspectTimer) clearTimeout(inspectTimer); inspectSeq++; canvas.dispose(); },
  };
}
