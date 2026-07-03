# Plan editor — the seed studio (Phase 1 implementation design)

The concrete design for `docs/contracts/layout-generation.md` §8 Phase 1: the plan JSON schema,
the plan→(layout, intent) compiler, and the minimal grid editor the author uses to build the
boring-seed corpus (`docs/contracts/layout-rules.md`, seed shopping list). File-first: plans are
repo files in `tools/seeds/` (like the existing seed pairs); the studio is the editor, git is the
store.

Builds on the landed `P9` export pipeline (`tools/seeds/`, `PUT /map/{slug}/intent`,
`GET /map/{slug}/export`).

## 1. The plan schema (`*.plan.json`)

Mini-layout semantics: all footprint coordinates are **proxy cells** (integers, signed, relative
to the symmetry centre); heights are blocks. One team's unit is authored; symmetry fans the rest.

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
    { "id": "bar-e",  "role": "lane",      "rect": [1, 5, 2, 6] },
    { "id": "cross",  "role": "hub",       "rect": [-1, 7, 2, 2] },
    { "id": "bar-w",  "role": "lane",      "rect": [-3, 4, 2, 9] },
    { "id": "stone",  "role": "mid",       "rect": [1, 1, 2, 2], "surface": 13 },
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
    "iron":   [ { "piece": "bar-e", "at": [0, 4] } ]      // SP7; optional in v1
  },
  "cliffs": [ { "a": "cross", "b": "bar-w" } ]            // land interfaces forced one-way (EL5)
}
```

Notes:
- `mirrors` per piece (default `true`): neutral on-axis pieces set `false`; mirrored stones stay
  `true` and are authored once (MD1/MD3).
- **No explicit interface objects.** `land` interfaces are *derived* from rect abutment; `gap`
  connectivity is *derived* from zones (§2). The author never draws a connection.
- Wool colours are not authored: one wool → the team colour, several → distinct dyes (the
  existing `LaneMapGenerator` convention). Team palette from the shared slot list.

### Schema v2 (first-use corrections)

Author feedback after building the first real seed reshapes the role model:

- **Pieces are anonymous** (`role: "piece"`, the default and the only palette draw tool). `lane`,
  `hub`, and `mid` are retired as authored roles — a lane is usually *several* pieces (cut for
  elevation/corners), a hub is a junction *region* that may sit mid-piece, and both are derived
  from the assembled graph (layout-rules.md PC1–PC2). Lint that referenced authored roles moves to
  derived lane-chains and junction regions.
- **Two optional intent-bearing roles remain**: `wool-room` (the full room region → bedrock column
  floor, redstone entrance line, red terrain↔room interfaces in the editor) and `spawn` (the spawn
  region → iron inside it auto-renews in the XML; lint keeps iron markers inside it). ST1–ST3.
- **Interface marks**: beside `cliffs`, a `walls` list (piece-id pairs) marks pre-built approach
  walls (ST4). Marker `at` offsets are half-cell doubles (markers are stamp centres).

## 2. Derived structure (computed, never stored)

- **Land interfaces** — two pieces connect iff they share a straight border segment of length
  ≥ G2's corridor minimum (in cells: ≥ ⌈10/cell⌉). Corner-touching and sliver contact are *lint
  errors*, not connections (Definitions, layout-rules.md).
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
  ledge, else the compiler stamps a step terrace (§3), or a `cliffs` entry forces one-way (EL3/EL5).

## 3. The compiler (`PgmStudio.Pgm/Plan/`)

`PlanCompiler.Compile(plan) → (SketchLayout, MapIntent)`, pure + deterministic:

- **Cells → blocks** by `globals.cell` (the v1 scale pass is this uniform multiply; the per-part
  stretch pass is future work — layout-generation.md §7.1).
- **Layout:** each piece → a rect `SketchShape` (`base_height` = surface); islands from §2 →
  `SketchIsland` (mirrors flag through); setup from globals (symmetry, centre, bbox from extents).
- **Intent:** team defs from the orbit order + palette; team-0 placements resolved to block
  coords (piece origin + offset, y = piece surface) and fanned via `Geom.Symmetry` (yaw from
  `facing` per orbit image); wools with auto colours, empty `room`/`monuments` (auto-wired at
  export, as the seeds do); `build.areas` from zones (holes as negative rects when present),
  `maxHeight` = surface + headroom; observer auto-placed per G6 — centre, y = build cap + 5.
- **Step terraces:** a `land` interface with |Δsurface| ≥ 2 and no `cliffs` entry gets a walkable
  1-block step strip along the shared border (WL5's 1–5-deep steps; v1 = uniform 1-wide terracing,
  refine later).
- **Regression anchor:** the three existing seeds re-expressed as `*.plan.json`; compiling them
  must reproduce today's `*.layout.json` + `*.intent.json` (allowing the observer/maxHeight fields
  the plan now derives). Golden-file tests in `tests/PgmStudio.Pgm.Tests/Plan*`.

## 4. Validation — errors vs rule lint

Two severities, both live in the editor and enforced by the compiler CLI:

- **Errors (structural):** unreachable wool from a capturing spawn (over land+gap); a
  frontline→wool path through a spawn piece (SP1); sliver/corner contacts; a wool without its
  flat stamp plateau (WL3: the stamp footprint at one surface, to the lane edges); overlapping
  same-surface pieces are fine (they union), overlapping different-surface pieces are an error.
- **Lint (the rules doc as a linter):** every violated `[expert]` rule cites its id — "G2:
  corridor 8 < 10", "SP2: spawn 15 from lane back, expected near back", "G5: hop 25 > 20",
  "BZ5: zone touches spawn piece". Lint never blocks compile (rules are provisional; seeds may
  intentionally break one to test the composer later).

Validator lives beside the compiler (`PlanValidator`), pure, unit-tested per rule id.

## 5. The editor UI

New page `Pages/Plan/PlanEditor.razor` (+ `js/studio/plan/`), reusing the studio canvas stack
(`js/studio/canvas`, `geometry/symmetry.js` for the mirror ghost, `render` layers). Deliberately
*not* a sketch-editor mode: different model, simpler tools, no Bézier/polygon machinery.

- **Canvas:** cell grid at `globals.cell`; draw / move / resize rects snapped to cells; pieces
  colour-coded by role; zones rendered as translucent overlays; the symmetry ghost renders the
  orbit images live (non-editable).
- **Palette:** piece roles (lane · hub · wool-room · mid) + zone tool + markers (spawn with a
  drag-to-set facing arrow, wool, iron).
- **Inspector (selected piece):** role, surface stepper (±2, EL1), `mirrors` toggle, id.
- **Overlays (toggleable):** derived land interfaces (green intervals; red where sliver/narrow),
  gap links through zones with hop distances, computed frontline edges, spawn→wool path trace.
- **Panels:** lint list (click → highlight the offender); plan JSON import/export (file
  download/upload — seeds live in git); autosave to localStorage.
- **Compile & test:** tabs previewing the compiled `layout.json` / `intent.json`; a **Create
  draft** button that runs the existing chain (`POST /api/sketch` → `PUT sketch` → `POST finish`
  → `PUT intent`) and surfaces the `GET /map/{slug}/export` link — draw → compile → walk the
  world in one sitting.

No new server endpoints in v1; the page drives existing ones. Plan persistence server-side (a
`map_artifact` beside the sketch blob) is deferred until generated-map drafts need it.

## 6. Milestones

Filed as the current focus in `TODO.md`:

1. **G16** — `PlanModel` + parser + `PlanValidator` (errors + rule lint), pure, tested.
2. **G17** — `PlanCompiler` + step terraces + the three seed plans with golden-file regression
   against the existing seed pairs.
3. **G18** — the canvas page: grid, rects, roles, heights, markers, mirror ghost, import/export,
   autosave.
4. **G19** — derived-structure overlays + live lint panel.
5. **G20** — compile preview + Create-draft/export wiring (the walk-test loop).
6. **G21** — the author burns down the seed shopping list; each new seed lands in `tools/seeds/`
   as `*.plan.json` (+ its compiled pair, regenerated by CI/test, not by hand).

## 7. Open points (small, decide during PL-schema)

- `facing`: enum (`front`/`back`/`left`/`right`) vs degrees — enum matches SP3 and fans cleanly.
- Iron markers in v1: include (SP7 exists) or defer with the renewables system?
- Zone holes: full rect-list, or only the single centre hole BZ4 needs?
