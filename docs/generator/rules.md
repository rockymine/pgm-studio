# Layout rules — the composer's v1 rule set (v3, frozen 2026-07-04)

The generator's actual content: per-role attachment rules, dimensions, and elevation defaults for
the plan composer (`docs/generator/model.md` §1, §5). v3 measures every rule against the
**twelve-seed corpus** (`tools/seeds/*.plan.json`; the eleventh, `big-board-…-parallel-mid`, is a
**trace of a real map** at 30/team; the twelfth, `mirror-tiny-map-cliff`, the tiniest at 5/team).
**FROZEN 2026-07-04**: the maxPlayers pass completed — every seed carries the author's per-team
count — closing the last blocker. Changes from here are **amendments** via the correction
protocol. Tags:

- **[corpus]** — confirmed by measured seed evidence (seeds cited).
- **[expert]** — author-stated, not yet exercised by a seed.
- **[open]** — awaiting the author's call (see *Open questions*).
- **[guess]** — still mine.

Rules are numbered for correction by id. Distances in blocks unless marked *cells*, and a distance
between two things on a board is **the walk between them over the walkable surface** — eight-connected,
a diagonal costing what a player walks rather than two steps, a climb charged the blocks it takes to place
and a fall counted but not charged, routing around voids — never the straight line through the air
(amendments 13 and 20). "Front" = toward the map centre / the enemy; "back" = toward the map edge.

## Definitions

- **Lane** — an elongated transit piece: length noticeably exceeds width, it carries flow between
  a dead end / objective and a junction. Not the hub (the junction residual the lanes originate
  from), not a stepping stone (standalone).
- **Connection (a `land` interface) [expert]** — two pieces connect wherever they share **any
  positive-length straight border** — that shared terrain is walkable. A border **narrower than the
  corridor minimum** (< G2) is a **narrow seam**: still a connection, legal and common in staircases
  and ledges (a 5-block shared step is one island, not two). Connectivity is therefore **split from
  corridor quality**: whether an assembled route is wide enough to fight through is judged on the
  **assembled footprint** by lane-chain analysis, **not per seam**. **Corner / point contact never
  connects** — two pieces diagonally across a point stay separate, even when other shapes share that
  same point (a point is not a walkable border).

## G — Globals

- **G1 [expert]** Grid cell **5** by default, but a parameter (4 is viable for finer detail). More
  fundamentally: the plan is a **mini layout** — the checkered-paper scale *proxy* map authors
  already draw, not block-true dimensions. Grid-born "artificial" distances are expected and are
  resolved downstream by the scale + roughen passes (design doc §2, "the plan is a mini layout").
- **G2 [expert]** Minimum corridor width **10**; larger maps trend toward **15**.
- **G3 [corpus, revised]** The v2 width band (40–60) fit almost none of the authored corpus:
  measured 2-team fanned boards run **30–130 wide × 100–280 long** (elaborated seeds typically
  80–130 wide; `mirror-big-board` 280×130), 4-team squares **130–180**. Wide-frontline designs
  legitimately exceed the old cap. All twelve seeds carry **honest per-team counts** (stored =
  the comfortable cap; the author notes maps play fine ± a few players): tiny 5 · base-2island
  10 · base-4team 10 · base-2wool 12 · wool-two-sided 12 · approaches 12 · isolated-spawn 14 ·
  odd-facing 16 · towers 18 · rotate 20 (XML-defined 16) · trace 30 · mirror-big-board 32. The
  envelope↔player coupling is G8.
- **G4 [corpus]** `rot_180` (2 teams) / `rot_90` (4 teams) are the defaults; `mirror_x`/`mirror_z`
  are valid and exercised end-to-end (`mirror-big-board` runs `mirror_x` through compile +
  export; `mirror-tiny-map-cliff` runs `mirror_z` through the same chain).
- **G5 [expert, refined]** Void gaps between *individual* landmasses: **10–20** for the
  crossing a route *depends on* — but a longer hop is fine when the **same buildable region
  offers a shorter alternative** (`four-team-towers-big`'s 25 sits beside 15- and 20-hops in one
  region). Lint therefore judges a region's **minimal** crossing, not every pair. Total crossing
  40–60. At **30 players/team** the traced `big-board-…-parallel-mid` runs each parallel lane as
  a single stone-free **35** crossing (also the region's minimum) — the hop envelope scales with
  player count (the G8 coupling).
- **G6 [expert]** Build headroom above the island surface: **≥20, up to ~40**. Island terrain
  height **5–20**. **The sky-layer smell:** low, flat terrain under a tall build cap casts a
  second play layer into the sky — players dig the base to bedrock, defend from above, and the
  match stalemates as coverless sky bow-fighting. So terrain wants height variation, and the cap
  is calibrated to the terrain, not set generously. Observer platform: out of player reach —
  **~5 above the build cap**, or off to the side beyond any build zone.
- **G7 [expert]** Follows G5: a single required-path hop stays ≤ **~20**; anything longer is a
  chain of hops (stones) summing to the 40–60 total crossing.
- **G8 [corpus, derived]** Map size is driven by the intended player count — `maxPlayers` is an
  *input* to the board envelope, not an afterthought. With all twelve author counts the coupling
  is: **land area per player rises with per-team land**, saturating around ~175–185 b/p:

  | land/team | players/team | b/p | seeds |
  |---|---|---|---|
  | 325 | 5 | 65 | mirror-tiny-map-cliff |
  | 950 | 10 | 95 | base-2island · base-4team |
  | 1250 | 12 | 104 | base-2wool · isolated-spawn-approaches |
  | 1500–1550 | 12–14 | 111–125 | four-team-wool-two-sided · isolated-spawn |
  | 2500–2875 | 16–18 | 156–160 | odd-facing-three-wool · four-team-towers-big |
  | 3500–5875 | 20–32 | 175–184 | rotate-wide-frontline · trace · mirror-big-board |

  Reading: bigger maps spend more land per player (elevation, longer crossings, rotation space).
  Composer: target players/team → read land/team off the table (interpolate) → land budget =
  teams × land/team. Counts tolerate ± a few players (author). Rotate's XML-defined 16 gives
  219 b/p — defined counts sit below the comfort cap.

## SP — Spawn

- **SP1 [expert]** The frontline→wool path never passes **through** the spawn (protection regions
  enforce this anyway); it **may pass around it** on a wide or split lane.
- **SP2 [expert]** Near the **back** of its lane — otherwise the space behind spawn is dead space
  with no purpose. (The current lint approximates "back" per-piece and misreads spawns placed
  mid-chain; honest measurement needs lane chains — `G24`.)
- **SP3 [corpus]** Faces the enemy by default; deliberate side-facing exceptions exist
  (`odd-facing-three-wool`, `four-team-wool-two-sided`, `mirror-big-board` all face left/right
  along their lane rather than dead at mid).
- **SP4 [corpus]** Raised spawns measured across the corpus: **+0 to +10** over base (the three
  base seeds flat; elaborated seeds +2/+4/+4/+4/+8/+8/**+10** — `mirror-big-board` spawns on the
  highest plateau). Common band **+4..+8**. `mirror-tiny-map-cliff` adds the first **lowered** spawn (**−2**,
  surface 7 vs base 9, wool at +2 above it) — at tiny scale the band extends to **−2..+10**.
- **SP5 [expert]** Spawn structure (cube, protection) is stamped at export; the stamp style may
  evolve. The plan reserves the area and floor level only.
- **SP6 [corpus]** Spawn **can** be `gap`-only (an isolated spawn island) — `isolated-spawn` and
  `isolated-spawn-approaches` both build it.
- **SP7 [corpus]** Resource placement (iron): **beside or ahead** of the spawn — players face
  mid and must see it. Iron *behind* the spawn is a bad smell (unseen, dead space). Corpus: 3
  beside, 2 ahead, none behind. (An earlier "violation" on `four-team-wool-two-sided` was a
  measurement artifact of the facing-semantics bug — the spawn points straight at its iron.)
- **SP8 [author]** A spawn's **egress steps by 1 level or takes a ramp**: a seam at **Δ≥2** ahead
  of the door is un-walkable bare (EL1's palette steps by 2, so bare seams are un-walkable by
  construction; bridgid-ii was hand-carved). Lint over the spawn piece's forward land seams; a
  cliff at the spawn's *back* is a legitimate wall and is not the egress. Note the corpus carries
  Δ2 spawn ledges on four seeds — the built form of each wants its ramp carved, which is the
  complaint's point.
- **SP9 [author]** A spawn door stands **≥15 blocks from bare void**, measured along the door's
  own line. A **build zone counts as ground** — the gap-only spawn (SP6) whose door opens onto its
  egress bridge is an authored motif; a *buffer* is exactly the declared emptiness this keeps off
  the doorstep (the `entrance-void` fault: a 25-deep drop at the door face).

## WL — Wool room

- **WL1 [corpus]** At the far/back end of a dead-end lane, inset ~**5**. Two-sided wools are
  authored twice (`four-team-wool-two-sided`: two stepped land seams into the room;
  `mirror-big-board`).
- **WL2 [corpus]** On a different lane than the spawn; wool↔spawn ≥ **20** — all 17 corpus pairs
  pass, tightest 22.4 (the base seeds), typical 36–58, up to 101 on the big board.
- **WL3 [expert, clarified]** The plan records only the wool's position and floor level; the
  physical room (cage, pedestal — the 8×8 stamp today) is stamped at export. Requirement on the
  plan: the wool sits on a **flat plateau covering at least the stamp footprint and extending to
  the lane edges** (even in a 15-wide lane, the room area is flat edge-to-edge).
- **WL4 [expert]** Isolated-wool variant: the connecting `gap` is commonly **10–20**; the height
  delta varies in size *and sign* (see EL2).
- **WL5 [expert, re-anchored]** Wool-approach elevation: the room plateau itself is flat (WL3);
  the approach climbs in **steps 1–5 blocks deep**. The v2 cap (+6 total) measured room height
  *vs base*, which the corpus exceeds routinely (+8 three times, +10 once) — but a high room
  beside an equally high spawn is no climb at all. The meaningful metric is the **approach climb
  along the attacker's path**, measurable once climbs land (`G29`); until then the cap is
  provisional and the lint should not fire on room-vs-base height.
- **WL6 [expert]** 1–3 wools per team, each on a **distinct** lane.
- **WL7 [corpus]** Separation between a team's wools, measured over 8 multi-wool pairs:
  **46–143** blocks (46.1 / 58.3 / 64 / 70 / 75 / 85.6 / 95.5 / 143). Working minimum ≈**45**. That
  sweep was taken with a cardinal walk; the same routes cost up to a quarter less under the octile walk
  the preamble states, so the band reads high by that much and the sweep behind it is not in the repository
  to re-run (amendment 21). The evaluator does not read these numbers — `wool-wool-distance`'s band is
  learned from the teaching seeds and is already in the octile unit.
- **WL8 [expert, new]** Wool approach routes: the default is a **single chokepoint route**;
  real maps sometimes add **alternative routes** to the wool (and then a build zone may touch the
  wool room — see BZ5). **[seed-needed]** The `wool-ringed-hole` sanction is a **naming contract**
  (amendment 2026-08-16): a hole ringed around a wool passes only when the ring reads as the wool's
  own box — its leg pieces share the wool room's **id prefix**, with at most one foreign sealing
  piece (`ClosureAnalysis.AnyHoleRingedBy`). `south-rim` beside room `wool-south` fires the hard
  term; renamed `wool-south-rim`, the same geometry is sanctioned. Name approach pieces after
  their room.
- **WL9 [author]** **Spawn↔wool balance.** A team's wools sit **comparably far from the spawn**: the
  spread (max − min) of the per-wool spawn→wool traversal distances stays modest. A large spread means
  one wool is trivially defended (the spawn on its doorstep) while another is left to fend for itself.
  Bands learned from the teaching seeds (`spawn-wool-spread`). The **size-independent factor read**
  is the ratio max ÷ min of the same distances (`spawn-wool-ratio`) — a 40-vs-105 pair on a big board
  and a 20-vs-52 pair on a small one read the same 2.6× — with an **authored cap**: the intent seeds
  set the tolerable factor; traced maps do not widen it.
- **WL10 [author]** **The spawn–wool–frontline triangle.** Two reads, both by surface traversal:
  (a) each wool keeps a real distance to the **frontline edge** — the seam where the mid build band
  meets the land, the line an attacker crosses (`wool-front-distance`, measured at the most exposed
  wool); (b) across a team's wools the triangle stays **balanced**: per wool, the *defence deficit* is
  its spawn distance minus its frontline distance, and the deficits' spread stays modest
  (`wool-front-balance`). The failure this bans: a front-near wool whose spawn is far (free to
  capture) beside a back wool whose spawn is adjacent (trivially defended) — the defender always
  guards the wrong door. Bands learned from the teaching seeds. Two **size-independent, authored-cap**
  companions: (c) the ratio max ÷ min of the per-wool frontline distances (`wool-front-ratio`) catches
  the equal-spawn-but-unequal-front boards at any size; (d) the **remoteness cap**
  (`wool-front-remoteness`, the largest per-wool front distance, any wool count) catches the stalemate
  the balance reads are blind to — a wool far from the front *and* far from everything can carry a
  perfectly balanced deficit while forcing the attacker to run the whole board into a defended
  chokepoint.
- **WL11 [author]** A wool room's **approach steps by 1 level or takes a ramp**: an entry seam at
  **Δ≥2** is un-walkable bare, the same reading `SP8` takes of a spawn's egress and for the same
  reason (EL1's palette steps by 2). The player who crosses that seam is the **attacker** — a team
  is kept out of its own wool, so nobody defends through the door — which is what makes the step
  worse here than at a spawn: it is met at the end of the run that decides the map, either as a wall
  to build up or as a drop that cannot be climbed back out of. Lint every **entry** interface of a
  wool-room piece rather than a facing: a room has no front, and every seam an attacker can arrive
  across is a door (`PlanCompiler.WoolEntrySegments`, the same set the cage cuts its doors on). A
  room reached only over a build zone states no land seam and is not this rule's business but
  `BZ5`'s.

## LN — Lane

- **LN1 [corpus]** Width **10** base — piece min-dims across 150 corpus pieces: 5-wide ×54 (the
  step/ledge idiom, **not** corridors), 10 ×81, 15 ×15, 20+ ×0 (the ">20 near mid/spawn" case is
  authored via assembled footprints, not single pieces). The stretch **in front of a wool stays
  ≤ ~16**.
- **LN2 [expert]** Length **20–50** before a junction or dead end; a lane may include a
  turn/twist (the L-shape case).
- **LN3 [expert]** Wool lanes dead-end at the back; the front end stops at the void edge
  (frontline) or at the hub. (Lane defined above.)
- **LN4 [expert, clarified]** Restated plainly in *Definitions*: pieces join along **any** shared
  positive-length border (a sub-corridor border is a legal *narrow seam*, not a break) — never at a
  bare corner/point.

## HB — Hub / connector

- **HB1 [expert]** Connector/crossbar width: **10 is the floor** (smallest maps); **15 is the
  widespread value**. Hubs can also be authored as one big open **plaza** piece — the parallel
  trace's 30×30 `piece-5` — rather than emerging as a junction residual.
- **HB2 [seed]** Every frontline→wool path crosses ≥1 hub/connector piece. (Unchallenged;
  pending the new seeds.)
- **HB3 [expert]** Hub-widens-into-plaza (1.5–2× lane width) is wanted **at plan level**, not
  left to roughen.
- **HB4 [author]** **L and Z hub↔frontline composition.** The hub and frontline may fold into an
  **L** (mirror-mid `ex-11`: `hub-11`+`frontline-11`, one band holds a step) or a **Z-tetris**
  (`ex-14`/`ex-15`: `hub-14`+`frontline-14` offset, each with its own parallel non-touching band +
  steps, the frontline connecting onward to a rotation piece `piece-14`). Bands stay parallel and
  non-touching; the onward connection is the rotation point. The hub need not be one square — shape
  it. Evidence: `tools/seeds/teaching/mirror-mid-examples.plan.json`.

## FR — Frontline

- **FR1+FR2 [expert, merged]** There is **no overlap/abut rule**. The seed's 5-block overlap was
  authoring simplification (aligning a simple rect to one front-end). Build zones are simple
  rects; **overlapping terrain is allowed and harmless** — the alternative is carving with
  negative regions or region unions, needless complexity. What matters is the buildable span
  over the void; the plan editor authors zones precisely.
- **FR3 [corpus]** Defenders-hold-high-ground-behind-the-frontline is **common, never strict** —
  frontline towers are authored (`four-team-towers-big`, surfaces to 17 at the front;
  `rotate-wide-frontline`'s raised mid-facing steps).
- **FR4 [expert, split]** Two distinct "angles of attack":
  - **Team approaches** — ways to reach the enemy's side: **1–3** (corpus: 1 on six seeds, 3 on
    three); 1 is acceptable only if it is wide.
  - **Wool approaches** — ways to reach a wool room: WL8.
- **FR5 [expert, corpus]** Opposing frontlines may sit at **different heights per lane** — in the
  parallel trace one lane ends on a **13** frontline facing the enemy's **5**, mirrored so each
  team owns one high and one low end. The strategic telling: a team chooses the **high-ground
  route** or the **incline** — and the high ground is the lane **farther from your own spawn**,
  an intentional design choice (the stronger push costs the longer rotation).
- **FR6 [author]** **Split vs wide frontline; the band docks flush.** A frontline is either
  **split** — two tips with a gap, hung off a hub (the common form: mirror-mid `ex-6`
  `frontline-6a`/`6b` off `hub-6`) — or **wide**, one broad face (`ex-2`/`3`/`4`, 6–8 cells). The
  mid band docks **flush** against the front edge: across **both tips** of a split frontline
  (`band-6` spans `frontline-6a`..`6b`), or aligned to the **corners** of a wide frontline (`band-3`
  kept aligned to `frontline-3`'s ends). A specialization of BZ7/BZ8 at the frontline interface.
- **FR7 [author]** **Variable-length parallel bands are a rot_180 device, not mirror.** Dual
  frontlines with parallel bands may run **different lengths** (mirror-mid `ex-5`: `band-5a` z0–3 vs
  `band-5b` z0–2). Under **rot_180** the images rotate so each team owns one short + one long
  approach; under **mirror** it leaves one permanently-short approach players always pick — so use
  it only for rot_180. Sibling to FR5 (asymmetric heights per lane).
- **FR8 [author]** **A crossing spans the face it docks against.** Measured per piece side as the
  **frontline share** of the exposed run: authored crossing faces read **1.00** across the corpus
  (the BZ8/BZ9 fit), the worst incidental partial **0.40**, and the funnel fault **0.25** — a
  10-block zone on an 80-block face, invisible to players as a front. Lint fires below **⅓** on a
  real crossing (≥10 frontline blocks). How wide a front must be in **absolute** blocks is
  deliberately open — the board's scale decides it, and `/plan/inspect` serves the run widths raw.

## MD — Mid / stepping stones

- **MD1 [expert]** Stones vary: 2×2 cells, 2×3, larger. Raised, level, or (rarely) **lowered**
  relative to base terrain.
- **MD2 [expert]** Gap values per G5 (10–20 per hop).
- **MD3 [expert → CT]** A team's side reads as a **once-connected island the author cut apart**.
  Purpose: harder/riskier wool access, defenders slowed between spawn and wool, retreat over
  fragile player-made bridges instead of solid terrain. Formalized in the **CT section**
  (below) — team-side cutting per CT5; the mid follows the interface/carving reading (CT1).
- **MD4 [expert]** Stones sit entirely inside the build zone.
- **MD5 [expert]** Large neutral mid islands: rare; **not v1**.
- **MD6 [author]** **Band steps sit in a grid, aligned, never a 1-D chain.** Stepping stones inside
  a band are placed in a **grid**, parallel, aligned to (or slightly inset from) the band border —
  never a single chain funnelling all flow through one gap. Do: mirror-mid `ex-2`/`ex-4` parallel
  `step-2a`/`2b`; `ex-3`'s one large `step-3` padded inside, the band kept aligned to the frontline
  corners. Don't: the `gen-p30-t2-rot_180-s1` vertical stone-chain. On a wide frontline the stone edges align with the
  build-zone border. Refines CT7 (stones extend the team islands' lines) for stones inside a band.
  **Column count [author, 2026-07-05]: two lateral columns are the NORM, three the hard maximum**
  (three appears in exactly one authored example) — never a wider grid; "wide, not too wide."

## CT — The mid interface & fragmentation (read from the closure) [expert]

Every plan has a **closure**: its terrain pieces ∪ its build zones treated as land. A playable map's
closure is **one connected mass** — that is what the traversability gate proves, and all ten seeds
pass it. Reading the seeds backwards from their closures gives the fragmentation grammar — but the
mid and the team sides fragment **differently**: the mid is *carved*, the team side is *cut*.

- **CT1 — the mid is an interface, not a cut.** There is no team-separation cut — the symmetry
  axis already plays that part. What the author shapes as "the middle" is the **interface between
  the two team territories**, and that interface always connects through bridge zones. Its
  physical forms, author-labeled across the corpus:
  - **Clean** — one connected build region holding **0..n mid islands** (`isolated-spawn` 0 —
    team islands only; `base-2island`/`base-2wool` 2; `base-4team` 4; `rotate-wide-frontline` 7;
    `mirror-big-board`, `odd-facing-three-wool`, and `mirror-tiny-map-cliff` too — the tiny one
    with a single **axis-spanning mid island** carrying its Δ6 cliff). Several authored zones may
    still be clean when they **connect into one big region** — the discriminator is that players
    travel **freely** between the mid stepping stones. Mid islands may sit in a **grid** without
    making it a hash — that takes a fractured region or a centre hole.
  - **Parallel** — two or more separate zone chains giving parallel team approaches. Corpus
    example: `big-board-wool-two-sided-plaza-parallel-mid` (a real-map trace, 30 players/team) —
    two lanes, each chain being **one authored zone + the other zone's symmetry image** joined
    across the axis, the chains never touching; each lane a single 35 crossing with asymmetric
    frontline heights (FR5). The form has a rotation cost — `isolated-spawn` re-authored as three
    parallel zones would leave passing the enemy spawn as the only lane-switch point, where its
    clean mid lets players bridge from any interface to any other. **Form choice controls
    rotation options.**
  - **Hash `#`** — the build region is **fractured** (or holed at the centre) and the mid islands
    **interconnect**: every route is directed through them; there is no big region to move freely
    in. `four-team-towers-big` (the archetype: centre hole + four grid-aligned islands),
    `four-team-wool-two-sided`, `isolated-spawn-approaches` (hash with parallel traits: three
    interconnected mid islands, eight zones directing all flow through them).
- **CT2 — team side vs mid: the true interface.** A team's side is *at least* the islands holding
  its spawn and wools plus the **minimum other islands** needed to connect them. Mid islands are
  what remains, claimed by closeness to the map's actual middle point. This assignment — not any
  cut line — is the boundary the rules are based on. (For rot_90 the team side is **not** forced
  into one quadrant — mid pieces at the crossing may sit on the axes; see CT11.)
- **CT3 — fragmentation depth.** Fragmentation means **many hops between small islands** (the
  hash case). The per-map question is how deep that regime reaches toward the team side.
  Individual hops keep G5's 10–20; longer total crossings are chains of hops with fragments
  between.
- **CT4 — the island-size gradient [corpus].** Measured over the 90 fanned islands of the
  ten-seed corpus (`docs/generator/seed-stats.md`, "Island gradient sweep"): islands **grow** with distance
  from the centre — pooled Spearman(area, centroid-distance) **+0.61**, holds per-seed in
  **8/10**. Stepping-stone candidates are islands fully submerged in a build zone, or small
  (≤100-block) islands with exactly two build-zone interfaces — **minus two author exclusions**:
  - an island holding a **wool or spawn marker is never a stone** — it carries intent, belongs to
    a team, and the marker constrains who can cross it (a spawn or wool at the island's centre
    means both teams can never share the transient link). Currently vacuous in the corpus (no
    measured stone held a marker) but binding on the composer.
  - a stone whose **every interfacing zone component touches only one team's islands** is a
    **team transient-link**, not a mid stone — the encased pad between a team's own islands (the
    WL4/SP6 bridge pads). Corpus examples: `rotate-wide-frontline`'s four 100-block corner pads,
    each sitting exactly between the spawn mass and a wool platform with both zones coming from
    them, none from mid — all four deep in the team third (63.6), as their function demands. (If
    such a pad *also* bordered a team-connecting zone it could be tagged mid, but connecting two
    marker islands stays its main function.)

  With that split the gradient sharpens: **mid stones thin 17/4/0** over the mid/transition/team
  distance thirds — a hard zero in the team third. Size is a measurement convenience, not the
  definition: `odd-facing-three-wool`'s 400-block L-island is functionally a **large stepping
  stone** — the long side borders the geographic mid, the short side feeds the team area. The two
  grow-outward exceptions (`odd-facing-three-wool` ρ 0.00, `rotate-wide-frontline` ρ 0.15) share
  one mechanism: the largest landmass is a **mid-band spine** with only 350–450-block pads beyond
  it. (MD1/MD4 describe the stones themselves.)
- **CT5 — carve the mid, cut the team side.** "Cut" is the wrong picture for the middle — it can
  hold many islands; the mid operator is **carving**: shaping the interface's islands and zones
  directly into one of CT1's forms. Cutting belongs to the **team side**: severing a piece from
  its parent isolates it behind a bridge — the isolated wool (WL4), the isolated spawn (SP6) —
  and, deliberate variants aside, each team side stays internally land-traversable after cutting.
- **CT6 — the fragmented-island "seed" is the whole corpus.** No dedicated seed is needed: every
  seed *is* a fragmented closure, and the interface statistics are the measured zone/hop numbers.
- **CT7 — stones align with the team islands [expert, corpus].** Stepping stones and mid islands
  are **grid-aligned with the actual team islands** — they sit on the team islands' lines,
  especially along the build-zone borders. Seen across essentially all the maps, and the standard
  look of 4-team CTW (`base-4team`, `four-team-towers-big`). For the composer: stone placement is
  not free scatter inside a zone; it extends the team islands' lines into the mid.

- **CT8 — internal holes are the rotation device [corpus, author; amendment 2026-07-04].** The
  closure encloses internal void pockets — **holes** — in **12/12 seeds** (2–13 per fanned board,
  4–72 cells... sizes in proxy cells; always in symmetric orbit multiples). "These holes are what
  enables player rotation" (author): a loop around a hole gives alternative routes between lanes
  without retreating through a single chokepoint. Three formation mechanisms, all corpus-exercised:
  **authored land holes** (`[]` pockets in the terrain itself — the parallel-mid trace ×2,
  `rotate-wide-frontline` ×4, `four-team-wool-two-sided`, `mirror-big-board`); **land+zone
  enclosure** (the pocket closes only when build zones count — `odd-facing-three-wool`,
  `four-team-towers-big`, `mirror-big-board`); **zone-touch enclosure** (mid build regions touching
  team terrain seal the pocket — both `isolated-spawn` seeds). Not mandatory ("not every map has to
  have one, but most do" — author) but measured universal at closure level: the composer treats
  **≥1 closure hole per team side as the default** and holelessness as a sampled exception; a lint
  flags a holeless plan, never blocks it.

  **Function is read from the hole's ring** (the pieces/zones bordering the void)
  [author, corpus-refined]: a hole whose ring **contains a wool** (a wool-carrying piece or a
  `wool-room`) is the **hole-mediated two-approaches device** — attackers route around the void
  into the room from two sides (the WL8 pattern realized by a hole); every other hole serves
  **rotation**. Measured: strict wool-ring holes are the three base seeds (`bar-w` in each) and
  `odd-facing-three-wool`'s `wool-3`, all sitting in the **far** distance third (deep team side,
  where rooms live); rotation holes run **78% near/mid** (18 distinct hole types). The base-seed
  hits are **author-confirmed** two-approach wools: an approach that crosses the sealing zone
  (bridged, not walked) still counts as an approach. Two cautions
  from the sweep: (1) WL1's *authored* two-sided rooms are a **sibling device** — seam-mediated
  (two land seams into the room), not necessarily hole-mediated; `four-team-wool-two-sided`'s
  land hole is one hop from its room's two seams, `mirror-big-board`'s land holes are unrelated
  to its wool entirely. (2) A **fourth formation mechanism** exists: the all-zone ring
  (`four-team-wool-two-sided`'s centre hole is bordered by zones only, no terrain piece).
  Composer: default holes are rotation holes (near/mid); a wool-ring hole is only produced when
  deliberately drawing the WL8 two-approach variant, never by accident.

  **A hole is an enclosed buffer** [author, 2026-07-05]: "a hole is kind of a buffer between lanes …
  if all four corners touch something, then it's a hole." Annotate holes with a `buffer` piece
  (non-generating) — as the `hole-*` buffers do in `mirror-mid-examples.plan.json` and the `hole`
  buffers in `rot-90-mid-example-*.plan.json`.

- **CT9 [author]** **The frontline rotation hole.** A split frontline + hub + band **encloses a
  rotation hole** in the tip-gap: the hub caps it above, the band (or a bridge) below, and a
  **buffer gap between band/bridge and the hub** is the void (mirror-mid `ex-6` `hole-6` between the
  tips under `hub-6`; `ex-8` `bridge-8b` splits the gap into `hole-8a`/`8b`; `ex-11` `hole-11`). A
  bridge bordering the frontline (`ex-8`), a step between the parallel bands (`ex-9`), or steps
  inside each band (`ex-10`) supplies the rotation link while preserving that buffer-to-hub hole.
  The CT8 rotation device realized at the frontline.
- **CT10 [author]** **rot_90 mid archetypes.** Fanned about the origin, 4-team mids recur as
  (`tools/seeds/teaching/rot-90-mid-example-*.plan.json`): **grid + central hole** (`ex-1`: a 2×2
  zone grid, one central hole from the fanned `hole` cell); **window-frame** (`ex-2`: 3×3 grid → 4
  holes; `ex-3`: L-frontline frame + a central rotation `stone-1`); **full centre region** (`ex-4`);
  **large central void** (`ex-5`: a 3×3 hole); **plus** (`ex-6`/`7`/`8`: four zones + a centre stone
  or hole). These specialize CT1's clean/hash forms for rot_90.
- **CT11 [author] — mid pieces at the crossing may sit along/atop the axes [corrects the inter-image
  clearance invariant; refines CT2].** A team's rot_90 islands are **not confined to one quadrant**: a
  frontline may **straddle the x=0 / z=0 axis** — `rot-90 ex-6` `frontline` [-1,-5,2,2] and
  `ex-7`/`8` [-3,-5,4,2] all straddle x=0 — and its four fanned images **abut cleanly at the axis**
  to form the plus/cross mid. That is the move a near-axis-but-off-axis piece cannot make (its
  quarter-turn image self-collides — the p5/t4-rot_90 infeasibility, G35). This **corrects** the
  G32-A grower's blanket "≥10-block clearance between all orbit images": the clearance keeps the
  **team sides** separate islands, but **mid pieces at the crossing (frontlines, mid stones) may
  reach and sit on the axes.** The unblock for the rot_90 self-collision. **Order-2 (rot_180/mirror)
  centre islands are the same licence:** a mid stone whose near edge sits **on** the axis (u=0) has its
  single fanned image **abut it across the axis** into one central island — a v-centred stone → one
  island, a v-symmetric pair → two (the `ex-10` form). Images that merely **abut or coincide** at the
  axis are one physical island, not a clearance breach; only **interior overlap** (a stone off *both*
  axes under rot_90) stays forbidden.
- **CT12 [author]** **The CTW strait is 15–40 blocks.** On a two-team wool board, the **direct
  crossing** between the two team islands — a build region carrying the pair with no third landmass
  in it — spans 15–40 blocks (the walk over the empty cells). Closer is two halves all but merged
  (the flush-fan fault produced literally one landmass); farther is a crossing nobody bridges.
  Chains over stepping stones are **not** straits — each hop is G5's — and a mid island between the
  teams makes the crossing indirect by construction, so complex mids are out of this rule's reach.

- **BZ2 [expert]** Lane backs (spawn, wool) sit **outside** the buildable area. Lanes and build
  zones *intentionally restrict and guide* the player — their function ensures gameplay and flow;
  a map is not an open greenfield playground. (The "narrower than the island" phrasing is
  dropped; the outside-ness is the rule.)
- **BZ3 [corpus]** Directed bridge: **10** wide dominates (26 of 43 corpus zones); 5-wide tight
  chokes exist (×5, lint-flagged, intentional); 15+ for open bands (×12).
- **BZ4 [expert]** 4-team: zones connect all teams; often with a **hole at the centre** so
  players must walk/bridge around it rather than straight across.
- **BZ5 [expert, retired as a prohibition]** Build zones **may touch spawn pieces** — a zone at
  the spawn is a real motif: the **defender-egress bridge** (`four-team-wool-two-sided`: the
  spawn's second exit is a bridge mainly for defenders rotating to their wool; attackers push the
  other crossings). No proximity rule; the old lint is dropped. Wool rooms may also be touched
  (WL8 alternative-approach variants).

The four rules below are **author-curated (amendment 2026-07-04)**: evidence is the teaching
sketch `tools/seeds/build-interface-dos-and-donts.plan.json`. The sketch informs **build-zone rules
only** — its islands are scaffolding (its `piece-9` dead-end is disclaimed), and it is not part of
the stat corpus.

- **BZ6 [author]** **The mid build region never interfaces a wool piece — and never contains a
  wool position.** Otherwise players bridge freely through the mid straight into the point and
  there is no gameplay direction. A zone touching a wool room exists only as the *deliberate*
  WL8 two-approach variant (BZ5's wool note), never as mid-band spillover.
- **BZ7 [author]** **Dock, don't overlap.** A connecting zone snaps **flush** against the pieces
  it connects (shared border, zero area overlap) — designers extrude a lane into an L/T-section
  for the zone to *visually dock against*, which is also what makes layouts readable. One
  sanctioned overlap form: the **clean-mid plaza** (sketch `zone-11`) may fully encase mid stones
  and partially lap the frontline pieces it connects — but stays in bounds, never overflows
  outward into unconnectable void, and never laps pieces it does not connect (`zone-12` is the
  negative: needless overlap + void overflow, "a prominent contamination in the generated seeds").
  A zone may cut across an L-shaped assembly only **preserving the L's geometry** (flush with one
  border, forming a straight line — sketch `piece-8`/`zone-4`/`piece-4`).
- **BZ8 [author]** **A bridge region requires readability.** Docking into a **large island's long
  straight face** needs a small **connector extrusion** (~2 cells wide, the "offspring" piece from
  the island) carrying the interface; a 2-wide zone lapping an 8-cell straight edge with no
  connector is the named failure. The good ends of the spectrum: a zone spanning the **full width
  of a narrow lane end** (sketch `piece-10`↔`zone-5`), and a connector piece with "the perfect
  width to bridge the gap and no overlap" (sketch `piece-6`↔`zone-2`↔`piece-3`). Zone width ≈
  interface width ≈ connector width.
- **BZ9 [author]** **Fit.** The zone spans exactly what it connects: not **underfit** (sketch
  `zone-13`: a 1-wide interface between two 2-wide parallel faces), not **overfit** (wider/taller
  than the gap, lapping pieces or spilling into void with nothing to connect to). Oversized mid
  regions spanning the whole board width "AND more" are the failure mode; the mid band is sized
  to the frontline interval it serves.
- **BZ10 [author]** **Band depth — no long-thin band.** Beyond BZ9's width fit, the band must not
  run **long and thin** into the frontline: a deep 2-wide band is the named smell (mirror-mid `ex-0`
  `band-0` 4×9; `ex-1` `band-1` / `ex-12` `band-12` 2×tall — the negatives). Parallel individual
  bands (`ex-7`) are acceptable only when **short**; a long frontline-band group degrades back into
  `ex-0`/`1`/`12`. BZ9 governs the band's width across the gap; BZ10 governs its depth from mid to
  frontline.
- **BZ11 [author]** **One zone for a compact middle.** Several zones merging into one region whose
  union is itself a **plain rectangle** are a stitch one zone would have drawn — the author reads
  one crossing, the player a patchwork (the four-zones-over-a-30-block-middle fault). An **L/T-shaped
  union is not stitching**: rectangles are the only shape a zone comes in, so a region that turns a
  corner needs several. Several zones are otherwise right only as **separate regions** — one per leg
  of a legged frontline, one flush zone per mid island (`frontline-dos-and-donts` teaching sketch).

## EL — Elevation

- **EL1 [corpus]** Plateau step unit: **2**. The authored surface palette was base 9 + even steps
  up (9/11/13/15/17/19 — all odd values), so every one of the 137 measured land-interface deltas
  is even by construction: histogram Δ0 ×47, Δ2 ×73, Δ4 ×10, Δ6 ×4, Δ8+ ×3. The traced
  `big-board-…-parallel-mid` extends the palette **below base** (frontline at 7 and 5), and
  `mirror-tiny-map-cliff` reaches **3** (its mid-cliff floor) — palette now **3–19**; terrain may
  dip below the base standard; still odd, deltas still even.
- **EL2 [expert]** Height deltas across `gap`s work **both ways**: attacker builds up and arrives
  low (defensive device), or the defended wool sits low and the defender holds height advantage
  *inside* the room.
- **EL3 [expert]** `land` interfaces: walkable step ≤1; 2–3 only as an explicit jump/ledge
  feature; ≥4 is either a **cliff** or a **stepped path edge** (the seam borders a staircase
  route). Neither is annotated: see EL5/EL6.
- **EL4 [expert]** Per island: base + up to **2** raised sections (not 1). Roughen never changes
  levels, only outlines.
- **EL5 [corpus; amendment 2026-08-14]** Cliffs (one-way drops) are made by **terrain, not by an
  annotation**. A drop is whatever the ground does at a seam — a plan states a surface per piece and
  the relief carves the rest, so a cliff is a consequence of two heights meeting, never a mark. The
  only interface a plan authors deliberately is a **wall** (`walls`), which stamps a structure; a
  `cliffs` list was authored, read by nothing but the lint that demanded it, and is deleted. The
  corpus reading it produced is kept below as measurement.
- **EL6 [expert]** **Cliff qualification** — what separates a real cliff from a stepped path edge. It is
  read off a **solved surface**, where a face's width and drop are measured on the ground rather than
  declared, which is what `ReliefReadback` applies. A cliff (a) cuts the **full width of a lane**,
  (b) is **≥10 blocks** wide, and (c) carries **Δ≥6**, *or* a shallow **Δ4 that walls a pit**
  (EL7's opposing-cliff geometry) with no gentle bypass — a lone Δ4 dead-end step-up is just a
  staircase edge, however wide. Of the corpus's 17 Δ≥4 seams this reproduces the author's
  verdicts exactly: rotate-wide-frontline 0 (5-wide strips + one lone step-up), mirror-big-board
  2 (the spawn-plateau seam Δ8 and the east cliff face Δ6), odd-facing 3 (the Δ8 jump + both pit
  walls). Scale note: the corpus's smallest qualifying cliff is `mirror-tiny-map-cliff`'s
  **10-wide Δ6** on its axis-spanning mid island ("9 vs 3", marked) — exactly at (a)/(b)/(c)'s
  lower bounds; the thresholds are absolute, and tiny maps sit right against them.
- **EL7 [expert, new]** **The pit** — twin opposing cliffs flanking a wool approach
  (`odd-facing-three-wool`): slows attackers like a bridge-gap but more forgivingly (a fall is
  recoverable bridging, not void), lets defenders reach bedrock faster, and the air exposure
  prevents tunneling to the wool. A gentler alternative to placing a build-zone gap hard against
  the room.

## GO — Destroy goals

- **GO1 [author; amendment 2026-08-16]** **A destroy goal sits three to four times as far from the
  enemy's spawn as from its own, by walk.** Per goal: the traversal from the owning team's nearest
  spawn and from the enemy's, both over the fanned closure (pieces ∪ build zones — the cross-team
  leg crosses the axis, so the un-fanned surface would cut it), ratio enemy ÷ own held to
  **[3.0, 4.0]**. Under the band the goal falls to a rush before a defence can form; over it the
  attack is a march across the whole board into a set defence. Calibrated by the author on the two
  shipped boards (3.0 and 3.9 by walk); the older 164-map 2.9-median table was straight-line off
  region centroids — the retired unit (amendment 13) — and does not bind this band. Scored by
  `goal-spawn-ratio` against the authored band, worst goal counted; a goal with **no** route is not
  this rule's business but the traversability gate's, which refuses the export outright.

- **GO2 [expert]** **A team's own destroy goals stand 35–65 blocks apart, by walk.** Two goals a team
  defends from one position are one goal with two names, and two so far apart that no position covers both
  make the defence a shuttle rather than a stand. The measure is the walk between them over the fanned
  closure, in the octile unit the preamble states — the destroy-side counterpart of `WL7`, which separates a
  team's wools. Applies with two or more goals owned by one team.

- **GO3 [expert]** **Opposing destroy goals stand 85–150 blocks apart, by walk.** This is what the contest
  spans: under the band the two objectives are close enough that a rush reaches one before the other team has
  formed, and over it the attacker crosses a board's width into a set defence and the match settles into a
  stalemate. The measure is `GO2`'s walk read across the axis rather than within a team, so one traversal
  answers both. Applies where both teams carry a destroy goal.

## PC — Pieces are anonymous

A narrow seam is legal connecting geometry per *Definitions*, so there is no per-seam width lint: corridor
quality of an assembled footprint is measured by lane-chain analysis, not seam by seam.

The corner lint is declared in code rather than stated here. `PC-C` is
`PgmStudio.Pgm.Plan.PlanRules.CornerContact`, so `GET /api/rules?rule=PC-C` answers what it means and what to
do about it, with the category and concerns every gate rule carries. A bare corner between pieces not already
in the same land component is still linted, and the plan validator is the only thing that fires it — a rule a
gate raises has to resolve in the catalogue, and a second statement of it in this file would be a copy free to
disagree with the one that runs.

- **PC1 [expert]** Pieces carry no semantic role by default — a piece is a modeling unit (cut for
  elevation, cornering, or interface-driving), and one *lane* is typically several pieces. Meaning
  is **derived from the assembled graph**: a lane = a maximal chain of pieces joined by full-width
  land interfaces with no branching (elevation steps and corners do not break it); a hub = a
  junction **region** (degree ≥3 in the walk graph, e.g. the "plus" in front of a spawn), which may
  sit mid-piece and is never reliably a whole piece; a degree-2 corner is not a hub; a mid /
  stepping-stone = a markerless standalone piece inside a build area.
- **PC2 [expert]** Route *purposes* are contextual, never authored: the same piece can be defender
  egress, an attack route, and the shortest wool-to-wool rotation, depending on which team's spawn,
  wools, and bridges you measure from (the isolated-spawn seed's centre plate is all three).
- **PC3 [expert]** Two roles remain explicitly authorable because they carry intent, and both are
  **optional**: `wool-room` and `spawn` (see ST1/ST2). Everything else is `piece`.

## ST — Stamped structures (export)

- **ST1 [expert]** *Wool room piece* (optional): defines the full room **region** and **sizes the
  stamped cage** — the shell footprint is the piece inset one block, per the WX rules
  (`docs/world-export/structures.md`). Its footprint is stamped **solid bedrock from y=0 to its
  floor** (no tunnelling in from below); a **redstone line with a torch at either end** lies on the
  last block row at each of the room's **entry interfaces** — every terrain↔room land seam and
  every abutting build-zone edge (WX6) — the conventional marker for where entrance protection
  begins. The editor renders terrain↔wool-room interfaces **red**. Each of the four corner chests
  turns to open into the room rather than facing a wall: a corner touches two shell walls at once,
  and the room's own door breaks the tie — every chest faces away from whichever of its two walls
  sits on the door's axis (`WoolChests`).
- **ST2 [expert]** *Spawn piece* (optional): defines the spawn **region** and **sizes the stamped
  spawn room** (the same WX footprint rule). Iron placed inside it is **auto-renewed** in the
  generated XML (load-bearing for gameplay); lint: when a spawn piece exists, iron markers belong
  inside it. Spawns have no redstone line.
- **ST3 [expert]** *Iron structure*: an iron marker stamps a **4×4×4 iron-block cube**.
- **ST4 [corpus]** *Pre-built wall*: 2 blocks thick, full seam width, **three courses of bedrock**
  above the approach side (top = approach surface +2) over solid bedrock down to y=0, capped by
  **one course of cobweb**. The web is part of the barrier, not decoration on it: it costs an
  attacker who bridges the top real time to cross and is cut with the shears every kit carries,
  which is what lets the stone itself be short enough that both halves of the lane still read as
  one place. Corpus pattern (11 walls over 5 seeds): walls sit on **gentle seams** — every marked
  interface has Δ ∈ {0, ±2} and border 10–15; nobody walls a cliff. Narrow seams are legal wall
  carriers. Each wall carries a **defence chest** set into **one** face (`DefenseChest`): the
  chest replaces the one bedrock block at the approach's ground level and the block above it is
  carved to air so the lid opens — a niche, not a box in front of the wall. Only that one face is
  opened, and the column **behind** each chest is left as bedrock, so a full vertical bedrock wall
  still stands: breaking the chest meets bedrock, not a way through. **Which face opens is
  authored**, because it is the same thing as which side of the line the supply is for: the plan's
  wall mark carries a `side` naming one of the seam's two pieces (`PlanWall.Side`, defaulting to
  its `a`), and the compiler resolves it to a face per orbit image — a reflection swaps which face
  has the smaller coordinate, so only the piece it looks out at survives the fan. In the plan editor
  the wall tool cycles a seam through *no wall → chests facing a → chests facing b → no wall*, and
  the open face is drawn as an amber bar just off the seam. One chest on a lane ≤ 10 wide, two on a
  wider one, evenly spaced along it. A full 27-slot half-stack loadout each: dark-oak + spruce
  planks and crafting tables to build with, end stone + a redstone block to reinforce, and two
  Efficiency II iron pickaxes.
- **ST5 [author]** *Build-region outline*: the build region is marked in the world by an **unpowered
  redstone line at y=1**, one air block clear of the region — so two blocks out from its edge — and
  holding that same one-block clearance from terrain. Only the **void-facing** edges carry a line: an
  edge docked against terrain is already marked by the terrain, and terrain overhanging the region
  pulls that line back rather than letting it touch. Clearance is measured **diagonally as well as
  along the axes**, which is what separates a line running *beside* a terrain column (a block shorter
  at that end) from one passing *past its corner* (full length). Where two void-facing edges meet at a
  convex corner the two lines **turn into each other**, so a region hanging free in the void is ringed
  while a region bridging two pieces gets two plain lines and no corner.
  <br>The outline follows the **cells, never the authored zone list**: zones sharing a border are traced
  as one region, an enclosed pocket inside such a region gets a ring of its own, and two regions
  touching only at a corner come out as a single unbroken staircase. A pocket ringed by regions that
  merely stand around it is outlined too, a side at a time, each side cut back by the clearance to the
  region diagonally beside it. The rule is scale-free — every length follows the zone and the board's
  cell size. Evidence: the teaching seed `tools/seeds/teaching/build-region-examples.plan.json`, whose
  markers are what a plan of that shape exports.
- **ST6 [author]** *Destroyable platform*: a **5×5, one-block-thick bedrock plate**, seated **one
  course beneath the ground's own surface block** under each destroyable — never thicker, which
  would read as a wall grown out of the floor rather than a plate under it. It stops the goal being
  undermined from below and the ground under it being mined out from under it (`StructureStamper.
  StampPlatform`, called from the destroyable stamp in `WorldBuilder`).
- **ST7 [author]** *Goal sky marker*: every wool room, destroyable and core carries a small marker —
  a solid 3×3×3 cube or a 3-D asterisk, the shape a per-call choice — floating clear of
  `BuildIntent.MaxHeight` (a fixed clearance above it, or above the tallest built terrain when no cap
  is authored), so it sits out of build reach by construction. Coloured to the goal: the wool's own
  colour for a wool room, the owning team's colour for a destroyable or a core. One marker per
  already-fanned goal entry — a wool room, a destroyable, a core are each one list entry per
  symmetry-orbit image (`PlanCompiler` fans team-outer) — so a mirrored board's markers match without
  the stamper (`GoalMarkerStamper`) doing any orbit math of its own.
- **ST8 [author]** *Approach wall geometry*: the interface a wall bars is a **10–20 block lane
  mouth** (a wall across a 30-block face bars a room, not a lane), and the wall stands **about 15
  blocks in front of** the wool room's entrance — judged against the **nearest parallel** entry seam,
  since a wall on a side interface defends a flank and its entry distance means nothing. The
  full-span clause needs no check: the compiler builds every wall across its whole interface. A wall
  pair that includes the wool room itself is `PL13`'s refusal, not this lint's.
- **ST9 [author]** *Role-piece cap*: a wool-room or spawn piece is at most **20×20 blocks** — the
  stamped building is sized by its piece, so piece size *is* building size, and a 90-block piece is
  a 90-block hall. The cap is the workaround for the piece/building coupling (`B178`) until that is
  broken. The floor at the other end is the dressing pass's `DR-SIZE` (a placed building ≥ 5×5).

## Facing semantics [expert]

Marker `facing` is **absolute board directions** — front = −z, back = +z, left = −x, right = +x
on the authored unit, fanned per orbit image. (The editor always meant this; the compiler briefly
interpreted "front" as toward-the-centre, which mis-yawed four seeds and mis-measured one iron —
both corrected.)

## Amendments (post-freeze)

1. **CT8 added (2026-07-04, composer round 1).** Internal holes / rotation loops — author-stated
   during the composer build-out, then measured universal (12/12 seeds at closure level; sweep in
   `docs/generator/seed-stats.md`, "Internal-hole sweep"). New rule, no existing rule changed.
2. **CT8 function split by ring (2026-07-04, composer round 1).** Author: wool in the hole's
   ring ⇒ the two-approaches pattern, else rotation. Corpus-validated with two refinements: the
   WL1 two-sided rooms are a seam-mediated sibling (not the same device), and the all-zone ring
   is a fourth formation mechanism. The naive correlation "land-only-hole seeds = two-sided-wool
   seeds" was tested and REJECTED (1 of 4, relaxed reading only) — the ring test, not the
   land/closure distinction, carries the function. Author-confirmed: bridged approaches count
   (the base-seed `bar-w` holes are two-approach wools).
3. **BZ6–BZ9 added (2026-07-04, composer round 2).** Build-zone interface discipline from the
   author's review of the first generated mids + the curated
   teaching sketch `build-interface-dos-and-donts.plan.json`: mid never touches wool (BZ6), dock
   don't overlap with the plaza-encasement exception (BZ7), connector-extrusion readability (BZ8),
   zone fit (BZ9).
4. **Frontline/mid rules added (2026-07-05, composer round 3).** From the author's frontline/mid
   teaching sets (`tools/seeds/teaching/mirror-mid-examples.plan.json` + `rot-90-mid-example-*`):
   split-vs-wide frontline + flush dock (FR6),
   rot_180-only variable-length parallel bands (FR7), the frontline rotation hole (CT9), rot_90 mid
   archetypes (CT10), band-step grids not chains (MD6), band depth / no long-thin band (BZ10), L/Z
   hub↔frontline composition (HB4), and the CT8 "hole = enclosed buffer" refinement. **CT11 is a
   correction**, not just an add: rot_90 team islands may sit along/atop the axes — the G32-A
   "≥10-block inter-image clearance" applies to team sides, not to mid pieces at the crossing; it
   refines CT2 and is the unblock for the p5/t4-rot_90 self-collision (G35).
5. **MD6 column count (2026-07-05, composer round 4).** Author: the mid stone grid runs **two
   lateral columns as the norm, three as the hard maximum** (three occurs in exactly one authored
   example); never wider — "wide, not too wide."
6. **CT11 extended to order-2 centre islands (2026-07-06, composer round 5).** The axis-sitting licence
   is not rot_90-only: under rot_180/mirror a mid stone straddling the axis (near edge at u=0) fans into
   one central island (or a v-symmetric pair, the `ex-10` form). Abutting/coincident images at the axis
   are one island; only interior overlap stays a clearance breach. Shipped as the composer's centre
   crossing (G36), with the pair form biased shallow so two 10×10 squares are the common pair.
7. **ST5 added (2026-07-31).** The build region is marked in the exported world, from the author's
   teaching seed `build-region-examples.plan.json` — five zones stating the offset, the void-facing
   edge test, the terrain clearance and the corner turn. New rule, no existing rule changed; ST1's
   entrance line is unaffected and stays powered, which is what distinguishes the two on sight.
8. **ST1/ST2 corrected: the piece sizes the stamp (2026-08-01, G31).** Author decision: the room
   shell is the piece inset one block, never a fixed 8×8 — the footprint rules, the parity-driven
   square pad, and the wall-parity door widths live as `WX1–WX7` in
   `docs/world-export/structures.md`, which governs the stamped geometry. ST1's entrance line also
   gains the build-zone interface (an abutting build zone is an entrance like any land seam — WX6),
   which was previously unstated.
9. **ST1 corner-chest facing corrected (2026-08-13).** The wool-cage corner chests stamped a fixed
   facing regardless of the room they sat in, so some opened toward a wall rather than into the
   room; ST1 now states the door-axis rule that fixes it. No other rule's stamped geometry changes.
10. **ST6 added (2026-08-13).** The destroyable platform — new rule, no existing rule changed.
11. **ST7 added (2026-08-13).** The goal sky marker — new rule, no existing rule changed.
12. **EL5 amended, EL6 retired as a lint, EL3 reworded (2026-08-14).** The `cliffs` mark is
    deleted from the plan document, the validator and the seeds. It was read in exactly one
    place — the EL6 lint it silenced — and no compiler, composer or stamper ever read it, so the
    annotation could not change the map it described. A cliff is what the terrain does where two
    surfaces meet; the only interface a plan authors on purpose is a wall. EL6's qualification
    survives as the reading `ReliefReadback` takes off a solved surface, and its corpus
    measurements are kept.
13. **Distance is the walk, not the line (2026-08-15).** Author's call, settling `B212`: every distance
    in these rules is traversal over the walkable surface. It changes no rule text — `WL9` and `WL10`
    already read "traversal distances" and "by surface traversal", and `WoolWoolDistance`'s docstring
    already states 4-connected rectilinear routing around voids — but it was nowhere stated for the
    family, so the preamble named the scale (blocks) and left the metric to be inferred, and it was
    inferred both ways. What it settles is which measure a *new* rule may be calibrated in. A straight
    line and a walk answer different questions about a board: the line is what a bow or an eye crosses,
    the walk is what a player carrying wool actually pays, and a separation rule is about the second.
    The consequence is that the corpus bands calibrated off `map.xml` region geometry — bucket 3's
    `B175`, `B179` and `B188`'s 164-map table — are in the wrong unit and must be re-measured, with the
    sweep committed this time rather than surviving as numbers in a backlog entry. `WL7`'s own
    46–143 band is unaffected: it was taken as traversal.

14. **The build ceiling is measured, and measured on the terrain (2026-08-15).** Author's call, settling
    `B221` and closing `B104`'s cause. `G6` asks for **≥20 blocks of build clearance above the
    island surface**; what was never stated is *which* surface, and every implementation so far had answered
    with the plan's nominal `surface` — a flat number the relief solve abandons, which is how boards came out
    with a ceiling under their own terrain and a destroy goal stamped above it. The rule is now: the cap is
    **the highest terrain column the world actually builds, plus 20**, at the floor of `G6`'s band rather than
    in the middle of it, for `G6`'s own second reason — a generous cap over flat terrain is the sky-layer
    smell. **Terrain, not what stands on it**: a house, a tree, a wool cage and a stamped structure are all
    excluded, or a taller shell would raise the ceiling that permits a taller shell.

    Two things follow. A plan states no ceiling at all — `PlanGlobals` lost the field, because a plan-level
    number would be a second source for one value and the one that gets overwritten. And **a goal marker's
    floor is the cap plus 5**, one rule for every goal kind, replacing the per-kind reasoning about how tall a
    destroyable's pillar or a core's casing happens to be. `G6`'s own observer note — "~5 above the build cap"
    — is the same distance and is unchanged.

15. **GO1 added (2026-08-16).** The destroy-goal spawn ratio, banded. The measurement shipped first
    (`GoalDistances`, the fanned-closure walk, amendment 13's unit) and the author stated the band off it:
    **[3.0, 4.0]**, fitting the two boards built to feel right (3.0, 3.9). New rule, new family (`GO`),
    no existing rule changed. The band lives on the term (`GoalSpawnRatio.AuthoredBand`), not in the
    generated envelopes — it is a ruling, not a distribution.

16. **WL8's sanction contract written in (2026-08-16).** No behaviour change: `ClosureAnalysis.AnyHoleRingedBy`
    has keyed the `wool-ringed-hole` sanction on the wool room's id prefix all along, but the contract was
    discoverable only by tripping the hard term — a hand author renaming `south-rim` to `wool-south-rim`
    found it the expensive way. WL8's text now states it.

17. **The piece-interface set: SP8, SP9, ST8, ST9, BZ11, FR8, CT12 added (2026-08-16).** Seven author
    rulings from the generated-board reviews, all lint (never a block), all quantifying over one
    per-interface read (`PieceInterfaces` over `ContactGraph` + the board deriver) rather than seven
    private derivations. Calibrated against both populations before landing: the corpus seeds and
    teaching sketches stay quiet except for Δ2 spawn ledges (SP8, four seeds — each built form wants its
    ramp), one-cell wall standoffs on cell-5 miniatures (ST8), and the traced board's 30-long spawn
    ground (ST9/SP9); the generated fault boards each fire on their measured fault (the foreshore funnel
    at share 0.25, the 90×15 hall, the flush strait, the void doorstep). FR8's absolute minimum front
    width is deliberately **left open** — the author's call is that the board's scale decides it and the
    example boards were never sized honestly — so FR8 lands as the share rule and the run widths are
    served raw by `POST /plan/inspect`. No existing rule changed; G5 (hop gaps) and CT12 (the strait)
    measure different things and coexist.

18. **PC-C moved out of this file (2026-08-22).** No behaviour change: the corner lint fires on exactly the
    boards it fired on before. The rule had been stated inside another id's bullet, which is the id the parser
    reads such a line as, so `GET /api/rules?rule=PC-C` answered an empty list while the lint raised it and
    `LayoutEvaluator` rejected boards for it. It is `PlanRules.CornerContact`, and this file cites it — the
    author's ruling that a rule may not live only in a markdown file, applied to the one layout id a gate
    raises under its own name.

19. **The catalogue serves only what is raised (2026-08-22).** No rule changed and none was deleted for
    being unraised: `GET /api/rules` answers the layout rules a plan lint, an evaluator term or a
    producibility finding can name — 34 of the 92 here — and `RulesEndpointTests` holds that set to the
    source in both directions. `BZ1` and `PC-S` left the file, both having said only that they were
    superseded, and `EL6` lost the account of its retirement as a lint and keeps the cliff qualification
    `ReliefReadback` applies.

20. **The walk is octile, and it is one walk (2026-08-23).** Author's call, settling `B246`. Amendment 13
    settled that a distance is a walk and left the walk itself to each caller, and fourteen of them had
    answered with a cardinal step count over whichever cell set was nearest to hand. The metric is
    `PgmStudio.Geom.Walk`: eight-connected octile — a straight step one block, a diagonal 1.41 — a climb of
    Δ charged the Δ−1 blocks a player places, a fall of three or less free, water at double, and three aims
    (travel, reach, comfort) a caller names rather than infers. Every distance band moved with it, because a
    diagonal is no longer paid for twice: the eight distance envelopes shrank by 0–26% over the 31 teaching
    maps, none by more than the 29.3% a pure-diagonal route can, and `seed-envelopes.md` carries the
    re-measured bands. No rule text changes and no rule's meaning changes — the bands are re-measured, not
    re-decided. The composer is untouched: its 72 fingerprint boards are byte-identical, because the metric
    is read after a board is composed and not inside the search. One band cannot be re-measured here and is
    marked instead: `WL7`'s stated 46–143 came off an eight-pair corpus sweep in the old unit, which
    amendment 13 called unaffected because it was already a traversal — true of the line-versus-walk
    question it was settling and not of this one. The evaluator reads the learned `wool-wool-distance`
    band rather than those numbers, so nothing judges a board by them; amendment 21 carries the mark, under
    the standing ruling that such a number is marked or replaced rather than re-swept.

21. **`GO2` and `GO3` added; `B175` and `B179`'s straight-line numbers retired (2026-08-23).** Author's call,
    closing `B212`. Two numbers had read as measured and were not: a 35-block minimum between a team's own
    destroy goals and a 95–110 target to the nearest enemy goal, both read straight-line off `map.xml` region
    centroids in the unit amendment 13 retired. They are replaced rather than re-derived, per the standing
    ruling that a corpus re-sweep buys precision this project does not need: **35–65** blocks between a team's
    own goals (`GO2`) and **85–150** between opposing ones (`GO3`), stated as walks in the octile unit and
    tagged `[expert]` — the author's numbers, held until a played board moves them. The two that cannot be
    replaced this way stay marked at their citation sites: `B188`'s 164-map table (`seed-stats.md`) and
    `WL7`'s eight-pair band.

22. **`WL11` added (2026-08-23).** Author's call, closing `B244`. `SP8` measures the seam ahead of a spawn
    door and complains at Δ≥2; nothing asked the same of a wool room, though the seam an attacker arrives
    across is met at the end of the run that decides the map. No existing rule changes: `WL11` is `SP8`'s
    reading applied to a room that has no facing, so every entry interface is the egress rather than the
    ones ahead of a door. The relief half of `B244` — a Δ the terrain creates under two pieces that declare
    the same surface — is closed by construction rather than linted: a structural room the author has not
    corrected is seated on the ground outside its own doors, so the plan number can no longer leave a room
    in a pit. A Δ≥2 face between two ordinary pieces stays unlinted, being a design element as often as a
    fault.

## Correction protocol

Reply by rule id. **Frozen 2026-07-04 as the composer's v1 rule set.** Further corrections are
**amendments**: applied in place, logged under *Resolved* with their round, and the composer
re-validated against them.

This file is **embedded in `PgmStudio.Domain`** and parsed, so `GET /api/rules` serves these rules rather than a
transcription of them (`docs/refusals.md`). What the parser needs is the shape already used throughout: a rule
begins at a `- **<id>` bullet and runs to the next one, its family comes from the `## <letters> — <name>`
heading above it, and its `[corpus]`/`[expert]`/`[open]`/`[guess]` tag becomes the evidence a reader is shown.
Amend freely inside that; a rule stated some other way is served with the wrong text or not at all, and the
path is in `PgmStudio.Domain.csproj` rather than in code, so moving the file breaks the build.

**The catalogue serves the rules the studio can cite, not all of these.** A plan-validator lint, an evaluator
term and a producibility finding are the three things that name a layout rule to a caller, and
`RuleCatalog.Raised` is the set they between them reach; `GET /api/rules` answers those and no others, because
the question it exists for is *what is this finding* and a rule nothing raises has no finding to explain. The
rest are law all the same, and this file is where the law is. A rule that starts being raised joins the
catalogue by being added to that set, which `RulesEndpointTests` holds to the source in both directions.
