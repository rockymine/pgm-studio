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
  **spawn** and **wool-spawn** (`SpawnStep`/`WoolSpawnStep`, `FEATURES.md`); the open slice is the rest:
  (a) wire the side-view into **`WoolMonumentsStep`** so a monument's Y is editable, not read-only
  (lift it off y=0 onto terrain); (b) **per-side focus** — `FocusSection` is still a `/concepts` mockup;
  the canvas **fit-island** exists but not per-team quadrant framing — refine the concept so the author
  can frame one team's quadrant while working its unit. (`FocusSection`)
- [ ] **N09 — Team id should track the team's colour.** The team id is seeded from the colour first picked
  (`Id = colour.Replace(' ','-')`), but `TeamAssignStep.SetColor` only updates the colour — so recolouring a
  team (e.g. red → purple) leaves `id="red"` and every id derived from it (`only-red`, `red-spawn-point`,
  the `…-red-monument` blocks, `reds-woolrooms`). Functionally fine (PGM resolves the id) but reads wrong.
  Re-derive the id on colour change and **cascade the rename** across the intent — `teams`, `islandTeams`,
  and `spawns[].team` / `wools[].owner` / `wools[].monuments[].team` — with a guard to skip the rename (just
  recolour) when the new colour-derived id would collide with another team's.
- [~] **N11 — Monument Y must seat on terrain; coord-input moves must re-snap.** The **point tool** now
  seats every spawn it places — team spawns + orbit copies, the observer, and wool spawns — on the target
  column's floor via the shared `ColumnFloor` helper. Still open: monuments aren't seated at all; and moving
  a spawn (team or wool) via the **coord inputs** rewrites X/Z without re-snapping Y to the new column, so
  only the point tool re-seats. Pairs with `N08` (monument Y editing) and `CV11` (the side-view clamp side
  of the same problem).
- [ ] **N12 — Configure only authors wool objectives; the intent and the plan tool already do all three.**
  `MapIntent` carries `Wools`, `Destroyables` and `Cores`, and `WoolGenerator`/`DestroyableGenerator`/
  `CoreGenerator` all emit their `map.xml` — the backend is complete. The **plan tool** places all three.
  Configure has only the wool path (`WoolObjectivesStep`, `WoolMonumentsStep`, `WoolRoomStep`,
  `WoolSpawnStep`, and the wool phase in `ConfigurePhases`), so a DTM or DTC map authored in the plan tool
  can be configured — the other objectives ride through untouched — but its objective cannot be *seen* or
  edited there, and a map that arrives without one can only be given a wool. Add the destroyable and core
  steps (both are simpler than wool: one region per defending team, no per-capturing-team monuments), and
  make the objective phase branch on which kind the map carries rather than assuming CTW.

## Sketch tool (S) — parked slices

The Sketch depth pass has shipped (`FEATURES.md` — select/drag, rotate, scale/squash, split, selection
highlight); these are the parked / dormant / deferred slices.

- [ ] **S9b — Angle/parallel snapping + droppable guide lines (parked).** S9 landed **position** alignment
  (edges/centres snap to other shapes + the symmetry centre, with guides). The remaining picture-editor bits:
  **angle/parallel** snapping (rotate a shape so its edges run parallel to another's — "hold two lanes
  parallel"), and **manually droppable** guide lines shapes snap to (vs the current auto-from-shapes). Both
  are their own work; park until needed.
- [ ] **S12 — Pin the Islands tree to the top of the sketch sidebar (UI polish, parked).** The residual weight
  above **Islands** is the **Layers** panel + the 12-tile **Library** palette. Collapse both behind `<details>`
  accordions (Library default-collapsed once the map has shapes), or move the Library to a toolbar popover (it's a
  "reach for a primitive" action, not persistent state). (`docs/sketch-tool-ux-review.md` P0#1;
  `docs/contracts/sketch-creation-flow.md` follow-on.)

## Editor & canvas infrastructure (C / CV)

Shared infra for **both** the Configure wizard (`/maps/{id}/configure`) and the frozen Edit editor
(`/maps/{id}/edit`). `C12`/`C14` are cross-cutting (serve both surfaces); `C9`/`C11`
are Edit-specific. Full canvas spec: `docs/contracts/canvas-interaction.md`.

- [ ] **C9 — Kits editing UI (Teams) + per-activity status dots.** Spawn `kit` is read/sent but has no
  edit UI; there is no status-dot system. *(Two sub-items — split if priorities diverge.)*
- [ ] **C11 — Wire + verify inspector edits across activities.** `OnDelete`/`OnRename` are wired only
  in Build Regions; the Regions/Teams/Objective inspectors are **unwired** (rename/delete silently
  no-op). Wire all three + verify rename/delete/coord-patch end-to-end.
- [~] **C12 — Build the shared component vocabulary (atoms → sections → shells).** The studio has a
  consistent CSS design system but **no Blazor layer that renders it** — the canonical skeleton
  (`panel-section` → `section-header` → `section-title`) is hand-typed across 44 of 64 razor files and
  the app shell is copy-pasted 11×. Full audit, atomic tree, API conventions (foldered under
  `Components/`, param-first + slot override; global CSS, no `.razor.css`), and the class→component map
  are the **contract in `docs/contracts/ui-conventions.md`** — follow it; `/design` is the
  zero-visual-diff regression oracle (components emit the same classes). **Phases A–C + D.1–D.2 shipped**
  (`FEATURES.md`): the atoms + `Section`, the shell (`StudioShell` + topbar/rail/footer), the workspace
  shells (`Workspace`/`Sidebar`/`Inspector`/`ContentColumn`), and — across every production surface (0
  raw markup outside the `/concepts` + `/design` leave-raw zone) — `Section` (D.1) plus the atomic
  vocabulary `Field`/`Button`/`Badge`/`ListRow`/`Chip` (D.2). Remaining:

  **D.3 — build + adopt the new components.** `CoordField`, `DetailHeader` **done** (`FEATURES.md`); the
  `/design` gallery **regenerated** to render the real components. `FlowBar` — once deferred as
  single-use — **shipped** (C21) once the Editor/Configure shell-convergence work needed it in a
  second consumer; it backs both `ConfigureLayout` and Edit's stepped activities (Setup, Build).
  `Console` stays single-use (the pre-flight log in `ReviewPreflightStep`) — not worth componentizing
  yet, left raw (same call as `CoordRow`, dropped because `ctrl-row` triples vary XYZ/XZ/R·H).
  `Card`/`CardGrid` **deferred** (only ~8 landing cards; low payoff).

  **Open — `Icon` adoption.** `Components/Primitives/Icon.razor` is **built but unadopted**: `<i
  data-lucide="@Name" @key="@Name">`, centralizing the lucide reconciler gotcha (recreate-on-glyph-change
  rather than patch a lucide-mutated `<svg>`). The ~156 raw `<i data-lucide>` across components and pages
  still stand — adopt incrementally (the icon-bearing components `Button`/`DetailHeader`/`Chip`, then the
  re-rendering page sites) when picked up. High churn, subtle benefit, so parked by choice, not blocked.

  **Open — polish**: fold the 1 `section-heading` use into `SectionHeader`; drop the inline `style=`
  occurrences now expressible as component params (`Align`/`MaxWidth`/`Fill`).
- [ ] **C14 — Dedupe activity code-behind.** The repeated `Post/Patch/Delete/Send` http trio
  (Build/Objective/Teams) + the `Index`/`CollectDescendants` region-tree walkers (3–4 activities) →
  a shared `MapApiClient` and/or `EditorActivityBase` / static `RegionNode` helpers.
- [~] **C28 — The client's remaining test layers (smoke has landed).** The **smoke layer + runner shipped**
  as `C31` (`tools/e2e.sh`, `tests/e2e/`) — every route is swept for "renders and raises nothing", seeded
  from a composed board; `icons.mjs` (C30) added the first *positive* render assertion on top of it.
  `PgmStudio.Client` is still **absent from the coverage report** (no test project
  references it), and two layers are still open:
  **(a) mount/interop** — per canvas tool, assert the bridge mounted and the surface has a real size; this
  is the C29 class of bug (a canvas at 45% of its workspace for weeks, in two tools) and it is assertable
  without knowing user intent.
  **(b) scenarios** — one flow per tool, specifically *the path that creates the artifact*, where a break is
  unrecoverable rather than cosmetic: Sketch `New → name → draw → Finish → Configure`; Plan
  `New → globals → piece → Compile`. The seed already proves that chain works headlessly.
  Deliberately **not** e2e: field-level inspector behaviour and anything asserting where geometry lands —
  those rot; extract the decidable logic instead (`CV12`). A bUnit project for the phase/step state machines
  is still worth considering, and is independent of the above.
- [ ] **CV12 — Two thirds of the JS layer is never loaded by a test.** `npm test --
  --experimental-test-coverage` reports 82.8% over the 15 modules the 148 tests import (several at 100%:
  `transform`, `symmetry`, `islands`, `polygon`, `plan-inspect`), but **26 of 41 files / ~6,900 lines are
  absent from the report** — they are never imported, which the coverage output shows as silence rather
  than zero. The untested set is the whole interactive layer: every canvas (`world-canvas` 1046,
  `plan-canvas` 1017, `sketch-canvas` 871, `sideview-canvas`, `canvas-base`), every bridge, every
  controller, `iso-webgl`, and `studio.js`. The split is coherent — pure geometry is tested, DOM/canvas
  code is not — so the win is not "test the canvases" wholesale but **extracting the decidable logic they
  contain** (hit-testing, snapping, viewport/transform maths, selection resolution) into pure modules the
  existing `node --test` setup can reach without a DOM. Pairs with the JS consolidation review.
- [ ] **CV21 — the world canvas has a `build` layer nothing paints into.** Stating the layer stack once
  (`CV19`) surfaced two layers with no content. One was removed there — a `block-highlight` rect created
  `visibility:hidden` whose only handle was assigned and never read. This is the other: the `build` group is
  created empty, no painter ever appends to it, and its toggle `setBuildVisible` has no caller outside the
  class — not the bridge, not any of the sixteen hosts. So it is an empty group with a visibility switch
  nobody throws. Removing it takes `setBuildVisible`, `#showBuild`, `#paintBuildRegion` and one line of the
  documented public surface with it, which is why it was left in place rather than swept during a
  behaviour-preserving refactor. Check first whether a Build phase was *meant* to fill it (the name suggests
  the Build-Regions work) — if so the task is to wire it, not delete it, and that is a different task in the
  feature section.

- [ ] **CV15 — The bridge invoke wrapper is inconsistent.** `plan-bridge` and `sketch-bridge` wrap
  `dotnetRef.invokeMethodAsync` in a local `fire()` that swallows the throw when the host hasn't wired a
  callback; `world-bridge` calls it unguarded, so an unwired callback surfaces as a console error instead
  of a no-op. Settle on one helper next to `fetch-json.js`. Tiny, but it is the only thing the five bridges
  genuinely share — the rest of their apparent repetition is per-tool document semantics and should stay
  separate.
## Backend, pipeline & internals (B / P / A)

- [ ] **B9 — Re-import a world into an existing map (keep the authored intent).** When an author tweaks the
  terrain (e.g. adds iron inside the spawns so the renewable populates) they currently have to import the
  updated world as a *new* map and hand-copy the intent across. Add a "re-import / update world" action on
  an intent-authored map that re-scans a chosen folder/zip in place — refreshing only the world-derived
  data (`islands_json`, `resource_block`, surface/layer parquets, monument candidates) and **preserving the
  `map_intent_json`**, then regenerating. Safe while island detection stays stable (the intent references
  islands by id, and spawns/wools are world coordinates); flag the author when the island set changes so a
  stale `islandTeams` mapping can be re-checked. (Manual procedure today: copy the `map_intent_json`
  artifact + re-scan, then `PUT /map/{slug}/intent`.)
- [ ] **B35 — Endpoint coverage: half the API is exercised by nothing.** `PgmStudio.Api` sits at **42.8%**
  lines (`tools/coverage.sh`), and the shortfall is not spread evenly — a long tail of endpoint files is
  effectively untouched while the tested ones are fine: `PreflightEndpoint` 2.6%, `ImportEndpoints` 3.6%,
  `IslandRolesEndpoint` 3.6%, `MonumentEndpoints` 5.3%, `LayersEndpoints` 5.5%, `ConfigureEndpoints` 6.2%,
  `AuthoringEndpoint` 8.2%, `IslandReviewEndpoints` 8.6%, `MapPlanEndpoints` 12.3%, `AnalysisEndpoints`
  13.0%, `RegionEndpoints` 15.6%. `ApiTestFactory` (B20) already gives schema-isolated MariaDB, so the
  marginal cost per endpoint is one happy path plus its error contract; these are cheap tests, not a
  redesign. Prioritise the ones that write: import, configure, region and map-plan.
- [ ] **B36 — The region/filter authoring-and-editing path is half covered.** A coherent cluster sits
  around 40–58% while its neighbours are high: `RegionAuthoringEncoder` 43.8% (370 uncovered lines),
  `RegionParser` 52.0% (295), `RegionEditor` 57.5% (180), `FilterParser` 48.9%, `RegionGeometry2d` 39.5%,
  `RegionBuilder` 43.7%, `FilterEditor` 41.9%, `WoolEditor` 58.5%. This matters more than the endpoint tail
  because it is map-contract logic, not glue — a silent regression here changes generated `map.xml` rather
  than returning a wrong status code, and `--authoring` is a manual harness, not a gate. Note the
  neighbours prove the standard is reachable: `MapParser` 92.9%, `XmlWriter` 88.1%, `RegionCategorizer`
  91.4%. Cover the type-specific region/filter branches first — that is where the uncovered lines are.
- [ ] **B37 — Objective separation via WX9 placeability: unify the resolutions, not the stampers.** The
  stamped-structure placeability attribute (`docs/world-export/structures.md` WX9, shipped for spawn iron)
  generalizes to the objectives: a **core or destroyable too close to a wool monument** merges structures
  that must read apart, and one **inside a spawn piece** is worse than ugly — the spawn's protection region
  makes an enemy goal unbreakable, so the map is silently unwinnable. The intended shape: the stampers stay
  heterogeneous (their inputs are irreducibly different — a wall owns a seam, a room a piece + marker +
  entries, an objective a marker + style), but every family's resolver produces one **resolved-stamp
  record** — kind, footprint box, `Placeable`, source marker — the uniform currency the pairwise separation
  rules run over (minimum distances to be decided from the corpus). `IronResolution` is the first instance;
  the preview's `StructureBox` is the drawing-side proof of the shape and would consume the record instead
  of assembling its own (placeability then reaches the iso view for free; mind B33 — the drawing frame
  stays a separate type). Stamp only placeable structures, flag the rest with the same
  marker-stays-visible discipline. Editor half: surface unplaceable markers on the plan canvas (the
  highlight ring the validation tab already uses for pieces), not only in the findings list.

- [ ] **B34 — The two map-list endpoints disagree on sort order, and the dashboard gets the noisy one.**
  `MapsListEndpoint` branches on the `stage` query param onto two differently-ordered repository methods:
  `MapRepository.ListAsync` sorts `OrderBy(Slug)`, `ListByStageAsync` sorts
  `OrderByDescending(UpdatedAt).ThenBy(Slug)`. The dashboard always requests `?stage=…`, so it always gets
  recency order — and on the imported Edit corpus `updated_at` records when the *pipeline* last wrote the
  row, not when the author last worked on the map, so it carries no authoring signal. The 349 Edit rows hold
  only 29 distinct timestamps (a re-processing pass stamped them in ~22-map batches a second apart), so the
  list renders as 29 alphabetical runs concatenated — it reads as scrambled, with the three maps outside the
  supported range (`3084`, `allure`, `lost_haven`, never re-processed) parked at the bottom. Recency earns
  its keep on Sketches/Plans/Configuring, where the timestamps are real edits and the lists are short.
  Preferred fix: slug order for the Edit stage, recency for the other three (one line); alternatives are
  slug everywhere, or leave it and let recency come good once maps are edited in the studio. Cosmetic — no
  data is wrong, and both orders are deterministic.
- [ ] **B33 — Three box types, two of them the same shape.** `PgmStudio.Minecraft` now holds two identical
  inclusive integer AABBs — `ScanBox` (`MonumentSuggester`: the region the author boxed, with
  `Contains`/`Expand`/`IntersectsChunk`) and `BlockBox` (`ObjectiveStamper`: a stamped structure's volume,
  with `Width`/`Height`/`Depth`/`CuboidMax`). Same six fields, same convention, different method sets —
  they're one value type wearing two role names. (`Api.Services.StructureBox` is **not** a third copy and
  should stay separate: it is a *drawing* frame with exclusive maxes plus `Kind`/`Color`, a different
  convention for a different job — the collision with it is what surfaced this.) Unify the two into one
  inclusive AABB with the union of the helpers. Deliberately **not** done inside `B24d`: it means editing
  `MonumentSuggester`'s 15 call sites, and that detector is corpus-validated at 96.6% precision — not
  something to churn as a drive-by during unrelated work. Low priority: unlike the symmetry duplication this
  is a value record, so there is no algorithm here that can silently drift.
- [ ] **B40 — The three dock styles are implicit; make them a type.** `Seat` picks between three seating
  rules using three *different discriminators* — `d.Wool is { } rich && Overhangs(rich.Family)` (shape
  family), `d.Kind == BoxKind.Frontline` (box kind), and falling through (everything else) — so there is
  nowhere in the code to ask which style a demand uses. The asymmetry runs deeper than the selector:
  two styles are named functions and the third is ~30 lines of inline loop body with no name; and the three
  are at different altitudes — `SeatOverhang`/`SeatFront` return a placed `(CellRect, BoxInterface)` while
  `SeatInRuns` returns a bare `int?` seat, leaving the caller to build the rect and the joint. That is why
  `boxes.Add`/`joints.Add` is written out three times with different arguments.
  **Scope:** (a) extract `SeatFullMouth` returning `(CellRect, BoxInterface)?` like its siblings, then (b)
  add an explicit `DockStyle { FullMouth, Overhang, ContactPatch }` **derived** from the demand — it is never
  sampled, it follows from the family roll — and dispatch on it. **Leave the failure policies alone**: they
  genuinely differ in kind (overhang demotes via `Compact`, the frontline kills the attempt, a wool may be
  dropped if another remains), and flattening them into flags loses more than it gains.
  The doc comment writes itself, because the three styles are indexed by *how much is known about the
  shape's entries*: full mouth knows nothing (require the whole mouth on one run and every entry lands —
  which is why the two-entry `U`/`H`/`Clamp` dock here); overhang knows there is exactly one and where it
  is; contact patch has no entry at all because a frontline is a face, not a corridor.
  **Oracle + the real constraint:** `ComposerFingerprint` + `ComposerVersionTests` must stay byte-identical.
  All three styles consume `rng`, so the invariant is not "does it compile" but **does the draw order
  survive** — hoisting one call above another moves the stream and every fingerprint goes red.

- [~] **B41 — Should a host's published capacity bound the grant it hands out?** The naming half has landed
  (`FEATURES.md`): `BoxJoint.Grant` is now distinct from a host's `EdgeOffer`, and the docstrings state the
  split — an offer's `WidthClass` is a *capacity* derived from the run's length, a grant's is a *selection*
  made per consumer kind. What remains is the behaviour question the rename deliberately did not answer.
  Today the two are entirely unlinked: `Seat` reads the hub's offers, keeps only `(Start, LengthCells)` as its
  **runs** and drops the published width, then `HubJoint` grants a width taken from the demand's kind
  (`WoolLaneCells` for a wool, `w` otherwise). So a hub can grant a corridor **wider than the run it sits on
  claims to support** and nothing objects. Either that is intended — capacity is advisory, the consumer knows
  its own lane — or the grant should be clamped to the offer, in which case a narrow run would demote a
  consumer's `cw` and some docks that succeed today would not.
  **Measure before deciding**: how often does the granted width actually exceed the published capacity of the
  run it lands on? If never, this is documentation; if often, it is a real gate the composer is missing.
  Changes what the filler builds, so it needs a before/after gallery and will move fingerprints.

- [ ] **B21 — MCP server: agent-drivable map authoring over the plan layer.** A thin MCP head (official
  C# SDK, `ModelContextProtocol` NuGet; new `PgmStudio.Mcp` project or a proxy over the running `:7894`
  API) so an AI agent can build a map end-to-end. The plan layer is the agent surface — `plan.json` is
  small, semantic, and the validator/evaluator return rule-id findings, giving the agent a compiler-style
  submit→lint→fix loop. **The gate this waited on is open: G119, G117 and G125 have all landed** (with
  G126's typed boxes), so the MCP head now wraps endpoints that exist rather than duplicating their
  plumbing. Of the three genuinely-new pieces, **`emit_family` is half-built**: G144's
  `GET /api/shapes/probe` already emits a canonical family through `BoxFiller` — profile check and
  docking gate included — and answers with the shape or a directed `FillRejection`, and
  `/api/shapes/probe/schema` serves the per-family knob surface and minimum box in the dock frame, which
  is the payload a tool-description needs to constrain its parameter enum. What is left of it is the
  *stamp*: placing the emission into an existing plan's typed box instead of returning a standalone
  `symmetry:none` plan. So the remaining new work is the PNG path for `plan_render`, that stamp, and the
  resource curation. Tools: `compose` (the G117 endpoint —
  request in; plan + canonical descriptor + derived facts + score out; starting material to mutate) ·
  `plan_validate` (errors + rule lint + full evaluator readout — the response must flag empty
  `placements`, which leave the feel terms vacuously green) · **`plan_feasibility`** (the G125
  read-back: mask → derived params → emit-compare, directed verdicts citing rule/task ids — the oracle
  that makes the loop converge; the validator alone passes plans the composer cannot produce, proven by
  the funnel exemplars scoring 0) · **`emit_family`** (stamp a canonical shape through the real emitters
  into a typed G126 box — agents never hand-cut rectangles) · `plan_render` (image content — agents
  self-correct far better seeing the board) · `plan_save`/`plan_get`/`plan_list` (the G119 store, with
  an agent-authored origin marking so agent output never contaminates the human-labeled corpus) ·
  `create_draft`/`export` (existing chain; return the export **link**, never the world zip inline). MCP
  resources: `generator/rules.md` + `generator/model.md` as the design brief, **`GET /api/shapes/catalog`
  as the machine-readable half of that brief** (G144 — the same vocabulary computed from the emitters, so
  unlike the prose it cannot drift, and each entry carries the tier that says whether the composer really
  produces it), `tools/seeds/*.plan.json` (incl. the funnel exemplars) as few-shot examples, and the G118
  verdict JSONL once it exists. **G146 blocks this, as correctness rather than tidiness**: the design
  brief describes Z as a production family and no sampler draws one, so an agent that reads the prose and
  asks for a Z authors a plan `plan_feasibility` then rejects — a loop that cannot converge, caused by a
  doc/code disagreement the catalog's tiering made visible. Either land G146 first or restrict
  `emit_family` to the catalog's in-mix tier. The same reasoning applies to the verdict JSONL: few-shot
  examples inherit whatever the sampler over-produces (measured at G144: the donut is 73 of 89 wool
  cards, U/H/L one apiece), so an unstratified corpus teaches an agent to write donuts. Scope
  is the **author agent** only; the **analyst agent** (mine verdicts/reject logs for rule + envelope
  refinements — read-only `verdicts_export`/`rejects_query`) is a small follow-on once the corpus has
  data, not before.

**DTM / DTC objectives (destroyables + cores).** The contract is `docs/contracts/destroyables-and-cores.md`
— it owns the XML surface, the **world-measured** structure families, the schema, and the two-team scope;
its rule ids (`OB*`/`DT*`/`DC*`) are cited below. Filed here (not `N`/`G`) because the bulk of each is
pipeline — parser, writer, schema, intent, stamper — with the plan-editor placement as the last mile.
**Both objectives now author end to end** (`FEATURES.md`): parse/write/codec, the schema, the world stamps,
and plan → intent → world → `map.xml` for destroyables (`B24`) and cores (`B25`). What is left below is the
import diagnostic (`B24e`), detection (`B26`), and the work the phantom classifier unblocked (`B31`, `B28`).

- [ ] **B24e — Flag an *imported* map whose objective region holds none of its material (a warning, not a
  gate).** Scoped down: the authored half of this is **already covered by tests** — `DestroyableWorldTests`
  and `CoreWorldTests` walk each emitted region with PGM's `[min, max)` and count the blocks, which is
  exactly the assertion this task was filed to add. For a generated map the region *is* the stamper's box
  (OB8), so a runtime gate would re-check something true by construction. **What has no cover is the import
  side**: the corpus sweep found **10 destroyables whose region contains none of its declared material**.
  Those are the author's own maps, already broken before we touched them — so this is a **diagnostic on
  import**, not a block on re-export. Blocking someone's export over a pre-existing dud is the studio
  overreaching; telling them is the value.
  Never "the region is full": by OB12 a region is legitimately mostly air (a 3×3×3 region holding a 1×3×1
  pillar is correct and common), so anything stricter flags most of the corpus.
  **Note the category difference** before extending `MapValidity`: its one existing rule (a wool needs a
  monument) is *"PGM refuses to load this map"* — an `InvalidXMLException`, so the map is unloadable. This
  one is *"PGM loads it fine and the goal has zero health"*, which PGM itself only logs a warning for. Two
  different severities of truth; do not blur them into one list without saying which is which. World access
  is **not** the blocker it was originally filed as — 14 test files already read blocks out of a built
  world. (OB3, OB11, OB12)
- [ ] **B31 — Island detection still guesses at the build floor a parsed phantom now states exactly.**
  `LayerExtractors.CleanBaseExclude` excludes stained glass (95) as a "build-floor marker removed pre-game
  via a `destroyables` mode-change" — a **material guess** ("glass as the lowest solid must be a build
  floor") that stands in for the phantom pattern because the parser could not see the mode. The phantom
  classifier removed that excuse: a `BlockSwap` phantom's region + its mode state **precisely which blocks
  vanish before play**,
  per map, with no material heuristic. Replace the guess with the fact — feed the phantom regions of a map
  into the scan so the cleaned base subtracts exactly what the mode erases, and drop 95 from the blanket
  exclusion once it does (the guess also silently eats decorative glass floors that are *not* build markers,
  which is the failure mode in the other direction). The plumbing is the real work, not the rule:
  `LayerExtractors` (`PgmStudio.Minecraft`) runs with no map context today, so the phantom regions have to
  reach it. Pairs with `G9`/`G12`. (OB16, `destroyables-and-cores.md` §2)
- [ ] **B29 — `<include>` is silently ignored, and 93% of the corpus uses it.** `MapParser` preprocesses
  `<if>`/`<unless>` (`ResolveVariants`) and `${constants}` (`ResolveConstants`) but has **no `<include>`
  handling at all** — the element is skipped and its content never enters the document, so every rule the
  fragment defines is invisible to us. **334 of our 358 `ctw/` maps (93%) use it**; `gapple-kill-reward` alone
  appears 815 times across both corpora, and PGM splices a **`global` include into every map** at the root
  (`MapIncludeProcessorImpl.getGlobalInclude` → `MapFilePreprocessor.preprocessChildren`). PGM resolves an
  include by id from **`config.getIncludesDirectory()` — a server directory, not the map folder** — so the
  bodies **are not in the corpus** and cannot be recovered from it: this task is blocked on obtaining the
  include library (the source PGM server config), which is a fetch, not a code change. Until then the honest
  move is to **flag** a map whose `<include>` we cannot resolve rather than parse it as complete — the same
  reasoning as `B22`. Once resolvable, splice at preprocess time (matching `MapFilePreprocessor`) so every
  downstream parser sees one flat document. **Reading ≠ emitting** — we already emit includes
  (`XmlWriter.cs:112`, `CtwStandards.cs:104`), so this is about *analysing imported maps* only and **gates
  nothing in `B28`**. First diagnostic: dump the distinct include ids per map, so the size of the unknown is at
  least visible.
- [ ] **B28 — Water lanes (CTW): detect all three forms, author the newest.** A **route that opens mid-match** —
  a gap between islands becomes bridgeable, adding a late-game way to reach the wool. A CTW feature, filed
  here only because its legacy form *is* a destroyable — no longer blocked, since destroyables and their
  phantom classification now parse. **The mechanic is `VoidFilter`, and it reads
  y=0 live:** a column is void iff `(x,0,z)` is air and wasn't a block-36 marker, and `getBlockAt` is evaluated
  **at query time, not load** — so filling y=0 with water at 15m makes the whole column non-void and
  `deny(void)` stops applying. Players then bridge a route that did not exist. (Same y=0 rule explains the
  block-36 marker and the stained-glass build floor — see `B31`.) **Three generations, detect all:** *Gen 1* =
  a fake destroyable with `materials="air"` at y=0 swapped to water by a mode (vesuvius 20m, newgen_classic 15m,
  dominion 10m, piorun 5m — ownership vestigial, split per-team only because `owner` is required); *Gen 2* =
  `<action><fill region="…" material="water" filter="only-air"/></action>` on a time `<trigger>`, no destroyable
  and no mode (`lupa`, `tulip_mania_ii` — which names its region `water-lane-fill-regions` —
  `icecream_sandwiched_ii`, `malupa`); *Gen 3* = **`<include id="water-lanes"/>` + a `<union id="water-lanes">`
  of y=0 cuboids and nothing else** — the behaviour factored into a shared fragment, keyed by the matching
  region id (`bridgid_ii`, `ad_astra`, `rushers_vs_defenders`, `araxa`, `turf_wars`, `royal_garden_ctw`; **5 of
  the 6 contain no fill, destroyable or mode at all, and none applies anything to its lane regions** — the
  include supplies 100% of the behaviour, keyed by the matching region id). **Author Gen 3, and it is nearly
  free.** We do not need the include's body to emit it — the server resolves it at load. `MapXml.Includes` +
  `XmlWriter.cs:112` already exist and `CtwStandards.cs:104` already ships `gapple-kill-reward` on every
  generated map via `m.Includes.Insert(0, …)`; a water lane is the same move — emit `<union id="water-lanes">`
  and add `"water-lanes"` to `Includes`. **One string and a region**: no `<actions>`/`<fill>`/`<trigger>` parser
  (that is Gen-2-only, and we have none of it today), no fake destroyable. Detection is likewise two facts —
  `<include id="water-lanes"/>` + the matching region — so **`B29` gates neither authoring nor detection here**.
  The authored primitive is **a set of y=0 rects** (a union of cuboids spanning y=0..1), not a path — straights
  and corners are both just rects; "bridgeable" is the authors' own word. Note the water bucket is **unrelated**
  (a universal movement tool for cancelling fall damage: 163 of 358 `ctw/` maps carry one, 157 of them with no
  lanes). Gen 1 detection is already unblocked — a fake lane is a `BlockSwap` phantom, which
  `Destroyable.Phantom` now classifies. (`destroyables-and-cores.md` §14)
- [ ] **B26 — Detect destroyables + cores from a world scan (later).** The `MonumentSuggester` move applied to
  the **easier** problem: scan the world, propose the objectives, let the author confirm which is a destroyable
  and which is a core. Wool monuments are a design free-for-all (96.6% precision / 57.8% recall, recall capped
  by unlabelled maps); these are far more standardised. **A core is obsidian enclosing lava** — a signature
  effectively nothing else in a map produces, so bounds and material fall out geometrically, not heuristically.
  **A destroyable is a material outlier** — a small isolated cluster in a closed four-material vocabulary
  (obsidian / emerald / gold / ender stone), 56% of them a 1–3 block obsidian pillar. The families predict their
  own parameters, so a detector can propose `leak` / `completion` / style, not just a box. Reuses the existing
  scan plumbing, the candidate-store shape (`monument_candidate`, `monument-candidate-store.md`) and the
  confirm-in-UI flow — only the classifier changes. **Trap (OB12):** propose the **structure's** bounding box
  and emit a region around it; the region itself is a human's loose box, is not in the world, and cannot be
  detected. **Never propose a phantom as an objective** — a marker is not a monument; `Destroyable.Phantom`
  already names the distinction, so respect it rather than re-deriving it. The parse/schema half it writes
  into has landed, and so have `B24`/`B25`'s authoring slices — a confirmed suggestion now has somewhere to
  go, so this is unblocked.
  **Test it against authored plans, not (only) the corpus — the ground truth is free.** Author a plan with a
  destroyable/core at a known anchor, compile it, build the world, run the detector, and assert it proposes
  that objective *at the anchor the plan named*, with the style/size the plan asked for. The whole loop is
  already in place (`DestroyableWorldTests`/`CoreWorldTests` build the world; the plan is the label), so this
  is a fixture generator, not a harness.
  **Why this matters more than it looks:** `MonumentSuggester`'s corpus recall is capped at **57.8% largely
  because ~⅓ of maps are unlabelled** — there is no ground truth to score against without hand-labelling. A
  generated world has ground truth *by construction*: we know exactly where we put the core, so precision and
  recall are both computable for free, over as many synthetic cases as we care to emit (every style, every
  casing size, on a slope, at a terrain edge). Corpus sweeps stay the reality check — synthetic worlds only
  contain the structures we know how to build, so they can confirm the detector finds ours and can never tell
  us what real authors do that we don't model. Use both, and expect the corpus to be the one that surprises.
  This also **subsumes what `B24e` was going to gate for authored maps**: a detector that finds the core where
  the plan put it has proved the blocks are there.

## Layout generation (G)

**The design long tail moved out of the board.** With the old grower path retired and the box pipeline
now the one composer (`FEATURES.md`), the ~40-task G backlog — much of it describing machinery that no
longer exists — is condensed into **`docs/generator/ideas.md`**: one idea per few lines, grouped
by theme, **ids preserved** (never reuse one). Pull an idea back onto the board by id when it becomes the
focus; the full original task text is in this file's git history. The current focus (the generator in the
studio, G117/G118) is in `TODO.md`.

- [ ] **CV16 — the authoring canvases have no frame budget, only habits.** The zoom stall (fixed in
  `FEATURES.md`) was two unrelated per-event costs that happened to land on the same handler, and neither was
  visible until measured: a grid rebuild whose memo was written for pan, and a `.NET` interop call per wheel
  tick. Both are the same class of mistake — doing work per *input event* rather than per *frame* — and
  nothing in the canvases prevents the next one. Two guards worth having: a debug overlay (or an e2e probe)
  that reports main-thread ms per interaction burst, so a regression shows up as a number rather than as
  someone noticing the picture go soft; and a rule that anything crossing into Blazor from a canvas handler
  goes through the frame coalescer, since interop is the expensive edge and its cost is invisible from the JS
  side. The screenshot approach does **not** work for this class of bug — `page.screenshot()` forces a fresh
  raster, so a transient compositor artifact never appears in the capture; measure the handler, not the pixels.

- [~] **G157 — terrain painting: the extensions.** The base model (TP1–TP6: rim/wall/surface/fill,
  corner-inclusive + `closed` rim, stamp consultation) is **built and shipped** (`FEATURES.md`,
  `TerrainPainter`, painting every sketch export map-wide); the four-stage architecture
  (`docs/world-export/terrain-painting.md` §5) is in place, so each remaining piece lands on one seam. Open
  slice (TP7–TP13): a configurable **rim depth** and **bedrock floor thickness** (absolute or
  terrain-relative) — both already carried by `TerrainTheme` + the band resolver and unit-tested, so what's
  left is exposing/authoring them, not building them; a **toggle for wall on terrain-to-terrain faces** (also
  already in the resolver); a **layered surface stack** (grass over dirt — `LayeredMaterial` exists, make it
  the default `SurfaceDepth`); **bucket toggles with fill as the required fallback** (resolver done, surface
  it); **scoped theming** (full-map today via the map-wide `Theme`; per-piece/collection later, resolved at
  interfaces — the theme-resolution lookup stage); and, last, **patterns** per bucket (voronoi/fractal area
  fills for surface/fill, layered or map-wrapping vertical runs for walls) as new `TerrainMaterial`
  implementations. Team-coloured walls (island→team) is the first real material knob beyond the neutral
  defaults.

- [ ] **G156 — cell-size-aware generator room sizing (WX2's generator half).** The stamped-room minimum is
  8×8 **blocks** (`docs/world-export/structures.md` WX2) but the emitters size rooms in **cells**
  (`ShapeEmitter.RoomDepthCells` = 2, corridor widths in cells), so a small-cell board (cell ≤ 3) can emit
  a wool room or spawn its own export refuses. Make the room depth/width floors cell-size-aware — enough
  cells to reach the block minimum — through `MinBox` and the spawn profile; the composer's cell-5 boards
  already clear it by construction, so this binds only when boards go small-cell.

- [ ] **G150 — stamp a catalog shape into a drawn box.** The plan editor can draw a typed box and then ask
  whether the composer could have produced what is in it (G125's feasibility panel), but there is no way to
  go the other direction and *place* something known-producible: nothing in `Features/Plan/` references the
  catalog or the emitters. So an author hand-cuts rectangles and finds out afterwards. Give a selected box a
  **family picker** — the in-mix tier of `GET /api/shapes/catalog` (G144), which is exactly the set the
  composer really samples — plus the knobs `GET /api/shapes/probe` already serves per family, and stamp the
  emission into the box as its members. Producible **by construction**, so the feasibility panel goes green
  without the author aiming at it.
  Most of this exists. The probe endpoint already emits through `BoxFiller` (profile check and docking gate
  included) and answers with the shape or a directed `FillRejection`; `/api/shapes/probe/schema` already
  serves the per-family knob surface and minimum box in the dock frame. What is new is the *stamp*: writing
  the emission into an existing plan's box rather than returning a standalone `symmetry:none` plan, which
  means placing pieces at the box's origin, giving them ids under the box, and replacing whatever was there.
  This is the editor half of **B21's `emit_family`** — build it here and the MCP tool wraps it rather than
  reimplementing it. It also pairs with G149: placing known-producible shapes and watching the G148 land
  readout move is the most direct way to find out what the budget is actually worth.

- [ ] **G151 — a box's rect should be the bounding box of its members.** The members inspector offers
  "Fix these members" / "Follow containment", and the fixed half behaves oddly on purpose-built-for-something-
  else grounds: named membership (`PlanBoxes.MembersOf`, the `Members` list) ignores geometry entirely, so a
  piece can be dragged *out* of its box and still be carried when the box moves (`plan-canvas.js`, the box
  drag translates `d.carried` in both modes). That is not an authoring mode — named membership exists for
  **provenance**, so a pinned board can record the grouping that actually produced it off `BoxPartition.KeyOf`
  rather than having it re-derived approximately. Exposing it as a toggle asks the author to edit with a
  mechanism built to preserve history.
  The fix is not a third mode, it is separating two questions that are currently one button. **Which pieces
  are members** is legitimately two modes (named vs containment) and both should stay. **What the rect is**
  should not be a mode at all: it should always be the bounding box of the members. That is not a new rule —
  `BoxPartition.Of` already computes a box's rect as `Bbox(members)`, and `Box`'s own contract says a box's
  contents "must touch its edges", which is the same statement. So dragging a member extends the box,
  dragging the box moves its members, and a member outside its own box becomes unrepresentable rather than
  merely strange. One case to decide: an empty box has no bounding box — keep its drawn rect until it has a
  member, which also leaves the draw-then-fill flow working as it does now.

- [ ] **G152 — the plan editor shows browser state instead of the plan it names.** Opening a map-backed plan
  can display a drawing that belongs to no plan at all: the same three maps show one picture in one browser and
  a different picture in another, and every plan "created" appears to contain a drawing made once, long ago.
  Measured: of the four maps named `untitled-plan*`, three hold a `plan_json` artifact of **two bytes** — `{}` —
  and only the fourth holds a real plan. `LoadFromMap` treats `{}` as "no stored plan yet" and deliberately
  keeps the editor's current document rather than importing garbage, which is correct reasoning about the wrong
  premise: the current document is not blank, because `plan-bridge` restores the last autosaved **localStorage**
  document at mount, before any route-specific load runs. So a map with no stored plan renders whatever that
  browser last had cached, which is why the picture is per-browser and identical across maps. The plan table
  itself is sound — driving the real UI (New → draw → Save, twice) writes two rows with correctly different
  geometry.
  Two fixes, and the first is a deletion. **Remove the document cache entirely** (`STORAGE_KEY`): the database
  is the store now, and a client-side copy of a document that the DB also holds can only ever disagree with it.
  The other three keys stay — overlays, height-map and surface-step are UI preferences, not documents, and they
  are not claiming to be the plan. Then **make Create-draft write the plan it compiled into the map's artifact**,
  so a map made from a plan opens with that plan instead of `{}` — the empty three exist because it doesn't.
  Accepted consequence: unsaved work is lost on reload until a DB autosave exists, which is its own task if it
  turns out to be wanted.

- [ ] **G153 — the feasibility read is per box and is reached through a list.** `G125` computes producibility
  **per box** and renders it in a left-panel list, so after clicking a box on the canvas the author has to find
  that same box again, by name, in a sidebar — for a read that is already about the thing under the cursor. The
  inspector on the right already opens on that box with its id, kind and members; the verdict belongs there,
  beside them: the parameter tuple that reproduces it or the nearest candidate, its directed findings, and the
  rule or task id each finding cites, with the click that paints the missing/extra cells kept as it is. The
  unit-level arrangement findings are genuinely **not** per box (parallel fronts, the frontline's pinned face,
  seat separation) and stay in the left panel, which leaves that panel one coherent job instead of a mixed list.

- [ ] **G154 — one plan editor, two bindings, two different tools.** `PlanTool` serves `/plan-editor` and
  `/maps/{slug}/plan` from a single component through five `@if (MapBacked)` branches, and the two render as
  different products. Map-backed gets the phase rail (Info · Draw), the flow bar, and the three panels as chips;
  the bare route gets no flow bar, no phases, the same three panels as **rail buttons**, and a collapsible
  sidebar the map-backed one cannot have (`SidebarOpen => MapBacked || leftOpen`). Same panels, two navigation
  models, one file — the thing `docs/contracts/tool-consistency.md` exists to prevent.
  Unify on the phase-rail + flow-bar + chips structure and keep the collapsible sidebar for both. The route may
  change **only** the topbar — its crumbs and which actions exist — because that is where the binding genuinely
  differs: a map-backed plan saves into its map's artifact, while a plan row saves as a row and forks when it
  was generated or imported. Rename the bare route to `/plans/{id}` (and `/plans/new`), which says what it is
  bound to where `/plan-editor` says nothing, updating the generator hand-off, the smoke sweep's route list and
  `plan-editor.md` with it.
  **Do not delete the route.** It is the only surface that opens a **plan row**, which is what the generator
  hands a candidate off as and what `G119`'s fork-on-edit rule operates on; routing candidates through
  `/maps/{slug}/plan` would mint a map per candidate looked at, and New, Import, Open and the origin badge have
  no home on a map-backed plan.

- [ ] **G149 — the land budget is a number the composer reads and then overshoots.** The first thing the
  G148 readout showed, measured over 40 boards at 12 players/team (budget 50 cells): land runs **63% to 222%**
  of budget, median **115%**, and **28 of 40 boards are over**. So the budget is not a cap — it is an input
  the allocator consults for its decisions (lane width, whether there is a frontline, the hub caps, the wool
  count — `TeamUnitAllocator.cs:46-52`, `UnitTuning.WoolCount`) and then nothing reconciles the result
  against it. `BoxFiller.WithinLandTarget` exists and no production path calls it.
  Nothing caught this because nothing measures it: the only land-ish term is `fill-ratio` (G8), which is land
  over the board's **bounding box**, not land against the **budget** — a different quantity that can sit
  happily in its band while the unit is at double its target.
  Decide what the budget means before changing anything, because both readings are defensible: either it is a
  target the fill should be reconciled against (then something must spend the overshoot down — the
  two-currency accounting says fragment converts surplus land to build, so the question is whether fragment
  ever runs), or it is only a sizing heuristic (then rename it, drop the "budget" framing, and stop implying
  a contract that does not exist). What is not defensible is the current state, where a number named budget is
  exceeded by half again on a typical board and nothing says so. Note G138 is adjacent but distinct: that one
  is about the composer taking the first acceptable plan rather than ranking; this is about the plan it takes
  not honouring its own sizing input.

- [ ] **G147 — verdict coverage on the catalog: which buckets has nobody judged?** *(sequenced after G118 —
  there is nothing to count until verdicts exist.)* The browse feed hands the author whatever the composer
  samples, so collection is passive: the corpus ends up shaped like the sampler, and the parts of the space
  the sampler rarely visits stay permanently unjudged. The measured skew makes that concrete — the donut is
  73 of the 89 wool cards while U, H and L are one apiece (G144) — so scrolling will produce a donut corpus
  and silence everywhere else, which is exactly the wrong input for rule refinement.
  The fix is to show the denominator. A `StructureNames.Canonical()` key is a triple
  (`wools:… | hub:… | front:…`) and the catalog already renders each component of that triple as a card, over
  a space now small enough to enumerate: 81 wool classes up to rotation/reflection, 7 hub forms, 3 frontline
  forms. So add a third filter facet beside *kind* and *reach* — **coverage** — with three states: *judged*
  (this bucket has verdicts), *thin* (one or two, not enough to trust), *unjudged* (nobody has ever looked at
  a board shaped like this). "Find me something nobody has judged" then becomes a chip click, and collection
  turns from an infinite scroll into covering a space with visible edges.
  Needs one query — verdict counts grouped by the structure key — which is a `GROUP BY` on the column G118
  already stores, so **design it with G118's schema rather than bolting it on after**. The UI is small: a chip
  row and a count badge over the same tally plumbing the catalog's `ByTier`/`ByFamily`/`ByKind` already use.
  **A card is a component of a bucket, not a bucket** — a board reading `wools:donut,l` touches both the donut
  and the L card — so per-card coverage is an aggregate over every bucket that card participates in. Build the
  aggregate first (cheap, and enough to spot a blind spot); add a per-bucket drill-down only if the aggregate
  proves too coarse to act on.

- [ ] **G145 — five emitter knobs are unreachable from the composer.** `ShapeEmitter.Emit` takes
  `attachments`, `woolExtend`, `entryShift`, `woolShift` and `attachmentOffset`, and `WoolBoxEmitter.Emit`
  passes all five through — but `WoolBoxEmitter.Fill`, the only path `BoxFiller` and therefore the whole
  compose pipeline uses, forwards none of them (`WoolBoxEmitter.cs`, the `ShapeEmitter.Emit` call inside
  `Fill`). Their only callers in the tree are `tools/compose/box-gallery.cs` (the two-attachment and
  moved-attachment donut cards). So the two-attachment donut, the extended-wool donut and both scythe
  endpoint shifts are built, tested, drawn in the galleries, and **cannot appear on a generated board**.
  Decide per knob rather than in bulk: the donut's second attachment is a genuine multi-access shape the
  hub could dock twice and is the strongest candidate to plumb; the scythe shifts are moot until the
  scythe itself is admitted (G146). Plumbing one means widening `WoolFill` (it already carries
  `AttachmentWidth` and `RingWalls`, so the shape of the change is settled) and giving `UnitRequests` a
  draw for it. **Do not "fix" this by deleting the knobs** — `EmitterPlacementKnobTests` gates them and
  `model.md` §4 describes them; the gap is the plumbing, not the geometry.

- [ ] **G146 — two families are in the vocabulary but never on a board.** The emitter builds eight
  terminal-capped families; the composer puts six on boards. **Z** is listed in
  `FillMenu.ProductionFamilies` and `BoxFiller` fills it happily, but `UnitRequests.WoolRequest` never
  draws it — the rich branch picks donut, then U/H/clamp, else L, and the fallbacks are I. The only caller
  that could reach it is the roll-indexed `BoxFiller.Fill(box, mouth, cw, roll, …)` overload, which has no
  caller in `src/` at all. So the menu advertises a family the sampler cannot produce, and the browse
  tool's own filter chip for it can never match. **Scythe** is the honest case: excluded from
  `ProductionFamilies` with a stated reason (its bay's mouth is its docking edge, so a flush dock seals it
  into WL8's forbidden enclosed void), with the elevation-stage alternative already parked as G81.
  Two separate decisions: either give the sampler a Z draw or drop Z from `ProductionFamilies` so the menu
  stops advertising it (the second is a one-line honesty fix and should not wait on the first); and leave
  the scythe out until G81 lands. Either way the catalog page (G144) will render both under a
  *reachable* / *emitter-only* badge, so the gap becomes visible rather than folklore.

- [ ] **G143 — the board deriver calls segments "edges", which is the one word the model reserves.**
  `model.md` fixes the vocabulary: an **edge** is one full side end to end, a **run** is a contiguous
  stretch along a boundary, an **interval** is where two things touch. `BoardStructure` breaks it —
  `FrontEdges`, `IntraEdges`, `SelfEdges` and `RedstoneEdges` are all `List<(X1,Z1,X2,Z2)>` of
  **cell-boundary segments**, not edges, and the deriver's own comments already call the grouped result
  a *run* (`GroupFrontlineRuns` → `FrontlineRuns`). So the code contradicts itself in one file: the raw
  list is named for a full extent, the grouped one for what it actually is. Rename the four to
  `FrontSegments` / `IntraSegments` / `SelfSegments` / `RedstoneSegments` (or `…BoundarySegments`), and
  sweep the comments that call a segment an edge. Mechanical — the type is a tuple list with a handful
  of consumers (`BoardDeriver`, the deriver gallery, the evaluator terms reading front edges) — but it
  has to land in one commit with the doc, or `model.md` will assert a rule the code visibly breaks.
  Check `BoxEdgeInterface`/`EdgeSpan`/`EdgeInterval` in the same pass: those name a genuine full edge
  and its sub-intervals, so they are correct and should stay, which is exactly why the deriver's misuse
  is worth removing rather than tolerating.
- [ ] **G138 — The composer accepts, it never chooses: a soft score has nowhere to act.** `Composer` takes
  the **first** plan that clears the gate and `break`s (`Composer.cs:59-84`) — no ranking, no comparison, no
  best-of-K. It contains zero references to `Evaluate` or `Score`, only `Gate`, and `Gate` runs hard terms
  only (`LayoutEvaluator.cs:86`). Worse, the compose path builds its context as `EvalContext.Build(plan)`
  with no envelopes, which defaults to `SeedEnvelopes.**Empty**` (`EvalContext.cs:34-38`) — so every soft
  term looks its band up, gets null, and stays dormant by design. **The authored envelopes have no causal
  influence on generated output whatsoever**; they only score plans after the fact, via the
  `Evaluate(PlanModel, …)` overload the API endpoints and galleries call.
  So any soft rule derived from `G118`'s verdicts is **inert until this lands**: generate K candidates,
  score all K, return the best. The loop already generates and discards candidates, so the change is small —
  but it converts the composer from *first-acceptable* to *best-of-K*, which is a real behaviour change and
  will move every fingerprint.
  **Sequence it after the bands are calibrated, not before.** Measured over 560 composed plans, a ranking
  today would be almost entirely a `spawn-wool-ratio` contest (outside its band on 44% of applicable plans
  at median distance 1.64) while four terms score nothing at all — see the `LEARNING.md` debt entry.
  Ranking before recalibration just amplifies one badly-fitted band. Order: `G118` collect → calibrate /
  gate the vacuous terms → this → soft rules become causal.

What stays here is the concrete non-design work on *imported* maps (island detection + playability):

- [ ] **G9 — Re-scan the corpus with stair-aware detection (remaining slice).** The over-split
  **detection fix landed** (`FEATURES.md`: `CleanColumns` + `DetectStairAware`), as did the review
  flag + role classifier. What remains: (a) **re-scan the corpus** so the stored `islands.json` /
  `island_sketch_json` reflect stair-aware (the live DB + `pgm-studio-output` were generated with the legacy
  detection — needs the source worlds, `OvercastCommunity/CommunityMaps`+`PublicMaps` `ctw/`), and decide
  whether to refresh the `--islands` Python-parity oracle to match; (b) the residual `a_new_day` **isolated
  raised-decor specks** (≈37-block grid bits with no walkable connection — correctly `small` via
  `IslandClassifier`, but a per-island prune could drop them); (c) any **under-split / merged** read beyond
  `abstract` (whose stained-glass build-floor is now excluded — `FEATURES.md`): `LooksUnderSplit` is the
  catch-all flag; the residual lever if one is found is to fall through to surface-based detection when a
  cleaned-base component is a map-spanning low-Y slab. Serves the shipped island-health / analysis
  features; the decompose-queue UI slice was dropped with the corpus-mining flywheel.
- [ ] **G12 — Re-prune flying blobs above terrain (stair-aware regression).** Stair-aware connectivity fixed
  the over-split (disconnected islands) but **re-introduced** the stark-y-jump / flying-island problem:
  decorative masses floating above the map (dragons/birds) now merge back into the islands when a near-vertical
  surface chain bridges them (e.g. **Duality**, **mame_i_shrunk_the_pvpers**). Re-add a guard: stop joining
  across a **really big y-increase**, and/or identify & **prune blobs whose base sits well above the terrain
  band** (the old float-prune did this on `DetectHeightAware`; the stair surfaces now leak past it).
  **`max_build_height`** is a natural cut/prune ceiling — anything whose mass is above it is non-playable
  decor. Re-validate the over-split fixes (a_new_day/thunder) still hold after re-adding the ceiling.
- [ ] **G65 — FannedGraph ↔ ContactGraph adjacency reconcile (deferred from G59).** `FannedGraph.LandAdjacent`
  (reachability) still diverges from the rect-layer authority `ContactGraph` on one count: any area overlap
  connects regardless of surface delta, while `Components` unions an overlap only at `SurfaceDelta == 0`.
  (The corridor-width half was reconciled — `LandAdjacent` now accepts Narrow seams, matching `Components`.)
  Pick one rule for the overlap case and add a test; needs per-node surface carried into the fanned graph and
  validation against the traversability harness (`tools/PgmStudio.RoundTrip --traversability`).
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
  `src/` + `tests/` (e.g. `ImportEndpoints`, `WorldScanStep`, `WorldFeatureWriter`) — sweep them.

**Deprioritized — may be dropped in a later pass.** Optional/deferred slices parked out of the active
long-tail so they stop competing with real work. Re-evaluate (or delete) when their area is next touched.

- [ ] **S10 — Auto-promote rectangles on Bézier (parked, optional).** Today S4 promotes via the inspector
  button / `P`; a rectangle keeps its 8-handle resize and has no Bézier affordance. If we ever want a
  rectangle's corner to sprout a Bézier handle that *implicitly* converts it to a polygon, it needs rect
  vertex/tangent handles in `sketch-edit-controller.js` (a UX decision on resize-handles vs vertex-handles).
  Low priority — explicit promotion already covers the need.
- [ ] **S16 — Resize library primitives after placement (mostly resolved; deferred).** `S21`'s island scale
  handles now resize a **placed** polyomino / n-gon — a single non-rectangle member gets the 8 bbox scale handles —
  so the after-placement resize is **covered**. The only remaining slice is optional **drag-to-size during
  placement** (`geometry/shape-library.js` `instantiate` drops at a fixed `defaultCell`). Low priority.
- [ ] **P8 — Pipeline re-run on config change (parked escape hatch, world-present only).** A
  parameterized re-scan honouring a bespoke `scan_layer`/`exclude_blocks` → re-detect islands → rewrite
  **layer-tagged** `layer.parquet` / `islands.json`. The per-map scan-layer + custom block-exclusion UI
  has been **removed** from both editors (detection is the fixed cleaned base; the world-scanning
  endpoints are gone), so there is no longer a config-change to honour from the UI — this remains only as
  a rare, local-only override path outside the hosted flow (new-map-authoring.md §6a). (Island-exclusion →
  symmetry re-run already works without a re-scan, B7.)
- [ ] **P7 — [Deferred decision] Consolidate the layer extractors / scan passes.** **`ND2` settles the
  "consolidate vs keep" half: KEEP the exact per-layer extractors** — the World step uses them in distinct
  roles (cleaned `Base` = detection · `Surface` = visual aid · `Segments` = vertical), so they're a feature,
  not duplication; their per-layer default ignored-block sets (`Base` gets the expanded ND2 noise set;
  Surface/Y0 = air-only) are the solid-policy. Still open: the byte-parity sub-question — a segment-derived
  surface would **not** be byte-parity with the reference (endpoint-only runs also can't honour user
  `exclude_blocks`). Pairs with A4.
- [ ] **A3 — Buildability endpoint perf (verify, then optimise if needed).** Per-cell NTS over the grid
  was flagged slow; the endpoint is now live and user-visible (`N03`'s buildability overlay landed).
  **First profile it under the Configure overlay** — only optimise (spatial index / batch) if it's
  actually slow in use; otherwise close.
- [ ] **A4 — [Consider, not perf] Vector-boolean island outlines (drop the rasterize→polygon round-trip).**
  Today island outlines come from a pixel round-trip: vector shapes → rasterize to cells → BFS → `BlocksToPolygon`
  (cells back to a polygon), done only to **avoid a C# polygon-boolean lib** (sketch-authoring.md §6). We
  already depend on NTS, so the sketch-finish island polygons *could* be computed by NTS vector boolean
  directly off the shapes (union adds, difference subs), dropping `BlocksToPolygon` + the BFS for the
  *polygon*. **Not a perf task** — the row-run fix already removed the hotspot, and the cell rasterize must
  still run for `layer_segment`/`layer.parquet` (Configure height side-view + analysis). Payoff is cleanliness
  + exact (smooth) outlines; cost is NTS boolean on the authoring path and a **staircase→smooth** outline
  divergence from scanned maps. Weigh before doing.
