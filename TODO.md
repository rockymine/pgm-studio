# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
When this board drains, pull the next theme up from `BACKLOG.md`. Board rules live in `CLAUDE.md`
(§ "Status & task board").

Task ids are a section letter + number (`S13`, `B10`, `G15`) — **globally unique and stable** across all
three files. Moving a task between files never changes its id; never renumber or reuse.

## The focus: a map an agent can author, and that a player can win

Sixteen maps were designed by an agent driving the system end to end, and every one of them was built. That
is the result worth reading first — the machinery reaches from a prose description to a loadable world with
no human in the middle. What the boards then showed is where the reach is thinner than it looks:
**`docs/tools/mapgen-review.md`** is the measured record of it, thirty-four `MG` entries in pipeline order, and
**`docs/tools/capabilities.md`** is the map of the documents the tool should have been written against.

The entries stay in `docs/tools/mapgen-review.md` as evidence, the way `docs/generator/audit.md` holds the generator's; an
entry **leaves it when its fix lands**. What is promoted onto the board below is ordered by the review's own
severity rather than by the pipeline.

**The eight that broke a map have shipped**, along with the platform and the marker a goal wants, a picture
per stage and the handbook (`FEATURES.md`; `mapgen-review.md` records which entry each closed). What is left below
is the residue that work turned up, and the shape of it is worth noticing: every one was found by a fix
rather than by the original review. A refusal caught three shipped specs whose cores stand in void. A stage
image turned out to answer a different question from the one its name owns, and to hand a reader a wrong
answer through an unlabelled colour. A density threshold turned out to be stated in the wrong unit. And a
building turned out to be the one composition the system cannot express.

What is deliberately **not** here: the design entries (a destroy board composed for destroy topology, a
forest placed rather than scattered, per-shape paint, houses that differ from each other). They are the
difference between a rough map and a good one, they are real, and they are a second wave — `mapgen-review.md`
holds them until one becomes the focus.

## Backend, pipeline & internals (B / P / A)

**These three are the current run, in order.** They come out of one finding, and the finding is worth stating
once because every entry below inherits it: **`tools/` grew a second copy of the system.** A two-day
experiment in whether an agent could drive the studio produced a CLI that reimplemented the parts it could
not reach — its own goal-over-void refusal, its own prop clearance, its own forest and village samplers, its
own reduced document format — and the board began treating that CLI as product, filing bugs against it and
listing its refusals in `FEATURES.md`. A tool is allowed to *drive* the system. It is not allowed to *be* a
second one, because the second one is what rots: it has no tests, no document that governs it, and it drifts
behind the thing it copies without anyone seeing (the relief fork sat 1614 cells off a settled solve for
exactly that reason). `B119` already moved the boundary so the copying stops being necessary — the export
path is `PgmStudio.Export` now, not a folder inside the web app — which is what makes `B118` cheap.

**Take them in the order listed.** `B118` undoes what was copied. `B128` is the one entry here that is not
about the second system at all: it is an authoring defect the boards exposed, and it comes before `B120`
because every destroy map that run authors wants a goal at a chosen height and would otherwise reproduce the
workaround once per map. Then `B120` finds out whether the result actually answers.

- [ ] **B118 — `MapSpec` is a smaller system wearing the big one's clothes, and two of its knobs are
  actively harmful.** The spec format was invented rather than derived: it names a handful of fields and
  hides everything the four real documents can say, so a shape became a footprint, a theme became four family
  names, and all sixteen boards came out with a rim, one theme, one relief style and the same wall. This is
  `mapgen-review.md` MG29, and its cost is measured there — a model given the spec reported five of six brief
  requirements as impossible, two of which the README it was quoting from documents.

  **Delete `trees`, `village` and `houses` outright**, with `Forest`, `Settle`, `Placed`, `KeepOut`, `Level`,
  `Clear` and `FannedAround` in `Program.cs`. They are a site sampler, and the studio deliberately has none:
  `sketch.md` states that dressing is authored and that there is no scatter, no density pass and no "fill
  this island with forest", because a tree is cover and where cover stands is a gameplay decision. The
  sampler contradicted the shipped design, MG9 files it as a fault, and it is what buried every generated
  board under a canopy nobody chose.

  **What replaces the format is a thin addressing layer**, which is MG29's own prescription: `plan`, `layout`
  and `intent` handed through **verbatim** as the real document types, with the convenience fields kept only
  as shorthand that expands into them rather than as the whole vocabulary. A spec must be able to say a
  `SketchShape` with its own theme, floor, base height, anchor heights and `relief_scope`; a `TerrainTheme`
  with its rim band and per-shape scope; a relief mark of any of the five kinds. Anything it cannot say is
  then a gap in the system, which is reportable, rather than a gap in the format, which teaches an author
  that the system cannot do it.

  `Retarget` goes too. Rewriting a capture board's wool markers into monuments is MG1, the largest entry in
  the review: a destroyable needs no room, no lane and no protection region, so a retargeted wool puts every
  goal at the back of a corridor it never wanted. A destroy board is authored as a plan, not converted.

- [ ] **B128 — A goal's height is authored by manufacturing a plan tier to carry it, and the landform then
  exists twice.** A destroyable or a core states which piece it rides and where on it —
  `DestroyablePlacement` and `CorePlacement` carry `{ id, piece, at, style, materials, float, name }` and **no
  height field of any kind** — and `PlanCompiler` resolves the anchor as `new Pt(px, piece.Surface, pz)`
  (lines 290 and 308; spawns and wool rooms take the same treatment at 205 and 260). So the only way to put a
  goal high or low is to author a plan piece standing at that surface, whose purpose is not to be ground but
  to be something for a marker to ride.

  **The cost is visible on the one board that was authored this way.** Ashen Quarry's mesa was authored
  correctly, as a `raise` shape tilted by per-vertex `anchor_heights`; to put the second destroyable on it the
  mesa had to be pushed **back down into the plan** as a tier at surface 58 and then promoted to a polygon
  again to recover the outline it already had. Its quarry is a plan tier at 24 for the same reason. Any
  landform carrying an objective is therefore authored twice — once as a plan rectangle for the marker, once
  as a polygon for the shape it actually is — in two idioms, in two files, with nothing checking that the two
  agree. That is also where the void column came from: two boundaries that had to meet, drawn independently.

  **There is no second height field, and `float` is already the right concept measured from the wrong
  thing.** `float` is the air gap under the structure — `ObjectiveDefaults.DestroyableFloat`, four — and a
  destroyable and a core float above the terrain **by design** (`approaches.md`, `[author]`). That is an
  offset over ground, which is exactly what a goal's height wants to be. What is wrong is the ground it
  counts from: `piece.Surface`, the plan's flat nominal world. So the fix is not a new knob beside it but the
  same knob measured from the **solved terrain** under the marker's column, and a second height concept would
  be the "second accepted format" `CLAUDE.md` forbids.

  **The offset resolves against the ground as built, and it is configurable in both directions** — a goal can
  stand higher or closer to the ground than the default. An offset is the right reading rather than an
  absolute world Y because it survives a relief pass moving the ground under it, and because it is what an
  author means by "the goal sits on the mesa". The absolute reading is the one that has already cost a build:
  a relief mark's `h` is absolute, was read as a lift, and put terrain at y4 on a board based at 41 while the
  export succeeded and the gate passed.

  **What this buys is that the landform is authored once.** With the offset counted from solved ground, a
  marker's x/z on an authored polygon is enough — the mesa stays a `raise` shape with its per-vertex anchors,
  the goal on it rides whatever height the relief left, and no plan tier has to be manufactured to carry it.

  The default stays in the band it is in — around five, and **four today**. That number is a gameplay
  constant rather than an implementation detail, so it is not to be nudged while the offset's meaning is being
  corrected; whether it moves from 4 to 5 is the author's to settle and is not part of this task.

  This is the authoring half of a defect the board already carries two other halves of, and it is filed apart
  from both because neither would surface it: `B105` is the correctness half — the compiler must stop reading
  a piece's `Surface` as a literal world Y for these four markers, since that is the flat-nominal mistake
  wearing a different field — and `B107` is the canvas half, where a destroy objective's sketch presence is a
  movable point with a stated height. What is missing between them is the **document** half: a field an author
  or an agent can write, so that a landform carrying an objective is authored once, as the shape it is.

- [ ] **B120 — Run the trial again, and find out whether the system now answers.** The point of the three
  entries above is that an agent can author a map by driving the real documents. That claim is untested. Take
  the brief `mapgen-review.md` already uses — *a destroy board, one connected island, the monument in the
  open with a forest closing the west flank, a hill east that attackers can bridge from, a village behind, a
  void channel twenty blocks in front* — and author it, stage by stage, reviewing each stage before the next
  is laid on it.

  **What the run has to produce besides a map** is the honest list of what could not be said and why. That is
  the actual deliverable: a capability that is missing is worth more written down than worked around.

  **The rules the run is held to.** They exist because breaking each one is what produced the mess this run
  is cleaning up:

  - **No capability is added in `tools/`.** If the run needs something the system cannot do, it is built in
    `src/` where the studio and every driver get it, or it is filed and the map is authored without it. A
    tool may compose, drive and report; a refusal, a placement rule, a sampler or a validation that lives in
    a tool is the exact defect `B116` undid and `B118` is undoing.
  - **No second format.** The run authors `PlanModel`, `SketchLayout` and `MapIntent` as they are. A
    convenience wrapper is allowed only where it expands into those documents and can be shown to.
  - **Nothing is scattered.** Every prop is placed because there is an answer to "why here". A run that
    cannot answer it leaves the ground bare and says so.
  - **Layers are not used.** The ground layer only, per `sketch.md`.
  - **Every stage is looked at before the next consumes it.** The preview endpoints answer a theme, a
    material, a prop or a plan without building a world; the round-trip harness reads a built world back as a
    heightmap, a contour, a surface, a traversability map. MG30 is fifteen boards judged from one top-down at
    the end, and every appearance fault in the review was visible in an image nobody rendered. An image
    answers *whether* something came out and never *what* it is — the plan render colours by role, so its
    blue is a build zone and never water (`B95`).
  - **A question about how a map plays is asked, not derived.** `docs/gameplay/approaches.md` is the
    document, and every claim in it is now marked `[author]` and settled, so it is law rather than advice.
    Inventing a gameplay conclusion from a correct measurement is what produced a filed, committed, wrong
    claim that every generated destroy map was unwinnable.
  - **A document that describes something unbuilt names its task id, or says nothing.**

- [ ] **B104 — A destroy goal is stamped above the build cap.** On `duskfell` the gold destroyable stands at
  y21–23 and `max_build_height` is 20; on `corvale` the emerald stands at y18–20 against the same cap. Blocks
  above the cap can still be broken, so this does not make the goal unbreakable — but a destroyable or a core
  belongs **below** the cap, and neither does. The cap itself is the cause rather than the placement: it is
  `plan.Globals.Surface + Headroom`, both halves of the plan's flat nominal world, so it is computed from a
  ground level the relief later abandons and lands under the terrain it is supposed to sit over. `B105` is the
  fix; what this entry owns is the check that the goal ends up under whatever cap that produces.

  **A floating goal is not the fault, and an earlier version of this entry said it was.** A destroyable and a
  core **float a few blocks above the terrain by design**, and have since PGM's beginning: a core that sits on
  the ground cannot leak, so attackers would have to mine the terrain out from under it first, and a
  destroyable flat on the ground is trivially covered and hidden. The four-block gap measured under
  `duskfell`'s goal is therefore correct behaviour, not a defect, and the same gap in the pre-existing build
  is correct too. What a goal needs beneath it is **terrain, somewhere below** — which is what `B82` already
  checks and checks correctly. The earlier claim that `B82` should compare the goal's height against the
  ground's was wrong and is withdrawn.

- [ ] **B105 — Retire `headroom`; a plan states a build ceiling, it does not derive one.** `PlanGlobals`
  carries `Headroom` (board-wide, default 11 — not per piece, despite the field reading like one) and
  `PlanCompiler` turns it into the map's only build cap with `plan.Globals.Surface + plan.Globals.Headroom`.
  Both halves of that sum are the plan's **flat nominal** world, so the cap is computed from a ground level
  the relief then abandons — which is the root `B104` names, and it produced boards whose ceiling sits below
  their own terrain. Derivation is the wrong shape here regardless of the numbers: a build ceiling is a
  decision about how high a player may build, and it should be **stated**, not inferred from a base plus a
  slack. `MapIntent`'s `BuildIntent.MaxHeight` is already the real field and the export already honours it;
  what is missing is a plan-level value that sets it, and an author or agent knowing it exists.

  So: remove `Headroom` from `PlanGlobals` and everything reading it, add a stated maximum build height in
  its place, and keep **per-piece `Surface`** exactly as it is — that one is load-bearing and correct as a
  plan-space concept. Two things travel with it. The compiler must stop reading a piece's `Surface` as a
  literal world Y for spawns, wool rooms, destroyables and cores (`PlanCompiler` lines 205, 260, 290, 308),
  because that is the same flat-world mistake wearing a different field; the anchor wants resolving against
  the ground as built. And the ceiling wants a sane relationship to the finished terrain rather than to the
  plan's base, since a map whose highest ground is y20 and whose cap is y20 permits no building at all.

- [ ] **B106 — A destroy goal may stand anywhere, and three documents say otherwise.** A destroyable or a
  core can be placed on **any piece of a plan** — a field, a plateau, a frontline, anywhere ground exists. It
  needs no room, no dead-end lane and no protection region. A wool needs all three, because a wool is a thing
  an enemy must reach and carry back, so it sits at the far end of a lane inset about five, walled, entered
  from one side. The two are not the same slot and never were.

  `Retarget` nonetheless reuses the wool markers, and — the part that actually matters — **the tool's own
  documentation states the conflation as a principle** in three places: the README's "a wool room, a monument
  and a core occupy the same slot in a board", the same sentence in `MapSpec`'s `objective_mode` docstring,
  and again in `Program.cs`. Every agent that has authored a destroy board read one of those and put the goal
  in a cage. Those sentences are corrected; the code behind them is not. The work is to let a spec place a
  destroy goal where the design wants it rather than where a wool budget put it, which is `MG1`'s corpus
  reading arriving as a placement rather than as a whole second composer.

  One naming problem sits underneath and is worth fixing while here, because it is the likeliest reason the
  conflation felt right: **two different things in this codebase are called protection.** One is the XML
  region rule that stops a player entering a spawn or a wool room and restricts what may be broken or placed
  inside it — a gameplay contract. The other is `Decorator.IsProtected`, "cells nothing may be placed on",
  which is a dressing keep-out and has no gameplay meaning at all. A goal that needs the second does not need
  the first, and one word for both invites exactly the inference that a destroyable must live somewhere
  protected.

- [~] **B107 — The sketch still cannot place or move an objective; only its height sticks.** The storage
  question is settled and the backend half is landed (`FEATURES.md`): a structural shape's stated height now
  survives a recompile, marked per field and carried by `intentRef`. What remains is the reach.

  **The canvas half.** `sketch-canvas.js` documents structural pieces as render-only — never hit-tested,
  never selected, never edited — so nothing can write the flag a user's correction would set. Unlocking
  selection, a drag, and an inspector row for the stated height is its own slice of the canvas and render
  layers, and it is what turns a proven mechanism into something an author can reach.

  **The destroy objectives.** A destroyable and a core carry **no rect in the plan** — `Anchor` is a bare
  point, unlike a spawn or a wool room — and that is correct rather than missing: neither has a footprint, and
  neither wants one. They sit anywhere terrain exists beneath them, floating a few blocks clear of it. So a
  sketch presence for them is a **movable point with a stated height**, not a rect to drag, and the height is
  the interesting half because it is the one thing the plan cannot know before the relief runs.

  **Position, separately.** Moving a piece rather than raising it is `S25b`, and the design here deliberately
  leaves rect and position tracking the plan so that a recompile stays authoritative about *where* while the
  author stays authoritative about *how high*.

- [ ] **B109 — Nothing checks a plan before it costs a build.** Authoring a plan by hand is arithmetic over
  rectangles in cells, and the repository offers no way to ask whether the arithmetic worked short of running
  the whole pipeline. Two pieces that overlap, a land interface too narrow to connect, a stray corner touch —
  none of these is reported until a world has been built. An author writing two boards by hand had to
  re-implement `ContactGraph.Classify` in a throwaway script to check adjacency before spending a build cycle,
  which shortened iteration enough to be worth the detour and is a tool the repository should have.

  `PlanValidator` already answers most of this with rule-id findings, and it is the piece that is closest to
  free: **`tools/mapgen` never calls it**, not even as a warning before building, and nothing documents how to
  invoke it standalone — reaching it meant reading `PlanValidator.cs` and writing a wrapper. Wiring it into
  the tool ahead of the build, and giving the geometry checks it does not cover a home beside it, turns a
  build cycle into a message. The same findings are what an agent needs, since they name rules rather than
  describing symptoms.

- [~] **B111 — The deletions.**
  The set is complete: `docs/tools/plan.md`, `sketch.md`, `library.md`, `generator.md`, `shapes.md`,
  `configure.md` and `edit.md`, all to one shape — *what it is · what it writes · the document model, field by
  field · what it compiles to · the phases and their steps · what it refuses · the API as an endpoint table
  with failure codes · driving it without the UI · limits*. Two of those sections are conditional — a tool
  with no gate needs no refusals section, a tool with no document of its own needs no model section — and the
  rest are the spine. Written from the code in the present tense, and usable as agent input, which is what
  puts the endpoints in them. `flow.md` is the eighth and the entry point: the four levels a map is described
  at, which tool works at which, the five hand-offs and their merge rules, and pointers out. It describes only
  the flow — no tool's own content is restated in it. **Author review pending.**

  **A tool that authors nothing bends the spine rather than breaking it.** The generator has no document to
  edit and no phases: its model section is the *request* (four numbers and a seed), its compile section is
  *what a compose produces*, and its phase section is the single browse workspace. Where a tool is
  statistical rather than authored, the description has to be **measured** — `generator.md` carries a
  400-board-per-row census of what each player count actually produces, taken from the endpoint itself, because
  prose about sampling weights cannot say whether a request makes rings.

  **Every JSON shape gets a worked example, and the examples are checked by being run.** Each is extracted from
  the document itself and posted to the live API — a plan that compiles clean, a layout that solves its relief
  and paints, every material kind rendered, the seeded house compared against what the endpoint returns. A
  document an agent authors from is wrong if its examples do not run, and only running them says they do.

  `docs/tools/capabilities.md` keeps the half `flow.md` deliberately leaves it: the **capability** reference —
  what the system can be asked for at each stage — which is a different question from how a map moves between
  the tools. `flow.md` points at it rather than absorbing it. The gameplay claims in it are the author's and
  settled, `approaches.md` having been read back in full.

  Then the deletions, which are the point of the exercise and wait until the set is complete: a document goes
  when a tool document owns its subject, which retires the plan, sketch and configure contract records but
  keeps the corpus measurements, `docs/generator/`'s eight and the world-export set. **The generator set is
  settled and its ninth file is gone.** `generator.md` and `shapes.md` own the two *surfaces* — browse and
  catalog — which `docs/generator/` never covered, and they defer the model to `model.md` rather than
  restating it, so none of the eight is retired by them. `wool-approach-read.md` is deleted: every id it
  turned on has shipped or been retired, and what it argued for now stands as a plain rule in `model.md` §4.7
  — the studio does not classify finished maps, because real maps differ too much. `audit.md` stays, with its
  HB4/FR6 entry corrected: the wide frontline it recorded as unreachable is measurably the only outcome a
  branch hub with a frontline produces.

  **The world-export set is the detail behind the sketch, and now says so both ways.** `sketch-relief.md`
  belonged with it rather than in `contracts/` — the pass that decides the ground the painter, the stampers
  and the dressing pass all land on — so it is `world-export/relief.md`, rewritten to what shipped. And
  `tools/sketch.md` cites the five of them from the phase that feeds each, which it did not before: a reader
  wanting the elevation solver or the painter's bucket rules had nothing saying those documents existed. Two
  documents are deleted outright: `sketch-creation-flow.md`, for naming four files and a route that are gone,
  and `finishing-model.md`, whose §1 and §2 describe theming as a plan-side concern and then catalogue what
  that arrangement breaks — a system that no longer exists and failures that can no longer occur. Its rationale
  is kept where it is load-bearing: why the finish belongs on the sketch in `flow.md`, the two stamp concepts
  in `structures.md`.

  **`docs/contracts/` is gone, and its name was the finding.** Five of its eighteen documents were contracts;
  the rest were a rationale record, corpus studies, PGM law, a UI build plan, a URL decision and a design — a
  folder named for a form most of its contents did not have, which is why the census had to group by subject
  to say anything. Every folder under `docs/` is now named for a subject: **`pgm/`** (the map contract),
  **`world-scan/`** (what the studio reads out of a world, the mirror of `world-export/`, which writes one),
  **`client/`**, **`gameplay/`**, with `project-structure.md` at the root beside the other whole-repo notes.
  `CLAUDE.md` carries the map. Pure relocation — every citation followed, no prose rewritten — with the four
  documents that need content work filed as **B112**–**B115** rather than fixed in the same pass.
  `docs/doc-status.md` §2 says what is duplicated and §5 which tools are unserved; its churn ranking (§3.4)
  **wants re-running against the full history**, since the container that produced it saw 197 commits over
  three days and cannot see drift older than that.

- [ ] **B92 — A building can be a solid volume behind its own facade.** `HouseStamper` raises walls, a roof
  and their openings, and the volume they enclose is left as air — "fill" appears in the house model only as a
  wall's infill between posts and as the gable's, never as the interior. That makes every building somewhere
  to walk into, which is right for a village and wrong for the two things a building is also good for: a
  **scenery building inside the map that is not enterable**, and a **run of buildings sealing the edge of the
  board**, which is how scenery does the work of a boundary — and the only way it can do that work at all in a
  mode where nothing may be placed.

  **The facade is kept, and that is the whole trick.** A filled building is not a solid block wearing a
  house's outline: its windows and its door stay exactly where they were, because they are what makes it read
  as a building rather than as a lump, and the fill sits **behind** them. The idiom is a dark fill — black
  wool being the obvious one — so a window reads as an unlit interior rather than as a hole into rock, which
  is a house with its lights off and is what an eye expects at the edge of a map. So the fill material is a
  knob rather than a constant, and the openings are untouched by it.

  It wants to be a `HouseStyle` field rather than a stamper flag, so a style carries whether it is a place or
  a mass. What still has to be settled: whether the fill respects the storey stack, since a building filled to
  its top course and one filled only to its first floor are different buildings; and how deep behind a door or
  window the fill starts, since flush against the opening and one course back read differently through the
  gap. `DressingScope` already protects the ground under a stamped building, so nothing downstream needs
  teaching.

- [ ] **B95 — A stage image has no key, and its colours are read as materials.** The plan render colours by
  **role** — hub violet, spawn green, wool amber, frontline orange, anything else slate, and a zone in blue
  (`#38bdf8` for a build zone, `#2563eb` for a water lane, separated only by shade, opacity and dash pattern).
  Nothing on the image says any of that. Blue is the universal visual code for water, so a reader who has the
  picture and not the key is handed a wrong answer rather than no answer: a generated board's central build
  zone was read as water on a map that carries none, and the misreading was then used to explain away a
  connectivity result. An image that invites a confident wrong reading is worse than one that is merely
  unclear, and the pictures exist precisely so that things get read off them.

  Two fixes, and the second matters more than the first. **A legend on the image** — the role swatches and the
  two zone kinds named on the plan render, and a scale on every world read-back — costs little and removes the
  guess. **A distinction that survives being looked at**: a build zone and a water lane are the same gap with
  different crossing rules, one open from the first minute and one opening part-way through a match, and
  encoding a difference that large as two shades of blue means it does not survive the render. Hatching, a
  label, or an outright different hue would.

  The general rule this teaches belongs beside the images in `docs/tools/capabilities.md`: **an image is a check, not a
  source of meaning.** A render answers "did the thing that was authored actually come out", and the document
  underneath answers "what is it". Reading semantics off pixels is how a build zone becomes water.

- [ ] **B96 — Density wants measuring as canopy share, not as a leaf count.** The leaf count is the only
  honest measure of *whether a forest was planted* — nothing but a tree lays a leaf, and a building's corner
  posts are logs — but it is a poor measure of **how wooded a board reads**, and two measurements on one board
  size prove it: a spruce forest at 17,600 leaves over many sites rendered as one solid mass with the routes
  buried, while `thornwake` at 17,897 leaves over 72 trees renders as a wood a player walks through. Nearly
  the same count, opposite maps, because the leaves are divided among a tenth as many trees. The number that
  would decide it is the share of ground columns standing under a leaf, which is a cheap read over the same
  voxels the census already walks, and it is scale-free in a way a raw count is not — a 120×240 board and a
  240×240 board do not want the same leaf count to read the same. Report it beside the leaf count and give the
  README a band in those terms; the two numbers disagreeing is itself informative, since a high count at a low
  share is a few enormous trees and a low count at a high share is scrub.

- [ ] **B97 — Leaves may lie against a building and never inside it.** A prop already writes only into air —
  `Decorator` skips any unburied cell whose target is not `Blocks.Air`, so a tree can never replace a wall, a
  roof or a post, and a canopy resting against a house is correct and wanted. What that mask does not catch is
  the **enclosed volume**: a building's interior is air, so a crown overhanging a roof drops leaves through it
  into the room below, and the room is then a room with a tree in it. The authoring convention this is
  imitating is exact — a map author pastes a tree beside a house masked against the house's own blocks and
  then **removes the leaves that landed inside it**, so the building stays empty. Only the second half is
  missing here.

  So the rule is three-way rather than two-way, and `B85` implemented only two thirds of it: a prop may not
  **root** inside a structure (done), a prop may not **replace** a structure's blocks (done), and a prop's
  cells falling in a structure's **enclosed interior** are dropped rather than written. The last wants the
  volume a stamped building encloses, which `HouseStamper` knows at stamp time and nothing records afterwards
  — recording it is most of the work, and `DressingScope` is where a stamped thing's extent already lives.
  Worth doing with `B92`, which fills that same volume with a stated material and therefore has to describe it
  anyway.

- [ ] **B98 — A stage image is a diagram, not a photograph.** The renders imitate what a map looks like —
  grass green, leaves green, stone grey — and that is the one thing an image for reasoning must not do,
  because a green tree on green ground is invisible in it. Legibility beats realism here: a reader, human or
  model, needs to tell foliage from surface from structure at a glance, and the surest way is deliberate
  false colour that no terrain would ever wear. Foliage in a vibrant purple against a muted ground says more
  in one look than an accurate render says in ten.

  Two things follow, and the second is the larger. **Contrast is the requirement**, so every render picks its
  palette to separate the categories it is drawing rather than to depict them — the same reasoning `B95`
  applies to the plan's role colours, applied to the world read-backs. And **a layer is worth seeing alone**:
  the top-down today is the finished rasterized map with everything on it at once, including things a given
  question does not want — the redstone lines marking a build region's edge in red, the observer platform
  floating over the middle — so the reading is a search rather than a look. One image per layer (ground ·
  relief · paint · structures · foliage · buildings · objectives), each in its own high-contrast scheme, plus
  the combined view that already exists, makes each pass answerable on its own, which is what a pipeline that
  is meant to be reviewed between stages actually needs. `B90` built the set; this is what makes it readable.

- [ ] **B99 — An objective reads as cut off from the board, and it is not yet known whether it is.** Three
  `dtcm` specs built for the first time once `B94` landed, and `goldhollow` and `spinebreak` rendered four and
  eight objective markers isolated from the board's navigable component — real ground beneath them, no walkable
  join. A goal nobody can reach is unwinnable exactly as a goal nobody can mine is, and it had been hidden
  behind the void refusal.

  A second run then found the same reading on **every** composed `dtm` board it tried — ten-plus seeds across
  both symmetries, before touching anything — which changes what the likeliest explanation is. Ten broken
  boards in a row is a worse hypothesis than one broken measurement, and there is a specific mechanism to
  suspect: `TraversabilityRender` snaps a marker to the component under the goal's own block, and that block
  is solid, so it is never itself navigable. A search that starts there can fail to find the component the
  ground beside it belongs to. That would also explain why the corpus convention the composer follows — a goal
  at the far end of a dead-end lane, inset about five — reads as isolation rather than as a dead end.

  A hand-authored board then settled it further: its **wool** markers read isolated too, and rebuilding the
  repository's own `tools/seeds/base-2wool.plan.json` through the same pipeline reported all four of *its*
  wool markers isolated as well, on a seed nobody suspects. The land interface was flush and the floor under
  the wool solid, so the reading is a property of the cage stamp against the renderer's strict two-cell
  headroom test rather than of any board's geometry. That is a measurement fault on a second objective kind,
  which makes the renderer the likely cause rather than a possibility.

  So the first move is to tell the two apart, and the cheap way is to ask the question from the ground rather
  than from the goal: take the walkable cells immediately around the marker and test whether *they* join the
  spawn's component. If they do, the render is at fault and the fix is in the snap. If they do not, the fault
  is real, it is in the composer's seating or the compiler's build regions, and it is the more serious of the
  two. Do not fix either until the measurement says which.

- [ ] **B101 — A destroy board's goal gets no dressing clearance at all.** `KeepOut` in `tools/mapgen`
  builds its exclusion rectangles from `intent.Wools` and `intent.Spawns` and from nothing else, so a
  destroyable and a core are invisible to it. On a capture board that is harmless, because the wool rooms are
  the goals; on a destroy board `Retarget` empties `Wools`, and the only protected thing left is the spawn.
  Nothing then stops the forest or the village planting a trunk against a monument's wall — two hand-designed
  boards landed their nearest tree eleven blocks off the goal, but by the author's steering rather than by any
  rule. `DressingScope` protects the ground a stamped structure stands on, which is why this has not produced
  a tree *inside* a goal, but clearance is a wider question than overlap: a goal wants open ground around it
  because that is what makes the approach legible, which is the whole method the review argues for. Add the
  destroyables and cores to `KeepOut`, and say so in the README, whose "any objective piece — a room or a
  spawn" reads as though it already covers them.

- [ ] **B102 — A rebuild writes over a region directory it never clears, so a stale chunk survives.**
  `AnvilRegionWriter.Write` calls `Directory.CreateDirectory` and nothing else, so every `.mca` a previous
  build left is still there. A chunk the new build does not touch — because its geometry moved — is read back
  as part of the new map. That is not a cosmetic problem: it makes a rebuild into an existing `out_dir`
  untrustworthy, which is exactly what iterating on a spec does, and it silently contradicts the README's own
  promise that "the same spec rebuilds the same map, so two runs can be compared" — true only into a directory
  nothing has written before. It cost a design session real time, presenting as building counts that could not
  be reconciled until the directory was deleted by hand. The fix is to clear the region directory before
  writing it. Note this is a different hazard from the concurrent-build race `CLAUDE.md` already warns about:
  that one is two builds at once, this one is one build after another.

- [ ] **B103 — The top-down leaves real ground blank on a narrow board.** On a board whose goal sits on a
  narrow dead-end spur, `TopDownRender` drew that spur — the most important corner of the map — as empty
  margin at every scale tried, while `HeightProfileRender` and `StructureFinder` showed real, populated
  terrain there in the same build. The ground was confirmed present by reading the region files directly. A
  renderer that omits ground is worse than one that is merely hard to read, because the omission is
  indistinguishable from a board that genuinely has nothing there — and the top-down is the view everything
  gets judged from first. Suspect the bounds computation rather than the drawing: the spur is at the extreme
  of the board's extent, which is where an off-by-one or an early bbox clamp would bite. It is also a second
  instance of the fault `mapgen-review.md` MG13 names, found on a newer renderer than the one that entry describes.

- [ ] **B79 — `map-layers` e2e: the plan editor's Compile button never arrives (13/14).** The suite drives to
  `/maps/{slug}/plan` on the seed's built map, then clicks `button:has-text("Compile")` to check that a
  *rebuild* states the trade before replacing a board someone has worked on. The click times out at 30s and
  the page records `HTTP 422 /api/plan/compile`, so the confirmation half of the suite never runs. **Not a
  regression from the island gate or the author fix**: it reproduces identically at `f42ec58`, the commit
  before either landed, and it is not the corpus either — re-running with `MapsRoots` pointed at an empty
  directory fails the same way, so the 15 generated maps under `CommunityMaps/ctw` are not the cause. It is
  also not contention: it reproduces with the suite run alone and no dev server up. It did pass once, on the
  first post-merge run, which is what makes it worth a task rather than a revert. The 422 is the plan
  **validator** refusing what the page posts, while the same map's stored plan compiles 200 through curl —
  so the first thing to find is what the editor sends that the stored document does not, and the finding
  ids in the 422 body name the rule.

