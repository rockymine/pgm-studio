# Handoff: the composer session (G32)

Written at the end of the design/rules session that froze `layout-rules.md` v3. The next
session's job is **G32 — the composer v1 skeleton** (`TODO.md`). Everything decided lives in the
repo; this file adds the reading order, the concrete plan of attack, and the working conventions
that were verbal until now.

## Read first, in this order

1. `CLAUDE.md` — project rules (board discipline, comment rules, test invocation, environment).
2. `docs/contracts/layout-rules.md` — **frozen 2026-07-04, the composer's v1 acceptance oracle.**
   Changes are amendments via its correction protocol only; do not relitigate.
3. `docs/contracts/layout-generation.md` §3 — the composer's order of operations (compose the
   closure → assign team sides → carve the mid → cut the team sides), plus the roughen pass (§4,
   later) and the traffic ground-truth section.
4. `docs/contracts/plan-editor.md` — the plan schema, derived structure, compiler, validation.
5. `docs/seed-stats.md` — the measured corpus envelopes every generated plan should land inside.
6. `docs/contracts/traffic-ground-truth.md` — formats + logs-only pipeline (G33, later phase).
7. `docs/cloud-setup.md` — environment notes for cloud sessions.

## State snapshot

- **Corpus**: 12 seeds in `tools/seeds/` (10 authored, 1 real-map trace, 1 tiny), all with honest
  per-team `maxPlayers` (= comfortable cap; maps tolerate ± a few players). Base-seed goldens
  (`*.layout.json`/`*.intent.json`) embed `maxPlayers` — regenerate the one field if counts move.
- **Rules**: v3 frozen. Key structures: CT (mid = interface between team territories, carved —
  clean 8 / hash 3 / parallel 1; team sides are cut), CT4 stones (marker islands never count;
  encased pads = team transient-links; mid stones thin 17/4/0), CT7 grid alignment, G8 coupling
  (land/player rises 65 → 184 with per-team land), EL1 palette 3–19 (step 2, all-odd surfaces),
  EL6/EL7 cliffs, FR5 asymmetric frontlines, facing = absolute board directions.
- **Ground truth**: `tools/traffic/ingwaz.*` — recovered plan validated against real traffic
  (23/23 void cells in zones, 171/171 land nodes in pieces, 6/6 islands, scale ×1.111). The
  traffic graph is reproducible from a raw log zip alone (contract doc has the method).
- **Pipeline**: plan.json → PlanCompiler → (SketchLayout, MapIntent) → rasterize/export → playable
  world ZIP. Endpoints: `POST /api/plan/inspect`, `POST /api/plan/compile`, then the sketch chain.
- **Tests**: `dotnet run --project tests/PgmStudio.Pgm.Tests` (257 green). Coverage test globs
  `tools/seeds/*.plan.json` — new seeds are auto-exercised; the EL6 lint test requires qualifying
  cliffs to carry `cliffs` marks.

## G32 plan of attack (suggested)

Code home: `src/PgmStudio.Pgm/Compose/` (needs only `Geom` + existing `Plan/` types). Stages,
each gated before the next:

1. **Envelope** — input `(playersPerTeam, teams, symmetry?)`; land budget from the G8 table
   (interpolate); board dims within G3's measured ranges.
2. **Team unit growth** — attachment rules per `layout-generation.md` §3.2: spawn piece
   back-of-lane (SP2), wool room(s) on separate lanes (WL1/WL2), corridor widths G2, lane depth
   via G5/G7 hops. Deterministic seeded RNG only (no wall-clock).
3. **Carve the mid** — v1: the **clean** form only (one connected zone region, 0..n mid stones
   sized per MD1, grid-aligned per CT7, thinning outward per CT4). Hash/parallel are v2.
4. **Cuts** — optional CT5 isolation (isolated wool/spawn behind a bridge) as a coin-flip motif.
5. **Markers + heights + walls** — SP3/SP4 spawn (facing absolute), SP7 iron beside/ahead,
   WL5 approach climb, EL1 palette (base 9, step 2), ST4 walls on qualifying elevation seams.
6. **Gates** — `PlanValidator` errors empty; lints reviewed (corpus passes with intentional
   flags); `FannedGraph` traversability; stat envelopes vs `seed-stats.md` (piece widths, zone
   dims, wool↔spawn distances, interface Δ histogram).
7. **Emit** — `plan.json` loadable in the plan editor (`/plan`); goldens for 2–3 fixed RNG seeds
   as regression anchors.

Acceptance: a generated plan is indistinguishable from a corpus seed under every lint and stat
sweep, and compiles through the existing chain to a playable export.

## Working conventions (were verbal)

- **Token routing**: implementation goes to **Opus subagents** (Agent tool, `model: "opus"`);
  keep the top-level model for design decisions, rule questions, and review gates. The author
  asked for this explicitly.
- **The author corrects by rule id** (layout-rules.md correction protocol). Post-freeze findings
  are amendments logged under *Resolved*. Never renumber ids; never reuse retired ones.
- **Phone review artifact**: the author reviews visually on his phone. The session artifact
  lives at `https://claude.ai/code/artifact/8775fd03-2c43-4242-a644-b0635add9ca4` (favicon 🗺️,
  keep it). A new session must pass that URL as the `url` parameter on its first Artifact call to
  redeploy in place (otherwise a fresh URL is minted). It currently holds: all 12 seed renders
  with mid-form/count tags, the CT4 island-gradient views, and the ingwaz traffic ground truth.
- **Commits**: only when work lands, message references the task id, board files updated in the
  same commit (task lives in exactly one of TODO/BACKLOG/FEATURES).
- The author appreciates being quoted his own framings back when they get formalized, and flags
  scope creep himself — when he says something is out of scope (e.g. match analysis), encode the
  boundary in the doc.

## Open threads beyond G32

- `G33` traffic pipeline (input: one log zip per map — the author uploads them like ingwaz's).
- `G31` scaled structure presets (8×8 stamps overlap 1-cell pieces on the tiny map).
- `G30` unfold analysis; `G24` junctions/chains; `G29` climbs; `G27` iso preview (all BACKLOG).
- maxPlayers semantics: stored = comfortable cap; rotate's real XML defines 16 vs cap 20.
