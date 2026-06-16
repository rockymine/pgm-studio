# Region authoring (B4a)

> **Status (this repo).** The **split view-model** (Primitives/Composed/Raw) below is **superseded for
> new maps** by the declarative intent model — see `new-map-authoring.md` (§7: shaping activities use
> intent forms; the Regions activity keeps the full tree). What stays live here: the **per-activity
> building blocks** and the **author-groups → engine-wires** loop (TODO `N4`, `F1`) for editing
> *existing* maps, and the **composites / cross-step references** section (carve-outs). The "D1 React
> port" framing is reference-only (this is a Blazor app).

*Spec — the region **authoring** surface: a curated split view-model + the per-activity
building-block workflow.* Status: **design**; the view-model split is a near-term backend change,
the interaction layer ships with the frontend switch (D1). Supersedes the stale region/objective bits
of `docs/requirements/editor-*.md`.

## The problem: a tree is a model, not a view

Today every region-bearing activity renders the **full nested compound tree** (`/regions/tree`).
That is the *model* — the literal PGM region graph, including anonymous `union`/`negative` scaffolding
and rule-container wrappers. It's faithful and useful to inspect, but it is the wrong *authoring*
surface:

- On a complete imported map the tree is enormous; the meaningful building blocks are buried.
- **As you author, you lose the ground truth.** Every time you wrap primitives in a union for some
  rule, the thing you actually drew sinks a level deeper into the tree. The tree grows; "what are my
  actual pieces" gets harder to see.

The building blocks of a CTW map are small and concrete — a spawn point, a spawn area, a build
rectangle, a wool room, a monument. Authoring should keep those in view and let the **structures**
(unions, negatives) and **wiring** (filters/apply-rules) be a *separate, intentional* layer.

## The split view-model

The authoring view is a **split**, not a prettier tree — three derived facets of the same regions:

| Facet | What | Derivation |
|---|---|---|
| **Primitives** | the leaf building blocks (`rectangle`/`cuboid`/`cylinder`/`circle`/`sphere`/`block`/`point`) | structural: a region with no children |
| **Composed** | the structures the author built from primitives (`union`/`negative`/`complement`/`intersect`), each annotated with the filters/apply-rules wired onto it | structural: a compound region + its `apply_rules`/`filters` refs |
| **Raw** | the full nested tree (`/regions/tree` today) | unchanged — demoted behind an "advanced / raw" toggle |

The split is **structural, not provenance-based** (we can't know if an imported region was
hand-drawn) — leaf = a building block, compound = a structure. That works the same authoring from
blank (primitives accumulate as you draw, then you group them) and editing an import (the leaves *are*
the ground truth; the compounds are what someone composed).

Each facet is **scoped to the active step** by the derived `category`/`roles`
(`region-categorization.md`, B5): the Spawns step shows spawn-role primitives/structures, Build shows
build-role, Objectives shows objective-role. The step decides which building blocks are in view.

## Layout

The studio uses a three-panel workspace (modelled loosely on Figma). Authoring places the split in
the **left sidebar** as **vertically stacked, collapsible sections** — the right panel is the
**inspector** (selected region's type/coords + its wiring), the centre is the **canvas + creation
toolbar**.

```
left sidebar (stacked)         centre                 right
┌─────────────────────┐  ┌───────────────────┐  ┌──────────────┐
│ Entities (teams/…)  │  │  canvas + toolbar │  │  Inspector   │
├─────────────────────┤  │  (draw primitives)│  │  (selected   │
│ Primitives (step)   │  │                   │  │   region +   │
├─────────────────────┤  │                   │  │   its rules) │
│ Groups & wiring     │  │                   │  │              │
└─────────────────────┘  └───────────────────┘  └──────────────┘
```

Stacked (not tabbed): the group count is small (2 teams, a handful of wool colours), and keeping
"my primitives" and "what I grouped them into" visible **together** is the point — a tab would hide
the relationship that the tree already obscures.

## The authoring loop

```
draw a primitive  →  group primitives into a structure  →  the engine wires the rule
   (author)              (author — the judgement call)        (preset template, C9)
```

The division of labour: **the author groups** (which primitives form a wool room / a build area / a
spawn region — human judgement), **the engine wires** (apply the correct filter + apply-rule by role,
from the `filter-region-wiring.md` templates). Players do **not** write filters; a custom filter
constructor is **deferred**. The four v1 templates (spawn protection, wool-room defense, wool-room
edit, build/void enforcement) cover the valid-map path; manual override stays possible but
preset-first is the default.

## Per-activity building blocks

*Grounded in the corpus (345 maps): spawn points are always inline `point`/`cylinder`/`cuboid`/`block`
(814/814, never named, never wired); `outback_outback_edition` shows the full wiring pattern below.*

### Spawns
- **Primitives:** the spawn **point** (inline `point`/`cylinder`/`cuboid`/`block` — where players
  appear; **nothing is wired to it**) and the spawn **area/building** region (`rectangle`/`cylinder`/…).
- **Groups:** per-team spawn area; an "all spawns" union for shared mechanics.
- **Wiring:** `enter = only-<team>` (spawn protection); `block_break = only-iron` +
  `block_place = only-iron-cause-world` (the iron/gold armor-replenish blocks, often with a renewable);
  optional kit reset.

### Build
- **Primitives:** `rectangle`s capturing void that must be crossed / floating terrain sections
  (see `docs/requirements/editor-build-regions.md`). Max build height is set here (already supported).
- **Groups:** union the build rectangles → take the `negative` (the not-build / void-affect area);
  some maps need `complement`/intersect for awkward shapes.
- **Wiring (the two halves differ):**
  - `block_place = not(void)` — you **cannot build into the void** (no bridging across the gaps).
  - `block_break` is **not** a blanket deny. By default a block with no solid block below it (at
    `y=0`) can't be broken — but terrain/trees often overhang the void, so maps allow breaking a
    **curated material set** there. The real filter is
    `any( all( any(leaves, log[, tnt]), void ), not(void) )` — *"break allowed if it's a tree
    material **and** in the void, **or** it isn't in the void at all."* So an overhanging tree can be
    cleared to cross, while the void floor itself stays unbreakable. (Example: `docs/xml_template.xml`
    `block-break-void-filter`; `annealing_iv` adds `tnt` to the allowlist.)
- **Engine:** the build/void template carries the breakable-material allowlist (default `leaves`/`log`,
  author-extendable) — same author-groups → engine-wires loop; the author shouldn't hand-write the
  nested `any/all/void` filter.

### Objectives
- **Primitives:** the wool item **spawn location**; the **monument** (always a `block` — where a team
  captures the wool); the **wool-room** region(s) (the building the wool sits in).
- **Groups — the two-grouping distinction (real, load-bearing):** the *same* wool-room primitives feed
  **two** groups: **per defending team** (because `enter` differs — the defender is locked *out* of
  their own rooms: `yellows-woolrooms enter=only-purple`) **and** **all wool rooms** (for shared
  mechanics, e.g. cobwebs breakable in every room). One primitive, two groupings.
- **Wiring:** `enter = only-<enemy-of-defender>`; block exceptions on the room; optional **kit on
  enter** (better armor); the wool **renewable/spawner** (when no chest/mob source exists) keyed to a
  **player-trigger region** (where entering players start the spawn — often the wool room itself).

When teams exist + spawn safely, and wools can be obtained, defended, and captured at their monuments,
the map is **valid**. Everything else regions can do (renewables, kits, mechanical exceptions) is
optional polish layered on top.

## Composites, categories & cross-step references

A composite often pulls in regions from a **different step**, so its members span
categories. Do **not** derive a composite's category from its members — derive it
from **its role (the rule wired onto it) + the step that authors it**. Membership
is allowed to cross steps freely.

**The canonical case — spawn-protection carves out the monuments inside it.**
In `annealing_iv` (4-team; each team captures its 3 enemy wools at monuments
**inside its own spawn**) the spawn region is a *complement*, not a union:

```
spawns                        (union)        block_break = only-iron   ← spawn edit-protection
  spawns__anon_0              (complement)   = spawn-areas − 12 monuments
    spawns__anon_0__anon_0    (union)        blue/red/green/yellow-spawn   [spawn]
    blue-team-red-wool … (×12) (block)       the monuments                 [monument]
```

The *why* is the rule: `block_break = only-iron` would block **placing the captured
wool** on a monument sitting inside spawn — so the author **subtracts the monuments**
from the protected region. The monuments aren't *grouped into* spawn; they're
**holes** in it (geometry, not concept). Verified: all 12 monument blocks fall
inside the four spawn areas. (Oracle: `tests/fixtures/region_authoring/annealing_iv.json`;
`outback` does the same — `spawns__anon_0` = `spawns − monuments`.) So `spawns` is a
**Spawns-step** structure even though it references **Objectives-step** monuments.

**This makes two things first-class in the authoring UI:**

- **Two member roles, not one.** Grouping isn't only union ("combine these"); it's also
  **subtract** ("this area, *minus* these carve-outs"). The group affordance needs a
  `union members` set and a `subtracted carve-outs` set (the complement's holes), which
  are usually the cross-step references.
- **Cross-step references.** Regions are a **shared pool**. Composing a structure in one
  step, you **search + reference an existing region** (from any step) as a member — the
  referenced region keeps its **home step** (the monument still belongs to Objectives;
  it just appears as a carve-out in the spawn structure). The "Primitives (this step)"
  panel stays step-scoped; the **reference search spans all regions**. This is how the
  split steps (Spawns ⟂ Objectives) still compose across each other.

**Engine hint.** Because "spawn − monuments-inside-spawn" (and "build − objects-inside-
build") is mechanical and recurring, the spawn-protection / build templates can
**auto-detect regions whose footprint falls inside the protected area and offer the
carve-out**. The author states intent ("protect this spawn"); the engine proposes the
subtractions — so the cross-step reference rarely has to be done by hand.

## Command & shortcut model (with B6)

Group / ungroup / set-base-child / wire-template / delete are **commands**. A context menu and a
keyboard-shortcut registry both dispatch the same commands, and **B6 undo/redo inverts them** — so the
interaction layer, the shortcut system, and undo/redo are **one** concern built **once**. This is why
the interaction half of B4a lands in the React port (D1): building a context-menu + shortcut + command
system in the current vanilla stack would build it twice (see `frontend-stack.md`).

## Build split (design now / React later)

- **Now (backend, survives D1):** `region_encoder` gains the **primitives / composed / raw** split
  keyed by step role facets; the per-activity building-block definitions above; the wiring templates
  already exist (C9). React consumes this contract.
- **D1 (React):** the stacked split-view sections, context-menu grouping, multi-select, and the
  keyboard-shortcut/command layer (shared with B6).

## Cross-references

- `region-categorization.md` (B5) — the `category`/`roles` that scope each facet to a step.
- `filter-region-wiring.md` (C9) — the preset templates the engine applies after grouping.
- `data-model.md` — Region/Wool/Spawn/ApplyRule shapes; `geometry.md` — region geometry.
- `frontend-stack.md` (D1) — the React port that builds the interaction layer; B6 — undo/redo commands.
- Supersedes the stale authoring narrative in `docs/requirements/editor-regions.md` /
  `editor-objectives.md` / `editor-build-regions.md`.
