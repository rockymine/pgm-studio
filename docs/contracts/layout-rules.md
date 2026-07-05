# Layout rules — the composer's v1 rule set (v3, frozen 2026-07-04)

The generator's actual content: per-role attachment rules, dimensions, and elevation defaults for
the plan composer (`docs/contracts/layout-generation.md` §3). v3 measures every rule against the
**twelve-seed corpus** (`tools/seeds/*.plan.json`; the eleventh, `big-board-…-parallel-mid`, is a
**trace of a real map** at 30/team; the twelfth, `mirror-tiny-map-cliff`, the tiniest at 5/team).
**FROZEN 2026-07-04**: the maxPlayers pass completed — every seed carries the author's per-team
count — closing the last blocker. Changes from here are **amendments** via the correction
protocol. Tags:

- **[corpus]** — confirmed by measured seed evidence (seeds cited).
- **[expert]** — author-stated, not yet exercised by a seed.
- **[open]** — awaiting the author's call (see *Open questions*).
- **[guess]** — still mine.

Rules are numbered for correction by id. Distances in blocks unless marked *cells*. "Front" =
toward the map centre / the enemy; "back" = toward the map edge.

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
  **46–143** blocks (46.1 / 58.3 / 64 / 70 / 75 / 85.6 / 95.5 / 143). Working minimum ≈**45**.
- **WL8 [expert, new]** Wool approach routes: the default is a **single chokepoint route**;
  real maps sometimes add **alternative routes** to the wool (and then a build zone may touch the
  wool room — see BZ5). **[seed-needed]**

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
  corners. Don't: the `gen-p30-t2-rot_180-s1` vertical stone-chain
  (`docs/composer-review-findings.md`). On a wide frontline the stone edges align with the
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
  ten-seed corpus (`docs/seed-stats.md`, "Island gradient sweep"): islands **grow** with distance
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
- **CT11 [author] — rot_90 islands may sit along/atop the axes [corrects the inter-image clearance
  invariant; refines CT2].** A team's rot_90 islands are **not confined to one quadrant**: a
  frontline may **straddle the x=0 / z=0 axis** — `rot-90 ex-6` `frontline` [-1,-5,2,2] and
  `ex-7`/`8` [-3,-5,4,2] all straddle x=0 — and its four fanned images **abut cleanly at the axis**
  to form the plus/cross mid. That is the move a near-axis-but-off-axis piece cannot make (its
  quarter-turn image self-collides — the p5/t4-rot_90 infeasibility, G35). This **corrects** the
  G32-A grower's blanket "≥10-block clearance between all orbit images": the clearance keeps the
  **team sides** separate islands, but **mid pieces at the crossing (frontlines, mid stones) may
  reach and sit on the axes.** The unblock for the rot_90 self-collision.

## BZ — Build zones

- **BZ1 [expert]** Superseded by FR1+FR2: zones are authored precisely in the plan editor;
  terrain overlap is permitted, not meaningful.
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
sketch `tools/seeds/build-interface-dos-and-donts.plan.json` + the review
`docs/build-zone-failure-modes.md` (per-example ids there). The sketch informs **build-zone rules
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
  feature; ≥4 is either a **cliff** (per EL6, needing a `cliffs` mark) or a **stepped path edge**
  (no mark — the seam borders a staircase route).
- **EL4 [expert]** Per island: base + up to **2** raised sections (not 1). Roughen never changes
  levels, only outlines.
- **EL5 [corpus]** Cliffs (one-way drops): in v1 and now in use — `odd-facing-three-wool` marks
  3 (incl. the pit pair), `mirror-big-board` 2 (the long spawn-side seam + the 15-long seam).
- **EL6 [expert, new]** **Cliff qualification** — what separates a real cliff from a stepped path
  edge (the seam beside a staircase/hairpin): a cliff (a) cuts the **full width of a lane**,
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

## PC — Pieces are anonymous

- **PC-S retired [expert]** The old per-seam *sliver* lint (PC-S — a shared border below the corridor
  minimum flagged as suspect) is **gone**: a narrow seam is legal connecting geometry per *Definitions*,
  so there is no per-seam width lint. Corridor quality of an assembled footprint is measured by
  lane-chain analysis, not seam by seam. **PC-C stays** — a bare corner between pieces not already in the
  same land component is still linted (a point never connects).

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

- **ST1 [expert]** *Wool room piece* (optional): defines the full room **region**; its footprint is
  stamped **solid bedrock from y=0 to its floor** (no tunnelling in from below); a **redstone line
  with a torch at either end** lies on the last block row at the room's entrance interface — the
  conventional marker for where entrance protection begins. The editor renders terrain↔wool-room
  interfaces **red**.
- **ST2 [expert]** *Spawn piece* (optional): defines the spawn **region**. Iron placed inside it is
  **auto-renewed** in the generated XML (load-bearing for gameplay); lint: when a spawn piece
  exists, iron markers belong inside it. Spawns have no redstone line.
- **ST3 [expert]** *Iron structure*: an iron marker stamps a **4×4×4 iron-block cube**.
- **ST4 [corpus]** *Pre-built wall*: 2 blocks thick, full seam width, top = approach side +4,
  down to y=0. Corpus pattern (11 walls over 5 seeds): walls sit on **gentle seams** — every
  marked interface has Δ ∈ {0, ±2} and border 10–15; nobody walls a cliff. Narrow seams are
  legal wall carriers.

## Facing semantics [expert]

Marker `facing` is **absolute board directions** — front = −z, back = +z, left = −x, right = +x
on the authored unit, fanned per orbit image. (The editor always meant this; the compiler briefly
interpreted "front" as toward-the-centre, which mis-yawed four seeds and mis-measured one iron —
both corrected.)

## Remaining seed work

The eleven-seed corpus covers the entire original shopping list (the eleventh, the traced
`big-board-…-parallel-mid`, additionally delivered the parallel-mid example, the below-base
palette, the plaza hub, and the 35 crossing at 30/team):

1. ~~cliff-marked seed~~ — **done**: five `cliffs` marks landed (mirror-big-board ×2,
   odd-facing ×3) under EL6.
2. ~~fragmented-island~~ — **resolved without a seed**: fragmentation is the closure reading
   (CT1–CT6); every seed is a fragmented closure and the interface statistics are already
   measured.
3. ~~maxPlayers pass~~ — **complete**: all twelve seeds carry the author's per-team counts
   (the last five: isolated-spawn 14 ✓ proposal, towers 18, mirror 32, wool-two-sided 12,
   odd-facing 16 ✓); the G8 coupling is derived from the full corpus. **No freeze blockers
   remain — v3 is frozen.**
4. ~~behind-spawn iron~~ — retracted (facing-semantics artifact; the iron is ahead).

## Resolved this round (was: freeze blockers)

1. **BZ5** — retired as a prohibition; the defender-egress bridge at spawn is a motif.
2. **Cliffs** — EL6 qualification encodes the author's criteria; marks added to mirror-big-board
   (2) and odd-facing (3); rotate-wide-frontline's seams are stepped edges, unmarked by design.
3. **G5** — refined to the region's minimal crossing (long hops beside shorter ones are fine).
4. **SP7 "violation"** — retracted (facing-semantics measurement bug; iron is ahead).
5. **CT1/CT4 revised per the author** — no team-separation cut exists (the symmetry axis plays
   that part); the mid is the *interface between team territories*, always connected through
   bridge zones. Carving replaced cutting as the mid operator (CT5); team-side vs mid assignment
   is spawn+wools+minimal-connectors vs centre-proximity (CT2); the island-size gradient and
   stepping-stone falloff measured and confirmed (CT4).
6. **CT1 forms author-labeled; stones corrected** — the mid-form taxonomy is now
   clean / parallel / hash (grid = alignment property, not a form; a grid of islands in one
   connected region is *clean*, not hash — `rotate-wide-frontline`, `base-4team`; hash requires a
   fractured or holed build region with interconnected mid islands — `four-team-towers-big`,
   `four-team-wool-two-sided`, `isolated-spawn-approaches`). Stone classification gained the
   marker exclusion and the team transient-link split (CT4), which sharpened the falloff to
   **17/4/0**; stone/team-island alignment became CT7.

## Amendments (post-freeze)

1. **CT8 added (2026-07-04, composer round 1).** Internal holes / rotation loops — author-stated
   during the composer build-out, then measured universal (12/12 seeds at closure level; sweep in
   `docs/seed-stats.md`, "Internal-hole sweep"). New rule, no existing rule changed.
2. **CT8 function split by ring (2026-07-04, composer round 1).** Author: wool in the hole's
   ring ⇒ the two-approaches pattern, else rotation. Corpus-validated with two refinements: the
   WL1 two-sided rooms are a seam-mediated sibling (not the same device), and the all-zone ring
   is a fourth formation mechanism. The naive correlation "land-only-hole seeds = two-sided-wool
   seeds" was tested and REJECTED (1 of 4, relaxed reading only) — the ring test, not the
   land/closure distinction, carries the function. Author-confirmed: bridged approaches count
   (the base-seed `bar-w` holes are two-approach wools).
3. **BZ6–BZ9 added (2026-07-04, composer round 2).** Build-zone interface discipline from the
   author's review of the first generated mids (`docs/build-zone-failure-modes.md`) + the curated
   teaching sketch `build-interface-dos-and-donts.plan.json`: mid never touches wool (BZ6), dock
   don't overlap with the plaza-encasement exception (BZ7), connector-extrusion readability (BZ8),
   zone fit (BZ9).
4. **Frontline/mid rules added (2026-07-05, composer round 3).** From the author's frontline/mid
   teaching sets (`tools/seeds/teaching/mirror-mid-examples.plan.json` + `rot-90-mid-example-*` +
   remarks in `docs/composer-review-findings.md`): split-vs-wide frontline + flush dock (FR6),
   rot_180-only variable-length parallel bands (FR7), the frontline rotation hole (CT9), rot_90 mid
   archetypes (CT10), band-step grids not chains (MD6), band depth / no long-thin band (BZ10), L/Z
   hub↔frontline composition (HB4), and the CT8 "hole = enclosed buffer" refinement. **CT11 is a
   correction**, not just an add: rot_90 team islands may sit along/atop the axes — the G32-A
   "≥10-block inter-image clearance" applies to team sides, not to mid pieces at the crossing; it
   refines CT2 and is the unblock for the p5/t4-rot_90 self-collision (G35).
5. **MD6 column count (2026-07-05, composer round 4).** Author: the mid stone grid runs **two
   lateral columns as the norm, three as the hard maximum** (three occurs in exactly one authored
   example); never wider — "wide, not too wide."

## Correction protocol

Reply by rule id. **Frozen 2026-07-04 as the composer's v1 rule set.** Further corrections are
**amendments**: applied in place, logged under *Resolved* with their round, and the composer
re-validated against them.
