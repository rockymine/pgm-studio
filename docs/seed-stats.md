# Plan-Seed Rule Statistics (blocks; cell=5)

All coords in blocks (cell coords × 5). Board centre = symmetry centre (0,0). Land interface = two piece rects sharing any positive-length border. Fanned bbox applies the symmetry orbit; teams = orbit order.

## Per-seed summary

| seed | sym | T | authored WxL | fanned WxL | base | surf-range | pcs | min-dim 5/10/15/20+ |  largest | zn | spn raise/facing/end | wools(raise) | iron | walls | appr | Δ≥4 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| base-2island | rot_180 | 2 | 12 | 30x60 | 30x130 | 9 | 9-13 | 4 | 0/4/0/0 | bar-w 10x45 | 1 | +0/front/back | +0 | - | 0 | 1 | 0 |
| base-2wool | rot_180 | 2 | 12 | 60x60 | 90x130 | 9 | 9-13 | 6 | 0/6/0/0 | bar-w 10x45 | 2 | +0/front/back | +0,+4 | - | 0 | 1 | 0 |
| base-4team | rot_90 | 4 | 12 | 30x60 | 130x130 | 9 | 9-13 | 4 | 0/4/0/0 | bar-w 10x45 | 1 | +0/front/back | +0 | - | 0 | 1 | 0 |
| four-team-towers-big | rot_90 | 4 | 12 | 80x85 | 180x180 | 9 | 9-17 | 15 | 4/5/6/0 | piece-6 30x15 | 5 | +4/front/back | +8,+0 | ahead(5,-5) | 2 | 3 | 0 |
| four-team-wool-two-sided | rot_90 | 4 | 12 | 60x60 | 130x130 | 9 | 9-15 | 9 | 0/9/0/0 | piece-3 10x20 | 6 | +2/left/front | +6 | behind(-10,0) | 2 | 3 | 0 |
| isolated-spawn-approaches | rot_180 | 2 | 12 | 75x65 | 90x120 | 9 | 9-15 | 9 | 3/6/0/0 | piece-5 35x10 | 6 | +4/right/back | +6 | - | 0 | 3 | 0 |
| isolated-spawn | rot_180 | 2 | 12 | 80x45 | 80x110 | 9 | 9-13 | 9 | 0/9/0/0 | lane-4 10x30 | 3 | +4/front/front | +4,+4 | beside(2,0) | 2 | 1 | 0 |
| mirror-big-board | mirror_x | 2 | 12 | 130x130 | 280x130 | 9 | 9-19 | 39 | 19/11/9/0 | piece-6 50x15 | 7 | +10/right/back | +10,+4 | beside(10,5) | 2 | 1 | 8 |
| odd-facing-three-wool | rot_180 | 2 | 12 | 85x95 | 180x100 | 9 | 9-17 | 21 | 6/15/0/0 | piece-2 10x30 | 5 | +8/left/front | +0,+8,+2 | beside(-10,0) | 3 | 1 | 3 |
| rotate-wide-frontline | rot_180 | 2 | 12 | 95x100 | 190x100 | 9 | 9-17 | 34 | 22/12/0/0 | piece-7 10x60 | 7 | +8/right/back | +8,+8 | - | 0 | 1 | 6 |

## Per-seed land-interface Δ histogram (|delta|: 0/2/4/6/8+) & step-width pieces

| seed | ifaces | 0 | 2 | 4 | 6 | 8+ | Δ≥4 | 5-wide pcs |
|---|---|---|---|---|---|---|---|---|
| base-2island | 2 | 2 | 0 | 0 | 0 | 0 | 0 | 0 |
| base-2wool | 3 | 3 | 0 | 0 | 0 | 0 | 0 | 0 |
| base-4team | 2 | 2 | 0 | 0 | 0 | 0 | 0 | 0 |
| four-team-towers-big | 12 | 5 | 7 | 0 | 0 | 0 | 0 | 4 |
| four-team-wool-two-sided | 7 | 0 | 7 | 0 | 0 | 0 | 0 | 0 |
| isolated-spawn-approaches | 5 | 2 | 3 | 0 | 0 | 0 | 0 | 3 |
| isolated-spawn | 6 | 0 | 6 | 0 | 0 | 0 | 0 | 0 |
| mirror-big-board | 49 | 16 | 25 | 4 | 2 | 2 | 8 | 19 |
| odd-facing-three-wool | 17 | 10 | 4 | 2 | 0 | 1 | 3 | 6 |
| rotate-wide-frontline | 34 | 7 | 21 | 4 | 2 | 0 | 6 | 22 |

## Per-seed zones (min-dim buckets 5/10/15+) & approaches

| seed | zones | 5 | 10 | 15+ | largest area | approaches | frontmost pieces |
|---|---|---|---|---|---|---|---|
| base-2island | 1 | 0 | 0 | 1 | 1500 | 1 | stone |
| base-2wool | 2 | 0 | 1 | 1 | 1500 | 1 | stone |
| base-4team | 1 | 0 | 0 | 1 | 750 | 1 | stone |
| four-team-towers-big | 5 | 1 | 2 | 2 | 375 | 3 | piece |
| four-team-wool-two-sided | 6 | 2 | 4 | 0 | 100 | 3 | piece |
| isolated-spawn-approaches | 6 | 2 | 4 | 0 | 150 | 3 | piece |
| isolated-spawn | 3 | 0 | 2 | 1 | 1200 | 1 | lane |
| mirror-big-board | 7 | 0 | 3 | 4 | 1750 | 1 | piece, piece-2 |
| odd-facing-three-wool | 5 | 0 | 4 | 1 | 750 | 1 | piece-17 |
| rotate-wide-frontline | 7 | 0 | 6 | 1 | 2400 | 1 | piece, piece-6 |

## Per-seed spawn / wool / iron detail

### base-2island
- spawn `bar-e` @(10,50) surf=9 raise=+0 facing=front dist-to-piece-centre=10.0 | end=back (mk→centre 51.0 vs piece-centre→centre 41.2; piece near/far 25.5/57.0)
- wool `bar-w` @(-10,60) surf=9 raise=+0
- wool-spawn dist: 22.4
- ⚠️ FLAGS: G3 2T width=30 (∉40..60)

### base-2wool
- spawn `bar-e` @(10,50) surf=9 raise=+0 facing=front dist-to-piece-centre=10.0 | end=back (mk→centre 51.0 vs piece-centre→centre 41.2; piece near/far 25.5/57.0)
- wool `bar-w` @(-10,60) surf=9 raise=+0
- wool `wl2-b` @(40,30) surf=13 raise=+4
- wool-wool sep: 58.3
- wool-spawn dist: 22.4, 36.1
- ⚠️ FLAGS: G3 2T width=90 (∉40..60)

### base-4team
- spawn `bar-e` @(10,50) surf=9 raise=+0 facing=front dist-to-piece-centre=10.0 | end=back (mk→centre 51.0 vs piece-centre→centre 41.2; piece near/far 25.5/57.0)
- wool `bar-w` @(-10,60) surf=9 raise=+0
- wool-spawn dist: 22.4
- ✓ no rule-number flags

### four-team-towers-big
- spawn `spawn` @(-67,72) surf=13 raise=+4 facing=front dist-to-piece-centre=5.0 | end=back (mk→centre 99.1 vs piece-centre→centre 95.7; piece near/far 85.1/106.3)
- wool `wool` @(-87,17) surf=17 raise=+8
- wool `wool-2` @(-17,82) surf=9 raise=+0
- wool-wool sep: 95.5
- wool-spawn dist: 51.0, 58.5
- iron `spawn` offset=(5,-5) ahead=5.0 lateral=5.0 → **ahead**
- wall `piece-4`↔`piece-10` kind=land border=15 Δsurf=-2
- wall `piece-5`↔`piece-7` kind=land border=15 Δsurf=+2
- ⚠️ FLAGS: WL5 wool raise=8 (>+6) [wool]

### four-team-wool-two-sided
- spawn `spawn` @(17,47) surf=11 raise=+2 facing=left dist-to-piece-centre=3.5 | end=front (mk→centre 50.6 vs piece-centre→centre 52.2; piece near/far 45.3/60.4)
- wool `wool` @(-32,57) surf=15 raise=+6
- wool-spawn dist: 51.0
- iron `spawn` offset=(-10,0) ahead=-10.0 lateral=0.0 → **behind**
- wall `piece-3`↔`piece-2` kind=land border=10 Δsurf=-2
- wall `piece-4`↔`piece-5` kind=land border=10 Δsurf=+2
- ⚠️ FLAGS: SP7 iron BEHIND spawn [spawn]

### isolated-spawn-approaches
- spawn `spawn` @(-42,22) surf=13 raise=+4 facing=right dist-to-piece-centre=3.5 | end=back (mk→centre 48.1 vs piece-centre→centre 47.2; piece near/far 40.3/54.1)
- wool `wool` @(2,52) surf=15 raise=+6
- wool-spawn dist: 54.1
- ⚠️ FLAGS: G3 2T width=90 (∉40..60)

### isolated-spawn
- spawn `lane-3` @(0,40) surf=13 raise=+4 facing=front dist-to-piece-centre=0.0 | end=front (mk→centre 40.0 vs piece-centre→centre 40.0; piece near/far 35.0/45.3)
- wool `lane-6` @(-40,50) surf=13 raise=+4
- wool `lane-7` @(35,50) surf=13 raise=+4
- wool-wool sep: 75.0
- wool-spawn dist: 36.4, 41.2
- iron `lane-3` offset=(2,0) ahead=0.0 lateral=2.5 → **beside**
- wall `lane-4`↔`lane-8` kind=land border=10 Δsurf=+2
- wall `lane-5`↔`lane-9` kind=land border=10 Δsurf=+2
- ⚠️ FLAGS: G3 2T width=80 (∉40..60)

### mirror-big-board
- spawn `spawn-2` @(-137,12) surf=19 raise=+10 facing=right dist-to-piece-centre=3.5 | end=back (mk→centre 138.1 vs piece-centre→centre 135.8; piece near/far 130.4/141.4)
- wool `wool` @(-127,-67) surf=19 raise=+10
- wool `wool-2` @(-42,47) surf=13 raise=+4
- wool-wool sep: 143.0
- wool-spawn dist: 80.6, 101.2
- iron `spawn` offset=(10,5) ahead=-5.0 lateral=10.0 → **beside**
- wall `piece-9`↔`piece-22` kind=land border=15 Δsurf=-2
- wall `piece-24`↔`piece-28` kind=land border=15 Δsurf=+0
- ⚠️ FLAGS: G3 2T width=130 (∉40..60); G3 2T len=280 (>200); WL5 wool raise=10 (>+6) [wool]

### odd-facing-three-wool
- spawn `spawn` @(-32,-2) surf=17 raise=+8 facing=left dist-to-piece-centre=3.5 | end=front (mk→centre 32.6 vs piece-centre→centre 35.0; piece near/far 25.0/45.3)
- wool `wool-2` @(-57,42) surf=9 raise=+0
- wool `wool` @(-47,-42) surf=17 raise=+8
- wool `wool-3` @(-87,7) surf=11 raise=+2
- wool-wool sep: 46.1, 64.0, 85.6
- wool-spawn dist: 42.7, 51.5, 55.9
- iron `spawn` offset=(-10,0) ahead=0.0 lateral=10.0 → **beside**
- wall `piece-3`↔`piece-16` kind=land border=10 Δsurf=+0
- wall `piece-11`↔`piece-8` kind=land border=10 Δsurf=+0
- wall `piece-2`↔`piece-13` kind=land border=10 Δsurf=+0
- ⚠️ FLAGS: G3 2T width=100 (∉40..60); WL5 wool raise=8 (>+6) [wool]

### rotate-wide-frontline
- spawn `spawn` @(-77,-2) surf=17 raise=+8 facing=right dist-to-piece-centre=3.5 | end=back (mk→centre 77.5 vs piece-centre→centre 75.0; piece near/far 70.0/80.2)
- wool `wool-2` @(-92,32) surf=17 raise=+8
- wool `wool` @(-92,-37) surf=17 raise=+8
- wool-wool sep: 70.0
- wool-spawn dist: 38.1, 38.1
- ⚠️ FLAGS: G3 2T width=100 (∉40..60); WL5 wool raise=8 (>+6) [wool-2]; WL5 wool raise=8 (>+6) [wool]

## Aggregate

**All wool-spawn distances** (17): 22.4, 22.4, 22.4, 36.1, 36.4, 38.1, 38.1, 41.2, 42.7, 51.0, 51.0, 51.5, 54.1, 55.9, 58.5, 80.6, 101.2
- min 22.4 / max 101.2; below WL2 (20): none

**All wool-wool separations** (8): 46.1, 58.3, 64.0, 70.0, 75.0, 85.6, 95.5, 143.0

**All spawn raises** (vs base): +0, +0, +0, +4, +2, +4, +4, +10, +8, +8

**Interface-Δ histogram** (all 137 land interfaces): 0=47, 2=73, 4=10, 6=4, 8+=3; Δ≥4 (cliff candidates)=17

**Zone-width histogram** (min-dim): 5=5, 10=26, 15+=12

**Piece-width histogram** (min-dim): 5=54, 10=81, 15=15, 20+=0

**Board dims** (fanned WxL, teams):
- base-2island: 30x130  (2-team)
- base-2wool: 90x130  (2-team)
- base-4team: 130x130  (4-team)
- four-team-towers-big: 180x180  (4-team)
- four-team-wool-two-sided: 130x130  (4-team)
- isolated-spawn-approaches: 90x120  (2-team)
- isolated-spawn: 80x110  (2-team)
- mirror-big-board: 280x130  (2-team)
- odd-facing-three-wool: 180x100  (2-team)
- rotate-wide-frontline: 190x100  (2-team)

**Iron placement classes**: ahead=1, behind=1, beside=3

**Wall interface Δsurf**: four-team-towers-big:-2, four-team-towers-big:+2, four-team-wool-two-sided:-2, four-team-wool-two-sided:+2, isolated-spawn:+2, isolated-spawn:+2, mirror-big-board:-2, mirror-big-board:+0, odd-facing-three-wool:+0, odd-facing-three-wool:+0, odd-facing-three-wool:+0

## Rule-number flag rollup

- **base-2island**: G3 2T width=30 (∉40..60)
- **base-2wool**: G3 2T width=90 (∉40..60)
- **four-team-towers-big**: WL5 wool raise=8 (>+6) [wool]
- **four-team-wool-two-sided**: SP7 iron BEHIND spawn [spawn]
- **isolated-spawn-approaches**: G3 2T width=90 (∉40..60)
- **isolated-spawn**: G3 2T width=80 (∉40..60)
- **mirror-big-board**: G3 2T width=130 (∉40..60); G3 2T len=280 (>200); WL5 wool raise=10 (>+6) [wool]
- **odd-facing-three-wool**: G3 2T width=100 (∉40..60); WL5 wool raise=8 (>+6) [wool]
- **rotate-wide-frontline**: G3 2T width=100 (∉40..60); WL5 wool raise=8 (>+6) [wool-2]; WL5 wool raise=8 (>+6) [wool]

**Notes:**
- G2 (corridor≥10): 5-wide pieces are legal steps, only counted (see step-width column), not flagged.
- G5 (hop 10..20): hops need the zone-union model; skipped here. Known prior outliers: **25** and **30**.
- EL1 (Δ multiple of 2): all authored surfaces are odd (9,11,13,15,17,19) so every interface Δ is even — no odd Δ found.
- G3 flag assumes width = smaller fanned dim, len = larger; the team-separation axis is NOT inferred. For 'wide-frontline' seeds the cross-board (frontline) span can exceed the current 40..60 width cap legitimately — see the fanned WxL column and judge against intent rather than treating every flag as a defect.
## Island gradient sweep (CT4)

Islands = connected components of the **fanned** terrain pieces (contact Land/Narrow/Overlap; corner
never connects), via the production derivation code. Distance = island centroid → symmetry centre
(0,0), blocks. Stepping stone = island fully submerged in the build-zone union, OR ≤100-block
island with exactly two build-zone interfaces (threshold = the natural break in the size
distribution; verdicts stable for thresholds 100–200). Distance bands = global thirds
(cuts 28.1 / 49.6).

| seed | sym | islands | ρ area↔dist | grow-outward (a) | stones mid/trans/team | falloff (b) |
|---|---|---|---|---|---|---|
| base-2island | rot_180 | 4 | +1.00 | ✓ | 2/0/0 | ✓ |
| base-2wool | rot_180 | 6 | +0.50 | ✓ | 2/0/0 | ✓ |
| base-4team | rot_90 | 8 | +1.00 | ✓ | 3/1/0 | ✓ |
| four-team-towers-big | rot_90 | 12 | +1.00 | ✓ | 0 | n/a |
| four-team-wool-two-sided | rot_90 | 12 | +1.00 | ✓ | 0 | n/a |
| isolated-spawn-approaches | rot_180 | 7 | +0.58 | ✓ | 0 | n/a |
| isolated-spawn | rot_180 | 6 | +1.00 | ✓ | 0 | n/a |
| mirror-big-board | mirror_x | 10 | +0.70 | ✓ | 4/0/0 | ✓ |
| odd-facing-three-wool | rot_180 | 8 | +0.00 | ✗ | 1/1/0 | ✓ |
| rotate-wide-frontline | rot_180 | 17 | +0.15 | ✗ | 6/4/1 | ✓ |

**Roll-up** (90 fanned islands): pooled Spearman(block-area, centroid-distance) = **+0.61**;
grow-outward holds in **8/10** seeds (ρ > 0.2); stepping stones thin monotonically toward the team
side — **17 / 4 / 4** over mid / transition / team bands (21 submerged + 4 two-interface-small);
falloff holds in **6/6** seeds that contain stones (four have none).

**Exceptions** (fail only grow-outward): `odd-facing-three-wool` (ρ 0.00) and
`rotate-wide-frontline` (ρ 0.15) share one mechanism — the largest landmass is a **mid-band spine**
(1650 blocks at dist 49.6 of max 84.4; 1750 at 56.8 of max 85) and the islands further out are
350–450-block pads smaller than it. That flattens grow-outward — but those far pads are exactly the
stepping stones, so the falloff holds even where the growth breaks.

**Correction note:** the SP7 flag in the rule-number rollup above ("iron BEHIND spawn",
four-team-wool-two-sided) was retracted — it was a facing-semantics measurement bug; the iron is
ahead of the spawn (see layout-rules.md, *Resolved this round*).

### Corrected stone classification (author round: markers & encasement)

Two exclusions applied to the stepping-stone candidates above: **marker islands are never stones**
(no measured stone held one — vacuous here, binding on the composer), and stones whose every
interfacing **zone component** touches only one team's islands are **team transient-links**, not
mid stones (automated as: zone component reaches islands of <2 orbit images). Re-measured:

| seed | mid stones [mid/trans/team] | team links | mid form (author) |
|---|---|---|---|
| base-2island | 2 [2/0/0] | 0 | clean, 2 mid islands |
| base-2wool | 2 [2/0/0] | 0 | clean, 2 mid islands |
| base-4team | 4 [4/0/0] | 0 | clean, 4 mid islands in a grid (no hole → not hash) |
| four-team-towers-big | 0 | 0 | hash + grid: centre hole, four aligned islands |
| four-team-wool-two-sided | 0 | 0 | hash |
| isolated-spawn-approaches | 0 | 0 | hash + parallel: 3 interconnected mid islands, 8 zones |
| isolated-spawn | 0 | 0 | clean, no mid islands (team islands only) |
| mirror-big-board | 4 [4/0/0] | 0 | clean: several zones connecting into one big region, free travel between mid stones |
| odd-facing-three-wool | 2 [2/0/0] | 0 | clean, same properties; mid islands: tiny 2×2 + the 400-block L |
| rotate-wide-frontline | 7 [3/4/0] | 4 [0/0/4] | clean, 7 grid mid islands in one big region |

**Corrected roll-up:** mid stones **21**, thinning **17 / 4 / 0** over the global distance thirds —
a hard zero in the team third. Team transient-links **4** (all `rotate-wide-frontline`'s corner
pads at 63.6, encased between the spawn mass and a wool platform — deep in the team third by
function). Distance-third cuts unchanged (28.1 / 49.6). Mid forms fully author-labeled:
**clean 7 · hash 3 · parallel 0**.

## Eleventh seed: big-board-wool-two-sided-plaza-parallel-mid (real-map trace)

Trace of a real map, `maxPlayers` **30 per team** — the corpus's first honest player count.
`rot_180`, fanned board **150×260**, 16 pieces, 2 zones. Key facts (all firsts for the corpus):

- **Parallel mid** (first corpus example — tally now clean 7 · hash 3 · parallel 1). Two lane
  chains, each = one authored zone + the *other* zone's rot_180 image joined across the axis
  (left: `zone` + `zone-2`-image; right: `zone-2` + `zone`-image); the chains never touch.
- **Crossings: 35 per lane**, stone-free, also the region minimum — above the 10–20 band, at
  30/team (hop envelope scales with player count; maxPlayers pass).
- **Surface palette below base**: frontline pieces at **7** (`piece-3`) and **5** (`piece-14`) —
  first sub-base terrain; still odd, all interface deltas still even (EL1 extended 5–19).
- **Asymmetric frontline heights** (FR5): the far lane ends on a 13 frontline (`piece-2`) facing
  the enemy's 5; mirrored, so each team owns one high and one low lane end. High ground = the
  lane farther from your spawn, by design.
- **Plaza hub**: `piece-5` is a **30×30** open square at 13 (HB1 note) — first hub authored as
  one big piece; the corpus piece-width histogram gains its first 30.
- Islands: 2 (each team side one connected mass); **no stepping stones** — mid stones/team links
  unchanged (21 / 4 corpus-wide). Wool at +8 over its lane (17 vs base 9), spawn at base,
  wool↔spawn on separate lanes.

## maxPlayers pass (six of eleven landed)

Author counts, per team; stored `maxPlayers` = the **comfortable cap** (upper end). Land = fanned
terrain block area. b/p = land per player at the cap (teams × cap):

| seed | author count | stored | land | b/p @cap | class |
|---|---|---|---|---|---|
| base-2island | 8–10 | 10 | 1900 | 95 | compact |
| base-2wool | 10–12 | 12 | 2500 | 104 | compact |
| base-4team | 8–10 | 10 | 3800 | 95 | compact |
| isolated-spawn-approaches | 10–12 (real-map model, XML 10) | 12 | 2500 | 104 | compact |
| rotate-wide-frontline | 16–20 (real-map model, XML 16) | 20 | 7000 | 175 | elaborated |
| big-board-…-parallel-mid | 30 (trace) | 30 | 10500 | 175 | elaborated |

**Coupling (G8 v0):** compact seeds cluster at **95–105 b/p**, real-map-grade at **175 b/p**
(rotate at its defined 16 gives 219 — defined counts sit below the comfort cap). Derived
proposals for the remaining five (awaiting the author): isolated-spawn (3100 land, compact)
**~14**; odd-facing-three-wool (5000) **~14–16**; four-team-wool-two-sided (6000, 4T) **~10–12**;
four-team-towers-big (11500, 4T) **~16**; mirror-big-board (11750, 2T) **~30**.

## Twelfth seed: mirror-tiny-map-cliff · final maxPlayers table · FREEZE

`mirror-tiny-map-cliff` — the tiniest map yet: **mirror_z** (first), **5 players/team**, fanned
board **25×70**, 9 pieces (several 1-cell), 2 zones, 650 land blocks (**65 b/p**). Facts:
- Surface palette **3–11**: sub-base 3/5/7 — including the first **lowered spawn** (−2, SP4
  extended) — wool at +2.
- **Axis-spanning mid island** (`piece-2` at 9 + `piece-6` at 3, self-mirrored across z=0) carrying
  a **10-wide Δ6 cliff** ("9 vs 3") — the corpus's smallest EL6-qualifying cliff, `cliffs`-marked
  (the EL6 lint demanded it; thresholds sit at their lower bounds at tiny scale). Mid form:
  clean (all zones chain into one region) → tally **clean 8 · hash 3 · parallel 1**.
- Markers at block centres of 1-cell pieces: the fixed 8×8 spawn/wool stamps overlap piece
  bounds → **scaled structure presets** filed as `G31`.

**Final maxPlayers (author, per team; stored = comfortable cap):**

| seed | count | land | b/p |
|---|---|---|---|
| mirror-tiny-map-cliff | 5 | 650 | 65 |
| base-2island | 10 (8–10) | 1900 | 95 |
| base-4team | 10 (8–10) | 3800 | 95 |
| base-2wool | 12 (10–12) | 2500 | 104 |
| isolated-spawn-approaches | 12 (10–12, real-map XML 10) | 2500 | 104 |
| four-team-wool-two-sided | 12 | 6000 | 125 |
| isolated-spawn | 14 | 3100 | 111 |
| odd-facing-three-wool | 16 | 5000 | 156 |
| four-team-towers-big | 18 | 11500 | 160 |
| rotate-wide-frontline | 20 (16–20, real-map XML 16) | 7000 | 175 |
| big-board-…-parallel-mid | 30 (trace) | 10500 | 175 |
| mirror-big-board | 32 | 11750 | 184 |

**The maxPlayers pass is complete — `layout-rules.md` v3 is FROZEN (2026-07-04) as the composer's
v1 rule set.** G8 carries the coupling table (b/p rising 65 → 184 with per-team land).
