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
  orbit**. → `intent.spawns`. (`SpawnPhase`; N02)
- **Teams · Spawn protection sub-step (N02)** — the **rectangle tool** draws a protection zone around a
  spawn; the confirmed symmetry orbit-fills the rest. Zones render as resizable **dummy regions** on the
  reused canvas (new `EditorCanvas` `RectDraw`/`OnRectDrawn` + `setAuthorRegions` — geometry goes to intent,
  not a `POST /regions`), so draw/select/resize reuse the editor's region handles; edits route to
  `intent.spawns[].protection`. The inspector surfaces the generator's wiring in an **Auto-wiring (derived)**
  section (`enter=only-<team>` + `block=never`); the generator builds the `spawn/protection` rectangle +
  filters on regenerate. (`ProtectionPhase`; N02)
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

## Analysis-backed authoring (backends — UI tracked in TODO)
- **Analysis endpoints over the ported services** — `GET /buildability`, `GET /traversability`,
  `GET /wool-availability`, `GET /monument-obstruction` (each wool monument's block must be air; flags a
  solid cell that blocks placement, over the `SegmentIndex`), `POST /wool-sources` (wool colours summarised
  inside a drawn rect — `{bounds}` → per-colour totals/types/repeatable, over the wool-block + PGM-spawner
  sources), `GET /wool-suggestions` (wool colours found in the world but not declared as objectives) and
  `POST /resources` (iron/gold/diamond blocks, optionally in a drawn rect, + how many a `<renewable>`
  already covers — renewable auto-config). The authoring overlays/panels that consume them are TODO
  `N03` / `NVAL` / `N04`. (F6, F2, F7)
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
