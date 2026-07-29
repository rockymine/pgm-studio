# Generator audit — where the implementation and the model disagree

The standing record of **known gaps between `model.md` and the code**, kept as measured evidence
rather than assertion. It exists because the model doc describes the system as it should be, and a
reader deserves to know which parts of it the generator does not yet honour.

An entry **leaves this file when its fix lands** (the commit references its id). Each names the
rule kind (`model.md` §1.14) the fix belongs to, because most of these are not bugs in a mechanism
— they are **missing rules that the taxonomy already has an address for**.

**Provenance.** The frequency measurements come from a 4-preset × 200-seed probe of
`Allocate → Fill` (small/mid/big/huge, as in `tools/compose/unit-gallery.cs`), run by
`tools/compose/seat-probe.cs` — **re-run it to re-measure**. Measurements are dated; the structural
claims below were re-verified against the code on 2026-07-27. Before trusting a number, check the
date and re-run.

---

## 1. `demand` is a declared kind with no live type

`model.md` §1.14 builds its first load-bearing distinction on **demand vs offer** — "the direction
of the arrow". The offer half is richly built out (`EdgeOffer` appears across seven files). The
demand half has **nothing behind it**: `FamilyDock.EntryDemand`, the table's own exemplar, was
retired when the clamp was redefined, and `grep FamilyDock src/` returns zero hits.

What survives of the concept is `UnitRequests.Overhangs` — a private predicate stating demand
**by exclusion** (`family is L or Donut`, i.e. "these do *not* need two hosts"). The clamp's
two-entry requirement, the taxonomy's exemplar, is now an implicit negation inside the allocator.

*Kind:* the missing kind is **demand** itself. → **G107**

## 2. The allocator's sampling layer has no address in the taxonomy

Most of the allocator is not shape-level rules at all: it decides from a budget **how many**
wools, **whether** a frontline, **how big** a hub, and **how often** each shape appears. Nothing in
§1.14 covers it. It has two faces.

**The mix — a steering distribution.** Roughly a dozen weights (`BentWoolChance`, `DonutChance`,
`StapleChance`, `ClampAdjacentChance`, `DonutCornerWoolChance`, `SideRoomChance`, `RingChance`,
`WidenedRingChance`, `ThirdWoolChance`, `FullFaceChance`, `ShiftedFaceChance`, `NoFrontlineInN`)
plus several uniform picks. No declared kind fits: a `menu` is a *set* and carries no frequency; a
`band` carries a distribution but is explicitly **descriptive and advisory**, which is the opposite
of what these do. A weighted *generative* distribution is a real, distinct kind — provisionally
**mix**. Naming it raises the question worth having: should a mix be **authored**, or **derived
from a band**? LN1 is the case in point — it records a measured corpus frequency (width 10 ×81,
15 ×15), which is exactly a band over the same choice the allocator makes with a hard threshold.
→ **G108**

**The ladders — budget→structure thresholds.** `WideLaneLand`, `FrontlineMinLand`, `TinyBoardLand`,
`FullTeamPlayers`, `HubCapCells`/`HubWideCap`. Not facts (nothing is read off geometry), not menus,
not fit gates. The closest declared kind is **target** — "a per-request, prescriptive constraint a
compose holds and verifies" — and that is what they should become; they differ from a target only
in that nothing verifies them afterwards. → **G109**

## 3. The trace to law — which constants are grounded

Re-verified against `TeamUnitAllocator.cs`, 2026-07-27; the code it cites moved to `UnitTuning`/`UnitRequests`/`UnitSeating`/`SeatGeometry` in B42 without changing behaviour.

| allocator rule | law | verdict |
|---|---|---|
| `WoolLaneCells = 2` | LN1 + `model.md` §3 ("the lane to the wool is simple, w2") | **grounded** |
| `w = 3` above `WideLaneLand` | LN1 (10 base, 15 larger; corpus 81:15) | values grounded, **threshold invented** — LN1 states a *distribution*, the code makes it deterministic |
| `WoolLengthRatio = 3` | LN2 (20–50 blocks before a junction/dead end) | **grounded** on the lower bound; the 50 cap is unimplemented |
| `CornerClearanceCells = 0` | the mass-level corner law | **vestigial** — it documents rather than acts (see below) |
| the frontline joint's `faceWidth` | FR6 (split vs wide, band docks flush) | right kind (**offer**), law partially served |
| `RingFitCells = 5` | geometry (`BodyEmitter.Ring` guards itself) | now used as a **form-choice threshold** in `ChooseHubForm`, which is a legitimate menu decision — not the duplicated fit gate it was once flagged as |
| `HubCapCells` / `HubWideCap` | none — HB1 constrains *width*, not box size | **ungrounded** |
| `FrontlineMinLand` | none | **ungrounded** |
| `TinyBoardLand`, `FullTeamPlayers` | WL6 gives 1–3; G8 couples players↔land | range grounded, **thresholds invented** |
| the shape-mix weights | WL8 governs wool approach routes | **ungrounded** — the donut *is* WL8's alternative-route case, but `0.25` derives from nothing |
| `RingChance`, `ThirdWoolChance`, `NoFrontlineInN` | none | **ungrounded** |

**On the corner law.** `Cells.HasDiagonalPinch` is the mass-level pinch test, and it is real — but
it is invoked **only from tests and the unit gallery**, never from `src/`. The invariant is
*asserted over composed units* rather than *gated at compose time*. That is a deliberate and
defensible position (the seat step is meant to make pinches unreachable by construction), but it
means no runtime path rejects a pinch, and `CornerClearanceCells = 0` is the constant that used to.

## 4. Laws the placement does not honour

Measured over 400 seeds × 4 presets on the **placed rooms** (not the boxes), in blocks. *Measured
2026-07-22 — re-run `seat-probe.cs` before citing.*

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
- **HB4 and FR6's wide frontline are unreachable**, not merely unimplemented: a branch hub with a
  frontline falls back to the rectangle, so the Bar is never chosen. The law describes a composition
  the code cannot currently produce.

## 5. Rules sitting in the wrong layer

Re-verified 2026-07-27 — both still present at `TeamUnitFiller.cs:137–143`.

- **The offer grouping is decided by the filler, by coin flip** (`rng.NextBool(0.5) ? Joint :
  Several`). Grouping is part of an **offer** (§1.14: "the edges/intervals it invites neighbours
  onto, *in which groupings*"), and offers are the allocator's plan — the allocator is what writes
  joints. Worse, this is exactly **FR6**: joint vs several *is* wide vs split frontline, an authored
  law. A coin flip stands in for it.
- **The frontline's form choice is also the filler's.** `frontForm` picks Bar-for-branch-hub /
  else staple-or-strand inside `TeamUnitFiller`, but form choice is declared the allocator's (§5, designations,
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
