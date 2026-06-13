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
- [ ] E7 — Overview: static map image missing (port `overview-renderer` to fill the canvas preview)
- [ ] E8 — Configure: canvas "leaks" xml regions + wrong canvas type — it reuses `EditorCanvas`; the
      reference uses a dedicated `configure-renderer` (islands/blocks layer preview). Re-read the
      reference impl; port the proper canvas (needs B9 pixels). Re-examine the whole Configure flow.

## C — Canvas & shared UI infrastructure
- [x] C1 — Hybrid canvas decision + interop (reused `EditorCanvas` JS via `studio-canvas.js`/`EditorCanvas.razor`)
- [x] C2 — Reusable `RegionTree` + `RegionInspector` + `Models/RegionNode.cs` + `GameColors.cs`
- [x] C3 — Editable inspector (`OnDelete`/`OnRename`) — first edit wiring
- [x] C4 — Studio design-system CSS (verbatim) + `/design` living reference page
- [ ] C5 — **Draw-tool interop** — region *creation* via draw tools (unlocks drawing in E3/E4/E5)
- [ ] C6 — Block-colour overlay (the "Blocks" toggle) — needs B4
- [ ] C7 — Side-view canvas for Build Regions Step 1, **with a draggable max-build-height slider** — needs B5
- [ ] C8 — `panel-resize` for `.sidebar-handle` drag (port `shared/panel-resize.js`)
- [ ] C9 — Kits editing UI (Teams) and per-activity status dots
- [ ] C10 — **Bug:** a region is missing from the region tree. Cylinders *do* render — the original
       symptom was misdiagnosed: `annealing_iv` has a cylinder region `blocks-filter-region` that never
       appears in the region tree (so it was assumed cylinders don't draw). Investigate why that region
       is dropped from the tree — likely the tree encoder / categorizer (`RegionCategorizer` /
       `encode_region_tree`) excludes it (filter-only region with no group? a category that the tree
       doesn't surface?). Check whether other maps drop similar filter-referenced regions.
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
- [ ] F3 (ref C13) — **Symmetry-aware authoring (source → counterparts).** Port the counterpart backend
      (`symmetry_authoring` + `regions_equivalent`/`is_counterpart` IoU on the existing
      `RegionGeometry2d`) + `POST /map/{slug}/regions/{id}/counterpart`; then the canvas accept/reject
      UI. (This subsumes **A2.** data-model §7.)
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
      activity. The full authoring UI/workflow described in the doc remains.

## S — Sketch + design (M8)
- [x] S1 — `/design` living UI reference (`Pages/Design.razor`)
- [ ] S2 — `sketch_api` (get / setup / layout / overview / export) + the sketch pages

## D — Data / ops
- [ ] D1 — Re-import the dev DB from current `map.xml` (stale `/tmp/pyfresh`: annealing_iv/bloom miss regions)
- [ ] D2 — Optional: a visible nav link to `/design`
- [x] D4 — **Dropped Bootstrap.** Migrated the dashboard (`Home.razor`) to the studio shell (topbar +
      activity rail + studio map list, links to `/editor/{slug}`); set the default layout to
      `EditorLayout`; deleted the dead `MapDetail.razor` + `MainLayout`/`NavMenu`; removed
      `wwwroot/lib/bootstrap` (8.4M) + the `index.html` link. Chrome-verified (349 maps, search, version
      tags; bootstrap.css now 404). Kept `wwwroot/css/app.css` (template loading/error-UI styles, no Bootstrap).
- [ ] D3 — Evaluate: `map_config` should probably **not** be a JSON-document artifact — consider a
      relational table (scan_layer, exclude_blocks, exclude_islands, confirmed). Potential later
      improvement; needs evaluation (weigh vs. the hybrid "JSON for irregular leaves" rule).
