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

## The focus: the seams, and which of them bite

A **seam** is one concept implemented once and not reached from the second place that needs it. Four were
found by following where a fact is stored or derived rather than by reading a type, and all four are closed —
`B197`, `B195`, `B200`'s model half, and `B202`. Only the last bit: two authored houses whose stamped rings
overlap, one placed, two claimed, 56 columns carrying a `Structure` claim over bare ground — and provenance
is *preferred* over the material estimate, so a stage image drew a building that was not there and said it
was certain. The other three were correct by coincidence of maintenance, and the four did not fail a single
test between them.

**The rule that came out of it**, written where the next pass meets it: `StructureClaim` — *a claim is taken
from the placement, never rebuilt beside it* — held by two regressions, a building dropped for overlapping
one already standing and a building authored over void, both claiming nothing.

`BACKLOG.md` groups the open work by the concept it spends — the house, distance, the audit's remainder —
and every group's landing site is built. The one open question that governs a whole group is the author's
(`B212`): a distance is the **walk over the walkable surface, never the straight line**, and the walk under
every measure is still flat (`B246`), so the thresholds stated in it want restating before anything enforces
them.

## Task groups

### Symmetry: the centre is settled, and the picture that still cannot show it

- [~] **B251 — A stage image still cannot prove a board is mirrored: the shading flips, and a claim can lie
  about where its blocks are.** The accent half is fixed — a structure and its own image share a hue
  (`FEATURES.md`) — and two causes remain.

  **`TopDownRender` shades each column against the one step north** (`TopDownRender.cs:354`), which cannot
  survive a 180° rotation by construction: a reader comparing halves sees a gradient that flips. Correct as a
  lighting cue and wrong as the only cue. Worth a symmetry-aware mode, or an overlay that draws the axis and
  the mirror residual directly rather than leaving it to the eye — which is what `B250` needed and could not
  get from a picture.

  **A claim rectangle is not always its partner's mirror, even where the blocks are.** On `firnline` the two
  spawn rooms are mirror-exact in the world — x −12..7 / z 87..102, **320/320** columns — while `spawn:0`'s
  claim x −5..4 / z 91..99 reflects to x −5..4 / z −100..−92 against `spawn:1`'s actual x −4..5 / z −99..−91.
  So the render draws a building a block from where it stands and the world is right. What wants settling is
  `SpawnRoom`'s frame: it is handed to `ClaimRect` as a rectangle of cells and mirrors as though its bounds
  were grid lines, and `StructureClaim`'s rule (`B202`) is that a claim comes from the placement rather than
  being rebuilt beside it.

### Provenance: A per-column record of which pass claimed the column last

- [ ] **B252 — A provenance owner id means two different things, so nothing can pair a stamp with its own
  mirror.** `house:{propId}:{k}` carries the **orbit image** in `k`, while `spawn:{i}`, `wool:{i}`,
  `destroyable:{i}`, `core:{i}`, `wall:{i}`, `roomfloor:{i}`, `redstoneline:{i}` and `ironcube:{i}` carry a
  running index into the **already-fanned** list. Both images of one thing are separate entries with nothing
  saying which thing they are two of, and `spawn:0` / `spawn:1` are indistinguishable in form from
  `house:h1:0` / `house:h1:1` while meaning something else. Give every claim the same pair — **what it is, and
  which image of it this is** — so a reader can group by identity and pair by image without guessing.

  `StructureFinder` already groups by owner to tell two touching buildings apart, so the identity half is
  load-bearing today; the image half is what `B250` had to recover by matching cell sets geometrically,
  which is how the first reading of it came out wrong.

- [ ] **B216 — Provenance records structures only; it should record every pass that places something.**
The sidecar carries `Ground` and `Structure` and nothing else — **no trees, no boulders, no paths, no
water**. Its own docstring argues they need no record because they separate from built ground by material;
the author's ruling is the other way: **provenance carries them too**. Material tells you what a block is,
not that a pass put it there or which prop it belonged to, and that is what a read-back has to be able to
prove.

The consequence is stated twice in the authoring reports: `--column` is the only read that can prove ground
cover exists, because the top-down will not show it, the export will not refuse it and the sidecar does not
carry it — which is how two flora props landed nothing on Coldharbour with no diagnostic anywhere. It also
left `B250`'s symmetry reading silent on exactly the families an author checks first. **The placement is
known at stamp time** and the tree renderers already read it to draw a crown and a base, so this is a write
rather than a derivation. It lands with `B252`'s owner shape, since a new claimant needs an identity a
reader can group on.

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

  *Not this: `OB17` already refuses a goal in void, in a spawn room or in a wool room over a shared
  `ObjectiveFootprint`, the unwinnable `block="never"` case included; `StructureClaim` (`B202`) answers which
  columns a stamp owns; `B142` answers what the dressing pass declined. The editor half shipped (`B59`,
  `C44`) and what remains of it is timing — structural findings do not run in the live feed (`G161`), so a
  refusal appears at Compile rather than as the marker is dragged. `G65` is adjacent and separate: whether two
  pieces touch, not how far apart two placements stand.*

  *moved here by the human because it sounds related and no other category fits*

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

### Other tasks and quick wins

- [ ] **G163 — `map-layers`' rebuild-confirmation step flakes about one run in three.** The step drives
  Compile on a freshly-opened plan and reads the drawer; when the plan document has not reached the client
  yet it compiles an empty plan, which is a 422 by design, so the drawer never opens and the following
  `page.click` times out at 30s. The spec guards it with a fixed `waitForTimeout(1500)` — a duration
  standing in for a condition, and the wrong guess about a third of the time. Measured 1-in-3 both with and
  without the `OB17` rule, so it is timing rather than validation. Waiting on the first piece id label
  (`.map-canvas-svg text`, the overlay's proof the document arrived) was tried and did **not** fix it, so
  the stall is later than the document load. **A caught failure now names the click, and it is not the one
  the paragraph above blames.** The step got as far as reading the drawer's button label ("the button names
  a rebuild" passed on `Rebuild this map`) and then timed out on `page.click("Rebuild this map")` — the
  *second* click, on a compile that answered 200, long before the empty-plan compile the 1500ms guard is
  aimed at. The recorded 422 is an earlier fault on the same page, not this one. A 30s timeout on a button
  whose text was just read means the element was found but never became actionable, which points at a
  drawer that keeps re-rendering rather than at a document that has not arrived — so the fix is a wait on
  the drawer settling, and the 1500ms guard may be guarding nothing. A flake in the browser gate costs more
  than the step is worth, because it makes every unrelated run ambiguous.

- [ ] **G154 — one plan editor, two bindings, two different tools.** `PlanTool` serves `/plan-editor` and
`/maps/{slug}/plan` from a single component through five `@if (MapBacked)` branches, and the two render as
different products. Map-backed gets the phase rail (Info · Draw), the flow bar, and the three panels as chips;
the bare route gets no flow bar, no phases, the same three panels as **rail buttons**, and a collapsible
sidebar the map-backed one cannot have (`SidebarOpen => MapBacked || leftOpen`). Same panels, two navigation
models, one file — the thing the tool-consistency alignment exists to prevent.
Unify on the phase-rail + flow-bar + chips structure and keep the collapsible sidebar for both. The route may
change **only** the topbar — its crumbs and which actions exist — because that is where the binding genuinely
differs: a map-backed plan saves into its map's artifact, while a plan row saves as a row and forks when it
was generated or imported. Rename the bare route to `/plans/{id}` (and `/plans/new`), which says what it is
bound to where `/plan-editor` says nothing, updating the generator hand-off, the smoke sweep's route list and
the plan schema doc with it.
**Do not delete the route.** It is the only surface that opens a **plan row**, which is what the generator
hands a candidate off as and what `G119`'s fork-on-edit rule operates on; routing candidates through
`/maps/{slug}/plan` would mint a map per candidate looked at, and New, Import, Open and the origin badge have
no home on a map-backed plan.

- [ ] **B79 — The plan tool must not offer Compile before the document it would compile has loaded.**
Reached by the SPA hop from the Configuring list, the tool's canvas is in the DOM before its plan document
is. Click **Compile** in that window and it posts `pieces: 0`, the validator correctly answers `422` `PL1`
*"this plan has no pieces — there is no land to build"*, and the drawer opens anyway because its tabs render
the source document. The draft button still reads **Rebuild this map** — `BuildLabel` comes from the map —
and is `Disabled="@(compiledLayout is null || draftBusy)"`: present, correctly labelled, not actionable. A
user who clicks quickly is told their board has no land, about a board with land. Gate the button, or the
post, on the document having arrived.

The suite half is one missing wait. `map-layers.mjs:75` waits for `.map-canvas-svg`, the element that exists
too early; at `:122`, before the *second* compile, it waits 1500 ms with a comment saying exactly why.
Fixing the tool makes both unnecessary.

*diagnosed 2026-08-16 by intercepting the editor's own `POST /api/plan/compile` under both navigations:
same database, `goto` → **200**, row-link → **422**. `./tools/e2e.sh all` gives `map-layers` 13/14 with
`smoke` 39/39 in the same run; `./tools/e2e.sh map-layers` alone is 18/18. `B229` was this filed a second
time — its hypothesis, that an earlier spec breaks the stored plan, is disproved by the same test.*

- [ ] **B34 — The two map-list endpoints disagree on sort order, and the dashboard gets the noisy one.**
  `MapsListEndpoint` branches on the `stage` query param onto two differently-ordered repository methods:
  `MapRepository.ListAsync` sorts `OrderBy(Slug)`, `ListByStageAsync` sorts
  `OrderByDescending(UpdatedAt).ThenBy(Slug)`. The dashboard always requests `?stage=…`, so it always gets
  recency order — and on the imported Edit corpus `updated_at` records when the *pipeline* last wrote the
  row, not when the author last worked on the map, so it carries no authoring signal. The 349 Edit rows hold
  only 29 distinct timestamps (a re-processing pass stamped them in ~22-map batches a second apart), so the
  list renders as 29 alphabetical runs concatenated — it reads as scrambled, with the three maps outside the
  supported range (`3084`, `allure`, `lost_haven`, never re-processed) parked at the bottom. Recency earns
  its keep on Sketches/Plans/Configuring, where the timestamps are real edits and the lists are short.
  Preferred fix: slug order for the Edit stage, recency for the other three (one line); alternatives are
  slug everywhere, or leave it and let recency come good once maps are edited in the studio. Cosmetic — no
  data is wrong, and both orders are deterministic.

- [ ] **B102 — Clear the region directory before a rebuild writes it.** `AnvilRegionWriter.Write` calls
  `Directory.CreateDirectory` and nothing else, so every `.mca` a previous build left is still there and a
  chunk the new build does not touch — because its geometry moved — is read back as part of the new map. That
  makes rebuilding into an existing `out_dir` untrustworthy, which is exactly what iterating on a spec does,
  and contradicts the README's promise that "the same spec rebuilds the same map, so two runs can be
  compared". It cost a design session real time, presenting as building counts that could not be reconciled
  until the directory was deleted by hand. Distinct from the concurrent-build race `CLAUDE.md` warns about:
  that one is two builds at once, this one is one build after another.

- [ ] **CV21 — the world canvas has a `build` layer nothing paints into.** Stating the layer stack once
(`CV19`) surfaced two layers with no content. One was removed there — a `block-highlight` rect created
`visibility:hidden` whose only handle was assigned and never read. This is the other: the `build` group is
created empty, no painter ever appends to it, and its toggle `setBuildVisible` has no caller outside the
class — not the bridge, not any of the sixteen hosts. So it is an empty group with a visibility switch
nobody throws. Removing it takes `setBuildVisible`, `#showBuild`, `#paintBuildRegion` and one line of the
documented public surface with it, which is why it was left in place rather than swept during a
behaviour-preserving refactor. Check first whether a Build phase was *meant* to fill it (the name suggests
the Build-Regions work) — if so the task is to wire it, not delete it, and that is a different task in the
feature section.