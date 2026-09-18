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

## The composer states distances in cells, and a grid scale moves them

- [ ] **G273 — A fine row should key to the hub's legs where the front has none.** `MidCarver.Keyed` lines
  its stones up with the faces of the unit's **front row**, which is the right feature when there is more
  than one — but a front presents a single face on about two boards in three, so the keyed row lands on one
  or two boards in twenty-four and the rest centre. The hub always has a body, and the holed forms (ring, G,
  P, twin, double-hole) all carry legs either side of a bay: those are the same kind of feature and they are
  there on every board. Read them out of the hub box's pieces the way `TryCarve` reads the front's, and key
  to them when the front offers nothing. `docs/generator/model.md` §5.13 and `rules.md` amendment 38.

  *Evidence: `p24 rot_180 seed 3` (`opus5-stennerwath`) — `frontline-t1` is one 64-block bar, so nothing to
  key to, while `hub-t2 x[-32,0)` and `hub-t3 x[16,36)` are two legs 16 blocks apart. Its stones sit centred
  at `x[-12,16)`, in the hub's own bay rather than in front of either leg.*

- [ ] **G272 — Bound the run a player walks around a hub's hole at 40 blocks.** A hole's job is rotation
  (`CT8`): a loop round it gives an alternative route between lanes. Past a certain length it stops being a
  loop and becomes a wall — two players on opposite sides never meet and neither can change direction. The
  author's bar is **40 blocks on the hole's longest side**. `HubBoxCells` sizes the hub from its budget share
  alone and the holed bodies build every wall one corridor thick, so the hole takes the whole difference as
  the box grows. Either the ring's walls thicken with the box or the body splits the void — a `double-hole`
  where one ring's hole would run long. Lands in `TeamUnitAllocator.ChooseHubBody`/`ChooseHubWalls`;
  `docs/generator/model.md` and `rules.md` (`CT8` is the rule that states the envelope and measures nothing).

  *Evidence: 12 seeds a band, cell 4 — the hole run's median is 16 · 16 · 18 · 24 blocks up the ladder and
  **14 of 38 centi boards exceed 40**, none below it. The corpus of 359 CTW maps puts the run's median at 20
  and its p90 at 43 (836 voids), so the bar is the corpus p90. `opus5-threapland` is the worked case: one
  76×36 hole at cell (-10,-19), where a double-hole hub would have given two.*

- [~] **G264 — The composer seats a wool room its own lane-width from the hub, so a goal's setback is the
  band's corridor and not a distance.** On a composed board the gap `WL12` names is the **inside corner of a
  bent wool**: `wool-a-room` sits at the far side of the wool lane, so the void between the room and `hub-t1`
  (or `frontline-t1`) is exactly that lane's width — 12 blocks at nano and 16 at milli and centi on the cell-4
  grid, against the author's floor of 16 from the room to the frontline. The **room's setback along its lane**
  takes a block basis of its own, in `WoolBoxEmitter` rather than in the seat clearance.
  `docs/generator/rules.md` and `model.md`.

  *Evidence: `p8 rot_180 seed 0` at nano, wool lane 3 cells — `wool-a-room x[-1,1) z[20,23)` stands off
  `hub-t1 x[-6,6) z[14,17)` by 12 blocks, the lane's own width and four under the floor. Nano is where it
  binds: a 16-block floor is 4 cells against a 3-cell lane, so the room has to move rather than the lane widen.*

## Nothing else is on the board

The walk drained, and so did what a building may stand on — `DR-PASS` is the author's ruling, the passage is
owed round a group of buildings, and `sketch/seats` answers it forwards (`FEATURES.md`).

`docs/backlog-strategy.md` names **the layer word** as the programme to pull next: `B263`, `B264`, `WE28`,
`TS64`, in `BACKLOG.md`. Three of the four want something first — `B263` a ruling on how the canvas says which
storey a prop is on, `B264` and `TS64` a surface — and `WE28` is the one that is settled and backend only.
