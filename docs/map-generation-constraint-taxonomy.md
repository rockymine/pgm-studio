# Map generation — the constraint taxonomy (review & proposal, 2026-07)

> **Status: review/proposal, pre-G88/G89; amended round 2 (author corrections + the edge
> taxonomy, §4).** A shared language for the generator's rules and constraints, an inventory of
> where each kind lives today, and a proposed order of changes. It **proposes**; it freezes
> nothing. When a piece of it ships, the vocabulary lands in `docs/contracts/map-generation.md`
> §1 (the locked terms) and the types in `docs/contracts/map-generation-vocabulary.md`, and the
> matching section here is retired. It defers to `map-generation.md` for the model,
> `shape-vocabulary.md` for the shape/designation layers, `layout-rules.md` for the numbers, and
> `map-generation-architecture-review.md` §9 for the evaluator design it extends.
>
> **Two standing correctives (author, round 2)** this doc must not overstate:
> - **`layout-rules.md` is a living rule set, not settled law.** It began as an interview
>   transcript grown into rules; its "frozen" label means *amend by protocol, by id* — it does
>   not mean the rules are final or beyond question. Nothing in it is taken for granted; rules
>   run a development like everything else. What *is* stable is the vocabulary and the
>   meta-model — and even the model is a **meta-model**: a mapping of how an authored layout can
>   be decomposed into machine steps, not a claim about how authors actually work.
> - **The seeds are final-fidelity artifacts, not composer targets.** Every seed already carries
>   the post-compose passes (fragmentation, resizing) baked in — it shows what the *end* of the
>   pipeline may look like, never what the composer should emit. Measuring them (the envelope
>   bands) checks what is *expressible* and informs rules a human then judges; **auto-deriving
>   rules from seed statistics does not work** — several band-derived rules were rejected on
>   review, which is exactly why the band-filling effort stopped. Bands are a triage aid,
>   author-curated one by one, never a rule factory.

The trigger: G88 (hub) and G89 (frontline) both need rules of the form *"where may a neighbour
attach, on which edges, in which groupings"* — e.g. *a frontline built as a spine + two arms
(an F/Π body) offers its arms' outward edges to the mid: either **both together** (one wide
build region — a single connected frontline) or **each individually** (the double frontline)*.
Expressing that wish today has no single home, because "rule" currently means seven different
kinds of thing in seven different dialects. This doc names the kinds once, so every future rule
has an obvious address — and an obvious prompt.

---

## 1. The kinds — one word per kind of rule

> **Adopted — now canonical in `map-generation.md` §1.14 (G94).** The twelve rule kinds (fact · menu ·
> fit gate · demand · offer · veto · gate · knob · target · band · hard term · law · doctrine) and the
> two load-bearing distinctions (**demand vs offer**, **target vs band**) are locked terms there; that
> doc governs. This section is retired — see §1.14 for the table and the one-line classification test.

## 2. Inventory — where each kind lives, forward and mirrored

The generator's standing doctrine is a forward/inverse pair at every level (emit↔derive,
allocator↔`BoxPartition.Of`). The rule kinds follow the same shape — most forward mechanisms
already have their derived read; the table names both sides, because a new rule should always
land as a *pair*:

| Concern | Forward (compose-side) | Inverse (derived read) |
|---|---|---|
| what fills a box | `FillProfiles` / `FillMenu` (menu), `MinBox` (fit) | `ShapeClassifier.Classify` / `ClassifyBody` (the mirror) |
| where a join lands | `DockingGate` over `BoxEdgeInterface` (gate over facts) | `Contact`/`InterfaceSegment` kinds; `BoxPartition.Of` joints |
| the frontline | **missing** — the Front designation's face offer (G89) | `ContactGraph.Frontline`/`FrontlineEdges` (piece edges facing a zone); `BoardDeriver` frontline edges → **`FrontlineRuns`** (team, face width, straight/offset) |
| what a build zone links | **missing** — the hub/frontline offers the zone consumes | zone kinds `front-front`/`front-neutral`/`front-solo`/`neutral-neutral`/`intra`/`self`; zone width + `IfaceMin`/`IfaceMax` (BZ3/BZ8/BZ9) |
| enclosed voids | declared holes (`buffer`/zone holes), CT9's authored recess | hole classes `encased`/`gap`/`frontline`/`middle`, declared/undeclared, `CrossRoutes` (parallel ways) |
| the mid | `MidCarver` (form = `f(frontline)`) | the CT mid-form read (`hash`/`parallel`/`channelled`) |
| feel | — (bands never steer) | `SoftTerm.Value` over `EvalContext`, banded by `seed-envelopes.json` |

Reading the table column-wise: the **inverse column is nearly complete** — the board deriver
already labels frontline edges, groups them into runs with widths and profiles, types every
build zone by what it links, and classes every hole. The **forward column has the two holes**
G88/G89 fill: nothing lets a designation *say* what those reads should come out as. The offer
concept is precisely "the forward twin of `FrontlineRuns` and the zone kinds", and the mirror
doctrine extends verbatim: **the designation drives; the deriver verifies.**

## 3. The offer model — the F example made precise

An **offer** is published by a designation onto a body's geometry:

- **what**: a set of edge **intervals** (position + width — the §1.5 interface primitive),
  read off the placed shape so they move with every knob (shape-relative, like the G80 facts);
- **width class** per interval (`w2/w4/w6`) — for the hub this *is* the constraint it sources:
  the consumed width is the `cw` the neighbour's fill menu reads (G88);
- **grouping** — the modes in which the intervals may be consumed: **jointly** (one consumer
  must span the group — FR6's "flush across both tips") or **severally** (each interval its own
  consumer);
- **vetoes** riding along — intervals that must *not* be consumed (the recess between twin
  tips stays the CT9 rotation hole; the hub-side spine edge is for the hub, not the mid).

The F worked through (frontline designation over `SpineArms(2)`, rotation fixed — spine docks
the hub, arm-tips toward the axis):

```
   hub side                                offer, mode SEVERAL        offer, mode JOINT
  ═════════ spine                          tip-a │ … │ tip-b          ╞═ tip-a … tip-b ═╡
   │     │  arms                           two consumers →            one consumer spans both →
   ▼     ▼                                 the DOUBLE frontline       one wide band, recess
  tip-a tip-b  ← the face (offered)        (two derived runs)         preserved as a hole (CT9)
```

Same body, two offers → two different derived boards: mode *several* reads back as **two
`FrontlineRuns`** (the twin/CT8 form); mode *joint* as **one wide run** with the recess
registering as a `frontline`/`middle`-class hole. The mid — "the most complicated part, a
function of what the frontline has to offer" — becomes a plain **consumer**: `mid =
f(frontline)` is cashed out as *the mid band consumes the frontline's offers under BZ7–BZ10*,
and non-straight faces (a notch, a Z/HB4 fold) are just offers whose intervals sit on different
lines — the derived run profile (`straight`/`offset`) is already the read-back of exactly that.

Three mechanism gaps stand between today's code and this being expressible (all small, all
already implied by the docs):

1. **Interval facts** *(shipped — G93)*. `BoxInterfaces.Of` no longer collapses an edge to a
   flat slot list: each edge carries its per-piece **intervals** ordered along it
   (`BoxEdgeInterface.Intervals`), so a twin face is two disjoint intervals on one edge — the
   box-perimeter sibling of the §4 classified boundary.
2. **Designation marks in the gate** *(on the board as G95; type sketch §7)*.
   `DockingGate.Role` is one global slot→role table (`entry` docks, `room` vetoes) —
   approach-only. Dock roles become per-designation **marks** (`entry`/`room` for approaches;
   `face` for the frontline; per-edge `interface` for the hub), stamped by the designation per
   `shape-vocabulary.md` §8, with the gate unchanged in spirit: one table over marks.
3. **Offer records on joints** *(on the board as G96; type sketch §7)*. A `BoxJoint` knows two
   boxes share an interval; it doesn't know the interval was *offered*, at what width class, in
   which grouping. The partition graph carries the offer so the partitioner places consumers
   against it (and `BoxPartition.Of` mirrors it back).

Never key any of it on a letter: "F" is a placement read of `SpineArms(2)` and drifts as the
arms slide (§3 of the shape vocabulary). The offer is stated over structural slots + marks
("the arms' outward end-edges"), so it survives every knob — and covers Π, T, E, and the plain
Bar with the same sentence.

## 4. The edge taxonomy — negative spaces, wall counts, offerable surface *(shipped)*

> **Terms adopted — canonical in `map-generation.md` §1.13 (G94).** The wall-count classes
> (notch/bay/hole/open), parts, mouths, wall slots, guard, and the *offerable surface = open ∧
> ¬terminal ∧ ¬guarded* definition are locked there; that doc governs on the vocabulary. This section
> and §4.1 are **retained as the shipped design record** — the rationale and the worked publish policy
> that G88/G89 bind to (which the canonical model doc does not absorb).

The vocabulary that grounds all of §3, now **computed from geometry alone**
(`Pgm/Shapes/BodyEdges.cs`, rendered by `tools/compose/edge-gallery.cs` →
`tools/compose/out/edge-gallery.html`). A body's negative spaces escalate by **wall count** —
the number of axis directions the body walls a connected void from:

| Walls | Class | The picture |
|---|---|---|
| 2 | **notch** | the corner an L wraps |
| 3 | **bay** | the staple's recess, the hook's bay — open one way |
| 4 (enclosed) | **hole** | the ring's void |
| ≤1 | *(open)* | plain outside — not a feature of the shape |

Every boundary edge is then classified by the space it faces: an edge walling a notch/bay/hole,
or a **free edge** — outward surface facing nothing — which is exactly the **offer candidate
set**. The names align with the emit-time `ShapeVacancy` kinds; the read is shape-relative and
total (any rectangle set — emissions, compounds, future hub bodies), needing no emitter
cooperation.

Why this is the load-bearing piece:

- **It generalizes the four-edge hub.** Today's hub is a solid rectangle, and the entire
  composer attachment rule is its four free edges: the back edge seats the spawn, the side
  edges take the wool boxes, the front edge the frontline. A real hub body (an L, a U, a
  spine-with-legs) has *more* edges, and how many depends on where the legs sit — the edge
  taxonomy is what replaces "the four edges of the rect" as the thing rules bind to: **offers
  live on free edges; notches/bays/holes are what remains.**
- **It drives vacancy publication.** Which negative spaces are eligible to be published (the
  parked vacancy-allocation work) becomes a rule over the classes: a hole is CT8 rotation
  currency, a bay is claimable through its mouth, a notch is a corner remainder — and the
  gallery is the instrument for deciding those rules by looking.
- **Placement decides the spaces, not K.** The same `SpineArms(2)` reads *one bay* as Π and
  *bay + notch* as F — pinned by tests (`BodyEdgesTests`); the rule language must therefore
  speak in spaces and edges, never letters (§3's closing rule, now verifiable).
- **The designation participates.** A clamp's recess is a **bay only because the room closes
  it** — the same two bars without the room wrap a mere notch (also pinned by a test). Negative
  space is read on the *finished* mass, so a designation can change a space's class — one more
  reason the offer lives in the designation layer.
- **A space decomposes into parts — negative space is a body too (author, round 4; shipped).**
  A non-rectangular space is itself a compound of rectangles: the uneven branch (a long and a
  short arm) wraps one **six-edge bay that is a U** — and Tetris works in the void exactly as in
  terrain (slide that U into the F and a solid rectangle remains). `BodyEdges` therefore
  **slab-decomposes** every space into rectangles (`NegativeSpace.Parts`), each classed by its
  **own body walls** — siblings count as open: the U's bar spanning the mouth reads
  *notch-grade* and borders the short arm's tip, the slot between the arms stays a *bay* part,
  the corner beyond the short arm a *notch* part. The space-level class stays correct and
  untouched — this is a **layer on top** — but rules gain reach: "a piece may attach to the
  inset leg's end (through the mouth-bar part)" is now stateable, where the flat bay class
  forbade the whole recess wholesale. And the space carries its **own compound identity**
  (`NegativeSpace.Form` — the body classifier's spine read now tries all four orientations):
  the uneven branch's bay literally reads `SpineArms(2)` — the void is a body, fully.
- **Parts nest — publish-eligibility has a covering order (author, round 5; partly open).**
  Decomposition exposes *depth*: one part sits in **front** of others, at the mouth, covering
  them. The degenerate E (equal long outer arms, shorter middle arm) wraps one bay whose three
  parts are all bay-grade — the front part spans the mouth and **hides the two slots behind
  it**. The front part is the space worth publishing; the covered ones are not (or only under
  circumstances) — the same way the uneven F's mouth bar fronts its bay leg and notch leg.
  Today the parts exist but carry no explicit order; the **covering/depth relation** (which
  part fronts the mouth, which parts it hides) is the open fact the vacancy-publication rules
  will bind to — publish-eligibility descends from the front part inward.
- **Every space knows its mouths (author, round 6; shipped).** `NegativeSpace.Mouths` carries
  one mouth per open direction — the open side, the **interval** along it (position + width,
  the §1.5 primitive), and the **`wN` width class** it tapers to (fill-menu convention, ties
  small) — because "what can dock *through* the mouth" is the same width grammar as every
  other interface (a `w2` mouth is a chokepoint lane, a `w6` mouth multi-access). A bay has
  exactly one (the fact its offer rules read), a notch two, a hole none. This dissolved a real
  asymmetry the author spotted: on the **emit side** the published vacancy's mouth was already
  a full `BoxInterface` (edge + offset + width) — *for bays only*, with notches and holes
  null; the **derive side** had no mouth at all. Now the derive read is uniform and richer
  than the emit-side publication. Still deferred, same vein: a hole's **ring composition**
  (which parts wall it) for the CT8 rotation reads.
- **The room's clearance guards beyond its wall (author, round 5; shipped).** The third layer:
  the terminal's sealed surface extends past the room itself — the room inflated by the
  **corridor minimum** (`BodyEdges.DefaultClearanceCells` = 2 cells = 10 blocks) is the guard
  region. Terrain boundary runs inside it read `Guarded` (splitting from the free remainder of
  their line — the L's band-top edge is free up to 10 blocks before the room, guarded after),
  and the adjacent negative space's parts split against the same rectangle, the covered piece
  `Guarded` too. Rationale: the room and its immediate approach are **final as the emitter
  designed them** — a piece docked or published inside the margin sits too close to the room
  and changes the objective's difficulty out from under the design. Guard is a *rule-grade*
  seal computed as a fact overlay (opt-in overload), stacking with ownership: the three axes on
  every run are *faces × terminal × guarded*, and the offerable surface is
  **open ∧ ¬terminal ∧ ¬guarded**.
- **The terminal seals its own wall (author correction, round 3).** Boundary runs are classified
  on **two independent axes**: what they face *and* who owns them — a run on the terminal room's
  own wall carries `Terminal`, and runs **split where ownership changes**, so a room capping a
  lane leaves one boundary line part free interval, part sealed interval (the L/I case). The
  free offerable surface is exactly *open ∧ not terminal*. Ownership is kept a **fact**; the
  never-attach verdict over it stays the gate's **rule** (`SlotDockRole.NeverDock` /
  `SealsWool`) — which is what lets the two sanctioned exceptions stay expressible: the clamp's
  designated room faces (the designation itself docks terrain there) and the elevation-stage
  dock. The L's room even shows the axes composing: its top wall is *terminal AND notch-facing*.

### 4.1 Targeting the taxonomy — the publish policy, worked (author question, round 6)

*How do I now express: a scythe's bay is no-go for publishing, but an L's notch, a degenerate
F's front notch, and a degenerate E's front bay are publishable?* The answer is a small
**publish policy** — ordered verdicts over the facts, one rule kind per level, with precedence
**space-veto → guard → part-allow → default deny**:

*(Decided [author, round 7] and shipped as `PublishPolicy` — `Compose/Boxes/PublishPolicy.cs`,
the `DockingGate`-style table over the edge-taxonomy facts; the ✓/✗ verdicts render on every
gallery card. The wall-composition selector originally drafted for R2 became a rationale, not
the trigger: the author's calls simplified the terminal-capped rule to kind level.)*

| # | Verdict | Binds at | Selector | Catches |
|---|---|---|---|---|
| R1 | **veto** | level 1 (whole space) | terminal-capped ∧ `Kind == Hole` | the donut's void — a terminal shape's enclosed void is its own device (CT8 currency), never published. A **terminal-free** hole (a bare ring's) *is* publishable — its size condition is a pending gate |
| R2 | **veto** | level 1 (whole space) | terminal-capped ∧ `Kind == Bay` | **every** approach bay: the scythe's (walls `entry-run · bar · room-run` — the WL8 second-approach rationale) and the clamp's (`entry · room · entry`), and the U/H entry-walled bay alike [author]. A terminal-free U's bay *is* publishable |
| R3 | **carve** | level 3 (guard) | the room's clearance margin | subtracts the guarded parts/edges from anything that survived the vetoes (the L's room-corner) |
| R4 | **allow** | level 2 (parts) | part **fronts a mouth** (`Front`) ∧ ¬guarded | the **L** notch (a single part — its own front), the **F**'s mouth-bar notch, the **E**'s front bay, and the **Z**'s second notch — `room-run`-walled, still published [author]: proximity is the guard's job, not a veto's. A hole (no mouth) offers all its parts |
| — | **deny** | default | everything else | covered parts (the E's two hidden slots, the F's bay leg) stay unpublished until a rule says otherwise |

**Publishing is an offer, never a fill.** A published vacancy enters the pipeline for a
**later step** to claim once the base is built — that is where a third wool can seat inside a
free-standing U's bay or a ring's hole — and it may legitimately stay empty. The policy decides
what is *offered*; nothing about the offer obliges a consumer.

Answers packed in there:

- **The scythe veto targets level 1** — the pure-shape space, all parts and edges at once. The
  reason (a second approach to the wool) applies to the whole recess; the guard's ~10-block
  margin could never carry it. The guard is only *reached* by spaces that survive the vetoes —
  the levels are a precedence order, not alternatives.
- **Never name the letter.** "Scythe" appears nowhere in R2 — the binding is the wall
  composition, and that makes the rule **designation-dependent for free**: a terminal-free
  scythe body's bay has only structural wall slots (`run/bar/leg`), so nothing fires — the same
  body publishes differently as an approach vs a bare compound, with zero per-shape code.
  Exactly the terminal-capped vs default-shape distinction, derived rather than declared.
- **"First level" in the allow rules = the front (covering) part** — R4's selector is the
  `NegativeSpacePart.Front` fact: a part fronts a mouth iff it holds a cell of the mouth
  interval.
- **The facts landed with the policy**: `NegativeSpace.WallSlots` (the slot/mark names of the
  walling pieces — the derive-side twin of `ShapeVacancy.Walls`, kept as the rationale carrier
  and the binding for future finer rules) and the part `Front` flag off the mouths.
- **The round-6 open calls are decided [author, round 7]**: the U/H bay — vetoed for terminal
  shapes, published for the bare body (which folded the terminal-capped bay rule down to kind
  level); the Z's `room-run` notch — published (the guard, not a veto, keeps pieces off the
  room). Still open: the terminal-free hole's **size gate**, and the publisher step itself
  (who consumes offers, when — after the base is built).

## 5. Targets — controlled variance

"I want layouts varied, but **controlled** variance": the sampler *chooses* among legal forms
today (family rolls, form rolls), but nothing lets a compose *hold* a chosen character and be
checked against it. That is the **target** kind:

- a `ComposeTargets` record on the request — e.g. *frontline runs per team: 2; connected: yes;
  mid form: parallel; hub form: ring* — each field optional, unset = sampler's free choice;
- **sampled** when unset (variance), **held** when set (control): targets steer the forward
  choices (which designation mode, which grouping, which form menu row) and are **verified**
  against the derived reads (`FrontlineRuns`, mid form, hole classes) at gate time — the mirror
  doctrine again, one level up;
- distinct from bands: a target out of corpus band is *allowed* (the author asked for it); a
  sampled compose stays inside bands by default.

This also unblocks the stalled soft-rule work: wishes that wouldn't fit as bands ("a certain
amount of frontlines, and connected ones") decompose into a **measurable** (a derived read — often
already computed), optionally a **band** over it (learned by `envelope-stats`, descriptive),
and optionally a **target** field (prescriptive). One new measurable falls out immediately:
**connected-run count** — group `FrontlineRuns` by the land component of their owning islands;
runs in one component are "connected frontlines" (the twin tips of one team unit), runs in
different components are parallel-lane fronts. Cheap to add (`SoftTerm.Value` over data the
board deriver already holds), and the natural verification hook for the F example's two modes.

## 6. Proposed order of changes

Ordered so each step is small, independently green, and consumed by the next; steps 2–3 are
the pre-work G89 wants anyway. (Task ids to be assigned when pulled onto the board; G88/G89/
G63-C are the existing anchors.)

0. **The edge taxonomy** *(shipped — §4)*. `BodyEdges` classifies negative spaces (notch 2 /
   bay 3 / hole enclosed) and boundary edges from geometry; `edge-gallery.cs` colour-codes them
   over every shape — the instrument the following steps' rules are decided against.
1. **Adopt the vocabulary** (doc-only) — *done (G94)*. The §1 kinds + the §4 edge terms landed in
   `map-generation.md` §1 as locked terms (§1.12 body/designation, §1.13 edge taxonomy, §1.14 rule
   kinds); §1 and the §4 terms here retired to pointers. `shape-vocabulary.md` folded into
   `map-generation.md` §5 (base shapes are terminal-free compounds, approaches a designation over
   them) and was superseded. Prompt templates (§8) remain here as the live design record.
2. **Interval facts** *(shipped — G93)*. `BoxEdgeInterface` re-grounds on per-piece
   **intervals** ordered along each edge (`EdgeInterval(Start, LengthCells, Slot)`), `Slots`
   the flat view; the clamp's mouth edge carries both entry bars as two disjoint intervals —
   `DockingGate` verdicts unchanged, every prior facts/gate test green unmodified.
3. **Designation marks** (with G88/G89's designations). `Hub(body, edgeWidths)` and
   `Front(body, face)` stamp marks; `DockingGate.Role` becomes mark-driven per designation.
   The approach path keeps its current table verbatim.
4. **Offers** (the heart of G88/G89). The hub designation publishes per-edge width offers (its
   constraint-source role, consumed as the neighbour's menu `cw`); the frontline designation
   publishes the face offer with grouping modes (joint/several) + the recess veto. `BoxJoint`
   carries offer provenance; the partitioner places consumers only on offers.
5. **Close the mirror** (with G63-C's re-baseline). A composed board's derived `FrontlineRuns` /
   zone kinds / hole classes must match what the offers intended — a compose-side assert over
   reads that already exist, catching drift the moment the switch lands.
6. **Targets** (after the switch). `ComposeTargets` on the request: sampled-or-held fields
   steering designation/grouping/form choices, verified against the derived reads. First
   fields: frontline runs per team, connected-run count, mid form.
7. **Measurables + bands as follow-through** (the unstalled soft-rule lane). Connected-run
   count first; then each parked soft wish triaged through §5's decomposition —
   measurable → band → (maybe) target — with every band author-curated (the header corrective:
   seeds describe outcomes; bands are never a rule factory).
8. **The offer-card gallery** (UX, after 4 — the edge half shipped as `edge-gallery.cs`). Per
   (compound × designation): offered intervals annotated with grouping and width class, vetoes
   and demands drawn — generated from the profile data, so the picture *is* the rule table; the
   regression surface for G88/G89.

## 7. The intended type structure — code homes for the missing kinds

The two §1 kinds without a code home, sketched as the types G95/G96/G98 are expected to land —
**intent, not code**; the implementing task refines freely, but the shapes below are what the
shipped facts already support.

**Designation marks (G95).** Marks are `ApproachSlots`-style string constants stamped by a
designation, and the gate's role table becomes designation-scoped:

```csharp
// Shapes — the marks the non-approach designations stamp (approach keeps entry/room)
public static class DesignationMarks { public const string Face = "face"; public const string Interface = "interface"; }

// the designations over a ShapeBody (siblings of ShapeEmitter.Approach)
ShapeEmitter.Hub(body, edgeWidths)  → HubShape    // body + interface-marked edges carrying widths, no terminal
ShapeEmitter.Front(body, faceEdge)  → FrontShape  // body + face-marked tip edges, rotation fixed, no terminal

// Compose — DockingGate.Role becomes per-designation; the approach row is today's table verbatim
DockingGate.Role(designation, slotOrMark) → SlotDockRole
```

**The offer (G96).** A new record over the G93 interval facts, published by the hub/front
designations, carried on the partition graph:

```csharp
// Compose/Boxes — where a neighbour may land, at what width, in which grouping
public enum OfferGrouping { Joint, Several }               // one consumer spans the group | one per interval
public sealed record EdgeOffer(
    BoxEdge Edge, EdgeInterval Interval,                   // where — the G93 fact, shape-relative
    int WidthClass,                                        // the wN a consumer's fill menu reads as its cw
    OfferGrouping Grouping, string GroupId);               // Joint groups resolve together (FR6's flush span)

public sealed record BoxJoint(string BoxA, string BoxB, BoxInterface Interface, EdgeOffer? Offer);
                                                           // provenance: which offer this joint consumed
```

Producers: the hub's per-edge width offers (the constraint source — a consumed `WidthClass` is
the `cw` fed to `FillProfiles.Families`), the frontline's face offer over its tip intervals
(the mid's consumer contract; the inter-tip recess is simply *not offered*, and its CT9 hole is
verified derive-side). Consumers: `BoxPartitioner` places mouths **only on offers**; the mid
carve consumes face offers under BZ7–BZ10. Gate: `DockingGate` grows two rejections —
`NotOffered` (a dock on an un-offered interval) and `GroupNotSpanned` (a Joint group a consumer
fails to span flush).

**Targets (G98).** A record on the compose request; unset = sampled (variance), set = held
(control) and verified against the derived reads:

```csharp
public sealed record ComposeTargets(
    int? FrontlineRunsPerTeam = null,    // steers the face grouping: several(K) vs joint
    bool? FrontlineConnected = null,     // verified vs connected-run count (runs per land component)
    string? MidForm = null,              // "channelled" | "parallel" | "hash" — vs the CT mid-form read
    Compound? HubForm = null);           // Rectangle | SpineArms | Ring | DoubleHole — the hub menu row
```

Verification is ordinary hard terms conditioned on "this target was set" — a held target may
leave the corpus bands (the author asked); a sampled compose stays inside them by default.

## 8. Prompting future rules — templates

Every rule prompt should state: **(a)** the kind (§1), **(b)** the binding in slot/mark terms —
never letters, never screen positions, **(c)** the law id it implements (or "new law — assign
an id, tag `[expert]`"), **(d)** the mirror expectation (what derived read verifies it;
byte-identical wool/spawn where the shared path is touched), **(e)** the vocabulary-doc row if
a type is added.

- **Menu**: "In `FillProfiles`, the frontline row at w4+ admits Bar and SpineArms(K≤2); empty
  menu stays a directed signal. Law: FR4/FR6."
- **Offer** (the F case, verbatim): "The frontline designation over `SpineArms(K)` offers the
  arms' outward end-edge intervals to the mid, modes joint (one band spans all tips, FR6) or
  several (one band per tip — the double frontline); the inter-tip recess is a veto (CT9). Mirror:
  joint reads back as one `FrontlineRun`, several as K runs in one land component."
- **Demand/gate**: "SpineArms(K) as a frontline demands all K tip intervals consumed — an
  unconsumed tip is a directed rejection, never a silent dangling face."
- **Knob**: "Per-arm length on the frontline emission, range X–Y; `ClassifyBody` must still
  read `SpineArms(K)` at every setting."
- **Target**: "Add `ComposeTargets.FrontlineRunsPerTeam` (int?, unset = sampled); steers the
  grouping mode; verified against derived `FrontlineRuns` at gate time; out-of-band values
  legal when explicitly set."
- **Band**: "New measurable connected-run count (runs grouped by owning-island land component);
  band learned by `envelope-stats` over the seeds; dormant until the band lands (never a
  violation without one)."
- **Hard term**: "Symptom on the derived board only (doctrine: no shape names in the
  evaluator): [symptom]; evidence rects + the law id."

---

## 9. Observed failure modes — the current defect list (living)

Author-observed over the unit-gallery seeds, each verified against the code and a 4-preset ×
200-seed probe of `Allocate → Fill` (small/mid/big/huge as in `tools/compose/unit-gallery.cs`;
the probe is `tools/compose/seat-probe.cs` — re-run it to re-measure this list).
This list lives here — the live review record — rather than in a new document. An entry **leaves
this list when its fix lands** (the commit references it); the fix work is board task **G106**.
Each entry names the rule kind (§1.14 / map-generation.md) the fix belongs to, because most of
these are not bugs in a mechanism but **missing rules** the taxonomy has an address for.

**F1 — Neighbour lanes abut: no seat gap.** A spawn and a wool box can share a boundary — no
gap between the two lanes. Probe: 31/37/38/**99** units per 200 (small/mid/big/huge; the huge
spike is the 3-wool plans). Mechanism: `SeatInRuns` packs seats against occupied intervals with
zero spacing, and `SeatOverhang` rejects only *overlap*, not touching; the corner inset going to
0 removed the last incidental spacing. Fix direction: a ≥1-cell **inter-seat gap** in
`SeatInRuns` (inflate occupied intervals by the gap) + a touch-check in `SeatOverhang`. Kind: a
**law** (lane spacing), applied as a demand in the seat step — distinct from the corner law
(corners stay 0; the mass-level pinch gate owns them).

**F2 — Lanes flush against a branch hub's legs (frontline-less units).** On L/U hubs without a
frontline the wool/spawn lanes can sit flush against the legs' walls. The hub's remaining free
surface is exactly where build regions attach in later stages, so a build region would land
touching the lane. Mechanism: a dock flush against a **non-corner run end** — a run ends
mid-edge only where the body's mass stops, so that end is a leg's wall and gets no inset by
design; the extreme is a **leg-tip run** (width exactly `cw`) the dock consumes end to end.
Probe: **23/3/1/1** units per 200 (small/mid/big/huge), and **every one of them a branch hub** —
so the attribution is exact but the frequency is a small-board effect (23 of the small preset's
37 branch hubs, ~⅔; near-zero elsewhere). Context: branch hubs are effectively
frontline-less-only today — with a frontline, the branch form's front free run (an arm tip,
`cw`) cannot host the `faceWidth` demand, so the allocator falls back to the rectangle (probe:
37/200 branch hubs on the no-frontline small preset vs 4–6/200 on the frontline presets), which
is why F2 tracks the branch-hub population rather than the board size. Fix direction: a ≥1-cell
margin between a seat and a **mass-adjacent run end**; a tip run narrower than `along + 2`
margins refuses the dock (demote / re-seat). **Cost of that rule, measured: it would refuse
166/495 · 159/505 · 246/505 · 195/673 of all current docks (30–50%)** — far more than the 27+2+1+1
it fixes, because the `along + 2` test also rejects every dock on a *full-edge* run of a small
hub. So the margin must be required only at **non-corner** run ends, not at every run end, or the
rule cascades into re-seats and allocation failures on the small preset. Kind: a **law** — the
build-surface clearance ("a cell between a lane and attachable hub surface"), the compose-side
twin of §4's room-clearance guard.

*Adjacent mode the probe separates — measured, and ruled **not a defect** (author).* A lane can
cover a whole hub side end to end, flush at both **box corners**: **101/79/29/37** units per 200,
an order of magnitude more common than F2 proper, and **always a wool** (273/273 whole-side docks
over the four presets; never a spawn, whose length runs outward so it only ever abuts `w` cells).
It is permitted by design — the corner law sets `CornerClearanceCells = 0` precisely so "the
neighbours may use the hub's full edge (which the side-tuck wool and the wide frontline face
want)".

The full dock is **fine in itself**. It reads badly only in a narrow sub-case: when the wool lane
edge and the hub edge run **parallel the whole way** and the two masses combine into a flat slab —
**no bay or notch** formed at their join. Where the wool's own body articulates the join (a w2 lane
widening to its room, the common case — small seed 4) there is nothing wrong with it. The fix, if
that sub-case is ever worth chasing, is a **small frontline** on small boards, not a spacing law.

Not scheduled, and deliberately not an F-entry: it is a **small-board** artifact, and the small
board is low-value — at 700 land the hub is always 4×4 while the smallest wool footprint is
already 4 (side-tuck `I`, `cw + rd × 2·cw`) or 5 (`L`), so the seat is forced, not chosen. It
fades as budgets rise (101 → 79 → 29 → 37) exactly as hubs outgrow the wool minimum. Small-board
layout issues are expected for now.

**F3 — The centred-stub single frontline (the "T").** Two-piece frontlines are always a T: a
tiny stub (reach − cw = 2 cells) centred on the bar — build regions attach poorly around it; a
proper L (arm at an end) or the twin would read better. Mechanism: the single is **centred by
construction** (`SpineArms(cw, [(w−cw)/2], …)` in `FrontlineBoxEmitter.BuildBody`). And the T
*dominates*: probe 126/164 mid, **164/164 big, 161/161 huge** — at `w = 3` the twin needs
spine length ≥ 2·cw = 6 with the arms then adjacent, so it **never fits** the cap-6 face and the
form menu silently collapses to the T (the Bar is never chosen — 0/489 fronts — because
form-answers-form reserves it for branch hubs, which fall back when a frontline exists, F2).
Fix direction: an **arm-placement knob** on the single (end-arm = an L frontline), a twin that
fits `w3` (wider face or an adjacent-arm guard with real separation), and a menu-collapse
**fact** (when only one form fits, that should be visible, not silent). Kind: **menu** content +
**knob**; the collapse is a missing fit-gate fact.

**F4 — Twin legs always equal.** The twin/U frontline's legs are always the same length; an
optional per-leg offset (≤ 2 cells, ~10 blocks, sometimes) would break the symmetry pleasantly.
Mechanism: `BodyEmitter.SpineArms` has one arm length for all arms (`h − cw`). Wrinkle: the face
offer is read off the **box's face edge** (`BoxInterfaces` runs), so a shortened leg's tip
leaves the box edge and its offer would silently vanish — per-leg lengths need per-arm support
in `SpineArms` **plus depth-aware face offers** (or per-tip interfaces). Kind: a **knob** (arm
length; `ClassifyBody` must still read `SpineArms(2)`) + a small offer-model extension.

**F5 — Square hub against a square frontline.** The flat square-on-square pairing still reads
on the mid/big/huge presets. Probe: this is **not** the Bar (never chosen) — it is the near-solid
small forms: at reach `w + 2` and `cw = w` the single-T is a solid bar with a 2-cell stub,
reading almost as a Bar flush on a rect/ring hub (rect+single-T = 109/164 mid fronts;
ring+single-T = 146/164 big). Root: the frontline's **reach and proportions do not scale** with
the board — `frontReach = w + 2` regardless of budget — so on big boards the front is a sliver
whose form barely matters. Fix direction: scale reach with the budget (G104's territory), open
the deeper forms (holed P / two-U-on-I — G100; the F3 L-form single), and make hub-form ×
front-form a real **pairing menu** (today one hard-coded preference in the filler). Kind:
budget **fact** + **menu**. *Small note (author, low weight): a thin front is not always wrong —
an intentional narrow choke that widens again is sometimes wanted on **small** boards (not big
ones), so the eventual rule is per-size, not a blanket minimum. A possible later home is the
fragmentation step (cutting voids into a box shape rather than only converting land→build) —
parked; the rule-based path stays preferred.*

**F6 — The donut wool is a 2:1 sliver.** *(Partly fixed — the `woolAtEnd` half landed; the root
below remains.)* The donut approach reads stretched. Probe was: every donut box exactly **10×5**
(or 5×10) — the min box. `MinBox(Donut)` chains stub (`cw`) + ring (`3·cw`) + trailing wool room
(`rd`) along one axis at minimum height, and the allocator sizes rich wools at exactly the min
box, so the donut was *always* the sliver.

*Landed:* the trailing `rd` is only the **non**-`woolAtEnd` room — the corner-integrated wool sits
inside the ring's own span, costing no width past it — so `MinBox`/`Need` no longer charge it, and
the allocator now picks the corner wool for donuts (`DonutCornerWoolChance`). Those donuts are
**8×5** (aspect 1.6, area 40) instead of 10×5 (2.0, 50); the probe now reports both.

*The other cheap route is closed:* growing the box toward a preferred aspect **cannot be funded**.
Measured per-wool budget share is **4–6 cells (small), 13–24 (mid), 29–44 (big/huge)** against a
donut minimum of 50 — every rich wool is already over its share at its own minimum, on small by
10×. There is no headroom to grow into anywhere, so the only lever that reshapes a rich wool is
its **minimum**. (That gap is itself a finding for **G104**: the two-currency budget badly
under-funds rich wools relative to their footprints.)

*Root, unchanged (author):* the sliver is not *required* — every internal dimension of the shape
(legs, bars, even the hole) is keyed to the **one lane width picked up front** for the whole map,
but in reality widths mix — some areas are 3 wide, some 2 — and **the generation cannot express a
per-area / per-piece width yet** (today's vocabulary is the map `w` plus the single wool-lane
override). Decoupling the non-lane dimensions (the hole, the ring bars) from `cw` shrinks the
donut further. Fix direction: the per-piece width as a **knob** in the emitters (a vocabulary
addition — the same knob **G105**'s asymmetric ring needs). Kind: a missing **knob**.

*(**F7 — the clamp's void too deep** — fixed and removed. `MinBox(Clamp)` had inherited the U's
`2·cw + rd` height, but the clamp has no crossbar to clear: its legs only run from the wool down
to the mouth, so `cw + rd` does it. Void depth below the wool went **4 cells → 2**.)*
