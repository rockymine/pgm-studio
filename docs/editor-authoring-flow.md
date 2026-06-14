# CTW Map Editor — Authoring Flow & Capabilities

*Status snapshot of the pgm-studio editor as of 2026-06-14. Describes what the editor can do
today, the gestures it understands, the intelligence (suggestion/detection) features that are
wired, and what remains planned. Grounded in the live source under
`src/PgmStudio.Client` and the contract specs in `docs/`.*

---

## 1. What the editor is

The editor is a Blazor WebAssembly port of the reference `pgm-map-studio` frontend, built as a
**three-panel workspace** modelled loosely on Figma:

```
left sidebar              centre                       right
┌────────────────┐  ┌────────────────────────┐  ┌──────────────────┐
│ entities/lists │  │  canvas + draw toolbar │  │   inspector      │
│ + region tree  │  │  (pan/zoom/draw)       │  │  (selected item) │
└────────────────┘  └────────────────────────┘  └──────────────────┘
```

It opens at `/editor/{slug}`. The shell (`Editor.razor`) renders a topbar (breadcrumb + error
toast + a still-disabled **Export XML** button) and a left **activity rail** that switches
between six activities. The canvas is a **hybrid**: Blazor owns the sidebar, inspector, and
state in C#; the proven vanilla-JS `EditorCanvas` (reused from the reference) owns pan, zoom,
rendering, selection, and draw gestures, bridged through `studio-canvas.js` interop.

### The six activities (rail order)

| # | Activity | Icon | Purpose | Draw-enabled? |
|---|----------|------|---------|---------------|
| 1 | **Configure** | `settings-2` | 3-step wizard: scan layer → island exclusion → symmetry confirm | dedicated layer/island/symmetry preview canvas |
| 2 | **Overview** | `book-open-text` | static map render, metadata, authors/contributors | static surface render |
| 3 | **Teams** | `users` | team CRUD, spawn points, spawn protection, spawn assignment | yes — `spawn` |
| 4 | **Build Regions** | `pickaxe` | Step 1 max build height (side-view); Step 2 build-area regions | yes — `build` |
| 5 | **Objective** | `goal` | wools, monuments, wool rooms, wool spawners | yes — `wool` |
| 6 | **Regions** | `layout-dashboard` | full region tree browser/inspector | **read-only** |

Two further rail buttons — **Filters** (`gavel`) and **Export** (`archive`) — are present but
disabled placeholders. The rail also shows a per-activity **status dot** (`data-status`),
currently driven only by Overview.

---

## 2. The authoring loop

The intended division of labour (per `docs/contracts/region-authoring.md`):

```
draw a primitive   →   group primitives into a structure   →   the engine wires the rule
   (author)               (author — the judgement call)          (preset template)
```

- **The author draws and groups** — which primitives form a wool room, a build area, a spawn
  zone — because that is human judgement.
- **The engine wires** — applies the correct filter + apply-rule by role, from preset
  templates. Players are **not** expected to hand-write filters; a custom filter constructor is
  deferred.

Today the editor implements the **draw** half of this loop end-to-end (a drawn primitive is
persisted as a region), plus symmetry-aware orbit fill and intelligent team setup. The
**group → wire** half (filter/region wiring templates) is the main planned work (§7, F1).

A freshly-drawn region is categorised **`other`** until it is wired to a use, because
`RegionCategorizer` is **usage-derived** and only runs over the full `map.xml` — it cannot
reflect a live editor draw. This is a known display gap (§7, E9/E10).

---

## 3. Current authoring capabilities (per activity)

### 3.1 Configure (the entry wizard)

A dedicated 3-step flow with its own preview canvas (`configure-renderer.js`), rebuilt on par
with the reference:

1. **Scan layer** — layer chips (surface / y0 / bedrock / base) with a live per-layer pixel
   preview; per-layer block include/exclude lists (persisted via `exclude-block`).
2. **Island exclusion** — include/exclude detected islands on the islands canvas; excluding an
   island re-runs symmetry detection (cache invalidation).
3. **Symmetry confirmation** — symmetry modes shown confidence-sorted with "detected" badges,
   editable centre X/Z + reset; `PATCH /symmetry` on finish, then routes to Overview.

**Known limitation:** changing the scan layer or block exclusions persists and updates the
preview but does **not** re-detect islands — that needs a pipeline re-run the port doesn't have
yet (§7, P8).

### 3.2 Overview

Static, pixelated top-surface render (ported `overview-renderer.js` + `shared/block-render.js`)
with the symmetry axis/centre overlay. Also hosts **map metadata** and the **authors/
contributors** list, with Mojang name↔uuid resolution (name→uuid on blur, uuid→name on load;
persisted to the `author` table).

### 3.3 Teams

- **Team CRUD** — add/rename/delete; edit display name, chat colour, dye colour, max/min
  players. "Add Team" auto-picks the next unused team colour and a unique id.
- **Spawn Points** vs **Spawn Protection** — the `spawn` category is split by **subtype**:
  `point` (the literal spawn from `spawns[].region`) vs `protection` (the surrounding anti-grief
  zone). Listed in two separate sections.
- **Spawn assignment** — assign a spawn region to a team or observer; set yaw and kit; unlink.
- **Draw** — `spawn`-category regions can be drawn directly on the canvas.
- **Intelligence** — symmetry-driven team suggestion (§5.1).

### 3.4 Build Regions

- **Step 1 — Maximum build height.** A **side-view canvas** (`sideview-canvas.js`, backed by
  `GET /segments?axis=x|z`) shows the map profile; **drag the height line** or type a value.
  X/Z axis toggle re-fetches; the height round-trips both ways and persists across axes.
- **Step 2 — Build regions.** Build-area region tree + canvas + inspector (delete/rename).
- **Draw** — `build`-category regions can be drawn on the Step 2 canvas.

### 3.5 Objective

- **Wools** — add/delete; edit colour, owning team, block position (X/Y/Z), wool-room region id.
  "Add" is gated to the next available dye colour.
- **Monuments** — per-wool monument list; add/delete; edit team, block position, monument region.
- **Wool Regions tree** — the wool-role region tree.
- **Draw** — `wool`-category regions can be drawn on the canvas.
- The objective is **one `wool` category** split by subtype `room` / `monument` / `spawner`
  (the editor lists Wool Rooms / Monuments / Wool Spawners).

### 3.6 Regions

Read-only browser of the **full nested region tree** (`/regions/tree`) with the shared
`RegionInspector`. No draw tools — this is the faithful model view, not an authoring surface.

---

## 4. Supported gestures

Gestures are handled by the reused canvas JS (`canvas-base.js` for navigation,
`editor-draw-controller.js` for drawing). The Blazor toolbar selects the active tool.

### 4.1 Navigation (all canvases)

| Gesture | Action |
|---------|--------|
| **Mouse wheel** | Zoom in/out, anchored at the cursor |
| **Middle-button drag** | Pan (works in any tool mode) |
| **Left-drag in Move mode** | Pan |
| **Left-click** | Select the region under the cursor (round-trips to C# `OnCanvasSelect`) |
| **Mouse move** | Live cursor coords (`X … Z …`) + live zoom % readout (written directly in JS, hot path) |
| **Mouse leave** | Clears the coord readout |

The toolbar always offers **Move** (`hand`, `M`) and **Select** (`mouse-pointer-2`, `S`).

### 4.2 Drawing (only on draw-enabled activities, when `DrawCategory` is set)

The toolbar exposes six creation tools; each completes a draw that POSTs `/regions` with the
activity's category, then reloads the canvas and refreshes the sidebar.

| Tool | Icon | Gesture | Emitted shape |
|------|------|---------|---------------|
| **Rectangle** | `rectangle-horizontal` | click-drag, release | `rectangle` (min/max x,z) |
| **Cuboid** | `box` | click-drag, release | `cuboid` (min/max x,z) |
| **Cylinder** | `cylinder` | click centre, move, click rim | `cylinder` (base x,z + radius), default height 10 |
| **Circle** | `circle` | click centre, move, click rim | `circle` (centre x,z + radius) |
| **Block** | `square` | click | `block` (single block at x+0.5, z) |
| **Point** | `dot` | click | `point` (x+0.5, z) |

Draw feedback:
- Rectangle/cuboid show a **live dashed preview rect** with corner anchor blocks during the drag.
- Cylinder/circle show a **dashed radius line, centre dot, dashed ellipse, and a live `r=N`
  radius label** between the two clicks.
- Any in-progress draw is cancelled on tool switch or repaint.
- On completion the tool auto-returns to **Select**.

### 4.3 Build Regions Step 1 (side-view)

| Gesture | Action |
|---------|--------|
| **Drag the height line** | Set the max build height (updates the input, marks dirty) |
| **X / Z toggle** | Switch the viewing axis (re-fetches the profile) |

---

## 5. Intelligence features (wired today)

### 5.1 Symmetry-driven team suggestion (`SmartSuggestion`)

When a map has no teams yet, the **Teams** activity reads the map's detected primary symmetry
and offers an intelligent setup card (reusable `SmartSuggestion` component — sparkle icon,
neutral "detected mode" badge, accept/dismiss):

- `rot_90` → suggests **four** teams (red / blue / green / yellow).
- `rot_180` / `mirror_*` → suggests **two** teams (red / blue).
- The card shows a short rationale ("90° rotational symmetry suggests four teams.") and a
  2-column swatch + name grid; **Accept** POSTs each team then reloads; **Dismiss** hides it for
  the session.

### 5.2 Symmetry-aware orbit fill on draw (F3)

On draw-enabled activities of symmetric maps, a toolbar **Orbit** chip (labelled with the
confirmed mode — "Orbit 90" / "Orbit 180" / "Orbit x" …) is shown and **on by default**. When a
region is drawn, the editor calls `POST /regions/{id}/orbit`, which creates the counterparts
implied by the confirmed symmetry so the region appears in **every symmetric position at once**:

- `rot_90` → 3 quarter-turn counterparts (1 → 4).
- `mirror_*` / `rot_180` → 1 counterpart (1 → 2).
- `mirror_*` → native PGM `mirror` region; `rot_180` → two chained mirrors; `rot_90` → baked
  primitive. Counterparts inherit the source's category.
- No-op on asymmetric maps; the chip is hidden when there is no confirmed symmetry.

### 5.3 Symmetry detection (Configure / Overview)

`SymmetryDetector` (island-pair transforms + polygon-IoU, confidence `0.4·support + 0.6·iou`)
computes symmetry on demand from the detected islands, surfaces detected modes confidence-sorted
in Configure step 3, and overlays the axis/centre on the Overview render. Confirm/reject/centre
persist via `PATCH /symmetry`.

### 5.4 Block-colour overlay (C6)

A **Blocks** chip on every `EditorCanvas` overlays the top-surface block colours (lazily fetched
top-surface layer) under the region outlines. Stays off on maps with no scan data.

### 5.5 Spawn / wool subtype derivation

The categorizer derives subtypes the UI relies on — spawn `point` vs `protection`, wool
`room` / `monument` / `spawner` — so the authoring lists are meaningful rather than flat buckets.

---

## 6. How a drawn region is persisted

1. The draw controller emits a shape result (`{type, bounds…}`).
2. `EditorCanvas.OnRegionDraw` builds the create payload (port of `drawResultToPayload`) keyed
   to the activity's `DrawCategory`, and `POST`s `/api/map/{slug}/regions`.
3. If **Orbit** is on, it `POST`s `/regions/{id}/orbit` to fill the symmetry counterparts.
4. The canvas reloads and the host activity's sidebar refreshes (`OnRegionCreated`).

The canvas renders the **primitive** regions whose own derived category matches the activity
(plus the "other" group on draw activities, so freshly-drawn primitives and their counterparts
remain visible in the step that drew them).

---

## 7. Planned features

Grouped by the task board (`TODO.md`). Status tags inline: **priority**, **in progress**, else to-do.

### Region authoring rework (the big one)

- **E9 — New-map authoring + show drawn regions in their step (priority).** Goal: author and
  configure a CTW map with **no pre-existing XML**. Replace the category-grouped tree with the
  **split view-model** (Primitives / Composed / Raw, scoped to the active step) so a freshly-drawn
  primitive shows structurally in the step's *Primitives* panel regardless of category. Per-step
  tools + guidance (e.g. the Objective "Block" tool surfaced as a "Monument" tool). Validate
  against `annealing_iv` + `outback_outback_edition`.
- **E10 — Show freshly-drawn regions on the draw canvas.** A drawn region currently lands in
  `other` and (partly) disappears from the step's canvas; resolve structurally with E9/R1.
- **R1 — Region authoring rework** per `docs/contracts/region-authoring.md` +
  `region-categorization.md`: the full Primitives/Composed/Raw split view-model, stacked
  collapsible sidebar sections, cross-step region references (a shared region pool), and the
  union-members vs subtracted-carve-outs grouping affordance.

### Filter ↔ region wiring + intelligent templates (the "engine wires" half)

- **F1 — Filter↔region wiring + templates (paired with E9/R1).** Port the wiring layer:
  `GET /wiring/suggestions` (scan spawns/wools/build facets → propose) + `POST /wiring/apply`
  (compose group regions + create filter + create apply-rule), plus the four v1
  **suggest-and-confirm** templates:
  1. **Build / void enforcement** — group buildable regions → `block_place=deny(void)` on the complement.
  2. **Spawn protection** — `enter=only-<team>` on a team spawn.
  3. **Wool-room defense** — `enter=not-<owner>` (defender excluded).
  4. **Wool-room build/break** — team check + material whitelist on the room.
  Interaction stance: **suggest + confirm, never silent**; "sense" is a soft warning, only
  dangling references are hard-rejected.

### Analysis-backed editor features (services ported; UI + a few endpoints remain)

- **F2 — Wool availability/detection UI** + `POST /wool-sources` (query a drawn rect) +
  `GET /wool-suggestions`; availability badges.
- **F3 — Symmetry-aware authoring (⏳ in progress).** Counterpart + orbit-fill done; **remaining:**
  the canvas accept/reject preview UI (counterparts are created immediately, no confirm step) and
  `regions_equivalent` / `is_counterpart` IoU detection.
- **F4 — Buildability live canvas overlay** (4-class colours; service + endpoint done, UI to do).
- **F5 — Traversability readiness panel** (service + endpoint done, UI to do).
- **F6 — Monument-obstruction badge** — wire `GET /monument-obstruction` + the objectives badge.
- **F7 — Resource/renewable auto-config** — wire `POST /resources` + the spawn-step "make
  renewable" UI.
- **F8 — 2.5D / 3D coordinate editing** — monument point/block + cuboid Y-coords via a side-depth
  selection view (needs design).

### Canvas & shared UI infrastructure

- **C8 — Sidebar panel-resize** drag for `.sidebar-handle`.
- **C9 — Kits editing UI** (Teams) and per-activity status dots.
- **C11 — Verify** inspector edits (rename/delete/coord patch) write end-to-end across activities.
- **C12 — Extract shared Blazor components** (AuthorDisplay, Workspace shell, ErrorToast,
  SectionHeader, ActivityRail).
- **C13 — Bug:** canvas crashes on a map with a null `bounding_box` — degrade gracefully.
- **C14 — Dedupe activity code-behind** (shared `MapApiClient`, region-tree walkers).

### Pipeline / world import

- **P7 — (deferred decision)** consolidate the layer extractors / scan passes.
- **P8 — Pipeline re-run on config change** — re-detect islands (+ symmetry) when the scan layer
  or block exclusions change (the reference uses an SSE `/pipeline/run`).

### Backend / API

- **B8 — External-source endpoints** (`sources`, `import-from-url`, `configure`).

### Analysis

- **A2 — region-geometry IoU / counterpart** for symmetry (subsumed by F3's detection).
- **A3 — Buildability endpoint perf** (per-cell NTS over the grid is slow).
- **A4 — Consolidate geometry** into one module.

### Sketch + design

- **S2 — `sketch_api`** (get / setup / layout / overview / export) + the sketch pages.

---

## 8. Notable current limitations

- **Drawn regions are uncategorised** (`other`) until wired — they show in Regions but not yet
  structurally in the step that drew them (E9/E10/R1).
- **No grouping / wiring UI yet** — the "group → wire" half of the authoring loop is the main
  planned work (F1). Players cannot yet compose unions/negatives or attach filters from the UI.
- **No accept/reject preview** for symmetry counterparts — they are created immediately (F3).
- **Configure can't re-detect islands** on a scan-layer/block-exclusion change (needs P8).
- **Export XML is disabled** in the topbar (placeholder).
- **Inspector edits** are wired in Build Regions but not fully verified across all activities (C11).
- The canvas can throw on maps with a **null bounding box** (xml-only / not-fully-pipelined maps) (C13).
