# Layout rules — the expert checklist (v3, corpus-measured)

The generator's actual content: per-role attachment rules, dimensions, and elevation defaults for
the plan composer (`docs/contracts/layout-generation.md` §3). v3 measures every rule against the
**nine-seed corpus** (`tools/seeds/*.plan.json`, sweep script `seed_stats`); this is the **freeze
candidate** — three open questions at the end block the freeze. Tags:

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
  legitimately exceed the old cap. Numbers remain **decoupled from player count** — every seed
  still carries the stale `maxPlayers: 12` — so the envelope↔G8 coupling is re-derived in the
  parked maxPlayers pass before the composer treats these as sampling bounds.
- **G4 [corpus]** `rot_180` (2 teams) / `rot_90` (4 teams) are the defaults; `mirror_x`/`mirror_z`
  are valid and exercised end-to-end (`mirror-big-board` compiles and exports through the
  reflection fan).
- **G5 [expert, open]** Void gaps between *individual* landmasses: **10–20**; total crossing
  40–60. The corpus contains two deliberate over-limit hops (**25** in `four-team-towers-big`,
  **30** in `odd-facing-three-wool`) — whether the band loosens (e.g. ≤30 for secondary bridges)
  or those stay lint-nagged exceptions is *Open question 3*.
- **G6 [expert]** Build headroom above the island surface: **≥20, up to ~40**. Island terrain
  height **5–20**. **The sky-layer smell:** low, flat terrain under a tall build cap casts a
  second play layer into the sky — players dig the base to bedrock, defend from above, and the
  match stalemates as coverless sky bow-fighting. So terrain wants height variation, and the cap
  is calibrated to the terrain, not set generously. Observer platform: out of player reach —
  **~5 above the build cap**, or off to the side beyond any build zone.
- **G7 [expert]** Follows G5: a single required-path hop stays ≤ **~20**; anything longer is a
  chain of hops (stones) summing to the 40–60 total crossing.
- **G8 [expert, new]** Map size is driven by the intended player count — `maxPlayers` is an
  *input* to the board envelope, not an afterthought.

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
  highest plateau). Common band **+4..+8**.
- **SP5 [expert]** Spawn structure (cube, protection) is stamped at export; the stamp style may
  evolve. The plan reserves the area and floor level only.
- **SP6 [corpus]** Spawn **can** be `gap`-only (an isolated spawn island) — `isolated-spawn` and
  `isolated-spawn-approaches` both build it.
- **SP7 [expert]** Resource placement (iron): **beside or ahead** of the spawn — players face
  mid and must see it. Iron *behind* the spawn is a bad smell (unseen, dead space). Corpus: 3
  beside, 1 ahead, and **one violation** — `four-team-wool-two-sided` has iron 10 blocks directly
  behind its left-facing spawn (suspected authoring slip; author to confirm or fix the seed).

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
  widespread value**.
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

## MD — Mid / stepping stones

- **MD1 [expert]** Stones vary: 2×2 cells, 2×3, larger. Raised, level, or (rarely) **lowered**
  relative to base terrain.
- **MD2 [expert]** Gap values per G5 (10–20 per hop).
- **MD3 [expert, reframed]** The deeper model than "stone count": a team's side reads as a
  **once-connected island the author cut apart** — cut a piece, displace it across a void, bridge
  it with a build zone. Purpose: harder/riskier wool access, defenders slowed between spawn and
  wool, retreat over fragile player-made bridges instead of solid terrain. Fragmentation is a
  **composer operator** (design doc §3), not a mid-piece parameter.
- **MD4 [expert]** Stones sit entirely inside the build zone.
- **MD5 [expert]** Large neutral mid islands: rare; **not v1**.

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
- **BZ5 [open]** Granularity undecided (*Open question 1*): "never touch the spawn's **piece**"
  keeps flagging purpose-built seeds whose spawn piece extends to the front; "never within N
  blocks of the spawn **marker**" is likely the intended meaning. Wool rooms **may** be touched
  in alternative-approach variants (WL8).

## EL — Elevation

- **EL1 [corpus]** Plateau step unit: **2**. The corpus surface palette is base 9 + even steps
  (9/11/13/15/17/19 — all odd values), so every one of the 137 measured land-interface deltas is
  even by construction: histogram Δ0 ×47, Δ2 ×73, Δ4 ×10, Δ6 ×4, Δ8+ ×3.
- **EL2 [expert]** Height deltas across `gap`s work **both ways**: attacker builds up and arrives
  low (defensive device), or the defended wool sits low and the defender holds height advantage
  *inside* the room.
- **EL3 [expert]** `land` interfaces: walkable step ≤1; 2–3 only as an explicit jump/ledge
  feature; ≥4 only as a `cliff` or via building.
- **EL4 [expert]** Per island: base + up to **2** raised sections (not 1). Roughen never changes
  levels, only outlines.
- **EL5 [expert, open]** Cliffs (one-way drops): **in v1** — but the `cliffs` field is still
  unused while the corpus carries **17 unmarked Δ≥4 seams** (mirror-big-board ×8,
  rotate-wide-frontline ×6, odd-facing-three-wool ×3, incl. one Δ8). Marking them is *Open
  question 2*.

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

## Remaining seed work

The nine-seed corpus covers the original shopping list except:

1. **cliff-marked seed** — the `cliffs` field's first real use; cheapest path: mark the intended
   one-way drops in `odd-facing-three-wool` (Open question 2).
2. **fragmented-island** — the MD3 pure form: a piece of the team's *own* middle (neither spawn
   nor wool) displaced across a void and bridged.
3. **maxPlayers pass** — honest player counts on all nine seeds, re-deriving the G3↔G8 envelope
   coupling (parked by the author).
4. *(fix)* `four-team-wool-two-sided`'s behind-spawn iron (SP7) — confirm slip or intent.

## Open questions (freeze blockers)

1. **BZ5 granularity** — spawn *piece* untouchable, or *marker* proximity (N blocks)? The
   composer needs one to place zones.
2. **Cliff marks** — the 17 unmarked Δ≥4 seams (incl. `odd-facing-three-wool`'s +8): mark as
   `cliffs`, add intermediate steps, or raise EL3's threshold?
3. **G5 band** — keep 10–20 (the corpus's 25/30 hops stay lint-nagged exceptions) or widen for
   secondary bridges (≤30)?

## Correction protocol

Reply by rule id. When the three open questions are answered and the two remaining seeds exist,
this document **freezes as the composer's v1 rule set**.
