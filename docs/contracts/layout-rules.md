# Layout rules — the expert checklist (v2, first correction round)

The generator's actual content: per-role attachment rules, dimensions, and elevation defaults for
the plan composer (`docs/contracts/layout-generation.md` §3). v2 folds in the author's first
review pass — **provisional**: `[expert]` answers stand until the author declares them final or a
seed contradicts them. Tags:

- **[expert]** — author-corrected (provisional).
- **[seed]** — measured from the seeds, not yet contradicted.
- **[seed-needed]** — the author will pin this with a dedicated authored seed (see the shopping
  list at the end).
- **[guess]** / **[?]** — still mine / still open.

Rules are numbered for correction by id. Distances in blocks unless marked *cells*. "Front" =
toward the map centre / the enemy; "back" = toward the map edge.

## Definitions

- **Lane** — an elongated transit piece: length noticeably exceeds width, it carries flow between
  a dead end / objective and a junction. Not the hub (the junction residual the lanes originate
  from), not a stepping stone (standalone).
- **Connection (a `land` interface)** — two pieces connect only where they share a **straight
  border segment at least one corridor width long** (≥ G2). No corner-touching, no sliver
  doorways: every place two pieces meet must itself be a walkable corridor mouth.

## G — Globals

- **G1 [expert]** Grid cell **5** by default, but a parameter (4 is viable for finer detail). More
  fundamentally: the plan is a **mini layout** — the checkered-paper scale *proxy* map authors
  already draw, not block-true dimensions. Grid-born "artificial" distances are expected and are
  resolved downstream by the scale + roughen passes (design doc §2, "the plan is a mini layout").
- **G2 [expert]** Minimum corridor width **10**; larger maps trend toward **15**.
- **G3 [expert]** 2-team boards: width normally **40–60** (up to 100 exists; wide/thin centres
  both real), length up to **~200** on large maps. 4-team square, up to **~240**. See G8.
- **G4 [expert]** `rot_180` (2 teams) / `rot_90` (4 teams) are the defaults; `mirror_x`/`mirror_z`
  are valid, less common in the wild, and the model supports them.
- **G5 [expert]** Void gaps between *individual* landmasses: **10–20**. The **total crossing**
  (all hops summed, one team's terrain to the enemy's half): **40–60**.
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
  with no purpose.
- **SP3 [expert]** Faces the enemy. Rare exceptions exist. **[seed-needed]**
- **SP4 [seed-needed]** Raised-spawn variant (overview) — the current seeds are deliberately flat;
  a dedicated seed will pin height + extent.
- **SP5 [expert]** Spawn structure (cube, protection) is stamped at export; the stamp style may
  evolve. The plan reserves the area and floor level only.
- **SP6 [expert]** Spawn **can** be `gap`-only (an isolated spawn island). **[seed-needed]**
- **SP7 [expert, new]** Resource placement (iron): **beside or ahead** of the spawn — players face
  mid and must see it. Iron *behind* the spawn is a bad smell (unseen, dead space).

## WL — Wool room

- **WL1 [expert]** At the far/back end of a dead-end lane, inset ~**5**. Wools approachable from
  **two sides** also exist in real maps. **[seed-needed]**
- **WL2 [expert]** On a different lane than the spawn; wool↔spawn ≥ **20**.
- **WL3 [expert, clarified]** The plan records only the wool's position and floor level; the
  physical room (cage, pedestal — the 8×8 stamp today) is stamped at export. Requirement on the
  plan: the wool sits on a **flat plateau covering at least the stamp footprint and extending to
  the lane edges** (even in a 15-wide lane, the room area is flat edge-to-edge).
- **WL4 [expert]** Isolated-wool variant: the connecting `gap` is commonly **10–20**; the height
  delta varies in size *and sign* (see EL2).
- **WL5 [expert]** Wool-approach elevation: the room plateau itself is flat (WL3); the approach
  raises by up to **+6** total, as **steps 1–5 blocks deep** (varied step sizes, not a smooth
  ramp by default).
- **WL6 [expert]** 1–3 wools per team, each on a **distinct** lane.
- **WL7 [seed-needed]** Minimum separation between a team's wools — to be pinned by multi-wool
  seeds.
- **WL8 [expert, new]** Wool approach routes: the default is a **single chokepoint route**;
  real maps sometimes add **alternative routes** to the wool (and then a build zone may touch the
  wool room — see BZ5). **[seed-needed]**

## LN — Lane

- **LN1 [expert]** Width **10** base (15 on larger maps); may exceed **20** mid-map or near
  spawn. The stretch **in front of a wool stays ≤ ~16** — wider than that stops reading as the
  wool's lane.
- **LN2 [expert]** Length **20–50** before a junction or dead end; a lane may include a
  turn/twist (the L-shape case).
- **LN3 [expert]** Wool lanes dead-end at the back; the front end stops at the void edge
  (frontline) or at the hub. (Lane defined above.)
- **LN4 [expert, clarified]** Restated plainly in *Definitions*: pieces join only along a shared
  straight border ≥ one corridor width — never at corners, never through slivers.

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
- **FR3 [expert]** Defenders-hold-high-ground-behind-the-frontline is **common, never strict** —
  frontline **tower** structures are a valid, interesting motif.
- **FR4 [expert, split]** Two distinct "angles of attack":
  - **Team approaches** — ways to reach the enemy's side: **1–3**; 1 is acceptable only if it is
    wide.
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
- **BZ3 [expert]** Directed bridge: **10** wide, spanning one gap.
- **BZ4 [expert]** 4-team: zones connect all teams; often with a **hole at the centre** so
  players must walk/bridge around it rather than straight across.
- **BZ5 [expert]** Build zones never touch a **spawn** piece. They **may touch a wool room** in
  alternative-approach variants (WL8).

## EL — Elevation

- **EL1 [expert]** Plateau step unit: **2** (not 4).
- **EL2 [expert]** Height deltas across `gap`s work **both ways**: attacker builds up and arrives
  low (defensive device), or the defended wool sits low and the defender holds height advantage
  *inside* the room.
- **EL3 [expert]** `land` interfaces: walkable step ≤1; 2–3 only as an explicit jump/ledge
  feature; ≥4 only as a `cliff` or via building.
- **EL4 [expert]** Per island: base + up to **2** raised sections (not 1). Roughen never changes
  levels, only outlines.
- **EL5 [expert]** Cliffs (one-way drops): **in v1**.

## Seed shopping list

The `[seed-needed]` roster — each authored seed pins one open rule; names are suggestions:

1. **raised-spawn** — spawn plateau above its lane (SP4): how high, how far it extends.
2. **isolated-spawn** — spawn piece connected by `gap` only (SP6).
3. **wool-two-sided** — a wool approachable from two directions (WL1/WL8), incl. whether a build
   zone touches the room (BZ5).
4. **multi-wool-spread** — 2–3 wools pinning the separation rule (WL7).
5. **fragmented-island** — the MD3 cut-apart team side: a mid piece of the *team's own* landmass
   displaced across a void and bridged.
6. **frontline-tower** — a raised structure *at* the frontline (FR3 counter-example).
7. **elevation-rich** — step unit 2, two raised sections, a stepped wool approach (+6, 1–5-deep
   steps), a cliff (EL1/EL4/EL5/WL5).
8. **big-board** — 15-wide lanes, longer body, player-count-scaled (G2/G3/G8).
9. **four-team-hole** — rot_90 with a central hole in the build zone (BZ4).
10. **mirror-mode** — a `mirror_x`/`mirror_z` layout (G4).
11. *(optional)* **odd-spawn-facing** — the SP3 exception, if a real motif is worth keeping.

## Correction protocol

Reply by rule id: a number ("SP2: 10, not 5"), a veto ("HB3: no, roughen's job"), or a missing
rule ("new WL9: …"). When the author declares the `[expert]` answers final and the
`[seed-needed]` seeds exist, this document is the composer's v1 rule set.
