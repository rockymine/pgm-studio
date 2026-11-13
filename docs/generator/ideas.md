# Layout generation — the idea pool

The layout generator's open work that is not on the board. **The generator is frozen** (author, November 2026):
nothing changes what the composer makes, so every entry here — a form, a seating preference, a rule's measure,
a page over the library — waits until that ruling is lifted. A fix or a speed-up that leaves every composed board
byte-identical is not frozen, and goes on `BACKLOG.md` instead.

**Every entry is one bullet — its id and a title in bold, then what it would build and where, with the evidence
that filed it as an indented italic paragraph beneath.** An id is never reused or renumbered, and an entry pulled
back onto the board keeps its id and leaves this file. What has landed is in `FEATURES.md`, so an entry states
only what is still open.

**Five entries are rules the Sketch's checks also run**, and so decide whether a download goes through: `G285`,
`G286`, `G287`, `G288` and `G301`, under *What a rule measures*. Their text is kept whole, so each can be pulled
back as written.

## The budget and the allocator: what a unit is sized to

- **G104 — What the budget should buy per box kind.** The spend gate holds a unit's total land against its band
  (`docs/tools/generator.md`, *Land spend is two currencies*), while the shares beneath it are invented
  (`audit.md` §3). Instrument what the two-currency budget produces per box kind at each size band, so a share
  can be set against a measurement.

- **G107–G111 — The taxonomy audit's five moves.** `demand` as a live kind (G107, `audit.md` §1); the `mix` kind
  for the allocator's weighted draws (G108, §2); the budget ladders as derived targets, folded into
  `ComposeTargets` (G109, §2); WL7 separation by construction, its traversal-spread half (G110); and the
  frontline's form choice moving to the allocator (G111).

- **G98 — `ComposeTargets`: a request that states the structure it wants.** Prescriptive per-request fields
  (frontline runs, mid form, hub form), sampled when unset, held and verified when set. It is what would let the
  Generator's filters ask for a structure rather than sieve the library for one, which the library-only feed
  rules out today (`docs/tools/generator.md` § Limits).

- **G130 — A widened ring wall has no price.** Ring-wall widening (G129) lets a hub ring carry one side wider than
  the rest, sampled under the ratio cap, and it spends the box's geometric slack — the hole narrows, the box does
  not grow — so the spend gate counts the land it adds while nothing chose to buy it. Decide whether a widened
  wall prices against the land share (it is real terrain the team must cross) and whether that price makes the
  sampler choose the wide side rather than draw it evenly; while it is free, its rate is capped by chance rather
  than by budget.

## Composed boards the author judged: what a larger board is made of

Twelve judged donut boards at 20 and 30 players named what a larger composed board gets wrong. `MD7` scores the
thin, long crossing; the rest is below, to be taken **one change at a time** and judged between, because
changing several at once made the boards worse.

- **G278 — A wider front paid for out of the hub.** *The author's ruling: not now; when resumed, the question is
  which single change is tried first.* A combined round — the face drawn from a fixed range per band (24–36 ·
  32–48 · 40–56 · 48–64 blocks), the hub capped at 44 · 52 · 68 · 76 blocks, two wools from micro up, a
  two-legged front only where each leg reaches `FR9`, 16-block stones — widened the crossing but made the hubs
  uniform (every milli hub a 68-block ring; the double-hole, G and P gone) and the wool placement worse, and was
  rolled back. The ranges are the author's and stand for when this is resumed.

- **G279 — A donut hangs toward the back, and its spawn steps inward to clear it.** `SeatOverhang` draws either
  flip; prefer the one whose ring runs toward the hub's back, and let `UnitSeating.SpawnCentre`'s back-end seat
  slide inward past the ring. Also seat the donut nearer the spawn where its route to the wool stays long.
  `docs/generator/model.md` §5.6.

  *Evidence: judged boards p20 seed 71 (the ring runs toward the middle) and p20 seed 25 (the donut sits far
  from the spawn with a long way to its wool).*

- **G280 — A donut may dock turned 90°, its entry and a third of its long bar against the hub's side wall.** A
  new dock for `CompoundForm.Donut` in `UnitSeating.SeatOverhang`/`WoolBoxEmitter`, beside the entry-stub dock.
  `docs/generator/model.md` §4.6.

  *Evidence: judged board p30 seed 32, where the author proposed it.*

- **G281 — An approach may raise its arm: an `I` or `L` seated at the hub's far edge, facing the way the spawn
  faces.** Every approach leaves the hub sideways. Seat it at the end of a lateral edge nearest the back, turned
  so its run heads toward the back, as a variety draw in `UnitRequests.WoolRequest` and `UnitSeating`.
  `docs/generator/model.md` §5.5–5.6.

- **G282 — On an L-shaped hub with the spawn at the back, the short arm takes a long `I` turned so its wool
  stands ahead of the spawn, with a build zone across the bay.** *The author's ruling: not now.* The zone gives
  attackers a second, shorter way in and moves the wool further from the spawn. Build it first as an adapted
  board in the Sketch and play it back with the author, before any composer change. `docs/tools/sketch.md`.

  *Evidence: judged board p20 seed 80, where both wools cannot sit well on an L hub.*

- **G283 — A wool stands too near its own spawn or too near the front on a two-wool board.** The non-donut wool
  sits close to its spawn on p20 seed 24, and the second wool close to the frontline on p20 seed 50, where the
  author would shift both wool boxes back. `spawn-wool-ratio` and `wool-front-ratio` already score it; the fix
  is in where `UnitSeating` seats the second wool. `docs/generator/model.md` §5.6.

## Seating: where each box docks on the hub

- **G124 — What the seat-separation law measures.** `SeatGeometry.TooClose` inflates **box envelopes**
  (corner-inclusive), so a donut's void margins make it over-reject placements whose emitted **terrain** keeps
  the gap — the hole-hub exemplar's donut box passes 1 cell from the spawn box while its terrain keeps 2. Decide
  the measurand — envelope, emitted terrain, or terrain with a margin — and align `SeatOverhang`/`SeatInRuns`
  blocking on it.

- **G139 — The seat-separation law has no rule id.** It is a live, load-bearing composer law (`model.md` §5,
  *What keeps neighbours apart*, enforced in the allocator's seat step and surfaced through `Producibility` and a
  `Contracts` DTO) that `rules.md` does not mention, so nothing can cite it and no evaluator term scores it. Give
  it an id in the LN or WL family, or state why it is mechanism rather than law; `G124` is what it measures.

- **B41 — Should a host's published capacity bound the grant it hands out?** An offer's `Width` is a *capacity*
  derived from the run's length and a joint's `BoxJoint.Width` a *selection* made per consumer kind, and the two
  are unlinked: `Seat` reads the hub's offers, keeps only their intervals as its **runs** and drops the published
  width, then `SeatGeometry.HubJointFrom` gives a width taken from the demand's kind (the band's wool corridor
  for a wool, its map corridor otherwise). So a hub can grant a corridor **wider than the run it sits on claims to
  support** and nothing objects. Either capacity is advisory — the consumer knows its own lane — or the grant is
  clamped to the offer, in which case a narrow run demotes a consumer's lane width and some docks that succeed
  would not. **Measure first**: how often the granted width exceeds the published capacity of the run it lands
  on. Never makes this documentation; often makes it a gate the composer is missing.

- **G166 — Seating prefers the canonical arrangement.** `UnitSeating` chooses which hub edge each neighbour
  request seats on and takes no view on the combination, so three boards in four come out lopsided (`G165`).
  Prefer the spawn on the edge opposite the frontline with the wools on the two lateral edges — the arrangement
  built maps converge on. The measured payoff is the imbalance halving (0.40 → 0.18) and the rotation past the
  spawn that the lopsided arrangement removes. Constraining the seat choice can only raise the rejection rate, so
  measure that alongside the arrangement split rather than assuming it stays flat.

- **G167 — A holed hub seats its docks across the hole.** A ring, double-hole, P or G hub offers two ways across
  only when the two docks straddle its void, and that is left to chance: ring hubs deliver two ways on 163 of 224
  spawn-to-wool crossings, and the ones that do not are dead by seating rather than by shape — the same compound
  form with both docks on one side is a wide room with a decorative hole in it. When the sampled hub compound
  encloses a void, prefer opposite walls for the two docks. The value is what `route-interference` measures: the
  far way round drops interference from 76% to 37%, the difference between an alternative and one worth taking.
  It touches the same seat choice as `G166`, so the two land together or in a known order.

- **G264 — A composed wool room can stand 12 blocks across void from a piece fronting the crossing.** The inside
  corner of a bent wool is its lane's width, 12 blocks at nano, which `WL20` allows against the team's own ground
  and `WL12` refuses against the front (16). The room's setback along its lane takes a block basis of its own
  where the ground across the corner fronts the band, in `WoolBoxEmitter` rather than in the seat clearance.
  `docs/generator/rules.md` and `model.md`.

  *Evidence: 4 of 400 composed boards (200 seeds each at 8 and 20 players), e.g. `p20 rot_180 seed 53`,
  `frontline-t1` ↔ `wool-b-room`, 12 blocks.*

- **G106 — The observed seat and emit failure modes.** Flush lanes at branch-hub run ends (`audit.md` §6), the
  tiny-stub fallback, twin-leg equality, and square-on-square.

- **G112 — P-aware neighbour placement**, so the P hub survives seating.

## The hub: its forms and its holes

- **G105 — Bigger and better hubs.** A raised depth cap, form-to-size fit, and the per-piece width knob where it
  is still uniform: the branch hub's **bar** thickness and **leg length** (the emitter's per-arm overload carries
  both; only the leg's width is sampled), and a hub arm count of three — the E or comb the compound emitter
  supports and `HubBoxEmitter` refuses.

- **G63-C.2 — The allocator's shape residue.** The deeper Z; the scythe (`G146`, `G81`); the spawn L; hub-form
  richness (L handedness, double-hole at width ≥9, a real rotation for `mirror_x`, an L hub coexisting with a
  frontline); the hub-floor clearance refinement; and CT1/LN2 invariants by construction (10-block image clearance
  and the 50-block chain cap baked into placement). Four-team `rot_90` and `mirror_x` boards come with this work:
  the composer accepts them as requests, and the library and the feed hold two-team `rot_180` and `mirror_z` only
  (`docs/tools/generator.md` § Limits).

- **G272 — Bound the run a player walks around a hub's hole at 40 blocks.** A hole's job is rotation (`CT8`): a
  loop round it gives an alternative route between lanes. Past a certain length it stops being a loop and becomes
  a wall — two players on opposite sides never meet and neither can change direction. The author's bar is **40
  blocks on the hole's longest side**. `HubBoxCells` sizes the hub from its budget share alone and the holed
  bodies build every wall one corridor thick, so the hole takes the whole difference as the box grows. Either the
  ring's walls thicken with the box or the body splits the void — a `double-hole` where one ring's hole would run
  long. Lands in `TeamUnitAllocator.ChooseHubForm`/`ChooseHubWalls`; `docs/generator/model.md` and `rules.md`
  (`CT8` is the rule that states the envelope and measures nothing).

  *Evidence: 12 seeds a band, cell 4 — the hole run's median is 16 · 16 · 18 · 24 blocks up the ladder and **14
  of 38 centi boards exceed 40**, none below it. The corpus of 359 CTW maps puts the run's median at 20 and its
  p90 at 43 (836 voids), so the bar is the corpus p90. `opus5-threapland` is the worked case: one 76×36 hole at
  cell (-10,-19), where a double-hole hub would have given two.*

- **G114 — An L that bends back on a tight hub.** The along-axis mirror, so a tight-hub L bends back instead of
  reverting to an I.

- **G41-D — The negative space a fill leaves, claimed.** A compound publishes the empty space it leaves inside its
  box (`Compound.Vacancies` — a U-hub's bay, a ring's hole), and nothing claims it. A third wool or a spawn
  seated in the hub's bay would.

## Wool approaches: the families the emitter builds and the sampler draws

- **G145 — Five emitter knobs are unreachable from the composer.** `ShapeEmitter.Emit` takes `attachments`,
  `woolExtend`, `entryShift`, `woolShift` and `attachmentOffset`, and `WoolBoxEmitter.Fill`, the only path
  `BoxFiller` and therefore the whole compose pipeline uses, forwards none of them. So the two-attachment donut,
  the extended-wool donut and both scythe endpoint shifts are built, tested, and **cannot appear on a generated
  board**. Decide per knob rather than in bulk: the donut's second attachment is a genuine multi-access shape the
  hub could dock twice and is the strongest candidate to plumb; the scythe shifts are moot until the scythe itself
  is admitted (`G146`). Plumbing one widens `WoolFill` (it already carries `AttachmentWidth` and `RingWalls`) and
  gives `UnitRequests` a draw for it. **The knobs are not deleted**: `EmitterPlacementKnobTests` gates them and
  `model.md` §4 describes them; the gap is the plumbing, not the geometry.

- **G146 — Two families are in the vocabulary but never on a board.** The emitter builds eight room-capped
  families and the composer puts six on boards. **Z** is on the wool menu (`FillMenu.For(BoxKind.Wool)`) and
  `BoxFiller` fills it, but `UnitRequests.WoolRequest` never draws it — the rich branch picks donut, then
  U/H/clamp, else L, and the fallbacks are I. **Scythe** is excluded from the wool menu with a stated reason (its
  bay's mouth is its docking edge, so a flush dock seals it into WL8's forbidden enclosed void), with the
  elevation alternative as `G81`. Two decisions: give the sampler a Z draw or drop Z from the wool menu, the second
  a one-line honesty fix that need not wait on the first; and leave the scythe out until `G81`. The catalog badges
  both (`docs/tools/shapes.md`).

- **G81 — The declared-bay scythe, admitted by height.** A flush host sealing the scythe's bay is legal only once
  height enforces the approach, and a composed board is flat (`docs/tools/generator.md` § Limits), so it waits on
  an elevation stage the composer does not have.

- **G82 · G83 — Entry widening, and the approach's budget.** Entry widening for the Z along its bar interface
  (G82), and a wool-approach budget law — per-part caps or a total path length (G83). The knob is
  `AttachmentWidth`, and the law binds at allocation.

- **G131 — Per-part width beyond the ring.** `G129` generalized the four walls of a ring; the rest of a board is
  one lane width per band (`UnitTuning.WoolLaneWidth`, `Box.HubLaneWidth`). The open cases are the parts docked
  onto a ring, which keep a plain lane width — the P's overhanging bar, the G's L-upright, the double-hole's U —
  plus the approach lanes and the frontline's spine. `G105` owns the per-piece width knob for hub compounds and
  `G82` entry widening for wool approaches; this is the generalization those are special cases of, and the
  donut's sampled hub-entry width (`AttachmentWidth`) is the precedent.

- **G113 — A third wool placed the way the corpus places one.** Two wools beside or behind the spawn and the
  third ahead of it, hung into the middle of the hub, which frees that negative space for what the hub's centre
  then holds. Doubling a third wool onto the spawn's side squeezes it in beside the others, which the author ruled
  out on the judged boards.

## The front line and the crossing

- **G100 — Holed frontline forms.** The P and the two-U-on-I as front lines; they need a face rule for where the
  mid meets a loop first.

- **G116 — A stone raised or lowered, and depth varied along a row.** The split band and the stone row ship
  (`FEATURES.md`). Open: a stone raised or lowered off the base terrain rather than level with it (`MD1`), and
  depth varying across a row instead of one depth per band. Both enter as `CrossingDesign` forms, with the
  `SampleCrossing` arithmetic in git history (hops 10..20, sum 30..60, CT7 column alignment) as the reference
  design.

- **G268 — A frontline spine docked flush on a hub wall makes one slab twice the corridor deep.** The frontline's
  spine is one corridor deep and the hub's wall behind it is another, and the spine docks edge to edge across its
  whole width, so the two read as a single solid run of `2 × corridor`. Measured over 48 composed boards a band
  (`docs/world-scan/map-size-ladder.md`): the fault appears on **22–32 of them at every band**, and its widest run
  grows with the corridor — 48×24 blocks at nano, 52×32 at micro, 100×32 at milli and 116×32 at centi — against
  a corpus whose own modal ground width is 10 · 14 · 16 · 16. The grid does not reach it: a board's modal width is
  30 at micro on cell 5 and 29 on cell 4. `TeamUnitFiller` already prefers a strand frontline over the solid bar
  on a holed hub; what is missing is the same preference read off the **edge the spine actually docks** rather
  than off the hub's form. `docs/generator/model.md` §5 and `rules.md` `FR6`.

  *Evidence: `p16 mirror_z seed 1` — `frontline-t1 z[5,9)` docked on `hub-t2 z[9,13)` across `x[-6,7)`: 52 × 32
  blocks of unbroken ground.*

- **G270 — A mid stone may grow to the build zone's border, and one spanning the band splits it in two.** A zone
  never grows to overhang the ground it docks (`BZ9` stands). A stone is read by how many of its edges border the
  zone: **four** — inside it; **three** — against one side, not spanning it; **two adjacent** — in a corner; all
  three allowed. **Two opposite** — across the whole band: allowed, and the band is then emitted as two zones, one
  per side, neither holding the stone (PGM accepts one zone either way; two is what mapmakers write). A stone with
  **one** edge on the zone would stand outside the band; the composer never lays one, and nothing is checked for
  it, since only the composer knows a stone (`MidCarver.IsStone`) and a frontline docks on one edge. Several
  stones with zone between them keep one zone. So `StoneInsetCells` stops holding a stone a cell off the band's
  ends, and `MD4` and `CT1`'s one band zone are restated: `rules.md` (a new amendment), `model.md` §5.13,
  `MidCarver.Stones`.

  *Evidence: `rot_180`, seeds 0–59, normal crossings spend a median of 85 · 72 · 81 · 88% of their share from nano
  to centi; `p16 rot_180 seed 0`, band 8×12 cells, one 6×6 stone held a cell off each end, 72%. Spanning the band
  it is 8×6, 48 of 50 cells, and the band becomes two 8×3 zones.*

- **G273 — A fine row should key to the hub's legs where the front has none.** `MidCarver.Keyed` lines its stones
  up with the faces of the unit's **front row**, which is the right feature when there is more than one — but a
  front presents a single face on about two boards in three, so the keyed row lands on one or two boards in
  twenty-four and the rest centre. The hub always has a body, and the holed forms (ring, G, P, twin, double-hole)
  all carry legs either side of a bay: those are the same kind of feature and they are there on every board. Read
  them out of the hub box's pieces the way `TryCarve` reads the front's, and key to them when the front offers
  nothing. `docs/generator/model.md` §5.13 and `rules.md` amendment 38.

  *Evidence: `p24 rot_180 seed 3` (`opus5-stennerwath`) — `frontline-t1` is one 64-block bar, so nothing to key
  to, while `hub-t2 x[-32,0)` and `hub-t3 x[16,36)` are two legs 16 blocks apart. Its stones sit centred at
  `x[-12,16)`, in the hub's own bay rather than in front of either leg.*

## The composer's own record: what a composed plan carries, and where its code lives

- **G159 — A composed plan carries its voids before it is compiled.** The compiler declares a board's voids on
  every compile (`PlanVoids`, `FEATURES.md`), so its holes are correct wherever it is built; `Composer.Compose`
  returns pieces only, and the declaration appears when something compiles it. Running the same step at the end
  of `Composer.Assemble` makes a generated plan self-describing from birth — one line, no new geometry, and it
  cannot disagree with the compiler because it is the same step. The composer fingerprint digests the plan JSON,
  so every board's digest moves: a `ComposerVersion` bump and a re-record of
  `tools/compose/composer-fingerprints.json`, worth spending on the next version bump rather than on annotation.

- **G63-D — The generic label-inheriting fragment pass.** With `GrowthOrder` strategies and vacancy allocation,
  and the isolation cut as a part-aware move — a cut severs a `run` or a `bar`, never a `room` or an `entry`.

- **G97 — Close the offer↔derive mirror**, so the runs, kinds and holes derived from a composed board match the
  offers' intent.

- **G122 — The composer decision trace.** Optionally record every sampled choice and rejected dock during a
  compose, keyed by the request descriptor — the backtracking tool for "why does this bucket look the way it
  does", better than re-running and watching, and what the Generator's drawer could show beside a board.

- **A8 — Should the layout generator be its own project?** `Pgm` holds two charters: the `map.xml` codec and the
  layout generator (`Compose`/`Evaluate`/`Shapes`/`Derive`/`Plan`, 93 files and 16k lines, touching no XML). The
  generator references `Domain` and `Geom`, and within `Pgm` it hands its board over as a sketch and an intent
  (`ComposedSketch`), so it reaches `Pgm.Sketch` and `Pgm.Authoring` too. A `PgmStudio.Compose` would make `Pgm`'s
  charter true again and the generator's own dependencies enforceable (it can reach the codec and nothing
  notices). Against it: a rename across every citation, and `PlanCompiler` — the plan → layout + intent seam —
  would sit on a project boundary rather than inside one. **Deferred (author): not yet**, because which side
  `PlanCompiler` belongs to is not yet known, and it is the one thing the split turns on; the answer arrives when
  the generator next needs a structural change. `docs/project-structure.md` §7.1.

## What a rule measures: the layout law the author ruled on

The author ruled on how each distance a rule checks is read (*Rule text audit*, *Declarations and geometry* tab,
2026-10-04). Each ruling moves which findings fire, so each lands with its `--goldens` re-recorded and the skerry
layout as the board that shows the over-warning is gone. `G289` — one measure per question in `PgmStudio.Geom`,
the same numbers first — is the refactor these build on, and stays on `BACKLOG.md`.

- **G285 — A crossing is a straight bridge from the nearest land.** A player walks the land the shortest way and
  bridges straight across the void; nobody routes around inside it. `G5` (rectangle gap, `ContactGraph.cs:312`),
  `CT12` (4-connected void path, `PieceInterfaces.cs:157`) and `WL12`, `WL20` and `LN6` (straight runs) read the
  same void three ways and over-warn: on the skerry layout a small front piece makes the check ask a diagonal
  route through the void to a wool room. One measure: walk the land, then the straightest bridge from land edge to
  land edge. `docs/generator/`.

- **G286 — A step a player cannot walk is 3 blocks.** `EL1`, `SP8`, `WL11` and `WX11` fire at 2. A 2-block step
  is fine where a block can be placed; 3 or more is the fault, on plan seams and above all out of a spawn. Lower
  is always better, so a soft term prefers it. Read the thresholds from `Walk.FreeRise`/`Walk.ScrambleStep`
  rather than literals. `docs/generator/`.

- **G301 — `CT12` measures a strait no build region spans.** `PieceInterfaces.IslandGaps`
  (`PieceInterfaces.cs:104`) pairs two landmasses only where one build region touches both, so two team sides
  whose build regions each stop short of the gap are no pair and the strait goes unmeasured — a 2-block gap on a
  rot_180 board raised no `CT12` (`PL9` refused it). Pair the two team sides of a two-team wool board whatever the
  build regions do, and keep the region test for `direct` alone. Lands with `G285`'s one measure if that comes
  first. `docs/generator/rules.md`.

- **G287 — A corridor is its size band's width.** `G2` is three numbers today: the size band's text, 10 blocks in
  the plan check (`ContactGraph.CorridorMin`) and 2 cells in producibility. It is the composer's band width, a
  multiple of 4: 8, 12, 16, 20, for generated wool layouts. What `G2` asks of a build region is open too: `LintG2`
  measures every entry in `zones` on its shorter side, while a region is cut against the front lines it lies
  between, so widening one moves it off them, and a mid region too thin says the front lines are too short
  (author). `MD7` already holds the mid region's width along the front lines per band, so settle which question
  `G2` asks of a region before it carries an edit. Read it beside `EZ2` (`Analysis/Playability/BuildZoneGap.cs`),
  which asks the same relation of the built ground: a coast pulled back from its build region leaving void under
  10 blocks where the plan put ground. `docs/generator/`.

- **G288 — A front line's width is its front edge.** `FR6` counts cells and `FR9` blocks, off different
  structures. The width is the edge that faces the enemy into the build zone, not every edge touching it: a 2 by
  20 piece poking into the zone is not a 42-block front. `docs/generator/`.

- **G291 — Settle the raised rules whose check is not their argument.** *Parked on the author.*
  `docs/generator/audit.md` §8 lists thirteen rules where `rules.md` argues one thing and the check does another;
  for each, the author says which is right, and the losing half changes in the same commit. `docs/generator/`.

- **G292 — A plan states a zone of rules.** A plan's `PlanZone` states build regions and water lanes; a third
  kind, with `ZoneIntent`'s rules (`enter`, `place`, `break`, `harm`, `kit`), would compile into
  `MapIntent.Zones`, which a map's intent already states directly (`docs/pgm/zones.md` §3) — so this is the
  one-way import (`POST /plan/compile`) carrying one. Blocked on the layout law: `BZ5` forbids every zone touching
  a spawn piece and the BZ rules read every zone a plan states (`PlanValidator.cs`), so a rule zone needs its own
  place in `docs/generator/rules.md`, amended by its correction protocol.

## Scoring: the evaluator and the flow read

- **G138 — The composer accepts, it never chooses: a soft score has nowhere to act.** `Composer` takes the
  **first** plan that clears `LayoutEvaluator.Gate` and returns — no ranking, no best-of-K — and `Gate` runs hard
  terms only. The compose path builds its context as `EvalContext.Build(plan)` with no envelopes, which defaults
  to `SeedEnvelopes.Empty`, so every soft term looks its band up, gets null, and stays dormant by design. **The
  authored envelopes have no causal influence on generated output**; they score boards after the fact, the
  library's card score among them. So any soft rule derived from `G118`'s verdicts is inert until this lands:
  generate K candidates, score all K, return the best. It moves every fingerprint. **Sequence it after the bands
  are calibrated**: over 560 composed plans a ranking would be almost entirely a `spawn-wool-ratio` contest
  (outside its band on 44% of applicable plans at median distance 1.64) while four terms score nothing at all —
  the `LEARNING.md` debt entry. Order: `G118` collect → calibrate or gate the vacuous terms → this → soft rules
  become causal.

- **G60 — The evaluator's soft-term leftovers.** Cramming, approach count and height terms; the
  keep-lowest-scoring hunt loop; the ranking harness with minimal-pair negatives.

- **G69 — Deriver primitives for dense mids.** A crossing-corridor read, rotation that means rotation, and robust
  stone classification — the prerequisites of the cramming term.

- **G66 · G67 · G68 — Rules made visible, and part-level terms.** An illustrated rule catalogue with a reject
  inspector (G66), fill-time part invariants (G67), and evaluator-side part terms (G68).

- **G43 — The composer↔teaching-set conformance sweep**: the aggregate soft distance per term between what the
  composer makes and the seed plans.

- **G262 — The seed corpus states iron the placement rules no longer seat.** Measured across `tools/seeds`: 12 of
  14 spawn-room cubes resolve unplaceable, on five seeds, because a cube and a walled room need `6 + 2 + 3` = 11
  blocks on one axis and those spawn pieces are 10×10, 15×15 and 20×10. Nothing is broken by it — an unplaceable
  marker stamps nothing and is flagged `WX9` — so this is a data refresh, not a defect: re-author each spawn piece
  so it either has the depth for a yard or states a footprint small enough to open one, then re-record whatever
  `docs/generator/seed-stats.md` measures off them.

- **G127 — The flow graph: junctions, lanes and climbs on the unioned footprint.** `PlanFlow` reads flow at piece
  fidelity — per objective and demand set the routes, a fork per door (`RouteFork`), the ribbon each side walks,
  the interference between them (`route-interference`) and the ground no journey reaches (`dead-share`) — over
  `Cells.WaysRound`, `Cells.RayCut` and `Cells.MinVertexCut`. Open is the substrate it was designed to stand on,
  solved and never built:

  > **Junction regions (hubs)** are computed on the **unioned island footprint**, so how the author cut
  > the plan into pieces cannot change the result. Every access **mouth** — a land interface or a bridge
  > mouth on the boundary — is an *interval* with an inward direction; extrude each mouth's span
  > perpendicular into the land, and a junction region is the intersection of corridors from **three or
  > more** mouths. A four-way "plus" yields the crossing rect, a three-way T likewise, and a two-mouth
  > corner yields nothing, because a corner is not a hub. Areal by construction — interval mouths, region
  > output, no thinning or skeletonization anywhere. A **lane** is then the corridor between junction
  > regions and dead ends, which is what the width and length rules are measured along, so a lane cut into
  > several pieces for elevation or cornering is still one lane.
  >
  > **Climbs** ride on those chains. A climb is a maximal run of land-interface traversals with monotone
  > elevation change; each traversal carries a horizontal direction (interface midpoint to midpoint) and a
  > delta. A climb whose direction reverses past roughly 120° while still monotone is a
  > **switchback** — net displacement far shorter than path length, height packed into a small
  > footprint — against a **straight ramp**, where displacement is about equal to length; a flat piece
  > between two climbing segments is a **landing**. Label a climb by its top-end anchor (nearest wool room
  > → wool approach, a junction or mid piece → mid ascent, else interior) and by use per team (on an
  > enemy-spawn→wool path it is an attacker climb, on an own-spawn→wool path a defender rotation). That
  > gives the composer vocabulary the distinction it currently cannot make: a straight approach against a
  > space-packing switchback against a defensible landing.

  **A route count at piece fidelity depends on how the pieces were cut**, which is the substrate's reason to
  exist. Four attack routes on `p30-s374` (`band → front-t2 → front-t1 → hub-t1 → b-t2 → b-t1 → b-t4 → b-room`
  at 15 cells, and three more at 18, 23 and 26) come from two forks multiplying — two frontline legs × two doors
  into the wool — so the count is a piece-adjacency fact and only the lengths are cell fidelity. On a generated
  board the composer named the pieces and the cheap method is right; on a board whose pieces an author cut, the
  same geometry cut differently gives a different route count.

  The emit side of the same mirror assembles the intended story from the vocabulary (each form a known
  mini-graph — donut a cycle, ring hub a cycle with tangent runs, twin frontline two parallel band edges — glued
  at the joints), and a mismatch is itself the finding (square-on-square: the story says hub + frontline, the mask
  says one many-mouthed blob). Per wool a **route signature** — the fork-degree sequence band→wool (`2⇒1⇒2⇒1`) —
  is one legible token for the evaluator and for the verdict tags of `G118` (plaza · no-funnel ·
  uncatchable-runner). The first terms, one per observed failure: **plaza** (junction area against mouth widths
  and corridor coverage, `G69` kin), **spread** (wander area along the route), **interception slack** (runner
  wool→home against defender spawn→cut-off on the fanned graph — uncatchable when every return route is negative
  at every node), and **redundancy** (disjoint wool↔band ways, generalizing the middle hole's parallel ways).
  pgmlogger traffic (`docs/gameplay/traffic-ground-truth.md`) is the ground truth the model is validated
  against.

  *Evidence: the flow read over 210 boards from `Composer.ComposeStages` with `PlanBoxAnnotation.Apply`, the
  family census widened to 5200 boards and 9154 wools. **Redundancy** reads zero on 97% of boards — the only
  families bringing a second door onto the hub are U (0.79%), H (0.69%) and clamp (0.38%), and on those the two
  doors sit 3 cells apart on every board, so one barrier covers both; the term measures nothing until the
  spacing and the sampling weight move. **Spread** over the 93 boards whose spawn→wool crossing has two ways: the
  long way is a median 1.40× the short, 22 exactly equal (`match-flow.md` §9). Counting the connected components
  of the minimum cut is **not** the ways-round test and answers opposite in both directions: it reported that
  rotation between a team's objectives never splits on any ring board, where the homotopy test finds it splits on
  81 of 84 (`match-flow.md` §2).*

- **G168 — A board is worth evaluating in two game states.** A two-wool map is two arrangements in sequence:
  before the first capture both objectives are defended from the spawn, and after it one room is the attacking
  team's forward node — a place worth travelling to for the chest gear the generator emits — and the wool-to-wool
  route becomes the live one. Terms that are vacuous in the first state carry the whole second phase, so
  evaluating only the opening scores half a match. This changes the evaluator's shape rather than adding a term:
  `EvalContext` carries which state is being read, and the terms that only apply post-capture (rotation between
  objectives) declare it. Interference is not one of them: its origin is each side's own spawn in both states.
  Decide early whether the two states produce two scores or one combined figure — a single number that averages a
  strong opening against a hopeless second phase describes neither. The played account is
  `docs/gameplay/match-flow.md` §4.8.

## The library, and what a person says about a board

The Generator page browses a library of composed boards and keeps nothing that is not a map
(`docs/tools/generator.md`); browse (G117), its structural sieve (G128), the showcase (G121) and the shape
catalogue (G144) ship. What a person says about a board has nowhere to be kept, and these entries are that store
and what it would answer. **Only human attention writes**: a verdict is stored when a person votes, never while
the feed scrolls. A verdict names its board by the descriptor rather than by the library row, since a fill under a
new composer version replaces the rows. Browse votes (absolute) and duel results (pairwise preference) are
separate datasets, joined only at analysis time.

- **TG5 — Player bands are counted and combined.** The Generator's band chips (`SizeBands`) carry no counts and
  only one can be on, because `GET /api/compose` serves one band per request and its feed returns no per-band
  totals. Accept several bands and answer a count per band, then give the chips counts and multi-select like the
  shape facets. `docs/tools/generator.md`.

- **G165 — Dock arrangement belongs in the structure summary.** Which face of the hub each box seats on is a board
  property with measured consequences and no representation: it is not the hub's compound form and not the
  approach family. With the compass rotated so the frontline is *front*, generated boards split **canonical**
  (spawn *back*, wools *left* + *right*) 27% against **lopsided** (spawn lateral, one wool on *back*) 73%, and the
  split predicts two things — the median spawn-distance imbalance is 0.18 against 0.40, and the second-wool
  rotation runs within ten blocks of the spawn on 63% of canonical boards against 2% of lopsided ones. The faces
  fall out of the mouth positions the box read already computes: add them to `StructureSummary` and
  `StructureSummary.Canonical()`, and to the structure the library rows store (`ComposedBoardLibrary`), which
  makes the arrangement a feed filter and a verdict or duel bucket key.

- **G118 — Verdict collection.** Tap-chip annotation tags (large toggleable pills, multi-select — never
  checkboxes) on both vote directions, both optional; the tag set seeded from the layout-rules vocabulary
  (wools-too-close · wools/spawns-should-swap · flat-front · crammed-mid · no-rotation · great-hub · …,
  extendable), each tag carrying its rule id where one exists — a downvote tagged with a rule whose term did *not*
  fire is a ready-made evaluator bug report. Store {descriptor, verdict, tags, free-text note, evaluator score and
  per-term snapshot, evaluator version}, with a JSONL export so the labelled examples drive rule refinement,
  envelope regeneration and analysis.

- **G120 — Duel mode.** Bucket-scoped side-by-side comparison: a **bucket** is a filter combination (e.g. 2 wools
  · F frontline · double-hole hub · one L + one donut), so both boards made broadly the same structural decisions
  — the closest thing to a controlled comparison, and a minimal-pair factory for the evaluator's labelled set. Two
  big renders, pick the better; the result is a **preference pair** `(winner, loser, bucket)` — never converted
  into a downvote — with a per-bucket ranking (Bradley-Terry or Elo) derived at analysis time.

- **G147 — Verdict coverage on the catalogue: which buckets has nobody judged?** *Sequenced after `G118` — there
  is nothing to count until verdicts exist.* The feed hands the author whatever the composer samples, so
  collection is passive: the corpus ends up shaped like the sampler, and the parts of the space the sampler rarely
  visits stay unjudged — the donut is 73 of the 89 wool cards while U, H and L are one apiece. So show the
  denominator. A `StructureNames.Canonical()` key is a triple (`wools:… | hub:… | front:…`) over a space small
  enough to enumerate — 81 wool classes up to rotation and reflection, 7 hub forms, 3 frontline forms — and a
  **coverage** facet beside *kind* and *reach* takes three states: *judged*, *thin* (one or two verdicts) and
  *unjudged*. It needs one query, verdict counts grouped by the structure key, so it is designed with `G118`'s
  store rather than after it. **A card is a component of a bucket, not a bucket** — a board reading
  `wools:donut,l` touches both the donut and the L card — so per-card coverage is an aggregate over every bucket
  the card takes part in; build the aggregate first, and a per-bucket drill-down only if it proves too coarse.

## The composer reached from outside: a box filled on request

- **G189 — Draw a box, say what goes in it, and the composer fills it.** An author or an agent draws a rectangle,
  names a family, and the composer emits the unit inside it — its own vocabulary reached from outside, bounded,
  verifiable against the box's own gates, and unable to spread across the rest of the board. Most of it exists:
  `GET /api/shapes/probe` emits one family's knob combination through `BoxFiller`, profile check and docking gate
  included, and answers with the shape or a directed `FillRejection`; `/api/shapes/probe/schema` serves each
  family's knob surface and minimum box; and the in-mix tier of `GET /api/shapes/catalog` is exactly the set the
  composer samples. What is new is the stamp — the emission written into a map's sketch as shapes at the
  rectangle's origin, producible by construction. The vocabulary is the limit and it is uneven: wool approaches
  are described in detail, and nothing describes how a mid island is composed, which has to be filled before a mid
  can be asked for by name.

- **G190 — A refusal that carries a candidate, not only a diagnosis.** A gate states what is wrong and, in its
  `fix`, what would satisfy it in words — `WX6`'s is *"give the wool room a border with a neighbouring piece, or
  place a build zone against it"*. For the geometric refusals that sentence describes an enumerable set: the
  neighbouring pieces that could carry a border, the rects a build zone could occupy. A gate that answered two or
  three of them beside the finding would turn the hardest class of refusal — board geometry a weaker driver gives
  up on — into a choice. It is not one change: each rule that could carry a candidate has to derive it, and most
  cannot. Worth trying on `WX6`, `PL11` and `SP1` first, the three that refused a board into being a different
  board.
