# Terrain steepness — what the corpus's ground charges

Ground that is walkable everywhere is a field, not a map. The reason to shape elevation at all is that some ground
should be harder to reach: a bank a team cannot climb from the water, a plateau with two ways up instead of five,
a fall that can be taken but not undone. Steepness is the control, and it works because the game's movement has
three thresholds rather than one. A step of at most one block is a jump and costs nothing, a step of two is a
placed block — slow, and the player stands still in the open to place it — and a step of three or more is building
in earnest, which is not done in a fight. `Walk.ScrambleStep` is the number two, and every read that classes a step
reads it, so a step is the same kind of step wherever it is counted.

| Step | Costs | What it is on a map |
|---|---|---|
| 0–1 | a jump, and nothing else | open ground |
| 2 | a placed block | a lip that shapes a route without closing it |
| 3+ | building in earnest | a face; not crossed in a fight |

None of the three is a fault, and a good map is a mix of all three. This is why a read of terrain reports what a
surface costs at each tier instead of scoring it against the flattest one: the number that matters is not whether
anything is unreachable but what the terrain charges. A barrier is usually a detour rather than a wall, because
there is normally a way round its end, so how far round is the measurement. A drop is not a barrier either, since a
player walks off a ledge and takes the fall: a face that stops a crossing one way lets it through the other, and
only a measurement taken in both directions shows the difference between a wall and a gradient of pressure. The
reads that take these measurements are `slopes` and `walk` in `read-backs.md`.

## What the corpus measures

The community corpus's destroy-the-monument maps were read twice: once over the **built surface**, the top of
everything a player stands on with walls and roofs included, and once over **natural ground**, stepping past a
building's own courses to the terrain underneath. The second is the one a terrain rule can be calibrated against.
A building is recognised by material and not by height, from a set that deliberately excludes stone, cobble,
gravel, sand and clay so an outcrop is not read as architecture.

| | min | p25 | median | p75 | max |
|---|---|---|---|---|---|
| plan width | 36 | 102 | **148** | 196 | 395 |
| plan depth | 41 | 118 | **164** | 230 | 513 |
| height range | 0 | 32 | **48** | 76 | 133 |
| body — 95% of columns | 0 | 11 | **19** | 30 | 88 |
| walk (0–1) | 25% | 62.6% | **72.6%** | 85.2% | 100% |
| scramble (2) | 0% | 1.5% | **5.5%** | 10.2% | 24.4% |
| barrier (3+) | 0% | 8.8% | **18.3%** | 25.9% | 73.9% |
| largest place | 1.9% | 21.2% | **29.4%** | 44.2% | 100% |
| places of 1% or more | 1 | 4 | **8** | 13 | 52 |
| cliffs | 0 | 2 | **8** | 13 | 60 |
| under liquid | 0% | 0% | **1.4%** | 7.1% | 75.3% |

*105 maps, natural ground.*

Filtering the architecture out makes the terrain read steeper, not gentler: walk falls from 77.3% to 72.6% and
barrier rises from 16.3% to 18.3%. A building's flat roof and level floor were smoothing the reading, and stripping
them exposes the ground the building was placed to stand on. The corpus's steepness is terrain and not walls, and
the tier shares were never the distorted numbers.

The number that was distorted is the cliff count. One mountain-mining map reads 36 cliffs off the built surface and
13 off natural ground: two thirds of its apparent cliffs were the walls of its own buildings. A cliff is qualified
by width and drop (`rules.md` EL6), and a rampart passes both tests, so any rule written against the built surface
counts architecture.

The corpus is steeper than smooth interpolation between pleasant heights. Two independent hand-designed boards of
gentle rolling terrain measured a 13- to 14-block body, 96.1% walk, under 3% barrier and one place holding 98%
of the ground: flatter and more open than the corpus at every percentile. Scaling every height's departure from the
base until the body reached 27 blocks brought the same board to 8 cliffs and three places with the largest at
45.6%, and turned its river from something crossable at two fords into a gorge with none on foot and two with a
placed block. A valley drawn as spurs between side valleys, a river stepping down into a lake and a benched flank
measured 83.9% walk over a 41-block body with water at 11 distinct levels, the first board to sit inside the
corpus range rather than at its edge.
