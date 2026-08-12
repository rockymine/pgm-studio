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

## Backend, pipeline & internals (B / P / A)

- [ ] **B43 — Retire the Python-oracle parity harness.** The project began as a port of the Python
  `pgm-map-studio` and still carries a parity harness that regenerates Python "oracles"
  (`parser.parse + serializer.to_dict` over the corpus at `/media/sf_repos/pgm-map-studio` into `/tmp/pyfresh`)
  and diffs the C# derivations against them. That reference is deprecated dead weight: the C# feature set
  overtook it long ago (the `map.xml`-contract `--parity` was already dropped, B30), and comparing every
  refactor against a frozen, out-of-date oracle makes safe changes look risky and blocks cleanups (it just
  did — the grid-algorithm consolidation, fix 1). Remove the four `*Parity` modes from
  `tools/PgmStudio.RoundTrip/Program.cs` (`--categorize` / `--buildability` / `--traversability` / `--wool`)
  and their regenerate-from-Python scaffolding; the project-native modes (`--extract` / `--islands` /
  `--scan-out` / `--authoring` / `--monument-slices` / …) stay. **Keep the concept — a regression / golden
  harness — but re-home it as project-native** (C# goldens or fixtures over the corpus, no second framework).
  Then sweep the residue: the `/media/sf_repos` + `/tmp/pyfresh` mentions and the parity paragraph in
  `CLAUDE.md` (Verification & gotchas) and the docs, and the "Port of X.py" / "matches scipy.ndimage" attribution
  comments in the ported C# (`Analysis/IslandDetector`, `Analysis/Traversability`, …)
  that the code-comments rule already bans. `appsettings.Development.json`'s `MapsRoots`/`Import.Root` point at
  the reference VM but are import-only — repoint or document, don't leave dangling.

## Sketch tool (S) — current focus: relief in the hand

A relief can now be seen and stated: the solver and its wire document, the rasterizer seam, the recompile
rule, the contour overlay, and a Relief phase whose five tools place the four mark kinds and the push, each
with a list row, an inspector and its own point grips (`FEATURES.md`). The model is reachable by hand and by
mouse, and what it produces is on screen while it is being stated.

What is left is the two interactions that make editing feel **live** rather than iterative: dragging a
contour to state a height (S53), which is what would make how a surface reads and how it is edited one
object; and resuming the solve on a drag instead of restarting it (S52). Both are about the loop between an
edit and its result, which is now the slowest thing about the phase. The design, its measurements and the
authoring plan are `docs/contracts/sketch-relief.md`; the prototype every figure comes from is
`tools/relief`. What this theme deliberately leaves parked is in `BACKLOG.md` — the readback (S43), erecting
shapes (S44), the stroke tools (S46) and per-shape participation (S48).

- [ ] **S53 — Relief: drag a contour to state a height.** The contour overlay is read-only and the marks are
  placed with their own tools (`FEATURES.md`); the interaction that joins them is missing. A contour dragged
  to a new height is a **line mark at that height** — which is what makes how the surface reads and how it is
  edited one object, rather than a picture beside a form. It belongs to the overlay rather than to a sixth
  tool: the line is already on screen and already carries its level, so what is needed is a hit test against
  the returned polylines, a drag that reads out a height, and the mark it writes on release. The awkward part
  is which contour a drag grabs when several run close together on a steep face, and the honest answer is
  probably the nearest *index* line, since those are the ones drawn heavily enough to aim at.

- [ ] **S52 — Relief: a drag resumes the solve instead of restarting it.** Every preview solves from flat.
  That is right for an edit that settles — the cascade already makes a whole map 325 ms — and wrong for a
  drag, which is a preview per frame. The solver takes a **warm start** and it is measured: resuming from the
  surface already on screen needs 40 sweeps against a cold solve's full run, 228 ms on a 192×128 map, and
  lands off by one block on 1,614 of its 24,576 cells. What is missing is the seam — the endpoint has nowhere
  to keep the previous continuous field, and the client has no way to say "this is the same relief, moved".
  Now worth having: marks and pushes are dragged by their bodies and by their point grips, so there are drags
  to resume, and each of them currently pays for a whole cold solve per settle.

## Layout generation (G) — the generator in the studio

The box pipeline is now **the** composer (the old grower path is retired — `FEATURES.md`), and the
emitted layouts are good enough to work *with*: the bottleneck has moved from the grammar to the
feedback loop. A standalone gallery script with a handful of seeds gives the author no control over the
variables and no way to record judgments. This theme integrates the generator into the studio itself —
compose interactively, filter what to see, and **collect annotated keep/discard verdicts** that become
the labeled positive/negative corpus every later refinement (rules, envelopes, AI passes) feeds on.
Build order: the persistence foundation → browse → verdicts → duels (G119 → G117 → G118 → G120); the
showcase (G121), the persistence foundation (G119), browse mode (G117), its structural sieve
(G128 — form/family filters) and the shape catalog page (G144) have shipped — see `FEATURES.md`. The
catalog lands ahead of verdicts on purpose: it is the reference surface for the vocabulary the tags are
written in, and its measured class counts are what make per-bucket coverage tractable later.
**Verdicts (G118) is next**, and it now
owns the up/down votes deferred out of browse (the browse pin is the only persistence action so far,
and the structural bucket key it stores is G118's verdict column / G120's duel bucket). The design long tail this focus deliberately
displaced is condensed in **`docs/generator/ideas.md`** (ids preserved — pull one back here
when it becomes the focus).

**Persistence doctrine for the whole theme: the feed is ephemeral; only human attention persists.** A
plan enters the database exactly when it is voted on, pinned, or saved from the editor — never while
scrolling. Generated rows are **immutable**: editing one forks a new `authored` row with a `parent_id`
back-reference, so the labeled corpus cannot be contaminated after the fact. Browse votes (absolute)
and duel results (pairwise preference) are **separate datasets**, unified only at analysis time. The
hold tray persists across reloads — pinned *means* persisted.

- [~] **G159 — a composed plan should carry its voids before it is compiled.** The compiler declares them on
  every compile (`PlanVoids`, `FEATURES.md`), so a board's holes are correct wherever it is built. What a
  freshly composed plan does not yet carry is the declaration itself: `Composer.Compose` returns pieces only,
  and the buffers appear when something compiles it. Running the same step at the end of `Composer.Assemble`
  makes a generated plan self-describing from birth — one line, no new geometry, and it cannot disagree with
  the compiler because it is the same step. The cost is the reason it is not folded in already: the composer
  fingerprint digests the plan JSON, so every board's digest moves, which means a `ComposerVersion` bump and a
  re-record of `tools/compose/composer-fingerprints.json`. Worth doing on the next version bump rather than
  spending one on annotation.

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
