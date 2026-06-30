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

The active Sketch depth pass is in `TODO.md` (`S12`–`S19`). These are the parked / dormant slices.

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

- [ ] **B10 — Generated team ids need the `-team` suffix.** Team ids are emitted **bare** (`red`, `blue`) from
  the colour (`TeamsPhase.razor.cs:101,109` and `SymmetryExpander.cs:67`, both `color.Replace(' ','-')`), but the
  corpus/template convention (`docs/template.xml`) is `red-team` / `blue-team`. The plumbing already supports it —
  `IntentNaming.Slug()` strips `-team`, so the derived ids stay colour-based (`only-red`, `red-spawn-point`,
  `reds-woolrooms`, `…-red-monument`). So just append `-team` at the two derivation sites. Coordinate with `N09`
  (its colour-change re-derivation must produce the suffixed id too) and reuse the same collision guard.
- [ ] **B11 — XML indent should be 4 spaces.** `XmlWriter.ToXml` relies on `XElement.ToString()`'s default
  2-space indent (`XmlWriter.cs:14`). Emit **4-space** indentation (explicit `XmlWriterSettings.IndentChars`, or
  post-process), preserving the existing self-close-space fixup + trailing newline. Update the dependent consumer:
  `ReviewXmlPhase.razor.cs:67` segments the document by a `^  </tag>` (two-space) match — retune it to the new indent.
- [ ] **B12 — README setup guide for users.** The repo README has no user-facing setup description. Write one:
  prerequisites (.NET 10 SDK pinned by `global.json`, MariaDB 10.11), DB/user provisioning (`pgm_studio`,
  `pgm`/`pgm_dev_pw`), running via `./tools/dev.sh` (:7894), and tests (`dotnet run --project tests/<Project>`, not
  `dotnet test`). Source the facts from CLAUDE.md's Environment / Tests sections.
- [ ] **P9 — Sketch world-folder export (`.mca` + `level.dat`) — sketch-originated maps only.** Add an Anvil /
  `level.dat` **writer** (`AnvilRegion` is read-only today; `fNbt` can write NBT, but the region header + chunk /
  section / palette encoding + zlib + `level.dat` are net-new) that exports a map **folder** for sketch-exported
  maps, alongside the XML. Contents: (a) the rasterized **terrain** from `SketchRasterizer` columns (`[YFloor,YTop]`
  per cell + the surface block); (b) **structures at the authored positions** — spawn and wool-room **cages**, and
  monument **pedestals** matching the detected pattern (bedrock block · air block for the monument cell · stained-
  glass cap · a sign placed against the bedrock — the `MonumentSliceExtractor` geometry); (c) a `level.dat` with
  the world spawn. **Normal Configure-imported maps export XML only** (they already ship a real world). Define the
  cage / pedestal block templates once and place them at the intent's spawn / wool / monument coords.
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
- [ ] **G15 — WFC + polyomino layout exploration (alternative generator, alongside the lane archetypes).** Refine
  the layout-generation concept with a **wave-function-collapse** approach over **polyomino** tiles as a *second*
  generator, kept parallel to the lane archetypes (`G1`/`G3`, not replaced). Prototype: a polyomino tile-set +
  adjacency / constraint rules → WFC solve → a symmetric `SketchLayout` the Sketch tool / Configure can edit (same
  hand-off as `G1`). Reuses the polyomino vocabulary from the sketch library (`S8`/`S16`). Capture the refined
  concept + the tile / constraint design in a new contract/design doc and note it in the `project_sketch_generators`
  working memory. Needs design before build.

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
