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

## Layout generation (G) — current focus: the generator in the studio

The box pipeline is now **the** composer (the old grower path is retired — `FEATURES.md`), and the
emitted layouts are good enough to work *with*: the bottleneck has moved from the grammar to the
feedback loop. A standalone gallery script with a handful of seeds gives the author no control over the
variables and no way to record judgments. This theme integrates the generator into the studio itself —
compose interactively, filter what to see, and **collect annotated keep/discard verdicts** that become
the labeled positive/negative corpus every later refinement (rules, envelopes, AI passes) feeds on.
The design long tail this focus deliberately displaced is condensed in
**`docs/layout-generation-ideas.md`** (ids preserved — pull one back here when it becomes the focus).

- [ ] **G117 — Interactive generator in the studio.** A compose API endpoint (players/board size,
  symmetry, seed; reroll) + a studio page rendering composed boards the way the board gallery does
  (pieces, band, markers, holes, per-board evaluator score with top contributors) — compose on demand
  instead of a pre-baked HTML file. Controls grow toward held `ComposeTargets` (ideas doc, G98): the
  filter panel ("which hub forms / frontline shapes / wool counts do I want to see") is that mechanism's
  UI face — start with what the request already carries (players, symmetry, seed) and add held targets
  as they land.

- [ ] **G118 — Verdict collection (the swipe loop).** Keep/discard on each composed board, with preset
  annotation tags for *why* (e.g. wool-too-remote · no-rotation · flat-front · crammed-mid · great-hub —
  the tag set seeded from the layout-rules vocabulary and extendable) plus a free-text note. Persist
  {plan JSON, seed/request, verdict, tags, evaluator scores} in MariaDB, and export the collection
  (JSONL) so the labeled examples can drive rule refinement, envelope regeneration, and AI-assisted
  analysis. The Tinder-style flow: fast to judge, every judgment retained.
