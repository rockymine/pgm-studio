# Ground coverage — where a map is lived on, and where it is dead

The distance terms say how far apart the objectives stand; nothing said whether the ground between and
beside them is part of the match. Boards have shipped whole regions no player will ever enter — scenery
plateaus off every route, plain middles with no reason to cross them — and every one passed the distance
reads. This measure is the answer: a classification of every ground cell by whether the match reaches it.

## The model

Players move between the map's **waypoints** — spawns, wools, destroyables, cores — along shortest walks
over the navigable ground (the same navigable set the traversability gate reads, one derivation). A walk is
taken between **every pair** of waypoints, because defenders travel to defend, attackers rotate goal to
goal, and mid fights happen between spawns. Each walk is widened by `GroundCoverage.CorridorMargin` (6) the
way a path's band claims more than its centerline, and each waypoint claims its own `PoiRadius` (10) ring —
the same ten blocks the goal standoff keeps props out of. That union is **reached** ground: the match uses
it.

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

The classes come off `Analysis/Playability/GroundCoverage.Read`, the corridors off `Geom.Cells.ShortestPath`
over `Traversability.Ground`'s navigable set, and the picture off the measure's own grid through
`Export/CoverageRender` — one derivation for the numbers, the JSON and the image.

## Limits

Routes are shortest walks; players also wander, so the corridor margin is doing the work of everything a
shortest path underestimates. Iron cubes are not yet waypoints (they live in the intent's structures, not
the map document the measure reads). The navigable set is the whole-map one — walkable terrain plus
buildable and bridgeable cells, so a cross-map route bridges through the build zones exactly as the
traversability gate's does — but the gate's per-team half (an `enter` rule barring one team somewhere) is
not applied here: a corridor may cross ground one team cannot enter, which slightly overstates reached
ground around oversized protection regions. What acts on the dead share — a complaint band, a score term —
is deliberately undecided until the measure has been read against enough boards.
