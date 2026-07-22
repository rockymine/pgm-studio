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
- **Plan editor entry on the landing** — the studio landing (`/`) leads with a featured *Plan a
  layout* origin card (author a coarse cell-grid seed → compile straight into a sketch draft), set
  above a labelled `or work a map through its stages` divider from the three lifecycle cards; the
  old footer *Plan* link is retired. A horizontal `.landing-plan` variant of the `.card--action`
  surface, grouped with the trio under `.landing-choices` + a hairline `.landing-divider` — all
  theme-token based (verified light + dark). (G70)
- **Centred staged map-overview list** — the `/maps` (and `?stage=sketch|configure`) result column
  (`Home.razor`) had a `max-width: 960px` but no horizontal centring, so it hugged the left edge unlike
  `/maps/new` and `/maps/new-sketch`; add `margin: 0 auto`. Verified: equal left/right gaps. (C20)

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
- **Panel resize (all editors)** — drag any `.sidebar-handle` bar to resize the panel it borders — the left
  `.workspace-sidebar` (drag right → wider) or the right `.workspace-inspector` (drag right → narrower) —
  clamped to **[200, 560] px** so a panel can neither collapse nor crowd the canvas. One delegated
  document-level pointer listener (`js/studio/shared/panel-resize.js`, self-installed once from `studio.js`)
  serves every editor at once and survives Blazor re-renders; the chosen width is written inline, overriding
  the shared `--sidebar-width` / `--inspector-width` token. The CSS handle shell (hover/drag accent line, now
  `touch-action: none`) already existed. Verified live in the plan editor: both handles resize + clamp at MAX.
  (C8)
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
- **Core UI component vocabulary (the atomic tier)** — `Button`/`Badge`/`Chip`/`Field`/`Section`/
  `SectionHeader`/`ListRow` under `Components/{Primitives,Forms,Data}/`, param-first with slot
  overrides, emitting the canonical CSS classes (zero visual diff vs `/design`). Adopted in the
  `/generator` filter rail (retiring the `gen-*` sidebar drift) and the `/maps` list. Contract:
  `docs/contracts/ui-conventions.md`. (C12 phase A)
- **App-shell components** — `StudioShell` (`editor-page` + optional rail/viewport/footer, with a
  `Bare` mode for custom bodies) + `Topbar`/`Crumb`/`ActivityRail`/`ActivityButton`/`AppFooter`/
  `AppFooterLink` under `Components/Layout/`. Adopted across all 11 `editor-page` sites, retiring the
  copy-pasted topbar / activity-rail / footer chrome. (C12 phase B)
- **Workspace-shell components** — `Workspace` (the flex row), `Sidebar` / `Inspector`
  (`workspace-sidebar|inspector` + inner `workspace-scroll`, with a `Footer` slot for the sidebar nav
  and `style` pass-through), and `ContentColumn` (the centered max-width "vertical content page") under
  `Components/Layout/`. Adopted across the ~28 EditorActivity / Configure-phase / Sketch / Plan
  surfaces; `sidebar-handle` bars stay raw so `panel-resize.js` keeps resizing by DOM sibling. (C12 phase C)
- **`Section` adopted across every production surface** — the hand-typed `panel-section` →
  `section-header` → `section-title` skeleton is gone; ~95 sections across 31 Configure / EditorActivity /
  Sketch / Plan files render `<Section Title=… Required? >` with `Actions`/`Header`/`Footer` slots (0 raw
  `panel-section` outside the `/concepts` + `/design` leave-raw zone). `Section` gained a `Required`
  asterisk param and `CaptureUnmatchedValues` (`style`/`id`/`@key` pass-through). Zero visual diff — the
  component emits the identical classes. (C12 phase D.1)
- **The atomic vocabulary adopted across every production surface** — the raw class markup for the five
  atoms is retired in favour of the components: `field` → `<Field>` (~102), `action-btn` → `<Button>`
  (~66), `badge` → `<Badge>` (~67), `list-row` → `<ListRow>` (~50), `filter-chip` → `<Chip>` (~23),
  across ~30 Configure / EditorActivity / Sketch / Plan / Generator files. `Field` gained a `LabelHint`
  slot (inline label notes); dynamic variants pass the verbatim ternary via `Class`. The few legitimate
  raw holdouts stay (a `<label class="action-btn">` wrapping `InputFile`; `SliceView`'s header-embedded
  field). Zero visual diff — the components emit the identical classes. (C12 phase D.2)
- **`CoordField` + `DetailHeader` components** — the repetitive coordinate cell (`coord-prefix` chip +
  `coord-input`, covering editable / read-only / disabled inputs via params and the `NumberField` variant
  via a `ChildContent` slot; ~35 uses) and the inspector detail head (`geo-type-icon` + `detail-label` +
  optional trailing badges, with `IconMuted`/`IconStyle`/`Mono` params; 28 uses) componentized under
  `Components/{Forms,Data}/` and adopted across ~20 Configure / EditorActivity / Sketch / Plan files.
  Zero visual diff. (C12 phase D.3)
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
- **Shared symmetry label + single-source orbit count** — the friendly symmetry wording (`"Mirror X
  (left/right)"`, `"Rotate 90°"`, …) was copy-pasted as a private `SymLabel` in four places
  (`WorldScanPhase`/`WorldSymmetryPhase`/`ConfigureLanding`/`ConfigureActivity`) plus a `SymLabelShort` in
  `TeamsPhase`; collapse them into one `Client/Models/SymmetryInfo` (`Label` + `ShortLabel`). The orbit
  *count* re-derivers (`BuildLayerPhase.SymmetryOrder`, the `SuggestedTeams`/`SuggestedCount` in
  `ConfigureLanding`/`WorldSymmetryPhase`/`TeamsPhase`) no longer re-encode the `rot_90 → 4 / else → 2`
  magic — they route through the `Geom.Symmetry.Order` leaf (`> 1 ? order : none`), which also fixes two
  latent edge cases (a `none` mode no longer counts as a mirror; `mirror_d1`/`d2` now suggest 2 teams on the
  landing). Presentation labels stay in `Client`; the count stays in `Geom`. The plan/sketch symmetry
  *pickers* are a separate concern (short author-chosen option lists, no diagonals) and are unchanged. (CV8)
- **Side-view max-Y clamp reaches the surface** — the Build-step draggable Y line was clamped one block
  short (`_applyHeight` → `y_min + y_count - 1`) even though the render math (`_lineCanvasY`) lets the line
  sit atop the highest block at `y_min + y_count`; raise the clamp by one so you can drag onto the topmost
  surface block. (CV11)
- **Unified primitive drawing styles across the four editors** — "draw a primitive" is now one data-driven
  thing: `renderShape` grows a real `point` case (a fixed-screen-radius circle, so a point stops rendering
  as a zoom-shrinking 1×1 rect and the Edit/Configure `marker` circle-branch collapses into it), and a
  shared `render/primitive-style.js` `primitiveStyle(treatment, {color,…})` holds every treatment recipe
  (`region`/`marker`/`sketch`/`terrain`/`technical`/`zone`, each with ghost/selected states) with colour
  always caller-supplied. It replaces `editor-canvas`'s `#regionAttrs` + marker attrs + the triplicated
  `#refreshRegionDisplay` numbers, sketch's `shapeAttrs`, and the inline plan piece/zone/ghost styling; the
  duplicated add/sub colour constants collapse to one `OP_COLORS`/`opColors` source (sketch render + draw
  controller). Icons route through `RegionNode.Icon` — `SpawnPhase`'s hardcoded `cylinder` and
  `WoolMonuments`' `square` become the canonical `point → dot`. Plan's surface-tint + hatch stay
  Plan-specific. Audit + design: `docs/contracts/primitive-styles.md`; canvas-interaction.md §10. (CV9)

## Backend / API (B)
- **`--parity` retired — PGM is the reference for the `map.xml` contract, not the Python oracle.** The
  harness compared `Serializer.ToDict(parse(map.xml))` against the reference's `xml_data.json` and had read
  **2 ok / 342 failed** for a long time, so it gated nothing. The red was not drift: the C# contract
  deliberately **exceeds** the reference's in at least four places — kit `force`/`effects`,
  `destroyables`/`cores`/`modes`, and the OB4 group-attribute inheritance the reference gets **wrong**
  (`tebulas_ii`'s 12 wools). On those the oracle is *silent*, not authoritative, so the comparison could
  never go green without teaching it a growing list of exceptions to its own claim. Deleted rather than
  patched, and `CLAUDE.md` now says what the reference is. Only the `map.xml`-contract check goes: the
  **analysis** oracles (`--categorize`/`--buildability`/`--traversability`/`--wool`/`--extract`/`--islands`/
  `--authoring`) compare derivations **both** sides own, were never implicated in this, and are untouched.
  (B30)
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
- **Deterministic Api.Tests (shared-schema isolation)** — the endpoint tests flaked (non-deterministic
  8/12/18 failures: "expected 1 but found 92", slug dedup, author-patch 404s) from a shared-schema race. Two
  root causes fixed: (1) minimal hosting resolves `ConnectionStrings:PgmStudio` from the **environment** ahead
  of a factory's `UseSetting`/`ConfigureAppConfiguration`, so an ambient dev-server `ConnectionStrings__PgmStudio`
  silently pointed every test at the live dev DB (never reset → counts accumulated) — a `[ModuleInitializer]`
  now pins the env var at `pgm_studio_test` before any host boots; (2) the read-only Plan/Health factories set
  no connection at all. Both now boot the one shared `ApiTestFactory` (forced test schema), and all eight
  DB-touching classes share a `[NotInParallel("api-db")]` group so no per-test reset overlaps another. Verified
  deterministic: **4 consecutive green runs**, including with an adversarial `ConnectionStrings__PgmStudio=dev`
  set (the dev DB row count stayed flat — tests no longer touch it). Consolidates 4 duplicated per-class
  factory/reset copies into one. (B20)
- **Objective-module gate** — the parser read only the tags it named, so a map's objective could vanish on
  round-trip with no error. `EnsureSupported` now rejects any map declaring an objective module it cannot
  read, joining the proto/modern-world gates. The line is PGM's own: a module contributing a **non-auxiliary
  `Gamemode` MapTag** is an objective (`wools`/`destroyables`/`cores`/`control-points`/`king`/`payloads`/
  `flags`/`score`); auxiliary modules (`blitz`, `ffa`, `rage`) modify play rather than the goal and stay
  ignorable. Corpus-verified over the 350 slugs: 12 rejects, exactly the maps carrying an unread objective.
  (B22, OB10)
- **DTM: destroyables + objective modes — parse, write, codec.** `<destroyables>` and `<modes>` now
  round-trip: `Destroyable` (owner · region · materials · completion · show · mode membership) and
  `ObjectiveMode` (after · material · show-before · filter · action) on `MapXml`, through `Serializer`/
  `Deserializer` and back out as XML. Grounded in PGM's own parser: **attributes cascade from every
  enclosing group** (`Xml.Flatten`, shared by wools/destroyables/cores — OB4), `materials`/`material` are
  both accepted, `completion` is a percentage with or without its `%` (`0.8` means 0.8%), mode membership
  is a **tri-state** (`modes="a b"` · `mode-changes` · neither) and combining the first two is rejected
  (OB9), and a `<region>` wrapper is the **union** of everything in it. The writer emits the flat canonical
  form — one block, explicit attributes, no nested groups (OB5). PGM's legacy bare-geometry region form is
  deliberately not ported: it appears only in proto 1.3.0/1.3.3 maps, already below the floor (OB6).
  Verified over both corpora: **188 maps / 619 destroyables / 153 modes parse, every region resolves, and
  191 maps round-trip through the writer with zero drift**; `alpine_mining_ii`, `abstract` and `sentient`
  reproduce the contract's worked examples exactly. (B24a, OB4/OB5/OB6/OB9/OB13)
- **Wool group-attribute inheritance (a live parse bug the same OB4 work fixes).** Wools inherited only
  `team`, so a wool declaring its `color`/`location` **only on the enclosing `<wools>` group** parsed as
  colourless at `0,0,0` — `tebulas_ii` lost all 12, and `firestone_lake_research_facility`,
  `road_trip_to_sunset_town` and `stratosphere_ctw` lost their locations. The reference app has the same
  bug (its oracle emits `color: ""` too), so this is a **deliberate, PGM-grounded deviation** from the
  oracle rather than drift. (B24a, OB4)
- **Phantom destroyables — a destroyable is not always an objective.** 8% of them are **scripted
  block-swap regions** that borrow the element purely to carry a `<mode>`. `Destroyable.IsObjective` names
  the concept so no consumer has to rediscover the discriminator, and `Destroyable.Phantom` splits the two
  sub-kinds: **`BlockSwap`** (a mode replaces its blocks at a match time — the pre-game build floor erased
  at `0s` is the common case, but the target is also water lanes and a wool disco floor) and **`Trigger`**
  (no mode; broken to fire a filter). The test is exact and semantic — **a goal players cannot see is not a
  goal** (`show="false"`) — not `completion="0%"` or `required="false"`, which flag genuine objectives.
  Serialised as a `phantom` key beside `show`. Corpus-verified against the contract's figures **exactly**:
  over all 1603 maps in both corpora, 297 carry `<destroyables>` / 959 leaves, of which **80 phantoms (8%)
  across 39 maps — 70 block-swap, 10 trigger** (deathrun_aperture's ten levers), and **30 maps are
  phantom-only**, i.e. PGM tags them DTM and they are not. (B27, OB16)
- **Gamemode is derived from the modules, not read off the `<gamemode>` element.** The element was parsed
  into a scalar `MapXml.Gamemode` **defaulting to `"ctw"`**, and both halves were wrong. Now split in two:
  `DeclaredGamemode` is the author's label verbatim (empty when absent — never invented) and **`Gamemodes`
  is the derived set**, the truth, since PGM decides the mode by which modules parsed. It is a **set**
  because CTW/DTM/DTC coexist. One deliberate deviation from PGM: a module contributes only if it holds a
  **real** objective, so a phantom-only map is not DTM whatever PGM's tag says (needs B27). Corpus-verified
  over 910 in-range maps: **70% declare no `<gamemode>` at all** (the default was fabricating it), **137
  declare one that disagrees with their own modules** (`ad`, `CTW`, or `ctw` on a DTM map), and **12 carry
  more than one gamemode**. `abstract` derives `[ctw]` not `[ctw,dtm]` — the carve-out working; `sentient`
  derives `[ctw,dtm]`; `alpine_mining_ii` derives `[dtm]` while declaring nothing. The `map.gamemode` column
  holds the label — round-tripped as written, and read-only. (B23, OB7/OB15/OB16)
- **The derived set is what the studio shows.** `Domain.Gamemodes.From(hasWools, hasRealDestroyable, hasCores)`
  is the one home for the rule; `MapXml.Gamemodes` and `MapRepository.GamemodesAsync` both route through it,
  so the parser and the list can't drift. **The set is computed, never persisted** — no `gamemode_derived`
  column to keep in sync, and the whole list costs three `DISTINCT map_id` lookups rather than a join per map.
  `MapSummary.Gamemodes` carries it to `Home.razor` as one tag per mode (`sentient` lists `CTW DTM`), and the
  Overview shows it read-only: it is derived, so typing could not change it, and `PATCH .../metadata` no longer
  accepts a `gamemode` key. A map with no objective module we read carries **no** gamemode rather than a blank
  tag — true of every sketch, and of no imported map. Live: 362 `[ctw]`, 1 `[ctw,dtm]`, 1 `[ctw,dtc]`;
  `lindorm` shows `CTW` despite declaring `ad`. (B32, OB7/OB15)
- **DTM: authoring a destroyable, plan → world → `map.xml`.** A `destroyables` marker in the plan editor
  compiles the wool way — team-outer, so each orbit image belongs to the team it lands on, with no monument
  mapping since every other team breaks the same structure. `{ piece, at }` is a complete, typical
  destroyable: the compiler defaults `style`/`materials`/`float` to the corpus's own centre of mass
  (pillar-3 · obsidian · 4) and derives the name PGM requires from owner and index (`Red Monument`,
  `Red Monument 2`) rather than asking. **OB8 is enforced by construction**: only the world-export path knows
  the terrain the box floats over, so it resolves each `ObjectiveStamper.DestroyableBox` once, stamps it, and
  carries it back on `DestroyableIntent.Box` — the generator emits that same box as the `<region>`, and a
  destroyable whose box is unresolved emits nothing rather than a guessed region. Verified end to end by
  walking the emitted region with PGM's own `[min, max)` semantics (OB13) and finding exactly the 3 stamped
  blocks. **OB14 is enforced twice** — the editor offers the tool only at `Symmetry.Order == 2`, and the
  validator errors on a hand-written `rot_90` plan that asks anyway, since a shared DTM goal is an open
  design question and compiling one would invent an answer. An unknown style is likewise an error, never a
  silent default. `DestroyableStyle` + its slug vocabulary moved to `Domain` (the lowest project the plan
  layer and the stamper both reach), and the client's kind→list dispatch became a keyed lookup — it was a
  ternary chain ending in `iron`, so any new kind would have silently placed, selected and deleted iron
  markers. (B24a/B24c/B24d, OB8/OB12/OB13/OB14, DT3)
- **DTC: authoring a core, plan → world → `map.xml`.** A `cores` marker rides the destroyable path; the delta
  is the casing. `{ piece, at }` compiles to DC1's modal core — a 5×5×5 obsidian shell 1 block thick, capping
  a 3×3×3 lava interior, floating 6 — and the knobs (`size`/`height`/`shell`/`openTop`) exist for the
  exceptions. **`float` and `leak` are enforced as one knob** (DC2): together they set how far players dig
  (`max(0, leak − float)`, the defaults giving 0), so authoring one alone is an **error** rather than a silent
  pairing with the other's default — a dig depth nobody chose. The validator also rejects a casing with no
  room for lava (`size − 2·shell < 1`), which stamps a solid block: a goal that can never leak, so never
  captured. Three things a core does *not* get: a **material** (obsidian is universal, PGM defaults to it, so
  no attribute is emitted), an invented **name** (PGM auto-names a core per team — unlike a destroyable, which
  it rejects nameless), and a **`leak` attribute** when it matches PGM's own default. The XML's `team`
  spelling (OB1) is `XmlWriter`'s alone — the doc tree says `owner` like everything else, and emitting `team`
  there parses back as an unowned core (caught by the round-trip test, not the generator's). Verified end to
  end: 27 lava fully wrapped by 98 obsidian, cap on, floor intact, floating clear of the terrain, and the
  emitted region walked with PGM's `[min, max)` holds all 125. The structure defaults now live once in
  `Domain.ObjectiveDefaults`, shared by the compiler that resolves them and the stamper that builds them.
  (B25a/B25c/B25d, DC1/DC2/OB1/OB8/OB13/OB14)
- **DTC: cores — parse, write, codec.** `<cores>` round-trips as `Core` (owner · region · material · leak ·
  mode membership), contributing `dtc` to the derived gamemode set. Structurally the destroyable with a
  different owning attribute, so it reuses `Xml.Flatten`, `ResolveObjectiveRegion` and the tri-state mode
  membership **unchanged** — which is what B24a's shared work was for. The owning attribute is `team`, not
  `owner` (a PGM inconsistency with a standing TODO in their source); the field is `Owner` (OB1). Unauthored
  `material`/`leak`/`name` stay unauthored rather than being materialised, so PGM's own defaults (obsidian /
  5 / per-team `Core`, `Core 2`) still apply and the map round-trips. The corpus leans on inheritance even
  harder than DTM: **`leak` is declared on the group in 318 of 320 cases and `modes` in all 76**. Verified
  over both corpora: **127 maps / 300 cores parse, every region resolves, zero round-trip drift**, and
  cores rejoin the parsed objective set (1036 maps now in range, up from 910).
  (B25a, OB1/OB4/OB9)
- **The DTM/DTC contract, checked end to end against its own claim.** With B22–B25 in, the 10 maps B22 had
  gated all parse, and the gamemode they derive independently reproduces what the contract asserts:
  **8 of the 10 are phantom-only pure CTW** (abstract, abstract_remix, citadel, down_side_up,
  fairy_tales_metamorphose, mine_your_own_business, newgen_classic, vesuvius → `[ctw]`), and **only
  `sentient` (`[ctw,dtm]`, 8 real destroyables) and `bungee_coorde` (`[ctw,dtc]`) are genuine**.
  down_side_up's 24 modes are its documented 12-step colour cycle. (B22/B23/B24a/B25a/B27)
- **Objective persistence — `destroyable` / `core` / `mode` tables** (`M0007`). All three hang off `map_id`
  per the hybrid rule (real columns for what we list and edit; JSON only for the irregular mode-id list),
  and deliberately **do not reuse `monument`**, whose `wool_id` FK is `NOT NULL` — a destroyable has no
  wool, so that FK makes a wool-less objective unrepresentable. `show` is a queryable column, since a map
  whose every destroyable is hidden is not DTM. Unlike wools, neither needs the doc-tree codec bypass: they
  are flat records with no grouped shape to lose, so they ride `MapXml` through `MapWriter`/`MapReader` like
  every other entity. Verified against real MariaDB (write → read → assert, incl. the phantom, the
  null-vs-empty mode set, and cascade delete) **and end to end over the dev corpus**: `--refresh-xml`
  refreshes 346 maps through the editor write path, is idempotent on a second run (0 changes), and lands 22
  destroyables / 2 cores / 33 modes. (B24b, B25b, §11)
- **Objective structure stamps — `ObjectiveStamper`.** The world half of DTM/DTC, built to the
  **world-measured** corpus families rather than to the XML (a hand-authored region is a loose box drawn
  around the structure and says nothing about its size). Destroyables: `pillar-1|2|3` (a 1×N column — 56%
  of the corpus and the simplest stamp in the system), `cube-3`/`cube-4` with an optional concentric
  **bedrock centre** that is invisible to the goal because `materials` names only the outer block (DT2), and
  `column-plus`, dynamite's 3×3 cross — 5 blocks a layer, corners open, which is the family's signature
  (DT4). Cores: a casing enclosing lava, **5×5×5 / shell 1 / 3×3×3 lava / capped top** by default, open-top
  a flag (DC1). Both **float** (DT3/DC2), so no carve, void or negative-space primitive is needed, and the
  base clears the highest ground the *footprint* spans — not the anchor column, which is a grid line whose
  one-sided sample would not survive the symmetry orbit. `DigDepth` makes DC2 explicit: leak and float are
  one knob (`max(0, leak − float)`), neither meaning anything alone. **`BlockBox` is the one box function
  OB8 demands** — shared by the stamp and the region generator, with `CuboidMax` encoding OB13 (a
  cuboid spans `[min, max)`), because a region that misses its structure yields a silent zero-health goal.
  `Blocks` gains obsidian, gold, emerald, end stone and lava. (B24d, B25d, DT1–DT4/DC1–DC2/OB8/OB13)
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
- **Spawns seat on terrain (N11)** — a spawn placed with the **point tool** lands on the column's floor
  instead of Y 0: team spawns + their orbit copies, the observer, and wool spawns all route through one
  `ColumnFloor` helper, which owns the +1 (`column-floor` reports the topmost solid block *inclusive*, so
  resting on it is floor + 1). A wool anchors the search at its **own level**, since it usually sits in a
  covered room whose roof would otherwise be the column's topmost surface. The side-view Y line **snaps to
  the floors of the marker's column** (`seatOnFloor`, opt-in via `SliceView.SeatOnFloor`) so it can't be
  dragged into a block or mid-air — a vertical run offers each of its floors; a region's Y stays free. The
  slice line tracks a Y that changes on its own, without refetching the depth map.
  (`ColumnFloor`, `SpawnPhase`, `WoolSpawnPhase`, `WoolObjectivesPhase`, `SliceView`, `sideview-canvas.js`; N11)
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
- **Lane sketch generators + Organic-generation demo — RETIRED** in favour of the plan-then-realize
  direction (`docs/contracts/map-generation.md`): the archetype starter generators (`LaneSketchGenerator`
  for H · Pinwheel · Trident · Organic, `OrganicLane`, `LaneMapGenerator`, `SketchLayoutPrep`, `AutoBridge`)
  and their surfaces are removed — the `POST /api/sketch/generate` + `/api/sketch/generate/stages` endpoints,
  the new-sketch "Generated layout" tab, and the `/concepts/organic` demo page (`render/gen-stages.js`,
  `studio.renderGenStages`) with them. The sketch tool now originates only from a blank framed canvas; a plan
  is authored in the plan editor and compiled instead. `SketchLayout` (the layout DTO), `SketchRasterizer`
  (finish/rasterize) and `IslandSimplifier` (island-import) live on — none depended on the generators.
  (was G4 / G5)
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
- **Plan-editor iso structure preview** — the `/plan-editor` 3-D view renders the structures the world build
  will stamp, in their materials, so the author sees what lands in the columns they drew: **spawn cubes** and
  **wool cages** (the 8×8×9 shells, team / wool colour), **iron cubes** (4×4×4), **approach walls** (bedrock,
  y=0→`TopY`), and the **wool-room prism tinted bedrock** — `RoomFloors` *is* that piece's fanned rect, so it
  tints the box already drawn instead of stacking a coincident one. Shells only; everything else stays grey.
  `PlanStructurePreview` (`Api/Services`, beside `SketchWorldBuilder` — the one project reaching both `Pgm` and
  `Minecraft`) derives the boxes from `PlanCompiler` output sized by the stampers' own constants/footprint
  helpers, normalizing their differing conventions (iron footprint max-inclusive; room floors / walls
  max-exclusive; wall `TopY` inclusive) into one min-inclusive/max-exclusive frame. Served on
  `POST /api/plan/inspect` (error-tolerant + already per-edit, unlike `/plan/compile`, which withholds its
  intent on structural errors — i.e. most of a live edit); colours ship as slugs because the wool dye
  assignment is a global cursor across the team loop, which a JS twin would drift from — the client maps them
  through `render/palette.js`. `iso-webgl.js` batches by colour (one draw per distinct material, opaque:
  translucency needs a depth sort the mirror image defeats). Tests compile a seed both ways and check every box
  against the blocks actually stamped, so a preview that lies fails the build. (G73)
- **Structure floors are symmetry-equivariant** — a structure and its orbit images now rest at the same height.
  They did not: a cube/iron anchor is a grid *line* the footprint straddles, and the floor was probed as the
  single block on its + side (`surfaceTop.GetValueOrDefault((anchorX, anchorZ), 1)`), which does not survive the
  orbit — `FanPoint` maps grid line `g → -g` correctly, but the mirror of *block* `g` is `-1-g`, so the images
  read the + side of one against the − side of another. Where a marker sat at a terrain edge, one image found
  ground and its mirror took the `, 1)` fallback and built **into the void at y=1** — measured on
  `isolated-spawn`, whose two iron cubes covered an identical 8/16 columns at top 13 (the geometry fanned
  perfectly) yet resolved baseY 1 vs 13. `PositionSnap.SurfaceYOver` now derives every structure floor from the
  footprint it occupies (highest top among its columns) — equivariant by construction, since a footprint is its
  own mirror — via `CubeStamper.Footprint` / `StructureStamper.IronCubeFootprint`, in the iron stamper, both
  cube kinds (`SketchWorldBuilder`) and the G73 preview alike. Room floors / redstone lines probe per-column and
  were never affected. Surfaced by the G73 preview reporting true floors. (G74)
- **Plan-editor derived-geometry overlays** — `POST /api/plan/inspect` (the canonical C# derived-structure feed
  for the editor's canvas; plan JSON in → ready-to-draw block-space overlay geometry out; malformed body → 400):
  derived **land interfaces** (cased-green seams; sliver/corner red), **gap links** with hop-distance labels
  (suppressed between pieces of the same land component — a walkable pair is no void crossing), and computed
  **frontline** edges — persisted overlay toggles drawn in a dedicated canvas layer. The bridge re-inspects
  debounced (~300ms, stale-response guarded). `PlanFinding` carries subject ids (read by the compiler + the
  evaluator). Originally shipped a live lint panel off this same feed; the **evaluator Score panel** (G60) is now
  the single validation surface, so `/plan/inspect` serves only the geometry overlays. (G19)
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
- **Plan editor sidebar / toolbar declutter** — the left sidebar is now a **collapsible settings panel**
  only (plan name · symmetry & globals · reference · overlays), folded by a thin settings **rail** (the
  studio logo + a sliders toggle, matching the other editors) so the canvas reclaims the width. The draw
  tools (piece / spawn / wool-room · build · wool / spawn / iron / wall markers · buffer / connector)
  moved out of the sidebar into the **canvas toolbar** as compact **icon-only** buttons sharing the
  `.draw-tool-btn` box with select / move — a solid role-colour swatch for terrain pieces, a glyph for
  markers, and **canvas-matching pattern swatches** for the tools whose fill is a pattern (build = the
  dashed translucent accent zone via CSS; buffer = its single-diagonal reserved-gap hatch, connector = its
  crossed attachment hatch — a centred inline SVG, 2 / 4 lines symmetric about the swatch centre), grouped `select · move │ piece · spawn · wool-room ·
  build │ spawn · wool · iron · wall │ buffer · connector` with separators; the visibility toggles moved the other
  way — out of the toolbar into an **Overlays** section in the settings panel. All tool/overlay wiring
  unchanged (same bridge calls); verified light + dark. (G71)
- **Plan editor validation activity + panel-edge collapse** — the left panel is now a rail-selected
  **activity**: a second rail icon (**Validation**, below Settings) switches the sidebar between *Settings*
  (plan name / globals / reference / overlays) and *Validation* (the evaluator score + fired rules, moved
  out of the right inspector so it no longer competes with the selection inspector). Selecting Validation
  switches on the **Rules evidence layer**, so the activity itself is the layer toggle — the Rules chip is
  gone from Overlays, and the layer follows an **open** validation panel (not a persisted flag), off in
  Settings or when collapsed. Each rail icon **toggles its own panel**: clicking the active-and-open one
  collapses the sidebar, clicking any other case opens/switches — so the icons handle both switching and
  hide/show, no separate collapse control. Click-a-fired-rule-to-isolate its evidence carries over. Verified
  light + dark. (G72)
- **Plan editor 3-D isometric height preview** — a read-only **3D** toggle in the canvas toolbar swaps the
  top-down view for an isometric render of the plan's terrain massing: each generating piece is extruded from
  the ground to its resolved surface height (annotation buffer/connector pieces and build zones produce no
  terrain and are skipped), with a mirror copy per orbit axis so the symmetry ghost stacks too — elevation
  differences read spatially while planning. Reuses the sketch tool's WebGL renderer (`render/iso-webgl.js`)
  unchanged: a new pure `planIsoSolids(doc)` in `plan-doc.js` maps pieces/surfaces onto the same
  `{exterior, top, floor, mirror}` solids the sketch iso consumes, so occlusion is GPU depth-buffered (taller
  columns occlude) and the mirror stays consistent. Lazily loaded (degrades to a disabled toggle + "no WebGL"
  when unavailable); a **Rotate 90°** button re-frames the yaw; the preview stays current with inspector-driven
  surface edits. 3 new `planIsoSolids` tests; JS 122 green. (G27)
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

- **Emitter placement knobs — endpoint shift, attachment width, side-dock** — `Pgm/Shapes/ShapeEmitter.cs`:
  the placement grammar the slot vocabulary was built for. **Shift** (the scythe's two independently-
  offsettable endpoints slide along the docking edge, and the piece each docks — spine / return leg —
  resizes with the shift; the donut's attachment slides along the ring's edge, ring unchanged).
  **Width** (the scythe entry's `attachmentWidth`, measured along the spine it docks — the same
  `w2/w4/w6 = cw/2·cw/3·cw` grammar as the donut's; the tail widens along the dock, never perpendicular).
  **Docking** (`RoomPlacement.SideTuck` generalised beyond `I` to `Z` and the scythe: the wool docks the
  terminal piece's side, perpendicular, and the terminal is shortened to the room's line). All knobs are
  clamped with named errors, keep the template slot order and piece count, and classify back to their
  family at both handednesses (`EmitterPlacementKnobTests`). The `Z`/`I` side-dock is **sampled in
  production** (a per-arm draw in the box fill); the scythe knobs stay tools/tests-side — a shifted entry
  leaves the mouth row, so it needs a corner-wrapping dock (or declarable bays) before the scythe's
  production gate opens (noted in `FillMenu`). Sweep 300/300, 574 tests green. (G50, G51, G52)

- **The team-unit allocate→fill loop — the filler (G63-C.1) + the allocator core (G63-C.2)** —
  `Compose/TeamUnitAllocator.cs` + `Compose/TeamUnitFiller.cs` + `tools/compose/unit-gallery.cs`: the box-driven
  switch's spine, inverting grow-then-derive to **allocate-then-fill**. `TeamUnitAllocator.Allocate` samples the
  frame-independent placement plan (`UnitPlan` — spawn on the back **or a lateral side**, wools assigned around it
  free-sides-first back-preferred, a third doubling onto the spawn's side, the front reserved for the frontline)
  and lays the box Rects out from the budget; it **owns the hub-form choice** (Rectangle/L/U/Ring biased by size —
  a big square hub prefers negative space over solid area) and seats every neighbour on the chosen form's **real
  free-edge intervals** (the §1.13 offerable surface, read off the hub's own emitted offers), falling back to the
  solid rectangle, `null` the directed no-fit signal; the chosen form rides on `Box.Form` (+ `FlipV` turning the
  solid spine to the demanded back, open feet to the front) for the filler to re-emit the same body, and each
  hub↔neighbour joint carries the hub's per-edge **`EdgeOffer`**. The frontline seats on the front side, its reach
  pushing the hub back behind it. Wools stay compact: the **length rule** (a back lane past ~3× the room dimension
  reads as a too-long corridor → the room tucks to the side), the **w2 wool-lane split** (`WoolLaneCells` — wools
  sized/offered at w2 regardless of the map's `w`, so a staple's 3-lane mouth fits a cap-6 hub), and the
  **seat-and-shift overhang** (`SeatOverhang` — a rich single-entry wool's narrow entry lands on a free run while
  the body overhangs, both handednesses tried, box-overlap-checked); a full-mouth staple a narrow edge can't hold
  demotes to `L`, a failed overhang to a compact inline `I`, rather than failing the unit. The mass-level corner
  law (`Cells.HasDiagonalPinch` over the composed mask) replaces coarse corner clearance (now 0) — **0 pinches**.
  `TeamUnitFiller.Fill` fills the partition **hub-first**: the hub emits at the allocator's form and per-edge
  widths, each neighbour **consumes its joint's offered width as its `cw`** (spawn via `SpawnBoxEmitter`, wool via
  the profile-gated `BoxFiller` at the allocator's `WoolFill`), and the frontline fills as a **join** — its form
  answering the hub's (the wide Bar only against a branch hub; a staple/strand elsewhere) — whose face offers flow
  out on `FilledUnit.FrontlineFace` for the mid (`mid = f(frontline)`). Seven wool shapes place: inline-`I`,
  side-tuck-`I`, `L`, `donut`, `U`/`H`, and the redefined **clamp** (docks like a `U`, the wool a cut cell between
  two legs on one mouth — centered `I+I` / corner `L+I` via `woolAtEnd`, retiring the dual-host `FamilyDock`).
  `unit-gallery.cs` renders the layouts (Ring/rectangle/`L` hubs, staple + strand frontlines, inline + side-tuck +
  overhang wools). `TeamUnitAllocatorTests` + `TeamUnitFillerTests`; Pgm suite 687/687. Contract:
  `map-generation.md` §1.13/§1.14/§5.5. (G63-C.1, G63-C.2 core)

- **The seat-step neighbour separation gap (F1 / WL2 by construction)** — `Compose/TeamUnitAllocator.cs`: no two
  spawn/wool neighbour bodies may seat within the **map lane width** of each other (2 cells = 10 blocks; 3 = 15 on
  wide boards, `LandPerTeam > WideLaneLand`) — a body-adjacency **law** applied as a **demand** in the seat step,
  enforced by the seat **gate**. Each seated spawn/wool projects onto the edge being seated (`ProjectOntoEdge`) as
  a forbidden along-interval `SeatInRuns` inflates by the gap, covering the same-edge abut and the adjacent-edge
  corner meeting in one pass (its along + perpendicular conditions reproduce `TooClose`, which also filters
  `SeatOverhang`'s placements); no single-sample rejection, so no-alloc is unchanged. A supernumerary wool that
  no longer fits with the gap **drops** rather than failing the unit (huge's doubled third wool, which a cap-6 hub
  cannot hold gapped — restoring it is the hub-growth work, G105). Closes §9 F1 (spawn/wool) + WL2; the frontline
  keeps no neighbour gap (build-zone's rule). Pgm suite 690/690. (G110 · taxonomy §9 F1, §10.4)

- **No-frontline front guard — no neighbour flush with the hub front face** — `Compose/FrontGuard.cs` (the
  deterministic post-pass: buffer/`Backness`, the slide, `Resolve`) + `Compose/TeamUnitAllocator.cs` (applies it
  while seating): on a
  frontline-less unit a spawn/wool ending flush with (or past) the hub's front face extends it into **one long flat
  frontier** — hub front + neighbour front reading as a single straight edge (the reported defect: flat front runs
  up to 20 cells) — which map design forbids. The guard now covers every dock, as **law**: an overhang wool
  (L / donut) keeps only placements **buffered ≥ 1 cell behind** the face (`Backness`; none ⇒ the compact I); a
  full-mouth lateral seat landing flush **slides back** to the nearest clear position (deterministic — no draw, so
  an already-off-front seat and every untouched unit re-seat bit-identically). Seats no slide can save go through a
  small **resolution search** (all processing orders × spawn back-edge slide variants): retry the slide with all
  neighbours known, **relocate** to the mirror lateral / back edge (backmost lawful seat), retry both at the reduced
  **wool-lane gap** (2 cells, 10 blocks — the narrower boards' own gap, still no-touch) as the last tier, then
  **drop** the wool while another remains; a residue on a non-rectangle form directed-nulls into the rectangle
  fallback. With a frontline the guard does not apply (the front is occupied and juts forward — no continuous line
  can form). Flush spawn/wools on no-frontline units: **0 across 4 presets × 64 seeds** (gate:
  `No_frontline_units_keep_every_neighbour_off_the_hub_front_face`); worst flat front run collapses 20 → 11 cells
  (= the hub's own width); with-frontline units bit-identical; pinch 0. Pgm suite 693/693. (front-guard · G114 filed)

- **Elongated hubs + the wide holed forms P, Double-hole, and G** — `Compose/HubBoxEmitter.cs` +
  `TeamUnitAllocator.cs` + `Shapes/BodyEmitter.cs`/`ShapeClassifier.cs`: the hub grows **wider, not squarer**. Its
  lateral span reads a larger cap (`HubWideCap` 5/7/9/11 by land) than its depth (`HubCapCells` 3/4/5/6), so the
  long edge gives the spawn/wools room to attach with the seat gap and reaches the width ≥ 9 the wide holed bodies
  need. Three new hub forms join the menu (`HubBoxEmitter.Forms`): **P** (a loop on a longer overhanging bar — the
  bar a long free run), **Double-hole** (a ring + a **full-height** U, two equal holes — the U made full-height so
  it fits a shallow-wide hub), and **G** (`BodyEmitter.G` — a ring + an L, the ring's hole plus an **open bay** the
  docking frontline seals into a taller hole — asymmetric holes; `ClassifyBody` reads it as one enclosed void + a
  three-walled bay, distinct from Ring/P, closing the mirror). `ChooseHubForm` (reading the frame-mapped box dims)
  picks {P, Double-hole, G, Ring} for wide boxes (≥ 9), the compact solid/branch menu below; a wide form too small
  directed-nulls and falls back to the rectangle. TwoUOnI stays off the hub menu. Huge hubs average ~8w × 5.5h;
  wide-form mix (huge/200): Ring 115 · G 26 · Double-hole 23; no-alloc/no-fill/pinch 0. Pgm suite 692/692. (G105
  partial · `map-generation.md` §5.5)

- **Map completion v0 — the box-model path closes the loop with a band-only mid** —
  `Composer.ComposeBoxStages` + `MidCarver.BandOnly` + `tools/compose/board-gallery.cs`: the first full board off
  the partition-first path. The crossing is the draw-free band-only design (uniform 20-block gap, no stones, no
  centre island); the allocator takes it as its axis margin (`Allocate` gains an optional `CrossingDesign` — the
  mid box arithmetic decides how far the unit's front sits from the axis); `MidCarver.TryCarve` consumes the
  filled unit as-is (its hub lateral extent now unions the box path's prefixed `hub-…` pieces; the grower's
  single `hub` piece is the degenerate case) and derives the band from the front faces — pinned <b>flush</b> on
  the box path (`flushOnly`): a flat front edge takes the build zone straight against it, never the plaza's
  one-cell lap (the m4 draw is still consumed; the grower path keeps sampling it). BZ8/BZ9/BZ6 unchanged. Closure holes are emergent only — a staple frontline's bay the band's flush dock seals still rings
  one (36/80 preset boards, always in symmetric pairs). The same hard-terms gate as the grower path. The board
  gallery renders the full fanned board with a per-card <b>loop-closed check</b> (a flood from the spawn over
  land + band must reach every fanned spawn image): 80/80 preset boards compose and connect, 0 rejects. Gate:
  `Box_composition_closes_the_loop_with_a_band_only_mid` (50 boards). Pgm suite 694/694. (G115)

- **The spawn–wool–frontline triangle terms (WL9/WL10)** — `Evaluate/Terms/TriangleTerms.cs`: three new soft
  terms close the relational gap the catalogue had. <b>spawn-wool-spread</b> (WL9): the spread of the per-wool
  spawn→wool traversal distances — one wool guarded while another is abandoned. <b>wool-front-distance</b>
  (WL10): the most exposed wool's traversal distance to the frontline edge, read off the derived board's
  <b>front-front build cells</b> (id-independent — works on teaching seeds and composed plans alike).
  <b>wool-front-balance</b> (WL10): the triangle — per wool the <i>defence deficit</i> (spawn distance −
  frontline distance), scored on the deficits' spread; the banned failure is a front-near wool with a far spawn
  (free to capture) beside a back wool with the spawn adjacent. All by the same rectilinear surface traversal as
  WL2/WL7. Bands learned from the 23 teaching maps (`envelope-stats`): spread [0,85] · front distance [24,165] ·
  balance [0,140] blocks. WL9/WL10 authored into `layout-rules.md`. Pgm suite 700/700. (G115)

- **The box pipeline is THE composer — the old grower path retired (G63-C.3/C.4)** — `Compose/`:
  `Composer.Compose`/`ComposeStages` now run the partition-first pipeline (envelope → `MidCarver.BandOnly`
  crossing → `TeamUnitAllocator` → `TeamUnitFiller` → hull-exact flush band → evaluator gate) as the one
  path. Deleted with the cut-over: `TeamUnitGrower` (the grow-then-fill authoring), `BoxPartitioner` (the
  grow round-trip seam), `SpawnWoolRooms` (the lane terminal carve — box rooms arrive pre-carved),
  `IsolationCut`/`CutResult` (dormant since G86; returns slot-aware, ideas doc G63-D), and `MidCarver`'s
  sampled-crossing vocabulary (`SampleCrossing`, stone rows/grids, the centre island, the plaza — the
  reference designs live in git history and re-enter as `CrossingDesign` forms, ideas doc G116). Shared
  statics relocated: `MaxChainBlocks`/`LaneChainMaxBlocks`/`ImageClearanceBlocks` → `ComposeGeometry`;
  `GrownUnit`/`GrownPiece` records → `Compose/GrownUnit.cs`. Supported requests: 2-team
  `rot_180`/`mirror_z` (4-team `rot_90` + `mirror_x` return through the allocator richness work).
  `ComposerTests` re-based to the box sweep (determinism, clean validation, flush/hull band, connectivity,
  distribution); gallery tools (`matrix`/`gallery-gen`/`box-gallery`/`derive-gallery`/`board-gallery`) on
  the surviving entry point — 20/20 matrix cases compose, 0 validator errors. Task board condensed with it:
  the ~40-task G long tail → `docs/layout-generation-ideas.md` (ids preserved), the new focus (G117/G118
  studio integration) on `TODO.md`. Suite 681/681.

- **Size-independent triangle factors + the stalemate probe** — `Evaluate/Terms/TriangleTerms.cs` +
  `tools/compose/stalemate-probe.cs`: the distance terms scale with the board, so the same 2× imbalance reads
  in-band on a big board and out-of-band on a small one — three <b>factor</b> terms fix that, all authored caps
  (`LearnsFromTraced` false; the intent seeds set the tolerance, traced maps never widen it):
  <b>spawn-wool-ratio</b> (WL9, max ÷ min per-wool spawn distance, band [1,1.2]), <b>wool-front-ratio</b> (WL10,
  same over frontline distances, band [1,1.53]), and <b>wool-front-remoteness</b> (WL10, the most remote wool's
  frontline distance, any wool count, cap 145 blocks — the outer ceiling; the size-independent catches come from
  the ratios, e.g. the remote-donut stalemate board reads spawn-ratio 2.1). Prototype of the deeper siege
  factors: `stalemate-probe.cs` reads the derived board per wool — approach count (`Approaches`), lane/entry
  width (`WoolShapes`), traversal to the nearest rotation hole (middle/frontline `Voids` shore), and the defence
  deficit — composed into a per-wool STALEMATE flag (single thin approach, defender no later than the attacker,
  no rotation hole within 40 blocks). (G115)

- **Donut growth knobs — the entry widens, the hole grows** — `ShapeEmitter` (donut) + `WoolFill.AttachmentWidth`
  + `TeamUnitAllocator.WoolDemand`: the donut always emitted its min box — good for reach, but the one-corridor
  hub entry was a fixed chokepoint and the hole a constant 1×2. The ring's span now <b>derives from the box</b>
  (the min box still gives the classic `3·cw`, so every existing min emission is bit-identical), and the
  allocator samples the growth: the hub-entry width 2–5 cells (`attachmentWidth` — the knob existed, nothing
  passed it; now plumbed through `WoolFill`/`BoxFiller`/`EntryOn` so the seat-and-shift docks the exact entry),
  and the enclosed hole up to <b>3 × 5</b> (along × deep — the box grows and the ring absorbs it; height already
  rode the box). The min box stays the floor, so crowded hubs fall back unchanged. Gate:
  `A_grown_donut_box_widens_the_ring_the_hole_and_the_entry`. Pgm suite 698/698. (G115)

- **Sampled frontline leg layouts — varied widths under the leg laws** — `FrontlineBoxEmitter.SampleArms` +
  `TeamUnitFiller`: the branch frontlines gain a sampled per-leg layout over the canonical forms (which stay the
  fallback): the single's notch varies 2–4 with the leg on either side; the twin becomes an uneven Π/F — two
  legs of sampled widths and placements, with end recesses (`ttvvtttxxx`-style). The <b>leg laws</b>: every leg
  ≥ 2 wide, a pair within <b>factor 2</b> of each other (never a 2 beside a 5), the inter-leg bay 2–4 wide, end
  recesses together ≤ ⅓ of the spine, the single leg strictly wider than its notch. Built through
  `BodyEmitter.SpineArms`' per-arm-size overload; under rot_180 the parallel-fronts law keeps only the symmetric
  samples (uneven variants live on mirror boards). Prep for the richer mids (G116). Gate:
  `Sampled_layouts_respect_the_leg_laws_across_many_draws`. Pgm suite 697/697. (G115)

- **The single frontline is the fat L — the centred T is banned** — `Compose/FrontlineBoxEmitter.cs`: the
  `single` form built a centred `cw`-wide strand (`vtv/ttt`), whose narrow tip is the whole front-face hull —
  under the hull-fit band that forces a too-thin mid band. It now builds the <b>fat L</b> (`vtt/ttt`): one arm
  anchored at the spine's start spanning all but one corridor width (the void notch a real recess), via
  `BodyEmitter.SpineArms`' per-arm-size overload — the per-slot width knob, already present. A leg not strictly
  wider than the notch (the thin-leg `vvt/ttt`, same thin band) directed-nulls. Under rot_180 the parallel-fronts
  law resamples the asymmetric L away, so singles appear on mirror boards; the menu and its draws are unchanged.
  Pgm suite 695/695. (G115)

- **The frontline box — the join box kind (G89) + the face offer (G96 frontline half)** — `Compose/FrontlineBoxEmitter.cs`:
  the **terminal-free** frontline join (map-generation.md §5.5). `FrontlineBoxEmitter` finishes a `BodyEmitter`
  `ShapeBody` with the Front designation — one edge the `face`, **no room/marker** — over the form menu **Bar** (the
  wide face, FR6), **single** (`SpineArms(1)`) and **twin** (`SpineArms(2)`), lifting the grower's `FrontForm { None,
  Single, Wide, Twin }` into `FillProfiles.FrontlineForms`. Rotation is fixed (spine Top docks the hub, face Bottom
  toward the axis); **only the face is offered** — the spine is the consumer side (it lands on the hub's offer), the
  sides inert. The **face offer** (G96's frontline half) carries the mid's grouping contract — **joint** (the tips
  share one group, one wide mid spans them, the recess unoffered → CT9's hole) vs **several** (one group per tip — the
  twin/double frontline). Offers derive from the shared `BoxInterfaces.Runs` free-run read (lifted out of the hub).
  The holed forms (P, two-U-on-I, G100) + the composer consuming the face offer (G63-C) are follow-ups. Pgm suite
  672/672. Contract: `map-generation.md` §1.14/§5.5. (G89, G96)

- **The hub box — the constraint-source box kind (G88) + the offer type (G96 hub half)** — `Compose/HubBoxEmitter.cs`
  + `Compose/Boxes/EdgeOffer.cs`: the **terminal-free** hub box (map-generation.md §5.5). `HubBoxEmitter` finishes a
  `BodyEmitter` `ShapeBody` with the hub designation — per-edge `interface` widths, **no room/marker** — over the
  authored form menu **Rectangle · L (`SpineArms(1)`) · U (`SpineArms(2)`) · Ring · Double-hole**, each sized to fill
  the box (a too-small box a directed null, an off-menu form a throw). It publishes one **`EdgeOffer`** per contiguous
  free run on each edge (**G96's hub half**: `Several`-grouped, the `wN` width a neighbour reads as its `cw` — the
  composer's `edgeWidths` constraint, geometric default; a U's bay reads as two bottom offers, a ring's wall as one
  full run). Offers derive uniformly from a new `BoxInterfaces.Of(ShapeBody)` free-edge read; `BoxJoint.Offer` carries
  the provenance; `FillProfiles.HubForms` is the hub's `Compound`-typed menu row. The composer consuming the offers +
  retiring the grower's `hubU×hubV` hub is G63-C. Pgm suite 665/665. Contract: `map-generation.md` §1.14/§5.5. (G88, G96)

- **Designation-scoped docking gate + the marks (G95)** — `Pgm/Shapes/Designation.cs` + `Compose/Boxes/DockingGate.cs`:
  `DockingGate.Role` re-grounds from one global slot table to **`Role(Designation, slotOrMark)`** — the
  binding G88/G89's hub/frontline designations stamp onto and the gate reads. New `Designation { Approach ·
  Hub · Frontline }` (wool and spawn are both `Approach`) and `DesignationMarks { interface · face }` — the
  siblings of the approach's `entry`/`room`. The **approach table is verbatim** (room → never-dock, entry →
  docking edge, structural slots internal), so every emit/dock/mirror test is byte-identical; the `Hub`
  (`interface` docks, no terminal → nothing vetoes) and `Frontline` (`face` docks) rows are defined and pinned,
  ready for the G88/G89 emitters to stamp. No new rule content — the binding only. Pgm suite 656/656. Contract:
  `docs/map-generation-constraint-taxonomy.md` §3 gap 2 / §7; `map-generation.md` §1.12, §5.3. (G95)

- **Shape vocabulary + rule kinds folded into the canonical doc (G94)** — `docs/contracts/map-generation.md`:
  the two-layer shape model is now canonical there. §5 reframes **bodies-then-designations** (a terminal-free
  **body** — the `Compound` escalation Rectangle · Spine+K arms · Zig · Hook · Ring · Double-hole — finished by
  a per-kind **designation**: approach, hub, frontline; §5.5 the hub/frontline form menus feeding G88/G89; §5.3
  the structural-slot vs designation-mark split). §1 gains the locked terms — **§1.12** body/designation,
  **§1.13** the edge taxonomy (notch/bay/hole by wall count · parts · mouths · guard · offerable surface),
  **§1.14** the twelve rule kinds (fact · menu · fit gate · demand · **offer** · veto · gate · knob · **target
  vs band** · law · doctrine). `shape-vocabulary.md` superseded (banner + section map; retained for its live
  code citations, delete follow-up G99); the constraint-taxonomy's §1 and §4 terms retired to pointers, its §4.1
  publish policy + §3/§5/§7 proposal kept as the live design record. Doc-only. (G94)

- **Interval facts on the box edges (G93)** — `Compose/Boxes/BoxInterfaces.cs`: `BoxEdgeInterface` re-grounds
  on **intervals** — each edge carries its per-piece stretches ordered along it (`EdgeInterval(Start,
  LengthCells, Slot)`, the room included as the room slot), `Slots` becoming the flat per-interval view — so a
  shape presenting two pieces to one edge is finally sayable: the clamp's mouth edge holds **both entry bars as
  two disjoint intervals with the bay's gap between them**, the twin-face precondition the frontline work (G89)
  and the offers (G96) bind to. `DockingGate` verdicts unchanged, every existing facts/gate test green
  unmodified, emissions untouched (Pgm suite 655/655). Contract:
  `docs/map-generation-constraint-taxonomy.md` §3/§6 step 2.

- **The edge taxonomy + the publish policy (G92)** — `Pgm/Shapes/BodyEdges.cs` +
  `Compose/Boxes/PublishPolicy.cs` + `tools/compose/edge-gallery.cs`: any rectangle set's **negative spaces**
  classed by wall count (**notch** 2 · **bay** 3 · **hole** enclosed · open ≤1), each with its **slab parts**
  (classed by their own body walls — the uneven branch's six-edge bay is a U whose mouth bar reads
  notch-grade), its own **compound `Form`** (the void is a body too — `ClassifyBody`'s spine read extended to
  all four orientations), its **mouths** (interval + `wN` width class; bay 1 · notch 2 · hole 0), and its
  **wall slots** (the derive-side twin of `ShapeVacancy.Walls`); every boundary edge classified on three axes —
  what it **faces** × **terminal** (the room seals its own wall, runs splitting at the ownership change) ×
  **guarded** (the room's 10-block clearance margin, which also splits the adjacent space's parts) — the free
  offerable surface being *open ∧ ¬terminal ∧ ¬guarded*. Over these facts, **`PublishPolicy`** (author-decided):
  terminal-capped shapes veto bays + holes and allow notches (incl. the Z's `room-run` notch — proximity is the
  guard's job); terminal-free bodies allow everything (hole size gate pending); the publishable region = front
  (mouth-touching), unguarded parts; **publishing is an offer, never a fill**. All rendered by the edge gallery
  (spaces tinted per part, mouths bracketed with width class, ✓/✗ verdicts per card), published as a hosted
  artifact; `BodyEdgesTests` + `PublishPolicyTests` pin every class, split, mouth, and author call. Contract:
  `docs/map-generation-constraint-taxonomy.md` §4/§4.1. (G92)

- **The new terminal-free compounds, standalone (M3, G91)** — `Pgm/Shapes/BodyEmitter.cs` + `Compound.cs` +
  `ShapeClassifier.cs`: the shapes the vocabulary names but `ShapeEmitter` couldn't build, now emitted as pure
  `ShapeBody` on the G90 Body stage. `BodyEmitter` generalizes the branch family from the fixed two-leg staple to
  **spine + K arms** (`SpineArms` — T at K=1, Π/F at K=2, E at K=3, arm placement a knob, **3 arms the cap**) and
  adds the holed recombinations **`Ring`**, **`DoubleHole`** (a ring + a U docked on its edge, its bay the second
  void — the two holes equal-sized or variant, the U sliding along the ring), **`P`** (a ring whose bottom bar runs longer than the loop, the loop
  sliding along it — one void), and **`TwoUOnI`** (two loops on a shared baseline, an open channel between). Each
  **classifies back to itself** through the new `ShapeClassifier.ClassifyBody` → `Compound` (a terminal-free
  taxonomy kept separate from `ShapeFamily` so the approach path stays byte-identical): void count is the strongest
  signal, the **two-void** pair split on whether an *open channel* comes between the voids (two-U-on-I) or a
  *solid wall* does (double-hole), the **one-void** pair on P's overhang concavity vs a clean ring, the void-free
  pair on the arm count (placement-independent — an F and a Π both read `SpineArms(2)`). Pieces carry the
  structural slots (`bar`/`leg`), no terminal. Verified: `BodyMirrorTests` (each compound emits → classifies back,
  one connected mass, no overlap, edge-aligned joins only per §3; the arm cap; the U's slide) + full Pgm suite
  628/628 green; drawn **standalone** in the body gallery (`tools/compose/body-gallery.cs`) with every piece
  labelled by its slot. The shared bodies the hub (G88) and frontline (G89) designations reuse. Contract:
  `docs/contracts/shape-vocabulary.md` §5/§10. (G91)

- **The terminal-free `Body` — the shape/designation split (M3, G90)** — `Pgm/Shapes/ShapeBody.cs` +
  `ShapeEmitter.cs` + `ApproachSlots.cs`: `ShapeEmitter.Emit` — which baked a wool `room` into every family —
  splits into a pure **`Body`** (`ShapeEmitter.Body` → a `ShapeBody`: structural-slotted rects + vacancies, no
  terminal/marker/id) and an **approach designation** (`ShapeEmitter.Approach(body, room, marker)`, which `Emit`
  composes over the body). `EmittedShape` now *is* a `ShapeBody` + the terminal room + marker (`Terrain`/
  `Vacancies` read through to the body), so every consumer stays untouched. `ApproachSlots` gains the documented
  **structural-slot** (`run`/`bar`/`leg`, shared by every kind) vs **designation-mark** (`entry`/`room`, stamped
  by the approach) split — emitted strings unchanged, so the mirror stays byte-identical. The split is
  **byte-identical** wool/spawn output: the emit↔derive mirror, the G50–G52 placement-knob tests, `BoxInterfaces`
  and the `DockingGate` stay green (619/619 Pgm), and `ShapeBodyTests` gates that the body carries exactly the
  emission's terrain (terminal-free) and that `Approach` reconstructs the emission. The shared stage the hub
  (G88), frontline (G89) and new-compound (G91) work builds on. Contract:
  `docs/contracts/shape-vocabulary.md` §8/§9. (G90)

- **The spawn seats at a sampled point along the hub's back edge (G85)** — `Compose/TeamUnitGrower.cs`: the spawn
  was pinned to the hub back edge's −v corner (`FillSpawn(..., hubVMin, ...)`) while the wool arms already sampled a
  point along their host edge. G85 gives the spawn that same point-flexibility (SP2): a `spawnVFrac` draw seats it
  anywhere along the back edge with its entry band kept fully on the hub (slide range `hubV − w`), and the
  wool-on-spawn dock (`ResolveAttachment`) follows the seated position instead of assuming the corner. It stays
  pinned at the −v corner only when the third wool shares the back edge (packed beside it). The spawn keeps its SP
  semantics — always on the back edge, facing the axis — so this is *lateral* flexibility, not edge/host freedom
  (that falls out of the allocate-first switch, G63-C, where the spawn is just a box with a sampled host + mouth).
  Re-keys the spawn RNG. Verified: seat takes 2 distinct positions on small hubs up to 6 on big ones (scaling with
  hub room), a distribution test pins it against silent degeneration, full Pgm suite 610/610 green, gallery 42/42.
  (G85)

- **The isolation cut is out of the compose loop — wool lanes stay pristine (G86)** — `Compose/Composer.cs`:
  `IsolationCut` carved a `bridge-a` build zone across a team's `spawn↔wool` route on ~40% of plans, *before*
  fragmentation had slot-carving rules — a bridge landing across an otherwise clean wool approach. `Composer`
  no longer calls it: the `cut` is a constant `null`, so the RNG re-keys (the whole-map layout shifts, the cut's
  three draws gone). The code is **kept intact and dormant** — `IsolationCut`/`CutResult`, the `ComposedStages.Cut`
  field, the `Assemble` `bridge-a` zone, the `IsolationCutCount` soft term — so it returns as a proper slot-aware
  fragment pass (cutting only a `run`/`bar`, never a `room`/`entry`, per `docs/contracts/map-generation.md` §5.3)
  with a one-line re-add. The two `ComposerTests` and one `WoolBoxGrowthTests` that asserted cuts occur retire
  with it (they land again with the pass). Verified: full Pgm suite 609/609 green, gallery 42/42 with 0 `bridge-a`
  zones. (G86)

- **The spawn box is a small fixed box — a size rule in `FillProfiles` (G84)** — `Compose/Boxes/FillProfiles.cs`:
  the porting kept the old grower's "grow the shape to absorb its budget share" sizing, so a spawn stretched with
  player count to ~100 blocks when the docs say a spawn is **small, ≤20** (`docs/contracts/map-generation.md` §4).
  The fix is a **size rule over the box model, not a resize solver**: `FillProfiles.SpawnSizes` is the per-`BoxKind`
  spawn allowlist as data — three small boxes `{I direct ~10×10, I run-up ~10×20, L hook ~20×20}` — and
  `SpawnLand(size, cw)` reads a size's land off `SpawnBoxEmitter.Box`. `TeamUnitGrower` samples a `SpawnSizes` box
  instead of drawing an L-style + split-frac + length-cap, dropping the `spawnLen`/`spawnLenCap`/`spawnURunCap`/
  `spawnLFeasible` solvers and the shape-shrink loop (the inflate stays). The spawn's budget weight (`spawnUnit = 2.0`)
  stays in the unit denominator, so **wool shares are unchanged** — the freed budget is left unspent (a sparser, less
  crammed map) rather than redistributed to grow the wools, per the "keep it simple, don't grow shapes" direction.
  The land floor widens to match (`AreaFloorTolerance = 0.40`, `BoxPartitioner.BudgetTolerance` 0.20→0.40, the
  composer area gate floor 0.8→0.6) — the window is now asymmetric, a unit runs under quota more than over.
  Re-keys the spawn RNG (goldens freeze after G63). Verified: spawn small on the 30p worst case, wool distribution
  unchanged, 0 gallery failures; `BoxPartitionerTests` + `ComposerTests` re-based to the sparser envelope, 612/612
  green. Contract: `docs/contracts/map-generation.md` §4. (G84)

- **The partition-first allocator seam — `BoxPartitioner` (M4, G63-B)** — `Compose/Boxes/BoxPartitioner.cs`: the
  `budget → BoxPartition` entry the box-driven switch is built around, shipping **parallel** to `TeamUnitGrower`
  (not yet the default). `Partition(env, rng)` allocates the partition a compose produces; where the grower lets
  each box's footprint *fall out of the fill* (`PlaceArm`/`PlaceSpawn` emit the shape, then compute the host
  window), the allocator makes the `BoxPartition` — typed boxes with their `Rect` footprints and their
  `LandTargetCells` land-budget halves, joined by their abutments — the first-class artifact. In this parallel
  stage the fill is still the grower's: `Partition` grows one unit and reads its partition off `BoxPartition.Of`,
  so the emitted partition **round-trips through the mirror by construction** ("the labels drive, the mirror
  verifies"). Over the bare mirror it adds the **seam** (so the G63-C inversion to allocate-then-fill changes this
  body, not its callers) and the **two-currency budget accounting**: `BudgetCells(env)` is the land currency (the
  team land target over the cell area; the footprint currency is the boxes' Rects), and `WithinBudget` is the
  balance check — `Valid()` (every box's land within its footprint) plus the total land inside the budget envelope
  — the invariant the directed `FillResult` repair drives each box's land to at the switch. Purely additive: no
  production path changed, no plan/golden churn (like G63-A). The literal *Rects-allocated-first* inversion (fill
  the partition through `BoxFiller`, wire `DockingGate`, retire the grower) lands at G63-C. Verified:
  `BoxPartitionerTests` (round-trip equals `BoxPartition.Of` of the grown unit across seeds; `Valid` + budget
  balance across seeds × player counts × every symmetry mode with each box's land within its footprint; the typed
  spine boxes present; `WithinBudget` rejects a land-starved partition), full Pgm suite 607/607 green. Contract:
  `docs/contracts/map-generation.md` §4/§8/§12. (G63-B)

- **The partition constraint graph — `BoxPartition` (M4, G63-A)** — `Compose/Boxes/BoxPartition.cs`: the typed
  target the box-driven switch is built around. A `BoxPartition` is the typed `Box`es (each an allocated
  footprint `Rect` + its `LandTargetCells` land-budget half) and the `BoxJoint`s between them (a shared edge
  interval `BoxInterface` + the box on the other side) — the constraint graph sampling produces once
  composition allocates boxes first and fills them second, replacing the imperative sample-then-place shape
  record. `Valid()` is its hard-invariant gate: non-degenerate boxes, unique ids, the land currency never over
  a box's footprint, and every joint a genuine abutment of two distinct real boxes (`SharedEdge` recomputed).
  **Boxes may overlap** — the partition allocates budgets and constraints, not exclusive area (piece-
  disjointness is the real invariant enforced downstream), so a joint is only asserted where two footprints
  truly abut. `BoxPartition.Of(unit)` is the **derive-side mirror**: it reads the partition a grown unit
  implies — labeled approach pieces group by their `BoxRef` into wool/spawn boxes, the structural pieces (hub,
  frontline, third wool lane) by id into their plain boxes, joints from the footprint abutments — so the
  partition a future allocator emits round-trips through it ("the labels drive, the mirror verifies"). Purely
  additive: no production path changed, no plan/golden churn. Verified: `BoxPartitionTests` (`SharedEdge`
  abutment intervals vs gaps/corners/overlap; the invariants reject degenerate/dup/over-budget/phantom-joint
  partitions; `Of` reads a `Valid` partition off real grown units across seeds with the spine + wool boxes
  present and the land currency summing; the hub is jointed to its neighbours). Contract:
  `docs/contracts/map-generation.md` §4/§12. (G63-A)

- **Docking as a declarative slot-edge gate — `DockingGate` (M3, G80)** — `Compose/Boxes/DockingGate.cs`:
  the one place that decides whether a box edge may receive a dock, as a table over slots rather than
  per-family imperative code. `SlotDockRole` tags each `ApproachSlots` slot once — `room` → **never-dock**
  (a dock seals the goal), `entry` → **docking edge** (the mouth), every corridor slot (`run`/`bar`/`leg` and
  the entry/room-qualified runs and bars) → **internal**; `FamilyDock` carries the per-family **demand** (how
  many distinct entry edges must connect: clamp 2, most 1) and **span** (clamp: the short edge). The gate
  resolves each box edge to its slots via the `BoxEdgeInterface` facts and applies the table: a dock is legal
  iff the edge lands on a docking-edge slot, touches no never-dock slot, and meets the span demand
  (`Check` → a directed `DockRejection` of `SealsWool`/`NotAnEntryEdge`/`WrongSpan`, `DockingEdges`,
  `CanDock`, `MeetsDemand`). The hard cases are just rows: the **clamp** docks its two short bars (demand 2,
  the long bay and wool wall rejected), the **scythe** docks its clean entry edge and rejects the
  room-contaminated canonical mouth — the gate reading slots off the shape, not a fixed mouth edge, so validity
  is **shape-relative for free** (an entry shift or a flip moves the edge and the verdict follows). It is a
  **compose-side gate, not an `ILayoutTerm`** — the evaluator reads the derived board only and the interfaces
  drop at `Assemble`, so the existing hard terms (WL8, the corner law) catch any symptom on derived topology
  as the mirror while G80 adds zero terms. The partitioner wiring (producing `FillResult` rejections, the
  clamp's dual-host corner-wrap placement) lands with G63. Verified: `DockingGateTests` (room never-dock /
  entry docks; clamp two short bars + demand; scythe clean edge vs room-contaminated mouth; U leg edge; the
  verdict tracks the room under flip; the slot-role map). Contract: `docs/contracts/map-generation.md`
  §4/§12. (G80)

- **The `BoxInterface` valid-edges data model — `BoxInterfaces` (M3, G41-B)** — `Compose/Boxes/
  BoxInterfaces.cs`: the interface model every fill and pattern binds to, replacing the single-mouth
  assumption. `BoxInterfaces.Of(shape, boxW, boxH)` reads a box's four edges off the emitted shape as
  `BoxEdgeInterface` **facts** — for each edge, its `EdgeSpan` **long/short** and the **template slots on it**
  (the pieces whose rects reach the edge, the room included; `TouchesRoom`/`HasTerrain` are convenience reads
  over the slots). It **observes; it does not judge**: whether an edge may *dock* is a *rule* (must land on an
  entry, must not seal the wool, plus the per-family demand/span), and that rule is the **G80 `DockingGate`**
  over these slots, not baked in — so every docking rule lives in one place (a room edge is legally docked at
  the elevation stage, G81, which is why "room ⇒ never-dock" is policy, not fact). It retires the single-mouth
  assumption: a box exposes all four edges as the multi-interface vocabulary. **Shape-relative**: every fact is
  read off the shape, so it moves with the shape (a room at a different corner, a flipped handedness) rather
  than naming a fixed box coordinate — the property that lets G80's gate make an entry shift carry its dock.
  Verified: `BoxInterfacesTests` (the I room edge is wool-touched / the mouth edge clear terrain; the slots on
  an edge are the pieces that reach it; the clamp's two short terrain edges + one wool-sealed; U's wool edge
  touched / leg edge clear; span; the facts move with the shape under flip). Contract:
  `docs/contracts/map-generation.md` §1.5/§4. (G41-B)

- **The profile-driven fill spine — `FillProfiles` + `BoxFiller` (M3, G41-A part 1)** — `Compose/Boxes/
  FillProfiles.cs` + `BoxFiller.cs`: the per-`BoxKind` fill profile is now a **type**, not two scattered data
  rows. `FillProfiles.Families(kind, cw)` composes the §4 width→menu rule for the wool box and the fixed
  {I, L} for the spawn; `Fits`/`FittingFamilies` add the **footprint gate** (a family's minimum box must hold
  the footprint). `BoxFiller` is the **one profile-gated fill entry point** over a positioned `Box`: it
  validates the family against the profile, emits it into the footprint (over `WoolBoxEmitter.Fill`), and
  reports the **land** the fill spends against `Box.LandTargetCells` (the two-currency balance) — "no shape
  fits" is a `FillResult` data channel, not a throw. `TeamUnitGrower`'s wool menu now reads `FillProfiles`
  (**byte-identical**: `FamiliesFor(w) == ProductionFamilies` for w∈{2,3}, verified by a hashed compose
  sweep). This is the spine the partitioner (G63) drives; routing the production arms through it + the
  intra-box fragment-to-target land with the box-Rect allocation (G41-A part 2 / G63). Verified:
  `BoxFillerTests` (profile gate, footprint fit, land accounting, roll-select, spawn-dispatch guard). Contract:
  `docs/contracts/map-generation.md` §4.1/§8. (G41-A)

- **Spawn boxes — the spawn emits through the shared emitter, the second box kind (M2)** —
  `Pgm/Compose/SpawnBoxEmitter.cs` + `TeamUnitGrower.cs`: the grower's hand-rolled spawn-lane geometry (its
  inline straight-run / L-hook `Place` calls, the spawn room carved later by `SpawnWoolRooms`) is replaced by
  a **`Box(Spawn)` filled through the shared machinery**. `SpawnBoxEmitter` is the role binding over
  `ShapeEmitter`: its **shape profile is plain data** (`Families` = {I, L} only — a spawn never forks or
  folds — and `Box`, the small SP box sizing), and `Fill` emits the family, maps it from the canonical
  mouth-top frame into the growth frame (box-local `bz → u` outward, `bx → v` cross, an L's turn to either
  side while the entry stays pinned on the hub edge), and stamps every piece's slot + the spawn `BoxRef`, the
  terminal a real `PlanRoles.Spawn` room carrying the marker (SP3 facing). Wool boxes now dock the spawn's
  **entry run** (never the marker's room — SP1 by construction), and `SpawnWoolRooms` skips the pre-carved
  spawn room. So the spawn is terminal-capped like a wool arm: the **same classifier, the same slot mirror**
  (G62), and G61's label invariant (`spawn-a/entry`…) apply unchanged — this and the wool box are the first
  two rows of the per-kind profile table the footprint/slot-budget work (G41-A/G63) reads. Changes RNG
  consumption (pre-G63 re-key). Verified: the full 300-case composer invariant sweep green, plus a spawn-box
  mirror test (classifies to I/L, slots re-derive, `Spawn`-role room) across seeds. Contract:
  `docs/contracts/map-generation.md` §4/§5.3. (G78)

- **Slot recovery — the emit↔derive mirror closes at the slot level (M2)** — `Pgm/Shapes/SlotAssignment.cs`
  + `Geom/Cells.cs`: `SlotAssignment.AssignSlots(family, pieces, roomId)` re-derives every piece's
  `ApproachSlots` slot from **topology alone** — path order for the chain families (I/L/Z/scythe), adjacency
  for the branches (U/H/clamp), and hole-edge geometry for the donut (the enclosed void via the new
  `Cells.EnclosedVoid`; bars vs legs off the hole's opposite edges, the room-bar anchored by the entry-bar's
  hub attachment) — never a canonical rect position, so entry/wool shift, side-tuck, donut
  attachment-offset/width/count, room-at-end, and any flip/mouth reorientation all survive. `ShapeMirrorTests`
  becomes a **true mirror**: emit → classify → re-derive slots → assert each equals the emitter's stamped
  slot, closing §5.4 at the slot level, not just the family. Scope is the generator's own artifacts (a box's
  pieces) — no derive-side recovery of authored/traced plans (retired by decision). `WoolLaneShape` the class
  **retires**: its lane read was a thin adapter over `ShapeClassifier.ClassifyOpen`, now called directly with
  the new public `ShapeClassifier.LaneName(LaneRead)` (BoardDeriver + lane-audit rewired). Verified: full Pgm
  suite green incl. the slot mirror over every family × size × flip × variant. Contract:
  `docs/contracts/map-generation.md` §5.3/§5.4/§12. (G62)

- **The corner law reads the mask, not the pair — donut admitted (M2)** — `Geom/Cells.cs` +
  `Pgm/Compose/TeamUnitGrower.cs` + `Boxes/FillMenu.cs`: `TeamUnitGrower.ValidateContacts` rejected any
  pairwise `Corner` verdict, gating the donut out of production — but a corner whose diagonal a third piece
  bridges is a harmless ¾-solid inside corner of one connected mass (the editor's `PC-C` lint suppresses
  exactly that). The pairwise Corner rejection is replaced by the **cell-level law**: `Narrow`/`Overlap`
  stay pairwise-rejected, and the composed cell mask is scanned for **diagonal pinch windows** (two tiles
  meeting only at a point with void on both opposite diagonals) via the new dependency-free primitive
  `Cells.HasDiagonalPinch` — ¾-solid corners pass, a genuine point pinch rejects. The donut's ring holds
  zero pinch windows, so `ShapeFamily.Donut` joins `FillMenu.ProductionFamilies` (menu now {I, L, Z, U, H,
  Donut}; changes RNG consumption — pre-G63 re-key). The pinch scan is a mass-level primitive G80's docking
  validation reuses. Verified: `CellsTests` (pinch vs ¾-solid vs ring), the full Pgm sweep green with the
  corner-law assertion updated (`ComposerTests`). Contract: `docs/contracts/map-generation.md` §4/§5.2. (G79)

- **Wool arms are box fills (M2 — the emitter's first production caller)** — `Pgm/Shapes/ShapeEmitter.cs` +
  `Pgm/Compose/Boxes/` + `TeamUnitGrower.cs`: the pure shape emitter extracted from the wool binding
  (canonical frame, `MinBox`/`MouthEdge`/`OrientMouthTop`, emit-side **vacancies** — a U's bay, a donut's
  hole — as `ShapeVacancy` data), the box scaffold types (`Box`/`BoxKind`/`BoxRef`/`BoxInterface`,
  `FillMenu` — the §4 width table as data — and `FillResult` with `Vacancy`), and the grower's inline
  1–3-segment wool-lane grammar replaced by **one box fill per wool arm**: the arm's budget share picks a
  family from the fit-filtered menu (deterministic roll), depth + width knobs size the box (surplus
  escalates the family or widens the bar — never a stretched lane), the mouth row docks inside the host
  edge window (u-floor keeps flipped bodies off the axis), and the wool room is emitted as a real
  role-bearing terminal. Every emitted piece carries **(box id, kind, slot)** ownership on `GrownPiece`,
  preserved through the isolation cut (`with`-translation) and the room carve (role-skip) and dropped only
  at `Composer.Assemble` — the labels drive, the mirror verifies. Production menu {I, L, Z, U, H} with the
  donut (corner tangencies) and scythe/clamp (self-sealed bay → WL8; unsealed by G50's entry shift)
  excluded as named gaps in `FillMenu`. Verified: full compose sweep 300/300 (incl. p30/t4/rot_90), the
  557-test Pgm suite green, `WoolBoxGrowthTests` (labels, role terminal, in-box family mirror, label
  survival through the cut, family variety across seeds). Contract: `docs/contracts/map-generation.md`
  §4/§5.3. (G61)

- **Fold-based scythe test — family reads stable under endpoint manipulation** — `Geom/Cells.cs` +
  `Pgm/Shapes/ShapeClassifier.cs`: the Scythe/Z split now asks whether the terrain **doubles back** (some grid
  row/column crosses it in two runs — `Cells.HasFold`, i.e. not orthogonally convex) instead of whether the
  bounding box carries a one-edge bay (`HasBay`, removed). The bbox read flipped when an endpoint slid off a
  box corner (the bay escaped past the vacated corner and the shifted/side-docked scythes read Z standalone,
  Scythe with a hub docked); the fold is a property of the cells alone, so the emitter's entry/wool-shift and
  side-dock manipulations keep their family in both contexts. Verified: `ShapeVariantTests` (14 variant grids
  × 2 scales, standalone + hub-docked) + the catalog/mirror/stress suites unchanged. Analysis:
  `docs/wool-approach-read-investigation.md` §6. Contract: `docs/contracts/map-generation.md` §1.8/§5.2.

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
  **Every term draws its own evidence (§9.7):** `Violation` carries a nullable `Evidence` list — four cell-space
  primitives (`EvidenceRect`/`Segment`/`Marker`/`Measure`, each tagged `offender`/`bound`/`measure`/`context`,
  the free-string tag leaving room for §9.8's `slot:*`) — attached to the seven ported terms while their
  geometry was in hand (a G5 hop draws a labelled measure across the void, BZ6 the wool + band rects). Review:
  `docs/map-generation-architecture-review.md` §5/§9; direction: `docs/contracts/layout-evaluator.md`. (G60)
- **Composer evaluator — soft scoring + surface distance (M2, part 2a)** — the evaluator's soft half.
  `SoftTerm` (a pure `Value(ctx)` metric + its own drawn `Evidence`); `SeedEnvelopes` generated by
  `tools/deriver/envelope-stats.cs` — it runs each term's `Value` over the seeds (so band and score can't drift)
  → embedded `Evaluate/seed-envelopes.json` + generated `docs/seed-envelopes.md` — scored as `Band` distance
  (normalized by half-width; rounding only ever widens a band). First soft-term batch: `fill-ratio` (G8),
  `max-chain-length` (LN2), `wool-wool-distance` (WL7), `spawn-wool-distance` (WL2).
  **Distances are rectilinear traversal over the walkable surface**, not straight-line: `Geom.Cells.ShortestPath`
  (4-connected BFS — routes around voids, hugs borders, no corner-cutting) over the k=0 terrain ∪ build cells,
  the real "how far a player travels" (materially larger than Euclidean — odd-facing wool↔wool 46→65). **WL2
  migrated off the Euclidean `PlanValidator` lint** to a surface `SpawnWoolFloor` hard gate term (≥20 blocks of
  travel); byte-identical because the generator never trips WL2 (0 gate rejects over the sweep) — the surface
  gate is the new oracle. Composed output **byte-identical** throughout; soft terms are gate-skipped and derive
  the board only outside the gate. `Cells` shortest-path tests + soft-term/envelope/floor tests green. (G60)
- **Composer evaluator — catalogue growth + traced corpus (M2, part 2b)** — six more soft terms and the traced
  teaching corpus. `lane-width` (LN1, narrowest wool lane in blocks — the goat-path guard) and
  `enclosed-void-count` (CT8, enclosed-hole count); the team-scale CT terms that **replace the blunt
  `island-count`**: `neutral-stepping-count` + `team-stepping-count` (CT4 — contested mid stones vs a team's own
  captive movement stones), `band-count` (CT1 — front-front crossings: one channelled, ≥2 parallel, none hash),
  `isolation-cut-count` (CT5 — intra/self team-side cuts); the four team-owned counts normalized ÷ orbit order so
  a 2-team and 4-team board compare. `tools/deriver/envelope-stats.cs` now teaches over the authored seeds **+**
  the traced real maps in `tools/seeds/traced/` (12 authored + 11 traced; `3084` held — its wools don't
  attribute), and a `SoftTerm.LearnsFromTraced` opt-out keeps `max-chain-length` an authored cap the traced
  long-chain maps must not widen. Composed output **byte-identical** (soft terms gate-skipped). (G60)
- **Composer evaluator — frontline runs (M2, part 2c)** — the deriver groups the flat frontline segments into
  per-team **faces**: `BoardStructure.FrontlineRuns` carries each run's `(Team, Width, Profile)` — width the
  face's longer extent in cells, profile **straight** (one colinear face, `isolated-spawn`) vs **offset** (the
  face steps, `base-2island`). Two soft terms read them: `frontline-count` (FR4 — faces per team ÷ orbit order,
  an over-exposed team side) and `frontline-width` (FR6 — the widest face, the wide-vs-split axis). Profile is
  derived but **not scored** (both straight and offset are authored-valid; it feeds the future composite and the
  evidence overlay). Additive deriver field, gate stays derive-free — composed output **byte-identical**. (G60)
- **Composer evaluator — the rotation term (M2, part 2d)** — `uncrossed-middle-void` (CT9): a contested `middle`
  void the deriver leaves with **no** crossing route (no front-front / neutral-neutral zone ringing it) is the
  rotation failure — the long dead void where the teams never meet. Band `[0,0]` (no authored map carries one),
  so any is punished; a contained `Band` fix floors a zero-tolerance `[0,0]` band's half-width to `1.0` so it
  scores O(1), not ~1e9 — preserving hard-dominates-soft (`[5,5]` and wider bands unchanged). Calibrated on six
  authored teaching seeds (`tools/seeds/teaching/`: an escalation of 3 crammed / over-stretched negatives + 3
  rotation resolutions — bridge zone, rotation stone, move-closer). Byte-identical composed output. (G60)
- **Composer evaluator — editor wiring (M2, part 3)** — the evaluator surfaces live in the plan editor.
  `Contracts/EvaluationDto` flattens the four `Evidence` primitives to one `EvidenceDto` (kind-keyed, cell-space)
  carried by `ViolationDto` (term/rule id, kind, soft distance, subjects) inside `EvaluationDto` (score · valid ·
  hard-first violations); `POST /api/plan/evaluate` (`PlanEvaluateEndpoint`) runs `LayoutEvaluator.Evaluate` on
  the posted plan and maps it, 400 on a malformed body. The plan-bridge debounces the evaluate POST alongside
  inspect (`runLive`), feeding the canvas a **Rules** evidence overlay (`PlanCanvas.setViolations` → offender/
  bound/measure/context styling table; measure labels ride the screen-space layer) and the Blazor **Score** panel
  (headline cost + fired-rule list; click a rule to **isolate** its evidence, click again to restore the
  all-violations overlay — `focusViolation`). Restores WL2 to the editor (the soft
  `spawn-wool-distance` + the hard `spawn-wool-floor`, retired from the structural lint). The Score panel is the
  editor's **single validation surface** — its STRUCT / PC-C / G2 / G5 hard terms cover every `PlanValidator`
  finding, so the old lint panel is dropped and `/plan/inspect` is trimmed to the geometry overlays alone.
  Endpoint + JS overlay-pref tests green. `docs/map-generation-architecture-review.md` §9.7. (G60)

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

- **The pipeline showcase page** — `tools/compose/showcase.cs` → `out/showcase.html`: one designed,
  self-contained explainer of the whole generation model, every figure rendered from the live composer (no
  hand-drawn images to rot). The hero is a <b>ten-stage walkthrough strip</b> (scroll-snap + dots/arrows) of
  one pinned board — huge corpus budget, seed 10: ring hub, twin frontline, I + H wools, score 3.4 — each
  frame the full fanned board faint with that stage's contribution lit (request/envelope, crossing
  arithmetic, the typed box partition, hub-first emission with joint widths, spawn, both wools with slot
  labels, the frontline face, fan + flush band, the gate with computed closure-hole tint). Below it the
  <b>deep dives</b>: the box model + the width→menu table (read off `FillMenu.Rows`), the nine approach
  families emitted slot-coloured with bay/hole tints, the two-layer slot model, the body vocabulary +
  hub/frontline designation menus with their published offers drawn bright, negative-space classes by wall
  count, docking legality (tile-glyph legal/illegal cards incl. the clamp's three docks and the scythe's
  WL8 flush seal), the two-currency budget measured off the walkthrough board (footprint vs land per box,
  the 0%-land band row), `mid = f(frontline)` with the three form sketches, and the full evaluator readout
  (hard gate clean, the two fired ratio terms glossed). (G121)

- **The plan store — persistence for the generator's feedback loop** — `M0008_Plan` + `Data/Plan/PlanStore.cs`
  + `Api/Endpoints/PlanStoreEndpoints.cs` + `Contracts/PlanDtos.cs` + the plan editor. A standalone `plan`
  corpus (no map FK): plan JSON, `origin` (generated | authored | imported), `content_hash` (SHA-256 of the
  canonicalized document — dedup + import identity), `parent_id` (fork provenance, self-FK ON DELETE SET
  NULL), and the generated-only descriptor columns (`request_json`/`seed`/`composer_version`). The store owns
  normalization + hashing and enforces the doctrine: a fresh or authored save writes in place, a generated/
  imported source **forks** a new authored row rather than mutating the immutable corpus; per-origin
  content-hash dedup. `ComposeDescriptor.For` + `ComposerVersion.Current` ("box-1") stamp the canonical
  versioned request that reproduces a generated plan (G117's card identity). Endpoints `GET/POST/DELETE
  /api/plans` (malformed body → 400); the editor gains **Save** + an **Open-from-DB** modal (origin chip ·
  name · date), file import/export untouched. Data 16 + Api 67 + Pgm 683 tests green. Prerequisite for
  G117/G118/G120. (G119)

- **Browse mode — the interactive generator in the studio** — `Pgm/Render/PlanBoardSvg.cs` +
  `Api/Endpoints/ComposeEndpoints.cs` + `Contracts/ComposeDtos.cs` + `Client/Pages/Generator/`. A studio
  page (`/generator`) that composes boards ahead and lets the author sieve and keep them. `GET /api/compose`
  loops seeds from a cursor, scores each with the evaluator, applies the sieve (size · symmetry · score
  threshold · wool count), renders each to a self-contained **server-side SVG** of the full fanned board
  (`PlanBoardSvg`, lifted from the compose tools — pieces by role, dashed band, spawn/wool/iron markers), and
  returns a page with the cursor to resume (infinite scroll via an `IntersectionObserver` helper). A card
  carries only its reproducible `ComposeDescriptor` + SVG + metrics; **pin** (`POST /api/compose/pin`)
  re-composes the plan and stores it as a generated row (G119, idempotent) — the **hold tray** is
  `GET /api/plans?origin=generated` and survives reload, with thumbnails from `GET /api/plans/{id}/svg`. The
  filter panel greys out what the composer can't yet make (`rot_90`/`mirror_x`/scythe); the **detail dialog**
  shows the large render, score breakdown (top soft contributors), copyable descriptor, pin, and **Open in
  plan editor** → `/plan-editor?plan={id}`, which loads the exact board as a generated plan (so editing forks,
  per G119). Votes deferred to G118. Pgm 685 + Api 71 tests green. (G117)

- **Browse structural sieve — form/family filters** — `Pgm/Derive/StructureSummary.cs` + `ComposeEndpoints`
  + `Client/Pages/Generator/` + `M0009`. The compose feed now sieves by **structure**, not just size/score:
  `StructureSummary.Derive(unit)` reads a composed unit's sorted wool **approach families**
  (`ShapeClassifier.Classify` per `BoxKind.Wool` box), its hub **body form**, and its frontline form or
  `none` (`ClassifyBody` per hub/frontline box) — off the labeled grown unit, never a finished map. The
  endpoint switched `Compose` → `ComposeStages` (same cost, gives the labeled unit) and runs
  **Compose → summary → structural sieve → evaluate → score sieve → render**, so structural rejects skip the
  evaluator and the render. Query params `wools=` (must-include — each family present), `hub=`/`front=`
  (any-of); the filter lives wholly outside the compose call so the seed→board map and the pin path stay
  reproducible. A per-request scan budget replaces the fixed cap and the page reports `Scanned` (matched =
  card count) — a low match rate is the promote-to-target (G98) signal. Client: family/form filter chips
  (Z/scythe greyed — not in the production mix) that apply on click, per-card structure badges that filter
  on click, and a "scanned N · matched M" line. The canonical bucket key (`wools:donut,l|hub:ring|front:none`)
  is `StructureNames.Canonical()`, persisted on pin (`plan.structure`, M0009) as G118's verdict column /
  G120's duel bucket. `StructureSummary.WoolFamilies` promoted from the box-gallery tool (both share it);
  vocabulary row added. Data 17 + Api 72 + Pgm 688 tests green. (G128)

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
  `/maps/new-sketch` page (`SketchCreate`, S11) originates one. (S2e) Plan:
  `docs/contracts/sketch-authoring.md`.
- **Sketch tool end-to-end verified** — a live pass of the whole chain on the running app: `POST /api/sketch`
  create → `PUT .../sketch` a two-island layout → `POST .../sketch/finish` rasterize (advances the map from
  the *sketch* to the *configure* stage) → the sketch-origin map **opens in the Configure wizard** (Map Info /
  ctw / auto-derived objective) → `GET .../export` returns a complete, well-formed world folder (`map.xml`
  parses, `level.dat`, `region/*.mca`). Confirms the originate → Finish → Configure → export path holds; the
  create/finish/export loop is also covered by Api.Tests integration tests. (S2) *(final verification slice;
  the tool itself shipped as S2a–e)*
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
  section (map name) + a **Blank** framed canvas (SVG-preview footprint + symmetry `choice-tile`s with W/D +
  centre `coord-field` rows); a single **Continue** creates the draft via `POST /api/sketch` (carrying the
  working frame → a seeded `setup`). The editor's footprint/symmetry **Setup** block moved off the always-open
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
- **README setup guide** — prerequisites, DB/user provisioning, dev + tests, and the two-step
  scan-out → import flow (incl. the stale-output `ROUND-TRIP DRIFT [kits]` gotcha + `--refresh-xml`
  fix). (B12)
