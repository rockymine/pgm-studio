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

`wood-skeleton.cs` — the same question asked of all 75, with the foliage disregarded. Every block of wood is
filed by its strongest join to the next (the ladder `leaf-contact.cs` reads a leaf on), the network is
decomposed into a stem and the limbs leaving it, and the thinning is measured both by branch order and by
steps out from the stem — the second owing nothing to the decomposition. Also reports the stem: its lean
measured on the bole, how it steps, how low the crown starts and how far the leader climbs. The wool tree is
printed on its own as the control, since `wool-skeleton.cs` already knows its answer.

```
dotnet run tools/tree-corpus/wood-skeleton.cs [regionDir] [--logs-only] [--grower] [--families] [--per-tree] [--sketch [N]]
```

Carpentry counts as wood by default, because the family that branches with dark oak slabs has no wood network
without it; `--logs-only` turns that off and is how the claim is checked (it breaks 13 of 75 log networks into
pieces). `--grower` reads `TreeSkeleton.Grow` + `SweptVolume.Sweep` over a height × seed sweep instead of a
world, through the same code, so the two columns in the doc are the same measurement. `--sketch` draws each
network as an elevation — stem, limbs and doublings by character — which is the fastest way to see whether a
decomposition is reading a tree or a mess.

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

`grower-gate.cs` — the scorecard. Grows and foliates a tree exactly as the dressing pass does — limbs swept
into wood, clusters placed on the tips, the crown filtered to what the tree holds — and prints every measure
the corpus supplies beside the corpus's own figure for it. This is the reading behind the "what the grower
does with it" table in `tree-corpus.md`, and the one to take before and after touching `TreeSkeleton`,
`TreeCrown` or `SweptVolume`, since a change that improves one measure and wrecks another shows up in the same
glance. `--by-height` breaks it down per size, which is how a crown that solidifies only on the biggest trees
is caught.

These three need no world — they are pure `PgmStudio.Geom`. Note that `dotnet run <script>.cs` caches its
build keyed on the script, so `rm -rf ~/.local/share/dotnet/runfile/grower-gate-*` before measuring a change
under `src/` or the old binary reports pre-change numbers with no error.
