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
