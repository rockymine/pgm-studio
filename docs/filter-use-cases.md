# CTW Filter & Apply Rule — Semantic Use Cases

Analysis of 345 CTW maps (CommunityMaps + PublicMaps), 3 946 apply rules, 7 772 filters.
This document maps gameplay design questions to the XML patterns that implement them,
organised by cluster and ordered by map-level prevalence. The Clusters cover *intent*; the
**Appendix** (bottom) is the *vocabulary* — which filter types attach to which events, and how
they compose — and is the reference for the C3/C4 editor and the C9 wiring UI.
*(Corpus figures re-verified 2026-06-10.)*

---

## Cluster 1 — Access Control (Who can go where)

### 1.1 Protect enemy spawn from entry
*"Prevent the enemy team from walking into your spawn."*

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

---

### 1.2 Lock own wool room — team cannot enter their own
*"Prevent a team from entering their own wool room, forcing them through enemy territory."*

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

**Semantic question:** "Which team owns this wool room?" (the opposing team gets in; the owning team is excluded.)

---

### 1.3 Right-click protection inside wool room / spawn
*"Prevent opening chests or using interactive blocks in a restricted area."*

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

---

## Cluster 2 — Block Editing Rules (What can be placed and broken)

### 2.1 Spawn block protection — iron regeneration
*"Players can only break iron blocks at spawn; iron regenerates automatically."*

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

---

### 2.2 Wool room block protection — restrict editing to team-specific blocks
*"Players can only edit certain blocks inside a wool room."*

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

---

### 2.3 Full block lockdown — no editing at all
*"No block placement or breaking permitted in this region."*

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

---

### 2.4 Void / outside-map protection
*"Prevent players from building into the void or outside the intended play area."*

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

---

### 2.5 Block physics denial — stop water, lava, redstone from spreading
*"Prevent certain blocks from triggering physics updates in wool rooms or spawn."*

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

---

## Cluster 3 — Kit Assignment (Equipment by zone)

### 3.1 Resistance kit reset — remove resistance effect outside spawn
*"Players lose spawn-protection resistance when they leave the spawn area."*

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

---

### 3.2 Wool room kit — extra equipment for wool room attackers
*"Players entering a wool room receive a specific kit (e.g. shears, special tools)."*

**Prevalence:** 31/345 maps (8%)

**Pattern:** `kit` applied to attackers entering the wool room. Often filtered to
the opposing team only via `filter=`.

```xml
<apply kit="wool-gear" region="red-wool-rooms" filter="only-blue"/>
<apply kit="wool-gear" region="blue-wool-rooms" filter="only-red"/>
```

---

### 3.3 Zone-based kit swap — different gear in different areas
*"Players receive (or keep) a specific kit while in a designated zone."*

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

---

## Cluster 4 — Movement / Launch Mechanics

### 4.1 Jump pads — velocity launch zones
*"Players who walk through this region are launched in a direction."*

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

---

### 4.2 Map boundary — prevent leaving the play area
*"Players cannot leave the designated play area."*

**Prevalence:** 4/345 maps (1%) — rare but present

**Pattern:** `leave="never"` on the play boundary region.

```xml
<apply leave="never" region="playspace" message="You cannot exit the map."/>
<apply leave="never" region="sides"     message="You may not exit the playing field!"/>
```

---

## Cluster 5 — Renewable Resources

### 5.1 Iron / gold block renewal at spawn
*"Iron (or gold) blocks at spawn regenerate after being broken."*

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

---

## Cluster 6 — Advanced / Special Mechanics

### 6.1 Time-gated features
*"Something changes or unlocks after a certain amount of time into the match."*

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

---

### 6.2 Original map state protection (player-placed vs map-original)
*"Players can only break blocks they placed; original map blocks are protected."*

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

---

### 6.3 Block placement against specific surfaces (anti-climb)
*"Prevent players from placing blocks against certain structures to climb over them."*

**Prevalence:** 3/345 maps (nyxis-type maps)

**Pattern:** `block-place-against` filter on anti-wall-climbing regions.

```xml
<apply region="anti-wall-climbing-region"
       block-place-against="anti-wall-climbing-filter"
       message="You may not directly place blocks against this part of the map."/>
```

---

## Summary: UX Question Mapping

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

---

## Recurring Filter Patterns by Name

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

---

## Appendix — Filter Vocabulary & Event Matrix (what attaches to what)

Reference for the editor (C3/C4) and the wiring UI (C9): the realistic filter vocabulary,
which filter types attach to which apply events, and how composites are built. Counts are
corpus-wide (345 maps, 7 772 filters, 3 946 apply rules) as of 2026-06-10.

### A.1 Filter type frequency

Leaf conditions dominate (`material` 2 751), then the composers (`all` 902, `any` 727, `not` 522,
`deny` 365) and `team` 767. The long tail (`variable`, `time`, `carrying`, `blocks`, `region`,
`offset`, `objective`, `after`/`pulse`, `class`, `kill-streak`, …) is the advanced surface.

| tier | types (by count) |
|---|---|
| **core leaves** | `material` 2751 · `team` 767 · `never` 342 · `always` 340 · `cause` 217 · `void` 194 |
| **composers** | `all` 902 · `any` 727 · `not` 522 · `deny` 365 · (`one`, `allow` rare) |
| **conditional / advanced** | `variable` 174 · `time` 88 · `alive` 71 · `carrying` 55 · `blocks` 50 · `participating` 39 · `offset` 31 · `region` 30 · `objective` 30 · `wearing` 18 · `after` 13 · `completed` 12 · `pulse` 8 · `class`/`spawn`/`grounded`/`kill-streak`/… ≤6 |

### A.2 Event × filter-type — *what is sensible where*

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

### A.3 How composites are built

Composers reference children by id; the children's types show the real shapes:

| composer | common child types | typical meaning |
|---|---|---|
| `all` (AND) | team · any · material · not · cause · void | "this team **and** this material/condition" (wool-room edit filter) |
| `any` (OR) | **material 2095** · team · all | "**any of** these block types" (editable-material whitelist) |
| `deny` (= NOT-allow) | any · material · all · participating · team · void | invert a condition (deny chests, deny void, deny physics) |
| `not` (NOT) | any · team · void · all · time · objective | "all **other** teams", "**not** void", time-gated negation |

### A.4 On "nonsensical" filters & stackability

Filters are **freely composable conditions** — there is no type that is inherently invalid, and the
editor (C3/C4) deliberately does **not** forbid combinations: it only rejects *dangling references*
(a child filter / region that doesn't exist). "Sense" is a function of **event + region + intent**,
not the filter type alone, and stacking (`all`/`any`/`not`/`deny`) makes otherwise-odd leaves
meaningful (e.g. `material` is meaningless on `enter`, but `all(material, team)` on `block` is the
canonical wool-room rule). The matrix in A.2/A.3 is therefore a **suggestion/soft-warning** source
for the C9 UI — surface the per-event vocabulary first, and *warn* (don't block) on pairings that
never appear in the corpus — not a hard validator in the C3/C4 routes.

### A.5 Event × *region geometry* — where rules attach

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
the C9 UI should steer player-position events to area/compound geometry and warn on block/point.
`block_*` events, by contrast, legitimately target single blocks, so block-on-block is fine.
(`mirror`/`translate` targets resolve to their source geometry — an area — so they're area-like too.)
