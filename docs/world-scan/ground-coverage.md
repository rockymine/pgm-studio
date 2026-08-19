# Ground coverage — where a map is lived on, and where it is dead

The distance terms say how far apart the objectives stand; nothing said whether the ground between and
beside them is part of the match. Boards have shipped whole regions no player will ever enter — scenery
plateaus off every route, plain middles with no reason to cross them — and every one passed the distance
reads. This measure is the answer: a classification of every ground cell by whether the match reaches it.

## The model

Players move between the map's **waypoints** — spawns, wools, destroyables, cores — over the navigable ground
(the same navigable set the traversability gate reads, one derivation). A journey is taken between **every
pair** of waypoints, because defenders travel to defend, attackers rotate goal to goal, and mid fights happen
between spawns; and every one of them aims at a place the map actually has, which is why no journey ever
wanders off across ground nothing stands on.

A journey claims a **corridor**, not a line. Every cell on a walk no more than `CorridorAllowance` (10
blocks) longer than the shortest belongs to it — an allowance, not a fraction of the distance. Two things
follow. A two-hundred-block detour is excluded by construction rather than by a ratio that happens to be
tight enough, and so is a there-and-back down a spur, since visiting a cell and returning is charged twice
the way out. And where a shape offers two ways round a hole the corridor carries **both**, which one
fattened shortest path cannot: a single geodesic has to commit to one side, and the other then reads unused
however many players walk it — while going round a hole is the most valuable thing a shape does.

Ten blocks is **calibrated against the one board known to carry dead ground**: run 4's `wheal-hazel`, whose
eighty-block neutral bar crosses a twenty-block build zone. The author's own reading of it, and this measure's:

| piece | the author | the measure |
|---|---|---|
| `works-lo-w` | dead | 100% |
| `west-spur` | dead | 100% |
| `works-yard` | about half | 50% |
| `moor` | about half | 50% |
| `bar` | about two thirds | 62% |
| `works-lo-n` (4 cells) | dead | 50% |

and its rebuild `wheal-hazel-v2`, which the author says should read as essentially nothing, comes out at
**0%**. Note that this tolerance is not the width of the lane a player spreads across while walking — that is
about twice as wide — but how far out of their way they will go for nothing, which is the smaller quantity
and the one a coverage read needs.

Each waypoint also claims its own `PoiRadius` (10) ring, the same ten blocks the goal standoff keeps props
out of. Ground any corridor or ring covers is **reached**: the match uses it.

**How much it is used is kept as well as whether.** `Traffic` counts, per cell, how many of the journeys cover
it. A cell one journey clips and a cell every journey runs down are both reached, and on a played map the
busiest cell sees twenty to a hundred and sixty times what the quietest does — so membership is the coarse
half of the answer. The count is the half that says which way round a hole is preferred and which is the one
players decline.

What remains is one of two things. Ground within `PropRadius` (4) of a placed tree, boulder or building is
**decorated** — scenery a player at least looks at, which is a legitimate finish for a destroy board's
edges. Everything else is **dead**: no route through it, no objective near it, nothing on it. Dead ground is
clustered into 4-connected **patches**, each reported with its area, centroid and the walk from its nearest
cell to the nearest reached ground — the numbers that say whether a patch wants a point of interest, a prop,
or deleting. Patches under `PatchFloor` (25) cells are counted but not named.

This is a **measurement, not a rule**: nothing refuses or scores on it yet. The dead *share* over the ground
total is the number that says whether a board is too big for what it plays — the judgement the goal-ratio
band cannot make, because it only measures along one line.

## Reading it

| Surface | Answers |
|---|---|
| `GET /api/map/{slug}/coverage` | the classes as digit rows over the grid (legend included), the shares, and the named dead patches; `?format=png` the same grid as a picture |
| `tools/mapgen` (every build) | one summary line — reached / decorated / dead shares — plus the five largest dead patches with coordinates |
| `tools/mapgen --stages` | `stages/coverage.png` — corridors and rings green, decorated fringe yellow, dead ground red, routes and waypoints marked |

The classes come off `Analysis/Playability/GroundCoverage.Read`, the corridors off `Geom.Cells.Corridor` over
`Traversability.Ground`'s navigable set, and the picture off the measure's own grid through
`Export/CoverageRender` — one derivation for the numbers, the JSON and the image. The endpoint carries the
traffic grid beside the classes, one base-36 digit per cell, with `journeys` and `busiest` so a caller can
scale it without walking the grid.

## Limits

The corridor is a geometry, not a behaviour. Calibrated against 12.9 M recorded position samples over six
hand-traced maps, a walk of this kind reaches a rank correlation of 0.44–0.64 against where players actually
stood — most of what it misses is not the route but the fact that half of a long match happens on structure
the players built above the map (`docs/gameplay/match-flow.md` §6.12), and that a rotational board sends each
team down its own flank so that mirror-image ground carries ten times the traffic of its twin (§6.5). Neither
is derivable from a plan. The measure is therefore good for finding ground **nothing** goes to and poor at
ranking two lanes that both carry traffic. Iron cubes are not yet waypoints (they live in the intent's structures, not
the map document the measure reads). The navigable set is the whole-map one — walkable terrain plus the
**granted** build zone, so a cross-map route bridges through the build zones exactly as the traversability
gate's does. A grant is a rule permitting building, or the complement of a void denial (`B247`): ground no
rule mentions is not a crossing — but the gate's per-team half (an `enter` rule barring one team somewhere) is
not applied here: a corridor may cross ground one team cannot enter, which slightly overstates reached
ground around oversized protection regions. What acts on the dead share — a complaint band, a score term —
is deliberately undecided until the measure has been read against enough boards.
