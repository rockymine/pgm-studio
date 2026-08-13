# Documentation status report

This is a census of every Markdown document under `docs/`, `tools/`, and `scripts/` (62 files,
excluding `CLAUDE.md`, `TODO.md`, `BACKLOG.md`, and `FEATURES.md`, which are the task board rather
than documentation) — what each one covers, where the same subject is described more than once, and
how likely each is to have drifted from the code. It closes with the two questions asked directly:
whether `docs/generator/`'s ninth file belongs there, and which tools have no document that actually
speaks for them. The evidence behind every claim is grep output or `git log` against this checkout;
nothing here was taken on the document's own word.

One fact governs the rot-ranking section and is stated once here rather than repeated at every row:
this checkout's visible git history holds 197 commits, all dated 2026-08-11 through 2026-08-13. That
is not the project's real age — the doc set references work (task ids into the hundreds, a v3 rule
set "frozen 2026-07-04", a corpus re-verified "2026-06-10") that predates the visible log entirely.
Every "last edited" date and every churn count below is only accurate for the three days the log
actually covers; a doc showing zero churn was not necessarily untouched for months, and a doc showing
high churn was not necessarily edited recently in the way that number implies. Treat the numbers as a
sample of the last three days' motion, not a lifetime odometer.

## 1. Inventory

Each tool has design documents scattered across `docs/`, `docs/contracts/`, and `docs/generator/` or
`docs/world-export/`, plus a `tools/*/README.md` for whatever prototype or dev-driver validates it.
The table groups by the tool a document is *about*, not the folder it sits in, because folder and
subject already disagree in several places (e.g. two "Sketch" documents live in `docs/contracts/`,
not a `docs/sketch/`). A document that names no single tool is marked cross-cutting; a document that
names several is listed once with all of them noted.

### Plan / layout generation

The generator's own eight-file set (per `CLAUDE.md`), plus everything that authors, measures, or
exercises the plans it produces.

| Document | Lines | Covers |
|---|---|---|
| `docs/generator/model.md` | 1505 | Canonical pipeline, vocabulary, shape model, scoring — governing doc for the whole track. |
| `docs/generator/vocabulary.md` | 220 | Living type catalog, one row per generation type, pipeline order. |
| `docs/generator/rules.md` | 616 | The frozen v3 rule set (CT/SP/WL/LN/HB/FR/MD/BZ/EL ids). |
| `docs/generator/evaluator.md` | 479 | Deriver-measurable and evaluator-term catalogue. |
| `docs/generator/audit.md` | 138 | Measured gaps between `model.md` and the code, evidence for open G-tasks. |
| `docs/generator/seed-stats.md` | 383 | Measured per-seed corpus statistics table. |
| `docs/generator/seed-envelopes.md` | 66 | Generated soft-term bands (do not hand-edit). |
| `docs/generator/ideas.md` | 191 | The G-track idea pool, ids preserved. |
| `docs/generator/wool-approach-read.md` | 237 | A dated (2026-07) investigation memo into wool-approach shape recovery — the ninth file; see §4. |
| `docs/contracts/plan-editor.md` | 294 | The plan JSON schema and the grid editor design ("Phase 1 implementation design"). |
| `docs/contracts/plan-as-map.md` | 78 | How a plan candidate becomes a map row in the Plan→Sketch→Configure→Edit lifecycle. |
| `tools/mapgen/README.md` | 271 | CLI usage for `tools/mapgen`, a whole-map-from-JSON-spec generator. |
| `tools/mapgen/surface.md` | 478 | The plan/layout/intent/world four-layer model `tools/mapgen` should target — a synthesis doc. |
| `tools/mapgen/review.md` | 425 | Pool of `MG`-tagged findings from reviewing the first fifteen `tools/mapgen` boards. |
| `tools/compose/README.md` | 42 | Usage for 2 of the 23 dev-driver scripts in `tools/compose/`; see §3.5. |
| `scripts/generator/README.md` | 44 | Python corpus-analysis scripts, table covers all 8 files present. |
| `tools/deriver/lanes/README.md` | 56 | Hand-labelled wool-lane fixture format and the training-harness check. |
| `tools/deriver/shapes/README.md` | 18 | The 17-file base wool-approach shape catalog fixtures. |
| `tools/seeds/README.md` | 53 | Documents 3 of 48 plan documents under `tools/seeds/`; see §3.5 and the filed `B108`. |
| `tools/seeds/teaching/SHOPPING-LIST.md` | 33 | Feature/rule-id tracker for the teaching-seed set; names roughly 10 of 17 files. |

### Sketch

| Document | Lines | Covers |
|---|---|---|
| `docs/contracts/sketch-authoring.md` | 255 | Sketch persistence design, `S2` — status header says "plan"; `S2` is shipped (§3). |
| `docs/contracts/sketch-creation-flow.md` | 141 | Full-screen creation page design, `S11`/`S12` — `S11` shipped, `S12` open (§3). |
| `docs/contracts/sketch-tool-improvements.md` | 277 | Footprint/height/3D/library depth pass, `S3`–`S8` — status header says "plan"; every named sub-task is shipped or retired (§3). |
| `docs/contracts/sketch-relief.md` | 712 | Interior elevation model for a sketch shape; live twin of `tools/relief`. |
| `docs/contracts/sketch-world-export.md` | 201 | Synthesising a real Anvil world + `map.xml` from a sketch. |
| `docs/sketch-tool-ux-review.md` | 131 | Outside-eye UX critique of the shipped Sketch tool; read-only assessment, not a spec. |
| `tools/relief/README.md` | 47 | Interior-elevation prototype, file-by-file table matching all 6 source files. |

### Configure

| Document | Lines | Covers |
|---|---|---|
| `docs/contracts/new-map-authoring.md` | 617 | The declarative intent model — teams, spawns, build space, wools — status "backend landed". |
| `docs/contracts/region-authoring.md` | 200 | Region authoring surface; self-marked superseded for new maps, kept for editing existing ones. |
| `docs/contracts/monument-suggestion.md` | 130 | World-scan monument suggestion spec; backend complete, UI not built. |
| `docs/contracts/monument-candidate-store.md` | 361 | Design for a pre-computed monument-candidate table; explicitly "design only". |
| `docs/contracts/objective-suggestion.md` | 172 | Core and destroyable suggestion from a world scan. |
| `docs/contracts/destroyables-and-cores.md` | 693 | DTM/DTC parse/store/generate/place — opening premise is stale; see §3. |
| `docs/contracts/filter-region-wiring.md` | 71 | The wiring relationship (region × event → filter) and the v1 template catalog. |
| `docs/contracts/region-categorization.md` | 322 | How the studio derives a region's gameplay category; cites a moved source path (§3). |
| `docs/monument-patterns.md` | 176 | Corpus analysis of monument-surround patterns; the study behind `MonumentSuggester`. |
| `docs/region-data-flow.md` | 142 | Region persistence, derivation, and draft-bucket flow. |
| `docs/filter-use-cases.md` | 623 | Corpus analysis of filter/apply-rule semantic use cases, with a filter-vocabulary appendix. |
| `docs/contracts/water-lanes.md` | 247 | Water lanes, the mid-match-bridgeable route mechanism; graduated out of `destroyables-and-cores.md` §14. |

### Edit

No document in the set is *about* the Edit tool. It appears only as a comparison column inside
cross-cutting documents (`tool-consistency.md`'s entry/identity table, `canvas-interaction.md`'s
"sixteen components mount `WorldCanvas`" inventory, `primitive-styles.md`'s four-editor styling
audit) and as the "editing existing maps" carve-out inside `region-authoring.md`, which is itself
about Configure-era authoring superseded for new maps. See §5.

### World export ("realize" — the pass from plan/sketch to a playable world)

| Document | Lines | Covers |
|---|---|---|
| `docs/world-export/structures.md` | 707 | Adaptive structure stamping (rooms, pads, styles) — status "implemented (G31)". |
| `docs/world-export/terrain-painting.md` | 450 | Terrain material painting (walls, rims, plateaus) — status "built and shipped". |
| `docs/world-export/decoration.md` | 504 | Flora, paths, scatter, canopy dressing pass. |
| `docs/world-export/finishing-model.md` | 265 | Persistence/authoring design for theming + dressing — status "part-settled, part-draft". |
| `docs/world-export/tree-corpus.md` | 304 | Findings from the 75-tree hand-built measurement fixture. |
| `docs/world-export/ideas.md` | 99 | Dressing-stage idea pool. |
| `docs/contracts/terrain-ground-truth.md` | 88 | What island detection counts as ground when scanning a world bottom-up. |
| `docs/contracts/traffic-ground-truth.md` | 108 | Player-traffic log format and derivation, self-contained. |
| `docs/contracts/block-palette.md` | 103 | Legacy-block-id-to-colour lookup shared by every top-down render. |
| `tools/decorate/README.md` | 36 | Dressing-pass prototype (`prototype.html`, `dress-map.cs`). |
| `tools/tree-corpus/README.md` | 100 | Tree-corpus measurement harness, covers all 10 scripts present. |
| `tools/traffic/README.md` | 47 | Recovered-footprint + traffic-graph pairs (ingwaz). |

### Cross-cutting or whole-repo (no single tool)

| Document | Lines | Covers |
|---|---|---|
| `docs/contracts/project-structure.md` | 187 | Project boundary inventory — companion to `CLAUDE.md`'s "Code placement" rule (§2). |
| `docs/contracts/geometry-consolidation.md` | 247 | Rationale record for the `PgmStudio.Geom` leaf — overlaps `CLAUDE.md`'s Symmetry note (§2). |
| `docs/contracts/canvas-interaction.md` | 219 | The client JS layer shared by all four tools' canvases. |
| `docs/contracts/primitive-styles.md` | 168 | Drawing-style audit across Sketch/Edit/Configure/Plan. |
| `docs/contracts/tool-consistency.md` | 129 | Entry, identity, and phase-model consistency across the four tools. |
| `docs/contracts/ui-conventions.md` | 244 | Shared Blazor component vocabulary over the CSS design system. |
| `docs/contracts/routing-and-ia.md` | 207 | URL shape and information architecture for the three map surfaces. |
| `docs/contracts/include-resolution.md` | 84 | `<include>` resolution — reading a map as PGM plays it vs. as the repo holds it. |
| `docs/design-decisions.md` | 102 | Grab-bag of non-obvious invariants that read as bugs, collected from review passes. |
| `docs/cloud-setup.md` | 115 | Cloud-container runbook; explicitly the cloud-specific half of `CLAUDE.md`'s Environment section. |
| `docs/match-flow.md` | 811 | Match-flow reading + played account; spans generator (board figures), traffic, and gameplay — owned by none of them alone. |

## 2. Overlap map

The most valuable finding in a doc set this size is not any one stale fact but the places where a
fact is stated more than once, because a duplicate is a fact that goes stale in one copy while the
other still reads current. Five clusters of duplication were found, ranging from a single number
repeated verbatim to five documents that each partially describe the same cross-tool machinery.

**The `PgmStudio.Geom.Symmetry` canonical claim is stated twice at full strength.** `CLAUDE.md`'s
Environment section (the "Symmetry / orbit math" bullet) and `docs/contracts/geometry-consolidation.md`
both assert, independently, that `PgmStudio.Geom.Symmetry` is the one canonical leaf with the same
member list (`Order`/`Point`/`Rect`/`Apply`/`ReflectPoint`/`RotatePoint`) and the same "do not add a
third copy" framing. `geometry-consolidation.md` is the fuller rationale record and `CLAUDE.md`'s
version reads as a condensed pointer to it, but nothing marks the relationship that way in the text —
if the leaf's member list changes, both need editing and only one is likely to get it.

**`CLAUDE.md`'s "Code placement" rule and `docs/contracts/project-structure.md` describe the same
project boundaries**, and this one is at least self-aware about it: `project-structure.md` opens by
calling itself "companion to the `## Code placement` rule in `CLAUDE.md` (the *rule*) ... this doc is
the *map*." That is a reasonable split (short rule, long inventory) but it means the *rule itself* —
which project a kind of code belongs in — exists in two independently-editable places, and only
`project-structure.md` carries the "headline finding: the cross-project boundaries are sound"
verdict that a reader would want to check against `CLAUDE.md`'s prose.

**The monument-suggester precision/recall figures (96.6% precision / 57.8% recall / 35 false
positives over 1721 monuments) are stated as fact in four places**: `CLAUDE.md`'s Verification
section, `docs/monument-patterns.md` (the source measurement), `docs/contracts/monument-suggestion.md`
(references the same corpus validation), and `docs/contracts/monument-candidate-store.md` (restates
the exact numbers to justify a design). A re-run of the corpus measurement — which `monument-patterns.md`
itself says has already happened once, "re-verified 2026-06-10" for a different figure — updates one
place and leaves three copies of the old number standing, with nothing that would flag the mismatch.

**Five cross-tool client documents describe overlapping ground with no single owner**:
`canvas-interaction.md` (what the JS layers are), `primitive-styles.md` (how a primitive is styled
across the four editors), `tool-consistency.md` (the phase/step model), `ui-conventions.md` (the
component vocabulary), and `routing-and-ia.md` (the URL shape). Each names a distinct question and
each explicitly cross-references the others ("read alongside"), which is the right instinct, but none
of the five is positioned as the entry point for "how does a tool actually work end to end" — a
reader has to hold all five open at once to get the full picture of any one tool's shell, and a change
to (for instance) the phase model requires remembering to check whether `canvas-interaction.md`'s
sixteen-component inventory or `ui-conventions.md`'s status table also needs the same edit.

**The region/filter/wiring cluster — `filter-region-wiring.md`, `filter-use-cases.md`,
`region-categorization.md`, and `region-data-flow.md` — cross-reference each other and a file that no
longer exists.** All three of `filter-region-wiring.md`, `region-authoring.md`, and
`region-categorization.md` point readers to `data-model.md` for the `Filter`/`ApplyRule` shapes; that
file is not present anywhere in the tree and has no trace in the visible git history, so it was either
renamed, folded into another doc, or dropped before this checkout's history begins. The four live
documents still describe the region/filter/apply-rule relationship from four different angles (wiring
templates, semantic use-cases, category derivation, persistence) without duplicating each other's
facts outright, but the missing fifth piece they all assume exists is itself a small, live instance of
doc-set decay. The same thing happened to `layout-rules.md`, cited by four separate files
(`seed-stats.md`, `evaluator.md`, `plan-editor.md`, `tools/seeds/teaching/SHOPPING-LIST.md`) as the
destination for rule amendments; `docs/generator/rules.md` — "Layout rules — the composer's v1 rule
set" — is almost certainly its renamed successor, but nothing says so.

**`tools/mapgen/surface.md` is a synthesis that duplicates four other documents' authoritative
claims by design.** Its own opening line calls it "the reference `tools/mapgen` should have been
written against" and its four-layer table (plan → layout → intent → world) restates ground that
`docs/generator/model.md` (the pipeline), `docs/contracts/plan-editor.md` (the plan schema),
`docs/contracts/plan-as-map.md` (the plan lifecycle), and `docs/contracts/sketch-world-export.md`
(the world layer) each already own. This is a defensible index document, not an accident, but it
means a schema change in any of the four source docs has a fifth place that can silently fall out of
sync.

**`docs/design-decisions.md` is a many-to-one distillation of the whole contract set by construction.**
Every entry is a short "decision — why it looks wrong — where it's enforced" triple whose enforcement
is described at length somewhere else (the rect/box exclusive-max convention, for instance, is a
geometry convention that likely also appears in whichever doc owns the affected sampling code). This
is the intended shape of a fast-lookup document, but it means each of its ~dozen entries is a second,
compressed copy of a fact whose primary copy lives elsewhere, and updating the primary copy does not
prompt an update here.

**`docs/contracts/traffic-ground-truth.md`, `tools/traffic/README.md`, and `docs/match-flow.md` §6 all
describe the same ingwaz traffic-graph validation.** This one is the least risky of the set —
`traffic-ground-truth.md` explicitly owns the format contract and calls itself self-contained, and
`tools/traffic/README.md` explicitly defers formatting questions to it — but `match-flow.md`'s own
text ("above both sits recorded traffic, which §6 reports") means the same ingwaz numbers (105
matches, 510 players, 6/6 islands, recall 1.0) are asserted in three places rather than two.

## 3. Rot ranking

The signal used is the one specified: each document's last meaningfully-edited date against how many
commits touched the code path it describes since that date. Because the visible history spans only
three days (see the opening note), the churn counts below say relatively little about long-term drift
by themselves — a document whose subject is one specific file and shows fifteen commits in three days
is a stronger signal than one whose subject is an entire project directory and shows the same number,
because the latter number is inflated by sheer subject breadth rather than genuine hot-spot activity.
The table marks broad-subject rows accordingly. This is a likelihood ranking, not a verdict: nothing
below was independently confirmed wrong except where §3.1–§3.3 say so explicitly from direct content
inspection, which is a stronger and separate kind of evidence from the date arithmetic.

### 3.1 Confirmed by content, not by date: three Sketch documents never left "plan"

Reading the Sketch design documents against `FEATURES.md` (the shipped-capability ledger) found stale
status headers that the churn metric cannot see, because the doc and the code that falsified it were
both edited inside the same three-day window:

- **`docs/contracts/sketch-tool-improvements.md`** is headed "Status: **plan**" and titled "S3–S8".
  Every one of S3, S4, S5, S6, and S7 appears in `FEATURES.md` as shipped, each cited by task id
  inline in the doc's own body (`(S3)`, `(S4, S15)`, `(S5 — rasterization...)`, `(S6)`, `(S7 —
  ...verified by unit tests...)`), and the doc's own §8 heading reads "Shape library — drag-in
  primitives (S8, retired)". The status line at the top of the file describes none of its own body.
- **`docs/contracts/sketch-authoring.md`** is headed "Status: **plan** (task `S2` in `TODO.md`)". `S2`
  is not in `TODO.md` — it is in `FEATURES.md`, shipped (the create/finish/export loop, integration-tested).
- **`docs/contracts/sketch-creation-flow.md`** is headed "Status: **planned**. Design for `S11`/`S12`".
  `S11` is shipped (`FEATURES.md`: the `/maps/new-sketch` page, the shape-tile palette). Only `S12`
  (pinning the Islands tree, parked in `BACKLOG.md`) is still open.

Three for three: every Sketch design document that carries an explicit status marker has a status
that undersells what shipped since it was written.

### 3.2 Confirmed by content: `destroyables-and-cores.md`'s opening premise is false today

The document's second sentence reads: "Today both elements are invisible to the parser — a DTM map
loads 'successfully' and silently loses its objectives (§10)." `FEATURES.md` records both fixes as
shipped ("DTM: destroyables + objective modes — parse, write, codec... 188 maps / 619 destroyables /
153 modes parse"; "DTC: cores — parse, write, codec... 127 maps / 300 cores parse, zero round-trip
drift"), and `CLAUDE.md` itself states the parser now exceeds the Python reference specifically on
"destroyables/cores". The supporting code is substantial and present:
`src/PgmStudio.Pgm/Authoring/DestroyableGenerator.cs` and `CoreGenerator.cs`,
`src/PgmStudio.Client/Features/Configure/{CoreAuthoring,CoreObjectivesStep,CoreCasingStep}.cs`,
`src/PgmStudio.Data/Features/CoreCandidateStore.cs`, `src/PgmStudio.Api/Endpoints/CoreEndpoints.cs`.
This is the single clearest content-level contradiction found in the set — a 693-line document (the
second-longest contract doc after `new-map-authoring.md`) whose framing sentence the rest of the
repository already disagrees with. The naming rules and structural analysis in its body (§1–§9) were
not checked line by line and may well still be accurate; only the opening premise was verified false.

### 3.3 Confirmed by content: a stale implementation path

`docs/contracts/region-categorization.md` cites its implementation at
`src/PgmStudio.Analysis/RegionCategorizer.cs`. The type now lives at
`src/PgmStudio.Pgm/Authoring/RegionCategorizer.cs` — `docs/contracts/project-structure.md` records the
move explicitly ("`A5`... relocated `RegionCategorizer` → `Pgm/Authoring/`"). The class was moved, not
deleted, so this is a stale pointer rather than dead content, but it is exactly the kind of small drift
the date-based ranking is meant to catch and, in this instance, did not (see §3.4).

`tools/deriver/lanes/README.md` similarly cites `WoolLaneShape` (`src/PgmStudio.Pgm/Plan/WoolLaneShape.cs`)
as the classifier its fixtures calibrate. `WoolLaneShape` is retired — `docs/generator/model.md` now
describes it as "the retired `WoolLaneShape`... a thin adapter" over `ShapeClassifier.ClassifyOpen`,
which is what `tools/deriver/lane-audit.cs` (the tool the README documents) actually calls. The README's
format description and the check command are still accurate; only the classifier name in its header
and in its "Current vocabulary" section heading are stale.

### 3.4 The numeric ranking

Subject paths were assigned from each document's own cited implementation (a specific `.cs` file where
one was named, the owning project directory otherwise). Rows whose subject is an entire project or
`src/` itself are marked broad and should be read as noise, not signal — the high counts there are an
artifact of counting every commit that touched anything in a large directory, not evidence that the
specific claims in the document moved.

| Document | Last edited | Commits to subject since | Subject | Read |
|---|---|---|---|---|
| `docs/contracts/project-structure.md` | 2026-08-11 | 64 | `src/` (whole tree) | broad — not a real signal |
| `docs/design-decisions.md` | 2026-08-11 | 64 | `src/` (whole tree) | broad — not a real signal |
| `docs/world-export/finishing-model.md` | 2026-08-11 | 41 | `Minecraft` + `Data` | self-declared "part-draft"; real hot spot |
| `docs/world-export/structures.md` | 2026-08-12 | 28 | `Minecraft` + `Domain` | narrow-ish subject, high churn |
| `docs/world-export/decoration.md` | 2026-08-12 | 26 | `Minecraft` | narrow-ish subject, high churn |
| `docs/world-export/ideas.md` | 2026-08-12 | 26 | `Minecraft` | idea pool, expected to trail shipped code |
| `docs/contracts/include-resolution.md` | 2026-08-11 | 16 | `PgmStudio.Pgm` (whole project) | broad-ish |
| `docs/contracts/tool-consistency.md` | 2026-08-11 | 16 | `Client/Features` (all tools) | broad |
| `docs/contracts/sketch-authoring.md` | 2026-08-11 | 16 | `Pgm/Sketch` + `Client/Features/Sketch` | confirmed stale by content, §3.1 |
| `docs/filter-use-cases.md` | 2026-08-11 | 16 | `PgmStudio.Pgm` (whole project) | broad |
| `docs/contracts/canvas-interaction.md` | 2026-08-11 | 14 | `Client/wwwroot/js` | self-declares "current, verified against tree" |
| `docs/contracts/geometry-consolidation.md` | 2026-08-11 | 12 | `PgmStudio.Geom` | narrow subject, moderate churn |
| `docs/contracts/sketch-creation-flow.md` | 2026-08-11 | 11 | `Client/Features/Sketch` | confirmed stale by content, §3.1 |
| `docs/sketch-tool-ux-review.md` | 2026-08-11 | 11 | `Client/Features/Sketch` | read-only critique, expected to trail |
| `docs/contracts/sketch-tool-improvements.md` | 2026-08-11 | 10 | `Pgm/Sketch` | confirmed stale by content, §3.1 |
| `docs/world-export/tree-corpus.md` | 2026-08-12 | 8 | `Geom` + `tools/tree-corpus` | narrow, moderate |
| `docs/contracts/primitive-styles.md` | 2026-08-11 | 6 | `Client/wwwroot/js/studio/render` | narrow, low-moderate |
| `docs/contracts/plan-editor.md` | 2026-08-11 | 3 | `Client/Features/Plan` + `Pgm/Plan` | narrow, low |
| `docs/contracts/plan-as-map.md` | 2026-08-11 | 3 | `PgmStudio.Data` | narrow, low |
| `docs/region-data-flow.md` | 2026-08-11 | 3 | `Data` + `RegionEndpoints.cs` | narrow, low |
| `docs/generator/model.md` | 2026-08-11 | 2 | `Pgm/Plan` + `Pgm/Compose` + `Pgm/Shapes` | narrow, low |
| `docs/generator/vocabulary.md` | 2026-08-11 | 2 | same as `model.md` | narrow, low — living doc, should track closely |
| `docs/generator/audit.md` | 2026-08-11 | 2 | `Pgm/Compose` + `Pgm/Plan` | narrow, low |
| `docs/generator/ideas.md` | 2026-08-12 | 2 | `Pgm/Compose` + `Pgm/Plan` | narrow, low |
| `docs/match-flow.md` | 2026-08-11 | 2 | `Pgm/Plan` + `tools/seeds/traced` | narrow, low |
| `docs/world-export/terrain-painting.md` | 2026-08-11 | 2 | 7 named `TerrainPainter`-family files | narrow, low — "built and shipped" self-claim holds |
| `docs/contracts/ui-conventions.md` | 2026-08-11 | 2 | `Client/Components` | narrow, low — matches self-declared partial status |
| `docs/contracts/destroyables-and-cores.md` | 2026-08-11 | 1 | `Pgm/Authoring` + 2 named files | low by date; **false by content**, §3.2 |
| `docs/contracts/new-map-authoring.md` | 2026-08-11 | 1 | `Pgm/Authoring` + `Client/Features/Configure` | narrow, low |
| `docs/generator/seed-stats.md` | 2026-08-11 | 1 | `tools/seeds` | narrow, low |
| `docs/generator/seed-envelopes.md` | 2026-08-11 | 1 | `tools/seeds` + `tools/deriver` | generated file, regenerate-on-change |
| `tools/compose/README.md` | 2026-08-11 | 2 | `tools/compose` | 21 of 23 scripts undocumented regardless of churn, §3.5 |
| `tools/relief/README.md` | 2026-08-12 | 1 | `tools/relief` | narrow, low |
| `tools/seeds/README.md` | 2026-08-11 | 1 | `tools/seeds` | coverage gap is the real issue, §3.5 / `B108` |
| remaining ~24 documents | 2026-08-11/12/13 | 0 | (see per-doc subject above) | zero in a 3-day window; not evidence of freshness |

### 3.5 READMEs that describe a fraction of their own directory

`tools/seeds/README.md` documents 3 of 15 top-level plan documents and neither of the two subfolders
(`traced/`, 16 real-map traces; `teaching/`, 17 feature-teaching plans) — already filed as `B108`, not
addressed here. It is not the only README in this shape, and one other case is a larger gap by file
count:

- **`tools/compose/README.md` documents 2 of the 23 scripts in `tools/compose/`** — `matrix.cs` and
  `gallery-gen.cs`. Undocumented: `showcase.cs`, which `CLAUDE.md` itself names as "`model.md`'s live
  twin" and a load-bearing verification tool; `seat-probe.cs` and `unit-gallery.cs`, both cited by
  `docs/generator/audit.md` as the source of its measured frequencies; `house-showcase.cs`,
  `body-gallery.cs`, `box-gallery.cs`, `edge-gallery.cs`, `mid-gallery.cs`, `board-gallery.cs`,
  `seed-showcase.cs`, `u-hub-showcase.cs`, `stalemate-probe.cs`, `sweep-saturation.cs`,
  `teaching-render.cs`, `compare-seeds.cs`, `nearest-seed.cs`, `exemplar-feasibility.cs`,
  `reproduction-gate.cs`, `unit-fingerprint.cs`, `fingerprints.cs`. This is the largest coverage gap
  found in the set — a reader pointed at this README to find `showcase.cs` would not learn it exists.
- **`tools/seeds/teaching/SHOPPING-LIST.md`** names files inconsistently: some rows cite a filename
  directly (`build-interface-dos-and-donts.plan.json`, `mirror-mid-examples.plan.json`) or a wildcard
  (`rot-90-mid-example-*`, covering 8 files), but seven of the directory's 17 plan files —
  `build-region-examples`, `crammed-frontline-double-band`, `crammed-frontline-single-band`,
  `double-band-middle-void-no-steps`, `double-frontline-pocket-mid-internal-crossing`,
  `double-frontline-pocket-mid-rotation-stone`, `overstretched-middle-void` — appear in no row at all.

By contrast, `scripts/generator/README.md`, `tools/deriver/lanes/README.md`,
`tools/deriver/shapes/README.md`, `tools/relief/README.md`, `tools/decorate/README.md`,
`tools/tree-corpus/README.md`, and `tools/traffic/README.md` were checked against their directories
and each accounts for every file present — `tools/relief/README.md` even carries a file-by-file table.
The gap is specific to `tools/seeds/` and `tools/compose/`, not systemic.

## 4. The `docs/generator/` ninth file

`CLAUDE.md` states the folder holds "eight files, no others." It holds nine; the file outside the
table is `docs/generator/wool-approach-read.md`.

It is not a stray or a duplicate of one of the eight — it is a different *kind* of document. The
other eight are living companions, each with an explicit maintenance discipline (`vocabulary.md`
updates "in the same commit" as a type change; `audit.md` entries "leave... when the fix lands").
`wool-approach-read.md` is dated in its own title ("the G50–G52 / G62 investigation (2026-07)"), reads
as a single empirical investigation from measurement through verdict to a proposed formulation change,
and closes with a dated addendum ("2026-07-16") recording the author's ruling and what was actually
implemented as a result — at which point, by its own text, "`G56` is retired" and "`G62`/`G68` are
reworded", i.e. most of its forward-looking content has already been folded into `model.md` and the
task board. What remains live in it is a historical record of *why* those decisions were made, which
none of the eight canonical files are positioned to hold (`audit.md` records current gaps, not settled
investigations).

This makes it a genuine ninth document in substance, not an oversight, but it does not fit the table's
contract either. Two directions close the gap without losing the record: fold the addendum's settled
conclusions into `model.md`'s history or `audit.md` and retire the file, or move it out of
`docs/generator/` to sit beside the scripts it validates (`scripts/approach_read_lab.py`,
`scripts/approach_read_gallery.py`, both already under version control) where a dated investigation
memo is a more natural fit than in a folder whose contract promises living companions only.

## 5. Which tools have no authoritative document

This is the question the inventory in §1 was built to answer, and grouping by tool rather than by
folder makes it visible directly.

**Edit has none.** No document in the 62-file set is about the Edit tool. It surfaces only as a
column in comparison tables inside documents about all four tools together
(`tool-consistency.md`, `canvas-interaction.md`, `primitive-styles.md`), and as the "editing existing
maps" carve-out that `region-authoring.md` keeps live after being superseded for new-map authoring by
`new-map-authoring.md`. There is no single place a reader lands to learn what the Edit tool does or
how it differs from Configure beyond "Configure creates, Edit opens existing" — a fact stated in
passing by `routing-and-ia.md`, not established by any document that owns it.

**The Generator tool's own UI has none.** `docs/generator/*` and `tools/compose/*` document the
*generation algorithm* — the composer, its rules, its scoring — not the `Client/Features/Generator`
surface a user actually opens (`GeneratorTool.razor`), which browses, sieves, and pins candidate
plans. `docs/contracts/tool-consistency.md` says so explicitly: "The Generator is out of scope (it's a
gallery/composer that produces plans, not a per-map editor)." The only trace of that browse/sieve/pin
gallery in any document is one clause inside `docs/contracts/plan-as-map.md`, which is about
persistence, not the UI. A reader wanting "how does the Generator tool work" has model.md-depth
coverage of what it produces and nothing on how a person uses it.

**Sketch has design documents but no current one.** Every Sketch document that carries a status
marker says "plan" or "planned" (§3.1), even where the feature has fully shipped — there is no
document in the set that reads, the way `canvas-interaction.md` does for the client JS layer, "Status:
current, describes what is in the tree today." `sketch-relief.md` comes closest in spirit (it is
explicitly a live twin of a running prototype, `tools/relief`), but it covers one feature slice, not
the tool as a whole.

**Plan and Configure are the two best-served tools**, though neither has one owning document either.
Plan's ground is split across `model.md` (algorithm), `plan-editor.md` (schema/editor, itself
un-updated from "Phase 1 implementation design"), and `plan-as-map.md` (lifecycle) — three documents
that between them cover the tool without overlap, which is a healthier pattern than Sketch's, but
still no single entry point. Configure's `new-map-authoring.md` is the closest thing in the whole set
to an authoritative, current, single-document description of a tool — it carries an explicit "backend
landed" status and is treated as the reference other Configure-area documents build on
(`destroyables-and-cores.md`, `objective-suggestion.md`, `monument-suggestion.md` all point back to
it) — but even it is bounded to the intent-authoring flow and does not cover the region/filter editing
surface, which is `region-authoring.md`'s (superseded-but-kept) territory.

## 6. Provably dead documents

None were deleted. The rule applied — delete only a document whose code, types, endpoints, or files
verifiably no longer exist — was checked against every document that carried a suspicious signal, and
none crossed it:

- **`docs/contracts/destroyables-and-cores.md`** describes a *before* state that the code has moved
  past (§3.2), but the code it names (`Destroyable`, `Core`, `DestroyableGenerator`, `CoreGenerator`)
  all exist and are the DTM/DTC implementation. The document is stale, not dead — its subject is more
  built than it says, not gone.
- **`docs/contracts/region-categorization.md`** cites a moved file path (§3.3); `RegionCategorizer`
  itself is very much present at its new location.
- **`tools/deriver/lanes/README.md`** names a retired class, `WoolLaneShape` (§3.3), but the fixture
  format and the check command it documents (`tools/deriver/lane-audit.cs`) are live and were run
  successfully as part of this review.
- **`data-model.md`** and **`layout-rules.md`**, cited by seven documents combined, do not exist
  anywhere in the tree and have no trace in the visible git history (§2). But neither is itself one of
  the 62 documents under review — they are dangling citations inside otherwise-live documents, which
  is a fact reported here rather than a document deleted.
- **`docs/contracts/region-authoring.md`** and **`docs/contracts/monument-candidate-store.md`** both
  read as superseded-or-unbuilt at first pass (the former self-declares its view-model split
  superseded; the latter self-declares "design only"), but both describe things that are either still
  in active use (`RegionEditor.cs` exists and is wired into filter/region endpoints) or not yet built
  at all — a design document for unbuilt work cannot be "dead," since it never described something
  that existed and later vanished. Both are left in place and reported here as ambiguous, per the
  brief.

If a document is found later that names a symbol no longer in the tree, delete it the same way this
review would have: grep the symbol first, cite the negative result, then remove the file — never on
suspicion alone.
