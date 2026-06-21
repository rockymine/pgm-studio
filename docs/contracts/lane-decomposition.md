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
