# Generate tool — measurement & scoring reference

This is the consolidated "how scoring/measurement works" reference for the Generate tool. It
merges four previously separate docs, in this order: the evaluator direction doc (the conceptual
model), the deriver's measurables and cost-function catalogue, the hand/tool-measured seed
statistics those terms are calibrated against, the generated soft-term envelope bands, and the
traffic-ground-truth pipeline (a related, still-separate effort to mine ground truth from real
match logs). `docs/tools/generate-rules.md` (the frozen rule law) and `docs/tools/generate.md`
(the canonical pipeline/model doc) remain separate and are cross-referenced throughout, not
duplicated here.

Tags used below: **[decided]** settled, **[open]** an author call is pending, **[later]**
deliberately deferred.

## 1. The evaluator model: derive structure, judge by property

> **Terminology + model:** `docs/tools/generate.md` is canonical for the pipeline, the two
> derivers, roles, and the score model. This document owns the detailed deriver-measurable and
> evaluator-metric catalogue (§2), plus the measured evidence that calibrates it (§3–§5).

This is the direction doc for the `G`-series composer track. It reframes layout generation around
an **evaluator** (a critic that scores a `plan.json`) instead of a generate-and-test sampler, and
pins the model that makes the evaluator authorable: **author intent · derive structure · judge by
property.** It sits above `docs/tools/generate-rules.md` (the frozen rule content the evaluator
scores against), `docs/tools/generate.md` (the canonical model), and the measured seed statistics
gathered in §3 below (the measured envelopes the soft terms use). It follows `generate.md`'s role
model (§1.4, §3): structural roles are *derived*, not authored — and derived cautiously (§2.3
below): the evaluator keys off **measurable** quantities, not named roles the author is not ready
to pin.

### §1. Why an evaluator — the diagnosis

The composer today is **generate-and-test**: sample structural params in a fixed draw order → local
repair (the grower's shrink/inflate) → reject-and-resample on a hard-invariant failure → an acceptance
gate on the assembled plan. It got us the twelve-seed fidelity and the corpus reading, and for *local*
properties (a lane width, a marker inset) it is fine. But every open failure mode shares one signature —
they are **global, relational** properties a local, sequential generator cannot guarantee and rejection
sampling cannot efficiently repair:

- **G39** (band ↔ frontline must interlock) — a relation between two pieces' edges.
- **G40** (enclosed dead-space ≤ ~10×10) — a property of a *region bounded by several pieces*.
- **G42** (spawn docks, never submerges) — a property of a piece relative to the whole merged mass.
- **G44** (spend surplus structurally, not as length) — not a constraint at all; an **objective**.
- **G45 / G37** (multi-access, no lone dead-end) — a property of the *route graph* (a loop), not a piece.

**Is it "rules not hard enough"?** Partly, and the distinction is load-bearing. G39 genuinely *is* a
too-soft rule (the band may land flush on one edge; the examples never do) — tighten it, no model change.
But G40/G42/G44 are **not** soft rules: they are missing objectives and missing global constraints, and
hardening a *local* rule cannot produce them. Worse, adding them as reject-conditions makes it worse —
every hard constraint bolted onto a fixed sampling distribution drops the acceptance rate and shrinks the
feasible region to a sliver the draw order rarely hits (this is the p5 infeasibility, the hole-hunt, the
draw-order coupling). The tell: **the rules are written as an acceptance oracle — a checker — but a
generator cannot invert a checker.** `generate-rules.md` says beautifully what a *good result* looks like;
the sampler reaches results by local moves and then checks. When the property is global, "check-and-reject"
and "construct-and-hope" both fail. The rules are not too soft; they are in the wrong *form*.

**The inversion.** Move *all* the rules out of the generator and into one **evaluator**: a pure function
`plan → (score, [violations])`. The generator becomes dumb and swappable; the evaluator is the durable
asset. Hard rules become large penalties; "feel" becomes distance from an authored-set envelope; the
objective (spend structurally, not length) becomes a term with a place to live. This subsumes **G43**
(composer ↔ example-set conformance metrics): G43 *is* the evaluator's soft half.

We can *recognize* bad layouts far more reliably than we can *enumerate* good ones. That asymmetry is the
whole reason to build a discriminator (critic) rather than a complete generative grammar: the critic is
**additive and never has to be complete** — you keep adding terms as you find failures, and a new term
never tanks an acceptance rate. No reinforcement learning: the evaluator is a hand-written, inspectable,
deterministic cost function, tuned against a labeled set.

### §2. The architecture — three layers

| Layer | What it is | Where it lives |
| --- | --- | --- |
| **Author intent** | the irreducible input a machine cannot recover | `plan.json` (frozen format) |
| **Derive structure** | the structural roles + topology, computed | the *deriver* (in-memory, never serialized) |
| **Judge by property** | metrics vs rules + authored envelopes | the *evaluator* (score + violations) |

Everything the earlier design wanted to *author* (frontline, hub, lane, mid) moves to **derive**;
everything the rules want to *check* moves to **judge**. `plan.json` stays exactly as the author-intent
layer. The confusion — "am I sure whether this is a frontline or a hub?" — is the signal: the things you
are unsure how to label are exactly the things you should not be labeling; they are derived.

## 2. The deriver's measurables and the cost-function catalogue

### §3. The substrate — `plan.json` unchanged, read as tiles

`plan.json` is **frozen [decided]**. It already *is* the author-intent layer:

- **Geometry on the 5-block cell grid** (`pieces[].rect` in cells) — every piece is a union of 5×5-block
  tiles by construction.
- **Height** (`pieces[].surface` + `globals.surface`) — full block resolution, per piece.
- **Intent markers** (`placements.wools` / `spawns`) — the objective + spawn anchors.
- **Deliberate voids** (`zones[].holes` + `buffer` pieces) — on-purpose emptiness.
- **Override channels** (`cliffs`, `walls`) — authored refinements over what the deriver would guess.

The evaluator is a **new read-only consumer**: `plan.json → rasterize to 5×5-cell tiles → derive roles →
measure → score`. The tile field and every derived role are computed views, never written back.

#### Resolution [decided]

- **Atom = the 5×5-block cell** — the grid `plan.json` already uses. Existing seeds are *already*
  tile-layouts at this resolution; adopting it is a reinterpretation, **not a migration**. A coarser 10×10
  atom would distort odd-sized authored pieces (a 2×3-cell room, a 3-wide turn) and corrupt the exact
  labeled ground truth the evaluator depends on — rejected.
- **The "10×10 piece" survives as a derived region + a soft preference**, not a grid law. Fine grid (5×5)
  = storage; region lens (~10×10, a lane ≈ 2 tiles wide, a room ≈ 2×2) = how the *rules* think. The
  regularity is a cost-function bias, never a constraint that rejects a deliberately irregular piece.
- **Authoring is by shape, not by tile.** The author drags a rectangle; the tool fills the cells under it.
  Granularity is an internal detail the deriver/evaluator/search do not feel (they work on *regions*).

#### Height is orthogonal [decided]

Footprint quantization does not touch height. Height is a **per-tile attribute** at full block resolution.
A frontline **tower** is a tile on the frontline edge with a tall surface; a **raised wool** is a room tile above its
approach; a **stepped approach** is a monotone run of surfaces. "Purposeful, not random" is an *evaluator*
concern (§6 below); height that correlates with a derived role is purposeful; height that does not is what
"random" means. The heights already annotated in the seeds are **training data for the height envelopes**,
not a liability the tile model must swallow.

### §4. Authored vs derived

**Authored** (irreducible intent, or a topological fact only the author knows the *purpose* of):

- filled-vs-empty geometry (the piece rects);
- `wool-room` / `spawn` regions + their objective markers;
- deliberate voids — `zones[].holes` and `buffer` pieces (the author asserting "I meant this void");
- per-tile surface (height);
- overrides — `cliffs`, `walls`, and (§5) any manual role correction.

**Derived** (computed, never authored) — split into locked *measurables* and provisional *labels* (§5):

- measurables — islands + their anchor role, marker branches (lane runs), junctions + per-objective
  approach count, axis position, build-interface counts, void topology (hole vs spacing, declared vs not);
- labels (provisional, over the measurables) — frontline (an *edge attribute*), the residual (unnamed),
  middle islands / stepping stones;
- height-roles — tower / raised-room / climb (§6).

**Roles stay minimal [decided].** `plan.json` roles are: anonymous `piece`; the intent-bearing
`wool-room` / `spawn`; the annotations `buffer` / `connector`. The retired legacy roles `lane` / `hub` /
`mid` / `frontline` **must not come back** — they are derived, so they must not live in the file. A
minimal role set is the correct expression of "derive structure," not a gap.

The **`connector`** role is a *template-composition* concept, not an evaluator one: in a full layout the
attachment point is derivable (where a marker branch meets the rest of the island); a `connector` only
earns its keep for a *fragment* (a reusable lane template with a dangling edge that has nothing to derive
its plug-point from). It stays out of the full-layout labeling loop; it returns if a stamp-templates
generator is built (§8).

### §5. The deriver — structure from markers + geometry

The deriver computes structure from the tile field + the markers ("peel the marker branches; the rest is
the residual"). Its output is **two tiers, and the split is the point:** a small set of **measurables** is
locked (pure topology/position — the evaluator keys off these), and a set of **labels** are provisional
names laid over the measurables for readability. The author can pin some structure crisply and rightly
refuses to pin the rest, so the model leans on what is measurable and treats the names as soft.

#### §5.1 Locked measurables (pure functions, no naming judgement)

1. **Island** — a land-connected component (flood fill). Connectivity, **not** shortest-path — a
   land-connected back piece *is* island; a piece across a void with its own marker is a *separate* island
   (the WL4/SP6 isolated wool/spawn). (Route/spine is a different derived thing, for traffic — don't
   conflate.)
2. **Island anchor role** — by the intent it carries: **team** (holds a spawn), **objective** (holds a wool
   but no spawn — the isolated-wool island), **neutral** (anchorless, intersects a build region),
   **decorative** (anchorless, outside any build region — excluded from scoring). Read from the **authored
   `spawn` / `wool-room` piece roles** (the strongest signal — the author explicitly marked the region) *and*
   the spawn/wool markers, so a room piece is recognised even before its marker.
3. **Marker branch (lane)** — the approach a marker owns. For a **wool** this is the **wool-lane stack**
   (§5.1 6c / §5.3): the terrain stacked from the room's redstone interface out to void / build / a T. Spawns
   do **not** stack an objective lane (their approach currently falls into residual — a **[later]** carve-out).
   The readable name is a "lane"; the tile set is the measurable.
4. **Junction + approach count** — a cell where ≥2 marker branches coincide (share an origin area). Its
   value is a **count, not a name**: the number of distinct branches meeting on the way to an objective is
   that objective's **approach count** — the multi-access measure the evaluator wants (WL8 / G45). *(You
   were clear on "junction = where lanes coincide" while wary of locking the term — so it lives here as a
   measured count, not a role.)*
5. **Axis position** — each piece's distance to / straddle of the symmetry axis, from cell coordinates.
6. **Build interfaces** — per island/piece, the count and total width of edges touching a build region.
6a. **Intra-team bridge** — a build region that lies on a team's own internal spawn↔wool route. Two forms:
   the **direct** bridge (a region joining a team's spawn-holding and wool-holding island); and the **chain**
   spawn ↔ *stepping stone* ↔ wool, where the intervening island is **CAPTIVE** to the team — every build
   region touching it stays single-team, so no enemy can reach it. A neutral island that any *second* team can
   also reach (a contested middle island / tower) is **not** captive, so a region bridging out to it stays a
   frontline. Formally: build the graph of a team's islands joined by single-team regions, keeping only route-
   eligible nodes (captive islands, plus spawn/wool anchors which may also face the shared mid); a single-team
   region **every** island of which is route-eligible (it never bridges out to a contested island) and whose
   component holds both a spawn and a wool qualifies. This admits the **direct** bridge (two anchors) and the
   **chain** (through a captive stepping stone) — both **two-piece gaps** that span *distinct* islands. It
   cleanly separates a **team stepping stone** (on the spawn↔wool movement path, captive) from a **neutral
   stepping stone** (contested, reachable by more than one team — a frontline island, §5.2). Kept as its **own
   signal**, not just an exclusion: it marks a deliberate **internal gap** — a piece chopped off the main mass
   and bridged back across a slow-down void (the CT5 isolation cut) — so it teaches the builder where
   intentional slow-the-attacker gaps and standalone lanes come from. Rendered distinctly (**pink dashed**) and
   counted per plan.
6b. **Self-bridge notch** — the same route test, but the region touches only **one** island: a build pocket
   carved *into* a single landmass, its two walls the same piece (`mirror-big-board`'s spawn wraps a void this
   way). Structurally distinct from 6a — it does not *gap two pieces*, it *shapes one* — so it is reported as
   its **own** signal (**cyan dotted**), separate from the intra-team count. Still an internal, non-frontline
   feature (its walls face only the team's own land), and still a learnable authored move: a piece hollowed to
   route players around a pocket rather than a lane cut between two pieces.
6c. **Wool lane** — the terrain a wool room owns as its approach, read off the terrain shape. The room
   **interfaces** with terrain along an edge (where the generator stamps the objective's **redstone line**);
   **stack** that interface straight outward as a fixed-width band, cell by cell, until void, a build region,
   or a **T** — a crossbar, where terrain reaches beyond the band on **both** sides (a one-sided jut is only a
   side branch and does *not* stop the stack; the long piece is the lane, the crossbar is crossing terrain that
   stops the overflow). A **two-sided** room stacks both ways (`four-team-wool-two-sided`: down 12 + right 8 =
   20/team, **80** total). If the forward stack immediately **dead-ends into void** (≤ a bar's thickness), the
   room is docked against the **side** of a lane (an L, not an I) — stack that lane along its **own axis**
   (perpendicular) instead, so the whole I the room docks against is the lane (`isolated-spawn`: the wool docks
   the side of an 18-tile I, ×4 = 72; `base-2wool`'s L-shape room stacks its bar to 8). A stack stops at **any**
   build (frontline or intra alike) — a wool lane *may* run to a frontline (`isolated-spawn`), so there is **no
   "never-frontline" rule**. **Only wools stack a lane — never spawns.** Validated to the tile on both grounding
   seeds (`base-2wool` **44** = long-I stack 14 + L-shape 8, ×2; `four-team-wool-two-sided` **80**); the
   crossbar stop also drops the T-bar overflow on `odd-facing-three-wool` (100→40) and
   `isolated-spawn-approaches` (24→16, keeping the 2×4 stem, dropping the crossing bar). The redstone interface
   line and the lane wash are both rendered; the tile count is reported per plan.
6c-shape. **Wool-lane shape** — the corridor's *topology*, read team-locally (k=0 unit only — a fanned mirror
   image would merge into the corridor near the centre). Seed at the room's terrain neighbours; the corridor
   width **W** is that first cross-section; a cell is a **junction** if it sits in a filled **(W+1)×(W+1)**
   block — a region wider than the corridor (this survives corners, where two W-wide arms overlap in only a
   W×W square, and stops at any wider hub). Flood the non-junction terrain from the room = the corridor; its
   **reflex (concave) corners are the bends**: 0 = **I**, 1 = **L**, 2 = **Z**, ≥3 = **complex** (the wool sits
   on a chunky island, not a clean lane — `odd-facing`'s 400-block L-island). `plaza` = a chunk right at the
   room; `none` = no terrain corridor (the isolated whole-island room — the SpawnWoolRooms bug). Validated on
   ground truth: `p16 s19` **Z**, `p16 s42` **I/L**, `rotate-wide-frontline` **Z** (authored Z lanes the
   objective-stack in 6c cannot trace), `isolated-spawn` **I**. This is the deriver's first **shape** vocabulary
   term — the label the intent→realize mirror needs to say "make an L-cut lane." **v1 caveat:** on small maps
   with no distinct hub (all-2-wide) the flood has nothing to stop at and over-reports `complex`; the robust
   fix is a medial-axis (thinning) trace, deferred. Per-wool shape+width is reported per card.
6d. **Build-zone kind** — a build region typed by **what it links**, read straight off the island incidence we
   already have (nearly free). A **team-owned** island (anchored spawn/wool, or a captive *team* stone)
   contributes a team "frontline" endpoint; a **neutral** stepping stone contributes a neutral endpoint. Then:
   - **intra** / **self** — the same-team internal cuts (§6a/§6b), found already.
   - **front↔front** — ≥2 teams: the crossing / direct team link. It may **carry neutral stones sitting inside
     it** (the CT5 fragmentation count, now attached to the specific zone — `rotate-wide-frontline`'s wide
     crossing holds **7**; `mirror-big-board`'s holds 4).
   - **front↔neutral** — one team + a neutral: a team's **bridge toward the mid**.
   - **neutral↔neutral** — only neutrals: a **mid-internal** link between neutral islands (usually crosses the
     symmetry axis — the one edge type that legitimately spans it).
   Every region in the corpus falls into exactly these; no leftover. From the zone grammar the **CT mid-form**
   (§, `generate-rules.md` CT) **derives for free**: any *neutral↔neutral* zone ⇒ the mid is fractured into
   interlinked islands ⇒ **hash**; else ≥2 *front↔front* crossings ⇒ **parallel**; a single crossing ⇒
   **channelled**. Validated against the frozen CT labels — `big-board-…-parallel-mid` → **parallel**,
   `four-team-towers-big` → **hash** (CT's stated archetype), `isolated-spawn-approaches` → **hash** ("hash with
   parallel traits"), the single-crossing seeds → **channelled**. Cross-checked against the **zone↔hole
   adjacency** (§7 below): a hole's bordering zone-kinds explain its class — a *gap* hole is ringed by intra/self, a
   *middle* hole by front↔front / neutral↔neutral; and a middle hole's **parallel ways** = the crossings ringing
   it (`big-board`'s central hole is flanked by **2** front↔front lanes; `four-team-towers-big`'s centre is
   ringed by **4** neutral↔neutral links). Zones are tinted by kind and the mid-form badges each card.
6e. **Zone width & interface width** — two width primitives per build zone, the missing key to the BZ *fit*
   family. **Zone width (BZ3)** is the corridor's narrowest cross-section: per cell the shorter of its
   horizontal/vertical run within the region, then the MIN over the region (a choke necks the whole zone). In
   cells, ×5 = blocks, so BZ3's **10-wide dominant = a 2-cell bridge**; the corpus buckets **choke** ≤1 (5,
   `base-4team`'s plus-arms, `mirror-tiny`), **bridge** 2 (10, the mode — nearly every intra/front↔neutral
   zone), **band** ≥3 (15+, `big-board`'s parallel lanes, `four-team-towers`'s neutral↔neutral links).
   **Interface / edge width (BZ8)** is the contact run where the zone docks each island — a nearly-free read of
   the shared border we already compute. Together they are **BZ9 fit**: a corridor much wider than its
   interfaces (`isolated-spawn` w4/if2) or an interface far wider than the corridor (`rotate-wide` w2/if6-24)
   are the over/underfit signals — the *verdict* is left to the cost function, the deriver reports the raw
   `width` / `ifaceMin` / `ifaceMax` per zone. Bonus: for a front↔front **band** the interface width **is the
   frontline width** (`mirror-big-board` if6-10, `big-board` if3), seeding FR6 (split vs wide) for free. This
   turns **BZ3, BZ8, BZ9, MD2** (gap-per-hop) from derivable-but-unwired to measurable, and the BZ3 bucket
   shows per card.
7. **Void topology** — a hole is **true void** (empty, non-buildable) the border can't reach without crossing
   **terrain or a build region** (both are walls for the enclosure flood): enclosed → **hole**, border-reachable
   → **spacing**. Build must wall the flood, otherwise a rotation pocket ("rotary device") near the frontline —
   encased by twin frontlines on some sides and the mid build band on the others — leaks to the border through
   the band and is missed. **Every** enclosed void is reported, **at any size** — the seeds carry intended holes
   as small as 1×2 cells (`mirror-tiny-map-cliff`, `rotate-wide-frontline`), so no size threshold may override
   the corpus (`generate-rules.md`'s "~10×10" is a *generation* norm, never a *detection* filter). Cross against
   the authored deliberate-void marks (buffer / `zones[].holes`) to split **declared** from **undeclared** (a
   deliberate CT8 pocket vs an accidental enclosed void — a top evaluator term, §6 below).
7a. **Hole position class** — *where* a hole sits, read purely off **what its boundary touches** (never size).
   Two derived discriminators, both already computed: how many **teams** the boundary reaches (team ownership),
   and — when it touches build — whether that build is a **frontline** (the contested crossing) or **intra/self**
   (a team's own isolation-cut, §6a). **Only anchored terrain (spawn/wool) confers team ownership** — a *neutral*
   stepping stone does not: it has no real team (its orbit-image label is arbitrary, and a centre island shared
   by both images carries a single fixed value), so counting it would mis-read one image of a mirror pair as
   contested — e.g. `isolated-spawn-approaches`'s four ring holes are all **frontline** pockets, but the shared
   centre stone made one image of each pair look 2-team until neutral terrain was excluded. This places every
   hole on one interior→contested spectrum:
   - **encased** — one team's terrain, **no build** boundary: a bubble deep inside a team's landmass
     (`big-board` spawn bubbles, `four-team-wool-two-sided` wool bubbles, `rotate-wide` 2-cell pockets).
   - **gap** — one team, build boundary **all intra/self**: a hole in that team's isolation-cut gap — it marks
     where a lane was chopped, so it ties straight to the intra-team bridge (`rotate-wide` `[7 intra]`,
     `mirror-big-board`, `odd-facing`).
   - **frontline pocket** — one team's terrain but the boundary **touches frontline build**: the team's exposed
     edge on the crossing (`four-team-towers-big`'s corner holes, `mirror-tiny-map-cliff`).
   - **middle** — reaches **≥2 teams**, or floats in **pure build**: the contested crossing / arena (`big-board`'s
     central 72-cell void, `four-team-towers-big`'s 4-team centre, `four-team-wool-two-sided`'s mid-band pockets).
   Orthogonal to declared/undeclared (§7): a hole carries **both** a position class and a declared flag, so the
   buffer worklist reads as e.g. "an *undeclared* **gap** hole" — far more actionable than a bare void. Validated
   by boundary composition across the corpus; rendered in four colours per class.

#### §5.2 Provisional labels (readability over the measurables — the evaluator prefers the measurable)

- **Frontline — a boundary *attribute*, not a piece** [decided that it is an attribute; **scope [open]**]. The
  **contested edge**: where a landmass meets the middle build zone, "where players mainly meet." It is fuzzy by
  nature (the author's own caveat) and depends on the mid — if the middle is one open build area with no islands,
  the frontline is simply whatever land touches it; if the middle is islands-in-build, it is the land facing the
  crossing. Modelled as a per-edge flag (which land edges face the mid void), so it composes with any piece: no
  "frontline piece" to segment, and no conflict when a wide face — or the residual's own bulk — borders the void.
  It is strictly an **outside** edge: the neighbouring cell must be buildable **and empty** (the crossing void) —
  an interior seam between two pieces is never a frontline, even where an author draws a big build rectangle that
  overlaps the terrain on both sides (a zone overlap must not manufacture a frontline). **Scope [decided]: an
  island has a frontline only if its border is VOID-DOMINANT** — more void-border than build-border. Such an
  island is exposed territory whose build-facing edges are its frontline. A **build-dominant** island (mostly
  surrounded by build — a mid stone embedded in the band, e.g. `base-2island`'s 2×2 stones at 6 build / 2 void)
  and a **pure-void** island (floating) are **stepping stones** with no frontline. Touching *some* void is not
  enough (the embedded stones do) — the border must be *predominantly* void. Not gated to spawn-bearing islands:
  an objective (isolated-wool) island is void-dominant (`isolated-spawn`, 4 build / 22 void) and keeps its
  frontline. **Across the whole corpus, void-dominant ≡ anchored (holds a spawn or wool)** — the geometric test
  and ownership coincide, which is the validation that this is the right cut. **Intra-team interfaces are
  excluded [decided]:** a build region on a team's own internal spawn↔wool route — direct, a chain through a
  **captive** stepping stone (§5.1 6a), or a **self-bridge** notch touching only that team's own island (§5.1
  6b) — is *not* a frontline; its edges are re-tagged from the front set, and — because a captive stepping
  stone is itself build-dominant — its *own* edges to that region are collected too even though the stepping
  stone would otherwise carry no frontline. The two-piece bridges are drawn **pink dashed** (intra-team); the
  single-island notch is drawn **cyan dotted** (self-bridge) and counted separately. Validated to the block:
  `base-2wool` 18→10 front (−8, +8 intra), `four-team-towers-big` 52→28 front (−6×4, +24 intra), `base-4team`
  unaffected (no separate wool island → no bridge), the chain case `rotate-wide-frontline` 44→28 front (−16,
  +32 intra: 16 re-tagged former frontlines + 16 formerly-unannotated stepping-stone edges), and the
  self-bridge case `mirror-big-board` 24→12 front (−12, +12 **self**: the spawn island's own wrap-around notch,
  6 edges × 2 teams — kept out of the intra count, which stays 48). Each generalization is byte-identical to its
  predecessor on every *other* seed — the chain rule only adds rotate-wide's stepping-stone regions, the
  self-bridge rule only adds mirror-big-board's spawn notches.
- **Residual — deliberately undefined, and now literally the remainder** [decided]. Every terrain tile gets
  exactly **one** label, by priority: authored **wool-room** / **spawn** piece → **stepping-stone island** →
  **wool lane** (§5.1 6c) → **residual**. Residual is simply the terrain no specific label claimed — there is
  **no branch↔residual erosion** (retired; see §5.3). The model does **not** name residual "hub" or fix its
  identity: it can be a plain square, a square with a hole, a square with several holes (an "Eight"), or
  something else. The evaluator only *bounds its shape properties* (§6 below); it never requires a shape. *(Per the
  author: "I would not define hub at all yet — it's literally the remainder.")*
- **Middle island / stepping stone** — a standalone island sitting in / touching a build region (spoken of
  as just "an island"; the term "stepping stone" is fine). **Geometrically [decided]: a build-dominant island
  (border more build than void — embedded in the crossing) or a pure-void (floating) island is a stepping stone**
  (it has no frontline; see the frontline bullet). On the corpus this is exactly the anchorless islands.
  Two sub-kinds, now told apart **geometrically** by the *captive* test (§5.1 6a): a **team stepping stone**
  is CAPTIVE — every region touching it stays single-team, so it sits on that team's own spawn↔wool route
  (players move spawn ↔ stone ↔ wool) and its edges register as **intra-team**, not frontline; a **neutral
  stepping stone** is reachable by more than one team (a contested centre island / tower, typically on or near
  the symmetry axis) and is *not* captive, so it keeps its frontline edges. This is a cleaner cut than the
  earlier "axis proximity + interface count" heuristic — reachability decides it. (`rotate-wide-frontline`'s
  isl11/isl13 are captive team stones; `four-team-towers-big`'s towers are contested neutral stones.)
  **A stepping stone is a whole island, so it is labelled as one — never split.** An anchorless island is
  coloured as an island by its kind (**stone-gray** neutral, **fuchsia** team) and counted per plan. Validated
  to the count across the corpus:
  `mirror-tiny-map-cliff` 3n/0t, `odd-facing-three-wool` 4n/0t, `rotate-wide-frontline` **7n/4t** (the four
  spawn↔stone↔wool stones), `isolated-spawn-approaches` 3n/0t, `mirror-big-board` 4n/0t, `four-team-towers-big`
  4n/0t, `four-team-wool-two-sided` 4n/0t, `base-2island` 2n/0t, `base-2wool` 2n/0t, `base-4team` 4n/0t. Every
  team stone requires a **separate wool island** to route to, so a seed whose spawn and wool share one island
  (or that has no wool island) yields 0 team stones.

The **mid** itself ranges from *one open build rectangle over the void* (players bridge freely) to
*islands nested in / bordering the build regions* (channelled crossings); the residual may legitimately
border the build region in the open case, which is exactly why "frontline" is an edge attribute and the
residual stays unnamed rather than being split at that border.

#### §5.3 Lane vs residual — resolved by the wool-lane stack [decided; erosion retired]

The old plan segmented lane↔residual by a morphological erosion (a branch is ~2 tiles wide, the residual is
the eroded core, "the one hard knob"). It **did not work**: the residual grew from the centre of a mass while
the "branch" nibbled the edges, so a big board like `big-board-…-parallel-mid` reported ~12 lane tiles — clearly
wrong. It is **retired.** The lane a wool room owns is now derived directly and correctly by the **wool-lane
stack** (§5.1 6c: stack the redstone interface out to void / build / a T, both ways for a two-sided room, along
the docked axis for a side-dock). **Residual is then simply the terrain no specific label claimed** — not a
room, not a stepping-stone island, not a wool lane. No threshold, no knob. (Spawn approaches are not yet a
label, so for now they fall into residual; carving those out the same way a wool lane is stacked is a natural
**[later]** step — but spawns deliberately do *not* stack an objective lane.)

#### §5.4 Derive-then-override [decided]

The deriver *proposes* every label; the author *corrects* only the few it gets wrong (a `labels` override
channel — an optional side-fixture, not part of a normal plan). So an ambiguous label is never a decision the
author must make up front. The corrections are the **test set for the deriver itself** — the disagreements are
the only labels ever produced by hand. **First instance, live:** the wool-lane-shape training set in
`tools/deriver/lanes/` — hand-authored `mirror=none` single-lane examples + a free-form `labels.json`, checked
by `tools/deriver/lane-audit.cs` against the shared `WoolLaneShape` classifier (author ↔ deriver diff, a `FIX`
list of every mismatch). Where the author's label has no matching classifier term, that mismatch is the signal
to extend the vocabulary (§6c-shape).

**Payoff:** every existing seed and every future hand-drawing becomes a labeled example with *zero*
annotation — draw geometry, drop two markers, mark deliberate holes, run the deriver. And the deriver is
half the evaluator: most rules are "the residual has ≤N holes," "the wool lane is ≤L tiles," "the objective's
approach count ≥2" — once the measurables are computed, the property checks are one-liners.

### §6. The evaluator — the cost function

Form: `score = Σ hard-penalty(violated well-formedness) + Σ w · envelope-distance(metric)`. Hard rules are
large penalties (a valid layout has none); soft "feel" is each metric's distance outside the authored-set
range from the measured seed statistics (§3 below). "Feels right" = "lands in the authored distribution." The
evaluator returns the score **and the list of violated terms** (each citing a `generate-rules.md` id) so a
failure is legible and a generator can act on it.

Starter property terms, grouped by the measurable they read (each ties to a frozen rule id):

- **Global** — symmetry orbit exact; island count = orbit order (CT1); land budget within ±20% (G8); fill
  ratio in the corpus band (0.32–0.60); **every enclosed void either declared or penalized** (an
  *undeclared* enclosed void = a suspected accidental hole — one of the highest-value terms).
- **Mid** — clean band spans the axis (CT1); a hole per side is the default, holelessness the exception
  (CT8); stones inside the band (MD4), two-column grid on wide fronts (MD6); band clears every wool by ≥2
  cells (BZ6).
- **Frontline (the void-facing edge)** — the team land's void-facing edge docks the band **flush** and
  **full-face**, split-vs-wide (FR6); its edge snaps to the mid corner lines and the shared interval
  coincides (**G39** — the interlock term); readable connector extrusion on a long-face dock (BZ8); no void
  overflow / underfit (BZ9). All measured on the edge attribute, not a "frontline piece."
- **Residual (unnamed)** — bound its shape *properties* only, never require a shape: hole count and aspect
  in the authored range; L/Z compositions allowed (HB4); plaza-widening scales with budget (HB1/HB3).
  Penalise nothing for *being* a plain square — only for landing outside the authored shape envelope.
- **Branch / lane** — width 10 (15 on big maps, LN1); max collinear chain ≤50 blocks (LN2); wool at the
  far/back end inset ~5 (WL1); **largest enclosed void a branch wraps ≤ ~10×10** (**G40**); **absolute
  length capped to the authored norm, surplus routed to width/plaza/more routes, not length** (**G44**).
- **Approach count (from junctions)** — each objective's branch-count on the way in ≥2 where multi-access is
  wanted; a lone dead-end (count 1) is the defender-holds-the-mouth anti-pattern (WL8 / **G45** / **G37**).
- **Spawn** — wool reachable from the frontline edge *not through* the spawn (SP1); near the back of its lane
  (SP2); faces the enemy by default (SP3); **docks by a readable edge, never interior to the merged land**
  (**G42**); iron beside/ahead, never behind (SP7); isolated-spawn allowed at ≥10/team (SP6).
- **Objective / wool** — wool↔spawn ≥20 (WL2); wool↔wool ≥45 (WL7); flat plateau covering ≥ the 8×8 stamp,
  edge-to-edge (WL3); 1–3 wools, each on a distinct lane (WL6); a third wool is rare and a real route, not
  crammed by the spawn (G45).
- **Height (purposeful, not random)** — surface deltas are multiples of 2 (EL1); ≤2 raised sections per
  island (EL4); a Δ≥4 full-width seam is marked a cliff only when it qualifies (EL6); **wool room ≥ its
  approach** (a real climb, WL5); a **tower** is a tall tile on the frontline edge that clears the void; and the
  cross-cutting term — **every raised tile must be explained by a derived height-role** (room / tower /
  step); unexplained elevation is the definition of "random" and is penalized. Match the authored
  raised-wool and tower-height distributions.

The thresholds are **not** invented here — they come from `generate-rules.md` (hard) and the measured stats in
§3 / the generated envelopes in §4 (soft). This doc fixes the *form* and the *catalogue*; the numbers stay in
those two sections.

### §7. The evaluation set — the real deliverable

The evaluator is *correct* when it ranks a labeled set the way the author does. That labeled set is the
asset you keep growing:

- **Positives** — authored good layouts (the seeds, plus new ones), auto-labeled by the deriver.
- **Negatives** — flagged bad layouts. The most valuable are **minimal pairs**: a good layout and a
  near-identical bad one differing in *exactly one* property (a band shifted one tile; a lane wrapping one
  too-big void). A minimal pair isolates the single term the cost function is missing or mis-weighting —
  worth more than ten unrelated positives.
- **Coverage** — the gap the author already senses: examples per **sub-problem** (mid / frontline / residual /
  lane / spawn / objective) × per **rotation mode** (rot_180 / rot_90 / mirror_x / mirror_z / none). The
  authored frontline + bridge sets exist; the other cells are the shopping list.

The 350-map corpus is **not** this set: it is unlabeled in plan-model semantics and its quality is mixed. The
authored examples — small, high-quality, labeled by intent — are the ground truth. The traffic pipeline
(top-level §5 below, "Traffic ground truth") may promote a *few* real maps into labeled layouts; do not
block on it.

### §8. Where the generator fits [later]

Build the evaluator **before** the generator — the opposite of today, where the rules are entangled in the
generator. `plan.json` is the interface for the whole loop: the generator *emits* it, the evaluator *scores*
it. It is the chromosome (search) or the solution (CP). Once the evaluator is trustworthy, the generator can
be, in increasing order of effort:

1. the current constructive grower, **ranked** by the evaluator instead of gated by hard rejects;
2. **local search** (hill-climb / anneal) over structural params using the evaluator as cost — the smallest
   change that attacks G44/G40 at the root (search *toward* the good region, not reject-and-hope);
3. **CP-SAT** over the relational skeleton (non-overlap, full-corridor adjacency, interlock equalities,
   void caps, reachability as constraints; budget as objective) — turns G39/G40/G42 from improbable into
   impossible; deterministic under a fixed seed, so goldens survive;
4. a **cyclic graph grammar** for multi-access — loops/alt-routes by construction (fixes the lone-dead-end
   G37/G45 at the source, rather than sampling and hoping).

**Shapes are a generator concern, never an evaluator one.** Named lane shapes (I/L/U/Z) are a palette to
*propose* from; the evaluator stays shape-agnostic (§6) so it can also bless a good layout no template
produced. Do not encode a shape whitelist in the evaluator — that is the enumeration trap in a new hat.

### §9. Build order

1. **Tile reading** — rasterize `plan.json` to the 5×5-cell field (occupancy + role + surface + buildable).
2. **Deriver** — structures from markers + geometry, with the width/branch cutoff; a debug render of the
   derived labels over a plan. *(Status: v1 landed as a review tool — `tools/deriver/derive-gallery.cs` fans
   each seed to the full board and renders islands + anchor roles, the branch/residual erosion split, per-wool
   approach counts, the frontline edge, and undeclared voids. Known-rough: the branch/residual cutoff
   over-calls residual on big/wide boards — the §5.3 knob to settle first — and approaches are counted for
   wools only. Promote into `Analysis` once the cutoff is tuned.)*
3. **Property terms** — the §6 catalogue as pure functions over the derived structures, each citing a rule
   id and returning a distance + a violation record.
4. **Evaluation set** — auto-label the seeds; add minimal-pair negatives per §7 coverage; assert the
   evaluator ranks them the way the author does. This is the test for the rules.
5. **Generator, later** — start with option 8.1, escalate as the evaluator earns trust.

### §10. Open questions

- **[open]** The cutoff threshold (§5): width ≤2 = branch vs a branch/degree rule — settle against the T-shape
  test cases once a few are hand-labeled.
- **[open]** Declared vs computed voids (§6): does the author *assert* every deliberate hole, or only the
  ones the topology would otherwise flag? Leaning: author asserts, evaluator flags undeclared enclosed
  voids.
- **[open]** Whether the deriver's `labels` override channel (§5) ever needs to be persisted in `plan.json`
  or stays a side-file test fixture. Leaning: side-file, keep the plan format frozen.
- **[later]** The generator family (§8) — not chosen until the evaluator is trustworthy.

## 3. Measured seed statistics

Hand/tool-produced per-seed measured statistics (blocks; cell=5) over the twelve-seed corpus, numbered
against the frozen rule ids in `docs/tools/generate-rules.md`. This is the concrete evidence §2's cost-function
catalogue and §4's generated envelopes are calibrated against — the numbers below are load-bearing, not
illustrative.

All coords in blocks (cell coords × 5). Board centre = symmetry centre (0,0). Land interface = two piece rects
sharing any positive-length border. Fanned bbox applies the symmetry orbit; teams = orbit order.

### Per-seed summary

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

### Per-seed land-interface Δ histogram (|delta|: 0/2/4/6/8+) & step-width pieces

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

### Per-seed zones (min-dim buckets 5/10/15+) & approaches

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

### Per-seed spawn / wool / iron detail

#### base-2island
- spawn `bar-e` @(10,50) surf=9 raise=+0 facing=front dist-to-piece-centre=10.0 | end=back (mk→centre 51.0 vs piece-centre→centre 41.2; piece near/far 25.5/57.0)
- wool `bar-w` @(-10,60) surf=9 raise=+0
- wool-spawn dist: 22.4
- ⚠️ FLAGS: G3 2T width=30 (∉40..60)

#### base-2wool
- spawn `bar-e` @(10,50) surf=9 raise=+0 facing=front dist-to-piece-centre=10.0 | end=back (mk→centre 51.0 vs piece-centre→centre 41.2; piece near/far 25.5/57.0)
- wool `bar-w` @(-10,60) surf=9 raise=+0
- wool `wl2-b` @(40,30) surf=13 raise=+4
- wool-wool sep: 58.3
- wool-spawn dist: 22.4, 36.1
- ⚠️ FLAGS: G3 2T width=90 (∉40..60)

#### base-4team
- spawn `bar-e` @(10,50) surf=9 raise=+0 facing=front dist-to-piece-centre=10.0 | end=back (mk→centre 51.0 vs piece-centre→centre 41.2; piece near/far 25.5/57.0)
- wool `bar-w` @(-10,60) surf=9 raise=+0
- wool-spawn dist: 22.4
- ✓ no rule-number flags

#### four-team-towers-big
- spawn `spawn` @(-67,72) surf=13 raise=+4 facing=front dist-to-piece-centre=5.0 | end=back (mk→centre 99.1 vs piece-centre→centre 95.7; piece near/far 85.1/106.3)
- wool `wool` @(-87,17) surf=17 raise=+8
- wool `wool-2` @(-17,82) surf=9 raise=+0
- wool-wool sep: 95.5
- wool-spawn dist: 51.0, 58.5
- iron `spawn` offset=(5,-5) ahead=5.0 lateral=5.0 → **ahead**
- wall `piece-4`↔`piece-10` kind=land border=15 Δsurf=-2
- wall `piece-5`↔`piece-7` kind=land border=15 Δsurf=+2
- ⚠️ FLAGS: WL5 wool raise=8 (>+6) [wool]

#### four-team-wool-two-sided
- spawn `spawn` @(17,47) surf=11 raise=+2 facing=left dist-to-piece-centre=3.5 | end=front (mk→centre 50.6 vs piece-centre→centre 52.2; piece near/far 45.3/60.4)
- wool `wool` @(-32,57) surf=15 raise=+6
- wool-spawn dist: 51.0
- iron `spawn` offset=(-10,0) ahead=-10.0 lateral=0.0 → **behind**
- wall `piece-3`↔`piece-2` kind=land border=10 Δsurf=-2
- wall `piece-4`↔`piece-5` kind=land border=10 Δsurf=+2
- ⚠️ FLAGS: SP7 iron BEHIND spawn [spawn]

#### isolated-spawn-approaches
- spawn `spawn` @(-42,22) surf=13 raise=+4 facing=right dist-to-piece-centre=3.5 | end=back (mk→centre 48.1 vs piece-centre→centre 47.2; piece near/far 40.3/54.1)
- wool `wool` @(2,52) surf=15 raise=+6
- wool-spawn dist: 54.1
- ⚠️ FLAGS: G3 2T width=90 (∉40..60)

#### isolated-spawn
- spawn `lane-3` @(0,40) surf=13 raise=+4 facing=front dist-to-piece-centre=0.0 | end=front (mk→centre 40.0 vs piece-centre→centre 40.0; piece near/far 35.0/45.3)
- wool `lane-6` @(-40,50) surf=13 raise=+4
- wool `lane-7` @(35,50) surf=13 raise=+4
- wool-wool sep: 75.0
- wool-spawn dist: 36.4, 41.2
- iron `lane-3` offset=(2,0) ahead=0.0 lateral=2.5 → **beside**
- wall `lane-4`↔`lane-8` kind=land border=10 Δsurf=+2
- wall `lane-5`↔`lane-9` kind=land border=10 Δsurf=+2
- ⚠️ FLAGS: G3 2T width=80 (∉40..60)

#### mirror-big-board
- spawn `spawn-2` @(-137,12) surf=19 raise=+10 facing=right dist-to-piece-centre=3.5 | end=back (mk→centre 138.1 vs piece-centre→centre 135.8; piece near/far 130.4/141.4)
- wool `wool` @(-127,-67) surf=19 raise=+10
- wool `wool-2` @(-42,47) surf=13 raise=+4
- wool-wool sep: 143.0
- wool-spawn dist: 80.6, 101.2
- iron `spawn` offset=(10,5) ahead=-5.0 lateral=10.0 → **beside**
- wall `piece-9`↔`piece-22` kind=land border=15 Δsurf=-2
- wall `piece-24`↔`piece-28` kind=land border=15 Δsurf=+0
- ⚠️ FLAGS: G3 2T width=130 (∉40..60); G3 2T len=280 (>200); WL5 wool raise=10 (>+6) [wool]

#### odd-facing-three-wool
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

#### rotate-wide-frontline
- spawn `spawn` @(-77,-2) surf=17 raise=+8 facing=right dist-to-piece-centre=3.5 | end=back (mk→centre 77.5 vs piece-centre→centre 75.0; piece near/far 70.0/80.2)
- wool `wool-2` @(-92,32) surf=17 raise=+8
- wool `wool` @(-92,-37) surf=17 raise=+8
- wool-wool sep: 70.0
- wool-spawn dist: 38.1, 38.1
- ⚠️ FLAGS: G3 2T width=100 (∉40..60); WL5 wool raise=8 (>+6) [wool-2]; WL5 wool raise=8 (>+6) [wool]

### Aggregate

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

### Rule-number flag rollup

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

### Island gradient sweep (CT4)

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
ahead of the spawn (see `generate-rules.md`, *Resolved this round*).

#### Corrected stone classification (author round: markers & encasement)

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

### Eleventh seed: big-board-wool-two-sided-plaza-parallel-mid (real-map trace)

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

### maxPlayers pass (six of eleven landed)

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

### Twelfth seed: mirror-tiny-map-cliff · final maxPlayers table · FREEZE

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

**The maxPlayers pass is complete — `generate-rules.md` v3 is FROZEN (2026-07-04) as the composer's
v1 rule set.** G8 carries the coupling table (b/p rising 65 → 184 with per-team land).

### Team-side allotment sweep (composer instrumentation, 2026-07-04)

Author's framing: the team side should grow inside a **rectilinear allotment** — rectangles laid
over the side *including its internal void gaps and build zones, but not the true outside void* —
whose orbit images tile the board without touching; the allotment bound is what forces lanes to
fold instead of stretch. Measured over the twelve seeds: **team footprint = the land components
holding team-0's markers** (spawn + wools; gap-isolated stones carry no marker and drop out),
bbox over those pieces, fills against that bbox, and whether the bbox's orbit images overlap.

| seed | bbox WxH | aspect | land fill | land+zones fill | images overlap |
|---|---|---|---|---|---|
| mirror-tiny-map-cliff | 25x20 | 1.25 | 40% | 45% | 0% |
| base-2island | 30x45 | 1.50 | 63% | 74% | 0% |
| base-4team | 30x45 | 1.50 | 63% | 74% | 0% |
| base-2wool | 60x45 | 1.33 | 43% | 52% | 0% |
| four-team-wool-two-sided | 60x40 | 1.50 | 58% | 67% | 0% |
| rotate-wide-frontline | 60x100 | 1.67 | 44% | 61% | 0% |
| odd-facing-three-wool | 65x95 | 1.46 | 32% | 39% | 0% |
| isolated-spawn-approaches | 75x45 | 1.67 | 31% | 36% | 0% |
| isolated-spawn | 80x45 | 1.78 | 43% | 49% | 0% |
| four-team-towers-big | 80x75 | 1.07 | 40% | 52% | 0% |
| big-board-…-parallel-mid | 105x120 | 1.14 | 42% | 43% | 0% |
| mirror-big-board | 105x130 | 1.24 | 40% | 50% | 0% |

**Anchors for the composer:**
- **Aspect 1.0–1.8** — the corpus team side is always a chunky box, never a sprawling cross;
  a unit whose footprint bbox exceeds ~1.8 has stretched instead of folded.
- **Land fill 31–63%** (median ≈42): allotment area ≈ land budget / fill with fill sampled
  ~0.35–0.60. Low fill = internally folded (odd-facing, the approaches seed); high fill = compact
  bars (the base seeds).
- **No corpus seed interlocks** its team-side bboxes (max image-overlap 0%) — the "tetris"
  interlock is a legal extension the corpus does not yet exercise ([seed-needed]); v1 allotments
  are plain non-overlapping boxes with clearance, per symmetry (half split / quadrant).

### Internal-hole sweep (CT8 evidence, 2026-07-04)

Fanned closure rasterized on the cell grid; void flooded 4-connected from outside; enclosed void
components = holes. Two passes: terrain only, and terrain ∪ zones (the closure). Sizes in cells.

| seed | land holes | closure holes |
|---|---|---|
| base-2island | 0 | 2: [4,4] |
| base-2wool | 0 | 2: [4,4] |
| base-4team | 0 | 4: [4,4,4,4] |
| big-board-…-parallel-mid | 2: [18,18] | 3: [72,18,18] |
| four-team-towers-big | 0 | 5: [24,24,24,24,16] |
| four-team-wool-two-sided | 4: [8,8,8,8] | 13: [8×4, 4×9] |
| isolated-spawn-approaches | 0 | 4: [9,9,9,9] |
| isolated-spawn | 0 | 4: [9,9,9,9] |
| mirror-big-board | 2: [2,2] | 10: [20,20,15,15,12×4,2,2] |
| mirror-tiny-map-cliff | 0 | 2: [2,2] |
| odd-facing-three-wool | 0 | 4: [32,32,8,8] |
| rotate-wide-frontline | 4: [2,2,2,2] | 8: [12,12,12,12,2,2,2,2] |

**Roll-up:** closure holes in **12/12** seeds (land-only in 4); counts 2–13 per fanned board,
always in orbit multiples; sizes 2–72 cells. The three formation mechanisms are in CT8
(`generate-rules.md`). Author: the holes are the rotation device — loops around them give routes
between lanes that don't retreat through a chokepoint.

## 4. Generated envelope bands

> **This section is GENERATED — do not hand-edit.** Generated by `tools/deriver/envelope-stats.cs`
> from the authored seeds in `tools/seeds/` and the traced real maps in `tools/seeds/traced/`. Each
> band is `[min, max]` of the metric across the maps that carry it; the evaluator scores a plan's
> distance outside the band (normalized by half-width). A term marked **authored-only** learns from
> the authored seeds alone — an authored cap the traced maps must not widen. **Re-run
> `tools/deriver/envelope-stats.cs` after adding a teaching map — edit the table below by hand and
> it will silently drift from the seeds it claims to summarize.**

Held out: `traced/3084` (wools do not attribute — degenerate band).

### Bands

| term | rule | lo | hi | maps | scope |
|---|---|---|---|---|---|
| `fill-ratio` | G8 | 0.201 | 0.496 | 23 | authored + traced |
| `enclosed-void-count` | CT8 | 0 | 15 | 23 | authored + traced |
| `neutral-stepping-count` | CT4 | 0 | 4.5 | 23 | authored + traced |
| `team-stepping-count` | CT4 | 0 | 2 | 23 | authored + traced |
| `band-count` | CT1 | 0 | 3 | 23 | authored + traced |
| `isolation-cut-count` | CT5 | 0 | 6 | 23 | authored + traced |
| `uncrossed-middle-void` | CT9 | 0 | 0 | 23 | authored + traced |
| `frontline-count` | FR4 | 1 | 7 | 23 | authored + traced |
| `frontline-width` | FR6 | 1 | 16 | 23 | authored + traced |
| `max-chain-length` | LN2 | 25 | 90 | 12 | authored-only |
| `lane-width` | LN1 | 10 | 20 | 23 | authored + traced |
| `wool-wool-distance` | WL7 | 65 | 265 | 14 | authored + traced |
| `spawn-wool-distance` | WL2 | 30 | 195 | 23 | authored + traced |
| `spawn-wool-spread` | WL9 | 0 | 85 | 14 | authored + traced |
| `wool-front-distance` | WL10 | 24 | 165 | 15 | authored + traced |
| `wool-front-balance` | WL10 | 0 | 140 | 10 | authored + traced |
| `spawn-wool-ratio` | WL9 | 1 | 1.2 | 6 | authored-only |
| `wool-front-ratio` | WL10 | 1 | 1.529 | 4 | authored-only |
| `wool-front-remoteness` | WL10 | 25 | 145 | 8 | authored-only |

### Per-map values

Authored seeds first, then traced maps (a `†` marks a value outside the term's band).

| map | fill-ratio | enclosed-void-count | neutral-stepping-count | team-stepping-count | band-count | isolation-cut-count | uncrossed-middle-void | frontline-count | frontline-width | max-chain-length | lane-width | wool-wool-distance | spawn-wool-distance | spawn-wool-spread | wool-front-distance | wool-front-balance | spawn-wool-ratio | wool-front-ratio | wool-front-remoteness |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| base-2island | 0.487 | 2 | 1 | 0 | 1 | 0 | 0 | 2 | 2 | 45 | 10 | — | 50 | — | 45 | — | — | — | 45 |
| base-2wool | 0.214 | 2 | 1 | 0 | 1 | 1 | 0 | 2 | 2 | 45 | 10 | 80 | 50 | 0 | 45 | 5 | 1 | 1.111 | 50 |
| base-4team | 0.225 | 4 | 1 | 0 | 1 | 0 | 0 | 2 | 2 | 45 | 10 | — | 50 | — | 45 | — | — | — | 45 |
| big-board-wool-two-sided-plaza-parallel-mid | 0.269 | 3 | 0 | 0 | 2 | 0 | 0 | 2 | 3 | 90 | 15 | — | 195 | — | 145 | — | — | — | 145 |
| four-team-towers-big | 0.355 | 5 | 1 | 0 | 0 | 1 | 0 | 2 | 3 | 30 | 10 | 135 | 75 | 15 | — | — | 1.2 | — | — |
| four-team-wool-two-sided | 0.355 | 13 | 1 | 0 | 0 | 2 | 0 | 2 | 2 | 40 | 10 | — | 60 | — | — | — | — | — | — |
| isolated-spawn-approaches | 0.231 | 4 | 1.5 | 0 | 0 | 1 | 0 | 3 | 2 | 35 | 10 | — | 75 | — | — | — | — | — | — |
| isolated-spawn | 0.352 | 4 | 0 | 0 | 1 | 2 | 0 | 3 | 2 | 45 | 20 | 115 | 65 | 5 | 55 | 5 | 1.077 | 1 | 55 |
| mirror-big-board | 0.323 | 10 | 2 | 0 | 1 | 5 | 0 | 2 | 3 | 50 | 15 | 200 | 130 | 20 | 85 | 25 | 1.154 | 1.529 | 130 |
| mirror-tiny-map-cliff | 0.314 | 2 | 1.5 | 0 | 1 | 0 | 0 | 2 | 1 | 25 | 10 | — | 30 | — | 25 | — | — | — | 25 |
| odd-facing-three-wool | 0.278 | 4 | 2 | 0 | 0 | 2 | 0 | 2 | 2 | 65 | 10 | 65 | 65 | 10 | — | — | 1.154 | — | — |
| rotate-wide-frontline | 0.368 | 8 | 3.5 | 2 | 1 | 6 | 0 | 1 | 12 | 60 | 10 | 90 | 50 | 0 | 70 | 5 | 1 | 1.071 | 75 |
| ◦ 803 | 0.201 | 0 | 0.5 | 1 | 0 | 1 | 0 | 2 | 8 | 60 | 15 | — | 85 | — | — | — | — | — | — |
| ◦ a-new-day-ii | 0.496 | 2 | 1 | 0 | 0 | 0 | 0 | 2 | 4 | 85 | 15 | 150 | 125 | 0 | — | — | 1 | — | — |
| ◦ a-new-day | 0.32 | 0 | 0 | 0 | 1 | 2 | 0 | 1 | 16 | 80 | 15 | 245 | 165 | 5 | 145 | 5 | 1.03 | 1 | 145 |
| ◦ acapulco | 0.296 | 6 | 2.5 | 0 | 1 | 0 | 0 | 7 | 5 | 124† | 20 | 148 | 104 | 20 | 96 | 16 | 1.192 | 1.042 | 100 |
| ◦ ad-astra | 0.481 | 2 | 0 | 0 | 3 | 0 | 0 | 3 | 6 | 145† | 15 | 265 | 185 | 5 | 165 | 10 | 1.027 | 1.03 | 170† |
| ◦ aequabilis | 0.491 | 10 | 1.5 | 0 | 0 | 0 | 0 | 6 | 4 | 120† | 16 | 208 | 124 | 0 | — | — | 1 | — | — |
| ◦ aether | 0.332 | 0 | 3 | 0 | 1 | 0 | 0 | 1 | 11 | 24† | 12 | — | 57 | — | 27 | — | — | — | 27 |
| ◦ after-hours | 0.396 | 0 | 0.5 | 0 | 0 | 0 | 0 | 1 | 8 | 55 | 15 | — | 120 | — | — | — | — | — | — |
| ◦ agrorythe | 0.482 | 15 | 0 | 0 | 2 | 4 | 0 | 2 | 7 | 120† | 15 | 215 | 135 | 5 | 140 | 70 | 1.037 | 1.464 | 205† |
| ◦ agrostid | 0.293 | 4 | 4.5 | 0 | 2 | 2 | 0 | 2 | 7 | 48 | 12 | 136 | 92 | 0 | 24 | 0 | 1 | 1 | 24† |
| ◦ bridgid-ii | 0.431 | 6 | 0 | 1 | 1 | 2 | 0 | 2 | 3 | 50 | 15 | 155 | 35 | 85 | 65 | 140 | 3.429† | 1.846† | 120 |

## 5. Traffic ground truth (active, partially built — G33)

The complete contract for player-traffic ground truth. **Input: one zip of raw log files per
map.** Everything else — the traffic graph, the emergent footprint, flow priors — derives inside
this repo; no external analysis project is consulted. Validated end-to-end on ingwaz
(`tools/traffic/`): the logs-only pipeline reproduces the reference graph's islands 6/6 and its
void cells at recall 1.0.

**Status: this pipeline is self-contained and still actively being built, and is not yet fully
wired into the evaluator's main path.** Per §7 above, the traffic pipeline may promote a *few* real
maps into labeled layouts for the evaluation set; do not block the evaluator on it. Treat this
section as documenting an independent, ongoing effort (tracked as `G33`), not a dependency the
evaluator currently requires.

### 1. Input contract: the per-map log zip

`{slug}.zip` containing `{slug}/*.parquet`, one file per recorded match (filename carries the
match start time; the exact pattern is irrelevant — every parquet in the zip is one match on
that map). Raw match data stays private with the author; the zips he shares are the pipeline's
only input.

### 2. Raw log format (pgmlogger parquet)

One row per event. Columns:

| column | type | meaning |
|---|---|---|
| `timestamp` | int32 | **seconds** (epoch). Sample cadence of position rows = the log interval (2 s) |
| `event_type` | uint8 | see event codes below |
| `player_id` | int32 | anonymous player id, **stable across matches** |
| `x`,`y`,`z` | int32 | block position (null on match markers) |
| `held_item` | int32 | held item id (unused here) |
| `inventory_count` | int32 | count of the held item (unused here) |
| `wool_id` | uint8 | wool identifier on wool events; null otherwise |

Event codes (inferred from data and validated on ingwaz — no plugin source needed):

| code | event | notes |
|---|---|---|
| 0 | match/logging start | one per file; null coords |
| 1 | match end | one per file; null coords |
| 2 | player spawn | at the spawn platform (constant y per spawn) |
| 3 | kill | killer's position |
| 4 | death | victim's position; **y < 0 ⇒ died falling in the void** |
| 5 | position sample | every `log_interval` seconds; **y < 0 ⇒ sampled mid-fall** |
| 6 | wool touch/pickup | `wool_id` set; at/near the wool room |
| 7 | wool capture | `wool_id` set; at the capture point (beside the owning team's spawn) |

### 3. Output format: `{slug}.traffic_graph.json`

```jsonc
{
  "map_slug": "ingwaz",
  "grid_size": 3,            // cell edge in blocks
  "log_interval": 2,         // seconds between position samples
  "match_count": 105,        // parquet files aggregated
  "position_count": 19030,   // standing position samples retained (y >= 2)
  "player_count": 510,       // distinct player_id
  "total_playtime_min": 1060.0,
  "nodes": [{
    "node_id": 0,            // dense index; edges refer to it
    "cx": -60, "cz": 54,     // cell anchor: floor(x/grid)*grid, floor(z/grid)*grid
    "coords": [-58.5, 55.5], // cell centre (cx + grid/2, cz + grid/2)
    "occupation": 11,        // standing samples (event 5, y >= 2) in the cell
    "island_id": 2,          // terrain island label; null = no land (void / build region)
    "poi_type": null,        // "spawn" | "wool" | null
    "poi_color": null,       // team/wool colour string on POI nodes
    "team": null,            // owning team id on POI nodes
    "fixed": false           // true on POI nodes (kept for renderer compatibility)
  }],
  "edges": [{
    "src": 0, "dst": 1,      // node_id, directed
    "transitions": 11        // consecutive-sample moves src -> dst
  }]
}
```

Only nodes with any traffic exist; `island_id` partitions the land nodes into terrain islands.
The ingwaz file in `tools/traffic/` is the reference instance (produced by the original
pipeline); a regenerated file may differ by a few `occupation` counts from filtering details —
`island_id`/POI/edge semantics are the load-bearing parts.

### 4. Logs-only derivation (no map knowledge)

Validated against the ingwaz reference (see `tools/traffic/README.md` for the numbers):

1. **Cells + occupancy + edges** — bucket standing position samples (event 5, y ≥ 2) on the
   grid; edges from consecutive samples of the same player life crossing cells.
2. **POIs** — spawns: event-2 clusters (one per team; block-exact). Wool rooms: event-6
   clusters per `wool_id`. Capture points: event-7 clusters (sit beside the owning spawn —
   which also yields team attribution: a player's team = the spawn cluster their lives start
   at).
3. **Symmetry centre** — midpoint of the spawn clusters (the symmetry type itself is testable
   by comparing the occupancy field under rot_180 / mirror candidates).
4. **Void / build regions** — the **fall-share** signal: per cell,
   `fall / (fall + stand)` where `fall` counts sub-zero-y rows (mid-fall positions **and**
   deaths; deaths alone are too sparse — R 0.43) and cells are pooled with their symmetry
   image. Share ≥ 0.08 ⇒ recall 1.0 (precision 0.39); ≥ 0.12 ⇒ P 0.52 / R 0.86. All residual
   error is **rim aliasing** (a grid cell straddling an island edge collects its lip's falls);
   the known fix is classifying falls at block resolution before aggregating.
5. **Islands** — connected components (4-neighbour) of traffic cells minus void cells: 6/6 on
   ingwaz.

### 5. Uses (and the boundary)

- **Recovered footprints** — land + emergent void zones as CT test articles (validated pairs
  like `tools/traffic/ingwaz.*`).
- **Flow priors** — per-map scalars scoring composer candidates: occupancy split over the
  mid/transition/team distance thirds, approach usage shares, void-vs-land occupancy, the
  kill/death frontline band.

Only log zips, graph JSONs, and derived priors enter this repo — no player identities beyond
the anonymous ids already in the logs, no per-match analytics, no match-analysis features.
Tracked as `G33`.
