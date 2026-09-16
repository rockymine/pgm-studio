# The size ladder — what a built CTW map measures, per team size

A composed board is sized from one number, the intended players per team, and every downstream decision
spends that number: how much land a team gets, how wide the ground it walks on is, how big the board the
land sits in. The coupling `G8` states is calibrated against the twelve authored seed plans
(`docs/generator/seed-stats.md`): twelve points, and five of the twelve counts began as proposals read off
the seed's own land before the author settled them.

The corpus answers the same question from the other side. Every CTW map in
`OvercastCommunity/CommunityMaps` and `OvercastCommunity/PublicMaps` is a world somebody built for a stated
team size, so "how much ground does a 24-a-side map have" has 65 measured answers rather than one
interpolated one.

Maps are not built for a single count. They work across a band, and the bands have names: **nano** 6–13
players per team, **micro** 14–21, **milli** 22–31, **centi** 32–47, **hecto** 48 and up. A budget is owed
per band, not per count.

## What was read, and how

363 CTW maps were scanned with `PgmStudio.RoundTrip --scan-out-all`; 359 finished. Four did not, and each
refusal is the studio's own: `allure` and `crown` are 1.13-or-later worlds whose palette format the Anvil
reader does not decode, `kytriak_te` is proto 1.3.0, below the supported floor, and `cargo` carries a
non-finite coordinate — which it hits after the world is read, so its islands are measured here and it is the
360th map. The 359 that finished were imported into MariaDB, which is where the feature parquets and the
surface layer live.

**Land** is the island block count out of `islands.json` — the studio's own cleaned-base, height-aware,
stair-aware reading of what ground a world has, summed over every island. Rasterising each island's polygon
and counting cells reproduces that count to a median ratio of 1.000, so the polygon and the block count are
the same shape read two ways. Two maps are exceptions where islands overlap in plan and the hole subtraction
eats land that is there (`subzero` 0.66, `villa_ii` 0.76); neither is near a group median.

The comparison that matters is against a plan's **piece area**, because that is what the composer's budget
is denominated in. Over the sixteen traced plans in `tools/seeds/traced/` the fanned piece area divided by
the same map's island block count runs 0.80 to 1.25 with a median of 0.98: a plan's rectangles over-cover
where the ground is ragged and under-cover where it spills past them, and the two wash out. **Island area is
piece area**, near enough to budget with.

**Width** is local thickness: the diameter of the largest inscribed disk that contains a cell. It is the
measure that answers "how wide is the ground *here*" without needing a skeleton or a lane decomposition — a
cell in the middle of a plaza reads the plaza's width, a cell on a two-wide bridge reads two. Every land cell
carries one, so a map has a width *distribution* rather than a width, and a group's distribution is the mean
of its maps' distributions, each normalised to its own land so a big map does not swamp a small one.

**Team size** is the largest `max` on a `<teams><team>`. It is a cap rather than a played count, and a
handful of maps set it as an invitation rather than a measurement — `down_side_up` declares 50 a side over
1,377 blocks² of land per team, which is nano ground under a hecto label. Medians absorb them; means would
not, and nothing here is a mean.

## Land does not curve — it is about 250 blocks² a player, at every size

The strongest result is the one the seed sample could not see. Over 331 maps with both a team count and a
player cap, land per team fits

    land/team = 176 × players^1.12        (r = 0.83 on log–log)

and an exponent of 1 is exactly "constant land per player". The 0.12 above it is a rise of a quarter across
the whole range: 213 blocks² a player at five, 268 at thirty-two. The per-group medians say the same thing
flatly.

| group | players/team | n | land/team p25 / **median** / p75 | land/player | wools | islands |
|---|---|---|---|---|---|---|
| *(below nano)* | 4–5 | 12 | 995 / **1248** / 1640 | 250 | 2 | 8 |
| nano | 6–13 | 77 | 1590 / **2250** / 2780 | 225 | 2 | 5 |
| micro | 14–21 | 90 | 3308 / **4026** / 5331 | 242 | 4 | 7 |
| milli | 22–31 | 65 | 5206 / **7076** / 9165 | 270 | 4 | 7 |
| centi | 32–47 | 84 | 6966 / **8732** / 11356 | 256 | 4 | 6 |
| hecto | 48+ | 3 | 10784 / **20191** / 28539 | 404 | 4 | 3 |

The rows cover the 331 maps that declare a cap and carry two or four teams; the twenty-odd with three, five,
six or eight teams, or with no cap at all, are outside every one of them. Land per team is *land the team's
half of the board holds*: the map's island total divided by its team count, so a four-team map contributes a
quarter of its ground. The wool column is the map's total wool count, not per team.

Three maps is not a measurement, so the **hecto row is an extrapolation with three witnesses** rather than a
budget. `dragons_hearth` (50/team, 258×736, 36,887 land a team) and `fta_royal_garden_ctw` (50/team, 400×306,
20,191) are real hecto maps; `down_side_up` is the mislabel above, and it is what pulls the row's p25 down to
10,784. Extending the law rather than the row gives about 300 blocks² a player at fifty.

The interquartile spread is wide — a factor of 1.6 to 1.8 within every group — which is the honest reading
of "maps are flexible". A budget is the middle of a band authors treat as a band.

## Ground runs 8 blocks wide at the bottom and 16 at the top, and almost never under 6

Width does rise with size, and unlike land it rises slowly: the tenth percentile of a map's local thickness
fits `5.5 × players^0.265`, which is 8 blocks at five players and 13 at thirty-two. What the pooled
distribution adds is where the ground actually *sits*, which is the number a minimum-element rule wants.

Width is read off every map that declares a cap, whatever its team count, because how wide the ground is
does not depend on how the teams are cut: 13 below nano, 80 nano, 93 micro, 65 milli, 84 centi. Share of a
group's ground at each width, in blocks:

| group | 4 | 6 | 8 | 10 | 12 | 14 | 16 | 18 | 20 | 24 | modal |
|---|---|---|---|---|---|---|---|---|---|---|---|
| *(below nano)* | 1.8 | 6.6 | **25.7** | 20.3 | 6.2 | 7.2 | 12.7 | 1.1 | 4.1 | 5.0 | 8 |
| nano | 0.8 | 2.6 | 5.0 | **17.5** | 13.5 | 17.4 | 13.5 | 6.8 | 5.1 | 2.4 | 10 |
| micro | 0.9 | 1.7 | 2.1 | 9.4 | 11.2 | **14.6** | 13.0 | 7.8 | 8.5 | 3.6 | 14 |
| milli | 0.8 | 1.0 | 1.5 | 4.0 | 9.6 | 10.5 | **12.8** | 8.6 | 6.2 | 6.3 | 16 |
| centi | 0.4 | 1.0 | 1.4 | 3.9 | 5.0 | 7.3 | **12.0** | 7.9 | 7.7 | 5.7 | 16 |

Read down a column instead and the floor appears. Ground narrower than **6 blocks** is 0.5–1.4% of a map at
every size above the sub-nano sample, and narrower than **8 blocks** is 1.5–4.0%. Those are ledges, bridge
approaches and the ragged edges of islands, not places a map is played. **Eight blocks is the floor authors
build to**, and it holds at every scale; what changes with size is not the floor but where the mass sits.

| share of ground narrower than | 6 | 8 | 10 | 12 | 14 | 16 | 20 |
|---|---|---|---|---|---|---|---|
| *(below nano)* | 3.1 | 9.7 | 35.4 | 55.7 | 61.9 | 69.1 | 82.9 |
| nano | 1.3 | 4.0 | 9.0 | 26.4 | 39.9 | 57.3 | 77.6 |
| micro | 1.4 | 3.0 | 5.2 | 14.6 | 25.8 | 40.4 | 61.3 |
| milli | 1.0 | 2.1 | 3.6 | 7.6 | 17.2 | 27.6 | 49.1 |
| centi | 0.5 | 1.5 | 2.9 | 6.9 | 11.9 | 19.1 | 39.0 |

So the element-width ladder the corpus states is **8 · 10 · 14 · 16 · 16** by mode, or, taking the quartile
that a working lane sits at rather than the mode, **8 · 12 · 14 · 16 · 17**. Either way it is a ladder of
four steps over a range of six in player count, not the flat minimum a single constant gives.

The five-player figure is worth stating on its own because it is where the composer has the least room:
a map built for five a side puts a quarter of its ground at exactly 8 blocks and 90% of it at 8 or more.

## The board the land sits in, and how much of it is land

Coverage is stable — a CTW map is a third of its bounding box, whatever size it is — so a board size follows
from a land budget and does not need measuring separately. It is measured anyway, because the composer clamps
board extents directly and the clamps are checkable against this.

| group | n (2-team) | short side p25 / **med** / p75 | long side p25 / **med** / p75 | aspect | coverage % |
|---|---|---|---|---|---|
| *(below nano)* | 11 | 50 / **68** / 82 | 95 / **102** / 128 | 2.1 | 32 |
| nano | 62 | 62 / **78** / 96 | 132 / **166** / 183 | 1.9 | 35 |
| micro | 79 | 80 / **106** / 128 | 184 / **210** / 242 | 2.0 | 41 |
| milli | 63 | 102 / **138** / 183 | 244 / **288** / 330 | 2.0 | 36 |
| centi | 84 | 124 / **161** / 198 | 261 / **304** / 369 | 2.0 | 38 |
| hecto | 3 | 166 / **258** / 282 | 252 / **400** / 568 | 1.4 | 36 |

Aspect is 2.0 at the median in every group, and the quartiles sit at 1.4–1.5 and 2.4–2.6. Four-team maps are
square — aspect 1.00 at all three quartiles over 30 maps — with a side of 165 / **222** / 279 and coverage
21 / 29 / 38%. Thirty of the 360 measured maps are four-team, and they are small maps: 15 nano, 11 micro, 2 milli,
none above.

## What the composer emits against it

Twelve boards a row, `rot_180` and `mirror_z`, cell 5, measured the same way: the fanned ASCII board
rasterised to blocks and put through the same local thickness. Land is terrain only — a build zone, a water
lane and an enclosed void are not ground, which is what makes the number comparable with an island count.
The corpus column beside it is the median over every map within four players of the row, so the two are read
at the same count rather than through a group boundary.

| players | composed board | land/team | budget/team | built ÷ budget | corpus land/team | built ÷ corpus | corpus board |
|---|---|---|---|---|---|---|---|
| 5 | 55×95 | 875 | 325 | 2.7× | 1486 | 0.59× | 64×138 |
| 6 | 55×95 | 875 | 426 | 2.1× | 1590 | 0.55× | 72×140 |
| 8 | 62×100 | 1000 | 664 | 1.5× | 2127 | 0.47× | 77×156 |
| 10 | 82×115 | 1250 | 950 | 1.3× | 2359 | 0.53× | 80×167 |
| 12 | 82×115 | 1250 | 1260 | 0.99× | 2810 | 0.44× | 87×177 |
| 14 | 75×130 | 1325 | 1610 | 0.82× | 3245 | 0.41× | 97×196 |
| 16 | 75×130 | 1450 | 2480 | 0.58× | 3621 | 0.40× | 105×203 |
| 20 | 85×150 | 2025 | 3500 | 0.58× | 4648 | 0.44× | 112×235 |
| 24 | 85×145 | 2050 | 4280 | 0.48× | 6105 | 0.34× | 127×258 |
| 32 | 85×145 | 2050 | 5920 | 0.35× | 8422 | 0.24× | 156×301 |
| 40 | 85×145 | 2050 | 5920 | 0.35× | 11676 | 0.18× | 176×408 |

Three readings. The **budget is not a contract in either direction**: overspent 2.7-fold at five players,
under-spent to a third from twenty-four up, and crossing the land actually built at twelve — the one count
where the two agree. The **board stops growing at twenty-four**, where the corpus is only halfway up its own
range, and starts growing only at ten. And land per team rises 2.3× across a range over which the corpus
rises 7.9×, so a composed board is 0.59× a real one at five players and 0.18× at forty.

Coverage is the one thing that lands. A composed board is 28–33% land against the corpus's 32–41%, so the
board extent follows the land budget at about the right ratio: the boards are too small because the land is,
not because the emptiness is wrong.

Width is the third reading and the flattest. The tenth percentile of a composed board's local thickness is
**exactly 10 blocks at every player count from five to forty** — two cells at cell 5, and nothing moves it.
Below that there is effectively nothing: 0.5% of a composed board is under 10 blocks, against 2.9% of a centi
map and 9% of a nano one. Above it the composed ground sits on a few discrete steps rather than a spread.

| group | source | <6 | <8 | <10 | <12 | <14 | <16 | <20 | modal width |
|---|---|---|---|---|---|---|---|---|---|
| *(below nano)* | corpus | 3.1 | 9.7 | 35.4 | 55.7 | 61.9 | 69.1 | 82.9 | 8 |
| | composer | 0.0 | 0.6 | 0.6 | 38.5 | 48.0 | 50.5 | 52.3 | 20 |
| nano | corpus | 1.3 | 4.0 | 9.0 | 26.4 | 39.9 | 57.3 | 77.6 | 10 |
| | composer | 0.0 | 0.6 | 0.6 | 26.2 | 32.1 | 34.7 | 36.8 | 20 |
| micro | corpus | 1.4 | 3.0 | 5.2 | 14.6 | 25.8 | 40.4 | 61.3 | 14 |
| | composer | 0.0 | 0.6 | 0.6 | 25.7 | 50.7 | 52.8 | 59.3 | 20 |
| milli | corpus | 1.0 | 2.1 | 3.6 | 7.6 | 17.2 | 27.6 | 49.1 | 16 |
| | composer | 0.0 | 0.5 | 0.5 | 19.8 | 43.2 | 45.7 | 62.4 | 12 |
| centi | corpus | 0.5 | 1.5 | 2.9 | 6.9 | 11.9 | 19.1 | 39.0 | 16 |
| | composer | 0.0 | 0.5 | 0.5 | 19.8 | 43.2 | 45.7 | 62.4 | 12 |

The two failures are opposite and they are both quantisation. At five players the composed ground is too
**wide** — its mode is 20 where the corpus's is 8, because a hub is four cells across and a cell is five
blocks. At thirty-two it is too **narrow** — a mode of 12 against the corpus's 16, and 43% of it under 14
blocks against the corpus's 12%. One grid emits one ladder of widths, and the player count does not reach it.

## What this says about the cell

Every width the composer states is a cell count — the wool lane is `WoolLaneCells = 2`, the map lane is 2 or
3, the frontline face is at least `FaceMinCells = 4` — so the grid scale, not the player count, is what sets
which widths exist. At cell 5 they are 5, 10, 15, 20; at cell 4 they are 4, 8, 12, 16, 20.

The measured ladder is 8 · 10 · 14 · 16 by mode, or 8 · 12 · 14 · 16 by the quartile a working lane sits at.
Cell 4 reaches both ends of it — the 8-block floor the whole corpus builds to, and the 16 that milli and
centi sit on — and cell 5 reaches neither: its 2-cell floor is 10, two blocks over the floor authors use, and
its next rung is 15 where the corpus sits at 14 and 16. Cell 5's one exact hit is nano's mode of 10, which is
also the one width a composed board has at every player count.

The gap floors run on the same arithmetic. A bay beside a goal is 16 blocks (`docs/gameplay/approaches.md`),
which is four cells at cell 4 and 3.2 at cell 5 — so at cell 5 the floor is either broken or rounded up to 20,
and rounding a clearance up by a quarter is what refused every 8-player board when the seat gap was given one
(`G264`).

This is why a width stated in cells cannot carry a ladder at all: moving the cell from 5 to 4 shrinks every
element by a fifth rather than giving the composer a finer rung to choose from. A ladder needs the widths
stated in **blocks** and divided by the cell where the grid is laid, and then the cell is free to be a
drawing scale rather than a design decision.
