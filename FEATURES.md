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
  `docs/client/routing-and-ia.md`.
- **Landing + staged dashboard** — `/` is a landing of three lifecycle cards (Sketch · Configure ·
  Edit) with live `stage-counts`; `/maps?stage=sketch|configure|edit` (default edit) is one staged
  overview (`Home.razor`) whose activity rail switches stage and whose primary action + resume target
  follow the stage. Backed by `map.stage` (`MapStage`, migration `M0004` + backfill), `GET
  /api/maps?stage=`, `GET /api/maps/stage-counts`; stage seeded/advanced at sketch-create, import, and
  sketch-finish. Editor home breadcrumbs return to the matching overview; sketch-finish lands on the
  Configure overview with a *Continue* offer rather than force-navigating into the wizard.
- **Plan-as-a-map: the plan is the first lifecycle stage.** An authored plan is a `map` row at
  `stage=plan` (like a sketch), so one map row travels `plan → sketch → configure → edit` through its
  `stage` field. Its coarse-grid layout lives in a regenerable `plan_json` **artifact** (outside the
  entity-replace codec), and a nullable `plan_source_id` (`M0010`) links back to the generator candidate
  it was authored from. Endpoints: `POST /api/plan` (originate a blank plan map), `POST
  /api/plan/{id}/author` (fork a generator candidate into a plan map), `GET`/`PUT /api/map/{slug}/plan`
  (the artifact). `PlanTool` gains `/maps/{slug}/plan` (loads/saves the artifact in place — no fork
  doctrine); the bare `/plan-editor` keeps the generator-candidate pool (`/api/plans`, `?plan=<id>`). The
  dashboard has a **Plans** stage (`/maps?stage=plan`, *New plan* action); the generator's card action is
  **Author this plan** → begins the lifecycle. The generator's many candidates stay `plan` rows (a
  separate pool). Contract: `docs/tools/flow.md`. (C27)
- **A map shows every layer it holds, and each one is one click (S38).** A map is one row that accumulates
  authoring layers and keeps every one it has ever had — a built plan still holds its plan, a configured
  sketch still holds its sketch. The overview did not say so: `map.stage` was doing two jobs at once, *how
  far the map has got* **and** *which list it appears in*, so each map was filed under exactly one stage
  and nothing anywhere revealed the rest. A planned, sketched, configured map showed up only under
  Configuring, its own plan was absent from the Plans list entirely, and reaching that plan meant two
  backward hops through two different lists whose second step only became visible after taking the first.
  Now **stage means progress only.** `GET /api/maps` carries the layers per row (`HasPlan` / `HasSketch` /
  `HasSurface` — the plan blob, the sketch layout, the rasterized world), and every row renders them as
  direct links into those tools, the one matching the map's stage marked as where it stands. The lists
  follow the same split: **Plans and Sketches list every map holding that layer** whatever it has since
  become, while **Configuring and Maps list the maps standing there** — so a map appears in more than one,
  which is correct, and the landing's Sketch count counts its own list. **Reopen is deleted**, not
  reworked: it existed to move a pointer so a map would reappear in a tool's list, and with listing off the
  pointer there is nothing to move. No route ever gated on stage, so `/maps/{id}/plan` already opened the
  plan of a configured map — the two-hop walk was purely a listing artifact. Visiting a layer now changes
  nothing at all, which matters because the visit used to be indistinguishable from the rebuild.
  (`MapsListEndpoint`, `MapSummary`, `Maps.razor`, `.map-layer`; e2e `map-layers`) (S38)
- **A plan builds onto its own map row (C32).** The plan editor's *Create draft* posted `/api/sketch` and
  minted a **new** map every run, so a plan compiled four times left four near-identical maps whose only
  relationship was a slug suffix (`-2`, `-3`, `-4`) — no lineage column, and `plan_source_id` never
  applied, being the fork link into the generator *candidate pool*, written only by *Author this plan*.
  A map-backed plan (`/maps/{slug}/plan`) now builds onto the row already open: layout, rasterize and
  intent all land on it, so one map carries `plan → configure` and re-compiling refreshes it in place.
  Identity follows for free — the compiled intent carries the plan's name, which the intent write applies
  to the document. The built row keeps its `plan_json` beside the new `sketch_layout_json`, which is what
  lets the overview link both of its tools (S38). The bare `/plan-editor` (a generator candidate, no map
  row) still originates one: there the build *is* the map's creation — and it now writes the plan onto that
  map too, so a board built from the bare route also carries the document its layout was compiled from and
  can be reopened in the plan editor rather than holding a layout whose source is nowhere (G152). Contract:
  `docs/tools/flow.md`, whose "the row advances" claim this makes true.
- **The plan editor shows the plan it names (G152).** Opening a map-backed plan could display a drawing
  belonging to no plan at all: the same three maps showed one picture in one browser and another in the
  next, and every plan "created" appeared to contain a board made once, long ago. `LoadFromMap` treats an
  empty `{}` artifact as "no stored plan yet" and keeps the editor's current document — correct reasoning
  about the wrong premise, because that document was not blank: `plan-bridge` restored the last autosaved
  **localStorage** copy at mount, before any route-specific load ran. So New, and any map without a stored
  plan, rendered whatever that browser last had cached. The fix is a deletion: **the document cache is
  gone.** The database is the store — a map's `plan_json` artifact or a `plan` row — and a client-side copy
  of a document the database also holds can only ever disagree with it. The three keys that remain (overlay
  chips, height-map fill, surface stepper) are UI preferences about this browser, not documents claiming to
  be the plan. Accepted consequence: unsaved work is lost on reload.
- **A rebuild changes the ground, not the finish (B49).** Building a plan onto its own map row wrote the
  compiled layout straight over the stored one — and a compiled layout is the board and nothing else. The
  keys a sketch accumulates on top of it (`themes`, `mapTheme`, `roomStyles`, `dressing`) have no
  representation in a plan, so the compiler never writes one, and a themed, dressed map recompiled after any
  plan edit came back as bare stone with every placed prop gone. The build now writes through
  **`PUT /api/map/{slug}/sketch/from-plan`**, which carries those four keys from the layout the map already
  holds onto the freshly compiled one (`SketchLayout.CarryFinish`) — a null on the incoming layout reads as
  absent, since the typed model spells every property out. The sketch editor's own `PUT …/sketch` still
  replaces verbatim, which is what lets deleting a theme or the last prop stick. Moving a map *backwards*
  never rebuilt it in the first place: opening a layer from the overview touches nothing (S38), and
  `POST …/sketch/finish` re-rasterizes the stored layout without touching the intent, so *open the sketch →
  finish* is the round trip out of Configuring and back. Compiling is for when the plan itself has changed.
  `docs/tools/flow.md` § "The hand-offs".
- **A rebuild keeps the credits, and says why it keeps nothing else (B52).** The layout half of a rebuild
  carries a map's finish across (B49); the intent half replaced the stored intent outright, and the compiler
  writes `"authors": []`, so every build wiped the author list off a map that had been through Configure.
  The build's intent write now goes through **`PUT /api/map/{slug}/intent/from-plan`**, which carries the
  authors and contributors over before projecting exactly as a plain PUT does (`IntentCarry`, one shared
  `IntentWrite.StoreAndProjectAsync` so an edit and a rebuild cannot regenerate a map differently). The
  boundary is the deliverable: teams, spawns, wools, build zones and the ST1–ST4 structures are the plan's
  and are meant to be replaced, and the two remaining slices are refused **with reasons**. `islandTeams` is
  a derivation the compile endpoint pre-fills from the geometry it just built, and island ids are positional
  — a stored assignment may name a different island once the board changes, so carrying it would relabel
  territory rather than preserve a decision. `symmetry` is absent from a compiled intent *on purpose*: that
  intent is already fanned across the orbit, so switching `SymmetryExpander` on would ask it to fill units
  that are already there. (At the time this landed, refusing the field was also what kept `B53`'s
  property-dropping hole out of reach; that hole is closed now, but the reason above stands on its own.)
  (B52)
- **The build says which of the two things it is about to do (S39).** One button meant both *originate this
  map* and *replace a board someone has since been working on*, and read the same either way. It now reads
  the map first (`GET /api/map/{slug}/layers` → the four layer facts for one map, the per-map form of what
  the list carries): a plan with no sketch and no world offers **Build the map** and runs; a map that has
  both offers **Rebuild this map** and states the trade before doing it — *replaces* the terrain and islands
  plus the teams, spawns, wools and build zones the plan states; *keeps* the terrain themes, room shells,
  placed dressing and the map's authors. That list is not decoration: it is exactly what B49 and B52 made
  true, so the sentence and the behaviour are the same fact. Cancel is a real exit, and a first build is
  never interrupted — there is nothing to lose, and a confirmation that always fires is one nobody reads.
  (`MapLayers`, `MapLayersEndpoint`, `PlanTool`, `.plan-rebuild-warn`; e2e `map-layers`) (S39)
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
  `WorldCanvas`** (island-select then symmetry overlay — the same canvas the Configure World phase
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
  real category. See `docs/pgm/region-data-flow.md`. (E10)

## Canvas & shared UI (C)
- **The icon set is vendored, not fetched (C30).** `index.html` pulled lucide from `cdn.jsdelivr.net`
  on every page load, pinned to `@latest`. That failed twice over: the CDN is unreachable from any
  egress-restricted environment, so **no icon rendered at all** there (blank nav rail, toolbars and
  chips), and an unpinned tag let the icon set change under the app without a commit — it already had,
  `check-circle-2`/`minus-square`/`plus-square` having been renamed upstream. `wwwroot/js/studio/vendor/
  lucide-icons.js` now carries the **111 icons this app names** (24.8 KB, against 414 KB for the full
  bundle) plus lucide's own render path, generated by `tools/vendor-icons.mjs` from a pinned release
  fetched with `npm pack` — download only, no install, no `node_modules`. Icon names are collected by
  testing every kebab-case string literal in the client against lucide, so the names built in C#
  (`RegionNode.Icon(type)`, `PhaseIcon`, `c.Icon`) are covered, not just the `data-lucide="…"` attributes;
  a name that is somehow still missed renders nothing, so the shim reports it as a **console error** and
  the smoke sweep fails the page rather than shipping a blank square. `tools/vendor-icons.mjs --check`
  fails if the committed file is stale. `tests/e2e/icons.mjs` asserts positively that placeholders became
  real svgs on three icon-dense routes, and pins the shim against lucide's semantics (aliases, caller
  attrs, class merging, the a11y opt-out). The harness's CDN allowance is gone. (C30)
- **A committed end-to-end harness — the smoke layer of C28 (C31).** The "Playwright N/N" numbers in the
  entries below were one-off runs in the sessions that produced them; nothing in the repo could reproduce
  one. `tools/e2e.sh` can: it resets **its own database** (`pgm_studio_e2e`) and serves on **its own port**
  (7895), so a run can never touch the dev data — the specs create maps, and on the dev DB they pile up in
  the dashboard. Migrate → build → start → wait for health → seed → run → tear down; `npm run e2e`,
  `--keep` to leave the server up.
  - **Seeded from the composer, not from hand-drawn boxes.** A pinned descriptor
    (`players/teams/symmetry/seed/cell`) composes the same real board every run — spawns, wools, a hub, a
    frontline, connected zones — so the pages render something representative *and* the fixtures are stable
    without committing a fixture file. Three maps, one per stage the routes need: a composed candidate
    committed to authoring (`plan`), the same layout carried through compile → sketch → **Finish** into
    world geometry (`configure`, and what `/edit` opens), and a draft holding that layout (`sketch`).
  - **`smoke.mjs` — 15 routes × (renders · is clean).** "Clean" means no uncaught exception, no console
    error, no failed or 4xx/5xx request. Anything tolerated must be named in `ALLOWED_FAULTS` **with a
    reason and a task id**, so an allowance is a decision on the record rather than a silent filter.
  - **`plan-refusals.mjs` — the refusal contract**, written by corrupting a composed plan one slice at a
    time so a refusal is attributable to the slice. Structural breakage is refused with findings; malformed
    input is 4xx and never 5xx; `Finish` refuses a one-island layout.

  It paid for itself before it was finished — three defects, none of which a unit test could see:
  **a dead `PgmStudio.Client.styles.css` link 404ing on every page** (the project has zero `.razor.css`, and
  its own UI contract says it never will — link removed here); **the icon set is a runtime CDN dependency**
  (C30, still open); and **`/plan/evaluate` answering 400 on a plan that is merely empty** while its sibling
  `/plan/inspect` answered 200 on the same body (B39, fixed below). It also measured that **nothing stopped
  an incomplete plan compiling** (B38, gated below) — asserted as the behaviour of the day so the suite
  stayed a signal, and written to fail loudly when a gate landed, which is how it did. 31/31 smoke,
  22/22 refusals. (C31)
- **The canvases share their machinery instead of re-deriving it (CV13 + CV14).** Two shapes of
  duplication, both of which had already cost something:
  - **The layer z-stack was stated twice per canvas** — once by the `#…Layer` field declarations, once by
    the append-loop array — so the two could drift silently. `render/layer-stack.js` states it **once**:
    the key order of the spec is the paint order, bottom first, and each group gets a
    **`data-layer="<name>"`** so a layer can be addressed by name rather than by its index among its
    siblings (an index that moves the moment a layer is inserted — which is exactly how the C29 probes
    broke). Sketch's 19 layers and Plan's 15 now read as two declaration blocks.
  - **`CanvasBase` owned the inverse projection but not the forward one**, so `wx * scale + panX` was
    written out in six places. It is now `Geom`-side pure math (`transform.toScreen`, unit-tested both
    directly and as the inverse of `_clientToSvg`) with a `_toScreen` on the base. Joined by the other
    shapes that were copied per canvas: `_size()`/`_hasBox()` (the `(clientWidth || 600) - 24` measure,
    cached so per-frame paths don't force a layout), `_resizeSvg()`, `_fitWorldBox(box, {margin, pad})`,
    the `ResizeObserver` + deferred-fit that C29 had to add to each canvas separately, and the whole
    **3-D preview lifecycle** (`_showIso`/`_hideIso` + `_isoLayers`/`_isoTag`/`_onIsoEnter` hooks) — which
    puts the "WebGL unavailable → stay in 2-D" fallback in one place.

  Behaviour-preserving by construction and checked as such: the same Playwright suites that gated C29
  return **identical numbers** (17/17 both tools, 11/11 sketch regression, 8/8 resize/zoom), JS 150/150.
  The **edit** canvas — rerouted too, and the one surface with no fixture here — was covered by building a
  map locally instead of pulling the corpus: a sketch drawn and **Finished** rasterizes to real world
  geometry, and `/maps/{slug}/edit` opens on it. Its `Regions` phase paints both islands, wheel-zoom moves
  the viewport (100% → 115%), the cursor read-out tracks, and the `Build Regions` side-view renders the
  slabs in cross-section — 7/7, no console errors. (CV13, CV14)
- **The landing page's Plan card opens the plan overview, not the bare editor.** It pointed at
  `plan-editor` — an unsaved scratch document — while Sketch/Configure/Edit all opened their
  `maps?stage=…` list, which is also what the page's own header comment says every card does. Now
  `maps?stage=plan`, so the four lifecycle cards behave alike and "Plan a layout" starts where the
  other three do: on the list, with a **New plan** action. (CV13)
- **One drawing-surface model for Sketch + Plan: tinted working area · viewport grid · scale bar** — a
  fresh sketch opened on a postage stamp you couldn't draw your way out of, and the plan editor it was
  modelled on had the same defects. Both canvases now share `render/canvas-chrome.js` and the contract in
  the drawing-surface model:
  - **The working area is a tinted region with a default size, present even when blank** — the size anchor
    that says how big a map is meant to be (Sketch **64×64 blocks** = 4×4 chunks; Plan **~60×60 blocks** =
    12×12 cells, expressed in blocks and converted to whole cells so it holds at any `globals.cell`). It
    grows to enclose the content plus a buffer, so drawing past its edge **visibly** grows it — the part the
    plan editor's bounded grid could never show, because its added border landed off-screen.
  - **The grid spans the visible viewport, not the content.** Previously the grid was clipped to the very
    rect `fit` framed, so it filled 85% of the surface with ~5 blocks of canvas beyond it and growing it
    meant drawing off the surface. Now there is always grid (and surface) outside the drawing; rebuilt only
    when the snapped visible extent moves, not per pan frame.
  - **A `N blocks` scale bar** (bottom-right, screen space) carries absolute size, since the working area's
    edge moves as the drawing grows.
  - **Both canvases re-measure themselves** with a `ResizeObserver`, deferring a fit requested while they
    have no layout box. Both tools mount their canvas inside a `display:none` Draw phase and both "fixed"
    it with a `resize()` nudge on the phase switch that ran *before* Blazor re-rendered — so both sat at
    **576×576 inside a 966×769 workspace** (45% of the available area). The nudges are gone.
  - Sketch's `load()` also fitted before the saved shapes were added, so an existing sketch reopened on the
    blank working area instead of on its drawing; it now fits after the load.

  Verified against the running app with Playwright: 17/17 across both tools (canvas fills the workspace on
  the Info→Draw path, default working area present and correctly sized, reads as a region rather than the
  whole surface, grid covers the surface, scale bar present, drawing past the edge grows the area), plus
  11/11 sketch regression and 8/8 window-resize / full-zoom-range. (C29)
- **Hybrid canvas** — the reference `WorldCanvas` JS reused via interop (`studio-canvas.js`). (C1)
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
  `docs/client/ui-conventions.md`. (C12 phase A)
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
- **`/design` regenerated from the real components** — the living style guide's examples now render the
  actual `<Section>`/`<Field>`/`<Button>`/`<Badge>`/`<ListRow>`/`<CoordField>`/`<DetailHeader>` (its `ds-*`
  gallery frame stays), so the showcase can no longer drift from production. Un-componentized patterns
  (draw tools, flow-bar, check-rows, region-tree rows, meters, cards) stay as hand-written references.
  Also filed: `Components/Primitives/Icon.razor` — `<i data-lucide="@Name" @key="@Name">` centralizing the
  lucide reconciler gotcha, **built but not yet adopted** (the ~156 raw `<i>` stand; roll out later). (C12 phase D.3)
- **`FlowBar` component** — the wizard sub-step strip (phase icon/title + optional sub-step pills +
  Back/Next), extracted from `ConfigureLayout`'s raw markup under `Components/Layout/`. Originally
  deferred as single-use (C12 phase D.3); built once the Editor/Configure shell-convergence work
  needed a second consumer. `ConfigureLayout` now composes it (`.configure-flow-bar` renamed to the
  generic `.flow-bar--flush`) with no signature change, and `/design`'s flow-bar showcase renders the
  real component instead of hand-typed markup. (C21)
- **Edit's activity rail reordered to lead with identity** — `overview` moves ahead of `setup`, matching
  the Configure wizard's `info`/`world` ordering (previously the two pages ordered the same pair
  differently with no reason). `Editor.razor`'s topbar/rail/switch stay otherwise untouched — C21 already
  found the shell chrome (`StudioShell`/`Topbar`/`ActivityRail`) was shared with no duplication to remove.
  (C22)
- **Overview (Edit) aligned with Map Info (Configure)** — drops the live map canvas + JS bridge
  (`overview-bridge.js`/`overview-renderer.js`/`studio.mountOverview`, no other consumers) for the
  same centered `ContentColumn` layout Map Info uses; Version/Objective/Contribution and the explicit
  Save button stay editable (Edit hand-edits an existing map's stored metadata, unlike Configure's
  regenerated intent slice). Map Info gains the Contribution field the raw editor already had: the
  intent's `authors`/`contributors` move from plain-string arrays to `{name, contribution?}` objects
  (`MapIntent.AuthorIntent`), threaded through `ResolveAuthorsAsync` — nothing downstream (DB, XML,
  generator) needed to change, `Deserializer.DecodeAuthor` already reads the key. No compat shim for
  already-stored intent blobs (dev/WIP data, no back-compat policy in this codebase). (C23)
- **Setup and Build (Edit) migrated onto `FlowBar`** — retires their bespoke step nav
  (`.cfg-step-bar` numbered tabs + a `Sidebar` `Footer` Prev/Next, both confirmed to have no other
  consumers and removed) for the same flow-bar strip Configure's World/Build phases use.
  `ConfigureActivity` (Setup)'s Prev/"Next or Finish" footer collapses into `FlowBar`'s
  `OnBack`/`OnNext`, with `NextLabel`/`NextEnabled` computed the same way `ConfigureWizard` computes
  them per-phase. `.configure-main`/`.configure-flow-bar` (C21) are the shared `.phase-body`/
  `.flow-bar--flush` wrapper both activities now use alongside `ConfigureLayout`. Completes the
  Editor/Configure shell convergence filed in `TODO.md` this session (C21–C24); the
  authoring-stage vocabulary was then unified across both tools (C25). (C24)
- **`FlowBar` on every Edit activity, with cross-activity Back/Next** — the four previously bare
  activities (Overview, Teams, Objective, Regions) gain a `FlowBar` too, for visual consistency with
  Configure (which shows it even on zero-sub-step Map Info). Its Back/Next is now a second, optional
  path alongside the activity rail: `Editor.razor` owns `GoAdjacent(delta)`, walking `Activities` in
  rail order (`Overview → Setup → Teams → Build Regions → Objective → Regions`) — Back disabled at
  Overview, and Next past Regions ("Done") navigates to `/maps`, the editor's counterpart to the
  Configure wizard's end-of-flow Export. Setup's step-2 Finish and Build's step-2 Next now advance to
  the *next activity* via the same `OnNextActivity` callback, replacing Setup's old hardcoded
  jump-to-Overview (`OnComplete`/`GoOverview`, both removed) — a real behavior change caught while
  wiring this up, not just cosmetic. (C26)
- **Unified authoring-stage vocabulary — Phase / Step / Section (+ `NavRail`)** — the Editor and the
  Configure wizard are two views over the same ordered authoring spine but had named it two ways
  (Editor = *activity* + *step*, Configure = *phase* + *sub-step*). Settled on three strict tiers:
  **Phase** (a rail stage), **Step** (a screen within a phase, driven by `FlowBar`), **Section** (a
  titled panel group). Pure rename, no behavior change, landed in slices: (1) the shared rail
  `ActivityRail`/`ActivityButton` → `NavRail`/`NavButton` (+ `.activity-rail`/`-btn`/`-logo*` →
  `.nav-*`) — kept generic since it also carries plain nav on Home/Sketch and panel toggles in Plan,
  not phases; (2) "sub-" dropped — `FlowBar` `SubSteps`/`CurrentSubStep` → `Steps`/`CurrentStep`,
  `ConfigurePhase.SubSteps` → `Steps`, wizard `subStep`/`SubLabel` → `step`/`StepLabel`; (3) the Edit
  page renamed to match `Sketch`/`Configure` — `Pages/EditorActivities/` → `Pages/Edit/` (host
  `Editor.razor` moved in, namespace `…Pages.Edit`), the six `*Activity` components → `*Phase`
  (`ConfigureActivity` → `SetupPhase`, matching its `setup` id and dodging the `ConfigurePhase`
  record), and `Editor.razor`'s `Activity`/`Activities`/`Is*Activity`/`On*Activity` → `Phase`
  equivalents. The generic `.activity-viewport` shell class is left as-is (chrome shared by all
  pages, like the rail). (C25)
- **Page file-role naming unified on `*Tool`** — the five tool hosts renamed to one pattern matching the
  "everything is a tool" model: `GeneratorBrowse`→`GeneratorTool`, `PlanEditor`→`PlanTool`,
  `SketchEditor`→`SketchTool`, `ConfigureWizard`→`ConfigureTool`, `Editor`→`EditTool` (fixes the bare,
  prefix-less `Editor`). Plus `Home.razor`→`Maps.razor` (the `/maps` dashboard; `Index.razor` stays the
  `/` landing) and `EditorLayout`→`StudioLayout` (it wraps all 11 pages, not just the editor). Routes,
  cascades (`ConfigureTool Wizard`), and `App.razor`'s `DefaultLayout` updated; class rename only, no
  behaviour change. Entry-page naming (`SketchCreate`/`ImportPhase`) deferred. (C26)
- **Identity phase label unified + Sketch on the phase model** — the identity surface is **`Identity`**
  everywhere (Configure `Map Info`/Edit `Overview` → `IdentityPhase`). The Sketch tool became a phase host:
  an **`Info`** phase with `Identity` (editable name + username-verified **authors**, via the shared
  `AuthorsEditor` + the map-metadata endpoint — sketches are map rows) and `Settings` (symmetry) steps,
  plus a **`Draw`** phase (the canvas, kept mounted/hidden across phase switches). The canvas **auto-grows
  to the drawn content** (plan-editor model — working bounds = content + a one-chunk buffer, snapped to
  chunk lines; the minimum area and the grid extent were later reworked by C29), fixing the old
  fixed-frame that didn't grow; footprint/size presets are gone (the exported
  world was always the tight content bounds). The `/maps/new-sketch` creation page is removed — **New
  sketch** creates an untitled draft and opens it on `Info` (`?phase=info`) to name it. Verified end-to-end
  with a Playwright harness against the running app. (C27)
- **Shared `AuthorsEditor` across every tool + abandoned-draft cleanup** — the Edit and Configure Identity
  phases dropped their duplicated author/contributor rows + Mojang resolution for the shared `AuthorsEditor`
  (the Sketch Info phase already used it), so all tools credit authors identically; each phase keeps only
  its own load/save (Edit → map metadata, Configure → the intent meta slice, verified-only). `AuthorsEditor`
  now resolves a row either way — stored uuid → name **or** stored name → uuid — so a name-only row (the
  Configure intent's shape) shows its head on load. And a **New sketch** draft left untouched is auto-discarded
  (`DELETE /api/map/{slug}/sketch/discard-if-empty`, called on the tool's dispose) when still pristine —
  sketch stage, default name, no authors, no shapes — so an abandoned click no longer litters the dashboard.
  Verified: curl (discard keeps renamed/drawn drafts) + Playwright (leave an empty draft → gone). (C27)
- **Plan tool on the phase model (map route)** — a map-backed plan (`/maps/{slug}/plan`) is now a phase host
  like Sketch: the rail is `Info`/`Draw`. The new `PlanInfoPhase` has an `Info` phase with **Identity**
  (plan name + username-verified authors via the shared `AuthorsEditor`, saved to the map-metadata endpoint)
  and **Settings** (the plan globals — symmetry + cell/surface/headroom/max-players — rendered here and
  forwarded to the plan-doc bridge by the host). The **Draw** phase is the canvas workspace (kept mounted,
  hidden across switches, re-measured on return), its sidebar stripped of name/globals and given an
  in-sidebar Settings/Validation switch since the rail carries phases now; Reference + overlays stay on the
  canvas. The map row's name is authoritative — a rename saved to metadata is synced into the plan doc on
  load, surviving a reload without an artifact re-save; **New plan** lands on `Info` to name it. The bare
  `/plan-editor` candidate route is unchanged (no phase host). Playwright 17/17. (C27)
- **Configure Import folded in as a conditional phase-zero** — the standalone `/maps/new` landing page is
  retired; `ConfigureTool` now owns the Import phase (the ex-`ImportPhase`, now the `ImportPhase`
  component — Source → Found → Plan, pick-a-folder **or** paste-a-link). It routes both
  `/maps/{slug}/configure` and `/maps/new`: the slug-less route (a map has no id until its world is
  imported) shows Import; on import it navigates to `/maps/{slug}/configure`, which **skips** Import and
  opens the wizard at **Identity**. So Import is phase-zero for a new/unimported map and never re-picked
  once the world is set. No server change (same `import-folder`/`import-url` endpoints + scan brief).
  **This completes C27** — every tool is now on the phase model. Playwright 10/10. (C27)
- **Spawn-protection rendering on the Teams canvas** — protection regions (the `subtype == "protection"`
  facet from the C16 spawn split) surface in a dedicated "Spawn Protection" section and render on the
  spawn-filtered Teams canvas, not just point spawns. (C18)
- **Graceful canvas degrade on missing/degenerate bounds** — `transform.js` `fit()`/`buildTransform`
  tolerate a null `bounding_box` or a zero/non-finite world extent (xml-only / not-fully-pipelined maps,
  single-region maps where min == max), falling back to unit scale so the transform stays finite instead
  of throwing `JSException` "unhandled error". (C13, `5dda68f`)
- **Region geometry editing** — drag the 8 resize handles (rectangle/cuboid) on the canvas *and* type
  exact coords in the inspector; both persist (`PATCH /regions/{id}` bounds/coords) and stay in sync via
  the shared `Models/RegionEdits` (`WorldCanvas` raises `OnGeometrySaved`; the host persists). Wired in
  all four Edit activities. `docs/client/canvas-interaction.md` §3. (CV1)
- **Arrow-key region nudge** — the selected rectangle/cuboid moves 1 block (Shift = 16) with the arrow
  keys; a single `document` keydown handler on the shared `WorldCanvas` (guards: canvas not visible,
  focus in a field, nothing selected) translates it live and persists through the same
  `onBoundsSave`/`OnGeometrySaved` path (debounced) — so Edit (PATCH) and Configure (intent + re-orbit)
  both get it. §4. (CV3)
- **Canvas interaction controllers** — `WorldCanvas` delegates every interaction mode to plain
  controllers (state-accessor closures + callbacks; the canvas forwards its `CanvasBase` hooks):
  `WorldDrawController` (draw), `WorldEditController` (8-handle resize + arrow-key move), and
  `EditorSelectController` (click-select modes: region / island, each a registered picker — so
  `_onCanvasClick` is one dispatch, not an `if`-chain). The shared abstraction the S2 sketch port
  reuses. §5. (CV4, CV5)
- **Shared renderers** — one `renderSymmetryOverlay` (`shared/symmetry-render.js`, all 6 symmetry
  types) replaces the three drifted copies in `WorldCanvas`/`ConfigureRenderer`/`OverviewRenderer`,
  **fixing** the latent bug where `ConfigureRenderer` couldn't draw diagonal mirrors and
  `OverviewRenderer` couldn't draw rotations or diagonals. `WorldCanvas` block + island rendering now
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
  (`WorldScanStep`/`WorldSymmetryStep`/`ImportPhase`/`ConfigureActivity`) plus a `SymLabelShort` in
  `TeamAssignStep`; collapse them into one `Client/Models/SymmetryInfo` (`Label` + `ShortLabel`). The orbit
  *count* re-derivers (`BuildLayerStep.SymmetryOrder`, the `SuggestedTeams`/`SuggestedCount` in
  `ImportPhase`/`WorldSymmetryStep`/`TeamAssignStep`) no longer re-encode the `rot_90 → 4 / else → 2`
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
  always caller-supplied. It replaces `world-canvas`'s `#regionAttrs` + marker attrs + the triplicated
  `#refreshRegionDisplay` numbers, sketch's `shapeAttrs`, and the inline plan piece/zone/ghost styling; the
  duplicated add/sub colour constants collapse to one `OP_COLORS`/`opColors` source (sketch render + draw
  controller). Icons route through `RegionNode.Icon` — `SpawnStep`'s hardcoded `cylinder` and
  `WoolMonuments`' `square` become the canonical `point → dot`. Plan's surface-tint + hatch stay
  Plan-specific. The shared helper is `render/primitive-style.js`; `canvas-interaction.md` §10. (CV9)

- **The Button variants are painted (C33).** `<Button Variant="primary|danger|warn">` emitted
  `action-btn--primary` / `--danger` / `--warn` and no stylesheet defined any of the three, so 47 buttons
  across the studio declared a meaning that rendered as the plain default — a destructive confirm looked
  exactly like the Cancel beside it. Each variant now takes the tinted-fill + coloured-edge treatment the
  studio already marks a state with (`.filter-chip--active`, `.draw-tool-btn--active`) in that meaning's own
  token, so no new colour enters the palette. (C33)

- **A drawer footer can no longer push its own actions off screen (C34).** `SideDrawer`'s body took a flex
  basis of 0, which in a fixed-height column means it absorbs none of a negative free space — so a footer
  that outgrew the room left overflowed the drawer instead. The plan editor's rebuild confirmation states
  what a second build trades *above* its buttons, which put **Rebuild anyway** below the bottom of the
  viewport with no way to scroll to it. The body now takes the shrink, the footer is capped at half the
  drawer and scrolls inside that cap, and the header never shrinks; the two confirm buttons share one row
  (`Fill`, not `Full`) instead of stacking. (C34)

- **One place to compile a map-backed plan, and one place to read its files out (C35).** The plan editor's
  topbar repeated what the surfaces under it already offered: **Compile** duplicated the flow bar's Next
  (which *is* Compile), and **Export** was a lesser copy of the compile drawer's own Download. The drawer
  now carries a **Plan** tab beside Layout and Intent — the plan document itself, kept whatever the compile
  answers, so a plan that will not compile is still the one an author can take a copy of — and the
  map-backed topbar is down to Save. The bare `/plan-editor` route has no flow bar and keeps its full bar
  (New · Import · Export · Save · Open · Compile). (C35)

- **The library's editor rail is wide enough to edit in (C36).** The rail was a fixed 320px while the grid
  beside it — which only picks what to work on — took everything else. At that width a material's own row
  could not hold a kind select, the extent it claims and a remove button at once, so the remove button fell
  to its own line, the extent sat unaligned beneath the style it belonged to, and the panel grew a
  horizontal scrollbar. The rail now takes `clamp(360px, 32vw, 560px)`; the extent moved into the material's
  row through a new `MaterialEditor.HeadExtra` slot (it belongs to the list that owns the entry, not to the
  material, but it reads as one row), which retires the `.material-entry` wrapper; `.panel-actions` wraps, so
  save · copy · delete never run off the edge. The sketch's Dressing inspector — the same editor embedded —
  starts at 420px, and the resize handle's range is `[260, 680]` rather than `[200, 560]`. (C36)

- **Explanations live behind a "?", beside what they explain (C37).** Every blurb in the library rails and
  the material form sat open under the control it explained, which meant it was restated once per copy of
  that control: a layer stack of three voronoi layers said what a voronoi is three times over, and the knobs
  it belonged to fell off the bottom of the panel. `<HelpMark>` is a "?" that sits **in** a row — a section's
  heading, the rail's title, the dropdown naming a material's kind — and the note is rendered by that row's
  owner directly beneath it, full panel width. `Section` carries this as a `Help` slot (with `SectionHeader`
  gaining a `TitleEnd` so the mark hugs the title rather than the right slot). A material's kind note appears
  **only where the material is authored**, never on a nested layer, band or stop: a nested entry is a reuse
  of a kind, not a second definition of it, and repeating the paragraph per copy was the original complaint
  in a new shape. Prose that is one short paragraph under its own heading and never repeats — the page
  intros, the sketch Dressing prop notes — stays prose. (C37)
- **One way to start a style, a theme or a room style (C38).** The three library tabs each invented their
  own: styles offered a second row of chips under the Kind filter (clicking one *created* a style, which the
  chips gave no sign of, and which read as a second set of filters), while themes and rooms asked for a name
  in the sidebar behind an **+ Add** button before the rail would open — so a theme was half-created in two
  places. All three now open the rail from one primary **New …** button at the top of the left rail, and the
  rail's Name field is where the name goes; the save stays disabled until it is filled in. A style starts as a
  solid and takes its kind from the editor's own dropdown, which is the same control that reads a saved
  style's kind back. The styles rail's filter section is titled **Filters** with a *Kind* field under it, the
  shape catalog's shape. (C38)

- **A material's row is one row of one height (C39).** Three separate misalignments in the same strip. The
  entry's name (`GRID LINE`, `LAYER 1`) shared the row with the controls, competing for its width and
  pushing the kind select out of line with every other kind select in the stack — it heads the block now.
  The extent arrived as a labelled `Field`, whose label stacked above its input and left the number sitting
  lower than the select beside it — it is a `CoordField`, carrying its unit inside the box
  (`COURSES 1` · `BLOCKS 2` · `CELLS 3`). And `BlockPicker` put the (id, data) pair on a second row below the
  name, reading as a separate setting rather than as the same one said exactly — chip, name and pair now
  share one row as `[Stone ▾] [ID 1] [DATA 0]`. All three rows (`.material-editor-head`,
  `.block-picker-row`, `.lib-bind`) give every control **`--control-height`**: a select, a coord-field and an
  icon button land on 31 / 29 / 26 px for the same type size, so the row states the height once instead of
  taking each element's own. Stating it is also what makes the height *definite*, which is what lets the
  square controls — the icon button, the colour chip, the bound-style swatch — read their width off it with
  `aspect-ratio: 1` and come out square rather than stretched into pills. (`align-items: stretch` is not a
  definite cross size: given nothing to measure, the ratio collapsed the chip to its borders.) The room
  composer's remove-course button takes `Variant="icon"`, which is what it always was.
  `.material-entry-scalar`, `.block-picker-ids`, `.block-picker-id` and `.block-picker-num` retire. (C39)

- **An icon scale, and a gate under the control row (C40).** Glyph sizes were eight arbitrary numbers
  (11 · 13 · 14 · 15 · 16 · 20 · 22 · 24px) across twenty rules with no scale behind them — and **six of
  those rules were dead**: they selected `i`, which the vendored shim *replaces* with an `<svg>`, so
  `.lib-rail-x i { width: 13px }` had never applied and that ✕ rendered at the JS default 16. Two of the
  dead ones were meant to colour the plan drawer's warning and success marks, which had therefore never
  been coloured. The five-step `--icon-xs/sm/md/lg/xl` scale replaces all twenty, the dead selectors are
  live, `svg.lucide` carries the default so the stylesheet owns the size (studio.js's attribute is now
  only a pre-paint fallback), and `.nav-rail`'s `!important` — which outranked nothing — is gone.
  `tests/e2e/controls.mjs` gates both this and `--control-height`: every control in a library row is one
  height, every square control is square, and every rendered glyph lands on a scale step. Its load-bearing
  check is the one the token did *not* produce — an unconstrained `select.field-input` must still measure
  `--control-height`, which is what ties the token to the type scale rather than to itself. Verified by
  mutation: a wrong token and an off-scale glyph both fail the run. (C40)
- **The canvas controls float on the map, placed by what they are (C41).** One 36-px bar above the canvas
  held everything at once — the draw tools, the layer chips, an island picker, fit, the 3-D toggle, the
  cursor, the live size and the zoom. Across a dozen icons that row overlaps itself the moment the canvas
  column is narrow, which is most of the time with both rails open, and it put the drawing tools at the far
  edge of the surface they draw on. The bar is gone from every drawing canvas (Sketch, Edit, Configure,
  Plan) and its contents are placed by what each control **is**: `CanvasReadout` top-left is what the canvas
  *is* (cursor, live size, zoom — read-only, pointer-transparent so a drag across it cannot stall, and each
  item collapses when it has nothing to report); `CanvasLayerBar` top-right is what it *shows*
  (`LayerChip`s, then the `CanvasRoundButton` view actions, then `ViewModeToggle` in the very corner,
  outermost because it decides what every other control in the row means — in 3-D the chips have nothing to
  toggle and fit becomes rotate); `CanvasDock` bottom-centre is what the pointer *does*, where the pointer
  already is. Surfaces share one blurred-glass look through `--canvas-float-*`, tokens that follow the theme.
  The dock holds `DockGroup`s, never loose buttons, and a group may carry an **accent** — the colour of the
  mode it is armed in — which reaches its controls as `--dock-accent`, so a `DockModeButton`'s word and the
  shape buttons beside it state the same thing at once and an armed carve cannot be read as an armed build;
  the group fades its contents (not its box) while the tool in hand is not one the mode applies to. Two
  mode toggles moved onto the dock this way: Sketch's Build/Carve out of the tool strip, and the buildable
  layer's Bridge/Hole out of the Configure sidebar (supplied as a `DrawMode` slot, so the canvas owns the
  shape and the step owns what the shape means). A dock control names itself **upward**: the browser's own
  tooltip renders below the pointer, and every control here is a few pixels off the bottom of the canvas, so
  the native one landed on the viewport edge or outside it. They carry the name as `data-tip` (with
  `aria-label` for the accessible name a glyph-only button would otherwise lack) and CSS draws it above, on
  a delay so crossing the dock does not trail a caption. It is the tool's **name**, not its manual: a lasso
  and a polygon explain themselves, and a sentence in every tooltip is a sentence in the way. The two mode
  buttons already print the state they are in, so theirs names the destination instead ("Switch to Carve"). Plan's Interfaces/Frontline/Labels/Heights overlays came
  out of the Settings panel onto the canvas they annotate. `tests/e2e/draw-tools.mjs` gates the operation
  contract: one control, colouring the three tools it decides for, dimmed when it decides nothing. The
  retired `.canvas-subbar` draw vocabulary (`.op-pill`, `.subbar-sep`, `.canvas-dim`, `.canvas-zoom`,
  `.canvas-island-select`, the whole `.plan-toolbar` palette) is deleted; `.canvas-subbar` itself survives
  only on the two surfaces that are not drawing canvases (the build-height side view, the import preview).
  Shown as its own section in the `/design` catalog. (C41)
- **A plan tool family shows the tool in hand and keeps the rest a chevron away (C42).** The plan editor
  laid out all nineteen of its drawing tools at once — a wall that wrapped to two rows and forced a hunt
  for the swatch used a minute ago. The nineteen are four families of one question each (which piece role ·
  which technical annotation · which marker · which box kind), and within a family only one is ever armed,
  so a family collapses to a `DockFlyoutGroup`: the option in its slot plus a chevron, Figma's shape-tool
  flyout. Click the option to arm it, click the chevron for the rest, pick one and it takes the slot — the
  last pick stays visible, which is what makes the collapse cost nothing. Six buttons remain. The flyout
  opens **upward** (a downward menu would open off the canvas) and **from the chevron's left edge** — the
  chevron is what opened it, so that is where it comes from. It lists the **whole** family, the option in
  the slot included, in three columns: a tick on the one that is armed, the glyph or swatch, and the
  option's name. A name and not a bare glyph because a flyout shows one family with nothing around it to
  say which family, and five unlabelled swatches are five colours with no meaning; the name says which
  family where the word collides across them ("Wool marker", "Wool box", "Wool room") and stands alone
  where it does not (Buffer, Destroyable, Core). A row carries no tooltip — it is already showing its name.
  The chevron's tooltip is the family's own plural (Pieces · Zones · Markers · Boxes), which is the only
  thing a control that opens a family has to say. Which family is open is the host's state, so opening one
  closes the others, and a scrim catches the dismissing click so it cannot instead start a drag on the map
  hidden behind the menu. The promoted option's button is `@key`ed on the option: a lucide glyph **cannot**
  be swapped in place, because lucide replaces the `<i data-lucide>` with an `<svg>` after every render, so
  there is no `<i>` left for Blazor to re-point — it patches a node it no longer owns, which showed as a
  promoted "destroyable" still wearing the flag icon and, on a flyout switch, as the reconciler taking
  `insertBefore` into a null parent. Keying moves the swap up to the button, which is still Blazor's to
  replace whole. (C42)
- **The `connector` role is retired (C43).** It was the second annotation role — an attachment-point mark
  for composing reusable lane/spawn *fragments*, whose dangling edge had nothing to derive a plug-point
  from. The composer works by box intersection, which makes the attachment point derivable everywhere, so
  the mark was never reached for and never would be. Gone from `PlanRoles` (the annotation set is now
  `buffer` alone), from `BoardDeriver`/`ShapeClassifier`/`PlanBoxes`, from `plan-doc.js`'s `ROLES` /
  `ROLE_COLORS` / `TECHNICAL_ROLES`, from the plan canvas's crossed hatch and the dock's swatch. A stored
  plan naming it loads as a plain `piece`, the same fold `lane`/`hub`/`mid` take — there is no migration
  because nothing authored one. `tools/compose/wool-lane-study.cs` and its six fixtures are deleted with it:
  the study was a rendering of connector-based authoring and could not outlive its subject. The generator
  docs (`model.md`, `vocabulary.md`, `evaluator.md`) follow. (C43)

## Backend / API (B)
- **A dressing document that fails to parse refuses the export by name, rather than exporting bare (`B130`).**
  `DressingJson.Deserialize`/`DeserializeProp` caught every `JsonException` and returned `DressingDoc.Empty` /
  `null`, commented "a hand-edited blob must not fail an export" — so one unrecognized prop `kind` or one field
  of the wrong shape discarded **every** prop on the map and the export still answered 200. A parse failure now
  throws `DressingParseException`, naming the prop (by id, or by position when it never got one) and the field,
  and `MapExportComposer` turns it into a **422** carrying the rule id `DR-DOC` — `{error, rule, message,
  subject, field}` — on both `GET /xml` and `GET /export`, joining `OB17`'s traversability refusal rather than
  falling through to the generic 500. One bad prop costs the *whole* document, not a silent partial list: a
  fifty-prop map with one wrong `kind` refuses fifty-for-fifty rather than shipping forty-nine unannounced.
  Two things travel with the fix. `AllowOutOfOrderMetadataProperties` removes the polymorphic `kind`
  discriminator's first-key constraint (`PlacedProp` and the nested `TerrainMaterial` on `pave`/`bank`/`rock`
  alike) — a hand-authored document has no reason to prefer one key order, so the reader no longer does either.
  And a prop's own enum fields (`style`, `form`) already read case-insensitively regardless of the naming
  policy they are written with, so the corpus writing `"Rough"`/`"Natural"` rather than the documented
  `"rough"`/`"natural"` was never the actual fault — `sketch.md`'s Dressing section says so explicitly now,
  since the theory that case broke parsing is exactly the kind of claim worth writing down as settled.
  (`DressingJson.cs`, `DressingJsonTests.cs`; DR-DOC)
- **One block volume, one type (`B33`).** `BlockBox` (`PgmStudio.Domain`) is the single inclusive integer
  AABB for every role a block volume plays — the region an author boxes for a scan, the volume a stamper
  fills, the casing `CoreSuggester` proposes — carrying the union of the helpers the two former copies had
  (`Width`/`Height`/`Depth`/`CuboidMax` + `Contains`/`Expand`/`IntersectsChunk`). `MonumentSuggester`'s
  `ScanBox` is gone; `Suggest`/`Gather`/`Score` take `BlockBox`. `Api.Services.StructureBox` stays separate
  and is not a third copy: exclusive maxes plus `Kind`/`Color` make it a drawing frame, a different
  convention for a different job. `MonumentSuggester.Gather` is bit-identical across the change — the same
  633 candidates over 40 corpus worlds hash to the same digest before and after
  (`tools/objective-probe/gather-digest.cs`), which is what lets a 96.6%-precision detector be re-typed
  without re-validating it.
- **DTC cores are proposed from a world scan; DTM destroyables measurably cannot be (B26).** An imported world
  arrives with no XML, and `CoreSuggester` finds the one signature effectively nothing else in a map produces:
  a lava volume whose every non-lava neighbour is obsidian. Maps are full of both, but a *sealed* container is
  deliberate and the only reason to build one is a core. The rim is the single permitted opening — a minority
  style leaves the lava flush with the casing top, so the cells above it may be all obsidian or all air but
  never a mixture, which is a spill; reading that is worth four points of recall. **267 candidates over 302
  corpus maps, 219 of them a declared core: 82% precision at 77% recall** against 284 declared. What it
  proposes is the structure — casing box, enclosed lava, shell thickness grown layer by layer, float, open-top
  — every parameter read off the geometry rather than defaulted, and never a `<region>`, which is a human's
  loose box (OB12) and is not in the world.
  **The destroyable half is measured, not shipped, and the first measurement of it was wrong.** That pass
  clustered across the four objective materials *pooled*, so a gold block touching obsidian terrain became one
  cluster and every incidental block entered the candidate set — 39,716 of them. Clustering per material, as a
  destroyable is actually built, cuts that to 15,480 and reverses the verdict: **98% of declared structures are
  a standalone connected cluster** (obsidian 298/298, emerald 108/108, gold 40/40, ender stone 65/67), and
  **95% have a same-material same-size partner** at the rotational image of the objective centre — a centre
  recoverable without the XML by letting candidate pairs vote for it. The false positives group cleanly by why
  they are not goals: 4,985 fully buried, 3,632 embedded in terrain, 945 submerged, 573 sprawling, 373 too
  large. Ranking rather than gating puts a declared structure in a map's top 5 for 47.6% of cases at a median
  11 candidates per map. It is not shipped because 34% never reach the candidate set at all, dropped by two
  filters that are known to be too tight (`B58`). One corpus claim was corrected on the way: DT3's "destroyables
  float 3–5 blocks" describes the generator, not the corpus — measured on the declared blocks, 424 of 571 rest
  directly on something.
  **Gathered and stored at ingest**: `CoreSuggester` runs inside the one world pass beside
  `MonumentSuggester.Gather`, and its output lands in `core_candidate` (`M0014`) carrying the casing box and
  every measured parameter — a different shape from `monument_candidate`, which stores evidence for a later
  scoring pass, because a core's signature is unambiguous enough that the row *is* the suggestion. Re-scan is
  delete-then-insert; deleting a map cascades. That storage is what makes the detector usable at all, since the
  `.mca` files are discarded straight after import and cannot be read again.
  Validated from both ends: the corpus for external truth at scale, and a **composed plan for truth by
  construction** — a plan states a core at an anchor, the pipeline builds the world, and the detector must
  propose that core with that casing, shell and cap. (B26, OB12/OB14, `docs/world-scan/objective-suggestion.md`)
- **Island detection tells terrain, markers and erased blocks apart (B31).** A bottom-up scan asks "where does
  the ground start", and a Minecraft world holds three kinds of solid block with only one of them ground. They
  are now separated by three rules, each grounded in a different authority. **Noise** (water, lava, foliage,
  redstone, cobweb, block-36) is never ground anywhere and stays a flat exclusion. **Markers** are ground-shaped
  but exist to be read: PGM's void filter reads `(x, 0, z)` and nothing else, so a sheet at the world floor
  declares a column buildable without being walkable — excluded, but **only at the floor**. **Erasure** is what
  the map itself states vanishes: a hidden destroyable whose mode replaces it with air at `0s`
  (`PhantomErasure`), read from the region and materials rather than inferred, and failing closed on anything it
  cannot state exactly.
  The height bound is the finding. Stained glass sat in the flat noise set, deleted wherever it was a column's
  lowest solid — which erased **30,872 columns of glass sea at y=58 on the_high_seas** and 17,369 at y≈60–77 on
  rock_the_casbah, while the maps whose glass genuinely bridges islands lay it at `y=0`, 100% of the time.
  Scoping to the floor preserves those exactly (newgen, outlyne, rushers_vs_defenders and ad_infinitum do not
  move) and returns terrain to 16 corpus maps carrying glass above it. Measured with `--island-erasure` over 398
  worlds. (B31, OB16, `docs/world-scan/terrain-ground-truth.md`)
- **Water lanes: all four wirings detected, the newest one authored (B28).** A water lane is a gap that
  becomes bridgeable part-way through a match, and the mechanism is PGM's void filter read *live*: a column
  is void iff `(x, 0, z)` is air, evaluated at query time, so anything landing at `y=0` opens the whole
  column from that instant. That single fact is the detector's discriminator — `WaterLaneDetector` accepts
  only a region reaching the void layer, which is what separates a lane from a decorative pool or a
  flag-status indicator no matter what either is called. Four wirings, ranked so one lane reports once:
  the shared `<include id="water-lanes"/>` plus its matching region, a `<fill material="water">` on a
  trigger, a `show="false"` destroyable swapped by a water mode (the phantom B24/B31 made legible), and a
  lane-named region nothing drives — which opens nothing and is reported as its own form rather than
  miscounted as a route. Over 563 corpus maps it finds 19 lanes across 15, including two nobody predicted:
  cannonquad_ii is **blitz** and hearts_of_atlantis is **DTCM**, because a lane is a property of the void
  filter and belongs to no gamemode. Authoring is the include form, which needs no fragment body since the
  server resolves it: a `water-lane` zone kind in the plan compiles to `WaterLaneIntent`, fans by symmetry,
  and emits one `y=0` cuboid union under the agreed id with the include paired to it at export. It is
  deliberately kept **out** of the buildable region — that is what leaves it closed at kickoff — and out of
  every derivation describing the starting board, since a lane is not a route until it opens.
  `--water-lanes` sweeps a corpus; `WL1` lints a lane drawn over terrain. When a lane opens is read from the
  map in every form, the include one included: the fragment declares `water-lane-timer` as an overridable
  `fallback` constant, so a map's own value wins and its absence means the fragment's 45m. (B28,
  `docs/pgm/water-lanes.md`)
- **`<include>` is read, resolved, and the two readings are kept apart (B29).** The element was skipped
  outright, so 198 of 208 corpus `ctw/` maps (95%) were analysed as if rules they pull in did not exist.
  `MapXml.Includes` records every referenced id (so it still round-trips), `MapValidity` warns per
  unresolved one — a warning, never an error, since an unresolved include is a map PGM loads perfectly and
  the studio reads incompletely — and `--includes` reports the corpus histogram. `IncludeLibrary` then
  resolves the bodies from a **configured directory**, which is what PGM itself does
  (`config.getIncludesDirectory()`); nothing is vendored, because the fragments are another project's source
  and a copy would go stale in silence. Resolution is recursive, splices `global` into every map the way PGM
  does, and tolerates a cycle or a missing id by leaving the document as it was.
  **The two readings are deliberately separate**: `Parse(path)` reads the map as written and is what export
  uses; `Parse(path, library)` reads it as played and must never be written back, because the references are
  still emitted and the content would be applied twice. Three seams had to move for it. The supported-range
  gates run **before** the splice — a module arriving from a fragment round-trips through its reference and
  cannot be silently lost, so gating after would have rejected 82 maps that export perfectly. Constants
  survive substitution, because a fragment declares its knobs as `fallback` constants and a map tunes it by
  declaring one it never interpolates. And `<filters>`/`<regions>`/`<kits>` now merge across **every** block
  rather than the first — with `global` always spliced ahead, reading the first block silently dropped the
  map's own, which is the bug `--resolve-includes` was written to catch and did.
  Measured: **367 of 563 maps change** when resolved, filters most (345), then regions and fills (68 each),
  apply rules (55) and kits (28); every corpus id resolves. Two negative results are load-bearing — no map
  gains a gamemode (`<score>`/`<flags>` have no parser: `B56`), and no water-lane verdict changes, because
  that signal is the reference and the region rather than the behaviour behind them. (B29,
  `docs/pgm/include-resolution.md`)
- **The wire is dot-separated on every machine, in every country (B48).** Query, route and form values bind
  through a converter that reads the ambient culture, so on a comma-decimal host `?leader=0.55` arrived as
  fifty-five — a valid number, a hundredfold out, silent, and correct again on the next developer's machine.
  It made the dressing phase's grown-tree wood picker ask for a tree hundreds of blocks tall, which never
  returned; the browser abandoned the request, and the failed fetch escaped an inspector's render path and
  took the whole client down with it, so the export button that "also broke" was pressing on a dead app.
  Three separate guarantees now hold it. `InvariantNumberBinding` registers invariant parsers for every
  numeric type on the model binder, making the wire boundary itself culture-independent rather than leaning
  on ambient state any library may change; the API, importer and round-trip harness each pin the process to
  the invariant culture at startup, as the client already did, so formatting agrees end to end; and a tree's
  and a boulder's knobs are bounded where they are read (`TreeProp.Reach`/`Shape`/`LeafCluster`,
  `BoulderProp.Reach`), because a prop's cost is superlinear in its reach and a value from outside the
  inspector's range is not a strange picture but a build that never finishes. Every send in
  `TerrainLibraryClient` now sits inside its own guard, so a failed preview leaves the picture blank — which
  is what the class always promised and one method did not do. Gated under an explicitly hostile `de-DE`
  culture, moved *after* the host boots so the assertion cannot pass on the startup pin alone.
- **The block palette is metadata-aware and texture-derived, not the in-game map colour (B45).** The surface
  render, the layer overlays, the terrain-paint picker and the monument slice dump all read one table, and it
  was Minecraft's own `MapColor` enum keyed on `(id, -1)`: every wood one tan, every stone variant one grey,
  every dye family one ramp, and any metadata at all ignored. The rewrite splits into three units.
  **`BlockVariants`** answers which nibble bits are a block's sub-type and which are placement state — the
  step that makes an exact match possible at all, since an east-west oak log is `17:8` and a persistent leaf
  block `18:4`; twenty-eight ids carry a mask, and the anvil (damage in the high bits), the quartz pillar
  (three axes, one texture) and the double plant (an upper half that does not record its plant) need
  arithmetic a mask cannot express. **`BlockPaletteData`** is the colour/name catalog for the whole 1.8 range,
  ids 0–197 plus every sub-type within them, expanded from one declaration per family so the six woods reach
  their planks, slabs, saplings, logs, leaves, stairs, fences, gates and doors without restatement — which is
  what had left the spruce door birch-coloured and the acacia door spruce-coloured. Colours are alpha-masked
  texture means taken from the face a top-down render sees, with three deliberate departures: biome-tinted
  blocks carry a fixed temperate tint, ores take their accent rather than the four-fifths stone matrix they
  average to, and **wool, stained glass and stained clay are three ramps** (one dye, three materials) rather
  than the single ramp that printed a brown-clay floor as brown wool. **`BlockPalette`** is the lookup:
  normalize → exact `(id, meta)` → block base → deterministic hash, over flat arrays keyed
  `(blockId << 4) | meta`, so a per-cell lookup is two array reads with no hashing, no tuple key and no
  allocation — hex strings are formatted once and returned interned (`PackedRgb` skips the string entirely),
  which retires the per-request colour cache in `LayerData.Pixels`. Contract:
  `docs/world-scan/block-palette.md`. `tools/palette/texture-average.cs` decodes 1.8 block PNGs with no image
  dependency and dumps/emits/diffs the means the table is authored from (`--check` is non-zero on drift).
  Tested: sub-type distinctness per family, one colour per wood across all nine of its ids, masking of axis /
  decay / half / growth bits, the three dye ramps, base fallback, ore separation, accessor agreement, and
  that no id in 0–197 reaches the unknown-block fallback.
- **Terrain paint is first-class, reusable data — a style/theme library, not just inline blobs (B44).** The two
  things a painter theme decomposes into now have their own tables (`M0011`): a **`style`** row is one reusable
  named material recipe `{name, kind ∈ solid|layered|teamTint|voronoi|noise|wallRun, params_json}` — the nestable
  material subtree stays in the JSON leaf, not over-normalized — and a **`theme`** row (the geometry knobs) plus
  its **`theme_bucket`** bindings `{bucket ∈ rim|surface|wall|fill, style_id, depth, enabled}` compose those styles
  into a full theme. `TerrainThemeComposer` (Minecraft, pure) is the lossless round-trip between a `TerrainTheme`
  and those pieces; `ThemeStore` (Data) persists them, creating a theme with its bindings in one transaction and
  cascading the bindings when a theme is forgotten while the styles survive. The HTTP surface is the full library:
  `styles` CRUD (`GET /api/styles?kind=` is the "show every voronoi" browse), `themes` CRUD, `GET
  /api/themes/{id}/json` reassembles a library theme back into the exact painter JSON, and `POST
  /api/themes/import` lifts a whole theme JSON in as one style per bucket + a theme binding them (400, never 500,
  on malformed JSON). Import-then-compose is byte-for-byte identity, proven end-to-end through the real database.
- **The library is a page you author in, and the sketch draws from it (B44).** `/library` is the studio's
  fourth entry point, on the shape catalog's browse layout (a filter rail, a grid of pictures, a rail for the
  one you picked — now one `lib-*` namespace both pages share). Its **Styles** half filters by kind, starts one
  of each kind from a default that shows what the kind does, edits it with the very `MaterialEditor` the
  sketch's theme phase uses (a style *is* a material — one form, no second way to author one), and saves,
  duplicates or deletes it; deleting one a theme still binds is refused with the names of the themes that would
  break, not a foreign-key error. Its **Themes** half composes: a style per bucket picked from the library, the
  bucket's depth and toggle, the bedrock/rim/wall knobs, and a live preview of what the composition paints
  (`POST /api/themes/preview` composes a draft without saving any of it). A bucket left unbound keeps the
  built-in finish, so a theme overrides only what it changes — bind a rim and a fill once, vary the surface and
  the wall. Bound and switched **off** are different answers and either is given without the other: a theme
  needs neither a rim nor a wall — surface over fill is a whole finish — so the toggle is offered whether or not
  a style is bound, and a styleless refusal persists as a binding of its own (`theme_bucket.style_id` is
  nullable, `M0013`). Only an unbound bucket that still paints is dropped on save, because that one really does
  say nothing. The sketch's Theme phase bridges both ways: copy a library theme into the sketch (a snapshot —
  editing it there leaves the library's copy alone) or save the open one back out as one style per bucket.
  `PUT /api/themes/{id}` replaces a theme's knobs and its whole set of bindings in one transaction.
- **Every library row carries its own picture, and a layer stack finally has one (B44).** A named JSON blob is
  not browsable, so a style ships the view that shows its kind something: patterns and tints read from above,
  and a **layer stack reads as a section** — sampled one course from above it was one flat colour, which is why
  it had no preview at all. A theme's picture is a sample plateau painted with it and cut open, classified by
  the real `TerrainProfile` and painted by `TerrainPainter.ColumnBlocks`, so rim depth, a switched-off wall and
  the bedrock floor move the picture exactly as they move the world. `SvgRaster` keeps them small enough to
  travel with the row — one rect per *rectangle* of one colour, merged along a row then down the rows that
  repeat it, so a solid swatch is one rect rather than 576 (the plan overlay render takes the same win).
- **A block the paint shortlist omits is typed, not unreachable (B44).** `BlockPalette` now names and colours
  the numeric format's data-variant blocks — andesite is stone `1:5`, and granite, diorite, podzol, red sand,
  the plank species, the stone-brick and quartz and prismarine variants are all their own entries — and
  `TerrainPalette` offers each beside the block it shares an id with. The picker tells the two meanings of a
  data value apart: a sixteen-shade family collapses to one line plus a colour row, a variant is listed under
  its own name, and the `(id, data)` pair underneath is editable for anything the shortlist still omits.
- **A plan must carry a map before it can become one — the compile gate asks two questions, not one (B38, B39).**
  `PlanValidator` only ever asked whether what a plan *said* was coherent, so a plan that said **nothing**
  passed: an empty document compiled `200` into an empty layout and a spawn-less intent — a map that cannot
  exist — and stripping the spawns out of a real board compiled just as happily. The missing question was
  completeness, and it is now `PlanValidator.Completeness`, run by `/plan/compile` alongside `Validate`:
  **no generating piece** ("there is no land to build") and **no spawn** ("nowhere to put a player") are
  errors that block the compile with the same `422 + findings` shape as a structural break; **no objective
  at all** — no wool, destroyable or core — is a *complaint*, since the goal is the author's and can still
  be set when the map is configured. A blank document reports only the missing land, because the other two
  are consequences of it.
  - **Deliberately not part of `Validate`.** That runs continuously — on every keystroke in the editor and
    on every candidate the composer scores, where a half-built plan is normal — and its errors feed the
    evaluator's hard `STRUCT` term. Folding completeness in there made the evaluator reject every mid-search
    candidate that had not placed its spawns yet; the three unit tests that caught it are the reason the two
    passes are separate.
  - **The complaint is not swallowed.** A compile that succeeds with an unmet complaint returns it as
    `warnings` on the `200`, and the plan tool's compile drawer shows it above the compiled JSON — so a
    goalless map says so instead of passing in silence.
  - **`/plan/evaluate` no longer calls an empty plan invalid** (B39). It answered `400 "Invalid plan
    structure"` on the bare `/plan-editor` document while `/plan/inspect` answered `200` on the same body,
    which showed as a fresh plan with a blank evaluator panel and no reason. Nothing is scorable without
    pieces, so it now answers `Evaluation.Empty` — the honest empty result rather than a thrown-and-caught
    "invalid". The e2e allowance for it is gone.
- **A joint records a `Grant`, not an `Offer`** — the two carry different quantities and the docs said
  otherwise. A host publishes an **offer** per free run whose `WidthClass` is a *capacity* derived from that
  run's length (`HubBoxEmitter`); a joint records a **grant**, whose width is a *selection* made per consumer
  kind (a wool takes the w2 wool lane, a spawn or frontline the map lane width). One run can carry two docks at
  two widths, so a grant was never the offer travelling forward — but `BoxJoint`'s docstring claimed the offer
  "rides along as provenance" and that `BoxPartition.Of` "mirrors it back", which is what made the two read as
  one thing. `BoxJoint.Offer` → `BoxJoint.Grant`, with `BoxJoint`'s and `EdgeOffer`'s docstrings rewritten to
  state the capacity/selection split; `HubBoxEmitter`'s docstring already had it right and is unchanged in
  substance. Pure rename plus comments — 761/761 Pgm tests, fingerprints untouched. The open question of
  whether the emitted capacity *should* bound the grant is deliberately not answered here (`B41`). (B41)
- **A plan rect is a type now — `CellRect` in `PgmStudio.Geom`.** `Box.Rect`, `PlanPiece.Rect`,
  `PlanZone.Rect`/`.Holes`, `ShapeVacancy.Rect`, `NegativeSpacePart.Rect`, `GrownPiece`, `MidStone`,
  `Vacancy`, `EvidenceRect` and the shape emitters all meant `[x, z, w, h]` in plan cells, enforced by
  nothing: `Rect[2]` was width by agreement, a three-element array compiled, and reading `[3]` as a depth
  compiled too. The convention is now **origin + exclusive extent** (`X`/`Z` + `Width`/`Height`, with
  computed `MaxX`/`MaxZ`), so the rotations and mirrors that used to read `[dim - r[1] - r[3], r[0], r[3],
  r[2]]` now say what they do. **Named `CellRect`, not `Rect`** — `Rect` is taken in the same project
  (`Authoring/MapIntent.cs`) and is the opposite convention throughout: world **blocks**, fractional,
  corner-pair. That split is the point: `Rect` = world blocks, `CellRect` = plan cells.
  **Placed in `Geom`**, the dependency-free leaf, so `Cells.BoundingBox` could return it instead of a bare
  inclusive corner tuple — collapsing the two 2-D conventions rather than leaving them adjacent. (Its extent
  counts both bounding cells, so `MaxX` is one past the far cell; the handful of call sites written in the
  inclusive convention bind their far corners explicitly.) First public value type in `Geom`.
  **`plan.json` is byte-for-byte unchanged**: a `CellRectJsonConverter` on the type keeps the four-element
  array, so every consumer agrees on the wire form by default, and the API DTOs that cross to the Blazor
  client still hand out `int[]`. **Oracle: the composer fingerprints did not move** —
  `tools/compose/composer-fingerprints.json` is untouched and `ComposerVersionTests` passes, which is what
  proves a pure representation change. 68 files; Pgm 751/751, Geom 66/66, Analysis 50/50, Domain 17/17,
  Minecraft 70/70. (B37)
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
- **The corpus regression net replaced the last outside oracle (B43).** The four analysis comparisons —
  region categories, buildability, traversability, wool availability — read a reference app's per-map JSON out
  of `/tmp/pyfresh` and diffed the C# derivation against it. They were green, and that was the problem: a live
  derivation pinned to a frozen copy makes every safe refactor look risky, and the copy could not be
  regenerated once the reference was deprecated. **`--goldens [featureRoot] [--update]`** keeps what they were
  for and drops what they depended on: it runs the same four derivations over every corpus map from the
  studio's own parse, and compares each against `tools/PgmStudio.RoundTrip/corpus-goldens.json` — **370 maps,
  1402 derivations**, 128 KB. Each entry reads `<digest> <summary>`, so a `git diff` of the record says what
  moved (`connected=True components=1 points=8` → `components=3`) before anyone re-runs anything, and a moved
  verdict names the map and the derivation and exits non-zero. Buildability's verdict grid is hashed per cell
  rather than summarized, since a per-cell change is exactly what no count would show. The record is meant to
  be re-recorded when a change is deliberate — it buys the look, not a veto. `--authoring` went with them: its
  `authoring_oracle.json` exists nowhere on disk, so it had been running over zero maps; `--authoring-fixture`
  is the review artifact that survives it. Also swept: every "port of X.py" attribution in `src/` and
  `tests/`, and the parity paragraph in `CLAUDE.md`. (`tools/PgmStudio.RoundTrip/CorpusGoldens.cs`)
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
  Settles `D3`. (NS)

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
- **Top-down world render (`--topdown <regionDir> <out.png>`)** — a world's surface as a PNG, one pixel
  block per column, coloured by the same `BlockPalette` table the layer view and the terrain picker read, so
  the image and the editor cannot disagree about what a block looks like. Relief comes from the step to the
  column one row north rather than from absolute height, which marks every wall, trench and plateau lip while
  leaving flat ground its own material colour; a flooded column is traced down to its bed and dimmed by the
  depth standing over it, so a shoreline reads. `--map <map.xml>` overlays what the XML declares — objective
  destroyables and cores in their owner's team colour, apply-rule regions outlined — against the terrain that
  is actually there; `--scale N` sets pixels per block (nearest-neighbour, so a block stays a square) and
  `--ymax Y` caps the scan below a ceiling structure. `PngWriter` emits the file directly (Up-filtered
  scanlines through `ZLibStream`), so the harness gains no imaging dependency. (B60)
- **Underground render (`--underground <regionDir> <out.png>`)** — the world below its own roof, which is the
  only view in which a cave system, a ravine or a mineshaft has a shape. The roof is found **per column**, not
  as a cutting plane: a plane slices a hillside and reports its solid interior as cavity while missing a tunnel
  under a valley, so each column takes its own highest terrain block — vegetation and the tunnel's own torches
  and rails do not count as terrain, or the furniture would raise the roof above the space it stands in — and
  only what lies strictly below is enclosed. An enclosed column is shaded by the height of its topmost void, so
  corridors at one level read as one tone, and brightened logarithmically by how much open space it holds, so a
  ravine separates from a crawlspace without swallowing the brightness range. Rails, supports, cobweb, torches,
  chests and spawners are painted over that in saturated colours no terrain shade reaches. `--band <yMin> <yMax>`
  narrows the scan to one level; `--ores` adds ore bodies (off by default — coal alone speckles the image).
  Shares `Raster` and `PngWriter` with the surface render. (B61)
- **Height profile (`--heightmap <regionDir> <out.png>`)** — terrain shape with nothing coloured by what the
  ground is made of, which is the view a layout is judged in: where the high ground is, how a valley runs,
  whether a route climbs. **Ground is not the topmost block** — a tree is seven blocks standing on flat grass,
  so vegetation, trunks and surface furniture are skipped, and so are liquids, because a river read at its
  waterline flattens the channel that gives the valley its shape. Three layers carry it: a hypsometric ramp for
  altitude, hillshade from a north-west sun computed on a **smoothed** copy of the grid (raw block heights step
  by whole blocks, so an unsmoothed gradient calls every one-block lip a cliff and the image turns to noise)
  while colour and contours stay on exact heights, and contour lines every `--contour N` blocks (auto: ~12
  bands; every fifth emphasised) because shading shows that a slope exists and only a contour says how much it
  climbs. `--grey` drops the ramp to greyscale, `--water` tints the flooded columns, and the console reports the
  height histogram the picture cannot state. (B62)
- **Built-structure extraction (`--structures <regionDir> <out.png>`)** — every building on a world, found and
  drawn over the terrain it stands on. **Material, not elevation, is the test**: height cannot separate a hut
  from the boulder beside it, and a hall on level ground has no elevation signature at all, but every building
  has a surface the world does not generate. Each column is classified by its top block once vegetation is
  stripped; built columns join into 8-neighbour components, one per candidate. Stone, cobblestone, andesite,
  gravel, sand and clay are deliberately **not** built materials — all generate in the open, and including them
  classifies every outcrop as architecture. Height is measured against a ring of natural columns just outside
  the component, since a building hides its own ground; it is reported, never filtered on, so a paved path
  reads as a real find that simply stands 0 blocks tall. `--min-area N` sets the size floor. Each structure also
  reports **rough** (how uneven the ring's ground is) and **seat** (its lowest built block against that ground —
  0 flush, positive floating, negative dug in). Those two describe placement; they do **not** score it, and must
  not be read as a quality measure: a map on a mountain has steep ground under every building by construction,
  so alpine_mining_ii reads as 44 structures on 3+ uneven ground while a flat map reads as clean. Validated on
  alpine_mining_ii: 107 structures, resolving into exact mirrored pairs about the map's rot-180 centre. (B63)
- **Trees vs timber, and paved routes (`--flora <regionDir> <out.png> --path <id[:data],…>`)** — separates a
  world's planting from the wood built into its structures, and traces its roads. **Log orientation cannot make
  that split**: an all-bark log is the natural trunk, but a tree's branches are ordinary rotated logs growing
  out of one, so the per-block rule discards a large part of every branched conifer. Connectivity decides
  instead — a log component is a tree if it holds an all-bark log *or* touches leaves, and branches arrive with
  their trunk because they are attached to it. **The trunk marker is read off the world, not assumed**: all-bark facing is the sharper
  test wherever an author uses it, since a branch leaving a trunk is rotated and drops out on its own, but a
  world that never uses it yields nothing under that test and falls back to upright logs. The choice is made on
  **outcome, not proportion** — a world with six trees and four hundred structural logs uses all-bark for every
  trunk it has while all-bark is a few per cent of its wood, so a share threshold reads it backwards; asking the
  sharper test first and falling back only when it finds no stems needs no threshold at all.
  Each tree's **canopy width** is measured by giving every leaf to its nearest
  trunk, because a fixed radius around a trunk reports the radius rather than the crown; the same assignment
  keeps neighbours apart, since two canopies standing close without overlapping leave every leaf unambiguously
  nearer one trunk, and where they do overlap the boundary falls midway and understates both rather than
  merging them. **Species is read from the canopy, not the trunk**, since an
  author may pair any wood with any leaf (alpine_mining_ii's pines are acacia trunks under birch leaves), and
  the trunk material is recorded rather than trusted. **A tree is a rooted all-bark run, not a connected lump
  of wood**: counting components shatters a detailed oak whose limbs meet its trunk diagonally and fuses
  neighbours whose canopies touch, while counting every log resting on the ground counts each drooping branch
  that reaches it — so a stem is a log on non-wood carrying an unbroken all-bark column above it, and stems
  sharing a footprint are one tree (a 2×2 oak base counts once), and the run must be **one wood**: a column
  changing species partway up is a shaft faced in bark, which is the only signal that separates a mineshaft
  entrance tower from the trees it stands among, since such a tower borrows their canopy. The **trunk×canopy
  pairing** is reported
  because that pairing is what a tree type is; a wood appearing under several canopies is a post that rooted
  beside foliage, which rarity alone cannot tell you — a map may plant only two oaks. The path palette is a
  **required argument**, never a constant:
  the same block is a road on one map and bulk terrain on another. Paving is traced as connected surface
  components and the gap between the largest ones is reported, because a route can be continuous underfoot and
  still break in material where it crosses a bridge, a ford or a floor. `--bridge` names materials that seed a
  crossing where the column **stands over water** — the same stone brick floors a building, but no floor has
  open water beneath it — and each seed then grows to its whole connected run, so the abutments on dry land
  come with the span instead of being cut off at the shoreline. Validated against a hand-built map's own counts: 2 oak, ~46 spruce and ~51 pine per side, recovered as
  **2.0 / 47.5 / 54.5** with every trunk wood 100% pure, and its paving resolving into one connected network
  per team. (B64)
- **Buildings from their roofs (`--buildings <regionDir> <out.png> --roof <id[:data],…>`)** — a roof is the
  only part of a building always visible from above, always made of something terrain never uses, and always
  continuous; walls are hidden under it and a footprint drawn from anything else guesses where a structure
  ends. **Standing clear of the terrain is the one gate**, because plank laid on the ground is a deck or a
  floor and nothing else separates it from a roof: same material, same connectivity, both fully grounded, but
  a roof has air under it. Clearance is measured against the terrain **directly beneath the
  footprint**, never a ring outside it — on a slope the ring sits well below, which lets plank worked flat into
  a hillside as texture read as a roof standing clear of nothing. Components are grown in plan but **bounded in height** (`--max-step`,
  default 4): a roof is a surface, so neighbouring columns join only if their tops are within a step — a pitch
  rises a block at a time and a tier change a few, while plank lying on the ground beside a shaft head sits ten
  or more below the roof it touches, and joining on plan adjacency alone welds that floor to the roof and
  returns a component that is neither. A component smaller than `--min-side` (default 6) is dropped: the smallest thing a map builds is still a room, and below that a
  component is a cap, a marker or a fragment of decking on which no measure means anything. **`--rim` names the
  second wood a roof is bordered with, and it is the discriminator that works** — a house roof is a fill
  enclosed by a border, so a cap that is one material all the way across was never a house roof however well it
  is framed or walled. Grounded share, corner-stem count and wall share between the posts are all reported and
  none gates; on the map this was built for the wall share is *higher* on the shaft heads than on the houses,
  so it cannot separate them and does not try. The ground it is measured against is taken **near the top of the
  terrain spread under the footprint, not at its middle**: a column standing over an open shaft finds no
  terrain until the shaft's floor and contributes a depth belonging to the hole rather than to the ground, so
  the low tail is exactly those columns and the bulk is the surface. Clearance is measured **at the ridge, not
  the eave** — a
  pitched roof comes down to meet its walls, so its lowest course sits as close to the ground as decking does
  and testing there discards the houses along with it. A slab's high data bit is masked before matching, since
  it records which half of a block the slab fills rather than what it is made of, and glass joins the stepped-
  past cover because smoke drawn over a chimney otherwise hides the roof it rises from. **A roof also has a room under it**: the grounded share is bounded above as well as
  below, because a floor reaches the terrain under almost every column while a building grounds only at its
  walls and is hollow between them — the two populations do not overlap, 15–31% for every house against
  93–100% for every plank floor, so the threshold's exact value carries no weight. Validated against a map author's own
  inventory: **11 of 11 houses and 5 of 5 shafts** located per team, with **no false positives** — 34
  components resolving as 23 rim-bordered house roofs (22 houses plus the observer island's one) and 11 caps
  (5 shafts per team plus the island's), the caps uniform at 6×6 and one material throughout. (B65)
- **One block vocabulary for every pass (`BlockRoles`)** — what a block is *doing* in a world (air, liquid,
  tree, flora, decoration, material), stated once in `PgmStudio.Minecraft` beside `TerrainPalette` and
  `DressingPalette`. It replaces **eight** per-render lists, each written as a negative ("what must I step
  past") and local to one question, which agreed wherever a map had tested them and diverged wherever none had:
  the wooden fences were a build material to one pass and absent from four, the rails were in five lists, two or
  four depending on which rail, and **fifteen blocks — mob heads, anvils, daylight sensors, spawners, banners,
  flower pots, cactus, nether brick fence — were in no list at all** and so were read as terrain by every pass
  that met them. Each pass now composes the roles it needs instead of owning an answer. Two composites carry the
  distinctions the old lists encoded by accident: `StandsOnGround` (tree · flora · decoration) for a pass reading
  the ground, and **`SeenThrough`, which excludes logs**, for the three passes reading the shape of a build — an
  author stands a log at each corner of a house as a post, and a roof finder that looks through them loses the
  corner-stem measure. Roles are placement, not material: a carpet and a snow layer are decoration though one is
  wool and the other is snow, because an author lays them on finished ground; full-cube furniture (furnace,
  crafting table, bookshelf) stays material, since the ground vocabulary already names full cubes. Verified on
  alpine_mining_ii — buildings still 34 components split **23 house roofs / 11 caps** with 4 corner stems, trees
  still **109 acacia-birch / 95 spruce / 4 oak** at canopy widths 8/7/13 — and the point of the change shows in
  the residue: unnamed ground fell from 0.4% to **0.2%**, the mob heads and daylight sensors leaving it for the
  decoration layer where they belong.
  <br>**Fullness is a separate axis** (`IsFullCube`) — shape, not role, since a dropper is decoration and fills
  its cube while a stone slab is a material and does not, and neither answer can be read off the other. It
  states what the ground vocabulary means by naming only full blocks, which was previously enforced by
  hand-picking ids and re-derived a second time as a slab-bit mask in the roof finder. The surface report now
  separates **partial block** from **unnamed**: a stair on the surface is not a gap in the vocabulary but a
  thing standing on ground, while unnamed narrows to a full block no family covers. On alpine_mining_ii that
  splits the last 0.2% into 28 columns of cobblestone stairs and **35 of iron block, gold block and gold ore —
  every one a resource deposit reaching the surface**, which the resource pass names. Nothing is left
  unexplained. Gated by ten role tests, including the two that tie the vocabularies together: every block a
  tone family names fills its cube, and none of them stands on the ground. (B68)
- **Resource deposits (`--resources <regionDir> <out.png>`)** — what a map gives a player to take and what it
  charges for it. **A deposit is the placement, not the block**: a seam of two hundred iron is one decision an
  author made once, so lumps are 6-connected per material and every measure is per lump — two maps with the same
  block count can be a hundred scattered pockets or two slabs, and only the lump count tells them apart. **Cost
  is cover, not depth**: a y tells you where a deposit sits and nothing about reaching it (the same y is bedrock
  under a mountain and open ground in a valley), so the measure is the solid blocks standing over it counted up
  its own column, charged at its *shallowest* block, which makes a cave above it free. **Openness is asked of the
  faces, not of the sky**: a deposit in a cave wall is visible to anyone walking past under a hundred blocks of
  mountain, so it is the share of a lump's six-neighbour faces meeting air, with sky exposure kept apart.
  **Balance is counted per block across the middle of the world's longer axis** — per deposit invents an
  imbalance out of one seam lying across the line. Solid mineral blocks and ores are separate classes, since a
  block is nine ingots and an ore is one; the ore's two readings stay intact (grey stone to the ground
  vocabulary, which is how it looks; a deposit here, which is what it is for). Measured: alpine_mining_ii pays
  **10,145 mineral blocks in 176 deposits** at median cover 13–15 plus 55,695 ore, cedar_crossing pays **450 iron
  in 2 seams** at cover 0, and townside_mini **206 iron in 8 lumps** fully enclosed — three different offers, and
  the deposit/cover pair is what separates them. (B67)
- **Ground materials, decoration and beds (`--surface <regionDir> <out.png>`)** — what a map's ground is made
  of once what stands on it is set aside. Decoration is read as a **layer**: a fern is not what the ground is
  made of, but which soil an author ferns is a decision worth recovering, so a column reports its material and
  its planting separately. The same column shape answers the riverbed — water is stepped through to the first
  solid block and the depth kept, so a bed reads as its own material instead of a sheet of blue. **Canopy is
  shade, not structure**: the ground under a wood is ground, and counting it as built drops a quarter of a
  forested map from the sample and cuts every material into fragments wherever trees stand, which then reads as
  speckle. **Patchiness is measured, not assumed** — a histogram cannot tell a scattered material from one laid
  in fields, so each is scored by how often it neighbours its own kind against how often it would by chance,
  with patch counts and median size giving the scale. Materials are also grouped into the **19 tone families over 72 full blocks**
  of `TerrainPalette` (TP16), which is also the paint picker's own offer list — the
  vocabulary an author actually reaches for, "the green", "the earth", "the grey stone" — because a per-block histogram splits one
  decision across four rows and reports the variation inside a field as though it were the field. Only **full cubes** are named, since the tones are a vocabulary for
  building ground and a slab, stair, fence or flower stands on ground rather than being it — a double slab is a
  full block and belongs, its single-slab sibling does not, and anything partial found on the surface reports as
  unnamed, which is the honest answer. Unlike a path or roof palette that table is a property of the blocks and
  not of a map (granite reads warm everywhere), so it is a default rather than an argument. Measured on alpine_mining_ii: every material 2.5×–24× self-adjacent, nothing random; and by tone the
  warm family resolves from 423 scattered material patches into **33 fields, the largest 1,821 cells** — the
  road network, recovered without being told its palette. (B66)
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
landed**, with the per-phase bodies the open work (TODO §Authoring). Contract: `docs/pgm/new-map-authoring.md`.
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
  reused edit-page `WorldCanvas` (its navigation toolbar — pan/zoom · fit island · reset — and its island
  base ↔ surface "Blocks" layer toggle), with a cleaned-base summary (the corpus-fixed noise exclusions)
  and a detection summary (layer · island count · detected symmetry). Writes no intent. (`WorldScanStep`; N01)
- **World · Islands sub-step (N01)** — review the detected islands and exclude the stray ones (decor /
  observer towers). Islands are selectable from the list **or by clicking the canvas** (the `WorldCanvas`
  gained island hit-testing + an accent-border highlight, gated so the editor's region selection is
  unchanged); the inspector shows centre / block count / Exclude·Include. Excluding reuses
  `PATCH /configure/{slug}/exclude-island` (re-runs symmetry, no re-scan) and dims the island; saves
  instantly (topbar Saving… → Saved). (`WorldIslandsStep`; N01)
- **World · Symmetry sub-step (N01)** — confirm the detected symmetry (or pick another / none) + its
  centre → the World intent slice (`intent.symmetry`), which the generator orbit-fills from. The canvas
  (`WorldCanvas` symmetry mode — base layer only) draws the axis/centre overlay; the inspector surfaces the
  suggested team count. Persists on phase-advance, which marks World done + unlocks Teams. (`WorldSymmetryStep`; N01)
- **Teams · step 1 sub-step (N02, "Teams & island assignment")** — create the teams (a Smart Suggestion
  proposes the count from the confirmed symmetry → palette teams) + edit name/colour + Max Players →
  `intent.teams` / `maxPlayers`; and tag islands to teams by clicking them on the canvas (tinted that
  team's colour) → `intent.islandTeams` (authoring aid the Spawn step consumes). Canvas = reused
  `WorldCanvas` in island-select mode, now **point-in-polygon** island hit-testing + **Select tool by
  default** (both also improve the World · Islands step). (`TeamAssignStep`; N02)
- **Teams · Spawn point sub-step (N02)** — the **point tool** drops team 0's spawn (island-aware: it
  takes the clicked island's team) and the confirmed symmetry orbit-fills the rest, each orbit spawn
  reassigned by the island it lands in; the **select tool** picks a placed marker (world-space hit-test,
  like the editor's). The inspector edits X/Y/Z/Yaw — editing the authored spawn's X/Z re-derives the
  orbit; the reused **side-view** (`SliceView`) sets the Y on the spawn's terrain, **shared across the
  orbit**. The **observer (`<default>`) spawn** is shown + editable with the same treatment (a neutral
  marker, the select tool, the inspector X/Y/Z/Yaw, and the side-view Y-snap) — defaulted to the map
  middle so observers don't fall in at 0,0,0; with it selected the point tool relocates it (no orbit).
  **Yaw auto-aims**: team spawns look at the map middle, the observer at a team spawn (`Geom.Heading`),
  recomputed on any move, manual edits stick. → `intent.spawns` + `intent.observer`. (`SpawnStep`; N02)
- **Teams · Spawn protection sub-step (N02)** — the **rectangle tool** draws a protection zone over a
  spawn; it's **owned by the team whose spawn it covers** and the confirmed symmetry orbits it onto the
  rest, each copy **owned by the team whose spawn IT covers** (shared `OrbitAssignment.ByCoveredAnchor`
  — spatial containment, never orbit order, so no spawn lands in an enemy's zone). Zones are **dummy
  regions** on the reused canvas; the authored zone is editable, the **orbit copies are non-editable ghost
  previews** (one-way derivation). Edits route to `intent.spawns[].protection`; the inspector shows the
  generator's **Auto-wiring (derived)** (`enter=only-<team>` + `block=never`). (`ProtectionStep`; N02)
- **Build · Build-height sub-step (N03)** — the max-build-height cap, set with the **shared
  `BuildHeightSideview`** — the Edit Build Regions step-1 side-view (`studio.mountSideview` / `SliceView`,
  axis toggle + draggable line) **extracted into one component used by both surfaces**, so they're
  identical. Number input ↔ canvas line stay in sync; → `intent.build.maxHeight`. (`BuildHeightStep`; N03)
- **Build · Buildable-layer sub-step (N03)** — the **rectangle tool** draws over-void bridges (areas) and
  no-build holes (the negative-rectangle / complement case); a Bridge/Hole toggle picks which. Build areas
  have no team identity, so it stores **authored-only** (`intent.build.areas`/`holes`) and the **canvas**
  renders the symmetry mirror as ghost previews in JS (`setAuthorMirror`); `BuildGenerator` orbits + unions
  them, complements the holes, and wraps the void-enforcement negative. (`BuildLayerStep`; N03)
- **Void enforcement, stated independently of a build area (B132).** `BuildIntent.VoidEnforcement` wires the
  corpus's `alpine_mining_ii` idiom — `block-place="deny(void)"` over `everywhere` minus its stated
  `Exclusions` — whether or not `BuildIntent.Areas` is declared, fixing `BuildGenerator.Apply`'s early return
  at `Areas.Count == 0`, which used to skip the void-enforcement wiring entirely on any map with no build
  rectangle and left every void on it bridgeable from the first minute. The default (null) is unchanged and
  stays permissive, matching every map exported before this landed; measured over the 112 `dtcm` corpus maps,
  100/112 restrict building somewhere but not all through the void (68 via `deny(void)`/`no-void`, 82 via a
  hard `never` region), so the studio does not guess which an author wants. `SymmetryExpander.OrbitBuild`
  orbits `Exclusions` alongside `Areas`/`Holes` (`docs/pgm/new-map-authoring.md` §5b).
- **Build · live buildability overlay (N03)** — a **Buildable** chip on the canvas sub-bar toggles a
  translucent per-column **verdict heatmap** (`GET /buildability`): green buildable · orange void-denied ·
  red never · yellow restricted. Reuses the block-overlay's pixelated `<image>` renderer (the grid → one
  PNG), sits below the authored bridges, and re-fetches on each toggle-on so it reflects the saved build
  slice. A sidebar **legend** (colour → plain-language meaning + what to do) shows while the overlay is on
  (`OnBuildableToggled`). (`WorldCanvas` `ShowBuildable` + `setBuildability`; `BuildLayerStep`; N03)
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
  (`WoolObjectivesStep`; `WoolAuthoring` shared helper; `ColumnFloorEndpoint`; N04)
- **Wools · Spawn sub-step (N04)** — confirm/adjust each wool's source point (seeded by the detected
  cluster centroid) + set its Y on the reused side-view; positions **orbit** like the team-spawn step
  (editing an anchor-team wool re-derives its mirror partners by mirrored position — colour/owner untouched,
  so green's mirror stays the real yellow). (`WoolSpawnStep`; N04)
- **Spawns seat on terrain (N11)** — a spawn placed with the **point tool** lands on the column's floor
  instead of Y 0: team spawns + their orbit copies, the observer, and wool spawns all route through one
  `ColumnFloor` helper, which owns the +1 (`column-floor` reports the topmost solid block *inclusive*, so
  resting on it is floor + 1). A wool anchors the search at its **own level**, since it usually sits in a
  covered room whose roof would otherwise be the column's topmost surface. The side-view Y line **snaps to
  the floors of the marker's column** (`seatOnFloor`, opt-in via `SliceView.SeatOnFloor`) so it can't be
  dragged into a block or mid-air — a vertical run offers each of its floors; a region's Y stays free. The
  slice line tracks a Y that changes on its own, without refetching the depth map.
  (`ColumnFloor`, `SpawnStep`, `WoolSpawnStep`, `WoolObjectivesStep`, `SliceView`, `sideview-canvas.js`; N11)
- **A Configure step keeps the keys it does not model (N13).** The spawn, protection and wool steps each
  rebuilt their intent entry from the handful of fields they edit, which deleted everything else on it. What
  else was on it was the plan compiler's: a spawn's `piece` and `iron`, a wool's `piece` and `entries` — the
  rects that size the stamped room, place its renewable ore and cut the cage's doors. Touching one control in
  Configure was enough, and the loss was invisible until the export, where a 25×15 authored spawn came back
  as the marker-anchored **8×8** default with no ore beside it at all. Each step now rewrites the entry it is
  replacing and overwrites only its own fields (`IntentSlice.Carrier`, matched on team for a spawn and on
  colour for a wool), which also retires the hand-written `protection` carry-through that patched the same
  hole one field at a time. **And the ore itself now places:** a marker on a grid line in one axis and a
  block centre in the other centres no square cube, and `RoomFrames.PlaceIron` refused it outright — so three
  iron markers down one side of a spawn piece, on an odd cell grid, produced three lints and nothing in the
  world. Such a marker now takes the whole size ladder (4·3·2) and settles on the nearest block lattice, half
  a block off centre on the odd axis, placed by away-from-zero rounding so the orbit image covers the images
  of the same cells (WX8/WX9, `structures.md`). Verified on a 25×15 spawn piece: a 16×13 shell — the piece
  minus the strip it yields — and all three 4×4 cubes stamped, mirror-consistently. (N13)
- **Wools · Monuments sub-step (N04)** — each wool needs **N−1** monuments (one per enemy team), modelled
  as the expected capturers; the scan pre-fills the signed pedestals. **Box** a cluster → `monument-suggestions`
  routes each hit to its colour's wool (capturing team = its island); an empty box drops a manual monument;
  one-click whole-map **Detect**. Capturing team editable per row. (`WoolMonumentsStep`; N04)
- **Wools · Room sub-step (N04)** — the **rectangle tool** draws a wool room, owned by the wool whose spawn
  it covers; the symmetry orbits it to the partner wools via the shared **`OrbitAssignment.ByCoveredAnchor`**
  (anchors = the wool spawns), accumulating across wools so a team that defends several wools gets each room
  (authored editable, orbit copies ghost). Shows the generator's **Auto-wiring (derived)** preview
  (`enter`/`block`=`not-<owner>` + `capture ×N`). (`WoolRoomStep`; N04)
- **Orbit-fill stopped deleting the slices it had never been taught about (`B53`, `G160`).** `SymmetryExpander`
  rebuilt the intent by naming its fields, so the four added to `MapIntent` after it was written —
  `Destroyables`, `Cores`, `IslandTeams`, `Structures` — were **dropped on every intent carrying a
  symmetry**. A map configured with a core exported with no core, and nothing said so; only the plan path
  escaped, because a compiled intent leaves `Symmetry` null and the expander returns early. Every intent
  slice is now a record and the expansion is `intent with { … }`, so carrying is the default and a transform
  is the thing that must be spelled out. Destroyables and cores also **orbit** now, the wool fill without a
  colour or per-capturing-team monuments: a goal authored once is the same goal for the other team, casing
  knobs and all, with its resolved box mapped so the region still scopes the mirrored structure (OB8). The
  regression test asks the *type* rather than a list of names — a slice nobody teaches the expander about
  fails there instead of vanishing from someone's map. `new-map-authoring.md` §4 now states which tier
  orbits what and why there is more than one.
  The hole was filed as `B53`, which judged it unreachable — "the two producers do not overlap: Configure
  sets `Symmetry` but authors none of the four, and the plan compiler fills all four but deliberately sets
  no `Symmetry`" — and named that a coincidence rather than a design. It was: giving Configure a Cores phase
  (`N12`) made Configure a producer of one of the four, and the coincidence stopped holding in the same
  commit that relied on it. (`SymmetryExpanderCarryTests`; B53 · G160)
- **A goal cannot be placed where the map's own rules would make it unbreakable (`OB17`).** A destroyable
  and a core sit more freely than a wool — no room, no per-team monument — so what bounds them is where they
  would stop working, and there are exactly three such places. **Over the void**, the build slice's
  `block_place=deny(void)` means the blocks cannot be broken. **Inside a spawn**, protection emits
  `block="never"` over the spawns union, which denies *everyone* including the attacking team: a map that
  cannot be won, and nothing downstream says so — PGM loads it and the round never ends. **Inside a wool
  room**, the room's own rules cover the goal. `PlanValidator` refuses all three as compile-blocking errors,
  so an agent driving the endpoint is stopped rather than shipping an unwinnable map (`B21`).
  Each is decided by the structure's **footprint**, not its marker — a marker legally inside its piece can
  still put a 5×5 casing two columns past the edge — and the footprint comes from `ObjectiveFootprint` in
  `Domain`, below both the plan layer and the world stamper, which now derive it from the one table. A
  validator sizing structures its own way would pass a plan the stamper then builds one block wider. The
  void test reads every block of the footprint against **all** pieces, so land assembled from abutting
  pieces counts as one surface; the room tests read the resolved **frame** rather than the piece holding it,
  since a spawn piece is often far larger than its room and refusing a goal at the far corner would be a
  refusal with no cause. (`ObjectiveFootprint`; `PlanValidator`; `PlanValidatorObjectivePlacementTests`
  covers each case firing, the one-block structure that legally stands where a wide one may not, and the
  straddle of two abutting pieces. OB17 · B37)
- **A refusal points at what it is about (`C44`).** A compile finding names its subjects and the canvas can
  pulse them, and for as long as both were true nothing joined them: the findings rendered as static divs,
  `PlanTool` parsed each finding's `subjects` and dropped the field, and `highlightSubjects` sat on the
  bridge with no caller. An author told a core overhangs the void had to find that core by eye. A finding row
  is now a button that pulses its subjects — and only when it has some, since a rule about the plan as a
  whole has nothing to point at and an inert row is more honest than a click that does nothing. Clicking
  **closes the compile drawer first**: it is modal and dims the board behind it, so a highlight painted under
  it would answer "which core?" with a ring nobody can see. The wiring is per-subject rather than per-rule,
  so it covers every finding the validator can raise rather than `OB17` alone, and it lands the editor half
  `B37` wanted for unplaceable markers. (`PlanTool.ShowFinding`; `tests/e2e/plan-findings.mjs` builds a plan
  refused for one specific reason and asserts the drawer yields, the board repaints, and the pulse clears
  itself — the last of which is also what proves the repaint was the highlight. C44 · B59 · B37)
- **A marker can be referred to, not only drawn (`B59`).** A piece has an id and a zone has an id; a marker
  had only its position in a list, which is enough to paint one and not enough to *name* one. The gap showed
  up the moment a rule had something to say about a particular marker: `OB17` could report that a goal stood
  in a spawn but not which goal, and an agent holding "the second core" loses that reference the instant a
  different core is deleted. Every placement now carries a persisted `id` — one `IPlanMarker` across spawn,
  wool, iron, destroyable and core — minted on load from the kind and a counter (`core-1`, `spawn-2`) and
  unique across the **whole** placement set rather than per kind, so a finding naming one is unambiguous.
  Minting on load is what makes the change free for documents already written: a plan from before markers had
  identity reads cleanly and gains ids, the same self-healing a piece id gets, and a duplicate id in a
  hand-edited document is dropped and re-minted rather than trusted. The C# model and `plan-doc.js` mint by
  the same rule, since either side may be the one that loads a plan first. Downstream, `PlanValidator`
  findings name the marker in both message and subjects, so a `B21` agent is told *which* goal to move
  rather than which piece to inspect, and `pulseSubjects` resolves a marker subject to a ring on its cell
  alongside the piece and zone rects it already drew — the paint side of `C44`, which wires it to a click.
  `ComposerVersion` moves to `marker-id-1` and all 72 board fingerprints with it: the ids are new bytes in
  the plan document, so every digest moves while no geometry does — 72 moved, 0 new, which is what a
  serialisation-only change should look like and the check that it was one. (`IPlanMarker`;
  `PlanModel.MintMarkerIds`; `PlanMarkerIdTests`; `tests/js/plan-doc.test.js`. B59 · B21 · B37)
- **An objective marker states the structure it builds (`G160`).** A core and a destroyable are placed as
  bare markers and take the generator's defaults; the knobs that vary them — a casing's footprint, height,
  wall thickness, capped-or-flush lava and float/leak pair, a destroyable's design, material and float — had
  existed in `CorePlacement`/`DestroyablePlacement`, in `plan-doc.js`, in the compiler and in the stamper
  since each was written, reachable only by hand-editing the plan JSON. They are now the panel that opens
  when a marker is selected, so every core the tool placed is no longer a capped 5×5×5 and every destroyable
  no longer a floating obsidian `pillar-3`.
  It lives **in the plan** and only there: the plan states a map's gameplay against the terrain it also lays
  down, and a core's marker sits on the piece its casing floats over. Configure writes `map.xml` and never
  touches the world, so it reads these out rather than setting them — its one editable field is `leak`, which
  is an attribute on the `<core>` element and nothing about the blocks.
  What is stored is only what differs: setting a field back to its default passes null through the one new
  bridge mutator (`setMarkerField`), which deletes the key, so a plan the author never varied stays the bare
  `{piece, at}` markers it was written as and a default that later moves moves for every plan that never
  disagreed with it. The vocabulary and those defaults are **served** (`GET /api/objectives/vocabulary`) —
  the Blazor client cannot reach `ObjectiveDefaults`, and a picker showing a default the stamper does not
  build is exactly what that one home exists to prevent. The material list is now one list for the same
  reason: `DestroyableMaterials` is both what the picker offers and what `SketchWorldBuilder` resolves, so a
  material cannot be offered that silently stamps obsidian while the XML names emerald.
  (`PlanTool` inspector; `plan-canvas.js` selection payload, `plan-bridge.js setMarkerField`;
  `ObjectiveVocabularyEndpoint`; `DestroyableMaterials`; `tests/e2e/plan-objective-variants.mjs` places a
  core and a destroyable and varies both in a browser. G160)
- **A world canvas that mounts before layout no longer writes a negative size (`N12`).** `WorldCanvas`'s
  rebuild measured its wrap directly (`clientWidth - 24`) and set the result on the `<svg>`, so a host that
  mounted it before the wrap had a box wrote `width="-24"` / `viewBox="0 0 -24 -24"` — which the browser
  rejects outright, three console errors per occurrence. It measures through the base's `_size()` now, which
  has carried the fallback all along; `fitBounds` reads the same measurement rather than its own copy. The
  bug was timing-dependent and predates the Cores phase — every canvas host could hit it — and it surfaced
  because a phase that mounts a canvas and paints immediately hits it often enough to fail a browser gate.
- **Cores are a phase of their own, and the objective phases are a group (`N12`, `B58`).** A PGM map may
  carry wools, destroyables and cores at once, so Configure gained a **Cores** phase beside Wools rather than
  a branch that swaps one for the other — an author adds a gamemode by filling a phase in, which is the only
  way a map that arrived with one objective can gain a second. The objective phases share **one** completeness
  gate (the map has *an* objective, not one of each), so a DTC map is no longer held behind an empty wool
  slice, and the wizard now indexes the rail, the unlocked range and Next into **the map's own phase list**
  instead of the catalog — three places where adding a phase would otherwise have meant something different
  from one map to the next.
  **Cores · Objectives** confirms what the ingest scan already found: `GET /core-suggestions`
  (`CoreSuggestionsEndpoint`) reads `core_candidate` with an optional box filter, and confirming a proposal
  writes a `CoreIntent` whose `Box` is the casing the detector measured — the field that decides whether
  anything is emitted at all, since `CoreGenerator` writes the `<region>` straight from it and skips a core
  that has none (OB8). Footprint and height come off that box, so a confirmed core states the 7×4 casing that
  is actually built rather than the 5×5 default. A core the detector missed is placed by drawing its
  footprint, its base `float` blocks above the ground under it — the world-export stamper's own rule.
  **Cores · Casing** reads the structure out — footprint, height, wall, capped-or-flush lava, float, and the
  region those scope — and sets the one field that is a rule rather than a structure: `leak`, whose distance
  over the measured float it spells out as the dig ("breach the casing **and** dig 3 blocks out from under
  it"). A plan-authored core has no volume yet and says why. The casing defaults are **served** by the
  endpoint rather than copied client-side, because `ObjectiveDefaults` must not drift from the stamper.
  The map context the steps share (teams, symmetry, islands, island→team) moved out of `WoolAuthoring` into
  `AuthoringContext`, since a core step needs the same teams and the same orbit.
  (`ConfigurePhases`/`ConfigureTool`/`ConfigureLayout`; `CoreObjectivesStep`, `CoreCasingStep`,
  `CoreAuthoring`, `AuthoringContext`; `CoreSuggestionsEndpoint`; `CoreIntentWireTests` pins the JSON the
  wizard writes against `CoreIntent`, and `tests/e2e/configure-objectives.mjs` drives both claims in a
  browser. N12 · B58)
- **A team id says team (B50).** The generated document's `<team>` ids were the bare colour — `id="red"`,
  `<team id="only-red">red</team>` — which is what the `color` attribute beside it already says, so nothing
  in the file named the team as a thing. They now read `red-team` / `blue-team`, and every reference the
  generators emit follows: the spawn link, the team filter's body, a wool's capturing team, a destroyable's
  and a core's owner. One helper owns the form (`IntentNaming.TeamId`), and `IntentNaming.Slug` still strips
  the suffix, so the region and filter ids named from a team are untouched — `red-spawn-point`, `only-red`,
  `reds-woolrooms` read the same whichever form the intent carries, and an intent whose ids already end in
  `-team` compiles to exactly the same document. (B50)
- **The defenders cannot empty their own wool-room chests (B51).** A wool room holds a supply chest for the
  team attacking it, and the room's `enter=not-<owner>` rule does not protect it: denying entry is not denying
  *use*, and a defender standing at the room's edge is outside the region with a chest inside it within reach.
  Each per-team room union now also carries
  `<apply use="deny(all(only-<owner>,chest-filter))" region="<owner>s-woolrooms"/>` over a named
  `<material id="chest-filter">chest</material>`. Written as an inline filter expression rather than a fifth
  named filter — it is a two-term composition used once per team, and at the apply is where a reader of the
  XML looks for it; the apply-rule editor already passes a non-simple reference through unresolved.
  `filter-region-wiring.md` template 5. (B51)
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
  both teams. (`MapIntent`, `TeamsGenerator`, `WoolGenerator`, `SymmetryExpander`, `ProtectionStep`,
  `WoolRoomStep`, `OrbitAssignment`; N10)
- **Wool-room wiring — the validated template structure (`docs/pgm/template.xml`)** — `WoolGenerator` now groups
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
  (`PreflightEndpoint`, `PreflightDto`, `Preflight`, `ReviewPreflightStep`; new-map-authoring.md §6)
- **Review & Export · Region tree sub-step (N07)** — the read-only inspect/debug view of the full generated
  region tree (between Pre-flight and XML). Intent maps drop the tree from the shaping steps (structure is a
  generated artifact), so it surfaces here: fetches `GET /map/{slug}/regions/tree` and renders it through the
  **reused editor `RegionTree` component** (category groups · collapse · type icons · synthetic-`__anon_N`
  styling · first-event tags), in the same single-column overview as Pre-flight, with a `read-only · N regions`
  badge and a note that the tree regenerates from the shaping steps. Writes nothing. (`ReviewTreeStep`;
  docs/tools/configure.md)
- **Review & Export · XML sub-step + gated Export (N06)** — the final sub-step: the generated PGM
  `map.xml`, segmented into containers picked on the left (**Full document** + Teams · Spawns · Wools ·
  Filters · Regions · Apply rules — the latter pulled from inside `<regions>`), each with a count, the
  selected block shown in `detail-xml-pre`. The flow-bar **Next becomes Export** (`ReviewXmlStep` fetches
  `GET /map/{slug}/xml`; on **409** the preview is replaced by the blocked message and Export is disabled;
  on 200 it registers the open gate + a download action with the wizard via `RegisterExport`). Export
  downloads exactly the previewed bytes through a new `studio.downloadText` Blob helper — `NextEnabled` at
  the final sub-step is the export gate, `Next()` runs the download. **This completes the Configure wizard
  spine** — a new map now flows intent → Map Info → World → Teams → Build → Wools → Review & Export → a
  validated, downloaded `map.xml`. (`ReviewXmlStep`, `ConfigureWizard` export wiring; new-map-authoring.md §6)
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
- **XML serializer conventions (`docs/pgm/template.xml`-faithful).** `XmlWriter.ToXml` serializes with **4-space
  indentation** (`XmlWriterSettings.IndentChars`, not the 2-space default / tabs) and **no `<?xml?>`
  declaration** (`OmitXmlDeclaration` — real PGM maps start at `<map>`); the `<void/>` filter is emitted
  **bare, without an id** (trivial + always inlined); and `<regions>` are now sub-ordered **by semantic role
  within each geometry type** (spawn points · wool spawns · spawn regions · monuments · build), so `*-point`
  and `*-spawn` ids no longer interleave. The `ReviewXmlStep` container segmenter was retuned to the 4-space
  indent. (`XmlWriter` + `ReviewXmlStep`; B11/B13/B15/B16)
- **Generated CTW-standards conventions (`docs/pgm/template.xml`-faithful).** Four corpus-alignment fixes to the
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
  (`TeamsGenerator`, `CtwStandards`, `SymmetryExpander`, `TeamAssignStep`; B10/B14/B17/B18)
- **Side-view point/block marker** — the inspector slice (`SliceView` / `SideviewCanvas`) now draws the
  inspected point/block as a marker dot at its primary-axis column + Y (tracking the draggable line when
  editable), so you can see *what* you're seating, not just the Y level. (shared; surfaced by N04 Spawn)
- **Geometry consolidation — two families, one home each (`A4`).** *Scalar* math lives in the
  dependency-free `PgmStudio.Geom` leaf (reachable by WASM client + server, no transitive deps):
  `Symmetry` (`Order`/`Point`/`Rect`/`Apply`/`Normal`/`OrbitAxes` + reflect/rotate) is the single canonical
  C# transform — every affine site routes through it (the per-phase client copies, `SymmetryExpander.Step`,
  both `ModeNormals`, and `RegionParser`/`RegionBoundsDeriver` `MirrorBounds` are gone), plus
  `Polygon.PointInRing` for the NTS-free projects (`SketchRasterizer`, client `SpawnStep`). *Area* geometry
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
  sign-facing → monument geometry). See `docs/world-scan/monument-suggestion.md`. (`5235107`, `45209a1`)
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
  `docs/world-scan/monument-candidate-store.md`. (F9)
- **`--migrate-only`** — `PgmStudio.Import` applies pending migrations to a live DB without importing. (F9)
- **`/authoring` concept page** — UI mock (no backend calls), the design reference for the real
  wizard. (`9f645dc` → `45209a1`)

## Layout generation (G) — auto map generation (lane sketch generators)
- **`model.md` rewritten as the paper it claims to be (G141).** Fifteen sections to nine, in pipeline
  order — pipeline · request · plan · shape model · allocate and fill · deriving the board · rules and
  scoring · code map · boundaries. Prose carries every claim and a table only supports one already made;
  the glossary is dissolved into the sections that use its terms; boxes and fill are folded into the
  shape and allocate sections that already described them; the two unbuilt passes (roughen, elevation)
  move to `ideas.md` as G142/G32-C; the board deriver gains a real section (islands, build regions,
  boundary runs, voids, the derived mid form) and with it the rule that **edge** means a full extent
  while **run** and **interval** name the parts. Section references are by name where the target could
  move. `tools/deriver/figure-check.cs` gates the 23 ASCII figures by parsing them out of the document
  and pushing each through the classifier that names that kind of thing — it found three wrong figures
  and one mislabelled family example on its first runs.
- **Lane sketch generators + Organic-generation demo — RETIRED** in favour of the plan-then-realize
  direction (`docs/generator/model.md`): the archetype starter generators (`LaneSketchGenerator`
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
  plan-then-realize direction, `docs/generator/model.md`): the page, its canvas bridge and
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
  generation: `docs/generator/model.md` (the **piece/interface plan model** — areal pieces +
  edge-interval interfaces, no skeletons; proxy-cell mini-layout semantics; one-way compile into sketch +
  intent with a detach point; rule-based composition, fragmentation moves, roughen + elevation passes; scope
  tiers), `docs/generator/rules.md` (the author-corrected per-role rule checklist + the seed shopping
  list), and the plan schema · compiler · seed-studio editor design (built as
  `G16`–`G21`). Resolves the `G15` exploration: **WFC evaluated and rejected** for the layout skeleton (CTW
  quality is global/relational — symmetry, spawn/wool separation, typed gaps — not local-adjacency texture);
  the polyomino vocabulary survives as the plan's proxy-cell grid. (G15)
- **Plan schema + validator** — `PgmStudio.Pgm/Plan`: `PlanModel` (the `*.plan.json` wire model — proxy-cell
  pieces/zones/placements/cliffs, one team's unit, symmetry fans the rest), `PlanDerived` (land interfaces
  from rect abutment, gap links through zones, islands, frontline, orbit fanning via `Geom.Symmetry`), and
  `PlanValidator` — structural errors (sliver/corner contacts, different-surface overlaps, unreachable wool
  over the fanned land+gap graph, wool path through a spawn piece) plus a non-blocking extensible **rule-lint
  table** citing `docs/generator/rules.md` ids (G2/G5/SP2/WL2/BZ5/EL1/EL3). 43 TUnit tests. (G16)
- **Plan compiler + seed plans (golden regression)** — `PlanCompiler.Compile(plan) → (SketchLayout,
  MapIntent)`, pure/deterministic: cells→blocks, land-connected pieces united into one polygon per component
  (`Geom.RectilinearUnion` — exact integer rect union reproducing the seeds' 12-vertex H / 6-vertex L),
  islands grouped by mirror flag, team-0 placements fanned per orbit (cardinal-quantized `facing` yaw),
  zones → `build.areas`, observer/maxHeight derived (surface+15 / surface+headroom), first wool = team colour
  + deterministic dye palette. The three seeds re-expressed as `tools/seeds/*.plan.json` with structural
  golden tests against the checked-in layout/intent pairs (base-2island/base-4team exact; base-2wool exact
  except two documented hand-authored values). Step terraces deferred (no seed exercises raised land seams).
  (G17)
- **A plan declares its negative space, and the compiler carves it (G160).** Terrain is stated by the pieces
  that generate it, so the ground a body encircles and no piece covers was void by omission — and a
  `SketchShape` polygon carries one ring, so the union outline stated each patch with its voids **filled in**.
  The hole that defines the `Ring` · `DoubleHole` · `P` · `G` compounds (`model.md` §4) exported as solid
  ground on **55% of 480 composed boards** (`Composed p30 t2 #9`: 950 phantom land cells across four holes).
  **`PlanVoids.Declare`** now runs as the compiler's first step and writes the omission down: every enclosed
  void becomes a `buffer` piece — the role that already meant reserved empty space, and which `BoardDeriver`
  already read as `declaredVoid`. An author may draw those buffers and need not; the step adds whatever is
  missing on every compile, so one deleted from a plan comes back, and it is **idempotent** (the same instance
  back when nothing is missing, so a caller can persist unconditionally). A void another piece lays ground
  into — a stepped plateau seated in a ring — is a plateau, not a void. `PlanCompiler` compiles each buffer to
  a `subtract` shape **clipped to what no generating piece covers**, so over a piece a buffer stays inert: it
  can declare a void, never destroy ground. Geometry through `RectilinearUnion.EnclosedVoids` (uncovered cells
  that cannot reach the outside, merged into rectangles — an L comes back as two) and `.Difference`. Over the
  480-board sweep the built footprint is now exactly the fanned piece union — zero phantom, zero missing —
  declaring is idempotent everywhere, and a deleted buffer restores the plan byte-for-byte. The terrain paint
  (G157) is what made the bug legible: an unrimmed, grassed-over hole reads as ground.
- **Plan editor page (seed studio canvas)** — `/plan-editor` (`Pages/Plan/PlanTool` + `js/studio/plan/`):
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
  `docs/generator/model.md`. (G55)
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
  markers inside the spawn piece when one exists). A `walls` list marks pre-built approach
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
- **The approach wall is three courses and a web, and the author picks which face opens (G170).** ST4's wall
  stood **five** courses of bedrock over the ground it barred, which is a fence between two halves of one
  lane rather than a line inside it. It is now **three** courses capped by **one course of cobweb** — the web
  is part of the barrier, not decoration on it: an attacker who bridges the top spends real time crossing it,
  and cuts it with the shears every kit already carries, which is what lets the stone be short enough that
  both halves still read as one place. The wall's other decision is now the author's too. It is two blocks
  thick precisely so one face can be opened for the defence chests while the other stays solid, and which
  face that is *is* which side of the line the supply is for — previously it fell out of whichever piece
  happened to have the smaller coordinate. A plan's wall mark carries a **`side`** naming one of the seam's
  two pieces (`PlanWall.Side`, default `"a"`), and the compiler resolves it to a face **per orbit image**,
  because a reflection swaps which face has the smaller coordinate and only the piece it looks out at
  survives the fan. In the plan editor the wall tool cycles a seam through *no wall → chests facing a →
  chests facing b → no wall* — the spawn marker's click-to-turn idiom, so the wall stays an annotation on a
  seam rather than gaining its own selection — and the open face is drawn as an amber bar just off it,
  without which a second click looks like it did nothing. (`StructureStamper.StampWall`, `WallDefenseChest`,
  `PlanCompiler`, `ContactGraph.WallChestPiece`, `plan-doc.cycleWall`; rules.md ST4) (G170)

- **The seed corpus — twelve author plans with honest player counts (rules v3 frozen)** — ten
  authored seeds + the real-map trace (`big-board-…-parallel-mid`, parallel mid, 30/team) +
  `mirror-tiny-map-cliff` (5/team, `mirror_z`, sub-base palette 3–7, the axis-spanning Δ6 mid
  cliff). Every seed stores the author's per-team count (comfortable cap); the G8 land-per-player
  coupling is derived (65 → 184 b/p rising with per-team land); all mid forms author-labeled
  (clean 8 · hash 3 · parallel 1); `docs/generator/rules.md` **froze 2026-07-04 as the
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
  build-zone interface discipline + the CT8 hole-ring split (`generator/rules.md`). 314 Pgm tests green.
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
  `docs/generator/model.md` §5. (G53)

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
  `generator/model.md` §1.13/§1.14/§5.5. (G63-C.1, G63-C.2 core)

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
  can form). Flush spawn/wools on no-frontline units: **0 across 4 presets × 64 seeds**, and over the wider
  4 × 600-seed sweep **7 of ~2400 units** keep one — every case a *saturated solid rectangle*, which is the
  guard's own documented exemption (the rectangle is the last fallback and has nowhere further to fall back to,
  so only it may keep a residue). The gate
  (`No_frontline_units_keep_every_neighbour_off_the_hub_front_face`) asserts that law rather than a blanket zero,
  over a sweep wide enough to reach the exempt case. Worst flat front run collapses 20 → 11 cells
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
  partial · `generator/model.md` §5.5)

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
  balance [0,140] blocks. WL9/WL10 authored into `generator/rules.md`. Pgm suite 700/700. (G115)

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
  the ~40-task G long tail → `docs/generator/ideas.md` (ids preserved), the new focus (G117/G118
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
  672/672. Contract: `generator/model.md` §1.14/§5.5. (G89, G96)

- **The hub box — the constraint-source box kind (G88) + the offer type (G96 hub half)** — `Compose/HubBoxEmitter.cs`
  + `Compose/Boxes/EdgeOffer.cs`: the **terminal-free** hub box (map-generation.md §5.5). `HubBoxEmitter` finishes a
  `BodyEmitter` `ShapeBody` with the hub designation — per-edge `interface` widths, **no room/marker** — over the
  authored form menu **Rectangle · L (`SpineArms(1)`) · U (`SpineArms(2)`) · Ring · Double-hole**, each sized to fill
  the box (a too-small box a directed null, an off-menu form a throw). It publishes one **`EdgeOffer`** per contiguous
  free run on each edge (**G96's hub half**: `Several`-grouped, the `wN` width a neighbour reads as its `cw` — the
  composer's `edgeWidths` constraint, geometric default; a U's bay reads as two bottom offers, a ring's wall as one
  full run). Offers derive uniformly from a new `BoxInterfaces.Of(ShapeBody)` free-edge read; `BoxJoint.Offer` carries
  the provenance; `FillProfiles.HubForms` is the hub's `Compound`-typed menu row. The composer consuming the offers +
  retiring the grower's `hubU×hubV` hub is G63-C. Pgm suite 665/665. Contract: `generator/model.md` §1.14/§5.5. (G88, G96)

- **Designation-scoped docking gate + the marks (G95)** — `Pgm/Shapes/Designation.cs` + `Compose/Boxes/DockingGate.cs`:
  `DockingGate.Role` re-grounds from one global slot table to **`Role(Designation, slotOrMark)`** — the
  binding G88/G89's hub/frontline designations stamp onto and the gate reads. New `Designation { Approach ·
  Hub · Frontline }` (wool and spawn are both `Approach`) and `DesignationMarks { interface · face }` — the
  siblings of the approach's `entry`/`room`. The **approach table is verbatim** (room → never-dock, entry →
  docking edge, structural slots internal), so every emit/dock/mirror test is byte-identical; the `Hub`
  (`interface` docks, no terminal → nothing vetoes) and `Frontline` (`face` docks) rows are defined and pinned,
  ready for the G88/G89 emitters to stamp. No new rule content — the binding only. Pgm suite 656/656. Contract:
  `docs/generator/audit.md` §3 gap 2 / §7; `generator/model.md` §1.12, §5.3. (G95)

- **Shape vocabulary + rule kinds folded into the canonical doc (G94)** — `docs/generator/model.md`:
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
  `docs/generator/audit.md` §3/§6 step 2.

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
  `docs/generator/audit.md` §4/§4.1. (G92)

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
  `docs/generator/model.md` §4 (the shape model). (G91)

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
  `docs/generator/model.md` §4 (the shape model). (G90)

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
  fragment pass (cutting only a `run`/`bar`, never a `room`/`entry`, per `docs/generator/model.md` §5.3)
  with a one-line re-add. The two `ComposerTests` and one `WoolBoxGrowthTests` that asserted cuts occur retire
  with it (they land again with the pass). Verified: full Pgm suite 609/609 green, gallery 42/42 with 0 `bridge-a`
  zones. (G86)

- **The spawn box is a small fixed box — a size rule in `FillProfiles` (G84)** — `Compose/Boxes/FillProfiles.cs`:
  the porting kept the old grower's "grow the shape to absorb its budget share" sizing, so a spawn stretched with
  player count to ~100 blocks when the docs say a spawn is **small, ≤20** (`docs/generator/model.md` §4).
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
  green. Contract: `docs/generator/model.md` §4. (G84)

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
  `docs/generator/model.md` §4/§8/§12. (G63-B)

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
  `docs/generator/model.md` §4/§12. (G63-A)

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
  verdict tracks the room under flip; the slot-role map). Contract: `docs/generator/model.md`
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
  `docs/generator/model.md` §1.5/§4. (G41-B)

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
  `docs/generator/model.md` §4.1/§8. (G41-A)

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
  `docs/generator/model.md` §4/§5.3. (G78)

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
  `docs/generator/model.md` §5.3/§5.4/§12. (G62)

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
  corner-law assertion updated (`ComposerTests`). Contract: `docs/generator/model.md` §4/§5.2. (G79)

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
  survival through the cut, family variety across seeds). Contract: `docs/generator/model.md`
  §4/§5.3. (G61)

- **Fold-based scythe test — family reads stable under endpoint manipulation** — `Geom/Cells.cs` +
  `Pgm/Shapes/ShapeClassifier.cs`: the Scythe/Z split now asks whether the terrain **doubles back** (some grid
  row/column crosses it in two runs — `Cells.HasFold`, i.e. not orthogonally convex) instead of whether the
  bounding box carries a one-edge bay (`HasBay`, removed). The bbox read flipped when an endpoint slid off a
  box corner (the bay escaped past the vacated corner and the shifted/side-docked scythes read Z standalone,
  Scythe with a hub docked); the fold is a property of the cells alone, so the emitter's entry/wool-shift and
  side-dock manipulations keep their family in both contexts. Verified: `ShapeVariantTests` (14 variant grids
  × 2 scales, standalone + hub-docked) + the catalog/mirror/stress suites unchanged. Contract:
  `docs/generator/model.md` §4.7.

- **Wool-box pieces carry their slot role** — `Pgm/Compose/WoolBoxEmitter.cs` + `TeamUnitGrower.cs`:
  `WoolBoxEmitter` now tags every emitted piece with its **slot role** (`ApproachSlots` on `GrownPiece.Slot`) —
  `entry` (the universal hub-attach), `run`, `bar`, `leg`, `room`, qualified `entry-run`/`room-run` and
  `entry-bar`/`room-bar` — per the §2 piece-vocabulary table, exposed as data via `ApproachSlots.Template`.
  It is a **shape-internal taxonomy, distinct from the map-level piece `role`** (terrain pieces keep `piece`),
  and is the foundation the shift (G50) / width (G51) / docking (G52) rules target — those name a slot instead
  of re-deriving it from geometry. Invariants held: a family emits a **stable piece count** (no collinear
  merges) and a role is a **template slot, not a property of the rectangle**. Verified: `WoolBoxEmitterTests`
  (25 cases — template order per family, flip/variant invariants, stable count) + the `ShapeMirrorTests` slot
  round-trip (G58). Contract: `docs/generator/model.md` §5. (G54)

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
  rewire onto `Cells` at M1 (G59). Review: `docs/generator/evaluator.md` §3. (G58)

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
  cites one `generator/rules.md` id, never a family name); `EvalContext` (derives `ContactGraph` + `PlanValidator`
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
  `docs/generator/evaluator.md` §5/§9; direction: `docs/generator/evaluator.md`. (G60)
- **Composer evaluator — soft scoring + surface distance (M2, part 2a)** — the evaluator's soft half.
  `SoftTerm` (a pure `Value(ctx)` metric + its own drawn `Evidence`); `SeedEnvelopes` generated by
  `tools/deriver/envelope-stats.cs` — it runs each term's `Value` over the seeds (so band and score can't drift)
  → embedded `Evaluate/seed-envelopes.json` + generated `docs/generator/seed-envelopes.md` — scored as `Band` distance
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
  Endpoint + JS overlay-pref tests green. `docs/generator/evaluator.md` §9.7. (G60)

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
  showcases multi-access, buffer spacing, and land/build-zone attachment points. **`connector` and that
  study are retired (C43)** — a composed layout attaches by box intersection, so the attachment point is
  derivable everywhere and the mark had nothing left to say. (G46 · G47 · G48)

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

- **Every drawing canvas paints its world layers, and one vocabulary says how (CV18)** —
  `render/canvas-painter.js` + `shape-render.js` + `sketch-render.js` + `symmetry-render.js` +
  `primitive-style.js` + `block-render.js` + `canvas-chrome.js` + all three canvases and their draw
  controllers + `tests/js/{canvas-painter,render,primitive-style,symmetry-render}.test.js` +
  `tests/e2e/paint.mjs`. With the plan canvas proven (below), the sketch and world canvases followed, and
  the primitives that were private to the plan conversion became the painter's own: `rect`/`line`/
  `segments`/`circle`/`dot`/`ellipse`/`path`/`ring`/`poly`/`text`/`image`, in **world** coordinates
  through a `toSurface` fit (identity for plan and sketch, the bbox transform for the world canvas), under
  one style vocabulary — `fill`/`fillAlpha`, `stroke`/`strokeAlpha`/`width`/`dash`, `alpha`. Two knobs a
  caller used to have to remember are now the painter's: a `width` is in screen pixels at every zoom, and
  a `var(--token)` colour is resolved and cached (a context takes neither for granted). `primitiveStyle`
  returns that vocabulary rather than SVG attributes, so the treatment tiers stayed one table rather than
  becoming two. Where a thing is drawn in both dialects the *geometry* was factored out instead of copied:
  `symmetryAxes` is the type→lines rule for the painted world canvas and the retained Configure preview
  alike, and the path builders serve both because a canvas `Path2D` takes SVG path data.
  The conversions removed the element caches with the elements. `SketchCanvas` no longer keeps a shape
  `<g>` per id, `WorldCanvas` no longer keeps a region group and shape map, and both now paint from the
  state that was always the source of truth — which also ends the one-way patching that made a marker
  inherit region opacities on the next selection refresh. A draw controller keeps its in-progress
  primitive as numbers and draws it through `paint(painter)`, so previews live in the same frame at the
  same scale as everything under them; the edit controllers' handles stay SVG, where a fixed pixel size
  and a `mousedown` target are the point. `WorldCanvas` gained a real `dispose` (a painted surface owns a
  canvas element, a resize observer and a theme watcher, and sixteen hosts mount it). `paint.mjs` now
  sweeps all three surfaces, entering the world canvas through the Edit tool's nav rail because the route
  itself lands on Identity, which mounts no canvas. JS 188/188, e2e 19/19 paint + 33/33 smoke + 22/22 plan
  refusals + 19/19 icons, client build clean. Each surface reports its own numbers: plan 35.2% painted
  coverage, sketch 25.0%, world 10.0%, all 64 distinct colours, all with the buffer at the CSS box × DPR,
  their screen layers still in the svg, and a wheel burst changing the pixels. **Accepted on a real
  Firefox**, which is the verification that counts here: Chrome never showed the artifact, so it can only
  report that a surface draws, while Firefox is where a stretched rasterization would still be visible. It
  is not — on any of the three. (CV18)

- **The side view shares the painter too (CV18 follow-up)** — `canvas/sideview-canvas.js` +
  `bridge/sideview-bridge.js`. The depth cross-section was the one surface still driving a 2-D context by
  hand. It never had the zoom artifact — it has no svg DOM and no viewport matrix — but it carried the two
  defects `CanvasPainter` exists to prevent: it re-read four CSS custom properties from the document on
  *every* paint (the exact shape the token cache replaces, and the in-repo precedent the CV18 investigation
  cited), and it handled `devicePixelRatio` not at all, so its text and hairlines were soft on any HiDPI
  display. It now takes the painter's backing store, token cache and `layer()` phases — which also stops
  `imageSmoothingEnabled` leaking out of the block blit that wants it. A third defect surfaced on the way:
  it sized the bitmap from the wrap's *padded* box while the element itself is `width: 100%` of the content
  box, so every frame was drawn 24px wider than the element and stretched to fit; it now measures the
  content box, as `CanvasBase` already did for the same padding. It keeps its own viewport (a fitted
  integer scale, no pan or zoom) and uses only the primitives whose call sites take plain numbers, since
  the box-shaped ones are named for the plan's x/z axes and this surface's second axis is elevation. Gained
  a `ResizeObserver` so it follows its container, and a real `dispose` on both bridge mounts. Verified by
  hand on Build Regions and the region inspector's slice: the depth map, the dashed height line with its
  label and drag tab, the marker dot, and a drag that lands on the Y under the cursor. JS 188/188, e2e
  19/19 paint + 33/33 smoke + 22/22 refusals + 19/19 icons, build clean. (CV18)

- **The plan canvas's world layers are painted, not retained (CV18)** — `render/canvas-painter.js` +
  `canvas/plan-canvas.js` + `tests/js/canvas-painter.test.js` + `tests/e2e/paint.mjs` +
  `tools/painter-probe.mjs`. Zooming an authoring canvas in Firefox left the picture soft until the next
  input: a viewport matrix on a transformed SVG DOM is a paint-property change, so the engine may stretch
  the rasterization it already holds and defer re-rastering indefinitely — no repaint the app can issue
  fixes that, and every nudge tried measured worse. The fix is the surface the reference tools (Figma,
  Excalidraw, tldraw) use: the plan editor's 13 world layers now draw each frame, at the current scale,
  onto a DPR-aware 2-D `<canvas>` pinned under the `<svg>` — no cached raster exists to go stale — while
  screen-space chrome (labels, selection box, resize handles, scale bar) stays SVG where DOM semantics
  pay, and the svg stays the single pointer target. `CanvasPainter` owns the per-canvas traps once:
  backing store sized to the CSS box × devicePixelRatio (clamped at 2), the viewport transform on the
  context (`screenPx` gives constant screen-width strokes — the by-hand `non-scaling-stroke`), and a
  CSS-token resolver with theme-flip invalidation that probes each value against the context and demotes
  what it won't parse (canvas colour parsing is historically a separate parser from the stylesheet's).
  Interaction code is untouched — hit-testing, snapping and selection were already data-driven off
  `plan-doc`, and the hover cursor now comes from the same `pickAtWorld` the click uses. The hatch fills
  became `CanvasPattern`s, the lint pulse drives its own rAF loop, and `painter.layers` replaces
  `data-layer` as the queryable paint order. Decided by measurement (`painter-probe.mjs`): rebuild-and-
  stroke 20.4 ms vs rasterize-once 0.6 ms at 45k cells, and `color-mix()` tokens parse in a 2-D context.
  SkiaSharp-on-Blazor was rejected — the documents, controllers and hit-testing live in JS, and marshalling
  draw calls or documents across the WASM boundary is the class of cost already hit three times. The
  server-side `PlanBoardSvg` divergence is accepted. `paint.mjs` asserts on pixels: painted coverage,
  buffer = box × DPR, chrome still in the svg, and that a wheel burst *changes* the pixel signature — a
  stretched raster would not. JS 166/166; the artifact is confirmed gone on a real Firefox. (CV18)

- **The world canvas states its layer stack once (CV19)** — `canvas/world-canvas.js` +
  `docs/client/canvas-interaction.md`. `CV13` gave the canvases `render/layer-stack.js` — the key order of
  the spec *is* the paint order, bottom first, and each group carries `data-layer="<name>"` so a layer is
  addressable by name rather than by its index among siblings. Sketch and plan were rerouted then; the world
  canvas, the one with sixteen mounts, was the holdout. It hand-built 13 groups as `id="layer-*"` across a
  dozen `#buildX()` methods, each creating its group *and* painting it, with the z-order stated **twice** —
  once by the `#buildLayerEl`/`#blockLayerEl`/`#islandLayerEl`… fields, once by the append sequence in
  `#build()`. Now one `layerStack` block declares it, the builders became `#paintX()` painters that clear and
  repaint a layer they no longer own, and the field wall collapsed into `#world`/`#screen`. Consequences worth
  noting: `refreshRegions` stopped swapping the region group node for a fresh one and simply repaints it;
  `#renderAnchors` owns its own clear, so all three callers are plain repaints instead of clear-then-render;
  and the hand-rolled `while (firstChild) removeChild` / `style.display = v ? "" : "none"` idioms gave way to
  `clearLayer`/`showLayer`/`showLayers`. **Two dead layers surfaced**, which is the point of stating a stack
  truthfully: `block-highlight` was a permanently `visibility:hidden` rect whose handle was assigned and never
  read — removed; the `build` layer has no painter and `setBuildVisible` no caller outside the class — kept
  for now and filed as `CV21`. Net 58 lines out, behaviour-preserving by construction: JS 150/150, e2e 33/33
  smoke + 22/22 refusals, with `configure` and `edit` clean. (CV19)

- **The world canvas is named for what it draws (CV20)** — `canvas/world-canvas.js` +
  `bridge/world-bridge.js` + `controllers/world-{draw,edit}-controller.js` +
  `Components/Editor/WorldCanvas.razor{,.cs}`. `EditorCanvas` was named for a route, and the wrong one:
  eleven of its sixteen mounts are **Configure** steps and only five are Edit phases, while its three
  siblings are all named for content (`PlanCanvas` a plan, `SketchCanvas` a sketch, `SideviewCanvas` a side
  view). What it draws is the **world** — bounding box, islands, the block overlay, the buildability
  heatmap, symmetry axes, spawns, wools, monuments, regions — which is already the term the canvas-surface
  work uses for that content. Its two interaction controllers followed the same rule the sketch pair
  established (a controller is named for its canvas), so `EditorDrawController`/`EditorEditController`
  became `WorldDrawController`/`WorldEditController`. Pure rename over 52 files: the interop contract needed
  no change (the entry point was already `studio.mountCanvas` and the callbacks already `OnCanvas*`), and
  the only load-bearing edge was the module path string in `studio.js`'s native dynamic import. Client build
  clean (0 warnings), JS 150/150, e2e 33/33 smoke + 22/22 plan refusals — the surfaces that matter here are
  the `configure` and `edit` routes, which render clean, and a mis-renamed module would have surfaced as a
  failed-request fault rather than passing quietly. (CV20)

- **Zoom stops going soft on the authoring canvases (CV17)** — `canvas/canvas-base.js` +
  `render/canvas-chrome.js` + `canvas/plan-canvas.js` + `canvas/sketch-canvas.js` + `render/sketch-render.js`.
  Zooming the plan or sketch canvas blurred the picture for a moment; panning and dragging a shape did not.
  Two independent per-event costs, both **per input event where they should have been per frame**:
  (1) the grid spans the *visible world* since the infinite canvas landed, so a wheel tick moves the snapped
  extent and misses the memo that was written for pan — every tick tore down and rebuilt the whole grid,
  which grows as you zoom out (60 → 348 lines); (2) `plan-bridge`'s `onZoom` crosses into .NET, so each tick
  also marshalled an interop message and re-rendered a component. With the main thread behind, the
  compositor re-uses the last raster scaled by the new matrix — which is what "muddy" was.
  Fixed by giving the canvases a frame: `CanvasBase` applies the viewport matrix synchronously (it must not
  lag the pointer) and coalesces the chrome repaint *and* the zoom report into one `requestAnimationFrame`,
  with `_flushViewportChrome()` for callers that must read in the same tick. A shared `gridStep` ladder
  (`canvas-chrome`) then draws every 1/2/5/10 units by how many pixels a unit spans — the zoom-level idea map
  software uses — so a zoomed-out grid stays a fixed handful of lines and the memo holds across most of a
  zoom. Both canvases share it; the chunk grid takes the step too.
  Measured on `/plan-editor`, 8 wheel ticks: **11.4 ms → 0.3 ms per tick**, grid lines after the burst
  348 → 116, zero grid mutations during it. 150 JS tests + e2e smoke green. (CV17)

- **Land spend on every generated board (G148)** — `ComposeEndpoints` + `Contracts/ComposeDtos.cs` +
  `Features/Generator/`. The browse feed could say a board scored 2.3 and had a ring hub, but not what it
  cost. Every number already existed and none was displayed: `BoxPartition.Of(unit)` carries each box's kind,
  footprint (`Rect`) and land, and `ComposedStages.Envelope.LandPerTeam` is the budget **that board** was
  built to — the endpoint already held the unit, so this is a group-by, not a new derivation. Cards get
  `52/50 · 103%` beside the score; the detail drawer gets the per-kind table. **Both currencies are reported**,
  because the model keeps them apart: footprint is the box rect (fixed when the box is seated), land is the
  walkable terrain inside it (what the fill spends) — a donut has a large footprint and modest land, so either
  alone misreads the shape. The display names its own units: cells, for **one team unit** (the board is that
  unit fanned), against a budget converted from the envelope's blocks². Its first run measured what nothing
  had been measuring — 28 of 40 boards over budget, median 115%, max 222% — filed as G149. Pgm 773 + Api 89
  green; e2e smoke 33/33. (G148)

- **Shape catalog — the vocabulary as a page** — `Pgm/Compose/ShapeCatalog.cs` + `ShapeCatalogEndpoints` +
  `Client/Features/Catalog/` + `css/studio/catalog.css`. `/catalog` renders **98 cards**: every approach
  family and body form the pipeline can put in a box, emitted once mouth-up, deduplicated by cell pattern,
  and coloured by box kind (the same id-prefix coding `PlanBoardSvg` gives a composed board). Each card
  carries its box, corridor width and knob tokens (`side-tuck`, `wool at end`, `attach 4`), and a **reach
  badge** — 91 *in the mix*, 1 *reachable*, 6 *emitter-only* — with a note naming the mechanism and its task
  id for the latter two, so a badge is never bare. The in-mix set is **collected by running**
  `UnitRequests.WoolRequest` over seeds and the grid of inputs it reads, so retuning a chance or cap in
  `UnitTuning` moves the catalog with it rather than drifting from a second copy. `GET /api/shapes/catalog`
  returns the bounded set in one page with whole-catalog tallies (a chip says what it would show before it is
  picked); the page filters in-browser. The measured shape it makes visible: the donut is 73 of the 89 wool
  cards, while U, H and L are one apiece — the imbalance any G118 verdict corpus inherits. Pgm 772 tests
  green (12 new, gating that each card's tier matches what the pipeline really does, and that a card's box is
  the smallest the emitter accepts rather than a chosen display size); e2e smoke 33/33 with `/catalog` added
  to the route sweep. (G144)

- **Shape catalog — the knob panel** — `ShapeProbeEndpoints` + `Features/Catalog/`. Clicking a card opens a
  live emit panel seeded from that shape: family, box width/height, corridor width, mouth, flip, side-tuck,
  wool-at-end and the donut's attachment width, re-emitted on every change through **`BoxFiller`**, so the
  profile check and the `DockingGate` run exactly as they do in composition. **A refusal is the deliverable,
  not an error**: `FillResult` already carried every reason as data and nothing surfaced it, so the panel
  prints the emitter's own words — `too-small` with the family's minimum box and a one-click *Resize to
  W×H*, `unsupported-knobs` with the guard's message verbatim (`"side-tuck room is supported for I, Z and
  scythe"`), `illegal-dock` with the `DockRejection`. On success it reports the land spent and the emitted
  slot template. `GET /api/shapes/probe/schema` serves the knob surface — each family's minimum box and which
  knobs it takes — so a family gaining a knob needs no matching client edit, and it reports the **dock frame**
  (`WoolBoxEmitter.MouthBox`), the only frame that can agree with what a size refusal says. Api 89 tests green
  (9 new): the schema covers every emittable family and flags the menu, the offered resize is checked to
  actually fill for six families (a transposed or off-by-one minimum would hand the author a button that
  refuses again), and the schema's minimum is asserted equal to the refusal's per family — the frame bug this
  endpoint shipped with, now gated. (G144)

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

- **Boxes as an authored plan annotation** — `Pgm/Plan/PlanModel.cs` + `Pgm/Plan/PlanBoxes.cs` +
  `Pgm/Compose/PlanBoxAnnotation.cs` + `Client/.../plan-doc.js`/`plan-canvas.js`/`plan-bridge.js` +
  `Features/Plan/PlanTool`. A typed `boxes` section in `*.plan.json` — `{ id, kind, rect, members? }` with
  `kind` ∈ `spawn`/`hub`/`wool`/`frontline`/`mid` (unknown folds to `mid`) — naming the partition a plan's
  pieces realize. **Membership** is one rule in one place (`PlanBoxes.MembersOf`, mirrored in JS): the pieces
  `members` names, else every **generating** piece wholly inside the rect (annotations are never members).
  Authoring-only, exactly like the tracing `reference` block: the compiler, validator and derivers ignore it
  — gated by a test compiling a boxed plan against the same plan with its boxes stripped — so the "plan on
  disk is label-free" doctrine stands. In the editor a box draws as an unfilled dashed envelope (one palette
  tool per kind, kind-coloured) and is **the plan tool's group, selecting exactly as the sketch tool's
  islands do**: single-click picks the box, **double-click drills** into the piece under the cursor, **Escape
  pops back out** to its box, and a drilled piece stays grabbed while the press lands inside it (the
  precedence `sketch-canvas`'s `_hitMovable` gives a drilled shape). Dragging a box **carries its members**
  (resolved at grab time, so nothing falls out mid-drag); resizing only re-scopes it and deleting removes the
  annotation alone. Markers keep single-click priority at both levels — a marker rides a piece rather than
  being grouped, and its sub-cell hit radius can't steal a click aimed at the box body. Boxes fan into the
  symmetry ghost; the inspector edits id/kind, lists the members and can **fix** them as an explicit list or
  release them back to containment. A kept
  board writes its own partition in (`PlanBoxAnnotation.Apply` on `POST /api/compose/pin`, members explicit
  off `BoxPartition.KeyOf`), so a picked generated board opens with the grouping that produced it. The two
  shifted-frontline exemplars migrate off the `buffer`-as-overlay misuse onto real boxes (`buffer` means a
  reserved gap again), and `tools/deriver/plan-readback.cs` reads `boxes` — per-box derived reads on both
  exemplars (g-hub: G hub + 2-arm frontline; hole-hub: Ring hub + Donut wool). Pgm 696 + 146 JS tests green.
  Contract: `docs/tools/plan.md`; vocabulary row added. Unblocks G125. (G126)

- **Feasibility read-back — "could the composer have produced this?" (G125)** — `Pgm/Compose/Producibility.cs`
  + `Compose/Boxes/FillRejection.cs` + `Api/Endpoints/PlanInspectEndpoints.cs` + `Contracts/FeasibilityDto.cs` +
  `Features/Plan/PlanTool`. The read the validator can't give: a plan scores 0 with no rule fired and is still
  unbuildable by the machine — which is exactly what both funnel exemplars are. It answers by **search, never by
  inverse**: the parameter space is already declared as data (the hub/frontline form menus, the wool production
  families, the spawn sizes, the wall and lane widths) and every emitter is a pure function of an explicit tuple
  with no RNG inside, so it enumerates what those tables admit, calls the **real emitters**, and compares masks —
  add a hub form and the search picks it up for free. The **reproduction gate** is the test that makes a negative
  verdict mean anything: every box of every composed board must be reachable this way *and* produce zero
  unit-level findings (30+ boxes over 10 seeds), so the check can't drift from the allocator it mirrors.
  The **why** comes from three existing sources, no new rule logic — the emitters' own rejections, a measurement
  against the same constant the emitter reads (`Cells.MinRunWidthRaw` vs `HubWallCells`/`WoolLaneCells`), and the
  **nearest miss** the enumeration yields for free (closest candidate + the extra/missing cells, which localises a
  discrepancy without a per-family analyser). Identity is a **hint, never a verdict** (a 1-cell-walled shape still
  reads as the `G` it topologically is); terrain and room compare separately. **Unit-level** findings cover the
  arrangement — the parallel-fronts guard (via `Composer.FrontFacesSymmetric`, generalized so the gate and the
  read share one implementation), the frontline's pinned face demand, and seat separation (via
  `TeamUnitAllocator.TooClose`, at the 2-cell floor `FrontGuard` can fall back to) — reported **alongside** the
  per-box reads, never instead of them. Findings **cite the task that would unblock them**: a nearest miss of the
  same form the box reads as means only its proportions are unreachable, so it names G105 (body widths / the
  asymmetric ring) or G82 (approach entry widening), with G129 for the generalization; a different form cites
  nothing rather than guessing. Prerequisite work landed in the same theme: every box-fill refusal now carries a
  typed `FillRejection` (hub/spawn/frontline had returned bare nulls, and `BoxFiller.Fill` **threw** on an
  unsupported knob combination against its own data-channel contract). Surface: `POST /api/plan/feasibility`
  beside `/plan/evaluate`, and a **Feasibility panel** grouped per box with the arrangement findings pinned
  above; clicking a box **paints the cells it differs by** on the canvas, reusing the evaluator's evidence
  styling. On the hole-hub exemplar: the ring's one over-wide wall localises to **2 cells** citing G105, the
  donut's 1-wide attachment to 8 cells citing G82, and the shifted frontline fires **twice** citing G123 — while
  the evaluator still reports score 0, nothing fired. Pgm 716 + Api 76 + 148 JS tests green. Contracts:
  `docs/tools/plan.md`, vocabulary rows added. Unblocks the B21 agent loop (an agent iterating against the
  validator alone converges on plans the composer can't reproduce). (G125)

- **The branch hub's leg sampler — a wide leg makes the L an L (G105)** — `Compose/HubBoxEmitter.cs` +
  `Compose/TeamUnitAllocator.cs` + `Compose/Producibility.cs`. `BodyEmitter.SpineArms` has always had a per-arm
  overload — *"each atom rectangle free to differ in size"* — but the hub called the uniform one, so every L and U
  leg came out exactly one corridor wide with its columns computed from the box. Measured before: leg width **2 in
  100% of cases** across 8p/12p/20p, arm starts fully determined, nothing sampled. The frontline drove the very
  same emitter family through the per-arm overload and got widths 2–6.
  A hub now draws its legs (`HubBoxEmitter.SampleArms`) under laws in corridors: a leg is **at least one and at
  most 2½** (`LegWidthCap*`, so 5 cells at `cw` 2), the **L keeps a corridor of notch** beside its leg and the
  **U a corridor of bay** between its two. Widths are otherwise free and explicitly **not** constrained to be
  even — a hub leg sits away from the symmetry axis and so has no parity to answer to, unlike the frontline's
  face. The notch/bay minimum is also the form's **size floor**: a spine too short to leave one refuses the form
  (a directed null into the rectangle fallback) rather than building a sliver, which it previously did.
  The layout rides on `Box.HubArms` exactly as the ring walls ride on `Box.HubWalls`, because the hub body is
  emitted twice — once by the allocator to read the runs it offers, once by the filler — and a body sampled
  twice would not agree with itself. `Producibility` sweeps the sampler for its range the same way it sweeps the
  wall and frontline-arm samplers, so the search admits exactly what the composer draws.
  Measured at 8p, where the L is the form the budget actually buys (243 of 676 hubs): leg widths **2:173 3:81
  4:52 5:26** — 29% now wider than a corridor, the 2½ cap reached — and **no notch or bay under 2**, where the
  old default produced 13 at 1. 408/408 still compose with zero unproducible boxes and zero unit findings.
  Pgm 744 green. (G105)

- **The hub draws its width free of parity — half the size ladder was unreachable (G135)** —
  `Compose/TeamUnitAllocator.cs`, `Compose/Composer.cs`. Two rules survived the switch to placing a unit by its
  face (G132) without anything left to do, because each was buying an alignment the face anchor now provides.
  **The hub's lateral span.** Under a laterally-flipping symmetry an odd draw was rounded to even and the hub
  centred on the axis, so it would coincide with its own image and the two sides' fronts line up. What has to
  coincide is the **face** — it carries its own parity law (G134) and the finished unit is centred on it — so the
  rounding bought nothing and cost half the ladder. Measured over 1203 boards per symmetry: `rot_180` spans were
  `4:513 6:387 8:126 10:177`, **0% odd**, against `mirror_z`'s 54.3% — widths 5, 7, 9 and 11 had never been built
  on a rotational board. Now `4:304 5:340 6:139 7:169 8:69 9:53 10:71 11:58`, **51.5% odd**, a ladder the shape of
  the mirror one. Every request still composes (1203/1203), the hub-form and wool mixes hold, mean evaluator score
  is flat (0.596 -> 0.601) and boards scoring a clean zero rise **616 -> 707**.
  **The front-slack resample.** The compose loop threw away a unit whose front hull sat too far off the axis. With
  the face centred and its span even the residual is half a cell — one cell of slack against a cap of four — so it
  could not fire: 903 boards per symmetry composed **bit-identically** with the cap in place and removed. The cap
  itself stays, because it is also the bound on the **BZ9 authoring finding**, which reads authored plans that may
  draw a front anywhere and have no face anchor to rely on. Only the composer's copy was dead.
  Geometry moved, so the composer version is **box-3** and the fingerprints are re-recorded — and the change is
  confined to where the rule applied: of 72 fingerprinted boards, all 36 `mirror_z` are unchanged and 22 of 36
  `rot_180` moved. Reproduction gate 408/408 with zero unit findings. Pgm 751 + Api 79 green. (G135)

- **The composer version is enforced, and a stale row says so (G137)** — `Compose/ComposerFingerprint.cs`,
  `tools/compose/fingerprints.cs`, `Api/Endpoints/PlanStoreEndpoints.cs`, the plan-open list + generator tray.
  A generated plan's identity is its `ComposeDescriptor`, and its contract is that *a descriptor reproduces its
  plan exactly within one composer version*. The version was a hand-written constant nothing checked, so it sat
  at `box-1` through every geometry change since it was introduced — measured, `(12p, rot_180, seed 3)` hashed
  `C7B9BA11…` before this branch and `A882FAD5…` after, both stamping `box-1`.
  **The guard.** A digest of what a fixed request set composes to, recorded next to the version it was taken
  under. Two failures, two different messages: a record from another version (regenerate it) and a board that
  moved while the version stood still (bump it, then regenerate). It asserts the **bookkeeping, never the
  layouts** — a changed board is always allowed, it just has to arrive with a changed version — which is what
  keeps it apart from the frozen goldens G32-D parks. A third test pins the premise the other two rest on: that
  composing one request twice gives the same plan.
  **Its sensitivity is its coverage, which had to be measured rather than assumed.** The first cut, two seeds per
  cohort, did **not** fire when the bay cap was perturbed: the cap only binds on a two-legged hub, a form about
  one 20-player board in sixteen carries, so twelve boards had none to move. At 12 seeds per cohort (72 boards,
  ~2s) the same perturbation moves `20p/mirror_z/s3` and the guard names it. A change touching only a rarer form
  can still slip through; the fix is more seeds and the cost is linear.
  **The read-back.** `PlanSummary.StaleComposer` marks a generated row an older composer made. The stored plan is
  untouched — geometry is stored, not recomputed — so the marker says the narrower true thing: it opens as
  stored, but its seed no longer re-composes to it. Shown on the plan-open list and on the hold-tray thumbnail.
  The endpoint test that asserted the literal `"box-1"` now asserts the card carries whatever the composer
  reports, which is the invariant it was reaching for. Pgm 751 + Api 79 green. (G137)

- **The filter chips say what the request actually produces (G136)** — `Contracts/ComposeDtos.cs`,
  `Api/Endpoints/ComposeEndpoints.cs`, `Features/Generator/GeneratorTool`. The wool chips could disable a family
  the composer cannot build; the hub and frontline chips were always enabled. But which forms a request produces
  is a property of the **request**, not a constant — it rides on the box sizes the land budget buys — so picking
  **Twin** at 8 players scanned the whole run and reported only *"No boards match these filters"*.
  Rather than predict reachability, the feed **reports what it saw**: a structural census counted over the boards
  each page composed, tallied **before the sieve** so a filter never hides the alternatives it filters against
  (the endpoint test asserts exactly that — filter to `ring` and the other hub forms are still counted). The
  client accumulates it across pages, keyed on the request so a change of players or symmetry starts over, and
  each chip carries its count. A count of zero dims the chip only past a confidence floor of 150 boards, because
  absence from 48 boards is a small sample and absence from 400 is an answer; the chip stays **pickable** either
  way, since a rare form is worth hunting for. The empty grid now names the cause: *"Twin did not turn up in any
  of the 409 boards this request composed — it is not a mix these players and symmetry produce."*
  Measured live at 8 players / `rot_180` over 409 boards: hubs `bar:284 single:125`, five of the seven hub chips
  at zero; wools `i:293 l:111 donut:5` with U/H/Clamp at zero; and **frontline `none:409`** — that cohort never
  builds one at all, which nothing in the UI had ever said. Pgm 751 + Api 79 green. (G136)

- **The two-legged hub on the wide boards — a bounded bay (G105)** — `Compose/HubBoxEmitter.cs` +
  `Compose/TeamUnitAllocator.cs`. With the legs now drawn rather than fixed, the U was still kept off the wide
  form menu and its bay was still whatever the spine had left over — so a wide box came out as two one-corridor
  stubs either side of a chasm, which is the shape that made the U look wrong on a big board in the first place.
  The bay is now **bounded above as well as below**: at least one corridor so the legs read apart, at most
  **two** (`BayCapCorridors`) so a wide spine is spent on the legs instead of swallowed by the gap. That is the
  same bound the frontline's own bay has always kept, so the two branch families now answer to one law rather
  than to two. The sampler draws the **bay first**, because it is the bounded quantity — whatever it does not
  take, the legs must absorb — which is what turns extra spine into leg width instead of leg count. The notch of
  an **L** is not a bay and keeps only the lower bound: it is a corner recess, and nothing sits on its far side.
  `DefaultArms` replaces the old fixed one-corridor-each fallback, which was not a shape the sampler could draw
  above a spine of 8; a spine the laws admit no layout for is refused (a directed null into the rectangle
  fallback) rather than built as a stub. With `SpineArms(2)` added to the wide menu, 20-player branch hubs go
  **17 → 49**, their leg widths **2:13 3:33 4:28 5:13** — only 15% still at a single corridor — and no bay at any
  cohort exceeds 4 (8p `2:52 3:34 4:15`, 12p `2:7 3:8 4:4`, 20p `2:17 3:12 4:9`). 408/408 compose with zero
  unproducible boxes and zero unit findings. Pgm 745 green. (G105)

- **The producibility sweep covers the sampler it draws from (G125)** — `Compose/Producibility.cs`. The search
  collects a sampler's range by *running* it over a fixed number of seeds rather than restating its laws, which
  quietly makes that count a **coverage guarantee**: a layout the composer really draws but the sweep never
  happened to see reads as unproducible, and the report blames the box instead of the search. Measured, 400 draws
  were not enough for the widest space — the frontline's two-leg layout draws bay, end recess, offset and split
  independently, so a 20-cell spine has ~390 outcomes whose rarest first appears around seed 5000, and 400 draws
  covered 59% of them. Everything else (ring walls, hub legs, the single-leg frontline) saturates within fifty.
  The sweep is now **50 000 draws, memoized per space** — the dimensions the sampler reads are all its range
  depends on, so the form, mouth, grouping and lane width a box is retried at reuse one answer — and the
  candidate enumeration **short-circuits on the first exact match**, so the producible case never pays for the
  rest of the space and the deeper sweep costs nothing on it. Guarded by
  `Every_leg_layout_the_frontline_sampler_draws_is_inside_the_search`, which emits every layout the sampler
  yields over 200 000 draws at spines 11/16/20 and requires each to reproduce; it fails on all three at the old
  count. Pgm 748 green. (G125)

- **The split band — two parallel crossings around an island (G116)** — `Compose/MidCarver.cs` +
  `Compose/Composer.cs`. A two-leg frontline was always crossed by one band spanning the hull of its face, so the
  bay between its legs was covered by band that borders neither leg. Where the face allows, the carve now spans a
  **single leg** and lets the symmetry supply the partner: under a rotation the band's own orbit image lands
  beside it rather than on top of it, giving two parallel crossings with the bay left as an untouched island. No
  schema change — the plan still carries one band rect, because every consumer already fans it.
  The mid **reads** the face and never shapes it. Three things are asked of the face and nothing is required of
  it: the image must land across the cross axis (mirrors are refused — the partner would fall back onto the
  original and leave the far leg uncrossed), the face must coincide with its own image, and it must be two runs
  with the axis in the gap. Legs may differ in width and a bay may be any size; such a face simply is not offered
  a split. **And a face that is offered one is equally valid crossed by a single band** — `CrossingDesign.SplitBand`
  is a per-board request, drawn only under a symmetry that could carry one so mirror boards keep their sequence.
  Measured over 900 `rot_180` boards: 69 faces (7.7%) could host a split — the rest refused as 665 single-run
  (a solid Bar face) and 166 unequal-legged — and 22 were carved, the appetite taking about a third of the
  eligible. Islands run 2–4 cells. All 22 split boards are **loop-closed** (the far leg reached through the
  band's image), 408/408 still compose with zero unproducible boxes and zero unit findings. Pgm 741 green,
  4 new `MidCarverTests`. (G116)

- **The unit is placed by its face, not by its hub (G132)** — `Compose/UnitPlacement.cs` + `Compose/Composer.cs`.
  The allocator centres the **hub** on the symmetry axis and then seats the frontline onto whichever hub run it
  finds, so the **face** — the only thing the mid actually docks — ended up wherever hub seating happened to put
  it. Under a laterally-flipping symmetry that is the wrong anchor: the image reflects `v`, so an off-centre face
  and its own image land on opposite sides of the axis and `MidCarver` must span the hull of both, leaving band
  that borders neither front. The finished unit is now **translated across the axis so its face is centred**,
  read off the emitted front-most terrain (a branch front's face is its arm tips, which its box is wider than).
  Nothing inside the unit moves relative to anything else, so the G123 funnel survives intact — a face seated
  off-centre *on its hub* stays off-centre on its hub; what changes is that the **hub** is now the thing free to
  sit off the axis, so the two hubs no longer mirror onto the same lateral position. Mirrors are untouched by
  construction (they preserve the cross axis, so a face already coincides with its image wherever it sits) and
  come out bit-identical. Measured over 900 `rot_180` boards: mutually-faced band 4.6 → **5.5 cells** (59% →
  79% of the span), band span 7.8 → **6.9**, faces centred 470 → **729**, and the two pathological buckets —
  40 boards whose fronts never faced at all, and 48 whose face sat off-centre by more than 2 cells — both go to
  **zero**. The residual is at most half a cell, from an odd face hull whose midpoint falls between cells.
  Reproduction gate holds: 408/408 compose, zero unproducible boxes, zero unit findings. Pgm 737 green. (G132)

- **The frontline face-parity law (G134)** — `Compose/TeamUnitAllocator.cs`. Under a laterally-flipping symmetry
  the opposing image reflects `v` about the axis *point*, so a face spanning `[lo, hi)` coincides with its own
  image only when `lo = -hi` — i.e. when its span is **even**. The hub was already forced even for exactly this
  reason; the face the mid actually docks was not, so an odd face landed half a cell off the centre `SeatFront`
  aims at and the band had to reach past it. The face is now rounded to an even span when the symmetry flips.
  Parity is the whole law — no lane multiple is involved (a 6-cell face is 3 lanes at `w2` and centres fine), and
  because the cell size is odd the rule reads identically in blocks and in cells. Measured over 900 `rot_180`
  boards: faces landing centred 470 → **531**, boards with dead band span 210 → **189**; `mirror_z` bit-identical,
  since a mirror preserves the cross axis and needs no parity. Governs the frontline *box*, so it is exact for the
  solid Bar form; a branch front's face is its arm tips, which the face-anchored placement reads directly (G132).

- **Per-side ring walls — a wider leg is a design decision (G129)** — `Shapes/ShapeBody.cs` (`RingWalls`),
  `Shapes/BodyEmitter.cs`, `Shapes/ShapeEmitter.cs`, `Compose/HubBoxEmitter.cs`, `Compose/TeamUnitAllocator.cs`,
  `Compose/Producibility.cs`. A board picked one lane width and every emitter took one `cw`, so a ring's four
  walls were necessarily equal — an expressive limit, not a simplification: widening one side of a loop is how
  an author says *more player flow goes through here*, and a uniform width can only say that about the whole
  board at once. Now every ring-bodied form (`Ring`, the `P`'s loop, the `G`'s ring, the `DoubleHole`'s, and the
  donut's) takes a **`RingWalls`** vector through one shared wall builder, and widening **spends the box's
  slack**: the wall thickens, the hole loses those cells, the box does not grow. The walls govern the ring's four
  sides only — what is *docked onto* a ring is a corridor, so the P's overhanging bar, the G's L-upright and the
  DoubleHole's U keep a plain `cw`, which is why the widened overloads take both.
  The **sampler** widens one side, drawn evenly from the four, by an amount capped so the widest wall is never
  more than twice the narrowest (the spread law the frontline's arms already keep), only where the hole can
  afford it and stay a corridor wide — 30% of ring-bodied hubs that have the slack. **Producibility** admits it
  without an enumeration blow-up by *running* the sampler over seeds to collect its range, the same way the
  frontline's arm layouts are collected, so the search never restates the law: what the composer can draw is
  exactly what the search accepts, and a ring widened past the cap is legal geometry the emitter builds but the
  search correctly refuses. Emit is byte-identical at uniform walls (the body fingerprints are unchanged).
  Measured over 408 boards: 408/408 compose, 20.5% of ring-bodied hubs come out widened, and the reproduction
  gate still reports **zero** unproducible boxes and zero unit findings. The hole-hub exemplar's ring, the
  standing example, moves from *"nearest Ring, 2 cells differ"* to producible as **`Ring walls 2/3/2/2`**.
  Harnesses: `tools/compose/reproduction-gate.cs`, `tools/compose/exemplar-feasibility.cs`. Pgm 737 + Api 76
  green. (G129)

- **The partial, shifted frontline face — the funnel dock, scalar half (G123)** — `Compose/TeamUnitAllocator.cs`
  + `Compose/Composer.cs`. The frontline's face was **pinned to the hub's full front width**
  (`faceWidth = max(w, hubV)`), so it spanned the whole edge and `SeatInRuns` had exactly one legal position:
  every board's front met the mid identically. Now the width is **sampled** and the face **seats by contact
  patch** — `SeatFront` accepts any position abutting the hub over at least one lane of a free run, which admits
  a face narrower than the edge (seated anywhere along it) and one **overhanging** either end. The payoff is the
  frontline as a flow funnel: the mid meets only part of the hub front, so the two onward routes around it cost
  differently. Two knobs, because width and position are different decisions — a face may be partial and still
  centred; **centred is the default** and sliding it is the sampled exception, since the slide is what costs the
  band. The joint carries the **clipped** abutment, not the face width, so the filler's offer stays honest under
  overhang.
  The parallel-fronts gate is relaxed from per-face mirror symmetry to **bounded band slack**
  (`Composer.FrontHullSlackCells`): `MidCarver` spans the band across the hull of *both* images' faces, which
  makes it self-symmetric by construction, so an off-centre front never makes the band impossible — it makes it
  wider than the front it docks, and that excess is what BZ9 actually bounds. The old test asked every face to
  have its mirror among the faces, which only a centred front can satisfy, and so rejected fronts costing the
  band nothing (a twin front with unequal legs, an asymmetric hub form).
  Two guards the relaxation needed, both found by the invariants rather than by inspection: an **overhanging**
  face keeps the neighbour separation gap from seated spawn/wool boxes (past the hub's corner no hub cell
  bridges the meeting, so corners would meet as a bare pinch), and a face end may not land where the hub's edge
  goes from filled outside it to empty inside — a hub bay starting exactly at the face's end (`PinchesAtEnd`).
  The pinned full-width face needed neither: it ended at the hub's own corners. Measured over 400 seeds:
  400/400 still compose, 26.8% of frontlines now take a partial or shifted face and 14.2% overhang the hub, and
  the composed output still reads zero unit findings and zero unproducible boxes — the G125 reproduction gate
  holds through the change. On the hole-hub exemplar both of its G123 blockers clear, leaving only its unrelated
  scale anomalies. (G123)

- **The spanning dock — a face anchored on every shoulder (G123)** — `Compose/TeamUnitAllocator.cs` (`Docks`) +
  `Compose/Producibility.cs` + `tools/seeds/shifted-frontline-spanning-dock.plan.json`. The contact-patch seat
  admitted a face if **some** patch was a lane wide; a face reaching across a bay-fronted hub's bay (a G, U or L)
  therefore only had to hold one side, and could rest on a sliver on the other — cantilevered over the hole.
  `Docks` requires **every** contact patch to be a lane wide, which is what seals the bay into a declared hole
  rather than leaving a lip over it; on a solid front there is one patch and it reduces to the previous rule.
  The producibility read gained the same law (`frontline-shoulder-too-narrow`), measured on the hub's
  **front-row terrain** rather than its box — a bay-fronted hub's box spans the bay, so a box-level overlap
  reports one wide contact where the face actually lands on two shoulders, which is the distinction this turns
  on. Measured over 1200 allocations: bay-spanning seats resting on a 1-cell shoulder went 13 → 0, all 45
  remaining hold ≥2 per shoulder, and 400/400 still compose with the partial-face (26.3%) and overhang (13.9%)
  rates unchanged. New exemplar `shifted-frontline-spanning-dock.plan.json` — the g-hub funnel duplicated at the
  composer's own corridor width (every wall and lane 2 cells, not 1) so it isolates the shifted frontline from
  that trace's half scale; it reads **producible end to end**, every box and no unit finding, and is the green
  target the half-scale original could never be. Pgm 722 + Api 76 + Geom 66 + 148 JS green. (G123)

## Sketch world-folder export (P9) — a playable `.mca` world for sketch-originated maps
- **A whole map from one JSON spec.** `tools/mapgen` (`README.md`) takes a spec — the board, the paint, the
  interior elevation, the trees, the buildings, and the houses the wool and spawn rooms are raised as — and
  writes `region/` + `level.dat` + `map.xml` through `SketchWorldBuilder`/`IntentGenerator`, so it can only
  contain what an author could draw. The board comes either from the layout generator (`compose`) or from a
  literal plan document; `objective_mode` **retargets** the goals the generator placed (`ctw` / `dtm` /
  `dtcm`) rather than adding a second generator, since a wool room, a monument and a core are one team's
  thing to defend in the same slot of a board. Themes name palette families, never block ids; relief takes
  the sketch's own mark vocabulary or scatters point marks; trees and buildings are placed against the
  **rasterized ground** and reported by what reached the voxels rather than by what was requested. Builds
  are deterministic. Eleven maps built with it (`tools/mapgen/specs/`). (`B21` groundwork · `S41` relief ·
  `G172` houses)
- **A single island is a map.** `POST /map/{slug}/sketch/finish` no longer demands two islands ("a CTW needs
  both sides"). An island is a connected landmass, not a side: over the 320 readable worlds of the
  destroy-the-monument corpus **17% are a single island and 26% carry a single major one**, and the
  generator's own thirty-player boards compile to exactly one — so the commonest shape in that category was
  refused. Symmetry decides whether a board has two sides and is stated in the setup; the gate now refuses
  only a layout that rasterizes to no ground at all.
- **Terrain finish — walls, rims, plateaus.** `TerrainPainter` (`PgmStudio.Minecraft`,
  `docs/world-export/terrain-painting.md` TP1–TP9, TP11–TP13) dresses the raw stone as the last world pass: a quartz
  **rim** on the top-most block of every edge (8-neighbour, so a reentrant corner never gaps), a **team-tinted stained-clay wall** on the exposed riser between bedrock and rim, a **surface** stack on the interior top,
  and stone left as **fill**. Built as the four-stage architecture —
  `TerrainProfile` (the theme-agnostic classifier), a per-cell `TerrainTheme`, the pure band resolver, and
  the `TerrainMaterial` seam. Touches **only stone**, so bedrock and every
  stamped structure (room plateaus, the bedrock approach wall, objectives) are consulted as height-bearing
  neighbours but never painted (TP6). Every per-column knob is themeable: **rim/surface depth** live on each
  bucket's `TopBand` (TP7/TP11 — the default surface is grass over two dirt, three deep), **bedrock thickness**
  is absolute or terrain-relative (TP8), **wall-on-terrain-faces** and the **rim/wall/surface toggles** route
  down the fill fallback chain (TP9/TP12), and **`rimEdges`** picks which of the three nested edge tests the rim
  caps (TP3): `void` — only where the footprint meets the void — `drop` (the default: void or lower), or
  `boundary` (the whole plateau outline, a face against a structure included). `void` is what a body built out
  of stacked shapes wants: a staircase of plateaus is a drop at every tread, so the default lips each one and
  the body reads as five plateaus that touch, while `void` caps the outside alone and it reads as one body.
  The wall is unaffected — it asks its own face question — so a tread's riser is still walled.
- **A house stamper, and the monument lifted out of the spawn cube (WX10, WX11).** `HouseStamper` builds a
  shell meant to be looked at: a sill one block proud of the walls, four corner posts with infill between,
  gable ends carried to the underside of the slope, and a gabled roof bordered by a verge, its ridge running
  the long way. Three roof facts are enforced by tests that ask about shape rather than contents — a slope
  climbing a whole block is laid in **whole blocks** (a slab fills half its cube and leaks while staying solid
  to a flood fill, so `BlockRoles.IsFullCube` is the check), the **overhang is part of the slope** and falls by
  the pitch like every other course, and a step over one course carries its own riser. Doorways are never under
  2×3. `MonumentStamper` moves the pedestal/placement-cell/cap/sign out of `SpawnCubeStamper` unchanged, so a
  wool room, a house or open ground can carry the same monument.
- **A wall frame, inked by how far the outline turns (TP19).** `WallFrameMaterial` takes a wall's top and
  bottom courses plus the corners of the shape it wraps, filling the panel between. A corner is decided by the
  boundary's **turn** rather than by a change of direction, so a staircased edge is not mistaken for one: at
  45° a rectangle's right angles ink, an octagon's vertices ink to a single cell each, a shallow bend does not,
  and a disc reaches the threshold nowhere — so it has no corners and the frame falls back to a layer stack.
  The same angle sets how far the ink wraps each corner, since the turn ramps to a vertex. `ColumnProfile`
  carries the per-column turn and `BucketContext` a cell's height above its band's foot; a room shell answers
  the same question closed-form as `atan2(window − d, d)`.
- **Beam ends past the corners, and the laid-log course they belong to.** Where two storeys meet, a
  `BeamStyle` runs log ends out past each of the four corners — two per corner, one along each axis, eight in
  all — each showing its **sawn end**, which is the one place on a building where a cut face outward is the
  point rather than the mistake. In plan the seam reads as a hash with the walls as its middle: a building made
  by laying logs against each other. The course *inside* the wall is a separate thing and needs no new
  machinery — it is an ordinary course of the wall's stack laid in the new `LaidLogMaterial`, a log that
  follows the wall's own run so its bark faces out and stands upright only at a corner, where no lying log
  could show bark to two faces at once. Keeping the two apart lets a beam course run in one material and its
  ends in another. It is also **the one thing a house writes outside its own footprint**, which is why a style
  has to ask for it.
- **A roof that climbs half a block at a time (B69).** `RoofField` is measured in **halves** rather than
  courses: a whole-course roof simply steps two at a time, so the six forms' arithmetic is untouched and a roof
  laid in cubes answers exactly what it always did. Naming a `RoofSlab` on the style puts the roof on half
  courses — it climbs half a block per block travelled and lays that slab on every odd step, with the style's
  own `Roof` filling the cubes between. It is the gentler slope a slab is actually for: at a whole block of
  rise a course of slabs leaves an open half between every pair and the roof can be seen straight through. A
  block id rather than a material, for the reason a window's is — which half of its cube a slab fills is
  geometry. The eave needed floor division rather than truncation, since its rise goes negative below the base
  plane and a truncating halve lifts the overhang clear of the slope it belongs to.
- **A window that is just an opening (`WindowForm.Open`).** Cut and left, distinct from `None`, which cuts
  none at all. Its size is entirely the author's, since no form is imposing one.
- **A log checkerboard (TP20).** `LogCheckerMaterial` lays a checkerboard with **one** log and varies how it is
  turned rather than what it is made of — upright on one square, on its side on the next — so the grain runs
  vertically and then across and a single block reads as a woven board. Its own kind rather than a checker over
  two solids because the two squares are one block and two orientations, and an orientation is not something a
  solid can carry: a log's data nibble *is* its axis, so a material resolving that nibble from the cell's
  coordinates turns every log the same way and paints a flat patch of wall. **A log on its side lies along the
  wall, never across it** — the axis decides which two of its six faces are sawn, and a log laid across a wall
  puts one straight out at the viewer. That needed a third perimeter fact beside the arc and the turn: the
  **run** (`GridBoundary.RunAt` / `RunsBothWays`, carried on `ColumnProfile` and `BucketContext`), the axis the
  wall is going where a cell sits, taken from the chord over the same window the turn is measured on. A corner
  has faces on both axes and no lying log shows bark to both, so it stands — which is what a timbered corner
  post is anyway. Off a wall the squares read as bark against sawn end, which is a log floor. The rule was read
  off `alpine_mining_ii` rather than guessed: every horizontal acacia log in its z-facing house walls is axis-x
  and none is axis-z.
- **Diagonal wall stripes and a checkerboard (TP17).** `WallDiagonalMaterial` shears a wall-run's stripe cycle
  by starting each course `Slope` cells further round the perimeter than the one beneath — slope 1 is 45° on a
  square-blocked face, larger lays it flatter, negative leans it the other way, 0 is the vertical run — reading
  world height so unequal walls meet with their diagonals in line. `CheckerMaterial` alternates two materials
  over squares laid **in the face it paints** (arc × height on an outer wall, x × z elsewhere), floored rather
  than truncated so the squares keep their size across the origin. Both are offered in the picker with their own
  panels. Same commit fixed `TerrainThemeComposer.KindOf`, which named only six of the nine kinds — a cell, a
  turbulence and an electric each fell through to the default and filed themselves in the `style.kind` column as
  `solid`, the one column a style library is queried by.
- **Terrain-paint patterns + theme JSON (TP13).** Any bucket's material can be a **pattern** at the same seam as
  a solid, in two families plus the wall's own. **Region:** `VoronoiMaterial` takes an ordered list of bands
  measured **inward from the cell boundary** — band 0 sits on the boundary and draws the grid as one connected
  network, each later band is a ring further in, the last takes the middle, and a cell too small to reach a band
  never shows it, which is what gives cell size a meaning. (Depth is the Worley `F2 − F1` gap, whose contours are
  hyperbolic, so the inner bands round off the cell's corners while the outline stays sharp.) `CellMaterial`
  takes the same regions and gives each *whole* region one material, having warped the lookup and loosened the
  sites off their grid — a fabric of organic patches where voronoi draws a diagram. **Field:** `NoiseMaterial`,
  `TurbulenceMaterial` and `ElectricMaterial` cut a fractal field into an N-stop ramp and differ only in how
  each octave is bent — left alone (cloudy), folded at every crossing (billowed, marbled) or ridged (thin
  branching filaments). Only neighbouring stops share a boundary, so a stop list reads as a ramp rather than a
  set of patches. The sum is normalised by its own deviation rather than its amplitude total, which holds the
  spread constant as octaves rise: dividing by the total averages samples, so the field used to crowd towards
  its middle and the first and last material an author named all but vanished (1.0% each at five stops, three
  octaves). **Wall:** `WallRunMaterial` (N stripes of any widths that wrap the **void-facing
  perimeter**, reading a per-column arc from `TerrainProfile`'s Moore boundary walk over each landmass — a walk
  that now stops when its loop closes, detected as the first repeat of a (cell, entry-direction) state rather
  than by agreeing on which state means "back at the start". The earlier Jacob's criterion never fired on a
  plain filled square: every trace ran to a millionth-iteration backstop, charging a flat ~110 ms per landmass
  whatever its size while still returning the correct ring, which made it invisible to every output assertion
  and the largest single cost in terrain painting);
  vertical wall bands are `LayeredMaterial`. All deterministic (hashed from a seed + cell, no RNG) and nesting —
  a pattern entry can be a team tint or another pattern. The whole theme serializes through **`TerrainThemeJson`**
  (one `kind` discriminator per material), closing the material model for the scoped-theming step. (G157)
- **Build / Carve is one control (S37).** Which way a drawn shape goes was two square icon buttons in a row
  of nine, tinted when active — a visual peer of the tool that draws rather than a property of what is about
  to be drawn, which made the most destructive state in the editor the least legible. It is now **one pill**,
  ruled off from the tool group, that names the mode it is in and flips to the other when clicked, coloured
  the way the canvas fills that operation. The strip therefore ends up **one control shorter** than it was: a
  two-state thing needs one button, and a toolbar this dense cannot afford a second copy of anything. No
  icon — every other control there is a glyph, so the one that is not reads as the different kind of thing it
  is, and a lucide glyph is swapped in once at mount, so an icon that changed with the mode would go stale on
  the first click. It dims when the tool in hand does not draw (rather than disabling — setting the operation
  before reaching for a tool is a reasonable order to work in). The wire words are untouched
  (`add`/`subtract`); only the label changed, because it should say what the next shape does to the land
  rather than name a set operation. The dead `.draw-tool-label` / `.draw-tool-btn--op-*` rules are gone and
  the separator is the subbar's own `.subbar-sep` rather than a twin of it. Shown in the `/design` catalog and
  gated by a new e2e spec (`tests/e2e/draw-tools.mjs`, 11 checks: exactly one operation control with nothing
  else repeating its state, dimmed on move and measure, awake on rectangle, a click flipping it each way, and
  the mode surviving a tool change).
- **The paint palette is grouped by tone family, and a family fills a pattern in one choice (TP16).**
  `TerrainPalette`'s taxonomy groups — Rock, Earth, Wood, Mineral — were replaced by the **19 tone families over
  72 full blocks** the surface analysis measures ground by, so one table now serves both: what an author paints
  and what a report names cannot drift apart. A taxonomy could not do the job a pattern needs — Rock held stone,
  obsidian and bedrock together, which is true about rock and useless as a palette for a field. Under every
  material list (a stack's layers, a voronoi's bands, a cell pattern's patches, a field's stops, a wall's
  stripes) sits **Fill from a family**: it lays one entry per block in the family's light-to-dark order,
  replacing what the list held, and the author removes what that ground does not use. The select reads the
  family a list currently holds, computed from the entries rather than remembered, so it names the family until
  the list is narrowed and returns to an offer once it is. The three sixteen-shade colour families (stained clay,
  wool, stained glass) stay as they were and keep their swatch row; a shade a tone family claims is offered under
  that family, and the row still reaches all sixteen. Blocks no family names — bedrock, iron and gold blocks,
  logs, plain glass — left the shortlist and are reached by typing the id/data pair, which the picker has always
  accepted. Gated by six new palette tests: a variant takes the family of the ground it reads as and not of the
  id it shares (granite is brick, andesite is grey stone), every family member is offered so a filled list can be
  re-picked, no block belongs to two families, and the in-family flag matches the group exactly.
- **Area patterns carry a rise, so a wall is not a stripe (TP15, S35).** Every area pattern — both region
  patterns and all three field ones — gained a **`Rise`**: the vertical period of its field in blocks, or 0 for
  none. A pattern of the plane answers a whole column at once, so it decided the ground and left every wall face
  as vertical stripes — which is what a wall-run draws on purpose and what an area pattern drew by accident. A
  positive rise samples the field over the **volume** instead, so the wall and the fill carry the fabric the
  surface does, and a room shell, a boulder and a water bank get it too (all three already pass a real Y).
  `Voronoi` gained a volume form searching the 3×3×3 neighbourhood, with the vertical axis measured in cells so
  a flat cell's gap grows at the same rate in every direction rather than making every boundary a horizontal one;
  `PatternNoise.Value` became anisotropic (its own vertical period, because terrain is a slab — hundreds of
  blocks across and a dozen tall) and `Field` grew a volume form. **A volume octave carries its own mean and
  deviation** (measured: plain 0.4996/0.1866, billow 0.3061/0.2136, ridge 0.5271/0.2735) — trilinear
  interpolation averages eight lattice corners where bilinear averages four, so reading a volume through the
  plane's numbers would crowd it towards its middle and starve the outer stops, exactly the collapse the
  normalisation exists to prevent. **Off by default**, and a stored theme therefore repaints identically: it is
  the more expensive field (measured over 480,000 resolves, one whole board's paint: a volume voronoi
  **1114 ms** against the plane's **331 ms**, a three-octave volume field **266 ms** against **128 ms**) and a
  one-to-three-course surface has nothing to vary, so it earns its cost on the buckets that are tall. Authored
  as one more scalar beside patch size and seed; the section preview, which already varies Y, now shows it. Tested: a flat pattern varies nowhere in a column and a risen one varies
  everywhere, a risen field keeps every stop above 4% at one octave and at five, a risen voronoi still cuts to a
  connected grid at any height, and a rise changes what a material *is*.
- **A path and a boulder are finished with a material, like everything else (S36).** `PathProp.Blocks` (a raw
  `(id, data)` list) and `BoulderProp.BlockId`/`BlockData` are now a single **`TerrainMaterial`** each —
  `Pave` and `Rock` — edited by the same `MaterialEditor` a theme bucket and a water bank already were, so a
  road can be a cell fabric or a noise ramp and a rock can be mottled. A boulder's rock is resolved in the
  **boulder's own frame** (offsets from its anchor), which is what keeps a mirrored pair one rock: resolving
  against map coordinates would hand two teams the same shape in different colours. Its depth is measured down
  from the rock's own crust, so a layer stack reads as a weathered skin over a core. **`PathStyle.Cobble` is
  retired** — it tiled a path's several blocks over a jittered grid, which is precisely `CellMaterial`, so it had
  become a mode of the stroke saying what the material vocabulary already said; `StrokeCell` lost its shade
  channel and a stroke now decides the shape of the band and nothing about its finish. Stored dressing upgrades
  on read (`DressingJson`, the sibling of `TerrainThemeJson.Upgrade` and delegating to it for the materials): a
  boulder's block pair becomes the solid it always was, a cobbled path becomes a `CellMaterial` over the *same*
  grid and salt it was already tiled by with its style falling back to `solid`, and any other path takes the
  first block it actually spent. The shape cards are now drawn in the author's own material (`?pave=` / `?rock=`
  carry it), so a picker answers "what would mine look like shaped that way". Tested: the mirrored pair is
  identical block for block at every offset, a layered rock weathers its crust, and both upgrades land.
- **Scoped per-piece theming + the Theme rail (TP10).** The terrain paint is resolved **per cell** instead of
  one theme map-wide: a piece override, its box/collection, else the map default — winner-takes-all (whole
  theme). Mirrors the team-ownership shape: the plan carries a theme registry + `mapTheme` + ordered scope
  assignments; `PlanCompiler` bakes them into the intent as the theme-JSON registry, a priority-resolved flat
  `pieceId → themeId` (boxes/collections expanded to member pieces), and the fanned piece footprints.
  **`TerrainThemeScope`** (the read side, `TeamTerritory`'s sibling) turns those into a per-cell `themeAt(x,z)`
  the painter reads (smallest footprint wins an overlap); `SketchWorldBuilder` paints through it. Boxes stay
  pure annotation — expanded to piece ids at compile, never read at export. Authored on the plan tool's new
  **Theme** rail (`PlanThemePhase` + plan-bridge theme methods), two steps: **Create** defines named themes and
  **previews each one's materials** (rim/wall/surface/fill swatches server-rendered through the real materials +
  `BlockPalette`, so a voronoi / noise / wall-run reads at a glance); **Apply** assigns themes on the real plan
  canvas (its own line below). SVG bucket previews via `TerrainPreview` + `/api/terrain/theme-preview`. Built
  from the existing design system (Section/Field/ListRow/Badge). Tested: compiler bake + scope resolution. (G157)
- **The Apply step is the plan canvas, not a list of dropdowns (G158).** The Theme rail's second step reuses the
  mounted plan canvas as a **read-only theme-assignment surface**: click a box, double-click to drill into a
  piece, **Ctrl-multiselect** a set of shapes, and assign / remove a theme on the selection from a left rail
  (map default · theme picker with its swatch preview · the selection with each shape's current theme). The
  **precise paint** the export would place is shown live as a **world-aligned canvas overlay** — the same
  `TerrainPreview.MapSvg` render (now returning its block-space bounds, background rect dropped so the void stays
  transparent), blitted in the plan's own frame and refreshed on each assignment; no client voxelization.
  `PlanThemePhase` is Create-only; Apply is a `PlanTool` mode hosting `PlanThemeApplyRail`; both flow-bar steps
  read `Theme | Create Apply`. Verified in-app (0 console errors) + full e2e. (G158; commits 645a092 / f186c63 / 0bba711)
- **A theme is authored as a form, not as JSON (TP14).** The Create step's textarea is gone: a theme is now
  **one section per paintable bucket** — rim, surface, wall, fill — each carrying whether it paints at all, how
  many courses it claims, and a **`MaterialEditor`** that switches the bucket between every material kind and
  **recurses into the materials a composite nests** (a stack's layers, a tint's neutral fallback, a voronoi
  palette, a noise ramp's stops, a wall run's stripes), each entry addable and removable, each pattern's
  scalars — patch size, scale, octaves, seed — an ordinary field. A new pattern arrives with two entries whose
  blocks sit far apart in the palette, since one entry (or two blocks that share a colour, as stone and
  cobblestone do) renders flat and reads as broken. Blocks come from **`TerrainPalette`** over
  `GET /api/terrain/blocks` — a curated offer list named and coloured by `BlockPalette`, the same table the
  preview and surface render use, so a picker swatch cannot promise a colour the export will not place — and
  the three sixteen-colour families are offered as **one line plus a colour row** rather than forty-eight
  dropdown entries. The editor mutates the **theme JSON node itself**, so there is no second model of a
  material to fall out of step with the painter's, and every edit re-renders that bucket's swatch through the
  real materials. The JSON stays, collapsed, as a read/write escape hatch. Tested: every offered block is one
  `BlockPalette` knows, the families are whole, and the shipping default theme is expressible in what the
  picker offers. (G158, TP14)
- **Terrain team tint.** The **team tint is a general material** — the wool 0–15 damage scale on
  any colour-by-damage block, usable on **any** bucket and composable in a layer/pattern (`BucketContext`
  carries the cell's team) — the default puts it on the wall. Ownership resolves through **`TeamTerritory`**:
  one shared decomposition on the canonical `IslandDetector` islands (the ids `islands_json`/configure use),
  each island owned by a stored `IslandTeams` value, else a spawn's team on it, else a wool's owner, else
  neutral — pre-filled once at `/plan/compile` and read at export, so the tint matches what configure
  assigns. Wired map-wide into `SketchWorldBuilder.Build`; unit-tested per column and over built worlds. (G157)
- **Anvil write side** — `AnvilRegionWriter` + `LevelDatWriter` (`PgmStudio.Minecraft`): emit the 1.8–1.12
  numeric Anvil format (region sector/location table, zlib chunks, nibble-packed `Blocks`/`Data`/`Add`
  sections; gzipped `level.dat` with world spawn + a real creation timestamp), the mirror of the read-only
  `AnvilRegion`. Write→read round-trip tested. (P9a, P9b)
- **World synthesis + stampers** — `SketchTerrainBuilder` (bedrock floor at y=0 + stone fill from the sketch
  columns, reporting each column's surface top), the shared `CubeStamper` room shell (floor · walls · roof,
  then the pad and doorway over them), `WoolCageStamper`
  + `WoolCageChests` (two-chest corner loadout), `SpawnCubeStamper` (spawn cube + auto-wired monuments:
  bedrock pedestal · air cell · wool-colour glass cap · label sign, placed by captured-wool count),
  `ObserverPlatformStamper` (solid 6×6 platform + four inward info boards), plus `SignBuilder`/`ChestBuilder`
  and `PositionSnap` (half-block-lattice X/Z, `ymax` Y, yaw→door facing). (P9c, P9d, P9g, P9h, P9i, P9j, P9l)
- **Adaptive room frames — the piece sizes the stamp.** One `RoomFrame` per wool cage / spawn room
  (`PgmStudio.Domain.RoomFrames`, the `WX1–WX7` rules in `docs/world-export/structures.md`): the shell
  footprint is the role piece inset one block (10×10 → the original 8×8; minimum 8×8-block piece, WX2), the
  marker's lattice parity picks the always-square pad (2×2 on a grid line, 3×3/1×1 on a block centre) with a
  one-block wall clearance and minimal inward shift, the exported spawn/wool point follows the pad (WX5), and
  doors are cut on the entry interfaces — terrain↔room land seams **and abutting build zones**, which also
  carry the ST1 entrance redstone — at wall-parity widths (odd → 3, even → 4, 2 at the 4-across minimum;
  door ≤ interior − 2, so door-wall monuments are never exposed). Monument seats and chest corners derive
  from the interior (`MonumentSlots`/`InteriorCorners`), so capacity scales with the room and the validator
  refuses over-capacity plans, plus WX2/WX3/WX6 refusals and the WX4 shift lint; the structure preview
  consumes the same frames (`SketchWorldBuilder.WoolFrame`/`SpawnRoom`), so it cannot disagree with the
  build; the composer legalizes emitted markers onto the lattice (`Composer.LegalizeMarker`, `box-4`).
  Markerless/plain-piece intents keep the legacy marker-anchored default shell. Spawn-piece **iron**
  resolves beside the room, never fused (WX8): the shell yields one edge (largest retained area, ties
  breaking mirror-consistently away from the marker), the cube degrades by parity (grid line 4 → 2, block
  centre 3), one block of air always between them, and the renewables wiring covers the resolved
  footprints; an unfittable marker resolves **unplaceable** (WX9) — the room stamps alone, the preview
  draws nothing for it, and the WX8 lint flags the marker with the clearance requirement. Placeability is
  the general contract the objective-separation rules reuse (B37). (G31, WX1–WX9)
- **Room styles — the shell is a course stack, the piece is still the footprint.** `RoomStyle`
  (`docs/world-export/structures.md` §7) finishes a shell without touching its geometry: floor, walls and
  roof are each a `RoomPart` — a stack of `RoomCourse` materials plus how far the part runs, read from the
  part's own base outward (a floor **downward** so the pad and its exported point never move, walls and roof
  upward) with the last course repeating, so height, floor depth and roof thickness are knobs rather than
  fixed layer indices. The band and the light slit became ordinary courses (a `TeamTintedMaterial` one and an
  air one), which retired `CubeKind`: a wool cage and a spawn cube are two bound styles, not two code paths.
  A part's air course is a **gap** (skipped, so no style can erase another stamp) while a doorway's air is an
  **opening** (written, cutting the door out of the wall). The roof adds a thickness, an optional centred hole
  measured on the shell, and a flush-or-overlapping **eave** — free of any new rule, since a shell is its
  piece inset one block (WX1) so an eave lands exactly on the piece boundary. Doors are the closed
  `Domain.DoorMaterials` set (air · cobweb · stained glass · panes), one row read by the stamper for its block
  **and** by `WoolGenerator` for the PGM material its block whitelist must name — a door the filter does not
  name would seal the cage. A spawn's door is pinned to air. The shipped styles rebuild the shipped shell
  block for block, held by a golden over the whole volume. (G34a)
- **Room-style library — the third tab.** A room style is a browsable row composed from the same `style`
  shelf a theme composes from (M0012 `room_style` + `room_style_course`, `/api/room-styles`, authored at
  `/library/rooms` — `docs/world-export/structures.md` §8). One difference in shape carries the distinction: a
  `theme_bucket` binds one style to a bucket, a `room_style_course` binds one to a part **at a position in
  that part's stack**, since a wall is a band over bedrock over a slit. The stack is stored under a unique
  (room, part, ordinal) index and rewritten wholesale on save; a part with no courses keeps the built-in
  finish, as an unbound bucket does. Both card pictures are stamped by the real `CubeStamper` over a sample
  frame and read back (`RoomStylePreview`) — the roof and its eave read from above, the course stack and the
  doorway from the side as a `BlockSideView` projection — so a card cannot promise a shell the export would
  not build. The door picker is served from `Domain.DoorMaterials` rather than restated in the client, which
  is the one way a door could be offered that the wool-room filter never whitelists. A style bound by a room
  is named in the 409 that refuses to forget it, beside the themes. (G34b)
- **A map binds its two room shells — the Theme phase's Rooms step.** A sketch snapshots one cage shell and
  one spawn shell into its layout under `roomStyles` (`docs/world-export/structures.md` §9), picked from the
  library on a third Theme step beside Create and Apply, since a shell is what the map is *made of* and is
  decided once for the whole map. There is **no per-room override**: a room is fanned across the symmetry
  orbit, so a shell that differed between the teams' cages would be a sightline one team has and the other
  does not. The binding is a **snapshot, not a reference** (`docs/tools/library.md`) — no `style_id` is
  stored, so a later library edit cannot rebuild a shipped map's rooms. `RoomStyleScope` is the read side,
  `TerrainThemeScope`'s sibling with the one shape difference that says the whole thing: a theme resolves per
  cell, a room style per map, so there is no `StyleAt` and nothing for one to take. An absent or unreadable
  snapshot falls back to the built-in shell, so a map that never opened the step exports exactly as it did
  before the step existed. Serializing a style also forced structural equality onto every material holding a
  collection (`RoomPart`, `LayeredMaterial`, the three patterns) — record equality compares a collection
  member by reference, so a stack read back from JSON had never equalled the one that wrote it. (G34c)
- **A shell became a house — six roofs, a floor in zones, windows and a porch (G34d).** The room style now
  decides four things beyond its materials (`docs/world-export/structures.md` §7.1–§7.4), and the picture of
  all of them is `tools/compose/house-showcase.cs`, stamped by the real `HouseStamper` and read back out of
  the world. **The roof is a height field** (`RoofField`): for each cell of its plan, the course that column
  tops out at and how many courses it writes to close the step down to its neighbours. `Flat`, `Gable`,
  `Hip`, `Gambrel`, `Shed` and `Saltbox` are one loop and differ in a single formula over the same two
  distances, and a hip over a square is a pyramid because the ridge is the run the longer side has left over.
  Two rules generalized with it: a column's riser is its deepest step down, so a pitch above one no longer
  leaves the slope open between its treads; and **the walls climb to meet the roof** wherever it stands above
  them, which retired the gable's own end-wall pass and is what closes a shed's back wall and flanks. A
  `RidgeCap` lays the line the slopes meet on in the verge. **The floor is divided in plan as well as in
  depth** (`FloorSurface`): a border ring, a field, and a centred inlay, keyed on how far a cell stands from
  the walls — zoning lives in the shell rather than in a material because a material resolves from the cell's
  own coordinates and cannot know where the walls are, so anything that is a *pattern* stays a material bound
  to the field. **A porch is taken out of the footprint, never added to it** (`PorchStyle`): the footprint
  comes from the piece (WX1), so the walls stand back from one wall of it and the strip they gave up is a
  deck with posts, a rail and its own canopy — the doorway is carried onto the wall's new line, the rail
  breaks where that doorway crosses it, the canopy is seated by where its *ridge* lands (one statement for
  all six forms), and where the room cannot spare the depth the porch is the part that gives way.
  **Windows are cut, and chosen as a block id** (`WindowStyle`/`HouseWindows`): a 2×2 stair lattice whose four
  stairs turn their raised halves outward so the quarters they are missing meet as the light, a slab-sill /
  slab-lintel band, and ordinary panes. Seats are spread evenly and centred on the run between a wall's two
  corner posts, and a seat meeting a doorway is dropped rather than shifted. The block is an id rather than a
  bound style — the one place a shell departs from the library's shape — because a stair's metadata is which
  way it climbs, and a material resolving data from where the cell sits would turn all four the same way.
  M0016 carries the new knobs; `border`/`field`/`inlay` are three more parts a course may bind, so the floor's
  zones needed no column. (G34d)
- **A building is a dressing prop — drag a rectangle, raise a shell (G34e).** A house no longer only appears
  where a wool cage or a spawn cube does. `HouseProp` is the dressing stage's sixth prop
  (`docs/world-export/decoration.md` §8): an author drags a footprint in the Dressing phase and the same
  `HouseStamper` raises the same `HouseStyle` on it. Rooms are untouched — they still resolve their frames from
  plan pieces and markers (WX1) and stamp before the painter, carrying the pad, monuments, chests and entry
  contract a prop has none of; a building is scenery a player walks into, stamped after the painter with the
  rest of the dressing. **The rectangle is a third interaction** beside the marker's click and the outline's
  trace, stored as its **two opposite corners** so the orbit fan mirrors it as the shape it is — a quarter turn
  swaps its width and depth with nothing told to swap them — and the **door turns with it**
  (`DressingSymmetry.TurnEdge`), or a mirrored pair both open toward the same half of the map. It is
  deliberately **not gated on the protected mask and never joins it**: that mask tells a *scatter* where not to
  grow, and a building is not scattered — someone drew this rectangle, and a refusal would silently drop a
  placement they can see. Its cells do join the pass's running claim, which is the different rule that stops
  grass growing through the walls. Ground is physics rather than policy: it seats on the lowest column of its
  own footprint so it settles into a slope, and an image over the void raises nothing. It is bounded at both
  ends — the stamper's own three-block floor, and a **192-block footprint ceiling** (three times the 8×8 shell a
  wool cage is stamped in, so a 12×16 house is buildable and a 20×30 one is not) that is the prop's alone, since
  a room's footprint comes from its plan piece and is no dressing limit's business. The cap is an area rather
  than a side length so a long low building is as buildable as a square one; it bounds what a building costs and
  how much map it covers, and **height is bounded separately and by the roof** — every form's rise is measured
  over the building's shorter side. The shell is a snapshot on the prop, not a library id (structures.md §9). **The porch's canopy is seated by its own lowest course**
  clearing the doorway rather than by the eave above it — one statement for all six canopy forms, and the one
  that survives a tower: a wall is a `RoomPart` extent whose last course repeats, so a twenty-four-course
  building is an ordinary style, and a canopy chasing that eave rode the wall the whole way up as a colonnade
  with the door it fronted left open to the sky. (G34e)
- **The gable face is its own part (G34f).** The triangle a sloped roof leaves standing at each end of a
  building was welded to the wall's top course, so the one thing nearly every hand-built house on the corpus
  does — a timbered or shingled gable over a plain wall — could not be said. `HouseStyle.Gable` and the
  `gable` room part name it: unbound it is the wall's top course carried up, so every stored style builds
  exactly what it always did. The reason it cannot be a course of the wall instead is that **the wall's stack
  has run out by then** — the courses end at the wall's top, so a wall that bands as it rises goes flat the
  moment it turns into a gable and has nothing left to say about the face. `room_style_course.part` is a free
  string, so it needed no migration. Documented against its neighbour in the same figure: the **verge** is the
  roof's own outermost ring, which on a flush roof is the raking edge directly over the gable and under an eave
  moves out to the overhang, leaving plain roof along the wall line. (G34f)
- **A building is a stack of storeys (G34g).** A house's height was a wall height; it is now a stack of rooms.
  A `Storey` states its **clear** — the blocks of air a player stands in — and the courses follow: one more
  than the clear where something stands over it, for the slab that carries the next, and none on the top
  storey, because the roof is its lid. Three storeys of three is eleven courses, not nine. Measuring the air
  rather than the masonry makes the number the author decides the number that is true, and is why three is the
  least a room may be. Walls and windows are laid **in each storey's own frame**, counting from that storey's
  floor, so a band or a sill of two lands at the same place on every storey and a taller ground floor moves the
  one above it whole instead of sliding its windows up the wall; only the ground storey is told about the
  doorway. Each storey but the last is closed by a slab across its interior, zoned by the storey **above** it,
  since that slab is the upper floor rather than the lower ceiling. The way through it is a **ladder** on the
  door wall one cell along from an interior corner — chests and monuments fill the corners and then the far
  wall inward, so with six wools the ceiling in practice that cell is free — moving to the wall's other end
  where the doorway reaches it. A style naming no storeys resolves to the single one its wall describes, and
  that fallback is marked a **shell** so a wall height stays literal: a two-course shed is two courses, not the
  three a room would need, and every style saved before storeys existed builds exactly what it always did.
  `HouseStyle` gained a hand-written equality for the same reason `RoomPart` has one — the generated one
  compares the new storey list by reference, which a round trip cannot survive — with a test naming its
  members so one added later cannot silently drop out of every comparison. `storeys`/`storey_clear` on
  `room_style` (M0017) carry a uniform stack from the composer. (G34g)
- **A house is composed from parts — roofs, storeys and porches as library rows (B71).** A room style held
  every knob of a whole building, so a **part** had no identity: a shingled roof with its pitch, its overhang
  and its capped ridge could not be reused, only re-entered house by house, and a stack of storeys could only
  be a count of identical ones. `roof_style`, `storey_style` and `porch_style` (M0018) are the level
  `style` → `theme` already had, applied to buildings — each owning the knobs of its part plus that part's
  course stacks — and a `room_style` becomes what binds them: its foundation and its door, one roof, an
  optional porch, and an **ordered** stack of storeys through `room_style_storey`. The split is by what owns a
  coherent set of decisions, not by nameable piece: a porch carries no courses at all, because its deck is the
  house's floor and its canopy the roof's material, and the remaining parts are one material each, which a row
  wrapping a style would only rename. A bound part **replaces the house's own columns for that part and only
  those**, so a house binding nothing is exactly the building it always was and no stored row had to move. The
  stack slot's own clear overrides the storey style's, so one preset is a tall ground floor in one house and an
  ordinary room in another — a shop under two flats is two presets bound three times. The ordinal is assigned
  from list position rather than trusted from the caller, since a caller free to number it could save a house
  with two ground floors and no first. Roofs and storeys keep separate course tables so each has a real foreign
  key to its owner, but resolving a course is written once (`PartCourses`) — duplicated schema shape is cheap,
  duplicated resolution logic is how two libraries come to disagree. A part a house still wears refuses its
  delete with the buildings wearing it. Authored on a new **Parts** tab whose one composer reads
  `PartKindInfo` for which parts and knobs each kind has, so three editors cannot drift. (B71)
- **The studio and the showcase draw buildings with the same renderers (B71).** The isometric, plan, section
  and sub-block elevation written for `tools/compose/house-showcase.cs` moved down to
  `PgmStudio.Minecraft.Views`, the lowest project both it and `PgmStudio.Api` reach — `SvgRaster` with them,
  and the showcase's private copies replaced by thin local names over the one implementation. The two have to
  agree about what a building looks like, and a picture one gets right and the other gets wrong is worse than
  either being wrong alone. `BlockShapes` came down as its own thing: what fraction of its cube a block fills
  is read out of legacy metadata, which is block knowledge rather than drawing knowledge, and it is what lets
  the elevation draw a stair lattice as the opening it is instead of a solid 2×2 patch. The room-style editor
  now shows all four views, including a **cutaway** taken on the plane the ladder stands in — the only view
  that shows a storey's slab, the clear under it and the way through it at once. Library **cards** keep the
  cheap run-merged section alone: an isometric is tens of kilobytes, which is nothing for one open editor and
  megabytes for a grid. (B71)
- **A storey names what it closes with, and the slab stops being the floor's leftover (B74).** The slab laid
  across a storey's interior to carry the one above it was always the house floor's own top material, so a
  building could not close its shop floor in flagstone and the flat over it in boards. `Storey.Ceiling` named
  it in the stamper and nothing could reach it — no part, no column, no knob — which made it the one piece of a
  building sayable only in code. It is now a **storey part** (`ceiling`), which needed no migration: a storey's
  parts are rows in `storey_style_course` keyed by (owner, part, ordinal), so naming one more is a word in the
  vocabulary rather than a column. Unbound it is the floor's top material exactly as before, so every stored
  storey builds what it always did. It takes **one material rather than a stack**, and the reason is that the
  course a player actually stands on up there is already divided by the storey *above*'s border, field and
  inlay: a stack here would be a second answer to a settled question, which is the `roof_thickness` mistake
  (B72) in a new place. The preview earns it — a storey that names a ceiling is drawn as **two of itself**,
  because a slab only exists under something and a storey drawn alone is a top storey, so the knob would
  otherwise be one whose picture never moves (the B70 failure). The **cutaway** is the view that shows it, the
  section being a projection of the outside with the near wall in front of the slab. (B74)
- **A door clears the post it was handed past, and a window can arch (B75).** A house cutting its own doorway
  already kept a block of wall clear of each corner post and narrowed the opening rather than giving the margin
  up, so a five-wide framed face carries a centred single opening. A door handed **in** kept nothing — and that
  is the path a wool room's frame takes and the path every library preview takes, so the cards had been drawing
  a two-wide door hard against the pillar. Both paths now go through one fit, and the margin **does not depend
  on a post standing there**: the rule is about the corner, which is where two walls meet and turn, and that
  turn is in a plain shell exactly as in a framed house — making it conditional would mean one building gained
  and lost the margin as its corners were bound and unbound, which is a style deciding where a door goes. It
  costs a wool cage nothing, and WX7 is why: a door already at least one block narrower than the interior on
  each side is exactly the length of the seat run, so a frame's door fits without narrowing and only one pushed
  hard against a corner moves at all. `WindowForm.Arched` is the door head's trick on a window: an upside-down stair in each of the
  opening's two top corners and light under them, two wide at the least because an arch is its two corners and
  one cell cannot hold both, two tall at the least because a head that took the only course would be an arch
  over nothing. And `HouseStyle.DoorEdge` lets a building name the wall it fronts on. A hall is what wanted it:
  windows are spread and centred on a wall's run and a doorway is centred on the same run, so on a long building
  they land on each other and the seats a door meets are dropped — a twenty-one-wide wall entered in the middle
  loses the two windows either side of its door. Entered at the gable end it keeps all four a side. (B75)
- **A village of five houses, cut from one masonry (B76).** `HousePresets.Village` — a cottage, a longhouse, a
  terrace, a counting house and a workshop, meant to stand together rather than to sample the model. They share
  one masonry and one timber, so what separates them is what each is *for* — small and steep, long and low, tall
  and narrow — rather than what each is made of: a settlement whose buildings differ in material reads as five
  settlements, one whose buildings differ in proportion reads as a village. Each is under the 192 blocks a
  dressing building is allowed, so every one can be dragged onto a map. The masonry is **stone and polished
  andesite** checkered a block at a time, and the pair is the point: they differ in texture and not in hue, so
  the board reads as coursed stonework rather than as a chequerboard. That is the rule for checkering a wall — a
  checker states the grid it is laid on, so the two squares must be near enough in value that the grid becomes a
  texture, and two blocks a player can name apart across a courtyard make a draughtboard. Size 1, because the
  board reads off the wall's own perimeter arc and a one-block square carries the alternation round a corner
  without a seam. Proportion was **measured rather than eyeballed**, and two of the five were wrong: the cottage
  at pitch 2 was 64% roof and the workshop's shed 65%. The workshop's fix is the one worth keeping — a shed
  climbs the whole of its shorter span where a gable climbs half of one, so on anything but a shallow building it
  is all roof, and half courses buy the same lean-to for half the height. Both sit at 50% now. (B76)
- **A wall that is all host takes a row, not one window (B77).** A window may name the block it is cut into, so
  that on a banded wall an opening lands in the planks rather than across the seam. The seater found each
  unbroken panel of that block and centred **one** window in it — right for a band, wrong for everything else,
  because a host names a block and not a band: a wall that is one material at the sill course resolves to a
  single panel the length of the whole run, and one window centred in that is one window on a twenty-one-block
  hall. Each panel is now spread and centred exactly as a whole wall is, so a two-cell band still holds one
  two-wide window and no more while a uniform wall gets its row — the longhouse goes from one window a side to
  four, and lengthening it adds windows rather than stretching the gaps. Only the seam between two panels can
  now be too tight, since within one the spread has already left a clear spacing. Worth recording how it was
  missed: the measurement that claimed four a side passed **no host question** to the seater, and with none the
  seater takes the spacing path — it measured the branch the building does not walk. Reading the stamped world
  is what found it. (B77)
- **A block named for a geometric role is checked to be that kind of block before a house style is stored
  (B160).** `doorHead.block`, its `fillBlock` under `upperSlab`, and a `windows`/`gableWindows`/storey-window
  `block` under `stairLattice`, `arched` or `slabBanded` used to be trusted as whatever id a style named, so a
  cobblestone id where the arch's own docstring calls for a stair built a solid lintel with no arch in it, and a
  glass pane where a slab band calls for a slab built a pane/air/pane stripe — silently, on four authored
  boards. `HouseStyleValidation.Check` and the classifier it reads, `BlockKinds` (the full stair set, the three
  *single* slabs — a double slab does not count, since it ignores the half a window or a door head writes into
  its data, and is the same fault as any other whole-block id), now run on every `POST`/`PUT` to `/room-styles`,
  `/roof-styles` and `/storey-styles`, and on a sketch's own bound `roomStyles.cage`/`roomStyles.spawn`
  (`PUT /map/{slug}/sketch`) — the door those snapshots actually enter the studio through. Answers **400**
  `{error: "invalid house style", findings: [{rule, field, message}]}`, one finding per fault, `rule` a stable
  id (`HouseStyleRules`) rather than this task's own; nothing is substituted for the author.
  (`docs/tools/library.md`, `docs/tools/sketch.md`)
- **A door's clear height is a rule of its own, not an accident of a genuine slab (B161).** A door head is
  written into the doorway's top course, so a three-course door with no head clears three and one with a head
  clears two plus half a block only when the fill is *genuinely* an upper slab — `HouseStyleValidation.
  ClearDoorHeight` is that arithmetic (the author's own numbers, not re-derived), and `Check` refuses anything
  under **2.5** (rule `HS2`). Paired with a wrong fill block (`HS1`), a three-course door with a solid
  cobblestone head clears a flat two, which is what two corpus boards actually built. **The task's other half —
  a spawn window must be plain (air or glass, no pattern) — is withdrawn by the author.** `stairLattice` and
  `slabBanded` windows are allowed on any house, a spawn included; the corpus complaint was always the block
  handed to the form (`B160`/`HS1`), never the form itself, which `HousePresets.Alpine` and `Workshop` already
  built correctly. No code shipped for the withdrawn half.
- **A building seated into terrain names its footing off, and the choice has a name (B164).**
  `HouseStyle.NoFooting` is `Sill` resolved to air — the mechanism was already there, since a sill resolving to
  air is a course `HouseStamper` skips like any other, and twelve of fourteen authored maps never reached for
  it. Naming it is the whole fix: an author reading the model now finds "no footing" rather than rediscovering
  a bare air material from a comment on one preset. `Alpine`, `Desert`, `Diorite`, `Townside` and `Stilts` — the
  five presets that meet the ground flush — are built from it now instead of a second-hand
  `new SolidMaterial(Blocks.Air)`.
- **A roof's own materials are checked against its pitch and its family (B168).** A slab named as the
  whole-block `roof` while `roofSlab` is unset builds a see-through roof — a course of slabs at a whole block of
  rise leaves an open half between every pair — and `HouseStyleValidation.Check` refuses it now (rule `HS3`);
  `HousePresets.Diorite`'s inverse (`Roof` a whole block, `RoofSlab` the slab, on a half-course rise) is the
  shape that passes clean. A log or a ground material named as `roof` or `verge` is refused outright wherever it
  is asked to fill either role — six Weirgate houses named a log verge, three of `quillon-barrow`'s named a
  grass roof over a podzol verge. `CheckRoofFamily` is the standalone half of the check a roof-style *part*
  runs on its own, since `roofSlab` is a house-level knob a roof part carries no column for and cannot be
  paired against in isolation.
- **A wall bends where the walked ring does, and it is one measurement (G172).** A house answered `Arc`, `Turn`
  and `Run` off its own rectangle in closed form while the terrain painter walked the same outline through
  `Geom.GridBoundary` — one idea with two implementations, which is what the symmetry rule exists to prevent.
  `Footprint` walks its outline now, at the window the painter reads, so a building and the plateau beside it
  cannot answer a wall-run material differently. The disagreement was in the bend alone: `Arc` and `Run` matched
  exactly over 264 perimeter cells of eight rectangles, but the closed form can see only the corner nearest a
  cell, so on a wall shorter than twice the window two corners fall inside it and it reports one bend where the
  ring turns two — 57 degrees out at the middle of a five-wide side, and six of a five-deep house's fourteen
  inked cells frame differently at a threshold of ninety. Nothing built changed shape, which is why the swap was
  cheap to make now: the bend is read only by `WallFrameMaterial`, which places nothing and which no shipped
  house style binds, and the corner posts come from `OnCorner` — four literal cells with no window and no angle
  in them. The measuring window is one constant on `GridBoundary` rather than a five in each caller, and a
  probe material stamped into a wall gates the agreement over both narrow and ordinary spans. It is also the
  footing for a footprint of more than one rectangle, which has no closed form to fall back on. (G172)
- **A plan is a union of wings, not a rectangle (G172).** `Footprint` is a type of its own, built from one or
  more touching `Wing` rectangles, and every question it answers is asked of its cells rather than of a min and
  a max — so an L, a T or a U is one plan with one closed ring rather than two buildings that happen to touch.
  A rectangle is the case where walking the cells agrees with the arithmetic it replaced, which is what lets
  every shipped building keep its shape. **A corner comes in two kinds**: an outer one is where the building
  turns away from itself — the five of an L — and an inner one is where two wings meet and it turns back into
  itself, which is one cell and carries a post like any other corner, so an L stands on six. The steps in from
  the wall are walked breadth-first from every wall cell at once rather than subtracted from an edge, which is
  the only way a cell in the crook of two wings counts to the wall actually nearest it: nowhere in the L stands
  more than 2 in, where its bounding box claims 4. The stamper still builds a single wing — the roof over more than
  one, and the walls and window runs that go with it, are the rest of G172. (G172)
- **A wall is a run, and a facing stopped being its name (G172).** `Footprint` splits its outline into
  `WallSegment` runs — every maximal stretch of wall with open ground on one side of it, ending wherever the
  building turns, away from itself or back into itself. A rectangle stands in four, one per side, which is
  exactly what a caller naming a wall by its compass direction used to read off a min and a max; an L stands in
  six and a T in eight. **The reason it had to stop being a facing is that two of an L's walls look the same
  way**, at different lines and over different stretches, so a window seated in one and a window seated in the
  other were indistinguishable — and a doorway in one wrongly blocked a window in the other. The window seater,
  the opening fit, the doorway pass, the gable windows, the porch rail and the ladder all take a run now, and a
  `WindowSeat` and a `WallOpening` carry theirs, so a window knows the line its wall stands on and is cut
  without being handed a box. Where a run has to be picked from a direction — a door handed in by a room frame
  names a side, not a wall — the rule is on the plan: the longest run looking that way whose stretch reaches the
  place asked for, length breaking the tie because a building is entered by its face rather than by its return.
  The margin an opening keeps off the end of its run is the same whichever kind of corner ends the run, because
  both are a turn. Nothing moved: the house showcase renders byte-identical across the change, over every roof,
  floor, porch and window figure it draws. (G172)
- **A house is stamped over a plan, not over a width and a depth (G172).** `HouseStamper.Stamp` takes a
  `Footprint`, and everything below the eave reads the plan's own cells: the sill runs one block proud of the
  **outline** rather than filling the box, the floor and the surface course cover what the plan holds, the walls
  and their posts follow the outline, the slab is whatever the interior turns out to be, and the log beams throw
  their two ends out of every corner the building turns away at — five on an L, not four. The refusal
  generalises with them: a plan with no cell off its own wall has no room in it, which is the same rejection a
  span under three blocks used to be and holds whatever shape the plan is. **The roof is the one part still
  written over a rectangle**, so a plan of more than one wing is roofed by a single field over the box drawn
  round it — but that field is **clipped to the plan and its overhang**, which keeps the stamper's own promise
  that nothing is written outside the footprint: the roof stops at the wing's own eave instead of bridging the
  notch. A porch is refused on such a plan for the same reason it is deferred. What found the shape of all of
  this was a **printed cut** — a text grid of one plane, one letter per material — which showed the sill
  following the notch, the posts, and the slab of roof hanging over open ground that the clip now removes. The
  single-wing path is unmoved: the house showcase renders byte-identical. (G172)
- **A wing states which way its ridge runs, where its proportions should not decide it (G183).** A roof
  pitches across the shorter side, so the ridge lies along the longer one — read from each wing alone, and two
  wings that touch may easily come out **parallel**: a 10 × 5 hall beside a 7 × 6 wing is one block from
  crossing and does not, so the roofs meet in a gutter rather than a valley, which is the thing a march exists
  to prevent. A **square** wing is worse — no longer side, the comparison ties toward x, and it can never cross
  anything whatever was wanted, which is how a 10 × 5 hall and a 5 × 5 wing became a fixture that tested no
  junction at all. `Wing.Ridge` overrides the proportions and `RoofField` takes the resolved axis rather than
  recomputing it, so the wing and its field cannot disagree. The rise then follows the span actually crossed,
  which is the point. Proven by putting a stated 5 × 5 through every law a junction is held to. It is a model
  field: a wing's overrides do not reach an authored document yet (`G184`).
- **The roof over a junction is one building's roof, not two roofs in one place (G179–G183).** Four measured
  defects, all of them the same mistake in different clothes: every question was asked of the **wing** that
  happened to be laying a block rather than of the **building** it belongs to. A marched cell was stamped verge
  because a march's first step lands exactly on the wing's own overhang line, so a T came out carrying four
  gables where it has three; every wing's roof course was laid over its whole rectangle, so the hall's eave ran
  across the wing's opening and the loft came out in two pieces on a march and four on a project; a wing's eave
  overhang filled the triangle a neighbour's verge hangs open, because **a verge climbs and an eave does not**
  and nothing in the model knew which of the two it held; and a wing's own gable face rose on the side standing
  against its neighbour, walling one loft off from the other. One comparison and one outline settle all four.
  **Only the highest roof over a cell is written there** — not a max of crowns, no surface blended and no field
  touched, each wing still answering for itself and the comparison deciding only which is the one showing — and
  the **rim is read from the roof plan as a whole**, a cell with a neighbour outside it. Faces rise on the
  **body's** perimeter, so the side of a wing against a neighbour is a doorway rather than an outside face.
  `RoofField.OnBorder` is deleted: it was the predicate the conflation lived in, answering one thing for an
  eave, a verge and the edge of a rectangle that is the middle of a house, and nothing needs it now. What an
  eave and a verge each are is written where the geometry is. Measured after: one enclosed loft per course on
  all four junctions, three verge cells at the ridge on a march and four on a project, and the hall's gable
  overhang open the whole way at `x = −1`, `z 5…9`. Nine tests carry it, including the flood fill that a seal
  test cannot stand in for — a seal passes happily on a roof with a hole in its body. Both `Ell()` fixtures had
  two **parallel** ridges and therefore no junction to test, which is how all four shipped unnoticed (`G182`);
  `EllMarch`/`EllProject` are the ones with crossing ridges, and `G186` redrew the rest.
- **A building stands on a foundation, and a stored style reads forward into it (B196).** The sill, the floor
  and its zoning were three fields sitting beside everything else in a 25-field record, and "no footing" was a
  bare `SolidMaterial(Air)` sentinel — so asking whether a building had one was a comparison against a magic
  material rather than a question the style could answer. `Foundation` is the one thing a building stands on:
  a **plate** claiming downward (whose top course is the ground storey's deck, which is why a ground storey
  names none), that plate's **surface** zoning, and an optional **footing** ringing it a block proud, where
  absent is the state itself. Depth and material — the two things the author asked to vary — are the plate's.
  `HouseStyle.NoFooting` is deleted with the sentinel it named.
- **The way in is one part, and the wall it is cut through is not part of it (B198).** Four fields described
  the doorway — what fills it, the beam over it, and how wide and how tall it is — and they are now a
  `Doorway`. A fifth shared their prefix and does **not** belong to them: `DoorEdge` is the wall the whole
  building fronts on, the one a `RoofForm.Shed` falls toward and a porch stands against, so filing it under
  the doorway would have left a roof asking the way in which way it falls. It is the style's own `Front`,
  which is also the name `HouseProp` already used for the same thing. The verb travelled with the type: the
  clear height a doorway actually leaves once its head is written into the top course was
  `HouseStyleValidation.ClearDoorHeight(style)` — a gate re-deriving from three fields it did not own — and is
  now `Doorway.Clearance`. `CutWidth` joins it, the never-under-two rule the docstring stated and two callers
  each clamped for themselves.
- **Everything above the eave is one part (B197).** Eleven of a `HouseStyle`'s fields described the roof —
  its form, pitch, overhang, half-course slab and that slab's variant, its hole and its ridge cap, and the
  three materials it is laid in — and one of them was named `Roof` and held the material the other ten
  describe the shape of, which is how a caller came to write `style.Roof` for a material and `style.Pitch` for
  how steeply that material climbs. They are one piece of a building, so they are one type: `RoofStyle`, whose
  `Body` is the material the old `Roof` field held. Nothing about what a roof builds changed; what changed is
  that a caller asking about the roof asks the roof, and `InHalves` — "is this a slab roof" — is answered on
  the part rather than re-derived from `Slab >= 0` at each of its call sites. The wire is untouched: a save
  request is still the flat row a library editor posts, and only the **snapshot** moved, which is what the
  upgrade below is for.
- **One upgrade walk, called from both places a style is stored (B197).** `HouseStyleJson.Upgraded` was
  private and took a string, so it ran only when a style was read on its own — and a house prop in a dressing
  document carries a whole style, which `DressingJson` deserialized straight past it. `B196`'s foundation move
  was therefore already being dropped for every placed building; the roof move would have been the second. The
  walk is now `HouseStyleJson.Upgrade(JsonNode)`, in place and public, called by the standalone reader and by
  the dressing reader's house prop alike — the shape `TerrainThemeJson.Upgrade` already had, for the reason a
  second upgrade path is a second thing to forget. A stored style names only what it changes, so **any** of the
  old flat fields alone identifies the old shape, which is what a trigger keyed on one field would have missed.
- **A house style can be migrated, which it could not be before (B196).** `DressingJson` has carried an upgrade
  hook since it had props to carry; `HouseStyleJson` had none, and a map keeps its bound style rather than a
  key into the library — so every style ever stored is still in a layout blob, and the reader falls back to the
  built-in shell on anything it cannot parse. A shape change without a hook was therefore not an error but a
  map that quietly stopped looking like itself, and `basalt-reach.styles.json` would have been the first. The
  hook lands with the foundation riding on it, and the air sentinel reads forward as no footing.
- **A storey stands on a deck, and one plate has one owner (B194).** The course between two storeys is the
  ceiling of the lower seen from below and the floor of the upper seen from above, and a block has only one
  identity — but the model gave it two owners: its **material** came from the storey below (`Storey.Ceiling`)
  and its **zoning**, the border and inlay a player actually walks on, from the storey above, with the upper
  one winning wherever it bothered to speak. The preset that set it had written `// the deck underfoot` beside
  the word `Ceiling`, and `structures.md` said outright that the course "is that storey's floor, not the
  ceiling of the one below" in the paragraph after the one assigning its material downward. It is now
  `Storey.Deck`, owned by the storey standing on it, with the ground storey's deck being the building's own
  floor — so nothing a ground storey says reaches a plate at all. The fallback came home too: it had been
  resolved inline in the stamper two hundred lines from `Levels` and against a different default, so a reader
  of that list would have concluded a deck had none. Every preset, test and library row moved up one storey,
  the same blocks stated by the storey that stands on them, and the world builds byte-identically.
- **Block geometry is written in one place, and an arch is one shape (B193).** In this format a block's
  metadata *is* its geometry — two bits of facing and an upside-down flag on a stair, one bit of half on a slab
  — so turning one is arithmetic, and it was written out at five sites: the corner-stair expression three times
  inside `HouseWindows.cs` alone, twice with the same explanatory comment beside it, plus the upper slab as a
  band's lintel and again as a door head's fill. `BlockGeometry` is the writing half of what `Views/BlockShapes`
  already read back to draw, and **nothing in it is named for what it is used on**: it sits beside `Blocks`, so
  a terrain material banding a wall in upper slabs reaches the same vocabulary an opening does. The
  `WindowForm.Arched` docstring had said outright that it was `DoorHeadForm.Arched` "doing its trick for a
  window instead of a doorway"; both now go through one `Arch`, and the difference between them is the one
  thing that is genuinely different — the `ArchSpan` across the middle, a beam where a head carries a wall and
  open where a window has no wall to carry. Nine tests check the writer against the reader rather than against
  a number, so a stair turned toward +x has to come back as one whose raised half draws on the right.
- **A wing states its own storeys, roof, ridge and joint from a document (G184).** Six fields existed on the
  model and none reached an author: a `HouseProp` held two corners per wing, so every authored building
  marched, the second gable was unreachable, and an author wanting a real valley had to be told to draw the
  wing deeper than it is wide. A wing entry is now `corners` plus an optional `spec`. The six are declared
  **once**, as `WingSpec`, carried by both the model's `Wing` and the document's `AuthoredWing` — three of
  them (`form`, `pitch`, `roofSlab`) are the same three a `HouseStyle` names, so the wing's are overrides and
  `Wing.RoofOver` resolves all three as **one** decision where a `FormOr`/`PitchOr`/`SlabOr` triple had
  resolved them separately and read the slab twice at one call site. `Wing.Storeys` became `StoreysHigh`: it
  is a **count** into the style's storey list while `HouseStyle.Storeys` is that list, and one name over both
  read as the same thing twice. A stored wing that is a bare corner pair upgrades to an entry stating nothing,
  which is exactly what it always meant.
- **One verb and one answer, not just one type (B192).** Sharing `Finding` left seven names for the verb that
  produces one — `Faults`, `Check`, `Refusals`, `Validate`, `Findings`, `Errors`, `Completeness` — over three
  return types, and left every consumer working out for itself whether anything had been refused. That last
  part was a trap rather than untidiness: a gate reporting only refusals answers an empty list for a clean
  document, so `Count > 0` reads as "refused" and happens to be right, while a gate reporting complaints as
  well answers a non-empty list for a document that is perfectly good and the same expression blocks it. Both
  kinds were read with `Count > 0`, correct only by accident of which gate an endpoint happened to call.
  **`Findings`** (in `Domain`) is what every gate answers: `Refuses` is the question asked once, `Refusals` and
  `Complaints` split the list, `Summary` is every sentence, `And` joins two gates' answers, `Under(root)`
  prefixes a field so a style bound twice onto one sketch reports `roomStyles.cage.doorHead.block` rather than
  a `doorHead.block` an author cannot place, and `None` makes nothing-wrong a value so a gate never returns
  null. **Every gate is `Check`**, with no interface — a gate takes whatever it needs to answer, a plan alone
  or goals plus the ground they stand on, and forcing one would make the context-carrying gates lie about what
  they read; what is uniform is the answer. `Refusals.StopAsync` gates an endpoint in one line and writes only
  the refusals, so a complaint never arrives dressed as one. Two shapes stay as they were and now say why: a
  parse throws, because it cannot carry on to collect a second fault, and carries its finding so the gate above
  answers in the shape anyway; `RoomFrames.ResolveRoom` answers a room *or* a refusal, because a resolve
  produces a value and stops at the first thing making that impossible where a gate collects everything wrong.
- **Every gate in the studio says no in one shape (B191).** Seven finding types had grown, one per gate — a
  plan finding, a house-style finding, a producibility finding, a joint fault tuple, an evaluator violation, a
  parse exception's loose fields, and three dictionaries built inline at the export gate — and with them six
  wire envelopes, so a panel rendering a refusal had to know which route it came from before it could read
  one. The differences were never real: each was a rule, a sentence and a subject under different field names,
  and `Violation`'s own docstring already said it carried "the same subject-id shape a `PlanFinding` carries".
  One `Finding(Rule, Message, Severity, Field?, Subjects?, Cites?)` in **`Domain`** replaces all of them —
  the lowest project every gate can reach, with the wire mirror in `Contracts` for the WASM client — and one
  `{error, message, findings[]}` envelope replaces the six, written by `Refusals` (API) or `Finding.Wire` (an
  untyped composer). What is genuinely not a finding stayed out: a `TermScore` is a distance that *carries*
  one. Two concepts that had been conflated are now separate and both kept: **severity** makes "complaint" a
  real thing rather than doc language (`PlanSeverity.Lint` had been the only non-blocking severity anywhere,
  so the dressing tool's six "complaints" were hand-rolled refusals), and **cites** holds the layout rule or
  the *open task* a finding points at, which must never share a field with a rule id — a rule is stable
  forever and a task id is a debt with a due date. Every rule id moved into a `*Rules` class beside the rule
  that fires it: `OB20` had been a bare string literal at its throw site and `OB19` had no id at all, while
  `DC2`, `OB14`, `SP1` and four `WX` refusals named their rule in a comment or in prose and passed none of it
  to the caller. Eleven `PL*` ids were minted for the plan's own structural errors, which had carried
  `rule: null`. `docs/refusals.md` is the catalogue and the shape; the tool documents' endpoint tables cite it
  rather than each describing an envelope of its own.
- **Wings abut, the hall is derived, and the joint is the wing's own choice (G185, G186).** The author's model,
  in the author's vocabulary: two footprints **abut** when no block belongs to both and no gap lies between
  them, they **overlap** when blocks are shared, and only the first makes a building. Where they abut they
  share the edge **whole** — the shorter lying within the longer — because a partial touch leaves part of the
  wing's end meeting its neighbour and the rest hanging over open ground. Which rectangle is the **hall** is
  derived and never named: the hall's ridge runs *along* the shared edge and the wing's runs *into* it, both
  along it is two ranges side by side and both into it is one longer range. A wing also reaches no further
  along that edge than the hall reaches across it, since a gable's height follows the span its slopes cross —
  measured against a 20 × 5 hall peaking at +8, a wing 3 wide reaches +7 and 5 wide reaches +8 and both march,
  while 7 wide reaches +9 and runs clean over. Equal is legal: two 5 × 5 squares with crossing ridges are one
  building whose plan is a single 5 × 10 box and whose roof levels are what tell them apart. `WingJoints`
  derives all of it from the rectangles alone and every failure carries a rule id — `HJ1` overlapping, `HJ2` a
  partial touch, `HJ3` a gutter, `HJ4` one longer range, `HJ5` a wing taller than its hall — which
  `HouseProp.Fault()` reports as an id and a sentence where `Footprint()` answers null. **March and project
  stop being read off the geometry and become `Wing.Projects`**, the one thing about a joint the rectangles
  cannot say, defaulting to march: the same two rectangles make an L that closes and an L that pushes through.
  A projection lengthens the **roof** and not the walls, along the wing's own ridge, so it arrives over the
  hall's far wall at the height it left its own. Two things fell out. The old `Marches` sampled the **middle
  cell** of the wing's across-span, so a partial touch answered yes and then built a ragged seam; the joint
  model answers for the whole edge. And the march is now **solved before any roof is laid**, because a course
  marched into the hall is a course of the wing's roof standing over ground the hall's roof also covers — laid
  afterwards it left the hall's own course written *under* the wing's ridge as a floor across the valley, which
  is `G185`'s two-lofts-on-an-abutting-wing exactly. `Decorator.TurnedFootprint` stopped dropping a wing's
  statements at the orbit and now turns the ridge axis with the rectangle, without which a quarter turn of a T
  came out as two ranges side by side. Every junction fixture was redrawn as an abutting pair, `Ell()` included,
  since a gutter is a shape no document may state.
- **A house on more than one wing closes at the turn, and stands on six posts (G172).** Where two wings meet,
  the wall of one runs into the wall of the other — and on a raster those two walls touch along a single
  vertical **edge** and nothing else. The cell behind that edge has building on all four sides of it, so a wall
  that stands wherever the plan is exposed *orthogonally* calls it interior and leaves it open: the building
  has no block where it turns, the two walls simply step past each other, and the room behind shows through the
  seam at a glancing angle. A wall now stands wherever the plan is exposed **at all**, diagonals included,
  which puts a block on that cell and makes the outline turn a real corner — and it takes a post, so an L
  carries six and not five. A beam is the one thing that stays with the outer corners alone: an inner one has
  no direction to throw a log end in that is not the building itself. **A flood fill cannot find this class of
  hole** — nothing steps diagonally, so the seal test passed the whole time — which is why the gate for it is
  geometric instead: no cell inside the house may touch the outside on any of its eight sides. The wall at the
  turn also settles what the crook is worth: nowhere in the L stands more than 2 blocks in, where before the
  two five-deep wings read as one room deeper than either of them is. (G172)
- **A storey is a plan of its own, so wings may stand to different heights (G172).** A `Wing` carries a storey
  count — nought taking the whole of the style's stack, which is what a building of level wings means — and
  `Footprint.At(level)` answers the plan of the wings still standing at that storey. Every pass a storey runs is
  then asked of that plan: its walls, its posts, its corners of both kinds, its window runs, the steps in from
  its wall, and the slab, which is laid over the storey *above*'s plan because a wing that stops is closed by
  its roof rather than by a floor for a room nobody built. **The wall a taller wing needs against its
  neighbour's roof falls out of this rather than being a rule of its own**: at that height the neighbour is not
  there to be met, so the line the two shared is simply the upper storey's outline — one open room downstairs,
  a walled gable upstairs, built by the ordinary pass. A plan only ever loses wings on the way up, which is what
  lets the way up be seated against the topmost storey's front: a cell inside the highest storey is inside every
  storey beneath it, where one chosen on the ground could be under open sky two floors later. The roof is still
  one field at one height, so a wing that stops lower has its walls climb to meet it — unequal wings are correct
  to the eave and roofed as though they were level, which is the rest of G172. (G172)
- **A building's roof is the union of its wings' roofs (G172).** Each wing is extruded as the whole building it
  would be alone — its own rectangle, its own eave from its own storey count, its own ridge axis from its own
  proportions — and the volumes are laid one after another, each closing its own riser against itself. **Never a
  max of crowns**: a max blends two surfaces into one and drags roof material down the wall between wings of
  unequal height. `RoofField` is untouched, which is the finding the whole arrangement rests on. Three rules
  carry the rest. A wing's roof reaches **its own walls plus its own overhang and no further**, so no stub hangs
  outside a wall it never touched — and since a wing's field is already its rectangle grown by the overhang,
  that is the bound rather than a clip on top of one. **No roof block below the wall top of whatever covers that
  cell**, which is what makes a one-storey wing stop against a two-storey one instead of pushing a slope through
  its standing wall, and what turns two abutting eaves into a valley instead of into each other's gutters. And
  **walls outrank roofs** — every volume is laid before any wall is, and the wall-top rule settles the rest.
  That last one moves a building of a single wing too, and it is the only thing on this branch that has: at a
  steep pitch the eave's riser used to reach into the top course of its own wall, and the wall now keeps it. No hole opens where it stopped, because the
  course below a wing's roof base *is* its wall top. The gate is the task's own acceptance test — **a wing's two
  gable ends are the same gable**, the one ending the building and the one standing against its neighbour,
  compared above the eave over the wing's own width. (G172)
- **A wing may project into another, and the cross-gable is built (G172).** Two joints: a wing reaching an
  outside wall of another makes a **valley**, which the union builds by itself, and a wing whose gable end
  lands between the other's own two ends is standing mid-slope and makes a **cross-gable**. Which of the two a
  joint gets was read off how far the rectangle was drawn, and is the wing's own choice under `G186`. Three things follow for that one. Its buried gable end
  is **a wall from the ground up**, posts and all — a face inside a neighbour of the same height is exposed by
  nothing, so a wall built only where the plan is exposed never builds it. Its gable face is drawn there by the
  same pass that draws the end closing the building, because a wing's roof plan ends on both. And it **cuts**
  the roof it pushes into. The cut is across the wing's **walls and not its overhang**, which is the correction
  a printed plan of the highest block over each column produced immediately: cut one column wider and the roof
  opens down both sides of the wing, because the verge that was to fill it is itself standing over the other
  wing's wall and the rule keeping roof out from under a wall keeps it out too. This is the shape that can carry
  **the acceptance test in the form the task states it** — a wing's two gable ends the same gable, plinth and
  wall included — and it passes over the wing's own width. That test wrote the last of the implementation: it
  failed first on a missing corner post at the buried end, which is exactly the sort of thing it exists to
  name. (G172)
- **A meet marches, so a valley does not dip (G172).** A wing whose gable end runs up against another does not
  stop at the wall: each course steps on along its own ridge into the other roof until it **hits a block, and
  stops**. The courses nearest the ridge travel furthest and the ones nearest the eave stop at once, which draws
  the crossing as a **diagonal valley** rather than as a wing abutting a wall. Left to abut, the ridge fell from
  its own crown to the neighbour's eave and climbed again — a gutter cut across the middle of a roof, reading as
  two buildings pushed together rather than as one that turns; the gate is that the surface along that ridge
  never falls between the wing's gable end and the ridge it runs into. **A marching end carries no overhang**,
  and that is not only a rule about what an overhang means outside a wall — it is what makes the march possible
  at all. With its own eave still in place every course struck a block at its first step and the march moved
  nothing, which a printed height map showed at once and no seal test could have. The march is the meet's half
  of the pair: a wing that *projects* never marches, having already cut the roof it pushed into. (G172)
- **A march stops on its own, and not only where something taller happens to meet it (G172).** "Hits a block,
  and stops" was the whole rule, and it broke wherever no block was ever going to be tall enough to hit: a
  wing at pitch 2 against a hall at pitch 1 on equal storeys, and a gabled wing against a **flat**-roofed hall
  with no ridge anywhere to strike. A course whose crown stood above everything in its way never got struck,
  so it marched the neighbour's whole length and came out its far overhang — the drawn-through shape, reached
  by a rectangle that was drawn to stop at the wall. `HouseStamper.Marches`' stop is now two conditions, either
  of which ends it: the neighbour's own roof field already stands as tall at that cell (read from its
  `RoofField` directly, not probed from placed blocks), or the course has marched as far from its own eave as
  its own roof plane would ever climb — a bound that does not depend on meeting anything, so it holds even
  where the neighbour's surface never rises to meet it. Caught by a printed cut, one plane at a time, of both
  reproducing footprints; the flood-fill seal test that gates a hole in a roof's body passed on both before the
  fix, which is why the cut and not the seal test is what the fix was checked against. (G172)
- **A building prop states a list of touching rectangles, and `Decorator` composes them into one house before
  stamping (G177).** `HouseProp.Wings` replaces the single pair of corners a placed building used to carry, so
  an L, a T or a U is authorable directly — by an agent writing `dressing.props`, and today only by one, since
  the canvas tool to add a second wing interactively is not built (`S60`). `HouseProp.Footprint()` composes the
  wings into one `Minecraft.Footprint`, each wing held to the same three-block floor a single rectangle always
  needed and the whole plan to `MaxFootprint` measured over the cells the wings actually cover rather than the
  box drawn round them. `Decorator.PlaceHouse` turns every wing round the symmetry orbit and stamps the
  composed plan once through `HouseStamper`'s `Footprint` overload, and the overlap rule (`MG7`) now reads two
  authored rectangles overlapping as two buildings colliding — still a drop — without a prop's own wings ever
  reaching that test against each other, since the whole plan is checked as one before any of it is placed.
  `DressingScope`'s provenance and goal-clearance readers turn and read the same composed, per-wing-grown plan
  rather than the bounding box round it, so an L's notch is not claimed as structure it never stood on. (G177)
- **Build-region outline — `BuildMarkerStamper`.** Every synthesised world marks its build regions with an
  unpowered redstone line at y=1, so a mapper can see where players may build without a block landing anywhere
  near the play surface (ST5). The line sits two blocks out from the region — one air block clear — and holds
  that same clearance from terrain, which is what pulls it back where terrain overhangs the region. Only
  void-facing edges carry a line, since a docked edge is already marked by the terrain it docks against, and
  two void-facing edges meeting at a convex corner turn into each other: a free-standing region comes out
  ringed, a region bridging two pieces comes out with two plain lines. Because the trace reads **cells rather
  than the zone list**, the multi-zone shapes need no cases of their own — zones sharing a border outline as
  one region with their enclosed pocket ringed in its own right, and two zones touching at a single corner come
  out as one unbroken staircase. Derived at export rather than at compile time because the clearance reads the
  terrain the world actually placed; the areas arrive already fanned, so the marker is symmetric with them. The
  sixteen zones of `tools/seeds/teaching/build-region-examples.plan.json` are the authored expectations, drawn
  by `tools/deriver/build-marker-check.cs` (`--svg` for the picture). (G155, ST5)
- **Dressing — what stands on the terrain, not what it is made of (G161).** The world's last pass, after
  `TerrainPainter`: where the painter can only change what a stone cell becomes, the `Decorator`
  (`PgmStudio.Minecraft/Dressing`, `docs/world-export/decoration.md` `DR*`) adds cells — plants in the air above
  the surface, half-buried rock, grown trees — and repaints the ones a route crosses. It runs last because the
  one fact it needs is what the paint just decided: soil takes cover, quartz takes none, and a column whose top
  block is a stamp takes nothing at all.
  **Everything is placed, not sprinkled.** A tree is cover and a boulder is a wall, so where each one stands
  decides how the map plays, and that decision belongs to the author rather than to a density field. Four tools
  on the Dressing phase's own canvas, two interactions between them: a **route** (DR-PA) and an **area of
  cover** (DR-FL) are dragged — press, trace, release, so there is no separate way to finish and no way to get
  stuck mid-draw — while a **tree** (DR-TR) and a **boulder** (DR-SC) are clicked into place. Each carries its
  own knobs, edited where it stands; the tool remembers the last ones, so a stand of ten oaks is ten clicks
  rather than ten forms. A placement ends its tool the way a finished draw tool does — the prop is selected and
  the canvas is back in select — because the thing just put down is the thing to move or tune, and a
  still-armed tool turns that first click into a second tree. What is selected then wears the same square
  grips a sketch polygon wears: one per traced point on a route or an area, one on a marker's anchor, each
  dragged to reshape in place. A path that runs two blocks wide of a bridge is a point to move, not a route to
  trace again. The fields that remain are the ones *inside* a drawn area — which blade of grass,
  which cobble — where placing them one at a time would be data entry.
  Behind them: `TreeSkeleton` growing limbs as Catmull-Rom splines with a leader knob, upward pull and
  per-step jitter; `SweptVolume` filling each as a capsule; `TreeCrown` placing one cluster per outer tip
  with a seam of air between neighbours, so a viewer reads each patch as its own branch's; `Blob`-eroded
  quadric lobes in four rock forms, half below the surface.
- **A grown tree is built to what 75 hand-built ones measure (G171, G172, G174, G175).** The corpus of
  `docs/world-export/tree-corpus.md` is now the grower's law, and the same harnesses score the result.
  **Nothing is emitted that the tree does not hold**: `TreeCrown.Rooted` keeps only foliage reaching wood
  through foliage, so a floating leaf is impossible rather than rare (36% of generated trees carried one, with
  islands up to 189 blocks); and `SweptVolume.Ball` always stamps the block its centre sits in, closing a band
  between radius 0.5 and √3/2 where a sample selected no cell at all — 21% of sweep samples placed nothing, and
  19% of trees emitted wood in more than one piece. **A cluster sits on its tip, not beyond it**, and is small,
  perforated and sized by the branch carrying it, which takes leaves touching wood from 14.8% to 36% (an
  author's 30.3%) and enclosed leaves from 7.2% to nil (1.7%). **A branch leaves by the angle it was given**:
  `TreeSkeleton.Steer` turns a child in its parent's own frame rather than in world yaw and pitch, which a
  vertical trunk discards entirely — first-order limbs moved from 24° off vertical to 60° (59°), their reach
  against the trunk's from 0.20 to 0.42 (0.40), and the wood from 7.9 occupied neighbours per block to 4.7
  (6.3). **Wood barely grows with height**, because an author's does not — 23 blocks at 5-9 courses and 53 at
  24-40 — so neither the trunk radius nor the lateral count scales with it, and a 40-course tree fell from 456
  blocks of wood to 322. A **whorled** form gathers the laterals into rings 5.2 courses apart, each shorter than the last and
  none forking: the conifer against the broadleaf, an author's toggle on the tree's own panel. Gated by
  `DressingAlgorithmTests` over a height and seed sweep. (`PgmStudio.Geom/Algorithms/TreeSkeleton.cs`,
  `TreeCrown.cs`, `SweptVolume.cs`; `tools/tree-corpus/grower-gate.cs`, `wood-skeleton.cs --grower`) (G171, G172, G174, G175)
- **Fairness is structural, not a filter (G162).** Every prop declares a `PropClass`. Cosmetic props —
  one-block plants — scatter freely; a flower one team has and the other does not decides nothing. Gameplay
  props are generated **once**, on the orbit's canonical representative, in the prop's own local frame, then
  stamped at every image with each offset **turned** by that image's transform. Fanning the site alone is not
  enough and that is the whole point: mirroring only the anchor leaves both teams the same unmirrored prop
  shape, so a boulder with a lobe to its east has one to its east on both halves of the map. The turn is a
  plain rotation with no half-cell correction — an offset is a delta between cells, and the delta between two
  mirrored cells is just the mirrored delta, the anchor having already been corrected. A stamp is
  all-or-nothing per image, so a prop whose mirror lands on missing or protected ground places on neither side.
  Measured on a `rot_180` board: free scatter left 361 cover cells whose mirror was bare, the fanned pass zero.
- **Paths — drag a route, and the ground remembers it (DR-PA).** A path **repaints** the surface it crosses
  rather than adding a cell, so it runs over a slope without becoming a ramp and a bridge over a void stays the
  draw phase's job; its cells become bare ground as they are laid, so nothing grows through the road. All six
  styles of the model exist, because being a *fill* rather than an outline is what makes them expressible:
  solid, worn (a per-cell dice), rough edge (the width wandered by a seeded noise field, each side reading it
  far apart so the band erodes rather than breathes), cobbled (the band tiled by a jittered grid across the
  path's own blocks), stepping stones (discs along the arc, with gaps) and tapered. `Geom.PathStroke` is the
  six gates on one distance field; `Geom.PathBand` is the outline the canvas strokes, twinned in
  `geometry/path.js` and pinned to the C# side by a parity test that checks the lattice hash bit for bit.
- **Water channels — the one prop that takes the ground away (DR-WA, G169).** A channel is drawn like a path
  — a dragged centerline and a width, the same swept-disc band — but water cannot drape on a surface (laid flat
  it reads as blue paint), so it **cuts a bed and fills it**: a shallow U deepest on the centerline and one
  block at the shore, filled to a single **water line** (the lowest surface the channel crosses, so the fill
  never floats above ground it did not cut), and any bank above the line cut back to air so the channel runs
  open. It **only ever replaces existing terrain** — the carve stops at each column's old surface and skips a
  column the surface map does not carry, so a channel keeps a hollow it crosses and leaves any stamp (a
  monument's wool) alone. The water meets the land through a **beach** — a shore band outside the water whose
  width wanders with a noise field and drops to nothing in places (`WaterBed.ShoreCells`). Both the beach and
  the bed floor are laid with the channel's **bank**, which is a full `TerrainMaterial` (a solid, or by default
  a jittered-voronoi patchwork of sand/gravel/coarse-dirt) edited by the same `MaterialEditor` the theme phase
  uses — not one block. Three **forms** drive water *and* land: **canal** (uniform, narrow even bank),
  **natural** (FBM-wandered width and shore), **stream** (narrows and shallows into riffles, spreads into wide
  flats). Like every prop it is fanned across the symmetry orbit, so both teams get the same water from the same
  side. `Geom.WaterBed` is the bed profile + shore band (reusing `PathBand`/`Polyline`); `Decorator.PlaceWater`
  is the carve-and-fill + beach; the form picker is drawn by the pass (`/api/terrain/water-forms`). Depth
  shading, edge life (reeds/lily pads) and ponds stay open under G169 (`docs/world-export/decoration.md` §7).
- **Every picker is drawn by the pass.** The six path styles, the three channel forms, the four rock forms and
  every species are rendered at card size by the real algorithm over the map's own finish
  (`/api/terrain/path-styles`, `/water-forms`, `/boulder-forms`, `/species`) — a dropdown of six words cannot
  say what separates a worn path from stepping
  stones, and a hand-drawn icon can promise a look the export does not produce. A species card carries its
  proportions too, so picking "spruce" takes a spruce's shape and the client keeps no second copy of the
  species table. The inspector's own picture is the same thing at full size: `DressingPreview` **places** the
  prop on a sample patch and draws the result, from above and cut open, cropped to what is there so a path and
  a tree read at the same scale. `DressingScope` answers what the pass needs from the map — what was placed,
  how it is mirrored, and what must stay bare: spawns and their margin, wool spawns, rooms and monuments,
  anchors, structure floors and walls, and every column a stamp already stands on.
- **Export endpoint** — `SketchWorldBuilder` assembles the world from a map's sketch layout + intent and
  returns a resolved intent (integer-snapped spawns + monument locations derived from the world air cells,
  capturers defaulted to every non-owner team) so the XML agrees with the world. `GET /api/map/{slug}/export`
  returns a `{slug}/` ZIP (`map.xml` + `level.dat` + `region/*.mca`) for sketch-origin maps and plain
  `map.xml` otherwise, behind the traversability gate (shared `MapXmlComposer`). The Configure Export button
  downloads it (`studio.downloadUrl`), and the wizard's manual Monuments sub-step is dropped for sketch maps
  (`GET /map/{slug}/origin`). Spec: `docs/world-export/sketch-world-export.md`. (P9e, P9f, P9k)

## Sketch tool (M8) — draw shapes → islands → world geometry
- **Grid-aligned sketch — block-accurate WYSIWYG (S23).** The sketch is now honest about the voxelized world
  it produces. Every stored shape is **block-integer**: `snapShape` (geometry/shape.js) rounds all coordinates
  to the grid, enforced at the `addShape`/`updateShape` chokepoint, so no edit path — vertex drag, midpoint
  insert, rotate/scale bake, placement — can leave a point between blocks (Bézier control handles snap too; the
  curve still samples smoothly between them). A **Blocks** toggle overlays the *rasterized* footprint —
  `geometry/rasterize.js` reproduces the C# `SketchRasterizer` exactly (same rings — circle 64-gon, Bézier
  16/edge — the same cell-centre `(x+0.5, z+0.5)` fill, the same add/subtract/override set algebra), merged into
  horizontal runs so a curve visibly reads as the stair-stepped cells it exports as, beneath its smooth outline.
  This is the prerequisite that makes the sketch the block-accurate surface the finishing pass will live on
  (the parity constants in `docs/tools/sketch.md`). Parity is unit-tested both sides.
- **The plan's spawn/wool pieces surface as locked, labelled rectangles in the sketch (S25).** Refining a
  plan in the sketch used to be blind: `PlanCompiler` fuses same-plane pieces into one island polygon, so on a
  single-height board the spawn and wool-room footprints — which survive only in `map_intent_json` — dissolved
  into the terrain with no marker for where they were. The compiler now **projects the intent's structural
  pieces into the layout** as `role`-tagged (`spawn`/`woolRoom`) rectangles, each carrying an `intentRef` (team
  id, or `owner:colour`) and colour and rendered as a labelled box in the **plan tool's role colour** (purple
  spawn / green wool, `plan-doc.js` `ROLE_COLORS`), the colour carrying the role and the label the identity
  (`<team> spawn` / `<colour> wool`). The piece rect is the whole link — it *is* the
  protection/room region, sizes the stamped
  foundation, and anchors the marker — so the rectangle alone re-secures the tie to the intent. They are **not
  terrain**: the `SketchRasterizer` and its `rasterize.js` twin skip any role-tagged shape, so a box overlays
  the fused island without double-carving it. They are **locked** (never hit-tested, selected, promoted,
  resized, moved, or sloped): the client partitions them out of the drawn-shape pipeline on load and merges
  them back on save, round-tripping without entering island detection. Making them movable (a drag writing back
  to the intent) is a deliberate later phase. Visibility changes; authoring still stays in plan/configure.
- **Terrain-paint theming moved onto the sketch, keyed on shapes/islands (`docs/world-export/terrain-painting.md` TP10).** The paint
  pass no longer lives on the plan: it is a **Theme phase** of the sketch tool, because the sketch rasterizer is
  what makes the world and the scope target is the final geometry, not plan pieces. Two steps, ported from the
  old plan Theme rail: **Create** authors a theme per bucket (the shared `MaterialEditor`/`BlockPicker`/
  `ThemeVocabulary`, now under `Features/Sketch`); **Apply** is the **island tree 1:1** plus the theme controls
  (map default · theme picker with per-bucket swatches · Apply/Remove on the selection), reusing the live canvas
  with the inspector hidden. An island themes its every member shape, a shape just itself. Storage
  is on the layout: a `themes` registry + `mapTheme` on `SketchLayout`, a `theme` id on each `SketchShape`
  (`sketch-bridge` round-trips them). Resolution is `TerrainThemeScope.ThemeAt(layout)` → `cell → shape → theme`
  via `SketchRasterizer.ShapeThemeOwners` (mirror-aware, smallest-area wins), so **reshaping a shape moves its
  paint**. Removed from the plan: the Theme rail, `PlanCompiler.BuildThemes`, and the intent's theme fields
  (superseding the plan-side G157/G158 theming above). Unit-tested (scope resolution + reshape) and e2e'd
  (persist + render).
  - **The Theme canvas is selection-only (S32).** Editing geometry belongs to the Draw phase, so the Apply
    step offers exactly two things: pick an island or shape, and move the view. Its toolbar carries the
    **move and select tools alone** — the tools that author geometry are simply absent, in place of the hint
    that used to sit there restating the rail beside it. Everything else is withheld at the source rather
    than ignored downstream: `SketchEditController.setEnabled(false)` draws no resize handle, vertex, Bézier
    tangent or midpoint ghost and declines every pointer hook; the island chrome keeps its dashed selection
    box but drops the rotate zones and scale grips; `_hitMovable` reports nothing draggable; the arrow-key
    nudge is off; and an armed library item is disarmed on entry. A drag therefore has nothing to begin on,
    rather than beginning and being discarded. The phase switch owns the mode, so no route in or out can
    leave the wrong one behind, and the selection now survives reaching for the hand tool — a tool change
    drops it only in Draw, where arrow-nudge would otherwise move something no longer visibly selected.
    e2e'd: the toolbar holds those two tools, and a drag across a selected island leaves the layout
    byte-identical.
  - **The Blocks overlay shows the blocks the export places (S30).** The sketch's **Blocks** toggle — which
    showed the rasterized footprint as neutral stone — is the terrain paint. `POST /api/map/{slug}/sketch/paint`
    takes the *live* layout and runs the export's own path over it (`TerrainPreview.SketchPaintCells`: rasterise
    the columns, build the terrain, paint it through `TerrainThemeScope` + `TeamTerritory`), then returns the
    top block of every footprint cell as the block-pixel payload the editor's block overlays already blit —
    one opaque pixel per block, `image-rendering: pixelated`, decoded once and blitted per frame
    (`render/block-render.js`). So the preview is the real paint: voronoi cells, noise fields, wall runs, rims
    and team tints all read as themselves, and the rim proves it — an edge-only bucket no per-shape colour
    could produce. The island fill drops to an outline under it, since the painted blocks are the interior.
    The payload is palette-indexed (`palette` + `color_idx`, expanded client-side) because terrain is a handful
    of blocks and a hex per cell was most of the response. Primary footprint only (the mirror image stays a
    smooth polygon, as the Blocks overlay always has).

    **The paint is a Theme-phase preview; Draw keeps the bare voxelization.** The overlay takes both the
    Blocks toggle and the Theme phase, because theming is a finishing pass over a finished sketch: while the
    shapes are still being drawn, Blocks shows the thing it exists to show — the exact cells an export would
    fill — and not a finish over the top of them. Drawing therefore issues **no paint round-trip at all**,
    which is also why it costs the drawing loop nothing. In Theme the fetch is debounced 120 ms against the
    geometry stream and immediate on a theme edit; until a bitmap arrives the plain stone footprint stands in.
    One `PushCanvasMode(phase)` pushes this and the select-only restriction together, from the single place a
    phase changes.

    **Only the column tops are resolved, not a whole world (S31).** Painting every block of every column into
    a `VoxelWorld` and then reading one of them back was the bulk of a preview. `TerrainPainter.ColumnBlocks`
    is now the single place a resolved band becomes blocks — it yields a column's blocks **top cell first**,
    the full paint walks the whole sequence and writes it, and `TopBlock` takes the first element and stops.
    Neither caller can resolve a cell differently from the other, which a parallel top-only implementation
    could not have promised; the preview pays one material resolve per column instead of one per block.
    Verified cell-for-cell identical to the paint-and-read-back path over solid, layered, voronoi, noise and
    team-tint themes with rims, walls, plateau steps, subtracts and mirror orbits — 1.2–1.6× faster end to
    end (369 ms → 238 ms on a 38k-cell board), and unit-tested as the invariant it is: every column's
    `TopBlock` equals what `Paint` writes on top of that column.

    **The footprint ships as runs, and the classifier reads each neighbour once (S33).** A painted board is
    patches of a few blocks separated by long stretches of void, so it is sent that way: `palette` plus
    row-major `runs` of `[paletteIndex, length, …]` over the bounding box, `-1` for a cell outside the
    footprint. Measured across the sketch corpus a board emits **0.05–0.27 runs per painted cell**, which
    beats the per-cell list several times over and a dense mask by more — real footprints fill only 17–40% of
    their own bounding box, so most of a mask would be void. `blockDataToDataUrl` decodes either form into the
    same bitmap and the server sends whichever is smaller, so a pathologically scattered footprint (or one
    whose bounding box is too large to raster) still has the cell list to fall back on. Alongside it,
    `TerrainProfile` now answers all of a neighbour's questions from one `CellFacts` lookup where three tables
    meant three or four hashes of the same coordinate pair, twelve neighbours deep per cell, and
    `GridComponents.Label` claims an unconditionally-joined neighbour with the same lookup that tests it.
    Together: **122 KB → 7.1 KB** and **176 ms → 93 ms** on a typical 14k-cell board, 345 KB → 18.7 KB and
    291 ms → 230 ms on a 200×200 one (warm medians; this VM varies about ±30 ms).

    This replaced a client-side approximation that resolved each theme to **one representative block colour**
    and painted it as translucent stroked runs. It could not show a pattern or a bucket even in principle, and
    what reached the screen was worse: at `fillAlpha 0.7` over the island fill every colour composited towards
    the result purple (`#a05a28` arrived as `rgb(140,91,95)`), and the hairline stroked round each one-cell-tall
    run banded it — a striped grey. The e2e passed throughout, because it screenshotted without reading a pixel;
    it now asserts exact palette hexes at full opacity, on both the payload and the canvas.
- **Sketch editor** — `/maps/{slug}/sketch` (`SketchTool` + `SketchPanel`/`SketchInspector`): draw 2-D
  shapes → live islands + mirror, with select/op/override/delete/rename. Pure geometry in
  `geometry/shape.js` + `geometry/boolean.js`; canvas + draw/edit controllers + `render/sketch-render.js`;
  `bridge/sketch-bridge.js`. A sketch **is a draft map**. (S2a, S2b, S2c)
  - **Surface slope — tilt a shape's top as a plane (S24).** Beyond plan rotation (which turns a shape in
    x/z), the *surface angle* sets its heights: **shift-click 2–3 vertices** to mark them as controls (they
    highlight), set a height for each in the inspector's **Surface slope** panel, and **Apply slope** fits a
    tilted plane through them and reads every other vertex's height off it — 2 controls give a ramp (contours
    perpendicular to the line), 3 give a fully-aimed plane. Heights round to whole blocks, so the fitted slope
    reads as the neat straight steps of a staircase. Pure `geometry/slope.js`; the result is ordinary
    `anchor_heights`, which the `SketchRasterizer` already TIN-interpolates and exports — no backend change.
  - **Draw tools = rectangle · polygon · lasso.** The circle was dropped — it could only be placed and
    moved, never resized or reshaped, so a Bézier-curved polygon does the same job with real control (the
    shape model keeps circle support so any already-saved circle still renders). The **lasso** is a
    freehand way to draw a polygon: on release its dense per-block trace is Douglas–Peucker simplified
    (`geometry/simplify.js`, the client twin of `Geom.PolygonSimplify`, tolerance 4) to a handful of
    anchors — chunky by design, add points back or round edges with the Bézier handles.
- **Sketch persistence** — the layout persists as a `SketchLayoutJson` map_artifact (outside the codec,
  like the draft bucket): `POST /api/sketch` create + `GET`/`PUT /api/map/{slug}/sketch` (debounced save +
  load-on-mount; 4 integration tests). (S2d)
- **Sketch finish / rasterize** — `SketchRasterizer` + `WorldFeatureWriter.WriteSketchAsync` +
  `POST .../sketch/finish` + the Finish button: the sketch rasterizes into the importer's geometry
  artifacts and flows into Configure (`MapStage.Configure` + a `configureUrl`; 6 rasterizer tests). The
  `/maps/new-sketch` page (`SketchCreate`, S11) originates one. (S2e) Plan:
  `docs/tools/sketch.md`.
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
  Plan: the S3 footprint-and-scale slice.
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
  tile CSS shared with the primitive palette. (S11 — the page itself was later retired by C27 below, which
  moved framing into the tool's own Info phase.)
- **Rectangle → polygon promotion** — an inspector **Convert to polygon** button (and the `P` shortcut)
  turns the selected rectangle into a 4-corner polygon (id / operation / override **and the height fields**
  `base_height`/`floor`/`anchor_heights` preserved — a promoted box keeps its column instead of resetting to
  the default), opening vertex-drag · midpoint-insert · Bézier editing. Pure `rectToPolygon`
  (`geometry/shape.js`); `promoteShape` in the bridge; the 8-handle rectangle resize is unchanged until you
  promote. (S4, S15) §2.
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
- **Height editing field + isometric 3-D preview** — a freshly drawn shape stands **9 blocks** tall, the
  plan document's surface height, so a sketch and a plan of the same board start at the same elevation; the
  sketch inspector gains **Height (thickness)**
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
- **Relief — the interior-elevation solver (S41, S42).** A shape used to state its height only at its outline,
  so a hill in the interior was unreachable by construction and a concave outline interpolated straight across
  its own notch. `PgmStudio.Geom.Relief` solves the interior instead: a **footprint** mask whose outline is a
  **no-flow boundary**, five **marks** that pin a stated height over a patch (point / line / area / rim /
  scarp), and **pushes** that lift a drawn ring after the solve with a chamfer-distance skirt, a **crown** that
  domes a round push and ridges a long one off the ring's medial axis, and per-vertex **amounts** so a ridgeline
  falls along its length. Between them sits a **screened Poisson relaxation** (red-black Gauss-Seidel), solved
  **coarse-to-fine** and resumable from the surface already on screen. A mark is *clipped, not confined*, so one
  placed past the edge raises the ground into a corner and stops; a band stops where its line stops, so it
  cannot wrap a half-disc round each end and close the gap it was drawn to leave. Fairness is not left to the
  mirrored marks agreeing: the grain is sampled through the fold and the solved field is folded before it is
  quantized, both on the cell **centre** — reflecting the corner pairs each cell with its image's *neighbour*,
  a one-cell shear that reads as symmetry and measures as a whole block. A shape can **hold** its height into
  the surrounding solve or **exclude** itself from it. Design + measurements: `docs/world-export/relief.md`;
  the prototype every figure comes from is `tools/relief`. (`Geom/Relief/{Footprint,Marks,ReliefSpec,ReliefSolver}.cs`;
  30 tests)
- **Relief is stored per island, rasterizes as the column top, and refuses to be orphaned (S41, S51).** A relief
  rides **top-level on the layout keyed by island id**, not nested in the shapes, because a plan recompile
  replaces every shape it produced and a relief is far more expensive hand work than a shape. `SketchRasterizer`
  takes a relief-bearing island's column **tops** from the solved field and leaves the **floor** alone — a relief
  says where the ground is, not how thick the slab under it is — and solves only over the cells that island's
  add-shapes actually contribute to the standing footprint, so a relief never re-adds ground a subtract took
  away. A **mirrored copy reads its heights back out of the island's own solved surface through the same
  transform**, so the two sides are identical by construction rather than to within a second solve's tolerance.
  Across a recompile the carry is its own rule, not a `FinishKeys` entry: island identity is derived from the
  geometry, so a re-fused board does not move an island but produces a different one, and `PUT
  /map/{slug}/sketch/from-plan` answers **409** naming the islands whose terrain would be orphaned and writes
  nothing — `?force=true` accepts the loss, which is the author's call and not the server's. One flat mark shape
  carries every kind and `"h"` reads a number or an array, so the document an agent emits is the one the editor
  saves. (`Pgm/Sketch/{SketchRelief,SketchLayout,SketchRasterizer}.cs`, `Api/Endpoints/SketchEndpoints.cs`;
  13 tests)

- **Relief: the contour overlay, traced on the server (S45).** A relief could be written and built but not
  *seen*. A **Relief** layer chip posts the live layout to `POST /map/{slug}/sketch/relief`, which solves it
  through the build's own entry point (`SketchRasterizer.ReliefFields`) and answers, per relief-bearing
  island, its height range, its bounds and its **traced contour lines** — so a previewed surface cannot differ
  from the surface that gets built, which is the only property that makes a preview worth drawing. Lines are
  traced by marching squares over the **continuous** field (`Geom.Relief.Contours`), because contouring the
  block surface returns the outlines of its own treads rather than lines of constant height; squares are
  sampled at cell centres so a contour stops half a block inside the land instead of running out over the
  void; the ambiguous saddle square is resolved by its centre, which is what keeps a pass between two summits
  reading as one ring; and a segment of no length — what a level passing exactly through a sampled height
  emits — is dropped rather than surfacing as a stray two-point stub on every whole number a mark stated.
  Loose pieces are chained into walking order from their **loose ends** first, so an open run is one line
  rather than two halves. The overlay draws every fifth block as an **index** line, heavier and the only one
  labelled, with the label placed on the line's straightest stretch; forty equal lines would say only that
  there is a slope somewhere. It follows its own toggle rather than a phase — a relief is geometry, so it is
  worth seeing while the shapes over it are still being drawn, which is exactly when the paint preview is not
  — and reuses the paint seam wholesale (debounce, post the live layout, load the reply as a canvas layer,
  drop replies overtaken by a newer edit). No JS twin: the reply is lines, so the client's whole share is
  stroking points it was handed. (`Geom/Relief/Contours.cs`, `SketchReliefEndpoint`, `sketch-render.js`
  `paintRelief`, `sketch-bridge.js`, `SketchTool.razor`; 9 + 3 + 6 tests)

- **Relief: a phase where terrain is stated, and marks are placed things (S41).** The five mark kinds solved
  and exported but could only be written as JSON. A **Relief** phase now sits between Draw and Theme — the
  order is the dependency, since a relief is geometry and changes what the rasterizer emits — with four canvas
  tools (spot height · ridgeline · bench · scarp), a list of what each island states, and an inspector for the
  numbers on the selected mark. It takes the Dressing phase's shape wholesale: a document (`ReliefDoc`), a
  controller with select / drag / point-grips / delete (`canvas.reliefTools`), a list and an inspector bound
  to the selection, per-kind settings carried across placements, and a bridge surface of flat methods. Entering
  the phase turns the contour overlay on with it, so the statement and the surface it produced are on screen
  together — the only way a mark can be tuned by eye.
  **Three things differ from dressing, and each is the model asserting itself over the borrowed shape.** A prop
  is placed on the map; a mark is placed **in an island**, because that is the unit a relief is solved over —
  so the island is fixed by where a trace *starts* and never revised. Judging it by coverage would break the
  one gesture the clipping rule exists for: a mark dragged past an edge raises the ground into a corner and
  stops, and ownership by area would hand it to whichever island the overhang crossed. For the same reason a
  mark, unlike a prop, may be dragged **off** its island entirely where a prop's drag stops at the void. And
  the **rim** gets no tool: it holds the whole outline, so there is nowhere to put it — it is a switch on the
  island, one that writes the rim **first** in the mark list, since a rim written last cuts a doorway through
  both ends of every ridge reaching the outline. A first mark in an island starts at that island's own **base**
  rather than at the last mark's height, which would state a cliff nobody asked for.
  **Colour carries the height, not the kind** — the opposite of the dressing overlay's rule and the right way
  round here, since every mark does the same thing to the ground and differs only in where and how high; the
  drawn shape already says which kind it is. Each mark wears its own number, and the two that state more than
  one wear both: a falling ridgeline shows its ends, a scarp its drop. A mark carries an **id** on the wire —
  the solver has no use for it, but a relief that renumbered its marks on load would move the selection under
  the author's hands. (`relief/relief-doc.js`, `controllers/relief-controller.js`, `render/relief-render.js`,
  `SketchReliefList` + `SketchReliefInspector`, `SketchRelief.cs`; 23 + 1 tests)

- **Relief: the push is drawn, not typed (S50).** A summit stated as a position and a radius can only be
  round, and the roundness was not a style but the shape of the only footprint that could be typed. A **Push**
  tool traces a ring like any other, and travels the *same* placed-thing pipeline as the four marks — one id,
  one selection, one set of point grips, one body drag, one list row — because what separates a push from a
  mark is not how it is drawn but what it does: a mark is a **constraint** (the ground here IS twelve, and two
  over the same ground must argue), where a push is a **relative lift** applied to the solved surface
  afterwards, so two over the same ground simply **add**. It is stored in the relief's own `pushes` array and
  carries no `kind` on the wire — the array already says what it is, and a field repeating that would be one
  more thing able to disagree; the word is added back when the document hands a push out. The inspector holds
  lift (negative digs), **skirt**, **crown** and roughness, and the canvas draws the skirt as a dashed outline
  at the falloff distance — the difference between a push and a bench made visible, since a bench ends at its
  outline where a push is still moving ground past it. Which side of the ring is "out" comes from the ring's
  own signed area, so a ring traced either way round gets its skirt outside it rather than inside, where it
  would read as a smaller push. **Per-vertex lift** is edited as one number per ring corner, expanded from the
  single amount so an author who wants one end lower has a number to change — and the document collapses the
  array back to one amount when every corner agrees, so undoing a variation leaves the push it started from
  rather than an array that happens to be flat. A per-corner array is never carried to the next push: it is
  sized to the ring it was stated on. (`relief-doc.js`, `relief-controller.js`, `relief-render.js`,
  `SketchReliefInspector`, `SketchReliefList`; 11 JS + 3 C# tests)

- **Relief: a contour is grabbed and moved to state a height (S53).** The overlay drew the solver's answer and
  the tools placed marks, and nothing joined them — the reading of a surface sat beside the form that edited
  it. A press near a contour now **grabs** it, and moving it writes a `line` mark at that contour's **own
  level** along its new position: a contour is a line of constant height, so moving one says the ground
  reaches that height here now. Two decisions carry it. **Index lines win a press** inside a slack, because
  several contours run close together on a steep face — that is what steep means — and the heavily drawn ones
  are the only ones an author can aim at. And the whole line **moves** rather than bending under the pointer:
  a contour has hundreds of points and a drag has one, so bending it locally would need a brush radius, a
  falloff and a rule for the ends — three settings to express what is one statement. A placed mark wins the
  press over a contour beneath it (a mark is a thing an author put there; the contours are what the solver
  made of them), the written mark is simplified like every other traced mark, and a contour pressed without
  moving states nothing. (`relief/contour-drag.js`, `relief-controller.js`; 7 tests)
- **Relief: a preview resumes instead of rebuilding (S52).** Every preview solved from flat, so each edit paid
  for the whole surface to be brought into existence again — and every preview is one small edit after the
  last. Each island's solve now resumes from the surface its previous preview settled on
  (`ReliefPreviewCache`, a bounded LRU keyed by map and island, matched on the exact footprint since a field
  is an array indexed by the grid it was solved on). **It cannot change an answer**, and that is the design
  rather than a hope: the relaxation stops when the field stops moving, so a resumed run that reaches that
  tolerance has reached the surface a cold one would — and `Lattice.Relax` now reports whether it *settled*,
  so a resume that fails is discarded and the cold cascade runs. The fallback is deliberately not held to the
  resume's sweep budget: a caller offering a head start may cap the attempt cheaply, and inheriting that cap
  would answer an unfinished surface in exactly the case the fallback exists for. The cache is handed the
  **unshifted** field, since what the rasterizer returns has its layer's `base_y` added and feeding that back
  would seed the next solve a whole layer high. (`ReliefSolver`, `ReliefPreviewCache`, `SketchRasterizer`;
  8 tests)

- **Relief: the readback — what the terrain charges, not whether it is flat (S43).** A relief walkable
  everywhere is a field rather than a map, so a single walkability score answers the wrong question: it ranks
  every barrier an author placed on purpose as a defect. `ReliefReadback` (`PgmStudio.Analysis`) reports at
  the game's **three thresholds** instead — 0–1 is a jump, 2 costs a placed block, 3+ is building in earnest —
  and none is a fault. Per tier: the share of boundaries a player can cross, the **places** that leaves, how
  much ground the largest holds, and the **ledges** stranded off it. That last split is what stops "one
  connected map with twenty cliff-top shelves" reading as "twenty-one pieces"; a place holds at least a
  hundredth of the ground. Faces are grouped by **which way they look** before being joined — a face is a
  thing that faces a direction, and joining the brink without regard to it wraps a 6×6 monolith's four sides
  into one twenty-cell run that qualifies as a cliff, which is the exact call the rule exists to get right.
  Crossings are counted **both ways**, because a drop is free the way it falls: a face that refuses a crossing
  one way lets it through the other, which is a one-way cliff rather than a wall. And the symmetry error,
  which nothing else in the report would show — an unfair map looks identical on every other measure. Served
  at `POST /map/{slug}/sketch/relief/read` next to the document it describes, which is what makes a relief
  correctable by a generator or an agent rather than only by eye, and shown as a **What it charges** panel in
  the Relief phase, fetched on a button rather than on every edit. (`Analysis/Playability/ReliefReadback.cs`,
  `SketchReliefReadEndpoint`, `SketchReliefReadback.razor`; 9 tests)

- **Relief: shapes erected out of the field, and the stair the block step owes (S44).** A relief makes rolling
  ground; what makes a map is the thing standing in it. One word on a shape says how its top is decided once
  its island carries a relief — **level** cuts a flat top at an absolute height (a mesa, whose faces are
  cliffs), **raise** holds it a fixed amount above the ground under it (a monolith or plinth, which keeps its
  prominence wherever it is dragged), **sink** the same downward (a quarry). Absent, a shape is ordinary
  ground and the relief is what its ground does — the default has to stay the default, or a drawn board would
  become a staircase of plates. Erected shapes are applied **after** the relief, which is the whole of what
  makes them erected, and they contribute their footprint without their thickness deciding the height: read
  before that separation, a `raise` found its own plate under itself and stood proud of it. `raise`/`sink`
  read the ground at the **median** of the cells covered, so the result is one flat-topped thing standing
  proud rather than a blanket following the hillside — flat-topped only when the shape says so, since the top
  is evaluated per cell through the shape's own height function: anchor heights and slopes compose with all
  three modes, so the mode decides what the surface is measured *from* while the height function decides what
  it *looks like*, and a sunk tilt is a quarry whose floor drains. A **skirt** of N blocks decides how hard a
  shape lands, blending the top toward the ground it meets across the outermost N cells of the footprint by
  inward distance from the outline; each cell blends toward the height immediately outside it, so a shape
  crossing a relief eases into low ground downhill and high uphill instead of levelling the slope. Measured,
  a skirt of 7 takes a mesa's worst edge step from **17 blocks to 2**, while a monolith left at 0 keeps its
  sheer face — which is the distinction, a landform sits in the terrain and a structure stands on it.
  Nothing downstream needed teaching — the painter already
  classifies a column by its neighbours, so a mesa face arrives as an edge with a known drop and is painted as
  a wall under a rim.
  **The stair repair** ships with it, being the same compositing question from the other side: snapping to a
  two-block step turns every riser into a wall, and a 60×40 hillside terraces into **six separate places**
  with nothing about the surface saying so. `StairRepair` cuts one stair per stranded place through its
  **cheapest** riser — the smallest intervention that reconnects, moving under eight cells and the walkable
  share by under 2% — and **refuses rather than half-cutting** when a stair would run out of footprint, since
  a riser plus a partial cut is the same map with a scar in it. On a mirrored map the surface is **folded
  again** afterwards: the repair decides things by walking the map, and a walk has a direction a half-turn
  does not preserve, so unfolded it hands one team a stair the other lacks. (`Geom/Relief/StairRepair.cs`,
  `SketchRasterizer.Erect`, `SketchInspector`; 15 tests)

- **The path primitive reaches the ground it draws on (S55).** A path is the one shape stored as something
  other than its own outline — its vertices are an **open** centerline and its radius a half-width — and the
  band those imply is what every consumer below the sketch expects, since island detection, the orbit fan,
  per-anchor height and the world export all read a ring. Deriving that band is `Geom.Algorithms.PathBand`
  and its JS twin, both already shipped for the dressing stroke; what was missing was the arm that reaches
  them. `RingOf` had no `path` case and fell to the empty ring, so a drawn path rasterized to **zero
  columns** — no terrain, no theme scope, no relief footprint. `toRing` had no case either and its default
  arm throws, so a committed path could take out the island recompute and the shape repaint, and the live
  preview handed `pathRing` a `vertices` key where it reads `points`, so the band never drew while it was
  being drawn. All three now route to the band, and `path_edge`/`path_seed` are properties on `SketchShape`
  rather than keys surviving only because the blob is stored as text. Width is the authored number to the
  block — radius 2 rasterizes 4 columns across, radius 6 twelve — and the height fields mean on a path what
  they mean everywhere else, so a raised causeway is a path with a thickness and not a new kind of shape.
  A path **mirrors as its band, not as its centerline**: a reflection reverses handedness, so re-deriving
  the band on the far side would swap the edge a rough or tapered width was drawn on. Measured over a
  mirrored island, zero cells differ from their image. (`SketchRasterizer.RingOf`/`MirrorShape`,
  `geometry/shape.js`, `sketch-draw-controller.js`; 6 + 4 tests)

- **A shape can leave its island's relief (S48).** The island is the unit a relief is solved over, because
  solving per shape is a different and wrong answer — a mark outside a shape says nothing to it, and measured
  over three abutting pieces the seams step **8 and 7 blocks** against **1 and 1** for the fusion. The fusion
  is not always what an author wants, and the case that decides it is a built thing standing on the ground,
  whose floor is not terrain. `Participation` had been modelled and tested in `Geom` since the solver landed
  with nothing reading it; the shape now carries the word. **hold** pins the shape at its own stated top and
  the surrounding surface is solved knowing where it has to arrive — a walled town the valley runs up to;
  **exclude** takes the footprint out of the solve entirely, so the land is whatever that outline would have
  produced at any height — a citadel on its own plinth. Both are boundaries and the land differs under
  either, because a hole has an outline the relaxation must bend around; what separates them is whether the
  shape's height travels into the ground around it. Measured over a compound on a 6→26 slope, the fused
  ground varies 3 blocks under it and both bindings flatten it to 0, while the land beyond the wall differs
  between the two. A held shape pins **one** level, read at its ring's centre, since a floor that followed a
  per-vertex tilt would be the slope it replaced; an excluded shape keeps its own column, tilt and all, and
  needs no stamping pass to do it — its cells were never in the solved field. The word is not asked of a
  shape declaring a `height_mode`: that shape already stands out of the field, and `raise`/`sink` read the
  ground under their own footprint, which an excluded footprint would not have. (`SketchRasterizer.SolveRelief`,
  `SketchInspector`, `sketch-bridge.js`; 7 tests)

- **Erected shapes reach the far side of a mirror (S57).** A mirrored island's copy takes its heights from the
  primary's solved surface, read back through the same transform — exactly symmetric by construction rather
  than symmetric to within a second solve's tolerance. An erected shape is settled *after* that surface,
  against the ground under it, so reading its height back through the mirror gave it the relief's answer
  instead of its own: a 24-block mesa stood at 24 on the authored side and **12** on its image, one team a
  mesa and the other a hillside. The image now gets the same passes the primary got, in the same order.
  Shipped with it, the two words a mirror was dropping outright — `height_mode` and `skirt` were absent from
  both arms of the shape transform, so an erected shape's image was ordinary ground before it was anything
  else. (`SketchRasterizer.RasterizeLayout`/`MirrorShape`; 1 test)

- **The height-mode and skirt controls are connected (S58).** The shape inspector had offered "Stands as"
  since the erect pass landed and a skirt since it gained one, and neither reached the document: the bridge
  had no `setHeightMode` and no `setSkirt`, so the dropdown threw into the console and the shape kept whatever
  it had. Both exist now, alongside `setReliefScope`, and all three write **absence** rather than an empty
  string — a shape without the word is ground, and a shape carrying `""` is a shape carrying a word nothing
  reads. (`sketch-bridge.js`)

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
- **A stated structural height survives a recompile (B107, backend half).** A spawn or wool-room piece is
  projected into the sketch as a `Role`-tagged shape whose `Floor`/`BaseHeight` already drive the relief's
  hold-pin — but `AppendStructuralShape` overwrote both with the plan's flat `surface` on every compile, so a
  correction made where the ground is actually known could not survive the next one. `SketchShape` now carries
  `height_authored`, and `SketchLayout.CarryStructuralHeight` merges an author-marked shape's height onto the
  freshly compiled layout, matched by **`intentRef`** — the team/owner:colour identity that survives a
  recompile, where a shape id and an island id are both regenerated. Marking the *field* rather than the shape
  keeps rect and position tracking the plan, and an unmarked shape still follows the plan's surface, so the
  carry cannot silently mask a deliberate plan-side height change. It follows the `CarryRelief`/`CarryFinish`
  precedent in `SketchFromPlanEndpoint`, and is gated end to end: the test asserts the relief solve holds the
  surrounding ground at the carried height rather than that JSON round-trips.
### Agent-drivable map generation — what sixteen agent-designed boards exposed (B78, B80–B90)

Sixteen maps were designed by an agent driving the system end to end and every one of them built, which is
the result worth reading first. The faults the boards then showed are recorded in `docs/tools/mapgen-review.md`;
these are the ones that shipped a map that could not be played as intended, and every one of them was silent
— the map built, loaded and looked correct from above.

- **A generated map goes out through the export composer (B80).** `tools/mapgen` called `XmlWriter` directly
  and skipped `MapXmlComposer`, so a board shipped without `itemkeep`, `toolrepair`, `itemremove` or hunger
  depletion (carried by 81–99% of the corpus) and without the reordering that puts `not-build-area` last —
  the rule that holds players out of the void. Routing through the composer restores all four and the
  renewables; a kitless map now reports rather than silently dropping its loadout rules. The `unbreakable`
  question was settled on the corpus rather than assumed: of 301 maps carrying `<toolrepair>`, 291 (97%) also
  mark a kit tool unbreakable, so the generated kit keeps it.
- **A destroy kit's pickaxe is paired to its goal (B81); the unbreakable-goal refusal it also shipped was
  retired by `B134`.** An iron pickaxe breaks obsidian without dropping it, so the mismatch B81 guarded
  against made a raid slow, never unwinnable — the premise it was filed against was wrong, though the fault
  it fixed was real: every destroy board in the batch had shipped the same fixed iron pickaxe regardless of
  goal material. What still stands: the pickaxe derives from the goal's material (obsidian → diamond,
  anything softer → iron), the spec can name that material (`objective_materials`, monuments only — a core's
  casing is not a knob), and the generator still refuses a material the stamper cannot build, because
  `DestroyableMaterials` silently falls back to obsidian while the generator writes the authored name
  verbatim, which would otherwise ship a goal whose declared material matches nothing in its own region.
- **A goal standing over void refuses the build (B82).** Checked against the ground already rasterized before
  dressing. It immediately caught three shipped `dtcm` specs whose cores stand in void (cause filed as B94).
- **The relief holds a room's floor instead of carving through it (B83).** The task's premise was false and
  checking it was the work: the structural shapes the compiler projects are `Role`-tagged annotations the
  rasterizer skips everywhere, so stating `relief_scope` on them would have been a no-op. The honouring was
  fixed — a room binds to its island by footprint overlap — and the compiler now states `hold` and the piece's
  own surface on the authored orbit image. Measured: standing bedrock 9740 → 6528 on one board, because
  `StampFoundation` had been filling a column inflated by relief pushed above the room's stated height.
- **A spawn's door faces the board, not the drop (B84).** Resolved against the piece's own open sides before
  the symmetry fan, so it reflects and rotates correctly rather than naming a compass direction.
- **Nothing is placed inside anything else (B85).** Overlap is decided against the occupied resting cells of
  everything already standing, after fanning; `tools/mapgen` samples candidate sites from one canonical half
  of the orbit so a prop's mirror is never independently re-drawn.
- **A prop is decided once for its whole orbit (B86).** `Decorator.Fan` and `PlaceHouse` collect every orbit
  image before writing any, so a mirrored board no longer keeps a tree on one side and drops its mirror.
  Verified at world level: 0 of 606 leaf columns and 0 of 208 log columns unmatched across the mirror.
- **A tree's protection is decided on the cells it rests on (B78).** Testing every cell a prop occupied made a
  taller tree likelier to be dropped whole, so height was silently inverted — a grown oak fell 1545 → 108 → 0
  leaves across heights 8/12/20 and now climbs 1545 → 3361 → 8136. Gated on the corpus: of 124,374 columns
  carrying both a leaf and an unambiguously man-made block, 106,354 (85%) have the leaf above it, and 204 of
  318 maps have at least one — hand-built trees overhang their own structures routinely. `tools/mapgen`'s
  grown-tree ceiling of 14 is removed.
- **A wool-room chest opens into the room (B87)**, its facing resolved from the room's own door rather than a
  second rule. **A destroyable stands on a one-block-thick 5×5 bedrock platform (B88)** so it cannot be
  undermined. **Every goal carries a marker above `max_build_height` (B89)** — a cube over a wool room, a
  cross over a destroyable or core, coloured to the goal — placed where it cannot be reached or griefed.
- **Every stage answers with a picture (B90).** A plan now renders as a raster as well as SVG
  (`GET /plans/{id}/png`), both drawn from one shared `PlanBoardScene` so the two encodings cannot disagree;
  the PNG encoder moved down to `PgmStudio.Geom`, four world read-backs moved from the round-trip tool into
  `PgmStudio.Minecraft.Render` and each gained an entry point taking a `VoxelWorld` directly, so the CLI and
  the generator call the same renderer. `tools/mapgen --stages` emits eight named images — plan, heightmap,
  contour, surface, dressing, topdown, traversability, structures — off the world it just built.
- **The traversability read knows what a build region is, and a core lands on ground (B93, B94).** The stage
  image had been reading navigability as ground plus headroom, which is blind to the way a capture board
  actually joins up — islands connected by build regions rather than by walkable ground — so it reported a
  false disconnection under a name this project had already given to the build-region-aware question. It now
  finds the apply rule gated on a void filter, reduces its region through a new `RegionBoxes.FootprintXZ`
  (added because `Of` silently returned nothing for a `rectangle` region, which carries no Y bounds), and
  joins those columns to the navigable graph while tinting them so bridged void still reads as standing on
  nothing. A water lane stays unbridged by construction, since it carries no such rule and opens on a timer —
  a board connected only after 45 minutes is not a connected board. One measured board went from 7 components
  with 2 isolated markers to 5 with none. Alongside it, `Retarget` no longer offset a `dtcm` core two cells
  along unconditionally: it measured room-to-edge within the monument's own piece and took the direction with
  the most room, so the three shipped specs that `B82` was refusing built with their cores on ground.
  (`Retarget` itself, and the whole reduced spec format it retargeted, is gone — deleted by `B118`.)
- **The export gate asks the questions `tools/mapgen` used to ask alone (B116); a third one it added, `OB18`,
  was retired by `B134`.** `MapExportComposer.ComposeAsync` already builds a sketch-originated map's world
  and holds its resolved intent, so it re-asks `ObjectivePlacement.Check` — void, spawn, wool room — against
  the ground the rasterizer actually produced rather than the plan's rectangles (`OB17`), the case a subtract
  cut, a relief solve or a post-compile sketch edit opens and the compile gate never sees again, and the case
  a map begun in Sketch never reaches at all. And a tree, boulder or building standing inside a goal's
  four-block clearance refuses rather than exporting silently (`OB19`, `DressingScope.GoalClearanceViolations`)
  — fanned across the map's own symmetry, so a violation only one team's mirror carries is still caught, and
  ground cover is exempt throughout. `B116` also wired `DestroyKitPairing.Unwinnable` in behind `OB17` as a
  third gate, `OB18` — a kit/material mismatch, refused as unwinnable on the premise that an iron pickaxe
  cannot mine obsidian at all; `B134` found the premise false (it breaks obsidian, it just does not drop it)
  and removed the gate, leaving the kit's pairing to `RequiredPickaxe`'s generation choice rather than a
  legality check. The two surviving refusals answer 409 from the one composer, so the studio and every
  headless driver are gated identically. This supersedes `B101`, which asked for the deleted `tools/mapgen`
  `KeepOut` list to learn about destroyables: a refusal on the export gate reaches every driver rather than
  one tool, and names the prop and the goal rather than dropping the placement silently.
- **The obsidian-mining claim behind `OB18` was false, and the refusal it justified is gone (B134).**
  `DestroyKitPairing`'s docstring asserted that an iron pickaxe does not mine obsidian at all, so a
  kit/material mismatch was an unwinnable map; it is wrong. Vanilla breaks any block with any tool given
  enough time — what a tool below the required tier changes is only whether the block *drops*, not whether it
  *breaks* — so a destroy objective, which only asks for the block gone, is won either way, merely slower.
  `MapExportComposer`'s `RefuseUnwinnableGoals` and `DestroyKitPairing.Unwinnable`/`KitPickaxeMaterials`/`Rule`
  are deleted outright rather than downgraded to a warning: after the correction there is nothing wrong left
  to warn about, and a warning surface for a non-defect would be new response-contract weight (`Contracts`,
  `Client`) spent on noise. `tools/mapgen`'s matching hard throw is deleted with it. `RequiredPickaxe` is
  unchanged and still upgrades a generated kit's pickaxe to match its goal's material (obsidian → diamond,
  anything softer → iron) — the corpus norm for a fast raid, now stated as a generation choice rather than a
  legality check. `MiningTiers`'s docstring is corrected to say what its table actually encodes: the tier
  required to *drop* a material, not to break it. Every restatement of the false claim — `DestroyKitPairing`,
  `MiningTiers`, `docs/pgm/destroyables-and-cores.md`, `docs/tools/capabilities.md`,
  `docs/tools/configure.md`, `mapgen-review.md`'s `MG18` row, and this section's own `B81`/`B116` entries —
  is corrected in the same commit.
- **A destroy or core board no longer claims to be a capture map (B131).** `MetaGenerator.Objective` used to
  branch only on wool count, defaulting the zero-wool case to `"Capture the enemies' wools!"` — so every
  destroy or core board the studio ever generated shipped a scoreboard line and description naming an
  objective it did not have. The objective text and the declared `<gamemode>` now both follow which of
  wools/destroyables/cores the intent actually carries, phrased from corpus-sourced clauses ("capture the
  wool(s)", "destroy the enemy's monument(s)", "leak the enemy's core(s)") joined when a board mixes more
  than one, and the gamemode using the same derivation `Domain.Gamemodes.From` applies to a parsed map
  (`destroyables-and-cores.md` OB7) rather than a fixed `"ctw"`. Cosmetic only, as the entry that filed it
  said — PGM already resolved the real objective against the `<core>`/`<destroyable>` elements regardless of
  what the scoreboard's first line claimed.
- **The export path is a project, not a folder inside the web app (B119).** `SketchWorldBuilder`,
  `MapXmlComposer` and `MapExportComposer` — plus the four map-facing readers only they used
  (`DressingScope`, `TerrainThemeScope`, `TeamTerritory`, `RoomStyleScope`) — moved out of `Api/Services` into
  a new `PgmStudio.Export`, which references `Domain`, `Analysis`, `Minecraft` and `Pgm`: every one of them
  already reachable from `Api`, so the cut added no dependency edge and inverted none. `MapExportComposer`
  itself split at its DB boundary rather than moving whole — `Compose` is now synchronous and pure, taking
  the traversability segments, the stored intent and the cached surface/resources as plain arguments instead
  of `PgmDb`/`FeatureData`; the DB reads that used to open the method stayed behind in `Api` as
  `MapExportLoader.ComposeAsync`, the only thing in the pipeline that still touches the store. `tools/mapgen`
  and `tools/PgmStudio.PatternMap` now reference `PgmStudio.Export` instead of the whole of `Api` — ASP.NET
  Core, FastEndpoints and the DB layer along with it — which is what made `B118`'s job cheap: a spec-driven
  map now goes through the one export path both the studio and a CLI reach, with nothing left to duplicate.
- **`tools/mapgen`'s spec is an addressing layer over the real documents, not a reduction of them (B118).**
  The site sampler is gone outright — `trees`, `village`, `houses` off `MapSpec` and `Forest`, `Settle`,
  `Placed`, `KeepOut`, `Level`, `Clear`, `FannedAround` out of `Program.cs` — because the studio has none: a
  tree is cover, and where cover stands is a gameplay decision no scatter pass gets to make
  (`docs/tools/sketch.md`, `mapgen-review.md` MG9). `Retarget` is gone with it: a destroyable needs no room,
  no lane and no protection region, so rewriting a capture board's wool markers into monuments (MG1, the
  review's largest entry) put every goal at the back of a corridor it never wanted — a destroy board is now
  authored as its own `plan`, `DestroyablePlacement`/`CorePlacement` placed on whichever piece the design
  wants. What replaced both is a new `PgmStudio.Pgm.DocumentOverlay`: `layout` and `intent` hand a
  `SketchLayout`/`MapIntent` fragment through **verbatim**, merged onto whatever `compose`/`plan` produced —
  an object key on both sides merges key by key, an array key on both sides is appended to, anything else the
  fragment replaces — so a spec can add a shape with its own theme, floor, base height, per-vertex anchor
  heights and `relief_scope`, a `TerrainTheme` with its rim band and per-shape scope, or a `MapIntent`
  fragment reaching a defence wall or an iron cube, none of which the surviving convenience fields
  (`theme`, `relief`, `room_shell`) could say. Closes `mapgen-review.md` MG29 and MG1.
- **A vertical section, textual and drawn (B121).** Every world read-back before this one looked straight
  down; `layered` exists to vary a material down a riser and nothing could check one. `ColumnReport`
  (`--column <regionDir> <x> <z> [x z ...]`) prints one or more columns bedrock to sky, every solid block
  named off the same `BlockPalette` table every other renderer reads. `SectionRender`
  (`--section <regionDir> <outPng> --x <lo> <hi> --z <fixed>`, or the axes swapped) draws an axis-aligned
  vertical slice as a PNG, with a horizontal scale ruled onto the image every N blocks (heavier every fifth)
  so a riser, a ramp's step heights, a building's storeys or a void column are counted rather than guessed —
  and two different kinds of "nothing" are drawn two different ways, a pale neutral for ordinary air inside a
  loaded chunk against the near-black every other renderer already uses for a hole nothing covers. Both live
  in `PgmStudio.Minecraft.Render` beside the plan-view renderers, reading a region directory or an in-memory
  `VoxelWorld` the same way. Verified against the two `pgm-studio-mapgen` worlds: ClayClay's south approach
  wall (bedrock, x17–29, z51–52, y11–15, a cobweb course at y16) stands proud of the flat clay field it grows
  from by 3–4 blocks in the section image; a probed column at (20, 44) reads the map's layer stack top to
  bottom (lime stained clay at y12 over clay to y1 over bedrock at y0); and Ashen Quarry's quarry-mouth void
  is a diagonal notch, not a straight edge — missing columns at z26/27/28/29 run 5/23/42/60 wide (x −9..−5,
  −26..−4, −44..−3, −61..−2) and close to zero at z30, which is exactly the diagonal land-polygon boundary
  `AGENT-REPORT-2.md` describes.
- **Three read-backs stopped misinforming (B124).** `BlockPalette.Name(31, 1)` answered "Tall Grass" for the
  plain single-block plant vanilla itself calls "Grass" — tall grass is the two-block `175:2` — so every
  surface census, decoration table and column probe told an author the opposite of what a `tallShare: 0`
  setting had actually done; the family (base and `31:1`) is now named "Grass". `--structures` found a
  building by material alone (`IsBuilt`, true/false, not identity), so any built column touching another —
  a spruce roof's edge against the stone-brick plaza it stands over, the plaza against the stone-brick
  cottage on it — joined the same component whatever either was made of. The flood now also requires two
  neighbouring columns' tops to be within `--max-step` (default 4) of each other, the same discipline
  `--buildings` already applies to a roof. Measured on Ashen Quarry (`--min-area 20`): 15 structures before,
  31 after; the two ~9,000-cell town-square blobs shed exactly the 1,550 cells of spruce-wood-slab and
  dark-oak-plank roof that had been touching the plaza directly, now split into fourteen 20–140-cell
  structures of their own. What the step alone cannot reach: the remaining ~8,300-cell blobs read `roof y
  50..50` — perfectly flat — because a cottage roofed flush with the plaza's own paving height has no step to
  find, elevation or not; that is the stone-brick-cottage case the review named, and it is still lost. The fix
  is therefore partial, and a full separation would want the enclosed volume a room stamps rather than a
  roof-height comparison. **That residual gap is closed by B133**, which gives `--structures` a recorded
  extent to read instead of a material-and-step guess wherever one is available. `Traversability`'s degenerate refusal — every point
  off-grid, so `main` stayed 0 and an isolated filter of `Component != main` matched none of them — reported
  "0 spawn/wool point(s) are not reachable" and named nobody, the one case an author most needs a name; the
  isolated filter now also catches `Component == 0` directly, and the all-off-grid message reads "no
  spawn/wool point is on navigable ground". `NavigationPoints` also reads destroyables and cores now, visible
  in every `Points` list — but, following CLAUDE.md's playability-oracle rule, they do not gate `Connected`:
  a destroyable floats above its terrain and is broken from range by design, so reachability-like-a-wool is
  an open gameplay question left recorded (`docs/design-decisions.md`) rather than answered by fiat.
- **The claim that an agent can author a map by driving the real documents is tested, and it holds (B120).**
  Six runs across three models — Opus 5 in a cloud container, Opus 5 local on a 1M-context build, Sonnet and
  Haiku 4.5 — authored **nineteen loadable maps** and seven reports against one shared brief plus each model's
  own designs, driving `PlanModel`, `SketchLayout` and `MapIntent` through the documented endpoints with no
  capability added in `tools/` and no second format. Every board that carries an objective builds, exports and
  loads. The deliverable the entry actually asked for was the honest list of what could not be said, and the
  runs produced it: six fields whose unit or scope lives only in the source, four refusals that pass
  vacuously, three read-backs that answer a different question from the one their name owns, and an evaluator
  term that cannot see a `subtract`. The author's review of twelve boards turned that into **forty-eight filed
  findings** (`B141`–`B188` in `BACKLOG.md`, bucketed for dispatch) of which twenty enforce rules that are the
  author's and that nothing checks. Two models' worth of caveat travels with the result: Haiku produced three
  loadable boards in run 1 and **zero** in run 2, having spent that run reading code to confirm capabilities
  it then never used, and its report describes both empty shells as "verified working" — which is what
  `B189`'s separate reviewer agent exists to catch. What the trial did **not** establish is that the boards
  are good; that is `B189`'s art direction and the composition entries behind it.
- **A board declaring more than one objective kind now writes a `<gamemode>` PGM can load (B155) — the one
  fatal finding of the forty-eight.** `MetaGenerator` had joined the derived modes into a single element
  (`string.Join(' ', Gamemodes.From(…))`), so a mixed board shipped `<gamemode>dtm dtc</gamemode>`; PGM parses
  `<gamemode>` as a **repeated** element holding one id each (`MapInfoImpl.parseGamemodes` loops
  `getChildren("gamemode")`, resolving every one against the closed 25-value `Gamemode` enum via
  `Gamemode.byId`, matched case-insensitively) and throws `InvalidXMLException("Unknown gamemode")` on the
  first id it cannot resolve — so the map failed to load, not merely to describe itself oddly. The premise
  behind the join was half right: PGM does not read `<gamemode>` to decide which modules run, but it does
  validate the element strictly, and `MetaGenerator` and `Domain.Gamemodes`' own docstrings had flattened "not
  authoritative" into "free text" — corrected in the same commit, alongside `destroyables-and-cores.md`'s `OB7`
  statement, which carried the same half-truth. `XmlWriter` now emits one `<gamemode>` per mode;
  `MapXml.DeclaredGamemode` is a list end to end rather than a scalar (`MapParser` reads every `<gamemode>`
  element instead of the first, and `Deserializer`/`Serializer`/`MapReader`/`MapWriter`/`MapImporter` follow);
  and the export composer refuses a declared label outside PGM's enum before it ships (`OB20`,
  `MapExportComposer.RefuseUnknownGamemode`, checked first and against every map regardless of origin — it
  needs no world and no resolved intent), the same shape as `OB17`/`OB19` (`docs/tools/configure.md`).
  Confirmed against PGM's own source (`Gamemode.java`, `MapInfoImpl.java`): the enum, the loop, and the throw
  are exactly as described. `tallow-kilnrow`, `ashfall-scar` and `basalt-reach` were the three committed maps
  that failed to parse; their `map.xml` is corrected by hand already, and a rebuild against the fixed studio
  no longer reproduces the fault.
- **A sketch-built map's water lanes reach `map.xml` (no id — the one board-rule slip the audit found).**
  A lane authored on the sketch was stored and never written, so `tallow-weirgate` shipped with one door on
  its east wool for the whole match instead of a second approach opening at 45 minutes — the board that was
  built is a different map from the one its own specs describe. Fixed at `10e031d4` with a regression test.
  Recorded here without an id because it shipped without one, which is the exception that proves the rule in
  `CLAUDE.md` § "Status & task board": a fix with no id is a fix nobody can trace to a decision.
- **A goal's height is an offset over solved ground, not a plan tier manufactured to carry it (B128).**
  `PlanCompiler` used to bake a destroyable's or a core's `Anchor.Y` from `piece.Surface` — the plan's flat
  nominal world — at compile time, before the layout was rasterized or the relief solved. Nothing downstream
  ever read that Y (`ObjectiveStamper.DestroyableBox`/`CoreBox` already resolved a goal's real floor from the
  world build's own `surfaceTop`, plus `float`), but authoring still had to chase the wrong ground: to give a
  destroyable a chosen height, an author had to draw a plan piece standing at that surface, whose only purpose
  was to give the marker something to ride — Ashen Quarry's mesa was pushed back into the plan as a flat tier
  at 58 for exactly this reason, then promoted to a polygon again to recover the outline it already had, so
  the landform existed twice, in two idioms, in two files, with nothing checking the two agreed. `float` is
  now measured from the ground the world build actually solves, and a destroyable/core marker may name no
  plan piece at all — `DestroyablePlacement.Piece`/`CorePlacement.Piece` empty reads `at` as an absolute board
  position (`PlanCompiler.ResolveGoalAnchor`) rather than a piece-relative offset, so a goal can ride an
  authored sketch landform with no tier manufactured to carry it, and `PlanValidator` no longer reports the
  absent piece as a dangling reference for these two marker kinds. The default float stays at 4 — a gameplay
  constant, not part of this fix. Leaves open: `B105` (the compiler still reads a piece's `Surface` as a
  literal world Y for spawns and wool rooms, and this task does not touch that) and `B107` (the canvas has no
  way to draw an absolutely-placed goal yet — only a hand-written or agent-authored plan can).
- **A stage image is a diagram now, not a photograph, and every one carries its own key (B98, B95).**
  `--topdown` (and every stage image it feeds) used to colour a column by its real block — stone, stone brick,
  cobblestone and andesite are all some shade of grey in the game, so the render painted one indistinguishable
  grey field wherever those met, and a tree stood on ground close to its own colour. The default reading is
  now `PgmStudio.Minecraft.RenderCategories`: five deliberately unrealistic hues — void near-black, water
  cyan, foliage violet, structure orange, ground a muted grey — chosen for maximum separation on the wheel
  rather than for resemblance to the material; `--material` switches back to the old per-block
  `BlockPalette` reading for a caller checking a theme's actual paint. `--topdown --layer
  ground|structure|foliage|objectives` isolates one question per image instead of drawing the combined view,
  and `tools/mapgen --stages` now emits `foliage.png` and `objectives.png` alongside the existing set — ten
  named images off six renderers. Every PNG this renderer, `--surface`, `--structures`, `--heightmap`/
  `--contour` and `--traversability-map` write now carries a legend baked into the image itself
  (`PgmStudio.Geom.Render.Legend`, backed by a hand-drawn `PixelFont`) — one swatch and name per colour
  actually used, plus a scale line stating blocks-per-pixel, wrapping and shrinking its own text so a narrow
  image never runs a label off the edge. The plan render (`PlanBoardSvg`/`PlanBoardPng`) carries the matching
  fix: a build zone now paints pink rather than a second shade of blue (`#38bdf8` and `#2563eb` sat about 23°
  apart on the hue wheel, both readable as "blue"; a build zone and the water-lane blue now sit over 45° apart)
  and a water lane draws under a diagonal hatch on top of its own colour, with a legend naming every role
  swatch and both zone kinds — the two shades of blue that once let a generated board's central build zone be
  read as water on a map carrying none. The role/zone colour constants moved to the new public
  `PlanBoardPalette` so a test can check the real values rather than a copy of them.
- **A cell's category is read from a recorded build, not guessed from its block, wherever one exists
  (B133).** `RenderCategories`, `StructureFinder` and `--structures` all asked the same wrong question of the
  same wrong source — "is this material one a world generates on its own" — which cannot separate a
  stone-brick cottage wall from a stone-brick plaza it stands on, or from a mesa an author painted to read as
  built: Ashen Quarry's town terrace and mesa hull both read solid Structure though neither is anything but
  terrain, and ground under-reported by exactly that much. `PgmStudio.Minecraft.WorldProvenance` is the fix —
  a per-column record of which pass claimed a cell, composited in placement order (`Ground` from the
  rasterizer, `Structure` from every stamp and every dressing-placed building that follows it, a later claim
  covering an earlier one) — built by `SketchWorldBuilder` alongside the voxels and persisted beside the
  region files a build writes (`WorldProvenanceFile`, one run-length-encoded sidecar per region directory,
  bundled into a downloaded world's zip too, since a block carries no provenance byte of its own).
  `RenderCategories.Of(blockId, provenance)` reads a recorded claim as authoritative for the Ground/Structure
  pair and falls back to the material estimate — the original single-argument overload, unchanged — when
  none was recorded; `TopDownRender` and `StructureFinder`'s `Run(regionDir, …)` overloads pick the sidecar up
  automatically, and the picture states which reading it used (`STRUCTURE READING: RECORDED PROVENANCE` /
  `MATERIAL ESTIMATE (NO RECORDED PROVENANCE)`, baked into the same scale line every render already carries).
  A world the studio only scanned carries no sidecar and keeps the material estimate, which stays the only
  reading available for it. **Closes B124's residual gap**: `--structures` now reads a stamped building's
  recorded extent instead of flooding by material-and-step, so a roof laid flush with its own plaza — the
  case the step test could not reach — cannot fuse with it at all. Reproducing Ashen Quarry's own plan
  through the current pipeline (its original build predates this recording) makes the size of the fix
  concrete: read by material and step alone the whole board floods into three components, the largest 80,722
  cells; read by recorded provenance the same world reports seven structures matching exactly what was
  stamped, the largest 133 cells.
- **A dressing-placed building claims its stamped extent, not its wall rectangle (B137).**
  `DressingScope.StructureFootprints` used to fan `HouseProp.Footprint()` — the two-corner rectangle a style's
  walls stand on — across the symmetry orbit and stop there, so a roof's `overhang`, its `verge` and a
  `BeamStyle`'s log ends all lay past the claimed ground: the eaves ring kept whatever provenance claim the
  terrain under it had, and with a log verge it read `Foliage` by material — eleven houses on `quillon-barrow`
  drew eleven rectangles outlined in tree-colour. `HouseStamper.StampedExtent(ground, style)` is the fix: the
  wall rectangle grown by the greatest of the roof's `Overhang`, the beams' `Reach` and the one-block sill
  every footprint carries regardless, read straight off the style's own fields — the same ones the stamper
  already reads to lay the roof and the beam ends — rather than re-derived from voxels a second time. A porch
  needed no case of its own: it is carved out of the footprint it is handed rather than added past it, so its
  canopy never overhangs further than the main roof already does. Verified on `quillon-barrow`'s `d-h1`
  (overhang 1, verge `17:1` Spruce Log): the column one block outside the old claim now reads the verge as
  `Structure`, and the eaves ring is gone from the category render entirely.
- **The isolated foliage layer can read as a point and a measured radius, not a mass (B138).** The category
  render paints every leaf and log cell a build wrote, so a wood reads as one irregular violet shape whose
  internal structure means nothing — two crowns that touch fuse into one blob and the tree count, the measure
  that actually decides whether a board reads as wooded, cannot be read off it. A tree is authored as one prop
  at one coordinate; `DressingScope.TreeFootprints` now answers with exactly that, fanned across the map's
  symmetry and paired with `Decorator.CanopyRadius` — the farthest a leaf of that tree's own deterministic
  build (`TreeTemplate`/`TreeSkeleton` plus `TreeCrown`) stands from its trunk, the measured figure rather than
  a species-nominal guess, read off the crown's own geometry with no world needed to measure it.
  `TopDownRender`'s `--layer foliage` plots each as a softly-tinted circle grown to that radius with a solid
  trunk mark on top, so overlapping crowns build up density rather than fusing, and every trunk stays
  countable. A mode of the isolated layer alone — the combined view keeps painting the mass, since a player's
  cover is the leaves and not the centres — and it needs the dressing document, never `WorldProvenance`, which
  carries no tree claim at all. `tools/mapgen --stages` passes the build's own document automatically;
  `PgmStudio.RoundTrip --topdown --layer foliage` takes one as an optional `--dressing <layout.json>`, and a
  scanned world, carrying neither, falls back to the mass reading it always had — stated on the console rather
  than silently substituted. Verified on `quillon-barrow`: 46 points (23 authored trees × the map's `rot_180`
  symmetry), each a distinct, countable circle where the mass render showed solid clumps.

- **A structure's claim is the stamper's own reach, and carries an owner (B139).** `HouseStamper.StampedCells`
  now claims the exact union of cells a stamp writes — a roof ring — rather than a bounding box grown to
  contain a corner beam's reach, which had been claiming a phantom ring between two neighbouring houses and
  fusing them; `quillon-barrow` reports 26 structures again, the pre-fusion count, while keeping the eaves
  reading as building. `WorldProvenance` then gives every `Structure` claim an owner alongside its layer —
  the identity of whichever stamp made it: a dressing prop's own `Id` plus its orbit image
  (`DressingScope.StructureFootprints`), a wool room or spawn's index, a destroyable's or core's marker —
  defaulting to `WorldProvenance.NoOwner` for the rasterizer's own ground, which needs none. `StructureFinder`
  reads it directly: with a provenance record present, candidate columns are grouped by owner instead of
  flooded for adjacency, so two buildings that genuinely share a wall read as two findings rather than one —
  proven on a synthetic pair of flush, same-material 3×3 buildings, which read as one 18-cell blob with no
  owner recorded and as two 9-cell findings once each carries its own. `quillon-barrow`, whose houses do not
  touch, still reports 26 end to end. The sidecar carries the identity through a small id table
  (`WorldProvenanceFile`'s `owners` array) rather than a string per cell, with a run breaking on an owner
  change as well as a layer change; on `quillon-barrow` the sidecar grows from about 33 KB to about 35 KB for
  a full owner-per-claim identity.
- **`--surface` names the rest of the stained-clay ramp, hay bale, and says what it still cannot (B147).**
  `TerrainPalette.Families` covered stained-clay data `1, 3, 5, 9, 11, 12, 13, 15`; the other eight
  (white, magenta, yellow, pink, gray, light gray, purple, red) now each join the tone family the fired
  colour actually reads as — magenta and purple into `mauve`, next to the light blue clay already there;
  yellow into `gold`, beside yellow wool; pink and gray into `brick` and `dark`; white, light gray and red
  into `sand`, `dirt` and `rust`. Hay bale joins `gold` for the same ochre yellow wool already offers there;
  packed ice already had a family. Nineteen hand-authored, light-to-dark-ordered families in total, unchanged
  in count and order — only their membership grew. The renderer now states its own coverage the way
  `--topdown` states which structure reading it used: a `SurfaceReport.Result.Unnamed` list of the full cubes
  on the board no family names, folded into the `UNNAMED MATERIAL` legend swatch as
  `UNNAMED MATERIAL (N BLOCKS NO FAMILY CLAIMS)` and printed as its own console section. On
  `tallow-kilnrow`, unnamed ground fell from 6,624 columns (33.6%, mostly the eight stained-clay shades this
  closes) to 965 (4.9%) — the honest remainder, all four still genuinely outside the vocabulary: Quartz
  Pillar (e.g. `(8, 18, -89)`), Smooth Sandstone (`(31, 1, -75)`), Smooth Red Sandstone (`(20, 26, -68)`) and
  decorative Bedrock (`(33, 21, -57)`).
- **A provenance sidecar from before the owner table falls back instead of throwing (B148).**
  `WorldProvenanceFile.TryRead` guarded `File.Exists` but not the deserialize, so the pre-`B139` sidecar
  shape — a bare JSON array of runs, not today's `{owners, runs}` object — failed to convert and raised an
  unhandled `JsonException` out of every caller, taking `--topdown --layer structure` down with it on exactly
  the older worlds a census is most useful for. `TryRead` now catches the conversion failure and also treats
  a `runs`-less object the same way, both falling back to null exactly as its own doc comment already
  promised for a missing file. Verified against the failure directly — a copied `tallow-kilnrow` region with
  its sidecar replaced by the pre-owner array shape, by `{}`, and by non-JSON text all now render normally on
  the material estimate instead of crashing — and against the corpus: every `pgm-studio-mapgen` world with a
  current-shape sidecar (`marlstone-steps`, `tallow-kilnrow`, …) still reads `RECORDED PROVENANCE`, and every
  world with none (`ashen_quarry`, …) still reads the material estimate, unchanged.
- **`--buildings` declines a town it did not build rather than undercount it (B149).** Its roof-material
  heuristic is tuned against corpus houses — log-framed corners, a roof material distinct from any terrain
  block, an exact `--roof` match — and a studio-generated theme can defeat all three without being wrong:
  `marlstone-steps` roofs its houses in materials `IsTerrain` also reads as ground (`98`, `155`, `159`), so
  the clearance gate finds the roof and the terrain at the same height and drops the building before any
  other measure runs. A `--roof` spec spanning every material `--structures` shows the town's roofs actually
  use (brick, stained clay, sandstone, quartz, stone brick) finds only the two all-brick-roofed houses of the
  24 the sidecar records, for exactly that reason. Rather than report a partial count silently, `--buildings`
  now reads the region's own `WorldProvenance` sidecar first and declines outright when one is present,
  naming `--structures` (a per-building table, owner-grouped) and `--topdown --layer structure` (the
  picture) as the census that already has the exact answer — the heuristic is not loosened, since it stays
  correct for the corpus houses it was measured against; it simply no longer runs where a better answer is
  free. A region with no sidecar is unaffected: `ashen_quarry` still finds its 18 spruce/dark-oak house
  roofs, four of them full-cornered, exactly as before.
- **The capability handbook — what the system can be asked for, and where to say it (B91).** `docs/tools/capabilities.md`
  mapped the four documents a map is made of; it now also states the surface underneath the spec's shorthand, in
  pipeline order, every claim naming the type that carries it and the endpoint that answers it: the destroyable's
  material and the four words the stamper can actually build from, the defence wall and iron cube the composer
  never asks for, a `TerrainTheme`'s five buckets against the spec's four words (nineteen tone families crossed
  with six of fourteen pattern kinds), the relief's five constraint marks against the separately-composing push,
  and a `HouseStyle`'s course bands, window styles, door head, beams and storey stack with `Footprint`'s wings.
  Written for an agent that reads before it writes, which is the fault it answers: the tool reached for a random
  answer wherever an author would have reached for a deliberate one, because the format it was written against
  could only say one theme and a rim. Two claims were dropped for disagreeing with the code — a **core**'s
  material is not a knob (no field on `CorePlacement`/`CoreIntent`; obsidian fixed by DC1), and a wing-carrying
  `Footprint` is buildable but unreachable from a placed prop.
- **Map XML refresh** — `--refresh-xml` re-derives every map's entities via the editor write path
  (preserves world features/artifacts); recovered annealing_iv's missing region, which fixed the
  former stale-DB symptom. (D1, closed C10)
- **Dropped Bootstrap** — dashboard migrated to the studio shell; default `StudioLayout`;
  `/design` reachable from the dashboard footer link. (D4, satisfies D2)
- **README setup guide** — prerequisites, DB/user provisioning, dev + tests, and the two-step
  scan-out → import flow (incl. the stale-output `ROUND-TRIP DRIFT [kits]` gotcha + `--refresh-xml`
  fix). (B12)
- **A database test resets by emptying its tables, and the Api tests share one host (B73).** The two
  database-touching test projects spent most of their time arriving at a clean database rather than
  testing against one. Each of the 81 resets dropped all 39 tables and re-applied all 19 migrations —
  DDL, and InnoDB gives every table its own file to create and flush, so one reset cost **713 ms**
  measured. A test cannot tell that apart from the same tables emptied, which is one round trip of DML
  over rows that have already gone and costs **2.5 ms**. `tests/TestSchema.cs` is the shared reset —
  linked into both projects rather than written out in each, since what would be duplicated is the
  meaning of a reset and two copies of that is how they come to disagree about what a test starts from.
  The migrations still run **once**: the reset reads the applied version first and rebuilds where it is
  not the newest this build knows, which covers a fresh database, one left by an older build, and the
  migration tests themselves, which delete a version row on purpose to prove the startup guard catches
  it. `RebuildSchemaAsync` stays for the two tests whose subject *is* the migrating. The second half
  is the host: `ApiTestFactory` documented itself as the single factory every test boots while 78 call
  sites each built their own, at about half a second each. There is now one for the assembly, and what
  makes that safe is where the API keeps its state — every service that reads or writes the database is
  scoped, so it holds nothing across a request, and the only singletons are immutable configuration.
  Measured over the same suite: **Data 25.6 s → 6.8 s**, **Api 72.9 s → 16.0 s**, the whole suite
  **117 s → 42 s**, with the same 1744 tests passing. The saving grows on a slower disk, since what was
  removed is fsync-bound DDL and what replaced it barely touches the disk at all. (B73)
