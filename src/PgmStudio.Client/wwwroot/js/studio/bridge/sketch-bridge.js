// sketch-bridge.js — JS-interop bridge for the Sketch tool's Layout canvas. Plays the reference's
// "layout activity" role on the JS side: owns the live shape list + the group recompute loop
// (geometry/boolean.js, the hot path), drives SketchCanvas, owns the arrow-key nudge, and pushes the
// group→shape tree to the Blazor panel. Blazor owns the toolbar/panel chrome + persistence; it calls
// the handle methods and receives OnShapeSelected / OnGroupSelected / OnLayout / OnDirty / OnToolChanged.
// getState() returns the layout for the host to PATCH (persistence wiring = S2d).

import { SketchCanvas } from "../canvas/sketch-canvas.js";
import { computeGroups, assignShapesToGroups, computeMirrorPreview, restoreGroupMeta } from "../geometry/boolean.js";
import { rectToPolygon, translateShape, rotateShape, boundsOfShapes, splitShape } from "../geometry/shape.js";
import { surfaceHeights } from "../geometry/slope.js";
import { defaultThemeJson, uniqueScopeId } from "../theme/theme-model.js";
import { isPush, pushAmounts, pushAmountPatch } from "../relief/relief-doc.js";
import { fireTo } from "./fire.js";
import * as Keys from "../shared/keys.js";

// Default footprint = 2-team landscape (120×80), framed about the origin. CTW maps fit a ~120-block long
// axis with 10–15-wide lanes; a tight default keeps the canvas at a scale where those read true.
const DEFAULT_SETUP = { bbox: { min_x: -60, max_x: 60, min_z: -40, max_z: 40 }, center: { cx: 0, cz: 0 }, mirror_mode: "rot_180" };

let _seq = 0;
const genId = () => `s${Date.now()}_${_seq++}`;

// Height invariants: every shape is at least one block tall (height >= 1, default 1) and its floor never
// dips below 0. clampHeight/clampFloor coerce an unset or out-of-range value to the nearest valid one.
const MIN_HEIGHT = 1;
const clampHeight = (h) => Math.max(MIN_HEIGHT, h ?? MIN_HEIGHT);
const clampFloor  = (f) => Math.max(0, f ?? 0);

// What a freshly drawn shape stands at — the plan document's surface height, so a sketch and a plan of the
// same board are the same height before either is touched. Distinct from MIN_HEIGHT, which is the floor a
// stored value is clamped to and stays 1: a one-block shape an author asked for is still valid.
const NEW_SHAPE_HEIGHT = 9;

function dimLabel(s) {
  if (s.type === "rectangle") return `${s.max_x - s.min_x}×${s.max_z - s.min_z}`;
  if (s.type === "circle")    return `r=${s.radius}`;
  // A path reads as how wide it is, not how many points it took to draw — the width is the knob an author
  // reaches for, and the point count is already visible as handles on the canvas.
  if (s.type === "polyline")      return `${(s.radius ?? 0) * 2} wide`;
  return `${s.vertices?.length ?? 0} v`;
}

export async function mount(svgEl, wrapEl, coordsEl, zoomEl, dimEl, dotnetRef, slug) {
  let setup = { ...DEFAULT_SETUP };
  // Stacked layers (S7b): each holds its own shapes/groups at a base_y. The canvas always edits the
  // ACTIVE layer's shapes; other layers keep cached shapes+groups for ghosting (2-D) and stacking (iso).
  let layers = [{ id: genId(), name: "Ground", baseY: 0, shapes: [], structural: [], groups: [], savedMetas: [] }];
  let active = 0;
  let groups = [];            // alias of layers[active].groups — kept current by recompute()
  let mirrorVisible = true;
  let selectedGroupId = null; // panel group selection (drives arrow-move of the whole group)
  let reliefMode = false;      // the Relief phase is up: marks are drawn, edited, and reported to the host
  let dressingMode = false;    // the Dressing phase is up: props are, and a shape is not reachable under them
  let view = "2d";             // "2d" | "iso" — the read-only isometric height preview (S6)
  let isoYaw = 30;
  // Terrain-paint theming (docs/world-export/terrain-painting.md TP10): a map-global registry + default; a shape's own override
  // rides on the shape (`shape.theme`), assigned via the Theme phase and resolved at export.
  let themes = {};
  let mapTheme = "";
  // The two room-style snapshots the map binds (structures.md §9): the shell every wool cage is stamped with
  // and the one every spawn cube is. Snapshots rather than library ids, so a library edit never rebuilds a
  // shipped map's rooms — and map-wide rather than per room, because a cage that differed between teams would
  // be a sightline that differed between teams.
  // Three states each, matching the stored layout: an object is the bound style, `undefined` is unpicked (the
  // built-in shell gets stamped), and `null` is no building at all — a pad on open ground. Keeping them apart
  // is what lets an open room survive being opened and saved here; collapsing them would rebuild it.
  let roomStyles = { cage: undefined, spawn: undefined };
  // Dressing (decoration.md) does NOT ride beside theming. A theme is a recipe named once and applied to many
  // footprints; a prop was put somewhere, so the canvas owns the placements and this owns only the load/save.
  // Theme phase: the canvas is a selection surface only. Geometry is the Draw phase's to edit.
  let selectOnly = false;
  // The theme a click paints while the Apply step is up; "" is no brush. Held here as well as on the canvas
  // because the assignment is the bridge's and the hit test is the canvas's.
  let themeBrush = "";

  function setThemeBrush(id) {
    themeBrush = id || "";
    canvas.setThemeBrush(themeBrush);
    fire("OnThemeBrush", themeBrush);
  }

  const fire = (name, ...args) => fireTo(dotnetRef, name, ...args);
  const markDirty = () => fire("OnDirty", groups.length);
  const syncActive = () => { if (layers[active]) layers[active].shapes = canvas.getShapes(); };

  // Compute a layer's groups from its shapes (used for non-active layers at load + on switch).
  function computeLayerGroups(shapes, savedMetas) {
    const { groups: next, addUnion, afterSub, overrideAddUnion } = computeGroups(shapes, []);
    assignShapesToGroups(shapes, next, addUnion, overrideAddUnion, afterSub);
    if (savedMetas?.length) restoreGroupMeta(next, savedMetas, ["id", "name", "mirrors"]);
    return next;
  }

  // The other layers' group outlines (for the 2-D ghost render).
  const ghostPolys = () => layers.flatMap((L, i) => i === active ? [] : L.groups.map(o => ({ exterior: o.exterior, holes: o.holes })));

  // The canvas's own callbacks are the other way an edit arrives — a draw, a drag, a key. Every one that
  // changes the document is a step; `history.step` is re-entrant, so the ones that arrive inside a pointer
  // drag fold into that drag's single step rather than making one each.
  const edit = (fn) => (...args) => history.step(() => fn(...args));

  const canvas = new SketchCanvas(svgEl, wrapEl, {
    cursorEl: coordsEl, zoomEl, dimEl,
    onShapeCreated: edit((partial) => {
      const shape = { ...partial, id: genId(), override: partial.override ?? false, base_height: clampHeight(partial.base_height ?? NEW_SHAPE_HEIGHT), floor: clampFloor(partial.floor) };
      canvas.addShape(shape);
      recompute();
      canvas.setActiveTool("select");
      fire("OnToolChanged", "select");
      selectShape(shape.id);
      markDirty();
    }),
    onShapeUpdated: edit(() => { recompute(); markDirty(); }),
    onShapeSelected: (id) => selectShape(id),
    // A brush in hand paints the shape it is clicked on; shift widens it to every shape the group holds, and
    // alt lifts that shape's theme back into the hand. All three write through the one assignment.
    onThemePaint: edit((id) => { setShapeTheme(id, themeBrush); afterThemeChange(); }),
    onThemePaintGroup: edit((groupId) => { setGroupTheme(groupId, themeBrush); afterThemeChange(); }),
    onThemeLift: (id) => {
      const shape = canvas.getShape(id);
      setThemeBrush(shape?.theme ?? "");
    },
    onThemeDrop: () => setThemeBrush(""),
    onGroupSelected: (id) => selectGroup(id),
    // Placing, moving and picking a prop all happen on the canvas; the bridge only has to relay the result.
    onDressingChanged: edit(() => afterDressingChange()),
    onPropSelected:    () => fire("OnDressing", dressingState()),
    // A placed prop ends its tool, the same as a completed draw: the toolbar follows the canvas back to select.
    onDressingPlaced:  () => { canvas.setActiveTool("select"); fire("OnToolChanged", "select"); },
    // A join is one edit and one undo step, and it answers a sentence either way — what it did, or why it
    // would not. The refusals it cannot answer are the joint model's, and those arrive with the preview.
    onDressingJoin: edit((result) => {
      if (result?.refused) fire("OnDressing", dressingState(result.refused));
      else afterDressingChange(result?.done === "apart"
        ? `Taken apart into ${result.wings} buildings.`
        : `Joined into one building of ${result?.wings ?? 0} wings.`);
    }),
    // Relief marks follow exactly the same three rules, for the same reasons.
    onReliefChanged: edit(() => afterReliefChange()),
    onMarkSelected:  () => fire("OnRelief", reliefState()),
    onReliefPlaced:  () => { canvas.setActiveTool("select"); fire("OnToolChanged", "select"); },
    onShapeDeleted:  edit((id) => { canvas.removeShape(id); recompute(); selectShape(null); markDirty(); }),
    onShapePromote:  edit((id) => promoteShape(id)),
    onSplit:         edit((a, b) => splitAt(a, b)),
    onVertexSelected: (shapeId, idx) => {
      const s = canvas.getShape(shapeId);
      const h = s ? clampHeight(s.anchor_heights?.[idx] ?? s.base_height) : MIN_HEIGHT;
      fire("OnVertexSelected", shapeId ?? null, idx, h);
    },
    // The shift-marked surface-slope control set changed — send each control's index + its current height
    // so the inspector can offer a height box per marked vertex + Apply.
    onSlopeControls: (shapeId, indices) => {
      const s = canvas.getShape(shapeId);
      if (!s?.vertices) { fire("OnSlopeControls", shapeId ?? null, "[]"); return; }
      const base = clampHeight(s.base_height);
      const controls = indices.map(idx => ({ idx, height: clampHeight(s.anchor_heights?.[idx] ?? base) }));
      fire("OnSlopeControls", shapeId ?? null, JSON.stringify(controls));
    },
  });

  // Promote a rectangle to a polygon (keeps id, so its group membership + selection survive); a no-op
  // for any other type. After promotion the shape edits as a polygon (vertex/midpoint/Bézier).
  function promoteShape(id) {
    const s = canvas.getShape(id);
    if (!s || s.type !== "rectangle") return;
    canvas.updateShape(rectToPolygon(s));
    recompute();
    selectShape(id);
    markDirty();
  }

  // Split tool (S14): the slice a→b cuts the topmost shape it crosses into two, in place. Try shapes
  // top-first; the first that yields a clean two-way cut is replaced by its two halves.
  function splitAt(a, b) {
    for (const s of [...canvas.getShapes()].reverse()) {
      const halves = splitShape(s, a, b);
      if (!halves) continue;
      canvas.removeShape(s.id);
      for (const h of halves)
        canvas.addShape({ ...h, id: genId(), override: h.override ?? false, base_height: clampHeight(h.base_height), floor: clampFloor(h.floor) });
      recompute();
      canvas.setActiveTool("select");        // a completed cut drops back to select (like the draw tools)
      fire("OnToolChanged", "select");
      selectShape(null);
      markDirty();
      return;
    }
  }

  function selectShape(id) {
    selectedGroupId = null;
    canvas.selectShape(id);
    fire("OnShapeSelected", id ?? null);
    fire("OnGroupSelected", null);
  }

  function selectGroup(id) {
    selectedGroupId = id ?? null;
    canvas.selectGroup(selectedGroupId);
    // A single-member group shows the shape inspector (its member) — set height / convert / op without
    // drilling; a multi-shape group shows the group inspector. Either way selectedGroupId stays set, so
    // arrow-nudge (and later rotate) act on the whole group. A phase placing things of its own drills to
    // neither: the Draw actions are unavailable there, and a selected shape is what the shape-level chords
    // reach.
    const isl = selectedGroupId ? groups.find(i => i.id === selectedGroupId) : null;
    const placesOwnThings = reliefMode || dressingMode;
    const single = !placesOwnThings && isl && isl.shapeIds.length === 1 ? isl.shapeIds[0] : null;
    fire("OnShapeSelected", single);
    fire("OnGroupSelected", single ? null : selectedGroupId);
    // In the Relief phase the group IS the unit being edited — its base, reach, step and grain are what the
    // marks are stated against — so picking one has to reach the inspector.
    if (reliefMode) fire("OnRelief", reliefState());
  }

  // Rotate the current selection by `deg` degrees about its bbox centre (the inspector's numeric field; the
  // canvas owns the drag-handle path). Group selected → all its members; a drilled shape → just that shape.
  function rotateSelected(deg) {
    const rad = (deg || 0) * Math.PI / 180;
    if (!rad) return;
    let ids;
    if (selectedGroupId) { const isl = groups.find(i => i.id === selectedGroupId); ids = isl?.shapeIds ?? []; }
    else if (canvas.selectedId) ids = [canvas.selectedId];
    else return;
    const shapes = ids.map(id => canvas.getShape(id)).filter(Boolean);
    if (!shapes.length) return;
    const b = boundsOfShapes(shapes);
    const pivot = [(b.min_x + b.max_x) / 2, (b.min_z + b.max_z) / 2];
    for (const s of shapes) canvas.updateShape(rotateShape(s, rad, pivot));
    recompute();
    markDirty();
  }

  // Recompute groups from the canvas's current shapes and push results to the canvas + panel.
  // `restoreFromSaved` (load only) seeds metadata from persisted records; live edits carry metadata
  // over via the previous groups (centroid match inside computeGroups).
  function recompute(restoreFromSaved = false) {
    const shapes = canvas.getShapes();
    const prev = restoreFromSaved ? [] : groups;
    const { groups: next, addUnion, afterSub, overrideAddUnion } = computeGroups(shapes, prev);
    assignShapesToGroups(shapes, next, addUnion, overrideAddUnion, afterSub);
    const sm = layers[active].savedMetas;
    if (restoreFromSaved && sm?.length) restoreGroupMeta(next, sm, ["id", "name", "mirrors"]);
    groups = next;
    layers[active].groups = next;
    layers[active].shapes = shapes;
    // `mirrors` rides along because the relief overlay ghosts a mark only on a group that opted in — the
    // rasterizer fans only those, so a ghost anywhere else is terrain that will never be built.
    canvas.setGroups(next.map(i => ({ id: i.id, shapeIds: i.shapeIds, exterior: i.exterior, holes: i.holes, mirrors: i.mirrors })));
    canvas.setGhostGroups(ghostPolys());
    refreshMirror();
    pushLayout();
    pushLayers();
    dropIsoMesh();
    refreshPaint();   // the geometry moved, so the paint on it has too (no-op unless the overlay is on)
    // A relief is solved over the group's own footprint, so moving the geometry re-shapes the ground under
    // it — and a re-fused group can change which relief applies at all.
    refreshRelief();
  }

  // ── the 3-D preview ────────────────────────────────────────────────────────
  // The picture is the world the export builds, not a second guess at it: the live layout goes to
  // `sketch/columns`, which runs the real build over it and answers every column's solid runs, and
  // `column-mesh.js` turns those into triangles. The client decides nothing about height — which is the
  // point, because it cannot: a shape's top is settled by the per-group relief solve and then again by
  // whatever the shape says about being erected, and neither is derivable here without a second copy of the
  // solver. What the browser used to extrude was the first of those three stages on its own.
  //
  // The build is the cost — around a second on a full board against forty milliseconds to read the columns
  // out of it — so it happens on entering the preview rather than on every edit. Nothing is drawn in 3-D, so
  // there is no edit to keep up with; the mesh is kept against the layout it was built from, and re-entering
  // an untouched board draws the cached one instead of asking again.
  let isoMesh = null, isoPayload = null, isoStamp = null, isoSeq = 0;

  // Which layers the preview is leaving out, by the ids the payload spells. Kept across a rebuild, so an
  // author who hid the deck to look under it does not have to hide it again after every edit.
  const isoHidden = new Set();

  function refreshIso() { if (view === "iso" && isoMesh) canvas.drawIso(isoMesh, isoYaw, setup.bbox); }

  // Re-mesh what is already in hand. Hiding a layer is a filter over the payload rather than a question for
  // the server: the runs say which layer drew them, so the board only has to be built once.
  async function remeshIso() {
    if (!isoPayload) return;
    const { meshColumns } = await import("../render/column-mesh.js");
    isoMesh = meshColumns(isoPayload, [...isoHidden]);
    refreshIso();
  }

  // An edit invalidates the picture. A stale mesh redrawn on rotate would show the board as it was two edits
  // ago and give no sign of it.
  function dropIsoMesh() { isoMesh = null; isoPayload = null; isoStamp = null; }

  // Enter the preview, then fill it. Two steps so the toggle answers the click at once and the wait is a
  // spinner over the 3-D surface rather than a frozen 2-D one.
  async function enterIso() {
    const ok = await canvas.enterIso();
    if (ok === false) { view = "2d"; fire("OnIsoUnavailable", ""); return; }
    view = "iso";

    const state = JSON.stringify(handle.getState());
    if (isoMesh && isoStamp === state) { canvas.drawIso(isoMesh, isoYaw, setup.bbox); return; }

    const seq = ++isoSeq;
    const built = await fetchColumns(state);
    if (seq !== isoSeq || view !== "iso") return;   // left the preview, or a newer entry overtook this one
    if (!built.payload) {
      // A build that did not happen has nothing to report as left out; a stale list would name shapes
      // against a picture that is not on screen.
      fire("OnIsoNotBuilt", "[]");
      canvas.hideIso(); view = "2d"; fire("OnIsoUnavailable", built.error); return;
    }

    isoPayload = built.payload; isoStamp = state;
    // What the build left out. The preview draws the world the export builds, so a shape the board's own
    // algebra discards is simply not in the picture — and an absence looks exactly like ground nobody drew.
    // The findings name which shapes, so the host can say so rather than leaving the author to notice.
    fire("OnIsoNotBuilt", JSON.stringify(built.payload.warnings ?? []));
    // A layer the board no longer has is not left hidden: it would be a switch the host cannot show and the
    // author cannot turn back on.
    const names = isoPayload.layers ?? [];
    for (const id of [...isoHidden]) if (!names.includes(id)) isoHidden.delete(id);
    fire("OnIsoLayers", JSON.stringify({ layers: names, hidden: [...isoHidden] }));

    const { meshColumns } = await import("../render/column-mesh.js");
    isoMesh = meshColumns(isoPayload, [...isoHidden]);
    canvas.drawIso(isoMesh, isoYaw, setup.bbox);
  }

  // Answers {payload} or {error}: the reason travels because the host shows it. A refused build carries the
  // studio's own envelope — a rule id and a sentence — and dropping that on the floor is what made every
  // failure read as "no WebGL", including on a browser that has it. A build that succeeds carries `warnings`
  // on the payload for the same reason: what it could not put in the world is the half a picture cannot show.
  async function fetchColumns(state) {
    if (!slug) return { error: "this sketch has no map to build" };
    try {
      const res = await fetch(`/api/map/${encodeURIComponent(slug)}/sketch/columns`, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: state,
      });
      if (!res.ok) return { error: await refusalText(res) };
      return { payload: await res.json() };
    } catch { return { error: "the build could not be reached" }; }   // offline or mid-navigation
  }

  // The sentence out of a refusal envelope {error, message, findings[]}, or the status when a body is not one.
  async function refusalText(res) {
    try {
      const body = await res.json();
      return body?.message || body?.error || `the build answered ${res.status}`;
    } catch { return `the build answered ${res.status}`; }
  }

  function refreshMirror() {
    if (!mirrorVisible || !setup.mirror_mode) { canvas.setMirrorPolygons([]); return; }
    const { cx = 0, cz = 0 } = setup.center ?? {};
    canvas.setMirrorPolygons(computeMirrorPreview(groups, setup.mirror_mode, cx, cz));
  }

  // Push the group→shape tree to the Blazor panel (compact — render fields + a precomputed dim label).
  function pushLayout() {
    const shapes = canvas.getShapes().map(s => ({
      id: s.id, type: s.type, operation: s.operation, override: !!s.override, dim: dimLabel(s),
      baseHeight: clampHeight(s.base_height), floor: clampFloor(s.floor),
      heightMode: s.height_mode ?? "", skirt: s.skirt ?? 0, reliefScope: s.relief_scope ?? "",
      radius: s.radius ?? 0, strokeEdge: s.stroke_edge ?? "", strokeSeed: s.stroke_seed ?? 0,
    }));
    const isl = groups.map(i => ({ id: i.id, name: i.name, mirrors: i.mirrors, shapeIds: i.shapeIds }));
    fire("OnLayout", JSON.stringify({ groups: isl, shapes }));
  }

  // Push the layer list (id/name/base_y + active) to the Blazor layer panel.
  function pushLayers() {
    fire("OnLayers", JSON.stringify({ active: layers[active].id, layers: layers.map(L => ({ id: L.id, name: L.name, baseY: L.baseY })) }));
  }

  /** Tell the canvas which layer is being drawn on, so whatever is placed lands on it. */
  function pushActiveLayer() { canvas.dressing?.setLayer(layers[active]?.id ?? ""); }

  // Load the active layer's shapes onto the canvas (after a switch/delete) and recompute. The active layer's
  // locked plan pieces (S25) ride alongside as a render-only overlay — never a drawn/edited shape.
  //
  // `groups` is seeded from the layer being loaded before the recompute, because `recompute` carries
  // identity over from whatever `groups` holds: leaving the outgoing layer's there matches the incoming
  // group against a stranger by centroid and adopts its id, and an id is what a relief is keyed by. Two
  // layers of one board are centred on the same place, so the match always succeeds and the ground of a
  // stacked board comes back named after the layer under it — losing its relief in the live layout and
  // writing the wrong id into the document on the next save.
  function loadActiveToCanvas() {
    canvas.clearShapes();
    for (const sh of layers[active].shapes) canvas.addShape({ ...sh });
    canvas.setStructural(layers[active].structural ?? []);
    selectShape(null);
    pushActiveLayer();
    groups = layers[active].groups ?? [];
    recompute(!groups.length && (layers[active].savedMetas?.length ?? 0) > 0);
  }

  function switchLayer(id) {
    const i = layers.findIndex(L => L.id === id);
    if (i < 0 || i === active) return;
    syncActive();
    active = i;
    loadActiveToCanvas();
    markDirty();
  }

  function addLayer() {
    syncActive();
    const baseY = Math.max(0, ...layers.map(L => L.baseY)) + 10;   // stack the new slab above by default
    layers.push({ id: genId(), name: `Layer ${layers.length + 1}`, baseY, shapes: [], structural: [], groups: [], savedMetas: [] });
    active = layers.length - 1;
    loadActiveToCanvas();
    markDirty();
  }

  function deleteLayer(id) {
    if (layers.length <= 1) return;             // always keep one layer
    const i = layers.findIndex(L => L.id === id);
    if (i < 0) return;
    syncActive();
    layers.splice(i, 1);
    if (active > i) active--;
    if (active >= layers.length) active = layers.length - 1;
    loadActiveToCanvas();
    markDirty();
  }

  function renameLayer(id, name) { const L = layers.find(l => l.id === id); if (!L) return; L.name = name; pushLayers(); markDirty(); }
  function setLayerBaseY(id, y) { const L = layers.find(l => l.id === id); if (!L) return; L.baseY = y; pushLayers(); dropIsoMesh(); markDirty(); }

  // Move the selection by whole blocks. A group moves as its shapes; a drilled shape moves alone.
  function nudge(dx, dz) {
    if (selectOnly) return false;   // select-only: the arrows are a move, and moving belongs to Draw
    return history.step(() => nudgeBy(dx, dz));
  }

  function nudgeBy(dx, dz) {
    let moved = false;
    if (selectedGroupId) {
      const isl = groups.find(i => i.id === selectedGroupId);
      for (const sid of (isl?.shapeIds ?? [])) {
        const s = canvas.getShape(sid);
        if (s) { canvas.updateShape(translateShape(s, dx, dz)); moved = true; }
      }
    } else if (canvas.selectedId) {
      const s = canvas.getShape(canvas.selectedId);
      if (s) { canvas.updateShape(translateShape(s, dx, dz)); moved = true; }
    }
    if (!moved) return false;
    recompute();
    markDirty();
    return true;
  }

  const onCanvas = () => wrapEl?.offsetParent != null;
  // One binding over the four arrows rather than four: they are one gesture with a direction, and a sheet
  // that lists them separately says the same sentence four times.
  const ARROWS = { arrowleft: [-1, 0], arrowright: [1, 0], arrowup: [0, -1], arrowdown: [0, 1] };
  const step = (e, by) => {
    const [dx, dz] = ARROWS[e.key.toLowerCase()] ?? [0, 0];
    return nudge(dx * by, dz * by);
  };
  Keys.register("sketch-bridge", [
    { id: "sketch.nudge", keys: Object.keys(ARROWS), label: "Nudge the selection one block",
      group: "Canvas", when: onCanvas, run: (e) => step(e, 1) },
    { id: "sketch.nudge16", keys: Object.keys(ARROWS).map(key => `shift+${key}`),
      label: "Nudge the selection sixteen blocks", group: "Canvas", when: onCanvas, run: (e) => step(e, 16) },
    { id: "sketch.undo", keys: "mod+z", label: "Undo", group: "Everywhere",
      when: onCanvas, inField: true, run: () => history.undo() },
    { id: "sketch.redo", keys: ["mod+shift+z", "mod+y"], label: "Redo", group: "Everywhere",
      when: onCanvas, inField: true, run: () => history.redo() },
    { id: "sketch.duplicate", keys: "mod+d", label: "Duplicate the selected shape", group: "Canvas",
      when: () => onCanvas() && !selectOnly && !!canvas.selectedId, run: () => duplicateSelected() },
  ]);

  // ── undo ────────────────────────────────────────────────────────────────────────────────────────
  // A step is a whole document — the value getState() answers and load() restores — so nothing here has to
  // know which edit happened, only that one did. A step is opened before an edit and closed after it, and
  // closing compares the two: a press that changed nothing costs no step, and a drag that fires on every
  // frame between one open and one close costs exactly one.
  const HISTORY_DEPTH = 60;
  const history = {
    past: [], future: [], pending: null,
    /** Open a step, unless one is already open. Answers whether this call is the one that opened it — only
     *  that caller may close it, so an edit arriving mid-drag folds into the drag rather than ending it. */
    begin() {
      if (this.pending !== null) return false;
      this.pending = snapshot();
      return true;
    },
    end() {
      if (this.pending === null) return;
      const before = this.pending;
      this.pending = null;
      if (snapshot() === before) return;
      this.past.push(before);
      if (this.past.length > HISTORY_DEPTH) this.past.shift();
      this.future.length = 0;
      pushHistory();
    },
    /** Run an edit as one step. Inside an already-open step it folds into that one. */
    step(fn) {
      const mine = this.begin();
      try { return fn(); } finally { if (mine) this.end(); }
    },
    undo() { return this.move(this.past, this.future); },
    redo() { return this.move(this.future, this.past); },
    move(from, to) {
      if (!from.length) return false;
      to.push(snapshot());
      restore(from.pop());
      pushHistory();
      return true;
    },
    get canUndo() { return this.past.length > 0; },
    get canRedo() { return this.future.length > 0; },
  };

  const snapshot = () => JSON.stringify(handle.getState());

  function restore(text) {
    let state; try { state = JSON.parse(text); } catch { return; }
    const wasSelected = canvas.selectedId;
    handle.load(state, true);
    // A shape that survived the step keeps its selection; one the step created is gone, so the selection
    // clears rather than naming nothing.
    if (wasSelected && canvas.getShape(wasSelected)) selectShape(wasSelected); else selectShape(null);
    markDirty();
  }

  const pushHistory = () => fire("OnHistory", history.canUndo, history.canRedo);

  // A press on the canvas opens a step and the release closes it, so a drag — which marks dirty on every
  // frame — is one step back however many frames it took.
  wrapEl?.addEventListener("pointerdown", () => history.begin());
  const endStep = () => history.end();
  window.addEventListener("pointerup", endStep);

  /** Copy the selected shape clear of its original, and select the copy. */
  function duplicateSelected() {
    const source = canvas.getShape(canvas.selectedId);
    if (!source) return false;
    return history.step(() => {
      const copy = { ...structuredClone(source), id: genId() };
      canvas.addShape(translateShape(copy, 4, 4));
      recompute();
      selectShape(copy.id);
      markDirty();
      return true;
    });
  }

  /** The board's frame, centre and mirror. `keepView` states the working area without framing it: the camera
   *  is the author's, and a restore that moves it reads as a different board rather than as a step back. */
  function applySetup(s, keepView) {
    setup = { bbox: s.bbox ?? setup.bbox, center: s.center ?? setup.center, mirror_mode: s.mirror_mode ?? setup.mirror_mode };
    if (s.bbox) { canvas.setBbox(setup.bbox); if (!keepView) canvas.fitToBbox(); }
    if (s.center !== undefined) canvas.setCenter(setup.center.cx ?? 0, setup.center.cz ?? 0);
    if (s.mirror_mode !== undefined) canvas.setMode(setup.mirror_mode);
    refreshMirror();
  }

  function groupById(id) { return groups.find(i => i.id === id); }

  // ── terrain-paint themes (docs/world-export/terrain-painting.md TP10) ────────────────────────────
  // The theme state the Theme phase reads: the registry, the map default, and the resolved per-shape override
  // (every shape carrying a `theme`). The group tree is the selection surface, so the phase derives an
  // group's theme from its member shapes (uniform → that theme, else mixed).
  function themesState() {
    const shapeThemes = {};
    let shapeCount = 0;
    syncActive();
    // Every layer, not the one being drawn on: a theme is the board's, so what carries one and how many
    // could are counted over the same set or the coverage answers a question nobody asked.
    for (const L of layers) for (const s of (L.shapes || [])) {
      shapeCount++;
      if (s.theme) shapeThemes[s.id] = s.theme;
    }
    return JSON.stringify({ themes, mapTheme: mapTheme || "", shapeThemes, shapeCount });
  }
  // A theme edit is a discrete action the author is waiting on the result of, so it repaints at once; only
  // the continuous geometry stream (drag, resize) is worth coalescing.
  function roomStylesState() { return JSON.stringify(roomStyles); }

  function afterThemeChange() { syncActive(); markDirty(); fire("OnThemes", themesState()); refreshPaint({ now: true }); }

  // ── dressing (decoration.md) ────────────────────────────────────────────────
  // Placed props live on the canvas (it is where they are put, moved and picked); the bridge announces changes
  // and carries them in and out of the stored document. Unlike a theme edit this does not repaint the Blocks
  // overlay: that shows the painter's surface colours, and dressing adds blocks *above* the surface.
  function dressingState(note) {
    const tools = canvas.dressingTools;
    const selectedId = tools?.selectedId ?? null;
    return JSON.stringify({
      props: canvas.dressing.props,
      selectedId,
      // Every picked id, primary first. The inspector edits one prop and the join reads the rest, so the
      // selection travels whole rather than the host inferring a second pick it cannot see.
      selection: tools?.selection ?? (selectedId ? [selectedId] : []),
      selected: selectedId ? canvas.dressing.byId(selectedId) : null,
      // The recipes travel with the state, because a placement names one and a preview of a placement has no
      // document behind it: what the inspector posts has to carry both halves or the key resolves to nothing.
      styles: canvas.dressing.styles,
      note: note ?? null,
    });
  }

  function afterDressingChange(note) { markDirty(); fire("OnDressing", dressingState(note)); }

  // ── the painted Blocks overlay ─────────────────────────────────────────────
  // The Blocks toggle shows the blocks the export places, not an approximation of them: the live layout goes
  // to `sketch/paint`, which runs the real painter over it and returns one colour per footprint cell, and the
  // canvas blits that as a bitmap. So a voronoi reads as its cells and a noise field as its patches — a
  // client-side "one representative colour per theme" could never show either. Only fetched while the overlay
  // is on, and coalesced: geometry edits fire continuously, and a paint is worth one round-trip per settle.
  // The wait is short because the paint itself is: a typical board repaints in tens of milliseconds, so this
  // is what a drag's trailing edge costs, not a cushion for slow work.
  //
  // It takes BOTH the Blocks toggle and the Theme phase. Theming is a finishing pass over a finished sketch,
  // so while the shapes are still being drawn the overlay shows the plain voxelization and nothing else —
  // that is what Blocks is for there, the exact cells an export would fill. Not painting during Draw also
  // costs the drawing loop nothing: no round-trip fires at all.
  const PAINT_DEBOUNCE_MS = 120;
  let paintTimer = null, paintSeq = 0, blocksOn = false, paintPhase = false;
  const paintWanted = () => blocksOn && paintPhase;

  function refreshPaint({ now = false } = {}) {
    if (!paintWanted() || !slug) return;
    clearTimeout(paintTimer);
    paintTimer = setTimeout(fetchPaint, now ? 0 : PAINT_DEBOUNCE_MS);
  }

  // Bring the overlay in line with the toggle + the phase: paint when both want it, drop the bitmap when
  // either stops, so re-entering can't flash a stale one.
  function syncPaint() {
    clearTimeout(paintTimer);
    if (paintWanted()) refreshPaint({ now: true });
    else canvas.loadPaintLayer(null);
  }

  async function fetchPaint() {
    const seq = ++paintSeq;
    try {
      const res = await fetch(`/api/map/${encodeURIComponent(slug)}/sketch/paint`, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(handle.getState()),
      });
      if (!res.ok) return;
      const data = await res.json();
      // Two wire forms, both palette-indexed: row-major runs (what a painted board almost always is), or a
      // per-cell list. The bitmap decoder reads runs directly; the cell list needs its colours expanded.
      if (!data.runs && data.color_idx) data.colors = data.color_idx.map(i => data.palette[i]);
      if (seq === paintSeq) canvas.loadPaintLayer(data);   // ignore a reply overtaken by a newer edit
    } catch { /* offline or mid-navigation — the overlay keeps the stone footprint */ }
  }

  // ── the stated relief (docs/world-export/relief.md) ────────────────────
  // The group in play: the one stating the selected mark, else the one the author has picked on the canvas.
  // Its own settings — base, reach, step, grain — are what the inspector shows and what it writes, so both
  // ask this rather than each deciding: a write reaching a different group than the panel is reading is an
  // edit that lands nowhere and says nothing.
  function reliefGroup() {
    const selectedId = canvas.reliefTools?.selectedId ?? null;
    const selected = selectedId ? canvas.relief.byId(selectedId) : null;
    return selected?.groupId ?? selectedGroupId ?? null;
  }

  // What an author has said about the ground inside each group. Distinct from the contour overlay below it:
  // this is the statement, that is what the solver made of it. Both are on screen at once during the phase,
  // which is the whole reason a mark can be tuned by eye.
  function reliefState() {
    const tools = canvas.reliefTools;
    const selectedId = tools?.selectedId ?? null;
    const selected = selectedId ? canvas.relief.byId(selectedId) : null;
    const groupId = reliefGroup();
    return JSON.stringify({
      // Marks and pushes as one list, because that is how the phase treats them: the sidebar lists them
      // together and the canvas selects across both. Which of the two a row is, its `kind` says.
      marks: canvas.relief.statements,
      selectedId,
      selected,
      // A push's per-vertex lifts, expanded to one number per ring vertex — what an inspector shows, since
      // an author who wants one corner lower needs a number to change and "the amount, except there" is not
      // one. Only for a selected push; null for everything else.
      amounts: selected && isPush(selected) ? pushAmounts(selected) : null,
      groupId,
      groupName: groupId ? (groupById(groupId)?.name ?? groupId) : null,
      // What every group stating a mark is called, so the list heads each one the way the group tree does.
      // A relief is keyed by group id and an id is not a name — `isl_1788640472266_0` heads a list of marks
      // an author placed under "Group 1".
      groupNames: Object.fromEntries(groups.map(group => [group.id, group.name ?? group.id])),
      // What the group's ground already stands at. The panel reads every stated height against it, and it
      // is what an untouched base is: a relief replaces the top of every column of its group, so a base
      // that differs from this moves the whole landmass.
      groupTop: groupId ? canvas.groupTop(groupId) : null,
      relief: groupId ? canvas.relief.peek(groupId) : null,
    });
  }

  function afterReliefChange() {
    markDirty();
    fire("OnRelief", reliefState());
    refreshRelief();   // the statement changed, so the contours it produced have too
  }

  // ── the relief contour overlay ─────────────────────────────────────────────
  // Same seam as the painted Blocks overlay, and for the same reason: the surface a relief produces is
  // solved by the export's own solver, so the only honest preview is the one the server draws. The lines
  // come back as world points and are stroked at the live zoom, so unlike the block bitmap this does not
  // need re-fetching to stay sharp — only when the layout changes.
  //
  // It follows the toggle alone rather than a phase. A relief is geometry: it is worth seeing while the
  // shapes over it are still being drawn, which is exactly when the paint overlay is not.
  const RELIEF_DEBOUNCE_MS = 140;
  let reliefTimer = null, reliefSeq = 0, reliefOn = false;

  function refreshRelief({ now = false } = {}) {
    if (!reliefOn || !slug) return;
    clearTimeout(reliefTimer);
    reliefTimer = setTimeout(fetchRelief, now ? 0 : RELIEF_DEBOUNCE_MS);
  }

  function syncRelief() {
    clearTimeout(reliefTimer);
    if (reliefOn) refreshRelief({ now: true });
    else canvas.loadReliefLayer(null);
  }

  async function fetchRelief() {
    const seq = ++reliefSeq;
    try {
      const res = await fetch(`/api/map/${encodeURIComponent(slug)}/sketch/relief`, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(handle.getState()),
      });
      if (!res.ok) return;
      const data = await res.json();
      if (seq === reliefSeq) canvas.loadReliefLayer(data);   // ignore a reply overtaken by a newer edit
    } catch { /* offline or mid-navigation — the overlay keeps whatever it last drew */ }
  }

  // Set (or clear, with a falsy themeId) a shape's theme override — the live canvas shape so it persists on sync.
  /** Every shape a group holds takes `themeId` (empty clears). The group scope, written per member shape,
   *  which is where a theme is stored — so the brush's shift-click and the handle's assignGroup are one act. */
  function setGroupTheme(groupId, themeId) {
    const isl = groupById(groupId);
    if (!isl) return;
    for (const sid of (isl.shapeIds || [])) setShapeTheme(sid, themeId);
  }

  function setShapeTheme(shapeId, themeId) {
    const s = canvas.getShape(shapeId);
    if (!s) return;
    if (themeId && themes[themeId]) s.theme = themeId; else delete s.theme;
  }

  // Start in the default "move" (pan) tool — matches the Blazor toolbar default. Without this the canvas
  // sits at CanvasBase's null tool, which the base treats as click-to-select, so a click on first load
  // would select a shape/group even though the move tool is shown (only the select tool should select).
  canvas.setActiveTool("move");

  // Seed the default working bounds so drawing + the mirror preview work immediately.
  applySetup(setup);
  canvas.resize();

  const handle = {
    setTool(tool)      {
      canvas.setActiveTool(tool === "select" ? "select" : tool);
      // Leaving select mode clears the selection — otherwise arrow-nudge keeps moving a shape that's no
      // longer visibly selected (you've switched to panning/drawing). Arrow-move is a select-mode action.
      // Not so in select-only mode: nothing there moves the selection, and the selection is what the phase
      // is *for*, so reaching for the hand tool to pan must not throw away what you picked.
      if (tool !== "select" && !selectOnly) selectShape(null);
    },
    // Selection-only: a phase that picks groups and shapes but edits none of them. Forcing the select tool
    // is part of the restriction — a draw tool left armed would add geometry, which is equally the Draw
    // phase's job — and it is where both phases that use the mode want to start. Lifting it only lifts it;
    // the tool stays where it is, which is what the Draw toolbar is already showing.
    setSelectOnly(on)  {
      selectOnly = !!on;
      canvas.setSelectOnly(selectOnly);
      if (selectOnly) canvas.setActiveTool("select");
    },
    setOperation(op)   { canvas.setOperation(op); },
    setMode(mode)      { applySetup({ mirror_mode: mode }); markDirty(); },
    setCenter(cx, cz)  { applySetup({ center: { cx, cz } }); markDirty(); },
    setBbox(b)         { applySetup({ bbox: b }); markDirty(); },
    setShapesVisible(v){ canvas.setShapesVisible(v); },
    setMirrorVisible(v){ mirrorVisible = v; canvas.setMirrorVisible(v); refreshMirror(); },
    setChunkVisible(v) { canvas.setChunkVisible(v); },
    setBlocksVisible(v){
      blocksOn = !!v;
      canvas.setBlocksVisible(blocksOn);
      syncPaint();
    },
    // The contour overlay. Unlike Blocks it is not phase-gated: a relief is geometry, so it is worth seeing
    // while the shapes over it are still being drawn.
    setReliefVisible(v){
      reliefOn = !!v;
      syncRelief();
    },
    // Whether this phase previews the finished paint. Only Theme does: Draw wants the raw voxelization while
    // the shapes are still moving, and painting the layout is server work worth not doing there at all.
    setPaintPreview(on) { paintPhase = !!on; syncPaint(); },
    setSnap(v)         { canvas.setSnapEnabled(v); },
    // enterIso tells the host when the preview cannot run, and which of the two it was: an empty reason is
    // WebGL itself, and any other is the sentence the build answered with. The host says which, because
    // "no WebGL" on a browser that has it sends the reader to the wrong place entirely.
    setView(v)         {
      if (v !== "iso") { view = "2d"; canvas.hideIso(); return; }
      enterIso();
    },
    rotateIso()        { isoYaw = (isoYaw + 90) % 360; refreshIso(); },
    // Show or hide one layer of the preview. The board is not rebuilt — the runs already say which layer
    // drew them, so this re-meshes what is in hand.
    setIsoLayerShown(id, shown) {
      if (shown) isoHidden.delete(id); else isoHidden.add(id);
      remeshIso();
    },
    setHeight(id, base, floor) {
      const s = canvas.getShape(id); if (!s) return;
      if (base  !== null && base  !== undefined) s.base_height = clampHeight(base);   // >= 1
      if (floor !== null && floor !== undefined) s.floor = clampFloor(floor);         // >= 0
      canvas.updateShape(s);   // refresh vertex labels (default = base height)
      pushLayout(); dropIsoMesh(); markDirty();
    },
    // Set one vertex's height (S5b). Materialises anchor_heights (length = vertices, default = base) on first use.
    setVertexHeight(id, idx, h) {
      const s = canvas.getShape(id);
      if (!s?.vertices || idx < 0 || idx >= s.vertices.length) return;
      const base = clampHeight(s.base_height);
      if (!Array.isArray(s.anchor_heights) || s.anchor_heights.length !== s.vertices.length)
        s.anchor_heights = s.vertices.map((_, i) => clampHeight(s.anchor_heights?.[i] ?? base));
      s.anchor_heights[idx] = clampHeight(h);   // a vertex is a height too — never below 1
      canvas.updateShape(s);   // re-render the vertex labels
      pushLayout(); dropIsoMesh(); markDirty();
    },
    // Fit a tilted plane through the 2–3 control vertices (each `{idx, height}`) and read every vertex's
    // height off it → the shape's whole top becomes a flat slope (2 controls = a ramp, 3 = an aimed plane).
    // Heights round to blocks, so a slope reads as the neat straight steps of a staircase.
    applySlope(id, samplesJson) {
      const s = canvas.getShape(id);
      if (!s?.vertices) return;
      let samples;
      try { samples = JSON.parse(samplesJson); } catch { return; }
      const pts = samples
        .filter(c => c.idx >= 0 && c.idx < s.vertices.length)
        .map(c => ({ x: s.vertices[c.idx][0], z: s.vertices[c.idx][1], h: clampHeight(c.height) }));
      const heights = surfaceHeights(s.vertices, pts);
      if (!heights) return;   // fewer than 2 distinct control positions — nothing to fit
      s.anchor_heights = heights.map(clampHeight);
      canvas.updateShape(s);
      pushLayout(); dropIsoMesh(); markDirty();
    },

    // The band a path stands for: its half-width, how its edges are drawn, and the seed a rough edge wanders
    // by. All three are stored on the shape and the ring is derived, so each lands as a reshape.
    setStrokeBand(id, radius, edge, seed) {
      const s = canvas.getShape(id);
      if (s?.type !== "polyline") return;
      if (radius !== null && radius !== undefined) s.radius = Math.max(1, Math.round(radius));
      if (edge) s.stroke_edge = edge;
      if (seed !== null && seed !== undefined) s.stroke_seed = Math.max(0, Math.round(seed));
      canvas.updateShape(s);
      recompute(); pushLayout(); dropIsoMesh(); markDirty();
    },

    // How a shape's top is decided once its group carries a relief, and how far in it eases into the ground
    // it meets. Neither changes the footprint, so the group does not need recomputing — but both change the
    // column, so the iso and the saved document do.
    setHeightMode(id, mode) {
      const s = canvas.getShape(id);
      if (!s) return;
      if (mode === "level" || mode === "raise" || mode === "sink") s.height_mode = mode;
      else delete s.height_mode;                    // absent, not empty: a shape without the word IS ground
      pushLayout(); dropIsoMesh(); markDirty();
    },

    setSkirt(id, blocks) {
      const s = canvas.getShape(id);
      if (!s) return;
      s.skirt = Math.max(0, Math.round(blocks ?? 0));
      pushLayout(); dropIsoMesh(); markDirty();
    },

    // Whether the shape's ground joins its group's relief. Solved on the server, so nothing here recomputes
    // — the next preview is what shows it.
    setReliefScope(id, scope) {
      const s = canvas.getShape(id);
      if (!s) return;
      if (scope === "hold" || scope === "exclude") s.relief_scope = scope;
      else delete s.relief_scope;
      pushLayout(); markDirty();
    },

    // Panel-driven edits.
    selectShape(id)    { selectShape(id ?? null); },
    selectGroup(id)   { selectGroup(id ?? null); },
    rotateSelected(deg){ rotateSelected(deg); },
    deleteShape(id)    { canvas.removeShape(id); recompute(); selectShape(null); markDirty(); },
    promoteShape(id)   { promoteShape(id ?? canvas.selectedId); },
    toggleOp(id)       { const s = canvas.getShape(id); if (!s) return; s.operation = s.operation === "subtract" ? "add" : "subtract"; canvas.updateShape(s); recompute(); markDirty(); },
    toggleOverride(id) { const s = canvas.getShape(id); if (!s) return; s.override = !s.override; canvas.updateShape(s); recompute(); markDirty(); },
    toggleMirrors(groupId) { const i = groupById(groupId); if (!i) return; i.mirrors = !i.mirrors; refreshMirror(); pushLayout(); markDirty(); },
    renameGroup(groupId, name) { const i = groupById(groupId); if (!i) return; i.name = name; pushLayout(); markDirty(); },

    // Layer ops (S7b).
    addLayer()              { addLayer(); },
    switchLayer(id)         { switchLayer(id); },
    deleteLayer(id)         { deleteLayer(id); },
    renameLayer(id, name)   { renameLayer(id, name); },
    setLayerBaseY(id, y)    { setLayerBaseY(id, y); },

    // ── terrain-paint themes (docs/world-export/terrain-painting.md TP10) ──
    getThemes() { return themesState(); },
    getRoomStyles() { return roomStylesState(); },
    // A snapshot as its JSON text, the text "null" for no building at all, or null/"" to fall back to that
    // kind's built-in shell.
    setRoomStyle(kind, styleJson) {
      if (kind !== "cage" && kind !== "spawn") return;
      let parsed = undefined;
      if (styleJson) { try { parsed = JSON.parse(styleJson); } catch { parsed = undefined; } }
      roomStyles = { ...roomStyles, [kind]: parsed };
      markDirty();
      fire("OnRoomStyles", roomStylesState());
    },
    defineTheme(name) {
      const id = uniqueScopeId(Object.keys(themes), name || "theme");
      themes[id] = defaultThemeJson();
      afterThemeChange(); return id;
    },
    renameTheme(oldId, newId) {
      if (!themes[oldId]) return oldId;
      const id = uniqueScopeId(Object.keys(themes).filter(k => k !== oldId), newId || oldId);
      if (id === oldId) return oldId;
      themes[id] = themes[oldId]; delete themes[oldId];
      if (mapTheme === oldId) mapTheme = id;
      for (const L of layers) for (const s of (L.shapes || [])) if (s.theme === oldId) s.theme = id;
      afterThemeChange(); return id;
    },
    deleteTheme(id) {
      if (!themes[id]) return;
      delete themes[id];
      if (mapTheme === id) mapTheme = "";
      for (const L of layers) for (const s of (L.shapes || [])) if (s.theme === id) delete s.theme;
      afterThemeChange();
    },
    // Replace a theme's material JSON (the raw TerrainTheme). Returns an error string on invalid JSON, else null.
    setThemeJson(id, text) {
      if (!themes[id]) return "No such theme.";
      let parsed; try { parsed = JSON.parse(text); } catch (e) { return e?.message || "Invalid JSON"; }
      themes[id] = parsed; afterThemeChange(); return null;
    },
    /** Which unit a plain click picks with no group entered — "group" or "shape". The phase states it. */
    setPickUnit(unit) { canvas.setPickUnit(unit); },
    /** Arm a theme so a click on a shape paints it; "" puts the brush down. */
    /** Where the map's destroyables and cores stand, from the intent — markers the board carries, not shapes
     *  it owns, so they are handed in rather than read out of the layout. */
    setObjectives(json) {
      let list; try { list = JSON.parse(json); } catch { list = []; }
      canvas.setObjectives(Array.isArray(list) ? list : []);
    },
    setThemeBrush(id) { setThemeBrush(id); },
    setMapTheme(id) { mapTheme = (id && themes[id]) ? id : ""; afterThemeChange(); },
    // Assign (or clear, with an empty themeId) a theme to one shape — a per-shape override.
    assignShape(shapeId, themeId) { setShapeTheme(shapeId, themeId); afterThemeChange(); },
    // Assign (or clear) a theme to every shape of a group — the coarse scope, written per member shape.
    assignGroup(groupId, themeId) { setGroupTheme(groupId, themeId); afterThemeChange(); },

    // ── dressing (decoration.md) ──
    // Placing is the canvas's; the bridge exposes reading the document, editing the selection, and the
    // per-kind settings a newly placed prop starts from.
    getDressing() { return dressingState(); },
    setDressingMode(on) {
      dressingMode = !!on;
      canvas.setDressingMode(dressingMode);
      if (dressingMode) fire("OnDressing", dressingState());
    },
    selectProp(id) { canvas.dressingTools?.select(id || null); },
    deleteProp() { if (canvas.dressingTools?.deleteSelected()) afterDressingChange(); },
    /** Join the selected buildings, or take a joined one apart — the inspector button's half of `mod+g`, so
     *  the two run the same operation rather than each having their own. */
    joinDressing() { canvas.joinDressing(); },
    /** Put a library recipe in the document's registry under its name. What the inspector's picker calls
     *  before it names the key on a placement, so the document always states what its placements name. */
    pullRecipe(key, recipeJson) {
      let recipe; try { recipe = JSON.parse(recipeJson); } catch (e) { return e?.message || "Invalid JSON"; }
      canvas.dressing?.pull(key, recipe);
      markDirty();
      return null;
    },
    /** Patch the selected prop. `patchJson` is a partial prop; returns an error string on bad JSON, else null. */
    updateProp(patchJson) {
      let patch; try { patch = JSON.parse(patchJson); } catch (e) { return e?.message || "Invalid JSON"; }
      canvas.dressingTools?.updateSelected(patch);
      afterDressingChange(); return null;
    },
    /** The starting values the next prop of a kind takes — what the inspector edits with nothing selected. */
    getPropSettings(kind) { return JSON.stringify(canvas.dressingTools?.settingsFor(kind) ?? {}); },
    setPropSettings(kind, patchJson) {
      let patch; try { patch = JSON.parse(patchJson); } catch (e) { return e?.message || "Invalid JSON"; }
      canvas.dressingTools?.setSettings(kind, patch);
      fire("OnDressing", dressingState()); return null;
    },

    // ── relief (docs/world-export/relief.md) ──
    // Placing is the canvas's; the bridge exposes reading the document, editing the selected mark, the group
    // settings the marks are stated against, and the per-kind settings a newly placed mark starts from.
    getRelief() { return reliefState(); },
    setReliefMode(on) {
      reliefMode = !!on;
      canvas.setReliefMode(reliefMode);
      // The phase shows the statement and its result together, which is the only way a mark can be tuned by
      // eye — so entering it turns the contour overlay on rather than leaving it to a second toggle.
      if (reliefMode) { reliefOn = true; syncRelief(); fire("OnRelief", reliefState()); }
    },
    selectMark(id) { canvas.reliefTools?.select(id || null); },
    deleteMark() { if (canvas.reliefTools?.deleteSelected()) afterReliefChange(); },
    /**
     * Rename the selected mark. Its id is the name every finding calls it by — a seam is reported as a pair
     * of them — so it is authored rather than minted, and a clash is refused with a sentence instead of
     * silently making two marks one.
     */
    renameMark(next) {
      const tools = canvas.reliefTools;
      if (!tools) return "nothing selected";
      const refused = tools.renameSelected(next);
      if (!refused) afterReliefChange();
      return refused;
    },
    /** Patch the selected mark. `patchJson` is a partial mark; returns an error string on bad JSON, else null. */
    updateMark(patchJson) {
      let patch; try { patch = JSON.parse(patchJson); } catch (e) { return e?.message || "Invalid JSON"; }
      canvas.reliefTools?.updateSelected(patch);
      afterReliefChange(); return null;
    },
    /** Patch the group's own relief — base, reach, step, grain, and the rim it carries. Not a mark: these
     *  are what every mark in the group is stated against, so changing one moves the whole surface. */
    updateGroupRelief(patchJson) {
      let patch; try { patch = JSON.parse(patchJson); } catch (e) { return e?.message || "Invalid JSON"; }
      const groupId = reliefGroup();
      if (!groupId) return "no group selected";
      canvas.reliefTools?.updateRelief(groupId, patch);
      afterReliefChange(); return null;
    },
    /**
     * State the lift at ONE of a selected push's ring vertices — what makes a drawn ridge fall along its
     * length instead of holding level. Collapses back to the single `amount` when every vertex agrees, so
     * undoing a variation leaves the push an author started from rather than an array that happens to be
     * flat. A no-op unless a push is selected: a mark has no lift to vary.
     */
    setPushAmount(index, value) {
      const tools = canvas.reliefTools;
      const selected = tools?.selectedId ? canvas.relief.byId(tools.selectedId) : null;
      if (!selected || !isPush(selected)) return "no push selected";
      tools.updateSelected(pushAmountPatch(selected, Number(index), Number(value)));
      afterReliefChange(); return null;
    },
    /**
     * What the relief CHARGES, per group — the readback (docs/world-export/relief.md §5–§6). Asked for rather than
     * pushed: it is a second solve's worth of measurement over the same field, and an author wants it when
     * they stop to read the board rather than on every edit.
     */
    async readRelief() {
      if (!slug) return "{}";
      try {
        const res = await fetch(`/api/map/${encodeURIComponent(slug)}/sketch/relief/read`, {
          method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(handle.getState()),
        });
        return res.ok ? await res.text() : "{}";
      } catch { return "{}"; }
    },
    /** The starting values the next mark of a kind takes — what the inspector edits with nothing selected. */
    getMarkSettings(kind) { return JSON.stringify(canvas.reliefTools?.settingsFor(kind) ?? {}); },
    setMarkSettings(kind, patchJson) {
      let patch; try { patch = JSON.parse(patchJson); } catch (e) { return e?.message || "Invalid JSON"; }
      canvas.reliefTools?.setSettings(kind, patch);
      fire("OnRelief", reliefState()); return null;
    },

    // Load a persisted layout: setup + the layers[] array. A flat board is a stack of one.
    load(state, keepView) {
      const wasActive = active;
      const s = state ?? {};
      if (s.setup) applySetup(s.setup, keepView);
      themes = (s.themes && typeof s.themes === "object") ? s.themes : {};
      mapTheme = (s.mapTheme && themes[s.mapTheme]) ? s.mapTheme : "";
      roomStyles = {
        cage: s.roomStyles && "cage" in s.roomStyles ? s.roomStyles.cage : undefined,
        spawn: s.roomStyles && "spawn" in s.roomStyles ? s.roomStyles.spawn : undefined,
      };
      canvas.setDressing(s.dressing && typeof s.dressing === "object" ? s.dressing : null);
      canvas.setReliefDoc(s.relief && typeof s.relief === "object" ? s.relief : null);
      const raw = (s.layers && s.layers.length) ? s.layers : [];
      // A layer's stored shapes are partitioned on load: role-tagged shapes are the plan's structural pieces
      // (S25) — carried as a locked render-only overlay, kept out of the drawn-shape pipeline (groups, raster,
      // mirror, edit) so they can neither be reshaped nor double-cover the ground. Everything else is terrain.
      layers = raw.map((L, i) => {
        const all = (L.layout?.shapes ?? []).map(sh => ({ ...sh }));
        return {
          id: L.id || genId(),
          name: L.name || (i === 0 ? "Ground" : `Layer ${i + 1}`),
          baseY: L.base_y ?? 0,
          shapes: all.filter(sh => !sh.role),
          structural: all.filter(sh => sh.role),
          groups: [],
          savedMetas: L.layout?.groups ?? [],
        };
      });
      if (!layers.length) layers = [{ id: genId(), name: "Ground", baseY: 0, shapes: [], structural: [], groups: [], savedMetas: [] }];
      // A restore keeps the layer being drawn on, for the reason it keeps the camera: which layer is active
      // is where the author is, not what the document says.
      active = keepView && wasActive < layers.length ? wasActive : 0;
      // Cache the non-active layers' groups (for ghosts/iso); the active one is computed by recompute(true).
      for (let i = 0; i < layers.length; i++) if (i !== active) layers[i].groups = computeLayerGroups(layers[i].shapes, layers[i].savedMetas);
      canvas.clearShapes();
      for (const sh of layers[active].shapes) canvas.addShape({ ...sh });
      canvas.setStructural(layers[active].structural ?? []);
      pushActiveLayer();
      recompute(true);
      // Frame what was loaded: applySetup states the working area, and the drawing is what should be on
      // screen. An undo passes keepView, because a step back that also moves the camera reads as a different
      // board.
      if (!keepView) canvas.fitToBbox();
    },
    // The layout for the host to persist (the SketchLayoutJson shape — now layers[]).
    getState() {
      syncActive();
      return {
        setup,
        // Terrain-paint theming (docs/world-export/terrain-painting.md TP10): the registry + default ride the layout; each shape's
        // own override rides on the shape below. Omitted when empty so an unthemed sketch serialises as before.
        themes: Object.keys(themes).length ? themes : undefined,
        mapTheme: mapTheme || undefined,
        // The bound room shells, omitted when neither is picked so a sketch that never opened the step
        // serialises exactly as it did before it existed.
        roomStyles: (roomStyles.cage !== undefined || roomStyles.spawn !== undefined)
          ? { cage: roomStyles.cage, spawn: roomStyles.spawn }
          : undefined,
        // Dressing rides the same way, and is likewise omitted when empty so an undressed sketch serialises
        // exactly as it did before the phase existed.
        dressing: canvas.dressing.isEmpty ? undefined : canvas.dressing.toJSON(),
        // Relief rides top-level keyed by group rather than on the shapes, because a plan recompile
        // replaces every shape it produced and a relief is hand work a plan cannot express. Omitted when
        // nothing is stated, so opening the phase and leaving it cannot add a key to the layout.
        relief: canvas.relief.isEmpty ? undefined : canvas.relief.toJSON(),
        layers: layers.map(L => ({
          id: L.id, name: L.name, base_y: L.baseY,
          layout: {
            // Merge the locked plan pieces (S25) back in so they persist with the terrain they annotate.
            shapes: [...L.shapes, ...(L.structural ?? [])],
            groups: (L.groups ?? []).map(i => ({ id: i.id, name: i.name, mirrors: i.mirrors, shapeIds: i.shapeIds })),
          },
        })),
      };
    },
    undo() { history.undo(); },
    redo() { history.redo(); },
    groupCount() { return groups.length; },
    fitToBbox() { canvas.fitToBbox(); },
    resize() { canvas.resize(); },
    dispose() {
      clearTimeout(paintTimer);
      window.removeEventListener("pointerup", endStep);
      Keys.unregister("sketch-bridge");
      canvas.dispose();
    },
  };
  // Every handle verb that changes the document, wrapped once so a panel edit is a step and no verb has to
  // remember to be one. A pointer edit is already bracketed by the press and the release, and a step opened
  // inside an open one folds into it, so the two paths cannot double-count. `load` is not here: opening a
  // document is not an edit of the one that was open.
  const MUTATORS = [
    "setMode", "setCenter", "setBbox",
    "setHeight", "setVertexHeight", "applySlope", "setStrokeBand", "setHeightMode", "setSkirt", "setReliefScope",
    "rotateSelected", "deleteShape", "promoteShape", "toggleOp", "toggleOverride", "toggleMirrors",
    "renameGroup",
    "addLayer", "deleteLayer", "renameLayer", "setLayerBaseY",
    "setRoomStyle", "defineTheme", "renameTheme", "deleteTheme", "setThemeJson", "setMapTheme",
    "assignShape", "assignGroup",
    "deleteProp", "updateProp", "deleteMark", "updateMark", "updateGroupRelief", "setPushAmount",
  ];
  for (const verb of MUTATORS) {
    const bare = handle[verb];
    if (typeof bare !== "function") throw new Error(`[sketch-bridge] no verb named ${verb} to make undoable`);
    handle[verb] = (...args) => history.step(() => bare(...args));
  }

  return handle;
}
