# Destroyables and cores — the DTM/DTC objectives

How the studio parses, stores, generates, and places PGM's other two objective types: the
**destroyable** (gamemode DTM) and the **core** (gamemode DTC). Today both elements are invisible to
the parser — a DTM map loads "successfully" and silently loses its objectives (§10).

Read alongside:
- `new-map-authoring.md` — the intent model these objectives slot into. Its wool slice is the
  template; §4 there (auto-derivations, coordinate flooring) applies unchanged.
- `plan-editor.md` — the plan schema. Destroyables and cores become two new placement kinds.
- `layout-rules.md` — the stamped-structure law (ST1–ST4). The structures here are ST-class.
- `filter-region-wiring.md` — the wiring templates. Neither objective needs any of them (§5).

> **Scope:** the base objective only. Sparks, repairability, progress display, scoreboard filters,
> proximity metrics, and `required` are deliberately out (§9).

---

## 1. Naming — read this first

PGM has **no `<monument>` element**. DTM's element is `<destroyables>/<destroyable>`;
`Gamemode.DESTROY_THE_MONUMENT` is produced by `DestroyableModule`. The word "monument" is already
taken throughout this codebase for the *CTW wool-monument* — the block a capturing team places wool
on — and it is load-bearing in `Wool.Monument`, `MonumentRow.wool_id`, `MonumentIntent`,
`MonumentObstructionDto`, `monument_candidate`, and `MonumentSuggester`.

**OB1 — the objective is called a `Destroyable` in code, never a monument.** The core is a `Core`.
Colloquially a destroyable *is* the DTM monument; in types, columns, ids, and JSON keys it is not.

---

## 2. What PGM actually requires

Both objectives are `ProximityGoalDefinition`s owned by one team and destroyed by everyone else.
Neither has the wool's per-capturing-team fan-out: **one object, one owner, N−1 attackers**. This
makes them structurally *simpler* than the wool we already ship, which drags a room region, a
spawner, a dye item, team filters, apply rules, and the monument-subtraction-from-spawns coupling.

### The destroyable (DTM)

`<destroyable owner name region materials [completion] [id] [modes|mode-changes]>`. PGM builds
`FiniteBlockRegion.fromWorld(region, world, materials)` — **the set of blocks inside the region that
match `materials`** — and the goal completes when `completion` (default `1.0`) of them are broken.
`owner`, `name`, `region`, and `materials` are required; everything else defaults.

### The core (DTC)

`<core team region [material] [leak] [name] [id] [modes|mode-changes]>`. `material` defaults to
obsidian, `leak` to `5`, and `name` auto-serialises per team (`Core`, `Core 2`, …). The core builds
two block sets from the world inside its region: the **casing** (blocks matching `material`) and the
**lava**. The owning attribute is `team`, not `owner` — a PGM inconsistency with a standing TODO in
their source; we mirror the XML but call the field `Owner` in code (OB1).

**OB2 — a core leaks when a lava block reaches `Y ≤ region.min.y − leak`, within ±15 blocks
horizontally of the core bounds.** (`CoreMatchModule.leakCheck` tests the lava's XZ against the leak
region, then its actual Y; `Core.java:82-88` builds that region as the core bounds inflated ±15 in
XZ, spanning `y = 0 … region.min.y − leak`.) The `leak/leakRequired` pair on `Core` is the progress
readout only — it does not gate completion.

**OB3 — both objectives only *warn* when the world doesn't back the XML.** An empty casing, missing
lava, or a destroyable whose region contains none of its `materials` logs a warning and yields a
degenerate goal (`maxHealth = 0`), not a load failure. PGM will not catch our mistakes here; §10 does.

**OB12 — the region is a box *around* the structure, not the structure.** This is the single most
important fact in this document and it is invisible from the XML alone. `materials` is a filter: the
goal is the set of matching blocks *inside* the region, and hand-authored regions are drawn with
generous slack around the real build. Measuring region boxes tells you nothing about the monument.

alpine_mining_ii is the worked example. Its region reads `min="20,43,146" max="23,46,149"` — a 3×3×3
box. The actual monument inside it is a **1×3×1 obsidian pillar**, three blocks in a single column,
surrounded by air:

```
  y=45   ·  49  ·          49 = obsidian, 0 = air, 188 = fence (decoration)
  y=44   ·  49  ·          the region is a loose box; the goal is the 3 obsidian blocks
  y=43   ·  49  ·          maxHealth = 3, not 27
```

Two consequences. **Any survey of these objectives must read the world**, not the XML — §6's figures
do. And **validation must never treat region ⊋ structure as an error** (§10); it is the norm for
imported maps.

**OB16 — not every `<destroyable>` is an objective; `show="false"` marks the ones that aren't.**
8% of them (80 of 951 leaves, across 39 maps) are not goals at all — they are **scripted block-swap
regions** that borrow the destroyable element purely to carry a `<mode>`. The tell is exact and
semantic rather than heuristic: **a goal players cannot see is not a goal.** Of the 80, not one names
itself a monument or reads as an objective; conversely the one *real* objective that targets an
air mode — `gold_in_them_thar_kills`, gold block, `completion="50%"`, crumbling at 20m — declares
`show="true"`. Neither `completion="0%"` nor `required="false"` is sufficient alone (170 destroyables
are non-required and most are genuine); `show="false"` is the discriminator.

They split in two: **70 carry a mode** (the scheduled swap) and **10 do not** (triggers — deathrun_
aperture's ten `lever` destroyables, broken to fire a filter).

The instance that matters for CTW is the **pre-game build floor**, and authors name it plainly:

| Map | name | materials | y | mode |
|---|---|---|---|---|
| abstract, abstract_remix | `monu` | stained glass | 0 | `0s → air` |
| newgen_classic | **`build-regions`** | stained glass | 0 | `0s → air` |
| newgen_classic | `water-lane` | air | 0 | `15m → water` |
| vesuvius | — | air; water | 0 | `20m → water` |
| down_side_up | — | wool:10 | 0 | 12-step colour cycle, every 60s |

A slab of stained glass sits at the **world floor** marking the bridge / build regions. It ships in
the world file, so it is visible while the server cycles the map; at match start the mode replaces it
with air and the real build region is defined by a void filter instead. abstract goes as far as
giving both "owners" the *identical* region — proof the ownership is vestigial. The swap target is
not always air (water lanes, a wool disco floor), so the mechanism is **a timed block-swap**, of
which "erase at `0s`" is merely the common case.

Four consequences, and they reach further than this document:

1. **It breaks gamemode-by-module-presence (OB7).** PGM tags *any* map whose `DestroyableModule`
   parsed as DTM — `new MapTag("monument", Gamemode.DESTROY_THE_MONUMENT, false)`, unconditional and
   non-auxiliary. **30 of the 297 maps with `<destroyables>` are phantom-only**, so PGM calls them DTM
   and they are not. Module presence gives you *PGM's answer*, not the truth. **A module whose every
   destroyable is `show="false"` contributes no gamemode.**
2. **Phantoms are load-bearing — dropping them is worse than losing an objective** (OB10). Lose
   abstract's phantom and the glass floor is never erased: the map keeps a solid bridge between the
   teams and plays wrong, rather than merely missing a goal.
3. **It is already costing us, today, in island detection.**
   `LayerExtractors.CleanBaseExclude` excludes stained glass (95) with the note that it is a
   "build-floor marker removed pre-game via a `destroyables` mode-change" — a **material heuristic
   standing in for this exact pattern**, guessing "glass as the lowest solid = build floor" because
   the parser cannot see the mode. Parsing the phantom makes it *exact*: its region states precisely
   which blocks vanish before play.
4. **The detector (§13) must not propose them.** A phantom is a marker, not a monument.

The community's own word for these is **fake** — dominion ships the comment
`<!-- TODO: replace fake lanes destroyables with fill actions -->`, and piorun separates its two
`<destroyables>` blocks with `<!-- actual monument -->`, opting the real one out via
`mode-changes="false"` so the swap mode cannot touch it. That TODO points at §14.

---

## 3. The XML we accept and emit

**OB4 — attributes inherit from the group element down to the leaf.** PGM wraps every element in an
`InheritingElement` (`XMLUtils.flattenElements`), so a nested group's attributes cascade. Real maps
depend on this heavily:

```xml
<destroyables materials="obsidian" mode-changes="true" repairable="false">
    <destroyables name="Hill Monument">
        <destroyable owner="green"><region><cuboid min="20,43,146" max="23,46,149"/></region></destroyable>
        <destroyable owner="orange"><region><cuboid min="-19,43,-183" max="-22,46,-186"/></region></destroyable>
    </destroyables>
</destroyables>
```

We already flatten nested `<wools>` in `MapParser.CollectWoolElements`, but it inherits only `team`.
**Generalise it to inherit every attribute**, and share it between wools, destroyables, and cores.

**OB5 — the writer emits the flat canonical form**: one `<destroyables>`/`<cores>` block, every leaf
carrying its own explicit attributes, no nested groups. This is what `WriteWools` already does, and
it follows the standing generator rule — emit a simpler canonical structure than a human wrote, as
long as it parses and plays. Round-trips are semantic, not textual.

**OB6 — the proto floor is a gift; do not port PGM's legacy branches.** All 150 corpus maps using
these elements declare `proto="1.5.0"`, and every legacy path sits below our 1.4.0 floor:
`CoreModule`'s `MODULE_SUBELEMENT_VERSION` sub-element form (1.3.6), `ObjectiveModesModule`'s
`MODES_IMPLEMENTATION_VERSION` bail (1.3.2), and `DestroyableModule`'s `.legacy()` region form. The
`region` property still has two spellings we must accept — the `region="id"` attribute and the
`<region>` child — because both are common at 1.5.0.

This includes the **block-extend fix**: `FiniteBlockRegion.fromWorld` inflates a cuboid's max by
`(1,1,1)` to undo an old bug where "legacy maps have cuboids that are one block too big", but it is
gated on `proto.isOlderThan(REGION_FIX_VERSION)` (1.3.1). At 1.5.0 it never fires — do not port it.

**OB13 — a cuboid's block count is `max − min`, not `max − min + 1`.** From `Bounds`, the block
span is `[roundDown(min), round(max) − 1]` inclusive, so `getBlockSize()` is
`round(max) − roundDown(min)`; for integer coordinates that is simply `max − min`. A cuboid
`min="20,43,146" max="23,46,149"` therefore spans blocks x∈{20,21,22}, y∈{43,44,45}, z∈{146,147,148}
— **3×3×3, not 4×4×4**. Off-by-one here silently inflates every measurement by one block per axis.
A `<block>x,y,z</block>` region is a single block, and is the most common destroyable region in the
corpus (26%) — region handling here must cover `block`, not just `cuboid`.

**OB7 — the objective module *is* the gamemode; `<gamemode>` is a label, not the truth.** This is
not a DTM/DTC quirk — it is exactly as true for the CTW maps we already ship, where what makes a map
CTW is the presence of `<wools>`, not the text in `<gamemode>`. PGM never reads that element to
decide: each module contributes a `MapTag` when it parses anything, and the gamemode falls out of
which modules produced one.

| Module present | Gamemode |
|---|---|
| `<wools>` | CTW |
| `<destroyables>` with ≥1 real objective (OB16) | DTM (`Gamemode.DESTROY_THE_MONUMENT`, tag `monument`) |
| `<cores>` | DTC (`Gamemode.DESTROY_THE_CORE`, tag `core`) |

The element is demonstrably unreliable: of the 150 maps carrying these objectives, **68 declare no
`<gamemode>` at all**, and 9 declare `ctw` while carrying destroyables. Our parser currently defaults
a missing gamemode to `ctw` (`MapParser.cs:63`), which would silently mislabel most of them.

**But module presence alone over-reports, so we deviate from PGM here** (the `≥1 real objective`
qualifier above). PGM tags a map DTM the moment `DestroyableModule` parses anything, and **30 of the
297 maps with `<destroyables>` carry nothing but phantoms** (OB16) — block-swap mechanisms, not
goals. Those maps are not DTM, whatever PGM's tag says. This is not a rare correction: **8 of the 10
maps in our own `ctw/` corpus that carry destroyables are phantom-only** (abstract, abstract_remix,
citadel, down_side_up, fairy_tales_metamorphose, mine_your_own_business, newgen_classic, vesuvius) —
pure CTW maps, every one. Only `sentient` (8 real destroyables) and `bungee_coorde` (a core) are
genuine. **A module contributes a gamemode only if it holds at least one non-`show="false"` leaf.**

**And it is a set, not a scalar** (OB15). `MapXml.Gamemode` is a single string today — deriving a
gamemode *set* from module presence is the correct model, and it retires the `"ctw"` default rather
than extending it.

**OB15 — CTW, DTM, and DTC coexist in the same map.** This is not a curiosity to guard against; it
is 10% of the objective corpus (72 of 742 maps across both corpora), and both corpora keep a
`mixed/` directory for it:

| Modules | Maps | |
|---|---|---|
| CTW | 353 (47%) | |
| DTM | 228 (30%) | |
| DTC | 89 (11%) | |
| DTM + DTC | 46 (6%) | e.g. dynamite, autumn_solstice |
| CTW + DTM | 21 (2%) | e.g. **ender_blast**, chimeric, vesuvius |
| CTW + DTC | 3 | e.g. **hot_dive**, the_4th_law |
| CTW + DTM + DTC | 2 | cacti_the_wool, the_fenland_epic_style |

Architecturally this costs us nothing — `wool`, `destroyable`, and `core` all hang off `map_id`
(§11), and each objective's validity is independent (`MapValidity`'s "every wool needs a monument"
is a wool rule, not a map rule). The requirement is purely negative: **nothing may assume a map has
exactly one objective type** — not the parser, not the schema, not the UI, not the gamemode field.
A wool room and a core can sit in the same map, and 26 real maps prove it.

---

## 4. Two teams only

**OB14 — the plan editor offers destroyables and cores for 2-team symmetries only.** `rot_180` and
`mirror_*` (orbit order 2) get the placement kinds; **`rot_90` (order 4) does not offer them at
all**. This is a plan-editor scope decision, not a parser limit — see the caveat below.

PGM grounds the distinction exactly. Both objectives define `canComplete(team) { return team !=
getOwner(); }`, and both compute `isShared = competitors.filter(canComplete).count() != 1`. With N
teams, N−1 competitors can complete, so:

> **`isShared` is precisely `teams != 2`.**

At 2 teams a goal has exactly one attacker: ownership and attack are unambiguous, progress belongs
to one team, and the wool model's "N−1 attackers each with their own monument" never arises. At 4
teams every goal becomes shared — three teams race the same monument, and PGM stops colouring it by
attacker and colours it by owner instead (`Destroyable.java:514`). Who gets credit, who is
eliminated when, and how alliances shake out are all live design questions with no canonical answer.
That is the complication, and it is not one the generator should invent a position on.

**This is a real restriction, not a rare edge.** In `dtcm/` (302 maps): 2 teams 157 (51%), **4 teams
107 (35%)**, 6 teams 19, plus a tail to 12+. Four-team DTM/DTC is a third of the corpus and we are
choosing not to generate it.

**The parser and schema must stay N-team.** OB14 constrains only what the plan editor *offers*.
Reading, storing, and round-tripping a 4-team DTM map must work — it is 35% of the corpus, and
nothing in §3, §10, or §11 may assume two teams. The restriction lives in the plan editor's placement
palette and nowhere else.

---

## 5. What these objectives do *not* need

No wiring templates, no spawner, no room region, no dye, no team filters, no apply rules, and no
subtraction from spawn protection. A destroyable or core is `owner + region + material(s)` plus a
stamped structure. The generator slice is correspondingly small — this is the cheapest objective in
PGM, not the most expensive.

---

## 6. The standard structures

Both objectives **float above the terrain**, which is why no carving, void, or negative-space
primitive is needed. The gap below is what lets a core's lava fall, and what players dig through to
extend it.

**Every figure in this section is world-measured, not XML-measured** — see OB12 for why that
distinction is the whole ballgame. The method: for each objective, resolve its region, read the
actual `.mca` blocks inside it, keep only those matching `materials`/`material`, and take *that*
bounding box. Corpus is `/media/sf_repos/CommunityMaps` + `/media/sf_repos/PublicMaps`
(n=500 destroyables, n=255 cores with a resolvable region and a known material).

### DT1 — the destroyable structures: the obsidian pillar dominates

**Over half of all destroyables are a 1-wide obsidian pillar, 1–3 blocks tall**, and 58% consist of
just 1–3 blocks in total. The cube is real but is the minority form. Pillars are 98% obsidian
(279/286); 3³ cubes are 86% emerald or gold (19 + 18 of 43).

| Style | True structure | Material | Corpus |
|---|---|---|---|
| `pillar-1` | 1×1×1 — a single block | obsidian | 134 (26%) |
| `pillar-3` | 1×3×1 | obsidian | 90 (18%) |
| `pillar-2` | 1×2×1 | obsidian | 62 (12%) |
| `cube-3` | 3×3×3, optional bedrock centre (DT2) | emerald / gold | 43 (8%) |
| `cube-4` | 4×4×4 | ender stone / emerald | 12 (2%) |
| `column-plus` | 3×3 plus-section column, 3 tall (DT4) | ender stone | see DT4 |

The one-block `pillar-1` is not a degenerate case to guard against — it is **the single most common
destroyable in the corpus** (riverbank's monuments are literally `<block>-4,9,30</block>`). The
pillar family is the default; `cube-3` and `column-plus` are the alternatives. Bespoke sculpture
above 4³ (DT4) we do not reproduce.

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

Two signatures fall out. **Fill is low** — 15–69% for all but one — because these are hollow,
decorative forms, not filled boxes. And **the large ones carry partial `completion`** (80–90%): they
are too big to break exhaustively, so authors let them fall early. This is the one place where the
otherwise-marginal `completion` attribute (§9) earns its keep.

**DT5 — a huge destroyable is a TNT tell.** Nobody mines 1000 blocks of ender stone by hand; these
monuments are destroyed with **TNT and cannons**, and the size *is* the signal. Every map in `dtcm/`
whose largest destroyable exceeds 200 blocks arms players with TNT — **7 of 7, no exceptions** —
against a 20–33% baseline everywhere else:

| Largest destroyable | TNT | no TNT | % TNT |
|---|---|---|---|
| 1–3 blocks | 16 | 61 | 20% |
| 4–30 | 16 | 32 | 33% |
| 31–200 | 4 | 8 | 33% |
| **> 200 blocks** | **7** | **0** | **100%** |

The maps name themselves: `blocks_destroy_the_dynamite`, `boombox`, `boomboxxxx`, `blast_mining`,
`blast_mining_ii`, `rock_the_casbah`. So the obelisk is not a decorative choice — it is **a monument
sized for cannon fire**, and it presupposes a TNT kit, block-drops, and the open sightlines a cannon
needs. That is a whole map archetype, not a stamp.

Which is exactly why the generator stamps the family's **small end** and stops: dynamite's 3×3 column
with a plus/cross section, 5 blocks per layer, height parameterised (default 3 → 15 blocks). It reads
as an obelisk at plan scale and is breakable by hand.

```
   ·  E  ·        E = ender stone; one layer of `column-plus`, repeated `height` times.
   E  E  E        The corners are left open — that hollow cross is the family's signature,
   ·  E  ·        and what separates it from `cube-3`. dynamite fences the corners for looks.
```

A true towering obelisk stays out of the generator: emitting one without the TNT economy around it
would produce a map that is technically valid and unplayable — a monument no one can break. If the
studio ever grows a TNT/cannon archetype, DT5 is the rule that says the obelisk comes *with* it.

### DT2 — the bedrock core is inert by construction

The cubes take an optional concentric bedrock centre (1×1×1 inside `cube-3`, 2×2×2 inside `cube-4`)
so players cannot hollow one out and hide inside. It costs nothing to model: `materials` names only
the emerald/gold, so **the bedrock is invisible to the goal** — neither counted in `maxHealth` nor
breakable. The corpus confirms it is real and common: a 26-block `cube-3` (27 − 1) is the modal
non-pillar block count.

```
   y=B+2   E E E
   y=B+1   E ▓ E        ← 1×1×1 bedrock centre; not in `materials`, so not part of the goal
   y=B     E E E
```

### DT3 — float

Destroyables float **3–5 blocks**; default `float = 4`. A `pillar-1` floating alone is the norm, not
an error.

### DC1 — the core structure (default 5×5×5, shell 1, lava 3×3×3)

The dominant real core casing is **5×5×5 obsidian** (57/255 = 22%; next 7×7×7 at 12%, 4×4×4 at 7%),
the shell is **1 block thick** (165/255 = 65%; 2 thick in 33%), and the lava interior is
correspondingly **3×3×3** (the modal lava volume, 46). Obsidian is effectively universal.

**The top is capped, not open.** 65% of cores enclose the lava fully (its top layer sits 1 below the
casing rim), 24% cap it 2 below, and only 11% expose it flush with the rim. The open-top variant is
real but is a minority style, so it is a **flag, not the default**:

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
Parameterise size and height; default both to 5, shell to 1.

### DC2 — float and leak are one knob

`leak = 5` is the mode (104/255) and also PGM's default; 3 and 4 are close behind (62, 64). Measured
float is bimodal: **27% of cores rest directly on a solid floor** (no gap at all — the lava must
spread or players breach the floor), and the rest cluster at 2–7 blocks of air.

With the core floating `F` blocks above the surface and leak level `L`, escaping lava free-falls to
`y = B − F` (it lands on terrain). By OB2 the core leaks at `y ≤ B − L`. Therefore:

> **players must dig `max(0, L − F)` blocks into the terrain below the core.**

`L ≤ F` leaks on its own the moment the casing is breached; `L > F` makes digging part of the
capture. Both are legitimate and both occur; the author picks. **Defaults: `float = 6`, `leak = 5`**
— no dig, matching the corpus centre. The two must be authored together, because neither means
anything alone.

### OB8 — one box function, two consumers

A generated structure and its emitted `<region>` must agree, or PGM silently produces a zero-health
goal (OB3). **The bounding box is computed once and shared** by the stamper and the region generator
— the shape `StructureStamper.IronCubeFootprint` already establishes. Never let the two derive it
independently. For *generated* maps we emit the region as the exact structure bounding box; the slack
seen in hand-authored maps (OB12) is an artifact we do not reproduce, per the standing rule that the
generator may emit a simpler canonical structure than a human wrote.

---

## 7. The plan and intent model

A destroyable and a core are placed exactly like a wool or a spawn: a marker in a piece, authored for
team 0 only, fanned across the symmetry orbit by the compiler. `owner`/`team` is the **defending**
team — the same meaning as `WoolIntent.Owner` — so orbit-fill is the wool path unchanged, minus the
monument mapping (there are no per-capturing-team monuments to transform).

```jsonc
"placements": {
  "spawns":       [ { "piece": "bar-e", "at": [1, 5], "facing": "front" } ],
  "wools":        [ { "piece": "bar-w", "at": [1, 8] } ],
  "iron":         [ { "piece": "bar-e", "at": [0, 4] } ],
  "destroyables": [ { "piece": "bar-w", "at": [2, 3] } ],  // style defaults to pillar-3; float/materials optional
  "cores":        [ { "piece": "mid",   "at": [2, 2] } ]   // size/shell/openTop/float/leak optional
}
```

Intent mirrors the wool slice on `MapIntent`, with the structure parameters resolved (the plan's
optional fields defaulted) by the compiler:

```csharp
public sealed class DestroyableIntent
{
    public string Owner { get; init; } = "";        // the DEFENDING team
    public string Name { get; init; } = "";         // required by PGM; auto-named if unauthored
    public string Style { get; init; } = "";        // pillar-1|2|3 · cube-3 · cube-4 · column-plus
    public string Materials { get; init; } = "";    // obsidian · emerald block · gold block · ender stone
    public Pt Anchor { get; init; }                 // marker column; the box floats above the surface
    public int Float { get; init; }                 // DT3, default 4
}

public sealed class CoreIntent
{
    public string Owner { get; init; } = "";        // maps to the XML `team` attribute (OB1)
    public string Name { get; init; } = "";         // optional; PGM auto-names when absent
    public Pt Anchor { get; init; }
    public int Size { get; init; }                  // DC1, default 5
    public int Height { get; init; }                // DC1, default 5
    public int Shell { get; init; }                 // DC1, default 1
    public bool OpenTop { get; init; }              // DC1, default false (65% of cores are capped)
    public int Float { get; init; }                 // DC2, default 6
    public int Leak { get; init; }                  // DC2, default 5
}
```

Names are required on destroyables and PGM will reject a nameless one. When unauthored, derive from
the owner and index (`Green Monument`, `Green Monument 2`) rather than pushing the burden to the
author.

---

## 8. Modes

77 of the 150 corpus maps declare `<modes>`, so this ships with DTM rather than after it. Modes are
almost entirely declarative for us — they change an objective's material at a match time — so there
is no world or structure impact. The work is parse, store, write, and **a third feature-id registry
alongside regions and filters** so `modes="a b"` resolves.

```xml
<modes>
    <mode after="25m" material="beacon" name="`bBEACON MONUMENT MODE"/>
    <mode after="45m" material="coal block" name="`8COAL MONUMENT MODE"/>
</modes>
```

**OB9 — mode membership is a tri-state, not a list.** `modes="a b"` is a specific set;
`mode-changes="true"` means *all* modes (PGM models this as a null set, not an enumerated one);
neither attribute means *no* modes. Combining both is an error PGM raises and we should too. Persist
as a `mode_changes` boolean plus a nullable id list, so the XML round-trips exactly.

Ids are optional in the XML and auto-generated by PGM when absent (252 of 333 `<mode>` elements
declare one). We generate on parse so the reference is always resolvable.

---

## 9. Deliberately unsupported

The corpus long tail is single-digit and none of it affects geometry, validity, or generation:
`sparks` (7), `show-progress` (12), `show-sidebar` (6), `show-effects` (6), `required` (6),
`repairable`, `scoreboard-filter` (84 — display only), and the shared `ProximityMetric`/`ShowOptions`
surface. **We already drop `ShowOptions`/`ProximityMetric` on wools**, so dropping them here is
consistent with the existing contract rather than new debt.

**`completion` is the exception — we parse, store, and write it**, defaulting to `1.0`. It is
semantically load-bearing (it changes when the goal completes) and far more common than a raw grep
suggests. It is also a worked example of why OB4 is not a footnote: in `dtcm/`, only **19** of 717
destroyable leaves declare `completion` on the leaf, but **141 have one after group inheritance** —
a 7× undercount — of which 113 are genuinely below 100%. Counting attributes without applying OB4
gets the wrong answer by an order of magnitude.

The modal values are 90%, 75%, and 80%, concentrated on DT4's large sculptures. We do not expose it
in the plan editor: every stamped style completes at 100%.

**Parse gotcha — the value is always a percentage, sign or not.** `parsePercent` strips any `%` and
divides by 100, so `completion="90"` and `completion="90%"` are identical (both 0.9), and
`completion="0.8"` means **0.8%**, not 80%. Both spellings occur in the corpus. Store the parsed
fraction, and re-emit with the `%` so the intent is unambiguous on the way out.

---

## 10. Validation and the data-loss fix

**OB10 — stop silently dropping objectives.** `MapParser.ParseInternal` never enumerates
`_root.Elements()`; it reads only the tags it names. A DTM map therefore parses "successfully",
passes `EnsureSupported` (proto 1.5.0), and loses its objectives with no error on round-trip. This is
a live data-loss bug independent of everything else in this doc, and it is the first thing to fix:
either parse these elements or reject them explicitly. Quietly eating them is the worst of the three.

**OB11 — validate what PGM only warns about (OB3).** The export gate must assert that each
destroyable's region contains at least one block matching its `materials`, and that each core's
region contains both casing blocks and lava. Because we generate the structure and the region from
one box (OB8), this should be unfalsifiable for authored maps — it is a guard against the generator
drifting, and a real check for imported ones. The corpus sweep found 10 destroyables that already
fail it (a region with none of its declared material), so this catches real breakage.

**The check is "at least one matching block", never "the region is full".** By OB12 a region
legitimately contains mostly air — a 3×3×3 region holding a 1×3×1 pillar is correct and common.
Anything stricter would reject most of the corpus.

`MapValidity` currently owns exactly one rule ("every wool needs a monument"). These join it there.

---

## 11. Persistence

New tables, per the hybrid decision — real columns for what we list and edit, JSON only for the
irregular leaf. **Both hang off `map_id`.** They must *not* reuse `monument`, whose `wool_id` is
`NOT NULL` with an `ON DELETE CASCADE` to `wool` — a destroyable has no wool, and the existing FK
makes a wool-less objective unrepresentable.

```
destroyable                            core
  id              PK                     id              PK
  map_id          FK → map.id            map_id          FK → map.id
  destroyable_key                        core_key
  name            NOT NULL               name            NULL   -- PGM auto-names
  owner           NOT NULL               owner           NOT NULL  -- XML attr `team` (OB1)
  region_key                             region_key
  materials       NOT NULL               material        NULL   -- NULL = obsidian
  completion      NULL  -- NULL = 1.0    leak            NULL   -- NULL = 5
  mode_changes    NOT NULL DEFAULT 0     mode_changes    NOT NULL DEFAULT 0
  modes_json      NULL                   modes_json      NULL   -- OB9

mode
  id  PK   ·   map_id FK → map.id   ·   mode_key   ·   name NULL
  after NOT NULL   ·   material NULL   ·   show_before NULL   ·   filter_key NULL
```

Unlike wools, neither needs the doc-tree codec bypass (`WriteWoolsFromDocAsync`/`GroupedWoolsAsync`).
That bypass exists because the flat `MapXml` cannot represent a monument-less wool or wool-level
fields; a destroyable and a core are flat records with no grouped shape to lose, so they can go
through `MapXml` like every other entity.

---

## 12. Sequencing

The two modes are not one piece of work and should not land as one.

**DTM first, and it is small.** The XML surface is four required attributes, and there is no wiring
to generate. The stamps ride the existing iron-cube pipeline (`IronPlacement` → `PlanCompiler` →
`StructureStamper.StampIronCube` → world, with a canvas preview already wired through
`PlanStructurePreview`) with material and size parameterised. `StampIronCube` hard-codes
`Blocks.IronBlock` and `IronCubeSize = 4`; obsidian, emerald, gold, ender stone, and bedrock need
adding to `Blocks`, which stops at stained glass today. Ship modes (§8) with it.

Within DTM the stamps are themselves ordered by payoff. **`pillar-1|2|3` first** — 56% of the corpus,
and a 1×N column is the simplest stamp in the system, barely more than `SetBlock` in a loop.
`cube-3` (+ DT2's bedrock centre) next, then `column-plus` (DT4) — the plus section is a mask over
the same box, so it costs a predicate, not a pipeline.

**Cores second.** Everything from DTM applies; the delta is the shell-and-lava emitter (DC1), lava in
`Blocks`, and the float/leak pair (DC2). No new class of operation — cores float, so the world model
is unchanged.

**Two independent of both, and neither should wait.** OB10 (the silent objective drop) is a live
data-loss bug on today's parser. OB7 (gamemode from module presence) is a correctness fix that
already misreads the CTW corpus we ship — DTM/DTC only made it visible.

**OB14 is a plan-editor gate, and it is the cheapest thing here**: `rot_90` hides the two placement
kinds. It costs nothing to honour from day one and avoids generating shared-goal maps we have no
design position on.

---

## 13. Later: detecting objectives from a world scan

Not in scope for the work above, recorded because §6's corpus study is most of the groundwork.

`MonumentSuggester` already solves the harder version of this problem for CTW: given a world and a
box the author drew, infer which blocks are the wool monument — 96.6% precision / 57.8% recall over
1721 monuments, using sign-facing inversion, sign-text classification, pedestal geometry, and
armour-stand fallbacks (`monument-suggestion.md`). The same shape applies here: **scan the world,
propose the objectives, let the author confirm which is a destroyable and which is a core.**

**This is the easier problem, not a harder one**, and §6 is why. Wool monuments are a design free-
for-all — the recall ceiling there is set by maps that mark them with nothing but a sign, or nothing
at all. Destroyables and cores are far more standardised:

- **A core is nearly unmistakable.** Obsidian casing enclosing lava is a signature almost nothing
  else in a Minecraft map produces. Find lava with an obsidian shell around it and you have found a
  core, plus its bounds, plus its material — geometrically, not heuristically.
- **A destroyable is a material outlier.** 56% are a 1–3 block obsidian pillar standing alone (DT1);
  the cube and column families are similarly distinctive. Detection is "find the small isolated
  clusters of obsidian / emerald / gold / ender stone", which is a `wool_block`-style material sweep.
- **The material vocabulary is tiny and closed** — obsidian, emerald, gold, ender stone cover
  effectively all of it, against wool's 16 colours × arbitrary surroundings.
- **The families predict their own parameters.** DT2 says a bedrock centre means a cube; DC2 ties
  float to leak; DT5 says >200 blocks means a TNT map. A detector can propose `leak`, `completion`,
  and the style, not just the box.

The existing CTW pattern-matching carries over directly — the same world-scan plumbing, the same
candidate-store shape (`monument-candidate-store.md`, `monument_candidate`), the same
confirm-in-the-UI flow. What changes is the classifier, and it gets a cleaner signal to work with.

The one trap is OB12: a detector must propose **the structure's** bounding box and then emit a region
around it. Detecting the region is impossible — the region is a human's loose box and is not in the
world.

---

## 14. Water lanes — a CTW feature this work unlocks

> **Scope note:** water lanes are a **CTW** feature, not DTM/DTC. They are documented here because
> their legacy implementation *is* a destroyable (OB16), so nothing can detect or author one until
> this work lands. Expect this section to graduate to its own contract doc when it is built.

A **water lane** is a route that opens **mid-match**: a gap between islands becomes bridgeable, adding
a new way to reach the wool late in the game. Three separate authors name the mechanic in their own
boss-bar text, and "bridgeable" is the word they all reach for:

| Map | Fires | The author's message |
|---|---|---|
| piorun | 5m | ``Middle lane will become bridgeable`` |
| dominion | 10m | ``Crescent Void gaps will be bridgeable in`` |
| newgen_classic | 15m | *(none)* |
| vesuvius | 20m | ``Farlanes will become bridgeable`` |
| tulip_mania_ii | 30s | ``Water Lanes will spawn in {0}`` → ``Water lanes have been added!`` |

**WL-A — the mechanic is `VoidFilter`, and it reads y=0 live.** From
`filters/matcher/block/VoidFilter.java`:

```java
return block.getY() == 0
    || (!WorldProblemListener.wasBlock36(world, x, 0, z)
        && world.getBlockAt(x, 0, z).getType() == Material.AIR);
```

A column is **void** iff the block at **`(x, 0, z)` is air** and was not a block-36 marker — so
`<apply block="deny(void)">` denies building in that whole column. Crucially `getBlockAt` is
evaluated **at query time, not at load**. Put *any* non-air block at y=0 and the column stops being
void from that instant. That is the entire trick: **fill y=0 with water at 15m and the gap becomes
buildable**, so players bridge a route that did not exist before. (This same y=0 rule is why a
stained-glass slab at the world floor reads as a build region, and why `wasBlock36` exists at all —
the invisible marker declares "buildable" without placing a visible block.)

**WL-B — there are three generations, and only the newest is worth authoring.**

*Gen 1 — fake destroyable* (vesuvius, piorun, dominion, newgen_classic). `materials="air"` blocks at
y=0 swapped to water by a mode. Ownership is vestigial: authors split one lane into per-team halves
purely because `owner` is required (piorun's `mid-blue` / `mid-red` share an edge and form one
straight lane). This is the form OB16 catches.

*Gen 2 — inline fill action* (`lupa`, `tulip_mania_ii`, `icecream_sandwiched_ii`, `malupa`). An
`<action>` containing `<fill>`, on a time `<trigger>`. No destroyable, no mode:

```xml
<actions>
  <trigger scope="match" filter="add-side-lanes">
    <action><fill region="lanes" material="water" filter="only-air"/></action>
  </trigger>
</actions>
```

`FillAction` takes `region` + `material` (+ optional `filter`, `update`, `events`) and writes the
blocks directly. `tulip_mania_ii` names its region `water-lane-fill-regions` and passes
`filter="only-air"` so the fill cannot overwrite terrain. dominion's `<!-- TODO: replace fake lanes
destroyables with fill actions -->` is an author migrating Gen 1 → Gen 2 in-place.

*Gen 3 — a shared include + a conventional region id* (`bridgid_ii`, `ad_astra`,
`rushers_vs_defenders`, `araxa`, `turf_wars`, `royal_garden_ctw`). **The behaviour is factored out
entirely.** The map declares only *where* the lanes are, under an agreed id, and pulls the rule in:

```xml
<include id="water-lanes"/>          <!-- the shared fragment: what a water lane DOES -->
...
<union id="water-lanes">             <!-- the map: only WHERE, under the matching id -->
  <union id="blue-lanes">
    <cuboid id="build-area-6" min="40,0,25" max="55,2,40"/>
    <cuboid id="build-area-8" min="20,0,10" max="40,2,25"/>
  </union>
  <union id="red-lanes"> … </union>
</union>
```

All six follow it identically, and **five of the six contain no `<fill>`, no `<destroyables>` and no
`<modes>` at all** — proof the include carries the whole mechanism, keyed by the region id alone.
This is the cleanest factoring of the three and the one to author: emit a `water-lanes` region and
the include, not a hand-rolled copy of the rule.

**WL-D — the include supplies 100% of a Gen-3 lane, and Gen 3 is therefore free for us.** Not one of
the six maps applies anything to its lane regions — **zero** `<apply>` on `water-lanes` /
`blue-lanes` / `red-lanes` across all six. The map contributes geometry and an id; the fragment
contributes everything else. Two consequences, and both cut *for* Gen 3:

- **Authoring is one line.** We do not need the include's body to emit it — the server resolves it at
  load. `MapXml.Includes` and `XmlWriter.cs:112` already exist, and `CtwStandards.cs:104` already
  uses them (`m.Includes.Insert(0, KillRewardInclude)`) to ship `gapple-kill-reward` on every
  generated map. A water lane is the same move: emit `<union id="water-lanes">` + add `"water-lanes"`
  to `Includes`. **Gen 3 costs one string and a region** — no `<actions>`/`<fill>`/`<trigger>` parser,
  no fake destroyable.
- **Detection is two facts.** `<include id="water-lanes"/>` + a `<union id="water-lanes">` *is* the
  signal. Reading the body (WL-E) would tell us what a lane does; it is not needed to know that the
  map has one.

**The water bucket is not part of this — it is a red herring, and a well-camouflaged one.** It is a
universal CTW movement tool (place water under yourself to cancel fall damage), present in **163 of
358 `ctw/` maps — of which 157 have no water lanes at all**. At a ~46% base rate, "4 of the 6 lane
maps carry a bucket in slot 7" is precisely what noise looks like. Read nothing into it.

**WL-E — reading an include ≠ emitting one; only the reading is a gap, and it does not gate Gen 3.**
We already **emit** includes (WL-D). What we do not do is **resolve** them: `MapParser` preprocesses
`<if>`/`<unless>` (`ResolveVariants`) and `${constants}` (`ResolveConstants`) but has **no
`<include>` handling**, so a fragment's content never enters the document we analyse. **334 of our
358 `ctw/` maps (93%) use `<include>`**; `gapple-kill-reward` alone appears 815 times across the
corpora, and PGM additionally splices a `global` include into *every* map at the root
(`MapIncludeProcessorImpl.getGlobalInclude`). Whatever those fragments define — kill rewards, lane
rules, bulk crafting — we have never seen it, so any analysis that assumes it is reading a complete
map is wrong.

That is a real fidelity gap and its own problem, but keep it in proportion: it is about **analysing
imported maps**, not about authoring, and it blocks neither authoring nor detecting a water lane.
Bodies live in `config.getIncludesDirectory()` on the server, not in map folders, so closing it is a
**fetch** (obtain the include library), not a code change.

**WL-C — the lane is a flat y=0 footprint, one or more rectangles.** Every instance is a union of
cuboids spanning `y = 0..1` (i.e. the single y=0 layer, OB13). Shapes vary: vesuvius unions a
`right` and `left` lane (~33×17 and ~17×34 — elongated straights); newgen_classic unions four
compact ~7×8 patches, one per team, each hand-commented with its colour. A corner/L lane is expressed
the same way — a union of rectangles — so the authored primitive is **a set of y=0 rects**, not a
path. Detection is correspondingly cheap: the region is authored, not inferred from the world.
