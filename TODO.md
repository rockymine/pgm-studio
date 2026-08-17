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

A **seam** is one concept implemented once and not reached from the second place that needs it. Four were
found by following where a fact is stored or derived rather than by reading a type, and all four are closed —
`B197`, `B195`, `B200`'s model half, and `B202`. Only the last bit: two authored houses whose stamped rings
overlap, one placed, two claimed, 56 columns carrying a `Structure` claim over bare ground — and provenance
is *preferred* over the material estimate, so a stage image drew a building that was not there and said it
was certain. The other three were correct by coincidence of maintenance, and the four did not fail a single
test between them.

**The rule that came out of it**, written where the next pass meets it: `StructureClaim` — *a claim is taken
from the placement, never rebuilt beside it* — held by two regressions, a building dropped for overlapping
one already standing and a building authored over void, both claiming nothing.

The audit pool is bucketed for dispatch in `BACKLOG.md`, and every bucket's landing site is built. One open
question governs bucket 1 and it is the author's (`B212`): a distance is the **walk over the walkable
surface, never the straight line**, so bucket 1's thresholds — `B175`'s 35 blocks, `B179`'s 95–110 — were
read in a retired unit and want restating before anything enforces them.

## Backend, pipeline & internals (B / P / A)

- [ ] **B106 — Rename one of the two things called protection.** One is the XML region rule that stops a
  player entering a spawn or a wool room and restricts what may be broken or placed inside it — a gameplay
  contract. The other is `Decorator.IsProtected`, "cells nothing may be placed on", a dressing keep-out with
  no gameplay meaning. A goal that needs the second does not need the first, and one word for both invites the
  inference that a destroyable must live somewhere protected — which is what produced the caged goals, and it
  survives the code that acted on it.

- [ ] **B92 — Give `HouseStyle` a fill material, so a building can be a mass rather than a place.**
  `HouseStamper` leaves the volume its walls enclose as air, which is right for a village and wrong for a
  scenery building that is not enterable and for a run of buildings sealing the edge of the board — the only
  way scenery does the work of a boundary in a mode where nothing may be placed. **The facade is kept**: the
  windows and door stay where they are and the fill sits *behind* them, so a window reads as an unlit interior
  rather than a hole into rock. A dark fill (black wool) is the idiom, which is why it is a knob and not a
  constant. A style field rather than a stamper flag, so a style carries whether it is a place or a mass.

  Two things to settle: whether the fill respects the storey stack (a building filled to its top course and one
  filled to its first floor are different buildings), and how deep behind an opening the fill starts (flush and
  one course back read differently through the gap). `DressingScope` already protects the ground under a
  stamped building, so nothing downstream needs teaching.

- [ ] **B102 — Clear the region directory before a rebuild writes it.** `AnvilRegionWriter.Write` calls
  `Directory.CreateDirectory` and nothing else, so every `.mca` a previous build left is still there and a
  chunk the new build does not touch — because its geometry moved — is read back as part of the new map. That
  makes rebuilding into an existing `out_dir` untrustworthy, which is exactly what iterating on a spec does,
  and contradicts the README's promise that "the same spec rebuilds the same map, so two runs can be
  compared". It cost a design session real time, presenting as building counts that could not be reconciled
  until the directory was deleted by hand. Distinct from the concurrent-build race `CLAUDE.md` warns about:
  that one is two builds at once, this one is one build after another.
