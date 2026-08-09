# Objective suggestion — proposing DTC cores from a world scan

An imported world arrives with no `map.xml`; the configure tool's job is to help an author produce one. For
wool monuments that help is `MonumentSuggester` (`monument-suggestion.md`). This document covers the same move
applied to the other two objectives. Both are detectable; they are not detectable the same way. A core has a
signature nothing else produces and is proposed outright. A destroyable has no local signature at all — it is
one to three ordinary blocks — and is reachable only by ranking candidates against each other, using the
symmetry that a two-team goal implies.

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

## 3. Destroyables — detectable, and the first measurement of them was wrong

The first pass concluded that a destroyable cannot be found from a world scan. That conclusion came from a
flawed measurement and is retracted here, because the correction changes the answer rather than the margin.

**The error was clustering across the material vocabulary pooled.** A destroyable is a connected mass of *one*
material — obsidian, or emerald, or gold, or ender stone, rarely a mixture — but the probe flooded all four
together, so a gold block touching obsidian terrain became a single cluster and every incidental block in the
vocabulary entered the candidate set. Clustering per material cuts the candidates from 39,716 to 15,480 before
any other change.

**Corrected, a declared destroyable is a standalone object 98% of the time.** Of 571 structures resolved to a
single material, 561 are *exactly* their connected cluster — the declared blocks are the whole mass, with
nothing of that material attached. Only 10 fuse into something larger. That is the number which decides
whether detection is possible at all, because a detector sees clusters: obsidian 298/298 standalone, emerald
108/108, gold 40/40, ender stone 65/67. The ceiling is 98%, not the obstacle.

**Symmetry pairs them, and the pairing is findable without the XML.** 538 of 564 declared structures (95%) have
a same-material, same-size partner at the rotational image of the objective set's centre — which follows from
OB14, since a destroyable is a two-team goal. The centre itself does not need to be known in advance: every
same-material same-size pair of candidates proposes the midpoint it would be symmetric about, and the true
centre is the one the most pairs agree on.

**What the false positives are, grouped by why they are not destroyables.** Of 14,418 false clusters:

| Count | Reason |
|---|---|
| 4,985 | fully buried — not one face touches air |
| 3,632 | embedded in terrain — under 25% of faces on air |
| 945 | submerged, or touching a fluid |
| 573 | sprawling — a bounding-box dimension over 8 |
| 373 | too large — over 64 blocks |
| 105 | not solid — under 50% of its own bounding box |
| 3,805 | survives all of the above |

The survivors concentrate in a handful of maps that use these materials architecturally — embers_2 alone
contributes 866 ender-stone clusters, warzone 442 obsidian — which is exactly the "used, but as large sections
or hidden in terrain" pattern, arriving as many small clusters rather than one big one.

**Ranking beats gating, and is the right shape for the flow anyway.** A confirm-in-UI step does not need a
candidate set that is mostly right; it needs the right ones at the top. Scoring candidates by material prior
(emerald runs nearly 1:1 true:false, obsidian nearly 1:22), symmetry pairing, air exposure and compactness, and
ranking per map, places a declared structure in the map's **top 5 for 47.6%** and top 10 for 54.6%, at a median
of 11 candidates per map and a median rank of 3.

That is an assist, not a detector, and it is not shipped yet — because 34% of declared structures are not even
reaching the candidate set, and the reason is known: the current filters drop anything over 128 blocks or with
no air face at all, and a real destroyable is sometimes both. Those two filters are worth more recall than the
whole ranking is worth precision, and fixing them is where the next work goes (`B58`).

## 4. Gather at ingest, or not at all

`CoreSuggester` reads `.mca`, so it runs once, inside the single world pass, exactly as
`MonumentSuggester.Gather` does. The world is discarded after import and there is no re-import path, so a
suggestion not captured during that pass cannot be recovered.

Validation runs from both ends. The corpus gives external truth at scale but only for maps someone else
authored; **a composed plan gives truth by construction** — a plan states a core at an anchor with a chosen
casing, the pipeline builds the world, and the detector has to propose that core at that casing with those
parameters. Both directions are gated in the suite, and the second is what makes a claim about *parameters*
(shell, open-top, size) rather than merely about position.
