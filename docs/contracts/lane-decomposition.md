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
- **Under-split / merged** (e.g. `abstract` / `abstract_remix`) — both teams read as one map-spanning island
  (the cleaned base degenerates and the y0/bedrock fallback's floor slab bridges the teams).

**Why segments-vs-cleaned-base alone can't auto-flag the over-split case.** The raw `layer_segments` *include*
the water/foliage the cleaned base drops, so "is there solid beneath this cell" is fooled by water at y0 and by
hollow-shell builds (a solid team island and a floating eagle can both show a large air gap under their surface
cell). Across the corpus, every simple signal either fires on ~137 healthy maps (bbox-nesting) or misses the
target (`a_new_day` fragments overlap the main island's bbox rather than nest inside it). So **over-split is left
to the manual review flag**; a robust auto-fix needs **3D stair-aware connectivity over a *cleaned* segment stack**
(runs of non-air, non-CleanBaseExclude blocks) so a structure reachable by walkable steps stays attached — a
world-rescan-and-redetect change that must be re-validated against the `--islands` parity set.

**What ships instead (reliable, in `Analysis/Footprint/IslandClassifier`):**
- **Role buckets by size** — `major` (≥25% of the largest landmass: the team islands that hold spawn/wools and
  need dissection), `neutral` (≥64 blocks but smaller: contested-middle stepping-stones / mids), `small`
  (sub-gameplay specks — where over-split fragments land, so they fall out of the gameplay model on their own).
  Corpus-validated: kanto 2 majors, green_gem 2+2, annealing_iv 4+8, two-quarter 4+4.
- **Under-split detector** — a symmetric N-team map should resolve into N comparable majors, so `majors < teams`
  flags the merged case (`abstract` → 1 major on a 2-team map). Clean: only ~22 corpus maps trip it, vs the 137
  false positives of the bbox heuristic.

**Surfaces:** `GET /map/{slug}/island-health` (roles + counts + `underSplit`), `GET`/`PUT /map/{slug}/island-review`
(the human's `{status, note}` flag — `status:"ok"` clears it), and the flag is echoed per map in
`GET /decompose/queue` (`reviewStatus`) so a reviewer can triage before cutting. The review path for a flagged
map is to re-detect (a smarter base / looser height params) and re-run `--store-island-sketch`.
