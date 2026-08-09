# Objective suggestion — proposing DTC cores from a world scan

An imported world arrives with no `map.xml`; the configure tool's job is to help an author produce one. For
wool monuments that help is `MonumentSuggester` (`monument-suggestion.md`). This document covers the same move
applied to the other two objectives. Both are detectable; they are not detectable the same way. A core has a
signature nothing else produces and is proposed outright. A destroyable has no signature *in itself* — it is
ordinary blocks in ordinary shapes — and is identified by its **neighbourhood**: what surrounds it, and how
far it sits above the ground around it.

Both halves were measured before either was built, against ground truth the corpus gives away for free — a map
that declares an objective says exactly where it is, so the world can be scanned and the proposals scored
without hand-labelling anything.

## 1. Ground truth is the map's own XML, read the way §6 reads it

A declared objective resolves to a region, and the region is **not** the structure: OB12 makes it a loose human
box that routinely encloses decoration, terrain and air. The truth is the blocks *inside* that region matching
the objective's declared material — `RegionBoxes.Of` for the geometry, `MaterialIds.Resolve` for the material,
the intersection for the structure.

Getting this wrong is not a detail. Labelling by the region box instead reported 1,107 "true" clusters against
726 declared destroyables, because every gold block a human's box happened to enclose became a positive. The
measurement said the signature worked; it was measuring itself.

## 2. Cores — lava sealed inside obsidian, and almost nothing else is

`CoreSuggester.Gather` floods each connected lava volume and asks whether **every** non-lava face-neighbour is
obsidian. Maps are full of lava and full of obsidian; a lava volume that is *sealed* in it is a deliberately
built container, and the only reason to build one is a core.

The rim is the single permitted opening. A minority of cores leave the lava flush with the casing top instead
of capping it, so the cells directly above the lava's top layer may be **all obsidian** (capped) or **all air**
(open) — but never a mixture, which describes a spill rather than a container. Reading that distinction is
worth four points of recall.

Volume bounds the rest: a casing is small (3×3×3 = 27 lava is the corpus mode, a 7×7×7 casing holds 125), so
an enclosed volume past `MaxLavaBlocks` is a reservoir, not a goal.

**Measured over 302 corpus maps carrying a declared objective: 267 candidates, 219 of them a declared core —
82% precision at 77% recall** against 284 declared cores. That is comparable to `MonumentSuggester`
(96.6% / 57.8%) and better where it matters for a confirm-in-UI flow, which tolerates a wrong proposal far
better than a missing one.

What is proposed is the **structure**, never a region: the casing box, the enclosed lava count, the shell
thickness grown outward layer by layer, the air gap beneath, and the open-top flag. Every one is read off the
geometry rather than defaulted, which is what lets a suggestion arrive as a core rather than as a box. The
`<region>` a confirmed core needs is the authoring side's exact structure box (OB8) — a human's slack is an
artifact the studio does not reproduce.

## 3. Destroyables — what the neighbourhood shows

Two earlier passes got this wrong in opposite directions, and both errors came from measuring too little of
the world. The first pooled all four materials into one flood fill, so a gold block touching obsidian terrain
became one cluster and everything incidental entered the candidate set; it concluded, wrongly, that a
destroyable is undetectable. The second measured only the structure and its immediate faces, which is not
enough to tell a goal from a decoration that happens to look like one.

The measurement that settles it dumps **every declared destroyable together with its neighbourhood** — ten
blocks outward on each horizontal axis, ten up, and **all the way down to `y=0`** — with nothing dropped for
being large. 614 structures across 223 maps.

### The structure alone says little

Size spans four orders of magnitude: median 8 blocks, p90 120, and a 31,105-block maximum that is a real,
declared destroyable. Any size cap throws away truth, and the earlier 128-block cap was discarding the top
tenth. Fill is likewise uninformative — median 100%, but so is the median false cluster's, because a single
block fills its own bounding box perfectly.

**Support is bimodal, which retires "destroyables float" as a rule.** Reading every footprint column down to
`y=0`: **353 of 614 rest fully on something**, 163 hover with nothing beneath any column, and 98 are partly
supported. The median air gap directly beneath is 0. DT3's "float 3–5 blocks" describes the generator's
default, not the corpus.

### The neighbourhood says a great deal

Two properties separate a goal from decoration, and neither is visible from the structure itself.

**Isolation.** A declared destroyable is typically the only thing of its material anywhere near: the median
count of same-material blocks within 10 is **6 for true structures and 65+ (the measuring cap) for false
ones**. Decoration is repeated by nature — a material chosen for a wall or a floor appears again immediately —
while a goal is placed once. This is the property that dissolves the pathological maps: embers_2's 866
ender-stone clusters are surrounded by each other.

**Elevation.** Measured against the terrain height in the ring around it, a declared structure sits a median
of **+5 blocks above local ground**, and 544 of 610 sit at or above it. False clusters sit at −2. A destroyable
does not have to float, but it is put somewhere prominent; decoration is level with what it decorates.

### Where that lands

Both signals together, with **no size cap and no air-face requirement**, over 15,488 per-material clusters
(1,062 overlapping a declared structure):

| same-material ≤ | elevation ≥ | true kept | false kept | precision |
|---|---|---|---|---|
| 0 | +2 | 355 / 1062 | 186 | **65.6%** |
| 0 | 0 | 389 / 1062 | 244 | 61.5% |
| 2 | +2 | 425 / 1062 | 266 | 61.5% |
| 8 | +2 | 553 / 1062 | 600 | 48.0% |
| 8 | −∞ | 642 / 1062 | 1452 | 30.7% |

Against the earlier best of 28% recall at roughly 15% precision, isolation and elevation are a four-fold
improvement in precision at higher recall. A confirm-in-UI flow wants the `same ≤ 8, elevation ≥ +2` row —
about one true proposal in two — rather than the strictest one.

### The material set is four, and it is four for a reason

Obsidian, emerald, gold and ender stone carry **84%** of declared destroyables. Wool, stained clay and stained
glass carry another 8% between them and must still be excluded: admitting wool takes the candidate set from
15,488 clusters to **439,440**, because a CTW map is largely made of wool. A material a map is built from
cannot mark a goal inside it, so those destroyables are unreachable by this method and that is the honest
ceiling — 84%, not 100%.

Nothing is shipped yet. The signals and their operating points are measured; what remains is the detector
itself and the confirm flow (`B58`).

## 4. Gather at ingest, or not at all

`CoreSuggester` reads `.mca`, so it runs once, inside the single world pass, exactly as
`MonumentSuggester.Gather` does. The world is discarded after import and there is no re-import path, so a
suggestion not captured during that pass cannot be recovered.

What it gathers lands in **`core_candidate`** (`M0014`, `CoreCandidateStore`) — one row per proposed core,
carrying the casing box and every measured parameter. That is a different shape from `monument_candidate`,
which stores *evidence* for a scoring pass to weigh later: a core's signature is unambiguous enough that the
gather pass already knows the structure, so the row is the suggestion rather than an input to one. A re-scan
is delete-then-insert per map, and deleting a map cascades its candidates away.

The counts surface on all three ingest responses beside `monument_candidates`, so an import says how many
cores it found without a second call.

Validation runs from both ends. The corpus gives external truth at scale but only for maps someone else
authored; **a composed plan gives truth by construction** — a plan states a core at an anchor with a chosen
casing, the pipeline builds the world, and the detector has to propose that core at that casing with those
parameters. Both directions are gated in the suite, and the second is what makes a claim about *parameters*
(shell, open-top, size) rather than merely about position.
