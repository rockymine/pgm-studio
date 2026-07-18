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
(`/maps/{id}/edit`). `C12` is cross-cutting (serves both surfaces); `C9`/`C11`
are Edit-specific. Full canvas spec: `docs/contracts/canvas-interaction.md`.

- [ ] **C9 — Kits editing UI (Teams) + per-activity status dots.** Spawn `kit` is read/sent but has no
  edit UI; there is no status-dot system. *(Two sub-items — split if priorities diverge.)*
- [ ] **C11 — Wire + verify inspector edits across activities.** `OnDelete`/`OnRename` are wired only
  in Build Regions; the Regions/Teams/Objective inspectors are **unwired** (rename/delete silently
  no-op). Wire all three + verify rename/delete/coord-patch end-to-end.
- [ ] **C12 — Extract shared Blazor components (remaining).** `SectionHeader` + the `MinecraftPlayer`
  author-resolve helper shipped (see FEATURES). Remaining, by payoff: the **`Workspace`** layout shell
  (sidebar/canvas/inspector slots, repeated across ~6 activities **and** ~16 configure phases) and
  **`ActivityRail`** (the rail + shared logo box; extract the templated item model when S2 lands).

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
- [ ] **B21 — MCP server: agent-drivable map authoring over the plan layer.** A thin MCP head (official
  C# SDK, `ModelContextProtocol` NuGet; new `PgmStudio.Mcp` project or a proxy over the running `:7894`
  API) so an AI agent can build a map end-to-end. The plan layer is the agent surface — `plan.json` is
  small, semantic, and `PlanValidator` returns rule-id findings, giving the agent a compiler-style
  submit→lint→fix loop. Tools: `plan_validate` · `plan_compile` (summary, not blobs) · `plan_render`
  (image content — agents self-correct far better seeing the board) · `compose` (a G32 plan as starting
  material to mutate) · `create_draft`/`export` (existing chain; return the export **link**, never the
  world zip inline). MCP resources: the frozen `layout-rules.md` as the design brief + `tools/seeds/*.plan.json`
  as few-shot examples — tool-description curation is the real work, not plumbing. Fast-follow after the
  composer (G32) lands; its gates (validator, stat envelopes, renderer) are exactly the tools this exposes.

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

Two tracks share this section. **The headline is the composer** (plan-then-realize): rule-based
composition of `plan.json` seeds under the frozen rules (`docs/contracts/map-generation.md` +
`layout-rules.md` + `plan-editor.md`). Its current focus — **box-driven map generation, box per box** —
is the batch in `TODO.md` (G61 → G79 → G62 → G78 → G41-A/B → G80 → G63-A/B → G63-C, M2–M4; G41-C promoted+split to G88/G89 in `TODO.md`; G41-D + G63-D parked below); the doc pass, the parked G60 soft-rule
long tail, the parked G32 realize subtracks, and the interface / hub / lane feature long-tail live here,
**reworded to be delivered *through* the box model** rather than against the current grower. The
island-detection / validation work follows. (The older / parallel **lane sketch generator** track — the
archetype starters that seeded a draft map from lane primitives — has been **retired** in favour of the
plan-then-realize direction; see `FEATURES.md` § Layout generation.) Landed so far (`FEATURES.md`): the
composer core + box-based wool-approach vocabulary (G49/G53/G54), island-outline simplification (`G6`),
the `island-roles` hook (`G11`), and the layout-generation design that resolved `G15`.
Builds on the Sketch tool (`S2`) and the intent model (`N`).

**Composer — box-model long tail (doc pass + marker/structure knobs; M2–M4 are the `TODO.md` focus)**
*(G41-C — open-variant hub & frontline patterns — was **promoted to `TODO.md` and split into G88 (hub) + G89
(frontline)**: those two complete the team unit the G63-C switch fills. Its design notes — the `FillPattern`
open-variant layer over the shared family machinery, `FrontForm` → frontline patterns, hub open patterns incl.
ring-with-hole/HB4, G39's corner/edge interlock as a `BoxInterface` constraint, per-kind slots with box-kind
ownership `hub-a/…`/`front-a/…` extending the label-preservation invariant — carry into G88/G89.)*

- [ ] **G41-D — Vacancy publishing + spawn-in-hub-bay (§4.4).** Fills start **publishing vacancies**: boxes
  may overlap (**piece-disjointness, not box-disjointness, is the invariant**), so a fill's residual envelope
  is published as claimable negative space — a **U-hub publishes its bay**, a twin frontline its recess (the
  CT8 recess generalized). Emit-side and exact (families are fixed templates), so no derive pass finds them.
  `FillProfiles` gates claims (a spawn may claim a hub bay whose mouth faces away from the axis) — this is
  what makes the **spawn-in-hub-bay** layout expressible (three wools L/T/R + the spawn in the U's bay)
  instead of forcing the G45 parallel-lane anti-pattern. Depends on G88/G89 (the U-hub/twin patterns produce the
  bays), G63-D (the partitioner claims vacancies from the partition). (review §4.4, §7.6)
- [ ] **G63-D — Generic label-inheriting fragment + `GrowthOrder` strategies + vacancy allocation.** Once the
  partitioner is the default (G63-C), **fragment** becomes a generic pass over the partition — and a
  **label-inheriting** one: a piece the pass splits or converts hands its (box, slot) ownership to its products
  (`IsolationCut` + the mid's low target are its two existing special cases), so a build zone knows which slot
  it replaced (`wool-a/entry-run`) and the §5.3 per-slot cut law (a `run`/`bar` may split, an `entry`/`room`
  stays whole) is enforced *at the cut* against the label, never re-derived; `IsolationCut`'s connector
  extrusion (born unlabeled today) is labeled the same way — fragmentation and connection mutation driven off
  labeled pieces to the limit, the moves citing slots and the mirror only verifying. `GrowthOrder` named
  strategies (`spawn-first`/`hub-first`/`mid-out`) make the emission order an **experiment axis** judged by the
  evaluator + G43, not doctrine. The partitioner allocates later boxes **from published vacancies** (§4.4) as
  well as fresh space, so a bay-seated spawn docks up to three walls for free (`spawn-first` inverts it — the
  hub's fill must wrap a staked pocket). Depends on G63-C, G41-D (the vacancy-publishing fills). (review §4.4,
  §4.5, §7.7)
- [ ] **G87 — Fill-to-`LandTargetCells` directed repair — reconsidered, not a solver.** Originally scoped as
  G63-C's core: allocate each box's footprint + land target from the budget, then resize the shape (directed
  repair off `FillResult`) until its land hits the target, retiring `SolveDepth`/`SolveWidth`/`spawnLen`. **Parked
  by author call:** "resize until it fits" grows shapes as the budget rises — the opposite of what the seeds need
  (they're already too large). Bring it back only as a **narrow rule** — grow a box solely when it is below its
  family's minimum, never to spend surplus budget — with surplus going to **count** (another box) or fragmentation,
  not length. Pairs with **G84**'s spawn cap; the **wool** depth/width wants the same cap-review (grow-to-absorb
  deepens wool approaches too — the I→L→Z escalation is wanted, unbounded stretch is not). (review §8; the sizing
  explainer artifact)
- [ ] **G76 — The marker inspector exposes none of a structure's knobs, so every placed marker is the
  default.** The stamps are all built and the plan format names them — what is missing is the UI. A
  destroyable has **six styles** (`pillar-1/2/3` · `cube-3` · `cube-4` · `column-plus`), a material from the
  closed DTM vocabulary (obsidian · emerald · gold · ender stone) and a `float`; a core has
  `size`/`height`/`shell`/`openTop` and the `float`+`leak` pair; a wool has a `color`. **The inspector offers
  kind, an "On piece" readout, spawn Facing, and Delete — nothing else.** So a marker dropped in the editor
  silently takes the compiler's default, and the only way to pick is to hand-edit the `.plan.json` and
  re-import. The wool-colour gap predates the objectives; the point is the pattern, not the one field.
  **Two rules the controls must respect**, both already enforced server-side and both easy to violate in a
  form: an unknown style is an **error**, never a silent default (so offer `DestroyableStyles.All`, not a
  free-text box), and `float`+`leak` are **one knob** (DC2) — a form that lets one be set alone reintroduces
  the dig depth nobody chose. Prefer showing the derived consequence over the raw pair: the honest readout is
  "players dig N blocks", i.e. `ObjectiveDefaults.DigDepth`. Cheap to sanity-check — the iso preview already
  redraws each box from the same stamper the export uses (G73), so a style change is visible immediately.
- [ ] **G77 — `bedrockCentre` is a stamp no authoring path can reach.** `ObjectiveStamper.StampDestroyable`
  takes a `bedrockCentre` flag that fills a `cube-3`/`cube-4`'s core with bedrock, so players cannot hollow
  one out and hide inside — it costs nothing to model because `materials` names only the outer block, leaving
  the bedrock invisible to the goal (neither counted in its health nor breakable). But **nothing can ask for
  it**: it is absent from `DestroyablePlacement`, `DestroyableIntent` and the world builder's call, so it is
  dead capability. Either thread it through as a flag (plan → intent → stamp, defaulting false) or delete it;
  a parameter no caller can set is a claim the code does not keep. Decide against the corpus — how many real
  cube destroyables are actually hollow-proofed. Pairs with `G76`, which is where the flag would surface.
- [ ] **G75 — Score a marker whose structure cannot paste.** The evaluator has no term for "this marker's
  structure has nowhere to sit": an iron / spawn / wool marker whose stamped footprint (4×4 iron, 8×8 cube)
  is not fully on terrain, up to none of it — the case that currently builds into the void. Sits beside G74:
  once the floor probe is equivariant, a partly-overhanging structure is a *quality* signal (rank it by the
  fraction of footprint off-terrain, or off a single surface) and a fully-unplaceable one is a hard/structural
  term. Reads `BoardStructure` measurables + the surface map, never a family name; scored as `Band` distance
  with `Evidence` so the canvas can isolate the offending marker (`docs/contracts/layout-evaluator.md` §6).
  Note the stamped footprint, not the marker point, is the subject — a marker legally on its piece can still
  hang its cube off the edge.

- [ ] **G64 — Doc pass on `map-generation.md` (reconcile with shipped code).** The canonical doc silently
  mixes description and aspiration. Declare the emission order an **experimental strategy axis over the
  constraint graph** (`spawn-first`/`hub-first`/`mid-out` are `GrowthOrder` knobs), not a fixed sequence —
  §2/§4 currently state it three different ways (Finding 1.1). Add **current-vs-target status per pipeline
  stage** (Finding 1.2). Reconcile "the frontline is an output" against the shipped mid-outward input.
  Name the board deriver as **code** (`BoardDeriver` / `ContactGraph` / `BoardStructure`), not the
  `tools/deriver` script (Finding 1.3). Trails the code that makes each statement true. (review §1, §7.8)

**Evaluator — the soft-rule long tail (parked: rules follow the composer's vocabulary, never run ahead of it)**
- [~] **G60 — Composer evaluator: soft-term long tail + ranking harness.** Parked while the box model lands
  (foundation, hard gate, soft-term catalogue base, frontline/rotation terms, editor wiring: `FEATURES.md`).
  Minting and labelling soft rules for structures the composer cannot yet compose proved the wrong stage —
  the crammed-frontline dead end (G69) was hard to detect and harder to explain *because the structure
  isn't composable yet* — so the remaining terms resume as each box kind's vocabulary (slots, patterns,
  vacancies) exists to bind them to. Remaining slices: **(1)** the §6 soft-term catalogue leftovers —
  cramming (parked on G69), approach count/WL8·G45 (gated on G62 slots), height/EL1·EL4 (blocked on the
  G32-C elevation pass) — each reading `BoardStructure` measurables only (never a family name — the
  enumeration trap), scored as `Band` distance with `Evidence`; **(2)** the hole-hunt loop keeps the
  **lowest-scoring** acceptable attempt — the first point composed output shifts, its own re-baseline;
  **(3)** the ranking harness `eval-rank.cs` + minimal-pair negatives (`tools/seeds/negatives/` +
  `labels.json`): `Score(negative) > Score(positive)` **and** the labelled term fires; per-term tests.
  (review §5, §9; `docs/contracts/layout-evaluator.md`)

**Rule visualization & slot-relation rules (§9.7/§9.8 — terms already return drawable `Evidence`)**
- [ ] **G66 — Rule-visualization renderers (illustrated catalog + reject inspector).** Generic passes over the
  `Evidence` primitives every term already returns — **zero per-term drawing code**. (1) `tools/deriver/rule-cards.cs`:
  reuse the `derive-gallery` SVG card machinery to render, per term, a **pass** card + a **violated** card with
  its evidence overlaid — the fixtures *are* the per-term unit tests — outputting an **illustrated
  `layout-rules.md`** (one HTML page: prose + do/don't card per rule id). A term is not *done* until its card
  renders, so the fixture doubles as the documentation and neither drifts. (2) **Reject inspector**: a logged
  `{seed, termId}` re-composes the failed attempt and renders the killing term's evidence (why-it-died becomes a
  picture). (3) **Minimal-pair visual diffs**: the G60 ranking harness renders each pair side by side with the
  negative's expected-term evidence. (The editor overlay is folded into G60's DTO wiring.) Depends on G60.
  (review §9.7)
- [ ] **G67 — Fill-time slot invariants.** Slot-relation rules checked **when a box is filled / in `emit-verify`**,
  where the emitter's slots are in hand: "only a `run`/`bar` splits into lane + build-lane, an `entry`/`room`
  stays whole", "the entry ≥ the lane it feeds", "the room-run stub stays shorter than its bar" — each a fill
  invariant citing its slot rule (`map-generation.md` §5.3), visualized through the same card machinery
  (`Evidence` tagged `slot:*`). This is where the majority of slot rules live. Depends on G61. (review §9.8)
- [ ] **G68 — Evaluator-side slot-relation terms (generated plans).** Slot rules as ordinary `ILayoutTerm`s
  over plans whose slots are **in hand**: composed output evaluated in the compose loop (`EvalContext` gains
  the emitter's `pieceId → family + slot` map; the mirror cross-checks it via G62's `AssignSlots`).
  **Conditional-fire**: a term runs only where slots are known — an authored/traced/loaded plan has none and
  scores clean on slot terms by construction (derive-side recovery of finished maps is retired,
  `docs/wool-approach-read-investigation.md`) — keeping slot rules off the enumeration trap
  (`layout-evaluator.md` §8). Evidence carries `slot:*`-tagged rects/measures; the **slot legend card per
  family** (the §5.3 template table drawn from `SlotTemplate` + `ShapeEmitter`) joins the `rule-cards.cs`
  output as the shared key. Depends on G62, G66. (review §9.8)
- [ ] **G69 — The deriver mis-reads dense mids: crossing-corridor + rotation primitives, then the cramming
  term.** The frontline-cramming negatives (`tools/seeds/teaching/crammed-frontline-*`) can't be scored because
  the deriver's structural reading systematically contradicts the play-experience on saturated mids — nine
  measurables tried over the Slice-C investigation (stones−crossings, stones/frontline-width, per-stone
  void-exposure, stone density, crossing aspect, stone spacing, opposing-frontline overlap, band-length/team-
  footprint, uncrossed-void) and none expresses cramming. The diagnosis, from the author's models + the
  reconciliation gallery (artifact `faf3ffcc`):
  - **acapulco is NOT a clean positive** — the author confirms it "can read bad" (its crossing runs nearly as
    long as the team footprint too). So the goal is **not** to separate crammed from acapulco; both should read
    mildly-to-badly crammed. The old "crammed ≡ acapulco" paradox dissolves — they're similar because they're
    both long-band-crammed.
  - **Band length isn't the separator** — `bandSpan/teamSpan` is 0.67 on `crammed-single` *and* on the good
    resolutions `rotation-stone`/`move-closer`; positives run higher (aether 0.84). The resolutions kept the long
    band and are good because they added **rotation**, not because the band shrank.
  - **The deriver's mid reads fight the eye, repeatedly:** per-stone void-exposure reads acapulco's stones *more*
    exposed than the crammed seeds' (opposite); opposing-frontline overlap reads acapulco *more* aligned
    (opposite to "offset masses"); `crossRoutes=2` on `crammed-double-band` claims rotation the author says isn't
    there ("you just hop between islands, no way to switch"); and its two far-end islands are mis-classed **team**
    stones because the (deliberately bad) spawn↔wool path routes through them (captivity/route rule bends to bad
    marker placement).
  The real work is **deriver primitives**, then the term: **(a)** a **crossing-corridor** read — the mid modelled
  as a corridor with a cross-section and a length, so "band as long as the team footprint" (single-band) and
  "band-width = stone-size, N hops, no lateral switch" (double-band) become expressible; **(b)** **rotation that
  means rotation** — a measure of "can a player actually switch sides here", not the ring-count `crossRoutes`
  (crammed-double must read *no rotation* despite two ringing zones); **(c)** **robust stone classification** —
  team-vs-neutral must not flip on degenerate spawn/wool routing (offset team masses / mid-rectangle stagger is a
  real element that falls out here — "a real element maps can use"); **(d)** only then the cramming term, likely
  FR4's "one crossing is fine only if it rotates / isn't over-long vs the footprint", with acapulco landing
  mildly bad by construction. Until (a)–(c), cramming is not expressible. (G60 §6; from the Slice-C
  investigation, artifact `faf3ffcc`)

**Composer — mid / frontline / interface (reframed as evaluator terms + partition constraints)**
- [ ] **G38 — Multiple / parallel mid bands + their variations.** The composer ships only the CT1 clean
  form (one band spanning the axis). Add **two-or-more parallel bands** (FR7, rot_180-only, variable-length)
  and the authored **variations**: a **hole in the build zone**, a **stepping stone between the dual bands**,
  the two-sided plaza (`big-board-wool-two-sided-plaza-parallel-mid`). Each band needs its own dock + hop
  arithmetic; the fan/merge must keep the bands distinct. Unshipped feature, not a bug (flagged 2026-07-05).
- [ ] **G39 — Frontline↔build-zone full-face dock (requirement — delivered via the box model, not worked
  standalone).** The band must dock the **full frontline face** at shared corner/edge lines: no
  flush-on-one-edge-short-on-the-other (`gen-p30-t2-rot_180-s1`), no too-thin build zone
  (`gen-p20-t2-mirror_z-s1`). Under refactor-first this is delivered as **(1)** a hard evaluator term in
  G60 (`band-docks-full-face`) that *catches* violations and **(2)** a `BoxInterface` full-face constraint
  at emission / partition time (G41/G63) that *prevents* them — the original standalone fix (teach the
  current band the CT7 corner-snap the stones use) is **dropped as throwaway**, since M2–M4 replace that
  band. Anchor requirement; do not attack directly. (review §1.2, §4.2, §9.6)
- [ ] **G40 — Enclosed dead-space / hole-size cap (requirement — evaluator term + partition constraint).**
  The hole enclosed by hub + frontlines + build zones stays **~10×10** (occasionally 10×20, never 10×40 —
  `gen-p30-t2-rot_180-s7`'s twin 35-block frontlines); generalize to **any** void an L/U lane wraps.
  Delivered as a **soft** G60 term (hole-extent band from the seeds) plus a `BoxInterface` / partition
  constraint on frontline extrusion (G41/G63); surplus routes to width / more routes via G61, never a
  stretched frontline or a void-wrapping lane (the length driver is **G44**). Anchor requirement.
  (review §4.2, §9)
- [ ] **G42 — Spawn docks to a piece, never submerges.** *New.* The spawn is meant to **dock** (abut) its
  neighbours; on some boards it is **fully engulfed** by the surrounding pieces (`gen-p20-t2-rot_180-s7` —
  which also surfaced the first accidental terrain hole). Enforce spawn-as-dock (SP): a spawn touches by a
  readable edge and is never interior to the merged land.
- [ ] **G43 — Composer ↔ example-set conformance sweep (consumer of the evaluator, G60).** Over a
  generated-board sweep, aggregate the evaluator's soft-distance-per-term against the teaching set
  (`tools/seeds/teaching/`) into a report — the eyeball-cards analogue, and the gate-in-aggregate that
  would have caught G39/G40 before the gallery. The measurables (hub-hole size, band↔frontline
  edge-coincidence / width-match, extrusion length, mid-piece share, island count) are **G60 terms**, not
  defined here; the hard gate is G60's hard terms. Feeds the G32-D goldens. Depends on G60. (review §9.5)

**Composer — hub / spawn / wool lanes**
- [ ] **G44 — Budget→length decoupling (traced root cause of the lane bloat).** The grower's area gate
  rejects any unit under 80% of `LandPerTeam`, and its only real spend-vocabulary is **longer lanes** — so
  a big budget is absorbed by length, not structure. Trace (`gen-p30-t2-rot_180-s7`, 220 cells → 217
  spent): a **95-block L** spawn lane and a **95-block U** wool lane that wraps a giant empty square,
  putting the wool out of play as a dead-end a defender just holds. Fixes in order: **(a)** cap absolute
  lane lengths to the authored norms (spawn ≈ 20–30 blocks; wool lanes bounded — LN2's 50-block cap is
  *per collinear chain*, so an L/Z stacks two); **(b)** route surplus into **structure, not length** — the
  wool-box migration (G61) supplies the vocabulary: **escalate the family** (I→L→Z→U/H/scythe) and widen,
  rather than stretch, with directed budget repair once the partitioner lands (G63); **(c)** re-examine the
  budget (whether `LandPerPlayer` over-scales past ~p16 and whether the area gate's lower bound should
  relax so a compact unit needn't hit the full target).
- [ ] **G82 — Z entry widening along its bar interface (the missing half of the G51 knob).** G51 shipped
  entry widening for the scythe only (`attachmentWidth`, along the entry ↔ entry-run seam, the w2/w4/w6
  grammar, hard-capped short of the bar). Give Z the analogous knob: the entry widens **along its
  interface with the slot it attaches to** (the crossing bar), same grammar, never perpendicular.
  Overfill cap: the entry must **never spread far enough to reach the room-run column** — that merges the
  arms and unmakes the Z — and must not swallow the notch vacancies the emitter publishes; reject with a
  named error, never silently clamp (the scythe guard in `ShapeEmitter` + `EmitterPlacementKnobTests` is
  the model). May largely fall out of G80's interface-width machinery — a wider Z entry is the same
  connection, wider — in which case this reduces to the emitter knob + guards + family-stability tests.
- [ ] **G83 — Wool-approach budget: own rules, not generic absorption (the widening spend).**
  *Investigation + rules.* The grower's second solve widens the whole Z box so the **crossing bar absorbs
  budget** (`SolveWidth`/`ArmWidthCap`) — which stretches the wool approach across the sum of its pieces:
  a long entry → bar → room-run walk is a bad spend that just makes the map play worse (the G44 lane
  bloat re-entering through width instead of depth). Wool approaches need their **own budget law**, one
  of: **slot-labeled piece limits** — per-slot length/area caps the labels make enforceable, plus a cap
  on the approach's **total path length** across pieces (LN2's 50 is per collinear chain, so an L/Z
  legally stacks two) — or **box growth limitations** (a wool box may not grow past its family's playable
  envelope; surplus routes to *other* boxes instead of inflating the approach). Refines G44(b): "widen
  rather than stretch" holds for hubs/frontlines, but for a wool approach widening the bar *is*
  stretching, path-wise. Sequenced with the partitioner's per-box budgets (G63, `Box.LandTargetCells`).
- [ ] **G45 — Third wool: rarer, and placed as a real route.** A third wool is sampled at **40%** for ≥16
  players and **always** built as a 2-cell dead-end straight back beside the spawn lane (`wool-lane-c`;
  `gen-p20-t2-rot_180-s13` — the wool squeezed next to the spawn). That parallel-lane placement is the **G45
  anti-pattern** — a failure mode of the square-hub model, **never a pattern to sample**. Reality: 2 wools
  common, **3 rare**, and a genuine third wool sits as **its own route**. The missing **positive** is the
  three-wool **L/T/R + spawn in a U-hub's bay** layout (a real third-wool route enabled by a claimed vacancy,
  §4.4) — author it as the G45 teaching seed the current set lacks. Lower the rate and add real 3-wool
  placement patterns. Depends on the G41-D/G63 vacancy mechanism (spawn claims the hub bay).
- [ ] **G37 — Lane-archetypes track (lane shapes · connections · hub shaping · alt entries).** The real
  lane grammar: authored **lane archetypes**, **what connects to the frontline**, **how hubs shape** (today
  the hub is a dumb square everything smashes into — G88), and **alternative entry points** to a lane (a
  long dead-end is pointless without alt routes — the defender just holds the mouth; not formalized yet).
  "Lane-heavy is bad" is a defect, not an archetype to sample (see the `composer-lane-archetypes-future`
  memory); the budget-driven over-long lanes it produces are traced to **G44**. Blocked on more teaching
  maps; sequenced **after** the interface layer (G39/G40).

**Composer — realize & unblock**
- [~] **G32 — Composer realize: the two remaining subtracks (C structures & elevation · D gates & goldens).**
  Everything else under this id shipped (`FEATURES.md`): the A track (envelope + team-unit grower), the
  B track (mid carve, isolation cuts, build-zone discipline, the compose acceptance gate), the
  `spawn`/`wool-room` room carve (G49), and the plan→sketch+intent compile chain (`PlanCompiler`,
  golden-pinned). Two separable slices remain:
  - **G32-C — structures & elevation.** Generated plans realize *flat and bare*. Give them their third
    dimension and their furniture: raise the spawn and face it toward play (SP3/SP4), stamp its iron
    (SP7), give every wool approach a stepped climb to the room (WL5), lift the rooms off the base
    surface (EL6), lay the height palette (EL1: base 9, step 2, odd heights only) and the walls (ST4).
    This is **a second generator, not a checklist** (review §11.3): about a third of the authored seeds'
    pieces are stair treads and every wool tops a deliberate climb, so the pass wants its own small
    pattern vocabulary (a staircase chain climbing an interface is a pattern in the map-generation §4.3
    sense). If generated maps ever read valid-but-flat, this is the missing soul. One of the last
    pipeline steps — parked while the box model lands, though it is technically independent of it.
  - **G32-D — gates, goldens, emit.** The full-pipeline acceptance gate on composed output:
    `PlanValidator` zero errors with zones present, `FannedGraph` fully traversable, stat envelopes vs
    `seed-stats.md`, the `plan.json` loadable in `/plan`, and fixed-RNG goldens under `tests/`.
    **Blocked on G63** — every box milestone re-keys the RNG, so goldens frozen earlier would just
    re-break.

  p5 / rot_90 stays a known limitation until **G35** (below).
- [ ] **G81 — Declared-bay scythe via elevation (the height mechanism — parked until the elevation
  stage).** A flush host that seals the scythe's bay (touching both entry and wool) is only legal once
  height can enforce the approach: the wool raised significantly so the **entry-host dock is the sole way
  in**, the scythe terrain **stepping up from entry to room** — the sealed bay then becomes a declared
  hole under it. Until the elevation pass exists (G32-C territory), a host touching a wool `room` is a
  **hard violation and rejects** (the G80 docking modes cover legal scythe entry). Blocking question: the
  step profile (how much rise per slot, and whether WL5's stepped-climb vocabulary already covers it). Have the composer author
  buffers/allotments during generation — reserve a ≥1-cell border on rot_90 boards so the quarter-turn
  image can't self-collapse, hold spacing on small boards — to unblock p5 (BZ6 + spawn ≥2×2 over-budget at
  325 blocks²) and p5/t4/rot_90 (interior-overlap self-weld). Teaching material + a `layout-rules.md`
  amendment first. Never fix p5 by enlarging the board (re-triggers the LN2 arm stretch).
- [ ] **G36 — Residual composer polish (from the B2 review).** What's left after the mid-feel slice shipped
  and (2)/(3) moved to G37/G41: **(1)** confirm the rot_180 mid-band asymmetry (`p30-s7`/`s13`) is a real
  off-centre band vs a render artefact; **(4)** cap spawn-lane growth (`p30-s13` over-grown L); **(6)**
  frontline-**count** variety (not every board double-frontline).

The remaining generator / detection / validation work sorts into three domains:

**Generator (lane algorithm → Configure)**

- [ ] **G29 — Climb profiling on lane chains (straight ramps vs switchbacks, approach labeling).** On the
  seam graph, detect *climbs* (maximal monotone-elevation traversal runs), classify straight ramp vs
  switchback/hairpin (direction reversal while climbing; displacement ≪ path length) and landings, and label
  each climb by its top-end anchor (wool approach / mid ascent / interior) and per-team use (attacker climb
  vs defender rotation). Spec: `docs/contracts/plan-editor.md` §2 "Climbs". Composer vocabulary for WL5/FR3
  (straight approach vs space-packing switchback vs defensible landing). Depends on `G24`'s chains.
- [ ] **G33 — Traffic ground truth: logs-only graph generator + flow priors + recovered footprints.**
  Input contract: **one zip of raw pgmlogger parquet per map** — nothing else (formats + the validated
  derivation live in `docs/contracts/traffic-ground-truth.md`; no external analysis project involved).
  Build: (a) the **logs-only `traffic_graph.json` generator** (occupancy/edges from 2 s positions, POIs
  from spawn/wool/capture event clusters, void via the symmetry-pooled fall-share signal at **block
  resolution** — the rim-aliasing fix — islands as traffic-minus-void components; ingwaz validation:
  islands 6/6, void recall 1.0); (b) a plan-editor overlay rendering traffic heat + the **emergent build
  regions**; (c) flow priors to score composer candidates (mid/team occupancy split, approach usage,
  void share, frontline band); (d) recovered footprints as CT test articles (first pair:
  `tools/traffic/ingwaz.*`). The author supplies per-map log zips (uploaded like ingwaz's, or batch-
  collected in a local session); only zips, graph JSONs, and priors enter the repo.
- [ ] **G31 — Scaled structure presets (stamps must fit tiny and huge maps).** The spawn cube / wool
  cage / iron cube stamps are fixed-size (8×8 footprints); on `mirror-tiny-map-cliff` (1-cell pieces,
  markers at block centres) the stamps overlap the piece bounds, and 30+/team boards could take larger
  presets. Scale the stamp presets with map class (the G8 coupling) or the carrier piece size; author
  request from the tiny seed.
- [ ] **G24 — Junction-region derivation + Hubs overlay + lane chains.** Derive hubs as *internal* computed
  structure on the unioned island footprint (mouth-extrusion intersections of ≥3 access mouths — see
  `docs/contracts/plan-editor.md` §2 "Junction regions"; corners yield nothing), expose them through
  `/api/plan/inspect` and a "Hubs" editor overlay, and build **lane chains** on top (corridors between
  junctions/dead-ends) so width/length lint and depth checks measure along a whole lane rather than per
  piece. The anchor for composer-side junction placement/verification later. (layout-rules.md PC1)
- [ ] **G34 — Theming & styling rules: material palettes + prop stamps (trees etc.).** Generated maps are
  100% playable and 100% bare stone — extend the meaning→structure move to the world's *read*. A rule-driven
  theming pass at rasterize/export: **theme = material palette** (per role/stratum: surface cap, body,
  cliff faces, wool-approach accents) + a **prop stamp library** (trees, rocks, lamps) with placement rules
  (density per piece kind, never inside corridor-min footprints or the spawn/wool stamp plateaus, respect
  build zones and G6 headroom, seeded-deterministic like the composer). The stamp machinery is precedent
  (spawn cubes / wool cages / ST1–ST4; `G31` scales them); capture theming rules the same way
  `layout-rules.md` was captured — expert-authored, correction-by-id, a `theming-rules.md` contract.
  **Deliberately moves the division-of-labour boundary** (baseline theming vs the author's "always manual"
  post-detach polish): generator does layout + *baseline* theming; character/set pieces/themed identity stay
  the author's (Tier 3 unchanged).

**Island detection**
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

**Validation / playability**
- [ ] **G65 — FannedGraph ↔ ContactGraph adjacency reconcile (deferred from G59).** `FannedGraph.LandAdjacent`
  (reachability) diverges from the rect-layer authority `ContactGraph` (`Classify` + `Components`) on two
  counts: **(1)** any area overlap connects regardless of surface delta, while `Components` unions an overlap
  only at `SurfaceDelta == 0`; **(2)** an edge connects only at full corridor width (`Land`), while `Components`
  also unions `Narrow` seams. Pick one rule for reachability and add a test (review 2.3 / 6.5). Needs per-node
  surface carried into the fanned graph (not there yet) and validation against the traversability harness
  (`tools/PgmStudio.RoundTrip --traversability`), so G59 left it behavior-unchanged with the divergence
  documented in `FannedGraph.LandAdjacent`.
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
