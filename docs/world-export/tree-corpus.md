# Tree corpus — what a hand-built tree measures like

The tree-showcase world holds 75 author-built trees, one per 19×19 platform, sorted into 14 families by
platform band plus a single wool tree. It is the measured ground truth for what a tree on a board should read
like, and the thresholds it supports are what any generated foliage is judged against. The world is committed
at `pgm-studio-mapgen/showcase/tree-showcase`, and it is read by one operational tool: `tools/seed-trees.cs`
cuts every tree standing in it into the library as a **copied** recipe (`decoration.md` §6), which is how a
board plants the author's own trees rather than a generated one. The numbers below are still the artifact —
re-taking a reading means a scratch pass over that world against today's code (`CLAUDE.md`, *Investigation
stays local*).

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

The generated column is `pgm-studio-mapgen/maps/pattern_test`, a world of placed trees read the same way, so
it says what a board's foliage measures like beside an author's rather than proving anything about one
algorithm. That map was rebuilt as `library_map` from the catalogue sweep (`B209`); the measurement is kept
under the name it was taken at.

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

## The wool tree is the author's branching model, stated

One tree in the corpus is built entirely of wool with each limb in its own colour, which makes its skeleton
directly readable — 270 blocks, 31 limbs across 15 colours (a colour is reused where two limbs never touch).

| order | limbs | blocks each | reach | angle off vertical |
|---|---|---|---|---|
| 0 (trunk) | 1 | 88 | 22.4 | 11° |
| 1 | 9 | 12.3 | 7.5 | 94° |
| 2 | 17 | 3.8 | 2.7 | 71° |
| 3 | 4 | 1.5 | 0.6 | 45° |

Three proportions fall out of it. A child keeps **0.29** of its parent's length, so a branch separates from
the trunk rather than competing with it. Primaries leave the trunk at **~94° off vertical**, essentially
perpendicular with a slight droop. And the author uses **three** orders past the trunk, in a 1 → 9 → 17 → 4
fan. The first primary attaches at y=7 of a 23-tall trunk, and the primaries are staggered up its whole
length — y 7, 8, 10, 11, 12, 12, 16, 19, 22 — never whorled.

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
**59° off vertical**; a second-order limb keeps 0.53 of its parent and stands at 67°. Across the whole corpus
the wood averages **6.3 occupied neighbours per block**, which is the density figure a generated skeleton is
read against — well past 45° and open rather than solid is the shape of the finding.

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
32 courses, 4 in 20, 4 in 23, 3 in 14).

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

Three thresholds the corpus supports directly, as a gate on any generated foliage: at least **99%** of leaves
reach wood through a chain of leaves, at least **25%** touch wood directly, and occupied neighbours per leaf
below about **9**. The corpus sits at 99.95%, 30.3% and 6.2.
