# Composer: the middle-out generation order (design note)

Status: **draft for correction-by-id** (2026-07-05). Motivating the implementation of the
round-3 frontline/mid rules (FR6, FR7, MD6, CT9, CT10, CT11, BZ10, HB4) in the composer.
Not yet a frozen contract — this note is the plan; `layout-rules.md` stays the authority on
the rules themselves.

## The symptom

Generated maps throw their land at growing **wool/spawn lanes extremely long**, and their
middles come out as a **thin band with a single column of aligned stepping stones** (the
2-row crossing fanned = 4 stones stacked in one column) instead of a **wide approach**. Both
are direct consequences of the generation *order*, not of any one rule.

## Current order (team-side-first, middle-last)

Per compose attempt (`Composer.ComposeStages`):

1. **`SampleCrossing` — arithmetic only.** Draws row-count (0/1/2), depths, half-gap. No
   geometry, and — the key point — **reserves no budget**. It only fixes the axis margin.
2. **`TeamUnitGrower.TryGrow` — spends all the land budget.** Hub (small, capped) + the rest
   split by *fixed weights*: `spawn 2.0 · each wool 1.8 · third wool 1.2 · frontline ≤1.0`
   (`TeamUnitGrower.cs:246-250`). With two wools + a twin front, the frontline gets **~15%**
   of the flexible budget, clamped to the lane cap.
3. **`TryCarve` — the band is fitted to whatever grew.** The band's lateral extent must live
   inside the hull of the unit's frontline faces (BZ9, `MidCarver.cs:94-98,130-143`); stones
   sit in one sampled column (`MidCarver.cs:114`).

So the middle is shaped **by** the team unit; the team unit is never shaped by the middle.
`spawn (2.0) + wools (2×1.8)` soak up ~85% of the flexible land, and land can only go into
*length* (clamped at LN2's 50-block cap, but that is long) → over-long lanes. The frontline,
at ~15%, is a 4–6-cell sliver → a narrow approach. And p5 fails 1800/1800 in **step 2**
(the BZ6 + spawn-floor budget infeasibility) — it never reaches the stones at all.

## Budget accounting: what the corpus actually says

**`bp` (`Envelope.BpAnchors`, land/player) was fit to PIECES-ONLY.** Measured on the 12 authored
seeds (which *are* the corpus anchors), piece-area/player equals `bp` almost exactly
(`base-2island` 95=95, `base-2wool` 104≈105, `rotate-wide-frontline` 175=175, `mirror-tiny`
65=65, …). So today's "budget = pieces" is **self-consistent** with `bp` — it is *not* a
double-count bug. But two things hide inside that:

1. **`bp` lumps mid-pieces in with team-side pieces.** Splitting each seed's pieces into
   team-side (marker islands + their connections, back from the axis) vs mid (frontline /
   stones / bands, near the axis), the **mid-piece share of the pieces budget** ranges across
   almost the whole corpus:

   | | mid-piece share of `bp` | mid land (pieces+zones) vs team-side |
   |---|---|---|
   | lane-heavy | big-board-parallel-mid **2%** · four-team-wool 6% · iso-spawn-approaches 16% | 13–71% |
   | median | **≈14%** | ~100–150% |
   | wide-approach | **rotate-wide-frontline 54%** · odd-facing 54% · isolated-spawn 48% | 226–**312%** |

   The composer sits at **~15% — the corpus median.** Its mid-piece budget isn't broadly
   wrong; it just **never samples the wide-approach high end** (`rotate-wide-frontline`), so
   every generated map reads lane-heavy. The mid share is **style-dependent — a sampled
   variable, not a fixed reservation** (which is also why over-reserving would be wrong: real
   corpus maps *do* sit at the lane-heavy end too).

2. **Build zones are large in the corpus and entirely unbudgeted.** Seed zone land runs
   **40–100% of piece land**; the median map's total mid (frontline + stones + **build zone**)
   is roughly *equal* to its team-side land, almost all of it zones. The composer has **no zone
   target** — `TryCarve` fits a thin band to the frontline hull (`MidCarver.cs:94-98,130-143`),
   so its build-zone land is far under corpus. This is the biggest single gap.

The teaching sets (drawn at p12/cell5 = the 50-cell p12 budget) pin the *shapes* the wide end
should take: frontline **piece** 12–16 cells (vs the composer's ~4–6), stones as a **lateral
2×2 grid** (vs one column), a wide-approach **band** at w≈6–8 × d≈5 (vs w≈2–4), and the BZ10
negative — the w=2 × d=9 thin extrusion (`band-0/1`) — to avoid.

### Why this doesn't stub the lanes (the constraint you flagged)

The large mid is mostly **build zones** — terrain players *cross*, not islands — and zones are
**not** in the pieces budget `bp`. Only mid-*pieces* compete with the lanes, and that share is
modest (~15–30% even at the wide end's piece count). So a wide approach is bought mostly with
**free build-zone geometry** (a deep/wide band + a stone grid), not by starving the lanes. The
lever that *is* safe to push is the zone target; the lever to push *sparingly and by sampling*
is the mid-piece share.

## The reorder (middle-out)

Two independent levers, sized from the corpus, applied before the team side grows:

1. **A build-zone target (the big, safe lever).** Give the crossing a **zone-land-per-player
   target** calibrated to the corpus zone/piece ratio, and sample the band's **width** (FR6:
   wide single vs split, w≈6–8 for the wide form) and **depth** (BZ10 floor, no `w=2 × d=9`)
   against it — instead of fitting a thin band to the frontline hull afterward. Lay stones as a
   **lateral 2×2 grid** across that width (MD6), not one column. This is the change that makes
   the middle read wide, and it spends **no `bp` (pieces) budget** — so it never touches the
   lanes.
2. **A sampled mid-piece share (the small, careful lever).** Split `bp` into a team-side share
   and a mid-piece share, and **sample the mid-piece share across the corpus range** — default
   near the median (~15%), reach toward the wide-approach end (~45–55%) on a minority of plans
   (the `rotate-wide-frontline` archetype). The frontline piece is grown to **dock the band
   fully** (FR6) up to that sampled share; surplus flows **back to the lanes**.
3. **Grow hub + spawn + wools into the remaining `bp`.** On a lane-heavy sample the mid share
   is ~10–15% and the lanes stay long; on a wide-approach sample it's ~50% and the lanes
   shorten — both are corpus-real.

### The bound that protects the lanes (load-bearing)

> **The band's size comes from a separate zone budget, not from `bp`, so it never competes with
> the lanes. The mid-*piece* share of `bp` is a sampled variable, defaulting at the corpus
> median (~15%) and only reaching the wide end on a minority of plans.**

This is why over-reserving is avoided *by construction*: the visible widening rides on the free
zone/stone geometry, and the one lever that does spend `bp` (the frontline piece) is sampled,
median-centred, and bounded by how much band it has to dock — not a flat large reservation.
Concretely at p12: a median sample keeps today's ~15% frontline and long lanes but adds a real
wide band + stone grid; a wide-approach sample lifts the frontline to ~45–55% (the
`rotate-wide-frontline` look) and accepts shorter lanes, as the corpus does.

## What changes in code, by rule

- **`MidCarver`** — the bulk of it:
  - *FR6*: band width sampled as a board-fraction (wide) or split-with-tips, and the frontline
    is asked to dock it flush to tips/corners.
  - *MD6*: stones laid as a **grid** across the band width (multiple columns), grid-aligned to
    the unit's front/hub lines (CT7) as today, but no longer a single `vMin`.
  - *BZ10*: a band-depth floor + a width/depth ratio guard so no band is long-and-thin.
  - *CT9*: keep the twin-front recess → closure-hole device, now realized against the wider band.
- **`Envelope`** — add a **zone-land-per-player** target (a second anchor table beside `bp`,
  fit to the seeds' zone/piece ratio) so the crossing has a corpus-calibrated build-zone size.
- **`TeamUnitGrower`** — the budget split:
  - split `bp` into a team-side share and a **sampled mid-piece share** (median ~15%, wide-end
    ~50% on a minority of plans); the frontline piece is grown to dock the band up to that
    share, surplus returning to the lanes. Replaces the flat `frontUnit ≤1.0` weight.
  - *HB4*: allow the L/Z hub composition (a hub built from two pieces) so the hub↔frontline
    join can be the L/Z the examples show — a later slice, not the first.
- **`ComposeGeometry` / tests** — only if we also emit CT10/CT11 axis-sitting mids (rot_90
  plus/window-frame). **Deferred:** that relaxes the frozen `AssertFannedSeparation` invariant
  and unblocks no current sweep case (see below); it is its own opt-in slice, not part of the
  reorder.

## What this does *not* fix

- **p5 stays a separate budget-floor question.** p5 dies in the grower on BZ6 + the spawn
  ≥2×2 floor (13-cell budget vs an ~18-cell minimum BZ6-valid unit) — a *team-side* floor the
  reorder doesn't touch (the mid taking less/more doesn't create the missing lane cells). The
  standing constraint holds: **do not fix p5 by growing the board.** If p5 is ever unblocked it
  is via a team-side minimum-unit machinery (the buffer/allotment idea), tracked separately.
- **CT11's axis-sitting relaxation is not on the p5 path.** Measured: p5/t4 never reaches the
  stone stage, so relaxing the separation rule changes nothing for it; and no other sweep case
  is *blocked* by the stone self-collision (it is a graceful reject-and-resample). CT11/CT10 in
  the composer is a *richer-mids feature* for big boards, opt-in and later — not this reorder.

## Open questions (for correction-by-id)

1. **Zone target shape** — should the build-zone-per-player target be a single anchor table
   (like `bp`), or sampled per plan across the corpus zone/piece range (17–155 blocks²/player,
   very style-dependent)? My lean: sample it, correlated with the mid-piece share draw (a
   wide-approach plan draws both a big band and a big frontline).
2. **Mid-piece share distribution** — the corpus runs 2%→54% with a ~14% median. Sample how —
   a skewed draw centred at the median with a wide-approach tail, or a small discrete set of
   archetypes (lane-heavy / balanced / wide-approach)?
3. **Grid stones vs the CT8 hole** — the wide band + stone grid (MD6) and the twin-front recess
   hole (CT8/CT9) are both mid devices; do we sample one *or* the other per plan, or can a wide
   band host both a stone grid and a recess hole?
4. **Slice order** — land MD6 (grid stones) + BZ10 (band depth) first inside the *current*
   order (cheap, visible win, no `bp` split), then add the zone target + mid-piece split; or
   do the split first?

## Appendix: the measurement

Seed decomposition (axis-distance heuristic: a piece is "mid" if its near edge is ≤2 cells from
the fanning axis; "team-side" otherwise; zones are all mid) — the basis for the shares above.
Reproduce with the `jq` in the session, or fold into `tools/compose` if we want it maintained.

| seed | P | mid-piece % of `bp` | zone % of piece | mid(pc+zn) vs team |
|---|---|---|---|---|
| big-board-parallel-mid | 30 | 2% | 10% | 13% |
| four-team-wool-two-sided | 12 | 6% | 33% | 42% |
| mirror-big-board | 32 | 7% | 53% | 65% |
| base-2wool | 12 | 8% | 128% | 147% |
| base-2island / base-4team | 10 | 10% | 158% / 88% | 188% / 100% |
| four-team-towers-big | 18 | 13% | 34% | 54% |
| isolated-spawn-approaches | 12 | 16% | 44% | 71% |
| mirror-tiny-map-cliff | 5 | 38% | 100% | 225% |
| isolated-spawn | 14 | 48% | 90% | 268% |
| odd-facing-three-wool | 16 | 54% | 50% | 226% |
| rotate-wide-frontline | 20 | 54% | 88% | 312% |
