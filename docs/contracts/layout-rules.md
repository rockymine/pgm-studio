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
  cut line — is the boundary the rules are based on.
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

## Correction protocol

Reply by rule id. **Frozen 2026-07-04 as the composer's v1 rule set.** Further corrections are
**amendments**: applied in place, logged under *Resolved* with their round, and the composer
re-validated against them.
