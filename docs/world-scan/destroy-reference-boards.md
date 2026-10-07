# Destroy boards the author points to, read from their worlds

The author names seven destroy boards in the corpora as the ones worth reading for a board's shape and its
terrain (`docs/gameplay/approaches.md`, *Destroy boards worth reading*). Their `map.xml` says where the spawns
and goals stand and nothing about the ground those stand on, so this document reads the worlds. Every board
here was imported into a studio of its own with `POST /map/import-folder`, drawn from its region files with
`PgmStudio.RoundTrip --heightmap`, and measured column by column with `pgm-studio-mapgen/tools/anvil.py`; the
spawn and goal positions are the centres of their regions as `MapParser` reads them. The same measure was taken
over the 134 boards with a destroyable or a core under `pgm-studio-mapgen/maps/`, so the two can be set side by
side.

## What each world is

**No two of the seven share a shape.** Each is described from its own height map, with the spawn of its first
team and its measured extent.

| Board | Extent (blocks) | Spawn (x, z) | What the world is |
|---|---|---|---|
| *Atromix* | 177 × 89 | (66, 574) | a side island for each team (x 26 to 89 for the first) with the spawn at its outer corner and the back monument on it, a 15-block island off its inner edge holding the front monument, and a middle island between the teams. Ground rises 54 blocks within 20 of the spawn |
| *The Fenland* | 119 × 100 | (46, 0) | one box: a raised rim the players walk round a flooded basin of small islands, the spawn on the rim at the back middle and the monument 25 blocks to its side |
| *Annealing III* | 180 × 152 | (56, −47) | two jagged landmasses mirrored across a gap, lobed and stacked along their outer edges, a small island in the gap; one landmass carries everything a team owns |
| *Monument Valley* | 123 × 163 | (4, −44) | one box cut by a cross-shaped lake; the spawns raised at y51 in opposite corners, each monument on a raised peninsula into the lake, diagonally in front |
| *Alpine Mining II* | 164 × 484 | (−41, 128) | a long lane of rolling ground, contoured everywhere; a river winds down each half from the high end into a lake across the middle, and the houses stand in the terrain, never on a pad |
| *Kuusepuu* | 196 × 76 | (−90, 12) | two lobed islands mirrored across a gap of small islands; the spawn at the outer edge of its island among buildings |
| *Sunrise over Paradise* | 201 × 201 | (0, −80) | rings of land in water, the spawns at opposite ends of the inner ring and the monument islands outside it, to the side, reached by boat |

## The spawn's seat

**On the seven the spawn has land behind it and ground beside it; on the agent boards it has neither.** Each
measure is taken around the spawn's region centre, over the ground column under it — the topmost block that is
not wood, leaf, plant or water.

| Measure | How it is taken | The seven, median (range) | Agent boards, median |
|---|---|---|---|
| land behind | blocks of the spawn's own island behind it, along the line to the enemy spawn | 20 (7–91) | 10 |
| to the void | the nearest column with no block at all | 6.5 (5–20) | 9 |
| rise beside | the highest ground within 20 blocks, above the spawn's own | 8 (0–54) | 0 |
| canopy beside | share of the ground within 20 blocks under a tree | 0.17 (0.01–0.59) | 0 |
| made beside | share of the ground within 20 blocks whose top block is built | 0.18 (0.08–0.64) | 0.32 |

Of the 268 agent spawns, 143 have nothing higher than themselves within 20 blocks, 141 no tree within 20,
and 78 neither: the spawn is a built pad at the board's back edge with ten blocks of it behind and flat ground in
front.
*Alpine Mining II* has no rise beside its spawn either — it is seated in the open at the top of the lane, with
ninety blocks of land behind it and trees round it.

## The board around it

**The seven are rougher, wetter and greener than the agent boards, and less of their bounding box is land.**
Steepness is the share of ground columns stepping two blocks or more to a neighbour, and a cliff five or more;
relief is the spread between the 10th and 90th percentile of ground height.

| Measure | The seven, median (range) | Agent boards, median |
|---|---|---|
| land as a share of the bounding box | 0.56 (0.40–0.79) | 0.69 |
| relief, blocks | 17 (3–33) | 12 |
| steep | 0.35 (0.12–0.36) | 0.16 |
| cliff | 0.10 (0.02–0.27) | 0.07 |
| water | 0.19 (0–0.82) | 0 |
| canopy | 0.18 (0.01–0.75) | 0.07 |
| built | 0.14 (0.07–0.43) | 0.17 |

93 of the 134 agent boards carry no water at all, and their median board is twice as smooth as the median of the
seven. *Sunrise over Paradise* is the flat exception and the wettest: 3 blocks of relief and 82% water.

## Where the goals stand

**The goal stands in front of the spawn and to one side.** Measured straight from the spawn's centre against
the line to the enemy spawn, the angles on the seven run from 14° (*Kuusepuu*) to 110° (*Atromix*'s back
monument), and over the 303 corpus destroy maps the median is 42°, against 28° on the agent boards
(`docs/gameplay/approaches.md`).

## Limits

**A world-scan read of an imported map is refused.** Every read under *The reads* in `read-backs.md` builds its
world from a stored sketch layout and answers an imported map `404` `SK6`, though its region files are on disk;
the CLI reads the folder instead, and the slope, incline, reach and eye reads have no CLI form (`WS83`).

**The measures are taken from a spawn region's centre**, so a spawn region spread over a room reads its middle,
and two spawn regions for one team read the first. *The Nile*, the eighth board the author names, is in neither
corpus.
