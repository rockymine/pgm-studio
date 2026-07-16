# Reading wool-approach shapes in real maps — the G50–G52 / G62 investigation (2026-07)

The question posed: what do **G50–G52** (the wool-box emitter's entry-shift / attachment-width /
docking-mode knobs) actually depend on in **G62** (derive-side slot recovery + classifier scoping),
and is G62 formulated right — in particular, *can the shape deriver read wool approach shapes inside
actual maps at all*, given that authored maps have already been through the pipeline's **fragment**
step and hold parts of every approach as build zones rather than terrain?

Everything below is empirical. The instruments are `scripts/approach_read_lab.py` (a Python port of
`Cells` + `ShapeClassifier.Classify`/`ClassifyOpen` + the emitter's family geometries, used because
this investigation ran where the .NET SDK could not be installed) and
`scripts/approach_read_gallery.py` (the evidence gallery →
`tools/deriver/out/approach-read-gallery.html`). The port is gated on reproducing the C# classifier
exactly: all 17 fixtures in `tools/deriver/shapes/` classify to their named family, and all eight
emitted families round-trip emit → classify. The experiments refuse to run if that gate fails.

---

## 0. Verdict in one paragraph

G50–G52 do **not** depend on G62 for their code: the knobs are emitter geometry, testable standalone,
and their generation-reachability gate is **G61**, exactly as `BACKLOG.md` says. The real coupling is
*definitional* and runs **both ways**: (a) G50–G52's acceptance criterion — "classifier-transparent" —
turns out to be **undefined until G62 fixes the scope**, because the family read of the variant
shapes flips with context (§3); and (b) G62's `AssignSlots` must be specified against G50–G52's
variant geometry or it will be built to the corner-pinned templates and immediately re-broken. On the
formulation doubt: it is justified. The traced corpus shows the classifier cannot read any wool's
approach on a real map today (it reads the *unit*, one family per map), and — the author's suspected
"definitive issue", confirmed — **fragmentation destroys the terrain-only family read**: converting a
single slot piece to a build zone changed the family in 13/13 trials, while reading terrain ∪ cut
recovered it in 13/13. A scope that merely stops a terrain flood at junctions (G62's current wording)
therefore cannot recover approach shapes from authored/traced maps; the scope must also decide the
*surface* (terrain + lane-width build links), which makes it a board-deriver consumer, not a
`WoolLaneShape` promotion. Details and a proposed reformulation in §4–§5.

---

## 1. What was measured

Three candidate reads, run over **19 wools across the 12 maps in `tools/seeds/traced/`**:

1. **`Classify` on terrain** — the shape deriver as it exists, unscoped (floods the terrain
   component reachable from the wool room).
2. **`ClassifyOpen`** — the junction-stop lane read (the corridor the room owns, up to the first
   junction/widening).
3. **`Classify` on terrain ∪ zones** — the naive "player-reachable surface" read (every build-zone
   cell treated as walkable).

Plus two synthetic experiments over the emitter's own output:

4. **Fragmentation** — emit a family, promote exactly one non-entry slot piece to a build zone,
   re-classify (terrain-only, and terrain ∪ the promoted cells).
5. **Variant grids** — the G50/G52 entry-shift / wool-shift / side-dock grids recorded in
   `BACKLOG.md`, classified standalone and with hub terrain docked at the entry.

---

## 2. Result: no existing read recovers the approach on a real map

| read (19 wools, 12 traced maps) | Isolated | I | L | Z | Scythe | Clamp | U | H | Donut | complex | plaza |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `Classify` on terrain (today) | | 2 | | 9 | | | | 2 | 6 | | |
| `ClassifyOpen` lane read | | 8 | 1 | 2 | | | | | | 8 | |
| `Classify` on terrain ∪ zones | | | | 5 | | | | 2 | 12 | | |

Three separate failure modes, all visible in the gallery:

- **The unscoped family read returns the unit, not the approach.** On every multi-wool map both
  wools read the **same** family (acapulco Z/Z, aequabilis Donut/Donut, a-new-day H/H, agrostid
  I/I…) — the flood covers whatever terrain mass the wool is welded to, and the "family" is that
  mass's global topology. This is review §3.5's prediction, now with numbers. Two sub-cases:
  - *whole-unit* (6/19): the map's terrain is one welded mass (ad-astra, a-new-day, agrorythe) and
    the read is the unit's outline/void count.
  - *island-fragment* (13/19): fragmentation already cut the unit into islands, and the read is
    whatever fragment the wool room sits on — agrostid's wools read **I** because each wool's island
    is literally a straight bar; the actual approach continues across two build zones the read
    cannot see.
- **The lane read works but reads a different, smaller thing** — the corridor up to the first
  junction *or first widening*. It is the only read that survives real maps (no `plaza`/`none`
  results), but 8/19 are `complex` (real approaches wander past 2 bends) and 8/19 are `I`, several
  of which are floods stopped early by a widening. By construction it can never see a fork, bay,
  ring, or the wool's seating — i.e. never a family.
- **Unioning the zones wholesale over-connects**: 12/19 read Donut, because board-scale build
  zones ring voids everywhere. The surface idea is right (see §3) but the granularity is wrong —
  zone *links*, not zone *areas*.

## 3. The fragmentation experiment — the author's "definitive issue", confirmed and inverted

Promoting **one** slot piece of an emitted family to a build zone (the smallest possible fragment
move — exactly what `IsolationCut` does, and what every authored map has done many times over):

| family | promoted slot | terrain-only read | terrain ∪ cut read |
|---|---|---|---|
| L | run | **Isolated** | L |
| Z | bar | **I** | Z |
| Z | room-run | **Isolated** | Z |
| Scythe | entry-run | **L** | Scythe |
| Scythe | bar | **I** | Scythe |
| Scythe | room-run | **Isolated** | Scythe |
| U | bar | **Isolated** | U |
| U | entry (either) | **L** | U |
| Donut | entry-bar | **U** | Donut |
| Donut | leg (either) | **Z** | Donut |
| Donut | room-bar | **Isolated** | Donut |

13/13 destroyed on terrain alone; 13/13 recovered on terrain ∪ cut. The conclusion is sharp:
**post-fragment, family identity lives on the play surface — land plus the lane-width build links —
not on terrain.** And since *every* authored/traced map is post-fragment (the corpus §2 confirms it:
13/19 wools sit on fragment islands), any derive-side approach read that floods terrain only is
structurally unable to read authored maps, no matter how good its stop policy is.

There is a second, subtler finding in the variant grids. The G50/G52 variants recorded in
`BACKLOG.md` do not all read their family standalone under the current classifier: the
shifted-entry scythe reads **Z** alone and **Scythe** once hub terrain docks at its entry (the hub
seals the bay's escape past the vacated corner); the shifted-wool and side-dock scythes read **Z**
in both contexts tried here. So the "verified Scythe·w2, classifier-transparent" notes on G50/G52
are not reproducible from the recorded grids alone — the original editor plans
(`scythenotboxaligned`, `scythewoolattachments`, in the DB, not the repo) must have carried
different sealing context, and should be re-checked when G50–G52 are picked up. The load-bearing
point is not that the notes are wrong; it is that **"classifier-transparent" is only defined
relative to a scope**. A bay is a bounding-box property, and shifting an endpoint off a corner
changes which escape routes the bay has — whether a neighbouring mass seals them is part of the
answer. Family identity at the Z↔Scythe boundary is *context-dependent by construction*.

## 4. What this does to G62's formulation

G62 currently bundles two deliverables — `SlotAssignment` (derive-side slot recovery) and
`CorridorExtent` (the junction-stop flood promoted from `WoolLaneShape`, parameterised by stop
policy, "giving the classifier a scope so it works on full composed/traced plans"). The evidence
splits them apart and re-scopes the second:

1. **`SlotAssignment` is sound as stated, but must be specified against the G50–G52 grammar.**
   It is emit-side-verifiable (emit → classify → re-derive slots → compare), needs no scope
   machinery, and unblocks the mirror upgrade and G67/G68. But "template-match the slot sequence
   onto the classified pieces" must match on **path order and adjacency** (the entry is the piece
   carrying the mouth interface; the room is the terminal; run/bar/leg are the chain between),
   never on canonical rect positions — a shifted entry, a widened tail, a shortened side-docked
   terminal all keep the template's *order* while breaking its *geometry*. The cheap insurance:
   G50–G52's variant grids (they already exist, in `BACKLOG.md` and now in
   `approach_read_lab.VARIANT_CASES`) become `AssignSlots` acceptance fixtures on day one, even
   though the emitter knobs land later.
2. **`CorridorExtent` as "a stop policy on the terrain flood" is under-specified — the scope needs
   a *surface* policy and a *stop* policy.** The approach read on any post-fragment plan must
   flood **terrain ∪ selected build links**, where "selected" is typed by what the board deriver
   already knows (`ContactGraph`/`BoardStructure`): traverse `intra`/`self` links of lane-like
   width and span (the cuts that fragment an approach), stop at `front↔front`/`front↔neutral`
   links (the approach ends at the frontline by definition) and at hubs/plazas (the width test).
   That makes the scope delimiter a **board-deriver consumer** — "board deriver delimits → shape
   deriver classifies" (review §3.5) is not just a nice layering, it is the only version that
   works — rather than a promotion of `WoolLaneShape`'s terrain-only flood. `WoolLaneShape`'s
   junction-stop test survives as the *stop* half; the *surface* half is new.
3. **The scope should also seal the frame.** The Z↔Scythe context-dependence (§3) suggests the
   classifier's bay/branch tests want a **scope frame**: when the scope is known (the wool box at
   compose time, the recovered extent at derive time), concavities that open only toward the mouth
   edge are entries, and the frame's other edges seal — which makes the standalone fixture and the
   in-plan read agree, and makes "classifier-transparent" a well-defined acceptance test for
   G50–G52. Without this, the same variant legitimately reads Z in one plan and Scythe in another.
4. **Even reformulated, expectations for reading *hand-authored* maps should be tempered.** The
   corpus says real approaches wander (`complex` bends), widen mid-lane (early plaza stops), and
   blur into rooms and hubs. A correct scope will recover families where the author drew boxy
   approaches, and should be allowed to answer **"no confident family"** everywhere else — which
   G68's conditional-fire rule already anticipates (recovery failure is never a violation). The
   practical consequence for **G56** (corpus mining): the minable signal on traced maps is the
   **lane read + junction/zone context** (and the family read where it is confident), not a family
   label per wool. G56's wording — classifier "needs a scope … to read a wool's approach on a full
   traced plan" — remains true, but its yield will be lane-vocabulary corrections more than new
   families.
5. **One consumer of slot recovery can skip recovery entirely.** For *composed* plans evaluated
   inside the compose loop, the emitter's slots are still in hand (`GrownPiece.Slot`);
   `EvalContext` could carry them directly, making evaluator-side slot terms (G68) useful on
   generated output without waiting for derive-side recovery — recovery is only needed once a plan
   crosses a save/author/trace boundary (slots and boxes are both derived-not-persisted, so a
   *reloaded* generated plan is in the same position as a traced one).

## 5. The dependency picture for the board

- **G50 / G51 / G52** — unchanged home and unchanged G61 gate ("reachable from generation once M2
  lands"). Two additions each: their variant fixtures double as `AssignSlots` acceptance fixtures
  (see §4.1); and their "classifier-transparent" acceptance criterion should be restated as
  "classifier-transparent *within the wool-box scope*" once the scope frame exists (§4.3) — as
  recorded, the transparency of the scythe variants is not reproducible standalone (§3).
- **G62** — worth splitting on its own fault line: **(a)** `SlotAssignment` + the true mirror
  (no scope needed; unblocks G67/G68-on-generated-plans; spec against the variant fixtures), and
  **(b)** the approach scope (surface policy + stop policy + frame, consuming
  `ContactGraph`/`BoardStructure`; prerequisite for G56 and for G68 on traced/loaded plans). The
  current wording — "the junction-stop flood promoted from `WoolLaneShape`, parameterised by stop
  policy" — describes only the smaller half of (b): it works for *composed, pre-fragment* plans
  (all-land units) but cannot read any post-fragment plan, which is every authored and traced map
  and every reloaded generated plan.
- **G56** — keeps its G62(b) dependency; re-scope its promise from "surface missed families" to
  "surface missed lane/junction vocabulary + families where confidently recovered".
- **G68** — keeps its G62(a) dependency for slot terms on generated output (via `EvalContext`
  carrying emit-time slots, §4.5) and its G62(b) dependency for everything else; its
  conditional-fire design is what makes the tempered expectations of §4.4 acceptable.

None of these edits were applied to `BACKLOG.md` — they are proposals for the author to fold in.

## 6. Addendum (2026-07-16) — the family drift is fixed; the strategy decision is applied

Author's ruling on this investigation, and what shipped from it:

- **Reading finished maps is out of scope by decision, permanently.** A real map's base plan is not
  recoverable — the traces themselves already simplify (aequabilis's circular holes mocked as
  rects; a-new-day's spawn lane traced as parallel pieces where a *hypothetical* original plan
  would hold one spawn-width piece, later cut and stretched). Hypothesizing the
  fragmentation/mutation moves a real map "went through" is the **human oracle's** mental model,
  deliberately not automated. Slot definitions exist **for generated maps only**; the classifier
  is the generator's mirror (without it we would not know what we generated) — a hard prerequisite
  for generation, never a reverse-engineering tool. `map-generation.md` §5.4 now states this;
  **G56 is retired**; **G62/G68 are reworded** to generated-plans-only (slots ride `EvalContext`
  from the emitter on composed plans; `AssignSlots` serves the mirror).
- **The §3 family drift (Scythe→Z under endpoint manipulation) is fixed** — the classifier's
  scythe test was the culprit. The old test asked whether the *bounding box* has a single-edge
  concavity (`Cells.HasBay`), and sliding an endpoint off a box corner opens the bay toward a
  second edge without unfolding the shape — which is why the read flipped with context. The new
  test asks whether the *terrain itself* doubles back — some grid row or column crosses it in two
  runs (`Cells.HasFold`, i.e. not orthogonally convex): the lines through a wrapped bay always
  cross two runs, and a Z staircase never does. Every shifted/side-docked variant now keeps its
  family, standalone **and** hub-docked, at 1× and 2× scale (`ShapeVariantTests` pins all of it;
  catalog/mirror/stress suites unchanged and green).
- **What the fix does *not* dissolve: the scope.** The fold is a property of whatever cell set the
  classifier is handed. A neighbour mass docked at the entry's mouth cannot flip the read any
  more, but a mass running a shape's whole flank wraps a genuine concavity — the component really
  does fold, and reading it as one shape is a scope error, not a classifier error. Inside
  generation the wool box is that scope (G61), which is the only place the family read is now
  defined to run.

## 7. Reproducing

```
python3 scripts/approach_read_lab.py        # E1 gate + E2/E3/E4 tables
python3 scripts/approach_read_gallery.py    # → tools/deriver/out/approach-read-gallery.html
```

The lab's port must be kept honest: if `ShapeClassifier`/`Cells` change, E1 fails and everything
downstream refuses to run. If the .NET SDK is available, prefer wiring the same experiments through
the real classifier (a `tools/deriver/` harness over `tools/seeds/traced/`) and retiring the port.
