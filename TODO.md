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

## The focus: the seams, and which of them bite

A **seam** is one concept implemented once and not reached from the second place that needs it. Four are known,
and every one was found by following where a fact is stored or derived rather than by reading a type:

- a house prop carries a whole style, and the dressing reader deserialized straight past the upgrade every
  standalone style ran (`B197`);
- a house style never ran the material walk at all, so a pattern stored inside one never read forward (`B195`);
- the inward walk a floor's zones are cut by exists for the housing raster and not for the terrain one, though
  both already share the perimeter trace beside it (`B200`);
- a house the dressing pass **declined to place** still claims its footprint as structure, because the claim is
  rebuilt from the author's intent instead of from the placement (`B202`).

**Only the last bites.** It is measured: two authored houses whose stamped rings overlap, one placed, two
claimed, 56 columns carrying a `Structure` claim over bare ground — and provenance is now *preferred* over the
material estimate, so a stage image draws a building that is not there and says it is certain. The other three
were correct by coincidence of maintenance. That is the whole difficulty: nothing in the type, the tests or the
documents separates a seam that bites from one that has not yet, and the four above did not fail a single test
between them.

`B203` maps the class rather than an instance — five answers to "which columns does this stamp own", four
tables for "what kind of block is this", one predicate written twice and one name covering two meanings.

**The dispatch pass is done, and it moved three tasks.** `B204` read the bucket bodies rather than their
titles and labelled each with the concept it spends and the one place that concept may land; the map is in
`BACKLOG.md` under *What each bucket spends*. Four of its five predicted labels were wrong in a way only the
bodies show: bucket 4 held three tasks that are not block-kind rules at all but one walk asked for on three
rasters, now **bucket 13**; bucket 10 is not document drift but the refusal vocabulary; bucket 9 has been
finished since before the table was written; and `B177` asked for a rule that `PlanValidator.LintSp2` already
implements, so an agent handed it as written would have shipped the second copy.

**What that leaves as the order.** Buckets 6 and 7 spend occupancy and wait on `B202` — dispatched before it
they entrench the fault it names. Buckets 1, 2, 3 and 10 all land in `PlanValidator` and go to one agent or
strictly in sequence. Buckets 4 and 5 share the block table. Bucket 13 wants its author call answered first.
Buckets 8, 11 and 12 share nothing and may run at once.

## Backend, pipeline & internals (B / P / A)

**Two findings, and the live-defect hunts against the same surfaces.** `B202` is the seam that bites and
`B203` is the class it belongs to; both are now the gate on eight of the bucketed forty-eight rather than
findings standing on their own. Under them: a building that is solid behind its facade, leaves lying inside
one, a stale chunk surviving a rebuild, and the two names that still cover two meanings each.

- [ ] **B202 — Provenance claims a building the dressing pass declined to place.** `Decorator.PlaceHouse`
  drops a house **whole** — every orbit image of it — when any image overlaps something already standing
  (MG7's drop), when any image has no ground under it, or when a turn fails. `DressingScope.StructureFootprints`
  rebuilds the same footprints from the layout JSON afterwards to make the provenance claim, and it takes
  `layoutJson` alone: no world, no `taken` set, no ground. It cannot know what was dropped, so it claims
  every authored house on every image regardless.

  **Measured** (`scratchpad/claimcheck.cs`, flat stone plateau at y=8, two authored houses whose stamped
  rings overlap, `mirror_mode: none`):

  | | authored | placed | claimed |
  |---|---|---|---|
  | houses | 2 | **1** (`tally.Houses`) | **2** (`house:a:0`, `house:b:0`) |

  | owner | cells claimed | with anything standing on them |
  |---|---|---|
  | `house:a:0` | 81 | 81 |
  | `house:b:0` | 81 | **25** — and those 25 are `a`'s blocks in the overlap |

  So 56 columns carry a `Structure` claim over bare ground. Three of them: `(18, 13)`, `(19, 13)`, `(20, 13)`
  — top block stone (1), nothing above y=8.

  **It matters because provenance is now preferred over the material estimate.** `StructureFinder` partitions
  candidates by owner when a record exists and drops the material+step reading entirely; `TopDownRender` prints
  "STRUCTURE READING: RECORDED PROVENANCE". So a stage image draws a building that is not there and the finder
  reports a structure with no blocks — and both say they are certain.

  **The fix is the direction of the derivation, not a filter.** `Decorator.Decorate` already knows exactly
  which cells it stamped; it returns a `DressingTally` of *counts*. Have it report the placed footprints and
  claim from those, rather than re-deriving the same fact from the author's intent — the record should come
  from the placement, never beside it.

  **And the rule is already written down, one function above the defect.** `DressingScope.GoalGroundAt` takes a
  goal's ground from *"the box the stamper wrote where there is one … by construction rather than by two
  derivations agreeing"* — the right direction, stated in a docstring, in the same file as
  `StructureFootprints`, which does the opposite. `SketchWorldBuilder` rebuilds a claim beside its stamp four
  more times (the room floor, the wall, the redstone line, the goal box) and shares a footprint function with
  its stamper exactly once, for the iron cube. So this is not a design that has to be invented, and the fix's
  reach is the whole file rather than the house case: `B204` files it as the **occupancy** concept, which
  buckets 6 and 7 both spend and neither may be dispatched before this lands.

  *found in the provenance dive, 2026-08-15 · `Decorator.PlaceHouse` · `DressingScope.StructureFootprints`.*

- [ ] **B203 — "Which columns does this stamp own" has five answers, and "what kind of block is this" has
  four tables.** Both are one question asked in several places, each place deriving it its own way.

  **Which columns a stamp owns:** the stamper itself (it places the blocks); `provenance.ClaimRect` beside it
  in `SketchWorldBuilder`; `DressingScope.ProtectedAt`'s keep-out mask; `TerrainProfile`'s paint gate (a column
  whose top block *is not stone* is a structure); and `DressingScope.StructureFootprints` for houses (`B202`).
  The room-floor case has the same rectangle converted from doubles twice, five lines apart, by two rules —
  `(int)f.MinX` in the stamp against `Math.Floor`/`Math.Ceiling` in the claim. Latent rather than live, since
  `PlanCompiler` fans integral rects today; `CLAUDE.md`'s own trap entry says this class has already cost hours.

  **What kind of block this is:** `BlockRoles` (7 predicates, scan-side roles), `BlockKinds` (6 predicates,
  what a house field may name), `DressingPalette.IsStamp` (what the dressing pass may not touch), and
  `BlockPalette` (colour). `IsLog` is written in both of the first two — `blockId is 17 or 162` against
  `Logs.Contains(id)`, agreeing today by coincidence of maintenance. `IsGround` and `IsNaturalGround` share a
  stem and not a meaning: the first is a closed list of six earth ids a roof may not be laid in, the second is
  everything left after built, liquid, log and grown are removed. A caller reaching for "is this ground" has to
  know which, and nothing says so at the call site.

  **The fix pattern is already in the repo, one scale down.** `DressingPalette.IsStamp`'s docstring: *"Stated
  once, here, because two passes ask it... Two lists would drift and the drift would show as a road eating a
  monument."* That is exactly right and exactly what the four tables above have not done.

  *found in the provenance dive, 2026-08-15 · `BlockRoles` · `BlockKinds` · `TerrainProfile` ·
  `DressingScope` · `SketchWorldBuilder`.*

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

