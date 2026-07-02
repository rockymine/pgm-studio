# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
When this board drains, pull the next theme up from `BACKLOG.md`. Board rules live in `CLAUDE.md`
(§ "Status & task board").

Task ids are a section letter + number (`S13`, `B10`, `G15`) — **globally unique and stable** across all
three files. Moving a task between files never changes its id; never renumber or reuse.

## Current focus — Sketch world-folder export (P9)

### P9 — Sketch world-folder export (`.mca` + `level.dat` bundled with the XML)

Sketch-originated maps have no real voxel world (the "world" is a synthetic `layer.parquet`), so they can't
ship a playable PGM map today. P9 synthesises a real Anvil world from the sketch columns + authored intent and
bundles it with `map.xml`. **Delivered at the Configure export point as one ZIP: a `{slug}/` folder containing
`map.xml`, `level.dat`, and `region/*.mca`.** **Normal Configure-imported maps export XML only** (they already
ship a real world). Anvil format = the **1.8–1.12 numeric block format** the `AnvilRegion` reader already
understands (matches the studio's supported range, proto ≥1.4.0 / pre-1.13).

**Full spec (complete, no open questions): `docs/contracts/sketch-world-export.md`** — terrain (bedrock floor /
stone fill), the shared 8×8×8 bedrock cube shell (light slit, roof hole, coloured strip, glass-pane/open doors),
wool-cage chest loadout, in-cube auto-wired monuments + sign text, `level.dat`, and the placement/coordinate
constraints (§5). Foundation-first order: **P9a + P9c** (writer + terrain, self-verifying via read-back), then
P9d cube library, then the stamping tasks (P9g/h/i), then wiring (P9e/f) and authoring (P9j/k).

- [ ] **P9a — `AnvilRegionWriter` (invert `AnvilRegion`).** New writer in `PgmStudio.Minecraft` emitting the
  numeric format `AnvilRegion.cs` reads: 1024-entry sector/location table (3-byte big-endian offset + 1-byte
  sector count), per-chunk 4-byte length + compression byte (2=zlib) + payload, `Level.Sections` with
  nibble-packed `Blocks`/`Data`/`Add` (index `(y<<8)|(z<<4)|x`) + `Y` tag, `Level.xPos/zPos`,
  `TerrainPopulated`. Input = an id+data voxel volume. `fNbt` handles the NBT save. **Round-trip test:** write →
  read back via `AnvilRegion` → assert block-equal (mirror of the reader is the spec).
- [ ] **P9b — `level.dat` writer (greenfield).** New NBT writer (no code today) → gzipped `Data` compound:
  `SpawnX/Y/Z` (world spawn from the observer/default spawn point), `generatorName=flat`, `LevelName={slug}`,
  version/`DataVersion` fields matching a 1.8–1.12 world, `GameType`, `MapFeatures=0`. Source the exact tag set
  from a 1.8-era `level.dat` sample.
- [ ] **P9c — Terrain synthesis from sketch columns.** `SketchRasterizer.RasterizeColumns` `(X,Z,YFloor,YTop)` →
  id+data voxel volume for P9a: **y=0 = bedrock (id 7)**, the solid `[YFloor,YTop]` span above = **stone (id 1)**.
  Handle stacked disjoint segments per `(x,z)` column; chunk the world into 16×16 region grids. (Fill materials
  are deliberately flat for now — a later task can add a surface palette.)
- [ ] **P9d — Block-template + cube-shell library.** Define block templates once (bedrock 7, stone 1, air 0,
  stained-glass 95:data, stained-glass-panes 160:data, wool 35:data, stained-clay 159:data, sign + chest
  tile-entities). Build the shared
  **8×8×8 hollow-bedrock cube emitter** parametrised by colour + variant (floor-indexed: roof 4×4 hole at
  layer 8, air light-slit at layer 6, colour strip at layer 4, 2×2 floor wool at layer 0, doors from layer 1) per
  `docs/contracts/sketch-world-export.md` §2. Colours from a dye slug → data nibble (0–15) via `BlockColors`.
  Consumed by P9g (wool cage) and P9h (spawn cube).
- [ ] **P9e — Full-map ZIP export at the Configure export point.** At `MapXmlEndpoint` (`GET /api/map/{slug}/xml`),
  for **sketch-origin maps** return a ZIP of `{slug}/map.xml` + `{slug}/level.dat` + `{slug}/region/r.<x>.<z>.mca`
  (wiring together P9a–P9d/g/h/i after the existing playability/traversability gate). Configure-imported maps keep
  the plain-XML response. Wire the download UI to request the bundle for sketch maps.
- [ ] **P9f — Sketch-origin gate + export UX.** Branch zip-vs-xml on sketch origin (presence of the
  `sketch_layout_json` artifact — the durable "was a sketch" signal, since `MapStage.Sketch` advances to
  `configure` on finish); surface build/export errors; keep the branch downstream of the traversability 409.
- [ ] **P9g — Wool-cage stamping.** Emit the wool-cage variant (§2: layer-4 wool strip, 2×2 floor-wool spawn
  point, four 2×3 doors in **wool-colour stained-glass panes** (id 160)) via the P9d emitter, coloured per `WoolIntent.Color`,
  anchored on the (integer-snapped, §5) wool spawn point and resting on the terrain surface (`ymax`). See P9i for
  chests.
- [ ] **P9h — Spawn-cube + monument stamping.** Emit the spawn-cube variant (§2: layer-4 stained-clay strip, 2×2
  floor wool, single 4×4 **open-air** door), team-coloured, anchored on the (integer-snapped) `SpawnIntent.Point`
  at the terrain surface. **Door wall derived from the spawn `Yaw`** (player spawns facing out; §5). Stamp the
  **in-cube monuments** (§3, fully auto-wired — no `MonumentIntent`): bedrock pedestal elevated one block in the
  corners · air placement cell · wool-colour glass cap · sign against the pedestal; positions by wool count
  (1–2 → door wall · 3–4 → back wall · 5+ → fill back wall); label per §3 (`Place the` / bold colour / coloured
  `Wool` / `here!`).
- [ ] **P9i — Wool-cage chest loadout.** Two stacked chests in each of the 4 interior corners (§2a), each a
  27-slot chest tile-entity: chest A = planks×16 · Speed I (3:00) potions · golden apples×16; chest B = diamond
  leggings · Power I + Infinity bows · planks×16. One 9-slot row per item type.
- [ ] **P9j — Position snapping + spawn orientation (sketch-origin authoring).** The authored spawn/wool
  positions anchor the structures (§5), so on sketch-origin maps they must be constrained: **X/Z snapped to full
  integers** (the 2×2 cube centre needs a whole-integer midpoint — MC block centres are `.5`), **Y snapped to the
  column top (`ymax`)** so the cube floor rests on terrain, and the **spawn `Yaw` captured/derived to orient the
  door** (player faces out). Enforce where positions are set for sketch maps (`/sketch/finish` land + the
  Configure position editors); store the snapped coords + yaw.
- [ ] **P9k — Remove the monument authoring step (sketch-origin Configure).** Monuments are fully auto-wired
  from wool count + spawn-cube geometry (§3), so the Configure wizard's manual monument step is dead for
  sketch-origin maps — drop it from the flow (and stop persisting `MonumentIntent` for these maps). Touches the
  Configure wizard (authoring UI), not the exporter.
- [ ] **P9l — Observer-platform stamping.** Emit the observer template (§2b) at the (integer X/Z-snapped)
  `ObserverIntent.Point`, at the observer's **floating authored Y** (not terrain-snapped): a **solid 6×6 bedrock
  platform** with four identical inward-facing **info boards** (1×2 bedrock + 2-sign pair) at the edge centres
  (grid in §2b). Per board: **left sign = map name** (`=== / [CTW] / {map name} bold / ===`), **right sign =
  authors** (`made by (italic) / {authors}`) from the map meta; the stamper derives each board's sign order +
  facing from its edge.
