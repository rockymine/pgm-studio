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

The board's open work is the same shape one layer out. A prop the dressing pass declines, a refusal crossing
the API, an artifact read from the database, a gate in the export chain — each is one concept given a
different form at each surface that touches it, so nothing downstream can read all of them. That is what the
first three groups below are, and the reason an agent driving the studio meets one on every loop.

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

### The API layer: one envelope, one gate chain

An agent driving the studio meets one of these on every loop, and they share a cause: the same answer is
given a different shape at each surface, so nothing downstream can read all of them. Fable's run-3 review
named the layer and re-verified it after its own fixes landed
(`pgm-studio-mapgen/reports/fable-run3-architecture.md`, with the author's follow-up ask recorded in this
repo's history); the counts below are re-measured against today's tree. The store it named is settled — one
`MapArtifactStore` keyed on the artifact kind — and what is left is the envelope and the chain.

- [~] **RP2 — Twenty hand-rolled refusals are left, and each needs a rule id nobody has ruled on.** The
  request faults are converted (24 sites, all `RQ1`). What remains is five groups, and every one of them is a
  question rather than a rename:

  **The work failed on a document that read.** `could not paint layout`, `could not build layout`, `could not
  solve relief` (×2), `could not render plan`, `composition failed for this descriptor` — a catch-all around a
  posted document that parsed. Measured (`TS14`): the only authored thing that reaches them is a malformed
  dressing document, which now answers `DR-DOC` by name, so what is left in the catch is a studio bug wearing
  a 400. `RQ1` says *could not be read*, which these are not; `RQ2` says the fault is the studio's and answers
  500. The ruling wanted is whether they become that.

  **Two are real sketch gates** and want `SK*` ids beside `SK1`: *No sketch layout to finish* and *Nothing is
  drawn: the layout rasterizes to no ground*. **The stored artifact is the unreadable one** in `stored plan is
  unreadable` (×2, 422): the row will not parse, which is nobody's request. **A library part is in use** (×3, 409, carrying `used`) — the library has no rule
  family. **Import** answers through one `Fail(code, msg)` helper at 20 sites across 400/403/409/413/415/422/
  500/502, which wants an import family of its own. **And the edges**: three 404s with a sentence,
  `WriteEndpoints`' `EditException` pass-through, and `MapExportComposer`'s 500 `Dict` — which cannot reach
  `Api`'s `RequestRules` at all, so `RQ2` would have to move to `Domain` beside `Finding.Envelope`.

- [ ] **RP3 — The gate chain's completeness depends on which entry point a caller came through.**
  `MapExportComposer.Compose` runs `OB20` (`RefuseUnknownGamemode`) and the traversability judgement before
  handing on; `ComposeSketch` runs the rest. `tools/mapgen` calls `ComposeSketch` directly
  (`Program.cs:141`) and so skips both. It cannot trip `OB20` today — it writes no gamemodes — so this is
  shape rather than a live defect, but a gate that fires on one of two entry points is one nobody can
  reason about, and it is the residue of the finding that `mapgen` shipped maps the HTTP export would
  refuse. Move both down into `ComposeSketch` so the chain is caller-independent, leaving `Compose` the
  doc-assembly leg it already reads as.

### Agentic Map Authoring

- [ ] **B253 — How a model actually drives the studio: read the six drivers and the fifteen reports, then
decide what the one driver is.** Nobody authored a map the same way twice. The `pgm-studio-mapgen` repository carries
**six** independent drivers — `tools/drive.ps1` (a thin poster, hand-authored layouts), `tools/drive.py`
(compiles the plan through the API, then patches the compiled layout by tier height), two per-spec
`assemble.ps1`, and `build.py`/`reconstruct.py` under `coldharbour`, `coldharbour_v2`, `quernstone` and
`thunder-series` — plus `tools/build.cs` and `world-build.cs`, against `tools/mapgen` in this repo. Two
point at different ports. Each was written because the one before it did not fit, and none of that reached
a document.

**Read them against the `reports/` (15) and `review/` (29) records** and answer three things. *What every
driver had to do itself* — the call order, the fanning, the `@style` resolution, the wait-and-look step —
is the shape of the driver the studio should ship. *What each model reached for and could not find* is
where the endpoints are unreachable rather than absent, which is `B109`'s and `B245`'s subject. *What a
driver had to know that no document says* is the gap `AUTHORING-BRIEF.md` should close.

The output is a finding, not a refactor: one written account of the authoring loop as it is actually
driven, and a decision on whether the one driver belongs in `pgm-studio` beside `tools/mapgen` or in
`pgm-studio-mapgen` beside the specs. Worth doing before `B245` and `B249`, which both assume an answer.

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
