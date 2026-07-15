# Map analysis & visualization scripts

Local, dependency-light Python for inspecting map geometry without a database. They consume the JSON the
RoundTrip tool emits and render PNGs / print metrics. Not part of the build or the test suite — diagnostic
harness, like `scripts/*_corpus.py`.

> **Note:** the lane-sketch **generator** (archetype starters — H/Pinwheel/Trident/Organic) has been
> retired in favour of the plan-then-realize direction, so the `--gen-sketch` / `--gen-map-preview` /
> `--gen-catalog` producers no longer exist. The generator-only scripts below (`render_sketch`,
> `render_catalog`, and the `map.json`-consuming `validate_play` / `measure` / `viz_paths` / `viz_void`)
> are kept as reference but have no live input. The **corpus** scripts still work on real maps.

Deps: `numpy`, `scikit-image`, `scipy` (the playability/heatmap maths use `skimage.draw`,
`skimage.morphology.skeletonize`, `scipy.ndimage`). PNG encoding is hand-rolled (no Pillow/matplotlib).

## Producing the inputs (RoundTrip tool, no DB)

```
dotnet run --project tools/PgmStudio.RoundTrip -- --skeleton-study  <regionDir> <map.xml> skel.json   # corpus terrain + build cells
```

## Scripts

| script | input | what it does |
|---|---|---|
| `corpus_analyze.py`  | `skel.json map.xml [ceil]` | per-cell void-clearance / geodesic / pinch analysis on a **real corpus map** (terrain from skel.json, regions from map.xml) |
| `corpus_validate.py` | `skel.json map.xml` | protection-aware playability check on a **real corpus map** |
| `render_sketch.py`   | `sketch.json`  | *(generator, dormant)* draws raw sketch polygons + mirror |
| `render_catalog.py`  | `catalog.json` | *(generator, dormant)* draws the style catalogue |
| `validate_play.py`   | `map.json`     | *(generator, dormant)* BFS captor spawn → enemy wool around spawn protection; PLAYABLE/UNPLAYABLE |
| `measure.py`         | `map.json`     | *(generator, dormant)* geodesics, void clearance, hub fork, spur/lane lengths |
| `viz_paths.py`       | `map.json`     | *(generator, dormant)* measured player-flow paths on terrain |
| `viz_void.py`        | `map.json [ceil]` | *(generator, dormant)* void-clearance heatmap + pinch strips + paths |

## Typical loop (corpus map)

```
dotnet run --project tools/PgmStudio.RoundTrip -- --skeleton-study <regionDir> <map.xml> /tmp/skel.json
python3 scripts/generator/corpus_analyze.py  /tmp/skel.json /path/to/map.xml 16   # clearance + pinches
python3 scripts/generator/corpus_validate.py /tmp/skel.json /path/to/map.xml      # confirm PLAYABLE
```

The colour ceiling arg on `corpus_analyze` maps that clearance (blocks) to the top (teal) colour, so
maps can be compared on one scale (e.g. `16`).
