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

## The backlog's fixes, while new capabilities wait
**The author has paused new capabilities** (November 2026): the backlog's fixes are worked first, then
optimisation and the user experience. The generator's behaviour is frozen — a fix or a speed-up that changes
nothing it makes is welcome, a change to what it makes is not — and its layout entries move to
`docs/generator/ideas.md`.

- [ ] **TS238 — The Problems list reads whole.** The sketch bar's Problems button opens a list whose rows fold
  out and link to their rule, and a row's title is cut to one line with an ellipsis (`.problem-sum`,
  `problem-list.css:40`), so a broken rule is read by its first words. A row wraps its title in full; and every
  rule the sketch's checks raise (`SketchPlayCheck`, the board and seam checks the plan's rules moved into) is
  listed there, since a rule that moved into the Sketch and no longer reaches the reader is the failure.
  `docs/tools/sketch.md`.
- [ ] **TS164 — Every Sketch phase's inspector is one width.** Decoration's and Palette's inspector is 420 px
  (`.workspace-inspector--wide`, `editor.css`) against 280 px in every other phase, so the canvas jumps on entering
  them. The author's ruling: 420 px is the inspector's default in every phase, one width rather than a rule that
  widens it, since a reader can drag the panel narrower. `docs/tools/sketch.md`.
- [ ] **TS130 — Every outline on the canvas edits the same way.** A line the canvas draws — a shape's polygon, a
  polyline, a modifier's path, a stroke, a fluid's or flora's outline — has points and edges, and wherever the
  element allows it a point drags, a point is inserted at an edge's midpoint and a point is deleted, by the same
  gestures as a shape's (`sketch-edit-controller.js`). The API's point edits and bends reach the same outlines:
  `editShapes` and `bendShapes` address a shape's `vertices` alone (`SketchGeometryEdit.Outline`), where
  `outlines` already writes a stroke's, fluid's or flora's `points` (`Refinement.Outlined`). `docs/tools/flow.md`.
- [ ] **PG29 — A kit loses nothing it states.** `MapParser.ParseKits` reads items, armour, effects, `max-health`,
  `force` and `clear`, and drops every other kit element in silence, so an exported map plays differently from the
  one read: `game-mode` in 325 corpus maps, `action` in 112, `double-jump` in 68, `health` in 56,
  `knockback-reduction` in 48, `walk-speed` in 40. Every element is read and written (`Kit`, `XmlWriter`,
  `Serializer`, the `kit` row, with a migration); a map is never refused for one (author). `docs/pgm/supported-maps.md`.
- [ ] **WE168 — A room's interior is placed relative to its house.** The chests (`WoolChests`, `RoomFrames.InteriorCorners`) and the monuments (`RoomFrames.MonumentSlots`) are
  stated on the room's `house` as positions relative to its first wing, movable in Layout, and a migration
  carries each derived position into the stored one. `docs/world-export/structures.md`.

## Bugs: what the studio does wrong now
**The author put the user-facing bugs first** (the backlog audit, November 2026). Each entry is the product doing
something wrong today, on a page a person uses or on the API an agent drives; the composer's own bugs wait
in `docs/generator/ideas.md` with the rest of the generator.

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
  `SeedFolder.Houses`. One cause is the saltbox's: a front on an end wall takes the low side as the steep one
  (`RoofField.SteepSide`), a fixed choice the image cannot mirror, so the image's steep side has to follow the
  authored room's. `docs/world-export/structures.md`. *Evidence: `dark-oak-stilt-hut 18x9 door -z mirror_x at
  (-9, 65, -73)`, `jungle-saltbox-cottage 14x13 door -x at (-6, 76, -78)`.*
