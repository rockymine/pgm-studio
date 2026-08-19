# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
When this board drains, pull the next group up from `BACKLOG.md`. Board rules live in `CLAUDE.md`
(§ "Status & task board").

**The groups below are concepts, not categories.** Each heading names one foundation and gathers whatever
entries spend it, whatever their ids say; a group is emptied by settling that ground, which is what makes its
entries small enough to do, rather than by working through them one at a time.

Task ids are a prefix + number (`TS13`, `WE10`, `G15`) — **globally unique and stable** across all three
files. The prefix names the document the task must leave correct, catalogued in `CLAUDE.md`; it says nothing
about which group the entry sits in. Moving a task between files or groups never changes its id, and the
retired prefixes on entries here (`B`, `S`, `N`, `CV`, `P`, `A`) stay exactly as they are.

## The focus: one shape per concept, and the surfaces that still disagree

A **seam** is one concept implemented once and not reached from the second place that needs it, and the last
run of them closed the same way: by following where a fact is stored or derived rather than by reading a
type. `StructureClaim` — now `PlacementClaim` — carries the rule the whole family produced: *a claim is taken
from the placement, never rebuilt beside it*. It has since caught the same defect twice more, in a goal's
anchor and in a room's claim rectangle, neither of which failed a test while it was wrong.

The board's open work is the same shape one layer out. A prop the dressing pass declines, a gate in the export
chain, a refusal an author meant to make anyway — each is one concept given a different form at each surface
that touches it, so nothing downstream can read all of them. That is what the groups below are, and the reason
an agent driving the studio meets one on every loop. Two of the family have closed the same way: one
`MapArtifactStore` for every artifact read from the database, and one refusal envelope for every failure
crossing the API, the Edit tool's thirty-six write routes included.

The one open question that governs a whole group is the author's (`B212`): a distance is the **walk over the
walkable surface, never the straight line**, and the walk under every measure is still flat (`B246`), so the
thresholds stated in it want restating before anything enforces them.

## Task groups

### Provenance: A per-column record of which pass claimed the column last

- [ ] **B37 — Every family's resolver should answer one resolved-stamp record, and only iron does.**
  `IronResolution(MarkerX, MarkerZ, MinX, MinZ, Size, Placeable)` is the shape and the only instance, with
  four consumers, all iron; the wall, the rooms and the objectives each resolve their placement inline. The
  record wanted is **kind, footprint box, `Placeable`, source marker**, produced by each family's resolver.
  The stampers stay heterogeneous — a wall owns a seam, a room a piece + marker + entries, an objective a
  marker + style — which is why the *resolution* is the thing to share and not the stamping.

  Two things need it. `PlanStructurePreview.StructureBox` assembles its own boxes for iron, destroyables and
  cores; consuming the record instead reaches placeability into the iso view for free. And the
  objective↔objective and objective↔monument **minimum distances** — a core merging with a wool monument they
  must read apart from — have nowhere to live until every placement answers in one shape.

  **It is the same rule one layer up.** `PlanStructurePreview.StructureBox` re-derives iron, destroyable and
  core boxes the builder already computes, which is a second derivation of one geometry — the failure
  `PlacementClaim` names for a claim and that has now cost two fixes, the goal anchor and the spawn room's
  claim rect. A resolver answering one record is what lets the preview *read* the placement instead. Every
  entry already carries a `StampId`, so the record has an identity to travel under.

  *Not this: `OB17` already refuses a goal in void, in a spawn room or in a wool room over a shared
  `ObjectiveFootprint`, the unwinnable `block="never"` case included; `PlacementClaim` answers which
  columns a stamp owns; `B142` answers what the dressing pass declined. The editor half shipped (`B59`,
  `C44`) and what remains of it is timing — structural findings do not run in the live feed (`G161`), so a
  refusal appears at Compile rather than as the marker is dragged. `G65` is adjacent and separate: whether two
  pieces touch, not how far apart two placements stand.*

  *moved here by the human because it sounds related and no other category fits*

### The gate chain a driver meets, and what it can ask before paying for a build

Fable's run-3 review named the layer these came out of (`pgm-studio-mapgen/reports/fable-run3-architecture.md`),
and two thirds of it are settled: one `MapArtifactStore` keyed on the artifact kind, and one refusal envelope
every route under `/api` now answers in. What is left is the chain of gates in front of an export — which of
them a caller reaches depends on the door they came through, and the ones an agent trips hardest cannot be
asked about until a world has been built.

- [ ] **RP3 — The gate chain's completeness depends on which entry point a caller came through.**
  `MapExportComposer.Compose` runs `OB20` (`RefuseUnknownGamemode`) and the traversability judgement before
  handing on; `ComposeSketch` runs the rest. `tools/mapgen` calls `ComposeSketch` directly
  (`Program.cs:141`) and so skips both. It cannot trip `OB20` today — it writes no gamemodes — so this is
  shape rather than a live defect, but a gate that fires on one of two entry points is one nobody can
  reason about, and it is the residue of the finding that `mapgen` shipped maps the HTTP export would
  refuse. Move both down into `ComposeSketch` so the chain is caller-independent, leaving `Compose` the
  doc-assembly leg it already reads as.

- [ ] **RP4 — The export's own objective gates have no pre-flight, and an agent pays a whole build for
  each.** `OB17` (a goal overhanging void, in a spawn, in a wool room) and `OB19` (a tree, boulder or
  building inside a goal's clearance) are refusals raised by `MapExportComposer` over the rasterized ground,
  so the first time a driver hears one is `GET /map/{slug}/export` answering 409 — after the world has been
  built. Run 4 hit them three times across four boards; nothing in `/plan/evaluate`, `/plan/compile` or
  `sketch/columns` predicts either, and the compile gate is deliberately silent about an absolutely-placed
  goal because it has no ground truth to judge against. The ground exists as soon as `sketch/finish` has
  run: answer both over it, on the read an agent already makes (`POST …/sketch/columns`, beside the `DR-*`
  complaints), as complaints there and refusals where they are now.

  *Evidence: `hollowbank` placed a destroyable at `(0, 45)` on a plan piece and cut a sally port through
  that piece in the layout — compile 200, export 409 `OB17`. `alabaster-rake` put a shed run at
  `x 15..24, z 58..62` against a goal anchored `(5, 47)`; the keep-out is a 10-block square about the anchor
  tested against the footprint plus its eaves, and neither cycle was predictable before the build.*

- [ ] **TC1 — The analysis reads an author needs before an export are prose, not endpoint tables.** The API
  carries 66 `/map/{slug}` routes; `docs/tools/`'s tables carry a fraction of them, and three of the ones an
  author reaches for hardest appear in **no table at all**: `GET /map/{slug}/traversability` (mentioned in
  prose 12 times), `/buildability` (3) and `/symmetry` (67). Ten more path segments — `island-health`,
  `island-review`, `island-roles`, `kit-reach`, `monument-obstruction`, `monument-orbit`, `resources`,
  `scan-world`, `wool-availability`, `wool-suggestions` — appear nowhere in `docs/tools/` under any form.
  A table is the surface an agent scans; prose in a paragraph about something else is not. Add the live
  analysis reads to `configure.md`'s API table beside `preflight` and `coverage`, with what each answers and
  what it fails with, and name the ones that belong to a scanned world rather than an authored one.

  *Evidence: run 4's Sonnet agent used `GET /map/{slug}/traversability` to confirm all four of its boards
  connected before export — and filed it as a missing document, having found the route by other means. That
  is the cheapest possible check on a board and it is not on the page an author reads.*

### The author's override: building a board the gates refuse

- [ ] **B249 — An author can force a compile and an export past its refusals; an agent cannot.** The gates
  are right to refuse an agent — an unenterable board or a wall through a wool room is a defect it cannot see
  — but they also refuse **an author doing something deliberately off the norm**, and there is no way past
  them. `sunspit` in `pgm-studio-mapgen` cannot be rebuilt against today's tree for exactly that reason —
  `PL13`, a wall on the wool room's interface (`B186`) — and `firnline` now rebuilds without the house it was
  authored with, which the keep-out mask declines as `DR-KEEP` (a house in a spawn door's approach). Both
  worlds load and play; only the pipeline that made them disagrees.

  **So the override reaches the keep-out mask as well as the refusals.** A decline is not a refusal, but it
  is the same question one layer down: the author placed the thing deliberately, and there is no way to say
  so.

  **The shape: a per-call override that names what it is waiving, not a global off switch.** It reaches the
  two places a refusal is raised — `PlanCompiler` (a `PL*` refusal) and `MapExportComposer` (`OB17`, `OB19`,
  the playability judgement) — and every waived finding is still **reported**, as a warning carrying
  its rule id, so a forced build says what it forced rather than going quiet. A refusal about the `map.xml`
  contract itself is not waivable: PGM has to be able to read the result.

  **It stays out of the agent's vocabulary.** Not in `docs/tools/capabilities.md`, not in the endpoint tables
  the briefs hand an agent, not in `mapgen`'s `--help` (`B245`). The authoring briefs already tell an agent
  that a refusal is a fault to fix; an override an agent knows about is an override an agent will reach for.
