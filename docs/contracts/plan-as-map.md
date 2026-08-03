# Plan as a map row

The map lifecycle is a full loop — **Plan → Sketch → Configure → Edit** — all one `map` row progressing
through its `stage` field. This note records how a *plan* joins that lifecycle.

## The split (decided)
- **Generator candidates stay `plan` rows.** The composer emits many candidate plans (the browse / sieve /
  pin gallery) with provenance (`seed`, `composer_version`, `content_hash`, `structure` bucket). These are
  the raw pool — never maps. `PlanStore` + the `plan` table are unchanged.
- **An authored plan is a map row.** Committing to a candidate (opening it to author) creates a `map` row
  at **`stage = "plan"`** whose plan blob lives as a **`plan_json` map artifact** — exactly as a sketch is
  a map row at `stage=sketch` with a `sketch_layout_json` artifact. The map keeps a **`plan_source_id`** →
  the source `plan` candidate (the old fork's `parent_id`, now carried on the map).

## Lifecycle
- Generator candidate (`plan` row) → **author** → `map` (stage=plan, `plan_json` artifact, `plan_source_id`).
- Plan (map, stage=plan) → **build** → the compiled layout is written to that same map's
  `sketch_layout_json`, rasterized, and its intent applied — so the row travels plan → configure carrying
  both authoring sources (C32).
- Sketch → finish → configure → edit (existing).

The build advances the **open map row**; it does not fork one. That is what makes the stage field a real
lifecycle rather than a label: a plan compiled four times is one map refreshed four times, not four
near-identical maps whose only relationship is a slug suffix. Identity follows for free, because the
compiled intent carries the plan's name and the intent write applies it to the document. The bare
`/plan-editor` route is the exception and stays as it is — a generator candidate has no map row to
advance, so building one there originates the map. Since the built row keeps its `plan_json` beside the
`sketch_layout_json`, **reopen** (`routing-and-ia.md`) alternates between the two: the sketch from
Configuring, and the plan it was compiled from once standing in the sketch.

## Consequences
- Plan **name + authors** reuse the map-metadata endpoint (like sketch); the C27 Plan phase-model slice
  becomes "it's just a map."
- `PlanTool` routes `/maps/{slug}/plan` and persists via the plan artifact, like every other tool.
- The maps dashboard gains a **Plan** stage column.

## Endpoints
- `POST /api/plan/{planId}/author` — candidate `plan` row → new `map` (stage=plan) + `plan_json` artifact
  + `plan_source_id`; returns `{ slug }`.
- `GET /api/map/{slug}/plan` — the stored plan blob (or `{}` when absent).
- `PUT /api/map/{slug}/plan` — replace the plan blob.

## Sequencing
Backend first (migration + endpoints, curl-verified), then `PlanTool` on `/maps/{slug}/plan`
(Playwright-verified), then the dashboard column + the Generator's "author this" wiring.
