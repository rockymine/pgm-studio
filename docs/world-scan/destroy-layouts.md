# Destroy layouts that are not a lane, read from their worlds

The author points to seven corpus destroy boards as layouts that are not a lane (`docs/gameplay/approaches.md`,
*Destroy layouts that are not a lane*), and this document reads their worlds. A board is never named in these
documents, so each is called by its layout. Every world was imported into a studio of its own with
`POST /map/import-folder`, drawn from its region files with `PgmStudio.RoundTrip` — `--heightmap`, `--topdown
--material` with the map's spawns and goals over it, and a `--section` through each spawn toward the enemy —
and measured column by column with `pgm-studio-mapgen/tools/anvil.py`. The spawn and goal positions are the
centres of their regions as `MapParser` reads them. The same measures were taken over the 134 boards with a
destroyable or a core under `pgm-studio-mapgen/maps/`, so the two sets can be set side by side.

## What each layout is, and where its spawn sits

**The spawn is seated in something on every one of the seven.** A position is given against the layout rather
than as a coordinate, since the layout is the thing a reader can find again; the goal is measured straight
from the spawn, ahead along the line to the enemy spawn and to the side of it.

| Layout | Extent | Where the spawn sits | Its goals |
|---|---|---|---|
| side islands and a middle | 177 × 89 | two round platforms a team, bridged onto the outer corners of a side island of thin floating ground; trees close round them and the island rises 54 blocks within 20 | the front monument on a 15-block island off the inner edge, 41 ahead and 37 aside; the back one on the side island, 14 behind and 37 aside |
| a rim round a basin | 119 × 100 | a house on the raised rim at the back middle, a little above the flooded basin, 10 blocks of rim behind it | 25 aside on the rim, level with the spawn |
| a tour widened into land | 180 × 152 | a building on a raised mound with a tree on it, at the outer edge of its landmass, 35 blocks of land behind; stacks of coloured stone stand along the lobes | two cores, 43° and 54° off the line |
| a box cut by water, walled by mountains | 123 × 163 | a roofed pavilion on top of a stone massif about 40 blocks thick, in a corner, a cliff falling from it to the lake | on a raised peninsula into the lake, 18 ahead and 57 aside |
| a long lane of rolling ground | 164 × 484 | in the open at the high end of the lane, 90 blocks of land behind, trees round it and mine headframes standing in the ground beyond | the river monument 30 ahead and 21 aside; the hill monument level with the spawn and 65 aside |
| islands in a box | 196 × 76 | a timber tower on a block of stone at the outer edge of its island, among rock outcrops and trees | 45 ahead and 11 aside — the nearest to straight ahead of the seven |
| rings in water | 201 × 201 | a small island of its own with a stone building, off the end of the inner ring across water | two, on islands outside the ring, 29 ahead and 45 aside each |

## The author's account, matched to the worlds

**Everything the author said of these layouts is in their worlds.** Each row is a claim from
`docs/gameplay/approaches.md` and where it stands.

| The author's claim | What the world shows |
|---|---|
| the spawn sits in the land, not on a box behind it | six of the seven spawns stand on raised ground, on rock or among trees; the seventh, on the rings, is an island of its own by design |
| it may stand some way into the land and still be at the back | 7 to 90 blocks of the spawn's own land lie behind it, 20 the median |
| mountains or stacks of rock beside it | the box walled by mountains seats it on the massif; the tour stands stone stacks along every lobe; the islands in a box ring it with outcrops |
| an iron mine near it | the long lane's mountain is veined with iron ore throughout (43,363 blocks), and its iron blocks are cut into two mirrored mined chambers of 650 blocks each at y13–27, 57 to 91 blocks behind each spawn under rising ground, with smaller deposits under the spawn and beside the river monument |
| the objective in front of the spawn and to one side | every goal of the seven stands aside of the line, 14° to 110°; the corpus median is 42° |
| a front monument and a back one | the side islands set one 41 blocks ahead and one 14 behind, both 37 aside |
| reached from the heights and from below | the box walled by mountains puts its goal on a raised peninsula below the mountain ridge and above a lake the river runs into |
| ground nobody walks, there to be looked at | the long lane is 164 blocks wide and carries 33 blocks of relief in rolling contours across all of it; how much of it a route crosses is not measured here |

## The spawn's seat, measured

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

Of the 268 agent spawns, 143 have nothing higher than themselves within 20 blocks, 141 no tree within 20, and
78 neither: the spawn is a built pad at the board's back edge with ten blocks of it behind and flat ground in
front. The long lane has no rise beside its spawn either, and is the one case of the seven: it is seated in the
open at the high end, with ninety blocks of land behind it and trees round it.

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

93 of the 134 agent boards carry no water at all, and their median board is half as steep as the median of the
seven. The rings in water are the flat exception and the wettest: 3 blocks of relief and 82% water.

## Where the goals stand

**The goal stands in front of the spawn and to one side.** Measured straight from the spawn's centre against
the line to the enemy spawn, the 999 goals on 303 corpus destroy maps sit a median 42° off that line (p25 18°,
p75 76°), a quarter within 20° of it; the 374 on the agent boards sit a median 28° off, and 38% within 20°.

## Limits

**A world-scan read of an imported map is refused.** Every read under *The reads* in `read-backs.md` builds its
world from a stored sketch layout and answers an imported map `404` `SK6`, though its region files are on disk;
the CLI reads the folder instead, and the slope, incline, reach and eye reads have no CLI form (`WS83`).

**`--underground` does not separate a mined chamber from ore-veined stone.** On the long lane it shades the whole
mountain as enclosed, so the mine was found by its iron blocks (id 42) instead, clustered and then cut with a
section.

**The measures are taken from a spawn region's centre**, so a spawn region spread over a room reads its middle,
and two spawn regions for one team read the first. The desert square is in neither public corpus, and only the
author's account of it stands.
