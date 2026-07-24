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
- Plan (map, stage=plan) → open in Sketch → stage=sketch, seeding a `sketch_layout_json` from the plan
  geometry (the rectilinear→shapes handoff — a **separate** feature, not this change).
- Sketch → finish → configure → edit (existing).

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
