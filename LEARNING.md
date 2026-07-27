# pgm-studio — Learning log

A notebook, **not** a fourth Kanban column. The task board (`BACKLOG.md` → `TODO.md` → `FEATURES.md`)
tracks what the project owes; this file tracks what *I* owe my own understanding of it.

Two sections, no ceremony:

- **Debt** — code in this repo I don't understand, or wouldn't have written this way. A line here is a
  question, not a task. Some resolve into an explanation; some resolve into deleting what's there and
  writing the version I'd have written. When one becomes real work it graduates to `BACKLOG.md`/`TODO.md`
  with a proper id and **leaves this file** (leave the pointer, not the duplicate).
- **Covered** — dated one-liners: the concept, and where the live example is in this repo. So the knowledge
  has an address I can go back to.

Entries are dated and cite `file:line`. Debt entries are `[ ]` open / `[>]` graduated to the board.

---

## Debt

- [ ] **`AxisMarginCells` and the mid band's half-gap are two constants that only agree by accident.**
  `MidCarver`'s own docstring states the invariant as fact — *"its half-gap is the allocator's axis
  margin"* — but nothing enforces it. `Envelope.AxisMarginCells` is a hardcoded `2` (cells);
  `MidCarver.BandOnly` computes `20 / (2 * cell)` (`MidCarver.cs:35`). In integer arithmetic those agree
  only for `cell` ∈ {4, 5}, and the documented "uniform 20-block gap" is only true at `cell = 5`
  (at `cell = 4` it is 16 blocks). `ComposeRequest` accepts any positive `cell` (`ComposeRequest.cs:40`).
  At `cell = 6` the band would span 1 cell per side while the unit keeps 2, so the band no longer reaches
  the front it is documented to dock flush against — and `BandContactsOk` won't catch it, because it
  rejects overlap and rejects touching a non-front piece, but a band touching **nothing** falls through
  the `continue` on `MidCarver.cs:83`. **Open question: which of the two is the source of truth?**
  (2026-07-27)

- [ ] **The clamps quietly eat the sampled parameter space.** `Envelope.Derive` reads like a wide
  continuous space — coverage sampled 0.28–0.42, aspect sampled 1.0–3.0 — but measured over 8,400
  envelopes, **3,319 of 8,400 2-team boards** have at least one dimension overridden by a clamp
  (`Envelope.cs:73-74`). At p = 5 the length floor pins *every* seed to exactly 100, discarding the aspect
  draw 300/300 times; at p = 32 the width ceiling pins 243/300. Realised aspect reaches **4.00** against a
  sampler ceiling of 3.0, because clamping one side while the other stays free stretches the ratio past
  its own bound. p = 16 is the only player count where no clamp fires. For 4 teams, **53%** of boards sit
  at the 180-block ceiling — over half the same size. Not obviously a bug; definitely not visible from the
  source. (2026-07-27)

- [ ] **Three composer facts that no test asserts and no comment mentions.** Measured over ~2,600 attempts,
  120 seeds per configuration: (a) `TeamUnitFiller.Fill` returned `null` **zero times** — its failure path
  is empirically dead, so either it is unreachable given the current allocator (an undocumented coupling)
  or it is reachable and untested; (b) the `ComposeAttempts = 60` budget has ~9× headroom over the worst
  case ever observed (7); (c) the acceptance gate — five lines of `Composer`'s docstring — **never fires on
  a 2-team board**, and on 4-team boards fires on up to 34.2% of attempts for one single rule,
  `wool-ringed-hole`. Scope: seeds 0–119, default symmetry only; `mirror_x`/`mirror_z` unmeasured.
  (2026-07-27)

- [ ] **`ComposeRequest` handles two bad inputs two different ways** (`ComposeRequest.cs:25-36`): an
  out-of-range player count is silently **clamped**, a bad team count **throws**. The defence is that a
  player count is continuous, so "nearest valid" is a meaningful reading of the request, while a team count
  is categorical and 3 is not "sort of 2" — clamp continuous, reject categorical. The weak parts stand:
  the bounds `5`/`32` are inline magic numbers rather than named constants, and the clamp is **silent**, so
  a caller asking for 40 players is never told they got 32. (2026-07-27)

- [ ] **`cell` is misnamed.** It is blocks-per-cell — a scale factor, not a cell. Propagates to
  `ComposeEnvelope.Cell` and `PlanGlobals.Cell`. (2026-07-27)

## Covered

- **2026-07-27 — Resolved config vs. the request.** `ComposeRequest` is *what was asked*; `ComposeEnvelope`
  is *what was decided*, with the random draws already resolved so no downstream stage can re-derive them
  differently. The seed is deliberately **absent** from the envelope: the envelope is immutable resolved
  state, `ComposeRng` is the mutable stream, and both come from the one seed.
  (`ComposeRequest.cs`, `Envelope.cs`)

- **2026-07-27 — Normalize at the boundary to eliminate case analysis.** `Frame` is not a team unit — it is
  a 2-field coordinate transform (`PrimaryAxis`, `Sign`), the 2D equivalent of a model-to-world matrix.
  The unit is grown in unit space `(u, v)` — `u` = distance from the symmetry axis, `v` = cross-axis — and
  converted to world `(x, z)` only at the boundary. The payoff, counted: outside input validation, exactly
  **two** places in ~4,100 lines of `Compose/` look at the symmetry string (`Frame.For`,
  `MidCarver.LateralFlip`). `TeamUnitAllocator` is 790 lines of geometry with zero symmetry branches.
  Same family as: convert to UTC on input, canonicalize paths on input. (`Frame.cs`)

- **2026-07-27 — Generate-and-test vs. construct.** The composer generates a complete candidate and throws
  it away on failure (`Composer.cs:59-84`), rather than constructing a valid layout directly. Right choice
  here because the constraints are global and interacting — whether a wool clears the band depends on the
  band, which depends on the front face, which depends on a hub form chosen before any of it — so a
  constructive placer would need each step to know the future. Rejection sampling keeps each stage locally
  simple and puts all global consistency in one readable evaluator. **The rule to carry: a resample is a
  placeholder for a construction you haven't found yet.** G135 is that conversion made concrete — the
  front-slack *rejection* became `UnitPlacement.CentreFaceOnAxis`, a *construction*, after the rejection
  was measured never to fire.

- **2026-07-27 — A generator's sampled distribution is not its realised distribution.** You cannot read
  behaviour off the parameters; you sample the thing and look at the output. Clamps are where the two
  diverge. Method: sweep the real code over a seed range, dump to JSON, plot. (`tools/` sweep pattern;
  `Envelope.Derive`)
