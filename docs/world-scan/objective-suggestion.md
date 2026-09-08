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

**And a declared destroyable is not always a goal.** A `<destroyables show="false">` group is a marker: it is
not displayed, it is not required, and it is gone the moment the match starts. Two shapes of it are in the
corpus. `abstract` marks its build zone with a 91×71×2 stained-glass slab at `y=0` that **red and blue declare
at the same coordinates**, and `ulcinj` draws indicator arrows in stained glass under `after-0s`, four cuboids
in one union under a single owner. Neither is a structure a player breaks. Counting them as truth charges a
detector for missing something that is not there — and pairing one against a real monument is how `ulcinj`,
whose obsidian monuments stand **218 blocks apart**, can be made to look like a map with a 17-block pair. The
rule: **skip `show="false"`, and take one structure per `<destroyable>`** rather than one per cuboid, since a
region that is a union is one objective. The same floor markers are what `B57` is about on the terrain side —
`scan_segment` still reads that slab as solid ground.

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

### Separation between proposals was measured and is not a rule

A pair of identical masses ten blocks apart reads as two team goals to a detector that judges each one alone,
and on `alpine_mining` such a pair — obsidian pillars in signed cobble alcoves, built exactly like the real
monuments — is the observer spawn. The corpus says the distance is real: across 123 maps and 457 cross-owner
pairs, **no genuine opposing pair is closer than 28 blocks** (p5 64, median 132), and nothing at all falls
within 24.

It still does not work as a filter, because the detector sees masses and not owners. Two goals of the *same*
team are routinely close — same-owner separation has a p5 of 14 — so a minimum-separation rule cannot tell a
display pair from a team's second monument. Swept over a stride sample against a 70.4% / 72.3% baseline (before the
duplicate collapse), both readings of the rule cost multiples of what they buy:

| minimum | rule | precision | recall |
|---|---|---|---|
| 16 | keep the most isolated | 67.3% (−3.1) | 72.3% (−0.0) |
| 30 | keep the most isolated | 71.6% (+1.2) | 67.0% (−5.3) |
| 50 | keep the most isolated | 73.4% (+3.0) | 61.7% (−10.6) |
| 30 | drop both | 75.4% (+5.0) | 55.3% (−17.0) |
| 50 | drop both | 72.1% (+1.7) | 46.8% (−25.5) |

An alphabetical sample makes the rule look free — it excludes `quintlet` (28), `malupa` (34) and
`chimeric_ii` (50), which are the maps a threshold takes apart. That is the reading to distrust, and the
reason the sweep is stated here rather than the conclusion alone.

### The map's own mirror axis was measured too, and fails for a sharper reason

The symmetry is already there to use: `SymmetryDetector.Detect` reads it off the island layout and answers
every mode it finds with a centre. So a candidate can be asked whether another candidate sits where the map
sends it. Against the same 70.4% / 81.0% baseline, keeping only candidates with a mirror partner scores
**70.7% precision at 73.8% recall** — 0.3 points of precision for 7 of recall — and asking only the
*strongest* mode is worse still (70.0% / 59.5%), because a map's islands can read as `mirror_x` while its
objectives are laid out under the `rot_180` about the same centre. Requiring an exact partner rather than one
within a block collapses recall to 11.9%: a detected centre is not an objective centre to the half block.

The reason it fails is worth stating, because it is the reason every layout rule fails here. On
`alpine_mining` the detected modes are `[mirror_x, mirror_z, rot_180]` about `(0.5, 72.5)`. The real Monument
A pair is an **exact** `rot_180` partner — miss `0,0`. And the observer-spawn pair, the false proposal this
was meant to reject, is a `mirror_z` partner missing by **one block**. An observer platform is built with the
same symmetry as the map it overlooks, because it is part of the map. Symmetry separates a well-built map
from a careless one; it does not separate a goal from a display, and neither does distance. That is why the
readings that work are the neighbourhood's.

Nothing here uses symmetry, and neither does `MonumentSuggester` — a wool monument is proposed from signed
pedestals, which is direct evidence rather than layout.

### The material set is four, and it is four for a reason

Obsidian, emerald, gold and ender stone carry **84%** of declared destroyables. Wool, stained clay and stained
glass carry another 8% between them and must still be excluded: admitting wool takes the candidate set from
15,488 clusters to **439,440**, because a CTW map is largely made of wool. A material a map is built from
cannot mark a goal inside it, so those destroyables are unreachable by this method and that is the honest
ceiling — 84%, not 100%.

`DestroyableSuggester` reads exactly this. It clusters each of the four materials, counts same-material
blocks in the neighbourhood to a cap of 65, measures the mass's underside against the median terrain of the
ring the neighbourhood covers outside its own footprint, keeps `same ≤ 8 & elevation ≥ +2`, and then
collapses masses of one material within 16 blocks onto their most isolated member. Validated against declared
structures with markers excluded, over a stride sample across the corpus: **100 proposals, 68 of them true,
covering every one of the 68 structures the readings reach — 68.0% precision at 81.0% recall.**

The collapse is about the *list*, not the verdict. A structure that clusters into several masses is proposed
several times, and offering the same goal four ways makes a worse thing to confirm from. Over the sample it
takes the proposals from 125 to 100 and the false ones from 37 to 32 while leaving coverage exactly where it
was, landing at one proposal per structure. The precision figure moves the other way — 70.4% to 68.0% — only
because a duplicate true proposal counts as a win in that ratio and as noise on the screen. A proposal right
about two times in three is something a person confirms rather than something the tool applies, and
Configure's Destroyables phase is where the list is read: each row states the two readings it was proposed
on, in words that can be checked against the world (`docs/tools/configure.md`).

## 4. Gather at ingest, or not at all

`CoreSuggester` reads `.mca`, so it runs once, inside the single world pass, exactly as
`MonumentSuggester.Gather` does. The world is discarded after import and there is no re-import path, so a
suggestion not captured during that pass cannot be recovered.

`DestroyableSuggester` runs in the same pass and on the same reading, for a sharper version of the same
reason: its signal is not in the structure at all but in what surrounds it, so it needs the world rather than
the goal, and the world is the one thing the pass has that nothing after it does.

What it gathers lands in **`core_candidate`** (`M0014`, `CoreCandidateStore`) — one row per proposed core,
carrying the casing box and every measured parameter. That is a different shape from `monument_candidate`,
which stores *evidence* for a scoring pass to weigh later: a core's signature is unambiguous enough that the
gather pass already knows the structure, so the row is the suggestion rather than an input to one. A re-scan
is delete-then-insert per map, and deleting a map cascades its candidates away.

A destroyable's proposals land in **`destroyable_candidate`** (`M0034`, `DestroyableCandidateStore`), the
same shape with one difference: the row carries the two readings as well as the box. A core's proposal is
self-evident — a sealed lava container is a core — while a mass of ender stone is a goal only relative to
what surrounds it, so the surface that asks a person to confirm one has to say *why*, and that sentence is
only writable if the readings outlive the world. They also order the list: least-surrounded first, because
isolation is what separates a goal from decoration.

The counts surface on all three ingest responses beside `monument_candidates`, so an import says how many
cores and destroyables it found without a second call. `GET /map/{slug}/destroyable-suggestions` serves them
back, optionally filtered to a `box`, beside the generator's defaults and the two vocabularies a picker
offers.

## 5. Confirming a core — the Cores phase

The configure wizard reads the stored proposals through `GET /map/{slug}/core-suggestions`, which returns
the rows as suggestions and takes an optional `box` to narrow them to the area an author drew. No scoring
runs and no world is touched; the row is already the suggestion.

Confirming one writes a `CoreIntent` whose **`Box` is the casing the detector measured**. That field is what
decides whether anything is emitted at all — `CoreGenerator` writes the core's `<region>` straight from it
and skips a core that has none (OB8) — so a confirmed proposal exports the structure that is genuinely in
the world rather than a box computed around it. Footprint and height are read off the same box, which is why
a confirmed core states a 7×4 casing when that is what was built instead of the generator's 5×5 default. The
defending team comes from the island the casing sits on, and the author corrects it like any other guess.

Two things the world cannot supply are defaulted rather than invented. `leak` is a rule, not a structure —
nothing about the blocks says how far the lava must fall — so it takes PGM's own default and pairs with the
measured `float` to give the dig depth. And a core the detector missed (about one in four) is placed by
drawing its casing footprint: its base sits `float` blocks above the ground under that footprint, the
world-export stamper's own rule, so a core described here and one built from a plan sit at the same height
over the same terrain. Drawing that footprint is description, not construction: the obsidian is already in
the imported world and the author is saying where it is.

A **plan-authored** core keeps no box here. Its casing is stamped against terrain that does not exist until
the world is built, so the field rides through untouched and the step says so instead of resolving it early.

**Which fields the phase may set follows from what it writes.** Configure produces `map.xml` and never
touches the world, so the casing's footprint, height, wall thickness, cap and float are read out rather than
edited: on an imported map they measure obsidian that already exists, and changing them would recompute a
region that no longer scopes it — the failure OB8 exists to prevent, since PGM builds the goal from the
blocks matching the material *inside* the region. On a plan-built map they are what the stamper is about to
place, which the plan states. `leak` is the exception and is one for a reason: nothing in the world says how
far the lava must fall, because it is an attribute on the `<core>` element and nowhere else. Shaping a casing
belongs to the plan tool's marker panel, where the structure and the terrain under it are authored together.

The phase itself is additive. A PGM map may carry wools, destroyables and cores at once, so Cores is its own
phase beside Wools rather than a branch that replaces it, and the objective phases share one completeness
gate — the map needs an objective, not one of each (`../pgm/new-map-authoring.md` §12).

Validation runs from both ends. The corpus gives external truth at scale but only for maps someone else
authored; **a composed plan gives truth by construction** — a plan states a core at an anchor with a chosen
casing, the pipeline builds the world, and the detector has to propose that core at that casing with those
parameters. Both directions are gated in the suite, and the second is what makes a claim about *parameters*
(shell, open-top, size) rather than merely about position.
