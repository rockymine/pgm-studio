# Handoff: the composer session (G32)

Originally written when `layout-rules.md` v3 froze; **updated mid-G32** after the first two
implementation tracks. The task is **G32 — the composer v1 skeleton** (`TODO.md`), split into
lettered tracks below. **G32-A and G32-B are committed; C/D remain** — read "Where G32 stands"
before touching anything.

## Orientation for a fresh session

You are likely landing on branch `claude/ctw-map-generation-design-u9n0hp` with **no prior
knowledge of it**. The branch introduced ~20k lines (plan layer, seeds, rules, composer) — do
**not** try to read it in one go. Orient in this order:

1. `git log --oneline -30` — the commit messages narrate the whole arc (plan editor → seed
   corpus → rules freeze → composer slices). Dive into a commit's files only when needed.
2. `CLAUDE.md` — project rules (board discipline, comment rules, test invocation, environment).
3. `docs/contracts/layout-rules.md` — **frozen 2026-07-04 as the composer's v1 acceptance
   oracle**, but *frozen ≠ static*: it grows through the correction protocol. Post-freeze
   amendments are logged in its **"Amendments (post-freeze)"** section — three have already
   landed (CT8 closure holes; the CT8 function split by hole ring; BZ6–BZ9 build-zone
   interface rules). Expect more: every author review round can mint new rules.
4. `docs/contracts/layout-generation.md` §3 — the composer's order of operations (compose the
   closure → assign team sides → carve the mid → cut the team sides).
5. `docs/contracts/plan-editor.md` — plan schema, derived structure, compiler, validation.
6. `docs/seed-stats.md` — measured corpus envelopes (incl. the team-side allotment sweep and
   the internal-hole/CT8 sweeps) every generated plan should land inside.
7. `src/PgmStudio.Pgm/Compose/` — the composer itself (~8 files; read `Composer.cs` first,
   it shows the stage order and what each unit owns).

Ask an agent for a summary of `Pgm/Compose`.

## G32 tracks

Code home: `src/PgmStudio.Pgm/Compose/` (depends only on `Geom` + existing `Plan/` types).
Deterministic seeded RNG only (`ComposeRng`, PCG32) — no wall-clock, goldens must be stable.

- **G32-A — envelope + team-unit grower.** ✅ Committed (`7b4603c`). `Envelope` (G8 land-budget
  interpolation, G3 board dims), `Frame` ((u,v)→(x,z) per-symmetry mapping), `TeamUnitGrower`
  (budget-solved piece sizing, bounded repair grid, hard invariants: full-corridor attachments
  only, LN2 chain cap ≤50 blocks, WL2 ≥20 / WL7 ≥45 marker distances, ±20% land budget,
  ≥10-block clearance between orbit images so the fanned board has exactly `teams` islands).
- **G32-B — mid carve + cuts.** ✅ Committed (with the B2 zone-discipline round). New: `MidCarver`
  (crossing sampled *before* growth — R0/R1/R2 hop designs, twin frontline chains as the CT8
  hole mechanism, mid stones via CT7-snapped candidate columns), `IsolationCut` (CT5 ~40%
  motif: translate + bridge zone), `ClosureAnalysis` (CT8 raster: `HoleSizes`,
  `AnyHoleRingedBy`), `ComposeGeometry`; modified `Frame`/`TeamUnitGrower`/`Composer`
  (`Composer.ComposeStages` returns Envelope/Unit/Crossing/Mid/Cut/Plan) + `MidCarverTests`.
- **G32-B2 — zone-discipline fix round (part of B).** ✅ Done. BZ6–BZ9 enforced (dock / fit /
  connector / clearance); the `!wantHole` hole-hunt resolved the over-holing (holed rates
  30/30 → ~23–27/30, never universal); p5 (t2 and t4/rot_90) left as a known limitation (**G35**).
  Suite 314 green. The B2 visual review findings are in `docs/composer-review-findings.md`
  (feeding **G36** + the teaching sets in `tools/seeds/teaching/SHOPPING-LIST.md`).
- **G32-C — markers/heights/walls.** SP3/SP4 spawn (facing absolute, raised), SP7 iron,
  WL5 stepped approach climb, EL1 palette (base 9, step 2, all-odd), ST4 walls, EL6
  discipline. Consider a mid-band spine piece (CT4) to relieve the p30 area floor.
- **G32-D — gates + goldens + emit.** `PlanValidator` zero errors with zones present;
  `FannedGraph` full traversability; stat envelopes vs `seed-stats.md`; `plan.json` loadable
  in the plan editor (`/plan`); fixed-RNG-seed goldens as regression anchors (goldens belong
  under `tests/`, **not** `tools/seeds/` — the corpus stays curated).

Acceptance: a generated plan is indistinguishable from a corpus seed under every lint and stat
sweep, and compiles through the existing chain to a playable export.

### The B-track verification matrix

Generated samples are named **`gen-p{players}-t{teams}-{symmetry}-s{rngSeed}`** — 20 cases:
players 5/12/16/20/30 × 2 teams rot_180 × RNG seeds 1/7/13, plus (10,4), (16,4,s5), (20,4,s9),
(12,2,mirror_x,s2), (10,2,mirror_z,s2). A throwaway .NET-10 file-based script drives it
(`ComposeRequest(players, teams, sym, seed, 5)` → `Composer.ComposeStages(req)`; needs
`#:project …PgmStudio.Pgm.csproj` and `#:property JsonSerializerIsReflectionEnabledByDefault=true`).
Report per case: pieces/zones/stones counts, validator errors+lints, `ClosureAnalysis` hole
count, cut present.

## Failure modes already hit in B (don't re-learn these)

Logged in detail in **`docs/build-zone-failure-modes.md`** (the author's review, verbatim —
read it) with his curated teaching sketch **`tools/seeds/build-interface-dos-and-donts.plan.json`**:

- **LN2 lane stretching** — surplus land budget was spent stretching lanes to 105/110 blocks;
  fixed with a hard chain cap + structural spending.
- **rot_90 island weld** — orbit images abutted, welding team sides into one island; fixed with
  ≥10-block inter-image clearance (islands == orbit order is sweep-asserted).
- **Boring T/plus silhouettes** — grammar poverty; reframed by the author's **team-side
  allotment model** (rectilinear bound per team, images tile without touching; corpus anchors:
  bbox aspect 1.0–1.8, land fill 31–63%, median 42% — see `seed-stats.md`).
- **Zone discipline (the B2 round)** — oversized mid bands spanning the whole board width,
  overlapping wool pieces and even the wool position (→ **BZ6**), zones overlapping pieces
  instead of docking flush (→ **BZ7**, with the sanctioned plaza-encasement exception),
  long-face docking without a readable connector extrusion (→ **BZ8**), zones overflowing
  into void / underfitting their interface (→ **BZ9**), and p5-specific defects: 1×2 spawn
  piece too small, spawn-severing bridge on small maps, wool lane hugging parallel with
  identical placement across RNG seeds (wanted: offset *away* from mid + variety).

**About the teaching sketch:** it exists **only to inform build-zone rules**. It contains
deliberate anti-patterns (and one disclaimed dead-end, piece-9) — it must **never** be edited
and must **never** drive changes to the validation of generated seeds (if it trips the
`tools/seeds/*.plan.json` coverage glob, exempt it by name rather than weakening a test or
"fixing" the sketch). If more such curated examples accumulate, a separate folder for these
"learned examples" is the likely home — flag it when the second one appears.

## Working conventions

- **Token routing**: the top-level model **orchestrates only** — specs tasks, reviews reports,
  encodes the author's review comments into rules and docs. All programmatic work (development,
  research scripts, measurements, sample regeneration, render/artifact updates) goes to
  **subagents**; the author chooses which models power them, so don't hardcode a family.
- **The visual review loop (load-bearing).** After every development slice, regenerate the
  sample matrix, render SVGs, and show the author a gallery **labelled with the seed ids**
  (`gen-p12-t2-rot_180-s7` style). He reviews visually, names errors by seed id and by concrete
  piece/zone ids, and often **reconstructs findings into curated plan examples** — which may
  intentionally carry lint warnings or errors. Those examples, plus his comments, become
  **rule amendments**: this is how CT8 and BZ6–BZ9 were born, and it is the expected path for
  future rules. `layout-rules.md` keeps growing this way — treat each review round as
  potential rule material, and log every change under "Amendments (post-freeze)".
- **The author corrects by rule id** (layout-rules.md correction protocol). Never renumber
  ids; never reuse retired ones.
- **Review artifact**: The session artifact lives at
  `https://claude.ai/code/artifact/8775fd03-2c43-4242-a644-b0635add9ca4` (favicon 🗺️, keep it).
  A new session must pass that URL as the `url` parameter on its first Artifact call to
  redeploy in place. It currently holds the G32 review gallery (generated samples + corpus
  context). In environments without the Artifact tool, a self-contained HTML gallery in the
  repo-visible filesystem works as a fallback.
- **Commits**: only when the author asks; message references the task id; board files updated
  in the same commit (a task lives in exactly one of TODO/BACKLOG/FEATURES). The **B commit**
  is pending: it should include the `Compose/` B files, the layout-rules.md amendments, and
  the author's two review files (`build-zone-failure-modes.md`, the teaching sketch).
- The author appreciates being quoted his own framings back when they get formalized, and
  flags scope creep himself — when he says something is out of scope, encode the boundary.

## Where G32 stands

- Committed: A track (`7b4603c`); **B track** — mid carve + isolation cuts + build-zone
  discipline (`MidCarver`/`IsolationCut`/`ClosureAnalysis`/`ComposeGeometry` + modified
  `Composer`/`Frame`/`TeamUnitGrower`), the BZ6–BZ9 + CT8-split `layout-rules.md` amendments,
  the author's two review files, and the B2 dev tooling in `tools/compose/` (matrix + gallery
  generators). **Suite 314 green.**
- The B2 visual review round is done — findings by seed id in `docs/composer-review-findings.md`.
  Build-zone discipline reads well (docks not overlaps, no void overflow); open polish is tracked
  as **G36** and the teaching sets in `tools/seeds/teaching/SHOPPING-LIST.md`.
- **Known limitation:** p5 (t2 and t4/rot_90) doesn't compose — structurally infeasible under
  BZ6 + spawn ≥2×2 in the fixed budget; the fix is the buffer tile + rot_90 border-reservation
  (**G35**), never a bigger board (that re-triggers LN2). Holelessness is resolved (the `!wantHole`
  hole-hunt), not a limitation.
- Next: **G32-C** (markers/heights/walls + piece roles), then **G32-D** (gates/goldens/emit).

## Open threads beyond G32

- `G33` traffic pipeline (input: one log zip per map — the author uploads them like ingwaz's).
- `G34` theming/styling rules (props, trees — maps are playable but bare stone). `B21` MCP
  server over the plan layer (prompt-driven map generation). Both BACKLOG.
- `G31` scaled structure presets; `G30` unfold analysis; `G24` junctions/chains; `G29` climbs;
  `G27` iso preview (all BACKLOG).
- maxPlayers semantics: stored = comfortable cap; rotate's real XML defines 16 vs cap 20.
