// plan-bridge.js — JS-interop bridge for the plan editor (the seed studio). Owns the plan document and
// drives PlanCanvas; Blazor owns the toolbar / globals form / inspector chrome and persistence UI. The
// canvas reports selection + edits back here (onSelect / onCreate / onDelete / onChange); this bridge
// mutates the document, mints ids, debounces a localStorage autosave, and pushes the selection JSON to
// the Blazor inspector. Import/export round-trip the plan wire format via plan-doc.

import { PlanCanvas } from "../canvas/plan-canvas.js";
import {
  emptyDoc, normalizeDoc, fromJson, toJson, uniqueId, toggleWall, defaultReference, ROLES, BOX_KINDS,
  planIsoSolids, viewBounds, markerList, MARKER_KINDS, boxMembers, defaultThemeJson,
} from "../plan/plan-doc.js";
import { parseOverlays } from "../plan/plan-inspect.js";

const STORAGE_KEY = "pgm-plan-editor";
const OVERLAY_KEY = "pgm-plan-overlays";
const HEIGHTMAP_KEY = "pgm-plan-heightmap";
const SURFACESTEP_KEY = "pgm-plan-surface-step";

export async function mount(svgEl, wrapEl, cursorEl, dotnetRef) {
  let doc = emptyDoc();
  // Fire-and-forget into the host. A handler the host doesn't wire rejects the InvokeAsync promise
  // asynchronously, which the surrounding try/catch (synchronous) can't see — so swallow it on the promise
  // too, or an unwired feed (e.g. OnThemes, whose C# side pulls via getThemes instead) becomes an unhandled
  // rejection that faults the page.
  const fire = (name, ...args) => {
    try { dotnetRef?.invokeMethodAsync(name, ...args)?.catch(() => { /* host may not wire it */ }); }
    catch { /* host may not wire it */ }
  };

  // Read-only 3-D height preview (G27): "2d" | "iso", with a user-rotatable yaw. Rebuilt from the plan
  // pieces/surfaces whenever the document changes while the preview is showing. The stamped structures
  // (G73) ride along from the inspect feed — the server derives them, so they trail an edit by one
  // debounce and refresh in place when they land.
  let view = "2d";
  let isoYaw = 30;
  let structures = [];
  function refreshIso() { if (view === "iso") canvas.showIso(planIsoSolids(doc, structures), isoYaw, viewBounds(doc)); }

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
    } else if (kind === "box") {
      // A drawn box groups by containment (no `members`) — the author sizes the envelope and the pieces
      // inside it follow. An explicit member list is what a composed partition writes, not what a hand
      // -drawn box needs.
      const id = uniqueId((doc.boxes || []).map(b => b.id), `${canvasBoxKind}-box`);
      (doc.boxes ||= []).push({ id, kind: canvasBoxKind, rect });
      canvas.setDoc(doc);
      canvas.select({ kind: "box", id });
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

  // Toggle a wall mark on a land-interface piece pair, then re-run the live feeds immediately so the heavy bar
  // (and the "not an interface" error, if the pair stops sharing a seam) refreshes without waiting on the debounce.
  function toggleWallMark(a, b) {
    toggleWall(doc, a, b);
    canvas.setDoc(doc);
    scheduleSave();
    runLive();
  }

  function deleteSelection(sel) {
    if (!sel) return;
    if (sel.kind === "piece") {
      const p = doc.pieces.find(x => x.id === sel.id);
      doc.pieces = doc.pieces.filter(x => x.id !== sel.id);
      if (p) {   // drop any markers that rode the removed piece + any cliff/wall marks referencing it
        for (const kind of MARKER_KINDS) {
          const list = markerList(doc, kind);
          if (list) markerList(doc, kind).splice(0, list.length, ...list.filter(m => m.piece !== sel.id));
        }
        doc.cliffs = (doc.cliffs || []).filter(c => c.a !== sel.id && c.b !== sel.id);
        doc.walls = (doc.walls || []).filter(w => w.a !== sel.id && w.b !== sel.id);
        // A box that named the removed piece keeps its remaining members; a box left naming nothing falls
        // back to containment rather than becoming an empty group.
        for (const b of doc.boxes || []) {
          if (!Array.isArray(b.members)) continue;
          b.members = b.members.filter(m => m !== sel.id);
          if (!b.members.length) delete b.members;
        }
      }
    } else if (sel.kind === "zone") {
      doc.zones = doc.zones.filter(x => x.id !== sel.id);
    } else if (sel.kind === "box") {
      // Deleting a box removes the annotation only — the pieces it grouped stay exactly where they are.
      doc.boxes = (doc.boxes || []).filter(x => x.id !== sel.id);
    } else if (sel.kind === "marker") {
      markerList(doc, sel.markerKind)?.splice(sel.index, 1);
    }
    canvas.clearSelection();
    canvas.setDoc(doc);
    scheduleSave();
  }

  // Role armed for the next drawn piece, and kind armed for the next drawn box (both mirrored in the canvas
  // so the draw preview's colour matches what will be created).
  let canvasRole = "piece";
  let canvasBoxKind = "hub";

  // ── autosave (debounced localStorage) ───────────────────────────────────────

  let saveTimer = null;
  function scheduleSave() {
    if (saveTimer) clearTimeout(saveTimer);
    saveTimer = setTimeout(() => { try { localStorage.setItem(STORAGE_KEY, toJson(doc)); } catch { /* private mode */ } }, 600);
    scheduleInspect();
    refreshIso();   // keep the read-only 3-D preview current with inspector-driven surface/geometry edits
  }

  // ── live inspect + evaluate (debounced POSTs; stale responses ignored) ──

  let overlays = { interfaces: true, labels: false, frontline: true, violations: true };
  try { overlays = parseOverlays(localStorage.getItem(OVERLAY_KEY)); } catch { /* default */ }

  // Height-map fill mode (off by default) — persisted like the overlay chips, under its own key.
  let heightMap = false;
  try { heightMap = localStorage.getItem(HEIGHTMAP_KEY) === "1"; } catch { /* default off */ }

  // Surface-stepper increment (blocks per ± click on a piece's surface; default 2 per EL1). An authoring
  // preference — persisted per browser, never written to the plan; the host reads it to size its ± buttons.
  let surfaceStep = 2;
  try { const v = parseInt(localStorage.getItem(SURFACESTEP_KEY), 10); if (v >= 1) surfaceStep = v; } catch { /* default 2 */ }

  let inspectTimer = null, inspectSeq = 0, evalSeq = 0, feasSeq = 0;
  function scheduleInspect() {
    if (inspectTimer) clearTimeout(inspectTimer);
    inspectTimer = setTimeout(runLive, 300);
  }
  // One edit fires three live feeds: the structural derivation (interfaces/frontline/lint), the rule evaluator
  // (score + fired-rule evidence), and the producibility read (could the composer have made this?). They are
  // independent endpoints with their own stale-response guards.
  function runLive() { runInspect(); runEvaluate(); runFeasibility(); }

  async function runInspect() {
    const seq = ++inspectSeq;
    let res;
    try {
      res = await fetch("/api/plan/inspect", { method: "POST", headers: { "Content-Type": "application/json" }, body: toJson(doc) });
    } catch { return; }                       // offline / transient — keep the last good overlay
    if (seq !== inspectSeq) return;            // a newer edit already fired
    if (!res.ok) {                             // malformed plan (400) — clear the derived overlay
      canvas.setInspect({ interfaces: [], gapLinks: [], frontline: [] });
      structures = [];
      refreshIso();
      return;
    }
    let data;
    try { data = await res.json(); } catch { return; }
    if (seq !== inspectSeq) return;            // re-check after the awaited body
    canvas.setInspect({ interfaces: data.interfaces || [], gapLinks: data.gapLinks || [], frontline: data.frontline || [] });
    structures = data.structures || [];
    refreshIso();
  }

  // The evaluator feed: the plan's score + every fired rule (hard-first) with cell-space evidence. The evidence
  // goes to the canvas overlay; the whole EvaluationDto goes to the Blazor score/violations panel. A 400
  // (malformed) clears both — an empty string signals "no evaluation" to the host.
  async function runEvaluate() {
    const seq = ++evalSeq;
    let res;
    try {
      res = await fetch("/api/plan/evaluate", { method: "POST", headers: { "Content-Type": "application/json" }, body: toJson(doc) });
    } catch { return; }                       // offline / transient — keep the last good evidence
    if (seq !== evalSeq) return;               // a newer edit already fired
    if (!res.ok) {                             // malformed plan (400) — clear the evidence + score panel
      canvas.setViolations([]);
      fire("OnEvaluation", "");
      return;
    }
    let data;
    try { data = await res.json(); } catch { return; }
    if (seq !== evalSeq) return;               // re-check after the awaited body
    canvas.setViolations(data.violations || []);
    fire("OnEvaluation", JSON.stringify(data));
  }

  // The producibility feed: per-box "could the composer have produced this?" plus the unit-level findings. The
  // whole FeasibilityDto goes to the Blazor panel; the canvas evidence follows the panel's own selection (the
  // author picks a box), so nothing is painted from this response directly. A 400 clears the panel.
  async function runFeasibility() {
    const seq = ++feasSeq;
    let res;
    try {
      res = await fetch("/api/plan/feasibility", { method: "POST", headers: { "Content-Type": "application/json" }, body: toJson(doc) });
    } catch { return; }                       // offline / transient — keep the last good read
    if (seq !== feasSeq) return;               // a newer edit already fired
    if (!res.ok) { canvas.setNearestMiss(null); fire("OnFeasibility", ""); return; }
    let data;
    try { data = await res.json(); } catch { return; }
    if (seq !== feasSeq) return;               // re-check after the awaited body
    // a fresh read invalidates whichever box's evidence was painted — the panel re-selects if it wants it back
    canvas.setNearestMiss(null);
    fire("OnFeasibility", JSON.stringify(data));
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
    fire("OnThemes", themesState());
    scheduleSave();
    paintReference().then(painted => { if (fit && painted) canvas.fit(); });
  }

  function persistOverlays() { try { localStorage.setItem(OVERLAY_KEY, JSON.stringify(overlays)); } catch { /* private mode */ } }

  // ── terrain-paint themes (TP10) ────────────────────────────────────────────
  // The theme state the Theme rail reads: the registry, the map default, the generating pieces + boxes to
  // assign, and the resolved per-piece / per-box theme (a later scope wins, boxes ordered before pieces so a
  // piece override beats its box).
  function themesState() {
    const pieceThemes = {}, boxThemes = {};
    for (const s of (doc.themeScopes || [])) {
      if (s.box) boxThemes[s.box] = s.theme;
      for (const pid of (s.pieces || [])) pieceThemes[pid] = s.theme;
    }
    return JSON.stringify({
      themes: doc.themes || {},
      mapTheme: doc.mapTheme || "",
      pieces: (doc.pieces || []).filter(p => p.role !== "buffer" && p.role !== "connector").map(p => ({ id: p.id, role: p.role })),
      boxes: (doc.boxes || []).map(b => ({ id: b.id, kind: b.kind, members: boxMembers(doc, b).map(p => p.id) })),
      pieceThemes, boxThemes,
    });
  }
  // Rebuild themeScopes so box scopes precede piece scopes (box < piece priority), and drop the empties.
  function normalizeScopes() {
    const all = (doc.themeScopes || []).filter(s => s.theme && (s.box || (s.pieces || []).length));
    const ordered = [...all.filter(s => s.box), ...all.filter(s => !s.box)];
    if (ordered.length) doc.themeScopes = ordered; else delete doc.themeScopes;
  }
  function afterThemeChange() {
    normalizeScopes(); scheduleSave(); fire("OnThemes", themesState());
    if (themeApplyOn) scheduleThemePaint();   // keep the live paint overlay current with each assignment
  }

  // ── theme-apply canvas mode (G157) ──────────────────────────────────────────
  // The Apply step turns the plan canvas into a read-only theme-assignment surface: pick shapes, assign a
  // theme, and see the precise paint the export would place — the server's themed render, blitted in the plan's
  // block frame and refreshed (debounced) on every assignment. Same painter as the old static preview, now live
  // on the interactive canvas instead of a dead panel.
  let themeApplyOn = false, themePaintTimer = null, themePaintSeq = 0;
  function scheduleThemePaint() {
    if (themePaintTimer) clearTimeout(themePaintTimer);
    themePaintTimer = setTimeout(runThemePaint, 250);
  }
  async function runThemePaint() {
    const seq = ++themePaintSeq;
    let res;
    try { res = await fetch("/api/terrain/theme-map-preview", { method: "POST", headers: { "Content-Type": "application/json" }, body: toJson(doc) }); }
    catch { return; }                          // offline / transient — keep the last good overlay
    if (seq !== themePaintSeq || !res.ok) return;
    let data; try { data = await res.json(); } catch { return; }
    if (seq !== themePaintSeq) return;
    canvas.setThemePaint(data.svg, { minX: data.minX, minZ: data.minZ, spanX: data.spanX, spanZ: data.spanZ });
  }

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
    armBoxKind(kind) { canvasBoxKind = BOX_KINDS.includes(kind) ? kind : "hub"; canvas.setBoxKind(canvasBoxKind); },
    fit() { canvas.fit(); },
    resize() { canvas.resize(); },

    // Read-only 3-D height preview (G27): swap between the 2-D top-down view and the iso extrusion of the
    // pieces' surfaces. showIso resolves false when WebGL/the preview module is unavailable — stay in 2-D
    // and tell the host so it can disable the toggle.
    setView(v) {
      if (v !== "iso") { view = "2d"; canvas.hideIso(); return; }
      canvas.showIso(planIsoSolids(doc, structures), isoYaw, viewBounds(doc)).then(ok => {
        view = ok === false ? "2d" : "iso";
        if (ok === false) fire("OnIsoUnavailable");
      });
    },
    rotateIso() { isoYaw = (isoYaw + 90) % 360; refreshIso(); },

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
      for (const b of doc.boxes || []) if (Array.isArray(b.members)) b.members = b.members.map(m => (m === oldId ? id : m));
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
    setBoxId(oldId, newId) {
      const b = (doc.boxes || []).find(x => x.id === oldId); if (!b || !newId || newId === oldId) return;
      b.id = uniqueId(doc.boxes.filter(x => x !== b).map(x => x.id), newId);
      canvas.setDoc(doc); canvas.select({ kind: "box", id: b.id }); scheduleSave();
    },
    setBoxKind(id, kind) {
      const b = (doc.boxes || []).find(x => x.id === id); if (!b || !BOX_KINDS.includes(kind)) return;
      b.kind = kind;
      canvas.setDoc(doc); canvas.select({ kind: "box", id }); scheduleSave();
    },
    // Freeze a containment-grouped box's membership as an explicit list (or release it back to containment),
    // so an envelope that must own an unusual set of pieces can say so.
    toggleBoxMembers(id) {
      const b = (doc.boxes || []).find(x => x.id === id); if (!b) return;
      if (Array.isArray(b.members) && b.members.length) delete b.members;
      else { const m = boxMembers(doc, b).map(p => p.id); if (m.length) b.members = m; }
      canvas.setDoc(doc); canvas.select({ kind: "box", id }); scheduleSave();
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
    focusViolation(index) { canvas.focusViolation(index); },

    // Paint one box's nearest-miss cells on the canvas (or clear with an empty string) — the feasibility panel's
    // isolate action, the producibility twin of focusViolation.
    showNearestMiss(json) {
      if (!json) { canvas.setNearestMiss(null); return; }
      try { canvas.setNearestMiss(JSON.parse(json)); } catch { canvas.setNearestMiss(null); }
    },

    // ── terrain-paint themes (TP10) ──
    getThemes() { return themesState(); },
    defineTheme(name) {
      if (!doc.themes) doc.themes = {};
      const id = uniqueId(Object.keys(doc.themes), (name || "theme").trim() || "theme");
      doc.themes[id] = defaultThemeJson();
      afterThemeChange(); return id;
    },
    renameTheme(oldId, newId) {
      if (!doc.themes || !doc.themes[oldId]) return oldId;
      const id = uniqueId(Object.keys(doc.themes).filter(k => k !== oldId), (newId || oldId).trim() || oldId);
      if (id === oldId) return oldId;
      doc.themes[id] = doc.themes[oldId]; delete doc.themes[oldId];
      if (doc.mapTheme === oldId) doc.mapTheme = id;
      for (const s of (doc.themeScopes || [])) if (s.theme === oldId) s.theme = id;
      afterThemeChange(); return id;
    },
    deleteTheme(id) {
      if (!doc.themes || !doc.themes[id]) return;
      delete doc.themes[id];
      if (!Object.keys(doc.themes).length) delete doc.themes;
      if (doc.mapTheme === id) delete doc.mapTheme;
      if (doc.themeScopes) doc.themeScopes = doc.themeScopes.filter(s => s.theme !== id);
      afterThemeChange();
    },
    // Replace a theme's material JSON (the raw TerrainTheme). Returns an error string on invalid JSON, else null.
    setThemeJson(id, text) {
      if (!doc.themes || !doc.themes[id]) return "No such theme.";
      let parsed; try { parsed = JSON.parse(text); } catch (e) { return e?.message || "Invalid JSON"; }
      doc.themes[id] = parsed; afterThemeChange(); return null;
    },
    setMapTheme(id) {
      if (id && doc.themes && doc.themes[id]) doc.mapTheme = id; else delete doc.mapTheme;
      afterThemeChange();
    },
    // Assign (or clear, with an empty themeId) a theme to one piece — a per-piece override, the top layer.
    assignPiece(pieceId, themeId) {
      doc.themeScopes = (doc.themeScopes || [])
        .map(s => s.box ? s : ({ ...s, pieces: (s.pieces || []).filter(p => p !== pieceId) }));
      if (themeId && doc.themes && doc.themes[themeId]) doc.themeScopes.push({ theme: themeId, pieces: [pieceId] });
      afterThemeChange();
    },
    // Assign (or clear) a theme to a box — a collection layer under piece overrides.
    assignBox(boxId, themeId) {
      doc.themeScopes = (doc.themeScopes || []).filter(s => s.box !== boxId);
      if (themeId && doc.themes && doc.themes[themeId]) doc.themeScopes.push({ theme: themeId, box: boxId });
      afterThemeChange();
    },

    // Enter/leave the Apply step's read-only theme-assignment canvas mode (G157): select-only pointer, the live
    // themed-paint overlay, and a first render. Leaving clears the overlay so the draw canvas paints normally.
    themeApply(on) {
      themeApplyOn = !!on;
      canvas.setSelectOnly(themeApplyOn);
      canvas.setThemeOverlay(themeApplyOn);
      if (themeApplyOn) { canvas.setTool("select"); canvas.clearSelection(); scheduleThemePaint(); canvas.fit(); }
      else canvas.setThemePaint(null, null);
    },
    // The current Ctrl-multi-selection (box/piece) as JSON — what a theme assignment applies to.
    getThemeSelection() { return JSON.stringify(canvas.getMultiSelection()); },
    // Drive the canvas selection from the host (a panel row); empty kind clears.
    selectShape(kind, id) { canvas.select(kind ? { kind, id } : null); },

    dispose() { if (saveTimer) clearTimeout(saveTimer); if (inspectTimer) clearTimeout(inspectTimer); inspectSeq++; canvas.dispose(); },
  };
}
