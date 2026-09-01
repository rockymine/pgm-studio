# Tree corpus — what a hand-built tree measures like, and where the grower misses

The tree-showcase world holds 75 author-built trees, one per 19×19 platform, sorted into 14 families by
platform band plus a single wool tree. It is the first measured ground truth for the grown tree of
`decoration.md` §6, and it says the grower is wrong in three specific, separable ways. The world is a
measurement fixture kept outside the repo — it ships with nothing and no runtime path reads it — so the
numbers below are the artifact, and re-taking a reading means a scratch pass over that world against today's
code (`CLAUDE.md`, *Investigation stays local*).

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
dark oak slabs, so any reading that counts only logs as wood reports that family's foliage as unsupported —
a leaf-contact measurement has to count carpentry as wood, or it is measuring the wrong thing.

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

The generated column is `pgm-studio-mapgen/maps/pattern_test`, which mixes template and grown trees and cannot be
split by block alone — so it indicates rather than proves, and the grower sweep below is what proves. That
map is now `library_map`, built from `tools/library-map.cs`'s spec (`B209`); the measurement is kept under the
name it was taken at.

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

## The wood on its own: how a branch is joined, and where it thins

Dropping the foliage leaves the thing a generator actually has to lay out — 4,044 blocks of trunk and branch,
3,082 of them logs, 270 the wool tree, and 692 the carpentry one family branches with. Read as a network, that wood is decomposed the way the wool tree's colours already decompose it: rooted at
the lowest block, a shortest-path tree over the 3×3×3 neighbourhood gives every block a parent, the **stem**
is the chain that carries the most wood at every fork, and a **limb** is a chain leaving it. A chain that
never gets more than a corner's reach from the limb it leaves is that limb doubled — the second column of a
two-wide bole, a knob on its side — and is absorbed into it rather than counted as a branch, which is what
keeps a thick trunk from reading as a bundle of parallel branches.

The 3×3×3 rule holds for wood as it does for foliage, and more strictly: **4,042 of 4,044 blocks touch other
wood**, the two that do not being one stray log each in trees 20 and 44. The network holds together in **72 of
75 trees**; the three that break have a piece hanging on foliage alone.

Where wood parts company with foliage is the tier it is held at. A leaf reaches wood at all in only 30.3% of
cases and hangs on a diagonal in 10.6%; a block of wood is seated squarely — **94.5% face, 5.1% edge, 0.3%
corner** by strongest join. But counting *joins* rather than blocks inverts it: of 12,802 joins in the
corpus, only 36.2% are face, against 43.0% edge and 20.8% corner. Both readings are true and together they
say what the diagonal is for. Wood comes in square-seated runs, and the runs are joined by diagonals:
reading the same trees on face joins alone leaves only **22 of 75** in one piece, the median tree in 4 and the
wool tree into 47. **The diagonal is how a limb departs, not how a block is held** — a branch leaves its
parent by stepping off a corner and then runs square from there.

That is also where the thinning shows. Filing every block by how many steps through the network separate it
from the stem gives a monotone slide down the ladder, with no decomposition assumption behind it:

| steps from the stem | blocks | wood neighbours | face | edge | corner | ends |
|---|---|---|---|---|---|---|
| on the stem | 2,096 | 7.4 | 99% | 1% | 0% | 2% |
| 1–2 | 1,027 | 6.9 | 94% | 6% | 0% | 4% |
| 3–5 | 695 | 3.5 | 87% | 12% | 1% | 13% |
| 6–9 | 196 | 2.8 | 83% | 17% | 0% | 17% |
| 10 or more | 23 | 2.1 | 70% | 26% | 4% | 35% |

Neighbours more than halve, the share held on a diagonal goes from 1% to 30%, and by ten steps out a third of
the wood is an end — a block with one neighbour or none. A limb thins by losing its grip, and a generator can
read its own output on exactly this curve.

The skeleton the corpus draws from that is a low fan on a straight stem. The median tree carries **three
limbs** off its stem and stops there; only 71 second-order limbs and 5 third-order exist across all 75 trees.
A first-order limb reaches **0.40 of its stem's reach**, leaves it **0.37 of the way along**, and stands at
**59° off vertical**; a second-order limb keeps 0.53 of its parent and stands at 67°. Read against
`TreeShape`, the angle is the finding that survives generalisation from the wool tree: `BranchAngle = 0.55 rad`
puts a generated limb at 41° off vertical where the author's sit at 59–67°.

The wool tree is the control for all of this, being the one tree whose skeleton is known independently. Read
by network rather than by colour it comes out at 1 → 12 → 17 → 2 limbs against the hand-read 1 → 9 → 17 → 4,
with primaries at 68° and 0.39 of their parent's reach against 94° and 0.29. The second order matches
exactly; the primaries differ because the network reading counts three flares at the foot as limbs where the
colour reading paints them trunk, and because a colour limb's angle is taken from its own attach block. So
the fan and the length ratios are good to about a quarter, and the angle should be read as *well past 45°*
rather than as a number to fit.

## The stem is straight and low-crowned, and three trees in ten lean

Measuring lean on the **bole** — the stem up to the course where the first limb of reach 3 or more leaves it —
rather than over the whole stem is what separates a leaning trunk from a straight one whose heaviest wood
turns off at the top. On that measure **53 trees stand upright** (under 8°), **13 lean** (8–25°) and **9 are
genuinely angled** (over 25°), which is the corpus's own statement that an angled trunk is a variant and not
the rule. A stem that does lean wanders rather than shearing: drift over travel is **0.66**, so two thirds of
its horizontal travel survives as net displacement and the rest doubles back.

The stem climbs squarely. Of 886 steps up a stem, **74.5% are face, 18.8% edge and 6.7% corner**, and 10 trees
of 75 never step sideways at all. But 28 of 75 contain a step with no rise in it — the wood turns and runs
flat — which is the trunk-and-branch equivalent of the lace in the foliage.

Two proportions matter to a generator more than the lean does. The crown starts **low**: the bole carries only
**0.33 of the stem's rise** before the first real branch leaves it. And the leader is **high**: the stem
reaches the topmost wood in 56 of 75 trees, so the author's tree is a continuous spine with branches staggered
up it rather than a fork.

Carpentry being structural is visible here rather than inferred. Counting only logs as wood (`--logs-only`)
breaks **13 of 75** log networks into pieces — tree 59 into fourteen — and leaves 43 blocks touching no wood
at all, because those families' branches are dark oak slabs and stairs.

## The grown tree's wood is too solid, too steep, and sometimes in pieces

Running the same reading over the grower's own swept wood — `TreeSkeleton.Grow` plus `SweptVolume.Sweep`, 8
heights × 12 seeds, 9,705 blocks — makes the two directly comparable:

| | hand-built (4,044 blocks) | grown (9,705 blocks) |
|---|---|---|
| wood neighbours per block | 6.3 | 9.6 |
| held face-to-wood | 94.5% | 97.7% |
| ends at 10+ steps from the stem | 35% | 21% |
| neighbours at 10+ steps from the stem | 2.1 | 3.4 |
| first-order limbs, reach against the stem's | 0.40 | 0.20 |
| first-order limb angle off vertical | 59° | 41° |
| stems with a step that does not rise | 28 of 75 | 0 of 96 |
| trees whose wood is in more than one piece | 3 of 75 | 18 of 96 |

The first four rows are the wood repeating what the foliage already said: the grower builds a solid, and its
limbs keep their girth to the end instead of running out. The angle and the reach ratio are new, and they
disagree with each other in a way worth stating plainly — a generated limb is *shorter* relative to its trunk
than an author's (0.20 against 0.40) while `LengthFactor = 0.62` is *larger* than the wool tree's 0.29,
because the grower's axis is so much longer than an author's bole that a child at 62% of it still lands short.
The proportion to fix is the one measured in blocks, not the knob read on its own.

The last two rows are defects rather than differences of taste. The grown stem never runs flat, so no limb
ever leaves horizontally. And **18 of 96 grown trees emit wood in more than one piece**, with 10 blocks
touching no wood at all, against 3 trees and 2 blocks in the corpus — where the corpus's are an author's
slips, the grower's are one line of arithmetic.

That line is in `SweptVolume.Ball`, and the wood network is what exposed it. A ball's membership test is the
distance from the limb's continuous centre to the *integer coordinate* of a candidate cell, so a centre
sitting near a cell corner is √3/2 = 0.866 away from every candidate around it — and the only floor is
`radius < 0.5`, which fills the containing cell outright. Between those two figures a sweep sample can fill
**nothing**: at radius 0.55 a third of positions fill no cell, at 0.60 a fifth, and at exactly 0.5 — where the
floor does not apply — very nearly half. Every twig the grower makes lands in that band, because `TreeSkeleton`
floors a limb's end radius at 0.55 and the axis's at 0.5, and **1,116 of the sweep's limbs end thinner than
0.866**. Over the 96 trees, **5,322 of 25,392 sweep samples (21.0%) place no block**. The detached wood is the
visible tail of it; the invisible part is that every generated twig is thinner and shorter than the spline it
was swept from.

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
below about **9**. The corpus sits at 99.95%, 30.3% and 6.2; the grower sat at 98.7%, 18.7% and 13.1.

## What the grower does with it

Every finding above is now law in the code, and the figures below are the same measures taken off the grower
rather than off the corpus: a tree grown and foliated exactly as the dressing pass does, scored on every
measure the corpus supplies — eight heights, three leaf sizes, eight seeds, at the knobs a placed tree ships
with:

| | before | after | corpus |
|---|---|---|---|
| leaves reaching wood through leaves | 98.7% | **100%** | 99.95% |
| leaves touching wood directly | 14.8% | **36%** | 30.3% |
| occupied neighbours per leaf | 12.5 | **7.3** | 6.2 |
| leaves enclosed on all six faces | 7.2% | **0.0%** | 1.7% |
| trees carrying a stranded leaf | 36% | **0%** | 0% |
| worst stranded island | 189 blocks | **0** | 2 blocks |
| trees whose wood is in one piece | 81% | **100%** | 96% |
| wood neighbours per block | 7.9 | **4.7** | 6.3 |
| first-order limb, off vertical | 24° | **60°** | 59° |
| first-order limb, reach against the trunk's | 0.20 | **0.42** | 0.40 |

Six changes carry it. `SweptVolume.Ball` stamps the block its centre sits in whatever the radius, which is
what stops a twig from evaporating and takes the wood from 81% to 100% in one piece. `TreeCrown` seats each
cluster **on** its tip rather than beyond it, and `TreeCrown.Rooted` emits only the foliage that reaches wood
through foliage — so a stranded leaf is not rare, it is impossible. Clusters are small, many and perforated
rather than few and solid, and each is sized by the branch carrying it. `TreeSkeleton.Steer` turns a child in
its parent's own frame, so a branch angle is the angle a branch actually leaves by even off a vertical trunk —
the single change that moved the limbs from 24° to 60° and thinned the wood from 7.9 neighbours to 4.7.

**A hand-built tree's wood barely grows with its height.** Read per tree and bucketed, the corpus carries 23
blocks of wood at 5–9 courses, 36 at 10–13, then 51, 53 and 53 all the way to 40 — an author adds crown as a
tree gets taller, not timber, and the tallest tree in the corpus carries about what a fourteen-course one
does. The grower ran 13 → 456 over the same range, six to nine times an author's wood at the top of it,
because both its trunk radius and its lateral count scaled with height. Both are now nearly flat in it, and
the sweep runs 13 → 322. The crown that few branches have to carry comes from the other half of the same
finding: a hand-built crown is **24% block over its own volume**, with **every one of its leaves carrying air
on some side** — there is no interior to it at all — so a cluster is filled a little under half rather than
nearly whole, and a handful of big lacy clumps foliate a tree that a dozen small dense ones could not.

What is not closed is the last of the density: **8.6 occupied neighbours per leaf against the corpus's 6.2**.
It still climbs with size — 8.0 at height 6 to 9.9 at height 40 — but far less steeply than the 8.3 to 10.8 it
climbed before, and `--by-height` is the switch that shows it. The gate in
`DressingAlgorithmTests` holds it under 11 — enough to catch a return to the solid, not enough to claim the
gap is shut. The other half of the same gap is that a generated tree still carries more wood for its foliage
than an author's: 2.2 leaves per block against a corpus that runs 2.7 to 13.4 on its own large trees.

The conifer is most of the way there. A whorled tree rings its whole trunk — three to five branches at one
height, the next ring 5.2 courses up, each ring shorter than the one below, none of them forking, and a spire
rather than a fork at the apex — and it separates from the staggered form on the measure that separates the
corpus's families: **63% of its foliage in the lower half against 49%**, where hand-built conifers run 60–77%
and broadleaves 43–61%. It still misses on the second: its widest tenth sits at **#4.4** where a hand-built
conifer's is #1–#3, so the bulk is at mid-height rather than in the bottom third.
