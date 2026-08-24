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

**[author]** A **defence wall** is a CTW device. It is bedrock, it is pre-built, and it exists to slow an
attack down and give the defence a prepared line to hold — which is why it is authored on the interface
between two pieces rather than derived, and why nothing generated ever asks for one (`mapgen-review.md` MG21).

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
world. The three places they may not stand are the void, a spawn and a wool room, which is `OB17` and is
enforced (`docs/pgm/destroyables-and-cores.md` §8).

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
answer it is neither number but what share of the ground stands under a leaf (`B96`).

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

## Where the rest lives

`match-flow.md` is what a match does to a finished board — the three fidelities a route has and the played
account behind them. `traffic-ground-truth.md` is what real matches measured. This document is upstream of
both: it is what the ground is *for* before anyone walks it.
