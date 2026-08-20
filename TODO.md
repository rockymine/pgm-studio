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

**The order below is not a preference, it is a dependency chain.** A shape cannot be declared where no
project can hold it, so the boundary comes first. Nothing can be verified until the surface says what it is,
so the contract comes next. A gate belongs to whichever door someone put it behind until there is one place a
use case lives, so the application layer is third. A caller cannot branch on a fault until the fault has a
class, so the taxonomy is fourth. And a state machine over a pipeline whose steps are still HTTP handlers has
nothing to hold, so the lifecycle is last.

**The board is deliberately larger than the soft cap** — seventeen entries against `CLAUDE.md`'s ~6–12. That
is the author's call and the trade is stated: this is one coherent programme with an order, and splitting it
across two files would hide the order, which is the only part that matters. **Nothing new is added here
until a phase drains.** A finding made while working lands in `BACKLOG.md`.

## Four of the seventeen carry a question only the author can answer

The rest are drivable from the entry plus `CLAUDE.md` — the shape is stated, the evidence is measured, and
the file and line are named. These are not, and each is blocked on a decision rather than on work. `RP13`
is kept in the table with the answer beside it, because the answer is the part an implementer needs.

| Entry | The question |
|---|---|
| `RP13` | *Answered by the author: **its own project**, and an operation is a step of the pipeline — the thing that builds the map, callable from two sides with no dependency on the UI running.* What is left is naming the steps, which the implementer states in the commit. |
| `RP14` | Sign-off on the six categories. The ids do not move, but a category is as stable as an id once a caller branches on it. The `concerns` list is `RP26`'s — its vocabulary is now written down there — and `RP14` ships without waiting for it. |
| `RP15` | What **is** `WX9`? It is stated as a rule in two documents and fired by nothing. Declaring it and retiring it are both one commit, and only the author knows which. |
| `RP16` | The transition table is a product statement, not a derivation. `flow.md` says the flow is one-way — does that mean a built map may never be re-planned, or only that nothing reads back up? |
| `RP19` | Keep `tools/relief`'s ten figures by committing them, or delete the tool. Either is right; which one depends on whether those figures are wanted in `relief.md`. |

One more carries a smaller one, which an implementer may make and state rather than ask: `RP12`, how far to
push declared request shapes in one pass — the write surface is the useful half.

## Phase 1 — say what the surface is

The schema is generated; what it can publish is bounded by what the code declares, which today is a path and
a verb. The first entry is a project split and the rest is mechanical: a shape has to have somewhere to live
before a route can declare it.

- [ ] **RP28 — Shared vocabulary is declared twice, because the leaf three parties can reach does not exist.**
  `Contracts` holds **114 types**, of which exactly **five** are read below `Api` — `MapStage`,
  `MaterialKind`, `RoofForms`, `RoomParts`, `ThemeBuckets`, all vocabulary rather than DTOs. `MaterialKind`'s
  docstring states the requirement: the editor, the HTTP surface and the `style.kind` column must spell it
  identically, and `Contracts` is the one leaf all three reach. `Minecraft` does not, so it re-declares the
  strings — `MaterialRecipes.cs:78-91` (a recipe switch plus `AreaPatterns`, a second enumeration of six
  names beside `MaterialKind`'s fourteen), `TerrainThemeComposer.cs:44-47`, `HouseStyleJson.cs:142,193`.
  `Finding` carries the same requirement and could not get in at all: it lives in `Domain`, so it grew
  `FindingDto`, and the two have drifted.
  Add **`PgmStudio.Vocabulary`**, a leaf referencing nothing, holding `Finding`, `Severity`, `MapStage` and
  the four string vocabularies. Two new edges carry it — `Domain → Vocabulary` and `Contracts → Vocabulary` —
  and every other project inherits it. `FindingDto`, `Finding.Wire()` and `Finding.Envelope` are deleted.
  Read each literal site before citing a count: `PlanModel.Surface` is an int depth, not the bucket.

  *Evidence: `GET /api/map/nope/plan` answers a finding carrying `field`, `subjects` and `cites` as explicit
  nulls beside the record's two computed properties (`subjectIds`, `refuses`), while `Finding.Wire()` one
  layer down writes only the three keys that have a value. A caller can tell which layer refused, which
  `Finding.Envelope`'s own docstring says must never happen.*

- [~] **RP18 — Forty of the seventy-four undeclared operations are hand-built reads.**
  The media types, the refusal envelope, the flat pixel/column encodings, the small acks and the three
  documents an agent writes are declared, and `SchemaCompletenessTests` holds the count so it only goes down.
  What is left here are the reads that build an anonymous object inline at the send —
  `IslandRolesEndpoint.cs:47` is representative — so each needs a record written from the handler that builds
  it, branches included. Thirteen collapse into acks that already exist: eight deletes that answer only that
  the thing is gone, and five writes that answer only that the write happened. The other twenty-seven are
  `scan-summary`, the configure state, `core-suggestions`, `segments`, `regions/tree`, `island-*`, the import
  results and `plan/inspect`, and each is a separate small reading. Blocks nothing and is blocked by nothing.

- [ ] **RP29 — The thirty-four edit routes answer an untyped `Dict`.** Every one ends in
  `WriteSupport.RunEditAsync` (34 call sites over `WriteEndpoints`, `RegionEndpoints`, `WoolAndFilterEndpoints`,
  `SpawnAndRuleEndpoints`, `AuthoringIntentEndpoints`, `WiringEndpoints`), which hands back whatever
  `Dictionary<string, object?>` an editor in `Pgm/Editing` returned, so all 34 publish a path and a verb and
  nothing about the answer. The shapes are few — `{}`, `{id}`, `{team}`, `{wool}`, `{monument}`, `{created}`,
  `{id, bounds}` — so six or seven records cover the surface. Needs `RP28` for somewhere those records can
  live that `Pgm` and the client both reach, and lands with `RP13`, where the answer an operation gives is
  the thing being named.

- [ ] **RP12 — Eighty-seven percent of the surface declares no request shape.** 110 of the 167 endpoints are
  `EndpointWithoutRequest` and **51 call sites read the body as `Dictionary<string, object?>`**. So
  `RequiredFields`, the one global input gate, returns on its first line for all of them: the promise it
  makes holds for 22 routes. The Edit tool's 74 refusal sites in `Pgm/Editing` are a request schema written
  by hand for exactly this reason. Give each write route a request record, bind at the edge, and let the
  hand-written field checks go with it. Needs `RP11` first, which is what makes the shapes checkable.

- [~] **RP11 — Two consumers still keep the contract by hand.** The schema is generated at
  `/api/openapi/v1.json` and browsable at `/api-docs`; nothing reads it yet. The Blazor client writes out
  **152 route strings** and parses **59 responses as `JsonElement`** against 16 typed, across 38 files; the
  endpoint tables in the eight `docs/tools/` documents are typed by hand, and `DocumentedRouteTests` catching
  a drifted row is a test where a derivation belongs.
  Generate a typed client from the document (NSwag's generator is already in the tree as the Swagger
  package's dependency) and render the endpoint tables from it, so the two copies become one derivation.
  `DocumentedBodyTests` posts 8 documented bodies against 93 write routes today and is the natural place to
  assert the tables against the schema.

## Phase 2 — one place a use case lives

One entry, and the phase's whole point: the step that stops a gate from belonging to a door.

- [ ] **RP13 — The use case is the HTTP handler, so a second driver needs a second copy of it.**
  `Api/Endpoints` holds **4,753 lines** against `Api/Services`' 1,169, and `Services/` is read-model
  builders. `SketchFinishEndpoint.HandleAsync` *is* the finish use case — load, gate, rasterize, detect,
  write, advance the stage — and nothing but an HTTP request can reach it, which is why `tools/mapgen` has
  its own. Add an application layer of request-in / `Findings`-out operations, with HTTP, the CLI and tests
  as three adapters over it. The load-or-404 prologue appears **49 times** and becomes one.

## Phase 3 — one class of fault

The cheapest of the five: a change to `Finding` plus a sweep of 71 constants, and the ids do not move.
Additive wherever `Finding` sits, so it neither blocks nor waits on `RP28` moving the record.

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

- [ ] **RP16 — The lifecycle is a column nothing reads and 716 lines of prose.** `map.stage` holds
  `plan`/`sketch`/`configure`/`edit`, is written at creation and once at `sketch/finish`, and every other
  read is the dashboard's filter. No endpoint refuses on it and none answers what a map at a stage may be
  asked for — `docs/tools/capabilities.md` answers that question in prose that nothing verifies. Give it a
  transition table, and put the allowed next moves with their routes on `GET /map/{slug}`, so a driver
  reads its affordances instead of learning them. Needs `RP13`: transitions over HTTP handlers have nothing
  to hold.

- [ ] **RP4 — The export's own objective gates have no pre-flight, and an agent pays a whole build for
  each.** `OB17` (a goal overhanging void, in a spawn, in a wool room) and `OB19` (a tree, boulder or
  building inside a goal's clearance) are refusals raised by `MapExportComposer` over the rasterized ground,
  so the first time a driver hears one is `GET /map/{slug}/export` answering 409 — after the world has been
  built. Run 4 hit them three times across four boards; nothing in `/plan/evaluate`, `/plan/compile` or
  `sketch/columns` predicts either, and the compile gate is deliberately silent about an absolutely-placed
  goal because it has no ground truth to judge against. The ground exists as soon as `sketch/finish` has
  run: answer both over it, on the read an agent already makes (`POST …/sketch/columns`, beside the `DR-*`
  complaints), as complaints there and refusals where they are now.

  *Evidence: `hollowbank` placed a destroyable at `(0, 45)` on a plan piece and cut a sally port through
  that piece in the layout — compile 200, export 409 `OB17`. `alabaster-rake` put a shed run at
  `x 15..24, z 58..62` against a goal anchored `(5, 47)`; the keep-out is a 10-block square about the anchor
  tested against the footprint plus its eaves, and neither cycle was predictable before the build.*

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
  the composition every export runs, while the method called `Compose` is the doc-assembly leg in front of it.
  `SketchTerrainBuilder` (`Minecraft/Stamping`) is the same misnaming one layer down. The loop's four names
  are settled and none of these three is one of them: rename to what they build — the world — and leave
  `SketchLayout`, `SketchRasterizer` and the sketch endpoints alone, because those genuinely belong to the
  drawing. 12 identifiers across `Export` and `Minecraft`; the `Pgm`, `Api` and `Client` ones are the tool's
  and stay.

- [ ] **WS5 — Objective suggestion is map-contract analysis living in the world package.**
  `Minecraft/Suggest/` holds `MonumentSuggester` and `CoreSuggester`; the first names a monument, a wool, an
  objective or a core **44 times**. `CLAUDE.md` charters `Minecraft` as "the world" and `Analysis` as the
  derivations over it, and `docs/world-scan/` already owns this subject — *monument and objective suggestion*
  — as a documentation folder. Move both to `Analysis`, where the other reads of a scanned world sit, and the
  package charter stops needing an exception. Its consumers are `Api/Endpoints/MonumentEndpoints` and the two
  candidate stores in `Data`, all of which already reference `Analysis`.

- [ ] **TN4 — The cheapest read of a plan is the one that needs a map row first.** `PlanBoardAscii.Render`
  is reachable through `GET /map/{slug}/plan/ascii` and `GET /plans/{id}/ascii` — both requiring stored
  state — while `compile`, `evaluate`, `inspect`, `feasibility` and `columns` all answer a posted plan with
  nothing stored. The grid is the read that shows a *relation between two rectangles*, which is what the
  other five cannot: `pgm-studio-mapgen`'s own notes name a sixteen-cell bar reached by a four-cell build
  zone as the whole of a 60%-dead landform, "visible at a glance and invisible in the render that was
  actually looked at". Because the render needs a row, `tools/board.py` is a 94-line Python reimplementation
  of it. Add `POST /plan/ascii` beside the other five and delete the third copy.

- [ ] **RP19 — `tools/relief` is 120 KB that generates nothing committed.** Its README calls it "the live twin
  of `docs/world-export/relief.md` — every figure and every number in that document is emitted by this tool",
  and `relief.md` carries **no image references at all**; `out/` is gitignored, so nothing it draws is kept.
  Against `CLAUDE.md` § *Investigation stays local* it is neither a gate, nor a generator of a committed
  artifact, nor an operational tool. It also carries its own `Mirror`/`Fold`/`SymmetryError`
  (`Terrain.cs:186,205,219`), which is the third copy of the transform the Traps section says must stay one
  leaf plus the JS twin — and the same folder is on record for having drifted from the shipped solver once.
  Either commit the ten figures it draws so `relief.md` shows them and the tool earns its bar, or delete it
  and keep the measurements the document already states.

- [ ] **RP10 — Sweep the history out of the code comments.** `CLAUDE.md` § *Code comments* states the rule —
  a comment says what the code does and why, in the present tense, and never what it used to do — and the
  tree does not keep it. **18 files** carry a docstring or inline comment whose subject is a state that no
  longer exists: `RulesEndpoint.cs:16` ("until now nothing answered…"), `MetaGenerator.cs:12` ("the studio's
  boilerplate used to say…"), `TeamUnitAllocator.cs:56` ("it used to be rounded to even…"),
  `FillProfiles.cs:8` ("in place of the per-kind logic that used to be scattered…"),
  `StructureFinder.cs:24`, `HouseStyle.cs:444`, `HouseStamper.cs:17,118`, `Decorator.cs:75`,
  `EvaluationDto.cs:42`, `SketchEndpoints.cs:243` and the rest of the 18. Each one is a before-and-after
  where a fact about the present shape would say the same thing shorter. Sweep them the way the port
  attributions were swept: rewrite as the fact, delete where the fact is already stated above it. The grep
  that finds them is `used to |had grown|until now|was (previously|formerly)|no longer (does|did)`, and it is
  worth leaving in the commit message so the next sweep starts from the same list.

- [ ] **RP8 — `project-structure.md`'s census disagrees with the tree it describes.** The size table at
  §"Project sizes" is a snapshot nothing regenerates, and every row of it has drifted: measured over
  `src/**/*.cs` (excluding `bin`/`obj`) the true counts are `Geom` 44/5,362 (stated 44/4,967), `Domain`
  27/2,515 (25/2,252), `Contracts` 15/1,026 (13/965), `Migrations` 22/1,513 (21/1,469), `Minecraft`
  79/15,456 (74/14,307), `Pgm` 148/22,838 (137/20,641), `Analysis` 17/3,035 (16/2,609), `Export` 8/1,575
  (7/1,171), `Api` 67/9,595 (69/8,675) and `Client` 82 (80). The folder breakdowns are worse than stale —
  they are counted at one level where the folders nest, so `Pgm/Compose` reads 42 against 28 direct children,
  and the "48 files are the codec / 85 files and 11,522 lines are the generator" split the §7.1 argument
  rests on cannot be reproduced from either number. A table nobody can regenerate is a table that is wrong
  between every pair of commits, so the fix is a counter, not a re-count: one script under `tools/` that
  writes the table (the `envelope-stats` pattern — a generator of a committed artifact), and the prose
  reworded to cite the shape rather than the totals.
