# Objective suggestion — proposing DTC cores from a world scan

An imported world arrives with no `map.xml`; the configure tool's job is to help an author produce one. For
wool monuments that help is `MonumentSuggester` (`monument-suggestion.md`). This document covers the same move
applied to the other two objectives, and its main result is that **the two do not behave alike**: a core is
detectable and a destroyable is not.

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

## 3. Destroyables — measured, and not shippable

The plan was the same: a destroyable is a material outlier in a closed vocabulary (obsidian, emerald, gold,
ender stone), mostly a 1–3 block obsidian pillar, and both objectives are said to float above the terrain. Each
of those is true and none of them separates.

Scanning 302 maps for connected clusters in that vocabulary produced **39,716 candidates against 726 declared
destroyables**. The reason no threshold rescues it is that the true structures are *also* tiny: the median true
cluster is **2 blocks**, and 34,789 false clusters are 8 blocks or fewer. Size cannot separate a two-block goal
from a two-block decoration.

Float does not either, and this corrects a claim in `destroyables-and-cores.md` §6: **the median true
destroyable has an air gap of 0** beneath it. Gating on any float at all keeps 259 of 1,036 true clusters while
still admitting 3,475 false ones.

Isolation is the best of a weak set — true clusters expose a median 40% of their faces to air against 0% for
false ones — but the whole gate grid tops out around 28% recall at roughly 15% precision:

| max size | min exposure | true kept | false kept |
|---|---|---|---|
| 8 | 55% | 288 / 1036 | 1,562 |
| 16 | 55% | 326 / 1036 | 1,704 |
| 32 | 40% | 475 / 1036 | 4,648 |
| 8 | 70% | 110 / 1036 | 899 |

Proposing five wrong objectives for every right one is worse than proposing none: it turns a confirm step into
a search. **No destroyable detector ships**, and the honest reason is that local block evidence does not
identify one.

The next thing to try is not a better threshold but a different kind of evidence — **symmetry**. A destroyable
is a two-team objective (OB14), so its mirror image across the map's symmetry is another destroyable, and a
candidate whose partner is also a candidate is enormously more likely to be real than one that stands alone.
The studio already derives the symmetry (`SymmetryDetector`). That is a global constraint rather than a local
one, which is exactly what this measurement says is missing (`B58`).

## 4. Gather at ingest, or not at all

`CoreSuggester` reads `.mca`, so it runs once, inside the single world pass, exactly as
`MonumentSuggester.Gather` does. The world is discarded after import and there is no re-import path, so a
suggestion not captured during that pass cannot be recovered.

Validation runs from both ends. The corpus gives external truth at scale but only for maps someone else
authored; **a composed plan gives truth by construction** — a plan states a core at an anchor with a chosen
casing, the pipeline builds the world, and the detector has to propose that core at that casing with those
parameters. Both directions are gated in the suite, and the second is what makes a claim about *parameters*
(shell, open-top, size) rather than merely about position.
