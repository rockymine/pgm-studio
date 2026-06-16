# pgm-studio — Tasks (open work only)

The live board. **It holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a
task is done, a commit lands (its message references the id), the task **leaves this file**, and a line
is added to **`FEATURES.md`** (the shipped-capability catalog). The board rules live in `CLAUDE.md`
(§ "Status & task board"); follow them — this file kept exploding when they were ignored.

Task ids are a section letter + number (`F4`, `C13`, `N1`). Ids are **stable** (commits + memory
reference them) — never renumber; new work gets the next number in its section.

## Current focus

M0–M5 foundation, the M6 editor shells, and the M7 pipeline are **landed** (see `FEATURES.md`). Two
fronts remain:

1. **New-map authoring — the headline.** The intent-model *backend* is done; the open work is the
   authoring **UI**. Build the wizard, starting with the **Teams/Spawns slice on `thunder_blank`**
   (N1). Design = the `/authoring` concept page + `docs/contracts/new-map-authoring.md`.
2. **Editor depth.** Wire the analysis-backed feature UIs over their already-done services (F); finish
   the cross-cutting editor/canvas infrastructure (C).

---

## Authoring (N) — the forward direction

Intent-model new-map authoring (`docs/contracts/new-map-authoring.md`) for new maps, plus the
group→wire flow for editing existing maps. The backend for both is largely shipped (`FEATURES.md`);
the work here is the **UI**. Author intent is gated on a `map_intent_json` blob — existing corpus maps
keep the region-first editor untouched.

> **Direction note.** The old "split view-model (Primitives/Composed/Raw)" plan
> (`region-authoring.md`, ex-R1b) is **superseded** for new maps by the intent wizard (N1–N3): per
> `new-map-authoring.md` §7 the shaping activities use **intent forms**, and the **Regions activity
> keeps the full tree** for inspecting existing maps. R1a (grouping) shipped — `FEATURES.md`. The
> `regions_equivalent`/`is_counterpart` IoU work (ex-A2) folds into F3.

- [ ] **N1 — Intent authoring UI: shell + first vertical slice (Teams & Spawns, `thunder_blank`).**
  Turn the `/authoring` mock into a working wizard wired to `GET`/`PUT /map/{slug}/intent`. Deliver
  Map Info + Teams & Spawns end-to-end: symmetry→team-count suggestion (reuse `SmartSuggestion` +
  `SymmetryExpander`), place team-0 spawn + optional protection, orbit-fill the other teams, auto-wire
  protection (F1 `spawn_protection` template), idempotent regenerate-on-save. Per `new-map-authoring.md`
  §5/§11 (the recommended first slice).
- [ ] **N2 — Intent authoring UI: Build + Wools slices.** *Build:* max height + over-void bridge rects
  + holes; the F4 buildability overlay as live feedback; union + void-filter wiring (F1
  `build_void_enforcement`). *Wools:* per-wool spawn / room / monuments — monument count pre-filled
  from team count; the **Monument tool = the block tool** + the monument-suggester smart-detect; wire
  room defense + build/break + capture. Per `new-map-authoring.md` §5/§6 + `region-authoring.md`
  (Objectives building blocks).
- [ ] **N3 — Intent authoring UI: Review & Export.** Surface the playability gate: run traversability
  (+ buildability), show connected/disconnected + the isolated spawn/wool points, loop back to Build on
  failure; XML preview; handle the export **409** in the UI (the gate is already enforced backend-side).
  Per `new-map-authoring.md` §6/§9.
- [ ] **N4 — Wire-after-group (existing-map editing).** Grouping ships (R1a); next is wiring the group:
  group regions → apply an F1 template by role; cross-step reference + carve-out (complement) detection
  (`region-authoring.md` "Composites & cross-step references"); canvas Ctrl-click multi-select. This is
  the call site for F1's wiring UI.

## Analysis-backed editor features (F)

The analysis **service** is ported for all of these (`FEATURES.md`); what remains is the **endpoint
and/or the Blazor UI** — each task says which.

- [~] **F1 — Filter↔region wiring UI.** Appliers + `POST /wiring/apply` done. Remaining: the UI call
  site — apply a template to a (grouped) region by role; reuse `SmartSuggestion`. Driven by N4's
  group→wire flow. (No suggestion engine — removed on purpose: spawn points are never wired.)
- [ ] **F2 — Wool availability/detection UI + 2 endpoints.** `GET /wool-availability` done. Add
  `POST /wool-sources` (query a drawn rect against the wool-block DB feature rows) + `GET /wool-suggestions`;
  Objective-step UI: draw→query, suggestion prompts, availability badges. *(May split endpoints vs UI.)*
- [~] **F3 — Symmetry authoring: accept/reject UI + equivalence detection.** Counterpart + orbit-fill +
  the Orbit toggle done. Remaining: the canvas **accept/reject** UI for orbit-created counterparts
  (today created immediately, no preview) + the `regions_equivalent`/`is_counterpart` IoU detection
  (subsumes ex-A2) to power dedup + the symmetry-violation review.
- [ ] **F4 — Buildability live canvas overlay.** Service + `GET /buildability` done; remaining: the
  4-class colour overlay (UI only). Also feeds N2's Build feedback.
- [ ] **F5 — Traversability readiness panel.** Service + `GET /traversability` done; remaining: the
  readiness/connectivity panel (UI only). Also feeds N3's gate.
- [ ] **F6 — Monument-obstruction badge.** Service (`SegmentIndex`/`WoolSources`) done; remaining:
  wire `GET /monument-obstruction` **endpoint** + the objectives-step badge.
- [ ] **F7 — Resource/renewable auto-config.** Service (`ResourceSources`) done; remaining: wire
  `POST /resources` **endpoint** + the spawn-step "make renewable" UI.
- [~] **F8 — 2.5D/3D coordinate editing.** `SliceView` side-view + Y-PATCH shipped in Build + Objective
  inspectors. Remaining: wire `OnSetY` in the **Teams + Regions** inspectors; design pass for a
  side-depth **3D selection view** (monument point/block + cuboid Y). *Needs design.*

## Editor & canvas infrastructure (C)

- [ ] **C8 — Panel resize.** The `.sidebar-handle` CSS shell exists; port the JS drag handler
  (`shared/panel-resize.js`).
- [ ] **C9 — Kits editing UI (Teams) + per-activity status dots.** Spawn `kit` is read/sent but has no
  edit UI; there is no status-dot system. *(Two sub-items — split if priorities diverge.)*
- [ ] **C11 — Wire + verify inspector edits across activities.** `OnDelete`/`OnRename` are wired only
  in Build Regions; the Regions/Teams/Objective inspectors are **unwired** (rename/delete silently
  no-op). Wire all three + verify rename/delete/coord-patch end-to-end.
- [ ] **C12 — Extract shared Blazor components.** (`Toast`/ErrorToast already done.) Remaining, by
  payoff: **`AuthorDisplay`** (cross-tool reuse with S2 — bundle the name↔uuid resolve), the
  **`Workspace`** layout shell (sidebar/canvas/inspector slots, repeated in 6 activities),
  **`SectionHeader`** (ruled title + "+ Add", ~17 uses), **`ActivityRail`** (extract when S2 lands).
- [ ] **C13 — Bug: canvas crashes on null `bounding_box`.** `buildTransform` (`transform.js`)
  destructures `min_x` off a null bbox → `JSException` "unhandled error" banner on xml-only /
  not-fully-pipelined maps. Degrade gracefully: skip render + show an empty-canvas hint when bbox is null.
- [ ] **C14 — Dedupe activity code-behind.** The repeated `Post/Patch/Delete/Send` http trio
  (Build/Objective/Teams) + the `Index`/`CollectDescendants` region-tree walkers (3–4 activities) →
  a shared `MapApiClient` and/or `EditorActivityBase` / static `RegionNode` helpers.
- [ ] **C18 — Render spawn-protection regions on the spawn-filtered canvas.** C16 split spawn into
  point/protection, but protection regions live in the "other" tree group and don't render on the
  Teams canvas. Surface them (e.g. via the subtype facet, like the draft filter).

## Backend, pipeline & internals

- [ ] **B8 — External-source endpoints.** `sources` + `import-from-url` (download a map from an
  Overcast / S3 `//download` zip link and import it). Player/Mojang already done (B6).
- [ ] **P8 — Pipeline re-run on config change.** A parameterized re-scan honouring
  `scan_layer`/`exclude_blocks` → re-detect islands → rewrite **layer-tagged** `layer.parquet` /
  `islands.json` (so B9 stops mis-serving a stale canonical). Today Configure persists the change +
  updates the preview but does **not** re-detect islands. (Island-exclusion → symmetry re-run already
  works, B7.)
- [ ] **A3 — Buildability endpoint perf.** Per-cell NTS over the grid is slow; optimise (spatial
  index / batch). Becomes user-visible once F4 lands.
- [ ] **A4 — Consolidate geometry into one module.** Duplication **audited** — 5 sites
  (`SymmetryDetector`, `RegionGeometry2d`, `RegionBoundsDeriver`, `RegionParser`,
  `Pgm/Editing/Geometry2d`). Establish one geometry module (point/bounds transforms + IoU) and route
  every call site through it; mind the Pgm↔Analysis package boundary. Pairs with P7.

## Lower priority / parked

- [ ] **S2 — Sketch tool.** `sketch_api` (get / setup / layout / overview / export) + the sketch pages.
  Completes M8. (`AuthorDisplay` from C12 is reused here.)
- [ ] **D3 — Evaluate `map_config` storage.** JSON-document artifact vs a relational table
  (`scan_layer`, `exclude_blocks`, `exclude_islands`, `confirmed`). Weigh against the "JSON for
  irregular leaves" rule. Evaluation only.
- [ ] **P7 — [Deferred decision] Consolidate the layer extractors / scan passes.** Blocked on a
  **solid-policy** decision: the layers want different ignored-block sets (Surface/Y0 = air-only;
  Segments = air ∪ non-solid); endpoint-only runs can't honour user `exclude_blocks`; a
  segment-derived surface would **not** be byte-parity with the reference. Decide: accept divergence
  vs keep the exact per-layer extractors (current). Pairs with A4.
