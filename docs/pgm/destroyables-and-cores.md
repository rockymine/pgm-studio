# Destroyables and cores — the DTM/DTC objectives

What PGM requires of its other two objective types — the **destroyable** (gamemode DTM) and the **core**
(DTC) — what the corpus actually builds for them, and the rules the studio holds itself to as a result. It is
the law and the measurements; the tools that author one are elsewhere.

Read alongside:
- `../tools/plan.md` — the plan schema. A destroyable and a core are two of its placement kinds, with their
  fields, defaults and the orbit-order gate stated there.
- `../world-scan/objective-suggestion.md` — proposing both from a world scan, which §5's corpus study is the
  groundwork for.
- `water-lanes.md` — the mechanism a first-generation lane is built from, which is a phantom destroyable (OB16).
- `../generator/rules.md` ST1–ST4 — the stamped-structure law. The structures here are ST-class.
- `filter-region-wiring.md` — the wiring templates. Neither objective needs any of them (§4).

Rule ids here are `OB*` (both objectives), `DT*` (destroyable structures) and `DC*` (core structures), local
to this file the way `structures.md` owns `WX*`.

> **Scope:** the base objective only. Sparks, repairability, progress display, scoreboard filters, proximity
> metrics and `required` are deliberately out (§7).

---

## 1. Naming — read this first

PGM has **no `<monument>` element**. DTM's element is `<destroyables>/<destroyable>`;
`Gamemode.DESTROY_THE_MONUMENT` is produced by `DestroyableModule`. The word "monument" is already taken
throughout this codebase for the *CTW wool-monument* — the block a capturing team places wool on — and it is
load-bearing in `Wool.Monument`, `MonumentRow.wool_id`, `MonumentIntent`, `MonumentObstructionDto`,
`monument_candidate`, and `MonumentSuggester`.

**OB1 — the objective is called a `Destroyable` in code, never a monument.** The core is a `Core`.
Colloquially a destroyable *is* the DTM monument; in types, columns, ids, and JSON keys it is not.

---

## 2. What PGM actually requires

Both objectives are `ProximityGoalDefinition`s owned by one team and destroyed by everyone else. Neither has
the wool's per-capturing-team fan-out: **one object, one owner, N−1 attackers**. This makes them structurally
*simpler* than the wool, which drags a room region, a spawner, a dye item, team filters, apply rules, and the
monument-subtraction-from-spawns coupling.

### The destroyable (DTM)

`<destroyable owner name region materials [completion] [id] [modes|mode-changes]>`. PGM builds
`FiniteBlockRegion.fromWorld(region, world, materials)` — **the set of blocks inside the region that match
`materials`** — and the goal completes when `completion` (default `1.0`) of them are broken. `owner`, `name`,
`region`, and `materials` are required; everything else defaults.

### The core (DTC)

`<core team region [material] [leak] [name] [id] [modes|mode-changes]>`. `material` defaults to obsidian,
`leak` to `5`, and `name` auto-serialises per team (`Core`, `Core 2`, …). The core builds two block sets from
the world inside its region: the **casing** (blocks matching `material`) and the **lava**. The owning attribute
is `team`, not `owner` — a PGM inconsistency with a standing TODO in their source; the studio mirrors the XML
and calls the field `Owner` in code (OB1).

**OB2 — a core leaks when a lava block reaches `Y ≤ region.min.y − leak`, within ±15 blocks horizontally of
the core bounds.** (`CoreMatchModule.leakCheck` tests the lava's XZ against the leak region, then its actual
Y; `Core.java:82-88` builds that region as the core bounds inflated ±15 in XZ, spanning
`y = 0 … region.min.y − leak`.) The `leak/leakRequired` pair on `Core` is the progress readout only — it does
not gate completion.

**OB3 — both objectives only *warn* when the world doesn't back the XML.** An empty casing, missing lava, or
a destroyable whose region contains none of its `materials` logs a warning and yields a degenerate goal
(`maxHealth = 0`), not a load failure. PGM will not catch a mistake here; §8 is what does.

**OB12 — the region is a box *around* the structure, not the structure.** This is the single most important
fact in this document and it is invisible from the XML alone. `materials` is a filter: the goal is the set of
matching blocks *inside* the region, and hand-authored regions are drawn with generous slack around the real
build. Measuring region boxes tells you nothing about the monument.

alpine_mining_ii is the worked example. Its region reads `min="20,43,146" max="23,46,149"` — a 3×3×3 box. The
actual monument inside it is a **1×3×1 obsidian pillar**, three blocks in a single column, surrounded by air:

```
  y=45   ·  49  ·          49 = obsidian, 0 = air, 188 = fence (decoration)
  y=44   ·  49  ·          the region is a loose box; the goal is the 3 obsidian blocks
  y=43   ·  49  ·          maxHealth = 3, not 27
```

Two consequences. **Any survey of these objectives must read the world**, not the XML — §5's figures do. And
**validation must never treat region ⊋ structure as an error** (§8); it is the norm for imported maps.

**OB16 — not every `<destroyable>` is an objective; `show="false"` marks the ones that aren't.** 8% of them
(80 of 951 leaves, across 39 maps) are not goals at all — they are **scripted block-swap regions** that borrow
the destroyable element purely to carry a `<mode>`. The tell is exact and semantic rather than heuristic: **a
goal players cannot see is not a goal.** Of the 80, not one names itself a monument or reads as an objective;
conversely the one *real* objective that targets an air mode — `gold_in_them_thar_kills`, gold block,
`completion="50%"`, crumbling at 20m — declares `show="true"`. Neither `completion="0%"` nor
`required="false"` is sufficient alone (170 destroyables are non-required and most are genuine); `show="false"`
is the discriminator.

They split in two: **70 carry a mode** (the scheduled swap) and **10 do not** (triggers — deathrun_aperture's
ten `lever` destroyables, broken to fire a filter). `Destroyable.Phantom` reports which
(`PhantomKind.None`/`BlockSwap`/`Trigger`).

The instance that matters for CTW is the **pre-game build floor**, and authors name it plainly:

| Map | name | materials | y | mode |
|---|---|---|---|---|
| abstract, abstract_remix | `monu` | stained glass | 0 | `0s → air` |
| newgen_classic | **`build-regions`** | stained glass | 0 | `0s → air` |
| newgen_classic | `water-lane` | air | 0 | `15m → water` |
| vesuvius | — | air; water | 0 | `20m → water` |
| down_side_up | — | wool:10 | 0 | 12-step colour cycle, every 60s |

A slab of stained glass sits at the **world floor** marking the bridge / build regions. It ships in the world
file, so it is visible while the server cycles the map; at match start the mode replaces it with air and the
real build region is defined by a void filter instead. abstract goes as far as giving both "owners" the
*identical* region — proof the ownership is vestigial. The swap target is not always air (water lanes, a wool
disco floor), so the mechanism is **a timed block-swap**, of which "erase at `0s`" is merely the common case.

Three consequences, and they reach further than this document:

1. **It breaks gamemode-by-module-presence (OB7).** PGM tags *any* map whose `DestroyableModule` parsed as
   DTM — `new MapTag("monument", Gamemode.DESTROY_THE_MONUMENT, false)`, unconditional and non-auxiliary.
   **30 of the 297 maps with `<destroyables>` are phantom-only**, so PGM calls them DTM and they are not.
   Module presence gives *PGM's answer*, not the truth. **A module whose every destroyable is `show="false"`
   contributes no gamemode.**
2. **Phantoms are load-bearing — dropping one is worse than losing an objective.** Lose abstract's phantom and
   the glass floor is never erased: the map keeps a solid bridge between the teams and plays wrong, rather
   than merely missing a goal.
3. **It is what island detection would otherwise have to guess.** `LayerExtractors.CleanBaseExclude` excludes
   stained glass (95) as a "build-floor marker removed pre-game via a `destroyables` mode-change" — a material
   heuristic standing in for this exact pattern. The parsed phantom is exact where the heuristic guesses: its
   region states precisely which blocks vanish before play.

The community's own word for these is **fake** — dominion ships the comment
`<!-- TODO: replace fake lanes destroyables with fill actions -->`, and piorun separates its two
`<destroyables>` blocks with `<!-- actual monument -->`, opting the real one out via `mode-changes="false"` so
the swap mode cannot touch it. That TODO points at `water-lanes.md`.

---

## 3. The XML accepted and emitted

**OB4 — attributes inherit from the group element down to the leaf.** PGM wraps every element in an
`InheritingElement` (`XMLUtils.flattenElements`), so a nested group's attributes cascade. Real maps depend on
this heavily:

```xml
<destroyables materials="obsidian" mode-changes="true" repairable="false">
    <destroyables name="Hill Monument">
        <destroyable owner="green"><region><cuboid min="20,43,146" max="23,46,149"/></region></destroyable>
        <destroyable owner="orange"><region><cuboid min="-19,43,-183" max="-22,46,-186"/></region></destroyable>
    </destroyables>
</destroyables>
```

The same flattening serves wools, destroyables and cores, and it inherits **every** attribute rather than only
the team.

**OB5 — the writer emits the flat canonical form**: one `<destroyables>`/`<cores>` block, every leaf carrying
its own explicit attributes, no nested groups. This follows the standing generator rule — emit a simpler
canonical structure than a human wrote, as long as it parses and plays. Round-trips are semantic, not textual.

**OB6 — the proto floor is a gift; PGM's legacy branches are not ported.** All 150 corpus maps using these
elements declare `proto="1.5.0"`, and every legacy path sits below the studio's 1.4.0 floor: `CoreModule`'s
`MODULE_SUBELEMENT_VERSION` sub-element form (1.3.6), `ObjectiveModesModule`'s `MODES_IMPLEMENTATION_VERSION`
bail (1.3.2), and `DestroyableModule`'s `.legacy()` region form. The `region` property still has two spellings
that must both be accepted — the `region="id"` attribute and the `<region>` child — because both are common at
1.5.0.

This includes the **block-extend fix**: `FiniteBlockRegion.fromWorld` inflates a cuboid's max by `(1,1,1)` to
undo an old bug where "legacy maps have cuboids that are one block too big", but it is gated on
`proto.isOlderThan(REGION_FIX_VERSION)` (1.3.1). At 1.5.0 it never fires.

**OB13 — a cuboid's block count is `max − min`, not `max − min + 1`.** From `Bounds`, the block span is
`[roundDown(min), round(max) − 1]` inclusive, so `getBlockSize()` is `round(max) − roundDown(min)`; for integer
coordinates that is simply `max − min`. A cuboid `min="20,43,146" max="23,46,149"` therefore spans blocks
x∈{20,21,22}, y∈{43,44,45}, z∈{146,147,148} — **3×3×3, not 4×4×4**. Off-by-one here silently inflates every
measurement by one block per axis. A `<block>x,y,z</block>` region is a single block, and is the most common
destroyable region in the corpus (26%) — region handling must cover `block`, not just `cuboid`.

**OB7 — the objective module *is* the gamemode; `<gamemode>` is a label, not the truth.** This is not a
DTM/DTC quirk — it is exactly as true for CTW, where what makes a map CTW is the presence of `<wools>`, not
the text in `<gamemode>`. PGM never reads that element to decide: each module contributes a `MapTag` when it
parses anything, and the gamemode falls out of which modules produced one.

| Module present | Gamemode |
|---|---|
| `<wools>` | CTW |
| `<destroyables>` with ≥1 real objective (OB16) | DTM (`Gamemode.DESTROY_THE_MONUMENT`, tag `monument`) |
| `<cores>` | DTC (`Gamemode.DESTROY_THE_CORE`, tag `core`) |

The element is demonstrably unreliable: of the 150 maps carrying these objectives, **68 declare no
`<gamemode>` at all**, and 9 declare `ctw` while carrying destroyables. So the author's word is kept as
`DeclaredGamemode` — sometimes it says something the modules cannot — and the answer is derived:
`Gamemodes.From(...)` reads the modules, and `MapXml.Gamemodes` is what anything downstream asks.

**The derivation deviates from PGM in one place**, the `≥1 real objective` qualifier above. PGM tags a map DTM
the moment `DestroyableModule` parses anything, and **30 of the 297 maps with `<destroyables>` carry nothing
but phantoms** (OB16). Those maps are not DTM, whatever PGM's tag says. This is not a rare correction: **8 of
the 10 maps in the `ctw/` corpus that carry destroyables are phantom-only** (abstract, abstract_remix,
citadel, down_side_up, fairy_tales_metamorphose, mine_your_own_business, newgen_classic, vesuvius) — pure CTW
maps, every one. Only `sentient` (8 real destroyables) and `bungee_coorde` (a core) are genuine.

**OB15 — CTW, DTM, and DTC coexist in the same map**, which is why the gamemode is a set and not a scalar.
This is not a curiosity to guard against; it is 10% of the objective corpus (72 of 742 maps across both
corpora), and both corpora keep a `mixed/` directory for it:

| Modules | Maps | |
|---|---|---|
| CTW | 353 (47%) | |
| DTM | 228 (30%) | |
| DTC | 89 (11%) | |
| DTM + DTC | 46 (6%) | e.g. dynamite, autumn_solstice |
| CTW + DTM | 21 (2%) | e.g. **ender_blast**, chimeric, vesuvius |
| CTW + DTC | 3 | e.g. **hot_dive**, the_4th_law |
| CTW + DTM + DTC | 2 | cacti_the_wool, the_fenland_epic_style |

Architecturally it costs nothing — `wool`, `destroyable` and `core` all hang off `map_id`, and each
objective's validity is independent (`MapValidity`'s "every wool needs a monument" is a wool rule, not a map
rule). The requirement is purely negative: **nothing may assume a map has exactly one objective type** — not
the parser, not the schema, not the UI, not the gamemode field. A wool room and a core can sit in the same
map, and 26 real maps prove it.

**A module the studio does not parse is refused, never dropped.** `MapParser` reads the tags it names rather
than enumerating the root, so an unparsed objective module would load "successfully" and lose the map's goal
on round-trip. `EnsureSupported` therefore rejects a map carrying an objective module outside the parsed set —
the modules contributing a non-auxiliary `Gamemode` tag — and that set grows as each parser lands. Silently
eating a goal is worse than refusing the map.

---

## 4. Two teams, and what these objectives do not need

**OB14 — a destroyable and a core are authored at orbit order 2 only.** `rot_180` and `mirror_*` get the
placement kinds; `rot_90` does not, and the plan validator refuses a plan that places one outside order 2
rather than compiling a map nobody has a design position on.

PGM grounds the distinction exactly. Both objectives define `canComplete(team) { return team != getOwner(); }`,
and both compute `isShared = competitors.filter(canComplete).count() != 1`. With N teams, N−1 competitors can
complete, so:

> **`isShared` is precisely `teams != 2`.**

At 2 teams a goal has exactly one attacker: ownership and attack are unambiguous, progress belongs to one
team, and the wool model's "N−1 attackers each with their own monument" never arises. At 4 teams every goal
becomes shared — three teams race the same monument, and PGM stops colouring it by attacker and colours it by
owner instead (`Destroyable.java:514`). Who gets credit, who is eliminated when, and how alliances shake out
are all live design questions with no canonical answer.

**This is a real restriction, not a rare edge.** In `dtcm/` (302 maps): 2 teams 157 (51%), **4 teams 107
(35%)**, 6 teams 19, plus a tail to 12+. Four-team DTM/DTC is a third of the corpus and the studio chooses not
to generate it. **The parser and schema stay N-team** — reading, storing and round-tripping a 4-team DTM map
works, and nothing outside the plan editor's placement palette may assume two teams.

Beyond that, these objectives need **no wiring templates, no spawner, no room region, no dye, no team filters,
no apply rules, and no subtraction from spawn protection**. A destroyable or core is `owner + region +
material(s)` plus a stamped structure — the cheapest objective in PGM, not the most expensive.

---

## 5. The standard structures

Both objectives **float above the terrain**, which is why no carving, void, or negative-space primitive is
needed. The gap below is what lets a core's lava fall, and what players dig through to extend it.

**Every figure in this section is world-measured, not XML-measured** — see OB12 for why that distinction is
the whole ballgame. The method: for each objective, resolve its region, read the actual `.mca` blocks inside
it, keep only those matching `materials`/`material`, and take *that* bounding box. Corpus is
`/media/sf_repos/CommunityMaps` + `/media/sf_repos/PublicMaps` (n=500 destroyables, n=255 cores with a
resolvable region and a known material).

### DT1 — the destroyable structures: the obsidian pillar dominates

**Over half of all destroyables are a 1-wide obsidian pillar, 1–3 blocks tall**, and 58% consist of just 1–3
blocks in total. The cube is real but is the minority form. Pillars are 98% obsidian (279/286); 3³ cubes are
86% emerald or gold (19 + 18 of 43).

| Style | True structure | Material | Corpus |
|---|---|---|---|
| `pillar-1` | 1×1×1 — a single block | obsidian | 134 (26%) |
| `pillar-3` | 1×3×1 | obsidian | 90 (18%) |
| `pillar-2` | 1×2×1 | obsidian | 62 (12%) |
| `cube-3` | 3×3×3, optional bedrock centre (DT2) | emerald / gold | 43 (8%) |
| `cube-4` | 4×4×4 | ender stone / emerald | 12 (2%) |
| `column-plus` | 3×3 plus-section column, 3 tall (DT4) | ender stone | see DT4 |

The one-block `pillar-1` is not a degenerate case to guard against — it is **the single most common
destroyable in the corpus** (riverbank's monuments are literally `<block>-4,9,30</block>`). The pillar family
is the default; `cube-3` and `column-plus` are the alternatives. Bespoke sculpture above 4³ (DT4) is not
reproduced.

The material vocabulary is also what a team's kit is paired to: an iron pickaxe breaks obsidian, it just does
not drop it, so a destroy map's kit still upgrades its pickaxe to match the goal's material — obsidian to
diamond, anything softer to iron (`MiningTiers`, `DestroyKitPairing.RequiredPickaxe`) — because that pairing
is the corpus norm and a faster raid is a better one, not because a mismatch would make the map unwinnable.
`docs/tools/mapgen-review.md` MG18 records the earlier, mistaken belief that it would, and `B134` is the
correction.

### DT4 — the ender stone column

Ender stone is the third material family and it behaves unlike the other two: it marks the **large,
sculptural monument** — a column, obelisk, or statue rather than a compact solid. Every ender stone
destroyable in `dtcm/`:

| Shape | Blocks | Fill | `completion` | Map |
|---|---|---|---|---|
| 9×18×9 | 1008 | 69% | 80% | autumn_solstice (`North`/`South Column`) |
| 5×39×5 | 819 | 84% | 90–100% | boombox / boomboxxxx |
| 17×8×17 | 948 | 41% | 99% | rock_the_casbah |
| 14×14×14 | 2744 | 100% | 80% | blocks_destroy_the_dynamite |
| 7×7×7 | 120 | 35% | 100% | cobalt_planet, ruby_planet |
| 3×7×3 | 31 | 49% | 100% | dangerous_cargo, wallop_9000 |
| 4×4×4 | 16 | 25% | 100% | fractal_descent |
| **3×3×3** | **15** | **56%** | 100% | **dynamite** |

Two signatures fall out. **Fill is low** — 15–69% for all but one — because these are hollow, decorative
forms, not filled boxes. And **the large ones carry partial `completion`** (80–90%): they are too big to break
exhaustively, so authors let them fall early. This is the one place where the otherwise-marginal `completion`
attribute (§7) earns its keep.

**DT5 — a huge destroyable is a TNT tell.** Nobody mines 1000 blocks of ender stone by hand; these monuments
are destroyed with **TNT and cannons**, and the size *is* the signal. Every map in `dtcm/` whose largest
destroyable exceeds 200 blocks arms players with TNT — **7 of 7, no exceptions** — against a 20–33% baseline
everywhere else:

| Largest destroyable | TNT | no TNT | % TNT |
|---|---|---|---|
| 1–3 blocks | 16 | 61 | 20% |
| 4–30 | 16 | 32 | 33% |
| 31–200 | 4 | 8 | 33% |
| **> 200 blocks** | **7** | **0** | **100%** |

The maps name themselves: `blocks_destroy_the_dynamite`, `boombox`, `boomboxxxx`, `blast_mining`,
`blast_mining_ii`, `rock_the_casbah`. So the obelisk is not a decorative choice — it is **a monument sized for
cannon fire**, and it presupposes a TNT kit, block-drops, and the open sightlines a cannon needs. That is a
whole map archetype, not a stamp.

Which is why the generator stamps the family's **small end** and stops: dynamite's 3×3 column with a
plus/cross section, 5 blocks per layer, height parameterised (default 3 → 15 blocks). It reads as an obelisk
at plan scale and is breakable by hand.

```
   ·  E  ·        E = ender stone; one layer of `column-plus`, repeated `height` times.
   E  E  E        The corners are left open — that hollow cross is the family's signature,
   ·  E  ·        and what separates it from `cube-3`. dynamite fences the corners for looks.
```

A true towering obelisk stays out of the generator: emitting one without the TNT economy around it would
produce a map that is technically valid and unplayable — a monument no one can break. If the studio ever grows
a TNT/cannon archetype, DT5 is the rule that says the obelisk comes *with* it.

### DT2 — the bedrock core is inert by construction

The cubes take an optional concentric bedrock centre (1×1×1 inside `cube-3`, 2×2×2 inside `cube-4`) so players
cannot hollow one out and hide inside. It costs nothing to model: `materials` names only the emerald/gold, so
**the bedrock is invisible to the goal** — neither counted in `maxHealth` nor breakable. The corpus confirms it
is real and common: a 26-block `cube-3` (27 − 1) is the modal non-pillar block count.

```
   y=B+2   E E E
   y=B+1   E ▓ E        ← 1×1×1 bedrock centre; not in `materials`, so not part of the goal
   y=B     E E E
```

### DT3 — float

The **generator** floats a destroyable 3–5 blocks; default `float = 4`, and a `pillar-1` floating alone is a
normal output, not an error.

**The corpus does not do this**, and the difference is worth stating because a detector that expects a gap will
miss most real structures. Measured on the declared blocks themselves (not the cluster containing them), across
571 single-material structures: the median air gap beneath is **0**, and **424 of 571 rest directly on
something**. Only 68 float 3–5. The floating pillar is the authored ideal; the corpus overwhelmingly seats its
destroyables on terrain, a pedestal or a build.

**The studio's `float` counts from the ground as built, not from a plan-nominal surface (`B128`).** A marker's
plan-side `piece` (when it has one) states an `x`/`z` and, for spawns and wools, a Y — but a destroyable or a
core is never given a Y at all: the structure floats `float` blocks over whatever the relief actually left
under its column, resolved by the world-export path once the layout is rasterized and the relief solved, which
is the only place that ground is known. `float` is an offset, not an authored world Y, so it survives a relief
pass moving the ground under it — a goal placed "on the mesa" stays on the mesa however the mesa's own height
is later solved. Because the plan compiler resolves nothing about height for these two markers, a destroyable
or a core is also the one marker kind that may name no plan piece at all: its `at` reads as an absolute board
position, and the ground it rides can be an authored sketch shape with no piece behind it — a landform is then
authored once, as the shape it is, rather than twice: once as a plan rectangle purely to give a marker
something to ride, and again as the polygon it actually is.

### DC1 — the core structure (default 5×5×5, shell 1, lava 3×3×3)

The dominant real core casing is **5×5×5 obsidian** (57/255 = 22%; next 7×7×7 at 12%, 4×4×4 at 7%), the shell
is **1 block thick** (165/255 = 65%; 2 thick in 33%), and the lava interior is correspondingly **3×3×3** (the
modal lava volume, 46). Obsidian is effectively universal.

**The top is capped, not open.** 65% of cores enclose the lava fully (its top layer sits 1 below the casing
rim), 24% cap it 2 below, and only 11% expose it flush with the rim. The open-top variant is real but is a
minority style, so it is a **flag, not the default**:

```
              ← 5 →                        openTop = false (default, 65%)
  y=B+4   O O O O O     ← obsidian cap
  y=B+3   O L L L O
  y=B+2   O L L L O     ← 3×3×3 lava, fully enclosed
  y=B+1   O L L L O
  y=B     O O O O O     ← floor;  region.min.y = B
          ·  ·  ·       ← air gap, `float` blocks (DC2)
  ────────▓▓▓▓▓▓▓▓──    ← terrain surface
```

With `openTop = true` the cap layer is omitted and the lava rises to `y = B+4`, flush with the rim.

### DC2 — float and leak are one knob

`leak = 5` is the mode (104/255) and also PGM's default; 3 and 4 are close behind (62, 64). Measured float is
bimodal: **27% of cores rest directly on a solid floor** (no gap at all — the lava must spread or players
breach the floor), and the rest cluster at 2–7 blocks of air.

With the core floating `F` blocks above the surface and leak level `L`, escaping lava free-falls to `y = B − F`
(it lands on terrain). By OB2 the core leaks at `y ≤ B − L`. Therefore:

> **players must dig `max(0, L − F)` blocks into the terrain below the core.**

`L ≤ F` leaks on its own the moment the casing is breached; `L > F` makes digging part of the capture. Both are
legitimate and both occur; the author picks. **Defaults: `float = 6`, `leak = 5`** — no dig, matching the
corpus centre. The two must be authored together, because neither means anything alone.

### OB8 — one box function, two consumers

A generated structure and its emitted `<region>` must agree, or PGM silently produces a zero-health goal
(OB3). **The bounding box is computed once and shared** by the stamper and the region generator — the shape
`StructureStamper.IronCubeFootprint` established. Never let the two derive it independently. For *generated*
maps the region is emitted as the exact structure bounding box; the slack seen in hand-authored maps (OB12) is
an artifact the generator does not reproduce, per the standing rule that it may emit a simpler canonical
structure than a human wrote.

---

## 6. Modes

77 of the 150 corpus maps declare `<modes>`. Modes are almost entirely declarative — they change an objective's
material at a match time — so there is no world or structure impact, and the work is parse, store, write, and a
feature-id registry alongside regions and filters so `modes="a b"` resolves.

```xml
<modes>
    <mode after="25m" material="beacon" name="`bBEACON MONUMENT MODE"/>
    <mode after="45m" material="coal block" name="`8COAL MONUMENT MODE"/>
</modes>
```

**OB9 — mode membership is a tri-state, not a list.** `modes="a b"` is a specific set; `mode-changes="true"`
means *all* modes (PGM models this as a null set, not an enumerated one); neither attribute means *no* modes.
Combining both is an error PGM raises and so does the studio. It persists as a `mode_changes` boolean plus a
nullable id list, so the XML round-trips exactly.

Ids are optional in the XML and auto-generated by PGM when absent (252 of 333 `<mode>` elements declare one).
The studio generates on parse so the reference is always resolvable.

---

## 7. Deliberately unsupported

The corpus long tail is single-digit and none of it affects geometry, validity, or generation: `sparks` (7),
`show-progress` (12), `show-sidebar` (6), `show-effects` (6), `required` (6), `repairable`,
`scoreboard-filter` (84 — display only), and the shared `ProximityMetric`/`ShowOptions` surface.
`ShowOptions`/`ProximityMetric` are already dropped on wools, so dropping them here is consistent with the
existing contract rather than new debt.

**`completion` is the exception — it is parsed, stored and written**, defaulting to `1.0`. It is semantically
load-bearing (it changes when the goal completes) and far more common than a raw grep suggests. It is also a
worked example of why OB4 is not a footnote: in `dtcm/`, only **19** of 717 destroyable leaves declare
`completion` on the leaf, but **141 have one after group inheritance** — a 7× undercount — of which 113 are
genuinely below 100%. Counting attributes without applying OB4 gets the wrong answer by an order of magnitude.

The modal values are 90%, 75% and 80%, concentrated on DT4's large sculptures. It is not exposed in the plan
editor: every stamped style completes at 100%.

**Parse gotcha — the value is always a percentage, sign or not.** `parsePercent` strips any `%` and divides by
100, so `completion="90"` and `completion="90%"` are identical (both 0.9), and `completion="0.8"` means
**0.8%**, not 80%. Both spellings occur in the corpus. The parsed fraction is stored, and re-emitted with the
`%` so the intent is unambiguous on the way out.

---

## 8. What has to be validated

PGM only warns (OB3), so the export gate is where a broken objective is caught.

**OB11 — assert what PGM lets pass.** Each destroyable's region must contain at least one block matching its
`materials`, and each core's region both casing blocks and lava. Because the structure and the region come
from one box (OB8), this is unfalsifiable for authored maps — it is a guard against the generator drifting,
and a real check for imported ones. The corpus sweep found 10 destroyables that already fail it (a region with
none of its declared material), so it catches real breakage.

**The check is "at least one matching block", never "the region is full".** By OB12 a region legitimately
contains mostly air — a 3×3×3 region holding a 1×3×1 pillar is correct and common. Anything stricter would
reject most of the corpus.

**OB17 — a goal may stand almost anywhere, and there are exactly three places it may not.** A destroyable and
a core are unlike a wool in how freely they sit: no room, no per-team monument, nothing that binds them to a
particular piece — and, since `B128`, not even a piece at all, for a marker placed by absolute board position.
What bounds them is where the map's own rules would make them unbreakable, and all three cases are decided by
the structure's **footprint** rather than by its marker — which is why a marker legally inside its piece can
still be wrong, and why the check needs the same footprint the stamper builds (`ObjectiveFootprint`, below both
the plan layer and the stamper for exactly that reason).

An absolutely-placed goal has no plan-level footprint to judge in the first place — the plan carries no ground
truth for it, only the sketch does, and the sketch is the one document the compiler never reads. The
compile-time gate below is silent about it (`PlanValidator.Footprint` answers null, the same answer it gives a
dangling piece reference, but for the opposite reason: there is deliberately no ground to check yet, not a typo
to report); the export-time gate, over the ground the rasterizer actually produced, is where such a goal is
checked for real, and it refuses a void placement exactly as it always has.

*Over the void.* The build slice applies `block_place=deny(void)` to the complement of the build areas, so
blocks hanging off the land cannot be broken and the objective can never be completed. A one-block pillar at
the very edge of an island is fine; the 4×4 cube centred on the same block is not.

*Inside a spawn.* Spawn protection emits `block="never"` over the shared `spawns` union — not "enemies may not
break" but **nobody** may, the attacking team included. A goal there is a map that cannot be won, and nothing
downstream reports it: PGM loads the map and the round simply never ends. The wool path avoids this by
construction (`WoolGenerator` folds each monument block out of the union so capturing a wool does not trip the
rule); a destroyable or a core has no such fold and is refused instead, because a goal inside a spawn is a
design error rather than a case to work around.

*Inside a wool room.* The room carries its own enter/block rules for its owner, which a second objective
sharing that ground inherits — and it reads as part of the room besides.

The refusals are **errors, not lint**: the compile gate answers 422 on errors alone, so an agent driving the
endpoint is stopped for every one of the three rather than shipping a map that cannot be won. The test against
the *room frame* rather than the piece holding it matters — a spawn piece is often far larger than the room
stamped on it, and refusing a goal at its far corner would be a refusal with no cause.

**The rule is stated once and asked twice.** `ObjectivePlacement` (`Pgm/Plan`) holds it, and takes the two
things a caller has to supply: what counts as land, and which stamped rooms are out of bounds. `PlanValidator`
supplies the plan's pieces and the frames the compiler will stamp, which catches the fault before a build is
spent. The second caller is `MapExportComposer`, over the ground the rasterizer actually produced — the only
place a subtract, a relief, or a sketch edited after its compile can be seen, since a plan that passed can
still export a goal standing over a hole somebody carved afterwards, and a map begun in Sketch never reaches
the compile gate at all. Every finding from either caller carries `OB17` as its rule id, and the export gate
answers **409**, matching the compile gate's refusal rather than the round-trip's plain 500.

Each finding names the offending marker by its **id** (`core-1`, `destroyable-2`) ahead of the piece it stands
on, and carries both as subjects. A refusal that named only the piece is ambiguous the moment two goals share
one — and the id is what makes the answer actionable to a caller that must then move a specific marker, rather
than merely diagnostic. Clicking the finding in the compile drawer closes the drawer and rings that marker on
the board, so the refusal reads as a place rather than a sentence.

**OB18 was retired — a kit/material mismatch is not a refusal.** `MG18`/`B81` asserted that an iron pickaxe
does not mine obsidian at all, so a mismatched kit made a monument unwinnable, and `B116` wired that claim
into the export gate as a hard 409 (`DestroyKitPairing.Unwinnable`, naming every goal it judged unbreakable).
The premise is false: an iron pickaxe **breaks** obsidian, it just does not **drop** it, and a destroy
objective only requires the block gone, so a mismatched kit makes a raid slow, never unwinnable — a design
choice about raid length, not a broken map. `B134` removed the gate and the derivation behind it.
`RequiredPickaxe` stays: the generated kit still upgrades its pickaxe to match the goal's material (obsidian
to diamond, anything softer to iron) because that pairing is the corpus norm and a faster raid is still a
better one — but it is a generation choice now, not a legality check, and the export gate is silent about a
kit an author hand-edits away from it.

A second export-time refusal, `OB19` — a tree, a boulder or a building standing inside a goal's clearance —
is a dressing rule rather than an objective one, and its home is `world-export/decoration.md` §3.1.

---

## 9. Where they are stored

`destroyable` and `core` are their own tables, both hanging off `map_id`, with `mode` beside them. Neither
reuses `monument`, whose `wool_id` is `NOT NULL` with an `ON DELETE CASCADE` to `wool` — a destroyable has no
wool, and that FK makes a wool-less objective unrepresentable.

Neither needs the doc-tree codec bypass the wools use (`WriteWoolsFromDocAsync`/`GroupedWoolsAsync`). That
bypass exists because the flat `MapXml` cannot represent a monument-less wool or wool-level fields; a
destroyable and a core are flat records with no grouped shape to lose, so they travel through `MapXml` like
every other entity.

---

## 10. Water lanes are made of a phantom

Water lanes have their own contract, `water-lanes.md`, which owns the mechanism (PGM's void filter, read live
at `y=0`), the four wirings the corpus authors them in, detection and the authored form. The connection here is
the oldest of those wirings: a first-generation lane *is* a destroyable — `show="false"`, materials `air`,
swapped to water by a mode — so it is a **phantom** (OB16) rather than a goal, and nothing could tell one from
a DTM objective until the phantom classification landed. `Destroyable.Phantom` reporting `BlockSwap` is what
`WaterLaneDetector` reads for that form.
