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

**The foundation, and it is one sentence: what a board is played for is derived eighteen times and a control
point is in none of them.** `DeclaredGoals` already claims the job — its docstring says it is "the one reading
of it, so the playability reads and the export gate answer over the same set" — and it cannot be, because it
sits in `Api/Services` where `Export`, `Pgm` and `Analysis` cannot reach it, and it walks wools, destroyables
and cores and stops. So every consumer kept its own walk: `DressingScope.WaypointsOf`, `.GoalGroundAt` and
`.GoalClearanceAt`, `MapExportComposer:523` and `:536`, `PlanValidator:166` (`PL3`), `MapIntent:811`
(`HasObjectives`), `FrontlineTerms:38`, and the three playability reads. Eighteen sites walk the intent's goals
one family at a time.

Fixing that is what makes the entries below small, and it is what the first two *are*: a goal set in the lowest
project every consumer reaches, control points in it, and every caller moved in the same commit. The contract
gates after it each land with a test that fails on the old behaviour; the authoring pair is the half that lets
a board be drawn with hills at all.

- [ ] **WS62 — Coverage, reach and the walk read are blind to a hill.** All three resolve a board's goals
  from the wool/destroyable/core lists, so on a capture board `GET …/coverage` answers 71.8% dead with its
  two largest dead patches centred on the two side hills, `04-routes.txt` prints "no route between a spawn
  and a goal", and `/reach` reports the same ground unreached. The walks exist — `GET …/walk?from=0,-48&
  to=-33,-1` on `opus5-threap-edge` is walked end to end — so what is missing is only that a control point
  counts as a goal in the set those three quantify over. One derivation, three readers.

- [ ] **TN19 — `PL3` calls a capture board objectiveless.** The plan tier counts wools, destroyables and
  cores, so a board played for hills is told "this plan has no objective — no wool, destroyable or core, so
  nothing wins the match" on every compile and every evaluate. A plan carries no capture point today
  (`TC8`), so the rule cannot count one there; what it can do is stop claiming the board wins on nothing
  when the intent beside it names hills and a score limit. Either take the intent's `Gamemodes` into the
  check or say the sentence the board's own case needs. Evidence: `specs/opus5-threap-edge`, a three-hill
  board that exports as `<gamemode>koth</gamemode>` with `<limit>750</limit>`, scores 0 and valid, and
  raises `PL3` twice per run.

- [ ] **PG8 — `required` left off ends the match on first capture.** `SimpleGoal.isRequired()` defaults to
  **true** at proto ≥ 1.4.0 — the studio's whole supported range — and `GoalsVictoryCondition` finishes the
  match the instant one team completes every required goal it can complete. A one-hill map with the attribute
  omitted ends on the first capture; a three-hill map ends when one team holds all three. Refuse a studio-
  authored KotH board that omits it, and raise a finding on an imported one. The escape hatch belongs in the
  same rule: `show="false"` clears the `stats` option, and `GoalMatchModule.addGoal` drops a goal without it,
  so a hidden point never ends anything. Evidence: 275 of 316 corpus KotH points write `required="false"`.

- [ ] **PG6 — A point that scores needs a `<score>` element, and nothing says so.** `tickScore` reads
  `ScoreMatchModule`, and `ScoreModule.parse` returns null when the document has no `<score>` child — so a
  map with `points="1"` on every hill and no score module scores nothing, all match, with no error anywhere.
  Raise a finding when a control point with a non-zero `points` or `owner-points` sits in a document with no
  `<score>`. Evidence: `koth/qboid` carries `<score><kills>0</kills><deaths>0</deaths></score>` under the
  comment *"placeholder so the score module will show up"*; 95 of the 103 corpus KotH maps declare a limit.

- [ ] **PG7 — A pad in a material outside the colour-affected set never changes colour.** The display regions
  are filtered to PGM's colourable materials — wool, carpet, stained clay, stained glass and panes, banners,
  ink sack on 1.8 — and `hard clay` is not one of them while `stained clay` is. A hill built in hardened
  clay, stone or planks parses, exports, loads and shows nothing, which is the map's only feedback that it
  was captured. Raise a finding when a point's progress or owner display region holds no colour-affected
  block, checked against the built world at the export gate where the blocks exist. Evidence: 246 of 359
  corpus pads are stained clay, 71 wool, 42 stained glass.

- [ ] **TC8 — Derive a board's points from its spawns, rather than being told each one.** The fan is built:
  an intent carrying a symmetry orbits its points by position, so the centre stays one point and a side
  becomes a matched pair (`SymmetryExpander.FillControlPoints`). What is missing is the step before it —
  choosing the anchors. The author's rule (`docs/gameplay/approaches.md`): one point at the map's centre of
  symmetry, the rest to the sides at **0.66 of the centre-to-spawn distance** on two teams and 0.90 on four,
  the four-team ring at 45° to the spawns. A count plus the spawn frame gives every anchor. It lands in the
  plan compiler, which has no capture-point placement at all — which is also why a plan-compiled intent, the
  one path that carries no symmetry, states each point by hand today. Evidence: of the 80 two-team corpus
  maps with a usable spawn frame, 60 hill sets are closed under that rotation and 39 of 55 three-point maps
  are exactly a centre plus a mirrored pair.

- [ ] **TC7 — The configure tool cannot place a hill.** The API is the way in — an agent adds
  `controlPoints` to the intent it already posts (`docs/pgm/control-points.md` §9) — and the wizard has no
  step for it. What the step states is a count and the anchors; the tuning is one shared block and already
  has its answer (`capture-time="5s"`, `points="1"`, `<score><limit>750</limit></score>`), so the step fills
  it rather than asking. Deriving the anchors from the board instead of asking for them is `TC8`.

- [ ] **WE120 — A team tint on a one-island board paints the whole map one colour, silently.**
  `TeamTerritory.Ownership` resolves an owner per canonical island and `anchor.TryAdd(id, s.Team)` keeps the
  first spawn read, so a board whose ground is one landmass — the ordinary shape of a capture board — gives
  every tinted cell to whichever team compiled first. Raise a finding where a theme in play carries a
  `TeamTintedMaterial` and an island carries spawns of more than one team, naming the island and the teams;
  it belongs beside the other paint findings, at the point the painter resolves its themes. Evidence:
  `specs/opus5-casemate` compiles to `islandTeams: {"1": "red"}` with spawns at `(0, −44)` red and `(0, 44)`
  blue, and its world holds **678 red terrain blocks on blue's half against 74 blue**, all 74 of them the
  gate house, which is a structure stamp and knows its own team.
