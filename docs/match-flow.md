# Match flow — how a CTW map is played, and how a plan is read for it

The generator settles geometry. A match is what that geometry produces once two teams of up to thirty
players are standing on it for forty minutes, and almost none of it is visible in a rectangle partition.
This document holds both halves. The first is the **flow reading**: the three fidelities a route has, what
is measured on each, and what the generator's own vocabulary does to movement. The second is the **played
account**: how a match actually unfolds on a CTW map, in order, from the opening rush to the second wool.

The played account is authored knowledge from matches, not a derivation. It is the thing the derivations
are checked against — where a measure and the account disagree, the account is the evidence and the measure
is the suspect.

Numbers quoted here are measured over composer output at version `marker-id-1`: a census of **5 200 boards**
(13 player counts from 8 to 32, 400 seeds each, 2 teams, `rot_180`, cell 5) carrying **9 154 wools**, and a
routed subset of **560 boards** where the full cell-level analysis ran.

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
one team's lane that the other team's lane also covers. This is the quantity behind §4.8, and it is not the
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
weapons. From it the attackers put pressure on the rest of the defence, which is where the **wool-to-wool
route** becomes the live one. A player holding the captured room builds a staircase of their own, or uses
the already-connected sky, to harass the defenders as they respawn; that pressure is what lets the rest of
the team make progress on the remaining wool.

### 4.9 Why the second wool is contested before it is reached

The tension in that phase is not that the rotation touches the spawn. It is that two routes collide: the
attacker pushing from the captured room toward the remaining wool, and the defender travelling from spawn to
the same wool. Both are heading for one objective from different origins, and the ground they cross is
shared.

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

The collision is also vertical, and asymmetric. A player arriving from the captured room travels on the sky
network at the build cap; a defender who has just died arrives at the spawn point on the ground, because
spawns sit on the terrain surface. On generated boards the surface is y = 9 and the cap is y = 20, so a
respawning defender has **eleven blocks of staircase to climb** before reaching the layer the attacker is
already standing on — and climbs it while that attacker holds height over them and while the attacker's own
teammates approach the same wool from the middle of the map. That is the shape of the pressure: not one
route crossing another on a flat plan, but one team above the other at the moment the other is weakest.

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

## 6. What a plan answers today

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

## 7. What a plan cannot answer

Respawn timing and player counts are outside the model entirely, and the push–pull is a function of both.
The vertical profile of a staircase against the headroom cap is not modelled, so nothing here says whether
a climb is practical. Armour, gear from wool-room chests, and the attrition that actually decides control
of a bridge are not geometry at all. And the ground truth for any of it is recorded traffic — the
`pgmlogger` work under **G33** — not a derivation.

## 8. What this changes downstream

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

Finally, a board is worth evaluating twice. Before the first wool falls, both objectives are defended from
the spawn. After it falls, one room is an attacker's forward node and the wool-to-wool route is live. That
is a change to the evaluator's shape rather than a new term, and it is the one this account most clearly
demands.
