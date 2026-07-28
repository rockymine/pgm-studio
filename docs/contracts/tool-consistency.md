# Tool consistency — entry, identity & the phase model

How a user *starts* working, where a tool's **identity + settings** live, and how they get
back to them — made consistent across the four map tools. The Generator is out of scope (it's a
gallery/composer that produces plans, not a per-map editor).

## The problem this fixes

Today each tool starts differently and keeps its settings somewhere different, and some of it is
one-way:

| Tool | Entry today | Identity/settings home | Name editable after start? | Authors |
| --- | --- | --- | --- | --- |
| Configure | `ConfigureLanding` page (`/maps/new`, import) → wizard | first phase **Map Info** | yes (rail) | authors + contributors |
| Edit | none (open existing) | first phase **Overview** | yes (rail) | authors + contributors |
| Sketch | `SketchCreate` page (`/maps/new-sketch`) | entry page (name) + sidebar (footprint/symmetry) | **no — name frozen after creation** | none |
| Plan | none (canvas) | foldable sidebar **Settings** panel | yes (sidebar) | none |

Four gaps: three different settings homes; the sketch name can't be changed after creation;
authorship exists only in Configure/Edit; and the identity surface is even labelled two ways
("Map Info" vs "Overview").

## The unified model

**Every tool adopts the phase model.** No separate creation pages. A tool's identity and settings
live in a phase — a full-page surface (no sidebar) the user can always switch back to via the rail —
so the canvas stays a focused editing area.

### Phases per tool

- **Configure** — `Import` (phase zero, conditional) · **`Identity`** · World · Teams · Build · Wools · Review
- **Edit** — **`Identity`** · Setup · Teams · Build · Objective · Regions
- **Sketch** — **`Info`** (steps: `Identity`, `Settings`) · **`Draw`** (the canvas)
- **Plan** — **`Info`** (steps: `Identity`, `Settings`) · **`Draw`** (the canvas)

Notes:
- **`Identity`** is the universal name-+-authors surface, and the unified label (retires "Map Info"
  and "Overview"). Configure/Edit expose it as their first phase directly (their settings are
  spread across their many later phases). Sketch/Plan have few phases, so they wrap `Identity` plus a
  `Settings` step inside an **`Info`** phase.
- **`Info` phase steps** (Sketch/Plan):
  - `Identity` — display **name** + **author(s)**.
  - `Settings` — the tool's technical globals. **No footprint/size**: the canvas **auto-grows to the
    drawn content** (see *Drawing-surface model* below), so for
    Sketch `Settings` is **symmetry only** (mode + centre). The exported world is the tight content
    bounds (the rasterizer already derives it from the shapes, never a frame). Plan keeps its cell/surface
    globals here.
- The **drawing canvas is its own phase** (`Draw`) in Sketch and Plan — the focus area.
- **Reference & overlays stay on the canvas** (the `Draw` phase), *not* in `Settings` — they are
  aids the user toggles *while drawing*, not configuration set once up front.

### Drawing-surface model (Sketch + Plan)

Both drawing canvases present the same three-part surface, shared via `render/canvas-chrome.js`:

1. **The working area** — a **tinted region**, present at a **default size even when nothing is drawn**,
   that grows to enclose the content plus a buffer (snapped out to grid lines). It is a **size anchor,
   not a fence**: it tells the author how big a map is meant to be, so a blank canvas doesn't invite a
   sprawling one. Drawing past its edge visibly grows it. Defaults:
   | Tool | Default working area | Grid step | Buffer past content |
   |---|---|---|---|
   | Sketch | **64×64 blocks** (4×4 chunks) | 16 blocks (chunk) | 1 chunk |
   | Plan | **~60×60 blocks** (12×12 cells at the default cell size 5) | `globals.cell` blocks | 3 cells |

   The plan default is expressed in **blocks and converted to whole cells**, so the anchor stays ~60
   blocks across whatever `globals.cell` is.
2. **The grid spans the visible viewport**, not the working area — so the surface is never fenced in and
   growing the drawing is just drawing where you already see grid. It is rebuilt only when the snapped
   visible extent actually moves, not per pan frame.
3. **A scale bar** (`N blocks`, bottom-right, screen space) carries absolute size. The working area's
   edge moves as the drawing grows, so it cannot serve as the scale reference itself.

**Fit** frames the working area with a margin of grid visible around it (`FIT_PAD_FRACTION`) — never
edge-to-edge, or there would be nothing visibly outside it to draw into.

Both canvases **re-measure themselves** with a `ResizeObserver` on their wrap, and defer a fit requested
while they have no layout box. A tool must **not** nudge `resize()` from a phase switch: that call runs
before the phase div is re-rendered, so it measures the still-hidden element.

### Component naming — `*Phase` vs `*Step`

A component's suffix states its altitude, so the file name never lies about the rail/flow-bar level it
sits at:
- **`*Phase`** — a component that renders a **whole phase**: a single-step phase (`IdentityPhase`,
  Edit's `SetupPhase`/`RegionsPhase`/…) or a phase that hosts its own steps inline (`ImportPhase`,
  `SketchInfoPhase`, `PlanInfoPhase`).
- **`*Step`** — a component that renders **one step of a multi-step phase**. The Configure wizard's
  multi-step phases (World, Teams, Build, Wools, Review) have no wrapper component — the phase is a
  `ConfigurePhase` record, and each step is a `*Step` leaf (`WorldScanStep`, `TeamAssignStep`,
  `BuildLayerStep`, `WoolMonumentsStep`, `ReviewXmlStep`, …). Edit's phases are all single-step, so
  every Edit body is a `*Phase`.

### Entry flow

The **map list** (`Maps.razor`, filtered by stage) is the shared starting surface. A **create /
import** button at the top enters the target tool's first phase.

- **Configure** enters at **`Import`** (phase zero) — paste a download link **or** pick a folder →
  creates the map. `Import` is **conditional**: it appears only for a new/unimported map. Opening an
  already-imported map (or one whose world is already set) **skips `Import`** and lands on `Identity`.
  The world is never re-picked once set.
- **Sketch / Plan** enter at **`Info`** (Identity + Settings), then `Draw`.
- **Edit** has no creation step — it opens an existing map directly on `Identity`.

## Authors

- **Configure + Edit** — **authors and contributors** (unchanged).
- **Sketch + Plan** — **authors only** (no contributors).
- **Username-verified in every tool** — the same Minecraft username → head/UUID resolution
  Configure/Edit already use (`InfoPhase`/`OverviewPhase` author rows). Reuse that component.

## Name editability

The display **name** is always editable in the `Identity` surface. The map keeps its stable **ID**;
only the display name changes, so renaming after creation is safe (no re-keying, no new record).
This closes the sketch "frozen name" gap.

## Storage

Keep it simple: **add an authors field to each tool's own record** (the sketch draft, the plan doc)
next to how Configure stores authors in intent-meta and Edit in the map's raw metadata. A **shared
"map identity" record** that authorship follows across stages is a **later** idea, not this work.

## Out of scope (later)

- **Collapsible/hideable side panels as a universal pattern.** Plan's foldable sidebar is a genuinely
  useful idea (more canvas room) that may later become the standard for all panels. This work instead
  *empties* Plan's sidebar (identity/settings move into the `Info` phase); revisiting collapsible
  panels app-wide is a separate future pass.
