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
`docs/contracts/new-map-authoring.md` §4; kept here as a pointer because it keeps resurfacing.

## Authoring intent model

### Orbits are materialized at authoring time — the stored intent is already per-team complete
The Configure wizard *stores* the full orbit: `SpawnPhase.PlaceAndOrbit` orbit-fills spawns for
every team, `ProtectionPhase` writes every team's rects, and the wool sub-steps write one wool
per owner. `SymmetryExpander.Expand` at export time is a **fill-in for missing entries only**
(it seeds a `have` set from what's authored and skips existing teams), so for wizard-authored
intents it is a no-op.

- *Looks wrong:* a consumer that iterates only `intent.Spawns`/`intent.Wools` (e.g.
  `SketchWorldBuilder`) appears to miss the mirrored teams that `SymmetryExpander` "will add
  later" — but those teams are already present in the stored intent, so world and XML agree.
- *Enforced:* `SpawnPhase.razor.cs` (`PlaceAndOrbit` + `WriteIntent`),
  `SymmetryExpander.FillSpawns`/`FillWools` dedup guards, and the orbit note in `CLAUDE.md`
  ("Spawn/Protection still compute orbit in C# via `OrbitAssignment` because they *store* it").

### At most one `SpawnIntent` per team
Every producer of `intent.Spawns` dedupes by team: `SpawnPhase.PlaceAndOrbit` guards with
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
  `Minecraft/PositionSnap.cs`.

## Sketch world synthesis (P9)

### Spawn-cube monuments fill the back wall first; the door wall is unreachable at real wool counts
`SpawnCubeStamper.Placements` iterates the back wall (`backNear`) before the door wall, six
cells per wall. The wizard clamps wools to at most 6 (`SketchEndpoints`), and a team captures at
most the wools it doesn't own, so monuments 1–6 all land on the back wall; a door-wall cell in
front of the opening would first be used by the **8th** monument.

- *Looks wrong:* the placement sequence appears to overflow onto the door wall and block the
  cube's only exit for "5+ wools".
- *Enforced:* the `[backNear, doorNear]` loop order in `SpawnCubeStamper.Placements` + the
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
