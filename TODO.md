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
- a house the dressing pass **declined to place** still claimed its footprint as structure, because the claim
  was rebuilt from the author's intent instead of from the placement (`B202`, **shipped**).

**Only the last bit, and it is fixed.** It was measured: two authored houses whose stamped rings overlap, one
placed, two claimed, 56 columns carrying a `Structure` claim over bare ground — and provenance is *preferred*
over the material estimate, so a stage image drew a building that was not there and said it was certain. The
other three were correct by coincidence of maintenance. That is the whole difficulty: nothing in the type, the
tests or the documents separates a seam that bites from one that has not yet, and the four did not fail a
single test between them.

**What `B202` leaves behind is the rule, written down where the next pass will meet it.** `StructureClaim` —
a claim is taken from the placement, never rebuilt beside it — with the two regressions that hold it: a
building dropped for overlapping one already standing, and a building authored over void. Both claim nothing.
`B203` was the rest of the class and has followed it, with a second live instance found on the way out: a room
floor's claim ran one column past its own bedrock on each axis, 169 claimed against 144 filled, on every wool
room the studio has built. The entry's own text said that site was latent, on the grounds that `PlanCompiler`
fans integral rects — which is exactly what made it live.

**The dispatch pass is done, and it moved three tasks.** `B204` read the bucket bodies rather than their
titles and labelled each with the concept it spends and the one place that concept may land; the map is in
`BACKLOG.md` under *What each bucket spends*. Four of its five predicted labels were wrong in a way only the
bodies show: bucket 4 held three tasks that are not block-kind rules at all but one walk asked for on two
rasters plus a continuous axis, now **bucket 13**; bucket 10 is not document drift but the refusal vocabulary;
bucket 9 has been finished since before the table was written; and `B177` asked for a rule that
`PlanValidator.LintSp2` already implements, so an agent handed it as written would have shipped the second copy.

**What that leaves as the order.** Buckets 6 and 7 spend occupancy and were waiting on `B202`; that has
landed, so they are dispatchable, and both should adopt `StructureClaim` rather than adding a claim of their
own. Buckets 1, 2, 3 and 10 all land in `PlanValidator` and go to one agent or strictly in sequence — `B206`
has cut that class to one verb so they do not each pick a different one. Buckets 4 and 5 share the block table
and it exists: `BlockFamilies` names each id family once, so a rule about what a field may hold is a lookup
rather than a sixth list. Bucket 13's author call is answered: **the walk crosses an elevation step.** Buckets
8, 11 and 12 share nothing and may run at once.

**Every bucket now has its landing site built, and the landscape work is done.** `Findings`/`Check` for the
refusals, `StructureClaim` for occupancy, `BlockFamilies` for block kind, one `PlanValidator` verb, and
`BuildingPlan` renamed clear of `Geom.Footprint` so bucket 13's lift has somewhere to go. The one piece of
shared machinery still to build is bucket 13's own: the inward walk lifted into `PgmStudio.Geom.Algorithms`
with `ColumnProfile` carrying an `Inset` beside its `PerimeterArc`.

## Backend, pipeline & internals (B / P / A)

**The live-defect hunts against the surfaces the seams ran through**: a building that is solid behind its
facade, leaves lying inside one, a stale chunk surviving a rebuild, and the two names that still cover two
meanings each.

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

