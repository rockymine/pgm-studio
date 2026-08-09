# Match flow — how a CTW map is played, and how a plan is read for it

The generator settles geometry. A match is what that geometry produces once two teams of up to thirty
players are standing on it for forty minutes, and almost none of it is visible in a rectangle partition.
This document holds both halves. The first is the **flow reading**: the three fidelities a route has, what
is measured on each, and what the generator's own vocabulary does to movement. The second is the **played
account**: how a match actually unfolds on a CTW map, in order, from the opening rush to the second wool.

The played account is authored knowledge from matches, not a derivation. It is the thing the derivations
are checked against — where a measure and the account disagree, the account is the evidence and the measure
is the suspect. Above both sits recorded traffic, which §6 reports: where the logs and the account disagree,
the logs decide.

Three sets of numbers appear, and they come from different places. Board figures are measured over composer
output at version `marker-id-1`: a census of **5 200 boards** (13 player counts from 8 to 32, 400 seeds each,
2 teams, `rot_180`, cell 5) carrying **9 154 wools**, and a routed subset of **560 boards** where the full
cell-level analysis ran. Corpus figures come from the recorded-match database: every processed,
spatially-classified match on a two-team two-wool map running past ten minutes — **333 matches on 94 maps**,
**615 team-frames** with a first capture, drawn from 12.9 M position samples. Structure figures — walls,
staircases, the sky network — come from a smaller set still, the fourteen longest matches sampled at two
seconds, and are marked as such wherever they are quoted.

---

## 1. Three fidelities

A route is one thing read at three scales, and each scale answers a question the others cannot. None of the
three is the real one.

**Boxes** are the composer's typed groups — spawn, hub, wool, frontline, plus the mid band. At this
fidelity a route is four or five nodes and the question is what kind of route it is. Every generated board
answers the same way: `band → frontline → hub → wool`. The box graph is a star with the hub as its centre,
so it cannot show an alternative and cannot see inside a box. A ring hub and a solid hub are the same node.

**Pieces** are the parts inside those boxes, and they carry the composer's own names: `frontline-t1` is the
bar, `frontline-t2` and `frontline-t3` are its legs, `wool-b-t1` through `wool-b-t4` are the approach runs,
`wool-b-room` is the room. At this fidelity the alternatives become countable and nameable — over this leg
or that one, in by this door or the other — and a route reads as a sequence a person can say aloud:
`band → front-t2 → front-t1 → hub-t1 → b-t2 → b-t1 → b-t4 → b-room`. This is the fidelity at which routing
options exist as objects.

**Cells** are the proxy grid, five blocks to a cell. Here each alternative becomes a corridor with a width
and a length, and the width is the thing that decides how many players can be in it at once. The cell route
carries a second meaning beyond the walk, established in §5: it is the footprint the late-game sky network
is confined to.

---

## 2. What a route is measured by

**The corridor.** Drawing one path through a space ten cells wide is a lie about where traffic goes. The
corridor is every cell a player could stand on while walking a route no more than 30% longer than the
shortest, brightest along the quickest line. It is what the figures render as a ribbon.

**The choke.** The minimum vertex cut between two ends — the cheapest set of cells that, if held, separates
them. Its position says where a clash happens; its **size is the funnel capacity**, which is the more
important reading and the one a bare "chokepoint" label loses. Ten blocks of frontage admits a different
number of players than twenty, and the count does not scale with the team size.

**Routing options.** Distinct piece-level routes, enumerated rather than scored. A longer way round is a
route, not a failed route: an attacker taking the far leg of a frontline is making a decision and the
defender has to cover it. Options are kept when the piece sequence genuinely differs, and each is reported
with its length.

**Ways round a void.** Whether a route crossing a shape must commit to one side of an enclosed void or may
pass either way. The test is topological: cut the region with a ray from the void out to its edge and check
whether the two ends stay connected; repeat with the ray on the other side; routes exist on both sides only
if both survive. Counting the connected components of the minimum cut is **not** this test and gives wrong
answers in both directions — an uncuttable door cell inside a single barrier splits it into two fragments
with no second route anywhere, and a genuine second way is missed whenever the cheapest cut lies elsewhere.

**Interference.** Two routes belonging to opposing sides, and how much ground they share. A single route
says nothing about tension; tension is two corridors laid over each other, so the measure is the fraction of
one team's lane that the other team's lane also covers. This is the quantity behind §4.9, and it is not the
same as either route's proximity to a box — two routes can collide for their whole length without either
one entering a third box at all.

---

## 3. The generator's vocabulary, in flow terms

### 3.1 One door, or two

Every box docks the hub through mouths, and the number of them caps everything downstream. Spawn boxes
have exactly one on every board measured. Wool boxes have exactly one when the approach family is **I, L or
donut** — 470 of 470 on boards where the attribution is unambiguous — and exactly **two** when it is
**U, H or clamp**. On the 170 boards carrying a rare family, the room stays reachable with either door
blocked, so the second entrance is real rather than a shape that resembles one.

The three two-door families are rare. Across 9 154 wools: I 71.07%, L 22.44%, donut 4.63%, **U 0.79%,
H 0.69%, clamp 0.38%**. Z and scythe never appear. The two doors are separated by exactly three cells on
every board that has them, which is a genuine second entrance for an attacker and a single place to stand
for a defender.

The donut approach encloses a void but still presents one door; giving it two is **G145**.

### 3.2 Voids, and what they relax

A void is what turns a corridor into a choice. Three of them occur, at three positions along the route, and
they multiply rather than compete.

At the **entrance**, the legs of a two-legged frontline and the mid band enclose a void between them, so the
band crossing forks before the attacker has touched the defender's land. This is the largest single source
of routing options in the corpus: **97%** of objectives behind a two-legged frontline have more than one
attack route, against 38% behind a plain bar.

In the **middle**, a hub body that encloses a void — ring, double-hole, P, G — offers two ways across when
its two doors straddle the hole. Ring hubs deliver this on 163 of 224 spawn-to-wool crossings and on 203 of
209 wool-to-wool rotations; double-hole and P deliver it on essentially all of theirs. Solid and branched
hubs never do. A large rectangular hub spreads players out without giving them a choice, and over a long
match that degenerates into push and pull along one shortest line.

At the **objective**, the bay of a U, H or clamp approach is the same mechanism a third time.

These do not merely add distance. They enforce rotation, and they do it for both sides: a defender coming
to stop a rush must also go around, which is why a void near the hub changes the defensive problem as much
as the offensive one.

### 3.3 Dock arrangement

Which face of the hub each box seats on is a board property in its own right, decided at seating, and it is
absent from the vocabulary. With the compass rotated so the frontline is *front*, two arrangements occur:

| arrangement | boards | spawn-distance imbalance |
|---|---|---|
| spawn *back*, wools *left* + *right* | 121 (27%) | 0.18 |
| spawn lateral, one wool on *back* | 332 (73%) | 0.40 |

The first is the arrangement built maps converge on. The second is lopsided, and it costs: the median
difference between the two spawn-to-wool distances, over their mean, roughly doubles. A wool docking to the
top and to one side is shorter to reach once a player is already in it, which makes losing control of the
sky above it lose the game outright, because the connection is small. Defence of such a board can be
simpler when a single attack route serves both wools, but is generally harder because the spawn is farther
from at least one objective than an even arrangement would put it.

Arrangement also decides whether the second-wool rotation runs past the spawn (§4.8).

---

## 4. The match, in order

### 4.1 Players are not one player

A route measure describes one traversal. A match has dozens of players per team moving along the same
routes repeatedly, dying, and returning. The behaviour that emerges is push and pull along the shortest
route rather than a single trip: attackers arriving from the mid toward a wool, defenders arriving from
their spawn or from their other wool, meeting wherever the geometry makes them meet. A large open hub
spreads that traffic out but does not change its axis.

Because players return after dying, **how long it takes a defender to get back to the choke matters as much
as the choke itself** — but distance alone does not decide whether the position is retaken, because the
attacking team is already standing on it. The measurable that respects this is relative, not absolute:
comparing the defender's distance from their spawn to the choke against the attacker's distance from the
band to the same choke, the median is 1.00 and the mean 1.75, and **on 44% of objectives the defender starts
farther from their own choke than the attacker does**.

### 4.2 The funnel

Where an approach has one door, that door is where attacking and defending players clash, and its width
decides how much can funnel through at once. Measured from the mid band to the wool room, the minimum cut
is **2 cells — 10 blocks — on 79%** of objectives, 4 cells on 18%, with almost nothing in between. That
frontage does not widen with the player count: a board built for thirty per team funnels through the same
ten blocks as one built for eight.

### 4.3 The wall, the pit, and the end of the ground game

Not every player goes up. Some stay at base, some keep rushing on the ground, and some dig in and build the
defensive wall in front of the wool room.

Wall building starts at one of two places, and both are emitted by the generator. Where the plan declares a
**wall interface**, a bedrock wall is stamped and players build on that line. Where it does not, the line
falls back to the face of the wool room itself, at the **redstone line** the generator emits at the room's
entrance. Building on the bedrock — or on the room face — is the correct choice because players cannot dig
through bedrock, so the wall cannot be undermined at its base.

That line also fixes where the ground is destroyed: players dig away the surface blocks **in front of** the
wall they build, leaving a pit down to the bare bedrock floor. The pre-placed wall stops ground rushers and
tunnellers, who must surface to cross it. Once the pit exists, tunnelling is denied altogether — a tunneller
becomes visible to the defenders at the moment the remaining terrain meets the excavated floor. Where no
wall is declared, the whole arrangement shifts inward toward the wool room, and the pit is dug there
instead.

The consequence is that the ground stops being viable as the match goes on, unless the defending team is
inattentive. Everything after this point happens above it.

### 4.4 The sky begins at the band

Late-game CTW is played on a second layer, at the map's maximum build height, and that layer is built
rather than authored. It starts where the frontlines meet the build band, because the band is the only
place the two sides can be joined. Early on the join is a flat bridge straight across the build band.

Height advantage matters, so teams do not stay flat: they staircase upward. Both teams' staircases climb
from opposite sides of the band until they meet, and the true push and pull begins there.

### 4.5 Height advantage and the push–pull

A team that wins the meeting point continues its staircase into a flat bridge heading for the enemy wool.
The bridge starts thin because someone has to lead it; players following behind widen it. Holding the
advantage means controlling the top of the opposing team's staircase, and the means are mundane and
effective: blocking the end of the stair with blocks, bow spam at anyone climbing, a water bucket poured so
it spreads down the stair and slows everyone on it.

A team that cannot break that control watches the sky advance toward its wool. The defence from below is
shooting upward and stacking up to reach the bridge. What usually turns it is attrition: the players out on
the far end of a bridge are few and exposed, and once they are killed the advantage flips and the other
team moves to destroy the sky. The players doing the destroying stand on the edge of the bridge and are
themselves easy targets, so control changes hands repeatedly.

### 4.6 The spawn staircase and the connected corridor

At some point in every match, a team builds a staircase directly from its spawn, usually with protective
measures — iron fences combined with wooden fences, which do not connect to each other, leaving gaps that
admit neither a placed block nor water. From there the team builds toward the middle, or toward wherever the
attacking team has already pushed its sky.

What forms is a single connected pathway from one team's **spawn box, over its hub box, over the mid band,
and into the other team's half**. That corridor is the late-game map. Travel along it takes real time, not
every player is in iron armour, and pushes are small and controlled, so the tug continues — but eventually
one side holds enough of it to branch off the sky path into a wool box.

### 4.7 Breaking a wool room

An attacking team over the wool works on two things at once: getting a player over the wall, and denying
the defenders their own movement — holding down the enemy spawn, digging away sections of the defenders'
bridge.

When someone finally wins that fight and gets over the wall, the defenders may still regain control and dig
away the approach sky between the hub box and the wool box, cutting the attacker off. But a player inside
the wool room is pressure by itself: the defence now needs eyes in more places at once. The attacker in the
room typically builds a stair for themselves to reach the top of the defenders' wall, or at least to shorten
the gap, because that reduces how far teammates must travel to escort the wool out.

The wool leaves in one of two ways. Either teammates reconnect the sky to the wall and the carrier walks
out, or the carrier drops from the wall and runs for cover and the mid — which is always connected by this
stage of the match, and which no one destroys, because destroying it is not practical.

### 4.8 The second wool

On a two-wool map one wool always falls first, and the defence then shifts to the other. Often the shift
happens much earlier, because defending a wool properly is expensive: digging the pit, building the wall,
and fending off the players who come to disrupt the work.

The captured room becomes a forward node for the attacking team — not a respawn point, but a place in their
network worth travelling to, because the wool room chests the generator emits hold better armour and
weapons. A player holding it builds a staircase of their own, or uses the already-connected sky, to harass
the defenders as they respawn. The room changes hands in the fullest sense: the logs put attacker presence
inside it at nearly nine times the defender's, and defenders who approach it lose the exchange outright
(§6.4).

What that pressure does **not** do is open a route. The **wool-to-wool rotation** is not taken: 5% of the
attacking lives that reach the captured room go on to reach the remaining one, and four fifths of the
players who reach the remaining wool came from their own spawn without going near the captured room at all.
The room-to-room line is the *defender's* lateral reinforcement lane, and the attackers stay off it. The
second wool is attacked the same way the first was.

### 4.9 Why the second wool is contested before it is reached

The tension in that phase is that two routes collide: the attacker approaching the remaining wool, and the
defender travelling from spawn to the same wool. Both are heading for one objective from different origins,
and the ground they cross is shared. The attacker's origin is their own spawn rather than the captured room
(§4.8), which changes where the collision starts but not that it happens.

That collision is universal on generated boards. Measuring the fraction of the defender's spawn-to-wool
corridor that the attacker's wool-to-wool corridor also covers, across 453 two-wool boards: the median is
**34%**, half or more on 27% of boards, and — the load-bearing figure — **no board has zero overlap**. There
is no generated layout on which the two routes miss each other.

An attacker therefore passes the defenders' reinforcement lane as a matter of course, unless the hub
encloses a void and the long way round is taken. That choice is worth its distance:

| attacker's route across a holed hub | share of the defender's lane it covers |
|---|---|
| near side (the short way) | 76% |
| far side (the long way) | 37% |

The far side measurably reduces the collision on **74%** of the boards that offer one, which is the clearest
statement of what a void in the hub actually buys: not a shortcut, and not merely rotation, but the option
to reach an objective without spending the whole approach inside the enemy's reinforcement lane. Solid hubs
have no such option. Dock arrangement (§3.3) still decides how close the rotation passes to the spawn point
itself, but interference is the more fundamental quantity and it never falls to nothing.

Recorded traffic puts the same quantity at a median **28.6%** of the defender's reinforcement lane covered
by attacker traffic, against the 34% predicted from geometry — close enough that the term is measuring the
right thing. The tail is where the two part company, and §6.6 takes it up.

The collision is also vertical, and asymmetric. An attacker deep in the defender's half travels on the sky
network at the build cap; a defender who has just died arrives at the spawn point on the ground, because
spawns sit on the terrain surface. On generated boards the surface is y = 9 and the cap is y = 20, so a
respawning defender has **eleven blocks of staircase to climb** before reaching the layer the attacker is
already standing on — and climbs it while that attacker holds height over them and while the attacker's own
teammates approach the same wool from the middle of the map. That is the shape of the pressure: not one
route crossing another on a flat plan, but one team above the other at the moment the other is weakest.

The asymmetry is real and the climb is a real tax. Over the defender's own back yard the attacking team is
at the ceiling 23–25% of the time against the defenders' 5.6–9.3%; and in the phase after a first capture,
60% of all lives reach the ceiling at some point, taking a median 22 seconds from spawning to get there
against a median life of 66 seconds.

---

## 5. What the build rules allow

`BuildGenerator` emits the declared build areas as `build-area`, wraps their complement as
`not-build-area`, and applies `block = no-void` to it. Outside the declared build area a player may
therefore edit **any column that is not void**, and inside it may bridge freely. On generated output the
only declared area is the mid band.

Three consequences follow, and they are what make the flow reading relevant to the late game rather than
only to the opening:

- Staircases on a team's own land, walls at the wool room, and pits are all legal, because those columns
  stand on terrain.
- The mid band is the one place a bridge may cross nothing, which is exactly why the sky network starts
  where the frontlines meet it.
- **A void anywhere else can never be bridged, at any height.** A ring hub's hole, the space between a
  frontline's legs, the bay of a U or H approach — those stay uncrossable when the match moves upward. The
  routing options those voids create are not opening-phase structure that the sky flattens; they persist to
  the end of the match.

The skybridge is therefore, quite literally, a projection of the shortest cell route, and the projection is
confined to land plus the band.

---

## 6. What the recorded matches show

The account above was written from play. The `pgmlogger` corpus makes it checkable: 333 long two-wool
matches on 94 maps, 12.9 M position samples, and — for anything needing what a player held in their hand —
the fourteen longest matches sampled at two seconds. Seven maps carry the structure findings. That is a
thin base for any single map's number and a sound one for the shape of the mode, which is how the figures
below should be read.

The frame costs nothing and works on any shape. Four points the database already knows — both spawns and
the two wool rooms an attacker must take — define an axis and a room-to-room segment, and every position
projects onto them. No polygon, no plan recovery, and 94 maps of arbitrary geometry become directly
comparable. Two traps are worth stating once: the terrain table covers land only, so any measure phrased as
*height above ground* silently drops play over the void, which on one map is a third of all ceiling
activity; and `map_wool_objectives` lists a single wool for both teams on a few maps, so capture events are
the source for who attacks what.

### 6.1 The ground stops being viable, and the ceiling is where the match goes

`max_build_height` is a hard ceiling and the play sits on it: the 95th percentile of player height lands
exactly on the declared cap, map after map. The share of samples up there climbs monotonically from **2.2%
in the first tenth of a match to 24.7% in the last**, and it scales with how long the match runs — a match
ending inside twenty minutes never passes 14%, one running past forty finishes near 32%. §4.3's claim that
the ground stops being viable is not a figure of speech.

Where the ceiling is used is as informative as how much. Along the spawn-to-spawn axis the at-cap share
peaks near **45% in the middle of the map** for both teams and falls away at both ends. Against distance to
the objective it falls off sharply: 33.6% at 50–80 blocks, 27.4% at 20–30, **6.6% inside ten**. The sky is a
transit layer with a terminus, and the last stretch to the wool is a ground problem — which is where the
wall, the pit and the funnel of §4.2 and §4.3 all live. That terminus is a property of the individual map,
not a constant, and locating it per board is unfinished work.

### 6.2 The wall stands where the map lets it, and the pit goes in front

Excavation is visible without any block log, but only under two conditions that a naive height-below-surface
test fails. A position counts toward a cell's floor only if it sits **below that cell's own surface**;
otherwise the measure picks up where players stood on the ground and reads its one-block wobble as digging,
which reports 91% of a map's cells as excavated against 17% when the filter is applied. And a cell is being
dug only if its floor **varies between matches** — a cell whose floor is identical every time is static map
geometry, a room interior or a ravine, however far below the surface it lies. Neither gate is a sample
count. Depth is then measured per cell rather than per sample, so it is not weighted by how much players
milled about inside the hole.

Read that way, **32% of the cells that ever see a sub-surface position are actively dug**, and the profile
runs against distance from the wool-room face. Normalised for how much diggable ground each column has —
surface down to the bedrock ceiling — **completeness peaks at 10–19 blocks from the room face at 0.83 and
falls to 0.70 by 80 blocks out**. Raw depth rises with distance instead, which is an artifact of deeper
bedrock further from the objective and not a statement about where the digging happens.

The excavation is a process, not a state. Taking the median time a cell is first dug: **5.5 minutes at the
room face, 6.5 at 10–19 blocks, 7.4 at 20–29 and 8.2 at 30–39**. Digging starts at the room and works
outward from it, which is why a ground push that needs the whole approach opened up takes a long time to
become possible.

Where the line falls is decided by what the map offers. Scanning all 94 worlds for straight bedrock runs
standing in front of a wool room finds one on **59 maps**, median outermost line 20 blocks from the wool or
about 13 from the room face. Where a map provides one the defence builds on it and holds it there; where it
provides none the wall comes back onto the room itself, because a wall on open ground can be undermined at
its base and a wall on the room cannot. Measured from the room face, holding positions within ten blocks of
it run **6.3% on maps with a bedrock line against 18.1% on maps without**, and wall-building 6.3% against
16.0%, with the line seven blocks closer. On `kanto` the mechanism is legible block by block: bedrock walls
seven and twenty-one blocks in front of each room, cobweb along the outer wall's crenellations and plugging
the room mouth.

The material choice is a hardness choice. Crafting tables, redstone blocks and iron bars appear in hand at
the base of these structures; they are 2.5 and 5 on the hardness scale against 2 for planks and 0.3 for
glass, so the expensive block goes where the wall is attacked.

### 6.2b An early wool falls onto ground nobody has touched

The two halves of &sect;6.2 meet in a single measure: how dug the ground around a room is at the moment its
wool is taken, counted over every diggable cell within 25 blocks and scoring an untouched cell as zero.

| when the wool fell | captures | completeness of the ground around it |
|---|---|---|
| inside 5 minutes | 1 029 | **0.010** |
| 5–10 minutes | 554 | 0.018 |
| 10–20 minutes | 263 | 0.038 |
| 20–40 minutes | 60 | 0.151 |
| past 40 minutes | 35 | **0.336** |

Within a map the same ordering holds on **42 of 43** maps carrying captures on both sides of the ten-minute
line. A wool taken in the opening minutes is taken across ground that is one per cent excavated — no pit, no
wall, nothing built. A wool held past forty minutes falls only after a third of the ground around it has
been dug out. On `outback`, where the contrast is sharpest, the rooms that fall inside ten minutes end at
0.14 completeness and the two that are contested to the end reach 0.91.

That is what makes the first capture a different event from the second rather than an earlier one. Nothing
structural stands between an attacker and a wool in the opening window, so what decides it is only where the
other team's players happen to be — which is &sect;6.5's subject.

### 6.3 Getting onto the sky, and whether a map builds a staircase

§4.6's spawn staircase is real but not universal. Where a map builds one it is unmistakable: on
`sanctum_wasser` both teams have one 25 to 30 blocks from their own spawn, climbing 15 blocks at a slope
near 1.0, walked by their own team for over 90% of samples and used in every ten-minute block of a
222-minute match. Other maps build none: `kanto` is flat on the ground for the first thirty blocks out of
spawn and then climbs continuously all the way across, gaining the same height along its lanes instead.

Entry to the network is concentrated wherever it happens. On `outback`, 5 443 ground-to-ceiling climbs
resolve into a handful of access points — **79% begin within 40 blocks of the climber's own spawn** and half
of all climbs happen at **10 of the 167 entry cells in use**. The climb takes a median four seconds, which
is what sprinting a steep staircase costs and implies no other mechanism.

Wall and staircase are separable from geometry alone, which matters because a plan can be measured the same
way. Both lie parallel to a room face and span about the same share of it, so orientation does not decide;
what decides is where the height gradient lives. Fitting height against position along *both* axes of a run
gives a ratio of 3 to 38 for a staircase and about 1 for a wall. Ownership is the independent check, and it
holds: over the fourteen longest matches the classification finds 8 walls, defender-held in 6 of them at a
median 92% of samples, and 5 spawn staircases walked by their own team in 4 of 5.

### 6.4 The captured room is a base, and the rotation is not a route

After a capture the map splits cleanly. Within fifteen blocks of the captured room the attacking team logs
**nearly nine times** the defenders' samples; within fifteen of the room still in play the defenders log
twice the attackers'. Presence in the captured room is sustained rather than incidental — 95% of team-frames
show it — and the room draws about 78% as much attacker traffic as the objective they still need. It is
also a losing place for the defence to enter: near the captured room defenders die more often than
attackers in absolute terms while holding a ninth of the presence, and near the live objective the sign
flips.

And it leads nowhere. Only **5%** of attacking lives that reach the captured room reach the other one, and
**81%** of the players who reach the remaining wool started at their own spawn and never went near it. The
room-to-room midsection is defender ground by more than nine to one. Holding the captured room shows no
measurable effect on whether the second wool falls, though with roughly one winner per match that test is
weak and the flat result should not be read as a proven null.

The reason the rotation is dead is an authoring convention that is not in the vocabulary. On **62 of the 87
maps** whose two objectives stand more than 40 blocks apart, the defending spawn lies on the segment between
them, a median 11 blocks off the line. The rotation therefore runs through the enemy's respawn point.
Traverse rates follow: 5% where the spawn is on the line, 16% where it sits off to one side, 82% where the
two wools are effectively one room.

The *midpoint* half of that convention is an artifact rather than a rule. 74 of 94 maps carry a mirror-exact
wool pair — median depth gap of one block — and where the two wools mirror about an axis the spawn on that
axis is equidistant by construction. On the 20 maps with genuinely asymmetric pairs the spawn moves off
centre as it should and is still on the line 74% of the time. The rule worth stating is **interpose the
spawn**, not centre it.

### 6.5 One wool falls early, and which one is predictable — but not from the plan

The median first capture lands at **6.1 minutes**, a third of the way into a median 16.5-minute match, with
76% inside ten minutes. A two-wool stalemate is not two contested objectives; it is one objective contested
for a long time after the other has gone.

Which one goes first is strongly predictable per map: mean concordance **0.839** across map-and-team pairs
with five or more matches, against a chance baseline of 0.661 at those sample sizes. Where the two
objectives differ materially — more than ten blocks in approach distance — the nearer one falls first 63% of
the time. But that describes about a fifth of frames. On the mirror-exact majority every distance rule
compares two equal numbers, and the discriminator is the **flank**: the first wool to fall sits on the same
side of the attacker's advance in **81%** of frames, and on 41 of 44 maps both teams favour the same hand
relative to their own direction of travel. Declaration order in `map.xml` is not the mechanism — the
first-declared wool falls first 49.1% of the time, which is chance.

The flank is not a habit, it is where the bodies are not. Measured on the attacking travel inside the enemy
half before the first capture, **both teams take the same hand in their own frame on 67.8% of 935 matches**
— 74.1% on `rot_180` boards. Same hand in each team's own frame means *opposite physical flanks*, so each
rush arrives where the other team's players are not.

What that buys is not fewer collisions on the way. Deaths in the middle third of the axis before the first
capture sit at a median of **zero** under either arrangement, on all 26 maps with enough matches to compare
— the two rushes never meet there regardless. What it buys is a thinner reception: in the ninety seconds
before the first wool falls, defenders are **34.9% of the players standing within 40 blocks of it when the
flanks are opposite against 43.1% when they are shared**, thinner on 18 of 25 maps. And the wool falls
sooner for it — **0.79 minutes sooner** within a map, faster on 18 of 26.

Match population does not explain it. Concurrent players on a long match climb from a median 26 at the
start to 44 by the hour, and early population predicts capture time hard on its own — 1.77 minutes with
under ten present, 3.67 with twenty or more. But the flank effect survives inside each band, at −0.23
minutes at 10–19 present and −0.93 at 20+, and same-hand matches carry slightly *higher* early population
than opposite-hand ones, so the confound runs against the effect rather than producing it.

The reason a flank preference can decide anything is &sect;6.2b: in that window the ground is one per cent
dug and nothing is built, so position is the only variable there is. Once a room survives it, the digging
and the wall begin and the second wool becomes a structural problem instead — which is why the effect
appears in the first capture and nowhere afterwards.

Some maps settle it outright. `sanctum_wasser` has two cheap objectives and two fortresses: across all six
recorded matches the same two wools fall in the first minutes and the survivor is always one of the other
two, once holding for three and a half hours.

### 6.6 Where interference agrees, and where it does not

The interference term predicted from generated boards — the share of the defender's spawn-to-wool corridor
that the attacker's approach also covers — has a median of 34%. Measured on recorded traffic over the
defender's reinforcement lane it is **28.6%**, interquartile 9.7% to 52.1%. For a quantity derived from
geometry alone and compared against 306 matches on 87 hand-built maps, that is agreement, and it says the
term is measuring what it claims to.

The disagreement is at the tail and it is the useful part. No generated board has zero overlap. **39 of 215
real team-frames, 18%, have no attacker traffic in the defender's reinforcement lane at all.** Built maps
offer approaches that miss the lane entirely and generated boards so far offer none.

### 6.7 Routes offered, and routes taken

A plan counts routing options. Whether a second option is used is a different question, and only recorded
play answers it. Every life that touched a wool at its spawner contributes the last stretch of its path;
resampling those by arc length and asking where the bundle separates into two groups, each holding at least
a fifth of the traffic, locates the fork.

Over **490 approach bundles on 150 maps**, the answer is lopsided and the distinction is *where* the fork
sits. A second way in at the objective, within 45 blocks, occurs on **9%**. A second lane further out
occurs on **35%**. And **63% are a single corridor end to end** — the best-sampled of them emphatically so,
with `honeycombed` at 157 approaches, `pirates_i` at 133 and `ingwaz` at 119 all showing a maximum bundle
gap under five blocks. Counting options therefore overstates what a board delivers: the option at the
objective is the rare kind, and the common one is the lane fork out in the middle that a two-legged
frontline and the mid band already produce (§3.2).

### 6.8 The voids a route forks around

What a route forks around is an enclosed hole in the playable surface, and that is recoverable without any
match data: rasterise the terrain footprint and the declared build regions, flood the complement inward
from the border, and whatever the flood cannot reach is enclosed. Labelled with `BoardDeriver`'s own classes
— **middle** when anchored terrain of two teams rings it, **encased** when nothing built touches it,
**frontline** when it touches a non-intra-team build region, **gap** when the only build touching it is
intra-team — the detector reproduces an author's own count of four maps they built: kanto 5, outback 10,
townside_mini 3, sanctum_wasser 8.

Whether a void is *used* needs a scale-free test, because a fixed separation threshold is blind to any map
whose voids are narrower than it. Taking the sign of each approach's offset from the spawn-to-wool axis
answers it at any size. Pooled over four maps, **encased** voids are rotated around on 22.7% of the
approaches that skirt them, **gap** 13.6%, **frontline** 11.9% and **middle** 10.8% — an ordering worth
noting and not yet worth trusting, because the spread inside each class is wider than the difference
between them. The case that shows why sits inside one class: outback's corner gaps are rotated around in
**5 of 97** skirting approaches, and sanctum_wasser's gaps in **22 of 101**.

### 6.9 Two maps, read against their own design

The two maps whose design intent is known in detail answer the question in opposite ways, and together they
give the rule the classes do not.

`outback`'s central build region is one polygon and an H on the ground: two wide bands joined by a bridge
fourteen blocks across at the dead centre. That bridge is the map's only lateral connection and the attack
never uses it — **none of 97 approaches that reached a wool crossed it**, three set foot on it, and it
carries 0.57% of every position sample. Inside each band the traffic hugs the outside: **90.8%** of approach
travel on the outer half against 9.2% on the inner. The four intra-team zones at the corners record 18, 11,
1 242 and 1 410 samples out of 323 152. The alternatives are not bad, they are pointless — the two bands are
wide and lead straight to the objective, so moving inward costs distance and buys nothing.

`sanctum_wasser` is the opposite case. Both of a team's objectives sit 156 and 168 blocks from its spawn and
fall five times apart in time, so distance explains nothing; which build regions the approaches cross
explains it entirely. The wool falling at a median 5.5 to 6.4 minutes is reached over the large central zone
plus one short hop. The wool holding for 28 to 46 minutes is reached over that zone plus a ten-by-forty-three
corridor, and the corridor is not one span but gap, islet, gap — a 130-cell island sits inside it. Reaching
the islet is not passing it. Those two crossings are the **largest two sky spans on the map**, 19.5% and
18.4% of all ceiling traffic over empty space, and the corner islands never connect onward: the span from an
inner corner island to the corridor islet beyond it carries 14 and 4 samples across every recorded match.
Crossing forty-three blocks of narrow build region at height against defenders who reach the same height by
running up the staircase at their own spawn is what produced this map's three-and-a-half-hour match.

The rule the two give jointly: **an alternative is used when taking it costs little, and refused when the
main road is already wide and direct.** A mid built to offer choice can be read by players as a single road
with decorative geometry either side of it.

One further reading falls out. The traffic on each map's mid islands is proportionally *early-game* — on
`sanctum_wasser` the mid island and the two outer edges run at post-to-pre-capture ratios of 10.7, 11.0 and
16.4 against a whole-map baseline of 28, while the corridor islets on the route to the surviving wool run at
170 and 71. And where the two mirrored halves of a map diverge, the divergence is a push: on the outer edge
that sees five times its mirror's sky traffic, the visiting team is present in matches it goes on to win 66%
of the time and is at the build ceiling for 34.3% of its samples, while the home team on that same ground is
on foot 40.8% of the time and winning 33%. **An asymmetry between mirrored halves of a recorded map is not
evidence of an unfair layout; it is what one side taking control looks like.**

## 7. What a plan answers today

| Measure | Reading |
|---|---|
| funnel width at an objective | 2 cells / 10 blocks on 79% of objectives; 4 cells on 18% |
| attack routes per objective | more than one on 566 of 1 029; median second route 1.31× the first, never worse than 1.92× |
| routes by frontline body | two legs 97% multi-route; one leg 47%; plain bar 38%; no frontline box 37% |
| doors per approach | I / L / donut one; U / H / clamp two, three cells apart |
| ways across the hub | ring 163/224 spawn-to-wool, 203/209 wool-to-wool; solid and branched hubs never |
| dock arrangement | canonical 27%, lopsided 73%; imbalance 0.18 against 0.40 |
| defender contest at the choke | defender starts farther than the attacker on 44% of objectives |
| route interference, second phase | attacker's rotation covers a median 34% of the defender's lane; zero on no board |
| what a hub void buys | 76% interference on the near side against 37% on the far side; the far side helps on 74% |
| rotation past the spawn point | canonical 63% within 10 blocks; lopsided 2% |
| climb from spawn to the sky | 11 blocks (surface y = 9, cap y = 20) |

## 8. What a plan cannot answer

Respawn timing and player counts are outside the model entirely, and the push–pull is a function of both.
The vertical profile of a staircase against the headroom cap is not modelled, so nothing here says whether
a climb is practical. Armour, gear from wool-room chests, and the attrition that actually decides control
of a bridge are not geometry at all.

Two of these now have a measured shape even though the plan cannot derive them. The climb is a 22-second tax
on 60% of late-game lives, and the push–pull reverses a median 3.2 times per ten minutes across 58% of the
map — quantities a plan can be scored against but not asked for. What no plan geometry reaches at all is
which of a mirror-seated pair of objectives falls first, because the discriminator is a flank preference the
board is symmetric under (§6.5).

## 9. What this changes downstream

The flow terms under **G127** should be built in an order this account implies. A **redundancy** term reads
zero on 97% of boards and will keep reading zero until two-door approaches are sampled more often and
spaced farther apart, so building it first measures nothing. A **spread** term has a live distribution
today. An **interception** term must be relative — the defender-versus-attacker approach ratio of §4.1 —
because absolute return distance describes a walk to an empty doorway, which is the one situation that
never occurs.

An **interference** term is the one this account adds that was not previously on the list, and it is the
term that gives a hub void its worth. Ways-round-a-void counts alternatives; interference says what the
alternative is *for*, which is reaching an objective without spending the approach inside the enemy's
reinforcement lane. A layout offering two ways that both collide equally has not bought anything.

Two seating rules fall out directly. When a hub encloses a void, seating the two docks on opposite sides of
it converts a decorative hole into two ways across — and, by the interference figures, into one low-collision
way and one high-collision way, which is the pair worth having. And preferring the canonical arrangement —
spawn opposite the frontline, wools flanking — roughly halves the spawn-distance imbalance and restores the
rotation-past-spawn dynamic that the lopsided arrangement removes.

Dock arrangement belongs in `StructureSummary.Canonical()` beside `hub:` and `wools:`, which also makes it a
verdict bucket key. It is cheap: the faces fall straight out of the mouth positions.

### 9.1 What the corpus changes

A third seating rule comes out of §6.4, and it is more specific than either of the two above: **interpose
the defending spawn between the two objectives it defends**. Built maps do this on roughly three quarters
of boards whether or not the two wools are symmetric, and it is what kills the wool-to-wool rotation — the
route runs through the enemy's respawn point, so nobody takes it. Centring the spawn is not the rule; on a
mirror-seated board the centring comes free, which is why it looked like one. It belongs beside the two
arrangement rules already filed as **G166** and **G167**.

**G164**'s interference term is vindicated and its gap is now visible. Measured traffic sits at 28.6%
against a predicted 34%, so the term measures what it claims. Its *origin* needs correcting: the task
defines the attacker's route as starting from a captured wool room, and the logs put four fifths of the
players who reach the remaining objective at their own spawn instead. Interference should be measured from
wherever the attack actually starts, which is the same origin in both game states.

The remaining gap is the generator's rather than the term's. 18% of real team-frames have zero overlap where
no generated board has any — built maps offer an approach that misses the defender's lane and generated
boards do not. Whatever produces that option, the far side of a hub void being one candidate and an outer
flank another, interference is the term that would show it arriving.

The **sky terminus** is a measurable the plan does not carry. Nothing in the model says how close to an
objective a bridge gets before the ground game takes over, and the logs put the wall line at a median 13
blocks from the room face where a map supplies bedrock to build on and against the room face where it does
not. `BuildGenerator` already stamps a bedrock wall where a plan declares a wall interface; what the corpus
supplies is the distance to stamp it at, and the fact that about two thirds of built maps declare one at
all. On the rest the defence chooses its own line, which is what a generated board reproduces today by
default.

Reading a board is not reading a route. §6.1 puts the ceiling at a quarter of all late-game samples and
§6.3 shows entry to it concentrated at a handful of points near each spawn — so a plan that scores a
ground route alone is scoring the phase that ends first. A **spread** term over the cell route is the term
that reaches the layer the match is actually played on, because §5 confines the sky to that footprint.

**Counting routing options overstates a board.** §6.7 puts a second way in at the objective on 9% of
recorded objectives and a single corridor end to end on 63%, so a plan reporting more than one route on 566
of 1 029 objectives is counting a thing players decline. The measure worth having is not how many options
exist but whether taking one is cheap: the alternatives that get used are the lane forks in the middle, and
the ones refused are those that trade distance for nothing. `outback` offers ten voids and a lateral bridge
across its mid and is played on two straight lines — a board can be rich in options and poor in choices, and
nothing currently distinguishes the two.

**Ways-round-a-void needs a companion.** The topological count says an alternative exists; §6.8 shows the
same class of void rotated around in 5 of 97 approaches on one map and 22 of 101 on another. Whatever
separates them is not the class, the size or the distance to the objective — exposure is the standing
hypothesis and it is unmeasured. Until it is, a hub void should be credited with an option rather than with
a route being taken.

### 9.2 Evaluating a board twice, corrected

A board is worth evaluating twice — **G168** — and the corpus changes what the second reading is. Before the
first wool falls, both objectives are defended from the spawn. After it falls, one room is an attacker's
forward position that produces attrition, the defender has one fewer thing to hold, and **the approach to
the remaining objective is unchanged from the approach to the first** — no new route opens, because the
wool-to-wool rotation is not taken (§4.8, §6.4). The second state is a change in pressure and in what the
defence must cover, not in the graph. A post-capture state defined as "rotation between objectives becomes
available" would model something that does not happen.

Which objective the second state belongs to is the open question. On a mirror-seated pair the plan cannot
know, because the discriminator is a flank preference the board is symmetric under, so both post-capture
states have to be scored. Seating the two objectives with a material asymmetry makes the answer roughly
two-to-one predictable, at the cost of the fairness the mirror provides. That trade is currently made by
default — the composer emits mirror pairs under `rot_180` — and is worth making deliberately.

### 9.3 How far these findings carry

The corpus figures rest on 333 matches and 94 maps, which is a sound base for the shape of the mode. The
structure findings — walls, staircases, entry points — rest on the fourteen longest matches and seven maps,
which is not. Eight walls and five staircases is a small sample, and the thresholds that classify them were
set on one map before being applied to the others. What makes those findings more than curve-fitting is the
ownership check: nothing in the geometry knows which team defends which room, and the classification agrees
with the recorded traffic in three cases out of four.

The right reading is that the mode's shape is established and the per-map numbers are provisional. A wall
stands 13 blocks out on the maps measured; that it stands *somewhere*, that the pit goes in front of it,
that the ceiling carries the late game, that the captured room is held and leads nowhere — those are the
claims the corpus supports, and they are the ones the generator should be built against.
