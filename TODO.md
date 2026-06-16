# pgm-studio — Tasks (open work only)

The live board. **It holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a
task is done, a commit lands (its message references the id), the task **leaves this file**, and a line
is added to **`FEATURES.md`** (the shipped-capability catalog). The board rules live in `CLAUDE.md`
(§ "Status & task board"); follow them — this file kept exploding when they were ignored.

Task ids are a section letter + number (`N02`, `C13`, `ND1`). Ids are **stable** (commits + memory
reference them) — never renumber; new work gets the next number in its section.

## Current focus

M0–M5 + the M6 editor shells + the M7 pipeline are **landed** (`FEATURES.md`), and the intent-model
authoring **backend** is done. The open headline is the **new `/authoring` editor** — a guided wizard
built from the concept page, a **separate page** from the existing `/editor` (left as-is for now).

1. **Settle the two design questions** (`ND1` navigation/flow, `ND2` stripped Configure) and
   **scaffold the new page** (`NS`).
2. **Build the steps in page order** (`N00`→`N05` + `NVAL`), starting with **Teams & Spawns** (`N02`)
   — the recommended first real slice.

Shared editor/canvas infra (C) serves both editors; the existing-`/editor` feature UIs (wiring,
counterparts) are **parked** until that path resumes.

---

## Authoring (N) — the new-map intent editor (`/authoring`, new maps only)

A **new guided wizard** at `/authoring` that builds a map from declarative intent
(`docs/contracts/new-map-authoring.md`; backend = the intent model in `FEATURES.md`). **Leave the
existing `/editor` (region-first, existing maps) untouched** — this is a separate page, not a refit.
Tasks are **in build order** and mirror the concept page's step buckets (00…07 + Validation). Each step
persists a slice of intent via `GET`/`PUT /map/{slug}/intent`, gated on a `map_intent_json` blob.

> The concept page (`Authoring.razor` + `Pages/Authoring/*`, named per its kicker — `InfoSection`=00,
> `WorldSection`=01, …) is the **visual reference** for every step. Settle `ND1`/`ND2` before building
> the steps they shape.
>
> The old "split view-model (Primitives/Composed/Raw)" plan is **superseded** for new maps
> (`new-map-authoring.md` §7: shaping activities use intent forms; the Regions activity in `/editor`
> keeps the full tree). The hand-wiring path (group→wire) is **parked** — the generator auto-wires.

**Design & scaffold first**
- [ ] **ND1 — Authoring navigation & flow (design).** Turn the concept page's three-level model
  (`NavModelSection`: activity rail = 6 phases · flow-bar = sub-steps · Back/Next) into a concrete
  wizard spec, and **close the gaps the page leaves open:** where do **THE FLOW** (`FlowSection`, the
  6-phase overview) and the **PRE-FLIGHT CHECKS** (`ReviewSection`: round-trip / mirror / buildability
  / traversability) actually live — a landing screen, a Review sub-step, a persistent panel? Plus phase
  locking (Review needs a connected map). Output: update the concept page + a flow note in
  `new-map-authoring.md`.
- [ ] **ND2 — Stripped Configure / World step (design + corpus).** Configure is the most unintuitive
  part; on a fresh page design the **minimal** 01-World — just enough to seed team count + spawn
  positions + a confirmed symmetry — and add complexity back only if needed. Decide: does Scan need
  **block include/exclude** (which forces a pipeline re-run → `P8`) or can it rely on the default
  top-surface scan? **Island exclusion** should work **without** a re-run (it only re-runs symmetry
  detection — B7 already invalidates that cache); confirm. Corpus-check what symmetry + island
  detection actually require. Scopes `N01`.
- [ ] **NS — New `/authoring` editor shell + relocate the concept mock.** Stand up the real wizard
  shell (activity rail + flow-bar + Back/Next + three-panel workspace) per `ND1`, intent-gated. Move
  the concept mock (`Authoring.razor` + `Pages/Authoring/*`) off `/authoring` → **`/concepts`** so the
  real editor claims `/authoring`. Leave `/editor` and `/design` as-is. *(Open: whether `/design` also
  moves under `/concepts`.)*

**Steps — in page order, each persists its slice of intent**
- [ ] **N00 — Map Info.** Identity (name; version / mode / objective auto-derived) + authors
  (username→uuid resolve via `MojangClient`, B6) → intent `meta`. (`InfoSection`)
- [ ] **N01 — World.** Scan → Islands → Symmetry, per `ND2`'s stripped design: detect terrain as
  neutral islands, confirm symmetry, seed team count + spawn positions. Reuses `SymmetryDetector` (B7)
  + island detection (P4). (`WorldSection`)
- [ ] **N02 — Teams & Spawns.** **The recommended first real slice** (`new-map-authoring.md` §11):
  teams + island assignment → spawn point → protection. Symmetry→count suggestion (`SmartSuggestion` +
  `SymmetryExpander`), orbit-fill the other teams, auto-wire protection, idempotent regenerate-on-save.
  (`TeamsSection`)
- [ ] **N03 — Build.** Build height (side-view, see `N08`) → buildable layer of over-void bridges +
  holes; the generator unions them + applies the void filter. **Live buildability overlay** (uses
  `GET /buildability`, done). (`BuildSection`)
- [ ] **NVAL — Validation gate (buildability + traversability).** Not a separate phase — the
  Build⇄Traversability loop and the export condition. Surface connected/disconnected + isolated
  spawn/wool points; on failure send the author back to Build. Uses `GET /buildability` +
  `GET /traversability` (both done). (`ValidateSection`)
- [ ] **N04 — Wools.** Colours → spawn → monuments → room. Monument count **derived** (N−1); the
  **Monument tool = the block tool** + the monument-suggester smart-detect (backend done); the
  generator wires room defense / build-break / capture. Consumes the wool / monument / resource
  endpoints under "Backend the steps need". (`WoolsSection`)
- [ ] **N05 — Review & Export.** The pre-flight checks (round-trip · mirror-consistency · buildability
  · traversability) + export; surface the **409** gate (enforced backend-side). Pre-flight placement
  per `ND1`. (`ReviewSection`)

**Surfaces & integration**
- [ ] **N06 — XML-preview surface.** The generated XML, segmented (teams / spawns / wools / regions /
  filters / apply-rules). (`XmlSection`)
- [ ] **N07 — Generated region-tree surface (read-only).** The full tree as the inspect/debug view of
  what the generator produced. (`TreeSection`)
- [ ] **N08 — Side-view + per-side focus integration.** The side-view slice is **done** (`SliceView`,
  `FEATURES.md`) — integrate it into the authoring inspector to set Y on point/block regions (lift
  spawn / monument / wool-spawn off y=0). **Fit-island** exists in parts (canvas toolbar) — refine the
  concept for per-side authoring (frame one team's quadrant while working its unit). (`FocusSection`)

**Backend the steps need (live — kept even though the existing-`/editor` overlays are parked)**
- [ ] **F2 — Wool source/availability endpoints.** `GET /wool-availability` done; add
  `POST /wool-sources` (query a drawn rect against the wool-block DB rows) + `GET /wool-suggestions`.
  → `N04`.
- [ ] **F6 — Monument-obstruction endpoint.** Service done (`SegmentIndex`/`WoolSources`); wire
  `GET /monument-obstruction`. → `N04`.
- [ ] **F7 — Resource endpoint.** Service done (`ResourceSources`); wire `POST /resources` (renewable
  auto-config). → `N04` spawn "make renewable".

## Existing editor — canvas & shared infrastructure (C)

While `/authoring` is the focus and `/editor` is frozen these are lower priority — but **shared** infra
(`C8` panel-resize, `C12` components, `C13` canvas bbox bug, `C14` helpers) serves the new authoring
editor too; **`C9`/`C11`/`C18`** are existing-`/editor`-specific.

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
  index / batch). Becomes user-visible once `N03`'s buildability overlay lands.
- [ ] **A4 — Consolidate geometry into one module.** Duplication **audited** — 5 sites
  (`SymmetryDetector`, `RegionGeometry2d`, `RegionBoundsDeriver`, `RegionParser`,
  `Pgm/Editing/Geometry2d`). Establish one geometry module (point/bounds transforms + IoU) and route
  every call site through it; mind the Pgm↔Analysis package boundary. Pairs with P7.

## Lower priority / parked

Existing-`/editor` authoring features — **not** used by the intent generator (which auto-wires), and
`/editor` is frozen. Resume when the existing-map authoring path is picked up. Their *backends* are
done (`FEATURES.md`).

- [ ] **Wire-after-group + filter-wiring UI** (ex-`N4` + ex-`F1`). Group regions in `/editor` → apply
  a wiring template by role; cross-step carve-out (complement) detection; canvas Ctrl-click
  multi-select. The wiring backend (`FilterWiring` appliers + `POST /wiring/apply`) is done.
- [ ] **Symmetry counterpart accept/reject UI + IoU equivalence** (ex-`F3` + ex-`A2`). Canvas
  preview/confirm for orbit-created counterparts + `regions_equivalent`/`is_counterpart` detection for
  dedup + symmetry-violation review. The counterpart + orbit-fill backend is done (the authoring
  generator already uses orbit-fill automatically).
- [ ] **3D / side-depth selection view** (ex-`F8` 3D half). The flat side-view slice is done (→ `N08`);
  a true 3D selection view (monument point/block + cuboid Y) needs design. Later.

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
