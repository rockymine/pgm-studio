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
fault has a class, so the taxonomy is third — **that phase has drained**, and its numbering is kept so the
two behind it keep the names every commit cites. And a state machine over a pipeline whose steps are still
HTTP handlers has nothing to hold, so the lifecycle is last.

**The board is deliberately larger than the soft cap** — ten entries against `CLAUDE.md`'s ~6–12. That
is the author's call and the trade is stated: this is one coherent programme with an order, and splitting it
across two files would hide the order, which is the only part that matters. **Nothing new is added here
until a phase drains.** A finding made while working lands in `BACKLOG.md`.

## Two of the ten carry a question the author has now answered

The rest are drivable from the entry plus `CLAUDE.md` — the shape is stated, the evidence is measured, and
the file and line are named. These two were blocked on a decision rather than on work; the ruling is in the
row, and the entry below builds to it.

| Entry | The question, and the ruling |
|---|---|
| `RP32` | **May a read pay for a build? No.** `GET /map/{slug}/findings` answers every gate it can reach from the stored documents, in milliseconds, and **names the gates it did not ask and why** — the export gates (`OB17`, `EX1`) need the rasterized world, which is seconds a `GET` would spend on every call. Nothing is lost by not paying it: those gates are already answered where the build is paid for, which is what `RP4` and `RP30` settled. A response that is silent about what it skipped would be the failure; one that names it is a complete answer to a bounded question. |
| `RP16` | **A stage is a progress marker, not a lock.** `flow.md`'s one-way flow means nothing reads back up — a later level never writes into an earlier one — not that a built map may never be re-planned. So the transition table names the forward moves as affordances and no endpoint grows a refusal on `map.stage`. |


## Phase 1 — say what the surface is

Every operation declares what it answers, what it takes and which refusals are its own; the endpoint tables
are held to all three, and every route the client calls is held to the schema. One entry is left: the
difference between a declared shape and a bound one.

- [ ] **RP40 — Bind the shapes the surface now declares.** The 42 declared bodies are read by hand behind
  the declaration, so `RequiredFields` runs on only the 22 routes that bind one, and the **15**
  `EditException.Unreadable` throws in `Pgm/Editing` stand where a binding would have refused. Binding is not
  a sweep: an update body needs absent-versus-null, which a bound record loses unless every field is
  optional, and a region create is a union over `type` that no one record expresses. So take the routes where
  a binding genuinely refuses something — a missing `region_id`, a `yaw` that is not a number, a
  `max_players` that is not an integer — and leave the rest declared. The other 38 refusal sites in
  `Pgm/Editing` read the map the edit lands on and stay whatever happens here.

## Phase 2 — one place a use case lives

One entry: the ten steps that still belong to the door they are reached through.

- [~] **RP13 — Ten use cases still live behind the door they are reached through.** A use case here is a
  handler that **reads stored state, does work, and writes it back** — the shape the three already in
  `Api/Services` have, which answer `Findings` and let the layer above render the envelope
  (`MapExportLoader`, `SketchFinish`, `MapFromDocuments`). Move these ten there, unchanged in behaviour, and
  fold the load-or-404 prologue — **37 occurrences, word for word** — into one:

  | Route | Class |
  |---|---|
  | `POST /sketch` | `SketchCreateEndpoint` |
  | `PUT /map/{slug}/plan` | `MapPlanPutEndpoint` |
  | `PUT /map/{slug}/sketch` | `SketchPutEndpoint` |
  | `PUT /map/{slug}/sketch/from-plan` | `SketchFromPlanEndpoint` |
  | `DELETE /map/{slug}/sketch/discard-if-empty` | `SketchDiscardIfEmptyEndpoint` |
  | `PUT /map/{slug}/intent` | `IntentPutEndpoint` |
  | `PUT /map/{slug}/intent/from-plan` | `IntentFromPlanEndpoint` |
  | `PATCH /map/{slug}/metadata` | `MetadataEndpoint` |
  | `PATCH /map/{slug}/symmetry` | `SymmetryPatchEndpoint` |
  | `POST /map/import-folder` | `ImportFolderEndpoint` |

  **The cheapest of them is already an operation, misfiled.** `IntentWrite.StoreAndProjectAsync`
  (`Endpoints/AuthoringIntentEndpoints.cs:37`) *is* the two intent writes, in the shape a service wants;
  moving it moves two rows at once. `WriteSupport.RunEditAsync` (`Endpoints/WriteEndpoints.cs:17`) is the same
  story one layer over — **23 call sites** across the region, spawn, wool and write endpoints — and is why
  none of the Edit tool's routes is on the list: they are one path already, in the wrong folder. It belongs in
  `Api/Services` with the rest, and moving it changes no behaviour at all.

  **`Api/Services` is the place, not a project of its own.** The second adapter a project was for does not
  exist: `tools/mapgen` is deleted, and the driver that replaced it — `drive.py` in the mapgen repo — is a
  Python HTTP client that cannot consume a .NET assembly at all. That a .NET CLI can already reach these
  operations is settled by `tools/seed-library.cs`, which references `PgmStudio.Api` and calls
  `Api.Services.LibrarySeed` directly. A new project would buy separation, not a consumer.

  *Re-derive the list with: an endpoint class that both loads state (`GetBySlugAsync`, `artifacts.Load*`,
  `ReadDocAsync`) and writes it (`Writes.StoreAsync`, `artifacts.StoreAsync`, `.UpdateAsync`,
  `MapAuthors.ReplaceAsync`), minus anything reaching `WriteSupport.RunEditAsync`.*

## Phase 4 — the loop answers for itself

What makes the pipeline drivable without a fifteen-document briefing: a caller asks what it may do next, and
hears a late gate early. `RP32` is what is left of that here: the instances are fixed, and the general form
— a caller asking what is wrong outright rather than hearing it one route at a time — is not.

- [ ] **RP32 — Nothing answers "what is wrong with this map right now".** Every gate is reachable only
  through the step it lives behind, so a fault authored at one step is heard at another; `RP4` and `RP30`
  were two instances of that shape, each fixed one route at a time, and this is the shape itself. A driver's loop is *act and hope the next call
  mentions it* rather than *act, then ask*. Add `GET /map/{slug}/findings`: every gate answerable at the map's
  current stage, as one `Findings` list carrying severity, so no route has to remember to report. It pairs
  with `RP16` on `GET /map/{slug}` — that answers what may be done next, this what is wrong now. It must
  **call** the gates rather than restate them, since a summary that re-implements one is a second copy free
  to disagree with it — but that needs no new layer: every gate it would ask (`SketchLayoutCheck`, the plan
  validator, the house-style checks) lives below `Api` and is reachable from an endpoint today, which is what
  `MapExportLoader` already does for the export's.

  *The one answer it needs first is in the table above: whether a read may pay for a build. Which gates a
  map is asked at which stage follows from it — a stage only decides which documents exist to ask of.*

- [ ] **RP16 — The lifecycle is a column nothing reads and 709 lines of prose.** `map.stage` holds
  `plan`/`sketch`/`configure`/`edit`, is written at creation and once at `sketch/finish`, and every other
  read is the dashboard's filter. No endpoint refuses on it and none answers what a map at a stage may be
  asked for — `docs/tools/capabilities.md` answers that question in prose that nothing verifies. Give it a
  transition table, and put the allowed next moves with their routes on `GET /map/{slug}`, so a driver reads
  its affordances instead of learning them. Needs `RP13`: transitions over HTTP handlers have nothing
  to hold.

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
