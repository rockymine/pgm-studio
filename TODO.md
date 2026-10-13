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

## Conquest maps: a flag to hold, a post a player can walk to, and no block to place
**Conquest is played for control, not completion, and by the author's ruling it has block interaction off.**
TDM, KotH, the flag modes, rage, blitz and payload are about holding ground in PvP; where a board gives blocks
at all, they are a few kit materials players may place and break for cover, and nothing else. King of the Flag
is the first flag mode the studio authors, and CTF's nets follow it. Every flag post must be walkable to, and
the walk now climbs, swims and jumps the way a player does. pgmvox (`pgm-studio-mapgen/freeform/lib/pgmvox`)
is the reference for the rest: `play/objectives.py` `Flag` and `play/rules.py` `Build`.

### The flag

- [ ] **PG20 — The parser reads `<flags>`.** `MapParser` refuses every map carrying it (`MapParser.cs:60`,
  `ParsedObjectiveModules`): 157 of the corpus's 1,626 `map.xml`. Read `<flag>`, the single `<post>` and the
  composite one (leaf points, nested posts inheriting their parent's attributes, `fallback`, `sequential`,
  `respawn-filter`) and `<net>` (top-level and nested, `flag`/`flags`, `post`, `rescue`) into `MapModel`, write
  them back, and add `flags` to `ParsedObjectiveModules`. PGM tags a flag map `ctf` only
  (`FlagModule.java:28`); KotF is a stated `<gamemode>kotf</gamemode>`, which 29 of the 38 corpus `kotf/` maps
  omit. `docs/pgm/flags.md` (new) and `docs/pgm/supported-maps.md`.
- [ ] **PG21 — A King of the Flag intent.** `MapIntent.Flags`: name, colour, and posts (point, name, yaw). It
  is neutral, so it fans by position like `ControlPointIntent`. `FlagGenerator` writes one flag over one
  composite `<post>` with respawn random (`sequential` false), `points-rate` 1 (author; 47 of 55 corpus KotF
  flags), the `<score>` limit KotH writes, and `<gamemode>kotf</gamemode>` through `Gamemodes.From`.
  `BannerBox` is filled by the export. `docs/pgm/flags.md`, `docs/tools/configure.md`.
- [ ] **PG25 — The carrier's pickup kit is one of three.** A flag states a `Carrier` word: `lowered` (default)
  writes `<max-health>` at a stated number of hearts, default 6 (12 health), with a drop kit restoring 20;
  `absorption` writes absorption amplifier 2 for 3 seconds; `boost` writes health boost at a stated level.
  Corpus KotF pickup kits, per map: health boost −3 (12 health) in 6, absorption 2 for 3s in 5, health boost
  −2 (16) in 2, `<max-health>14` in 1; none raises health. PGM resets max health on respawn
  (`Alive.java:110`). `docs/pgm/flags.md`.
- [ ] **WS83 — Banners are gathered at ingest.** A standing (176) or wall (177) banner is read in the single
  world pass beside chests and mob spawners (`FeatureExtractors.cs:84`, `:113`): position, base colour,
  patterns and facing, into `flag_candidate` rows (migration), served by `GET /map/{slug}/flag-suggestions`.
  The world is discarded after import, so a banner not read then is gone (`objective-suggestion.md` §4).
  `docs/world-scan/objective-suggestion.md`.
- [ ] **WE163 — A plan-built flag post stamps its banner.** PGM refuses to load a flag with no banner at its
  starting post (`Flag.java:121`, `ModuleLoadException`) and copies the banner's colour, patterns and facing
  into the carried flag. `FlagStamper` places a standing banner in the flag's colour facing the post's yaw,
  through a banner tile entity beside `SignBuilder`, with solid ground under every post, and fills
  `BannerBox`. pgmvox's `Flag` stamps nothing, so its flag boards need a hand-placed banner.
  `docs/world-export/structures.md`.
- [ ] **TC15 — Configure's Flags phase.** A scanned banner is confirmed as the flag's starting post and its
  colour; further posts are placed on the ground. A post is refused unless the walk reaches it from every
  spawn with no block placed, and the starting post is refused without a banner. Saving
  writes `MapIntent.Flags`. `docs/tools/configure.md`.

### Blocks off

- [ ] **PG22 — A conquest board refuses block interaction.** `BuildGenerator.Apply` writes no rule when
  `Build.Areas` is empty (`BuildGenerator.cs:73-76`), so a generated KotH board lets anyone place and break
  anything. `BuildIntent` gains a mode word: `void` (today's rule, for CTW, DTM and DTC), `never`
  (`<apply block="never">`, use still allowed) and `only` (named materials placed and broken, nothing else), as
  pgmvox's `Build` writes them (`play/rules.py:212`). It defaults to `never` on a board with no completion
  objective. 39 of the 45 corpus KotF maps write `block="never"`. `docs/pgm/new-map-authoring.md` §5,
  `docs/tools/configure.md`.
- [ ] **PG23 — Cover a conquest kit gives.** An `only` board's materials arrive as a spawn-kit stack and a kill
  reward of the same material (leaves, say), so the only blocks in play are the ones the board hands out.
  `docs/pgm/new-map-authoring.md`.

## The programme: a board's source, every change to it, and the words it is stated in
**The author put this programme first.** The studio keeps the source a board is stated in and every change to
it, hands the author's hand edits to the agent's next round, and hands out the vocabulary the source is
written in. The source, its changes, the kit it is written in, coasts, pulls, basins, outlines and the fan
as statements, and `pgm-studio-mapgen` stated in all of them have shipped; what remains is the point edits
reaching every outline a board states. The layer word (`B264`, `WE28`, `TS64`) waits behind it.

### A point edit and a bend reach every outline

- [ ] **TS130 — Point edits and bends reach every outline.** `editShapes` and `bendShapes` address a shape's
  `vertices` alone (`SketchGeometryEdit.Outline`), where `outlines` already writes a relief `area` mark's and a
  push's `ring` and a stroke's, fluid's or flora's `points` (`Refinement.Outlined`). Both reach those three,
  each refused where an edit folds the ring, so a mark or a prop takes a pull or a bend instead of stated
  points. `docs/tools/flow.md`.

  *Evidence: 51 of the 79 seeded outlines on the 5b and 5c boards are relief rings or prop points, and so is
  Gypsum Reach's wash, a push whose ring splices six authored points into a lobed ellipse.*
