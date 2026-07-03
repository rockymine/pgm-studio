# pgm-studio — Backlog (later)

The **long tail** — open work that isn't in the current focus. The active slice is in **`TODO.md`**;
shipped capabilities are in **`FEATURES.md`** (the Done column). Flow: **`BACKLOG.md` → `TODO.md` →
`FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` started-but-parked — **never `[x]`.** A task lives in
exactly **one** of the three files; pull one up into `TODO.md` when it becomes now/next (its id does not
change). Sections + ids match `TODO.md` — a task slots into the same section wherever it lives. Parked /
deferred items stay here, flagged inline. Board rules live in `CLAUDE.md` (§ "Status & task board").

Task ids are a section letter + number, **globally unique and stable** across all three files; never
renumber or reuse.

## Authoring (N) — the new-map intent editor (`/maps/{id}/configure`, new maps only)

The guided wizard at `/maps/{id}/configure` (UI label **Configure**) that builds a map from declarative
intent (`docs/contracts/new-map-authoring.md`; backend + every page-order step are landed —
`FEATURES.md`). **Leave the existing Edit editor untouched** — a separate surface, not a refit. Only
the focus-integration polish remains.

- [ ] **N08 — Monument Y via side-view + per-side focus.** The side-view (`SliceView`) already sets Y on
  **spawn** and **wool-spawn** (`SpawnPhase`/`WoolSpawnPhase`, `FEATURES.md`); the open slice is the rest:
  (a) wire the side-view into **`WoolMonumentsPhase`** so a monument's Y is editable, not read-only
  (lift it off y=0 onto terrain); (b) **per-side focus** — `FocusSection` is still a `/concepts` mockup;
  the canvas **fit-island** exists but not per-team quadrant framing — refine the concept so the author
  can frame one team's quadrant while working its unit. (`FocusSection`)
- [ ] **N09 — Team id should track the team's colour.** The team id is seeded from the colour first picked
  (`Id = colour.Replace(' ','-')`), but `TeamsPhase.SetColor` only updates the colour — so recolouring a
  team (e.g. red → purple) leaves `id="red"` and every id derived from it (`only-red`, `red-spawn-point`,
  the `…-red-monument` blocks, `reds-woolrooms`). Functionally fine (PGM resolves the id) but reads wrong.
  Re-derive the id on colour change and **cascade the rename** across the intent — `teams`, `islandTeams`,
  and `spawns[].team` / `wools[].owner` / `wools[].monuments[].team` — with a guard to skip the rename (just
  recolour) when the new colour-derived id would collide with another team's.
- [ ] **N11 — Spawn / wool-spawn / monument / observer Y must seat on terrain, not default to 0.**
  `WoolObjectivesPhase` snaps its seed Y to the column floor (`ColumnFloorAsync` → `GET /map/{slug}/column-floor`),
  but `SpawnPhase` defaults a new spawn/observer to **`Y=0`** (`SpawnPhase.razor.cs:92`; `PlaceAndOrbit(..., y=0)`
  :149) and `WoolSpawnPhase` never snaps — so a placed spawn sits at world-bottom and the player falls out of the
  world. On point/rect placement (**and the orbit copies**) snap Y to the terrain floor at that column via the same
  `ColumnFloorEndpoint`, for team spawns, the observer, wool spawns, and monuments. Reuse the `WoolObjectivesPhase`
  snap helper. Pairs with `N08` (monument Y editing) and `CV11` (the side-view clamp side of the same problem).

## Sketch tool (S) — parked slices

The Sketch depth pass has shipped (`FEATURES.md` — select/drag, rotate, scale/squash, split, selection
highlight); these are the parked / dormant / deferred slices.

- [~] **S2 — Sketch tool: end-to-end verification.** The tool itself is **complete and shipped**
  (`FEATURES.md`): originate → Setup → draw → live islands + mirror → Finish/rasterize, with persistence
  (`SketchLayoutJson` artifact + `POST /api/sketch` + `GET`/`PUT /api/map/{slug}/sketch`, 4 integration
  tests) and rasterize (`SketchRasterizer` + `WriteSketchAsync` + `POST .../sketch/finish`, 6 tests).
  **The only open slice** is a single **end-to-end pass** of a sketched map *through* the Configure wizard
  → Edit (sketch-create → Finish → Configure → validated export) — verification, not implementation.
  Full plan: `docs/contracts/sketch-authoring.md`. (`AuthorDisplay` from C12 is reused here.)
- [ ] **S9b — Angle/parallel snapping + droppable guide lines (parked).** S9 landed **position** alignment
  (edges/centres snap to other shapes + the symmetry centre, with guides). The remaining picture-editor bits:
  **angle/parallel** snapping (rotate a shape so its edges run parallel to another's — "hold two lanes
  parallel"), and **manually droppable** guide lines shapes snap to (vs the current auto-from-shapes). Both
  are their own work; park until needed.
- [ ] **S10 — Auto-promote rectangles on Bézier (parked, optional).** Today S4 promotes via the inspector
  button / `P`; a rectangle keeps its 8-handle resize and has no Bézier affordance. If we ever want a
  rectangle's corner to sprout a Bézier handle that *implicitly* converts it to a polygon, it needs rect
  vertex/tangent handles in `sketch-edit-controller.js` (a UX decision on resize-handles vs vertex-handles).
  Low priority — explicit promotion already covers the need.
- [ ] **S12 — Pin the Islands tree to the top of the sketch sidebar (UI polish, parked).** The residual weight
  above **Islands** is the **Layers** panel + the 12-tile **Library** palette. Collapse both behind `<details>`
  accordions (Library default-collapsed once the map has shapes), or move the Library to a toolbar popover (it's a
  "reach for a primitive" action, not persistent state). (`docs/sketch-tool-ux-review.md` P0#1;
  `docs/contracts/sketch-creation-flow.md` follow-on.)
- [ ] **S16 — Resize library primitives after placement (mostly resolved; deferred).** `S21`'s island scale
  handles now resize a **placed** polyomino / n-gon — a single non-rectangle member gets the 8 bbox scale handles —
  so the after-placement resize is **covered**. The only remaining slice is optional **drag-to-size during
  placement** (`geometry/shape-library.js` `instantiate` drops at a fixed `defaultCell`). Low priority.

## Editor & canvas infrastructure (C / CV)

Shared infra for **both** the Configure wizard (`/maps/{id}/configure`) and the frozen Edit editor
(`/maps/{id}/edit`). `C8`/`C12`/`C14`/`CV8`/`CV9` are cross-cutting (serve both surfaces); `C9`/`C11`
are Edit-specific. Full canvas spec: `docs/contracts/canvas-interaction.md`.

- [ ] **C8 — Panel resize.** The `.sidebar-handle` CSS shell exists (`css/studio/editor.css`); port the
  JS drag handler (`shared/panel-resize.js`) — no handler file exists yet.
- [ ] **C9 — Kits editing UI (Teams) + per-activity status dots.** Spawn `kit` is read/sent but has no
  edit UI; there is no status-dot system. *(Two sub-items — split if priorities diverge.)*
- [ ] **C11 — Wire + verify inspector edits across activities.** `OnDelete`/`OnRename` are wired only
  in Build Regions; the Regions/Teams/Objective inspectors are **unwired** (rename/delete silently
  no-op). Wire all three + verify rename/delete/coord-patch end-to-end.
- [ ] **C12 — Extract shared Blazor components.** (`Toast`/ErrorToast already done.) No `Shared/`
  component directory exists yet. Remaining, by payoff: **`AuthorDisplay`** (cross-tool reuse with S2 —
  bundle the name↔uuid resolve), the **`Workspace`** layout shell (sidebar/canvas/inspector slots,
  repeated in 6 activities), **`SectionHeader`** (ruled title + "+ Add", ~17 uses), **`ActivityRail`**
  (extract when S2 lands).
- [ ] **C14 — Dedupe activity code-behind.** The repeated `Post/Patch/Delete/Send` http trio
  (Build/Objective/Teams) + the `Index`/`CollectDescendants` region-tree walkers (3–4 activities) →
  a shared `MapApiClient` and/or `EditorActivityBase` / static `RegionNode` helpers.
- [ ] **CV8 — C# symmetry label/count helper.** Collapse `SymLabel` (identical in **4** files:
  `WorldScanPhase`/`WorldSymmetryPhase`/`ConfigureLanding`/`ConfigureActivity`) into one shared
  `SymmetryInfo` — that helper need only own the **label string**. For the **count**, do *not* add a new
  mapping: `Geom.Symmetry.Order(mode)` already returns it (`rot_90`→4 else→2) and is already called by
  `OrbitAssignment`/`SpawnPhase`/`WoolAuthoring`; point the 4 re-derivers
  (`BuildLayerPhase`/`ConfigureLanding`/`TeamsPhase`/`WorldSymmetryPhase`) at the leaf instead. The
  `SpawnPhase` geometry copies already route through `PgmStudio.Geom` (A4, done), not here.
  (Contract §6.3; reuse-and-synergy.md "issues".)
- [ ] **CV9 — Parametrise primitive drawing styles (shape + colour + style + icon).** Edit and Configure
  draw the same primitives but diverge (canvas-interaction.md §10): `renderShape` (`render/shape-render.js`)
  has no point case so a point renders as a 1×1 `<rect>` (block-like) on Edit while Configure uses an
  ad-hoc `marker`-flag `<circle>`; colour is `var(--canvas-region)` default on Edit vs an explicit team
  colour on Configure; marker = solid vs region = dashed/translucent. Make "draw a primitive" one
  data-driven thing: a real `point` render (dot/circle) in `renderShape`, a parametrised colour + style
  (marker/outline) instead of the `marker` branch, and fix `SpawnPhase`'s hardcoded `cylinder`
  sidebar/inspector icon → match `RegionNode.Icon` (point → `dot`). (Contract §10.)
- [ ] **C20 — Centre the staged map-overview list.** `/maps`, `/maps?stage=sketch`, `/maps?stage=configure`
  (one component, `Home.razor`) render the result list **left-aligned**: the `.workspace-scroll` wrapper has
  `max-width: 960px` but no `margin: 0 auto` (`Home.razor:39`), unlike `/maps/new` (`ConfigureLanding.razor`) and
  `/maps/new-sketch` (`SketchCreate.razor`) which centre. Add `margin: 0 auto`. One-line fix.
- [ ] **CV11 — Side-view max-Y clamp is one block short.** The draggable Y line can't reach the topmost surface
  block: `_applyHeight` clamps to `y_min + y_count - 1` (`sideview-canvas.js:281`) while the coordinate maths
  allows up to `y_min + y_count`, so you can't drag onto the highest block / match the surface. Raise the max
  bound by one. Pairs with `N11` (the placement-snap side of the same side-view problems).

## Backend, pipeline & internals (B / P / A)

- [ ] **B12 — README setup guide for users.** The repo README has no user-facing setup description. Write one:
  prerequisites (.NET 10 SDK pinned by `global.json`, MariaDB 10.11), DB/user provisioning (`pgm_studio`,
  `pgm`/`pgm_dev_pw`), running via `./tools/dev.sh` (:7894), and tests (`dotnet run --project tests/<Project>`, not
  `dotnet test`). Source the facts from CLAUDE.md's Environment / Tests sections.
- [ ] **P8 — Pipeline re-run on config change (parked escape hatch, world-present only).** A
  parameterized re-scan honouring a bespoke `scan_layer`/`exclude_blocks` → re-detect islands → rewrite
  **layer-tagged** `layer.parquet` / `islands.json`. The per-map scan-layer + custom block-exclusion UI
  has been **removed** from both editors (detection is the fixed cleaned base; the world-scanning
  endpoints are gone), so there is no longer a config-change to honour from the UI — this remains only as
  a rare, local-only override path outside the hosted flow (new-map-authoring.md §6a). (Island-exclusion →
  symmetry re-run already works without a re-scan, B7.)
- [ ] **A4 — [Consider, not perf] Vector-boolean island outlines (drop the rasterize→polygon round-trip).**
  Today island outlines come from a pixel round-trip: vector shapes → rasterize to cells → BFS → `BlocksToPolygon`
  (cells back to a polygon), done only to **avoid a C# polygon-boolean lib** (sketch-authoring.md §6). We
  already depend on NTS, so the sketch-finish island polygons *could* be computed by NTS vector boolean
  directly off the shapes (union adds, difference subs), dropping `BlocksToPolygon` + the BFS for the
  *polygon*. **Not a perf task** — the row-run fix already removed the hotspot, and the cell rasterize must
  still run for `layer_segment`/`layer.parquet` (Configure height side-view + analysis). Payoff is cleanliness
  + exact (smooth) outlines; cost is NTS boolean on the authoring path and a **staircase→smooth** outline
  divergence from scanned maps. Weigh before doing.
- [ ] **A3 — Buildability endpoint perf (verify, then optimise if needed).** Per-cell NTS over the grid
  was flagged slow; the endpoint is now live and user-visible (`N03`'s buildability overlay landed).
  **First profile it under the Configure overlay** — only optimise (spatial index / batch) if it's
  actually slow in use; otherwise close.
- [ ] **B9 — Re-import a world into an existing map (keep the authored intent).** When an author tweaks the
  terrain (e.g. adds iron inside the spawns so the renewable populates) they currently have to import the
  updated world as a *new* map and hand-copy the intent across. Add a "re-import / update world" action on
  an intent-authored map that re-scans a chosen folder/zip in place — refreshing only the world-derived
  data (`islands_json`, `resource_block`, surface/layer parquets, monument candidates) and **preserving the
  `map_intent_json`**, then regenerating. Safe while island detection stays stable (the intent references
  islands by id, and spawns/wools are world coordinates); flag the author when the island set changes so a
  stale `islandTeams` mapping can be re-checked. (Manual procedure today: copy the `map_intent_json`
  artifact + re-scan, then `PUT /map/{slug}/intent`.)
- [ ] **P7 — [Deferred decision] Consolidate the layer extractors / scan passes.** **`ND2` settles the
  "consolidate vs keep" half: KEEP the exact per-layer extractors** — the World step uses them in distinct
  roles (cleaned `Base` = detection · `Surface` = visual aid · `Segments` = vertical), so they're a feature,
  not duplication; their per-layer default ignored-block sets (`Base` gets the expanded ND2 noise set;
  Surface/Y0 = air-only) are the solid-policy. Still open: the byte-parity sub-question — a segment-derived
  surface would **not** be byte-parity with the reference (endpoint-only runs also can't honour user
  `exclude_blocks`). Pairs with A4.

- [ ] **B20 — Api.Tests: shared-schema parallel contamination (flaky counts).** The suite runs against one
  shared `pgm_studio_test` schema with parallel test classes and no per-test isolation, so row-count and
  slug assertions race and accumulate across runs — observed tonight as 8, 12, and 18 failures on different
  runs ("Expected 1 but found 92", slug dedup returning `my-sketch-3`, author-patch 404s), each time
  baseline-verified as pre-existing via stash-compare; the old note documented it as a milder 1/5/8 flake.
  Investigate + fix with real isolation: per-class database/schema (create+migrate+drop), or
  transaction-per-test rollback, or unique-prefixed fixtures with scoped assertions — pick the cheapest that
  makes counts deterministic; serializing the collection is the fallback. Update the cloud-setup.md note
  once the flake is gone.

## Layout generation (G) — auto map generation (lane sketch generators)

The "meaning → structure" engine: seed a draft map from lane primitives, then hand an editable
`SketchLayout` to the Sketch tool / Configure wizard. **The current headline direction is the
plan-then-realize model** — piece/interface plans compiled one-way into sketch + intent, rule-based
composition from expert-authored rules (`docs/contracts/layout-generation.md` + `layout-rules.md` +
`plan-editor.md`); its Phase 1 build-out (`G16`–`G21`, the plan editor / seed studio) is the current focus
in `TODO.md`. The lane-archetype tasks below are the earlier, parallel track. **The staged plan and design
decisions of that track are in the `project_sketch_generators` working memory**
(`/root/.claude/projects/-media-sf-repos/memory/project_sketch_generators.md`). Landed so far (`FEATURES.md`):
the lane archetypes + Geom split, the generate UI + editor prep, Bézier rounding, the Organic demo page
(`G4`), island-outline simplification (`G6` base), the lane-decompose surface + queue + overlays + select/
categorize (`G6`/`G7`/`G8`), the `island-roles` hook (`G11`), and the layout-generation design that resolved
the `G15` exploration. Builds on the Sketch tool (`S2`, parked) and the intent model (`N`).

The open work sorts into three domains:

**Generator (lane algorithm → Configure)**

- [ ] **G27 — Plan editor: 3-D isometric preview (reuse the sketch iso preview).** The author wants to see
  elevation spatially while planning; the sketch tool's iso preview (prism/terrain calc in
  `sketch-bridge.js`) should be reusable over the compiled plan's pieces/surfaces. Follows the G25
  height-map toggle as the richer visualization.
- [ ] **G24 — Junction-region derivation + Hubs overlay + lane chains.** Derive hubs as *internal* computed
  structure on the unioned island footprint (mouth-extrusion intersections of ≥3 access mouths — see
  `docs/contracts/plan-editor.md` §2 "Junction regions"; corners yield nothing), expose them through
  `/api/plan/inspect` and a "Hubs" editor overlay, and build **lane chains** on top (corridors between
  junctions/dead-ends) so width/length lint and depth checks measure along a whole lane rather than per
  piece. The anchor for composer-side junction placement/verification later. (layout-rules.md PC1)
- [ ] **G5 — Pinwheel blade `Lane.Strip` self-overlaps on its tight curl.** The Pinwheel archetype's blade
  is a tight comma; `Lane.Strip`'s inner offset crosses itself (≈3 self-intersections in the raw simplified
  ring) → polygon-clipping renders a phantom hole in each blade. Independent of the Bézier rounding
  (`SketchLayoutPrep` via `RingRounding.Smooth` correctly *declines* to round a self-overlapping polygon, so
  rounding doesn't cause or hide it). Fix at the source — either clamp the blade curl radius against the lane
  width in `LaneSketchGenerator.Pinwheel`, or clip the inner offset in `Geom.Lane.Strip` so a tight
  centerline can't produce a self-crossing strip. Surfaced by the `SketchLayoutPrepTests` self-intersection
  regression.

**Island detection**
- [ ] **G9 — Re-scan the corpus with stair-aware detection (remaining slice).** The over-split
  **detection fix landed** (`FEATURES.md`: `CleanColumns` + `DetectStairAware`), as did the review
  flag + role classifier. What remains: (a) **re-scan the corpus** so the stored `islands.json` /
  `island_sketch_json` reflect stair-aware (the live DB + `pgm-studio-output` were generated with the legacy
  detection — needs the source worlds, `OvercastCommunity/CommunityMaps`+`PublicMaps` `ctw/`), and decide
  whether to refresh the `--islands` Python-parity oracle to match; (b) the residual `a_new_day` **isolated
  raised-decor specks** (≈37-block grid bits with no walkable connection — correctly `small` via
  `IslandClassifier`, but a per-island prune could drop them); (c) any **under-split / merged** read beyond
  `abstract` (whose stained-glass build-floor is now excluded — `FEATURES.md`): `LooksUnderSplit` is the
  catch-all flag; the residual lever if one is found is to fall through to surface-based detection when a
  cleaned-base component is a map-spanning low-Y slab. Serves the shipped island-health / analysis
  features; the decompose-queue UI slice was dropped with the corpus-mining flywheel.
- [ ] **G12 — Re-prune flying blobs above terrain (stair-aware regression).** Stair-aware connectivity fixed
  the over-split (disconnected islands) but **re-introduced** the stark-y-jump / flying-island problem:
  decorative masses floating above the map (dragons/birds) now merge back into the islands when a near-vertical
  surface chain bridges them (e.g. **Duality**, **mame_i_shrunk_the_pvpers**). Re-add a guard: stop joining
  across a **really big y-increase**, and/or identify & **prune blobs whose base sits well above the terrain
  band** (the old float-prune did this on `DetectHeightAware`; the stair surfaces now leak past it).
  **`max_build_height`** is a natural cut/prune ceiling — anything whose mass is above it is non-playable
  decor. Re-validate the over-split fixes (a_new_day/thunder) still hold after re-adding the ceiling.

**Validation / playability**
- [ ] **G2 — Protection-aware reachability port (memory stage S4).** `MapValidity` (every-wool-needs-a-monument)
  and the `NVAL` export gate (`PreflightEndpoint`) already shipped (`FEATURES.md`). The open slice is to **port
  protection-aware reachability** from `scripts/generator/validate_play.py` to C# `Analysis/Playability`:
  today's `Traversability.Check` only tests connectivity, **not** spawn-protection-as-wall, so it passes maps
  the generator's Python validator would fail. Feed it into the `NVAL` / preflight gate.

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
  follows this (CLAUDE.md). ~19 task-id references + ~41 parity/"port of" references remain across
  `src/` + `tests/` (e.g. `ImportEndpoints`, `WorldScanPhase`, `WorldFeatureWriter`) — sweep them.
