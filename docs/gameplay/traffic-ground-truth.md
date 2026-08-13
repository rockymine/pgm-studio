# Traffic ground truth — formats & derivation (self-contained)

The complete contract for player-traffic ground truth. **Input: one zip of raw log files per
map.** Everything else — the traffic graph, the emergent footprint, flow priors — derives inside
this repo; no external analysis project is consulted. Validated end-to-end on ingwaz
(`tools/traffic/`): the logs-only pipeline reproduces the reference graph's islands 6/6 and its
void cells at recall 1.0.

## 1. Input contract: the per-map log zip

`{slug}.zip` containing `{slug}/*.parquet`, one file per recorded match (filename carries the
match start time; the exact pattern is irrelevant — every parquet in the zip is one match on
that map). Raw match data stays private with the author; the zips he shares are the pipeline's
only input.

## 2. Raw log format (pgmlogger parquet)

One row per event. Columns:

| column | type | meaning |
|---|---|---|
| `timestamp` | int32 | **seconds** (epoch). Sample cadence of position rows = the log interval (2 s) |
| `event_type` | uint8 | see event codes below |
| `player_id` | int32 | anonymous player id, **stable across matches** |
| `x`,`y`,`z` | int32 | block position (null on match markers) |
| `held_item` | int32 | held item id (unused here) |
| `inventory_count` | int32 | count of the held item (unused here) |
| `wool_id` | uint8 | wool identifier on wool events; null otherwise |

Event codes (inferred from data and validated on ingwaz — no plugin source needed):

| code | event | notes |
|---|---|---|
| 0 | match/logging start | one per file; null coords |
| 1 | match end | one per file; null coords |
| 2 | player spawn | at the spawn platform (constant y per spawn) |
| 3 | kill | killer's position |
| 4 | death | victim's position; **y < 0 ⇒ died falling in the void** |
| 5 | position sample | every `log_interval` seconds; **y < 0 ⇒ sampled mid-fall** |
| 6 | wool touch/pickup | `wool_id` set; at/near the wool room |
| 7 | wool capture | `wool_id` set; at the capture point (beside the owning team's spawn) |

## 3. Output format: `{slug}.traffic_graph.json`

```jsonc
{
  "map_slug": "ingwaz",
  "grid_size": 3,            // cell edge in blocks
  "log_interval": 2,         // seconds between position samples
  "match_count": 105,        // parquet files aggregated
  "position_count": 19030,   // standing position samples retained (y >= 2)
  "player_count": 510,       // distinct player_id
  "total_playtime_min": 1060.0,
  "nodes": [{
    "node_id": 0,            // dense index; edges refer to it
    "cx": -60, "cz": 54,     // cell anchor: floor(x/grid)*grid, floor(z/grid)*grid
    "coords": [-58.5, 55.5], // cell centre (cx + grid/2, cz + grid/2)
    "occupation": 11,        // standing samples (event 5, y >= 2) in the cell
    "island_id": 2,          // terrain island label; null = no land (void / build region)
    "poi_type": null,        // "spawn" | "wool" | null
    "poi_color": null,       // team/wool colour string on POI nodes
    "team": null,            // owning team id on POI nodes
    "fixed": false           // true on POI nodes (kept for renderer compatibility)
  }],
  "edges": [{
    "src": 0, "dst": 1,      // node_id, directed
    "transitions": 11        // consecutive-sample moves src -> dst
  }]
}
```

Only nodes with any traffic exist; `island_id` partitions the land nodes into terrain islands.
The ingwaz file in `tools/traffic/` is the reference instance (produced by the original
pipeline); a regenerated file may differ by a few `occupation` counts from filtering details —
`island_id`/POI/edge semantics are the load-bearing parts.

## 4. Logs-only derivation (no map knowledge)

Validated against the ingwaz reference (see `tools/traffic/README.md` for the numbers):

1. **Cells + occupancy + edges** — bucket standing position samples (event 5, y ≥ 2) on the
   grid; edges from consecutive samples of the same player life crossing cells.
2. **POIs** — spawns: event-2 clusters (one per team; block-exact). Wool rooms: event-6
   clusters per `wool_id`. Capture points: event-7 clusters (sit beside the owning spawn —
   which also yields team attribution: a player's team = the spawn cluster their lives start
   at).
3. **Symmetry centre** — midpoint of the spawn clusters (the symmetry type itself is testable
   by comparing the occupancy field under rot_180 / mirror candidates).
4. **Void / build regions** — the **fall-share** signal: per cell,
   `fall / (fall + stand)` where `fall` counts sub-zero-y rows (mid-fall positions **and**
   deaths; deaths alone are too sparse — R 0.43) and cells are pooled with their symmetry
   image. Share ≥ 0.08 ⇒ recall 1.0 (precision 0.39); ≥ 0.12 ⇒ P 0.52 / R 0.86. All residual
   error is **rim aliasing** (a grid cell straddling an island edge collects its lip's falls);
   the known fix is classifying falls at block resolution before aggregating.
5. **Islands** — connected components (4-neighbour) of traffic cells minus void cells: 6/6 on
   ingwaz.

## 5. Uses (and the boundary)

- **Recovered footprints** — land + emergent void zones as CT test articles (validated pairs
  like `tools/traffic/ingwaz.*`).
- **Flow priors** — per-map scalars scoring composer candidates: occupancy split over the
  mid/transition/team distance thirds, approach usage shares, void-vs-land occupancy, the
  kill/death frontline band.

Only log zips, graph JSONs, and derived priors enter this repo — no player identities beyond
the anonymous ids already in the logs, no per-match analytics, no match-analysis features.
Tracked as `G33`.
