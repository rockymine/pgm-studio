# Approaches — what the ground around an objective does to a match

Every other document in this repository can be settled by reading something. The corpus says what authors
built, `PGM` says what a server does with an element, and the code says what the studio produces. **None of
them says what is correct for a map as it is played**, and this document is made entirely of that kind of
claim. It is therefore the one document whose contents are the author's rather than the repository's, and it
is kept apart from the tool and capability documents for exactly that reason: a claim about how a map plays
that got mixed into a description of a JSON field is a claim nobody ever audits.

**Every statement here carries its standing.** A claim marked **[author]** was stated by the author and is
settled — every claim below is one, so anything in this document may be turned into a rule id, a validator
finding or a generator constraint. A claim marked **[review]** is drawn from what real maps do or from the
studio's own faults and is waiting on the author; it may be right, and it is not yet law. Nothing carrying
that mark becomes a rule while it still does, and the mark exists because of how this document grows: someone
collects what the author said and sorts it, and a sentence nobody has read back is exactly the kind that
turns an unreviewed opinion into a constraint.

**A claim decided on a map note links back to the thread it was decided in.** A note the agent reads
as a ruling is a gameplay decision meant to hold on every map, and once the author resolves it the claim is
written here marked **[author](/maps/{slug}/sketch?note={id})** — the link opens the Review phase's Notes step on that
note's thread, with the place it was about and the words it was settled in (`docs/tools/sketch.md`, *Notes*).
A claim without a link was stated elsewhere — in a conversation or a review — and stands the same.

The failure this separation prevents has already happened once, and it is worth stating so nobody repeats it.
A destroyable and a core **float a few blocks above the terrain by design** — a core resting on the ground
cannot leak, and a destroyable resting on it is trivially covered — and that has been PGM's behaviour from the
beginning. Measuring the gap and reasoning from first principles produced a confident, filed, committed claim
that every generated destroy map was unwinnable. The measurement was right and the conclusion was invented.
Neither the corpus nor the code would have corrected it; one question would have.

## The layout is the design, not the container

**[author]** A layout is a control on player flow. The voids, the gaps between pieces and the placement of
pieces decide where a player can go, how long it takes and what they can see on the way, and every later
decision inherits that. A board is therefore not a container that scenery is sprinkled into: the ground *is*
the design, and the scenery is a second layer of the same argument.

**[author]** The rectangles a composer emits are a starting point rather than the shape. They are rectilinear
to keep a first pass legible, which is precisely why the pipeline walks into the sketch tool next — the shapes
are there to be dragged into a swirl, given Bézier edges, cut with a subtract, stepped in height. A capture
layout can be as organic as a destroy one, and taking the compiled rectangles as final is taking the
scaffolding for the building.

**[author]** On a capture map, flow is controlled primarily by **void**, and the void is the design. The gaps
between pieces are not what is left over once the ground is drawn; they are the instrument.

**[author]** A void works on a plain rectangle too. Even a large rectangular board becomes a designed one by
cutting a hole in front of the objective — a gap far enough across that it cannot simply be jumped, roughly
twenty blocks, though that number is illustrative rather than a law. It need not be a straight edge: an
organic polygon reads as terrain where a ruled line reads as a wall. What it does is force every attacker to
pass **around** it, which is a decision, a delay, and a place a defender can watch.

**[author]** On a destroy board that instrument is narrower than the paragraph above makes it sound, and the
narrowing is law rather than preference. **Void belongs between the teams, not across an approach.** A hole
cut in the middle of a team's own ground — between its objectives and the middle, where its defenders move —
funnels play into whatever side channels are left and empties the ground the contest was supposed to happen
on. `tallow-kilnrow` is the worked counter-example: an 88-block cut across 65% of the board's width sat
between the objectives and the middle while the mid band, where the two sides actually meet, stayed solid
ground. The hole was where the join belongs and the join was where the hole belongs.

**[author]** So on a `dtm` or `dtc` board, the middle-of-terrain hole is **withdrawn**, and what replaces it
is a **depression or a pond** — the same interruption of a run, the same reason to go around or drop through,
without removing the ground. A depression is also an entrance from *below*, which is a tactic the hole does
not offer at all. Void still does its work at the seam between the two teams' lands, and there it is the same
instrument the capture boards use.

**[author]** One consequence is worth naming with it, because the obvious correction overshoots: a hole is
also what makes a flank worth walking to. Four small holes around a connected middle draw play into the
centre and leave the flanks unused, which is a different failure from the one being fixed. Where the void
goes on a destroy board is therefore a composition decision about which ground should be contested, not a
geometry one about how much of the board is missing.

## Each element makes a specific tactic

**[author]** This is the part worth reading slowly, because "put a forest there" is not the point — what the
forest *does* is. The elements differ in dimension and in timing, not in flavour.

A **void hole** in front of a goal makes players go around, and turns the two ways round it into two
approaches a defender must split attention across. A **hill** is not merely height and sightline: attackers
climb to its ledge and bridge from there toward the objective, arriving from **above**, so a defender on the
ground has to watch the sky as well as the approaches, and the bridge is a visible commitment that takes time
to build. A **forest** gives cover to within a few blocks of the objective, which makes it most valuable
**early**, when someone can move through unseen and be on the goal quickly; it also gives height a second way,
since a tree can be climbed for the same advantage a hill gives. A **small depression** near the objective is
an entrance from **below** — a player drops in, tunnels, and comes up under the goal where nobody is looking.
A **river or a drop** forces a bridge, which is a chokepoint that must be built before it can be used. A
**village** gives cover the whole way in and is fought through room by room. And **open ground** exposes,
which is what an objective itself wants around it.

Read together those are approaches from **around**, **above**, **below** and **through** — not four flavours
of the same walk. That difference is what separates a composed objective from a decorated one.

## An objective sits exposed, and the ground around it is composed

**[author]** A monument or a core in the open, a forest on one side, a hill on the other, a village behind.
That is the method in one sentence: the approach is legible, the defender has somewhere to hold, the attacker
has a way to arrive unseen and a price for using it. None of it survives being scattered.

**[author]** The point of composing it that way is that the approaches **differ**. Ringing an objective with
different ground is not scenery variety — it is how a goal comes to have several ways in that are not the same
way twice, and that arrive from different directions in three dimensions. A defender must then choose what to
watch and an attacker must choose what to pay, which is a decision on both sides rather than one lane
repeated. Flat ground inside a nice environment is a real style and a legitimate answer; it is rarely the
better one, and it should be a choice rather than what happens when nobody decides.

**[author]** Cover keeps its distance from a goal. A tree, a boulder or a building may not stand within
**four blocks** of the ground a destroyable or a core covers, and ground cover may: grass, fern and flowers
grow across that ground and under a floating monument, while the two-block grass that hides a footstep does
not. The rule and its mechanism are `docs/world-export/decoration.md` §3.1.

**[author]** A wool's monument may stand anywhere near the spawn, and a monument is a **standalone stamp**.
It is tied to the capturing team's spawn structure for simplicity, not because it belongs inside one — an
author who wants the wool carried to a stone in the market square, a shrine on the concourse or a plinth on
the green is authoring something legitimate. Two conditions bound it, and they are the whole of the rule:
the monument stands **near that team's spawn**, and it is **discoverable** — a player carrying a wool must
not have to search for where to put it. A monument far from the spawn, or hidden inside a structure a player
has to find, is the failure this exists to prevent.

**[author]** A **defence wall** is a CTW device. It is bedrock, it is pre-built, and it exists to slow an
attack down and give the defence a prepared line to hold — which is why it stands on the interface between
two pieces rather than being derived from the ground. A composed board carries one per wool approach, on a seam
nothing runs past, near the room — and two on a donut, one across each leg by the entry bar, because a donut is
two ways in and one wall leaves the other open (`model.md` §5.14).

**[author]** **The wall is meant to be in the way, and that is the whole of it.** A wall standing across a
wool's approach is not a fault to be routed around: blocking the way in is the device's purpose, giving the
defence something prepared before it has built anything and costing a tunneller the shortcut. A report that
reads a walled approach as a wool nobody can reach has read the wall as damage rather than as the line. Two
things bound it. **One wall, on one interface** — a wool behind two or three of them is not a prepared line,
it is a sealed room. And it is **narrow enough to be a line rather than a barricade**: twenty blocks of
bedrock across a lane is a wall players go round instead of through, and a wall nobody engages with is only in
the defence's way.

**[author]** **Ground pulled out past the wall's ends is what breaks it.** The wall spans the interface it is
authored on, so terrain widened beyond that interface leaves an open shoulder beside it — and a wall with a
walk around it has stopped being a decision and become an obstacle to the team it was built for. A skilled
player jumping the wall is the device working; a player strolling past its end is the device gone. When an
approach is reshaped, the wall's interface is reshaped with it.

**[author]** **A wool room is defended from its corner, not from inside a field.** The room wants **two of
its faces on void** — it sits in a corner, so a defender holds two lines and an attacker has two to choose
from. Three faces on void is the ordinary composed shape, one connecting piece and the rest open, and that is
fine. What is not is a room with ground on every side: terrain added all the way round a room leaves nothing
to hold and nowhere for the fight to happen, and the room stops being a place and becomes a spot in a field.
Adding area around a room while reshaping the ground near it is the way this happens, and the room being
central to its own terrain is the tell. A room at the end of a spur is the other failure and a milder one —
it is defensible and it is one queue.

**[author]** **A bay is at least sixteen blocks across where it touches a goal or a spawn.** Negative space
between two pieces is crossed by jumping long before it is crossed by building: a short gap between a
frontline and a wool room, or between a spawn and a wool room, lets a player tower at the near edge and jump
in, which deletes the approach the board was built around. Sixteen blocks is the floor for such a bay, and a
plain hole in the middle of a team's own ground may be twelve. The distance is in **blocks** and not in
cells — a floor stated as a cell count moves with the grid scale, and the crossing does not care what the
grid was.

**[author]** The capture side of this is already law and the destroy side is not. `rules.md` WL8 records that
a wool's default is a **single chokepoint route** and that real maps add alternative routes — and, usefully
for a river or a drop, that an approach crossing a sealed zone counts as an approach even when it must be
bridged rather than walked. There is no equivalent rule for a destroyable or a core, which is exactly where
one is wanted: a goal a team defends wants more than one angle onto it, or the defence is a single doorway and
the attack is a queue.

## A destroy board is not a capture board with a different goal

**[author]** The topology is inverted. In capture, the thing a team wants is deep in *enemy* ground, so the
board is built around a long run out and a longer run back. In destroy, the thing a team defends is its
**own** monument: the spawn sits remote at the back, the monument is a short walk forward of it, and the
contested space is everything beyond. That single difference resizes the whole board — the run is shorter, the
defended ground is smaller and closer, and the space between the two teams is correspondingly larger and
emptier. It is also why destroy maps have room for scenery that capture maps do not.

**[author]** A destroyable and a core may stand almost anywhere ground exists — a field, a plateau, a
frontline. Neither needs a room, a dead-end lane or a protection region; both are stamped directly into the
world. The three places they may not stand are the void, a spawn and a wool room, which is `OB17` and `OB30` and is
enforced (`docs/pgm/destroyables-and-cores.md` §8).

**[author]** A monument or a core never stands at the end of a lane — ending a lane is what a wool does — and
beyond that a destroy board's shape is open. It may be islands in the void: one long island with a side island
holding the objective, a peninsula of several islands, one large island of abstract shapes with smaller islands
between, a large flat island where built ground and nature take turns, floating islands. A layout may be drawn
from a rule rather than invented — points sampled at random over an area, a noise map, or a set of about twenty
nodes toured by annealing and turned into the map, which is how *Annealing I*–*IV* were made.

**[author]** The spawn sits remote at the back on every kind of board, and on a destroy board it sits **in**
the land at its back rather than on a box behind it. A spawn hung off the hub is how capture boards evolved and is
right for them; a destroy spawn hung off the back of the main island across a gap is attached to the map without
belonging to it. Its house stands on the terrain — on a hill, with mountains or stacks of rock beside it, an
iron mine somewhere near — and it may stand ten blocks into the land and still be at the back.

**[author]** The objective stands in front of the spawn, where a defender sees it, and rarely straight ahead: it
is shifted to one side, and on some boards it stands nearly beside the spawn a few blocks off. Two are an east
and a west, or a front and a back, where back is near the spawn but still apart from it. Measured straight from
the spawn region's centre against the line to the enemy spawn, the 999 goals on 303 corpus destroy maps sit a
median 42° off that line (p25 18°, p75 76°), a quarter within 20° of it; the 374 on this studio's agent boards
sit a median 28° off, and 38% within 20°.

**[author]** Both float a few blocks above the terrain, and that is the design rather than a defect. What a
goal needs beneath it is terrain somewhere below, not terrain directly under its lowest block.

**[author]** Where a board carries more than one goal they are placed **against each other rather than
scattered** — a west and an east, or two forward with one back near the spawn, or two back with one forward.
That arrangement is the board's shape, because each goal is a place a team has to hold and their spacing is
what decides whether the defence is one line or three. Measured over the 127 corpus maps carrying a destroy
objective, per team: one destroyable in 55% of them, two in 37%, three in 5%; cores are rarer and tighter,
one in 77%, two in 19%, three in a single map. The ordinary combined board is one destroyable and one core.
**A large board with a single goal on it is not an underfilled board** — it is the most common destroy map
there is.

## A capture-point board is laid out around its own centre

**[author]** A hill is a **square** pad. Round is legal, common and looks well, but what the studio authors
is square, and a board that wants a disc says so rather than getting one by default.

**[author]** The count follows the team count. **Two teams get two or three points**, and three is the
ordinary board; **four teams get one per team plus a centre**, which is five. In the corpus this is the
shape almost without exception: over the 103 KotH maps outside two arcade point-grids, the 90 two-team maps
carry three points 61 times, two 14 times and one 8 times, and of the 10 four-team maps five carry exactly
five points.

**[author]** The arrangement is **one point dead centre and the rest to the sides** — and "the sides" means
across the line between the spawns, not along it. That is the whole difference between a capture board and a
destroy board: a destroyable belongs to the team behind it, so it sits forward of its own spawn and the pair
of them defines a front; a capture point belongs to nobody, so it has to be the same walk for everyone, and
the only positions that are the same walk for everyone lie on the map's own axis of symmetry.

The corpus states it as symmetry rather than as distance. Of the 80 two-team maps with a usable spawn frame,
**60 have a hill set closed under the 180° rotation that swaps the two spawns** — every point is either at the
centre of that rotation or has a partner that is its image — and 51 have exactly one point at the centre
itself. Among the three-point maps that is 43 of 55 with a centre point and 39 of 55 that are precisely a
centre plus a mirrored pair. 167 of 217 points sit equidistant from both spawns. Four-team boards do the same
thing one order up: every one of the seven with a usable frame is a ring of exactly four around nought, one or
two centre points, and five of the seven put the ring at **45° to the spawns** — on the diagonals *between*
neighbouring spawns, so each ring point is the same walk for two teams rather than the doorstep of one. The
two exceptions are deliberate: `koth/yukoth` places its four in front of the spawns and hands each team its
own with `initial-owner`, which is a different game.

**[author]** Distance is stated against the board, not in blocks. An off-centre point sits at roughly
**two-thirds of the way from the map centre to a spawn**, and anywhere from half to the whole of it is
ordinary. Measured: 0.66 of the centre-to-spawn distance for two teams (quartiles 0.52 and 0.88) and 0.90 for
four (quartiles 0.52 and 0.99). On the median corpus board — spawns 94 blocks apart — that is a point about
30 blocks out from the middle, but the ratio is the rule and the block count is the consequence, because a
board twice the size wants its points twice as far out.

**[author] This section settles where the points go, and it settles nothing about what is around them.** A
capture board is a control game before it is anything else — its ground is built rather than landscape, its
points are entered from a decided number of directions, and a point raised high over open ground is a point
whose first holder keeps it. That law is `match-flow.md` §10, and it governs the board this one places the
points on.

`docs/pgm/control-points.md` owns the mechanism these claims are about: what a point is, what it is built of
and what PGM does with it.

## Two mechanisms whose use is narrower than they look

**[author]** A **water lane** is a gap between islands that becomes bridgeable part-way through a match rather
than at the start. Players cannot build there for **45 minutes**, and the consequence is a hard constraint
rather than a preference: **a lane can never be what connects two teams' lands**, because for three quarters
of an hour there would be no route between them. The regions that join a board are build zones; a lane is
something else.

**[author]** What it is for, then, is a **second** approach that opens late — where a goal is tucked away and
the endgame should change shape rather than the opening. `docs/pgm/water-lanes.md` owns the mechanism.

**[author]** Whether a hole can be **crossed** is a separate decision from cutting it, and it is made in the
intent rather than in the geometry. A void gap with no build region over it is permanent: nobody bridges it,
and the approach it forces is around. The same gap with a build region covering it is crossable from the first
minute, at the price of the time and material a bridge costs and the visibility of building one. Both are
legitimate and they play differently, so a channel cut without deciding which it is has had half of it decided
by accident.

## Circulation is decided before dressing

**[author]** Scenery is placed last in the pipeline and decided first in the design, and reversing those is
what makes a board read as cluttered rather than as furnished. Placing props wherever the ground will take one
produces trees and buildings standing in the routes, so reaching a build region means walking round a house
and then round a tree, neither of which anybody put there.

The order that works states the **movement** first: where a player walks from spawn to goal, where the
flanking approach runs, where a village's street is. Those runs and a margin either side are then the ground
foliage does not get, and everything else is where a wood or a settlement may stand. It turns density from a
number into a consequence, because the space left over once the circulation is drawn is the space a forest is
allowed to fill.

**[author]** Density is a design decision. A leaf count alone does not say whether a board is wooded or
buried, and what settles it is how many trees the leaves are divided among — a few hundred leaves per tree is
a canopy with gaps under it, and a few dozen is a blanket laid over the board. The measure that would actually
answer it is neither number but what share of the ground stands under a leaf.

## A second storey is played on

**[author]** A tunnel under the ground is not scenery beneath the board. It is a second way to an objective —
a flanking route with its own cover, its own sightlines and its own way of being defended — so the storeys of
a stacked board and the ways between them are part of the map, and a read that cannot see them is describing
half of it.

That is what makes the gap between two storeys a design quantity rather than an artifact of drawing. A deck
over a yard with nothing joining them is two boards; a stair, a ramp or a hole cut in the roof is what makes
it one, and where the join is decides which approach the second storey serves. `docs/tools/sketch.md`
§ Layers carries what the studio does with that, and `SK11` is the complaint it raises where a storey has
nothing onto it.

## Destroy boards worth reading, and what each shows

**[author]** These are the destroy boards the author points to for a board's shape and its terrain. None is a
template; read together they show how far apart two good destroy boards can be. All but *The Nile* are in the
corpora.

| Board | Corpus | What it shows |
|---|---|---|
| *Atromix* | `PublicMaps/dtcm/atromix` | an isolated island for each team's monuments, and a middle island between the teams |
| *The Fenland* | `PublicMaps/dtcm/the_fenland` | one box: the spawn in the back middle, a raised border players walk, a middle of tiny islands, rivers and trees, and the monument to the left of the spawn |
| *Annealing III* | `CommunityMaps/dtcm/annealing_iii` | islands from about twenty nodes toured by annealing |
| *Monument Valley* | `PublicMaps/dtcm/monument_valley` | one box with the spawns raised and diagonally across; the monument raised and diagonally in front, mountains round it to come at it from height, a river running toward it to come at it from below |
| *Alpine Mining II* | `CommunityMaps/dtcm/alpine_mining_ii` | a long lane with two monuments and a great deal of terrain, some of it never walked and there to be looked at; an iron cave under the mountain whose exit leads toward a monument; small alpine villages laid out properly |
| *The Nile* | not in either corpus | tunnel spawns; sandy ground with intricate desert building up top; the river through the middle with palms and a large wheat field; a single monument on a square board; an iron mine |
| *Kuusepuu* | `CommunityMaps/dtcm/kuusepuu` | islands laid out in a box |
| *Sunrise over Paradise* | `PublicMaps/dtcm/sunrise_over_paradise` | geometric islands, some joined by bridges, inside a large ring of water; the monument islands all the way to the side of the spawn, reached only by boat |

## Where the rest lives

`match-flow.md` is what a match does to a finished board — the three fidelities a route has and the played
account behind them. `traffic-ground-truth.md` is what real matches measured. This document is upstream of
both: it is what the ground is *for* before anyone walks it.
