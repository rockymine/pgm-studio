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
- [~] **N11 — Monument Y must seat on terrain; coord-input moves must re-snap.** The **point tool** now
  seats every spawn it places — team spawns + orbit copies, the observer, and wool spawns — on the target
  column's floor via the shared `ColumnFloor` helper. Still open: monuments aren't seated at all; and moving
  a spawn (team or wool) via the **coord inputs** rewrites X/Z without re-snapping Y to the new column, so
  only the point tool re-seats. Pairs with `N08` (monument Y editing) and `CV11` (the side-view clamp side
  of the same problem).

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
  `/design` gallery **regenerated** to render the real components. Decisions that shrank the rest: `FlowBar`
  and `Console` are **single-use** (one wizard bar in `ConfigureLayout`, one pre-flight log in
  `ReviewPreflightPhase`) — not worth componentizing, left raw (same call as `CoordRow`, dropped because
  `ctrl-row` triples vary XYZ/XZ/R·H). `Card`/`CardGrid` **deferred** (only ~8 landing cards; low payoff).

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
- [ ] **B21 — MCP server: agent-drivable map authoring over the plan layer.** A thin MCP head (official
  C# SDK, `ModelContextProtocol` NuGet; new `PgmStudio.Mcp` project or a proxy over the running `:7894`
  API) so an AI agent can build a map end-to-end. The plan layer is the agent surface — `plan.json` is
  small, semantic, and the validator/evaluator return rule-id findings, giving the agent a compiler-style
  submit→lint→fix loop. **Pull up after G119 + G117 + G125 land** — the MCP head then wraps endpoints
  those tasks build (duplicated plumbing before them), and its genuinely new work shrinks to three
  things: the PNG rasterization path for `plan_render`, the `emit_family` stamp tool, and the
  tool-description/resource curation (still the real work). Tools: `compose` (the G117 endpoint —
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
  resources: `layout-rules.md` + `map-generation.md` as the design brief, `tools/seeds/*.plan.json`
  (incl. the funnel exemplars) as few-shot examples, and the G118 verdict JSONL once it exists. Scope
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
longer exists — is condensed into **`docs/layout-generation-ideas.md`**: one idea per few lines, grouped
by theme, **ids preserved** (never reuse one). Pull an idea back onto the board by id when it becomes the
focus; the full original task text is in this file's git history. The current focus (the generator in the
studio, G117/G118) is in `TODO.md`.

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
  `src/` + `tests/` (e.g. `ImportEndpoints`, `WorldScanPhase`, `WorldFeatureWriter`) — sweep them.

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
