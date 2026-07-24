# Plan

Plan is the tool that authors — or receives from the layout generator — a declarative
`*.plan.json`: a mini-layout (rects on a proxy-cell grid, symmetry, markers for spawns/wools/goals)
that compiles into a `SketchLayout` + `MapIntent` pair. It is the shared substrate between hand
authoring and generation: the generator's composer emits candidate plans, and an author can draw
one from scratch or trace one over an existing map's top-down render. Either way the artifact is
the same JSON, compiled by the same pure `PlanCompiler`.

Plan is the first stage of the map lifecycle — **Plan → Sketch → Configure → Edit** — a single
`map` row progressing through its `stage` field. Opening a
generator candidate "to author" turns it from an ephemeral `plan`-table row into a real `map` row
at `stage="plan"` carrying a `plan_json` artifact; from there it moves into Sketch (seeding a
`sketch_layout_json` from the plan geometry), then Configure, then Edit, exactly like a plan built
by hand in the editor. See `docs/tools/generate.md` for the composer/candidate side of this split,
and `docs/contracts/regions-and-filters.md` for the region/filter concepts (`build.areas`,
`room`/`monuments`) that plan intent auto-wires into.

## 1. The `*.plan.json` schema

Mini-layout semantics: all footprint coordinates are **proxy cells** (integers, signed, relative
to the symmetry centre); heights are blocks.  One team's unit is authored; symmetry fans the rest.

```jsonc
{
  "plan": 1,
  "meta": { "name": "Base 2-Island", "notes": "" },
  "globals": {
    "cell": 5,                  // blocks per cell (G1; a parameter, not a constant)
    "symmetry": "rot_180",      // rot_180 | rot_90 | mirror_x | mirror_z (G4; team count = orbit order)
    "maxPlayers": 12,           // drives the envelope lint (G8)
    "surface": 9,               // base island surface height, blocks (G6)
    "headroom": 11              // build cap = surface + headroom (G6)
  },
  "pieces": [
    // rect = [x, z, w, h] in cells; surface overrides globals.surface (plateaus, EL1/EL4)
    { "id": "bar-e",  "role": "piece",     "rect": [1, 5, 2, 6] },
    { "id": "cross",  "role": "piece",     "rect": [-1, 7, 2, 2] },
    { "id": "bar-w",  "role": "piece",     "rect": [-3, 4, 2, 9] },
    { "id": "stone",  "role": "piece",     "rect": [1, 1, 2, 2], "surface": 13 },
    { "id": "wl2",    "role": "wool-room", "rect": [5, 5, 4, 4], "surface": 13, "mirrors": true }
  ],
  "zones": [
    // build zones: plain rects, MAY overlap terrain (FR1+FR2); holes for the 4-team ring (BZ4)
    { "id": "mid-band", "rect": [-3, -5, 6, 10], "holes": [] },
    { "id": "bridge-e", "rect": [3, 7, 2, 2] }
  ],
  "placements": {
    // authored for team 0 only; the compiler fans orbit images. Positions are piece-relative cells.
    "spawns": [ { "piece": "bar-e", "at": [1, 5], "facing": "front" } ],
    "wools":  [ { "piece": "bar-w", "at": [1, 8] }, { "piece": "wl2", "at": [3, 1] } ],
    "iron":   [ { "piece": "bar-e", "at": [0, 4] } ],     // SP7; optional in v1
    // DTM goals (OB14: two-team symmetries only). style/materials/float/name are all optional —
    // the compiler defaults them to pillar-3 · obsidian · 4 · "<Team> Monument".
    "destroyables": [ { "piece": "bar-w", "at": [2, 3] } ],
    // DTC goals (OB14 too). size/height/shell/openTop/name optional → 5 · 5 · 1 · false · PGM-named.
    // float+leak are one knob (DC2) — set both or neither; default 6/5 = no dig.
    "cores": [ { "piece": "mid", "at": [2, 2] } ]
  },
  "cliffs": [ { "a": "cross", "b": "bar-w" } ]            // land interfaces forced one-way (EL5)
}
```

Notes:
- `mirrors` per piece (default `true`): neutral on-axis pieces set `false`; mirrored stones stay
  `true` and are authored once (MD1/MD3).
- **No explicit interface objects.** `land` interfaces are *derived* from rect abutment; `gap`
  connectivity is *derived* from zones (below). The author never draws a connection.
- Wool colours are not authored: one wool → the team colour, several → distinct dyes (the
  existing `LaneMapGenerator` convention). Team palette from the shared slot list.
- **Optional `reference` block** (tracing provenance): `{ "map": <slug>, "offset": [x,z] cells,
  "scale": 1, "opacity": 0.5 }` records the real map this plan was traced over and where its top-down
  render sat under the grid. Authoring-only — the compiler never reads it, so it has no effect on the
  compiled layout/intent. Omitted for genuinely new (untraced) plans.

### Schema v2 (first-use corrections)

Author feedback after building the first real seed reshapes the role model:

- **Pieces are anonymous** (`role: "piece"`, the default and the only palette draw tool). `lane`,
  `hub`, and `mid` are retired as authored roles — a lane is usually *several* pieces (cut for
  elevation/corners), a hub is a junction *region* that may sit mid-piece, and both are derived
  from the assembled graph (`generate-rules.md` PC1–PC2). Lint that referenced authored roles moves
  to derived lane-chains and junction regions.
- **Two optional intent-bearing roles remain**: `wool-room` (the full room region → bedrock column
  floor, redstone entrance line, red terrain↔room interfaces in the editor) and `spawn` (the spawn
  region → iron inside it auto-renews in the XML; lint keeps iron markers inside it). ST1–ST3.
- **Interface marks**: beside `cliffs`, a `walls` list (piece-id pairs) marks pre-built approach
  walls (ST4). Marker `at` offsets are half-cell doubles (markers are stamp centres).

### Derived structure (computed, never stored)

- **Land interfaces** — two pieces connect iff they share **any positive-length** straight border.
  A border ≥ G2's corridor minimum is a full-width `land` interface; a shorter one is a `narrow`
  seam (still a connection — a walkable step/ledge). Corner/point contact never connects and lints
  PC-C; a narrow seam is legal geometry, not a lint (`generate-rules.md` Definitions).
- **Gap connectivity** — a zone connects every piece its rect (minus holes) overlaps or abuts;
  pieces sharing a zone are `gap`-linked with hop distance = the void span between their
  footprints inside the zone (lints G5/G7).
- **Frontline** — computed, not authored: the piece edges facing a zone (FR-series semantics).
- **Islands** — connected components over `land` interfaces; each becomes one `SketchIsland`.
- **Junction regions (hubs)** — computed on the *unioned* island footprint, so piece-cutting style
  cannot change the result. Every access **mouth** (a land interface or a bridge mouth on the
  boundary) is an interval with an inward direction; extrude each mouth's span perpendicular into
  the land; a junction region is the intersection of corridors from **≥3 mouths** (a 4-way "plus"
  yields the crossing rect; a 3-way T likewise; a 2-mouth corner yields nothing — corners are not
  hubs). Areal by construction — interval mouths, region output, no thinning. Exposed as an editor
  overlay ("Hubs") and the anchor for **lane chains**: a lane = the corridor between junction
  regions / dead ends, which is what width- and length-lint measure along (a lane cut into pieces
  for elevation or cornering is still one lane).
- **Climbs (elevation profile on chains)** — a *climb* is a maximal run of land-interface traversals
  with monotone elevation change. Each traversal carries a horizontal direction (interface midpoint
  to midpoint) and a delta; a climb whose direction reverses (>~120°) while still monotone is a
  **switchback/hairpin** (net displacement ≪ path length — height packed into a small footprint),
  vs a **straight ramp** (displacement ≈ length); a flat piece between climbing segments is a
  **landing**. Climbs are labeled by their top-end anchor (nearest wool room → wool approach; a
  junction/mid piece → mid ascent; else interior), and by use per team (on enemy-spawn→wool paths =
  attacker climb; on own-spawn→wool paths = defender rotation). Feeds composer vocabulary: straight
  approach vs space-packing switchback vs defensible landing.
- **Elevation transitions** — from surface deltas across `land` interfaces: 0 walk, ≤ jumpable →
  ledge, else the compiler stamps a step terrace (below), or a `cliffs` entry forces one-way
  (EL3/EL5).

### The compiler (`PgmStudio.Pgm/Plan/`)

`PlanCompiler.Compile(plan) → (SketchLayout, MapIntent)`, pure + deterministic:

- **Cells → blocks** by `globals.cell` (the v1 scale pass is this uniform multiply; the per-part
  stretch pass is future work).
- **Layout:** each piece → a rect `SketchShape` (`base_height` = surface); islands from the derived
  structure above → `SketchIsland` (mirrors flag through); setup from globals (symmetry, centre,
  bbox from extents).
- **Intent:** team defs from the orbit order + palette; team-0 placements resolved to block
  coords (piece origin + offset, y = piece surface) and fanned via `Geom.Symmetry` (yaw from
  `facing` per orbit image). `facing` is **absolute** — front = −z, back = +z, left = −x, right = +x
  on the authored unit, fanned through each orbit image (the editor and compiler agree by
  definition); wools with auto colours, empty `room`/`monuments` (auto-wired at
  export, as the seeds do); `build.areas` from zones (holes as negative rects when present),
  `maxHeight` = surface + headroom; observer auto-placed per G6 — centre, y = build cap + 5.
- **Step terraces:** a `land` interface with |Δsurface| ≥ 2 and no `cliffs` entry gets a walkable
  1-block step strip along the shared border (WL5's 1–5-deep steps; v1 = uniform 1-wide terracing,
  refine later).
- **Regression anchor:** the three existing seeds re-expressed as `*.plan.json`; compiling them
  must reproduce today's `*.layout.json` + `*.intent.json` (allowing the observer/maxHeight fields
  the plan now derives). Golden-file tests in `tests/PgmStudio.Pgm.Tests/Plan*`.

### Validation — errors vs rule lint

Two severities, both live in the editor and enforced by the compiler CLI:

- **Errors (structural):** unreachable wool from a capturing spawn (over land+gap); a
  frontline→wool path through a spawn piece (SP1); a wall mark off any land interface; a wool
  without its flat stamp plateau (WL3: the stamp footprint at one surface, to the lane edges);
  overlapping same-surface pieces are fine (they union), overlapping different-surface pieces are
  an error. (Narrow seams connect and are legal; a bare corner between separate areas is PC-C
  lint.)
- **Lint (the rules doc as a linter):** every violated `[expert]` rule cites its id — "G2:
  corridor 8 < 10", "SP2: spawn 15 from lane back, expected near back", "G5: hop 25 > 20",
  "BZ5: zone touches spawn piece". Lint never blocks compile (rules are provisional; seeds may
  intentionally break one to test the composer later).

Validator lives beside the compiler (`PlanValidator`), pure, unit-tested per rule id. Rule ids
throughout (`G1`-`G8`, `SP1`-`SP7`, `WL3`/`WL5`, `EL1`/`EL3`-`EL5`, `BZ4`/`BZ5`, `FR1`/`FR2`,
`PC1`/`PC2`/`PC-C`, `MD1`/`MD3`, `ST1`-`ST4`, `DC2`, `OB14`) are defined in
`docs/tools/generate-rules.md`.

## 2. The editor UI

Page `Features/Plan/PlanTool.razor` (+ `js/studio/plan/`), reusing the studio canvas stack
(`js/studio/canvas`, `geometry/symmetry.js` for the mirror ghost, `render` layers). Deliberately
*not* a sketch-editor mode: different model, simpler tools, no Bézier/polygon machinery.

- **Canvas:** cell grid at `globals.cell`; draw / move / resize rects snapped to cells; pieces
  colour-coded by role; zones rendered as translucent overlays; the symmetry ghost renders the
  orbit images live (non-editable).
- **Reference backdrop (tracing):** a **Reference** panel picks any processed map (`GET /api/maps`
  `hasSurface` flag) and paints its top-down block render (`GET /map/{slug}/layers/top-surface` via
  the shared `render/block-render.js` rasteriser) in a bottom canvas layer, behind the grid.
  Auto-centred on the symmetry origin, then Opacity / Offset (cells) / Scale / Recenter / Clear
  place it; the canvas is a block-unit frame, so a real 10-block lane traces as 2 cells at scale 1.
  Persisted as the schema's optional `reference` block (round-trips + restores on reload). Trace
  one team's sector and check the live mirror ghost against the map's opposing teams. Feeds the
  box-based / wool-approach vocabulary.
- **Palette:** piece roles (lane · hub · wool-room · mid) + zone tool + markers (spawn with a
  drag-to-set facing arrow, wool, iron, destroyable, core). The destroyable and core tools appear
  only for the order-2 symmetries — a goal one team defends means nothing at four (OB14).
- **Inspector (selected piece):** role, surface stepper (± a configurable step, default 2 per EL1;
  the step is a persisted editor preference — set any value in the globals panel or switch the
  common ones (1 / 2 / 3) in-context via quick-preset chips under the stepper, applied live
  mid-edit), `mirrors` toggle, id.
- **Overlays (toggleable):** derived land interfaces (green intervals; a slimmer green core where
  narrow; red only at a bare corner point), gap links through zones with hop distances, computed
  frontline edges, spawn→wool path trace, and the evaluator's fired-rule **evidence** (the Rules
  overlay — `docs/tools/generate-measurement.md`).
- **Panels:** the **Score** panel — the evaluator's live cost + every fired rule (structural errors
  + rule lint via the STRUCT / PC-C / G2 / G5 terms, then soft feel), each click-to-**isolate** its
  evidence on the canvas (the single validation surface; there is no separate lint list); plan JSON
  import/export (file download/upload — seeds live in git); autosave to localStorage.
- **Compile & test:** tabs previewing the compiled `layout.json` / `intent.json`; a **Create
  draft** button that runs the existing chain (`POST /api/sketch` → `PUT sketch` → `POST finish`
  → `PUT intent`) and surfaces the `GET /map/{slug}/export` link — draw → compile → walk the
  world in one sitting.

File-first origins: plans were originally repo files in `tools/seeds/` (like the existing seed
pairs), git as the store; see §3 below for how server-side persistence supersedes this for
authored (in-DB) plans while `tools/seeds/` remains the corpus location for hand-built seed plans.

## 3. Persistence & lifecycle

The map lifecycle is a full loop — **Plan → Sketch → Configure → Edit** — all one `map` row
progressing through its `stage` field. A plan joins that lifecycle as follows:

- **Generator candidates stay `plan` rows.** The composer emits many candidate plans (the browse /
  sieve / pin gallery) with provenance (`seed`, `composer_version`, `content_hash`, `structure`
  bucket). These are the raw pool — never maps. `PlanStore` + the `plan` table are unchanged; see
  `docs/tools/generate.md`.
- **An authored plan is a map row.** Committing to a candidate (opening it to author) creates a
  `map` row at **`stage = "plan"`** whose plan blob lives as a **`plan_json` map artifact** —
  exactly as a sketch is a map row at `stage=sketch` with a `sketch_layout_json` artifact. The map
  keeps a **`plan_source_id`** → the source `plan` candidate (the old fork's `parent_id`, now
  carried on the map).

Lifecycle:
- Generator candidate (`plan` row) → **author** → `map` (stage=plan, `plan_json` artifact,
  `plan_source_id`).
- Plan (map, stage=plan) → open in Sketch → stage=sketch, seeding a `sketch_layout_json` from the
  plan geometry (the rectilinear→shapes handoff — a separate feature from plan authoring itself).
- Sketch → finish → configure → edit (existing).

Consequences:
- Plan **name + authors** reuse the map-metadata endpoint (like sketch) — "it's just a map."
- `PlanTool` routes `/maps/{slug}/plan` and persists via the plan artifact, like every other tool.
- The maps dashboard gains a **Plan** stage column.

Endpoints:
- `POST /api/plan/{planId}/author` — candidate `plan` row → new `map` (stage=plan) + `plan_json`
  artifact + `plan_source_id`; returns `{ slug }`.
- `GET /api/map/{slug}/plan` — the stored plan blob (or `{}` when absent).
- `PUT /api/map/{slug}/plan` — replace the plan blob.

## 4. Current state / open items

Milestones **G16-G21** defined the initial build-out (`PlanModel`/parser/`PlanValidator`;
`PlanCompiler` + step terraces + seed golden-file regression; the canvas page; derived-structure
overlays + live lint; compile preview + Create-draft/export wiring; the seed shopping-list
burn-down). Check `TODO.md`/`FEATURES.md` for which of these have actually shipped — don't assume
from this doc.

Open points carried from the original design, still worth resolving if unresolved:
- `facing`: enum (`front`/`back`/`left`/`right`) vs degrees — enum matches SP3 and fans cleanly.
- Iron markers in v1: include (SP7 exists) or defer with the renewables system?
- Zone holes: full rect-list, or only the single centre hole BZ4 needs?

See also: `docs/tools/generate.md` (canonical model/terminology for interfaces, roles, pipeline,
and the composer/candidate side), `docs/tools/generate-rules.md` (the frozen rule law, cited by id
throughout), `docs/tools/generate-measurement.md` (the Rules-overlay evidence engine), and
`docs/contracts/regions-and-filters.md` (the region/filter concepts plan intent auto-wires into).
