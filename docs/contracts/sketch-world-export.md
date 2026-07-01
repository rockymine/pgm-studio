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
the floor** (floor = layer 0, roof = layer 8). The two variants differ **only** in door count/size and
contents (chests vs monuments) — the colour-strip size is identical:

| Layer (from floor) | Wool cage | Spawn cube |
|---|---|---|
| 8 (roof) | bedrock, **4×4 centre hole** | bedrock, **4×4 centre hole** |
| 7 | bedrock | bedrock |
| 6 | **missing** (light slit around all four walls) | **missing** (light slit) |
| 5 | bedrock | bedrock |
| 4 (colour strip) | **wool** (room colour) | **stained clay** (team colour) |
| 3 | bedrock | bedrock |
| 2 | bedrock | bedrock |
| 1 (doors begin) | bedrock + door opening | bedrock + door opening |
| 0 (floor) | bedrock, **2×2 centre = wool** (room colour) — wool spawn point | bedrock, **2×2 centre = wool** (team colour) — player spawn |

**Doors** (begin at layer 1):
- **Wool cage:** four doors, one centred per wall, each **2 wide × 3 tall** (layers 1–3), made of **stained-glass
  panes (id 160) in the room's wool colour**, so players can enter from all sides.
- **Spawn cube:** a **single 4×4 door** (layers 1–4) on one wall, **open (air)** — no glass.

Colour: the layer-4 strip + the 2×2 floor wool follow the **room colour** (wool cage: wool, **no stained clay**)
/ **team colour** (spawn cube: clay strip, wool floor). Dye slug → data nibble (0–15) via `BlockColors`
(wool 35, clay 159, glass 95 all key off the same data value).

### 2a. Wool-cage chests

Each of the **4 interior corners** holds **2 chests stacked** (bottom + top). A "row" = 9 slots.

- **Chest A** — row of **planks ×16**; row of **Speed I potions (3:00)**; row of **golden apples ×16**.
- **Chest B** — row of **diamond leggings**; row of **Power I + Infinity bows**; row of **planks ×16**.

### 2b. Observer platform (standalone — not a cube)

The observer/default spawn (`ObserverIntent.Point`) gets its own template: a **6×6 bedrock platform**
(integer-snapped X/Z/Y, §5). At the centre of each edge sits a **1-tall × 2-wide bedrock wall piece**, with
**signs mounted on its inner face**. Top-down layout (`b` = raised bedrock, `s` = sign, `o` = platform floor):

```
oobboo
oossoo
bsoosb
bsoosb
oossoo
oobboo
```

Sign text (map name + authors from the map meta):

- **Left signs** (reading clockwise):
  ```
  ===
  [CTW]
  {map name}   (bold)
  ===
  ```
- **Right sign**:
  ```
  made by   (italic)
  {author 1}
  {author 2}
  {author 3}
  ```

**Open (see §7):** the grid shows bedrock+signs on **all four** edges, but text is given only for the left and
right walls — the top/bottom walls' sign content, the sign facing (inward assumed), whether the two signs in a
pair share text, and the platform's Y (terrain surface vs the observer's floating height) are unresolved.

(Spawn cube has **no chests**.)

---

## 3. Wool monuments (inside the spawn cube)

Monuments are **part of the spawn cube**, placed in its **corners**. Geometry (bottom-up): a **bedrock
pedestal elevated one block** off the cube floor (it is *not* the floor) → an **air placement cell** above it
(the wool goes here) → a **stained-glass cap in that monument's wool colour** above that. A **sign** is mounted
against the pedestal side.

Monuments are **fully auto-wired** — the exporter derives every monument from the wool set + spawn-cube geometry.
There is **no manual monument authoring step** (it is removed from the Configure wizard for sketch-origin maps);
positions are *not* read from a freely-authored `MonumentIntent.Location`.

**Placement by wool count** (per capturing team):

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

## 5. Placement anchoring & coordinate constraints

**Scope: this whole document applies only to maps that land from the sketch endpoint
(`POST /api/map/{slug}/sketch/finish`) into Configure.** Normal Configure-imported maps ship a real world and
export XML only — none of the structure synthesis or the constraints below touch them.

- **The authored spawn / wool positions anchor the structures.** The spawn cube is centred on the player
  `SpawnIntent.Point`; each wool cage is centred on its wool spawn point (the 2×2 floor-wool marker). Move the
  point → move the cube. Setting these positions *is* how the author places the structures.
- **X / Z must snap to full integers.** The cube has a 2×2 centre, and Minecraft block centres sit at `.5`, so
  the de-facto midpoint is a whole integer. Spawn/wool X,Z must be stored as integers (not `.5`), or the 2×2
  centre — and thus the whole cube — won't align to blocks.
- **Y snaps to the column top (`ymax`).** Player and wool spawns are forced to the **topmost layer** — the
  layer-segment `ymax` at that column (the DB blob is `ymin, ymax`) — so the cube floor rests on the terrain
  surface (structures sit *on top of* terrain, never embedded / floating).
- **Spawn yaw → door orientation.** The spawn cube's single 4×4 door must face the intended entry direction: the
  player spawns facing out through the door. The spawn `Yaw` and the door wall are derived together so the entry
  faces the right way; monuments then sit against that door wall / the back wall per §3.

## 6. Resolved decisions

- **No stained clay in the wool cage** — clay is spawn-cube-only (layer-4 strip). Wool cage uses wool only.
- **Monument** — bedrock pedestal (elevated one block) · air placement cell · **stained-glass cap in that
  monument's wool colour** · sign against the pedestal.
- **Doors** — wool-cage doors are **stained-glass panes (id 160) in that wool's colour**; the spawn-cube door is
  **open air**.
- **Slit** — the layer-6 course is a true gap (air), not glass.
- **Layers numbered from the floor** — floor 0 (spawn + 2×2 wool marker), doors from layer 1, colour strip at
  layer 4, light slit at layer 6, roof hole at layer 8. Colour-strip size is identical across both variants; the
  only differences are door count/size and chests (wool cage) vs monuments (spawn cube).

## 7. Open questions — observer platform (§2b)

- **Top/bottom sign content.** The grid puts bedrock+signs on all four edges; only the left and right walls have
  specified text. Do the top/bottom walls carry signs too (blank / mirror of left+right / other text), or no
  signs?
- **Two signs per wall.** Each wall has a 2-wide sign pair — do both signs show the same 4-line text, or is it
  split across the pair?
- **Sign facing.** Assumed to face **inward** (readable by an observer standing on the platform) — confirm.
- **Platform Y.** Snap to the terrain surface (`ymax`, like the cubes) or place at the observer's authored
  (floating) height? Observers typically spectate from above.
