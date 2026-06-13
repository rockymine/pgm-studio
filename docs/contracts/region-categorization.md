# Region Categorization Contract

How the studio derives what a region *is* and what it's *used for* — the full model behind
`data-model.md` §10. Category is **derived, never persisted**; `region_categories` in
`xml_data.json` is only a store of **user overrides** layered on top of this derivation.

A region falls into one of the gameplay categories below, with `build` taken only from
void-enforcement *structure*, never from `lane`/`bridge` names (§5) — trading raw coverage for
near-zero false positives.

> **Implementation:** `studio/services/region_categorizer.py` — `derive_region_facets` →
> `{id: {category, roles}}`; `categorize_regions` → flat `{id: category}` with user overrides
> applied. Oracle: `tests/fixtures/region_categories/`.

---

## 1. Two facets

A region has two orthogonal facets. Conflating them is the root mistake.

1. **`category`** — what the region *is* in gameplay (spatial/semantic identity).
2. **`roles`** — what the region is *used for* (rule machinery, conditional timing, the
   apply-rules/filters attached). A region may have a `category` *and* roles, or **only**
   a role (a pure filter target has no gameplay category).

The single most important rule: **a region's `category` is derived from intrinsic gameplay
signals, never from the fact that a filter is applied to it.** Filter targeting is a role.

---

## 2. `category` taxonomy

| value | meaning | primary signal |
|---|---|---|
| `spawn` | team spawn point + protected spawn area | `spawns[].region`; `enter=only-<team>` (with disambiguation) |
| `observer_spawn` | observer / `<default>` spawn | `observer_spawn.region` |
| `wool_room` | wool storage / defense | `wool_room_region`; `enter=not-<team>` (defender excluded) |
| `monument` | wool **delivery** objective | `wool.monuments[].monument_region` |
| `wool_spawner` | wool regeneration zone | `spawner.spawn_region` **only when the spawner dispenses wool** (the `player_region` is the wool **room**, → `wool_room`) |
| `build` | buildable / traversable space (subtype `footprint` \| `traversal`) | void-structure (§5) |
| `mechanic` | special mechanic (subtype `kit` \| `shop` \| `renewable` \| …) | **non-wool spawner** regions (golden-apple/arrow/…); renewable refs; `*spawner*` names |
| `other` | genuine uncategorized | — |

Objectives are **three categories, not one**: `wool_room` (source, defended),
`monument` (goal, delivery), `wool_spawner` (regeneration). A monument is gameplay-opposite
to a wool room and must not live under "wool".

Corpus distribution (named regions, after dropping block-targeting as a spatial signal):
`spawn` 19% · `wool_spawner` 15% · `wool_room` 14% · `monument` 9% · `build` (incl.
void-structure) · `observer_spawn` 1% · `mechanic` <2% · plus `rule_container` role 6% and
a residual `other`.

---

## 3. `roles` facet

Orthogonal flags/data attached to a region regardless of category:

- **`rule_container`** — the region has **no gameplay identity** and exists *only* as a filter
  target (`category = other`). Set for `negative` wrappers (the whole-world "everything except X"
  enforcement regions: `not-spawns`, `not-build-area`, `void-area`, `no-bridges`). A `complement`
  is **not** flagged — unlike a `negative` it carries a positive base (child[0]) and inherits that
  child's category (e.g. a `spawns` union over a `complement` keeps `category=spawn`). The editor
  surfaces `rule_container`s under a "rule wiring" view, not as primary geometry. A rule-shaped
  union like `spawns` is **not** a pure container, so it does not get `rule_container`.
- **`rule_group`** — the region **has** a gameplay category **and** is the union that batches a
  rule over its **same-category peers**. Detection: a `union` with apply-rules attached whose
  named, categorized descendants (reached through anonymous intermediate unions) are **all the same
  category** and number ≥2. Clean example (`annealing_iv`): `woolrooms` (`wool_room`, batches a
  break filter over the 4 team rooms). Counter-example: `spawns` is **not** a rule_group — it's
  `union → complement(spawn-areas − the 12 monuments)`, geometry *sculpted* for the
  `block_place=only-iron` rule by carving monuments out, so its descendants mix `spawn` and
  `monument`; it keeps `category=spawn` and its rules in `roles`, but no `rule_group` flag (it's
  rule-*shaped*, not a peer grouping). The editor lists rule_groups under their category but tags
  them so the author knows editing membership re-scopes the attached rule; C9's templates use the
  flag to offer "add the new region to this group so the rule covers it too", and ungroup can warn
  that a rule is attached.
- **`time_gated`** — the region's behavior is gated by an `after` / `time` / `pulse`
  filter; carries the resolved `duration`. This is how dynamic build extensions work
  (stalemate-breaker water lanes — see §5, §8). 21 corpus maps use time filters.
- **`rules`** — the apply-rules targeting the region and the filter ids they reference
  (the rule wiring), for display and validation.

**Wire format (as emitted by the derivation and asserted by the oracle).** `roles` is a flat
list of strings: the flags first, in the order `rule_container`, `rule_group`, `time_gated`
(the time flag carries its duration, e.g. `time_gated=30s`), followed by the rule-wiring
entries `"<event>=<filter_id>"` sorted alphabetically by event. Only the spatial edit/access
events `block`, `block_break`, `block_place`, `enter` are recorded; `block_physics`, `use` and
`kit` are mechanic-level and omitted (they never change a category and would only add noise).

---

## 4. Derivation precedence

Assign `category` by the first matching signal (most reliable first). **Never overwrite a
category already set by a more reliable signal.**

1. **spawn** ← `spawns[].region`, **observer_spawn** ← `observer_spawn.region`. (Authoritative.)
2. **monument** ← `wool.monuments[].monument_region`.
3. A spawner's `player_region` is the **detection** region the player stands in; its `spawn_region`
   is where the items drop. If the spawner **dispenses wool**: **wool_spawner** ← `spawn_region`,
   **wool_room** ← `player_region`. If it dispenses anything else (golden apple, dye, …): the
   `spawn_region` dispenser is a **mechanic**, and the `player_region` keeps its own identity (it's
   often a real wool room the spawner feeds — peloponnesia's gapple spawner → `lime-woolroom`).
4. **wool_room** ← `wool.wool_room_region`; and ← `enter=not-<team>` rules (defender
   excluded — reliable, see §6).
5. **apply message** (§6.1) — the author's `message` text names the zone explicitly
   ("…enter the enemy's **spawn**!" → spawn; "…edit the **wool room**!" → wool_room;
   "…break the **spawner**" → mechanic). High-signal; applied before the build sweep so a
   spawn-protection zone sitting inside the void-complement is not swallowed as build.
6. **spawn** ← the **spawn-floor pattern** (`block_break` restricted to a material — iron *or* gold
   — **and** `block_place` denied: players may only break the spawn floor), or a **spawn-protection
   kit** (a `kit`/`lend_kit` whose id names spawn protection/regen — resistance + regeneration in
   spawn, e.g. `spawn-protection`, `spawn-regen`; excludes `leave-`/`remove-spawn` kits that strip
   the buff *outside* spawn; e.g. mushroom_gorge `base-sides`).
7. **build** ← void-structure **and** permissive placement (§5).
8. **mechanic** ← non-wool spawner `spawn_region` (step 3).
9. **spawn | wool_room** ← `enter=only-<team>` rules, disambiguated: in `spawns[]` → spawn;
   monument/spawner-adjacent or wool-named → wool_room; else leave for name heuristics.
10. **name heuristics** on **primitives only** (not on compounds): `*monument*`, `*wool*`/`*room*`/
    `wr`-token (→ `wool_room`; `wr`/`wrs`/`wr2` match as **whole tokens** only — not the substring of
    `wrapper`, a void-mechanic region), `*spawner*` (→ `mechanic`, checked before `*spawn*`), `*spawn*`
    (→ `spawn`).
    **No `build` name heuristic** — `lane`/`bridge` names are *not* taken as build (§5 derives build
    from void structure; a lane with no void parent is a movement mechanic, not build space).
11. **Constrained recursion** (§7).
12. **mechanic (fallback)** ← `renewables[].region_id`, and apply rules with a `velocity`/`kit`/
    `lend_kit` action (regen zones, portal-boost pads, kit dispensers). Applied **last**, claiming
    only what is otherwise `other` — a wool room with wool regen stays a wool room, but a `portals`
    union or an `iron-regen` zone becomes `mechanic`. Never relabels a `negative`/`complement`
    rule-wrapper (it keeps `rule_container`).

Do **not** use `block` / `block-place` filter targeting as a `category` signal — record it
as a `roles.rules` entry instead. (Treating it as "build" produces near-all false positives —
e.g. `spawns` would be tagged build merely because it carries an iron-only rule.)

---

## 5. Build regions: static and time-gated

Build regions are derived from **rule structure**, not naming (`build` is <2% of regions by
name alone). PGM grants buildability to columns with a block at Y=0; authors carve buildable
space out of the void and enforce the boundary with a filter (`editor-build-regions.md`).

**Static (void-complement) — the common case.** A region targeted by a placement filter that
resolves to (or, for an inline filter, *names*) a `void` is the enforcement wrapper (`void-area`,
`not-build-area`, `no-bridges`). The buildable space is its carved-out children:

> **build = the carved-out children (recursively) of the void-enforcement wrapper, minus any
> region that is — by signal *or* by name — a `spawn`/`wool_room`/`monument`/`wool_spawner`.**

Two wrapper shapes occur and the carved-out children differ:
- **`negative`** ("everywhere except X") → the single child **X** is the build space; the
  negative itself is `other` + `rule_container`.
- **`complement`** ("base − A − B …") → the **subtracted** children (child[1:]) are the build
  space; child[0] is the void base.

Void detection follows the filter tree to a `void` leaf even when wrapped in `not`/`any`/`all`;
some maps inline the filter so only its descriptor survives (`deny(void)`, `not-void`), matched
by the `void` token in placement-rule context.

The objective exclusion matters because the complement subtracts *every* editable region — wool
rooms and spawns included — so name-recognisable objectives are dropped from the build subtree
(they take their own category). This auto-captures `build-area`, `lanes`, `bridges`, and island
footprints without enumerating their names. Subtypes: island-like → `footprint`; bridge/lane/gap
→ `traversal`.

**Time-gated (dynamic).** A build region whose `block` rule is gated by an `after`/`time`/
`pulse` filter opens mid-match (anti-stalemate). Category is still `build`; add the
**`time_gated`** role with the duration. Examples: `add-water-lane` (30s), `golden_drought_vi`
`60m/80m/100m/120m`, `mame_…` `after-30m/60m/90m`.

**Permissive placement (positive form).** The inverse of the void-negative: instead of denying
placement outside the build area, a map can *allow* placement inside it. A region used as the
**filter** of a `block_place` rule ("you may place where you are inside this region") **is** the
build area, and its children are build. Vertex's global rule
`<apply block-place="playable-area" block-break="deny-bottom-layer"/>` is the canonical case —
`playable-area` (= `blue-side` ∪ `red-side`) is the whole buildable map floor.

> **Why vertex needs this (build mechanics).** PGM marks a block-column as **void** (untouchable)
> when it has air at y=0. Maps with **bedrock** at the world bottom are safe — players can't break
> the floor, so the buildable footprint never changes. Vertex has **stone** at the bottom, which is
> breakable: clearing a column to y=0 would make PGM treat it as void and lock building there,
> letting players reshape the map. So vertex defines the full floor as the `playable-area` build
> region and adds `block-break="deny-bottom-layer"` to protect the bottom stone layer. (The water
> layer at y=0 in maps like `icecream`/`agrostid` is, by the same rule, auto-classified buildable
> where the void filter applies.)

**Pure-geometry (Approach B).** A non-buildable region with `block-place=never` and no void
filter is a `rule_container` (lockdown target), not a build region.

Caveat: a `lane` with **no** void parent and **no** rule (e.g. `ad_astra`'s `water-lanes`) is
*not* build — it's likely a movement mechanic. The structural rule correctly excludes it;
naming alone would not.

---

## 6. `enter`-filter polarity (spawn vs wool disambiguation)

`apply enter=<filter>` rules mark protected zones. Resolving the filter's team polarity:

- **`enter=not-<team>` → `wool_room`** — the team is *excluded* (defender can't enter their
  own wool room). Reliable: 50/52 = 96% in the corpus.
- **`enter=only-<team>` → ambiguous** — 447 spawn vs 445 wool_room corpus-wide. Some maps use
  `only-<owner>` to let *only* the owner into their wool room (opposite convention). Resolve
  with `spawns[]` / monument adjacency / name; otherwise leave as a neutral protected zone.

The polarity also reveals the **owning team** of a wool room (the excluded team), corroborating
the derived owner from the wool model (contract §6).

### 6.1 Apply-message signal

CTW authors write a human `message` on nearly every protection rule, and these are remarkably
consistent across the corpus (3204 messages, clean clusters). They are a high-confidence
categorization signal — used when structural references are absent (a protected zone that isn't
in `spawns[]`):

| message contains | category | corpus count (top phrasings) |
|---|---|---|
| `wool room(s)` / `woolroom` | `wool_room` | ~900 ("…enter your own wool room!", "…edit the wool room!") |
| `spawn` (not `spawner`) | `spawn` | ~600 ("…enter the enemy's spawn!", "…edit spawn!", "…break iron blocks in spawn!") |
| `enemy/opponent base` | `spawn` | ~40 ("…enter the enemy's base!") |
| `spawner` | `mechanic` | ~20 ("…edit the spawner!", "…break the spawner") |
| `void` / `build outside` / generic | *(none)* | build is structural; generic carries no identity |

Match `spawner` **before** `spawn` (it contains it). Apply the message signal **before** the build
sweep (§4 step 5) so a message-named spawn/wool zone inside the void-complement keeps its identity.

The **spawn-floor pattern** (§4 step 6) is the structural twin of the "…break iron/gold in spawn"
message: spawn platforms let players break only the floor material (iron *or* gold — both occur)
while placement is fully denied (`block_break`=material filter **and** `block_place`=deny/never).

---

## 7. Compound and recursion rules

Compounds give PGM meaning and are needed for round-trip, but they break naive categorization.

- **`negative`/`complement` are containers, never spatial.** Flag `rule_container`; never
  assign them a gameplay category from their name (`not-spawns` is **not** a spawn) and never
  propagate a category into or out of them.
- **`union` recursion is constrained.** Propagate a union's category to its children **only**
  when the union's category came from an intrinsic spatial signal (e.g. a `reds-woolroom`
  union → its children are wool_room), and **never overwrite** a child's own direct category,
  and **never** recurse through a `negative`/`complement`.
- A child reached only through a `rule_container` keeps its own intrinsic category (or `other`
  + role), so wool monuments and spawn areas are never relabeled by the wrapper around them.

---

## 8. Worked examples

### `annealing_iv` (4-team)

| region | type | category | roles |
|---|---|---|---|
| `blue-spawn-point` | cylinder | spawn | |
| `blue-spawn` | rectangle | spawn | `enter=only-blue` |
| `blues-woolroom` | union | wool_room | `enter=not-blue` (blue defends), block rule |
| `blue-team-red-wool` | block | monument | |
| `blue-wool-spawn` | cuboid | wool_spawner | |
| `build-area` | union | build | |
| `not-build-area` | negative | — | `rule_container` (void enforcement) |
| `spawns` | union | spawn (base of its complement child) | iron-only block rules (no `rule_container`, no `rule_group`) |
| `not-spawns` | negative | — | `rule_container` |

Result: 34/35 named regions categorized; only `blocks-filter-region` is genuinely `other`.

### `icecream_sandwiched_ii` (time-gated build)

```
<any id="water-lane-building"><after id="add-water-lane" duration="30s" .../></any>
<union id="water-lanes"> → blue/red-water-lanes → lime/cyan/yellow/orange-water-lane
<apply region="building-water-lanes" block="water-lane-building" message="...void area!">
```

`water-lanes` and children → `category = build` (subtype `traversal`), `roles = {time_gated:
{duration: "30s"}, rules: [block=water-lane-building]}`. The full wiring round-trips; the model
surfaces *that it is a build region* and *that it opens after 30s*.

---

## 9. Known limitations

- `enter=only-<team>` is ambiguous (spawn vs wool_room) and falls back to `spawns[]`/name; a small
  set of regions stay neutral-protected.
- `mechanic` uses a single bucket + free-text subtype (kit/shop/renewable are low-prevalence),
  rather than a rigid enum.
- Build subtype (`footprint` vs `traversal`) is best-effort from geometry/naming; not all maps
  make the distinction explicit.

The hand-verified test oracle (`tests/fixtures/region_categories/`) covers `annealing_iv` (4-team),
`vertex` (2-team), `acapulco` (multi-wool 2-team), and `icecream_sandwiched_ii` (time-gated), each
labeled `{ region_id: { category, subtype?, roles[] } }`.
