# Lane decomposition — labeling rubric

How a human marks a CTW map's islands into lane pieces on the decompose surface (`/maps/{slug}/decompose`,
TODO `G6`). This is the **ground truth** the auto-cutter (`G6`) and the lane-width/role stats (`G3`) learn
from, so the category *semantics* below are load-bearing — keep them consistent across the corpus.

## How to cut: subtract the lanes, the hub is the residual
Peel off the genuine **wool lanes, spawn lane(s), and frontline** pieces; **whatever remains is the hub** —
do not carve the hub itself. So the hub can be large, irregular, and hole-bearing (kanto's hub keeps both
courtyard holes); a complex hub is expected, not a defect. The tool's iterative peeling supports this: each
cut removes one lane, the shrinking remainder ends as the hub.

Only **one side** of a symmetric map is marked; the rest is its mirror (`mirror_mode` saved with the cut).

## Categories
- **wool** — a lane dead-ending at a wool tip.
- **spawn** — the spawn lane / spur.
- **frontline** — a **lane of the dissected (team) island** that **touches a build region** — the island's
  edge onto the crossing / fighting space. It is frontline *because* it borders the buildable space, not by its
  shape. Reserved for pieces cut from the large island; a *standalone* island that happens to sit in the build
  region is a **stepping-stone**, not frontline. (Judging this needs the build regions visible — TODO `G8`.)
- **hub** — the core the lanes originate from (spawn + wools radiate from it). It **stays whole even when part
  of it borders a build region** — see *open mid* below. Do **not** split off the bordering sliver to separate
  the spawn/wool origins from the frontline edge; that would misrepresent the structure.
- **stepping-stone** / **mid** — neutral contested-middle **islands** (standalone, not part of a team island).
  A stepping-stone by definition sits in / touches the build region, but it is labeled **stepping-stone**, not
  frontline — `frontline` is only ever a lane of the dissected island.
- **decorative / outside-playable** — non-gameplay islands (excluded from the model).

## The build-region topology axis (directed-flow vs open-mid)
Two ways a map connects the teams across the void, which shape the frontline:
- **Directed flow** — stepping-stone islands joined by **individual bridge regions**; crossings are channeled,
  player flow is directed.
- **Open mid** — **one large build rectangle** over the void; players bridge freely island-to-island.

On an open-mid map the **hub** can itself touch the build rectangle. It is still **hub**, not frontline, and is
left whole. This axis feeds the frontline model (`G10`) and the contested-middle shape language (`G3`).

## Island-detection health (G9) — failure modes, role buckets, and the review flag
The simplified hull is only as good as the island detection feeding it. Two detection failure modes drift the
sketch from the real layout:

- **Over-split** (e.g. `a_new_day` / `a_new_day_ii`) — raised but **grounded** features (buildings, stairs,
  terraces) carved off a single connected landmass into spurious fragments. Root cause: the **cleaned base**
  excludes foliage/water/redstone noise, so a structure sitting on a foliage- or water-covered terrace reads at
  its **high** Y (the lowest *non-excluded* block), and the height-aware connectivity (`|Δ cleaned-base Y| ≤ tol`)
  then snaps it off the lower terrain even though you walk up onto it. A cleaned-base-vs-segment cross-section of
  `a_new_day` shows the tell: the carved cells are spruce/cobble **stairs** (block 67/134) over a terrace whose
  raw segments are continuous — i.e. one walkable mass, not a float.
- **Under-split / merged** (e.g. `abstract` / `abstract_remix`) — both teams read as one map-spanning island.
  Root cause (confirmed on the world + map.xml, *not* the old block-36/degenerate-fallback guess): the map sits
  on a **stained-glass build-floor at Y=0**. PGM auto-detects a low glass slab as buildable just like the
  invisible block-36 marker; abstract's `map.xml` makes this explicit — a `<destroyables materials="stained
  glass" mode-changes="true">` over the centre paired with `<modes><mode after="0s" material="air"/></modes>`
  **turns the glass floor to air at game start**, and the build region is defined explicitly as a
  `<complement id="build-region">` guarded by a `deny(void)` filter. The bottom-up cleaned base read that
  continuous glass slab as terrain and bridged the teams into one ~4937-cell mass (the base was *not* degenerate,
  so the y0/bedrock fallback never fired). **Fix: stained glass (95) joins the cleaned-base exclude, beside the
  {36} marker** (`LayerExtractors.CleanBaseExclude`). Because the base read is bottom-up-lowest, this only
  affects columns where glass is the *lowest* solid (a glass floor) — decorative glass walls/windows above other
  blocks are untouched. abstract then separates into symmetric team pairs (no longer flagged by
  `LooksUnderSplit`), with no change to the tested healthy maps (kanto/green_gem/two-quarter/vegas/thunder) or
  the over-split maps. (abstract's Y=0 floor is multi-material — bedrock/nether-brick also bound the extent — but
  those are scattered; the glass was the *continuous* bridge, so excluding it alone suffices.)

**The over-split fix: stair-aware connectivity.** A per-cell signal can't *flag* the over-split reliably (the raw
`layer_segments` include the water/foliage the cleaned base drops, so "is there solid beneath this cell" is fooled
by water at y0 and by hollow-shell builds; every corpus signal either fires on ~137 healthy maps or misses
`a_new_day`). The detection itself is fixed instead: `LayerExtractors.CleanColumns` reports, per column, the
lowest cleaned-solid Y **plus every standable surface** (a cleaned-solid block with no cleaned-solid above), and
`IslandDetector.DetectStairAware` joins two adjacent columns when **any** pair of their surfaces is within a step
(`stepTolerance`, default 3) — so a walkable staircase (surfaces one block apart per column) carries a raised
structure back onto the terrace it climbs from. Including each column's base level in the surface set makes this
**strictly additive** to the old height-aware base connectivity (it can only *merge* over-split fragments, never
split a team island or change the float prune), so it is safe to wire as the default detection
(`WorldFeatureWriter`, `--scan-out`, `--island-sketch`). A genuinely detached float shares no surface within a step
of the terrain, so it still splits off and prunes.

Validated by re-scanning the real worlds (`--island-stairaware`): targets improve — `a_new_day` 17→14,
`a_new_day_ii` 9→5, `thunder` 33→17 — by absorbing stair-connected structures, while **team-island count and
symmetry are preserved on every map** (kanto 2→2, green_gem 4→4, two-quarter 8→8, vegas 3→3, mame 2+2 majors). The
legacy `DetectCleaned` stays for the `--islands` Python-parity harness; the stored corpus `islands.json` reflects
stair-aware only after a re-scan. The **under-split / merged** mode (`abstract`) is fixed separately by the
stained-glass build-floor exclude above; `LooksUnderSplit` + the review flag remain the catch-all for any
other merged read.

**Classification (in `Analysis/Footprint/`):**
- **Semantic role by anchor** (`IslandRoleClassifier`) — the rubric above, computed: an island is **team** when
  it holds a spawn (the team `spawns[].region` — *not* `only-<team>` protection rules, which also guard wool
  rooms and so dropped spawn markers onto them), **objective** when it holds a wool but no spawn
  (`wools[].location`, wool-room region, or a wool-*dispensing* spawner region — economy spawners like mame's
  gold nuggets are skipped, and the capture **monument** is never an anchor — it sits on the enemy side),
  **neutral** when anchorless but intersecting a build region (a stepping-stone / mid), and **decorative** when
  anchorless and outside the build region (e.g. an observer island above max-build-height). Anchors resolve to
  footprints via `RegionGeometry2d` and test by intersection (robust to concavities — what missed mame's spawn
  before the stair-aware fix); build regions come from `RegionCategorizer`. Validated against the ground truth
  (kanto/thunder/annealing_iv/a_new_day/mame/green_gem).
- **Size buckets** (`IslandClassifier`, the anchorless fallback) — `major` (≥25% of the largest), `neutral`
  (≥64 blocks), `small` (specks). Plus the **under-split detector**: a symmetric N-team map should resolve into
  N comparable majors, so `majors < teams` flags the merged case (only ~22 corpus maps trip it, vs 137 false
  positives for the bbox heuristic).

**Surfaces:** `GET /map/{slug}/island-health` (roles + counts + `underSplit`), `GET`/`PUT /map/{slug}/island-review`
(the human's `{status, note}` flag — `status:"ok"` clears it), and the flag is echoed per map in
`GET /decompose/queue` (`reviewStatus`) so a reviewer can triage before cutting. The review path for a flagged
map is to re-detect (a smarter base / looser height params) and re-run `--store-island-sketch`.

## Wiring the role classifier into the decompose workflow
The classifier (`IslandRoleClassifier`) works one tier **above** the lane cuts — it decides *which* islands get
dissected vs. labeled whole — so it feeds the decompose tasks directly. The integration is built on **one
backend hook** the UI tasks consume.

**The hook — `GET /map/{slug}/island-roles`.** Per detected island (aligned to the `island-sketch` shape order,
which is 1:1 with the islands), returns `{ index, role, block_count, anchors: [{ kind: "spawn"|"wool", x, z }] }`
plus the **build-region outline** (the `RegionCategorizer` build geometry as a polygon). Pure backend, reuses
`IslandRoleClassifier.ExtractAnchors`/`Classify`/`BuildRegion`; testable without the browser. This is the
prerequisite for the three UI tasks below.

1. **Auto-fill the whole-island tags (`G7`).** The role *is* the whole-island category: `decorative` → tag
   `decorative` (excluded, no cut); `neutral` → tag `stepping-stone`/`mid` (no cut); `team`/`objective` → flag
   **for dissection** (the only islands the human peels). So the surface pre-tags the non-team islands and the
   human confirms + cuts only the 1–2 team islands per side, instead of tagging all N from scratch. Optionally
   pre-seed those whole-island tags into `lane_decomposition_json` on first open.
2. **Anchors + build region become the cut overlays (`G8`).** `G8`'s "wool tip / spawn spur" and "declared build
   areas" overlays *are* the classifier's anchor footprints + build-region outline — already computed, just
   surfaced as overlay layers. No separate objective/build read needed; the hook carries them.
3. **Anchors seed the auto-cutter + auto-label its pieces (`G6`).** Within a team island the anchors are the lane
   *targets*: a piece dead-ending at a wool anchor is `wool`, a piece containing the spawn anchor is `spawn`, a
   piece whose edge meets the build region is `frontline`, the residual is `hub`. So the anchors both seed where
   the medial-axis cutter places seams (hub→wool tip, hub→spawn) and label each resulting piece without asking.
4. **Queue triage.** `GET /decompose/queue` can carry each map's role breakdown (`{ team, objective, neutral,
   decorative }`); a map with **0 team islands** sorts to the top next to the review flag (a likely detection
   failure to fix before cutting).

Caveats inherited from the workflow: the roles only reflect the new detection on **re-scanned** maps (the stored
corpus is still old islands — `G9`), and decompose marks **one symmetric side**, so the surface uses that half of
the per-island roles.
