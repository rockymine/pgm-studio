# Generator archetypes — a shape-language study of real CTW maps

Findings from three Overcast CommunityMaps CTW maps, scanned with the studio's own
clean-base layer extractor, island detection, and the Douglas–Peucker polygon
simplifier (`PgmStudio.Geom.PolygonSimplify`). Run the study with:

```
dotnet run --project tools/PgmStudio.RoundTrip -- --island-study <regionDir> <out.json> [tolerance]
```

Simplification makes the staircased block outlines readable — raw → simplified vertex
counts: annealing_iv 2832 → 173 (6%), kanto 1346 → 64 (4%), green_gem 2622 → 106 (4%).

## The three maps

### Annealing IV — rot_90 pinwheel, one wool per team
- 12 islands: 4 large team **blades** (2478 cells), plus 4+4 satellite islands forming the
  contested inner ring.
- The team islands are curved blades arranged in 4-fold rotational symmetry (`rot_90`).
  Each blade carries a hole and curls inward toward the centre.
- One wool per team at the blade's **outer tip** (the far dead-end), monument alongside it.

### Kanto — two-team rectilinear body, two wools per team
- 2 team islands (6230 cells), top and bottom, `mirror_z` across the mid.
- Each body has **three top prongs**: wools on the outer two prongs, monuments (⊕⊕)
  clustered on the central prong. Two bottom legs reach toward the mid.
- Interior rectangular **holes** (courtyards) inside the body.

### Green Gem — two-team trident, diagonal arms
- 4 islands: 2 team tridents (7152) + 2 chevron mid-islands (1545) in the centre.
- Each team island is a **Y/trident**: two diagonal arms angling outward with wools at the
  tips, plus a central wool down the stem; the monument hub sits where the arms meet.
- Separate chevron mid-islands are contested stepping-stones.

## Placement rules (consistent across all three)
- **Wools** sit at the far/outer **tips** of lanes/arms/prongs — dead-ends toward the map edge.
- **Monuments** cluster at the team's **hub** (near the spawn / where the arms meet), often paired.
- **Multiple wools per team** are common (Kanto 2, Green Gem 3, Annealing 1).
- **Mid islands** are separate contested pieces (satellites / chevrons), bridged in Configure — the
  *norm*, not the exception (see the corpus island-count section below).
- Symmetry is `rot_90` (Annealing) or `mirror_z` (Kanto, Green Gem).

> These three maps are a qualitative deep-dive; the recipes were validated against the **full ingested
> corpus** (below). Everything holds except the *island count* — these three are simpler than the median map.

## Corpus island-count — the contested middle (N = 347)

Measured across the full ingested CTW corpus (`gamemode=ctw` maps with island geometry + a wool objective)
with `scripts/island_corpus.py`, which reads the populated MariaDB and classifies each island as
**objective-bearing** (hosts a wool/monument — what the generator emits) or **neutral** (the contested
middle the generator omits).

**Island count.** Median **5** islands/map (mean 7.9, p25 3, p75 9, max 231 = a decorative outlier). **45%**
of maps have ≤4 islands — but that splits into **24% with just 1–2** (the two team bodies are compact or
bridged into one landmass) and **21% with the clean 3–4** archetype. The other **55%** carry 5+.

**Objective vs neutral.** Objective islands/map median **2**: 53% of maps are two separate team islands (the
`mirror_z` case), **30% put both teams on one connected landmass**, 10% are four-team. Then the middle:

- **88% of maps have ≥1 neutral island**; median **3** (median **2** once decoration is filtered, i.e.
  ≥64 blocks); **81%** have ≥1 gameplay-sized neutral island.
- **The pieces are small.** Median neutral island = **3.6%** of the team island. 46% are stepping-stones
  (<3%), 44% satellites (3–25%), only 10% substantial (>25%). By absolute block-count: 32% <64
  (decoration), 39% 64–255, 22% 256–1023, 6% ≥1024. → add *several small/medium* pieces, **not a big
  central blob**.
- **They're symmetric.** **66%** of gameplay neutral islands have a `mirror_z` twin → generate them as a
  symmetric set (the rest are on-axis singles + rot maps the mirror_z check misses).
- **Mixed position.** 39% sit near the centre (a contested centre); 61% are scattered (flanking satellites
  between the lanes).

**Holes (the diamond primitive).** Only **18%** of islands carry ≥1 hole — real, but a minority feature.

**The generator gap → G3.** The Organic archetype emits the 2 objective islands and **zero** neutral pieces
(`LaneSketchGenerator.Organic` passes `mids: []`). The clean-archetype assumption nails the simple ~45% tail
but misses the symmetric 2–3-piece middle that 88% of maps have. The `G3` targets that follow directly:
add a symmetric **neutral mid-set** (~2 pieces, 64–1023 blocks ≈ 3.6% of the team island, ~40% central /
~60% flanking) via `Assemble`'s mid-island slot, and rework holes from the per-lane `HoleChance=0.45` toward
a per-island ~18% rate. Re-run `scripts/island_corpus.py` to re-validate after.

## How the sketch shape system builds them
The lane/shape primitives already cover these:
- **Lanes** (`Geom.Lane.Strip`) with straight, diagonal, or smoothed (`Geom.Algorithms.CatmullRom.Spline`) centerlines
  → prongs, trident arms, and curved blades.
- **rot_90 / mirror_z** (`setup.mirror_mode`) → the pinwheel (4 copies) or the two-team mirror.
- **Subtract shapes** (the rasterizer's set algebra) → interior courtyards / blade holes.
- **Circles / polygons** → satellite and chevron mid-islands.

The generator archetypes (`LaneSketchGenerator`) mirror these recipes: the `H` board, plus the
pinwheel (rot_90, one wool per team), the pronged body (Kanto-like), and the trident (Green Gem-like).

## Lane-graph decomposition (13-map study)

To understand *where* objectives sit, each island is reduced to its **lane graph** (the "twig with
branches"): clean-base mask → `skimage` skeletonize → component-based graph (endpoints = lane tips,
degree-≥3 = junctions, edges = lanes) → **anchor-aware prune**. The anchors — fixed nodes the prune never
removes — are the map's objective positions (spawn/wool/monument, from the xml) **and the island↔build-region
contact cells** (build regions come from `RegionCategorizer`; the export tool emits them). Anchoring stops
pruning from eating the lanes that lead to an objective or a bridge connector. (`--skeleton-study` exports the
geometry + objectives + build cells; the skeleton itself is computed in the Python study harness.)

Median distance from each objective to the nearest **lane tip**, across 13 maps:

| map | islands | wool→tip | monument→tip | spawn→tip |
|---|---|---|---|---|
| kanto | 2 | 1.1 | 3.1 | 4.6 |
| green_gem_ctw | 4 | 1.8 | 61.6 | 66.5 |
| geometric_domination | 9 | 2.5 | 7.6 | 2.5 |
| constellation | 6 | 5.4 | 2.1 | 1.1 |
| duality | 16 | 4.5 | 7.6 | 1.1 |
| aether | 14 | 1.2 | 7.4 | 4.6 |
| amphitheater | 4 | 1.5 | 4.8 | 2.5 |
| expedition | 7 | 0.7 | 6.1 | 1.9 |
| colorado | 4 | 2.5 | 5.0 | 1.1 |
| celestial_islands | 10 | 4.5 | 51.9 | 55.7 |
| annealing_iv / fall_of_babylon / dragons_hearth | 12–23 | (deep-room wools, see note) | | |

**What it confirms (the rules the generator should follow):**
- **Wools sit at lane tips** — dead-ends at the far end of a lane (≤ ~5 blocks for 10 of 13 maps). A team's
  several wools are at *distinct* lane tips, not the same place.
- **Spawns and monuments sit at the hub** — where the lanes meet — either dead-centre (green_gem, celestial:
  spawn/monument 50–66 from any tip) or on their own short spawn-lane (duality, colorado, constellation:
  spawn→tip ~1). Never in a wool's lane.
- So a team island is a **hub with branches**: spawn/monuments at the hub, one branch per wool ending in a
  tip. This is the structure the generator must emit — the earlier "blade blob with everything clustered"
  is exactly what to avoid.

Note: where wool→tip is large (annealing's curved blades, fall_of_babylon, dragons_hearth) the wool sits in
a wool-*room* set back from the lane tip, or the island is large enough that the room reads as its own small
structure — still a dead-end pocket off the lane, just not the skeleton's extreme endpoint.

## Lane primitives (the building blocks)

Each lane's thinned pixel path is straightened shape-preservingly with open-polyline Douglas–Peucker
(`PolygonSimplify.Polyline`) — staircases collapse to clean segments, vertices remain only at real bends —
and each bend is measured (the turn angle from straight). Over the 13-map sample (467 lanes, 287 bends):

**Bend angles** (deviation from straight, nearest octant): 45° **42%**, 67.5° 21%, 22.5° 20%, 90° 12%,
135° (hairpin) 5%. → the **45° family dominates**; right-angles are the minority. These are diagonal maps,
not grid maps.

**Lane shapes**: straight **59%**, single 45°-bend 10%, double-bend 7%, wiggle 6%, shallow-bend 6%,
right-angle (L) 5%, single 67.5°-bend 4%, hairpin 2%. → most lanes are **straight runs** (median length 10,
~4–28 blocks) joined by a single 45°-family bend.

**Diamonds**: 101 independent cycles across the 13 maps — lanes split and **loop around an interior hole**
(the diamond primitive). Holes are a first-class feature, not noise.

So the generator's alphabet is: **straight segment** (stretchable), **45°/octant bend**, **right-angle bend**
(rarer), and **diamond** (a lane loop around a hole). A team island is these primitives composed into a
hub-with-branches — straight runs out to wool tips, bends to route around the terrain, diamonds where a
hole is wanted — then mirrored/rotated by the board symmetry.
