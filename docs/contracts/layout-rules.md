# Layout rules — the expert checklist (strawman v1)

The generator's actual content: per-role attachment rules, dimensions, and elevation defaults for
the plan composer (`docs/contracts/layout-generation.md` §3). This v1 is a **strawman extracted
from the three seeds** (`tools/seeds/`) for the author to correct — every rule is tagged:

- **[seed]** — measured directly from the seed files; presumed intentional.
- **[guess]** — proposed by extrapolation; needs the author's number or a veto.
- **[?]** — open question; the seeds don't answer it.

Rules are numbered for correction by id ("WL3: no, 15"). Distances in blocks. "Front" = toward the
map centre / the enemy; "back" = toward the map edge.

## G — Globals

- **G1 [seed]** Authoring grid: 1 cell = **5 blocks**; all piece bounds, interface intervals, and
  objective placements sit on the 5-block grid (pre-roughen).
- **G2 [seed]** Minimum corridor width: **10** (2 cells). Every walkable piece dimension in the
  seeds is ≥10.
- **G3 [seed]** Board envelope: 2-team ≈ 40–100 × 140 (void axis long); 4-team square ≈ 140×140.
- **G4 [seed]** Symmetry: `rot_180` for 2 teams, `rot_90` for 4. **[?]** is `mirror_z` (true
  mirror, not point rotation) wanted as a 2-team alternative?
- **G5 [seed]** Void between opposing team islands along the void axis: **40** at the closest
  team-island approach; stepping-stones subdivide it into hops of **5–10**.
- **G6 [seed]** Build max height = base terrain + **11** (terrain 9, cap 20). Observer platform at
  base + **15**.
- **G7 [guess]** Max hop a player is expected to bridge without stepping stones: the seeds' longest
  unaided span is 10 (bridge) and 40 (open mid, with two stones). Proposed rule: a `gap` interface
  wider than **15** on a *required* path needs a stepping-stone chain or is rejected.

## SP — Spawn

- **SP1 [seed]** The spawn sits on its **own lane**, never on a wool's lane; the paths
  frontline→wool never pass through the spawn piece.
- **SP2 [seed]** Depth: near the **back** of its lane — **5** from the back end, ≥**25** from the
  lane's front end.
- **SP3 [seed]** Faces the enemy (yaw toward mid).
- **SP4 [seed]** Same level as its lane (terrain 9) — the seeds do **not** raise the spawn.
  **[?]** the author has described raised spawns for overview: is that a default (+how much?), an
  optional variant, or noise?
- **SP5 [seed]** Spawn structure (cube, protection) is stamped at export — not a plan concern
  beyond reserving the piece area.
- **SP6 [guess]** Spawn piece is never `gap`-only (isolated spawn is a deliberate rare variant the
  composer must be *told* to make, not sample).

## WL — Wool room

- **WL1 [seed]** At the **far/back end** of a dead-end lane, inset **5** from the dead end.
- **WL2 [seed]** On a **different lane** than the spawn; straight-line wool↔spawn distance ≥**20**
  (seed: ~23).
- **WL3 [seed]** The wool sits at the lane's terrain level (no pedestal at plan level; the cage is
  stamped at export).
- **WL4 [seed]** *Isolated wool* variant: the wool piece's only interface is one `gap` of width
  **10**, span **10**, bridged by a dedicated build zone; the isolated piece is raised **+4** over
  the main island.
- **WL5 [guess]** Wool-approach elevation (the author's "harder approach"): the last **10–15**
  blocks of a wool lane step **up** toward the room by **+2..+4** (as a `step`/`ramp` land
  interface from a cut). Default on? amplitude?
- **WL6 [guess]** Per-team wool count 1–3; each wool on a **distinct** lane (never two wools one
  lane).
- **WL7 [?]** Minimum separation between a team's two wools (the seeds' two are ~40 apart on
  perpendicular axes) — is there a rule, or is "distinct lanes" sufficient?

## LN — Lane

- **LN1 [seed]** Width **10** (2 cells); roughen may vary it later within [**~7**, **~15**]
  **[guess]** — floor/ceiling?
- **LN2 [seed]** Length **30–45** before a junction or dead end.
- **LN3 [seed]** Wool lanes dead-end at the **back**; their **front** end stops at the void edge
  (becoming frontline) or at the hub.
- **LN4 [seed]** Lanes join through a connector/hub piece (the crossbar), never by grazing corners;
  a `land` interface is a full shared interval of width ≥**10**.

## HB — Hub / connector

- **HB1 [seed]** The crossbar: width **10**, connecting two parallel lanes at roughly the middle of
  their overlap (seed: centred on the shorter bar).
- **HB2 [seed]** Every frontline→wool path crosses ≥1 hub/connector piece — the interior fight
  happens at the junction, not on the wool lane's full length.
- **HB3 [guess]** A hub may widen into a plaza (the corpus/OrganicLane habit) — 1.5–2× lane width.
  Wanted at plan level, or is that roughen's job?

## FR — Frontline

- **FR1 [seed]** The frontline is the set of lane front-ends facing the void; the open-mid build
  zone **overlaps the island by 5** (1 cell) at these ends, so bridging starts on land.
- **FR2 [seed]** Directed bridges instead **abut** (zero overlap): the bridge zone spans exactly
  the 10-block gap edge-to-edge. **[?]** intentional distinction, or should bridges also overlap 5?
- **FR3 [seed]** Frontline pieces are the island's **lowest** level (terrain 9; everything raised
  is behind/above). **[?]** confirm as a rule: defenders hold high ground behind the frontline.
- **FR4 [guess]** Angles of attack per team: 1–3 `gap` interfaces reach the frontline; 2 default
  (one open front or two directed bridges).

## MD — Mid / stepping stones

- **MD1 [seed]** Stepping stone: **10×10** (2×2 cells), raised **+4** over base terrain.
- **MD2 [seed]** Gaps: **5** stone↔island, **10** stone↔stone across the centre line.
- **MD3 [seed]** Count: 1 per side (mirrored pair). **[guess]** allow 1–3 pairs on larger boards.
- **MD4 [seed]** Stones sit entirely inside the build zone.
- **MD5 [?]** Are larger neutral mid pieces (a contested centre island with its own geometry, not
  just stones) part of the v1 vocabulary?

## BZ — Build zones

- **BZ1 [seed]** Open-mid: one central band, **30** wide across the flow axis (board 40 → leaves a
  5-block unbuildable margin each side), spanning the void + 5 overlap onto each island.
- **BZ2 [seed]** The band's width is **less than the island width** — the lane backs (spawn, wool)
  are *outside* the buildable area, so nobody bridges straight to a wool room.
- **BZ3 [seed]** Directed bridge: **10** wide, spanning one gap (10), abutting both pieces.
- **BZ4 [seed]** 4-team: one build rect per arm, overlapping at the centre so all teams connect.
- **BZ5 [guess]** Build zones never touch a spawn or wool-room piece (BZ2 generalized to a rule the
  validator enforces).

## EL — Elevation

- **EL1 [seed]** Base terrain **9**; the plateau step unit is **+4** (raised stones, isolated wool
  island at 13).
- **EL2 [seed]** A **+4** delta between `gap`-joined pieces means the attacker builds *up* to
  arrive — arriving low. Confirmed as a device?
- **EL3 [guess]** `land` interfaces: walkable step ≤**1** per block travelled; **2–3** only as an
  explicit jump/ledge feature; ≥**4** only as a `cliff` (one-way drop) or via building.
- **EL4 [guess]** Per-island level count: 1–3 plateaus (base, one raised section, optionally the
  wool approach), not a heightmap. Roughen never changes levels, only outlines.
- **EL5 [?]** Cliffs (one-way drops) in v1 — yes/no?

## Correction protocol

Reply by rule id: a number ("SP2: 10, not 5"), a veto ("HB3: no, roughen's job"), or a missing rule
("new WL8: …"). Accepted corrections replace the tag with **[expert]** — when no **[guess]**/**[?]**
tags remain, this document is the composer's v1 rule set.
