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

The relief model is built and headless: the solver, its marks and pushes, the symmetry fold, the wire
document, the rasterizer seam and the recompile rule have all shipped (`FEATURES.md`). A relief written into
a stored layout by hand exports as terrain today. **Nothing authors one** — there is no mode, no tool, no
inspector and no way to see the surface before it is built, so the whole model is reachable only by editing
JSON. This theme is that half, in the order each step unblocks the next: see it (S45) → place marks (S41) →
draw pushes (S50). The design, its measurements and the authoring plan are
`docs/contracts/sketch-relief.md`; the prototype every figure comes from is `tools/relief`. What this theme
deliberately leaves parked is in `BACKLOG.md` — the readback (S43), erecting shapes (S44), the stroke tools
(S46) and per-shape participation (S48).

- [ ] **S45 — Relief: the preview, solved on the server.** Nothing shows a relief before it is built. The
  canvas draws the field's **contours**, which is both the readable view of a height field and the
  direct-manipulation surface — dragging a contour writes a line mark at that height, so how the surface
  reads and how it is edited are one object. **No JS twin**: the paint preview already establishes the seam
  (debounce the edit, post the layout, load the reply as a canvas layer, drop replies overtaken by a newer
  edit via a sequence number), and a relief solve is far cheaper than the whole-map paint that seam already
  carries. Two measured facts make it affordable: the solve **cascades** coarse-to-fine (317 ms against
  519 ms on a 192×128 map, agreeing to within a block, the advantage growing with the footprint), and a drag
  **resumes** from the surface already on screen (40 sweeps, 228 ms on that map, off by one block on 1,614 of
  24,576 cells). Both are in the solver already; this slice is the endpoint and the overlay. Porting the
  relaxation to JS would buy milliseconds and cost a second implementation of a cascade, a chamfer sweep and
  a symmetry fold — the duplication `Geom.Symmetry` exists to prevent (`sketch-relief.md` §14, §15).

- [ ] **S41 — Relief: marks as placed things.** The five mark kinds solve and export; none can be *placed*.
  A relief mode between Draw and Theme, with the canvas tools that put a point, a line, an area and a scarp
  on the ground, a list of what an island states, and an inspector for the numbers on the selected one — the
  Dressing phase is the pattern to follow, since it is the same shape of problem (a placed thing, a list, an
  inspector). Two behaviours are load-bearing and were bugs before they were rules, so the tools must not
  quietly undo them: a mark is **clipped, not confined**, so one placed *past the edge* raises the ground
  into a corner and stops — which is how a spawn hill is authored with no wasted strip behind it, and the
  canvas therefore has to let a mark be dragged outside the island; and a **scarp band stops where its line
  stops**, which is what leaves the gaps a crossing is authored at.

- [ ] **S50 — Relief: draw the push, don't type a radius.** The push solves — ring falloff across the land,
  crown off the medial axis, per-vertex amounts, roughness (`FEATURES.md`, `sketch-relief.md` §2.1–2.2) —
  and the only way to state one is by hand in JSON, which is precisely the hand-authoring the feature exists
  to replace: a summit typed as a position and a radius can only be round. The slice is the ring **drawn**
  like any other polygon, plus `amount` / `falloff` / `crown` / `roughness` as inspector numbers and the
  per-vertex lift edited on the ring's own anchors, where a per-vertex height already reads as a height.
  Crown is the one to get in front of an author rather than bury: it is what makes a drawn ridge a ridge
  instead of a plateau, and its default of 0 is the least natural of its three settings.

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
