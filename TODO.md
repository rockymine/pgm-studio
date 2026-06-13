# pgm-studio — Tasks

Single source of truth for status. **Legend:** `[ ]` to-do · `[~]` in progress · `[x]` done.
Task IDs are a category letter + number (e.g. `E3`, `B6`). Detailed history lives in git + memory;
this file is the live task board.

**Current focus:** M6 editor UI — all six activity *shells* are ported & Chrome-verified; the
remaining editor work is cross-cutting infra (draw-tool interop, blocks overlay), not whole activities.

## M — Milestones (high level)
- [x] M0 — Environment & scaffold (toolchain, MariaDB, solution, dev.sh)
- [x] M1 — Schema + migrations + DAL (21 tables, linq2db)
- [x] M2 — Domain + PGM codec (round-trip 350/350)
- [x] M3 — Importer (parquet + json → MariaDB)
- [x] M4 — Read API + Blazor read-only slice
- [x] M5 — Analysis port (parity-verified)
- [~] M6 — Write API + editor UI (write API complete; all 6 activity shells done; infra below)
- [~] M7 — Pipeline / world import (reader + extractors + scan + artifacts done; colours/sources left)
- [~] M8 — Sketch + design pages (`/design` done; sketch left)

## E — Editor activities (exact port of the reference studio frontend)
- [x] E1 — Editor shell: topbar + activity rail + activity-switch state machine + Overview activity
- [x] E2 — Regions (geo-tree + inspector + canvas, descendant selection)
- [x] E3 — Teams (teams CRUD + spawn list + spawn/observer assignment, spawn-filtered canvas)
- [x] E4 — Objective (wools + Add + inspector + monuments, wool-filtered canvas)
- [x] E5 — Build Regions (Step 1 max-height save; Step 2 build tree + canvas + inspector delete/rename)
- [x] E6 — Configure (3-step wizard: scan-layer / island-exclude / confirm → Overview)
- [x] E7 — Overview static map render. Ported `overview-renderer.js` + `shared/block-render.js`; bridge
      `overview-canvas.js` (mounted via `studio.mountOverview`) fetches bbox (regions/tree) + top-surface
      (B4) + symmetry (B7) and paints the pixelated surface image + symmetry axis/centre overlay.
      Verified on acapulco (full map render + purple symmetry centre).
- [x] E8 — Configure rebuilt on-par with the reference. Ported `configure-renderer.js` (dedicated
      layer/islands/symmetry preview — replaces the leaky `EditorCanvas`) via `configure-canvas.js`
      (`studio.mountConfigure`). Full 3-step flow: Step 1 layer chips + live per-layer preview
      (B9 on-demand pixels) + block include/exclude lists (B9 block-types, persisted via exclude-block);
      Step 2 island include/exclude (persists + re-runs symmetry via the B7 cache invalidation) on the
      islands canvas; Step 3 symmetry modes (confidence-sorted, detected badges) + center X/Z + reset,
      `PATCH /symmetry` on finish → Overview. Verified on acapulco (layer switch regenerates, islands
      outlines, rot_180 axis+centre, finish→Overview). **Deferred (P-series):** re-detecting islands on
      a scan-layer/block change needs a pipeline re-run the port lacks (reference uses an SSE
      `/pipeline/run`); also the scan-layer's canonical `layer.parquet` isn't layer-tagged, so a
      config `scan_layer` that doesn't match the scanned artifact previews the scanned layer.
- [ ] E9 — **New-map authoring + show drawn regions in the step they're drawn (priority).** Today a
      region drawn in an activity (C5) is categorised **"other"** and only appears in the Regions
      overview, not the step it was made in — because `RegionCategorizer` is **usage-derived and only
      runs over full map.xml**, so it cannot reflect live editor draws (nothing is wired to them yet).
      Storing "other" on draw is probably right (we can't stay in sync with the categorizer); the gap is
      **display**. Fix per `docs/contracts/region-authoring.md` (R1): replace the category-grouped tree
      with the **split view-model** (Primitives / Composed / Raw, scoped to the step by leaf-vs-compound
      + role) so a freshly-drawn primitive shows in the active step's *Primitives* panel structurally,
      regardless of category. Interim, at least display the drawn region in its step (or show
      uncategorised primitives in every step). **Per-step tools + guidance:** the Objective **"Block"
      tool = a "Monument" tool** (monuments are `block`); guide authors to define monument / wool
      location (point) / wool-room (area) / spawn point vs protection and surface exactly those tools.
      Resolves naturally once filters are wired (F1) + the tree layout changes (R1). **Goal: complete
      the editor to author/config a map with NO pre-existing xml** (stripped copy of `thunder`). Always
      validate region-authoring against **annealing_iv** + **outback_outback_edition** (author-built,
      pre-existing xml). Next up: do F1 (filter wiring) + R1 (split tree) together.

## C — Canvas & shared UI infrastructure
- [x] C1 — Hybrid canvas decision + interop (reused `EditorCanvas` JS via `studio-canvas.js`/`EditorCanvas.razor`)
- [x] C2 — Reusable `RegionTree` + `RegionInspector` + `Models/RegionNode.cs` + `GameColors.cs`
- [x] C3 — Editable inspector (`OnDelete`/`OnRename`) — first edit wiring
- [x] C4 — Studio design-system CSS (verbatim) + `/design` living reference page
- [x] C5 — **Draw-tool interop** — region creation via the editor canvas. `editor-draw-controller.js`
      already had the draw machinery; forwarded its `onRegionDraw` through `studio-canvas.js` →
      `EditorCanvas.OnRegionDraw` [JSInvokable], which builds the create payload (port of
      `drawResultToPayload`) and POSTs `/regions`, then reloads the canvas + fires `OnRegionCreated`.
      Added rectangle/cuboid/cylinder/circle/point tools to the canvas toolbar (shown when `DrawCategory`
      is set) and wired `DrawCategory`+`OnRegionCreated` in Teams(spawn)/Objective(wool_room)/
      BuildRegions(build); Regions stays read-only. Verified on acapulco: draw → `POST /regions` 200 →
      persisted (2 rectangles created + deleted in cleanup). **Note:** a freshly-drawn region is
      uncategorised → lands in the tree's "other" group until it's wired to a use (spawn/wool/etc.) —
      `region_categories` is a derived, non-persisted hint by contract (the reference SPA only shows it
      immediately because it tracks the category client-side; the port reloads from the backend).
- [x] C6 — Block-colour overlay ("Blocks" toggle) on the shared `EditorCanvas`. The reused
      `editor-canvas.js` already had the block machinery (`loadBlockLayer`/`setBlocksVisible`/
      `#renderBlockImage`); added `setBlocks(visible)` to the `studio-canvas.js` bridge (lazily fetches
      B4 top-surface, returns false when no scan data) + a `Blocks` chip in the canvas subbar wired in
      `EditorCanvas.razor`. Verified on acapulco (Regions): toggle overlays the surface colours under
      the region outlines (island fill dims), available across all EditorCanvas activities.
- [x] C7 — Side-view canvas for Build Regions Step 1 + draggable max-build-height line (B5-backed).
      Ported `sideview-canvas.js`; bridge `sideview-canvas-bridge.js` (`studio.mountSideview`) fetches
      `/segments?axis=` and wires the drag → `OnHeightChanged` (C# updates the height field, marks dirty;
      typing/Save round-trips both ways). X/Z axis toggle re-fetches; height persists across axes.
      Mounted only while Step 1 is shown (re-mounts on return). Verified on acapulco (drag up → Y rises,
      input syncs, both axes render). Gotcha: bridge must call `canvas.resize()` after mount so the
      bitmap matches the laid-out box (else the drag hit-test is off).
- [ ] C8 — `panel-resize` for `.sidebar-handle` drag (port `shared/panel-resize.js`)
- [ ] C9 — Kits editing UI (Teams) and per-activity status dots
- [x] C10 — **Resolved (root-caused to D1, no code bug).** Cylinders render fine; `annealing_iv`'s
       cylinder `blocks-filter-region` was missing from the tree only because it's **absent from the DB**
       (51 region rows, codec gives 52). Both the Python and C# codecs serialise the region from the
       current `map.xml`, and round-trip parity (350/350) rules out the encoder/tree. The dev DB was
       imported from a **stale `xml_data.json`** predating the region → it's the known stale-import issue.
       Fix = re-import (D1); no change to the codec/tree/canvas. (Diagnosed 2026-06-13.)
- [ ] C11 — **Verify:** the region inspector's edits (rename / delete / coord patch) actually write to
       the DB end-to-end across activities (only wired so far in Build Regions; not fully verified)´
- [ ] C12 — **Extract shared Blazor components** to cut markup duplication (audit 2026-06-13).
      Real candidates, by payoff:
      - **AuthorDisplay** — the author row (avatar + Mojang-resolve-on-blur name + contribution +
        remove), today inline as `OverviewActivity.PersonRow`. The reference duplicates the *same*
        row + resolve logic in `sketch-overview-panel.js`, so the sketch overview (S2) will reuse it
        → **highest payoff (cross-tool reuse)**. Bundle the name↔uuid resolve helper with it.
      - **Workspace** layout shell — `.workspace` + `.workspace-sidebar`/`.sidebar-handle`/
        `.workspace-canvas` (+ inspector) wrappers repeat in all 6 activities; a component with
        RenderFragment slots (Sidebar/Canvas/Inspector) removes the boilerplate. (Covers the listed
        "Sidebar".)
      - **ErrorToast** — `@if (error is not null) { .toast--error }` repeated in 4 activities
        (Build/Configure/Objective/Teams). Trivial, one param.
      - **SectionHeader** — ruled title + optional "+ Add" button, ~17 occurrences. (Covers "InputFields"
        partially; a labeled `FormField` for `.field`/`.field-label`/`.field-input` is lower payoff.)
      - **ActivityRail** — currently single-use in `Editor.razor`; becomes reusable once the sketch
        shell (S2) lands (the reference `sketch.html` has its own rail). Extract when S2 is built.
      Already done (no work): **EditorCanvas** (6 activities — and it already contains the canvas
      toolbar, so the listed "CanvasToolBar" needs no separate extraction), **RegionTree** (3),
      **RegionInspector** (4 — the listed "Inspector").
      NOT components → dedupe in code-behind instead (see C14): the `Post/Patch/Delete/Send` http
      trio and the `Index`/`CollectDescendants` region-tree walkers.
- [ ] C14 — Dedupe activity **code-behind** (not markup): the repeated `Post/Patch/Delete/Send`
      http-helper trio (Build/Objective/Teams) and the `Index`/`CollectDescendants` region-tree
      walkers (3–4 activities) → a shared `MapApiClient` service and/or an `EditorActivityBase` /
      static `RegionNode` helpers.
- [ ] C13 — **Bug:** canvas crashes on a map with a null `bounding_box` — `buildTransform`
       (`transform.js`) destructures `min_x` off a null `bbox`, throwing a `JSException` through
       `EditorCanvas.OnAfterRenderAsync` → Blazor "unhandled error" banner (repro: `/editor/2d`,
       any canvas activity). These are xml-only / not-fully-pipelined maps with no bbox in
       `regions/tree`. Degrade gracefully: skip render (show an empty-canvas hint) when bbox is null,
       rather than throwing. (Related data gap: TODO D1 stale dev DB / xml-only maps.)

## B — Backend / API
- [x] B1 — Region authoring + tree encoders + `GET /regions/authoring`,`/regions/tree`,`/islands` (350/350)
- [x] B2 — `RegionBoundsDeriver` (compound/transform `bounds_2d` recomputed on read)
- [x] B3 — Configure endpoints (`state` / `scan-layer` / `exclude-island`) over the map_config artifact
- [x] B4 — `GET /map/{slug}/layers/top-surface` (block-colour overlay data) → unblocks C6. Reads the
      cached `layer.parquet` artifact (`SurfaceLayer`), maps each column's (block_id,block_data) to a
      hex colour (P5 `BlockColors`), returns xs/zs/colors + bounds. Verified: 15621 pts on acapulco.
- [x] B5 — `GET /map/{slug}/segments?axis=x|z` (side-view profile) → unblocks C7. Projects the
      `layer_segment` rows onto a 2D (primary × y) depth map (`SideView`, port of `_build_depth_map`);
      nearest-depth normalised 0–255, empty=-1. Parity test vs reference on synthetic segs; verified
      on acapulco (158×104). Bad axis → 400.
- [x] B6 — `PATCH metadata` now persists authors/contributors to the `author` table (full-replace,
      skips empty-uuid rows); uuid is canonical with the resolved username cached in `author.name`.
      Added `GET /api/minecraft/player?name=|uuid=` (`MojangClient`) + Overview UI: name→uuid on blur,
      uuid→name on load. Codec emits author `name` only when set (map.xml round-trip parity preserved, 350/350).
- [x] B7 — **Symmetry detection** (`SymmetryDetector`, port of symmetry/detection.py: island-pair
      transforms + NTS polygon-IoU, confidence 0.4·support+0.6·iou) + `GET /map/{slug}/symmetry`
      (computes on demand from islands_json − excluded islands, caches as symmetry_json) +
      `PATCH /map/{slug}/symmetry` (confirm/reject/center) + Configure **state** wiring (symmetry_status
      / configure_complete; island-exclusion invalidates the cache). Importer no longer carries stale
      symmetry.json (port owns the cache). Parity: 7/7 maps vs reference (only `1`↔`1.0` JSON
      formatting differs) + synthetic unit test. **Remaining: frontend** Configure step-3 UI (show
      detected modes + axis overlay + confirm) — currently confirm-only; track under E8.
- [ ] B8 — External-source endpoints: `sources`, `import-from-url`, `configure` (`player`/Mojang done in B6)
- [x] B9 — Configure layer endpoints (port of `studio/routes/configure.py`): `PATCH /configure/{slug}/
      exclude-block` + `GET /configure/{slug}/layers/{type}/pixels` + `…/block-types` (shared `LayerData`
      = `_pixels_from_parquet`/`_block_types_from_parquet`). Scan layer → canonical `layer.parquet`
      artifact; non-scan layers (y0/bedrock/base) generated on demand by scanning the world (P6
      extractors, raw default args — exclusion is client-side) and cached as per-type parquet artifacts
      (port of `_resolve_layer_parquet`/`_generate_layer_cache`). 400 unknown type / 404|[] when no world.
      Parity vs reference on acapulco: all 4 layers' block-types match exactly (id/name/count/order);
      pixels = identical to B4 (the only colour diffs are unknown-block fallbacks, the P5 limit).

## P — Pipeline / world import (M7)
- [x] P1 — Anvil `.mca` reader (byte-exact vs Python)
- [x] P2 — Feature extractors (wool/resource/chest/spawner/segments) — 11/11 parity
- [x] P3 — `POST /map/{slug}/scan-world` (world → DB feature rows)
- [x] P4 — Surface scan + island detection + layer.parquet/islands.json/map_config artifacts (10/10 parity)
- [x] P5 — Block colours (`minecraft/colors.py` → `PgmStudio.Minecraft/BlockColors.cs`) for the surface
      render. Full known-table parity vs Python oracle (197/197, `RoundTrip --colors`) + unit tests.
      (Unknown-block fallback isn't byte-parity — Python's `hash()` isn't portable; known blocks exact.)
- [x] P6 — Ported the remaining layer extractors (`minecraft/layers.py` → `LayerExtractors.Y0/Bedrock/
      Base`; shared `BuildVolume`). Verified parity vs reference on acapulco (y0/bedrock/base block-types
      match exactly). Wired into B9's on-demand generation. Bedrock stays a distinct id==7 extractor.
- [ ] P7 — **DECISION (deferred): consolidate the layer extractors / scan passes.** Explored 2026-06-13:
      enrich `SegmentsExtractor` with per-run bottom/top block id+data to derive a *solid* base/surface
      from the single segments pass (compute once). Blockers to decide first: (1) **solid policy** — the
      layers want different ignored-block sets (Surface/Y0 = air-only; Segments = air ∪ 31 non-solid ∪
      {36}); one run-structure can't serve both. (2) endpoint-only runs can't honour user `exclude_blocks`
      or `max_build_height` (need interior blocks). (3) Y0 is interior (not a run boundary) + cheap →
      keep separate; Bedrock is a positive id==7 match → keep separate. Net: a segment-derived
      surface/base would be a *solid* layer, NOT byte-parity with the reference. Decide: redefine
      (accept divergence) vs keep exact per-layer extractors (current). See also [[A4]] geometry merge.
- [ ] P8 — **Pipeline re-run on config change** (Configure E8 deferral). The reference re-runs island
      detection (+ symmetry) via an SSE `/pipeline/{slug}/run?force_layout=1` when the scan layer or
      block exclusions change; the port has no such endpoint (scan-world is surface-only, fixed). So in
      Configure, changing `scan_layer`/`exclude_blocks` persists + updates the preview but does NOT
      re-detect islands. Need a parameterized re-scan (scan-world honouring `scan_layer`+`exclude_blocks`
      → re-run island detection → rewrite the `layer.parquet`/`islands.json` artifacts, layer-tagged so
      B9 stops mis-serving a stale canonical). Island-exclusion → symmetry re-run already works (B7).

## A — Analysis
- [x] A1 — All algorithms ported + parity-verified (categorizer 350/350; buildability/traversability/wool 10/10)
- [ ] A2 — `region_geometry` IoU / counterpart for symmetry (NTS area ops)
- [ ] A3 — Buildability endpoint perf (per-cell NTS over the grid is slow — optimise)
- [ ] A4 — **Consolidate geometry into one module** (new `PgmStudio.Analysis/Geometry/` folder). The
      reference single-sources transforms in `geometry.py` (reflect_point_2d / rotate_point_2d /
      reflect_bounds_2d / rotate_bounds_2d, the converters, IoU); the port has duplicated/parallel
      copies — point reflect+rotate in `SymmetryDetector`, NTS `Reflect`/affine in `RegionGeometry2d`,
      bounds reflect/rotate in `RegionBoundsDeriver` (+ `RegionParser`). Establish one common geometry
      module (point/bounds transforms + IoU) and route all call sites through it. (Not yet explored —
      audit the duplication first; mind the Pgm↔Analysis package boundary.)

## F — Analysis-backed editor features (reference `plans/refactor-plan.md` C-series)
The reference has the **backend done** for these; in this port the analysis *services* are ported
(M5 / A1) but most *endpoints* + all the *UI* (the D-series) are the last hurdle. Contract docs copied
under `docs/`.
- [ ] F1 (ref C9) — **Filter↔region wiring + intelligent templates.** Port `services/filter_wiring.py`
      + `routes/wiring.py` — `GET /wiring/suggestions` (scan spawns/wools/build facets → propose) +
      `POST /wiring/apply` (compose group_regions + create_filter + create_apply_rule), the 4 v1
      templates (spawn protect / wool-room defense / wool-room edit / build-void). Then the suggest/
      confirm UI. Docs: `docs/contracts/filter-region-wiring.md`, `docs/filter-use-cases.md`.
      (Filter/region/apply-rule editor services already exist; the wiring layer + routes do not.)
- [ ] F2 (ref C12) — **Wool availability/detection UI** + the two missing endpoints:
      `POST /map/{slug}/wool-sources` (query a drawn rect) + `GET /map/{slug}/wool-suggestions`
      (`/wool-availability` + `WoolSources` already done). Objectives step: draw→query, suggestion
      prompts, availability badges.
- [~] F3 (ref C13) — **Symmetry-aware authoring (source → counterparts).** **Counterpart backend done:**
      ported `symmetry_authoring` → `SymmetryAuthoring.CreateCounterpart` (+ `Geometry2d` reflect/rotate)
      and `POST /map/{slug}/regions/{id}/counterpart` (body {mode, center?}; centre falls back to the
      confirmed symmetry artifact). mirror_* → native PGM `mirror` region; rot_180 → two chained mirrors;
      rot_90 → baked primitive. Parity unit test vs reference (6 cases, all modes×types); live-verified
      on thunder_blank (block (10,10) + rot_90 → (-9,10)). **Remaining:** orbit-fill on draw (chain to
      make 1→4 for rot_90), the Objective draw integration, and the canvas accept/reject UI. The
      `regions_equivalent`/`is_counterpart` IoU detection (subsumes **A2**) is still to do.
- [ ] F4 (ref C14) — **Buildability live canvas overlay** (service + `GET /buildability` done) — UI
      overlay with the 4-class colours.
- [ ] F5 (ref C15) — **Traversability readiness-panel** (service + `GET /traversability` done) — UI.
- [ ] F6 (ref C16) — **Monument-obstruction badge** — wire `GET /map/{slug}/monument-obstruction`
      (segments-based; `SegmentIndex` + `WoolSources` ported) + the objectives-step badge.
- [ ] F7 (ref C17) — **Resource/renewable auto-config** — wire `POST /map/{slug}/resources`
      (`ResourceSources` ported) + the spawn-step "make renewable" UI.
- [ ] F8 (ref D2) — **2.5D / 3D coordinate editing** (monument point/block + cuboid Y-coords) — a
      side-depth selection view; needs design.

## R — Region authoring rework (larger, conceptualized)
- [ ] R1 — Region authoring rework per **`docs/contracts/region-authoring.md`** (authoring split:
      primitives vs composed) + **`docs/contracts/region-categorization.md`** (the categorizer spec).
      Partly done: B1 (`RegionAuthoringEncoder`) + `RegionCategorizer` (parity 350/350) + the Regions
      activity. The full authoring UI/workflow described in the doc remains. **→ now prioritised with F1
      (E9): the split view-model (Primitives/Composed/Raw, step-scoped) is what makes drawn regions show
      in their step + unblocks no-xml authoring.**

## S — Sketch + design (M8)
- [x] S1 — `/design` living UI reference (`Pages/Design.razor`)
- [ ] S2 — `sketch_api` (get / setup / layout / overview / export) + the sketch pages

## D — Data / ops
- [x] D1 — Refreshed every map's XML entities (regions/filters/teams/wools/…) from the current `map.xml`
      via `dotnet run --project src/PgmStudio.Import -- --refresh-xml` (parse → `SaveDocAsync`, the editor
      write path) — preserves world features/artifacts, no re-scan. 349 maps refreshed; annealing_iv
      51→52 regions (recovered `blocks-filter-region`, fixing the C10 symptom — verified in the tree +
      canvas). World feature/artifact rows are unchanged (worlds didn't change); re-scan via P3 if needed.
- [ ] D2 — Optional: a visible nav link to `/design`
- [x] D4 — **Dropped Bootstrap.** Migrated the dashboard (`Home.razor`) to the studio shell (topbar +
      activity rail + studio map list, links to `/editor/{slug}`); set the default layout to
      `EditorLayout`; deleted the dead `MapDetail.razor` + `MainLayout`/`NavMenu`; removed
      `wwwroot/lib/bootstrap` (8.4M) + the `index.html` link. Chrome-verified (349 maps, search, version
      tags; bootstrap.css now 404). Kept `wwwroot/css/app.css` (template loading/error-UI styles, no Bootstrap).
- [ ] D3 — Evaluate: `map_config` should probably **not** be a JSON-document artifact — consider a
      relational table (scan_layer, exclude_blocks, exclude_islands, confirmed). Potential later
      improvement; needs evaluation (weigh vs. the hybrid "JSON for irregular leaves" rule).
