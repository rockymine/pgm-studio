# pgm-studio — Shipped features

What the app does today. The live task board for **open** work is **`TODO.md`**; this file is the
catalog of **landed** capabilities — the "done" half that used to clutter the board. One line per
capability, grouped by area, with the task id(s) that delivered it (for git traceability). This is
**not** a changelog: describe the capability, not the diff. Add an entry here the moment a task leaves
`TODO.md` as done.

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
  Configure mode) — 3-step wizard (scan-layer → island-exclude → symmetry confirm) with a dedicated
  layer/islands/symmetry preview; finish → Overview. (E6, E8)
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
- **`SmartSuggestion` component** + symmetry-derived intelligent team creation (reads `/symmetry`,
  suggests 2/4 palette teams). (C15)
- **`Toast` error component** — shared across activities. (from C12)
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

## Pipeline / world import (M7)
- **Anvil `.mca` reader** — byte-exact vs Python. (P1)
- **Feature extractors** — wool / resource / chest / spawner / segments, 11/11 parity. (P2)
- **`POST /scan-world`** — world → DB feature rows. (P3)
- **Surface scan + island detection** — `layer.parquet` / `islands.json` / `map_config` artifacts. (P4)
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
  `reviewStatus`). Root-cause analysis of the over-split mode in `docs/contracts/lane-decomposition.md`. (G9)
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
  `WoolIntent.Room` is now **nullable** — a roomless wool (the author hasn't drawn its room yet) still
  generates its objective + monuments and skips the room region / spawner / wiring, instead of failing
  intent deserialization (a `null`→non-nullable-`Rect` 500). Verified end-to-end on n00_demo (2-team
  `mirror_x`, 2 wools/team): 4 wools + 4 monuments, valid CTW XML (`<wool team>` = the monument-derived
  capturer, as PGM requires). (N04)
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
- **New-map landing (Import flow)** — `/maps/new`: **Source** lists importable world folders and scans the
  chosen one (`POST /map/import-folder`); **Found** shows the detection brief over the reused editor canvas
  (island base + surface overlay), with each finding selectable for a detail explanation — island sizes,
  wool colours + resource types (`GET /map/{slug}/scan-summary`), chest count — and symmetry / suggested
  teams as inline facts; **Plan** presents the six phases as cards, then Start → the wizard at Map Info.
  Reuses `ConfigureRenderer` via `scan-canvas.js` and a generic `.card` / `.card-grid` / `.callout`. (NS)
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
  seeded — same seed → same layout. (`docs/contracts/organic-lane-generation.md`)
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
- **Lane-decomposition surface (manual cut tool)** — `/maps/{slug}/decompose` (dashboard footer →
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
