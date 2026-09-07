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

**The mapgen-authoring programme was read against the code and drained to one entry (author, 2026-09-07).**
Four of its five were withdrawn rather than worked, each for a reason the reading could state: `B262` and
`B265` asked for surfaces over reads nobody takes that way, `B150` for an evaluator tier the context cannot
carry, and `B171` for a vocabulary that does not describe a hand-drawn board. What survives is the one
measured defect, and `docs/backlog-strategy.md` § *Withdrawal is a legitimate answer* carries what each
withdrawal cost. Anything found while working goes to `BACKLOG.md`.

## What a gate says when it refuses

- [ ] **TN2 — `/plan/evaluate` answers `STRUCT` where the validator named a rule, and one sentence where
  several fired.** The term folds every `PlanValidator` refusal into one hard violation whose finding carries
  `StructuralIntegrity.Rule` — the sentinel `STRUCT` — so the real `PL` id is dropped on **every** refusing
  plan, and `GET /api/rules?rule=STRUCT` answers empty (six letters miss `RuleCatalog`'s
  `^[A-Z]{2,3}(?:-[A-Z]+|[0-9]+)$`), leaving a driver's rule lookup nothing to read. Where more than one fired
  the message is `"{n} structural errors ({first})"` and the rest are dropped; `SubjectIds` is the flat union,
  so four `PL4` pairs arrive as six unpaired piece ids. The endpoint's own other branch already answers the
  right shape — `PlanInspectEndpoints.Structural()` emits one `ViolationDto` per refusal carrying the
  finding's own rule — so one endpoint answers two shapes for one class of fault; make the non-empty branch
  agree. `StructuralTerms.cs:21-32`, `PlanInspectEndpoints.cs:362-380`, and the `/plan/evaluate` row of
  `docs/tools/plan.md`.

  *measured over the 94 plan documents in `pgm-studio-mapgen/specs`, lifted to shape version 2: six refuse,
  all six lose the id; `sandscar-complex` folds four `PL4` overlaps (deltas 10, 5, −5, 3) into one sentence
  and `tanglewold` two `PL13` walls.*
