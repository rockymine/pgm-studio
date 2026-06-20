# Generator analysis & visualization scripts

Local, dependency-light Python for inspecting the lane-sketch **generator** (`PgmStudio.Pgm.Sketch`)
without a database. They consume the JSON the RoundTrip tool emits and render PNGs / print metrics.
Not part of the build or the test suite — diagnostic harness, like `scripts/*_corpus.py`.

Deps: `numpy`, `scikit-image`, `scipy` (the playability/heatmap maths use `skimage.draw`,
`skimage.morphology.skeletonize`, `scipy.ndimage`). PNG encoding is hand-rolled (no Pillow/matplotlib).

## Producing the inputs (RoundTrip tool, no DB)

```
dotnet run --project tools/PgmStudio.RoundTrip -- --gen-sketch      Organic <seed> sketch.json   # raw SketchLayout polygons
dotnet run --project tools/PgmStudio.RoundTrip -- --gen-map-preview Organic <seed> map.json      # islands + intent (spawns/protection, wools/rooms, bridges)
dotnet run --project tools/PgmStudio.RoundTrip -- --gen-catalog     catalog.json                 # every hub style + lane behaviour
dotnet run --project tools/PgmStudio.RoundTrip -- --skeleton-study  <regionDir> <map.xml> skel.json   # corpus terrain + build cells
```

## Scripts

| script | input | what it does |
|---|---|---|
| `render_sketch.py`   | `sketch.json`  | draws the raw **sketch polygons** (per-team + their mirror), vertices marked — the base model before rasterization |
| `render_catalog.py`  | `catalog.json` | draws the **style catalogue**: hub styles (top row) + lane behaviours (bottom row) |
| `validate_play.py`   | `map.json`     | rasterizes the map and BFSes each captor's spawn → enemy wool **around the enemy spawn protection**; renders terrain + bridges + protection + wool rooms; prints PLAYABLE/UNPLAYABLE |
| `measure.py`         | `map.json`     | geodesic spawn↔spawn / spawn→wool, void clearance at objectives, hub fork, spur/lane lengths |
| `viz_paths.py`       | `map.json`     | the measured player-flow **paths** drawn on plain terrain (symmetric, mirrored) |
| `viz_void.py`        | `map.json [ceil]` | **void-clearance heatmap** (red thin → teal deep) + the thinnest-terrain pinch strips + paths |
| `corpus_analyze.py`  | `skel.json map.xml [ceil]` | the same clearance/geodesic/pinch analysis on a **real corpus map** (terrain from skel.json, regions from map.xml) |
| `corpus_validate.py` | `skel.json map.xml` | protection-aware playability check on a **real corpus map** |

## Typical loop

```
dotnet run --project tools/PgmStudio.RoundTrip -- --gen-map-preview Organic 13 /tmp/m.json
python3 scripts/generator/viz_void.py     /tmp/m.json /tmp/void.png 16    # eyeball clearance + pinches
python3 scripts/generator/validate_play.py /tmp/m.json /tmp/play.png      # confirm PLAYABLE
python3 scripts/generator/measure.py      /tmp/m.json                     # the numbers
```

The colour ceiling arg on `viz_void`/`corpus_analyze` maps that clearance (blocks) to the top (teal)
colour, so corpus and generated maps can be compared on one scale (e.g. `16`).
