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

- [~] **G32 — Composer v1 skeleton.** Landed so far: envelope + team-unit grower (`FEATURES.md`).
  Remaining: carve the **clean** mid (one merged build region + CT7-aligned stones thinning per CT4;
  G5/G7 hop arithmetic; CT8 closure holes as the default), CT5 isolation cuts (~40% motif), then
  markers/heights/walls (SP4/EL1/WL5/SP7/ST4), gate on `PlanValidator` zero-errors + `FannedGraph`
  traversability + the `seed-stats.md` envelopes, emit `plan.json` loadable in the plan editor with
  fixed-seed goldens. The frozen rules are the acceptance oracle.
