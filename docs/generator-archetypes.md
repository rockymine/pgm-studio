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
- **Mid islands** are separate contested pieces (satellites / chevrons), bridged in Configure.
- Symmetry is `rot_90` (Annealing) or `mirror_z` (Kanto, Green Gem).

## How the sketch shape system builds them
The lane/shape primitives already cover these:
- **Lanes** (`Geom.Lane.Strip`) with straight, diagonal, or smoothed (`Lane.Smooth`) centerlines
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
