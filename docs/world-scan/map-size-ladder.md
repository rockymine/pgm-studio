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

## What the composer builds against it

Twelve boards a row, `rot_180` and `mirror_z`, cell 5, measured the same way: the fanned board rasterised to
blocks and put through the same local thickness. Land is terrain only — a build zone, a water lane and an
enclosed void are not ground, which is what makes the number comparable with an island count. The corpus
column beside it is the median over every map within four players of the row, so the two are read at the same
count rather than through a group boundary.

| players | band | land bbox | land/team | budget | built ÷ budget | coverage | corpus land/team | built ÷ corpus |
|---|---|---|---|---|---|---|---|---|
| 6 | nano | 102×150 | 2450 | 2250 | 1.09× | 32% | 1590 | 1.54× |
| 10 | nano | 102×150 | 2450 | 2250 | 1.09× | 32% | 2359 | 1.04× |
| 12 | nano | 102×150 | 2450 | 2250 | 1.09× | 32% | 2810 | 0.87× |
| 16 | micro | 132×180 | 4050 | 4025 | 1.01× | 35% | 3628 | 1.12× |
| 20 | micro | 132×180 | 4050 | 4025 | 1.01× | 35% | 4784 | 0.85× |
| 24 | milli | 170×215 | 6875 | 7075 | 0.97× | 37% | 6105 | 1.13× |
| 32 | centi | 200×230 | 7900 | 8730 | 0.90× | 34% | 8422 | 0.94× |
| 40 | centi | 200×230 | 7900 | 8730 | 0.90× | 34% | 11676 | 0.68× |
| 48 | hecto | 275×335 | 16200 | 20190 | 0.80× | 35% | 13015 | 1.24× |

The budget is a contract now: what a unit built sits between 0.80 and 1.09 of what its band bought, and an
attempt outside 0.70–1.30 is resampled rather than shipped. Against the corpus the ratio runs 0.68 to 1.54
and sits within a sixth of parity at every band's own centre, which is what a band means — a nano board is a
little large for six a side and a little small for twelve, because it is one map serving both. Coverage
lands at 32–37% against the measured 32–41%, so the board follows the land rather than being sized beside it.

Width is the reading that still has an outlier. The modal local thickness of a composed board is 10 blocks at
nano, 16 at milli and centi and 20 at hecto, against a corpus of 10 · 16 · 16 · 18 — but **30 at micro**,
where the corpus sits at 14.

| band | source | <6 | <8 | <10 | <12 | <14 | <16 | <20 | <24 | modal width |
|---|---|---|---|---|---|---|---|---|---|---|
| nano | corpus | 1.3 | 4.0 | 9.0 | 26.4 | 39.9 | 57.3 | 77.6 | 85.8 | 10 |
| | composer | 0.0 | 0.4 | 0.4 | 33.6 | 63.8 | 65.1 | 72.5 | 85.7 | 10 |
| micro | corpus | 1.4 | 3.0 | 5.2 | 14.6 | 25.8 | 40.4 | 61.3 | 75.0 | 14 |
| | composer | 0.0 | 0.3 | 0.3 | 9.3 | 12.0 | 13.1 | 45.1 | 57.6 | 30 |
| milli | corpus | 1.0 | 2.1 | 3.6 | 7.6 | 17.2 | 27.6 | 49.1 | 61.0 | 16 |
| | composer | 0.0 | 0.2 | 0.2 | 0.5 | 1.0 | 1.3 | 42.4 | 52.7 | 16 |
| centi | corpus | 0.5 | 1.5 | 2.9 | 6.9 | 11.9 | 19.1 | 39.0 | 53.0 | 16 |
| | composer | 0.0 | 0.1 | 0.1 | 0.4 | 0.8 | 1.1 | 43.5 | 53.4 | 16 |
| hecto | corpus | 1.7 | 3.8 | 5.4 | 7.1 | 15.3 | 16.9 | 30.4 | 39.6 | 18 |
| | composer | 0.0 | 0.1 | 0.1 | 0.2 | 0.4 | 0.6 | 0.9 | 38.6 | 20 |

Two differences survive, and they are the same one read twice. A composed board carries almost nothing under
10 blocks where a real one carries 3–9% — the emitters build to the band's corridor and never under it, while
an author lays a ledge or a bridge approach. And a composed board is **flat-tailed**: its ground sits on a
few discrete widths rather than spreading, because everything is emitted at one corridor or at a multiple of
it. Micro is where that costs most: its corridor is three cells on the cell-5 grid, so the frontline's spine
and the hub's own front wall are three cells each and dock flush into a six-cell slab — thirty blocks, the
band's mode.

## What this says about the cell

Every width the composer builds to is now the band's own, stated in **blocks** and divided by the cell where
the grid is laid: the map's corridor is 12 · 14 · 16 · 16 · 22 and the wool approach's is 10 · 12 · 14 · 14 ·
18. What the grid decides is which of those a board can actually express. At cell 5 the reachable widths are
5, 10, 15, 20; at cell 4 they are 4, 8, 12, 16, 20.

The measured ladder is 8 · 10 · 14 · 16 by mode, or 8 · 12 · 14 · 16 by the quartile a working lane sits at.
Cell 4 reaches both ends of it — the 8-block floor the whole corpus builds to, and the 16 that milli and
centi sit on — and cell 5 reaches neither: its 2-cell floor is 10, two blocks over the floor authors use, and
its next rung is 15 where the corpus sits at 14 and 16. So cell 5 rounds micro's 14 up to 15 and centi's 16
down to 15, which is why a composed micro board's ground stacks into six-cell slabs while nano's, whose 12
rounds to a clean 2, comes out at the corpus's own mode.

The gap floors run on the same arithmetic. A bay beside a goal is 16 blocks (`docs/gameplay/approaches.md`),
which is four cells at cell 4 and 3.2 at cell 5 — so at cell 5 the floor is either broken or rounded up to 20,
and rounding a clearance up by a quarter is what refused every 8-player board when the seat gap was given one
(`G264`).

This is why a width stated in cells cannot carry a ladder at all: moving the cell from 5 to 4 would shrink
every element by a fifth rather than offering the composer a finer rung to choose from. A ladder needs the
widths stated in blocks and divided by the cell, which is what leaves the cell free to be a drawing scale
rather than a design decision — and what makes cell 4 worth having.
