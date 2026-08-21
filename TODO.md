# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

## The vision, and why this is the whole board

**The studio is one pipeline with two audiences, and only one of them can read prose.** A person drives it
through a browser — panels, a canvas, a wizard. An agent drives it through `/api` and a CLI, and everything
it can learn about a map it has to learn from a response body. Both are first-class, and the second one is
the reason the first can be trusted: an agent that can author a map end to end is a continuous test that the
tool actually works, and every place it stumbles is a place a person was going to stumble more quietly.

**What is being built is a studio that can describe itself.** The four levels a map is described at, the
gates in front of each hand-off, the rules those gates cite and what a map may be asked for at each stage —
all of that exists, and almost all of it is stated in prose that nothing verifies. So the same fact is
written down two or three times, drifts, and is discovered again by an agent that paid a build cycle for it.
`docs/architecture.md` is the survey; this board is the work it named.

**The order below is not a preference, it is a dependency chain.** Nothing can be verified until the surface
says what it is, so the contract comes first — over the boundary `RP28` settled, since a shape cannot be
declared where no project can hold it. A gate belongs to whichever door someone put it behind until there is
one place a use case lives, so the application layer is second. A caller cannot branch on a fault until the
fault has a class, so the taxonomy is third. And a state machine over a pipeline whose steps are still HTTP
handlers has nothing to hold, so the lifecycle is last.

**The board is deliberately larger than the soft cap** — eighteen entries against `CLAUDE.md`'s ~6–12. That
is the author's call and the trade is stated: this is one coherent programme with an order, and splitting it
across two files would hide the order, which is the only part that matters. **Nothing new is added here
until a phase drains.** A finding made while working lands in `BACKLOG.md`.

## Four of the eighteen carry a question only the author can answer

The rest are drivable from the entry plus `CLAUDE.md` — the shape is stated, the evidence is measured, and
the file and line are named. These are not, and each is blocked on a decision rather than on work. `RP13`
is kept in the table with the answer beside it, because the answer is the part an implementer needs.

| Entry | The question |
|---|---|
| `RP13` | *Answered by the author: **its own project**, and an operation is a step of the pipeline — the thing that builds the map, callable from two sides with no dependency on the UI running.* The steps are named now: the thirteen handlers that read and write, three of which are already operations. |
| `RP14` | Sign-off on the six categories. The ids do not move, but a category is as stable as an id once a caller branches on it. The `concerns` list is `RP26`'s — its vocabulary is now written down there — and `RP14` ships without waiting for it. |
| `RP15` | What **is** `WX9`? It is stated as a rule in two documents and fired by nothing. Declaring it and retiring it are both one commit, and only the author knows which. |
| `RP16` | The transition table is a product statement, not a derivation. `flow.md` says the flow is one-way — does that mean a built map may never be re-planned, or only that nothing reads back up? |
| `RP19` | Keep `tools/relief`'s ten figures by committing them, or delete the tool. Either is right; which one depends on whether those figures are wanted in `relief.md`. |


## Phase 1 — say what the surface is

Every operation declares what it answers and, on all but three write routes, what it takes. What is left is
the three the generator refuses, the difference between a declared shape and a bound one, and the two
consumers that keep the contract by hand instead of reading the records it is built from.

- [ ] **RP41 — Three preview routes cannot declare their body, and a hierarchy is why.**
  `POST /terrain/material-preview` takes a `TerrainMaterial`, `/terrain/theme-preview` a `TerrainTheme` and
  `/room-styles/preview-snapshot` a `HouseStyle`; all three reach `TerrainMaterial`, and declaring any of
  them fails the whole document with *"Discriminator value for FieldPatternMaterial not found"* — a 500 on
  `/api/openapi/v1.json` and `/api-docs`. `[JsonPolymorphic]` sits on `TerrainMaterial`
  (`Minecraft/Painting/TerrainTheme.cs:72`) and names fourteen leaves; `FieldPatternMaterial`
  (`TerrainPatterns.cs:163`) is an unregistered abstract link between it and three of them, which
  System.Text.Json is content with and the generator is not.

  Two answers, and the choice is the author's. Register the link, which is a schema-shaped change to a domain
  type. Or **compose instead of inherit**: `FieldPatternMaterial` shares five fields across noise, turbulence
  and electric and nothing else, which is the shape `CLAUDE.md` warns about, and the three would hold a value
  rather than extend a base — a change to the stored theme JSON, so it needs the library read first.
  `SchemaCompletenessTests.StillUntyped` is 3 and falls to 0 when this lands.

- [ ] **RP40 — Bind the shapes the surface now declares.** The 39 declared bodies are read by hand behind
  the declaration, so `RequiredFields` runs on only the 22 routes that bind one, and the **15**
  `EditException.Unreadable` throws in `Pgm/Editing` stand where a binding would have refused. Binding is not
  a sweep: an update body needs absent-versus-null, which a bound record loses unless every field is
  optional, and a region create is a union over `type` that no one record expresses. So take the routes where
  a binding genuinely refuses something — a missing `region_id`, a `yaw` that is not a number, a
  `max_players` that is not an integer — and leave the rest declared. The other 38 refusal sites in
  `Pgm/Editing` read the map the edit lands on and stay whatever happens here.

- [ ] **RP42 — The endpoint tables are checked one way and derived none.** `DocumentedRouteTests` asserts
  every tabled path exists; nothing asserts a route *appears* in a table, or that the status codes a row
  claims are codes the schema publishes. Generating the tables is the wrong fix — the "Answers" column is
  editorial prose written for that tool's reader, and a generated sentence would be worse. Check instead:
  every `/api` route is in exactly one table or on a named list of the deliberately unlisted, and each row's
  codes are a subset of its operation's. `DocumentedBodyTests` posts 8 documented bodies against 67 write
  routes and is the file it belongs beside.

- [ ] **RP43 — Decide whether a generated client is worth its dependency.** Route strings are what stays
  hand-written after `RP11`: **152 literal and 106 interpolated** across 49 files, where a typo is a runtime
  404 rather than a compile error. A generated client fixes that and nothing else — the response types come
  from `Contracts` either way. It costs `NSwag.CodeGeneration.CSharp` as a build-time package (14.7.1
  restores clean through the proxy) and a committed generated file, which is a second shape of the surface
  in the tree. Answer it after `RP11` drains, when what is left to gain is measurable rather than assumed.

## Phase 2 — one place a use case lives

One entry, and the phase's whole point: the step that stops a gate from belonging to a door.

- [~] **RP13 — A step of the pipeline lives behind the door it is reached through.** Of 149 handlers, the
  bodies total 2,146 lines at a median of 10, so the volume is not the problem: **thirteen** read state and
  write it back, and three of those also run a gate. Those are the use cases, and each is reachable only by
  sending an HTTP request. Three now sit in `Api/Services` as operations that answer `Findings` and let the
  layer above render the envelope — `MapExportLoader`, `SketchFinish`, `MapFromDocuments`. Move them to a
  project of their own with HTTP, the CLI and tests as three adapters, and take the remaining ten with them.
  The load-or-404 prologue appears 37 times verbatim and becomes one.

  *What makes it worth doing is the **order**, not the duplication: storing an intent projects the document
  from the intent's own `meta`, so authors written before it are lost — silently, with 200 on every call. That
  rule is written in `flow.md`, in the mapgen repo's driver, its README and its generation notes, and was
  enforced nowhere until `MapFromDocuments` made the sequence itself the answer. `RP32` needs the same layer
  for the same reason: a findings summary has to **call** the gates, not restate them.*

## Phase 3 — one class of fault

The cheapest of the five: a change to `Finding` plus a sweep of 71 constants, and the ids do not move.

- [ ] **RP14 — A fault carries an id but not a class, so a caller has to know all 71 to branch once.** The
  only machine-legible thing a finding carries is the id. A caller deciding whether to fix the request,
  change the design, change the map or report a bug has no field to read it off, so every driver either
  learns 71 ids or guesses from the sentence.

  **The fix is additive; no id changes and no rename.** `Finding` gains **`category`**, a closed six —
  `malformed`, `unknown`, `conflict`, `unsatisfiable`, `unplayable`, `internal` — which is the only thing a
  caller branches on. `RuleDoc` gains **`name`**, the rule constant's own identifier kebab-cased, which the
  catalogue already carries buried inside `owner` (`…PlanRules.NoSpawn` → `no-spawn`), so `WX6` reads
  `room-unreachable · unplayable` with no new authoring. `?category=` narrows `/api/rules`. Answer the
  envelope as RFC 9457 Problem Details in the same commit, whose `type` URI is the `/api/rules` lookup that
  already exists.

  **The family prefix is not the defect.** `PL2` and `EX2` read as one fault under two ids until their
  `fix` lines are compared — *add an entry in `placements.spawns`* against *give the intent at least one
  spawn*. Two documents, correctly two rules; the same holds for the five ids meaning *a name that resolves
  to nothing*, which sit over the plan, the layout, the map document and the request. The prefix is already
  most of a **subject** axis.

  *`RP26` carries the other half and is the author's: a rule concerns a **combination** — `WX6` is a plan, an
  objective and a structure at once — so what a rule gets is a `concerns` **list**, not a single subject, and
  a prefix could never have carried it. Ship `category` and `name` without waiting; the list is additive
  again when it lands.*

- [ ] **RP31 — A prop the studio deleted and a lane it merely thinks is narrow arrive as one severity.**
  `Severity` (`Domain/Finding.cs:4`) is `Refusal | Complaint`, and `Complaint`'s own docstring rules out half
  of what it carries: *"a complaint the author may ignore … none of them the tool's to overrule"*. True of
  `OB23` and `DC3`, where the goal stands and the finding is a remark. False of all six `DR-*` rules, where
  the tree, boulder or building is **deleted from the world** — the author cannot ignore that, because the
  thing they authored is gone. So a caller reading `warnings` cannot answer the question a write leaves open:
  did what I posted survive? Add `Severity.Decline`, carried by the six `DR-*` rules and by `OB19` once
  `RP4`'s ruling lands. Same envelope, same `warnings` key, no new route: `Finding.Refuses` stays the refusal
  test and a decline is a success that took something away. One site turns a boolean into a three-way,
  `Finding.Refuses` (`Vocabulary/Finding.cs`), since the record is what is serialized.
  Orthogonal to `RP14`'s `category` — that says what
  kind of fault, this what became of the input. `refusals.md` and each `DR-*` `<remarks>` change with it.

- [ ] **RP15 — A rule id cited as a bare literal is checked by nothing, and one of them resolves to
  nothing.** The plan lint cites fourteen ids as string literals; thirteen are layout rules `rules.md`
  states, and **`WX8` is declared nowhere** — fired by the lint, stated as a rule in
  `docs/world-export/structures.md`, cited in `docs/tools/plan.md`, and absent from `GET /api/rules`.
  `WX9` beside it is never fired at all. Declare `WX8` where `RoomFrameRules` lives or retire it, decide
  what `WX9` is, and add the assertion that runs the other way: every id any gate or lint can emit resolves
  in the catalogue. `RulesEndpointTests` only checks that declared rules carry a sentence.

## Phase 4 — the loop answers for itself

What makes the pipeline drivable without a fifteen-document briefing: a caller asks what it may do next, and
hears a late gate early.

- [ ] **RP16 — The lifecycle is a column nothing reads and 709 lines of prose.** `map.stage` holds
  `plan`/`sketch`/`configure`/`edit`, is written at creation and once at `sketch/finish`, and every other
  read is the dashboard's filter. No endpoint refuses on it and none answers what a map at a stage may be
  asked for — `docs/tools/capabilities.md` answers that question in prose that nothing verifies. Give it a
  transition table, and put the allowed next moves with their routes on `GET /map/{slug}`, so a driver reads
  its affordances instead of learning them. Needs `RP13`: transitions over HTTP handlers have nothing
  to hold.

- [ ] **RP4 — The export's two objective gates are asked at the one route that cannot report them cheaply.**
  `OB17` (a goal overhanging void, in a spawn, in a wool room) and `OB19` (a tree, boulder or building inside
  a goal's clearance) are raised in `MapExportComposer.ComposeSketch`, so a driver first hears either at
  `GET /map/{slug}/export` answering 409. Run 4 hit them three times across four boards.

  **`POST …/sketch/columns` already holds everything both need** — it calls the same
  `SketchWorldBuilder.Build(layoutJson, intent)` and reports `built.Declines`, so the build is paid there and
  the gates are simply not asked. `RefuseGoalClearance` is `DressingScope.GoalClearanceViolations(layout,
  goals)` and needs no world; `RefuseObjectivePlacement(columns, goals)` reads the columns that route
  rasterizes. Ask both there, beside the `DR-*` complaints.

  `OB17` is unpredicted for **one case**, not all: `PlanValidator` runs `ObjectivePlacement.Check` at compile,
  and the silence is the absolutely-placed goal (`B128`), which has no plan ground to judge. `OB19` has no
  earlier answer at all.

  *The author's ruling on what each is at export. `OB17` stays a refusal: it indicts the objective itself,
  there is nothing to drop, and a map in that state is not exportable. `OB19` indicts a prop, and a prop is
  removable, so it becomes a decline like every `DR-*` — the tree, boulder or building drops, the finding
  names it, the map exports. `OB19` leaving the refusal set rewrites `configure.md` §What it refuses and its
  two endpoint rows, `decoration.md:26,173`, `destroyables-and-cores.md:606` and `sketch.md:752` in the same
  commit, and needs `RP30` in it too: without that channel the loudest fault on the surface becomes a line in
  a file inside a zip.*

  *Evidence: `hollowbank` placed a destroyable at `(0, 45)` on a plan piece and cut a sally port through
  that piece in the layout — compile 200, export 409 `OB17`. `alabaster-rake` put a shed run at
  `x 15..24, z 58..62` against a goal anchored `(5, 47)`; the keep-out is a 10-block square about the anchor
  tested against the footprint plus its eaves, and neither cycle was predictable before the build.*

- [ ] **RP30 — The two routes that build the artifact are the two that do not say what they dropped.**
  `POST …/sketch/columns` and `POST /plan/columns` answer every `DR-*` decline under `warnings`, naming the
  rule, the cell and the prop. `GET /map/{slug}/export` writes them to `region/dressing-report.json` **inside
  the zip** — `MapExportEndpoint.cs:90` calls that "the only record an HTTP caller ever gets of a dropped
  prop" — and `GET /map/{slug}/xml` builds the same world through the same pass and reports nothing at all.
  Neither calls `Complaints.Add`, so the middleware's lost-complaint log (`Complaints.cs:157`) never fires
  either, and `refusals.md`'s promise that a non-JSON success "logs it rather than dropping it in silence"
  does not hold on the one route where props actually drop. Hand the declines over on both, and answer them
  in a response header carrying the count and the rule ids, so a caller that never unzips knows to look.

- [ ] **TN5 — Five routes take a posted plan and nothing says what kind of answer each gives.**
  `POST /plan/compile` transforms (a plan → `{layout, intent}`), `evaluate` judges the board against the rule
  law (score + lint), `feasibility` judges the **composer** rather than the map ("could the composer have
  produced this plan" — its own docstring calls the report "a live map of composer gaps"), `inspect` derives
  geometry (distances, wall rects, the canvas overlays) and `columns` projects the world the plan would build.
  Two judgements, one transform, two projections — a real grouping that appears in no document, so a caller
  reads five summaries to learn that only one of them changes anything and only one of them is about the
  generator. Name the kinds in `plan.md`'s endpoint table and in the OpenAPI tag.

  *The author's ruling on `feasibility`: it reports the composer's own limits, so it belongs to the studio
  and not to the authoring loop. Keep the route, mark it a diagnostic, and keep it out of the agent-facing
  surface the way `B249`'s override is kept out — an agent shown a report of what the generator cannot do
  will treat it as a statement about its board.*

## Phase 5 — the names, and what the survey turned up beside them

None of these blocks anything. They are the drift the survey measured, and each is small enough to take while
a phase above is compiling.

- [ ] **WE11 — The world builder is named for the tool whose document happens to reach it.**
  `SketchWorldBuilder` (`Export`) synthesises the voxel world, the world spawn and the resolved intent for
  **every** map — a plan compiles to a layout and arrives here too — and `MapExportComposer.ComposeSketch` is
  the composition every export runs. `SketchTerrainBuilder` (`Minecraft/Stamping`) is the same misnaming one
  layer down. The loop's four names are settled and none of these is one of them: rename to what they build —
  the world — and leave `SketchLayout`, `SketchRasterizer` and the sketch endpoints alone, because those
  genuinely belong to the drawing. **Seven identifiers** across `Export` and `Minecraft` — the types
  `SketchWorldBuilder`, `SketchWorld`, `SketchTerrainBuilder`, `SketchTerrain` and the methods `ComposeSketch`,
  `SketchWorld`, `SketchTerrain` — at 21 sites; the `Pgm`, `Api` and `Client` ones are the tool's and stay.

  *`Compose` is two methods and wants deciding with them: `MapExportComposer.Compose` is the entry point that
  delegates to `ComposeSketch`, and `MapXmlComposer.Compose` is the document leg. Two composers whose main
  method has one name is the same fault a layer up.*

- [ ] **WS5 — Objective suggestion is map-contract analysis living in the world package.**
  `Minecraft/Suggest/` holds `MonumentSuggester` and `CoreSuggester`; the first names a monument, a wool, an
  objective or a core **91 times** over 453 lines, the second 18 over 186. `CLAUDE.md` charters `Minecraft` as
  "the world" and `Analysis` as the derivations over it, and `docs/world-scan/` already owns this subject —
  *monument and objective suggestion* — as a documentation folder. Move both to `Analysis`, where the other
  reads of a scanned world sit, and the package charter stops needing an exception. The consumers are
  `Api/Endpoints/MonumentEndpoints` and the two candidate stores in `Data`, all of which already reach
  `Analysis`.

  **The move is not a file move, and that is the work.** `Analysis` and `Minecraft` are siblings — both
  reference only `Domain` and `Geom` — and the suggesters read `AnvilRegion`, `Nbt`, `Blocks` and
  `MonumentSliceExtractor`, all `Minecraft`. So either `Analysis` gains an edge to `Minecraft`, or the pair
  splits at the seam it already has: the chunk read stays where the chunks are, and what it derives — a
  monument, a core, the confidence behind each — goes to `Analysis`. The second is the one the charter
  describes.

- [ ] **TN4 — The cheapest read of a plan is the one that needs a map row first.** `PlanBoardAscii.Render`
  is reachable through `GET /map/{slug}/plan/ascii` and `GET /plans/{id}/ascii` — both requiring stored
  state — while `compile`, `evaluate`, `inspect`, `feasibility` and `columns` all answer a posted plan with
  nothing stored. The grid is the read that shows a *relation between two rectangles*, which is what the
  other five cannot: `pgm-studio-mapgen`'s own notes name a sixteen-cell bar reached by a four-cell build
  zone as the whole of a 60%-dead landform, "visible at a glance and invisible in the render that was
  actually looked at". Because the render needs a row, the mapgen repo carries `tools/board.py`, a 94-line
  Python reimplementation of it. Add `POST /plan/ascii` beside the other five, so the copy next door has
  nothing left to answer.

- [ ] **RP19 — `tools/relief` is 148 KB of source that generates nothing committed.** Its README calls it
  "the live twin of `docs/world-export/relief.md` — every figure and every number in that document is
  emitted by this tool",
  and `relief.md` carries **no image references at all**; `out/` is gitignored, so nothing it draws is kept.
  Against `CLAUDE.md` § *Investigation stays local* it is neither a gate, nor a generator of a committed
  artifact, nor an operational tool. It also carries its own `Mirror`/`Fold`/`SymmetryError`
  (`Terrain.cs:186,205,219`), which is the third copy of the transform the Traps section says must stay one
  leaf plus the JS twin — and the same folder is on record for having drifted from the shipped solver once.
  Either commit the ten figures it draws so `relief.md` shows them and the tool earns its bar, or delete it
  and keep the measurements the document already states.

- [ ] **RP10 — Sweep the history out of the code comments.** `CLAUDE.md` § *Code comments* states the rule —
  a comment says what the code does and why, in the present tense, and never what it used to do — and the
  tree does not keep it. **23 comments across 20 files** have a state that no longer exists as their subject:
  `RulesEndpoint.cs` ("until now nothing answered…"), `MetaGenerator.cs` ("the studio's boilerplate used to
  say…"), `TeamUnitAllocator.cs` ("it used to be rounded to even…"), `FillProfiles.cs` ("in place of the
  per-kind logic that used to be scattered…"), `StructureFinder.cs`, `HouseStyle.cs`, `HouseStamper.cs` (two),
  `Decorator.cs` (two), `EvaluationDto.cs`, `SketchEndpoints.cs` (two) and nine more. Each one is a
  before-and-after
  where a fact about the present shape would say the same thing shorter. Sweep them the way the port
  attributions were swept: rewrite as the fact, delete where the fact is already stated above it. The grep
  that finds them is `used to |had grown|until now|was (previously|formerly)|no longer (does|did)`, and it is
  worth leaving in the commit message so the next sweep starts from the same list. It needs eyes: it answers
  29 lines in 26 files, and six of those are `used to` meaning *in order to* — a corpus root "used to locate a
  world", a tool "used to pick markers".

- [ ] **RP8 — `project-structure.md`'s census disagrees with the tree it describes.** The size table at §3 is
  a snapshot nothing regenerates, and **every row of it has drifted**. Measured over `src/**/*.cs`
  (plus `.razor` for `Client`, excluding `bin`/`obj`), true against stated: `Geom` 44/5,362 (44/4,967),
  `Domain` 25/2,372 (25/2,252), `Contracts` 25/1,547 (13/965), `Vocabulary` 4/434 (4/393), `Migrations`
  24/1,577 (21/1,469), `Minecraft` 79/15,474 (74/14,307), `Import` 4/471 (4/472), `Pgm` 148/22,716
  (137/20,641), `Analysis` 15/2,831 (16/2,609), `Data` 14/2,436 (14/2,316), `Export` 8/1,629 (7/1,171),
  `Api` 70/9,657 (69/8,675), `Client` 186/23,095 (80/13,436). The folder breakdowns are worse than stale —
  counted at one level where the folders nest, so `Pgm/Compose` reads 42 against 28 direct children, and the
  "48 files are the codec / 85 files and 11,522 lines are the generator" split the §7.1 argument rests on
  cannot be reproduced from either number.

  A table nobody can regenerate is wrong between every pair of commits, so the fix is a counter, not a
  re-count: one script under `tools/` that writes the table (the `envelope-stats` pattern — a generator of a
  committed artifact), and the prose reworded to cite the shape rather than the totals.
