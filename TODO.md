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

## Layout generation (G) — current focus: the composer

Phase 2 of plan-then-realize: the rule-based composer generating `plan.json` seeds under the **frozen**
rule set (`docs/contracts/layout-rules.md`, v3) in the §3 order of `docs/contracts/layout-generation.md`
— compose the closure, assign team sides, carve the mid (clean/parallel/hash), cut the team sides,
validate, compile through the existing seed chain.

- [ ] **G32 — Composer v1 skeleton.** Deterministic-seeded generator producing valid small plans:
  globals from a target player count (teams/symmetry; land budget via the G8 coupling), grow the team
  unit by attachment rules (spawn back-of-lane, wool on a separate lane, G2/G3 dims), carve a **clean**
  mid (v1 form; hash/parallel later), apply CT5 isolation cuts where drawn, place markers/walls, gate on
  `PlanValidator` + `FannedGraph` traversability, emit `plan.json` loadable in the plan editor. The
  frozen rules are the acceptance oracle.
