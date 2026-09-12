# Control points — the CP, KotH and payload objective

A control point is a place on the map that a team owns by standing on it. Nothing is broken and nothing is
carried: a player walks into a region, a timer runs while their team is the only one — or the largest one —
inside it, and when the timer fills the point changes hands. Ownership pays out in score, and the score limit
is what ends the match. Between the timer and the score there is a second visible half: the blocks of the
point are recoloured to the owning team's dye colour, and the fraction captured is drawn on them as a pie.

The structure that gets recoloured is the thing players mean by "the hill", and it is built out of a
colour-affected material — of 359 pads read out of the corpus worlds, **246 are stained clay, 71 wool and 42
stained glass** (§6). It is drawn either as a filled square or as a disc, and both are ordinary: 174 of those
pads are solid rectangles and 120 are discs.

The studio reads both spellings of the point and the `<score>` module they pay into, and writes them back
under the spelling they arrived in. `<payloads>` is still refused (`supported-maps.md`): a payload is a
furnace minecart players push, and none of the geometry that describes one is read. This document is the
contract: what PGM does, read from `tc.oc.pgm.controlpoint`, and what authors actually build, measured over
both corpora.

## 1. One module, three elements

`ControlPointModule` parses three root elements and builds the same `ControlPointDefinition` from each.
`ControlPointParser.Type` — `HILL`, `POINT`, `PAYLOAD` — is the only thing that differs, and all it changes
is a handful of **defaults** and which map tag the module contributes. There is one objective here wearing
three names, which is why this document is named for the module rather than for the gamemode.

| element | type | tag | gamemode |
|---|---|---|---|
| `<control-points><control-point/></control-points>` | `POINT` | `controlpoint` | `cp` |
| `<king><hills><hill/></hills></king>` | `HILL` | `controlpoint` | `koth` |
| `<payloads><payload/></payloads>` | `PAYLOAD` | `payload` | `payload` |

The CP and KotH tags carry the **same id**, `controlpoint`, and the module adds the KotH one only
`if (tags.isEmpty())` — so a map declaring both elements is tagged CP, not KotH. No corpus map does; the
two are alternatives, not a pair.

Payload is a **furnace minecart players push around the board**, and the capture mechanic rides along with
it: the point moves, so it takes a required `location` and `radius` instead of a capture region and uses
`EverywhereRegion` for capture. It is behind a server experiment flag (`experiments.payload`) and throws
`InvalidXMLException` when the flag is off, with the message that its XML syntax is expected to change. Nine
corpus maps carry one. The studio refuses it, and it is out of scope below except where a default differs.

**Any element inside the container is a point.** `XMLUtils.flattenElements(root, "control-points",
"control-point")` recurses into the container with `minChildDepth` exhausted, at which point it takes *every*
child regardless of tag name. The same call shape flattens `<king>` over `"hills"`/`"hill"`, so
`<king><hill/></king>` parses exactly like `<king><hills><hill/></hills></king>`, and a misspelled child tag
inside `<hills>` is silently accepted as a hill rather than reported.

**Attributes descend one level at a time, and therefore all the way down.** Every element in the document is
an `InheritingElement`, which on construction copies its parent's attributes into itself for every name it
does not set. Flattening builds a copy of `<hills>` (inheriting `<king>`'s attributes) and then copies of
each `<hill>` whose parent is that copy — so a `<hill>` inherits from `<hills>` which inherits from `<king>`,
and a value written on the outermost element reaches the innermost. This is how the corpus writes the shared
tuning once: `<hills required="false" capture-time="5s" points="1" …>` with the per-hill elements carrying
only a name and their regions.

**An unnamed point is called "Hill", whichever element it came from.** One `AtomicInteger` runs across the
whole document, advanced only by points that declare no `name`; the first gets `Hill`, the rest get
`Hill 2`, `Hill 3`. Control points are parsed before hills and hills before payloads, so an unnamed
`<control-point>` is also named "Hill".

## 2. What a point is made of — three regions and a material filter

**The capture region is required** (`capture-region`, or the short spelling `capture`, as a child element or
an id attribute) and it is the only one that decides anything about play. Membership is per block: the player
tracker takes the player's position, floors it to a block, and asks the region whether it contains that
**block's centre**. The block a player occupies is the one their feet are in, so a player standing on top of
a pad whose top layer is at `y = P` is at `y = P + 1`, and the capture region has to reach that high. It does
in 306 of the 344 corpus points whose pad could be located in the world; the misses are all maps that put
the *display* regions somewhere else entirely (§6).

**The progress display region** (`progress-display-region` / `progress`) and **the owner display region**
(`owner-display-region` / `captured`) are optional, purely cosmetic, and must be **block-bounded** —
`BlockBoundedValidation` refuses a region whose blocks cannot be enumerated, so `<everywhere/>` and an
unbounded complement are errors here while they are legal as a capture region.

Both display regions are enumerated **once, at match load**, into a `FiniteBlockRegion` of the blocks that
pass the **visual-materials filter**, and the original block states are saved into the world snapshot. Two
consequences follow and both matter to an author. A block placed inside a display region after the match
starts is never recoloured, because the set was fixed before it existed. And the owner display region is
built with `InverseFilter(progressDisplayRegion)` applied on top — **a block belongs to at most one of the
two, and the progress region wins**.

`visual-materials` is a *filter property list*: absent, it defaults to every colour-affected material;
present, the named filters are combined with `any`. The colour-affected set is PGM's, not a choice this
document makes:

| server | colour-affected materials |
|---|---|
| 1.8 (SportPaper) | wool, carpet, stained clay, stained glass, stained glass pane, banner, ink sack |
| modern (1.20.6+) | the above plus concrete, concrete powder, shulker box, glazed terracotta, candle, candle cake, wall banner |

**Hardened clay is not in it; stained clay is.** A pad built out of `hard clay` parses, exports, loads and
never changes colour. So does one built of stone, brick or planks. This is the single easiest way to build a
hill that looks finished and shows nothing, and nothing yet says so (`PG7`).

## 3. The capture state machine

`ControlPointTickTask` ticks every point once per server tick (50 ms), and each tick does a capture cycle and
a score cycle.

The capture cycle counts the players inside the capture region that pass the `player-filter`, groups them by
team, and finds the leading team and the runner-up. `capture-rule` then turns those counts into a **lead**,
which is the number of player-ticks the dominating team gets credited with:

| `capture-rule` | lead | meaning |
|---|---|---|
| `exclusive` (default) | zero if anyone else is present | one team alone on the point |
| `majority` | leader minus everyone else | more than half the players |
| `lead` | leader minus runner-up | more than any single other team |

`time-multiplier` scales the tick by `1 + (lead − 1) × multiplier`, so extra bodies capture faster. Its
default for a hill is `0.1`; **163 of 316 corpus points set it to `0`** and turn the crowd bonus off
outright.

With a lead the point is dominated by that team; with no lead it either decays or, when players are present
but no team leads, is *contested*. Four rates govern what happens to the accumulated `capturingTime`, all of
them multipliers on the tick rather than durations:

| attribute | what it scales | `<hill>` default | `<control-point>` default |
|---|---|---|---|
| `recovery` / `recovery-rate` | the owner pushing progress back down | `1` | `+∞` (instant) |
| `decay` / `decay-rate` | progress bleeding away with nobody on the point | `0` (holds) | `+∞` (instant) |
| `contested` / `contested-rate` | progress bleeding away while contested | = `decay` | = `decay` |
| `owned-decay` / `owned-decay-rate` | an owned point drifting back to neutral | `0` | `0` |

`incremental` is shorthand for the first two and **may not be combined with any of them** — PGM throws if it
is. `incremental="true"` means `recovery=1, decay=0`; `false` means both `+∞`. Its default is `true` for a
hill and `false` for a control point, which is the whole difference between the two elements in one line: a
hill remembers partial progress, a control point snaps back the moment the last player steps off.

`owned-decay` requires `neutral-state`, and PGM refuses the combination without it.

**`neutral-state`** decides whether the point passes through an unowned state between owners — default
`true` for a hill, `false` for a control point. With it, a challenger first spends `capture-time`
*uncapturing* and then another `capture-time` capturing; without it, the point flips straight from one owner
to the next. `permanent` (default `false`) disables a point after its first change of state.

`capture-time` is the base duration, default **30 s** — which only one corpus KotH map writes; **193 of 316
points write `5s`** and the rest cluster at 3–10 s.

Two filters narrow who plays: `capture-filter` is asked of the *team* and says who may own the point at all;
`player-filter` is asked of each *player* and says who counts toward the lead. Both are rare: a capture
filter on 11 points across 2 maps, a player filter on 14 across 4.

`beacon` is parsed into a `BlockVector` on the definition and read by nothing. It is dead in current PGM and
no corpus map sets it.

Every attribute `ControlPointParser` reads, in one place, with the default each element gives it. Where two
columns differ, that difference *is* the element:

| attribute | `<hill>` | `<control-point>` | `<payload>` |
|---|---|---|---|
| `capture-region` / `capture` | **required** | **required** | everywhere (not read) |
| `progress-display-region` / `progress` | none, block-bounded | none, block-bounded | none, block-bounded |
| `owner-display-region` / `captured` | none, block-bounded | none, block-bounded | none, block-bounded |
| `location`, `radius` | — | — | **required** |
| `visual-materials` | every colour-affected material | same | same |
| `id`, `name` | optional; `Hill`, `Hill 2`, … | same | same |
| `initial-owner` | none | none | none |
| `capture-time` | `30s` | `30s` | `30s` |
| `incremental` | `true` | `false` | `true` |
| `recovery` / `recovery-rate` | `1` | `+∞` | `1` |
| `decay` / `decay-rate` | `0` | `+∞` | `0` |
| `contested` / `contested-rate` | `= decay` | `= decay` | `= decay` |
| `owned-decay` / `owned-decay-rate` | `0`, needs `neutral-state` | same | same |
| `time-multiplier` | `0.1` | `0` | `0` |
| `neutral-state` | `true` | `false` | `true` |
| `capture-rule` | `exclusive` | `exclusive` | `exclusive` |
| `permanent` | `false` | `false` | `false` |
| `points` | `1` | `1` | `0` |
| `owner-points` | `0` | `0` | `1` |
| `points-growth` | `+∞` | `+∞` | `+∞` |
| `show-progress` | `true` | `false` | `true` |
| `required` | unset → **`true`** at proto ≥ 1.4.0 | same | same |
| `capture-filter`, `player-filter` | none | none | none |
| `show`, `show-messages`, `show-effects`, `show-info`, `show-sidebar`, `show-waypoint`, `stats` | `true` | `true` | `true` |
| `scoreboard-filter` | allow | allow | allow |
| `beacon` | parsed, unread | parsed, unread | parsed, unread |
| `beam`, `display-filter` | — | — | `true`, allow |

## 4. Scoring, and the two ways a KotH match ends

A point pays its owner `points` per second (default `1`, `0` for a payload), a one-off `owner-points` on
capture which is taken back off the loser (default `0`, `1` for a payload), and `points-growth` doubles the
per-second rate every that-many seconds (default `+∞`, i.e. never).

**None of that happens without a `<score>` element.** `tickScore` looks up `ScoreMatchModule`, and
`ScoreModule.parse` returns null when the document has no `<score>` child — so a KotH map with `points="1"`
on every hill and no score module scores nothing at all, for the whole match, silently. Corpus authors know
it: `koth/qboid` carries `<score><kills>0</kills><deaths>0</deaths></score>` under the comment *"placeholder
so the score module will show up"*. 95 of the 103 corpus KotH maps declare a score limit. The studio reads
the element and the difference — a map with no `<score>` gets a null `ScoreConfig` and writes no element back
— but raises no finding over a point that scores into nothing (`PG6`).

`<score><king/></score>` appears in a handful of maps. It is a legacy marker that zeroes the default kill and
death scores; at proto ≥ 1.3.6 those already default to zero, so on every map the studio will ever read it is
a no-op that has to round-trip and nothing more.

**The trap is `required`.** A control point is a `Goal`, and `SimpleGoal.isRequired()` returns the attribute
when set and otherwise **`true` for any map at proto ≥ 1.4.0** — which is exactly the studio's supported
floor, so for the studio there is no other case. `GoalsVictoryCondition` ends the match the instant one
competitor completes *all* of its required goals, and a control point is complete for the team that owns it.
On a three-hill map with `required` left off, a team that takes all three at once wins on the spot; on a
one-hill map, the first capture ends the match.

**275 of 316 corpus KotH points write `required="false"` explicitly**, and that is the convention: a KotH
match is won on score, not on goals. The maps that leave it off divide cleanly into two groups. Some are
below proto 1.4.0, where the attribute did not exist and the legacy fallback — *require nothing when a score
module is loaded* — does the right thing. The rest mean it: `koth/wuhu_island` requires all ten of its
points, and `koth/skirmish` builds the same idea explicitly, as a fourth control point named "Judgement" with
`capture="everywhere"`, `capture-time="0s"` and a filter that only allows a player whose team already owns
the other three. Hold everything, win immediately.

One escape hatch exists and is worth knowing because it looks unrelated. `GoalMatchModule.addGoal` returns
early for a goal without the `stats` show option, and `show="false"` clears every show option including that
one — so a hidden point is not registered as a goal at all and cannot end the match however `required` reads.
That is why `koth/qboid`'s 28 `show="false"` cubes do not win on first touch.

Victory conditions are ordered `IMMEDIATE, TIME_LIMIT, BLITZ, SCORE, GOALS`. Only 7 of 103 corpus KotH maps
set a `<time>` limit; the score limit is the ending, and `750` is its modal value (44 of 95), followed by
`2500` (14) and `500` (10). With the modal build — three hills, one point per second each — 750 is roughly
eight minutes of a team holding two hills of three.

## 5. What the blocks do

`ControlPointBlockDisplay` renders two things onto the world, and understanding the first explains why hills
are round.

**The owner display** is flat: every block of the owner display region is set to the controlling team's dye
colour, or restored from the snapshot when the point goes neutral.

**The progress display is a pie.** A `SectorRegion` is built about the **centre of the progress display
region's bounds** — the bounding box of the enumerated blocks, not any centre the author wrote — covering
angles `0` to `(1 − progress) × 2π`, where the angle of a block is `atan2(dz, dx)` normalised to `[0, 2π)`
and `0` is the **+X** direction. Blocks inside that wedge take the **controlling** team's colour; blocks
outside it take the **capturing** team's. So the owner's wedge starts as the whole circle and shrinks toward
`0` as the capture runs, and the challenger's colour fills in behind it — growing from the +X axis and
sweeping through −Z, −X, +Z back to +X, which is counter-clockwise on a plan drawn with +X right and +Z
down. It closes the circle exactly as the capture completes.

Two things fall out of that. A pie drawn on a shape that is not centred on its own bounding box wipes about
the wrong point and reads wrong; a disc or a centred square is what the renderer assumes. And at progress
zero the sector is the whole circle, so **every block takes the controller's colour** — which is why a map
that passes the same region id to `progress` and `captured` still shows ownership correctly, and also why
that map's owner display region ends up empty (§2: the progress region wins the overlap).

Neutral is not a colour PGM picks. It restores the snapshot, so **the pad's neutral appearance is whatever
the author built**. In the corpus that is overwhelmingly white: white is the dominant dye on 263 of 359 pads,
and 290 of 359 are a single colour throughout.

## 6. What the corpus builds

163 map slugs across `CommunityMaps` and `PublicMaps` carry a control-point-family element — 91
`<control-points>`, 64 `<king>`, 9 `<payloads>`. No map mixes `<king>` with `<control-points>`; one
(`other/payload/skycastle_islands`) carries a payload alongside control points. 105 of them sit under
`koth/`. Every number below is over those 105 less `koth/qboid` and `koth/grand_qboid`, which are arcade
point-grids of 28 and 55 hidden cubes and skew every distribution they enter: **103 maps, 316 points**.

**How many, and where.** The maps split almost evenly between the two elements — 59 use `<king>`, 44 use
`<control-points>` — and the element says nothing about the structure. What does say something is the number
of teams that spawn:

| spawning teams | maps | points per map |
|---|---|---|
| two | 90 | one ×8, two ×14, **three ×61**, four ×3, five ×3, six ×1 |
| three | 3 | one ×1, four ×2 |
| four | 10 | four ×2, **five ×5**, six ×2, ten ×1 |

The positions are not stated in blocks by anything in the corpus; they are stated by the board's own symmetry.
Taking the two spawns of a two-team map as the frame, **60 of the 80 maps with a usable one carry a hill set
closed under the 180° rotation that swaps the spawns** — each point is either at the centre of that rotation
or has a partner that is its image — and 51 have exactly one point at the centre itself. Among three-point
maps that is 43 of 55 with a centre point and 39 of 55 that are exactly a centre plus a mirrored pair. 167 of
217 points are equidistant from both spawns, which is the same fact read per point rather than per map.

Four-team boards repeat it one order up. Every one of the seven with a usable frame is a ring of exactly four
around nought, one or two centre points, and five of the seven set the ring at **45° to the spawns** — on the
diagonals between neighbouring spawns rather than in front of any one of them. The two that do not are
`koth/toca` at 12° and `koth/yukoth` at 0°, and the second is the informative one: its four sit directly in
front of the spawns and each carries `initial-owner` naming that team, so every team begins owning its own
and the game is about taking somebody else's.

An off-centre point sits at **0.66 of the centre-to-spawn distance** for two teams (quartiles 0.52 and 0.88)
and 0.90 for four (quartiles 0.52 and 0.99). The median two-team board has its spawns 94 blocks apart, which
puts an off-centre point about 30 blocks from the middle — but the ratio is what the corpus states and the
block count is what falls out of it. `docs/gameplay/approaches.md` carries the author's ruling on all of this.

**Shape and size.** The capture region is a cuboid 169 times, a cylinder 95, a union 30. The cuboids are
square in plan 126 times of 153 measurable, with a **median footprint of 7 × 7** (quartiles 7 and 9) and a
**median height of 4** (quartiles 3 and 5). The cylinders have a **median radius of 5.5** (quartiles 4.5
and 7) and a median height of 4; `radius="4"` and `radius="7"` are the two commonest values. These are the
*capture* regions, which usually carry a block of margin, so they run a little larger than the pads below.

**The pad itself**, read out of the worlds as the colour-affected blocks inside each progress region:

| | measured over 359 pads |
|---|---|
| material | stained clay 246, wool 71, stained glass 42 |
| one layer thick | 282 |
| bounding box square | 329 |
| solid rectangle (≥ 95 % of its box) | 174, median side 5 |
| disc (≥ 80 % of its inscribed circle, not solid) | 120, median diameter 9 |
| dominant dye white | 263 |

The progress region is drawn *around* the pad rather than onto it: the median region is only **26 %**
colour-affected blocks, the rest being the floor, the border and the air above. That is the intended use —
the author boxes the structure loosely and the material filter picks the pad out of it.

**How the capture volume sits on the pad.** The capture region's lowest block is the pad's own top layer in
221 of 344 cases and one block above it in 56 more; the median capture column is 3 blocks tall; and the
capture footprint equals the pad's in 187 cases or is exactly two blocks wider — one block of margin all
round — in 93. The pad's main layer lies entirely under the capture column in more than three quarters of
cases. The capture region *is* the pad, extended upward into the air a player stands in.

**The owner display region** is present on 193 of 314 points, and 189 of those resolve to a box beside a
capture box that also resolves. It shares the capture footprint in plan 94 times, sitting over or through the
capture column — a banner strip, a roof, a beam.

**And it is a thing in the sky as often as a thing on the pad.** 62 of the 189 put it **entirely above** the
capture volume, a median 7 blocks over the capture top (quartiles 2 and 15, maximum 26); 114 overlap the
volume and 13 sit under it. The high ones are built to be read from across the board rather than from the
pad: `koth/beach_battles` captures at `y 9–12` and shows on a castle roof at `y 20–30`, and
`koth/industrial`'s `north-signal` is `<cuboid min="-4,35,-25" max="7,41,-16"/>`, a 12 × 6 × 9 slab standing
26 blocks over a capture volume at `y 5–9`. Every one of its 594 solid blocks is colour-affected — **578 white
stained clay** (`159:0`) and **16 white wool** (`35:0`) — so the whole slab is white while the point is
neutral and the holder's dye the moment it is taken. That is what an owner display region is *for*: the pad
says who is standing on it and the signal says who owns it, to a player who cannot see the pad.

**Two worked examples, checkable in game.**

`koth/supheriox`, hill "Top" — `<capture><cylinder base="-0.5,36,0.5" radius="6.6" height="4"/></capture>`,
with a `height="1"` cylinder on the same base as the progress region. At `y = 36` the world holds **136
blocks of white stained clay** (`159:0`) in a disc 13 across, centred on that base at `x = −0.5, z = 0.5`,
with one white stained-glass block at dead centre and a wood-slab ring around it (`126:5`, 84 blocks) that is
not colour-affected and therefore never repaints. π × 6.6² is 136.8; the pad is exactly the cylinder.

`koth/rafiki`, hill "Middle" — `capture="base-middle" progress="base-middle"`, and `base-middle` is
`<cuboid min="4,6,5" max="-3,9,-2"/>`. At `y = 6` the world holds a **7 × 7 slab of white wool** (`35:0`, 49
blocks) set flush into a spruce-plank floor, spanning `x −3…3, z −2…4`. The cuboid's blocks are `x −3…3`,
`y 6…8`, `z −2…4` — the pad layer plus two blocks of air, and a player standing on the pad is at `y = 7`, in
the middle of it. Its `captured` region is a separate `26 × 4 × 2` strip at `y 16–19` holding ten wool blocks
and five banners: the flag over the hill.

**A cuboid's two corners are unordered, and its upper bound is exclusive at integer coordinates.**
`CuboidRegion` normalises the pair through `Vector.getMinimum`/`getMaximum`, which is why `rafiki` can write
the larger corner as `min`. Given the normalised span `−3 … 4`, the blocks are `−3 … 3`: membership is tested
at block centres, and `3.5 ≤ 4 < 4.5`. A 7-wide pad is written as a span of 7, not of 6.

## 7. The conventional hill, in one block

Everything above, as the shape the corpus converges on. What is *correct* for a map as it is played is the
author's to say, and what has been said — square pads, two or three points on two teams and five on four, one
dead centre and the rest to the sides — is `docs/gameplay/approaches.md`.

```xml
<king>
  <hills required="false" capture-time="5s" points="1" time-multiplier="0"
         neutral-state="true" incremental="true" show-progress="true" permanent="false">
    <hill name="North"  capture="north-capture" progress="north-capture"/>
    <hill name="Middle" capture="mid-capture"   progress="mid-capture"/>
    <hill name="South"  capture="south-capture" progress="south-capture"/>
  </hills>
</king>
<score><limit>750</limit></score>
```

with each `*-capture` region a cuboid or cylinder — median 7 × 7, or radius ≈ 5.5 — standing on a one-layer
white stained-clay pad of the same footprint or a block narrower all round, its lowest block the pad layer and
three blocks of air above. "Middle" sits at the map's centre of symmetry and the other two are images of each
other under the rotation that swaps the spawns, about two-thirds of the way out from the centre to one.

## 8. What the studio does with one

**It reads one, stores it and writes it back.** `MapParser.ParseControlPoints` reads both spellings into
`Domain.ControlPoint`, carrying the element rather than resolving it; `ParseScore` reads `<score>` into
`ScoreConfig`, or `null` where the map declares none. `XmlWriter` re-emits each point under the element it
arrived in, `control_point` and `map_score` store it, and `Gamemodes.From` derives `cp`, `koth` and `tdm`
from what is there. Over both corpora, 286 maps carry one or the other; all 286 parse and survive the XML
round trip unchanged.

**Nothing states a knob the map did not.** Every optional attribute is `null` or `""` all the way down to
its nullable column, because PGM's default for it depends on the element — a hill keeps partial capture
progress and a control point discards it, from the same unwritten `incremental` — so materialising one
stores a different map.

## 9. And what it builds

An agent authors a capture board by adding one array to the intent it already posts to
`PUT /api/map/{slug}/intent`. There is no second endpoint and no new document:

```json
"controlPoints": [
  { "name": "North",  "anchor": { "x": 0, "y": 0, "z": -24 } },
  { "name": "Middle", "anchor": { "x": 0, "y": 0, "z": 0 } },
  { "name": "South",  "anchor": { "x": 0, "y": 0, "z": 24 } }
]
```

Four fields are knobs — `name`, `anchor`, `size` and `points`, plus `captureTime` — and everything else PGM
will read off the point is §7's convention, written the same on every board. `scoreLimit` sits beside them on
the intent and defaults to 750 on a board carrying a point that pays.

**The pad is cut into the ground rather than raised over it**, and that is the one way the stamper differs
from every other objective's. A destroyable and a core float — the gap is what a raid climbs and what a core's
lava falls through — but a hill is ground: `ControlPointStamper` replaces the terrain's top course over a
square footprint with white stained clay, and the capture volume is the three blocks of air starting at that
course. A pad that floated would be a hill nobody could stand on.

**The marker over it is the board's one changing signal.** Every goal the studio stamps carries a small
marker hanging five blocks over the map's build ceiling, out of reach by construction (`ST7`). A wool room's
is the wool's colour and a destroyable's or a core's is the owning team's, fixed for the match, because those
goals belong to somebody from the start. A point does not, so its marker is a 3-block wool asterisk laid in
**white** and its box is emitted as the point's `owner-display-region` — PGM paints it flat in the holder's
dye on capture and restores the white when the point goes neutral. It is the owner region and not the
progress one because the progress display is a pie about the centre of its own bounds (§5), and a marker
sharing that region would put the centre in the sky and wipe the pad about the wrong point. The two regions
are disjoint, so the `InverseFilter(progress)` PGM applies over the owner region (§2) takes nothing from it.

A point therefore exports with three regions and the score beside them:

```xml
<region id="middle-capture" type="cuboid" …/>   <!-- the pad course + the air a player stands in -->
<region id="middle-pad"     type="cuboid" …/>   <!-- the pad course alone: the progress pie -->
<region id="middle-marker"  type="cuboid" …/>   <!-- the 3×3×3 over the build ceiling: the owner display -->
```

Two properties the flat pad still has to hold. It is **level** across its whole footprint, because the
progress pie is drawn about the centre of the blocks PGM finds and a pad following a slope draws that pie
across several courses; where the ground falls away it skirts down to meet it, bounded, so it reads as a
plinth cut into a hillside rather than a sheet hanging off one. And the volume over it is **cleared**, so a
pad laid under a tree is still somewhere a player can stand.

The two emitted regions are the stamper's own boxes (OB8): the capture region is the volume, the progress
display region is the pad course alone — one block thick, every block of it colour-affected. No owner-display
region is written, because at zero progress PGM paints the whole progress region in the controller's colour
(§5) and a second region over the same blocks would be subtracted away to nothing.

**What is not built** is on the board in `BACKLOG.md` under *"The hill: a goal owned by standing on it"*.
Each sentence becomes false when its task ships:

- **`PG6`** — a scoring point with no `<score>` element scores nothing; nothing says so for an **imported**
  map. A studio-authored board always writes one.
- **`PG7`** — an **imported** pad built in a material outside the colour-affected set never changes colour,
  and nothing says so. The stamper only ever lays stained clay.
- **`PG8`** — `required` left off ends the match on first capture, and an imported map that leaves it off is
  not flagged. Every point the studio writes states `required="false"`.
- **`TC7`** — the configure tool has no step for placing one; the API is the way in.
- **`TC8`** — the plan model has no capture-point placement, so a plan-compiled intent states each point
  rather than one side. An intent that carries a symmetry does fan them.

What a board should *be* is not on that list. It is the author's rather than the corpus's, it has been ruled,
and it is written down in `docs/gameplay/approaches.md`: square pads; two or three points on two teams and
five on four; one point at the centre of symmetry and the rest to the sides, about two-thirds of the way out
to a spawn.
