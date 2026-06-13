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
- [ ] C10 — **Bug:** cylinders not rendered on the canvas (they should draw as ellipses from `bounds`
       in `renderShape`; encoder doesn't compute `polygon_2d` for cylinders — check the bounds path)
- [ ] C11 — **Verify:** the region inspector's edits (rename / delete / coord patch) actually write to
       the DB end-to-end across activities (only wired so far in Build Regions; not fully verified)

## B — Backend / API
- [x] B1 — Region authoring + tree encoders + `GET /regions/authoring`,`/regions/tree`,`/islands` (350/350)
- [x] B2 — `RegionBoundsDeriver` (compound/transform `bounds_2d` recomputed on read)
- [x] B3 — Configure endpoints (`state` / `scan-layer` / `exclude-island`) over the map_config artifact
- [ ] B4 — `GET /map/{slug}/layers/top-surface` (block-colour overlay data) → unblocks C6
- [ ] B5 — `GET /map/{slug}/segments?axis=` (side-view profile) → unblocks C7
- [x] B6 — `PATCH metadata` now persists authors/contributors to the `author` table (full-replace,
      skips empty-uuid rows); uuid is canonical with the resolved username cached in `author.name`.
      Added `GET /api/minecraft/player?name=|uuid=` (`MojangClient`) + Overview UI: name→uuid on blur,
      uuid→name on load. Codec emits author `name` only when set (map.xml round-trip parity preserved, 350/350).
- [ ] B7 — **Symmetry detection** + `GET /map/{slug}/symmetry` + Configure step-3 wiring (currently confirm-only)
- [ ] B8 — External-source endpoints: `sources`, `import-from-url`, `configure` (`player`/Mojang done in B6)
- [ ] B9 — Configure layer endpoints — **port of `studio/routes/configure.py`** (these exist in the old
      project): `PATCH /configure/{slug}/exclude-block` (block-exclusion toggle, like exclude-island);
      `GET /configure/{slug}/layers/{type}/pixels` (configure-canvas preview); `GET …/layers/{type}/
      block-types` (block-exclusion list). Includes the `_generate_layer_cache` re-scan +
      `_pixels_from_parquet` / `_block_types_from_parquet` helpers (configure.py:155–260).
      (Already ported from configure.py: `state`, `scan-layer`, `exclude-island`. Still separate:
      `symmetry` PATCH → B7. top-surface=B4, segments=B5.)

## P — Pipeline / world import (M7)
- [x] P1 — Anvil `.mca` reader (byte-exact vs Python)
- [x] P2 — Feature extractors (wool/resource/chest/spawner/segments) — 11/11 parity
- [x] P3 — `POST /map/{slug}/scan-world` (world → DB feature rows)
- [x] P4 — Surface scan + island detection + layer.parquet/islands.json/map_config artifacts (10/10 parity)
- [ ] P5 — Block colours (`minecraft/colors.py`, ~214 lines) for the surface render

## A — Analysis
- [x] A1 — All algorithms ported + parity-verified (categorizer 350/350; buildability/traversability/wool 10/10)
- [ ] A2 — `region_geometry` IoU / counterpart for symmetry (NTS area ops)
- [ ] A3 — Buildability endpoint perf (per-cell NTS over the grid is slow — optimise)

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
