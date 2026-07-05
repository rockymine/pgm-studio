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

- [~] **G32 — Composer v1 skeleton.** Landed (both in `FEATURES.md`; suite 314 green): envelope +
  team-unit grower (A) and mid carve + isolation cuts + build-zone discipline (B). Remaining:
  **G32-C markers/heights/walls** — SP3/SP4 spawn (facing absolute, raised), SP7 iron, WL5 stepped
  approach climb, EL1 palette (base 9, step 2, all-odd), ST4 walls, EL6, and set piece roles (all
  pieces render neutral until this lands); consider a CT4 mid-band spine to relieve the p30 floor.
  **G32-D gates + goldens + emit** — `PlanValidator` zero-errors with zones present, `FannedGraph`
  full traversability, stat envelopes vs `seed-stats.md`, `plan.json` loadable in `/plan`, fixed-RNG
  goldens under `tests/`. p5/rot_90 stays a known limitation until **G35**. Frozen rules are the oracle.
