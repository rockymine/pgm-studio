# Base wool-approach shape fixtures

The base wool-approach catalog (`docs/contracts/map-generation.md` §5 — **canonical**) materialised
as real `*.plan.json` files, one per shape: `isolated`, `i-straight`, `i-sidetuck`, `l-corner-{1,2}`,
`clamp`, `scythe-{1,2}`, `scythe-3-wide`, `u-flush-{1,2}`, `h-stub-{1..3}`, `donut-{1..3}`.

These are a checked-in reference of the doc's `t`/`v`/`w` grids in the actual plan format; the source of
truth is the grid catalog in the contract. The classification round-trip — build each grid, classify with
`ShapeClassifier` (isolated → donut → clamp → branch U/H → open I/L/Z/scythe), and assert the catalog
family — is a suite test: `ShapeCatalogTests` in `tests/PgmStudio.Pgm.Tests/Shapes/`. The emit↔derive
mirror and the width-invariance stress set are `ShapeMirrorTests` / `ShapeStressTests` alongside it. The
`t`/`v`/`w` notation lives **only** in the doc and inline in those tests — it is never a persisted format.

Run the shape tests with:

```
dotnet run --project tests/PgmStudio.Pgm.Tests
```
