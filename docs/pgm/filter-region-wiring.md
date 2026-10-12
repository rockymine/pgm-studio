# Filter ↔ Region Wiring

How behavior attaches to regions, and the v1 suggestion templates the editor offers. This doc owns
the **wiring relationship** and the **template catalog**. It does not restate:
- the **Filter / ApplyRule shapes** → the types themselves, `Domain/Filter.cs` and `Domain/MapModel.cs`;
- the filter **vocabulary** + the *event × filter-type* and *event × region-geometry* matrices →
  `filter-patterns.md` (Appendix A.2–A.5) + the corpus recipes (Clusters 1–6);
- how wiring **surfaces per region** as `roles` → `region-categorization.md` §3.


## The relationship

A region is **inert geometry**. Behavior comes from an **apply-rule**: `region × event → filter`
(plus optional actions `kit`/`lend_kit`/`velocity`/`message`). Events: `enter`, `leave`, `use`,
`block`, `block_place`, `block_break`, `block_physics`, `block_place_against`, and the kit/velocity
condition `filter`. One rule may carry several `event→filter` keys at once (canonical, not a
normalization target); filters compose (`all`/`any`/`not`/`deny`) and reference children by id.

**This introduces no new persisted type.** Wiring is `apply_rules` + `filters` (`Domain/MapModel.cs`, `Domain/Filter.cs`)
referencing regions by id; in the region view it appears as the `roles` `<event>=<filter_id>` entries
(`region-categorization.md`).

## How PGM decides one block change

Read off PGM itself (`regions/RegionMatchModule`, `listeners/BlockTransformListener`, `filters/operator/*`,
`filters/matcher/block/VoidFilter`, `wool/WoolMatchModule`, `core/CoreMatchModule`,
`destroyable/DestroyableMatchModule`); every apply rule a map states is answered inside this sequence.

**Every change to a block is one event, whoever or whatever makes it.** A player placing or breaking, a bucket,
an explosion, fire, a piston, a falling block and **flowing water or lava** all become one `BlockTransformEvent`;
flowing lava carries its owner as the player, where PGM knows one. A change is a **place** when the new block
is not air and a **break** when the old one is not, so a block laid into water is both.

**Four stages see it, in this order, and a stage after the first runs only on a change still standing.** The
observers' guard refuses anyone not playing; the region rules answer next; then a monument refuses its own team
damaging it and a repair it does not allow, and a core refuses its own team breaking its casing and anything
touching its lava; last, the wool module — which runs on every change, cancelled or not — refuses any block on a
monument but its own wool placed by its own team, and lets that one through whatever was said before it.

**The region rules are one pass in document order, and the first rule that does not abstain decides.** From
proto 1.3.3 a single pass walks every rule: a rule is skipped unless it names the change's scope (`block` names
both, `block-place` and `block-break` one each) and its region holds the block — the old block for a break, the
new one for a place — and the first filter answering allow or deny settles the change. A change no rule answers
stands. PGM puts a few rules ahead of the map's own: `<maxbuildheight>`, which refuses a place above it; the
entrance and exit of every portal, which refuse a place; TNT without block damage; and the old `<playable>`.

**The filter is asked about the block itself.** A `material` filter matches the old block on a break and the
new one on a place. `<void/>` reads the block's own column: true when the block is at y=0, or when the block at
`(x, 0, z)` is air and was not a block 36 — PGM removes every block 36 on load and remembers the ones at y=0, so
an invisible marker there makes the column ground. `all` denies when any child denies and otherwise allows when
any allows; `any` allows when any child allows and otherwise denies when any denies; `not` turns allow and deny
round and keeps an abstention; `deny` denies only where its child allows and abstains everywhere else.

**So a goal over a column with nothing at y=0 cannot be completed under a void rule.** The studio's break side,
`any(all(<vegetation>, void), not(void))`, lets leaves and logs through over the void and refuses obsidian, so a
monument or a core whose columns carry no mark cannot be broken; and since lava flowing is a place, the place side
refuses every void column the lava would run into, so the core cannot leak either. A floating goal needs its
columns marked at y=0 — block 36 if nothing should show — or a build region over them, as Fungi Grove's
`under core` rectangles are. A wool is not affected: its monument is decided last, by the wool module.

**`Editability` reads the same pass at two grains.** The zone grid answers for a column — whether anybody may
place or break there, a material filter counting as a permission for somebody — and an empty void column, one no
segment of the scan stands in, holds nothing a break rule could let anyone take. `GoalEdits` asks the pass for a
goal's own material: a core's or a monument's block broken in each of its columns (`EZ3`), and lava placed in any
column under or beside a core (`EZ4`).

## What attaches where

The sensible defaults (a **soft-warning** source for the UI, never a hard validator — `filter-patterns.md` A.4):

- `enter` / `use` → **team**(-based) filters, on **area/compound** regions only (never a single
  `block`/`point` — you can't "enter" a 1-block region).
- `block` / `block_place` / `block_break` → `never` / `material` / `all`/`any`/`deny`/`not` over those;
  on areas, void wrappers, occasionally a single block.
- `block_physics` → `deny`. The kit/velocity `filter` condition → team / `all`.

Full matrices: `filter-patterns.md` A.2 (event × filter-type) and A.5 (event × region-geometry).

## v1 templates (suggest + confirm)

Five templates, grounded in the corpus's most common shapes. Each is **suggested from a map signal**
and **confirmed by the author** — never auto-applied or silently mutated. Each emits standard
`Filter` + `ApplyRule` entries (no special persisted form).

| # | Template | Trigger (signal) | Emitted wiring | Recipe |
|---|---|---|---|---|
| 1 | **Build / void enforcement** | positive build region(s) from the Configure Build phase (+ `layer_y0.parquet`) | group buildable regions → apply `block-place=no-void` to the **complement**, with `block-break=over-void-breakable` beside it so what decoration left over the void can still be cut down | Cluster 2.4 |
| 2 | **Spawn protection** | a team spawn region (`spawns[].region`) | on the protection zone, apply `enter=<team>` — a team id is itself the filter matching its members, so no `<team>` filter restates it; on the shared `spawns` region (a union only where two or more spawns are protected), `block=never` (anti-grief; `never` is built-in — no new filter), **restated as `block-break=only-<ore>` + `block-place=only-<ore>-cause-world` where ore lives in a spawn** (below); optionally `use=<team>` | Cluster 1.1 |
| 3 | **Wool-room defense** | a wool-room region with a derived owner (§6 owner) | apply `enter=not-<owner>` (defender excluded) | Cluster 1.2 |
| 4 | **Wool-room build/break** | a wool-room region | apply `block=<team>-woolrooms-filter` (team check + material whitelist) | Cluster 2.2 |
| 5 | **Wool-room chest lockout** | a wool-room region with a derived owner | apply `use=deny(all(<owner>,chest-filter))` — the defenders cannot open the attackers' supply chest in their own room | Cluster 1.2 |

**Template 2's blanket deny is wrong the moment a spawn holds ore.** `block=never` protects a spawn holding
nothing but its own floor, and locks the CTW economy where it holds iron: the ore cannot be mined, so the
`<renewable>` that would regrow it never fires and the resource is scenery a player is told they may not edit.
So the rule is restated as the pair `docs/pgm/template.xml` uses — `block-break` admits only the ore,
`block-place` admits only that ore placed by the `world`, which is the renewable putting it back — and
everything else in the spawn stays untouchable either way. Ore reaches a spawn two ways, scanned out of an
imported world or stamped there by the plan as an iron cube, and neither pass can see the other's, so both
name what they found and `SpawnOreProtection` states the rule once over the union.

Template 5 exists because template 3 does not cover it. Denying *entry* is not denying *use*: a
defender standing at the room's edge is outside the region but within reach of a chest inside it, and
the supply in a wool room is for the team attacking it. The rule is written as an inline filter
expression rather than a named filter — it is a two-term composition used once per team, and at the
apply is where a reader of the XML looks for it.

Template 1 is the canonical *suggest + confirm* flow: detect the positive build regions, propose
"auto-group and apply the void filter to the complement?", let the author confirm/adjust/decline.

**Placing and breaking are one attribute short of one rule, and the difference matters.** `block` names both
scopes at once, so a void filter written under it seals whatever already stands over the void — and what
stands over the void on a studio-built board is decoration, because the dressing stage scatters trees and
flora across a coast and a canopy reaches past it. The break side therefore carries its own filter,
`block-break-void-filter = any(all(<the vegetation list>, void), block-place-void-filter)`: everything placing
allows, plus, over
the void only, the log, leaves and plants the dressing palette writes. Terrain materials are deliberately
absent — a crag or a sea stack is a shape the author built, and admitting stone out there would let a team
mine the board apart. `EZ1` is the read-back that finds the columns this is for.

The **declarative** generator wires the same shape from the other direction, unprompted: an intent carrying
`BuildIntent.Areas` emits the pair over `not-build-area`, the negative of what those rectangles leave
(`new-map-authoring.md` §5b). Both paths produce one void-marked filter ruled onto a `negative`/`complement`
wrapper and nothing wider, because PGM stops at the first apply rule that decides — the difference is that
the declarative one runs from a stated intent field rather than from a signal an editor session offers up
for confirmation.

## Interaction stance

**Suggest + confirm, never silent.** Detect a signal → propose the wiring → author confirms,
adjusts, or declines. The C3/C4 editors reject only **dangling references** (a child filter/region
that doesn't exist); "sense" (event/region/intent fit) is a **soft warning**, not a block.

## For B1

Wiring adds **no new typed shape**: the typed models are `Filter` + `ApplyRule` (`Domain/Filter.cs`, `Domain/MapModel.cs`),
and the region view exposes its attached rules via `roles` (`region-categorization.md`). The
templates above are pre-built `Filter` + `ApplyRule` combinations a wiring surface *emits* — not a
persisted entity. So B1 types filters/rules straight from §9 + the `roles` view; a wiring surface
(routes, suggestion engine, templates) builds on those types and cannot reshape them.
