# Regions and Filters — Domain Contract

The region and filter model behind PGM map editing, in one place: what a region *is* and how it gets
a `category`/`roles` (§1.1–1.9), where that data actually lives and when it's computed (§1.10), how
filters express real gameplay behavior in the corpus plus the type/event/geometry vocabulary they
draw on (§2), and how the two connect — region × event → filter — including the editor's v1
suggestion templates (§3).

Consolidates: `region-categorization.md`, `region-data-flow.md`, `filter-use-cases.md`,
`filter-region-wiring.md` (all four retired in favor of this file).

---

## 1. The region model

### 1.1 Two facets

A region has two orthogonal facets. Conflating them is the root mistake.

1. **`category`** — what the region *is* in gameplay (spatial/semantic identity).
2. **`roles`** — what the region is *used for* (rule machinery, conditional timing, the
   apply-rules/filters attached). A region may have a `category` *and* roles, or **only**
   a role (a pure filter target has no gameplay category).

The single most important rule: **a region's `category` is derived from intrinsic gameplay
signals, never from the fact that a filter is applied to it.** Filter targeting is a role.

**Implementation:** `src/PgmStudio.Pgm/Authoring/RegionCategorizer.cs` — `DeriveFacets` →
`{id: RegionFacet(Category, Roles, Subtype)}`; `Categorize` → flat `{id: category}` with
`region_categories` user overrides applied. Verified by synthetic unit tests
(`tests/PgmStudio.Pgm.Tests/RegionCategorizerTests.cs`) and the corpus parity guard
`tools/PgmStudio.RoundTrip --categorize <pyfresh> <pyfacets>` against the Python oracle
(which still emits flat `wool_room`/`monument`/`wool_spawner` — see the §1.2 parity note).

### 1.2 `category` taxonomy

| value | meaning | primary signal |
|---|---|---|
| `spawn` | team spawn point + protected spawn area (subtype `point` \| `protection`) | `spawns[].region`; `enter=only-<team>` (with disambiguation) |
| `observer_spawn` | observer / `<default>` spawn | `observer_spawn.region` |
| `wool` | the objective, with subtype `room` \| `monument` \| `spawner` | `wool_room_region` / `enter=not-<team>` → `room`; `wool.monuments[].monument_region` → `monument`; wool-dispensing `spawner.spawn_region` → `spawner` |
| `build` | buildable / traversable space (subtype `footprint` \| `traversal` — *designed, not yet emitted*) | void-structure (§1.5) |
| `mechanic` | special mechanic (subtype `kit` \| `shop` \| `renewable` \| … — *designed, not yet emitted*) | **non-wool spawner** regions (golden-apple/arrow/…); renewable refs; `*spawner*` names |
| `other` | genuine uncategorized | — |

**Subtype status (this codebase).** `DeriveFacets` currently emits a `subtype` only for **`spawn`**
(`point` \| `protection`) and **`wool`** (`room` \| `monument` \| `spawner`); `build` and `mechanic`
regions are emitted with `subtype = null`. The `footprint`/`traversal` and `kit`/`shop`/`renewable`
subtypes below are the intended refinement (still best-effort by geometry/naming) and are documented
for the design, not because they are produced yet.

The objective is **one `wool` category split by subtype**, not three flat categories: `room`
(the wool source/storage, defended — `enter=not-<team>`), `monument` (the delivery **goal** — gameplay-
*opposite* to a room), `spawner` (regeneration). The three are still bound to one wool (a wool has a
room, monument(s), and often a spawner), so they share a category, but the subtype keeps the
room-vs-monument opposition the model depends on — they are never an indistinct bucket. (A wool-
dispensing spawner's `player_region` is the wool **room** → subtype `room`; its `spawn_region` is the
`spawner`.) The editor's Objective activity lists them as Wool Rooms / Monuments / Wool Spawners.
**Parity note:** the Python reference still emits the flat `wool_room`/`monument`/`wool_spawner`; the
`--categorize` harness maps C# `wool`+subtype ↔ those, so the 350-map guard is preserved.

**Spawn subtype (implemented).** `spawn` carries a `subtype` separating the two things authors
treat differently: **`point`** — the literal spawn, the region in `spawns[].region` (where the
player materialises) — and **`protection`** — the surrounding anti-grief zone (`enter=only-<team>`,
the "…enemy's spawn!" message, the spawn-floor block pattern, spawn-protection kits). The two are
**disjoint across the whole 350-map corpus** (a spawn point never carries an enter rule), so the
split is unambiguous: point ⟺ in `spawns[]`, every other `spawn` region is protection. The editor's
Teams activity lists them as separate "Spawn Points" / "Spawn Protection" sections. (The protection
*mechanism* — no-enter barrier vs no-edit/grief — is already in `roles`, so subtype + roles is complete.)

Corpus distribution (named regions, after dropping block-targeting as a spatial signal):
`spawn` 19% · `wool_spawner` 15% · `wool_room` 14% · `monument` 9% · `build` (incl.
void-structure) · `observer_spawn` 1% · `mechanic` <2% · plus `rule_container` role 6% and
a residual `other`.

### 1.3 `roles` facet

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
  them so the author knows editing membership re-scopes the attached rule; the C9 wiring feature's
  templates (§3.3) use the flag to offer "add the new region to this group so the rule covers it
  too", and ungroup can warn that a rule is attached.
- **`time_gated`** — the region's behavior is gated by an `after` / `time` / `pulse`
  filter; carries the resolved `duration`. This is how dynamic build extensions work
  (stalemate-breaker water lanes — see §1.5, §1.8). 21 corpus maps use time filters.
- **`rules`** — the apply-rules targeting the region and the filter ids they reference
  (the rule wiring), for display and validation. This is the same `region × event → filter`
  relationship described in full in §3.

**Wire format (as emitted by the derivation and asserted by the oracle).** `roles` is a flat
list of strings: the flags first, in the order `rule_container`, `rule_group`, `time_gated`
(the time flag carries its duration, e.g. `time_gated=30s`), followed by the rule-wiring
entries `"<event>=<filter_id>"` sorted alphabetically by event. Only the spatial edit/access
events `block`, `block_break`, `block_place`, `enter` are recorded; `block_physics`, `use` and
`kit` are mechanic-level and omitted (they never change a category and would only add noise).

### 1.4 Derivation precedence

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
   excluded — reliable, see §1.6).
5. **apply message** (§1.6.1) — the author's `message` text names the zone explicitly
   ("…enter the enemy's **spawn**!" → spawn; "…edit the **wool room**!" → wool_room;
   "…break the **spawner**" → mechanic). High-signal; applied before the build sweep so a
   spawn-protection zone sitting inside the void-complement is not swallowed as build.
6. **spawn** ← the **spawn-floor pattern** (`block_break` restricted to a material — iron *or* gold
   — **and** `block_place` denied: players may only break the spawn floor), or a **spawn-protection
   kit** (a `kit`/`lend_kit` whose id names spawn protection/regen — resistance + regeneration in
   spawn, e.g. `spawn-protection`, `spawn-regen`; excludes `leave-`/`remove-spawn` kits that strip
   the buff *outside* spawn; e.g. mushroom_gorge `base-sides`).
7. **build** ← void-structure **and** permissive placement (§1.5).
8. **mechanic** ← non-wool spawner `spawn_region` (step 3).
9. **spawn | wool_room** ← `enter=only-<team>` rules, disambiguated **by the region's own name**:
   a `*spawn*` name → spawn, a `*wool*`/`*room*`/`*monument*` name → wool_room; else left for the
   name heuristics (step 10). (Regions in `spawns[]` are already `spawn` from step 1, so this only
   resolves the remaining `only-<team>` zones.)
10. **name heuristics** on **primitives only** (not on compounds): `*monument*`, `*wool*`/`*room*`/
    `wr`-token (→ `wool_room`; `wr`/`wrs`/`wr2` match as **whole tokens** only — not the substring of
    `wrapper`, a void-mechanic region), `*spawner*` (→ `mechanic`, checked before `*spawn*`), `*spawn*`
    (→ `spawn`).
    **No `build` name heuristic** — `lane`/`bridge` names are *not* taken as build (§1.5 derives
    build from void structure; a lane with no void parent is a movement mechanic, not build space).
11. **Constrained recursion** (§1.7).
12. **mechanic (fallback)** ← `renewables[].region_id`, and apply rules with a `velocity`/`kit`/
    `lend_kit` action (regen zones, portal-boost pads, kit dispensers). Applied **last**, claiming
    only what is otherwise `other` — a wool room with wool regen stays a wool room, but a `portals`
    union or an `iron-regen` zone becomes `mechanic`. Never relabels a `negative`/`complement`
    rule-wrapper (it keeps `rule_container`).

Do **not** use `block` / `block-place` filter targeting as a `category` signal — record it
as a `roles.rules` entry instead (§1.3). (Treating it as "build" produces near-all false
positives — e.g. `spawns` would be tagged build merely because it carries an iron-only rule.)

### 1.5 Build regions: static and time-gated

Build regions are derived from **rule structure**, not naming (`build` is <2% of regions by
name alone). PGM grants buildability to columns with a block at Y=0; authors carve buildable
space out of the void and enforce the boundary with a filter.

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
footprints without enumerating their names. (Intended subtypes — island-like → `footprint`;
bridge/lane/gap → `traversal` — are **not yet emitted**; build regions carry `subtype = null`.)

**Time-gated (dynamic).** A build region whose `block` rule is gated by an `after`/`time`/
`pulse` filter opens mid-match (anti-stalemate). Category is still `build`; add the
**`time_gated`** role with the duration. Examples: `add-water-lane` (30s), `golden_drought_vi`
`60m/80m/100m/120m`, `mame_…` `after-30m/60m/90m`. (This is the same structural pattern behind
filter-use-cases §2.6.1 "time-gated features" — that section covers the smaller set of *non-build*
time-gated mechanics, e.g. spawn kit upgrades after 20 minutes.)

**Permissive placement (positive form).** The inverse of the void-negative: instead of denying
placement outside the build area, a map can *allow* placement inside it. A **region** (not a filter)
named as the value of a `block_place` *or* `block` rule ("you may place where you are inside this
region") **is** the build area, and its children are build. Vertex's global rule
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

**Pure-geometry.** A non-buildable region with `block-place=never` and no void filter is a
`rule_container` (lockdown target), not a build region.

Caveat: a `lane` with **no** void parent and **no** rule (e.g. `ad_astra`'s `water-lanes`) is
*not* build — it's likely a movement mechanic. The structural rule correctly excludes it;
naming alone would not.

### 1.6 `enter`-filter polarity (spawn vs wool disambiguation)

`apply enter=<filter>` rules mark protected zones. Resolving the filter's team polarity:

- **`enter=not-<team>` → `wool_room`** — the team is *excluded* (defender can't enter their
  own wool room). Reliable: 50/52 = 96% in the corpus.
- **`enter=only-<team>` → ambiguous** — 447 spawn vs 445 wool_room corpus-wide. Some maps use
  `only-<owner>` to let *only* the owner into their wool room (opposite convention). Resolve
  with `spawns[]` / monument adjacency / name; otherwise leave as a neutral protected zone.

The polarity also reveals the **owning team** of a wool room (the excluded team), corroborating
the derived owner from the wool model.

#### 1.6.1 Apply-message signal

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
sweep (§1.4 step 5) so a message-named spawn/wool zone inside the void-complement keeps its identity.

The **spawn-floor pattern** (§1.4 step 6) is the structural twin of the "…break iron/gold in spawn"
message: spawn platforms let players break only the floor material (iron *or* gold — both occur)
while placement is fully denied (`block_break`=material filter **and** `block_place`=deny/never).

*Relation to §2's use-case clusters:* this table counts *message-text* occurrences (3204 messages,
by substring) and drives **categorization** when structural signals are absent. Cluster 1.1 "Protect
enemy spawn from entry" (277/345 maps, 80%) and Cluster 1.2 "Lock own wool room" (333/345 maps, 96%)
in §2.1 count the same underlying gameplay pattern a different way — **by map**, with the authored
XML shapes — for the *use-case* narrative (what the editor should suggest), not for categorization.
Same corpus phenomenon, two different statistics for two different jobs: don't expect the percentages
to reconcile numerically.

### 1.7 Compound and recursion rules

Compounds give PGM meaning and are needed for round-trip, but they break naive categorization.

- **`negative` is a pure container.** It resolves to `other`, is flagged `rule_container`, and is
  never assigned a gameplay category from its name (`not-spawns` is **not** a spawn).
- **`complement` inherits its base.** Unlike a `negative`, a `complement` resolves to the category
  of its first child (the positive base) and is **not** flagged `rule_container` — so a `spawns`
  union over a `complement` keeps `category = spawn`.
- **Compound categories resolve bottom-up.** A compound's category is *derived from its children*,
  never pushed down onto them: a `union` takes the shared category of its named, same-category peers
  (≥2, reached through anonymous intermediate unions) and otherwise falls back to its base child; a
  `mirror`/`translate` takes its `source_id`'s category. Resolution **never overwrites** a child's
  own direct category and **never** crosses a `negative` (which stays `other`).
- A child reached only through a `rule_container` keeps its own intrinsic category (or `other`
  + role), so wool monuments and spawn areas are never relabeled by the wrapper around them.

### 1.8 Worked examples

**`annealing_iv` (4-team)**

| region | type | category | roles |
|---|---|---|---|
| `blue-spawn-point` | cylinder | spawn (subtype `point`) | |
| `blue-spawn` | rectangle | spawn (subtype `protection`) | `enter=only-blue` |
| `blues-woolroom` | union | wool (subtype `room`) | `enter=not-blue` (blue defends), block rule |
| `blue-team-red-wool` | block | wool (subtype `monument`) | |
| `blue-wool-spawn` | cuboid | wool (subtype `spawner`) | |
| `build-area` | union | build | |
| `not-build-area` | negative | — | `rule_container` (void enforcement) |
| `spawns` | union | spawn (base of its complement child) | iron-only block rules (no `rule_container`, no `rule_group`) |
| `not-spawns` | negative | — | `rule_container` |

Result: 34/35 named regions categorized; only `blocks-filter-region` is genuinely `other`.

**`icecream_sandwiched_ii` (time-gated build)**

```
<any id="water-lane-building"><after id="add-water-lane" duration="30s" .../></any>
<union id="water-lanes"> → blue/red-water-lanes → lime/cyan/yellow/orange-water-lane
<apply region="building-water-lanes" block="water-lane-building" message="...void area!">
```

`water-lanes` and children → `category = build` (subtype `null` — `traversal` is not yet emitted),
`roles = [time_gated=30s, block=water-lane-building]`. The full wiring round-trips; the model
surfaces *that it is a build region* and *that it opens after 30s*.

### 1.9 Known limitations

- `enter=only-<team>` is ambiguous (spawn vs wool_room) and falls back to `spawns[]`/name; a small
  set of regions stay neutral-protected.
- `mechanic` is a single bucket with **no `subtype` emitted yet** (kit/shop/renewable are
  low-prevalence; planned as free-text, not a rigid enum).
- Build `subtype` (`footprint` vs `traversal`) is **not emitted yet** — build regions carry
  `subtype = null`. When added it will be best-effort from geometry/naming.

Verification is by synthetic-fixture unit tests in `tests/PgmStudio.Pgm.Tests/RegionCategorizerTests.cs`
(per the repo's "synthetic fixtures only" rule — no real game files under `tests/`) plus the
`tools/PgmStudio.RoundTrip --categorize` corpus parity guard against the Python `derive_region_facets`
oracle. There is no checked-in `tests/fixtures/region_categories/` directory in this repo.

### 1.10 Persistence: derived category, full-replace save, draft bucket

How a region travels from the editor to the database and back — why its **category is never
stored**, why **all region rows are dropped and rewritten on every save**, and how that shaped the
**draft bucket** for freshly drawn regions. This is about *where the data lives and when it's
computed*, distinct from §1.1–1.9's *what the categories mean*.

#### 1.10.1 What is persisted

A map's regions live as **relational rows** in the `region` table (the hybrid model — see
`CLAUDE.md`): `region_key`, `type`, `bounds_json`, `coords_json`, `child_ref_ids_json`, `source_id`.
That's the **canonical PGM geometry/structure** and nothing else.

**The category is *not* a column and is *not* stored anywhere.** Neither is `subtype` or the rule
`wiring` — they are all **derived on read** by `RegionCategorizer` from *usage* (which `spawns[]` /
`wools[]` / `spawners[]` / `apply_rules[]` reference the region). This is the contract's rule:
*"category is derived, never persisted; `region_categories` is only a store of user overrides."*

#### 1.10.2 The save path is *entity-replace* — and why

`MapWriter.SaveDocAsync(mapId, doc)` does, in one transaction:

1. `DeleteEntitiesAsync(mapId)` — **delete every** author/team/kit/region/filter/wool/spawn/spawner
   row for the map (features and `map_artifact` blobs are **kept**).
2. `Deserializer.FromDict(doc)` → rebuild the domain `MapXml` from the document dict.
3. `WriteEntitiesAsync` + `WriteWoolsFromDocAsync` — **re-insert** all rows from scratch.

So **every edit drops and recreates all region rows.** This is deliberate:

- **The codec is the source of truth.** Writes go `doc dict → FromDict → rows` through the exact same
  serializer used for round-trip parity (350/350). There is no second "diff/patch the rows" code path
  to keep correct; replace-from-document is provably consistent with the canonical format.
- **No identity churn that matters.** Rows use a surrogate `id` (identity), but regions are addressed
  by **`region_key`** (the map-level id like `build-area` or `ui-rect-1`), which is **stable across the
  replace** because it comes from the document. References (`child_ref_ids_json`, `source_id`, spawn/
  wool region keys) are by key, so they survive.
- **Editor-only metadata has nowhere to ride.** `FromDict` only understands the PGM map format, so
  anything not in that format (e.g. `region_categories`) is **dropped** — `RegionEditor.cs` notes
  *"region_categories is an editor-only undo hint; it is not persisted (FromDict drops it)."*

**Key implication:** you **cannot** stash editor state in a column on `region` (or any entity table) —
the next save wipes it unless it round-trips through the codec, which would pollute the canonical
format. Editor-only state must live **outside** the entity-replace path. (This is exactly why the
draft bucket in §1.10.5 is an artifact blob, not a region column.)

#### 1.10.3 The read path + when derivation happens

`MapReader.ReadDocAsync(map)` rebuilds the full document dict from the rows (regions, filters,
spawns, wools, spawners, apply-rules…). It does **not** produce `region_categories`.

**Derivation is not an event — it is recomputed on every read.** `RegionCategorizer` runs, statelessly,
only when an endpoint asks for it; the result is returned and thrown away (never cached, never written
back). Call sites:

| endpoint | what it derives |
|---|---|
| `GET /regions/tree` (`AuthoringEndpoint`) | `Categorize` (grouping) + `DeriveFacets` (per-node category / subtype / wiring) — **the one the editor sidebars + canvas hit on every load/reload** |
| `GET /regions` (`AnalysisEndpoints`) | `DeriveFacets` (facets + counts) |
| `GET /regions/authoring` | `Categorize` |

So when wiring changes (attaching a filter / spawn / wool → that saves an `apply_rule`/`spawn`/
`wool` row), there is nothing to "re-run": the **next** `/regions/tree` fetch rebuilds the doc *with*
that row and re-derives, so the region's category/subtype update automatically.

A freshly drawn region reads as **`other`** for the same reason: at draw time the saved doc has the
region row but **no** rule/spawn/wool referencing it, so the next derivation has no signal — until the
wiring is saved (§1.10.5) does the draft bucket lets that region show up before then.

#### 1.10.4 `Categorize` vs `DeriveFacets` (overrides vs pure derivation)

- `DeriveFacets(doc)` → `{id: {category, subtype, roles}}` — **pure derivation**, no overrides. This is
  the parity-checked output and what each **tree node** carries (`node.category`, `node.subtype`,
  `node.wiring`).
- `Categorize(doc)` → flat `{id: category}` = `DeriveFacets` **+ `region_categories` overrides** applied
  on top. Used for tree **grouping**.

Because `region_categories` isn't persisted (§1.10.2), the two agree in practice today. The **canvas**
filters by `node.category` (the pure value), so a drawn region (`other`) needs the separate signal in
§1.10.5 to show.

#### 1.10.5 The draft bucket: showing drawn-but-unwired regions

A region drawn in an activity is correctly `other` (unwired). To still show it **in the step it was
drawn in**, without faking the derived category, the editor keeps a small **sidecar**:

- **Store:** a `region_drafts_json` **`map_artifact`** blob = `{region_key: editor_step}` where
  `editor_step ∈ {teams, objective, build}`. It lives **outside** the entity-replace codec, so it
  **survives `SaveDocAsync`** (`DeleteEntitiesAsync` keeps artifacts) and is **never** part of the PGM
  document the codec/categorizer see. `RegionDraftStore` (in `RegionEndpoints.cs`) reads/writes it.
- **Write:** `POST /regions` (and the orbit follow-up route) carry `draft_step`; after the edit, the
  endpoint tags `{newKey: step}` (and each orbit counterpart) into the blob.
- **Read:** `/regions/tree` loads the blob, **prunes** keys whose region no longer exists, and attaches
  `node.draft_step` via `EncodeTree`/`EncodeNode`.
- **Render:** each draw activity passes its `DraftStep`; the sidebar shows a **"Draft"** section of nodes
  with `draft_step == myStep && category == "other"`, and the canvas renders those too.
- **Graduate:** a node is shown as a draft **only while its derived category is still `other`**. The
  moment the wiring lands, the next `/regions/tree` derives its real category/subtype → it **leaves** the
  Draft section and the canvas's draft set, and appears in its proper subtype section via normal
  derivation. The stale `draft_step` is then ignored (and pruned). The draft bucket is purely a
  **bridge**; it never competes with or mutates the derivation.

**End state (once wiring is attached):** a configured activity has the *same base data as the real
corpus maps* — geometry rows + wiring (filters/spawns/wools/apply-rules) — and the draft bucket is
empty, because every region is now classified by derivation.

#### 1.10.6 How the canvas displays a region: wired vs just drawn

The canvas (`studio-canvas.js` → `EditorCanvas`) renders the **primitive** nodes (compounds excluded;
their primitive children carry the classification) selected by this rule, walking the **whole** tree
(objective/spawn/build regions nest inside rule-containers in the "other" group, so a group-name filter
would miss them):

```
render n  ⟺  n is primitive AND (
                 n.category ∈ {the activity's categories}        // WIRED
              OR (n.draft_step == this step AND n.category == "other")  // JUST DRAWN
             )
```

- **Wired region** — e.g. a rectangle referenced by `enter=only-red`, or a region in `spawns[]`. Its
  derived `category` (`spawn`/`wool`/`build`) matches the activity, so it renders via the **first** clause.
  No draft entry is needed or used.
- **Just-drawn region** — `category == "other"`, but it carries `draft_step` from the activity that drew
  it, so it renders via the **second** clause. Its orbit counterparts are tagged the same way and
  render alongside it.
- **A drawn region that gets wired** — once a rule/spawn/wool references it, derivation gives it a real
  category; it now matches the first clause and **drops out** of the second (the `category == "other"`
  guard), so it never double-renders. The two clauses are mutually exclusive by construction.

The mutual-exclusion guard (`category == "other"` on the draft clause) is what lets a region move from
"drawn" to "wired" with no flicker, no duplicate, and no cleanup step.

---

## 2. The filter model

Analysis of 345 CTW maps (CommunityMaps + PublicMaps), 3 946 apply rules, 7 772 filters. §2.1 maps
gameplay design questions to the XML patterns that implement them, organised by cluster and ordered
by map-level prevalence — the *intent* view. §2.2 is the *vocabulary*: which filter types attach to
which events, and how they compose — the reference for the editor's authoring routes and the wiring
UI (§3). *(Corpus figures re-verified 2026-06-10.)*

### 2.1 Usage patterns by intent

#### Cluster 1 — Access Control (Who can go where)

**1.1 Protect enemy spawn from entry** — *"Prevent the enemy team from walking into your spawn."*

**Prevalence:** 277/345 maps (80%)

**Pattern:** `enter` filter on a spawn region, restricting to the owning team only.
The filter is typically a named `<team>` filter. Message shown is always a denial.

```xml
<filters>
    <team id="only-blue">blue-team</team>
    <team id="only-red">red-team</team>
</filters>
<regions>
    <apply enter="only-blue" region="blue-spawn" message="You may not enter the enemy's spawn!"/>
    <apply enter="only-red"  region="red-spawn"  message="You may not enter the enemy's spawn!"/>
</regions>
```

**Semantic question:** "Which team owns this spawn, and which teams are denied entry?"

**Variants:**
- Enter filter is the *owning team* (enemy is implicitly denied by default)
- Enter filter is a `<not>` wrapping the owning team (explicit: non-members denied)
- Some maps use `deny(blue-team)` inline shorthand

**1.2 Lock own wool room — team cannot enter their own** — *"Prevent a team from entering their own
wool room, forcing them through enemy territory."*

**Prevalence:** 333/345 maps (96%) — the single most universal rule

**Pattern:** `enter` filter on the wool room region, permitting only the *opposing* team.
This is the defining rule of CTW: your own team's wool room is off-limits to you.

```xml
<!-- Only blue may enter red's wool rooms (blue must steal red's wool) -->
<apply enter="only-blue" region="red-wool-rooms" message="You may not enter your team's own wool room!"/>
<apply enter="only-red"  region="blue-wool-rooms" message="You may not enter your team's own wool room!"/>
```

Sometimes combined into a single rule with `block` + `enter` + `use`:
```xml
<!-- Annealing IV — one rule covers entry, block editing, and chest use -->
<apply enter="not-blue" region="blues-woolroom" message="You may not enter your own wool room!"/>
```

**Semantic question:** "Which team owns this wool room?" (the opposing team gets in; the owning team
is excluded.) See §1.6.1 for how this same pattern's `message` text feeds region *categorization*
(distinct statistic, same underlying phenomenon).

**1.3 Right-click protection inside wool room / spawn** — *"Prevent opening chests or using
interactive blocks in a restricted area."*

**Prevalence:** 192/345 maps (55%)

**Pattern:** `use` filter (right-click events) on wool room or spawn chest regions.
Most commonly paired with `enter` on the same region. The use filter is usually the same
team filter as the enter filter, or `deny-beacon` / `deny(chest)`.

```xml
<!-- Tumbleweed — wool room chests locked from owning team -->
<apply use="only-blue" region="red-wool-rooms"/>
<apply use="only-red"  region="blue-wool-rooms"/>

<!-- Epsilon — beacon access locked -->
<apply use="deny(beacon)" region="beacon-area" message="You may not use the beacon!"/>

<!-- chest protection only -->
<apply use="deny-chest" region="wool-rooms" message="You may not open this chest!"/>
```

**Semantic question:** "Who can interact with containers/buttons/beacons in this zone?"

#### Cluster 2 — Block Editing Rules (What can be placed and broken)

**2.1 Spawn block protection — iron regeneration** — *"Players can only break iron blocks at spawn;
iron regenerates automatically."*

**Prevalence:** 179/345 maps (51%)

**Pattern:** `block-place` + `block-break` with asymmetric filters on the spawn region.
Break allows only iron blocks (`only-iron`). Place allows only world-placed iron (i.e. the
renewable plugin placing blocks back), achieved by combining `material:iron block` with
`<cause>world</cause>`.

```xml
<!-- Tumbleweed -->
<filters>
    <material id="only-iron">iron block</material>
    <all id="only-iron-regen">
        <material>iron block</material>
        <cause>world</cause>
    </all>
</filters>
<regions>
    <apply block-break="only-iron" block-place="only-iron-regen"
           region="spawns" message="You may not edit the spawn areas!"/>
</regions>
```

Paired with a `<renewable>` block so iron that is broken regenerates:
```xml
<renewables>
    <renewable region="spawns" rate="1" renew-filter="only-iron" replace-filter="only-air"/>
</renewables>
```

**Semantic question:** "What blocks are breakable at spawn? Should iron regenerate?"

**Variants:**
- Gold blocks at spawn (some maps use gold instead of iron)
- Both iron and gold (`any` filter)
- `deny-players` on place (no player placement at all, only renewables)

**2.2 Wool room block protection — restrict editing to team-specific blocks** — *"Players can only
edit certain blocks inside a wool room."*

**Prevalence:** 335/345 maps (97%) — almost always present alongside entry restriction

**Pattern:** `block` filter on wool room region, permitting only the opposing team's
allowed blocks. Often uses a named composite filter (`woolrooms-filter`) that includes
specific material types.

```xml
<!-- Outback: team-specific filter combining team check + material whitelist -->
<filters>
    <all id="yellows-woolrooms-filter">
        <team id="only-yellow">yellow-team</team>
        <filter id="woolrooms-filter"/>
    </all>
    <any id="woolrooms-filter">
        <material>web</material>
        <material>wood:0</material>
        <material>stained clay:4</material>
        <!-- ... other allowed materials ... -->
    </any>
</filters>
<regions>
    <apply block="yellows-woolrooms-filter" region="yellows-woolrooms"
           message="You may not edit the wool room!"/>
</regions>

<!-- Simple variant (Tumbleweed) — team is the only constraint -->
<apply block="only-red"  region="blue-wool-rooms" message="You may not edit your team's own wool rooms!"/>
<apply block="only-blue" region="red-wool-rooms"  message="You may not edit your team's own wool rooms!"/>
```

**Advanced variant — original map state protection:**
Some maps use a `<blocks>` filter that compares the current block against the original
world state, allowing players to remove only player-placed blocks:

```xml
<!-- Annealing IV: only player-placed blocks in woolroom are breakable -->
<filters>
    <deny id="woolrooms-break-filter">
        <blocks region="blocks-filter-region">
            <not>
                <any>
                    <material>air</material>
                    <material>stained glass pane:3</material>
                    <!-- original structural blocks that cannot be touched -->
                </any>
            </not>
        </blocks>
    </deny>
</filters>
<regions>
    <apply block-break="woolrooms-break-filter" region="woolrooms"
           message="You may not edit the wool room!"/>
</regions>
```

**Semantic question:** "Which team can edit this wool room? Which block types are permitted?"

**2.3 Full block lockdown — no editing at all** — *"No block placement or breaking permitted in
this region."*

**Prevalence:** 235/345 maps (68%)

**Pattern:** `block="never"` (static deny). Used for observer spawns, spawners,
structural features, and areas that must be preserved.

```xml
<apply block="never" region="obs-spawn"   message="You may not modify the observer's spawn!"/>
<apply block="never" region="spawners"    message="You may not obstruct the spawners!"/>
<apply block="never" region="spawn-protection" message="You may not modify the spawn areas!"/>

<!-- Place-only lockdown (no building, breaking still allowed) -->
<apply block-place="never" region="bottom-no-build" message="You may not build here!"/>
```

**Semantic question:** "Should this region be fully read-only? Read-only for placement only?"

**2.4 Void / outside-map protection** — *"Prevent players from building into the void or outside
the intended play area."*

**Prevalence:** 97/345 maps (28%)

**Pattern:** `block-place` restricted to `deny(void)` (blocks placed where the underlying
column is void/air at Y=0 are denied). `block-break` often paired with a different
filter that still allows breaking certain surface blocks.

```xml
<!-- Simple — applies everywhere or on a "not-build" region -->
<apply block-place="deny(void)" message="You may not edit the void here!"/>

<!-- With separate break filter (void-touching surface blocks can be broken) -->
<filters>
    <any id="block-break-void-filter">
        <all>
            <any>
                <material>leaves</material>
                <material>log</material>
            </any>
            <void/>       <!-- only breakable if touching void -->
        </all>
        <not id="block-place-void-filter">
            <void/>
        </not>
    </any>
</filters>
<regions>
    <apply block-place="block-place-void-filter"
           block-break="block-break-void-filter"
           region="not-build-region"
           message="You may not edit the void!"/>
</regions>

<!-- Height ceiling variant -->
<apply block-place="never" region="ceiling" message="You have reached the maximum build height!"/>
```

**Semantic question:** "Where is the playable boundary? What is the void protection region?"
See §1.5 for how this maps to the `build` region category (the void-enforcement wrapper vs. its
carved-out buildable children).

**2.5 Block physics denial — stop water, lava, redstone from spreading** — *"Prevent certain blocks
from triggering physics updates in wool rooms or spawn."*

**Prevalence:** 57/345 maps (16%)

**Pattern:** `block-physics` filter on wool rooms or the whole map. The filter is almost
always a `<deny>` wrapping an `<any>` of specific materials.

```xml
<!-- Most common: deny redstone wire updates -->
<filters>
    <deny id="deny-redstone">
        <any>
            <material>redstone wire</material>
            <material>redstone lamp on</material>
        </any>
    </deny>
</filters>
<regions>
    <apply block-physics="deny-redstone" region="woolrooms"/>
</regions>

<!-- Wool room with ladder + trap door physics denial -->
<apply block-physics="deny-ladder" region="wool-rooms"/>

<!-- Lava flow prevention -->
<filters>
    <deny id="deny-lava">
        <any>
            <material>lava</material>
            <material>stationary lava</material>
        </any>
    </deny>
</filters>
<apply block-physics="deny-lava" region="whole-map"/>
```

**Semantic question:** "Should redstone / lava / water be allowed to flow in this region?
Which block physics events should be frozen?"

#### Cluster 3 — Kit Assignment (Equipment by zone)

**3.1 Resistance kit reset — remove resistance effect outside spawn** — *"Players lose
spawn-protection resistance when they leave the spawn area."*

**Prevalence:** 58/345 maps (16%)

**Pattern:** `kit` applying a reset kit to the complement of the spawn region.
The kit itself clears only effects (not inventory). Region is typically `not-spawns`.

```xml
<!-- kit clears potion effects when player is outside spawn -->
<apply kit="reset-resistance-kit" region="not-spawns"/>
```

The kit definition typically:
```xml
<kit id="reset-resistance-kit">
    <!-- clears resistance effect; items left intact -->
</kit>
```

**Semantic question:** "Should spawn protection apply only inside the spawn region?"

**3.2 Wool room kit — extra equipment for wool room attackers** — *"Players entering a wool room
receive a specific kit (e.g. shears, special tools)."*

**Prevalence:** 31/345 maps (8%)

**Pattern:** `kit` applied to attackers entering the wool room. Often filtered to
the opposing team only via `filter=`.

```xml
<apply kit="wool-gear" region="red-wool-rooms" filter="only-blue"/>
<apply kit="wool-gear" region="blue-wool-rooms" filter="only-red"/>
```

**3.3 Zone-based kit swap — different gear in different areas** — *"Players receive (or keep) a
specific kit while in a designated zone."*

**Prevalence:** 9/345 maps (2%)

**Pattern:** `lend-kit` on a zone region. The kit is given on entry and removed on exit —
useful for loadout changes tied to specific map areas (defence zones, special corridors).

```xml
<!-- new_life_ctw: different kit for defenders vs attackers -->
<apply lend-kit="defend-kit" region="blue-defense-region" filter="only-blue"/>
<apply lend-kit="attack-kit"  region="blue-attack-region"  filter="only-blue"/>

<!-- bloom: healing area gives resistance -->
<apply region="spawns-healing-area" lend-kit="resistance-kit"/>
```

**Semantic question:** "Should players have different equipment in this specific zone?"

#### Cluster 4 — Movement / Launch Mechanics

**4.1 Jump pads — velocity launch zones** — *"Players who walk through this region are launched in
a direction."*

**Prevalence:** 15/345 maps (4%)

**Pattern:** `velocity` applied to a region. The vector encodes direction and magnitude.
Sometimes filtered to a specific team or match phase.

```xml
<!-- Simple upward pad -->
<apply velocity="0.0,3.0,0.0" region="jumppads"/>

<!-- Directional pad -->
<apply velocity="0,2,-4.8" region="blue-jump-pads"/>
<apply velocity="0,2,4.8"  region="red-jump-pads"/>

<!-- Conditional: only during match start -->
<apply velocity="0,0.5,50"  filter="all(match-start,red-team)" region="blue-icarus-plane"/>
<apply velocity="0,0.5,-50" filter="all(match-start,blue-team)" region="red-icarus-plane"/>
```

**Semantic question:** "Where are the jump/launch pads? What direction and strength?"

**4.2 Map boundary — prevent leaving the play area** — *"Players cannot leave the designated play
area."*

**Prevalence:** 4/345 maps (1%) — rare but present

**Pattern:** `leave="never"` on the play boundary region.

```xml
<apply leave="never" region="playspace" message="You cannot exit the map."/>
<apply leave="never" region="sides"     message="You may not exit the playing field!"/>
```

#### Cluster 5 — Renewable Resources

**5.1 Iron / gold block renewal at spawn** — *"Iron (or gold) blocks at spawn regenerate after being
broken."*

**Prevalence:** 179/345 maps (51%) — often paired with Cluster 2.1

This is primarily a `<renewables>` declaration, but requires a matching `<block-drops>` rule
so the renewable system fires correctly:

```xml
<block-drops>
    <rule region="spawns" filter="only-iron" wrong-tool="false">
        <drops><item material="iron block"/></drops>
        <replacement>iron block</replacement>
    </rule>
</block-drops>
<renewables>
    <renewable region="spawns" rate="1"
               renew-filter="only-iron"
               replace-filter="only-air"/>
</renewables>
```

**Semantic question:** "Which blocks regenerate? What region? What rate?"

#### Cluster 6 — Advanced / Special Mechanics

**6.1 Time-gated features** — *"Something changes or unlocks after a certain amount of time into
the match."*

**Prevalence:** ~5 maps

**Pattern:** `<time>` or `<after>` filters combined with apply rules, kit grants, or
velocity launches. Often used with variable-based locking.

```xml
<!-- factorio maps: spawn kit upgrades after 20 minutes -->
<filters>
    <time id="20m-passed-red">20m</time>
</filters>
<apply kit="amended-spawn-kit" filter="20m-passed-red" region="enter-red"/>
```

See §1.5 for the region-categorization treatment of the (structurally related but distinct)
time-gated **build** regions, which carry the `time_gated` role.

**6.2 Original map state protection (player-placed vs map-original)** — *"Players can only break
blocks they placed; original map blocks are protected."*

**Prevalence:** 4/345 maps

**Pattern:** `<blocks region="...">` filter compares the current world state against
the region's original block types. Only map-original blocks are protected; player-placed
blocks can be freely removed.

```xml
<filters>
    <deny id="only-wool-room-break">
        <blocks region="wool-room-blocks">
            <not>
                <any>
                    <material>air</material>
                    <material>web</material>
                </any>
            </not>
        </blocks>
    </deny>
</filters>
<regions>
    <apply block="only-wool-room" region="wool-rooms"
           message="You may only modify blocks placed by a player here!"/>
</regions>
```

**6.3 Block placement against specific surfaces (anti-climb)** — *"Prevent players from placing
blocks against certain structures to climb over them."*

**Prevalence:** 3/345 maps (nyxis-type maps)

**Pattern:** `block-place-against` filter on anti-wall-climbing regions.

```xml
<apply region="anti-wall-climbing-region"
       block-place-against="anti-wall-climbing-filter"
       message="You may not directly place blocks against this part of the map."/>
```

#### Summary: UX Question Mapping

The table below maps each semantic use case to a proposed editor question,
sorted by map-level prevalence.

| Prevalence | Use Case | Proposed UX Question |
|---|---|---|
| 97% | Wool room block editing | "Which team can edit this wool room?" |
| 96% | Wool room access | "Which team owns this wool room?" (derives who is excluded) |
| 80% | Spawn entry protection | "Which team owns this spawn?" |
| 68% | Full block lockdown | "Should this region be uneditable?" |
| 55% | Right-click protection | "Should containers/buttons be locked in this region?" |
| 51% | Spawn iron protection + renewal | "Should iron blocks regenerate at this spawn?" |
| 28% | Void/boundary protection | "What is the play boundary region? Allow void placement?" |
| 16% | Resistance kit reset | "Should spawn resistance clear when players leave spawn?" |
| 16% | Block physics denial | "Should redstone/lava/water physics be frozen here?" |
| 8% | Wool room kit | "Should attackers entering this wool room receive a kit?" |
| 4% | Jump pads | "Where are the jump pads? Direction and strength?" |
| 2% | Zone-based kit swap | "Should players have a different kit in this zone?" |

#### Recurring Filter Patterns by Name

These filter IDs appear across dozens of maps with near-identical semantics,
showing strong convention around CTW XML authoring:

| Filter pattern | Semantics |
|---|---|
| `only-<team>` | `<team>team-id</team>` — the named team |
| `not-<team>` | `<not><team>...</team></not>` — all other teams |
| `only-iron` | `<material>iron block</material>` |
| `only-iron-regen` / `only-iron-cause-world` | `<all><material>iron block</material><cause>world</cause></all>` |
| `only-iron-regen` (place) + `only-iron` (break) | The canonical spawn renewal pair |
| `deny-chest` | `<deny><material>chest</material></deny>` |
| `deny(void)` | Inline shorthand; blocks on void columns |
| `woolrooms-filter` | `<blocks region="...">` or material whitelist — wool room allowed materials |
| `<team>-woolrooms-filter` | `<all><team>...</team><filter id="woolrooms-filter"/></all>` |
| `block-place-void-filter` + `block-break-void-filter` | Void boundary pair |
| `deny-physics` / `deny-redstone` | `<deny><any><material>redstone wire</material>...</any></deny>` |

### 2.2 Filter vocabulary & event matrix (what attaches to what)

Reference for the editor's authoring routes and the wiring UI (§3): the realistic filter
vocabulary, which filter types attach to which apply events, and how composites are built. Counts
are corpus-wide (345 maps, 7 772 filters, 3 946 apply rules) as of 2026-06-10.

#### A.1 Filter type frequency

Leaf conditions dominate (`material` 2 751), then the composers (`all` 902, `any` 727, `not` 522,
`deny` 365) and `team` 767. The long tail (`variable`, `time`, `carrying`, `blocks`, `region`,
`offset`, `objective`, `after`/`pulse`, `class`, `kill-streak`, …) is the advanced surface.

| tier | types (by count) |
|---|---|
| **core leaves** | `material` 2751 · `team` 767 · `never` 342 · `always` 340 · `cause` 217 · `void` 194 |
| **composers** | `all` 902 · `any` 727 · `not` 522 · `deny` 365 · (`one`, `allow` rare) |
| **conditional / advanced** | `variable` 174 · `time` 88 · `alive` 71 · `carrying` 55 · `blocks` 50 · `participating` 39 · `offset` 31 · `region` 30 · `objective` 30 · `wearing` 18 · `after` 13 · `completed` 12 · `pulse` 8 · `class`/`spawn`/`grounded`/`kill-streak`/… ≤6 |

#### A.2 Event × filter-type — *what is sensible where*

Each apply event checks a different thing, so each pulls a different filter vocabulary. This is the
crux of "filters that make sense": a `material` filter on `enter` is meaningless (it inspects the
*block*, but `enter` inspects the *player*) — and indeed **never occurs** in 345 maps. Top resolved
filter types per event (`deny()`/`not()` = inline descriptor; `region-or-id` = a region used as a
filter or a builtin):

| event (uses) | dominant filter types | reads |
|---|---|---|
| `enter` (1535) | **team** 1064 · region-or-id 163 · deny()/not 120/61 | who may walk in — **team-based** |
| `use` (462) | **team** 208 · not 75 · deny 51 | right-click/containers — **team-based** |
| `block` (1391) | **never** 430 · all 289 · deny 343 · not 106 | combined place+break — lockdown / composite |
| `block_place` (532) | all 141 · **never** 103 · not 95 · deny 166 | placement restriction / void |
| `block_break` (464) | **material** 185 · any 102 · deny 51 · all 49 | break-only-X (iron/gold spawn floor) |
| `block_physics` (76) | **deny** 43 · never 17 | freeze water/lava/redstone |
| `filter` (76, kit/velocity cond.) | **team** 45 · all 17 | conditional kit/jump pad |
| `leave` (5) · `block_place_against` (3) | never/deny | rare (leave-spawn buff; anti-climb) |

So the **sensible default vocabulary per event** is: `enter`/`use` → team (and team composites);
`block*` → `never` / `material` / `all`/`any`/`deny`/`not` over those; `block_physics` → `deny`;
`filter` (the kit/velocity condition) → team/`all`.

#### A.3 How composites are built

Composers reference children by id; the children's types show the real shapes:

| composer | common child types | typical meaning |
|---|---|---|
| `all` (AND) | team · any · material · not · cause · void | "this team **and** this material/condition" (wool-room edit filter) |
| `any` (OR) | **material 2095** · team · all | "**any of** these block types" (editable-material whitelist) |
| `deny` (= NOT-allow) | any · material · all · participating · team · void | invert a condition (deny chests, deny void, deny physics) |
| `not` (NOT) | any · team · void · all · time · objective | "all **other** teams", "**not** void", time-gated negation |

#### A.4 On "nonsensical" filters & stackability

Filters are **freely composable conditions** — there is no type that is inherently invalid, and the
editor deliberately does **not** forbid combinations: it only rejects *dangling references*
(a child filter / region that doesn't exist). "Sense" is a function of **event + region + intent**,
not the filter type alone, and stacking (`all`/`any`/`not`/`deny`) makes otherwise-odd leaves
meaningful (e.g. `material` is meaningless on `enter`, but `all(material, team)` on `block` is the
canonical wool-room rule). The matrix in A.2/A.3 is therefore a **suggestion/soft-warning** source
for the wiring UI (§3) — surface the per-event vocabulary first, and *warn* (don't block) on
pairings that never appear in the corpus — not a hard validator in the authoring routes.

#### A.5 Event × *region geometry* — where rules attach

The other half of "what makes sense": the **geometry type** of the region a rule targets
(`tools/analyze_apply_targets.py`, 345 maps). Rules overwhelmingly target **unions** (2 238) and
area primitives (`rectangle` 949, `cuboid` 333) and the void **`negative`/`complement`** wrappers;
single `block` regions appear only 5× total and `point` **never**.

| event family | targets (by count) | geometry rule |
|---|---|---|
| `enter` (1535), `use` (462) | rectangle · union · cuboid · complement · cylinder · circle | **player-position events → area or compound regions** |
| `block` / `block_place` / `block_break` | union · negative · complement · cuboid · rectangle · `above` · (global) | edit events → areas, void wrappers, **and occasionally a single `block`** (protect one monument block) |
| `block_physics` (76) | union (mostly) | area/compound |
| `filter` (kit/velocity cond.) | union · negative · rectangle · cuboid | area/compound |

**The decisive finding:** across 345 maps there is **exactly one** `enter`/`use` rule on a
`block`/`point` region — and it's a *synthetic* auto-generated region, not authored. So **`enter`/
`use` on a single block or point is effectively never valid** (you can't "enter" a 1-block region):
the wiring UI should steer player-position events to area/compound geometry and warn on block/point.
`block_*` events, by contrast, legitimately target single blocks, so block-on-block is fine.
(`mirror`/`translate` targets resolve to their source geometry — an area — so they're area-like too.)

---

## 3. The wiring layer

How behavior attaches to regions, and the v1 suggestion templates the editor offers. This section
owns the **wiring relationship** and the **template catalog**. It does not restate:
- the **Filter / ApplyRule shapes** → `data-model.md` §9;
- the filter **vocabulary** + the *event × filter-type* and *event × region-geometry* matrices → §2.2
  (A.2–A.5) + the use-case recipes → §2.1 (Clusters 1–6);
- how wiring **surfaces per region** as `roles` → §1.3.

Supersedes the (unstable, already removed) `docs/requirements/editor-filters.md`.

### 3.1 The relationship

A region is **inert geometry**. Behavior comes from an **apply-rule**: `region × event → filter`
(plus optional actions `kit`/`lend_kit`/`velocity`/`message`). Events: `enter`, `leave`, `use`,
`block`, `block_place`, `block_break`, `block_physics`, `block_place_against`, and the kit/velocity
condition `filter`. One rule may carry several `event→filter` keys at once (canonical, not a
normalization target); filters compose (`all`/`any`/`not`/`deny`) and reference children by id.

**This introduces no new persisted type.** Wiring is `apply_rules` + `filters` (`data-model.md` §9)
referencing regions by id; in the region view it appears as the `roles` `<event>=<filter_id>` entries
(§1.3).

### 3.2 What attaches where

The sensible defaults (a **soft-warning** source for the UI, never a hard validator — §2.2 A.4):

- `enter` / `use` → **team**(-based) filters, on **area/compound** regions only (never a single
  `block`/`point` — you can't "enter" a 1-block region).
- `block` / `block_place` / `block_break` → `never` / `material` / `all`/`any`/`deny`/`not` over those;
  on areas, void wrappers, occasionally a single block.
- `block_physics` → `deny`. The kit/velocity `filter` condition → team / `all`.

Full matrices: §2.2 A.2 (event × filter-type) and A.5 (event × region-geometry).

### 3.3 v1 templates (suggest + confirm)

Four templates, grounded in the corpus's most common shapes. Each is **suggested from a map signal**
and **confirmed by the author** — never auto-applied or silently mutated. Each emits standard
`Filter` + `ApplyRule` entries (no special persisted form).

| # | Template | Trigger (signal) | Emitted wiring | Recipe |
|---|---|---|---|---|
| 1 | **Build / void enforcement** | positive build region(s) in the Build Regions step (+ `layer_y0.parquet`) | group buildable regions → apply `block_place=deny(void)` (or `never`) to the **complement** | §2.1 Cluster 2.4 |
| 2 | **Spawn protection** | a team spawn region (`spawns[].region`) | on the protection zone, apply `enter=only-<team>` **and** `block=never` (anti-grief; `never` is built-in — no new filter); optionally `use=only-<team>` | §2.1 Cluster 1.1 |
| 3 | **Wool-room defense** | a wool-room region with a derived owner (§1.6 owner) | apply `enter=not-<owner>` (defender excluded) | §2.1 Cluster 1.2 |
| 4 | **Wool-room build/break** | a wool-room region | apply `block=<team>-woolrooms-filter` (team check + material whitelist) | §2.1 Cluster 2.2 |

Template 1 is the canonical *suggest + confirm* flow: detect the positive build regions, propose
"auto-group and apply the void filter to the complement?", let the author confirm/adjust/decline.

### 3.4 Interaction stance

**Suggest + confirm, never silent.** Detect a signal → propose the wiring → author confirms,
adjusts, or declines. The authoring editors reject only **dangling references** (a child filter/region
that doesn't exist); "sense" (event/region/intent fit) is a **soft warning**, not a block.

### 3.5 Typed-model note

Wiring adds **no new typed shape**: the typed models are `Filter` + `ApplyRule` (`data-model.md` §9),
and the region view exposes its attached rules via `roles` (§1.3). The templates above are pre-built
`Filter` + `ApplyRule` combinations the wiring feature *emits* — not a persisted entity. Any feature
that types filters/rules should type straight from that model + the `roles` view; the suggestion
engine and templates build on those types and cannot reshape them.
