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

`BACKLOG.md` groups the open work by the concept it spends — the house, distance, the audit's remainder —
and every group's landing site is built. The one open question that governs a whole group is the author's
(`B212`): a distance is the **walk over the walkable surface, never the straight line**, and the walk under
every measure is still flat (`B246`), so the thresholds stated in it want restating before anything enforces
them.

## Backend, pipeline & internals (B / P / A)

- [ ] **B106 — Rename one of the two things called protection.** One is the XML region rule that stops a
  player entering a spawn or a wool room and restricts what may be broken or placed inside it — a gameplay
  contract. The other is `Decorator.IsProtected`, "cells nothing may be placed on", a dressing keep-out with
  no gameplay meaning. A goal that needs the second does not need the first, and one word for both invites the
  inference that a destroyable must live somewhere protected — which is what produced the caged goals, and it
  survives the code that acted on it.

- [ ] **B102 — Clear the region directory before a rebuild writes it.** `AnvilRegionWriter.Write` calls
  `Directory.CreateDirectory` and nothing else, so every `.mca` a previous build left is still there and a
  chunk the new build does not touch — because its geometry moved — is read back as part of the new map. That
  makes rebuilding into an existing `out_dir` untrustworthy, which is exactly what iterating on a spec does,
  and contradicts the README's promise that "the same spec rebuilds the same map, so two runs can be
  compared". It cost a design session real time, presenting as building counts that could not be reconciled
  until the directory was deleted by hand. Distinct from the concurrent-build race `CLAUDE.md` warns about:
  that one is two builds at once, this one is one build after another.
