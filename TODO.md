# pgm-studio — Tasks (open work only)

The live board. **It holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a
task is done, a commit lands (its message references the id), the task **leaves this file**, and a line
is added to **`FEATURES.md`** (the shipped-capability catalog). The board rules live in `CLAUDE.md`
(§ "Status & task board"); follow them — this file kept exploding when they were ignored.

Task ids are a section letter + number (`N02`, `C13`, `G14`). Ids are **stable** (commits + memory
reference them) — never renumber; new work gets the next number in its section.

## Current focus

The **Configure wizard** (`/maps/{id}/configure`) is complete end-to-end — `N00`–`N07` + the `NVAL`
gate are **landed** (`FEATURES.md`): a new map now flows intent → Map Info → World → Teams → Build →
Wools → Review & Export → a validated, downloaded `map.xml`. M0–M7 + the intent-model backend are
landed too.

The open work splits three ways:
1. **Authoring polish (N)** — only `N08` remains (monument Y via side-view + per-side focus).
2. **Layout generation (G)** — the forward-looking bulk: the lane generator → Configure, island
   detection fixes, and the decompose ground-truth tool. This is where most new work now lives.
3. **Shared editor/canvas infra (C / CV)** — serves both Configure and Edit; lower priority.

The existing **Edit** editor (`/maps/{id}/edit`, region-first, existing maps) is **frozen** — its
feature UIs are parked (§ Lower priority) until that path resumes.

---

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
- [ ] **CV8 — C# symmetry label/count helper.** Collapse `SymLabel` (identical in
  `WorldScanPhase`/`WorldSymmetryPhase`) + the suggested-team-count mapping
  (`WorldSymmetryPhase`/`TeamsPhase`/`SpawnPhase`) into one shared `SymmetryInfo`. The `SpawnPhase`
  geometry copies already route through `PgmStudio.Geom` (A4, done), not here. (Contract §6.3.)
- [ ] **CV9 — Parametrise primitive drawing styles (shape + colour + style + icon).** Edit and Configure
  draw the same primitives but diverge (canvas-interaction.md §10): `renderShape` (`render/shape-render.js`)
  has no point case so a point renders as a 1×1 `<rect>` (block-like) on Edit while Configure uses an
  ad-hoc `marker`-flag `<circle>`; colour is `var(--canvas-region)` default on Edit vs an explicit team
  colour on Configure; marker = solid vs region = dashed/translucent. Make "draw a primitive" one
  data-driven thing: a real `point` render (dot/circle) in `renderShape`, a parametrised colour + style
  (marker/outline) instead of the `marker` branch, and fix `SpawnPhase`'s hardcoded `cylinder`
  sidebar/inspector icon → match `RegionNode.Icon` (point → `dot`). (Contract §10.)

## Backend, pipeline & internals (B / P / A)

- [ ] **B8 — Import from URL (UI enablement).** The backend is **done** — `POST /api/map/import-url`
  fetches + imports an Overcast / S3 `//download` zip (with SSRF safeguards) — but the landing screen's
  download field is still **`disabled`** (`ConfigureLanding.razor`). Enable + wire the field to the
  endpoint. (The "open a local world folder" half shipped — `import-folder` + `import-candidates` +
  `/maps/new`, `FEATURES.md`.)
- [ ] **P8 — Pipeline re-run on config change.** A parameterized re-scan honouring
  `scan_layer`/`exclude_blocks` → re-detect islands → rewrite **layer-tagged** `layer.parquet` /
  `islands.json` (so B9 stops mis-serving a stale canonical). Today `PATCH /configure/{slug}/scan-layer`
  persists the change + updates the preview but does **not** re-detect islands (noted deferred in
  `ConfigureActivity`). (Island-exclusion → symmetry re-run already works, B7.)
- [ ] **A3 — Buildability endpoint perf (verify, then optimise if needed).** Per-cell NTS over the grid
  was flagged slow; the endpoint is now live and user-visible (`N03`'s buildability overlay landed).
  **First profile it under the Configure overlay** — only optimise (spatial index / batch) if it's
  actually slow in use; otherwise close.

## Layout generation (G) — auto map generation (lane sketch generators)

The "meaning → structure" engine: seed a draft map from lane primitives, then hand an editable
`SketchLayout` to the Sketch tool / Configure wizard. **The full staged plan, design decisions, and what
has already shipped are in the `project_sketch_generators` working memory**
(`/root/.claude/projects/-media-sf-repos/memory/project_sketch_generators.md`). Landed so far (`FEATURES.md`):
the lane archetypes + Geom split, the generate UI + editor prep, Bézier rounding, the Organic demo page
(`G4`), island-outline simplification (`G6` base), the lane-decompose surface + queue + overlays + select/
categorize (`G6`/`G7`/`G8`), and the `island-roles` hook (`G11`). Builds on the Sketch tool (`S2`, parked)
and the intent model (`N`).

The open work sorts into three domains:

**Generator (lane algorithm → Configure)**
- [ ] **G1 — Auto-placement straight into Configure (memory stage S3).** `POST /map/generate`: run
  `LaneMapGenerator` for a chosen archetype/seed → a full `MapIntent` (teams, spawns, wools, bridges) that
  seeds the Configure wizard for an imported/blank map (the generate-into-a-new-*sketch* half already
  shipped — `POST /api/sketch/generate`; no `→ MapIntent` bridge yet). The generator's `ObjectiveHint`s
  surface as **editable suggestions** the author confirms or moves — not baked placements.
- [ ] **G3 — Contested-middle shape language + refinement loop (memory stage S5).** The corpus gap, measured
  against the **studio's own detection** (`scripts/island_corpus.py`, N=347, over the studio-scanned corpus —
  `IslandDetector.DetectCleaned`, the generator's own pipeline; written up in `docs/generator-archetypes.md`):
  `LaneSketchGenerator.Organic` emits the 2 team islands and **0 neutral mid pieces** (passes `mids: []`),
  but **91%** of real maps carry a contested middle — median **4 gameplay-sized neutral islands** (total-island
  median **9** vs the generator's 2; validated: annealing_iv 12 / green_gem 4 / kanto 2). Add a **symmetric
  neutral mid-set**: ~4 pieces (a `MidPieces` knob — none exists in `LaneLayoutOptions`), **small/medium**
  (64–1023 blocks ≈ 4% of the team island — *not* a big central blob; only 13% of real neutrals are >25% of a
  team island), placed ~40% central / ~60% flanking between lanes, fanned by the board symmetry through
  `Assemble`'s existing mid-island slot (66% of real neutrals have a mirror twin). Reuse the noise field for
  placement + the circle/polygon shape vocabulary. Also **rework holes** — the studio detection finds holes on
  only **10%** of islands, so move `HoleChance` from per-lane 0.45 toward a per-island ~10% rate (and allow
  holes on the neutral pieces). Plus a **refine-on-feedback loop** and **seed-variation for the deterministic
  archetypes** (today only Organic varies by seed; H/Trident/Pinwheel are fixed). Re-run `scripts/island_corpus.py`
  to re-validate. Needs UI.
- [ ] **G5 — Pinwheel blade `Lane.Strip` self-overlaps on its tight curl.** The Pinwheel archetype's blade
  is a tight comma; `Lane.Strip`'s inner offset crosses itself (≈3 self-intersections in the raw simplified
  ring) → polygon-clipping renders a phantom hole in each blade. Independent of the Bézier rounding
  (`SketchLayoutPrep` via `RingRounding.Smooth` correctly *declines* to round a self-overlapping polygon, so
  rounding doesn't cause or hide it). Fix at the source — either clamp the blade curl radius against the lane
  width in `LaneSketchGenerator.Pinwheel`, or clip the inner offset in `Geom.Lane.Strip` so a tight
  centerline can't produce a self-crossing strip. Surfaced by the `SketchLayoutPrepTests` self-intersection
  regression.
- [ ] **G10 — Frontline model from buildable adjacency (research; later).** Beyond per-island lanes: detect
  which island **edges touch buildable regions** and which islands a player can **step between** (adjacency
  across the buildable / bridge space) → a better **frontline** model than per-island role tags. Builds on the
  build-area data + `G6`'s lanes. Needs design.

**Island detection**
- [ ] **G9 — Re-scan the corpus with stair-aware detection + decompose-queue UI (remaining slice).** The
  over-split **detection fix landed** (`FEATURES.md`: `CleanColumns` + `DetectStairAware`), as did the review
  flag + role classifier. What remains: (a) **re-scan the corpus** so the stored `islands.json` /
  `island_sketch_json` reflect stair-aware (the live DB + `pgm-studio-output` were generated with the legacy
  detection — needs the source worlds, `OvercastCommunity/CommunityMaps`+`PublicMaps` `ctw/`), and decide
  whether to refresh the `--islands` Python-parity oracle to match; (b) the residual `a_new_day` **isolated
  raised-decor specks** (≈37-block grid bits with no walkable connection — correctly `small` via
  `IslandClassifier`, but a per-island prune could drop them); (c) any **under-split / merged** read beyond
  `abstract` (whose stained-glass build-floor is now excluded — `FEATURES.md`): `LooksUnderSplit` is the
  catch-all flag; the residual lever if one is found is to fall through to surface-based detection when a
  cleaned-base component is a map-spanning low-Y slab; (d) a **decompose-queue UI** to show/set the review
  flag + island-health read (the collection endpoint exists; browser UI deferred).
- [ ] **G12 — Re-prune flying blobs above terrain (stair-aware regression).** Stair-aware connectivity fixed
  the over-split (disconnected islands) but **re-introduced** the stark-y-jump / flying-island problem:
  decorative masses floating above the map (dragons/birds) now merge back into the islands when a near-vertical
  surface chain bridges them (e.g. **Duality**, **mame_i_shrunk_the_pvpers**). Re-add a guard: stop joining
  across a **really big y-increase**, and/or identify & **prune blobs whose base sits well above the terrain
  band** (the old float-prune did this on `DetectHeightAware`; the stair surfaces now leak past it).
  **`max_build_height`** is a natural cut/prune ceiling — anything whose mass is above it is non-playable
  decor. Re-validate the over-split fixes (a_new_day/thunder) still hold after re-adding the ceiling.
- [ ] **G14 — island-roles: spawn/wool markers absent on some maps, duplicated on others.** Marker placement
  is inconsistent because the anchor sources aren't uniformly present and the points don't always land on a
  detected island. Measured over 109 decomposed maps: **no spawn anchor** on 2 (banana_split, checkmate),
  **no wool anchor** on 4 (columbia_ctw, down_side_up, ender_hill, enderiumctw), **>1 spawn on one island** on
  7, **>2 wool on one island** on 8. Causes: (a) `wools[].location` is the goal/proximity reference (often at
  the monument or in a wool room, **off** the walkable island) → the point anchor intersects nothing and is
  dropped (columbia_ctw/3084 have `location` but no `wool_room_region`, no spawners); (b) **not all maps have
  the spawners module** — those maps stock wool via **chests**; (c) we dropped the `only-red`/`only-blue`
  spawn-protection rules, so a spawn region that doesn't intersect its island leaves the team with no spawn
  anchor; (d) the several sources double-mark one objective. **Fix direction:** the XML already says *which*
  wools are objectives (`wools[]`); resolve each to a reliable on-island position from the **scanned data**
  rather than XML heuristics — query the actual wool **blocks** in the wool-room region
  (`GET /wool-availability` / `POST /wool-sources`, the `WoolBlockRow` table) or trust the **chest** holding
  the objective wool. One de-duplicated anchor per objective, on the island. `G13` depends on reliable spawn
  anchors from this.

**Decompose tool (ground truth → auto-cutter)**
- [ ] **G6 — Lane decomposition (marker dragging → auto-cutter).** The **manual cut surface landed**
  (`/maps/{slug}/decompose`: lasso → pick two seam points → split, iterative peeling, role tag, save
  `lane_decomposition_json`; one side only via symmetry dedup; shared editor chrome — `FEATURES.md`).
  Remaining, in order: (a) **marker dragging** — drag a lasso∩edge marker along its edge to set a non-corner
  seam (kanto's prong *bases* need a marker on the body edge, not just existing corners — needed even for 90°
  maps); (b) once enough maps are hand-cut, build the **auto-cutter** trained/validated on the gathered ground
  truth (cut at concave necks / medial axis so lanes **tile** the outline) — feeds `G3` per-lane width/length.
  The `G11` anchors **seed** it: a cut runs hub→wool tip and hub→spawn, and each resulting piece **auto-labels**
  (wool at a wool anchor · spawn at the spawn anchor · frontline where the edge meets the build region · hub =
  residual).
- [ ] **G13 — Decompose: team-coherent one-side view (the orbit dedup mixes teams).** The current view
  (`dedupBySymmetry` in `decompose-bridge.js`) is **orbit-based, not team-based**: it sorts island pieces by
  centroid (z, then x), keeps the first per symmetry orbit, removes the centroid-matched mirror twin
  (`tol=12`). It only stays team-coherent when each team's islands sit cleanly on one side of the sort axis.
  **Diagnosis of the "team A spawn + team B wool" mix:** a team's wool lane reaches *forward* past the symmetry
  centre (and in CTW you often capture the *enemy's* wool, on their side), so the z-sort **interleaves** the two
  teams. Only bites when a team's home is detected as **multiple separate islands** (spawn split from wools).
  Compounded by brittle `tol=12` centroid match, a single primary `mode` (the dual-mirror x-AND-z case), and
  near-axis pieces matching the wrong twin. **Fix: group islands by team via the `/island-roles` spawn
  anchors** — assign each island to the team whose spawn it holds / is nearest-to / connects to, then show that
  one team's group. Robust to forward-reaching wools, no axis pick (handles dual-mirror naturally). A signed
  half-plane through the symmetry centre is the cheap fallback for cleanly-separated single-axis maps. Depends
  on the marker reliability fixed in `G14`.

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

- [~] **S2 — Sketch tool: end-to-end verification.** The tool itself is **complete and shipped**
  (`FEATURES.md`): originate → Setup → draw → live islands + mirror → Finish/rasterize, with persistence
  (`SketchLayoutJson` artifact + `POST /api/sketch` + `GET`/`PUT /api/map/{slug}/sketch`, 4 integration
  tests) and rasterize (`SketchRasterizer` + `WriteSketchAsync` + `POST .../sketch/finish`, 6 tests).
  **The only open slice** is a single **end-to-end pass** of a sketched map *through* the Configure wizard
  → Edit (sketch-create → Finish → Configure → validated export) — verification, not implementation.
  Full plan: `docs/contracts/sketch-authoring.md`. (`AuthorDisplay` from C12 is reused here.)
- [ ] **P7 — [Deferred decision] Consolidate the layer extractors / scan passes.** **`ND2` settles the
  "consolidate vs keep" half: KEEP the exact per-layer extractors** — the World step uses them in distinct
  roles (cleaned `Base` = detection · `Surface` = visual aid · `Segments` = vertical), so they're a feature,
  not duplication; their per-layer default ignored-block sets (`Base` gets the expanded ND2 noise set;
  Surface/Y0 = air-only) are the solid-policy. Still open: the byte-parity sub-question — a segment-derived
  surface would **not** be byte-parity with the reference (endpoint-only runs also can't honour user
  `exclude_blocks`). Pairs with A4.
