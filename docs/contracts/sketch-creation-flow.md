# Sketch creation flow — move Setup to a full-screen creation page

> **Status: planned.** Design for `S11`/`S12` (`TODO.md`, Sketch tool §). Resolves the heaviest part of
> the Sketch-tool UX review's **P0#1** (`docs/sketch-tool-ux-review.md`) and the creation-flow asymmetry
> with Configure. Touches `routing-and-ia.md` (the Sketch primary action). No data-model change.

## Problem

Two findings, one root.

1. **The editor sidebar buries the island tree (P0#1).** On load, `SketchEditor`'s left column stacks
   **Setup** (6 fields) → **Layers** → the **Shapes library** (12 tiles in 3 groups + help) → **Islands**,
   inside one `workspace-scroll` at `--sidebar-width: 280px`. The Islands tree — referenced on every edit —
   sits at the very bottom, scrolled off-screen on a normal viewport. Setup is touched *once per map*; the
   tree is touched constantly. (Confirmed live on `h-sketch`: "ISLANDS" renders right at the bottom edge.)

2. **Sketch and Configure create maps differently.** Configure has a dedicated full-screen landing
   (`/maps/new`, `ConfigureLanding`): its own route, phase chrome, centered content. Sketch has an **inline
   disclosure** inside the listing — `Home.razor:75-106` injects Name + "Start blank" / archetype+seed +
   "Generate" *between the section header and the list*, in the 960px column, with no footprint or symmetry
   at all. The set-once frame is instead authored in the editor sidebar (problem 1).

**Root:** footprint + symmetry are **creation-time** decisions wearing editor-sidebar clothing. Author them
on a dedicated creation page (mirroring `/maps/new`), land in the editor already framed, and the always-open
Setup block leaves the sidebar.

## What makes this cheap: the frame is already persisted

`setup = { bbox, center, mirror_mode }` is **already a per-sketch stored property**, not live-only editor
state:

- The bridge's `getState()` writes `setup` into the sketch layout JSON; `load(state)` calls
  `applySetup(state.setup)` (`sketch-bridge.js`).
- `SketchEditor` reads `setup` back on mount and syncs the Setup controls (`SketchEditor.razor.cs:65-86`).
- A blank sketch is currently seeded `"{}"` (`SketchEndpoints.cs:58`), so the frame starts at the bridge
  default (landscape 120×80 / `rot_180`) until the user edits the sidebar.
- The **generate** path already bakes a frame in from the archetype (`SketchLayoutPrep.ForEditor`); e.g. the
  `H` archetype loads as Custom 90×90 / Mirror Z.

So this is a **relocation of where the frame is authored**, not a new data concept. The bridge `load()` and
the editor's load-sync already consume a pre-seeded `setup` — neither needs a change.

## Decisions (locked)

1. **Move primary frame authoring to creation; keep a *collapsed* "Frame" accordion in the editor** as an
   escape hatch (reframe mid-draw), not the always-open 6-field block.
2. **Generate mode does not expose footprint/symmetry** — the archetype owns the frame. Blank mode exposes
   them. Keeps the model honest and the screen uncluttered.
3. **A real route, `/maps/new-sketch`**, full-screen, mirroring `/maps/new`. Replaces the inline disclosure
   so the two creation flows are structurally identical.

## The creation page — `/maps/new-sketch`

A full-screen page in the workspace shell (topbar + 3-stage rail + centered column, ~760px). **One screen,
no Source/Found/Plan stepper** — sketch creation has no scan step, so the multi-substep machinery would be
overkill. It is built from sibling `panel-section`s under `workspace-scroll` (which supplies the `--space-6`
gap), mirroring the Configure `InfoPhase` "Identity" section layout:

- **Identity** — Map name + a **Start from** segmented picker (a `filter-group-options--fill` /
  `filter-chip` control — *not* `action-btn`-styled — with **Blank canvas** / **Generated layout**).
- **Blank** shows two more sections: **Footprint** (a row of SVG-preview `choice-tile`s
  `landscape | portrait | square | custom` + a Size W/D `coord-field` row) and **Symmetry** (a row of
  SVG-preview `choice-tile`s `mirror_x | mirror_z | rot_180 | rot_90` + a Centre X/Z `coord-field` row).
  The preset↔W/D logic (`Presets`, `InferPreset`, `SetPreset`) lives in the page's code-behind.
- **Generate** shows one section: **Starter layout** (Archetype select · Seed + reroll dice). **No**
  footprint/symmetry controls (derived from the archetype).
- A single **Continue** button (`action-btn--primary`, no icon) routes via `Submit()`: blank →
  `POST /api/sketch` (carrying the frame), generate → `POST /api/sketch/generate` → `/maps/{slug}/sketch`.

The conditional Blank/Generate sections live in a keyed `.cfg-step-panel` (`@key="tab"`) so the swap keeps
the section gap and recreates cleanly (no lucide/Blazor reconciler corruption). The icon-tile chooser is a
reusable `.choice-*` CSS set (tile chrome + `choice-fill`/`choice-outline`/`choice-axis`/`choice-dot`
thumbnails) shared by Footprint and Symmetry, styled like the sketch primitive palette (`.lib-thumb`).

The "New sketch" button on the Sketch overview becomes a plain link to this route, exactly like Configure's
`<a href="maps/new">` Import button.

## Backend — one small change

Extend `SketchCreateEndpoint` (`POST /api/sketch`) to accept the frame and seed it:

- Parse optional `{ name, width, depth, mode, centerX, centerZ }`. **When the body carries a frame** (any
  of those fields), build the origin-centred bbox (`min_x=-width/2, max_x=width/2, min_z=-depth/2,
  max_z=depth/2` — the same frame `SketchEditor.PushBbox` uses) and seed the artifact
  `{"setup":{"bbox":{…},"center":{"cx":…,"cz":…},"mirror_mode":"…"}}`. **A frameless body** (e.g. `{name}`)
  keeps seeding `"{}"` — the bridge's `DEFAULT_SETUP` then frames the editor to landscape on load, so a
  frame-agnostic caller still lands on landscape without a written blob.
- **The landscape footprint (120×80 / `rot_180` / centre 0,0) is the default** — both for an absent
  individual field inside a provided frame, and (via the bridge) for a frameless create. This is a
  *default*, not a compatibility shim: the new page always sends an explicit frame, and the only production
  caller (`Home.razor.cs:82`) is replaced in this same change. Seeding `"{}"` only when no frame is given
  keeps frame-agnostic callers (tests that check slug/dedup, asserting an empty layout) passing untouched.
- Clamp width/depth to `>= 16` (matches the editor `NumberField` min); fall back to the default on a
  malformed field. The endpoint owns the bbox math so neither Blazor page duplicates geometry on the wire.
- `SketchGenerateEndpoint`: **no change** (archetype already seeds the frame).

## Editor — drop Setup from the sidebar

- Remove the always-open **Setup** `<section>` (`SketchEditor.razor:36-72`) from `workspace-scroll`.
- Re-home the same Footprint + Symmetry controls in a **collapsed "Frame" disclosure** (`<details>`),
  default-collapsed, so mid-draw reframing is still possible without occupying the top of the column.
- All handlers stay (`OnPresetChange`/`OnWidth`/`OnDepth`/`OnModeChange`/`OnCenterX`/`OnCenterZ`/`PushBbox`,
  and the load-sync) — they still drive the bridge; they just live in the accordion now.
- Net sidebar order becomes **Frame (collapsed)** → Layers → Library → **Islands**, lifting the tree toward
  the top. (Collapsing Layers/Library is the `S12` follow-on below.)

## Listing — replace the inline disclosure

- `Home.razor`: swap the `ToggleNewSketch` button + the `creatingSketch` block (lines 61-64, 75-106) for a
  single link to `/maps/new-sketch`.
- `Home.razor.cs`: remove the create state/handlers that move to the new page —
  `creatingSketch`, `sketchName`, `genArchetype`, `genSeed`, `busy`, `createError`, `ToggleNewSketch`,
  `RerollSeed`, `CreateSketch`, `CreateGenerated` (and reset of `creatingSketch` in `OnParametersSetAsync`).

## Routing & IA doc

`routing-and-ia.md` (status: landed) names the Sketch primary action and the create transition. Update:

- "Landing & exits": Sketch's primary action is now **New-sketch → `/maps/new-sketch`** (a route, like
  Configure → Import), not an inline overview action.
- Add `/maps/new-sketch` to the route table.
- The `POST /api/sketch` transition note still holds — it now also carries the frame.

## Tests

- Integration: `POST /api/sketch` with a `{width, depth, mode, centerX, centerZ}` body seeds the artifact
  with that `setup` (a follow-up `GET /api/map/{slug}/sketch` returns it), and an omitted frame falls back
  to the landscape default. (TUnit; run the test project directly per `CLAUDE.md`.)

## Files

- **New:** `Pages/Sketch/SketchCreate.razor` (+`.cs`) at `/maps/new-sketch`; this doc.
- **Edit:** `Home.razor` / `Home.razor.cs`; `SketchEditor.razor` / `SketchEditor.razor.cs`;
  `SketchEndpoints.cs`; `routing-and-ia.md`.

## Follow-on (`S12`) — finish P0#1

With Setup gone, the remaining sidebar weight is **Layers** + the **Library** palette above **Islands**.
Pin Islands to the top by collapsing both behind `<details>` accordions (Library default-collapsed once the
map has shapes), or move the Library to a toolbar popover (it's a "reach for a primitive" action, not
persistent state). Separate task — `S11` is the relocation; `S12` is the remaining collapse.
