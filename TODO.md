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
with it are settled and shared (`FEATURES.md`): *adjacent* is one predicate, `ContactGraph.Connects`, which the
fanned graph asks rather than answering its own; *a fork* belongs to a demand set and a door, because the same
hole answers differently to a side walking in and a side walking back out; and *dead ground* is one verb,
`Cells.Stretches`, which the plan tier and the built-world tier each ask at their own floor. What the group
still holds is two entries waiting on a ruling, and nothing else.

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

- [ ] **B169 — Complain about spawn ground that carries nothing and contests nothing.** *Parked on a ruling:
  what ground the complaint is measured over, and whether it replaces `SP2`'s check or joins it.* Raw size is
  not the test (author): a spawn seated on a large rectangle that *is* the map is fine, and Mirefast's 92-wide
  `steading` at least carries nine houses and two ramps. What fails is flat dead area around a spawn placed at
  the back. The rule id exists — **`SP2`**, "a spawn sits near the back of its lane, because the space behind
  a spawn is dead space" — and it composes with `ST9` (piece ≤ 20×20) and the door's approach (the first
  20×20 in front of the door kept clear), so the measure to add is *what is this ground for*, not how wide it is. The 15-block
  figure is a rule of thumb for the common case, not the rule.

  **Why it is parked.** The measure is there at both tiers — `PlanFlow.DeadPlaces` names every dead stretch
  with the pieces under it, `GroundCoverage.Patch` adds the walk to the nearest used ground — and `dead-share`
  shows how a term reads one. What is undecided is the subject. Weirgate's dead ground is on the `yard`, not
  the spawn piece, so a share of the spawn's own rectangle measures the wrong thing; a share of every piece
  touching it, and dead ground within a reach of the spawn point, each answer differently and neither is
  derivable here. And `SP2` lints the spawn *marker* into the back half of its piece — the proxy for this
  concern — so the complaint either replaces that check or sits beside it under the same id; a second `SP2`
  is the failure mode.

  *author, 2026-08-14 · Weirgate's `yard` spans `x −40…40` against a spawn piece of `x −10…10`; Mirefast's
  `steading` is 92 wide for a 20-block spawn. The corpus does not support a spawn-isolation rule: `dtcm` puts a
  spawn a median 7.5 blocks from the board edge and the generated ones sit 5–15 out.*
