# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Three numbers are the author's and are not to be re-derived.** A protection region is at most **20×30**
blocks (`ST10`), a building footprint at most **20×20** (`ST9`), and the smallest room with no building over
it is **4×4** (`WX2`). A dressed prop's 192-cell ceiling (`HP3`) and a room building's 20×20 measure the same
concept since `WE71`, and holding them apart is a deliberate not-yet.

## Bugs: what the studio does wrong now
**The author put the user-facing bugs first** (the backlog audit, November 2026). Each entry is the product doing
something wrong today, on a page a person uses or on the API an agent drives; the composer's own bugs stay
deferred in the backlog with the rest of the generator, and `PG29` waits there on the author's ruling.

- [ ] **TS167 — The seat preview lets a house stand on paving, as the build does.** `/sketch/seats` refuses a
  house on a route cell as ground taken (`ClaimRaster.Stops`) while the pass seats it (`Decorator.FirstOverlap`
  ignores paving), so the preview and the build disagree. Make the seat read agree, and check a path can be drawn
  up to the door through the keep-out rings. `docs/tools/sketch.md`.

- [ ] **TS141 — A room style stated as a library fork is honoured, or refused.** In `dressing.styles` a
  house stated as `{"library": <name>, "kind": "house", "shell": <parts>}` builds the fork (`DressingJson`
  resolves the shell over the row), but the same object under `roomStyles.spawn` stores 200, raises nothing and
  builds the bare library row. Resolve `roomStyles.<part>` through the same reader the dressing registry uses,
  or answer `RQ1` at the path; a second accepted shape that is silently dropped is the failure.
  `docs/tools/sketch.md`. *Evidence: `pgm-studio-mapgen/reports/sonnet55-halcyon-cays.md`, the kit section — the
  spawn halls came out cyan clay over pink-white clay.*

- [ ] **TL35 — A copied tree states no species.** A tree save requires `species` (`RQ1`) and the library
  answers one for every row, so every copied recipe states `"species": "oak"` — `acacia-1`, `birch-3` and
  `sequoia-1` alike, and so every entry of the seed folder's `trees.json`. A copied tree
  reads only its body, so the word is inert, but it says of an acacia what the showcase's `kinds.json` exists to
  stop anyone reading off a block. `TreeStyleSaveRequest.Species` is required of a template only,
  `PropStyleLibrary.TreeOf` gives a copied row none and `DressingJson` writes none for one; the snapshot is cut
  again after. `docs/tools/library.md`.

  *Evidence: all 84 entries of the snapshot read `"species":"oak"`.*

- [ ] **PG31 — A spawn keeps every region it states.** `Spawn.Region` holds one region, so a spawn written
  with several `<region>` children keeps the first and loses the rest on read — 83 spawns in 34 corpus maps
  (`skill_issue`'s defenders spawn at either end). A portal naming a lost one exports a reference to nothing.
  `Spawn.Regions`, each with its own yaw, read and written by `MapParser`/`XmlWriter`, stored and served.
  `docs/pgm/portals.md`.

- [ ] **WE162 — Two seeded houses stamp their doorway on other columns in a mirror image.** The stamper's
  orbit tests (`A_room_and_its_rot_180_image_stand_on_the_same_columns`,
  `A_room_and_its_mirror_images_stand_on_the_same_columns`, `HouseStamperTests`) run over the eight houses they
  were written for and pass; run over every seeded house, `dark-oak-stilt-hut` (hip roof, stilts) and
  `jungle-saltbox-cottage` (saltbox) fail, their door columns landing apart between a room and its image. Find
  which of the two forms moves the doorway, fix it in `HouseStamper`, and widen `RoomStyles()` to
  `SeedFolder.Houses`. `docs/world-export/structures.md`. *Evidence: `dark-oak-stilt-hut 18x9 door -z mirror_x at
  (-9, 65, -73)`, `jungle-saltbox-cottage 14x13 door -x at (-6, 76, -78)`.*

- [ ] **RP98 — A stated null is a value the store refuses, not one the report trips on.** `kit.CellMaterial(
  rise=None)` writes `"rise": null`; the store answers 200 and `GET /map/{slug}/report` then answers `400 RQ1
  Cannot get the value of a token type 'Null' as a number @ rim.material.rise`. Refuse the null where the
  document is stored (the material reader), and have the kit treat an argument of `None` as unstated.
  `docs/architecture.md`. *Evidence: `pgm-studio-mapgen/reports/sonnet55-pippin-coomb.md`, the kit section.*
