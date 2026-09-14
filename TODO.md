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

## The hill: a goal owned by standing on it

PGM's fourth objective family: a region a team owns by standing in it. The codec has landed (`FEATURES.md`) —
both spellings of a point and the `<score>` module parse, store and re-emit — so what is left is what the
studio should *say* about one and what it should *build*. The contract, the state machine and the corpus
measurements behind every number below are `docs/pgm/control-points.md`; read it before the first entry,
because the group's shape comes from it. What a board should *be* — square pads, the count per team count, one
point dead centre and the rest to the sides — is the author's and is settled in `docs/gameplay/approaches.md`.
Payload stays out: it is a furnace minecart players push, behind a server experiment flag whose XML PGM says
will change.

**The foundation is one sentence: what a board is played for is derived in several places and a control point
is in none of them.** The playability half and the plan half have landed (`FEATURES.md`) — `NavPoints`,
`DeclaredGoals` and `PL3` now know all four objective families, and a plan can state the capture points a
board is played for. `MapIntent.HasDestroyGoal` and `FrontlineTerms` are not this: both are about destroy
goals specifically and are right as they stand.

The contract gates have landed with them. What is left of the cause is the **dressing** half, which walks
the intent's goals one family at a time in three more places (`DressingScope.WaypointsOf`, `.GoalGroundAt`,
`.GoalClearanceAt`) and so plants props on a pad and routes the board between everything except its hills.

- [ ] **TC7 — The configure tool cannot place a hill.** The API is the way in — an agent adds
  `controlPoints` to the intent it already posts (`docs/pgm/control-points.md` §9) — and the wizard has no
  step for it. What the step states is a count and the anchors; the tuning is one shared block and already
  has its answer (`capture-time="5s"`, `points="1"`, `<score><limit>750</limit></score>`), so the step fills
  it rather than asking. The anchors are the plan's to derive and it does (`FEATURES.md`), so the step asks
  for a count on a board with no plan behind it and shows what the plan already worked out on one with.

- [ ] **WE120 — A team tint on a one-island board paints the whole map one colour, silently.**
  `TeamTerritory.Ownership` resolves an owner per canonical island and `anchor.TryAdd(id, s.Team)` keeps the
  first spawn read, so a board whose ground is one landmass — the ordinary shape of a capture board — gives
  every tinted cell to whichever team compiled first. Raise a finding where a theme in play carries a
  `TeamTintedMaterial` and an island carries spawns of more than one team, naming the island and the teams;
  it belongs beside the other paint findings, at the point the painter resolves its themes. Evidence:
  `specs/opus5-casemate` compiles to `islandTeams: {"1": "red"}` with spawns at `(0, −44)` red and `(0, 44)`
  blue, and its world holds **678 red terrain blocks on blue's half against 74 blue**, all 74 of them the
  gate house, which is a structure stamp and knows its own team.
