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

**The order was a dependency chain, and three of its four links have drained.** Nothing could be verified
until the surface said what it is, so the contract came first; a gate belonged to whichever door someone put
it behind until there was one place a use case lives, so the application layer came second; and the loop
could not answer for itself until both. The phases keep their numbering so every commit's name still points
somewhere.

**Six entries left**, and none of them blocks another. What remains is one naming question the loop raised
and the drift the survey measured beside it — small work, each entry drivable from itself. A finding made
while working lands in `BACKLOG.md`.

## Phase 4 — the loop answers for itself

What makes the pipeline drivable without a fifteen-document briefing: a caller asks what it may do next, and
hears a late gate early. Both reads ship; one entry is left, and it is about what a route's *summary* says
rather than about what the loop can do.

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
