# The finishing model — persisting and authoring theming + dressing

The data-and-authoring design for the **finishing pass**: the two world passes that dress a realized map —
terrain paint (`terrain-painting.md`, TP) and dressing (`decoration.md`, DR). Those docs describe *what is
placed*; this one describes *how the placement is stored and how it is authored across the plan and sketch
tools*. It is the connective design both pass-docs assume, and it exists because the current answer — one
opaque JSON blob per map — cannot support the two things the finishing pass actually needs: a **library of
reusable styles** to draw from, and a **scope that survives editing** so a theme still lands where it was
meant to after the geometry changes.

**Status: part-settled, part-draft.** §3 (the theme/style tables, task **B44**) is a settled decision. §4–§6
are **drafts recorded here so the analysis is not lost** — they are non-final and await further design input;
nothing in them is committed until it lands its own task. Read the settled section as the plan of record and
the drafts as a working sketch.

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

## 4. Cross-tool authoring — where a theme is scoped (draft)

*Draft — non-final; the direction below is a working sketch, not a decision.*

The tension is genuine. The **plan** gives per-piece control — coarse rects, structural, "this hub is stone,
that frontline is grass." The **sketch** gives varied shapes, and fuses same-level pieces into one island.
Paint executes in the sketch world, over the sketch's *final* geometry, which is also the only geometry a
sketch-only map has. So the finishing authoring wants to live where the geometry is final, not only where the
structure was first drawn.

The draft direction is a **region-keyed scope resolved against final geometry**. A theme scope keys on a
durable region — a plan piece, a sketch shape, or an island — and the export resolves a cell to a theme by
whichever region owns it in the *built* geometry, not by a frozen rect. The plan's per-piece assignment
becomes a **coarse seed**; the sketch is where the theme is finalized, and the only place a sketch-only map
can be themed. When pieces fuse into one island, theming there is at island granularity — the accepted cost
of the merge. Concretely this implies porting the Apply-step canvas UX (click a shape, Ctrl-multiselect,
assign — G158) into the sketch tool keyed on sketch shapes, and re-deriving the theme footprint from the
built geometry rather than the plan rect. The open parts: the exact scope key, and how a plan's per-piece
seed maps onto post-merge islands.

## 5. Dressing — data model and placement (draft)

*Draft — non-final.*

Dressing is **three data shapes, not one**, and the shape decides the tool:

| Kind | Shape | Saved as |
|---|---|---|
| Scatter / overlay (flora, boulder fields) | parametric, seed-driven, applied to a scope | `{kind, scope, params, seed}` — theme-shaped |
| Stroke-drawn (paths, water channels) | a drawn centerline + a style | `{kind, centerline, style, seed}` — sketch-lasso-shaped |
| Placed props (individual boulders, trees) | hand-placed instances, or a scatter recipe | `{kind, position, shape/seed, size}` |

A map's dressing is one **polymorphic `dressing` list**, each entry tagged by kind. Dressing *styles* (a
gnarled-oak recipe, a cobble-path style, a meadow-flora recipe) are reusable in exactly the way terrain
styles are, so they want the same library/snapshot treatment as §3 — a map's dressing is an application of
library dressing styles to scopes, strokes, and points.

Placement follows the data shape. Stroke and point dressing — paths, water, individual boulders and trees —
is **not a per-piece concept**: it crosses piece and island boundaries and lands on specific terrain, so it
belongs where the fine geometry and a drawing surface are, the sketch tool (or a dedicated *Dress* stage
after it). Scatter/overlay dressing is scope-based and could be authored in either tool, but executes in the
sketch world alongside paint, so it is natural to keep it there too. The plan's contribution to the finishing
pass stays the coarse per-piece theme seed. Open: whether dressing is a stage of the sketch tool or its own,
and how dressing scopes share the theme scope model of §4.

## 6. A build order (draft)

*Draft — only the second item is committed.*

1. Region-keyed theme scopes re-derived from the built geometry — fixes the silent desync of §2 and unlocks
   sketch-only theming.
2. **The theme + style library tables — task B44** (the one settled piece; see §3).
3. Port the Apply-step canvas UX into the sketch tool (extends G158), keyed on sketch shapes.
4. Dressing as a polymorphic directive list on the same scope/library machinery, scatter recipes first (they
   are closest to themes), then the stroke and placed kinds.
