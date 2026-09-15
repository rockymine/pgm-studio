# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Three numbers are the author's and are not to be re-derived.** A protection region is at most **20×30**
blocks (`ST10`), a building footprint at most **20×20** (`ST9`), and the smallest room with no building over
it is **4×4** (`WX2`). A dressed prop's 192-cell ceiling (`HP3`) and a room building's 20×20 measure the same
concept since `WE71`, and holding them apart is a deliberate not-yet.

## Distance, and the walk every measure is taken with

`Geom.Walk` is the traversal now — eight-connected and octile, charging a climb in the blocks a player places,
counting a fall, slowing through water, narrowed per team where an `enter` rule bars one — and it runs over a
set that reads a surface as somewhere a player can stand rather than as any column holding a block.

**The walk is one; what is asked of it is not, and that is the ground this group spends.** Three answers taken
with it are each written twice or written in a shape that cannot carry the truth. *Adjacent* is settled
(`FEATURES.md`): one predicate, `ContactGraph.Connects`, which the fanned graph asks rather than answering its
own. Two are left. *Dead ground* is derived by `PlanFlow.DeadPlace` at plan tier and `GroundCoverage.Patch`
over a built world — the same four fields in two projects that cannot see each other, so the only place the
shared half can go is `Geom`. And *a fork* is one pair of cells in `RouteFork` where a board carries a set,
with the singular baked into seven fields of `FlowLeg` and into the prose `PlanFlow.Describe` writes.

- [ ] **WE45 — `DR-PASS` still takes the widest side, and its width is absolute.** *Parked on a ruling: the
  number, and what "every side" exempts.* The wrong-rectangle fault is fixed (`FEATURES.md`). What is left is
  that the rule passes on **one** clear flank, so a building with three sides open and a two-block ledge on the
  fourth stands; and that five blocks is absolute, so a twenty-block passage with a fifteen-wide house in it
  leaves five and passes, which the author has ruled is not a way past.
  `docs/world-export/decoration.md`.

  **Why it is parked.** `DR-PASS` is a `Decline` — the building is dropped from the exported world — and the
  entry's own measurement says every-side at five fails 76 of 122 buildings. Ten would fail more. Three things
  have to be settled together: the depth; whether a flank over **void** is exempt (the rule text promises "a
  coast house is a house", and today `Band` fails a flank with any missing ground, so `&&` alone declines every
  coast house); and whether the verdict stays a `Decline` at that hit rate or becomes a `Complaint`.

  *122 buildings on 32 boards: 4 fail today. A side with ground and under 3 clear blocks fails 51, under 5
  fails 76. `whinnymoor/hut-w` reads E=24 W=23 S=2 N=22.*

- [ ] **WS3 — A board has fork points, plural, they belong to a demand set, and `RouteFork` reports one.**
  `PlanRoutes.Fork` takes the last cell common to *every* option and the first common to every option from the
  target, so a journey with several decision points lands a split between them and describes none of them.
  Compute a branch point **per option pair** — the last cell that pair shares — and report the set, each with
  the pair it separates and how long the choice is live.

  **And a fork is not a property of the board.** townside carries three (author): one leaving spawn, round the
  hole the build zone frames; one at the second hole by the wool; and a third at *that same hole* for the run
  back out with the wool, which is a different choice over the same ground. So a fork has to be reported
  against the demand set it was read for — attack, defend, or the back-run — and the same hole can answer
  differently for each.

  **The demand set states its own ground** (author). The back-run is its own journey rather than the attack
  reversed. A **defender never leaves their own half**: the defend walk runs over the board with the enemy's
  piece images removed and their own build zones kept, so a hole the enemy owns is not a door of theirs — on
  the eight example plans that drops example 8's defend from three routes to two and example 7's from two to
  one. And a room is crossed only by whoever may: an **attacker** may run in one side of a corner wool room
  and out the other, a **defender** may not cross the room they defend, and a **spawn** is walked through by
  nobody. The last two already hold on those plans; the first keeps 12 of example 3's 36 attack ways.

  **A defender rounds only a hole that is theirs**, which `BoardDeriver` already classifies: `encased` (a
  bubble in one team's landmass) and `gap` (their own isolation cut) are theirs; `frontline` and `middle` are
  rounded by walking the neutral crossing and are not. Filtering the doors by that class and confining the
  ground to their own pieces plus non-crossing build agree on all eight plans, so either states it — example
  1's defend drops from two ways to one at 79 blocks, example 7's from two to one.

  **Exclusion is for what cannot happen; everything else is ranked** (author). A long way round on a team's
  own ground stays in the report, sorted by distance, because **a defender is not only someone who just
  spawned** — a player at the frontline who sees an attacker and gives chase is defending from a different
  origin. So the read weighs example 5's 133 and example 3's 103, and never hides them.

  *measured on townside: the attack passes three holes and the one fork spans (25,−40) → (15,105), 177 blocks
  of a 266-block walk. Read per door it is two decisions — the mid crossing at (0,0) live over 123 blocks at
  1.12×, and the hole by the wool at (0,85) live over 59 at 1.08× — and the third door is moot against the
  shortest, which is the reference question below.*

  **The narrow middle is not a funnel and must not be scored as one.** The two teams' median lines run 35–50
  blocks apart through it and converge only at the objective: the crossing carries two ways and neither team
  chooses between them, the same one-per-team partition ingwaz shows. Per-team spread cannot separate *one
  way* from *two ways, one each*.

  **The defend journey has no route read at all.** `PlanRoutes.Read` walks to the objective, and a defence may
  not enter the room it defends, so a defend read answers nothing where the distance answers a doorstep
  (`PlanFlow.ReachDoorstep`). The route read wants the same target: the nearest cell of their own ground
  touching the room.

- [ ] **B169 — Complain about spawn ground that carries nothing and contests nothing.** Raw size is not the
  test (author): a spawn seated on a large rectangle that *is* the map is fine, and Mirefast's 92-wide
  `steading` at least carries nine houses and two ramps. What fails is flat dead area around a spawn placed at
  the back. The rule id exists — **`SP2`**, "a spawn sits near the back of its lane, because the space behind
  a spawn is dead space" — and it composes with `ST9` (piece ≤ 20×20) and the door's approach (the first
  20×20 in front of the door kept clear), so the measure to add is *what is this ground for*, not how wide it is. The 15-block
  figure is a rule of thumb for the common case, not the rule.

  **`GroundCoverage` answers this directly once it is honest.** *Dead* is already exactly "ground with no
  route through it, no objective near it and nothing on it", named per patch with an area, a centroid and a
  walk to the nearest used ground — which is the measure this entry asks for, phrased as the picture rather
  than as a width. The walk it draws corridors with now prices a climb and a crossing and knows which
  ground is granted, so what remains is the picture itself rather than the measure under it.

  *author, 2026-08-14 · Weirgate's `yard` spans `x −40…40` against a spawn piece of `x −10…10`; Mirefast's
  `steading` is 92 wide for a 20-block spawn. The corpus does not support a spawn-isolation rule: `dtcm` puts a
  spawn a median 7.5 blocks from the board edge and the generated ones sit 5–15 out.*

- [~] **G187 — A flow reading the evaluator can fire.** The evaluator's 29 terms walk the surface for
  distances (`SurfaceNav`), and `Evaluate/` cites neither `PlanRoutes` nor `PlanFlow` — so no flow answer
  scores anything. A **dead-share** term wants writing over the answer `PlanFlow.Read` already gives, at
  `POST /plan/evaluate`: the first call in the loop, before a map row exists. It lands in `Evaluate/Terms`
  beside the others, and it is what makes `G164` a short consumer rather than a project.

  *`PlanFlow.Result.DeadShare` is computed and served as prose today, and nothing reads the number.*

- [ ] **G164 — interference: how much of one side's route the other side's route covers.** Every flow
  measure so far reads one traversal at a time, and a single route cannot express tension. Tension is two
  corridors laid over each other: the attacker pushing from a captured wool room toward the remaining
  objective, and the defender travelling from spawn to the same objective. The measurable is the fraction of
  the defender's corridor that the attacker's corridor also covers, computed on the cell mask the same way
  the corridors already are. Measured over 453 two-wool boards at `marker-id-1`: median **34%**, half or more
  on 27%, and **no board reaches zero** — passing the reinforcement lane is unavoidable on generated output.
  This is the term that gives a hub void a purpose the ways-round-a-void count cannot: on a holed hub the
  near way leaves 76% interference and the far way 37%, and the far way measurably reduces the collision on
  74% of the boards offering one, so a layout whose two ways collide equally has bought nothing. Derive side
  belongs beside `BoardDeriver`; the term belongs in `Evaluate/Terms`. It reads a pair of routes rather than
  one, so the origin "a captured wool room" comes from G168's post-capture state — until that exists,
  computing it once per wool treated as captured is the honest stand-in. Background and the full numbers:
  `docs/gameplay/match-flow.md` §2, §4.9.
