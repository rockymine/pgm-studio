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
exactly that reason). `B119` moved the boundary so the copying stopped being necessary — the export path is
`PgmStudio.Export` now, not a folder inside the web app — which is what made `B118` cheap: it deleted the
copy, the site sampler and the reduced spec format both, in favour of the real documents `B119` had just made
reachable.

**One remains.** `B128` is done — a destroyable/core's `float` now counts from the ground the world build
actually solves rather than the plan's flat nominal surface, and the marker itself may name no plan piece at
all, so a goal can ride an authored sketch landform with no tier manufactured to carry it. That leaves `B120`,
which finds out whether the result actually answers.

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
    a tool is the exact defect `B116` and `B118` undid.
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

- [ ] **B136 — The two features that make a shape stop looking drawn are reached almost never.** Measured
  over the eleven maps in `pgm-studio-mapgen`, counting non-null uses in the authored specs rather than
  serialized nulls:

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

- [ ] **B134 — "An iron pickaxe does not mine obsidian at all" is false, and a 409 now refuses maps on it.**
  `DestroyKitPairing`'s docstring states the premise outright — *"an iron pickaxe does not mine obsidian at
  all, so a mismatch is not a rough edge, it is an unwinnable map"* — and it is wrong. An iron pickaxe
  **breaks** obsidian; what it does not do is **drop** it. A destroy objective only requires the block to be
  gone, so an obsidian monument against an iron pickaxe is winnable, and slow, which is a design choice about
  how long a raid should take rather than a broken map.

  The claim entered as `mapgen-review.md` MG18, shipped as `B81`, and `B116` then wired
  `DestroyKitPairing.Unwinnable` to the export gate as `OB18` — a **hard 409**. So the studio now refuses to
  export a class of map that plays correctly, and the refusal names a rule id that asserts the false claim.
  That is the worst direction for this error to travel: it began as a note, became a fix, and is now a gate.

  **The severity is the gate, not the pairing.** `RequiredPickaxe` upgrading a kit to match its goals is
  useful and stays. What has to go is the inference from *cannot drop* to *cannot break*, and with it `OB18`'s
  standing as a refusal — a mismatch is at most a warning, and probably not even that.

  **The author's rule for the pairing, which settles what it should do instead: the material defines the
  kit.** Obsidian pairs with a diamond pickaxe; anything softer pairs with iron. That is a statement about
  what the kit should be *generated* as, not about what is legal, so it belongs in `RequiredPickaxe` and
  nowhere near a gate.

  Grep the repository before calling this done: the false sentence is repeated in `DestroyKitPairing`'s
  docstring, in `docs/pgm/destroyables-and-cores.md`, in `mapgen-review.md`'s MG18 row, and in whatever
  `FEATURES.md` says `B81` and `B116` shipped. Each one reads as settled fact.

- [ ] **B135 — The paired core defaults leak on the first break, with nothing to dig.** `ObjectiveDefaults`
  carries `CoreFloat = 6` and `CoreLeak = 5` and documents them as a pair (DC2). Read against PGM, that pair
  leaks immediately. `Core.java` builds a leak region whose top is `coreRegion.min.y − leakLevel` and sets
  `leakRequired = lavaRegion.min.y − max.y + 1`, so with the lava sitting at the casing's floor the lava must
  descend **`leak + 1` = 6** blocks below itself to count as leaked. Six blocks of authored air sit under the
  casing, so the lava falls exactly that far with no terrain in the way: the core leaks the moment it is
  opened, and the dig that is supposed to be the second half of the task does not exist.

  `PlanTool` already computes `CoreDigDepth => Math.Max(0, CoreLeak - CoreFloat)` and shows zero, so the
  studio displays the consequence without treating it as wrong. **Whether a core should require digging at
  all is a gameplay question and is not settled here** — PGM's own default `leak` is 5 and says nothing about
  float. What is established is the mechanism and that the shipped pair sits exactly on the boundary where
  the dig vanishes, which is unlikely to be a chosen number.

- [ ] **B133 — What a cell *is* cannot be read off the block that sits there, and three read-backs try.**
  `RenderCategories.Classify` decides `Structure` with `BlockRoles.IsBuilt(blockId)`, `StructureFinder`
  finds a building by material, and `--structures` fuses a house into the ground the moment the two share a
  palette. All three ask the same question of the same wrong source: **a block does not know what placed
  it.** Stone brick is stone brick whether it is a cottage wall, a paved plaza or a mesa the author painted
  to read as built, so a material test cannot separate them and no refinement of the palette will.

  The B98 render is the decisive evidence and it is worth stating precisely, because the picture looks like
  a success. Foliage and water come out right. **Everything else is wrong in the same direction**: Ashen
  Quarry's town terrace and the mesa hull both read solid orange as structures when both are terrain, and
  ground correspondingly under-reports, because the town is painted in a built-looking material. A reader
  who trusts that image concludes the board is half buildings. `B124` narrowed the same fault in
  `--structures` with a step test and said in its own entry that a roof flush with the paving it stands on
  still defeats it — which is the same limit reached from the other side.

  **The system already knows the answer and throws it away.** A world the studio builds is built in passes:
  the rasterizer lays base terrain, the stampers raise rooms, walls, iron cubes and objectives, and the
  dressing pass places trees, boulders, buildings and flora at coordinates an author chose. Every one of
  those passes knows the cells it claimed at the moment it claimed them — `DressingScope` already walks a
  prop's `ClearanceFootprint` and a house's `Footprint` for exactly this reason — and none of it survives
  into the finished voxels, so the renderers re-derive from blocks what the build could simply have
  recorded.

  So the model is **provenance carried as layers, resolved in placement order rather than by palette**: base
  terrain, then structures, then props, each layer knowing which cells it owns, composited so a later layer
  covers an earlier one. A render then colours by *which layer claimed the cell*, which makes a stone-brick
  cottage on a stone-brick plaza two different things because two different passes put them there, and
  makes a painted mesa terrain no matter what it is painted with. It also gives `--structures` a definition
  that cannot fuse, since a stamped building's extent is recorded rather than flooded for.

  Two things are worth settling as this is built. The layer record wants a home that survives the build —
  beside the voxels rather than inside them, since a block cannot carry a provenance byte — and it wants to
  be optional, because a world the studio **scanned** rather than built has no provenance and the
  material-based reading stays the only one available for those. That split is real and should be named
  rather than papered over: a built map gets the truth, an imported one gets the estimate, and a render
  should say which it is showing.

- [ ] **B131 — Every map the studio writes says it is a capture map, whatever it is.** `MetaGenerator`
  states it in its own docstring — "Version/gamemode are fixed for new **CTW** maps; the objective is
  generated from the wool count" — and `Objective(intent)` returns `"Capture the wool!"` for one wool and
  `"Capture the enemies' wools!"` for any other count, **zero included**. So a destroy board with two
  monuments and no wool ships telling the player to capture wools that do not exist. Three boards authored in
  the trial run carry that line, and the run found it only by reading the exported `map.xml`. The map
  `ashen_quarry` shipped with it too, so it predates the run rather than being something the run introduced.

  **It is cosmetic, and the severity is worth stating exactly so nobody prioritises it as a break.** PGM
  loads the map and plays it correctly: the scoreboard resolves the real objective against the `<core>` and
  `<destroyable>` elements themselves, so a destroy board scores as a destroy board. What is wrong is the
  **first line of the scoreboard and the map description** — the sentence a map gets to explain itself with,
  read by every player who joins, saying the wrong thing on every destroy map the studio has ever written.

  What it should say follows from what the intent already carries — wools, destroyables, cores, or a
  combination, which is the ordinary corpus board — so nothing needs measuring. The declared `gamemode` wants
  the same treatment, and `MapParser` is the reference for how a gamemode falls out of which objective
  modules are present rather than from a label.

- [ ] **B132 — With no build area declared, nothing stops a player bridging the void.** `BuildGenerator.Apply`
  returns at `if (b.Areas.Count == 0) return;` — before it reaches the `no-void` filter and the
  `not-build-area` negative that carry void enforcement. So a map that declares no build area gets **no**
  `block=no-void` rule at all, and every void on it becomes bridgeable from the first minute.

  That is the exact inverse of what the void is for. `approaches.md` states it as settled law: **a void gap
  with no build region over it is permanent** — nobody bridges it, and the approach it forces is *around*,
  which is what makes a channel a control on flow rather than a delay. The same document makes crossability a
  deliberate decision made in the intent rather than in the geometry, and this makes it accidentally, in the
  permissive direction, for every board that never thought to declare an area. Both destroy boards in the
  trial run had to declare a decoy land-only build zone purely to turn void enforcement back on, which is a
  workaround for a default that is backwards.

  **The corpus settles it, and the enforcing majority is large.** Over the 112 `dtcm` maps in
  `CommunityMaps`: 102 declare a `maxbuildheight`, **68 enforce the void**, 82 carry a hard `block="never"`
  region, and **100 of 112 carry one or the other**. Nearly every real destroy map restricts building
  somewhere. Two earlier readings of this entry claimed the opposite from a grep that matched only
  `no-void` and `<void/>` and missed **`deny(void)`**, which is the dominant spelling — 31 uses as
  `block-place`, 25 as `block`. Both are withdrawn; the corpus agrees with the entry's original instinct.

  **`alpine_mining_ii` is the idiom worth copying**, and it is more precise than "a build area":

  ```xml
  <complement id="build-filter"><everywhere/><union id="obs-spawn">…</union></complement>
  <apply block-place="deny(void)" message="You may not build outside of the map!" region="build-filter"/>
  ```

  Enforcement is applied over **everywhere minus a small exclusion**, not over a drawn build rectangle, and
  it is **`block-place`, not `block`** — so a player may still *break* a block hanging over the void, which
  the map's own comment says is deliberate. Eighteen of the enforcing maps scope it through `everywhere` or
  a complement like this. A build *area* and void enforcement are therefore two different decisions in the
  corpus: the area says where the map is, and the void rule says you may not extend it.

  So the defect is narrower and more useful than "the guard is backwards": **there is no way to ask for void
  enforcement without declaring a build area**, because the two are welded together in one method that exits
  early. An author who wants a permanent channel and no build restrictions otherwise has to invent a decoy
  land-only zone — which is what both destroy boards in the run did. Decouple them: let the intent state
  void enforcement, optionally scoped to a region the way the corpus does, independently of whether any
  build area exists. Note the ordering constraint that already governs this code — the broad `no-void` rule
  allows editing any solid block and PGM stops at the first matching rule, so build must be applied last
  (`IntentGenerator`) — and keep it.

- [ ] **B130 — A malformed dressing document is discarded whole, and the export says 200.**
  `DressingJson.Deserialize` ends `catch (JsonException) { return DressingDoc.Empty; }`, commented "a
  hand-edited blob must not fail an export". So any parse error anywhere in the document — one prop's enum
  written in the case the documentation shows rather than the case the converter accepts, one polymorphic
  `kind` that is not the object's first key — throws away **every prop on the map** and the build succeeds
  with nothing to read. `DeserializeProp` swallows the same way, returning null.

  The failure this produces is the worst shape a failure can have here: an author writes fifty placed props,
  one of them has a wrong enum case, and the map builds **bare**, exports clean, passes every gate, and
  renders as a board somebody forgot to dress. Nothing names the prop, the field or the fact that anything
  was dropped. It was found by an agent authoring a board in this run, which noticed only because it went
  looking for trees it had placed and could not find them; a run that did not check would have shipped the
  bare board and reported the dressing model as not working.

  **The comment is the bug.** An export that silently ships less than it was asked for is not a kinder
  outcome than one that refuses — it is the same class of fault as a malformed map being accommodated rather
  than rejected, which `MapParser.EnsureSupported` refuses on principle and `supported-maps.md` writes down,
  and the same class as a vendored shim blanking an icon instead of failing loudly. A dressing document that
  does not parse is an authoring error, and an authoring error wants a message naming what did not parse.

  So: the parse failure surfaces rather than being swallowed, with the offending prop and field named, and
  it joins the export gate's other refusals as a 4xx carrying a rule id rather than a 500. Two things
  travel with it. The case a prop's enums are written in should be **one** case — whichever the converter
  accepts — and the documentation's examples must be in it, since a document whose examples do not
  round-trip is the fastest thing in any document to go quietly wrong. And a polymorphic `kind` that must be
  the first key is a serialization detail leaking into an authoring contract; if the reader can be made
  order-insensitive it should be, and if it cannot, the constraint belongs in `sketch.md` next to the prop
  table rather than being discovered by a 500.

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
  the interesting half because it is the one thing the plan cannot know before the relief runs — `B128` landed
  that half in the document (`float` counts from solved ground, and the marker itself may name no plan piece
  at all); what is still missing is a way to draw and drag that point on the canvas.

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

