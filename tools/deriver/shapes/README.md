# Base wool-approach shape fixtures

The base wool-approach catalog (`docs/contracts/map-generation.md` §5 — **canonical**) materialised
as real `*.plan.json` files, one per shape: `isolated`, `i-straight`, `i-sidetuck`, `l-corner-{1,2}`,
`flanked`, `scythe-{1,2}`, `scythe-3-wide`, `h-branch-{1..4}`, `donut-{1..3}`, `plug`.

These are generated, not hand-authored — do not edit them by hand. The source of truth is the `t`/`v`/`w`
grid catalog in the contract; `tools/deriver/shapes-gen.cs` mirrors those grids inline, converts each to
the plan format, writes it here, and classifies each with the library's `WoolApproachShape` (the four-way
skeleton test: isolated → donut → plug → H → thin I/L/Z/scythe) to check it against its catalog family.
The `t`/`v`/`w` notation lives **only** in the doc and inline in the generator — it is never a persisted
format.

```
dotnet run tools/deriver/shapes-gen.cs
```

Last run: **16 OK / 0 MISMATCH / 1 W-ambiguous**. The one W-ambiguous case (`scythe-3-wide`) is the
documented boundary: a 2-wide fold and a plug are indistinguishable from the scale-free shape alone —
the block/junction predicates are relative to the realized corridor width `W`, which `t`/`v`/`w` does
not fix (see the contract's skeleton test).
