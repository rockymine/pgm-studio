# Sketch world-folder export (P9)

Sketch-originated maps have no real voxel world (their "world" is a synthetic `layer.parquet`). To ship a
playable PGM map, the studio synthesises a real Anvil world from the sketch column geometry
(`SketchRasterizer.RasterizeColumns`) + the authored `MapIntent`, and bundles it with `map.xml`.

**Delivery.** At the Configure export point (today `GET /api/map/{slug}/xml`, `MapXmlEndpoint`), a
**sketch-origin** map returns a **ZIP** whose entries sit at its top — a world directory's own contents,
which is what a server is handed, so the archive unpacks into whatever folder the caller chose:

```
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

From `SketchRasterizer.RasterizeColumns` → `ColumnSegment` solid runs, `(X, Z, YFloor, YTop, Layer)`:

- **y = 0 = bedrock** (id 7), under a column whose ground reaches it and nowhere else. A slab standing over
  open void — a bridge across a strait, a deck overhanging a court — lays no floor beneath itself, so a fall
  off it meets the void and the column stays out of the Y0 set a void filter reads.
- The solid span above (`[YFloor, YTop)`) = **stone** (id 1).

Flat materials for now — a later task may add a surface palette. Handle stacked disjoint segments per `(x,z)`.

**Structures sit on top of the existing terrain** — a cube/monument base rests on the terrain surface Y at its
footprint, never embedded in or floating above it.

---

## 2. Room shell template (the shipped styles)

Both the **wool cage** and the **spawn cube** are one shell whose footprint comes from the room's resolved
frame — its plan piece inset one block, or the 8×8 marker-anchored default
(`docs/world-export/structures.md`, WX1: the shipped 10×10 piece yields the original 8×8 shell) — finished by
a `RoomStyle` (that file's §7: a course stack per part, plus the pad and doorway stamped over them). Layers
are numbered **from the floor** (floor = layer 0, roof = layer 8).

What follows is the two **shipped** styles, not a law about shells: a cage and a spawn differ only in their
band material, their door and their contents (chests vs monuments), and everything below is one course stack
away from being something else.

| Layer (from floor) | Wool cage | Spawn cube |
|---|---|---|
| 8 (roof) | bedrock, **centred hole** (proportional, capped 4×4) | bedrock, **centred hole** |
| 7 | bedrock | bedrock |
| 6 | **missing** (light slit around all four walls) | **missing** (light slit) |
| 5 | bedrock | bedrock |
| 4 (colour strip) | **wool** (room colour) | **stained clay** (team colour) |
| 3 | bedrock | bedrock |
| 2 | bedrock | bedrock |
| 1 (doors begin) | bedrock + door opening | bedrock + door opening |
| 0 (floor) | bedrock, **wool pad** (room colour) — wool spawn point | bedrock, **wool pad** (team colour) — player spawn |

**Doors** (begin at layer 1; widths per `structures.md` WX7 — 4 on a roomy even wall, 3 on an odd wall, 2 at
the 4-across minimum):
- **Wool cage:** one door per **entry interface** (a land seam or an abutting build zone — WX6), **3 tall**
  (layers 1–3), made of **stained-glass panes (id 160) in the room's wool colour**. The marker-anchored
  default cage keeps a door per wall. The material is one of the four `Domain.DoorMaterials` choices — air,
  cobweb, stained glass, panes — and no more: the wool room's block rule is a whitelist, so a door it does
  not name cannot be broken (`structures.md` §7).
- **Spawn cube:** a **single door, 4 tall** (layers 1–4) on the yaw-derived wall, **open (air)** — pinned
  there whatever the style says, since a player spawning in has to be able to walk straight out.

Colour: the layer-4 strip + the 2×2 floor wool follow the **room colour** (wool cage: wool, **no stained clay**)
/ **team colour** (spawn cube: clay strip, wool floor). Dye slug → data nibble (0–15) via `BlockColors`
(wool 35, clay 159, glass 95 all key off the same data value).

### 2a. Wool-cage chests

Each of the **4 interior corners** holds **2 chests stacked** (bottom + top). A "row" = 9 slots.

- **Chest A** — row of **planks ×16**; row of **Speed I potions (3:00)**; row of **golden apples ×16**.
- **Chest B** — row of **diamond leggings**; row of **Power I + Infinity bows**; row of **planks ×16**.

### 2c. The goal's buried plate, and the chest under it

Every destroyable stands over a **one-block-thick 5×5 bedrock plate**, buried **three courses** beneath the
ground's own surface block and centred on the goal's anchor. Its job is a shaft: the goal's foundation is
ordinary terrain, so without the plate a tunnel driven up from below reaches daylight under the monument and
the ground it stands on is mined out from under it. One course is the whole of the plate — a thicker slab
reads as a wall grown out of the floor rather than as a plate under it — and the span is fixed rather than
matched to the structure, because what it stops is a shaft rather than the goal.

**A core takes no plate** (the author's ruling). A core is won by digging under it until its lava leaks, so
bedrock at a fixed depth would be a floor laid across the objective's own rules: the dig a `float`/`leak`
pair asks for is bounded by the terrain the board has, not by a slab the stamper put there. The chest below
is a core's all the same.

**The depth is what makes room for the chest.** At one course down there is nothing between the surface and
the rock; at three there are two, and a **defence chest** stands at the footprint's centre with
the course above it carved to air so the lid opens. The course above *that* is left whole, so a defender
breaks one block of ground and drops onto the supply. It is the same 27-slot loadout an approach wall carries
(`DefenseChest`, `docs/generator/rules.md` ST4): dark-oak and spruce planks and crafting tables to build with,
end stone and a redstone block to reinforce, and two Efficiency II iron pickaxes. Both places are ground a
team holds, which is why it is one chest rather than two designs.


### 2b. Observer platform (standalone — not a cube)

The observer/default spawn (`ObserverIntent.Point`) gets its own template: a **solid 6×6 bedrock platform**
(integer-snapped X/Z; placed at the observer's **authored, floating Y** — *not* terrain-snapped, §5 — and lifted above the highest block in its own 6×6 where the board builds something over that point, since the floor course is bedrock written through whatever occupies those cells; a lift is reported as `EX5`). At the
centre of **each of the four edges** sits an identical **"info board"**: a **1-tall × 2-wide bedrock wall** with a
**2-sign pair mounted on its inner face, facing the platform centre**. Top-down layout (`b` = raised bedrock,
`s` = sign, `o` = solid bedrock floor):

```
oobboo
oossoo
bsoosb
bsoosb
oossoo
oobboo
```

Each board's two signs are **split by role** — viewed from the centre, the **left sign = map name**, the
**right sign = author info** (map name + authors from the map meta):

- **Left sign:**
  ```
  ===
  [CTW]
  {map name}   (bold)
  ===
  ```
- **Right sign:**
  ```
  made by   (italic)
  {author 1}
  {author 2}
  {author 3}
  ```

All four boards are identical; the stamper computes each board's left/right sign placement + facing from which
edge it sits on.

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

- **The room's plan piece sizes the structure; the marker places the pad.** A wool or spawn marker on a
  role piece takes the shell its piece resolves to (`docs/world-export/structures.md` — the frame rules,
  WX1–WX7); a marker with no piece (hand-authored / sketch-origin intents) keeps the marker-anchored 8×8
  default shell. The exported spawn/wool point is the **pad centre**, following any clearance shift (WX5).
- **X / Z live on the half-block lattice.** A whole coordinate is a grid line (the 2×2 pad straddles it); a
  `.5` coordinate is a block centre (a 1×1/3×3 pad). Parity must match on both axes — the pad is always
  square (WX3).
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

- **Observer platform (§2b)** — solid 6×6 bedrock; four identical inward-facing info boards (1×2 bedrock +
  2-sign pair each); per board the **left sign = map name**, **right sign = authors**; placed at the observer's
  **floating authored Y** (not terrain-snapped).
