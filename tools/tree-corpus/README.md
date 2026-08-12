# tools/tree-corpus

Measurement harness for the **hand-built tree corpus** — 75 author-built trees standing one per 19×19
platform, read as ground truth for what the grown tree in `docs/world-export/decoration.md` §6 should
produce. The findings live in `docs/world-export/tree-corpus.md`; these are the scripts that produced them.
Every script writes to stdout, so `out/` is the conventional place to capture a run — it is gitignored, and
any number in that doc is reproduced by re-running the script that owns it.

## `tree-showcase/`

The corpus world itself, committed here so the harness is self-contained: `level.dat` and four region files,
about 1 MB. It is a measurement fixture rather than anything the product ships — nothing packages it and no
runtime path reads it. `.gitattributes` marks `*.mca` and `*.dat` binary, which is load-bearing: the repo's
`* text=auto eol=lf` rule would otherwise be free to normalise a byte inside a compressed chunk, and a
corrupted region file fails silently.

## Running

Each script is a file-based single-file tool. Run them **from the repo root**, which is what the default
world path is relative to:

```
dotnet run tools/tree-corpus/<script>.cs [regionDir] [flags]
```

`regionDir` defaults to `tools/tree-corpus/tree-showcase/region`; pass another world to compare against it. Note that `dotnet run <script>.cs` caches its build keyed on the
script — after changing anything under `src/`, `rm -rf ~/.local/share/dotnet/runfile/<script>-*` or the old
binary answers with pre-change numbers and no error.

## Reading a world

`census.cs` — the inventory. Names the platform courses as flat sheets so a trunk is never fused to the floor
it stands on, clusters everything above them into tree bodies, and reports per tree the trunk wood, the canopy
species, the carpentry built into the branches, and the height. It closes with a replay of the flora tool's
stem test, tree by tree, which is what shows why a rooted-vertical-run classifier under-counts this world.

`crown-profile.cs` — the silhouette. Foliage per course and reach from the stem, then the same profile with
each tree resampled onto its own crown height so a family holding a 19-block tree and a 35-block one is not
read as tapering merely because the short ones stop early. Reports where the crown carries its bulk and how
many tiers it is built in — the two measures that separate a conifer from a broadleaf.

`wool-skeleton.cs` — the branching model. The corpus holds one tree built entirely of wool with every limb in
its own colour, so its skeleton can be read directly: what attaches to what, where along the parent, at what
angle, and how much of its parent's reach a child keeps.

`column-probe.cs` — a vertical dump of a named box, course by course, as ASCII. The debugging tool behind
every claim about what actually stands where.

```
dotnet run tools/tree-corpus/column-probe.cs <minX> <maxX> <minZ> <maxZ> [maxY] [--region <dir>]
```

## Comparing hand-built against generated

`leaf-contact.cs` — the metric the comparison turns on. Every leaf is filed by its strongest contact in its
3×3×3 neighbourhood (face, edge or corner, against wood or against another leaf), then by whether a chain of
leaves reaches wood at all, and finally by whether vanilla decay would have kept it. Pass `--carpentry` on a
world whose authors branch with slabs, so foliage hanging on a slab limb is not reported as unsupported.

`leaf-contact-per-tree.cs` — the same reading taken tree by tree, so the worst crowns can be named rather than
averaged away. `--families` groups the showcase by platform band, which is one family per band.

## Checking the grower itself

`grower-crown-check.cs` — runs the real `TreeSkeleton.Grow` + `TreeCrown` over a sweep of heights, leaf sizes
and seeds and measures exactly what the corpus was measured on, so the two are directly comparable. Reports
how many crown clusters never touch their own branch.

`grower-tip-gap.cs` — the geometry underneath that: whether a branch tip lies inside the leaf cluster meant to
hang on it, and the vertical lift that decides it.

These two need no world — they are pure `PgmStudio.Geom`.
