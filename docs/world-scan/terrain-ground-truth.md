# Terrain ground truth — what island detection counts as ground

Island detection scans a world from the bottom up and asks, per column, "where does the ground start". The
answer decides how many islands a map has, which decides team territory, symmetry, and the whole configure
canvas. It is also not obvious, because a Minecraft world contains three kinds of solid block and only one of
them is ground.

Three rules separate them, and each is grounded in a different thing: what a block *is*, what PGM *reads*,
and what the map *says*.

## 1. Noise — blocks that are never ground, anywhere

`SurfaceExtractors.CleanBaseExclude` is the flat exclusion: water and lava (the usual island *bridge*), foliage
(leaves, logs, canopy, saplings, tall grass, vines, lily pads), redstone lines, cobweb, and PGM's invisible
block-36 marker. A bottom-up scan looks up into anything above the terrain, so on a decorated world these form
connected masses that merge islands which are not connected at all.

Nothing here is height-dependent, because none of it is ever the floor a player stands on wherever it sits.

## 2. Markers — blocks that are ground-shaped but exist to be read, not walked

PGM's void filter reads `(x, 0, z)` and nothing else: a column is void iff the block at the world floor is
air. So laying a sheet at `y=0` makes a whole column buildable without putting anything where players can see
it, and authors use that constantly to declare build regions (`../pgm/water-lanes.md` §1 covers the same mechanism
from the other side — filling that layer mid-match is what opens a lane).

A marker sheet is therefore **not terrain**, and `FloorMarkerIds` excludes it — but *only at the floor*, which
is `FloorMarkerMaxY = 1`, the single layer maps write as a cuboid spanning `y = 0..1`.

The height bound is the whole rule, and it was learned the expensive way. Stained glass sat in the flat noise
set for a long time, excluded wherever it happened to be a column's lowest solid. That deleted markers
correctly and deleted maps catastrophically:

| Map | Glass-base columns | At the floor |
|---|---|---|
| newgen · outlyne · rushers_vs_defenders | 1,480 – 5,729 | **100%** |
| ad_infinitum | 4,710 | 100% |
| the_high_seas | 30,872 | **0%** — all at `y=58` |
| rock_the_casbah | 17,369 | 0% — `y≈59–77` |
| cannonquest | 2,799 | 0% — `y=4, 50` |

Every map whose glass floor genuinely bridges its islands lays it at `y=0`. the_high_seas' glass is a sea
surface thirty thousand columns wide, and the flat rule erased the map's entire walkable level. Scoping the
exclusion to the floor preserves the first group *exactly* — those four maps' island counts do not move — and
returns the second group's terrain: 16 corpus maps change, all of them maps carrying glass above the floor.

Block 36 needs no such bound and stays in §1: it is invisible and structural, never a building material.

## 3. Erasure — blocks the map itself says will be gone

A hidden destroyable (`Destroyable.Phantom == BlockSwap`) is how an author scripts the world, and a mode at
`0s` replacing its materials with air deletes those blocks during load. `PhantomErasure.From(map)` reads that
statement — the region says where, the materials say which blocks — and island detection subtracts exactly it.
No material heuristic is involved: the map is the authority.

Two conditions are load-bearing and both are read:

- **The mode must fire at zero.** A mode at `15m` changes the world mid-match, so those blocks *are* terrain
  when the match starts, which is the moment island detection describes.
- **The swap must be to air.** The mirror case is the water lane — materials `air` becoming `water` — where
  blocks appear rather than vanish, and nothing is subtracted.

Everything unresolvable fails closed, keeping the block as terrain: a material name outside `MaterialIds`, a
region that does not reduce to boxes, a cuboid with no vertical bound. An erasure that cannot be stated
exactly is not applied at all.

**What this is worth today, stated honestly: nothing measurable.** Both corpus maps that declare an erasing
phantom (newgen_classic, ulcinj) lay their sheet at `y=0`, where §2 already excludes it, so the fact and the
rule agree and the island counts are identical either way. It is kept because it is the map's own statement
rather than an inference, and because it generalises where §2 cannot — a phantom erasing a different material,
or blocks above the floor, is read correctly and no corpus map exercises that.

## 4. Where each rule applies

All three run at **ingest**, inside the single world pass (`WorldFeatureWriter.WriteAsync` →
`SurfaceExtractors.CleanColumns` → `islands_json`). The world is discarded afterwards, so an already-imported
map keeps the island picture it was imported with; a change to any rule here reaches existing maps only
through re-import.

Only §3 needs the map's XML, and only a corpus map scanned through `scan-world` has one — a world arriving by
zip or folder import carries no XML by design, because the XML is what the configure tool *produces* from it.
Those imports pass `PhantomErasure.None`, and §1 and §2 carry them entirely.

`segment` is a separate ingest derivation with its own exclusion (`FeatureExtractors.SegmentExclude`)
and does not apply §2 or §3, so the query-time footprint built from it (`SegmentIndex.BaseColumns` →
`IslandDetector.CleanedBaseFootprint`) still counts a floor marker as solid. That inconsistency is real and
filed (`B57`); it is narrower than it sounds, because that path feeds kit-reach analysis rather than the
island picture.
