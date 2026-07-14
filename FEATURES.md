# pgm-studio — Shipped features

What the app does today. Open work lives in **`TODO.md`** (the current focus) and **`BACKLOG.md`** (the
long tail); this file is the **Done** column — the catalog of **landed** capabilities, the "done" half
that used to clutter the board. One line per capability, grouped by area, with the task id(s) that
delivered it (for git traceability). This is **not** a changelog: describe the capability, not the diff.
Add an entry here the moment a task ships (it leaves `TODO.md`). Board rules: `CLAUDE.md` § "Status & task board".

> Detailed history lives in git + the auto-memory. Parity figures (350/350 codec, categorizer 350/350,
> buildability/traversability/wool 10/10, colours 197/197) are verified by the harnesses in `tools/`.

## Foundation (M0–M5)
- **Environment & scaffold** — toolchain, MariaDB, solution, `tools/dev.sh`. (M0)
- **Schema + migrations + DAL** — 21 tables, FluentMigrator + linq2db (MySqlConnector). (M1)
- **Domain + PGM codec** — `map.xml ↔ document` round-trip, lossless 350/350. (M2)
- **Importer** — feature parquet + json → MariaDB; no world re-scan needed to migrate existing maps. (M3)
- **Read API + read-only Blazor slice.** (M4)
- **Analysis port** — categorizer, buildability, traversability, wool/resource sources, symmetry,
  region geometry — all parity-verified against the Python reference. (M5, A1)

## App shell & routing
- **Map-centric URL structure** — the map is the path resource, the mode a trailing segment. Live:
  dashboard `/maps`, **Edit** `/maps/{id}/edit`, **Configure** `/maps/{id}/configure`, **Sketch**
  `/maps/{id}/sketch`, origination `/maps/new`, concept showcase `/concepts`, design system `/design`.
  Slugs are the on-disk map dir; query params hold view state only. Contract:
  `docs/contracts/routing-and-ia.md`.
- **Landing + staged dashboard** — `/` is a landing of three lifecycle cards (Sketch · Configure ·
  Edit) with live `stage-counts`; `/maps?stage=sketch|configure|edit` (default edit) is one staged
  overview (`Home.razor`) whose activity rail switches stage and whose primary action + resume target
  follow the stage. Backed by `map.stage` (`MapStage`, migration `M0004` + backfill), `GET
  /api/maps?stage=`, `GET /api/maps/stage-counts`; stage seeded/advanced at sketch-create, import, and
  sketch-finish. Editor home breadcrumbs return to the matching overview; sketch-finish lands on the
  Configure overview with a *Continue* offer rather than force-navigating into the wizard.

## Editor shell & activities (M6)
- **Editor shell** — topbar + activity rail + activity-switch state machine. (E1)
- **Regions activity** — geo-tree + inspector + canvas, descendant selection. (E2)
- **Teams activity** — teams CRUD + spawn list + spawn/observer assignment, spawn-filtered canvas;
  **Spawn Points / Spawn Protection** split by subtype. (E3, C16)
- **Objective activity** — wools + monuments + inspector, wool-filtered canvas;
  **Wool Rooms / Monuments / Spawners** split by subtype. (E4, C17)
- **Build Regions activity** — Step 1 max-build-height (side-view + draggable line), Step 2 build tree
  + canvas + inspector delete/rename. (E5)
- **Setup activity** (rail label; renamed from "Configure" to free that word for the top-level
  Configure mode) — a 2-step confirm flow (**island-exclude → symmetry confirm**) over the **reused
  `EditorCanvas`** (island-select then symmetry overlay — the same canvas the Configure World phase
  uses); finish → Overview. Detection runs on the studio-chosen **cleaned base** — no per-map scan-layer
  or custom block-exclusion choice and **no world re-scan** (aligned to the Configure World phase; the
  world-scanning scan-layer/block-exclusion endpoints were dropped so the surface is hosted-safe).
  Excluding an island recomputes symmetry from the already-detected islands. The bespoke
  `studio.mountConfigure` + `configure-bridge.js` path retired (the shared `ConfigureRenderer` stays for
  the `/maps/new` scan preview); excluded islands share the one `map_config` store across both surfaces.
  (E6, E8, C19)
- **Overview activity** — static pixel surface render + symmetry axis/centre overlay. (E7)
- **Draft bucket** — a freshly drawn region shows in the activity step that drew it, via an editor-only
  `region_drafts_json` sidecar kept **outside** the codec; it graduates out the moment wiring derives its
  real category. See `docs/region-data-flow.md`. (E10)

## Canvas & shared UI (C)
- **Hybrid canvas** — the reference `EditorCanvas` JS reused via interop (`studio-canvas.js`). (C1)
- **Reusable `RegionTree` / `RegionInspector`** + `Models/RegionNode.cs` + `GameColors.cs`. (C2, C3)
- **Studio design-system CSS** (verbatim) + the `/design` living reference page. (C4, S1)
- **Draw-tool interop** — region creation on the canvas (rectangle/cuboid/cylinder/circle/point/block
  → `POST /regions`). (C5)
- **Block-colour overlay** — the "Blocks" toggle paints the top surface under region outlines. (C6)
- **Side-view canvas** — Build step-1 depth view + draggable max-build-height line. (C7)
- **Mouse body-drag move (shared)** — a `CanvasBase` seam (`_toWorld` / `_hitMovable` / `_moveBy` /
  `_commitMove`) lets you drag a **selected** shape/region's body to reposition it, alongside arrow-nudge;
  block-snapped, threshold so a plain click still selects. Sketch drags the selected shape (→ `translateShape`
  + live island recompute); Edit drags the selected region (→ `translateBounds` + debounced save);
  non-overriding canvases (Configure/SideView/…) are unaffected by construction. The duplicated translate
  logic is consolidated into the geometry leaf — `geometry/shape.js` `translateShape` (shape model) +
  `translateBounds` (AABB) — so no canvas keeps an inline copy. (CV10)
- **Alignment snapping (smart guides)** — while body-dragging a sketch shape, its bbox edges + centre snap to
  other shapes' edges/centres and the **symmetry centre**, with dashed **guide lines** at each match (picture-
  editor style — aligns lanes). A **Snap** toggle disables it; **Alt** bypasses per-drag. Adds an absolute,
  snap-aware move path to `CanvasBase` (`_moveStart` / `_moveTo`, alongside CV10's incremental `_moveBy`);
  the sketch canvas does the snap + guide render. Position alignment only — angle/parallel + droppable guide
  lines are parked (S9b). (S9)
- **Alignment snapping on rectangle resize** — the smart guides now also fire on the sketch **8-handle
  resize** path, not just move: the dragged edge(s) snap to other shapes' edges/centres + the symmetry centre
  with a dashed guide, honouring the **Snap** toggle and **Alt** bypass. `SketchEditController.onResizeMove`
  feeds the proposed edge(s) through a `snapEdges` hook; the canvas owns the targets/guides (`#snapResize`,
  the resize counterpart of `_moveTo`) and clears the guide on release. (S19)
- **`SmartSuggestion` component** + symmetry-derived intelligent team creation (reads `/symmetry`,
  suggests 2/4 palette teams). (C15)
- **`Toast` error component** — shared across activities. (from C12)
- **Spawn-protection rendering on the Teams canvas** — protection regions (the `subtype == "protection"`
  facet from the C16 spawn split) surface in a dedicated "Spawn Protection" section and render on the
  spawn-filtered Teams canvas, not just point spawns. (C18)
- **Graceful canvas degrade on missing/degenerate bounds** — `transform.js` `fit()`/`buildTransform`
  tolerate a null `bounding_box` or a zero/non-finite world extent (xml-only / not-fully-pipelined maps,
  single-region maps where min == max), falling back to unit scale so the transform stays finite instead
  of throwing `JSException` "unhandled error". (C13, `5dda68f`)
- **Region geometry editing** — drag the 8 resize handles (rectangle/cuboid) on the canvas *and* type
  exact coords in the inspector; both persist (`PATCH /regions/{id}` bounds/coords) and stay in sync via
  the shared `Models/RegionEdits` (`EditorCanvas` raises `OnGeometrySaved`; the host persists). Wired in
  all four Edit activities. `docs/contracts/canvas-interaction.md` §3. (CV1)
- **Arrow-key region nudge** — the selected rectangle/cuboid moves 1 block (Shift = 16) with the arrow
  keys; a single `document` keydown handler on the shared `EditorCanvas` (guards: canvas not visible,
  focus in a field, nothing selected) translates it live and persists through the same
  `onBoundsSave`/`OnGeometrySaved` path (debounced) — so Edit (PATCH) and Configure (intent + re-orbit)
  both get it. §4. (CV3)
- **Canvas interaction controllers** — `EditorCanvas` delegates every interaction mode to plain
  controllers (state-accessor closures + callbacks; the canvas forwards its `CanvasBase` hooks):
  `EditorDrawController` (draw), `EditorEditController` (8-handle resize + arrow-key move), and
  `EditorSelectController` (click-select modes: region / island, each a registered picker — so
  `_onCanvasClick` is one dispatch, not an `if`-chain). The shared abstraction the S2 sketch port
  reuses. §5. (CV4, CV5)
- **Shared renderers** — one `renderSymmetryOverlay` (`shared/symmetry-render.js`, all 6 symmetry
  types) replaces the three drifted copies in `EditorCanvas`/`ConfigureRenderer`/`OverviewRenderer`,
  **fixing** the latent bug where `ConfigureRenderer` couldn't draw diagonal mirrors and
  `OverviewRenderer` couldn't draw rotations or diagonals. `EditorCanvas` block + island rendering now
  go through the shared `blockDataToDataUrl` / `polyToPath`, and all four interop bridges share one
  `fetchJson` (`shared/fetch-json.js`). §6.1. (CV6)
- **Unified intent primitives + forgiving select** — Configure renders all intent geometry as one kind of
  thing: dummy regions in `#nodeMap` (protection rectangles *and* spawn points), picked by the single
  `#hitTest`. That picker gained a **2-block margin** (smallest containing region, else nearest within 2
  blocks) so 1-block primitives (points/spawns) are forgiving to click everywhere. The bespoke spawn path
  — `#hitTestSpawn`, the `#authorSpawns` marker layer, `setAuthorSpawns`, the `spawn` select mode,
  `onSpawnPick` — is gone. §2.

## Backend / API (B)
- **Region authoring + tree encoders** — `GET /regions/authoring`, `/regions/tree`, `/islands`. (B1)
- **`RegionBoundsDeriver`** — compound/transform `bounds_2d` recomputed on read. (B2)
- **Configure endpoints** — `state` / `scan-layer` / `exclude-island` / `exclude-block` /
  `layers/{type}/pixels` / `…/block-types`, over the `map_config` artifact. (B3, B9)
- **Top-surface layer endpoint** — `GET /layers/top-surface` (block-colour overlay data). (B4)
- **Segments endpoint** — `GET /segments?axis=` side-view profile (windowed, ±X/±Z). (B5)
- **Metadata write + Mojang resolve** — authors/contributors → `author` table; `GET /minecraft/player`
  resolves name↔uuid. (B6)
- **Symmetry detection** — `SymmetryDetector` + `GET`/`PATCH /symmetry` + Configure wiring. (B7)
- **Symmetry table** — promoted from the `symmetry_json` blob to a first-class `symmetry` table (`M0003`):
  hybrid shape (scalar `status`/centre/chosen-mode columns + `modes_json`; `center_cell`/`primary` derived
  on read via `SymmetryStore`). GET/PATCH + the orbit/counterpart/Configure consumers read columns, not a
  blob. Has the authoring World-step inputs (`excluded_islands_json`, `detection_layer`) ready for `N01`.
  Settles `D3` (new-map-authoring.md §6b). (NS)

- **Schema-drift guards** — the API asserts the FluentMigrator `VersionInfo` is at the newest known
  migration at startup and fails fast naming the pending versions + the exact fix command (never
  auto-applies); `PgmStudio.Import` resolves its connection string the same way the API does
  (`PGM_STUDIO_DB` override → `ConnectionStrings:PgmStudio` via appsettings / the API's User Secrets /
  env) and echoes the source; `--migrate-only` prints an explicit applied/up-to-date summary so a silent
  no-op is impossible to misread. `docs/cloud-setup.md` updated. (B19)
## Pipeline / world import (M7)
- **Anvil `.mca` reader** — byte-exact vs Python. (P1)
- **Feature extractors** — wool / resource / chest / spawner / segments, 11/11 parity. (P2)
- **`POST /scan-world`** — world → DB feature rows. (P3)
- **Surface scan + island detection** — `layer.parquet` / `islands.json` / `map_config` artifacts. (P4)
  `IslandDetector.BlocksToPolygon` unions one rectangle per maximal horizontal run (not one square per
  cell) — identical output, ~50× fewer GEOS inputs; cut sketch-finish from ~700ms to ~150–200ms (warm).
- **Block colours** — `BlockColors`, 197/197 known-table parity. (P5)
- **Layer extractors** — `Y0` / `Bedrock` / `Base` (+ shared `BuildVolume`), generated on demand and
  cached. (P6)
- **Cleaned-base island detection** — `LayerExtractors.CleanBase` (corpus-derived noise exclude:
  water/lava/foliage/redstone/cobweb) + `IslandDetector.DetectHeightAware`/`DetectCleaned`
  (height-aware connectivity prunes floating builds over void; y0/bedrock fallback). The new-map
  detection layer (ND2 §6a); validated on real worlds via `--clean-base-render`
  (`scripts/render_clean_base.sh`). (A5)
- **Stained-glass build-floor exclude** — a low stained-glass slab is a build-region floor (PGM auto-detects it
  like the invisible block-36 marker; such maps remove it pre-game via a `destroyables` mode-change and define
  their build region with a void filter — confirmed in `abstract`'s map.xml). `LayerExtractors.CleanBaseExclude`
  now drops stained glass (95) beside {36}; since the base read is bottom-up-lowest, only glass *floors* are
  affected (decorative glass walls/windows above other blocks are untouched). Un-merges the under-split teams on
  abstract/abstract_remix (one ~4937 blob → symmetric team pairs) with no change to the tested healthy or
  over-split maps. (G9)
- **Stair-aware island detection** — `LayerExtractors.CleanColumns` reports each column's lowest cleaned-solid Y
  **plus every standable surface**, and `IslandDetector.DetectStairAware`/`DetectCleanedStairAware` join adjacent
  columns when any surface pair is within a step — so a walkable staircase keeps a raised structure attached to
  its terrace instead of the cleaned base reading the high floor as a cliff and carving it off. Including the base
  level makes it strictly additive to the height-aware base connectivity (only merges over-split fragments; never
  splits a team island or changes the float prune), so it is the default detection in `WorldFeatureWriter` /
  `--scan-out` / `--island-sketch`. Validated on re-scanned worlds via `--island-stairaware`: a_new_day 17→14,
  a_new_day_ii 9→5, thunder 33→17, with team-island count + symmetry preserved on every map (kanto/green_gem/
  two-quarter/vegas/mame). The legacy `DetectCleaned` remains for the `--islands` Python-parity harness. (G9)
- **Semantic island role classifier** — `IslandRoleClassifier` tags each island by gameplay role from its
  objective anchors (not size): **team** (holds a spawn — the team `spawns[].region`),
  **objective** (holds a wool — `wools[].location`, wool-room region, or a wool-*dispensing* spawner region;
  economy spawners like gold nuggets are skipped, and the capture **monument** is never an anchor),
  **neutral** (no anchor but intersects the build region — a stepping-stone/mid), **decorative** (no anchor,
  outside the build region — e.g. an observer island). Anchors are resolved to footprints via
  `RegionGeometry2d` and tested by intersection (robust to concavities); build regions come from
  `RegionCategorizer`. Surfaced on `GET /map/{slug}/island-health` as `roles`. Validated against the corpus
  ground truth (kanto/thunder/annealing_iv/a_new_day/mame/green_gem). (G9)
- **Island size classifier + detection-health triage** — `IslandClassifier` buckets detected islands by size
  into `major` (team islands, ≥25% of the largest), `neutral` (gameplay-sized mids/stepping-stones, ≥64 blocks),
  and `small` (sub-gameplay specks / over-split fragments); corpus-validated (kanto 2 majors, green_gem 2+2,
  annealing_iv 4+8). `LooksUnderSplit` flags the merged-teams failure mode (majors < teams, e.g. `abstract`).
  Surfaced via `GET /map/{slug}/island-health` (roles + counts + `underSplit`) and the human review flag
  `GET`/`PUT /map/{slug}/island-review` (`{status,note}`; echoed per map in `GET /decompose/queue` as
  `reviewStatus`). (G9)
- **Island-roles hook (`GET /map/{slug}/island-roles`, G11)** — the decompose-workflow integration hook the
  G6/G7/G8 UI tasks consume. Per detected island in island-sketch order: `{ index, role, blockCount,
  anchors:[{kind:"spawn"|"wool", x, z}] }` plus the `buildRegion` outline as GeoJSON. `IslandRoleClassifier.Assess`
  reports each island's role + the anchors it carries in one pass (`Classify` delegates to it); the endpoint
  distance-clusters a wool's several footprints (location + room + spawner) into one lane target, so a symmetric
  map yields symmetric anchors. Shared `IslandRoleData` plumbing with `island-health`. Reflects the new detection
  on re-scanned maps. (G11)
- **Headless scan-to-files (`--scan-out` / `--scan-out-all`)** — the RoundTrip tool runs the studio's own
  extractors with no database and writes an importer-ready per-map directory (`wools/resources/chests/
  spawners/layer_segments.parquet`, `monument_candidates.parquet` from the F9 `MonumentSuggester` gather,
  `layer.parquet`, `islands.json` from the cleaned base + y0→bedrock fallback, `map_config.json`, and
  `xml_data.json` from the studio's parser). The heavy world scan runs on a fast host;
  `dotnet run --project src/PgmStudio.Import <outRoot>` ingests the cheap files into MariaDB (including
  monument candidates), or `… <outRoot> --monuments-only` re-ingests just the monument-candidate gather for
  maps already in the DB. Verified end-to-end (row-counts + doc round-trip).
- **Supported map range (enforced in `MapParser`)** — the parser accepts **proto >= 1.4.0** only (PGM's
  id-based regions/filters/kits floor) and rejects **modern worlds** (`min-server-version >= 1.13.0`, whose
  post-"flattening" palette chunks the Anvil reader can't decode), throwing `UnsupportedMapException` with a
  clear reason. `--scan-out` parses + validates `map.xml` up front (before the world scan), so a rejected map
  leaves no partial output; `--scan-out-all` skips-and-logs it and continues. Over the 350-map CTW corpus only
  `kytriak_te` (proto 1.3.0) and `allure` (1.21.10 world) are excluded. Stated in CLAUDE.md.
- **Surgical islands re-ingest (`--islands-only`)** — replaces each map's `islands_json` artifact from the
  re-scanned `islands.json` files and refreshes the derived `island_sketch_json`, **without** the full
  re-import that drops the map row and FK-cascades away its human authoring artifacts (intent / decomposition /
  review / sketch). Only `islands.json` changes between re-scans of the same world, so this is the minimal
  update; skips dirs not yet in the DB. Shares the Douglas-Peucker sketch derivation with `--store-island-sketch`
  (`IslandSketchArtifact`). Used to land the stair-aware re-detect across the corpus (348 maps updated).

## New-map authoring — intent model (backend) ★ headline direction
The forward path (**meaning → structure**): the author states intent and the generator emits the
region/filter/apply-rule graph. Backend landed + unit-tested; the **wizard shell UI + intent wiring are
landed**, with the per-phase bodies the open work (TODO §Authoring). Contract: `docs/contracts/new-map-authoring.md`.
- **Configure wizard shell (UI)** — `/maps/{id}/configure`: activity rail (six phases) + flow bar (phase
  identity · sub-steps · Back/Next) + three-panel workspace, driven by a phase/sub-step state machine. On
  entry it loads the stored intent (`GET /map/{slug}/intent`) and derives the **rail gating from its slices**
  — a phase is done (green dot) when its slice is present (`meta`·`symmetry`·`teams`·`build`·`wools`), and the
  unlocked range is **purely slice-derived** (the leading run of done phases — no session "furthest"), so
  revisiting a part-authored map reopens exactly its progress and you can't rail-jump past it.
  The `/maps/new` landing (Import: Source → Found → Plan) originates a map and hands off to Map Info. Map
  Info is a real phase body (`N00`); the rest are scaffolds the `N01`–`N05` tasks fill. Reuses
  `ConfigureLayout` across both surfaces. (NS)
- **Wizard save model (ND4)** — a phase **saves on advance**: leaving it (Next / rail jump) `PUT`s the whole
  intent (one idempotent regenerate) when dirty, a clean phase is a no-op, and a fresh slice unlocks the next
  phase. Forward `Next` is **gated on the current phase being complete** (`CanAdvance`; phase bodies define
  completeness, scaffolds default to true), so you fill a phase in before progressing. The only affordance is
  a topbar text indicator — **Saved · Saving… · Unsaved** (no icons); done is the rail's green dot. Phase
  bodies patch `Intent` + call `MarkDirty` via a cascaded wizard ref. Doc: §12. (ND4, NS)
- **Map Info phase (N00)** — the identity slice: map name + authors + contributors → intent `meta`, edited
  on a form that writes the working intent live and gates `Next` until there's a name and ≥1 **verified**
  author. Usernames are checked against Mojang **on blur** (`GET /minecraft/player`, reusing the Overview
  editor's flow) → canonical name + mc-heads avatar head, or a flagged error; only verified names reach the
  intent, so a bad username can't survive into the map. Version / mode / objective are shown locked
  (generator-derived); the server re-resolves usernames → UUIDs on the save `PUT`. (`InfoPhase`; N00)
- **World · Scan sub-step (N01)** — a read-only review of the extracted world: the centre panel is the
  reused edit-page `EditorCanvas` (its navigation toolbar — pan/zoom · fit island · reset — and its island
  base ↔ surface "Blocks" layer toggle), with a cleaned-base summary (the corpus-fixed noise exclusions)
  and a detection summary (layer · island count · detected symmetry). Writes no intent. (`WorldScanPhase`; N01)
- **World · Islands sub-step (N01)** — review the detected islands and exclude the stray ones (decor /
  observer towers). Islands are selectable from the list **or by clicking the canvas** (the `EditorCanvas`
  gained island hit-testing + an accent-border highlight, gated so the editor's region selection is
  unchanged); the inspector shows centre / block count / Exclude·Include. Excluding reuses
  `PATCH /configure/{slug}/exclude-island` (re-runs symmetry, no re-scan) and dims the island; saves
  instantly (topbar Saving… → Saved). (`WorldIslandsPhase`; N01)
- **World · Symmetry sub-step (N01)** — confirm the detected symmetry (or pick another / none) + its
  centre → the World intent slice (`intent.symmetry`), which the generator orbit-fills from. The canvas
  (`EditorCanvas` symmetry mode — base layer only) draws the axis/centre overlay; the inspector surfaces the
  suggested team count. Persists on phase-advance, which marks World done + unlocks Teams. (`WorldSymmetryPhase`; N01)
- **Teams · step 1 sub-step (N02, "Teams & island assignment")** — create the teams (a Smart Suggestion
  proposes the count from the confirmed symmetry → palette teams) + edit name/colour + Max Players →
  `intent.teams` / `maxPlayers`; and tag islands to teams by clicking them on the canvas (tinted that
  team's colour) → `intent.islandTeams` (authoring aid the Spawn step consumes). Canvas = reused
  `EditorCanvas` in island-select mode, now **point-in-polygon** island hit-testing + **Select tool by
  default** (both also improve the World · Islands step). (`TeamsPhase`; N02)
- **Teams · Spawn point sub-step (N02)** — the **point tool** drops team 0's spawn (island-aware: it
  takes the clicked island's team) and the confirmed symmetry orbit-fills the rest, each orbit spawn
  reassigned by the island it lands in; the **select tool** picks a placed marker (world-space hit-test,
  like the editor's). The inspector edits X/Y/Z/Yaw — editing the authored spawn's X/Z re-derives the
  orbit; the reused **side-view** (`SliceView`) sets the Y on the spawn's terrain, **shared across the
  orbit**. The **observer (`<default>`) spawn** is shown + editable with the same treatment (a neutral
  marker, the select tool, the inspector X/Y/Z/Yaw, and the side-view Y-snap) — defaulted to the map
  middle so observers don't fall in at 0,0,0; with it selected the point tool relocates it (no orbit).
  **Yaw auto-aims**: team spawns look at the map middle, the observer at a team spawn (`Geom.Heading`),
  recomputed on any move, manual edits stick. → `intent.spawns` + `intent.observer`. (`SpawnPhase`; N02)
- **Teams · Spawn protection sub-step (N02)** — the **rectangle tool** draws a protection zone over a
  spawn; it's **owned by the team whose spawn it covers** and the confirmed symmetry orbits it onto the
  rest, each copy **owned by the team whose spawn IT covers** (shared `OrbitAssignment.ByCoveredAnchor`
  — spatial containment, never orbit order, so no spawn lands in an enemy's zone). Zones are **dummy
  regions** on the reused canvas; the authored zone is editable, the **orbit copies are non-editable ghost
  previews** (one-way derivation). Edits route to `intent.spawns[].protection`; the inspector shows the
  generator's **Auto-wiring (derived)** (`enter=only-<team>` + `block=never`). (`ProtectionPhase`; N02)
- **Build · Build-height sub-step (N03)** — the max-build-height cap, set with the **shared
  `BuildHeightSideview`** — the Edit Build Regions step-1 side-view (`studio.mountSideview` / `SliceView`,
  axis toggle + draggable line) **extracted into one component used by both surfaces**, so they're
  identical. Number input ↔ canvas line stay in sync; → `intent.build.maxHeight`. (`BuildHeightPhase`; N03)
- **Build · Buildable-layer sub-step (N03)** — the **rectangle tool** draws over-void bridges (areas) and
  no-build holes (the negative-rectangle / complement case); a Bridge/Hole toggle picks which. Build areas
  have no team identity, so it stores **authored-only** (`intent.build.areas`/`holes`) and the **canvas**
  renders the symmetry mirror as ghost previews in JS (`setAuthorMirror`); `BuildGenerator` orbits + unions
  them, complements the holes, and wraps the void-enforcement negative. (`BuildLayerPhase`; N03)
- **Build · live buildability overlay (N03)** — a **Buildable** chip on the canvas sub-bar toggles a
  translucent per-column **verdict heatmap** (`GET /buildability`): green buildable · orange void-denied ·
  red never · yellow restricted. Reuses the block-overlay's pixelated `<image>` renderer (the grid → one
  PNG), sits below the authored bridges, and re-fetches on each toggle-on so it reflects the saved build
  slice. A sidebar **legend** (colour → plain-language meaning + what to do) shows while the overlay is on
  (`OnBuildableToggled`). (`EditorCanvas` `ShowBuildable` + `setBuildability`; `BuildLayerPhase`; N03)
- **Wools · Objectives sub-step (N04)** — a **detect-and-confirm** objectives list, not a colour-picker.
  On entry the world is scanned (`GET /monument-suggestions` map-wide + `POST /wool-sources`): signed
  monuments ("Place the X Wool here!") name each objective colour and give the capturing team (the island
  the monument sits on → owner = the complement); physical wool clusters give the source location; physical
  wool **no monument names** (or sitting in a team's own spawn) is flagged **decorative and excluded by
  default** (re-includable). The author confirms/rejects, fixes an owner, recolours, or hand-adds a missing
  wool (the ~7% detection can't find). Owner inference is **client-side** (`Polygon.PointInRing` + the
  `islandTeams` assignment). Writes `intent.wools` (owner + colour + a floor-snapped seed spawn + the
  detected monuments) — the seed Y is snapped onto the terrain floor at the wool's column via the new
  `GET /map/{slug}/column-floor` (segment top at/below the wool's base), not the floating pile centroid.
  (`WoolObjectivesPhase`; `WoolAuthoring` shared helper; `ColumnFloorEndpoint`; N04)
- **Wools · Spawn sub-step (N04)** — confirm/adjust each wool's source point (seeded by the detected
  cluster centroid) + set its Y on the reused side-view; positions **orbit** like the team-spawn step
  (editing an anchor-team wool re-derives its mirror partners by mirrored position — colour/owner untouched,
  so green's mirror stays the real yellow). (`WoolSpawnPhase`; N04)
- **Wools · Monuments sub-step (N04)** — each wool needs **N−1** monuments (one per enemy team), modelled
  as the expected capturers; the scan pre-fills the signed pedestals. **Box** a cluster → `monument-suggestions`
  routes each hit to its colour's wool (capturing team = its island); an empty box drops a manual monument;
  one-click whole-map **Detect**. Capturing team editable per row. (`WoolMonumentsPhase`; N04)
- **Wools · Room sub-step (N04)** — the **rectangle tool** draws a wool room, owned by the wool whose spawn
  it covers; the symmetry orbits it to the partner wools via the shared **`OrbitAssignment.ByCoveredAnchor`**
  (anchors = the wool spawns), accumulating across wools so a team that defends several wools gets each room
  (authored editable, orbit copies ghost). Shows the generator's **Auto-wiring (derived)** preview
  (`enter`/`block`=`not-<owner>` + `capture ×N`). (`WoolRoomPhase`; N04)
- **WoolGenerator multi-wool-per-team + partial-intent fixes (N04)** — (1) `not-<owner>` / `only-<owner>`
  room filters are per-team, not per-wool, so a team defending several wools now **shares** them (both
  creations guarded); a second same-owner wool previously collided on the filter id (HTTP 409). (2)
  `WoolIntent.Room` is **optional** (then nullable; now an empty `List<Rect>` — see N10) — a roomless wool
  (the author hasn't drawn its room yet) still generates its objective + monuments and skips the room region /
  spawner / wiring, instead of failing intent deserialization. Verified end-to-end on n00_demo (2-team
  `mirror_x`, 2 wools/team): 4 wools + 4 monuments, valid CTW XML (`<wool team>` = the monument-derived
  capturer, as PGM requires). (N04)
- **Multi-rectangle wool rooms + spawn protection — union footprints (N10)** — a room/protection is now a
  **union of rectangles**, not one: `WoolIntent.Room` and `SpawnIntent.Protection` are `List<Rect>`. The
  generators emit the buildable-area pattern — a lone rect is the region itself (`{slug}-spawn` / `{color}-wool`),
  several become numbered children (`-1…-n`) unioned into it — and the wool/spawner/enter/block wiring
  references the union. `SymmetryExpander` orbits **every** rect (`.Select(TransformRect…)`), `Preflight`
  checks `.Count > 0`, and `ResourceRenewables` expands a union to its child boxes for in-spawn ore detection.
  In Configure the **Protection** and **Wool Room** phases accumulate: the first rect over a spawn selects the
  unit, further rects while it's selected **add** to it (extras orbit by the primary's step via the new
  `OrbitAssignment.ByCoveredAnchorSet`), and the inspector lists each rect with a per-rect delete (× / Clear).
  Verified live (thunder_blank, `mirror_x`): a 2-rect spawn + 2-rect room orbit-fill into valid unioned XML on
  both teams. (`MapIntent`, `TeamsGenerator`, `WoolGenerator`, `SymmetryExpander`, `ProtectionPhase`,
  `WoolRoomPhase`, `OrbitAssignment`; N10)
- **Wool-room wiring — the validated template structure (`docs/template.xml`)** — `WoolGenerator` now groups
  the rooms per defending team into a `<team>s-woolrooms` union (all under a top `woolrooms` union) instead
  of per-wool rules, and replaces the blanket `block=not-<owner>` ("forbid everything") with a shared
  **`woolrooms-filter`** whitelist: a single `<any>` allowing the spawn-kit blocks (`wood`, `stained clay`) +
  player-placed `water`/`stationary water`, and breaking the entrance decoration (`web` cobweb, `stained
  glass` + `stained glass pane`). The room edit rule is `block = all(not-<owner>, woolrooms-filter)` (per
  team, `<team>s-woolrooms-filter`), with `enter=not-<owner>` — so attackers may edit only the whitelisted
  materials, not grief everything. Enabled by a serializer fix: `XmlWriter` now keeps a filter top-level when
  an **apply rule / renewable references it** (`ExternalFilterRefs`), so `not-<owner>` resolves from both its
  enter rule and the `all`. Verified on n00_demo (regenerated). (N04)
- **Review & Export · Pre-flight sub-step (N05; folds in the NVAL validation gate)** — the export gate.
  `GET /map/{slug}/preflight` runs the four generated-map checks server-side and returns the export verdict:
  **round-trip** (the document survives the export codec — `FromDict → XmlWriter → re-parse`, codec-idempotent,
  no field lost) and **mirror** (`RegionCategorizer.DeriveFacets` recovers every declared classification —
  spawn/protection · wool/room · build · wool/monument, monuments structurally via `MapValidity`) are pure
  (`Pgm/Authoring/Preflight`); **buildability** (every spawn/wool/monument placement over solid ground, not
  open void) and **traversability** (spawn↔wool chain connected) reuse the analysis layer. `ExportReady`
  mirrors what `GET /xml` enforces (round-trip must not throw + connectivity), so the XML sub-step's Export
  stays gated; mirror + buildability are advisory. The phase body is a **read-only overview** (a single
  centred column, **not** the 3-column editing workspace): the four check rows, a validate log, and **one
  static top-down map of everything authored** — real island polygons (from `/islands`, collinear-simplified)
  + the **orbit-filled** buildable bridges (`intent.build.areas` mirrored by the confirmed symmetry via the
  canonical `Geom.Symmetry`, like the generator) + the spawn-protection zones (dashed) and wool rooms (filled)
  + the spawn (circle, team chat colour) / wool (square, dye colour) / monument (diamond, dye colour) nodes,
  all in their **real colours** (`GameColors` chat/dye palettes), a node cut off from the chain ringed red —
  the playability picture in one image, no live canvas. A failed traversability/buildability/round-trip links the author back
  to **Build**, and a
  **Re-run checks** button (+ re-run on re-entry) closes the Build⇄Traversability loop.
  (`PreflightEndpoint`, `PreflightDto`, `Preflight`, `ReviewPreflightPhase`; new-map-authoring.md §9/§12)
- **Review & Export · Region tree sub-step (N07)** — the read-only inspect/debug view of the full generated
  region tree (between Pre-flight and XML). Intent maps drop the tree from the shaping steps (structure is a
  generated artifact), so it surfaces here: fetches `GET /map/{slug}/regions/tree` and renders it through the
  **reused editor `RegionTree` component** (category groups · collapse · type icons · synthetic-`__anon_N`
  styling · first-event tags), in the same single-column overview as Pre-flight, with a `read-only · N regions`
  badge and a note that the tree regenerates from the shaping steps. Writes nothing. (`ReviewTreePhase`;
  new-map-authoring.md §7/§12)
- **Review & Export · XML sub-step + gated Export (N06)** — the final sub-step: the generated PGM
  `map.xml`, segmented into containers picked on the left (**Full document** + Teams · Spawns · Wools ·
  Filters · Regions · Apply rules — the latter pulled from inside `<regions>`), each with a count, the
  selected block shown in `detail-xml-pre`. The flow-bar **Next becomes Export** (`ReviewXmlPhase` fetches
  `GET /map/{slug}/xml`; on **409** the preview is replaced by the blocked message and Export is disabled;
  on 200 it registers the open gate + a download action with the wizard via `RegisterExport`). Export
  downloads exactly the previewed bytes through a new `studio.downloadText` Blob helper — `NextEnabled` at
  the final sub-step is the export gate, `Next()` runs the download. **This completes the Configure wizard
  spine** — a new map now flows intent → Map Info → World → Teams → Build → Wools → Review & Export → a
  validated, downloaded `map.xml`. (`ReviewXmlPhase`, `ConfigureWizard` export wiring; new-map-authoring.md §9/§12)
- **CTW standards in generated exports + PGM-faithful formatting** — generated (intent) maps now export the
  standard CTW boilerplate ~every corpus map carries: `<itemkeep>` (the non-armor, **non-block** kit items —
  tools/weapons/consumables), `<toolrepair>` (the kit's tools/weapons), `<itemremove>` (the kit's
  team-coloured armor **+ the kit's build blocks** (the stacked items, dropped not kept) **+ the terrain drops
  of the blocks on the top surface** — seeds/long grass from grass, sapling/apple from leaves, string from
  cobweb, flint from gravel, … via a block-id→drop table fed by the surface palette; generous, since removing
  an absent item is a no-op), a `<block-drops>` rule that **suppresses the kit blocks' place-and-break
  drop** (`chance="0"`) so they can't be farmed, and a default `<kill-rewards>` granting a stack of building
  blocks per kill (the kit's blocks — wood ×16 + the team-coloured block ×8, the corpus norm of ~24 blocks
  across ~2 items, on top of the gapple include) — all **derived from the spawn kit + surface** (`CtwStandards`,
  corpus-grounded over N=199 incl. the surface-palette↔itemremove correlation) — plus the server-defined
  `<include id="gapple-kill-reward"/>` and `<hunger><depletion>off</depletion></hunger>`, and `<renewables>`
  for the world-scanned **resource blocks (iron / gold / diamond)** so mined ore regrows (`ResourceRenewables`,
  fed by the `resource_block` feature data): one renewable per ore type with a **tight** region for
  performance — if all of an ore's blocks sit in the team spawns, the spawn rects are unioned (`spawns`) and
  the `block=never` protection is relaxed once to `block-break` the in-spawn ores + `block-place` them only by
  the renewable's `cause=world` (the corpus pattern); otherwise a rectangle per spatial cluster, unioned when
  there's more than one (`only-iron`/`only-gold`/`only-diamond` + `only-air` filters, `avoid-players=2`).
  Applied **at export, gated to intent maps** (the export
  endpoint enriches the `MapXml` before `ToXml`); corpus-map exports are untouched (not round-tripped). The
  `XmlWriter` also now matches the corpus's formatting: self-close as `/>` (no space before the slash), a
  trailing newline, region elements carry `id` as the **first** attribute (`<rectangle id="…" min="…"
  max="…"/>`), `<apply>` carries `message` as the **last** attribute, `<regions>` ordered by type
  (primitives → compounds → `<apply>` applicators last), and a
  uuid → username **comment** under each `<author>`/`<contributor>` (`<!-- name -->` on its own line at the
  same indent, from the resolved `Author.Name`; skipped when unresolved). (`CtwStandards`, `XmlWriter`, `MapXmlEndpoint`)
- **XML serializer conventions (`docs/template.xml`-faithful).** `XmlWriter.ToXml` serializes with **4-space
  indentation** (`XmlWriterSettings.IndentChars`, not the 2-space default / tabs) and **no `<?xml?>`
  declaration** (`OmitXmlDeclaration` — real PGM maps start at `<map>`); the `<void/>` filter is emitted
  **bare, without an id** (trivial + always inlined); and `<regions>` are now sub-ordered **by semantic role
  within each geometry type** (spawn points · wool spawns · spawn regions · monuments · build), so `*-point`
  and `*-spawn` ids no longer interleave. The `ReviewXmlPhase` container segmenter was retuned to the 4-space
  indent. (`XmlWriter` + `ReviewXmlPhase`; B11/B13/B15/B16)
- **Generated CTW-standards conventions (`docs/template.xml`-faithful).** Four corpus-alignment fixes to the
  generated `map.xml`: team ids now carry the `-team` suffix (`red-team`/`blue-team`) at the derivation sites
  while `IntentNaming.Slug` keeps derived ids colour-based (`only-red`, `red-spawn-point`); the spawn kit's
  **build blocks** (`wood`, `stained clay`) go to `<itemkeep>` (not `<itemremove>`), so the `chance="0"`
  `<block-drops>` rule suppresses farming as intended (armour stays in `<itemremove>`); the spawn-**kit item
  slots** match the template (tools 0–3, wood 4, stained clay 5, water bucket 7, golden apple 8, arrow 28,
  shears 29, iron spade 30); and **spawn protection** grants an infinite `damage resistance` effect in-spawn
  and force-strips it on leave — a `reset-resistance-kit` (`force="true"`, duration 0) applied over a
  `not-spawns` complement (`<apply kit="reset-resistance-kit" region="not-spawns"/>`). Potion effects + the kit
  `force` flag round-trip end-to-end (domain ↔ Dict ↔ XML ↔ DB): `KitEffect`, `MapParser`/`XmlWriter`,
  `Serializer`/`Deserializer`, and a new `force`/`effects_json` on the `kit` table (migration `M0006`).
  (`TeamsGenerator`, `CtwStandards`, `SymmetryExpander`, `TeamsPhase`; B10/B14/B17/B18)
- **Side-view point/block marker** — the inspector slice (`SliceView` / `SideviewCanvas`) now draws the
  inspected point/block as a marker dot at its primary-axis column + Y (tracking the draggable line when
  editable), so you can see *what* you're seating, not just the Y level. (shared; surfaced by N04 Spawn)
- **Geometry consolidation — two families, one home each (`A4`).** *Scalar* math lives in the
  dependency-free `PgmStudio.Geom` leaf (reachable by WASM client + server, no transitive deps):
  `Symmetry` (`Order`/`Point`/`Rect`/`Apply`/`Normal`/`OrbitAxes` + reflect/rotate) is the single canonical
  C# transform — every affine site routes through it (the per-phase client copies, `SymmetryExpander.Step`,
  both `ModeNormals`, and `RegionParser`/`RegionBoundsDeriver` `MirrorBounds` are gone), plus
  `Polygon.PointInRing` for the NTS-free projects (`SketchRasterizer`, client `SpawnPhase`). *Area* geometry
  stays on NetTopologySuite in `Analysis`: `RegionGeometry2d` (region dict → footprint) builds, and
  `Geometry2dOps` (`CoversCell` + `IoU`) is the one home for the cell-sampling and IoU idioms
  (Buildability/ResourceSources/WoolSources/SymmetryDetector route through it). `Traversability.RegionCentre`
  places nav-points via footprint centroid-if-inside (else interior point), so they can't land in a
  union/complement gap; the canonical map-bbox is the surface-layer extent (one clip box for every pass).
  Editor region hit-test stays AABB (coheres with the AABB resize/move model); `shape.js` is sketch-only.
  Parity unchanged (buildability/wool/traversability 10/10). (`A4`)
- **One symmetry math, by runtime** — the canonical `PgmStudio.Geom.Symmetry` is shared by the WASM client
  (orbit assignment) **and** the server. Live canvas previews use the JS twin `geometry/symmetry.js`
  (`applySymmetry`/`applySymmetryToBounds`/`orbitAxes`, all six modes) via the editor canvas's
  `setAuthorMirror` + a non-selectable `ghost` flag — the same machinery the sketch tool's mirror uses.
  Identity assignment is the shared `OrbitAssignment` (point-aware) for Protection/Wools and island-aware
  in Spawn. (N02/N03)
- **New-map landing (Import flow)** — `/maps/new`: **Source** either lists importable local world folders
  and scans the chosen one (`POST /map/import-folder`), or fetches + scans a world from a download link
  (`POST /map/import-url` — allow-listed host, auto-uniquified slug so repeat imports of the same world
  coexist as `name-2`/`name-3`); **Found** shows the detection brief over the reused editor canvas
  (island base + surface overlay), with each finding selectable for a detail explanation — island sizes,
  wool colours + resource types (`GET /map/{slug}/scan-summary`), chest count — and symmetry / suggested
  teams as inline facts; **Plan** presents the six phases as cards, then Start → the wizard at Map Info.
  Reuses `ConfigureRenderer` via `scan-canvas.js` and a generic `.card` / `.card-grid` / `.callout`. (NS, B8)
- **Typed intent model** `MapIntent` (+ `SymmetryIntent`), persisted as the `map_intent_json` sidecar
  (outside the codec, like the draft bucket). (`ea76f13`)
- **Generator** `IntentGenerator.Apply` — meta / teams / build / wool slices → PGM document via the
  normal save path; idempotent regenerate-on-PUT. (`ea76f13`, `4bb3bcc`, `f631c11`, `4697e43`)
- **Symmetry-fill** `SymmetryExpander` — derives team count from the confirmed symmetry, synthesizes
  palette teams, and orbits the authored unit onto the other teams.
- **Endpoints** `GET` / `PUT /map/{slug}/intent` (`AuthoringIntentEndpoints`).
- **Playability export gate** — `GET /map/{slug}/xml` returns **409** for an intent map whose
  spawn↔wool chain isn't traversable-connected. (`0ac03ae`, `MapXmlEndpoint`)
- **Monument suggester + slice extractor** — smart-detect for the Monuments step (corpus-learned
  sign-facing → monument geometry). See `docs/contracts/monument-suggestion.md`. (`5235107`, `45209a1`)
- **Monument candidate store** — `MonumentSuggester` split into ingest-time `Gather` (world →
  candidates) + pure `Score` (`Suggest == Score(Gather)`); `monument_candidate` table (M0002) gathered in
  `scan-world`; served by `GET /map/{slug}/monument-suggestions` (box, no world access) +
  `POST /map/{slug}/monument-orbit` (symmetry reflect/rotate). Makes monument suggestion a DB query — the
  stateless-web-tier goal. Four anchor types: monument-label **wall signs**, wool-head/named **armour
  stands**, **wool item frames** (4th type — frame on the monument's pedestal/cap, structural pocket test
  excludes decorative palette/“frog-eye” frames; 17 maps have wool frames, ~6 real), and a last-resort
  **unsigned-monument allowlist** (label-free maps only, skipped when anchored): a distinctive pedestal
  (bedrock/clay/glass/wool) under a colour/marker cap (glass/wool/clay/barrier) with ≥1 open side — the 14
  ped×cap combos real label-free monuments use (lupain = bedrock+glass). Corpus: anchored path
  **96.7% / 58.7% / 35 FP**; label-free (`--label None`) **97.4% / 191 TP / 5 FP / 93.7% colour**. The
  single-signal + terrain-ambiguous geometry spray (~97% of the old store) is **not persisted** — flood
  maps collapse (dreamland 5859→311, fall_of_babylon 5035→40, lupain 52→2).
  `docs/contracts/monument-candidate-store.md`. (F9)
- **`--migrate-only`** — `PgmStudio.Import` applies pending migrations to a live DB without importing. (F9)
- **`/authoring` concept page** — UI mock (no backend calls), the design reference for the real
  wizard. (`9f645dc` → `45209a1`)

## Layout generation (G) — auto map generation (lane sketch generators)
- **Lane sketch generators** — `LaneSketchGenerator` grows a starter Capture-the-Wool sketch from lane
  primitives for four archetypes (H · Pinwheel · Trident · Organic); `OrganicLane` grows a team's island
  from a spawn hub out to noise-spread wool tips (variable-width `Lane.Ribbon` hulls, optional organic
  holes, forked lanes, spawn-on-a-spur). `POST /api/sketch/generate` originates a draft sketch from a
  chosen archetype/seed (`SketchLayoutPrep` simplifies + Bézier-rounds lanes for the editor). Pure +
  seeded — same seed → same layout.
- **Organic-generation demo page** — `/concepts/organic` visualises the whole Organic pipeline on one
  page, one panel per stage (value-noise field → anchor sampling → lane spines → ribbon hulls →
  assembled + mirror) with static explanatory text; a seed / wools control re-runs the **real** generator
  live. `OrganicLane.GrowStages` captures the per-stage intermediates from a single traced `Grow` run
  (no second code path); `POST /api/sketch/generate/stages` emits them; `render/gen-stages.js` paints each
  stage (reuses the geometry / render layers). (G4)
- **Island-outline simplification → sketch format** — `IslandSimplifier` turns a real island's detected
  outline into the editable sketch layout (Douglas-Peucker simplified exterior `add` polygon +
  a `subtract` per hole, via `PolygonSimplify`); `RoundTrip --island-sketch` previews one map's layout,
  and `PgmStudio.Import --store-island-sketch` stores it for every map as the `island_sketch_json` artifact
  (derived from `islands_json`, distinct from the authored `sketch_layout_json` so it neither re-stages the
  map nor clobbers a draft). Simplification only — the faithful outline; cutting it into lanes is `G6`.
  `scripts/island_shapes.py` is the shape-feature analyzer behind it. (G6 base)
- **Lane-decomposition surface (manual cut tool) — RETIRED** with the corpus-mining flywheel (the
  plan-then-realize direction, `docs/contracts/map-generation.md`): the page, its canvas bridge and
  the queue/load/save endpoints are removed; the pure seam-split geometry (`geometry/decompose-cut.js`)
  lives on under the sketch tool's split feature, and saved `lane_decomposition_json` artifacts remain as
  data. As shipped: `/maps/{slug}/decompose` (dashboard footer →
  `/decompose`, a queue of two-team CTW maps): loads a map's `island_sketch` outline and the author
  **lassos** a region → picks **two seam points** (existing corners or lasso∩edge markers) → the piece
  **splits** into a lane + remainder (iterative peeling), with a role tag per piece (spawn/wool/frontline/
  hub/other), undo, and Confirm→Next that saves `lane_decomposition_json` (its presence marks the map done +
  drops it from the queue, keeping the original outline as the diff). **One side only** — islands are deduped
  by the map's primary symmetry (`GET /symmetry`) so the author cuts a single team's set; `getState` records
  the `mirror_mode`. Uses the shared editor canvas chrome (toolbar · Focus-piece · zoom · cursor). Canvas
  `bridge/decompose-bridge.js` + pure `geometry/decompose-cut.js` (node-tested); `DecomposeEndpoints.cs`
  (queue / load / save). The ground-truth-gathering precursor to the `G6` auto-cutter. (G6)
  - **Queue browsing** — `‹` / `›` step through the to-do queue without decomposing (unsaved cuts dropped;
    Confirm & Next stays the save path), boundary-aware disabled state, and the progress label shows position
    (`{i} of {N} to do · {done} done`) — so a reviewer can check maps ahead/behind before cutting.
  - **Reference overlays (`Blocks` · `Anchors` · `Build`)** — three independent canvas overlays to guide
    cutting, each a `filter-chip` that persists (re-fetched per map) as you browse the queue: (a) the
    **block-colour** palette (`GET /layers/top-surface`, `render/block-render.js`) below the pieces; (b)
    **objective anchors** — wool tips + spawn spurs as ringed markers on top; (c) the **declared build region**
    as a dashed outline under the pieces. (b)/(c) consume the `GET /map/{slug}/island-roles` hook's `anchors`
    + `buildRegion`. (G8)
  - **Select tool + inspector categorization (G7)** — a **select** tool picks a piece on the canvas (or a row
    in the redesigned colour-coded left list); a **right inspector** sets its category from button groups —
    **Lane role** (spawn/wool/frontline/hub/other) + **Whole island** (stepping-stone/mid/decorative) — instead
    of the slow per-piece dropdown. Whole-island tags are **pre-filled from `/island-roles`** (neutral →
    stepping-stone, decorative → decorative; team/objective left to cut), so the human confirms the auto-tags and
    cuts only the team islands. Persists per shape in `lane_decomposition_json`. (G7)
- **Layout-generation design (plan-then-realize) + expert rule capture** — the direction docs for full map
  generation: `docs/contracts/map-generation.md` (the **piece/interface plan model** — areal pieces +
  edge-interval interfaces, no skeletons; proxy-cell mini-layout semantics; one-way compile into sketch +
  intent with a detach point; rule-based composition, fragmentation moves, roughen + elevation passes; scope
  tiers), `docs/contracts/layout-rules.md` (the author-corrected per-role rule checklist + the seed shopping
  list), and `docs/contracts/plan-editor.md` (plan schema · compiler · seed-studio editor — built as
  `G16`–`G21`). Resolves the `G15` exploration: **WFC evaluated and rejected** for the layout skeleton (CTW
  quality is global/relational — symmetry, spawn/wool separation, typed gaps — not local-adjacency texture);
  the polyomino vocabulary survives as the plan's proxy-cell grid. (G15)
- **Plan schema + validator** — `PgmStudio.Pgm/Plan`: `PlanModel` (the `*.plan.json` wire model — proxy-cell
  pieces/zones/placements/cliffs, one team's unit, symmetry fans the rest), `PlanDerived` (land interfaces
  from rect abutment, gap links through zones, islands, frontline, orbit fanning via `Geom.Symmetry`), and
  `PlanValidator` — structural errors (sliver/corner contacts, different-surface overlaps, unreachable wool
  over the fanned land+gap graph, wool path through a spawn piece) plus a non-blocking extensible **rule-lint
  table** citing `docs/contracts/layout-rules.md` ids (G2/G5/SP2/WL2/BZ5/EL1/EL3). 43 TUnit tests. (G16)
- **Plan compiler + seed plans (golden regression)** — `PlanCompiler.Compile(plan) → (SketchLayout,
  MapIntent)`, pure/deterministic: cells→blocks, land-connected pieces united into one polygon per component
  (`Geom.RectilinearUnion` — exact integer rect union reproducing the seeds' 12-vertex H / 6-vertex L),
  islands grouped by mirror flag, team-0 placements fanned per orbit (cardinal-quantized `facing` yaw),
  zones → `build.areas`, observer/maxHeight derived (surface+15 / surface+headroom), first wool = team colour
  + deterministic dye palette. The three seeds re-expressed as `tools/seeds/*.plan.json` with structural
  golden tests against the checked-in layout/intent pairs (base-2island/base-4team exact; base-2wool exact
  except two documented hand-authored values). Step terraces deferred (no seed exercises raised land seams).
  (G17)
- **Plan editor page (seed studio canvas)** — `/plan-editor` (`Pages/Plan/PlanEditor` + `js/studio/plan/`):
  an SVG cell-grid canvas (heavy line per 5 cells) with draw/move/resize role-coloured rect pieces (fill
  tinted by surface), translucent dashed zones, spawn/wool/iron markers (spawn facing cycles on click;
  markers re-parent to the piece under them, keeping piece-relative offsets), a per-piece inspector (id,
  role, surface ±2, mirrors toggle), a globals form (symmetry / cell / surface / headroom / maxPlayers), and
  a live dimmed **mirror ghost** of every orbit image (via `geometry/symmetry.js`; view + grid fit to
  content ∪ ghost extents). Plan JSON import/export in the `PlanModel` wire shape (seed round-trip tested) +
  debounced localStorage autosave; pure geometry in `plan/plan-doc.js` (node-tested, 16 tests); mounted via
  `studio.js` native import; dashboard footer "Plan" link. (G18)
- **Plan-editor overlays + live lint** — `POST /api/plan/inspect` (the single canonical C# validator/derived
  structure serving the editor; plan JSON in → findings with subject ids + ready-to-draw block-space overlay
  geometry out; malformed body → 400): derived **land interfaces** (cased-green seams; sliver/corner red),
  **gap links** with hop-distance labels (suppressed between pieces of the same land component — a walkable
  pair is no void crossing, which also de-noises the G5 lint), and computed **frontline** edges — three
  persisted overlay toggles drawn in a dedicated canvas layer. The bridge re-inspects debounced (~300ms,
  stale-response guarded); a lint panel lists errors-then-lint with rule chips, click pulsing the subject
  pieces. `PlanFinding` carries subject ids. Pgm 219 / Api 40 / JS 102 tests green. (G19)
- **Plan compile preview + walk-test loop** — `POST /api/plan/compile` (malformed → 400; structural errors →
  422 with the error findings, lint never blocks; else `{layout, intent}` serialized with each consumer's own
  JSON options for byte-exact downstream compatibility) + the editor's **Compile** drawer (Layout/Intent tabs
  with Copy/Download, 422 findings rendered in place) and **Create draft** flow — the sequenced
  create → PUT sketch → finish → PUT intent chain with per-step failure naming, then a sketch-editor link and
  a status-checked **Download world ZIP** (`GET /map/{slug}/export`). Proven live end-to-end: a compiled seed
  plan produced a playable `{slug}/` world ZIP (map.xml + level.dat + region/*.mca). Full-loop integration
  test in Api.Tests (45 green). (G20)
- **Plan editor visibility & interaction pass** — world-coordinate **marker-first picking** (nearest marker
  within 0.42 cell wins over the piece under it — the old integer-cell hit test made half-cell markers
  unselectable; re-click on a selected spawn cycles facing, selection never silently rotates; drag /
  inspector-delete / Delete key on the selected marker), a persisted **Heights** toggle (monotonic
  navy→teal→gold ramp over the plan's surface range + in-piece height labels), and **zone mirror ghosts**
  (build areas + holes fan through the same orbit images as pieces; view bounds include them) — a rot_90
  pinwheel's centre tiling is finally visible while authoring. JS 115 tests. (G25)
- **Plan-editor reference backdrop (trace real maps)** — a **Reference** sidebar section picks any processed
  map (`GET /api/maps` now flags `hasSurface`; 367/390 traceable) and paints its top-down block render behind
  the grid as a tracing aid, reusing the shared `render/block-render.js` rasteriser in a new bottom
  `#refLayer` of `PlanCanvas`. Auto-centres the map bbox on the symmetry origin, then **Opacity / Offset X·Z
  (cells) / Scale / Recenter / Clear** controls place it; because the plan canvas is a block-unit frame, a real
  10-block lane reads as 2 cells at scale 1. The placement (map slug + offset/scale/opacity) is an **optional
  `reference` block** in the plan wire model — round-trips in the `*.plan.json` file as provenance, restores +
  repaints on reload, and is **ignored by the compiler** (verified: a seed compiles byte-identically with and
  without it). Builds the corpus that informs the box-based / wool-approach vocabulary in
  `docs/contracts/map-generation.md`. (G55)
- **Configurable surface step** — the piece surface stepper's ± increment (formerly hardcoded ±2 per EL1) is
  now an editor preference: a **Surface step (y)** field in the globals panel sets any whole value ≥ 1, and
  **1 / 2 / 3 quick-preset chips** under the inspector's surface stepper switch the common ones in-context,
  applied live mid-edit. Persisted per browser (bridge `getSurfaceStep`/`setSurfaceStep`, key
  `pgm-plan-surface-step`); the ± button tooltips read the current step. Not part of the plan file. (G57)
- **Zone-union connectivity + contact lint** — buildable **regions** = union-find components of zone rects
  (merged on overlap or positive-length shared border; corner-point touch does not merge); straight-span
  gap-link overlays test containment against the merged region, while fanned **reachability** links every
  piece a region touches with no straight-span requirement — chained bridging across adjoining zones works,
  validating the four-team pinwheel centre (24 cross-team errors → 0). Sliver/corner contacts downgraded
  from structural errors to lint **PC-S**/**PC-C** (deliberate thin ledges and corner touches are author
  judgment); different-surface overlap stays an error. Pgm 244 tests. (G26)
- **Quiet plan canvas (Labels toggle)** — piece/build-area id labels and the gap connectors + hop numbers
  are hidden by default behind one persisted **Labels** chip (replaces the Gaps chip; legacy pref key
  ignored). Height-mode surface numbers stay (data, not ids); the selected piece/zone still shows its lone
  id for orientation. JS 118 tests. (G28)
- **Plan schema v2 — anonymous pieces + intent roles + wall marks** — pieces are anonymous by default
  (`role: "piece"`); legacy `lane`/`hub`/`mid` (and any unknown value) map to `piece` on parse in both the C#
  `PlanModel` and the JS `normalizeDoc`, so old plans/seeds load clean. Two optional intent-bearing roles kept:
  `wool-room` (terrain↔room land seams render **red**, per ST1) and `spawn` (new **ST2** lint keeps iron
  markers inside the spawn piece when one exists). A `walls` list beside `cliffs` marks pre-built approach
  walls (piece-id pairs); `PlanDerived` exposes the wall-marked land interfaces and a structural **error** when
  a wall pair shares no land seam. The editor palette collapses to one **Piece** draw tool plus the two area
  roles (neutral piece tint; distinct wool-room/spawn tints), a **Wall** tool toggles a wall mark on the
  nearest land interface clicked, and `/api/plan/inspect` interface segments carry `woolRoom`/`wall` flags so
  the canvas renders red seams / heavy dark wall bars from data. Compiler passes `walls` through untouched
  (stamping is a later task). Pgm 230 / JS 107 tests green (Api plan inspect/compile endpoints green). (G22)
- **Export structures — room floors, entrance redstone, iron cubes, approach walls (ST1–ST4)** — the plan
  compiler derives a `MapIntent.Structures` section (block-coordinate directives, fanned across the symmetry
  orbit) that the sketch world-export path stamps via `StructureStamper` (`PgmStudio.Minecraft`): each
  `wool-room` piece's footprint becomes solid bedrock y=0→surface; each terrain↔room entrance seam gets a
  redstone-wire row one block inside the room with a redstone torch at each end; each iron marker becomes a
  4×4×4 iron cube resting on the surface (footprint centred on the snapped marker); each `wall`-marked
  interface becomes a 2-thick full-width bedrock wall rising y=0→approach-side surface +4. The **approach
  side** is the wall-pair member with the larger walk-graph (land + gap) distance to the nearest same-unit
  wool marker (ties → the lower-surface side). Iron cubes inside a `spawn` piece carry `renew=true` and get a
  per-cube renewable region in the generated `map.xml` (`StructureRenewables`: `iron-cubes` union +
  `<renewable renew-filter="only-iron" replace-filter="only-air" avoid-players="2">`). The `isolated-spawn`
  seed carries the authored intent (spawn/wool-room roles, an in-spawn iron marker, wall marks on the two
  elevation seams). Pgm 234 / Minecraft 49 tests green; end-to-end world round-trip in Api.Tests reads the
  stamped block ids back. (G23)

- **The seed corpus — twelve author plans with honest player counts (rules v3 frozen)** — ten
  authored seeds + the real-map trace (`big-board-…-parallel-mid`, parallel mid, 30/team) +
  `mirror-tiny-map-cliff` (5/team, `mirror_z`, sub-base palette 3–7, the axis-spanning Δ6 mid
  cliff). Every seed stores the author's per-team count (comfortable cap); the G8 land-per-player
  coupling is derived (65 → 184 b/p rising with per-team land); all mid forms author-labeled
  (clean 8 · hash 3 · parallel 1); `docs/contracts/layout-rules.md` **froze 2026-07-04 as the
  composer's v1 rule set**. (G21)

- **Composer — envelope + team-unit grower (first slice)** — `PgmStudio.Pgm/Compose/`: a
  deterministic-seeded generator (own PCG32 — golden-stable across runtimes) growing one team's
  authored unit from a player count alone. `Envelope` interpolates the G8 coupling (players →
  land budget) and samples board dims in the G3 bands; `TeamUnitGrower` grows hub / spawn lane /
  1–3 wool lanes / frontline chains on a symmetry-generalized (u,v) frame (`Frame`), with hard
  invariants enforced by bounded retry: full-corridor attachments only (no narrow seams/corners),
  WL2/WL7 marker distances, LN2 lane-chain cap ≤50, ±20% land budget, ≥10-block clearance between
  orbit images (team sides are separate islands — exactly `Teams` land components per fanned
  board), footprint aspect inside the measured corpus band. Structural surplus spending (third
  wool at p≥16, doglegs, plaza hubs, frontline chains) instead of lane stretch; silhouette variety
  via sampled attachment hosts/depths and arm asymmetry. Zones/mid/heights are the remaining G32
  slices. 300 Pgm tests green (43 new: known-answer RNG pins, envelope bands, invariant +
  distribution sweeps ~1,080 composes). (G32 — first slice)

- **Composer — mid carve, isolation cuts + build-zone discipline (B track)** — `PgmStudio.Pgm/Compose/`:
  `MidCarver` samples the crossing before growth (R0/R1/R2 hop designs, twin frontline chains as the CT8
  hole mechanism, mid stones on CT7-snapped candidate columns) and carves the mid band sized between the
  minimal connecting interval and the face hull (never board-width — BZ9), docking flush to the frontline
  faces (BZ7/BZ8) and clearing every wool piece by ≥2 cells (BZ6). `IsolationCut` severs a marker piece
  behind a bridge (CT5; spawn only at ≥10/team — SP6); `ClosureAnalysis` rasters the closure for holes
  (`HoleSizes`/`AnyHoleRingedBy`); `ComposeGeometry` fans images. `Composer.ComposeStages` runs the full
  order (envelope → crossing → grow → carve → cut → assemble) behind an acceptance gate (`PlanValidator`
  zero-errors, every gap hop in 10..20, BZ6 clearance re-checked post-cut, no wool-ringed hole) with a
  hole-hunt on both branches (holed by default, holeless the sampled exception). Rules amended: BZ6–BZ9
  build-zone interface discipline + the CT8 hole-ring split (`layout-rules.md`). 314 Pgm tests green.
  **Known limitation:** p5 (t2 and t4/rot_90) is structurally infeasible under BZ6 + spawn ≥2×2 within the
  fixed budget — deferred to the buffer-tile fix (G35). (G32 — B track)

- **Composer — real `spawn` + `wool-room` room pieces** — `PgmStudio.Pgm/Compose/SpawnWoolRooms.cs`: a
  post-growth pass that carves each objective's terminal lane into a compact role-bearing ROOM (a
  `wool-room` per wool, one `spawn`) the plain lane pieces dock to — instead of dropping a marker on an
  anonymous piece, so `PlanCompiler`'s role paths fire: a generated wool now stamps a bedrock room floor +
  red entrance seam (ST1) and a spawn auto-renews its iron (ST2). The room is a 2-cell-deep ≥10×10-block
  plateau (WL3 stamp cover) split off the marker's dead-end (WL1), with the marker re-hosted at its unchanged
  world position (WL2/WL7 preserved); a terminal too short to leave a ≥2-cell approach — or one isolated
  behind a bridge (WL4/SP6) — becomes the room whole, and a split that would degrade a neighbour contact
  falls back to whole. Geometrically neutral (room ∪ remnant = the terminal's cells), so every grown
  invariant holds. Runs after the isolation cut so a severed marker piece is its own isolated room. The
  compose review gallery (`tools/compose/gallery-gen.cs`) renders the rooms in the editor's role colours.
  323 Pgm tests green. (G49)

- **Wool-approach classifier — width-independent, structural** — `Pgm/Shapes/ShapeClassifier.cs` (dissolved
  from `Pgm/Plan/WoolApproachShape.cs` by G58): the
  categorizer's read of a wool box, rebuilt so **nothing keys off the absolute width of any piece** (uniform
  scale and per-piece thickness never change the family). One tree: enclosed void → **donut**; wool bridging
  two opposite bars (removing it disconnects the terrain) → **Clamp**; else by bend count off the outline
  (0 → I, 1 → L; ≥2 → the two-leg **branch** — two terrain legs share a bbox edge the wool does not sit on —
  split into **U** when the crossbar overhangs the wool's footprint (flush on a bar wider than itself) vs **H**
  when the wool caps a room-run stub its own width; no branch → **scythe** if a single-edge bay is wrapped,
  else **Z**). A bay is a one-bbox-edge concavity (any width), a branch is two runs on a shared edge (a thick
  leg is still one leg), the U/H split is the crossbar's overhang past the wool. **Plug dropped** (a solid body
  is a wide/solid **I**; the room-only dock is an interface concern, replaceable by a short-entry **I**). Fixes
  the wide-H→Scythe/Plug, wide-Z→Plug, and wide-bay→Z misreads. Verified by the mirror/catalog/stress suite
  (`shapes-gen`/`emit-verify`/`stress-shapes`, now the TUnit `Shapes/` tests — G58). Contract:
  `docs/contracts/map-generation.md` §5. (G53)

- **Wool-box pieces carry their slot role** — `Pgm/Compose/WoolBoxEmitter.cs` + `TeamUnitGrower.cs`:
  `WoolBoxEmitter` now tags every emitted piece with its **slot role** (`ApproachSlots` on `GrownPiece.Slot`) —
  `entry` (the universal hub-attach), `run`, `bar`, `leg`, `room`, qualified `entry-run`/`room-run` and
  `entry-bar`/`room-bar` — per the §2 piece-vocabulary table, exposed as data via `ApproachSlots.Template`.
  It is a **shape-internal taxonomy, distinct from the map-level piece `role`** (terrain pieces keep `piece`),
  and is the foundation the shift (G50) / width (G51) / docking (G52) rules target — those name a slot instead
  of re-deriving it from geometry. Invariants held: a family emits a **stable piece count** (no collinear
  merges) and a role is a **template slot, not a property of the rectangle**. Verified: `WoolBoxEmitterTests`
  (25 cases — template order per family, flip/variant invariants, stable count) + the `ShapeMirrorTests` slot
  round-trip (G58). Contract: `docs/contracts/map-generation.md` §5. (G54)

- **Shape substrate + one family enum (M0 consolidation)** — `Geom/Cells.cs` + `Pgm/Shapes/`: the shared
  rectilinear cell substrate (N4 · flood · connected components · enclosed-void · reflex corners · bays ·
  bounding-box · min-run-width) extracted to the `Geom` leaf, and the base-shape taxonomy unified into **one
  `ShapeFamily` enum** (`Isolated, I, L, Z, Scythe, Clamp, U, H, Donut`) shared by emit and derive — the mirror
  now closes as `derived == requested` on one type, not a `ToString()` bridge across the old
  `ApproachFamily`/`ApproachShape` pair. `WoolApproachShape` dissolves into `Shapes/ShapeClassifier` reading
  **terminal** cells (nothing wool-specific; the dead `laneWidth` param is gone); the wool-lane string read
  becomes a `LaneRead` enum via `ShapeClassifier.ClassifyOpen`, with `WoolLaneShape` kept as a thin string shim.
  The three run-by-hand mirror harnesses move into the suite — `ShapeMirrorTests` (emit↔derive), `ShapeCatalogTests`
  (the §5 t/v/w catalog), `ShapeStressTests` (extreme-geometry width-invariance) — plus direct `CellsTests`.
  Pure refactor: `derive-gallery` output **byte-identical** over all base + generated cases; Geom 61/0, +67 shape
  tests green, 5 pre-existing Pgm failures unchanged. `ClosureAnalysis` / the gallery raster / `FannedGraph`
  rewire onto `Cells` at M1 (G59). Review: `docs/map-generation-architecture-review.md` §3. (G58)

- **Board deriver into `src` (M1)** — `Pgm/Derive/`: the raster-layer board reader — islands + anchor roles,
  stepping-stone kinds, build-zone kinds/widths/interfaces, per-wool approaches + lane shapes, frontline/intra/
  self edges, wool lanes, the mid form, and boundary-classified enclosed voids — extracted from the ~460-line
  run-by-hand `derive-gallery.cs` into `BoardDeriver.Derive(plan) → BoardStructure`, a library call the
  evaluator (G60) and the conformance sweep (G43) can now make. The gallery is **render-only** over
  `BoardStructure`. `Plan/PlanDerived` → `Derive/ContactGraph` (the rect layer: contacts, interfaces, gap
  links, build regions, frontline edges, components; test → `ContactGraphTests`). `BoardDeriver`'s substrate
  routes through `Geom.Cells` (N4 / components); `ClosureAnalysis` documented as a deliberate fast-path twin of
  `BoardStructure.Voids` (kept dense-grid for the composer's 60-attempt hunt loop). Pure refactor:
  `derive-gallery` output **byte-identical** over all base + generated cases; Pgm 410 pass (5 pre-existing
  failures unchanged), Api builds clean. Canonical doc §1.3/§6.2 now name the classes, not the script. The one
  deferred slice — `FannedGraph.LandAdjacent` ↔ `ContactGraph` surface-overlap reconcile — is G65.
  (G59)
- **Composer evaluator engine — foundation (M2 groundwork)** — `Pgm/Evaluate/`: the one place layout rules are
  scored. `LayoutEvaluator.Evaluate(ctx | plan, profile) → Evaluation` (`Score = Σ hard-penalty + Σ w·distance`,
  lower is better, 0 = perfect) + a hard-only short-circuit `Gate`; `ILayoutTerm` (reads derived measurables,
  cites one `layout-rules.md` id, never a family name); `EvalContext` (derives `ContactGraph` + `PlanValidator`
  findings once, **lazy `BoardStructure`** so the gate never derives the board on its resample loop);
  `EvaluationProfile` (per-term enable/weight — the criteria on/off switch); `SeedEnvelopes` + the `Band`
  distance convention (metric normalized by the band half-width). **`Composer.Acceptable` dissolved** into the
  gate: seven hard terms port it one-to-one — `StructuralIntegrity` (STRUCT), `LintRejectTerm` (WL2/PC-C/G2),
  `GapHopBand` (G5), `BandWoolClearance` (BZ6), `WoolRingedHole` (WL8) — plus an opt-in `IComposeRejectSink`
  (RNG-reproducible `{seed,request,attempt,stage,termId,ruleId,subjects}`, null by default). Faithful:
  composed output **byte-identical** over the 300-case sweep; 25 new tests (distance convention, each term at
  its boundary, engine score/gate/profile, and a permanent every-composed-plan-passes-the-gate guard) green.
  Review: `docs/map-generation-architecture-review.md` §5/§9; direction: `docs/contracts/layout-evaluator.md`.
  (G60)

- **Plan authoring — freeform templates (`none` symmetry · `connector` piece · palette resort)** —
  `Geom.Symmetry` + `Client/wwwroot/js/studio/` + `Client/Pages/Plan/` + `Pgm/Plan/`: three plan-editor
  primitives that let an author design reusable single-unit lane / spawn templates. **G46** adds a `none`
  symmetry (order 1, empty orbit — `Symmetry.Order`/`OrbitAxes` + the JS twin `orbitAxes`) so a single freeform
  unit authors with no mirror ghost fighting the shape; it compiles order-1 through `PlanCompiler` and inspects
  clean. **G47** adds a second annotation role `connector` beside `buffer` (`PlanRoles.Annotations`) — an
  attachment-point mark ("other structure docks / overrides here"), non-generating (filtered from the
  graph/export like buffer), rendered as a teal crossed hatch in the editor and the compose tools. **G48**
  resorts the palette into three labelled kinds — Pieces (piece/spawn/wool-room + build), Markers
  (wool/spawn/iron/wall), Technical (buffer/connector). 53 Geom + 323 Pgm + 121 JS + 48 Api tests green. A
  study of six hand-authored wool-lane templates (`tools/compose/wool-lane-study/` + `wool-lane-study.cs`)
  showcases multi-access, buffer spacing, and land/build-zone attachment points. (G46 · G47 · G48)

- **Plan model — the `buffer` annotation piece (non-generating design tile)** — `PgmStudio.Pgm/Plan/` +
  `Client/Pages/Plan/`: a new annotation-role class (`PlanRoles.IsAnnotation`/`IsGenerating`) whose first
  member `buffer` marks reserved empty space (lane spacing, the rot_90 border, holes — a hole is an enclosed
  buffer). Informational-only: filtered out of `PlanDerived` (absent from interfaces/components/frontline/
  gap-links/`FannedGraph`/the compiler), skipped by `ClosureAnalysis` (a buffer marks empty space, never
  counts as land, so it can't erase the rotation hole it documents), invisible to world export; a spawn/wool
  on a buffer is a validation error. Authored + rendered as an orange diagonal hatch in the plan editor and
  the compose render tools. 323 Pgm + 121 JS tests green. Enables the composer-side reservation (G35). (G35 slice)

## Sketch world-folder export (P9) — a playable `.mca` world for sketch-originated maps
- **Anvil write side** — `AnvilRegionWriter` + `LevelDatWriter` (`PgmStudio.Minecraft`): emit the 1.8–1.12
  numeric Anvil format (region sector/location table, zlib chunks, nibble-packed `Blocks`/`Data`/`Add`
  sections; gzipped `level.dat` with world spawn + a real creation timestamp), the mirror of the read-only
  `AnvilRegion`. Write→read round-trip tested. (P9a, P9b)
- **World synthesis + stampers** — `SketchTerrainBuilder` (bedrock floor at y=0 + stone fill from the sketch
  columns, reporting each column's surface top), the shared `CubeStamper` 8×8 hollow-bedrock shell (roof
  hole, layer-6 light slit, layer-4 colour strip, 2×2 floor wool, glass-pane / open doors), `WoolCageStamper`
  + `WoolCageChests` (two-chest corner loadout), `SpawnCubeStamper` (spawn cube + auto-wired monuments:
  bedrock pedestal · air cell · wool-colour glass cap · label sign, placed by captured-wool count),
  `ObserverPlatformStamper` (solid 6×6 platform + four inward info boards), plus `SignBuilder`/`ChestBuilder`
  and `PositionSnap` (integer X/Z, `ymax` Y, yaw→door facing). (P9c, P9d, P9g, P9h, P9i, P9j, P9l)
- **Export endpoint** — `SketchWorldBuilder` assembles the world from a map's sketch layout + intent and
  returns a resolved intent (integer-snapped spawns + monument locations derived from the world air cells,
  capturers defaulted to every non-owner team) so the XML agrees with the world. `GET /api/map/{slug}/export`
  returns a `{slug}/` ZIP (`map.xml` + `level.dat` + `region/*.mca`) for sketch-origin maps and plain
  `map.xml` otherwise, behind the traversability gate (shared `MapXmlComposer`). The Configure Export button
  downloads it (`studio.downloadUrl`), and the wizard's manual Monuments sub-step is dropped for sketch maps
  (`GET /map/{slug}/origin`). Spec: `docs/contracts/sketch-world-export.md`. (P9e, P9f, P9k)

## Sketch tool (M8) — draw shapes → islands → world geometry
- **Sketch editor** — `/maps/{slug}/sketch` (`SketchEditor` + `SketchPanel`/`SketchInspector`): draw 2-D
  shapes → live islands + mirror, with select/op/override/delete/rename. Pure geometry in
  `geometry/shape.js` + `geometry/boolean.js`; canvas + draw/edit controllers + `render/sketch-render.js`;
  `bridge/sketch-bridge.js`. A sketch **is a draft map**. (S2a, S2b, S2c)
- **Sketch persistence** — the layout persists as a `SketchLayoutJson` map_artifact (outside the codec,
  like the draft bucket): `POST /api/sketch` create + `GET`/`PUT /api/map/{slug}/sketch` (debounced save +
  load-on-mount; 4 integration tests). (S2d)
- **Sketch finish / rasterize** — `SketchRasterizer` + `WorldFeatureWriter.WriteSketchAsync` +
  `POST .../sketch/finish` + the Finish button: the sketch rasterizes into the importer's geometry
  artifacts and flows into Configure (`MapStage.Configure` + a `configureUrl`; 6 rasterizer tests). The
  `/maps/new-sketch` page (`SketchCreate`, S11) originates one. (S2e) — the tool is complete (originate →
  frame → draw → Finish); only an end-to-end pass through Configure → Edit remains (TODO `S2`). Plan:
  `docs/contracts/sketch-authoring.md`.
- **Footprint presets + size legibility** — the footprint frame sets a **non-square** working area
  (width X × depth Z) from presets: 2-team landscape `120×80` (default), portrait `80×120`, square
  `120×120` (4-team / D2), or custom — replacing the old 512-square that made 10–15-block lanes
  undrawable. A live **on-canvas size readout** (`canvas-dim`) shows the active draw's `W × D` or the
  selected shape's extent. (S3)
  Plan: `docs/contracts/sketch-tool-improvements.md` §1.
- **Ruler distance reads on the ruler line** — the measure tool renders its block distance as **pure
  screen-space text running along the ruler line** (at the midpoint, kept upright, with a thin halo so it
  stays legible over shapes at any zoom, re-drawn on every pan/zoom) instead of in the `canvas-dim` sub-bar,
  which now keeps only the draw `W × D` / selected-extent. A canvas-wide **`user-select: none`** on the shared
  drawing surface (`.map-canvas-svg`) stops a drag from selecting the on-canvas SVG labels. (S18)
- **New-sketch creation page** — `/maps/new-sketch` (`SketchCreate`): the full-screen origination entry
  (mirrors Configure's `/maps/new`), reached from the Sketch overview's New-sketch link. An **Identity**
  section (map name) + a `filter-chip` **Start from** picker → **Blank** (SVG-preview footprint + symmetry
  `choice-tile`s with W/D + centre `coord-field` rows) or **Generate** (archetype/seed); a single
  **Continue** creates the draft via `POST /api/sketch` (now carrying the working frame → a seeded `setup`)
  or `POST /api/sketch/generate`. The editor's footprint/symmetry **Setup** block moved off the always-open
  sidebar into a collapsed **Frame** accordion, lifting the Islands tree toward the top. Reusable `.choice-*`
  tile CSS shared with the primitive palette. (S11) Plan: `docs/contracts/sketch-creation-flow.md`.
- **Rectangle → polygon promotion** — an inspector **Convert to polygon** button (and the `P` shortcut)
  turns the selected rectangle into a 4-corner polygon (id / operation / override **and the height fields**
  `base_height`/`floor`/`anchor_heights` preserved — a promoted box keeps its column instead of resetting to
  the 1-block default), opening vertex-drag · midpoint-insert · Bézier editing. Pure `rectToPolygon`
  (`geometry/shape.js`); `promoteShape` in the bridge; the 8-handle rectangle resize is unchanged until you
  promote. (S4, S15) §2.
- **Shape library (drag-in primitives)** — a left-sidebar palette (above the island tree) of pure-geometry
  primitives: n-gons {3,5,6,8}, polyominoes (L · U · T · I-bar · scythe · cross · line-with-branch), and a
  hole-square add+sub composite. Click a thumbnail → a ghost follows the cursor → click the canvas to place
  (Esc cancels); each entry instantiates ordinary `SketchShape`s, centred + block-snapped at a default cell
  size — so islands/mirror/rasterizer need no new code. Catalog + `instantiate`/`libraryMeta` in
  `geometry/shape-library.js`; `armPlace` + canvas place-mode/ghost; the `SketchLibrary` component. (S8) §8.
- **Per-shape & per-anchor height (rasterization)** — `SketchShape` gains `base_height` / `anchor_heights` /
  `floor`; `SketchRasterizer.RasterizeColumns` carries each cell's `[YFloor, YTop]` through the 4-step algebra
  (taller add wins on overlap), with a per-vertex **TIN** surface (`Geom.Triangulation` ear-clip + barycentric)
  for polygons whose anchor heights match their vertices; mirror copies preserve the column + vertex/anchor
  alignment. `WriteSketchAsync` writes the real span to `layer_segment` (the SliceView reads it) and the
  surface block at `YTop`. Verified by Geom + rasterizer unit tests and a DB-level finish (uniform + ramp).
  (S5 — rasterization; per-anchor editing UI is S5b) §3.
- **Floor = elevation, Height = thickness** — the column model is the intuitive one: **Floor** is where a
  shape's base sits and **Height** is how tall it is, so `YTop = base_y + floor + height` (previously `floor`
  was the bottom-Y and `base_height` an absolute top-Y, which read like a second height in the inspector).
  Applied in `SketchRasterizer.RasterShape` (`top = floor + thickness`), the iso preview's prism/terrain calc
  (`sketch-bridge.js`), and the inspector labels/hint (`SketchInspector.razor`); stored sketches re-rasterize
  under the new meaning (no backward-compat). Rasterizer unit tests cover the floor-lifted column + per-vertex
  thickness. (S17) §3.
- **Per-vertex height editing** — with a polygon selected, **click a vertex** to set its height (inspector
  *Vertex N height* field); every vertex shows its height as a **label** on the canvas (the shape's height
  profile), the selected one highlighted. Writes `anchor_heights[]`; on finish the rasterizer TIN-interpolates
  the slope (a raised corner ramps down across the footprint — verified `0→14` gradient in `layer_segment`),
  visible in Configure's height side-view. Click-vs-drag split by a movement threshold
  (`sketch-edit-controller`). (S5b) §3.
- **Height editing field + isometric 3-D preview** — the sketch inspector gains **Height (thickness)**
  (`base_height`) + **Floor (elevation)** fields on the selected shape; a **3D** toggle swaps the top-down
  canvas for a read-only **WebGL
  isometric** view (`render/iso-webgl.js`). Each shape becomes
  a prism (footprint extruded floor→top) or, for per-anchor shapes, a TIN-draped sloped solid; an
  orthographic camera at the true-iso elevation (yaw-rotatable) with key/fill/ambient lighting renders them
  on a ground-plane reference. Occlusion is resolved by the GPU **depth buffer** — correct and
  mirror-symmetric by construction (it replaced a bespoke SVG painter's-algorithm renderer whose single
  depth key occluded the two mirror halves inconsistently). The renderer is hand-written directly on the
  WebGL API (one Lambert shader + a small mat4 helper, reusing the in-repo `earClip` triangulator) — no
  scene-graph library, so it adds no vendored dependency. (S6) §4.
- **Iso draped-TIN slope** — per-anchor shapes (S5b) render in the iso as **sloped solids**: a
  TIN-triangulated top (JS `geometry/triangulation.earClip`, the twin of `Geom.Triangulation`) lit by the
  GPU from the scene lights, with walls whose top edge follows the vertex heights; their flat island
  prism is skipped. Mirror copies slope too (`applySymmetry` on the vertices). So a ramp/terrace is visible
  in 3-D while authoring, not only on finish. (S5c) §4.
- **Stacked layers (rasterization)** — `SketchLayout` gains an ordered `layers:[{ id, name, base_y, layout }]`
  (a legacy single `layout` loads as one layer at `base_y=0`). `SketchRasterizer.RasterizeColumns` rasterizes
  each layer in its own Y (primary + per-layer island mirror), shifts its columns by `base_y`, and concatenates
  — a column spanning multiple layers keeps **separate segments** (e.g. ground + a sky bridge, the gap
  preserved). `WriteSketchAsync` writes every segment to `layer_segment` and the surface row at each column's
  max top. Verified by unit tests + a DB-level finish (two Y bands, shared column carries both). (S7 —
  rasterization; editor UI is S7b) §5.
- **Stacked-layers editor** — a **Layers** panel in the sketch sidebar: add / select (active) / delete layers
  and set each layer's **name** + **Base Y**. The canvas edits the active layer with the **other layers
  ghosted** (faint dashed outlines, `renderGhostIslands`); the iso 3-D preview **stacks** every layer by
  `base_y` (a block floating 30 above the ground reads as a sky platform). The bridge holds multi-layer state
  (active index + per-layer shapes/islands) and persists the `layers[]` array (round-trips on reload). The
  `SketchLayers` component. (S7b) §5.
- **Canvas island selection + whole-island body-drag** — the Figma group model on the sketch canvas:
  **single-click selects the containing island** (drawing its axis-aligned **bounding box + corner
  anchors**), **double-click drills into the member shape** under the cursor (its resize/vertex handles),
  and **Esc** pops back out to the island / deselects. A **single-primitive island** shows the shape's own
  handles at the island level too, so single-click still resizes a lone rectangle (double-click is a no-op
  there). The whole island **body-drags** — all members translate together, snap-aware — via the shared
  `CanvasBase` move seam (`_hitMovable`/`_moveStart`/`_moveTo`/`_commitMove`) extended to a multi-shape
  handle; the bridge hands the canvas each island's id + member shapeIds + geometry (`setIslands`). Pure
  `boundsOfShapes` computes the island bbox (node-tested). The foundation for island rotate (`S13`, at the
  corner anchors) and the parked squash/scale (`S21`). (`sketch-canvas.js` + `sketch-bridge.js`; S20)
- **Rotate an island (Figma model)** — with an island selected, four **rotate zones** sit just outside the
  bbox corners (custom rotate cursor); dragging one turns the whole island about its **bbox centre**. The
  angle is the cursor's swept angle around the pivot — **distance-independent**, relative to grab, and
  **unwrapped** so you can spin past 360°; **Shift snaps to 15°**. A numeric **Rotate (°)** field in the
  inspector applies a rotate-by about the same centre (clears after each apply). Pure `rotateShape(shape,
  angleRad, pivot)` **bakes** the rotation into geometry — polygon/lasso rotate vertices + Bézier controls,
  a circle's centre orbits (radius kept), a rectangle promotes via `rectToPolygon` first (carrying its height
  fields); islands / mirror / rasterizer / iso recompute from the moved coords. (`geometry/shape.js`
  `rotateShape` + `sketch-canvas.js` rotate handle + `sketch-bridge.js` `rotateSelected`; node-tested; S13)
- **Squash / scale an island via the bbox anchors** — a selected island's bbox gets **8 scale handles**
  (4 corners + 4 edge midpoints): an **edge** stretches/squashes along one axis, a **corner** scales both,
  anchored on the opposite edge/corner — **Shift** locks a corner to a uniform scale, **Alt** scales about
  the centre; clamped so an island can't collapse or flip. Shown for multi-shape islands **and** a single
  polygon/lasso/circle (a lone rectangle already squashes via its own 8-handle resize). Pure `scaleShape`
  bakes it in: a rectangle stays axis-aligned (min/max scaled), a circle stays round (centre scaled, radius
  by the geometric mean — no ellipse type), polygon/lasso scale vertices + Bézier controls; islands / mirror
  / rasterizer recompute. (`geometry/shape.js` `scaleShape` + `sketch-canvas.js` scale handles; node-tested; S21)
- **Split tool — slice a shape in two** — a toolbar tool (scissors) whose **two clicks draw a slice line**;
  the shape the segment crosses is cut into two polygons in place (rubber-band preview, Esc cancels; a
  completed cut drops back to Select, a missed slice stays armed). Pure `splitShape(shape, a, b)` finds the
  segment's outline crossings and reuses the decompose cutter's `splitPiece` to arc-split the ring (first &
  last crossing for a concave >2-crossing shape); a rectangle promotes via `rectToPolygon` first, circles
  are unsupported. Both halves keep operation / override / base_height / floor (Bézier controls +
  per-vertex anchor_heights are dropped on a cut); the bridge replaces the shape with its two halves and
  recomputes islands. (`geometry/shape.js` `splitShape` + `sketch-canvas.js` split tool + `sketch-bridge.js`
  `splitAt`; node-tested; S14)
- **Selection outline highlight** — selecting on the sketch canvas now changes the **outline**, not just the
  anchors: the selected **shape's** outline (its Bézier curve) — or, for a multi-shape island, the **island's**
  outline (exterior + holes) — glows in **accent** (stroke + faint fill) in an always-visible overlay layer,
  independent of the **Shapes** toggle. So a drilled member is findable within a busy island instead of showing
  only its handles + a sliver of the shared outline. Follows move / rotate / scale / resize / vertex edits via
  the recompute path. (`sketch-canvas.js` `#renderSelectionHighlight` + `#selectionLayer`; S22)

## Analysis-backed authoring (backends — UI tracked in TODO)
- **Analysis endpoints over the ported services** — `GET /buildability`, `GET /traversability`,
  `GET /wool-availability`, `GET /monument-obstruction` (each wool monument's block must be air; flags a
  solid cell that blocks placement, over the `SegmentIndex`), `POST /wool-sources` (wool colours summarised
  inside a drawn rect — `{bounds}` → per-colour totals/types/repeatable, over the wool-block + PGM-spawner
  sources), `GET /wool-suggestions` (wool colours found in the world but not declared as objectives) and
  `POST /resources` (iron/gold/diamond blocks, optionally in a drawn rect, + how many a `<renewable>`
  already covers — renewable auto-config). The authoring overlays/panels that consume them are TODO
  `N03` / `NVAL` / `N04`. (F6, F2, F7)
- **Kit-reach (budget-aware traversability)** — `GET /kit-reach`: can a fresh spawn bridge to each wool
  with only the placeable blocks its spawn kit grants? Reuses the `Traversability` grid but runs a 0-1 BFS
  (walkable 0, bridgeable 1 = one placed block) for the cheapest bridge cost per spawn→wool, vs. the kit's
  placeable-block budget (`KitBlocks`) → ok/warning/error. Walkable ground = the floating-mass-pruned
  **cleaned base** (`SegmentIndex.BaseColumns` + `IslandDetector.CleanedBaseFootprint`), so a build floating
  over void can't pose as free standing-ground in the Y-agnostic 2D grid. Per-life lower bound (kits refill
  on respawn). n00_demo: 96-block kit, own wools 6, far wools 24 (one 12×6 + the 18×20 middle).
- **Filter↔region wiring templates** — 4 v1 appliers + `POST /wiring/apply` (the suggestion engine
  was deliberately removed). The generator uses these to auto-wire; the hand-wiring UI is parked.
- **Symmetry-aware authoring** — counterpart creation + orbit-fill on draw
  (`POST /regions/{id}/counterpart`, `/orbit`) + the Orbit toggle. The generator orbit-fills
  automatically; the accept/reject UI + IoU equivalence detection are parked.
- **Side-view Y editing** — `SliceView` cross-section + draggable Y line (point/block) wired in Build +
  Objective inspectors; lifts a region off `y=0` onto the surface. Authoring integration is TODO `N08`.
  (`new-map-authoring.md` §8)
- **Region grouping interaction** — Ctrl-click multi-select, Ctrl+G group/ungroup, shortcut registry,
  `POST /regions/group` + `/ungroup`. (ex-R1a; wire-after-group is parked.)

## Data & ops (D)
- **Map XML refresh** — `--refresh-xml` re-derives every map's entities via the editor write path
  (preserves world features/artifacts); recovered annealing_iv's missing region, which fixed the
  former stale-DB symptom. (D1, closed C10)
- **Dropped Bootstrap** — dashboard migrated to the studio shell; default `EditorLayout`;
  `/design` reachable from the dashboard footer link. (D4, satisfies D2)
