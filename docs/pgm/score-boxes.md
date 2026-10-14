# Score boxes — a region that pays the team of whoever enters it

A score box is a region inside the `<score>` module: a player who walks into it, and passes its filter, pays
its points to their team. Maps built around one are played to carry yourself into the enemy's box, and the
box usually sends its scorer home through a portal on the same region, so a score is a run and not a camp.
This document is the contract, read from `tc.oc.pgm.score` in `PGMDev/PGM`, and what the corpus builds.

## 1. The box

`ScoreModule` reads every `<box>` of every `<score>` element. A box names its region by a `region` attribute
or a `<region>` child, and pays `points` — also spelled `value` — which is **0** where unstated on every proto
the studio reads, since `DEFAULT_SCORES_TO_ZERO`; a box paying only for items it redeems states none. A
`filter` decides who may score in it, and allows everyone where unstated; `silent` keeps the score out of
chat. A box may instead pay for items a player carries in, listed under `<redeemables>`; the studio does not
read those, so a box that states them refuses the map (`supported-maps.md`).

PGM tags a map with a box `scorebox`, but as an auxiliary tag carrying no gamemode: what PGM derives from the
module is `tdm`, and a score box map states `<gamemode>scorebox</gamemode>` itself where it wants to be listed
as one. Of the 78 corpus maps with a box, 26 state it.

## 2. The way home

A box is almost always paired with a portal on its own region. 82 of the 209 corpus boxes have one, and 69 of
those land the scorer in a `destination` region — their team's spawn — while 54 filter the portal to the same
team as the box. The pair is what makes a score a round trip: entering pays, and the same step sends the
scorer back to start again. The portal is the `<portals>` module's (`portals.md`).

## 3. What the studio reads and writes

`MapParser.ParseScore` reads each box into `ScoreConfig.Boxes` (`Domain/MapModel.cs`, `ScoreBox`): its region,
named or registered inline under a synthetic id, its points under either spelling, its filter and whether it
is silent. The writer emits `points`, and a region or filter the map wrote inline goes back inline.

In the map document a box is `score.boxes[]` (`Serializer.EncodeScoreBox`), and in the database one row of
that JSON in `score_box` (migration `M0072`), read back in document order.

Swept over both corpora, the 52 parseable maps carrying a box — 167 boxes — write every box back unchanged
through the XML and the JSON codec. The 23 that redeem items are refused.

| what a box states | boxes |
|---|---|
| `points="1"` | 122 of 209 |
| no points | 24 |
| a filter | 161 |
| `silent` | 85 |
| redeemables | 38 |

## 4. Authoring a box

`MapIntent.ScoreBoxes` states a board's boxes: each a `team`, the inclusive `box` of blocks it covers, the
`points` it pays — 1 where unstated, as 122 of 209 corpus boxes pay — and whether it sends its scorer `home`,
true where unstated. `ScoreGenerator` writes each into the `<score>` it already owns as a cuboid region
filtered to its team, a team id being itself the filter matching its members, and a box sending its scorer
home gains a portal on the same region, filtered the same way, landing on the team's spawn point at the
spawn's yaw. That is the corpus pair of §2: the destination is the spawn region, so the scorer lands where
the team spawns.

A box belongs to the team it scores for, so under a symmetry it fans by team the way a core does: each other
team gains its image of the box, with its own way home, and a team already holding a box keeps its own. The
board states `<gamemode>scorebox</gamemode>`, which PGM would not derive, and its objective reads "Reach your
score box!". No score limit is written unless the author states `ScoreLimit`: the corpus states none on 33 of
its 78 box maps and ends the rest anywhere from 1 to 750. A box board plays for control, so nothing is built
on it unless the author says otherwise (`new-map-authoring.md` §5c).

A box is entered on foot, so a plan-built board's world build clears every box to air after the dressing pass
and every stamp, fanned by team across the board's symmetry first, since boxes are stated on the intent alone;
the map then names every box the world cleared (`WorldBuilder.ClearScoreBoxes`).

A box is a goal the reads walk to: `NavPoints` seats each box on its region as kind `scorebox`, owned by nobody,
and the export's `EX1` refuses a board whose box no spawn can walk into (`world-scan/read-backs.md`). On a map
read from a world, the Configure tool's Score phase draws the boxes (`tools/configure.md`).
