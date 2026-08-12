# Tree corpus — what a hand-built tree measures like, and where the grower misses

The tree-showcase world holds 75 author-built trees, one per 19×19 platform, sorted into 14 families by
platform band plus a single wool tree. It is the first measured ground truth for the grown tree of
`decoration.md` §6, and it says the grower is wrong in three specific, separable ways. The world is committed
at `tools/tree-corpus/tree-showcase` as a measurement fixture, and the harness that produced every number
here is `tools/tree-corpus` — each figure below is reproduced by re-running the script that owns it, named
in that folder's README.

The world is worth describing before the numbers, because its layout is what makes them clean. Every tree
body fits entirely inside its own platform and no crown reaches across a gap, so a plain connected-component
pass assigns leaves to trunks with no arbitration at all — none of the nearest-trunk machinery that reading a
planted forest demands. The platforms are a 19×19 oak plank frame around a 13×13 grass centre, laid in bands
along z, and each band holds one family. There are exactly 75 frames and none stands empty.

Two properties of the corpus break the tools that already read worlds. Every log in it is **all-bark** — 3082
of 3082, branches included — so the flora tool's trunk marker carries no information here, and its rooted
vertical run of three same-species logs reaches only 52 of the 75 trees: 14 of them hover a course above their
platform, 8 have trunks that lean or spiral so no column ever stacks three, and one is built of wool and has
no logs at all. The other property is that **carpentry is structural**: one family builds its branches out of
dark oak slabs, so any reading that counts only logs as wood reports that family's foliage as unsupported.
`leaf-contact.cs --carpentry` exists for exactly that.

## Leaf attachment is the discriminating measure

Every leaf, filed by its strongest contact in its own 3×3×3 neighbourhood:

| strongest contact | hand-built (20,906 leaves) | generated (16,329 leaves) |
|---|---|---|
| face-to-wood | 10.7% | 7.7% |
| edge-to-wood | 13.2% | 7.9% |
| corner-to-wood | 6.4% | 3.1% |
| **any wood contact** | **30.3%** | **18.7%** |
| face-to-leaf | 59.2% | 80.8% |
| **edge-or-corner-to-leaf** | **10.6%** | **0.5%** |
| nothing at all | 7 (0.03%) | 4 (0.02%) |

The generated column is `CommunityMaps/ctw/pattern_test`, which mixes template and grown trees and cannot be
split by block alone — so it indicates rather than proves, and the grower sweep below is what proves.

The row that carries the finding is **edge-or-corner-to-leaf: 10.6% against 0.5%**. A fifth of the author's
foliage hangs on diagonally, because a hand-built crown is drawn as lace; a generated crown fills a solid
ellipsoid, where a diagonally-attached leaf essentially cannot arise. Density says the same from the other
side — occupied neighbours per leaf, median tree: **6.2 hand-built against 13.1 generated**. The generated
crown is twice as solid as anything the author built.

Attachment failure is close to absent in the corpus and structural in the generated world. Counting slab
branches as wood, **11 leaves of 20,906 fail to reach wood** — nine blobs, the largest two blocks — and **no
tree of the 74 leafed ones carries a single stranded leaf**. pattern_test holds **219 stranded leaves in 22
islands, the largest 57 blocks**, and those 22 are not detached parts of trees: they are separate bodies in
the world containing no wood whatsoever. 68 trees, 22 free-floating leaf clouds.

## The crown is detached from the skeleton by construction

Running the real `TreeSkeleton.Grow` and `TreeCrown` over 480 trees — 8 heights × 5 leaf sizes × 12 seeds —
isolates the cause to one line of `TreeCrown.Clusters`:

```csharp
var center = tip.Position + outward * 1.6 + new Vec3(0, Math.Max(0, outward.Y) * 1.4 + height * 0.25, 0);
```

That lifts a cluster's centre a median **3.0–3.3 blocks** above its tip while the cluster's vertical
half-height is only **1.2–3.0**, so the branch tip lies *outside* the ellipsoid meant to hang on it in
**~100% of clusters** — the sole exception is height 40 at maximum leaf size, and even there 81%. Measured on
the swept blocks, **1,516 of 4,020 crown clusters (37.7%) never touch their own branch**. The crown holds
together only because neighbouring clusters overlap and rescue each other, which is also why it reads as one
merged mass rather than per-branch foliage.

`decoration.md` §6 states the lift as intent — leaves sit at the branch ends and not on the wood. The corpus
refuses that premise: 30.3% of hand-built leaves are in direct contact with wood.

Two further faults compound it. `Density = 0.92` punches random holes that leave single-leaf specks on cluster
rims. And the grower emits far too few tips — 3 at height 6, 13 at height 40 — so each cluster must be large
to fill a crown, which leaves `SeamMargin = 0.30` nothing to separate. Small trees are the worst case by a
wide margin: at height 6–9 the grower produces only 3 tips and **42–76% of the crown is stranded**, with
islands up to 105 blocks.

## The wool tree is the author's branching model, stated

One tree in the corpus is built entirely of wool with each limb in its own colour, which makes its skeleton
directly readable — 270 blocks, 31 limbs across 15 colours (a colour is reused where two limbs never touch).

| order | limbs | blocks each | reach | angle off vertical |
|---|---|---|---|---|
| 0 (trunk) | 1 | 88 | 22.4 | 11° |
| 1 | 9 | 12.3 | 7.5 | 94° |
| 2 | 17 | 3.8 | 2.7 | 71° |
| 3 | 4 | 1.5 | 0.6 | 45° |

Set against `TreeShape`, three constants are wrong. `LengthFactor = 0.62` gives a child 62% of its parent's
length where the author gives **0.29**, so generated children are more than twice as long relative to their
parent and compete with the trunk instead of separating from it. `BranchAngle = 0.55 rad` (31°) is far too
tight against primaries that leave the trunk at **~94° off vertical**, essentially perpendicular with a slight
droop. And `Levels` clamped to 2–3 cannot express the **three** orders past the trunk the author uses, in a
1 → 9 → 17 → 4 fan. `ChildStart = 0.30` is the one default that matches: the first primary attaches at y=7 of
a 23-tall trunk. The primaries are staggered up the whole trunk — y 7, 8, 10, 11, 12, 12, 16, 19, 22 — never
whorled.

## The families are distinct silhouettes, not one crown in fourteen palettes

Each platform band holds one family, and they do not vary around a single profile. Wood contact ranges
11%→76% across them and density 4.8→15.8 occupied neighbours per leaf; sets 5 and 11 are the only two that
reach the generated world's density at all. That spread is the argument against one crown model.

Silhouette separates them cleanly. Measuring where a crown carries its bulk — each tree resampled onto its own
crown height first — and how many tiers it is built in, four families are conifers and ten are not, with no
overlap:

| set | z | trees | make-up | widest tenth | foliage in lower half | tiers | width : height |
|---|---|---|---|---|---|---|---|
| 4 | −372 | 5 | acacia log / birch leaves | #1 | 73% | 3 | 0.53 |
| 2 | −460 | 3 | dark oak log / birch leaves | #2 | 77% | 4 | 0.81 |
| 3 | −416 | 2 | dark oak log / spruce leaves | #2 | 68% | 4 | 0.72 |
| 7 | −249 | 8 | acacia log / birch leaves | #3 | 60% | 7 | 0.39 |
| — | | | *the other ten sets* | #4–#6 | 43–61% | 1–2 | 0.75–2.37 |

The widest tenth of a conifer crown sits in the bottom third; every broadleaf family puts it at #4 or above.
Tier count separates at the same place — 3 to 7 against never more than 2. And the tiering is regular enough
to hand to a generator: **tier spacing is 4.6 to 5.8 courses** across all four conifer families (7 tiers in
32 courses, 4 in 20, 4 in 23, 3 in 14). The grower produces no tiers at all.

Set 8 is the corpus's one flat-crowned acacia and isolates on proportion: seven courses tall, 16.6 across,
**width:height 2.37** where the next-widest family is 1.70 and both acacia-log conifers sit at 0.39 and 0.53.
Being a single family of one silhouette it is a sample, not a distribution — worth knowing before treating it
as trainable.

Two palette facts fall out of this. Sets 4 and 7 pair **acacia logs with birch leaves** — dark bark under pale
foliage, the pine reading — which confirms trunk material was never the species signal; only set 8 pairs
acacia with acacia. And **spruce leaves appear in one family only** (set 3), so the corpus's conifers are a
shape family rather than a material one, which is the case for driving a generator from silhouette instead of
from block id.

## What this gives the generator

Three thresholds the corpus supports directly, as a gate on generated foliage: at least **99%** of leaves
reach wood through a chain of leaves, at least **25%** touch wood directly, and occupied neighbours per leaf
below about **9**. The corpus sits at 99.95%, 30.3% and 6.2; the grower at 98.7%, 18.7% and 13.1.
