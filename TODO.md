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

## Sketch tool (S)

- [ ] **S25 — Surface the plan's spawn/wool as labeled, intent-linked rectangles in the sketch.** When you
  add detail to a plan in the sketch tool, the structural elements the plan already placed — the **spawn**
  and the **wool room** first — must stay **visible**, not merely survive invisibly in the DB. Today they
  don't: `PlanCompiler.Compile` writes two blobs — `sketch_layout_json` (pure geometry) and
  `map_intent_json` (the semantics: `SpawnIntent.Piece`/`.Point`/`.Protection`, `WoolIntent.Room`/`.Piece`,
  all `Rect` footprints) — and the sketch model (`SketchShape`, `PgmStudio.Pgm/Sketch/SketchLayout.cs`) has
  no notion the intent exists. So opening a plan-derived map in the sketch shows land with **no marker for
  where the spawn or wool is**, and detail work is blind — the info survives (shared DB) but isn't shown.
  **Decision — this deliberately revises `docs/world-export/finishing-model.md` §6**, which had settled on
  "the sketch does not author these; they live in the intent/DB." The reason to reverse: the sketch *is* the
  fine-grained plan tool, so when you refine a plan the spawn/wool must be legible in it, not lost. Render
  the spawn/wool footprints as **`rectangle` sketch shapes that are labeled** (`spawn · <team>`,
  `wool · <colour>`) **and linked to their intent entity** — displayed distinctly, and by construction
  **not promotable to polygon, single height, no slope** (slope is already polygon/lasso-only, verified, so
  that constraint falls out for free — no work). The linking already has a home: a map's plan / sketch /
  intent are separate rows on the same map, so the sketch can read `map_intent_json` and project these in.
  **Locked for now** (read-only placeholders); making them *movable* — which must write the move back to the
  intent so sketch and intent don't diverge — is a deliberate **later phase**, not v1. Update
  `finishing-model.md` §6 in the same commit when this lands (doc follows the build, not ahead of it).
  **Blocking design points, to settle at the start (author will detail these in a fresh session):**
  (1) representation + link mechanism — a new `role`/`intentRef` on `SketchShape` merged from the intent at
  load, vs. a read-only overlay the sketch draws but never stores — and how it stays in sync as the
  plan/intent changes; (2) which intent entities in v1 (spawn + wool room are the ask; protection / build /
  monuments / iron come later); (3) the distinct render treatment; (4) if/when they become editable and how
  the move writes back to the intent. (The plan→sketch route already exists: compiling a plan can reach the
  sketch, and a compiled map need not land straight in configure.)

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
  comments in the ported C# (`Analysis/IslandDetector`, `Analysis/Traversability`, `Minecraft/BlockPalette`, …)
  that the code-comments rule already bans. `appsettings.Development.json`'s `MapsRoots`/`Import.Root` point at
  the reference VM but are import-only — repoint or document, don't leave dangling.

## Layout generation (G) — current focus: the generator in the studio

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
