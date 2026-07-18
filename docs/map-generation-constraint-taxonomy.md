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

The one-line test for classifying any new wish: *does it change what can be **picked** (menu),
whether a pick **fits** (fit gate), whether a join is **legal** (dock law: demand / offer /
veto), how a legal join **varies** (knob), what this compose is **aiming at** (target), or how
good the result **reads** (band / term)?*

| Kind | Is | Rejects? | Exemplar today |
|---|---|---|---|
| **fact** | an observation off geometry, no policy | never | `BoxEdgeInterface` ("observes, does not judge"), `FrontlineRuns` |
| **menu** | a generative allowlist — what may be *chosen* | empty = directed signal | `FillMenu.Rows`, `FillProfiles.Families` |
| **fit gate** | does the choice fit the box | directed (`TooSmall`) | `ShapeEmitter.MinBox`, `FillProfiles.Fits` |
| **demand** | a shape's requirement *on its environment* (inbound) | via the gate | `FamilyDock.EntryDemand` (the clamp's 2 entries) |
| **offer** | constraints a shape *imposes outward* — the edges/intervals it invites neighbours onto, and in what groupings | via the gate | **no code home yet** — the G88/G89 gap (§3) |
| **veto** | a never-attach mark | via the gate | `SlotDockRole.NeverDock` (the wool room), CT9's recess |
| **gate** | the hard legality check applying demand/offer/veto, with a *directed rejection* | yes, legibly | `DockingGate` → `DockRejection`, `FillResult.IllegalDock` |
| **knob** | a free parameter *within* legality; never changes identity | guard-railed | `entryShift`, `attachmentWidth`, `SpineArms` arm placement |
| **target** | a **per-request, prescriptive** constraint this compose aims at — chosen or sampled, then held | steers + verifies | **no code home yet** — the "controlled variance" gap (§4) |
| **band** | a **descriptive** envelope measured off the seeds — advisory (the seeds are final-fidelity, see the header corrective) | never (scores distance) | `SoftTerm` + `seed-envelopes.json` (`frontline-count` [1,7]) |
| **hard term** | a well-formedness symptom check on the derived board | flat penalty | `WoolRingedHole`, `GapHopBand` |
| **law** | the id-bearing author rule the mechanisms implement — a **living** rule set amended by protocol, provenance not gospel | n/a (provenance) | `layout-rules.md` FR6, CT9, BZ8 |
| **doctrine** | a meta-rule about where rules may live | n/a | "labels drive, the mirror verifies"; "the evaluator never sees a shape name" |

Two distinctions carry most of the weight:

- **demand vs offer** — direction of the arrow. An approach *demands* (its entry must land on a
  host). A hub/frontline *offers* (its edges dictate where and how wide neighbours may land —
  the "constraint source" of `map-generation.md` §4). Everything the docking machinery models
  today is inbound; G88/G89 are the outbound half.
- **target vs band** — prescription vs description. A band says *authored maps run 1–7
  frontline runs*; a target says *this compose wants exactly 2, connected*. Bands never steer a
  compose (they score it); targets steer first and verify after. Conflating them is why the
  soft-rule backlog stalled: wishes like "a certain amount of connected frontlines" are
  targets, and the band machinery was the only place to put them.

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

1. **Interval facts.** `BoxInterfaces.Of` collapses each bounding-box edge to a flat slot
   list; a twin face is two disjoint intervals on one edge. The facts should be the body's
   **classified boundary runs** (the edge taxonomy, §4 — now code in `BodyEdges`): free runs
   carrying `(range, piece, slot/mark)`, notch/bay/hole walls carrying their space.
2. **Designation marks in the gate.** `DockingGate.Role` is one global slot→role table
   (`entry` docks, `room` vetoes) — approach-only. Dock roles become per-designation **marks**
   (`entry`/`room` for approaches; `face`/`hub-edge` for the frontline; per-edge `interface`
   offers for the hub), stamped by the designation per `shape-vocabulary.md` §8, with the gate
   unchanged in spirit: one table over marks.
3. **Offer records on joints.** A `BoxJoint` knows two boxes share an interval; it doesn't know
   the interval was *offered*, at what width class, in which grouping. The partition graph
   carries the offer so the partitioner places consumers against it (and `BoxPartition.Of`
   mirrors it back).

Never key any of it on a letter: "F" is a placement read of `SpineArms(2)` and drifts as the
arms slide (§3 of the shape vocabulary). The offer is stated over structural slots + marks
("the arms' outward end-edges"), so it survives every knob — and covers Π, T, E, and the plain
Bar with the same sentence.

## 4. The edge taxonomy — negative spaces, wall counts, offerable surface *(shipped)*

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
1. **Adopt the vocabulary** (doc-only). The §1 kinds + the §4 edge terms land in
   `map-generation.md` §1 as locked terms (offer, target vs band, demand, veto, knob; the
   wall-count classes); prompt templates (§7) ride along. Retire the matching sections here.
2. **Interval facts** (small code). `BoxEdgeInterface`'s facts re-ground on the classified
   boundary (§4): free runs carry `(range, slot/mark)`, walls carry their space class;
   `DockingGate` verdicts unchanged — byte-identical wool/spawn output is the acceptance bar.
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

## 7. Prompting future rules — templates

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
