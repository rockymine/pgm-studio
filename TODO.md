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
Build order: the persistence foundation → browse → verdicts → duels (G119 → G117 → G118 → G120);
the pipeline showcase page shipped first (G121, `FEATURES.md`). The design long
tail this focus deliberately displaced is condensed in **`docs/layout-generation-ideas.md`** (ids
preserved — pull one back here when it becomes the focus).

**Persistence doctrine for the whole theme: the feed is ephemeral; only human attention persists.** A
plan enters the database exactly when it is voted on, pinned, or saved from the editor — never while
scrolling. Generated rows are **immutable**: editing one forks a new `authored` row with a `parent_id`
back-reference, so the labeled corpus cannot be contaminated after the fact. Browse votes (absolute)
and duel results (pairwise preference) are **separate datasets**, unified only at analysis time. The
hold tray persists across reloads — pinned *means* persisted.

- [ ] **G119 — The plan store (the persistence foundation).** MariaDB `plan` table (FluentMigrator +
  linq2db): plan JSON, `origin` (generated | authored | imported), the canonical request descriptor +
  seed + composer version on generated rows, `parent_id` (fork provenance), `content_hash` (dedup +
  import identity), timestamps. The plan editor gains DB save/load and an open-from-DB browser; file
  import/export stays unchanged (files remain the user-to-user sharing path). Editing a generated plan
  **forks, never mutates** (doctrine above). Prerequisite for G117/G118/G120's persistence.

- [ ] **G117 — Browse mode (the interactive generator in the studio).** A compose API endpoint
  (request params + a seed cursor; the server composes ahead, runs the derivers, and ships only cards
  passing the **sieve filters** — wool families, hub/frontline/mid forms, wool count, size, score
  threshold) + a studio gallery page: infinite scroll; the filter panel (greying out what the composer
  cannot yet produce — 4-team `rot_90`, `mirror_x`, the scythe); quick up/down votes; the **hold
  tray** (pin a card → persists via G119, survives reload; unpinned cards are simply forgotten); and
  the **detail dialog** (large render, score breakdown with top contributing terms, copyable
  descriptor, vote/tag controls, Open-in-plan-editor — reserving the slot where the 3D preview lands
  once elevation exists). Card identity is the **canonical versioned request descriptor** (schema +
  composer version + every request field + seed) — reproducible within a composer version, honest
  across them. Filters that prove popular are the promotion queue into held `ComposeTargets` (ideas
  doc, G98).

- [ ] **G118 — Verdict collection.** Tap-chip annotation tags (large toggleable pills, multi-select —
  never checkboxes) available on both vote directions, both optional; the tag set seeded from the
  layout-rules vocabulary (wools-too-close · wools/spawns-should-swap · flat-front · crammed-mid ·
  no-rotation · great-hub · …, extendable), each tag carrying its rule id where one exists — a
  downvote tagged with a rule whose term did *not* fire is a ready-made evaluator bug report. Persist
  {plan ref, descriptor, verdict, tags, free-text note, evaluator score + per-term snapshot, evaluator
  version} via G119; JSONL export so the labeled examples drive rule refinement, envelope
  regeneration, and AI-assisted analysis.

- [ ] **G120 — Duel mode (the tournament).** Bucket-scoped side-by-side comparison: a **bucket** is a
  filter combination (e.g. 2 wools · F frontline · double-hole hub · one L + one donut), so both
  boards made broadly the same structural decisions — the closest thing to a controlled comparison,
  and a minimal-pair factory for the evaluator's labeled set. Two big renders, pick the better; the
  result is a **preference pair** `(winner, loser, bucket)` — never converted into a downvote — with a
  per-bucket ranking (Bradley-Terry/Elo-style) derived at analysis time. A separate dataset from the
  browse votes by design.
