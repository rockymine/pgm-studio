// plan-bridge.js — JS-interop bridge for the plan editor (the seed studio). Owns the plan document and
// drives PlanCanvas; Blazor owns the toolbar / globals form / inspector chrome and persistence UI. The
// canvas reports selection + edits back here (onSelect / onCreate / onDelete / onChange); this bridge
// mutates the document, mints ids, runs the live feeds and pushes the selection JSON to the Blazor
// inspector. Import/export round-trip the plan wire format via plan-doc.
//
// The document is NOT cached client-side. The database is its store — a map's `plan_json` artifact, or a
// `plan` row — and a second copy in localStorage could only ever disagree with it: the editor restored that
// copy at mount, before any route-specific load ran, so a map with no stored plan yet rendered whatever the
// browser last had cached. That is why the same three maps showed one drawing in one browser and another in
// the next, and why "New plan" opened someone else's board. The three keys that remain are UI preferences —
// overlay chips, the height-map fill, the surface stepper — which are about this browser, not about a plan.

import { PlanCanvas } from "../canvas/plan-canvas.js";
import {
  emptyDoc, normalizeDoc, fromJson, toJson, uniqueId, cycleWall, defaultReference, ROLES, BOX_KINDS,
  viewBounds, markerList, MARKER_KINDS, boxMembers,
} from "../plan/plan-doc.js";
import { parseOverlays } from "../plan/plan-inspect.js";
import { fireTo } from "./fire.js";

const OVERLAY_KEY = "pgm-plan-overlays";
const HEIGHTMAP_KEY = "pgm-plan-heightmap";
const SURFACESTEP_KEY = "pgm-plan-surface-step";

export async function mount(svgEl, wrapEl, cursorEl, dotnetRef) {
  let doc = emptyDoc();
  const fire = (name, ...args) => fireTo(dotnetRef, name, ...args);

  // Read-only 3-D preview: "2d" | "iso", with a user-rotatable yaw. It draws the world the plan compiles to
  // — the ground its pieces build and the structures standing on it — rather than an extrusion of the pieces,
  // so what it shows is what the map will be. A compiled plan carries a full intent, which is why the cages,
  // spawn cubes and monuments are in this picture and not in the sketch tool's.
  //
  // The compile and build cost about a second, so it happens on entering the preview. Nothing is drawn in
  // 3-D, so there is nothing to keep up with while it is open; the mesh is kept against the document it came
  // from, and re-entering an untouched plan draws it again instead of rebuilding.
  let view = "2d";
  let isoYaw = 30;
  let isoMesh = null, isoStamp = null, isoSeq = 0;
  function refreshIso() { if (view === "iso" && isoMesh) canvas.drawIso(isoMesh, isoYaw, viewBounds(doc)); }
  function dropIsoMesh() { isoMesh = null; isoStamp = null; }

  async function enterIso() {
    const ok = await canvas.enterIso();
    if (ok === false) { view = "2d"; fire("OnIsoUnavailable", ""); return; }
    view = "iso";

    const state = toJson(doc);
    if (isoMesh && isoStamp === state) { canvas.drawIso(isoMesh, isoYaw, viewBounds(doc)); return; }

    const seq = ++isoSeq;
    const built = await fetchColumns(state);
    if (seq !== isoSeq || view !== "iso") return;
    if (!built.mesh) { canvas.hideIso(); view = "2d"; fire("OnIsoUnavailable", built.error); return; }
    isoMesh = built.mesh; isoStamp = state;
    canvas.drawIso(isoMesh, isoYaw, viewBounds(doc));
  }

  // Answers {mesh} or {error} — see the sketch bridge: a refused build carries a sentence worth showing,
  // and throwing it away is what made every failure read as "no WebGL".
  async function fetchColumns(state) {
    try {
      const res = await fetch("/api/plan/columns", {
        method: "POST", headers: { "Content-Type": "application/json" }, body: state,
      });
      if (!res.ok) return { error: await refusalText(res) };
      const { meshColumns } = await import("../render/column-mesh.js");
      return { mesh: meshColumns(await res.json()) };
    } catch { return { error: "the build could not be reached" }; }
  }

  async function refusalText(res) {
    try {
      const body = await res.json();
      return body?.message || body?.error || `the build answered ${res.status}`;
    } catch { return `the build answered ${res.status}`; }
  }

  const canvas = new PlanCanvas(svgEl, wrapEl, {
    cursorEl,
    onSelect: (sel) => fire("OnSelect", sel ? JSON.stringify(sel) : null),
    onZoom: (pct) => fire("OnZoom", pct),
    onTool: (t) => fire("OnTool", t),
    onChange: () => afterEdit(),
    onCreate: (kind, rect) => createRect(kind, rect),
    onDelete: (sel) => deleteSelection(sel),
    onCycleWall: (a, b) => cycleWallMark(a, b),
  });

  // ── document mutations (canvas + inspector edits funnel here) ───────────────

  function createRect(kind, rect) {
    if (kind === "zone") {
      // The id says which kind it is, because a plan carries both and an author reads the list before the
      // canvas. The default kind is left off the object entirely, so a plan of build zones is unchanged.
      const lane = zoneKind === "water-lane";
      const id = uniqueId(doc.zones.map(z => z.id), lane ? "lane" : "zone");
      doc.zones.push(lane ? { id, kind: "water-lane", rect, holes: [] } : { id, rect, holes: [] });
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
        : role === "buffer" ? "buffer" : "piece";
      const id = uniqueId(doc.pieces.map(p => p.id), base);
      doc.pieces.push({ id, role, rect });
      canvas.setDoc(doc);
      canvas.select({ kind: "piece", id });
    }
    afterEdit();
  }

  // Cycle a wall mark on a land-interface piece pair (none → chests facing a → chests facing b → none), then
  // re-run the live feeds immediately so the heavy bar and its chest-face tick (and the "not an interface"
  // error, if the pair stops sharing a seam) refresh without waiting on the debounce.
  function cycleWallMark(a, b) {
    cycleWall(doc, a, b);
    canvas.setDoc(doc);
    afterEdit();
    runLive();
  }

  function deleteSelection(sel) {
    if (!sel) return;
    if (sel.kind === "piece") {
      const p = doc.pieces.find(x => x.id === sel.id);
      doc.pieces = doc.pieces.filter(x => x.id !== sel.id);
      if (p) {   // drop any markers that rode the removed piece + any wall mark referencing it
        for (const kind of MARKER_KINDS) {
          const list = markerList(doc, kind);
          if (list) markerList(doc, kind).splice(0, list.length, ...list.filter(m => m.piece !== sel.id));
        }
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
    afterEdit();
  }

  // Role armed for the next drawn piece, and kind armed for the next drawn box (both mirrored in the canvas
  // so the draw preview's colour matches what will be created).
  let canvasRole = "piece";
  let canvasBoxKind = "hub";
  let zoneKind = "build";       // which kind the zone tool draws — build (open now) | water-lane (opens later)

  // ── what every edit does ────────────────────────────────────────────────────
  // One call at the end of each mutation, so no edit can reach the canvas without the derived views
  // following it. Persistence is not among them: saving is the host's, on the author's word.

  function afterEdit() {
    scheduleInspect();
    dropIsoMesh();   // the 3-D preview is of a world this edit has just changed; it is rebuilt on re-entry
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
      return;
    }
    let data;
    try { data = await res.json(); } catch { return; }
    if (seq !== inspectSeq) return;            // re-check after the awaited body
    canvas.setInspect({ interfaces: data.interfaces || [], gapLinks: data.gapLinks || [], frontline: data.frontline || [] });
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
    afterEdit();
    paintReference().then(painted => { if (fit && painted) canvas.fit(); });
  }

  function persistOverlays() { try { localStorage.setItem(OVERLAY_KEY, JSON.stringify(overlays)); } catch { /* private mode */ } }

  // A fresh mount opens the blank document `doc` already is. What fills it is the host's route-specific
  // load — a map's artifact, or a plan row — and nothing else, so the editor shows the plan it names.
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

    // Swap between the 2-D top-down view and the 3-D one. enterIso tells the host when the preview cannot
    // run — no WebGL, or a build that did not come back — so it can disable the toggle.
    setView(v) {
      if (v !== "iso") { view = "2d"; canvas.hideIso(); return; }
      enterIso();
    },
    rotateIso() { isoYaw = (isoYaw + 90) % 360; refreshIso(); },

    newDoc() { load(emptyDoc()); },
    importJson(text) { try { load(fromJson(text)); return null; } catch (e) { return e?.message || "Invalid plan JSON"; } },
    exportJson() { return toJson(doc); },
    getMeta() { return metaJson(); },

    // Reference (tracing) backdrop: pick a real map to trace over, nudge its placement, or clear it.
    async setReferenceMap(slug) {
      if (!slug) { delete doc.reference; canvas.setReference(null, null); fire("OnMeta", metaJson()); afterEdit(); return null; }
      const data = await fetchSurface(slug);
      if (!data) return "That map has no cached surface render.";
      doc.reference = defaultReference(slug);
      canvas.setReference(data, { offset: doc.reference.offset, scale: doc.reference.scale, opacity: doc.reference.opacity });
      canvas.fit();
      fire("OnMeta", metaJson());
      afterEdit();
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
      afterEdit();
    },
    recenterReference() {
      const ref = doc.reference; if (!ref) return;
      ref.offset = [0, 0]; ref.scale = 1;
      canvas.updateReference({ offset: ref.offset, scale: ref.scale, opacity: ref.opacity });
      canvas.fit();
      fire("OnMeta", metaJson());
      afterEdit();
    },
    clearReference() { delete doc.reference; canvas.setReference(null, null); fire("OnMeta", metaJson()); afterEdit(); },

    setName(name) { doc.meta.name = name || "Untitled plan"; afterEdit(); },
    setGlobal(key, value) {
      const g = doc.globals;
      if (key === "symmetry") g.symmetry = value;
      else g[key] = Number(value);
      canvas.setDoc(doc);
      if (key === "cell") canvas.fit();
      afterEdit();
    },

    // Inspector edits on the current selection.
    setPieceId(oldId, newId) {
      const p = doc.pieces.find(x => x.id === oldId); if (!p || !newId || newId === oldId) return;
      const id = uniqueId(doc.pieces.filter(x => x !== p).map(x => x.id), newId);
      for (const m of [...doc.placements.spawns, ...doc.placements.wools, ...doc.placements.iron]) if (m.piece === oldId) m.piece = id;
      for (const w of doc.walls || []) { if (w.a === oldId) w.a = id; if (w.b === oldId) w.b = id; }
      for (const b of doc.boxes || []) if (Array.isArray(b.members)) b.members = b.members.map(m => (m === oldId ? id : m));
      p.id = id;
      canvas.setDoc(doc); canvas.select({ kind: "piece", id }); afterEdit();
    },
    setPieceRole(id, role) { const p = doc.pieces.find(x => x.id === id); if (!p) return; p.role = role; canvas.setDoc(doc); canvas.select({ kind: "piece", id }); afterEdit(); },
    stepPieceSurface(id, delta) {
      const p = doc.pieces.find(x => x.id === id); if (!p) return;
      const next = (p.surface ?? doc.globals.surface) + delta;
      if (next === doc.globals.surface) delete p.surface; else p.surface = next;
      canvas.setDoc(doc); canvas.select({ kind: "piece", id }); afterEdit();
    },
    togglePieceMirrors(id) { const p = doc.pieces.find(x => x.id === id); if (!p) return; if (p.mirrors === false) delete p.mirrors; else p.mirrors = false; canvas.setDoc(doc); canvas.select({ kind: "piece", id }); afterEdit(); },
    /** Arm the zone kind the palette drew from; the canvas mirrors it for the draw preview. */
    setZoneKind(kind) { zoneKind = kind === "water-lane" ? "water-lane" : "build"; canvas.setZoneKind(zoneKind); },
    setZoneId(oldId, newId) {
      const z = doc.zones.find(x => x.id === oldId); if (!z || !newId || newId === oldId) return;
      z.id = uniqueId(doc.zones.filter(x => x !== z).map(x => x.id), newId);
      canvas.setDoc(doc); canvas.select({ kind: "zone", id: z.id }); afterEdit();
    },
    setBoxId(oldId, newId) {
      const b = (doc.boxes || []).find(x => x.id === oldId); if (!b || !newId || newId === oldId) return;
      b.id = uniqueId(doc.boxes.filter(x => x !== b).map(x => x.id), newId);
      canvas.setDoc(doc); canvas.select({ kind: "box", id: b.id }); afterEdit();
    },
    setBoxKind(id, kind) {
      const b = (doc.boxes || []).find(x => x.id === id); if (!b || !BOX_KINDS.includes(kind)) return;
      b.kind = kind;
      canvas.setDoc(doc); canvas.select({ kind: "box", id }); afterEdit();
    },
    // Freeze a containment-grouped box's membership as an explicit list (or release it back to containment),
    // so an envelope that must own an unusual set of pieces can say so.
    toggleBoxMembers(id) {
      const b = (doc.boxes || []).find(x => x.id === id); if (!b) return;
      if (Array.isArray(b.members) && b.members.length) delete b.members;
      else { const m = boxMembers(doc, b).map(p => p.id); if (m.length) b.members = m; }
      canvas.setDoc(doc); canvas.select({ kind: "box", id }); afterEdit();
    },
    cycleFacing(index) { const m = doc.placements.spawns[index]; if (!m) return; const o = ["front", "right", "back", "left"]; m.facing = o[(o.indexOf(m.facing) + 1) % 4]; canvas.setDoc(doc); canvas.select({ kind: "marker", markerKind: "spawn", index }); afterEdit(); },

    // Set one structure field on an objective marker — the casing knobs on a core, the design and material
    // on a destroyable. A null value REMOVES the key, which is what keeps a marker the author never varied
    // the bare `{ piece, at }` that plan-doc normalises it to: the host passes null when a field returns to
    // its default, so an unchanged plan does not grow a copy of every default it agreed with.
    setMarkerField(kind, index, key, value) {
      const m = markerList(doc, kind)?.[index];
      if (!m) return;
      if (value === null || value === undefined || value === "") delete m[key]; else m[key] = value;
      canvas.setDoc(doc);
      canvas.select({ kind: "marker", markerKind: kind, index });
      afterEdit();
    },
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

    // Drive the canvas selection from the host (a panel row); empty kind clears.
    selectShape(kind, id) { canvas.select(kind ? { kind, id } : null); },

    dispose() { if (saveTimer) clearTimeout(saveTimer); if (inspectTimer) clearTimeout(inspectTimer); inspectSeq++; canvas.dispose(); },
  };
}
