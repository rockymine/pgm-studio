# Sketch world-folder export (P9)

Sketch-originated maps have no real voxel world (their "world" is a synthetic `layer.parquet`). To ship a
playable PGM map, the studio synthesises a real Anvil world from the sketch column geometry
(`SketchRasterizer.RasterizeColumns`) + the authored `MapIntent`, and bundles it with `map.xml`.

**Delivery.** At the Configure export point (today `GET /api/map/{slug}/xml`, `MapXmlEndpoint`), a
**sketch-origin** map returns a **ZIP** of a single `{slug}/` folder:

```
{slug}/
  map.xml
  level.dat
  region/
    r.<x>.<z>.mca
```

**Normal Configure-imported maps export XML only** (they already ship a real world). Sketch origin is detected
by the presence of the `sketch_layout_json` artifact (durable signal — `MapStage.Sketch` advances to
`configure` on finish).

**Anvil format = the 1.8–1.12 *numeric* block format** the `AnvilRegion` reader already understands
(`Blocks`/`Data`/`Add` nibble-packed sections; matches the studio's supported range, proto ≥1.4.0 / pre-1.13).
The writer is the mirror of the reader.

---

## 1. Terrain

From `SketchRasterizer.RasterizeColumns` → `(X, Z, YFloor, YTop)` solid columns:

- **y = 0 layer = bedrock** (id 7).
- The solid span above (`[YFloor, YTop]`) = **stone** (id 1).

Flat materials for now — a later task may add a surface palette. Handle stacked disjoint segments per `(x,z)`.

**Structures sit on top of the existing terrain** — a cube/monument base rests on the terrain surface Y at its
footprint, never embedded in or floating above it.

---

## 2. Cube template (shared 8×8×8 shell)

Both the **wool cage** and the **spawn cube** are a hollow **8×8×8 bedrock** shell. Layers are numbered **from
the top** (roof = layer 1, floor = layer 8):

| Layer (from top) | Wool cage | Spawn cube |
|---|---|---|
| 1 (roof) | bedrock, **4×4 centre hole** | bedrock, **4×4 centre hole** |
| 2 | bedrock | bedrock |
| 3 | **missing** (light slit around all four walls) | **missing** (light slit) |
| 4 | bedrock | bedrock |
| 5 | **wool** (room colour) | **stained clay** (team colour) |
| 6 | bedrock | **stained clay** (team colour) |
| 7 | bedrock | bedrock |
| 8 (floor) | bedrock, **2×2 centre = wool** (room colour) — wool spawn point | bedrock, **2×2 centre = wool** (team colour) |

**Doors** (at floor level, made of **stained glass**):
- **Wool cage:** four doors, one centred per wall, each **2 wide × 3 tall**, so players can enter from all sides.
- **Spawn cube:** a **single 4×4 door** on one wall.

Colour: wool + stained clay follow the **room colour** (wool cage) / **team colour** (spawn cube); the
`WoolIntent.Color` / spawn team dye slug → data nibble (0–15) via `BlockColors` (wool 35, clay 159, glass 95 all
key off the same data value).

### 2a. Wool-cage chests

Each of the **4 interior corners** holds **2 chests stacked** (bottom + top). A "row" = 9 slots.

- **Chest A** — row of **planks ×16**; row of **Speed I potions (3:00)**; row of **golden apples ×16**.
- **Chest B** — row of **diamond leggings**; row of **Power I + Infinity bows**; row of **planks ×16**.

(Spawn cube has **no chests**.)

---

## 3. Wool monuments (inside the spawn cube)

Monuments are **part of the spawn cube**, placed in its **corners**. The pedestal is **elevated one block** off
the cube floor (it is *not* the floor) so a **sign** can be mounted against the pedestal side. The wool is placed
in the air cell above the pedestal.

**Placement by wool count** (the capturing team's monuments, `MonumentIntent.Team`):

- **1–2 wools:** corners **against the wall that has the door**; sign parallel to the door wall.
- **3–4 wools:** the **other** corners, mirroring the sign position — parallel to the **back wall**.
- **5+ wools (rare):** **fill the back wall** with monuments.

Because positions are derived from the cube geometry + wool count, the exporter computes monument coords from the
spawn cube, not from a freely-authored `MonumentIntent.Location`.

**Sign label** (always, 4 lines):

1. `Place the`
2. *colour name* — **bold**, in the wool colour (e.g. bold red "Red")
3. `Wool` — in the wool colour
4. `here!`

---

## 4. `level.dat`

Gzipped NBT `Data` compound: world spawn (`SpawnX/Y/Z`) at the observer/default spawn, flat generator,
`LevelName = {slug}`, and 1.8–1.12 version tags. **Gotcha: a correct creation timestamp** (`LastPlayed` / the
world's date field). Crib the exact tag set from a real 1.8-era CTW `level.dat` —
`OvercastCommunity/CommunityMaps/ctw/…` (e.g. `outback_outback_edition/`).

---

## 5. Open questions (to resolve before/while building)

- **Stained clay in the wool cage.** The spec says "wool and stained clay in the room colour," but the wool-cage
  layer table only places wool (layer 5 + floor). Does the wool cage use stained clay anywhere, or is clay
  spawn-cube-only?
- **Monument pedestal block.** What is the elevated pedestal made of (bedrock / team-colour clay / wool)? Is
  there still a stained-glass **cap** (as in the earlier canonical template), or does the in-cube monument drop
  the cap since the wool sits in open air below the roof?
- **Door / light-slit colour.** Are the stained-glass doors coloured (team/room) or plain? Same for whether the
  layer-3 slit is a true gap (air) or glass.
- **Layer-index mapping.** The "Nth from top" → y-offset mapping in §2 is derived; confirm the door height
  (y 1–3 for the 2×3 wool door) doesn't clash with the layer-5 coloured ring.
