# The finishing model — persisting and authoring theming + dressing

The data-and-authoring design for the **finishing pass**: the two world passes that dress a realized map —
terrain paint (`terrain-painting.md`, TP) and dressing (`decoration.md`, DR). Those docs describe *what is
placed*; this one describes *how the placement is stored and how it is authored across the plan and sketch
tools*. It is the connective design both pass-docs assume, and it exists because the current answer — one
opaque JSON blob per map — cannot support the two things the finishing pass actually needs: a **library of
reusable styles** to draw from, and a **scope that survives editing** so a theme still lands where it was
meant to after the geometry changes.

**Status: part-settled, part-draft.** Two tasks are committed: **B44** (§3, the theme/style library tables) and
**S23** (§7, grid-aligning the sketch — the prerequisite that decides where finishing lives). §4–§8 are
**converging drafts** — the finishing pass moves onto the sketch model, its scope keys on sketch shapes,
dressing rides the same stage, the structural stamps stay in plan/configure as read-only context, and the
finishing UI is a phase of the grid-aligned sketch. Recorded here so the analysis is not lost; the shape is
agreed, the details past B44/S23 land task by task.

## 1. Where the data lives today

Themes are an **opaque JSON blob, nowhere relational**. A plan's themes — the `doc.themes` registry
(`themeId → TerrainTheme JSON`), the `doc.mapTheme` default, and the `doc.themeScopes` box/piece scopes — are
stored as one blob: `plan_json` on `map_artifact` for a map-backed plan, or `plan.plan_json` on the bare
`/plan-editor` route. At compile, `PlanCompiler.BuildThemes` folds the scopes into a `MapIntent` (`Themes`,
`MapTheme`, `PieceThemes`, `PieceFootprints`), and creating a draft persists that as a *second* blob,
`map_intent_json`, on `map_artifact`. No `theme`, `style`, `material` or `pattern` table exists in any
migration.

A theme is a **monolith**. The theme JSON is a set of geometry knobs (`bedrock`, `closed`,
`wallOnTerrainFaces`) plus four themeable buckets — `rim`, `surface`, `wall`, `fill` — each carrying a
polymorphic material node keyed by `kind` ∈ {`solid`, `layered`, `teamTint`, `voronoi`, `noise`, `wallRun`},
recursively nestable (a pattern's palette entries are themselves materials). Every material is inlined into
its theme; nothing is shared or addressable across themes.

A theme scope is **anchored to a plan-piece rect, frozen at compile**. `PieceFootprints` is each generating
piece's `Rect` fanned across the symmetry orbit; the export resolves a cell to a theme by those rectangles
(`TerrainThemeScope.ThemeAt`, smallest footprint winning a shared cell). It is never recomputed from any later
geometry.

The sketch tool has **no theme concept and fuses geometry**. A sketch map is pure geometry — shapes and
islands, with no theme field anywhere in its model. Turning a plan into a sketch draft runs `IslandDetector`,
which 8-connectively unions adjacent same-level pieces into **one island polygon**; that fusion is in both the
display and the island/team-ownership data. The per-piece theme maps ride alongside on the separate intent
blob, untouched by the fusion.

## 2. What works, and what silently breaks

A themed plan **survives into a sketch draft and paints correctly** at export: the intent carries the themes
verbatim and `TerrainPainter` applies them through the scoped resolver — provided the sketch geometry is not
edited.

Reshaping **desyncs the theme**. Because the scope is the frozen plan rect, resizing a sketch polygon changes
the terrain but not the theme footprint: cells inside a piece's old rectangle still paint its theme, any new
area paints the map default, and the theme boundary sits on rectangles that no longer match the shape. Two
pieces fused into one island and then resized paint their two old rectangles, not the new shape. Nothing
errors; the paint simply lands on invisible geometry.

A **sketch-only map cannot be themed** at all. The theme UI is plan-only, and a sketch-origin intent never
receives theme fields, so it paints the built-in default everywhere.

One minor leak: the resolved intent that `SketchWorldBuilder` re-projects into the world XML does not copy the
theme fields. This is harmless for painting — the paint already happened over the world using the input intent
— but the re-projected intent no longer advertises the theme data it was built from.

## 3. Persistence — themes and styles as first-class data (settled: B44)

**Two concerns are currently conflated into one blob.** There is the theme a map *uses* — the per-region
scopes, map-specific, rightly stored with the map — and the *library* of reusable themes and styles to draw
from — cross-map, browsable. Today both are the same JSON, so the library does not exist: there is no way to
find "every voronoi pattern," and every author rebuilds the built-in default by hand.

**A style is the reusable unit.** The theme JSON already contains the decomposition — a theme binds a material
to each of its four buckets, and a material is the polymorphic, nestable leaf. Lifting the material into its
own row makes it a **style**: a named, kind-tagged material recipe, `{id, name, kind, params_json}`, where
`params_json` holds the material subtree. Materials nest, but the nesting stays inside the leaf rather than
being over-normalized into sub-material rows — a style is a self-contained recipe browsed by its top-level
kind. This is the thing an author reuses: "show every voronoi," "apply the cobble wall-run to this rim."

**A theme is a composition of styles.** A `theme` row carries the geometry knobs; a `theme_bucket` binding —
`{theme_id, bucket, style_id, depth, enabled}` — fixes which style fills each of `rim`/`surface`/`wall`/`fill`.
A full theme is a theme row plus its four bindings, resolving to exactly the theme JSON the painter already
consumes. This is the schema rule the rest of the map contract follows: real tables for entities that are
listed and edited, JSON for the polymorphic leaves.

**Apply is a snapshot, not a reference.** A map's applied theme copies the library theme at apply time, so a
later library edit does not silently repaint a shipped map — the same "editing forks a copy" doctrine the
generator's plan persistence uses. Authoring and browsing happen in the library; the map carries a frozen
instance in its own scope store.

This is task **B44**: the `style` and `theme`/`theme_bucket` tables, the read/write path, a migration that
lifts existing inline-blob themes into styles + themes + bindings (deduping identical materials), and the
retirement of the Client↔server theme-material duplication (`ThemeVocabulary` vs the server `TerrainThemeJson`
model) — the style/material schema becomes the one source both sides read.

## 4. The finishing pass belongs on the sketch, not the plan (draft — converging)

*Draft — the direction below is converging but not yet a locked decision.*

The finishing pass belongs on the **sketch model**, for a reason that also explains why it feels misplaced
today: the sketch rasterizer is what *makes the world*, and a generator can emit a plan but not a theme. The
plan is the simple, rectilinear, grid-bound structural layer — the thing a generator produces and the thing
an author reaches for first because the grid makes small rectangles for steps and staircases easy. Paint over
that model works, but it is authored one stage before the world it paints exists, and a generator could never
have produced it. So the plan stays structural; theming and dressing move to the sketch, where the geometry
is final and where a sketch-only map has geometry at all.

**Nothing is lost in the move — the scope target just switches.** The box and piece scopes become **sketch
shape / island scopes**, plus the full map: the same map → collection → shape hierarchy, keyed on the sketch's
own geometry instead of plan pieces. The merge that worried §2 is, by the author's own account, *helpful*
here: same-level pieces fuse into one polygon (one shape to theme), pieces that differ in height survive as
distinct islands (so the per-level distinction that matters is kept), and the sketch **cut tool** can break a
shape down further when finer control is wanted. So the scope resolves against the built geometry, and the
Apply-step canvas UX (click a shape, Ctrl-multiselect, assign — G158) **ports into the sketch** keyed on
shapes. Plan-side theming drops to at most a coarse seed, and is a candidate for retirement once the sketch
carries it.

(The sketch also justifies the move by what it adds: geometry detail the plan cannot express — per-vertex
height, and, not built today, tilting a whole shape to an angle instead of hand-configuring each vertex. That
is a sketch-geometry feature, not part of the finishing pass, but it is why the fine authoring lives there.)

## 5. Dressing rides the same finishing stage (draft)

*Draft.*

Dressing is **three data shapes, not one**, and the shape decides the tool — one of which the sketch already
has:

| Kind | Shape | Tool | Saved as |
|---|---|---|---|
| Scatter / overlay (flora, boulder fields) | parametric, seed-driven, scoped | a brush scoped to shapes | `{kind, scope, params, seed}` — theme-shaped |
| Stroke-drawn (paths, water channels) | a drawn centerline + a style | the sketch **lasso** (exists) | `{kind, centerline, style, seed}` |
| Placed props (lakes, trees, boulders) | hand-placed, or a scatter recipe | a **place / stamp tool (missing)** | `{kind, position, shape/seed, size}` |

A map's dressing is one **polymorphic `dressing` list**, each entry tagged by kind. Dressing *styles* (a
gnarled-oak recipe, a cobble-path style, a meadow-flora recipe) are reusable exactly as terrain styles are, so
they take the same library/snapshot treatment as §3. The one thing the sketch is missing is a **place/stamp
concept**: today stamping is plan-only (the structural stamps of §6), so placing a lake or a tree accurately
needs a new, dressing-owned stamp tool in the finishing stage. The lasso already covers strokes.

## 6. Reconciling the structural layer — two stamp concepts, not one moved (draft)

*Draft.*

The constraint that binds the whole design: the plan **already precedes the XML**. It carries the structural
stamps — the spawn building, the wool room, the bedrock approach walls, the objective markers — and the region
geometry (protection, build) and the volume positions, all baked into the intent and adjustable afterward in
the configure tool. The sketch does not author these; they live in the intent/DB (and are reachable enough
that the 3-D preview already draws the boxes and volumes).

The resolution is **not** to move them. The structural stamps and regions stay authored in plan + configure —
they are generator-emittable and objective-defining. The finishing stage **reads them as read-only context**,
it does not own them: the painter already touches only stone (TP6), so a spawn building or wool room is never
painted, and a dressing stamp (a lake, a tree) places against that same world and avoids them. So there are
**two distinct stamp concepts** — *structural* stamps (plan / configure, existing, precede the XML) and
*dressing* stamps (the finishing stage, new) — and the finishing stage shows the structural ones as context
the way the 3-D preview does.

## 7. The finishing stage lives on a grid-aligned sketch (draft — resolved through a prerequisite)

The placement question turns on one fact: the finishing pass runs on the **rasterized world** — every dressing
prototype assumes blocks. That is what decides its home, and it rules out both current candidates as-is. The
stages today are Plan → Sketch (geometry) → Configure (objectives / teams / regions) → export. The **freeform
sketch hides voxelization** (continuous Bézier coordinates), so dressing a river there has no blocks to land on.
The **configure stage** shows the rasterized world top-down (and an iso view) but cannot edit the shapes — it
only draws XML regions over them. So neither hosts finishing without a change.

The unlock is a **grid-aligned sketch** (task **S23**, now landed): every placed point snaps to the block grid
(`snapShape`, enforced at the store chokepoint), a **Blocks** toggle shows the *rasterized* footprint (curves
and rounded edges still draw smoothly — the overlay shows the blocks they voxelize into, via a client
`geometry/rasterize.js` that reproduces `SketchRasterizer` exactly), and a shape now stores as **block
coordinates** (the rasterization is needed downstream regardless). This was worth doing on its own — a tool that
hides that everything voxelizes is a liability — and it is the prerequisite that makes the sketch the
block-accurate surface the finishing pass needs.

With that in place the placement resolves: **finishing is a phase of the (grid-aligned) sketch, not a stage
after configure**, because the sketch is then the one place that is both geometry-editable *and* block-accurate
— configure is neither. The structural elements — the spawn building, the wool room, the iron that shapes the
XML — are shown **immutable** in the finishing phase (read as intent context, the §6 rule). What grid-align
does *not* solve, and the finishing phase still must: **assemble the full previewable world** — the rasterized
sketch, the structural stamps, and the theme paint — for dressing to place against.

## 8. A build order (draft)

*Draft — S23 and B44 are the committed tasks; the rest await their own.*

1. **Grid-align the sketch — task S23** (block-snap + rasterized preview + block-coords storage). Independent
   value; the prerequisite for everything below.
2. **The theme + style library tables — task B44** (§3).
3. Port the Apply-step canvas UX into the sketch as the **finishing phase**, keyed on shape/island scopes
   (extends G158) — unlocks sketch-only theming; structural stamps shown immutable.
4. Region-keyed scopes resolved against the built geometry, so a reshape moves the paint (fixes §2's desync).
5. Dressing in the finishing phase: assemble the previewable world, then lasso strokes first (rivers/paths — the
   tool exists), then the scatter brush, then the new dressing place/stamp tool (§5).
