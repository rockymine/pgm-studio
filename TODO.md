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

**The machinery answers, and the answer arrived with a bill.** Six agent runs across three models authored
**nineteen loadable maps** and seven reports by driving the real documents end to end, with no human in the
middle — which is what `B120` asked and is now settled. The author then reviewed twelve of those boards
against the plans, the layouts and the built worlds, and every claim in that review re-measured: **forty-eight
findings, and one of them fatal.** Three committed maps do not parse at all, because a board carrying two
objective kinds ships `<gamemode>dtm dtc</gamemode>` and PGM's `Gamemode` is a closed enum (`B155`).

**Twenty of the forty-eight enforce rules that are the author's**, stated rather than measured, and that
nothing in the system checks: a spawn door stands 15 blocks off the void and opens onto 20×20 of clear ground
it can climb back onto; a wool-room piece is at most 20×20 and a placed building at least 5×5; two islands sit
15–40 apart; two goals of one team sit 35 apart; obsidian caps at three blocks; a grass course is exactly one
block; a log is never a roof. They are law under `CLAUDE.md`'s oracle clause. **They are filed in
`BACKLOG.md` as `B141`–`B188`, bucketed into thirteen dispatch groups** — each bucket one agent, one pass, one
area of code — with the collisions between them stated (`B1`–`B3` all land in `PlanValidator`).

The `MG` entries stay in `docs/tools/mapgen-review.md` as evidence, the way `docs/generator/audit.md` holds the
generator's; an entry **leaves it when its fix lands**. Two want reopening on the audit's measurement: `MG28`
closed the whorled tree on a leaf count where the ratio is 1.26 leaves per log (`B174`), which is `B96`'s fault
recurring on a different subject.

**What the boards showed that no gate will fix.** Ten of twelve settlements are one street of identical houses
behind the spawn; three boards are unreadable in opposite directions, one vanishing into its own rock and one
clashing; every objective on the widest board sits in a six-block column. Those are composition rather than
defect — no refusal is wanted — and they are why the next run is not the same run again. The authoring
apparatus itself is the second half of this focus (`B189`): art direction, named map briefs, and a reviewer
that is a separate agent from the author.

## Backend, pipeline & internals (B / P / A)

**These three are the current run, in order.** They come out of one finding, and the finding is worth stating
once because every entry below inherits it: **`tools/` grew a second copy of the system.** A two-day
experiment in whether an agent could drive the studio produced a CLI that reimplemented the parts it could
not reach — its own goal-over-void refusal, its own prop clearance, its own forest and village samplers, its
own reduced document format — and the board began treating that CLI as product, filing bugs against it and
listing its refusals in `FEATURES.md`. A tool is allowed to *drive* the system. It is not allowed to *be* a
second one, because the second one is what rots: it has no tests, no document that governs it, and it drifts
behind the thing it copies without anyone seeing (the relief fork sat 1614 cells off a settled solve for
exactly that reason). `B119` moved the boundary so the copying stopped being necessary — the export path is
`PgmStudio.Export` now, not a folder inside the web app — which is what made `B118` cheap: it deleted the
copy, the site sampler and the reduced spec format both, in favour of the real documents `B119` had just made
reachable.

**That run is finished and `B120` with it.** `B128` landed — a destroyable/core's `float` now counts from the
ground the world build actually solves rather than the plan's flat nominal surface, and the marker itself may
name no plan piece at all, so a goal rides an authored sketch landform with no tier manufactured to carry it —
and the trial that tested the result ran three times over three models. Nineteen maps, seven reports, and the
forty-eight findings now in `BACKLOG.md`. What is on this board is what the trial made urgent: the label PGM
refuses, the authoring apparatus the next run needs, and the entries the audit corrected.

- [ ] **B155 — A map declaring two gamemodes writes one element with a space in it, and PGM refuses to parse
  it.** `MetaGenerator.cs:37` builds the label as `string.Join(' ', Gamemodes.From(…))` and `XmlWriter.cs:113`
  writes it as a single element, so a board carrying two objective kinds ships `<gamemode>dtm dtc</gamemode>`.
  PGM parses the element as a **repeated** one holding a single id each: `MapInfoImpl.parseGamemodes` loops
  `root.getChildren("gamemode")`, calls `Gamemode.byId(el.getText())` and throws
  `InvalidXMLException("Unknown gamemode")` when it matches nothing. `Gamemode` is a closed 25-value enum
  matched with `equalsIgnoreCase` and no splitting, so `"dtm dtc"` is simply not a value. **The map does not
  load.**

  **The premise underneath it is the real fault.** `MetaGenerator.cs:32` and `Gamemodes.cs` both state that
  `<gamemode>` is "a free-text label PGM never reads authoritatively (`OB7`)". Half of that is right and the
  half that is wrong is fatal: PGM does not use the element to decide which modules run — those come from the
  objective elements — but it *does* validate it, strictly, and an unknown value kills the parse. "Not
  authoritative" was flattened into "free text", and a value was invented on that basis. Across ~350 corpus
  maps every `<gamemode>` holds exactly one id and maps with several **repeat the element**
  (`cacti_the_wool` carries six); nothing in either corpus has ever written a space-separated value.

  **The fix is two-sided and the reader has the mirror of the same bug.** The writer wants a list, not a
  scalar — one `<gamemode>` per mode. `MapParser.cs:125` takes `GetText("gamemode")`, the first element only,
  so importing a corpus map declaring several keeps one and silently drops the rest;
  `MapXml.DeclaredGamemode` is a scalar where PGM's model is a list and both directions inherit it. A
  validation gate belongs with it: the studio can check the label against PGM's enum, which is the kind of
  refusal the export composer already hosts.

  Affects three committed maps — `tallow-kilnrow`, `ashfall-scar`, `basalt-reach`. Correcting the XML by hand
  is a two-line edit per map and needs no rebuild. Separately, **seven boards declare `ctw` and carry no
  wool** (`ashen_quarry`, `quillon-barrow`, `quillon-foundry`, `sonnet-holdfast`, `sonnet-cinderreach`,
  `haiku-canonical-destroy-3`, `haiku-dtm-tower`); those parse, because `ctw` is a valid id, and a rebuild
  against the current studio corrects the label.

  *Found by the author reading PGM, not by any run · `PGM/…/map/MapInfoImpl.java:312–320` · `PGM/…/api/map/Gamemode.java` · corpus swept for counter-examples.*

- [ ] **B189 — The authoring apparatus: art direction, named briefs, and a reviewer that is not the author.**
  Three runs asked three models for "a map of your own design" and got, three times over, one street of
  identical houses behind the spawn on a square board under a palette nobody checked. The brief is where that
  came from: it asked for a **visual identity you can name in a sentence** and then supplied no vocabulary for
  saying one, so every model reached for the same defaults, and it asked each model to review its own work,
  which is how a report came to describe two empty shells as "verified working with 2 destroyables".

  The replacement is four documents in `pgm-studio-mapgen`, and their shape is the point rather than their
  contents. **`ART-DIRECTION.md`** is the visual law — the material rules the audit measured (one course of
  grass, a slab only on a half-course rise, no log in a roof or a verge, no footing on terrain, obsidian at
  three blocks), the settlement rules that break the street, and the two failure modes a palette has. It is
  written as constraints an author can check before building, because every one of them was checkable at the
  theme and nothing checked. **`MAP-BRIEFS.md`** replaces "a map of your own design" with **named briefs** — a
  mode, a size in the corpus band, a stated visual identity, a stated composition of approaches, and the one
  thing each board is a test of. **`REVIEWER-BRIEF.md`** is a **second agent**, given the board and not the
  author's intent, whose checklist is the twenty author rules with their numbers in it and whose output is a
  per-item table with coordinates. **`AUTHORING-BRIEF.md`** is the author's own brief rewritten around the
  other three.

  What this entry owns is keeping them true. Every rule in `ART-DIRECTION.md` and every check in
  `REVIEWER-BRIEF.md` is a `B141`–`B188` id that has not shipped yet, so **each one is a debt with a due
  date**: when its bucket lands the check moves from the reviewer's list into the pipeline's refusals, and the
  reviewer document loses a row. A reviewer still enforcing `B163` after `B163` ships is a second copy of the
  system, which is exactly the fault `B118` undid in `tools/`.

- [ ] **B140 — A map with no objective, no spawn and no team exports 200 and looks like a map.** Two boards
  from the second authoring trial (`haiku-r2-canonical-8`, `haiku-r2-ctw-mid`) built a world, wrote region
  files and a provenance sidecar, and exported clean. Their `map.xml` is **ten lines**: a name, an empty
  `<version>`, a `<gamemode>`, an empty `<objective>`, one include and a hunger rule. No `<team>`, no
  `<spawn>`, no `<destroyables>`, no `<wools>`, no `<cores>`, no regions, no filters.

  **Every gate passed vacuously.** `OB17` asks whether a goal stands in void, `OB18`'s successor asks nothing
  now, `OB19` asks whether a prop crowds a goal, and the traversability check asks whether spawn and wool
  points reach each other. All four quantify over collections that are **empty**, so a board with nothing on
  it satisfies all of them — the pipeline's whole refusal surface is built to catch a map that says something
  wrong and none of it catches a map that says nothing at all. That is the more serious half of this entry: a
  map cannot be played without a team and a spawn, so a map lacking both is not a rough map, it is not a map.

  **The `ctw` label is the smaller half and has its own cause.** `MetaGenerator.DeclaredGamemode` derives the
  label from what the intent carries and correctly yields the empty string for a map carrying none (`B131`),
  but it only runs on the intent path — and these maps have no stored intent, so the export takes the plain
  branch and the label survives from `docs/pgm/template.xml:5`, which hardcodes `<gamemode>ctw</gamemode>`.
  So the template still asserts what `B131` stopped the generator asserting, on exactly the maps least able
  to correct it.

  What the refusal should ask is the question no current gate does: whether the map has an objective at all,
  and a team and a spawn to contest it. It belongs with the others on the export composer, carrying a rule id,
  and it wants stating in terms of the intent rather than the document, since the intent is what an author
  actually failed to write.

  **This entry is understated on its own evidence, and the correction changes the fix.** It frames the empty
  boards as a map whose author *failed to write* an intent. But `specs/haiku-r2-canonical-8/plan.json` carries
  **two spawns and two destroyables**. The author did state them; they were **lost between the plan and the
  export**, and every stage answered 200. That is a harder failure than an author writing nothing, and the
  refusal proposed above — stated against the intent — would still not catch it, **because the intent was
  never stored**. So the gate wants asking at the last point that can see both: what the plan declared, and
  what the export is about to write. `B141` is the same family one level down, on the sketch document.

- [ ] **B136 — The two features that make a shape stop looking drawn are reached almost never.** **The census
  below is stale and wants re-running before anything is concluded from it:** it covers **eleven** maps and
  there are now **nineteen**, and the two boards it does not see are the strongest ones — Opus 5's
  `marlstone-steps` and `basalt-reach` move every row, **including the Bézier column it records as zero**.
  Re-run it over all nineteen `specs/` folders first; the gradient below may or may not survive. Measured over
  the eleven, counting non-null uses in the authored specs rather than serialized nulls:

  | | per-shape `theme` | `anchor_heights` | Bézier `controls` | `height_mode` | `skirt` | relief `marks` |
  |---|---|---|---|---|---|---|
  | Opus, three boards | 5 · 6 · 8 | 2 · 1 · 1 | **0 · 0 · 0** | 2 · 2 · 3 | 2 · 2 · 3 | 5 · 3 · 3 |
  | Sonnet, three boards | 2 · 4 · 4 | **0 · 0 · 0** | **1 · 0 · 0** | **0 · 0 · 0** | **0 · 0 · 0** | 4 · 5 · 3 |
  | Haiku, three boards | **0** | **0** | **0** | **0** | **0** | **0** |
  | `ashen_quarry` (earlier run) | 1 | 2 | **0** | 1 | 2 | 6 |

  **A Bézier curve has been authored once, on one shape, across every map this repository holds.** Per-vertex
  `anchor_heights` — the slant control, where an outline's corners each take a height and the surface solves
  between them — is used only by the strongest model and only once or twice a board. `height_mode` and
  `skirt` follow the same line. Every other outline on every map is a straight-edged polygon at one height.

  The gradient is the finding. Per-shape themes and relief marks are reached by two models out of three and
  are the features the documents lead with; the shape-level height and curvature controls are reached by one
  model, barely, and the weakest model reaches **nothing** — its three boards are compiled plan rectangles
  under a single blanket theme, which is the exact output the first fifteen generated boards had. So the
  documents are not the binding constraint for the top of the range and are clearly binding at the bottom.

  What this is **not** is a request for a new capability: all six columns are shipped, documented in
  `capabilities.md` and `sketch.md`, and demonstrated on a committed map. It is a question about **reach** —
  why a control that changes how a board looks more than any other is the one an author does not get to. The
  candidates worth testing rather than assuming: the fields sit on `SketchShape` and a compile emits shapes
  with them null, so an author edits a document rather than asking for a shape; nothing previews a slant or a
  curve without building a world; and the worked examples in the documents are rectangles, so the first thing
  a reader copies has straight edges and one height.

- [ ] **B135 — The paired core defaults leak on the first break, with nothing to dig.** `ObjectiveDefaults`
  carries `CoreFloat = 6` and `CoreLeak = 5` and documents them as a pair (DC2). Read against PGM, that pair
  leaks immediately. `Core.java` builds a leak region whose top is `coreRegion.min.y − leakLevel` and sets
  `leakRequired = lavaRegion.min.y − max.y + 1`, so with the lava sitting at the casing's floor the lava must
  descend **`leak + 1` = 6** blocks below itself to count as leaked. Six blocks of authored air sit under the
  casing, so the lava falls exactly that far with no terrain in the way: the core leaks the moment it is
  opened, and the dig that is supposed to be the second half of the task does not exist.

  **The corpus settles it, and a zero dig is legitimate — that half of the entry is withdrawn.** Ten `dtcm`
  maps carrying cores use `leak` 3–6, median 5, so the studio's `CoreLeak = 5` is the corpus norm exactly.
  Probing two of them with `--column` finds two opposite designs, both shipped:

  | map | `leak` | casing floor | air beneath | dig required |
  |---|---|---|---|---|
  | `stone_fields` | 5 | y23 (obsidian), lava y24–26 | 4 (y19–22), chest y18, solid y17 | **2 blocks** |
  | `fungi_grove` | 6 | y15 (obsidian), lava y16–19 | 11 (y4–14), floor y3 | **none** — it hangs over a chasm |

  So a core that leaks the moment its casing opens is a real design: `fungi_grove` suspends one over a drop
  and the whole task is breaking the shell. The studio's `float 6` / `leak 5` reproduces that pattern, which
  makes it a **default**, not a defect.

  **The "no way to ask for it" half is false and is withdrawn.** An earlier version of this entry said
  `CoreFloat` and `CoreLeak` are paired to a single outcome "with no per-core control on the marker or the
  intent … no way to ask for it". There is: `basalt-reach` authors `"float": 5, "leak": 8` on its core marker
  and ships `leak="8"` in its `map.xml`, so per-core control exists on the plan marker, the intent, the
  validator and the XML writer, and a board can express `stone_fields`' shell-then-dig today. The `const` pair
  is a **default** that is reachable past, not a ceiling. What remains of this entry is the off-by-one below —
  and it is not cosmetic, because it is what the studio *tells an author their dig is*: on `basalt-reach` the
  studio reported 3 and the real dig is 4.

  **And the arithmetic the studio shows an author is off by one.** `PlanTool` computes
  `CoreDigDepth => Math.Max(0, CoreLeak - CoreFloat)`. PGM sets `leakRequired = lavaBottom − (coreBottom −
  leak) + 1`, and the lava sits one course above the casing floor, so `leakRequired = leak + 2` and the lava
  must reach `coreBottom − leak − 1`. The true depth is therefore **`leak + 1 − float`**. Both formulas give
  0 at the shipped pair, so the error is invisible at the default and wrong everywhere else — at `leak 5`,
  `float 4` the studio says 1 and `stone_fields` measures 2.


- [ ] **B129 — The section renderer cuts one plane, so everything behind the cut is missing.**
  `SectionRender` samples a **single one-block-thick slice** and paints each cell with the block that stands
  exactly on the plane. That is the right reading for checking a `layered` material, which is what it was
  built for, and it is the wrong one for looking at a map: a cut through solid ground is a solid slab,
  because a solid slab is genuinely what sits on that plane. A cut through Ashen Quarry's town at z=60 is
  two courses of stone brick over forty-seven of andesite over bedrock, measured by `--column` and rendered
  faithfully — and it shows none of the buildings standing a few blocks either side of it, none of the room
  interiors, and nothing of the town's silhouette. The picture is accurate and nearly uninformative, which
  is a harder fault to notice than a wrong one.

  **The studio already computes exactly the missing quantity, on the other side of the house.**
  `Analysis/Layer/SideView.Build` projects a map's vertical solid segments onto a `(primary × y)` grid as a
  **depth map** — for each cell, the distance from the viewer to the nearest solid along the perpendicular
  axis, `0` nearest and `-1` for a cell nothing occupies — for four viewing directions (`nz`/`pz`/`nx`/`px`,
  with the positive-side ones mirroring left-to-right). `GET /map/{slug}/segments` serves it and
  `js/studio/canvas/sideview-canvas.js` paints it as a depth-tinted cross-section. So a section that shows
  what stands behind the plane is not a new idea here; it is an existing one the block-level renderer never
  reached, and the two want the same projection.

  Two differences are real and have to be settled rather than glossed. `SideView` reads `layer_segments`
  rows, which exist for a map the studio has **scanned**, while `SectionRender` reads a region directory or
  a `VoxelWorld` — so the projection wants doing over voxels rather than over segments, and the shared thing
  is the algorithm, not the input. And a depth map answers *how far* rather than *what*, so a depth-only
  section loses the material identity that makes the current one worth having: the two are complementary
  modes of one renderer, not a replacement.

  **The existing instance is greyscale, and colour is the half it never got.**
  `sideview-canvas.js` ramps nearest to farthest across light stone to very dark, so depth reads and category
  does not. A block-level section drawing the same projection can carry both — distance as value, material or
  category as hue — which is the pairing that makes a building behind the cut legible as a building rather
  than as a lighter smudge.

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
  literal world Y for **spawns and wool rooms** (`PlanCompiler` lines 205, 260) — the destroyable/core half of
  this is done (`B128`): `float` now counts from the ground the world build actually solves rather than
  `piece.Surface`, and the marker itself may name no piece at all. Spawns and wool rooms still bake their
  room floor from `piece.Surface` at compile time, the same flat-world mistake wearing a different field; the
  anchor wants resolving against the ground as built. And the ceiling wants a sane relationship to the
  finished terrain rather than to the plan's base, since a map whose highest ground is y20 and whose cap is
  y20 permits no building at all.

- [~] **B106 — Two different things in this codebase are called protection.** **The placement half of this
  entry has landed and its premise is stale.** It described `Retarget` reusing the wool markers so a destroy
  goal could only stand where a wool budget put one, and the three documents stating that conflation as a
  principle. `Retarget` was **deleted by `B118`**, the README sentence is corrected, and `B128` shipped the
  replacement: a destroyable or a core may name an **empty `piece`** and give `at` as an absolute board
  position, its height resolving from the solved terrain plus `float`. Goals authored that way ship on three
  committed maps. Nothing of that half is left to do.

  What remains is the naming problem underneath it, and is worth fixing while here, because it is the likeliest reason the
  conflation felt right: **two different things in this codebase are called protection.** One is the XML
  region rule that stops a player entering a spawn or a wool room and restricts what may be broken or placed
  inside it — a gameplay contract. The other is `Decorator.IsProtected`, "cells nothing may be placed on",
  which is a dressing keep-out and has no gameplay meaning at all. A goal that needs the second does not need
  the first, and one word for both invites exactly the inference that a destroyable must live somewhere
  protected — which is the inference that produced the caged goals in the first place, and it survives the
  code that acted on it.

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
  the interesting half because it is the one thing the plan cannot know before the relief runs — `B128` landed
  that half in the document (`float` counts from solved ground, and the marker itself may name no plan piece
  at all); what is still missing is a way to draw and drag that point on the canvas.

  **Position, separately.** Moving a piece rather than raising it is `S25b`, and the design here deliberately
  leaves rect and position tracking the plan so that a recompile stays authoritative about *where* while the
  author stays authoritative about *how high*.

  **And the raster does not draw an absolutely-placed goal at all**, which folds in here because this entry
  owns the canvas half. `GET /plans/{id}/png` draws `tallow-mirefast`'s five pieces, both spawns and the
  legend, and nothing at `(0, −50)` where the wardstone stands. `B128`'s empty-`piece` marker is the most
  useful thing on the board for an agent — it is how a landform carries an objective without a tier
  manufactured to hold it — and **the one picture the plan layer offers cannot show what it produced**, so an
  agent authoring from the render has no way to see its own goal.

- [ ] **B109 — Nothing checks a plan before it costs a build.** Authoring a plan by hand is arithmetic over
  rectangles in cells, and the repository offers no way to ask whether the arithmetic worked short of running
  the whole pipeline. Two pieces that overlap, a land interface too narrow to connect, a stray corner touch —
  none of these is reported until a world has been built. An author writing two boards by hand had to
  re-implement `ContactGraph.Classify` in a throwaway script to check adjacency before spending a build cycle,
  which shortened iteration enough to be worth the detour and is a tool the repository should have.

  **Half the premise is stale and the correction narrows the work.** `POST /plan/inspect` and
  `POST /plan/evaluate` both exist and are documented at `plan.md:417`, so a plan *can* be asked about without
  a build. What survives is that **nothing an author reaches calls them**: no driver invokes the validator
  ahead of a build, and an agent authoring by hand found the endpoints only by reading source. That is reach,
  not absence — the same shape as `B177`, where `SP2` and `SP7` are written law that nothing applies to an
  authored plan.

  **This entry is the home the audit's plan-space rules need, and it is why it is worth doing before them.**
  Buckets 1–3 in `BACKLOG.md` are fourteen findings that are all geometry over plan rectangles — what a spawn
  door faces (`B158` `B169` `B172` `B177` `B180`), how big a piece is and how far apart (`B156` `B157` `B167`
  `B170` `B178` `B186`), how far apart the goals are (`B175` `B179` `B188`) — and each one is a rule with a
  number in it that `PlanValidator` is the natural place for. Landing this entry first means those fourteen
  are findings added to a reachable validator rather than fourteen separate checks looking for a home. The
  findings name rules rather than describing symptoms, which is what an agent needs and what a human reviewer
  can check a board against.

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

  **The measurement has now been made, without being asked for, and it points at the renderer.** Sonnet's
  second run rendered `sable-marsh` and **two of its four wool markers read isolated — and they are exactly
  the two walled rooms**; the two open ones read connected. A wall in front of a room is not a board fault, so
  the discriminator this entry asks for has answered: the reading tracks the cage rather than the geometry,
  which matches the ClayClay precedent and the strict two-cell headroom test named above. That is evidence
  rather than proof — it is one board — but it is the right shape of evidence and it should be the first thing
  reproduced when this is picked up.

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
