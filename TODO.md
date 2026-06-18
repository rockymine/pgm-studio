# pgm-studio — Tasks (open work only)

The live board. **It holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a
task is done, a commit lands (its message references the id), the task **leaves this file**, and a line
is added to **`FEATURES.md`** (the shipped-capability catalog). The board rules live in `CLAUDE.md`
(§ "Status & task board"); follow them — this file kept exploding when they were ignored.

Task ids are a section letter + number (`N02`, `C13`, `ND1`). Ids are **stable** (commits + memory
reference them) — never renumber; new work gets the next number in its section.

## Current focus

M0–M5 + the M6 editor shells + the M7 pipeline are **landed** (`FEATURES.md`), and the intent-model
authoring **backend** is done. The open headline is the **new Configure wizard** (`/maps/{id}/configure`)
— a guided wizard built from the concept page, a **separate surface** from the existing **Edit** editor
(`/maps/{id}/edit`, left as-is). Routes + labels settled in `docs/contracts/routing-and-ia.md`.

1. **Design questions are settled** — `ND1` (nav/flow, §12), `ND2` (stripped World, §6a), `ND3` (landing,
   §12) and `ND4` (save model, §12) are **done**; the wizard shell, the `/maps/new` import landing, and the
   intent-gated/save-on-advance wiring (`NS`) are **landed** (`FEATURES.md`).
2. **Build the steps in page order** (`N00`→`N05` + `NVAL`), starting with **Teams & Spawns** (`N02`)
   — the recommended first real slice. Each plugs its slice into the wizard's save seam (patch `Intent`,
   call `MarkDirty`; the wizard persists on phase-advance).

Shared editor/canvas infra (C) serves both editors; the existing **Edit** (`/maps/{id}/edit`) feature UIs
(wiring, counterparts) are **parked** until that path resumes.

---

## Authoring (N) — the new-map intent editor (`/maps/{id}/configure`, new maps only)

A **new guided wizard** at `/maps/{id}/configure` (UI label **Configure**) that builds a map from
declarative intent (`docs/contracts/new-map-authoring.md`; backend = the intent model in `FEATURES.md`).
**Leave the existing Edit editor (`/maps/{id}/edit`, region-first, existing maps) untouched** — a
separate surface, not a refit.
Tasks are **in build order** and mirror the concept page's step buckets (00…07 + Validation). Each step
persists a slice of intent via `GET`/`PUT /map/{slug}/intent`, gated on a `map_intent_json` blob.

> The concept page (`Authoring.razor` + `Pages/Authoring/*`, named per its kicker — `InfoSection`=00,
> `WorldSection`=01, …) is the **visual reference** for every step. Settle `ND1`/`ND2` before building
> the steps they shape.
>
> The old "split view-model (Primitives/Composed/Raw)" plan is **superseded** for new maps
> (`new-map-authoring.md` §7: shaping activities use intent forms; the Regions activity in Edit
> (`/maps/{id}/edit`) keeps the full tree). The hand-wiring path (group→wire) is **parked** — the generator auto-wires.

**Steps — in page order, each persists its slice of intent**
- [ ] **N03 — Build.** Build height (side-view, see `N08`) → buildable layer of over-void bridges +
  holes; the generator unions them + applies the void filter. **Live buildability overlay** (uses
  `GET /buildability`, done). The drawn bridge rects reuse N02's `RectDraw`/`setAuthorRegions`
  dummy-region pattern → resize + arrow-nudge come for free (ex-CV2). (`BuildSection`)
- [ ] **NVAL — Validation gate (buildability + traversability).** Not a separate phase — the
  Build⇄Traversability loop and the export condition. Surface connected/disconnected + isolated
  spawn/wool points; on failure send the author back to Build. Uses `GET /buildability` +
  `GET /traversability` (both done). (`ValidateSection`)
- [ ] **N04 — Wools.** Colours → spawn → monuments → room. Monument count **derived** (N−1); the
  **Monument tool = the block tool** + the monument-suggester smart-detect (detector done; stateless
  serving = `F9`); the generator wires room defense / build-break / capture. Consumes the wool / monument
  (`F9`) / resource endpoints under "Backend the steps need". The drawn wool-room rects reuse N02's
  `RectDraw`/`setAuthorRegions` dummy-region pattern → resize + arrow-nudge for free (ex-CV2). (`WoolsSection`)
- [ ] **N05 — Review & Export.** `ND1` settled this as **one phase, three flow-bar sub-steps:
  Pre-flight → Region tree (`N07`) → XML (`N06`)**; **Export = the flow-bar `Next` on the XML sub-step**,
  enabled only when the pre-flight gate is open (the **409**, enforced backend-side). This task = the
  **Pre-flight sub-step**: the four checks (round-trip · mirror-consistency · buildability ·
  traversability) + the buildability/traversability maps. (`ReviewSection`; `new-map-authoring.md` §12.)

**Surfaces & integration** — `N06`/`N07` are the other two sub-steps of the Review & Export phase (`N05`).
- [ ] **N06 — XML sub-step (preview + export).** The generated XML, segmented (teams / spawns / wools /
  regions / filters / apply-rules); the flow-bar `Next` here **is Export** (gated on `N05`'s 409).
  (`XmlSection`)
- [ ] **N07 — Region-tree sub-step (read-only).** The full generated tree as the inspect/debug view of
  what the generator produced — the second sub-step, between Pre-flight and XML. (`TreeSection`)
- [ ] **N08 — Side-view + per-side focus integration.** The side-view slice is **done** (`SliceView`,
  `FEATURES.md`) — integrate it into the authoring inspector to set Y on point/block regions (lift
  spawn / monument / wool-spawn off y=0). **Fit-island** exists in parts (canvas toolbar) — refine the
  concept for per-side authoring (frame one team's quadrant while working its unit). (`FocusSection`)

> The `N04`-feeding analysis backends (`F2`/`F6`/`F7`) are **done** (`FEATURES.md`); only the
> consuming authoring UI (`N04`) remains.

## Existing editor — canvas & shared infrastructure (C)

While the Configure wizard (`/maps/{id}/configure`) is the focus and Edit (`/maps/{id}/edit`) is frozen
these are lower priority — but **shared** infra (`C8` panel-resize, `C12` components, `C13` canvas bbox
bug, `C14` helpers) serves the new authoring editor too; **`C9`/`C11`/`C18`** are Edit-specific.

- [ ] **C8 — Panel resize.** The `.sidebar-handle` CSS shell exists; port the JS drag handler
  (`shared/panel-resize.js`).
- [ ] **C9 — Kits editing UI (Teams) + per-activity status dots.** Spawn `kit` is read/sent but has no
  edit UI; there is no status-dot system. *(Two sub-items — split if priorities diverge.)*
- [ ] **C11 — Wire + verify inspector edits across activities.** `OnDelete`/`OnRename` are wired only
  in Build Regions; the Regions/Teams/Objective inspectors are **unwired** (rename/delete silently
  no-op). Wire all three + verify rename/delete/coord-patch end-to-end.
- [ ] **C12 — Extract shared Blazor components.** (`Toast`/ErrorToast already done.) Remaining, by
  payoff: **`AuthorDisplay`** (cross-tool reuse with S2 — bundle the name↔uuid resolve), the
  **`Workspace`** layout shell (sidebar/canvas/inspector slots, repeated in 6 activities),
  **`SectionHeader`** (ruled title + "+ Add", ~17 uses), **`ActivityRail`** (extract when S2 lands).
- [ ] **C13 — Bug: canvas crashes on null `bounding_box`.** `buildTransform` (`transform.js`)
  destructures `min_x` off a null bbox → `JSException` "unhandled error" banner on xml-only /
  not-fully-pipelined maps. Degrade gracefully: skip render + show an empty-canvas hint when bbox is null.
- [ ] **C14 — Dedupe activity code-behind.** The repeated `Post/Patch/Delete/Send` http trio
  (Build/Objective/Teams) + the `Index`/`CollectDescendants` region-tree walkers (3–4 activities) →
  a shared `MapApiClient` and/or `EditorActivityBase` / static `RegionNode` helpers.
- [ ] **C18 — Render spawn-protection regions on the spawn-filtered canvas.** C16 split spawn into
  point/protection, but protection regions live in the "other" tree group and don't render on the
  Teams canvas. Surface them (e.g. via the subtype facet, like the draft filter).

## Canvas interaction & de-duplication (CV)

A cross-cutting refactor of the **shared** `EditorCanvas` (used by **both** Edit `/maps/{id}/edit`
and the Configure wizard `/maps/{id}/configure`). Goal: wire up built-but-dead interactive UX
(resize, move), collapse render duplication, and formalise the controller pattern — **without
degrading behaviour**. Full technical spec: `docs/contracts/canvas-interaction.md`.

- [ ] **CV6 — Dedupe JS renderers.** Extract `renderSymmetryOverlay` + `renderIslandPaths` (next to
  `shape-render.js`) and make `EditorCanvas` use the shared `blockDataToDataUrl` — collapsing the
  3–4 copies across `EditorCanvas`/`ConfigureRenderer`/`OverviewRenderer`. **Fixes a latent bug:**
  `ConfigureRenderer` can't draw diagonal mirrors (`mirror_d1`/`mirror_d2`). Also fold the bridge
  boilerplate (`fetchJson` + mount/dispose handle) into one shared module. (Contract §6.1–6.2.)
- [ ] **CV7 — Prune / realign the public surface.** Wire the selection/bounds methods the resize+move
  features need through the bridge; **keep** the not-yet-wired feature methods (`addRegion`,
  `renameNode`, `focusRegion`, …) — they back unbuilt UI, don't delete. Update the stale
  `editor-canvas.js` doc header to match the real exposed surface. Keep `#hitTestSpawn` (it picks
  markers, not regions). (Contract §7.)
- [ ] **CV8 — C# symmetry label/count helper.** Collapse `SymLabel` (identical in
  `WorldScanPhase`/`WorldSymmetryPhase`) + the suggested-team-count mapping
  (`WorldSymmetryPhase`/`TeamsPhase`/`SpawnPhase`) into one shared `SymmetryInfo`. The `SpawnPhase`
  geometry copies (`PointInRing`/`Orbit`/`Reflect`/`Rotate`) go through **A4**, not here. (Contract §6.3.)
- [ ] **CV9 — Parametrise primitive drawing styles (shape + colour + style + icon).** Edit and Configure
  draw the same primitives but diverge (canvas-interaction.md §10): `renderShape` has no point case so a
  point renders as a 1×1 `<rect>` (block-like) on Edit while Configure uses an ad-hoc `marker`-flag
  `<circle>`; colour is `var(--canvas-region)` default on Edit vs an explicit team colour on Configure;
  marker = solid vs region = dashed/translucent. Make "draw a primitive" one data-driven thing: a real
  `point` render (dot/circle) in `renderShape`, a parametrised colour + style (marker/outline) instead of
  the `marker` branch, and fix `SpawnPhase`'s hardcoded `cylinder` sidebar/inspector icon → match
  `RegionNode.Icon` (point → `dot`). Pairs with CV6. (Contract §10.)

## Backend, pipeline & internals

- [ ] **B8 — Source ingestion (landing screen, `ND3`).** *(now)* **Open a local world folder** — list
  xml-less world folders under the maps roots (`region/*.mca`, no `map.xml`) → create the map record →
  `POST /map/{slug}/scan-world` (exists); the now-path that lets the landing screen validate new-map
  authoring on real terrain folders. *(later)* **`import-from-url`** — fetch + import an Overcast / S3
  `//download` zip link; the landing screen's download field stays disabled until this lands.
  Player/Mojang already done (B6).
- [ ] **P8 — Pipeline re-run on config change.** A parameterized re-scan honouring
  `scan_layer`/`exclude_blocks` → re-detect islands → rewrite **layer-tagged** `layer.parquet` /
  `islands.json` (so B9 stops mis-serving a stale canonical). Today Configure persists the change +
  updates the preview but does **not** re-detect islands. (Island-exclusion → symmetry re-run already
  works, B7.)
- [ ] **A3 — Buildability endpoint perf.** Per-cell NTS over the grid is slow; optimise (spatial
  index / batch). Becomes user-visible once `N03`'s buildability overlay lands.
- [ ] **A4 — Consolidate geometry into one module.** Duplication **audited** — 5 sites
  (`SymmetryDetector`, `RegionGeometry2d`, `RegionBoundsDeriver`, `RegionParser`,
  `Pgm/Editing/Geometry2d`). Establish one geometry module (point/bounds transforms + IoU) and route
  every call site through it; mind the Pgm↔Analysis package boundary. Pairs with P7.
## Lower priority / parked

Existing-Edit (`/maps/{id}/edit`) authoring features — **not** used by the intent generator (which
auto-wires), and Edit is frozen. Resume when the existing-map authoring path is picked up. Their
*backends* are done (`FEATURES.md`).

- [ ] **Wire-after-group + filter-wiring UI** (ex-`N4` + ex-`F1`). Group regions in Edit → apply
  a wiring template by role; cross-step carve-out (complement) detection; canvas Ctrl-click
  multi-select. The wiring backend (`FilterWiring` appliers + `POST /wiring/apply`) is done.
- [ ] **Symmetry counterpart accept/reject UI + IoU equivalence** (ex-`F3` + ex-`A2`). Canvas
  preview/confirm for orbit-created counterparts + `regions_equivalent`/`is_counterpart` detection for
  dedup + symmetry-violation review. The counterpart + orbit-fill backend is done (the authoring
  generator already uses orbit-fill automatically).
- [ ] **3D / side-depth selection view** (ex-`F8` 3D half). The flat side-view slice is done (→ `N08`);
  a true 3D selection view (monument point/block + cuboid Y) needs design. Later.

- [ ] **Comment hygiene sweep — purely functional comments.** Code comments must describe behaviour
  only: **no** references to the Python reference app ("port of", "mirrors the reference", parity/oracle)
  and **no** implementation-phase / task ids (`NS`, `N00`, `B8`, `P5`, `ND2`, …). New code already
  follows this (CLAUDE.md). Sweep the existing comments across `src/` + `tests/` + `tools/` to match.

- [ ] **S2 — Sketch tool.** `sketch_api` (get / setup / layout / overview / export) + the sketch pages.
  Completes M8. (`AuthorDisplay` from C12 is reused here.)
- [ ] **P7 — [Deferred decision] Consolidate the layer extractors / scan passes.** **`ND2` settles the
  "consolidate vs keep" half: KEEP the exact per-layer extractors** — the World step uses them in distinct
  roles (cleaned `Base` = detection · `Surface` = visual aid · `Segments` = vertical), so they're a feature,
  not duplication; their per-layer default ignored-block sets (`Base` gets the expanded ND2 noise set;
  Surface/Y0 = air-only) are the solid-policy. Still open: the byte-parity sub-question — a segment-derived
  surface would **not** be byte-parity with the reference (endpoint-only runs also can't honour user
  `exclude_blocks`). Pairs with A4.
