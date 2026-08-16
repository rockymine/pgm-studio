# Design decisions that read as bugs

Non-obvious invariants and conventions in this codebase that repeatedly trip up reviewers
(human or agent): each looks like a defect at the point of use, but is deliberate and enforced
elsewhere. Collected from review passes (most recently the P9 sketch-world-export review, where
most refuted findings traced back to one of these). When a review candidate matches an entry
here, check the cited enforcement point before filing it.

Format per entry: **the decision** — *why it looks wrong* — **where it's enforced/proven**.

## Geometry & regions

### Rect/box max coordinates are exclusive (cell-centre sampling)
Rectangles and cuboids treat `min` as inclusive and `max` as exclusive: a single block cell is
`[x, x+1) × [z, z+1)`, sampled at its centre `(x+0.5, z+0.5)`.

- *Looks wrong:* a rect built as `anchor ± half` around a `Size`-wide structure appears one
  column too large on the +X/+Z sides (e.g. `SketchWorldBuilder.CubeRect` returns `cx±4` for a
  cube whose blocks span `[cx-4, cx+3]` — exactly right under this convention).
- *Enforced:* `Analysis/Region/RegionGeometry2d.cs` maps `rectangle` to `Box(min, max)` with no
  `+1` and a single `block` to `Box(x, z, x+1, z+1)`; `RegionAuthoringEncoder` writes a block as
  `max = min + 1`. The degenerate-box comment in `RegionGeometry2d` documents the cell-centre
  convention.

### Wool `<location>` is floored; monument `<block>` is not
The intent generator floors the wool location but passes monument block coords through raw —
PGM itself floors `BlockRegion` but keeps wool locations as raw vectors. Already documented in
`CLAUDE.md` ("Wool-location flooring asymmetry is intentional") and
`docs/pgm/new-map-authoring.md` §4; kept here as a pointer because it keeps resurfacing.

## Authoring intent model

### Orbits are materialized at authoring time — the stored intent is already per-team complete
The Configure wizard *stores* the full orbit: `SpawnStep.PlaceAndOrbit` orbit-fills spawns for
every team, `ProtectionStep` writes every team's rects, and the wool sub-steps write one wool
per owner. `SymmetryExpander.Expand` at export time is a **fill-in for missing entries only**
(it seeds a `have` set from what's authored and skips existing teams), so for wizard-authored
intents it is a no-op.

- *Looks wrong:* a consumer that iterates only `intent.Spawns`/`intent.Wools` (e.g.
  `SketchWorldBuilder`) appears to miss the mirrored teams that `SymmetryExpander` "will add
  later" — but those teams are already present in the stored intent, so world and XML agree.
- *Enforced:* `SpawnStep.razor.cs` (`PlaceAndOrbit` + `WriteIntent`),
  `SymmetryExpander.FillSpawns`/`FillWools` dedup guards, and the orbit note in `CLAUDE.md`
  ("Spawn/Protection still compute orbit in C# via `OrbitAssignment` because they *store* it").

### At most one `SpawnIntent` per team
Every producer of `intent.Spawns` dedupes by team: `SpawnStep.PlaceAndOrbit` guards with
`spawns.All(s => s.Team != tk)`, `SymmetryExpander.FillSpawns` skips teams already in its
`have` set, and `LaneMapGenerator` emits exactly one spawn per team slot.

- *Looks wrong:* code keyed on team id alone (e.g. `SketchWorldBuilder`'s
  `monLoc[(woolIndex, team)]` indexer) appears to lose data if a team had two spawns — the
  overwrite is real but the state is unreachable.
- *Enforced:* the three producers above; no other code path appends to `intent.Spawns`.

## Analysis / export pipeline

### The traversability gate is geometry-based; post-gate coordinate snapping can't flip it
The export gate (`Traversability`) derives navigation points from **region centres** and the
rasterized column map, not from the spawn/monument point coordinates that
`IntentGenerator.Apply` later rewrites. `PositionSnap.SnapXZ` moves a point ≤ 0.5 block, and the
gate's point→component resolver (`LabelAt`) already searches a radius-3 neighbourhood.

- *Looks wrong:* "gate checks the doc, then `Apply` mutates it" reads as a classic
  check-then-mutate race — but the mutated fields are not the fields the gate certified, and
  sub-block movement is far inside the resolver's slack.
- *Enforced:* `Analysis/Playability/Traversability.cs` (`NavigationPoints`, `LabelAt`),
  `Minecraft/Stamping/PositionSnap.cs`.

### A destroyable/core gates traversability the way a wool does
`Traversability.NavigationPoints` reads destroyable and core region centres alongside spawns and
wools, and every goal kind gates `Connected`/`Isolated`: an isolated destroyable refuses the export
with the same `EX1` an isolated wool does.

- *History:* the gate was spawn/wool-only for one release, held open deliberately because whether a
  destroy goal *should* be required reachable the way a wool is was a gameplay question the corpus and
  the code could not answer. The author answered it (2026-08-16): an unreachable goal is a match nobody
  can finish, so it refuses — a hard 409, not a warning.
- *What is judged:* not the goal's own column — a destroyable and a core float a few blocks above the
  terrain by design — but the nearest navigable ground around its centre (the snap radius in `LabelAt`),
  which is the approach ground an attacker actually needs.
- *Enforced:* `Analysis/Playability/Traversability.cs` (`NavigationPoints` reads all four kinds;
  `Check`'s `gating` list is spawn/wool/destroyable/core).

### Protection regions gate traversability per team
One navigability map cannot see the one way a small floating goal genuinely becomes unreachable: an
`enter` rule barring the attacking team from the ground its approach crosses — a goal tucked behind an
oversized spawn protection (the author's ruling; the same conversation that settled goal gating). So
where a map's apply rules provably deny a team entry somewhere, `Traversability.Check` walks that team's
own navigable set — the shared one minus its denied cells — from its spawns to every goal it does not
own, and an unreached goal refuses with the team named (`IsolatedPoint.For`).

- *Two denials every properly wired map carries are not faults:* a wool room barring its own defender
  (`enter=not-<owner>`) — the defender is never required to reach its own wool — and a spawn protection
  admitting only its own team, which cannot cut off the team it admits.
- *The filter reader is deliberately permissive:* a team filter answers by its team, the boolean wrappers
  compose, and anything unresolvable answers "allowed" — an exotic wiring can only under-refuse, never
  invent a barred region that is not there.
- *Enforced:* `Traversability.TeamIsolations` + `AllowsTeam`; the entry-denial rasterization is
  `Buildability.RegionMask`, the same one the block rules read, so two rule readers cannot disagree about
  which cells a region covers.

### A water lane is not a route; an open build zone over void is
Before the lane timer fills it, a water lane is a void a player falls into — the water arrives
(y0→y1) at the 45-minute mark, and a map that needs the lane to reach an objective would be
unplayable for the whole match a public server actually runs. The navigability map already says
this without a special case: a lane's columns have no Y=0 ground and its `deny(void)` rule makes
them `void_denied`, which is not navigable, so a lane-only route reads disconnected and refuses.
The same gap with no rule over it reads navigable on purpose — a bare build zone over void is
*meant* to be crossed, block by placed block.

- *Looks wrong:* an unwired map (no `apply_rules`) reads its void gaps as navigable and passes.
  That is truthful, not lax: without a deny rule PGM lets players bridge the gap, so the map as
  wired genuinely connects. The wiring, not the gate, is what such a map is missing.
- *The author's ruling (2026-08-16):* a lane counted traversable pre-timer would ship a map that
  is never loaded on a public server; lane-only routes must refuse.
- *Enforced:* `Buildability.Compute` (`void` rules over the Y=0 mask), `Traversability.Check`
  (navigable = buildable | restricted, never `void_denied`), pinned by
  `TraversabilityTests.A_water_lane_under_a_void_deny_is_not_a_route_but_an_open_build_zone_over_void_is`.

### Intended-gameplay walls and climbs are not traversability faults
A bedrock wall, a climb-up, a drop into a pit around a goal — obstacles a defender is meant to
hold and an attacker is meant to break or build past — are PGM's nature: block place and block
break are the game's verbs, so ground that costs blocks to cross is still ground. The
traversability gate therefore judges *connectivity of navigable ground*, never route comfort: it
refuses only what genuinely blocks — a protection region in front of a wool, a goal off the
navigable grid, a lane-only route — and no score or refusal penalizes an intended wall as a
"climb". A ten-block drop or climb as the only way to a goal is bad *relief design*, which is the
relief tooling's concern to surface, not a playability failure to refuse.

- *The author's ruling (2026-08-16):* wool rooms and spawns gate because their protection regions
  genuinely bar a team; monuments and cores are small floating objectives incapable of making
  ground non-traversable, so gating the walk *toward* them is right and gating anything more
  would refuse intended maps.
- *Enforced:* by absence, deliberately — nothing in `Analysis/Playability` measures climb cost,
  and nothing should without a new ruling.

## Sketch world synthesis (P9)

### Spawn-cube monuments fill the back wall first; the door wall is unreachable at real wool counts
`SpawnStructureStamper.Placements` iterates the back wall (`backNear`) before the door wall, six
cells per wall. The wizard clamps wools to at most 6 (`SketchEndpoints`), and a team captures at
most the wools it doesn't own, so monuments 1–6 all land on the back wall; a door-wall cell in
front of the opening would first be used by the **8th** monument.

- *Looks wrong:* the placement sequence appears to overflow onto the door wall and block the
  cube's only exit for "5+ wools".
- *Enforced:* the `[backNear, doorNear]` loop order in `SpawnStructureStamper.Placements` + the
  wool-count clamp in `SketchEndpoints`.

### The export temp world directory is always cleaned up
`MapExportEndpoint.BuildWorldZip` deletes its temp directory in a `finally`, so writer failures
don't leak the tree. (Failures do currently escape the structured-error path — that part is a
real finding — but the leak claim is not.)

## Tests & fixtures

### "Synthetic fixtures only" permits committed seed JSON; "corpus harnesses" means the real map corpus
The `CLAUDE.md` Tests rule ("Synthetic fixtures only; corpus/round-trip harnesses live under
`tools/`, not `tests/`") draws the line at the **350-map real corpus**: sweeps over it belong in
`tools/` (`PgmStudio.RoundTrip`). Small hand-authored committed fixtures (e.g.
`tools/seeds/*.json`) driven by an integration test under `tests/` are the *synthetic* side of
that line, and integration tests without a 1:1 source unit have precedent
(`PgmStudio.Data.Tests/SchemaRoundTripTests`).

- *Looks wrong:* a `tests/` class with "round trip" in its name and no matching source unit
  pattern-matches the rule's ban.
- *Enforced:* rule text in `CLAUDE.md` (## Tests); precedent in
  `tests/PgmStudio.Data.Tests/SchemaRoundTripTests.cs`.
