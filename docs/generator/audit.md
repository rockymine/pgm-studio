# Generator audit — where the implementation and the model disagree

The standing record of **known gaps between `model.md` and the code**, kept as measured evidence
rather than assertion. It exists because the model doc describes the system as it should be, and a
reader deserves to know which parts of it the generator does not yet honour.

An entry **leaves this file when its fix lands** (the commit references its id). Each names the
rule kind (`model.md` §7, *Every rule is one kind*) the fix belongs to, because most of these are not bugs in a mechanism
— they are **missing rules that the taxonomy already has an address for**.

**Provenance.** The frequency measurements come from a 4-preset × 200-seed probe of `Allocate → Fill`
(small/mid/big/huge), swept over `Composer` and counted per finding — a scratch pass, not a checked-in
harness, so **re-measuring means writing it again against today's code**, which is the point: a number
re-derived from a stale script is worse than one re-derived from scratch. Measurements are dated; the
structural claims below were re-verified against the code on 2026-07-27. Before trusting a number, check the
date and re-measure.

---

## 1. `demand` is a declared kind with no live type

`model.md` §7, *Every rule is one kind* builds its first load-bearing distinction on **demand vs offer** — "the direction
of the arrow". The offer half is richly built out (`EdgeOffer` appears across seven files). The
demand half has **nothing behind it**: `FamilyDock.EntryDemand`, the table's own exemplar, was
retired when the clamp was redefined, and `grep FamilyDock src/` returns zero hits.

What survives of the concept is `UnitRequests.Overhangs` — a private predicate stating demand
**by exclusion** (`family is L or Donut`, i.e. "these do *not* need two hosts"). The clamp's
two-entry requirement, the taxonomy's exemplar, is now an implicit negation inside the allocator.

*Kind:* the missing kind is **demand** itself. → **G107**

## 2. The allocator's sampling layer has no address in the taxonomy

Most of the allocator is not shape-level rules at all: it decides from a budget **how many**
wools, **how big** a hub, and **how often** each shape appears. Nothing in
§7 covers it. It has two faces.

**The mix — a steering distribution.** Roughly a dozen weights (`BentWoolChance`, `DonutChance`,
`StapleChance`, `ClampAdjacentChance`, `DonutCornerWoolChance`, `SideRoomChance`, `RingChance`,
`WidenedRingChance`, `ThirdWoolChance`, `FullFaceChance`, `ShiftedFaceChance`)
plus several uniform picks. No declared kind fits: a `menu` is a *set* and carries no frequency; a
`band` carries a distribution but is explicitly **descriptive and advisory**, which is the opposite
of what these do. A weighted *generative* distribution is a real, distinct kind — provisionally
**mix**. Naming it raises the question worth having: should a mix be **authored**, or **derived
from a band**? LN1 is the case in point — it records a measured corpus frequency (width 10 ×81,
15 ×15), which is exactly a band over the same choice the allocator makes with a hard threshold.
→ **G108**

**The ladders — budget→structure thresholds.** `UnitTuning`'s shares and the hub box they size. Not
facts (nothing is read off geometry), not menus, not fit gates. The closest declared kind is **target**
— "a per-request, prescriptive constraint a compose holds and verifies" — and the spend gate now
verifies the one that matters, the land a unit must come out holding; the shares beneath it still
differ from a target in that nothing checks them one by one. → **G109**

## 3. The trace to law — which constants are grounded

Re-verified against `TeamUnitAllocator.cs`, 2026-07-27; the code it cites moved to `UnitTuning`/`UnitRequests`/`UnitSeating`/`SeatGeometry` in B42 without changing behaviour.

| allocator rule | law | verdict |
|---|---|---|
| `WoolLaneFloorCells = 2` | LN1 + `model.md` §2 ("the lane to the wool is simple, w2") | **grounded** as a producible floor |
| `CorridorBlocks` / `WoolCorridorBlocks` per band | G2, measured as the local thickness of 359 built maps | **grounded** |
| `WoolLengthRatio = 3` | LN2 (20–50 blocks before a junction/dead end) | **grounded** on the lower bound; the 50 cap is unimplemented |
| `CornerClearanceCells = 0` | the mass-level corner law | **vestigial** — it documents rather than acts (see below) |
| the frontline joint's `faceWidth` | FR6 (split vs wide, band docks flush) | right kind (**offer**), law partially served |
| `RingFitCells(cw, cell)`, `WideHubCells(cw, cell)` | geometry (a ring is two walls and a hole; a docked bar keeps one beside it) and `LN6`'s 12-block floor for a plain hole | **grounded** — derived from the corridor and the rule rather than stated |
| `HubBoxCells` — area from the share, aspect sampled | none — HB1 constrains *width*, not box size | area **grounded** in G8 through the share; the 1.3–2.4 aspect is **invented** |
| the box shares (`FrontlineShare`, `WoolShare`, `HubMinShare`) | G8 gives the total, nothing splits it | total **grounded**, the split **invented** |
| `WoolCount` per band | WL6 gives 1–3; the corpus gives 1 at nano and 2 above | **grounded** |
| `SpendFloor` / `SpendCeiling` | none — G8 gives a number, not a tolerance | **invented**, and the one the author has said to tune on what the boards look like |
| the shape-mix weights | WL8 governs wool approach routes | **ungrounded** — the donut *is* WL8's alternative-route case, but `0.25` derives from nothing |
| `RingChance`, `ThirdWoolChance`, `SecondWoolChance` | none | **ungrounded** |

**On the corner law.** `Cells.HasDiagonalPinch` is the mass-level pinch test, and it is real — but
it is invoked **only from tests and the unit gallery**, never from `src/`. The invariant is
*asserted over composed units* rather than *gated at compose time*. That is a deliberate and
defensible position (the seat step is meant to make pinches unreachable by construction), but it
means no runtime path rejects a pinch, and `CornerClearanceCells = 0` is the constant that used to.

## 4. Laws the placement does not honour

Measured over 400 seeds × 4 presets on the **placed rooms** (not the boxes), in blocks. *Measured
2026-07-22 — re-measure before citing.*

- **WL7 — wool↔wool separation. Systematically violated.** The law records a corpus of 46–143 blocks
  with a working minimum ≈45. The composer produced **min 21, median 41–55, max 87–98**, with
  **31–53% of all wool pairs below 45**. The whole distribution is compressed — the composer's
  *maximum* sits well below the corpus maximum. *Caveat:* the plan is a mini-layout and grid-born
  distances are resolved downstream by scale and roughen, so an absolute block comparison is not
  decisive alone. The distributional argument survives it: this is not a constant offset, it is a
  narrower spread sitting at the corpus floor. The seat gap does **not** achieve WL7 — that is a
  marker-to-marker *traversal* spread, not a body-adjacency floor, so it belongs with the
  hub-growth / budget work that gives boxes room to spread. → **G110**
- **WL2 — wool↔spawn ≥ 20. Fixed.** Was violated on the huge preset only (111/930 pairs under 20,
  min 12) by the third wool doubling onto the spawn's side. The seat-separation gap resolved it:
  that wool can no longer seat within the gap, so it **drops** rather than cramming.
- **WL6 — 1–3 wools, each on a distinct lane. Holds.** 0/400 units place two wools on one hub edge.
- **HB4 and FR6's wide frontline — no longer unreachable.** This entry recorded the Bar as a
  composition the code could not produce: a branch hub with a frontline was said to fall back to the
  rectangle, leaving `TeamUnitFiller`'s Bar-for-branch-hub arm dead. Re-measured **2026-08-13** on a
  different instrument — 480 composed boards through `GET /api/compose` (240 each at 12 and 20
  players, `rot_180`), cross-tabulating each board's hub form against its frontline form —
  **87 branch hubs carry a frontline at 12 players and every one takes the Bar; 14 of 14 at 20.**
  The arm is not dead; it is the only outcome. What remains open on FR6 is the *grouping* (§5), not
  whether the wide face can be reached.

## 5. Rules sitting in the wrong layer

Re-verified 2026-07-27 — both still present at `TeamUnitFiller.cs:137–143`.

- **The offer grouping is decided by the filler, by coin flip** (`rng.NextBool(0.5) ? Joint :
  Several`). Grouping is part of an **offer** (§7: "the intervals it invites neighbours
  onto, *in which groupings*"), and offers are the allocator's plan — the allocator is what writes
  joints. Worse, this is exactly **FR6**: joint vs several *is* wide vs split frontline, an authored
  law. A coin flip stands in for it.
- **The frontline's form choice is also the filler's.** `frontForm` picks Bar-for-branch-hub /
  else staple-or-strand inside `TeamUnitFiller`, but form choice is declared the allocator's (§4, designations,
  and the allocator already owns the hub-form choice). The two halves of one decision — the hub form
  and the form that answers it — sit on opposite sides of the allocate/fill seam. → **G111**

## 6. Open seat defect — lanes flush against a branch hub's legs

On L/U hubs **without a frontline**, wool and spawn lanes can sit flush against the legs' walls. The
hub's remaining free surface is exactly where build regions attach in later stages, so a build region
would land touching the lane. Mechanism: a dock flush against a **non-corner run end** — a run ends
mid-edge only where the body's mass stops, so that end is a leg's wall and gets no inset by design.

Measured **23/3/1/1** units per 200 (small/mid/big/huge), and **every one a branch hub** — so the
attribution is exact but the frequency is a small-board effect. Fix direction: a ≥1-cell margin
between a seat and a *mass-adjacent* run end. **Measured cost of that rule: it would refuse 30–50%
of all current docks** — far more than the ~27 it fixes, because an `along + 2` test also rejects
every dock on a *full-edge* run of a small hub. So the margin must be required only at **non-corner**
run ends, or the rule cascades into re-seats and allocation failures. No such margin exists in the
code today. *Kind:* a **law** — build-surface clearance, the compose-side twin of the room-clearance
guard. → **G106**

## 7. A hypothesis that did not survive

Recorded so it is not re-derived. The staple's full-mouth check is made in the *demand* step against
the hub's **bbox edge length**, before the form is chosen — while the dock actually lands on a free
**run** of the chosen form, which on a branch or holed hub is shorter. That looked like a fit gate
evaluated against the wrong surface, one step too early. Measured: **0 disagreements out of 47
staples**. Staples only survive where the edge is wide, and there `run == bbox edge`; elsewhere they
demote before it matters. No defect.

## 8. Raised rules whose check is not their argument

Read off the code on 2026-10-04, when the raised rules became `LayoutRules` constants. A served rule's text
states what its check does; where `rules.md` argues for more or for something else, the difference is here,
because which of the two is right is a gameplay question and the author's to settle (→ **G291**). The rules
whose author rulings are already filed — `G2` (G287), `G5` and `CT12` (G285), `EL1`, `SP8` and `WL11`
(G286), `FR6` and `FR9` (G288) — are not repeated.

| rule | `rules.md` argues | the check does |
|---|---|---|
| `CT4`, `CT13` | islands grow with distance from the centre and thin to none in the team third | count the stepping stones both teams reach (`CT4`) and one team reaches (`CT13`) against learned bands; no gradient |
| `CT8` | a plan with no hole is flagged | no such check; the learned band starts at 0, so only too many holes scores, and the shared attack route is its own rule (`CT14`) |
| `FR4` | 1 to 3 team approaches, one only if it is wide | counts frontline faces per team against a learned band |
| `G8` | land per team of 2250, 4025, 7075 and 8730 by size band | the share of its frame the ground fills, a learned band, with ground off every route its own rule (`LN5`); the land budget is only the composer's spend gate |
| `LN1` | a 10-block base, capped about 16 in front of a wool | the narrowest wool lane against a learned band; no cap |
| `LN2` | 20 to 50 blocks to a junction or dead end | a learned band of 25 to 110; `ComposeGeometry.LaneChainMaxBlocks` is read by nothing |
| `BZ6`, `MD7` | the middle build zone | only a zone whose id is `mid-band`, so a hand-drawn plan's middle is never judged |
| `SP1` | the route may pass a spawn on a wide lane | walks piece to piece, so passing beside a spawn on its own piece counts as through it |
| `SP2` | the spawn at the back of its lane | the back half of the spawn's own piece |
| `ST1` | the wool room's region, cage and entry lines | only producibility cites it, for a box room that differs from the composer's 2-cell room |
| `ST8` | about 15 blocks in front of the entrance | anywhere from 10 to 20 |
| `WL7` | a working minimum of 45 blocks | a learned band only |
| `WL12`, `WL20`, `LN6` | bays and holes beside a goal, and holes beside none | a goal's straight crossings over any void, and a hole's narrowest; a bay touching no goal is not checked |
