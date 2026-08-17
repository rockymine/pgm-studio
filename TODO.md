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

### The keep-out mask: what it stops, what it says, and what it is called

One mask decides where the dressing pass may put anything, and all three questions about it are open: it
does not stop everything it should, it cannot say why it stopped, and it is named after a different concept
that already exists in the map contract.

- [ ] **WE1 — A declined prop is invisible to the surface that asked for it, and the approach lets one land
  that it will then refuse.** Two halves, one answer.

  **The approach protects.** `DressingScope.ApproachAt` — 20 blocks out from a spawn's doored face,
  `WoolApproach` 10 from a wool entry — is read only by `MapExportComposer`, never by the build's protection
  mask, so a tree in a door's approach is built, drawn in the 3-D preview, and refused at export by `OB21`.
  The author's ruling is that it protects as well: the prop never lands. The boulder exemption goes with it —
  a boulder is large enough that the low-cover sightline argument does not hold, and the carve-out costs an
  agent more than it buys. `OB21` then stops being reachable by an authored prop.

  **The decline is reported, in the shape everything else refuses in.** `DroppedProp(Id, Kind, Reason)` is an
  eighth finding record that never joined the consolidation. Two of its four reasons — *"the column at (x, z)
  is protected"*, *"ground already claimed at (x, z)"* — name a cell and neither the rule nor what claimed
  the ground, though `GroundClaims` holds the `ClaimKind` and a `PlacementClaim` now holds the owner. Make a
  decline a `Finding`: a `DR-*` rule from `docs/refusals.md`, `Subjects: [propId]`, `Severity.Complaint`.
  Then return it from the build endpoints beside the payload — `/sketch/columns` and `/plan/columns` hold
  `SketchWorld.DroppedProps` today and discard it — so an agent learns **why, where and what** on the loop it
  actually drives, instead of unzipping `dressing-report.json`.

  **A decline is a complaint on a 200, not a refusal**, so it never touches the refusal envelope: the world
  built, and some props did not land in it. The shape already exists and wants reusing rather than inventing
  — `/plan/inspect` returns `warnings`, `/plan/evaluate` returns `lint`, and `FeasibilityDto` carries `Unit`,
  each a `FindingDto[]` beside a success payload. Three names for one shape is its own small drift; pick one
  and use it here rather than minting a fourth.

- [ ] **B106 — Rename one of the two things called protection.** One is the XML region rule that stops a
  player entering a spawn or a wool room and restricts what may be broken or placed inside it — a gameplay
  contract. The other is `Decorator.IsProtected`, "cells nothing may be placed on", a dressing keep-out with
  no gameplay meaning. A goal that needs the second does not need the first, and one word for both invites the
  inference that a destroyable must live somewhere protected — which is what produced the caged goals, and it
  survives the code that acted on it.

  `DressingScope.ProtectedAt` makes the collision concrete: it *reads* `SpawnIntent.Protection` — the XML
  region — to build the mask that means the other thing, so one word names both the source and the derived
  set. The internal verb is already right (`KeepRect`, `KeepArea`), so `KeptClearAt` / `IsKeptClear` reads
  with the code that builds it and leaves `Protection` to the contract. Lands with `WE1`, which changes what
  goes into that mask and what it says when it stops something.

### The API layer: one envelope, one gate chain

An agent driving the studio meets one of these on every loop, and they share a cause: the same answer is
given a different shape at each surface, so nothing downstream can read all of them. Fable's run-3 review
named the layer and re-verified it after its own fixes landed
(`pgm-studio-mapgen/reports/fable-run3-architecture.md`, with the author's follow-up ask recorded in this
repo's history); the counts below are re-measured against today's tree. The store it named is settled — one
`MapArtifactStore` keyed on the artifact kind — and what is left is the envelope and the chain.

- [ ] **RP2 — The refusal envelope still has three anonymous shapes beside the typed one.** `Refusals.Of`,
  `WriteAsync` and `StopAsync` are the envelope, and `RefusalDto` + `Finding.Envelope` are the deliberate
  pair carrying the drift warning. Beside them sit **31** hand-rolled sites in three sub-shapes — 24
  `new { error }`, 4 `new { error, message = ex.Message }`, 3 `new { error, used }` — plus a bare `{error}`
  `Dict` in `MapExportComposer`'s exception path. A caller cannot write one parser for that. Convert each
  onto the envelope; the only non-mechanical part is giving each its rule id from `docs/refusals.md`, which
  is the work that makes the conversion worth doing rather than a rename.

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
  them. Two boards in `pgm-studio-mapgen` already cannot be rebuilt against today's tree for exactly that
  reason, both by gates that shipped after they were authored: `firnline` on `OB21` (a house in a spawn door's
  approach, `B235`) and `sunspit` on `PL13` (a wall on the wool room's interface, `B186`). Both worlds load
  and play; only the pipeline that made them now says no.

  **The shape: a per-call override that names what it is waiving, not a global off switch.** It reaches the
  two places a refusal is raised — `PlanCompiler` (a `PL*` refusal) and `MapExportComposer` (`OB17`, `OB19`,
  `OB21`, the playability judgement) — and every waived finding is still **reported**, as a warning carrying
  its rule id, so a forced build says what it forced rather than going quiet. A refusal about the `map.xml`
  contract itself is not waivable: PGM has to be able to read the result.

  **It stays out of the agent's vocabulary.** Not in `docs/tools/capabilities.md`, not in the endpoint tables
  the briefs hand an agent, not in `mapgen`'s `--help` (`B245`). The authoring briefs already tell an agent
  that a refusal is a fault to fix; an override an agent knows about is an override an agent will reach for.
