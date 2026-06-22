# pgm-studio — Tasks (open work only)

The live board. **It holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a
task is done, a commit lands (its message references the id), the task **leaves this file**, and a line
is added to **`FEATURES.md`** (the shipped-capability catalog). The board rules live in `CLAUDE.md`
(§ "Status & task board"); follow them — this file kept exploding when they were ignored.

Task ids are a section letter + number (`N02`, `C13`, `ND1`). Ids are **stable** (commits + memory
reference them) — never renumber; new work gets the next number in its section.

## Current focus

M0–M5 + the M6 editor shells + the M7 pipeline are **landed** (`FEATURES.md`), and the intent-model
authoring **backend** is done. The open headline is the **new Configure wizard** (`/maps/{id}/configure`)
— a guided wizard built from the concept page, a **separate surface** from the existing **Edit** editor
(`/maps/{id}/edit`, left as-is). Routes + labels settled in `docs/contracts/routing-and-ia.md`.

1. **Design questions are settled** — `ND1` (nav/flow, §12), `ND2` (stripped World, §6a), `ND3` (landing,
   §12) and `ND4` (save model, §12) are **done**; the wizard shell, the `/maps/new` import landing, and the
   intent-gated/save-on-advance wiring (`NS`) are **landed** (`FEATURES.md`).
2. **Every page-order step is built** — `N00` Map Info → `N01` World → `N02` Teams → `N03` Build →
   `N04` Wools → `N05` Review & Export (all three sub-steps: **Pre-flight**/`NVAL` · **Region tree**/`N07` ·
   **XML + Export**/`N06`) are **landed** (`FEATURES.md`). The Configure wizard now runs end-to-end, intent →
   gated export. The only remaining authoring polish is `N08` (side-view / per-side focus integration).

Shared editor/canvas infra (C) serves both editors; the existing **Edit** (`/maps/{id}/edit`) feature UIs
(wiring, counterparts) are **parked** until that path resumes.

---

## Authoring (N) — the new-map intent editor (`/maps/{id}/configure`, new maps only)

A **new guided wizard** at `/maps/{id}/configure` (UI label **Configure**) that builds a map from
declarative intent (`docs/contracts/new-map-authoring.md`; backend = the intent model in `FEATURES.md`).
**Leave the existing Edit editor (`/maps/{id}/edit`, region-first, existing maps) untouched** — a
separate surface, not a refit.
Tasks are **in build order** and mirror the concept page's step buckets (00…07 + Validation). Each step
persists a slice of intent via `GET`/`PUT /map/{slug}/intent`, gated on a `map_intent_json` blob.

> The concept page (`Authoring.razor` + `Pages/Authoring/*`, named per its kicker — `InfoSection`=00,
> `WorldSection`=01, …) is the **visual reference** for every step. Settle `ND1`/`ND2` before building
> the steps they shape.
>
> The old "split view-model (Primitives/Composed/Raw)" plan is **superseded** for new maps
> (`new-map-authoring.md` §7: shaping activities use intent forms; the Regions activity in Edit
> (`/maps/{id}/edit`) keeps the full tree). The hand-wiring path (group→wire) is **parked** — the generator auto-wires.

**Steps — in page order, each persists its slice of intent.** The **Pre-flight sub-step** (`N05`, which
also delivers the `NVAL` validation gate) is **landed** (`FEATURES.md`); the remaining Review & Export
sub-steps are below.

**Surfaces & integration** — the Review & Export phase (`N05` Pre-flight · `N07` Region tree · `N06` XML +
Export) is **fully landed** (`FEATURES.md`); only the focus-integration polish remains.
- [ ] **N08 — Side-view + per-side focus integration.** The side-view slice is **done** (`SliceView`,
  `FEATURES.md`) — integrate it into the authoring inspector to set Y on point/block regions (lift
  spawn / monument / wool-spawn off y=0). **Fit-island** exists in parts (canvas toolbar) — refine the
  concept for per-side authoring (frame one team's quadrant while working its unit). (`FocusSection`)

## Existing editor — canvas & shared infrastructure (C)

While the Configure wizard (`/maps/{id}/configure`) is the focus and Edit (`/maps/{id}/edit`) is frozen
these are lower priority — but **shared** infra (`C8` panel-resize, `C12` components, `C13` canvas bbox
bug, `C14` helpers) serves the new authoring editor too; **`C9`/`C11`/`C18`** are Edit-specific.

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

## Canvas interaction & de-duplication (CV)

A cross-cutting refactor of the **shared** `EditorCanvas` (used by **both** Edit `/maps/{id}/edit`
and the Configure wizard `/maps/{id}/configure`). Goal: wire up built-but-dead interactive UX
(resize, move), collapse render duplication, and formalise the controller pattern — **without
degrading behaviour**. Full technical spec: `docs/contracts/canvas-interaction.md`.

- [ ] **CV8 — C# symmetry label/count helper.** Collapse `SymLabel` (identical in
  `WorldScanPhase`/`WorldSymmetryPhase`) + the suggested-team-count mapping
  (`WorldSymmetryPhase`/`TeamsPhase`/`SpawnPhase`) into one shared `SymmetryInfo`. The `SpawnPhase`
  geometry copies (`PointInRing`/`Orbit`/`Reflect`/`Rotate`) already route through `PgmStudio.Geom`
  (A4, done), not here. (Contract §6.3.)
- [ ] **CV9 — Parametrise primitive drawing styles (shape + colour + style + icon).** Edit and Configure
  draw the same primitives but diverge (canvas-interaction.md §10): `renderShape` has no point case so a
  point renders as a 1×1 `<rect>` (block-like) on Edit while Configure uses an ad-hoc `marker`-flag
  `<circle>`; colour is `var(--canvas-region)` default on Edit vs an explicit team colour on Configure;
  marker = solid vs region = dashed/translucent. Make "draw a primitive" one data-driven thing: a real
  `point` render (dot/circle) in `renderShape`, a parametrised colour + style (marker/outline) instead of
  the `marker` branch, and fix `SpawnPhase`'s hardcoded `cylinder` sidebar/inspector icon → match
  `RegionNode.Icon` (point → `dot`). Pairs with CV6. (Contract §10.)

## Backend, pipeline & internals

- [ ] **B8 — Source ingestion (landing screen, `ND3`).** *(now)* **Open a local world folder** — list
  xml-less world folders under the maps roots (`region/*.mca`, no `map.xml`) → create the map record →
  `POST /map/{slug}/scan-world` (exists); the now-path that lets the landing screen validate new-map
  authoring on real terrain folders. *(later)* **`import-from-url`** — fetch + import an Overcast / S3
  `//download` zip link; the landing screen's download field stays disabled until this lands.
  Player/Mojang already done (B6).
- [ ] **P8 — Pipeline re-run on config change.** A parameterized re-scan honouring
  `scan_layer`/`exclude_blocks` → re-detect islands → rewrite **layer-tagged** `layer.parquet` /
  `islands.json` (so B9 stops mis-serving a stale canonical). Today Configure persists the change +
  updates the preview but does **not** re-detect islands. (Island-exclusion → symmetry re-run already
  works, B7.)
- [ ] **A3 — Buildability endpoint perf.** Per-cell NTS over the grid is slow; optimise (spatial
  index / batch). Becomes user-visible once `N03`'s buildability overlay lands.

## Layout generation (G) — auto map generation (lane sketch generators)

The "meaning → structure" engine: seed a draft map from lane primitives, then hand an editable
`SketchLayout` to the Sketch tool / Configure wizard. **The full staged plan, design decisions, and what
has already shipped are in the `project_sketch_generators` working memory**
(`/root/.claude/projects/-media-sf-repos/memory/project_sketch_generators.md`). Landed so far: the Geom
algorithm split + lane archetypes (`3d7879b`, `77b4747`), the generate UI + editor-ready prep (`fc45eb8`),
and Bézier lane-rounding recovery + random seed (`07f70ff`). Builds on the Sketch tool (`S2`, parked) and
the intent model (`N`). The memory labels the remaining stages `S3`/`S4`/`S5`; the board ids here are
`G1`–`G4` because the memory's `S`-stages would collide with the parked `S2 — Sketch tool` (a different
feature).

- [ ] **G1 — Auto-placement straight into Configure (memory stage S3).** `POST /map/generate`: run
  `LaneMapGenerator` for a chosen archetype/seed → a full `MapIntent` (teams, spawns, wools, bridges) that
  seeds the Configure wizard for an imported/blank map (the generate-into-a-new-*sketch* half already
  shipped). The generator's `ObjectiveHint`s surface as **editable suggestions** the author confirms or
  moves — not baked placements.
- [ ] **G2 — Pre-flight validation + protection-aware reachability (memory stage S4).** Wire the
  `MapValidity` export-gate (every wool needs a monument — already a class) + a `GET /map/{slug}/validity`
  DTO, and **port protection-aware reachability** from `scripts/generator/validate_play.py` to C#
  `Analysis/Playability`: today's `Traversability.Check` only tests connectivity, NOT spawn-protection-as-
  wall, so it passes maps the generator's Python validator would fail. Feeds the `NVAL` export gate.
- [ ] **G3 — Contested-middle shape language + refinement loop (memory stage S5).** The corpus gap, measured
  against the **studio's own detection** (`scripts/island_corpus.py`, N=347, over the studio-scanned corpus —
  `IslandDetector.DetectCleaned`, the generator's own pipeline; written up in `docs/generator-archetypes.md`):
  the Organic archetype emits the 2 team islands and **0 neutral mid pieces** (`LaneSketchGenerator.Organic`
  passes `mids: []`), but **91%** of real maps carry a contested middle — median **4 gameplay-sized neutral
  islands** (total-island median **9** vs the generator's 2; validated: annealing_iv 12 / green_gem 4 / kanto 2).
  Add a **symmetric neutral mid-set** to the generator: ~4 pieces (a `MidPieces` knob), **small/medium**
  (64–1023 blocks ≈ 4% of the team island — *not* a big central blob; only 13% of real neutrals are >25% of a
  team island), placed ~40% central / ~60% flanking between lanes, fanned by the board symmetry through
  `Assemble`'s existing mid-island slot (66% of real neutrals have a mirror twin). Reuse the noise field for
  placement + the circle/polygon shape vocabulary. Also **rework holes** — the studio detection finds holes on
  only **10%** of islands, so move `HoleChance` from per-lane 0.45 toward a per-island ~10% rate (and allow
  holes on the neutral pieces). Plus a **refine-on-feedback loop** and **seed-variation for the deterministic
  archetypes** (today only Organic varies by seed; H/Trident/Pinwheel are fixed). Re-run `scripts/island_corpus.py`
  (against the studio-scanned corpus) to re-validate. Needs UI. `G4` feeds this.
- [ ] **G5 — Pinwheel blade `Lane.Strip` self-overlaps on its tight curl.** The Pinwheel archetype's blade
  is a tight comma; `Lane.Strip`'s inner offset crosses itself (≈3 self-intersections in the raw simplified
  ring) → polygon-clipping renders a phantom hole in each blade. Independent of the Bézier rounding
  (`SketchLayoutPrep` via `RingRounding.Smooth` correctly *declines* to round a self-overlapping polygon, so
  rounding doesn't cause or hide it). Fix at the source — either clamp the blade curl radius against the lane
  width in `LaneSketchGenerator.Pinwheel`, or clip the inner offset in `Geom.Lane.Strip` so a tight
  centerline can't produce a self-crossing strip. Surfaced by the `SketchLayoutPrepTests` self-intersection
  regression (which tests a clean curved crossbar precisely because the Pinwheel blade doesn't yet satisfy it).
- [ ] **G6 — Lane decomposition (manual cut tool → ground truth → auto-cutter).** The **manual cut surface
  landed** (`/maps/{slug}/decompose`: lasso → pick two seam points → split the outline into lane + remainder,
  iterative peeling, role tag, save `lane_decomposition_json`; **one side only** via symmetry dedup; the shared
  editor canvas chrome — toolbar · focus · zoom; `FEATURES.md`). Remaining, in order:
  (a) **marker dragging** — drag a lasso∩edge marker along its edge to set a non-corner seam (kanto's prong
  *bases* need a marker on the body edge, not just existing corners — so this is needed even for 90° maps);
  (b) once enough maps are hand-cut, build the **auto-cutter** trained/validated on the gathered ground truth
  (cut at concave necks / medial axis so lanes **tile** the outline) — feeds `G3` per-lane width/length. The
  `G11` anchors **seed** it: a cut runs hub→wool tip and hub→spawn, and each resulting piece **auto-labels**
  (wool at a wool anchor · spawn at the spawn anchor · frontline where the edge meets the build region · hub =
  residual).
- [ ] **G9 — Re-scan the corpus with stair-aware detection + decompose-queue UI (remaining slice).** The
  over-split **detection fix landed** (`FEATURES.md`: `CleanColumns` + `DetectStairAware`, wired into
  `WorldFeatureWriter`/`--scan-out`/`--island-sketch`; validated on the cloned worlds with team structure
  preserved), as did the review flag + role classifier. What remains: (a) **re-scan the corpus** so the stored
  `islands.json` / `island_sketch_json` reflect stair-aware (the live DB + `pgm-studio-output` were generated with
  the legacy detection — needs the source worlds, `OvercastCommunity/CommunityMaps`+`PublicMaps` `ctw/`), and
  decide whether to refresh the `--islands` Python-parity oracle to match; (b) the residual `a_new_day` **isolated
  raised-decor specks** (≈37-block grid bits with no walkable connection — correctly `small` via
  `IslandClassifier`, but a per-island prune could drop them); (c) any **under-split / merged**
  read beyond `abstract` (whose stained-glass build-floor is now excluded from the cleaned base — see
  `FEATURES.md`): `LooksUnderSplit` is the catch-all flag; the residual lever if one is found is to fall through
  to surface-based detection when a cleaned-base component is a map-spanning low-Y slab. (d) a
  **decompose-queue UI** to show/set the review flag + island-health read (browser; deferred).
- [ ] **G10 — Frontline model from buildable adjacency (later).** Beyond per-island lanes: detect which island
  **edges touch buildable regions** and which islands a player can **step between** (adjacency across the
  buildable / bridge space) → a better **frontline** model than per-island role tags. Builds on `G8`'s
  build-area data + `G6`'s lanes. Research; needs design.
- [ ] **G12 — Detection: re-prune flying blobs above terrain (stair-aware regression).** Stair-aware
  connectivity fixed the over-split (disconnected islands) but **re-introduced** the stark-y-jump / flying-island
  problem: decorative masses floating above the map (dragons/birds) now merge back into the islands when a
  near-vertical surface chain bridges them (e.g. **Duality**, **mame_i_shrunk_the_pvpers**). Re-add a guard:
  stop joining across a **really big y-increase**, and/or identify & **prune blobs whose base sits well above the
  terrain band** (the old float-prune did this on `DetectHeightAware`; the stair surfaces now leak past it).
  **`max_build_height`** is a natural cut/prune ceiling — anything whose mass is above it is non-playable decor.
  Re-validate the over-split fixes (a_new_day/thunder) still hold after re-adding the ceiling.
- [ ] **G13 — Decompose: split the shown set along the symmetry axis, not the orbit dedup.** The current
  one-side view (`dedupBySymmetry`, centroid-orbit matching) sometimes shows a **mix of teams** — e.g. team A's
  spawn island next to team B's wool island — so the author decomposes an incoherent half. A **half-plane cut
  through the symmetry centre** (keep the side on one signed half of the mirror axis) would give a coherent
  team set. **Danger:** maps detected as **mirror-x *and* mirror-z** (4-fold / dihedral) have two axes — a naive
  half-plane can split across the wrong one and still mix teams; need to pick the axis that separates teams (or
  fall back to orbit dedup). Most-annoying live issue; the corpus is mostly `rot_180` (51/66 labeled) with a few
  single-axis mirrors, so the simple signed-half works for the common case — handle the dual-mirror case explicitly.

## Lower priority / parked

Existing-Edit (`/maps/{id}/edit`) authoring features — **not** used by the intent generator (which
auto-wires), and Edit is frozen. Resume when the existing-map authoring path is picked up. Their
*backends* are done (`FEATURES.md`).

- [ ] **Wire-after-group + filter-wiring UI** (ex-`N4` + ex-`F1`). Group regions in Edit → apply
  a wiring template by role; cross-step carve-out (complement) detection; canvas Ctrl-click
  multi-select. The wiring backend (`FilterWiring` appliers + `POST /wiring/apply`) is done.
- [ ] **Symmetry counterpart accept/reject UI + IoU equivalence** (ex-`F3` + ex-`A2`). Canvas
  preview/confirm for orbit-created counterparts + `regions_equivalent`/`is_counterpart` detection for
  dedup + symmetry-violation review. The counterpart + orbit-fill backend is done (the authoring
  generator already uses orbit-fill automatically).
- [ ] **3D / side-depth selection view** (ex-`F8` 3D half). The flat side-view slice is done (→ `N08`);
  a true 3D selection view (monument point/block + cuboid Y) needs design. Later.

- [ ] **Comment hygiene sweep — purely functional comments.** Code comments must describe behaviour
  only: **no** references to the Python reference app ("port of", "mirrors the reference", parity/oracle)
  and **no** implementation-phase / task ids (`NS`, `N00`, `B8`, `P5`, `ND2`, …). New code already
  follows this (CLAUDE.md). Sweep the existing comments across `src/` + `tests/` + `tools/` to match.

- [~] **S2 — Sketch tool.** Draw 2-D shapes → islands → world geometry, from nothing. Full plan +
  persistence design in `docs/contracts/sketch-authoring.md` (a sketch **is a draft map**; layout
  persists as a `SketchLayoutJson` map_artifact; finish rasterizes → the importer's geometry artifacts
  → Configure). **Landed:** S2a geometry (`geometry/shape.js` + `geometry/boolean.js`, +20 tests),
  S2b canvas + draw/edit controllers + `render/sketch-render.js`, S2c-Layout (`bridge/sketch-bridge.js`
  + `Pages/Sketch/SketchEditor` at `/maps/{slug}/sketch` + `SketchPanel`/`SketchInspector` tree+inspector
  — draw → live islands + mirror, select/op/override/delete/rename/mirrors),
  S2d persistence (`SketchLayoutJson` artifact + `POST /api/sketch` create + `GET`/`PUT
  /api/map/{slug}/sketch`; debounced save + load-on-mount; 4 integration tests),
  S2e finish/rasterize (`SketchRasterizer` + `WorldFeatureWriter.WriteSketchAsync` +
  `POST .../sketch/finish` + the Finish button → the sketch rasterizes into the importer's geometry
  artifacts and flows into Configure; 6 rasterizer tests), the `/maps/new` "Sketch from scratch" entry
  + the in-editor Setup panel (size/symmetry/centre). The sketch tool itself is complete (originate →
  Setup → draw → Finish). (Overview = Configure's Map Info, not a duplicated sketch step.) **Remaining:**
  end-to-end verification of a sketched map *through* the Configure wizard → Edit (depends on the N-series
  Configure flow);
  **S2d** `SketchLayoutJson` `ArtifactKind` + the `/api/.../sketch/*` endpoints (load/save) ·
  **S2e** server rasterize/finish (reuse `IslandDetector` + `Geom.Symmetry` + a `WorldFeatureWriter`
  sibling). Completes M8. (`AuthorDisplay` from C12 is reused here.)
- [ ] **P7 — [Deferred decision] Consolidate the layer extractors / scan passes.** **`ND2` settles the
  "consolidate vs keep" half: KEEP the exact per-layer extractors** — the World step uses them in distinct
  roles (cleaned `Base` = detection · `Surface` = visual aid · `Segments` = vertical), so they're a feature,
  not duplication; their per-layer default ignored-block sets (`Base` gets the expanded ND2 noise set;
  Surface/Y0 = air-only) are the solid-policy. Still open: the byte-parity sub-question — a segment-derived
  surface would **not** be byte-parity with the reference (endpoint-only runs also can't honour user
  `exclude_blocks`). Pairs with A4.
