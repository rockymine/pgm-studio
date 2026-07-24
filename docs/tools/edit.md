# Edit — the per-map editor for existing maps

**Edit** is the pre-existing editor at `/maps/{id}/edit` for editing existing/imported maps directly —
the **Build**, **Objective**, **Teams**, and **Regions** activities, each manipulating the real
region/filter/apply-rule graph in place. It predates **Configure** (`docs/tools/configure.md`, the
new-map declarative-intent wizard) and is **not** being refit into the intent model: Edit is now
**frozen**. No new authoring features land here — only shared-infrastructure bugfixes (canvas, HTTP
plumbing) that happen to serve both surfaces. Lifecycle position: **Plan → Sketch → Configure → Edit**
— Edit is where a generated-or-imported map gets touched up once it already has a `map.xml`.

---

## 1. The authoring model — building blocks, not the raw tree

Every region-bearing activity can render the **full nested compound tree** (`/regions/tree`) — the
literal PGM region graph, including anonymous `union`/`negative` scaffolding and rule-container
wrappers. It's faithful and useful to *inspect* (the Regions activity keeps it as the read-only
structure/debugging view), but the CTW building blocks an author actually thinks in are smaller and
concrete: a spawn point, a spawn area, a build rectangle, a wool room, a monument. The sections below
describe those building blocks and the loop that turns them into wired regions.

### The authoring loop

```
draw a primitive  →  group primitives into a structure  →  the engine wires the rule
   (author)              (author — the judgement call)        (preset template, C9)
```

The division of labour: **the author groups** (which primitives form a wool room / a build area / a
spawn region — human judgement), **the engine wires** (apply the correct filter + apply-rule by role,
from the `../contracts/regions-and-filters.md` templates). Players do **not** write filters; a custom filter
constructor is out of scope. The four wiring templates (spawn protection, wool-room defense, wool-room
edit, build/void enforcement) cover the valid-map path; manual override stays possible but
preset-first is the default.

### Per-activity building blocks

*Grounded in the corpus (345 maps): spawn points are always inline `point`/`cylinder`/`cuboid`/`block`
(814/814, never named, never wired); `outback_outback_edition` shows the full wiring pattern below.*

**Spawns**
- **Primitives:** the spawn **point** (inline `point`/`cylinder`/`cuboid`/`block` — where players
  appear; **nothing is wired to it**) and the spawn **area/building** region (`rectangle`/`cylinder`/…).
- **Groups:** per-team spawn area; an "all spawns" union for shared mechanics.
- **Wiring:** `enter = only-<team>` (spawn protection); `block_break = only-iron` +
  `block_place = only-iron-cause-world` (the iron/gold armor-replenish blocks, often with a renewable);
  optional kit reset.

**Build**
- **Primitives:** `rectangle`s capturing void that must be crossed / floating terrain sections. Max
  build height is set here.
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

**Objectives**
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

### Composites, categories & cross-step references (carve-out detection)

A composite often pulls in regions from a **different activity**, so its members span categories. Do
**not** derive a composite's category from its members — derive it from **its role (the rule wired
onto it) + the activity that authors it**. Membership is allowed to cross activities freely.

**The canonical case — spawn-protection carves out the monuments inside it.**
In `annealing_iv` (4-team; each team captures its 3 enemy wools at monuments **inside its own spawn**)
the spawn region is a *complement*, not a union:

```
spawns                        (union)        block_break = only-iron   ← spawn edit-protection
  spawns__anon_0              (complement)   = spawn-areas − 12 monuments
    spawns__anon_0__anon_0    (union)        blue/red/green/yellow-spawn   [spawn]
    blue-team-red-wool … (×12) (block)       the monuments                 [monument]
```

The *why* is the rule: `block_break = only-iron` would block **placing the captured wool** on a
monument sitting inside spawn — so the author **subtracts the monuments** from the protected region.
The monuments aren't *grouped into* spawn; they're **holes** in it (geometry, not concept). Verified:
all 12 monument blocks fall inside the four spawn areas. (Oracle:
`tests/fixtures/region_authoring/annealing_iv.json`; `outback` does the same — `spawns__anon_0` =
`spawns − monuments`.) So `spawns` is a **Spawns-activity** structure even though it references
**Objectives-activity** monuments.

This makes two things first-class in the Edit UI:

- **Two member roles, not one.** Grouping isn't only union ("combine these"); it's also **subtract**
  ("this area, *minus* these carve-outs"). The group affordance needs a `union members` set and a
  `subtracted carve-outs` set (the complement's holes), which are usually the cross-activity references.
- **Cross-activity references.** Regions are a **shared pool**. Composing a structure in one activity,
  you **search + reference an existing region** (from any activity) as a member — the referenced
  region keeps its **home activity** (the monument still belongs to Objectives; it just appears as a
  carve-out in the spawn structure). The "Primitives (this activity)" panel stays activity-scoped; the
  **reference search spans all regions**. This is how the split activities (Spawns ⟂ Objectives) still
  compose across each other.

**Engine hint.** Because "spawn − monuments-inside-spawn" (and "build − objects-inside-build") is
mechanical and recurring, the spawn-protection / build templates can **auto-detect regions whose
footprint falls inside the protected area and offer the carve-out**. The author states intent
("protect this spawn"); the engine proposes the subtractions — so the cross-activity reference rarely
has to be done by hand.

---

## 2. Shared canvas infrastructure

Edit shares its canvas with Configure — every Configure phase mounts the same `EditorCanvas` (via
`studio.mountCanvas` → `studio-canvas.js`) that Edit uses, so region resize (8-handle drag), arrow-key
move, hit-testing, and primitive render styles are one mechanism serving both surfaces. The full
canvas contract — the layered `geometry/render/canvas/controllers/bridge` architecture, the resize/move
persistence path, the controller pattern, and the render/style unification across editors — lives in
`docs/architecture/canvas-interaction.md`; this doc doesn't duplicate it. Bugfixes to that shared
mechanism are the one category of change that still lands against Edit.

---

## 3. Known gaps

Edit's remaining incompleteness (tracked in `BACKLOG.md`, "Editor & canvas infrastructure" section):

- **C9 — Kits editing UI (Teams) + per-activity status dots.** Spawn `kit` is read/sent but has no
  edit UI; there is no status-dot system.
- **C11 — Wire + verify inspector edits across activities.** `OnDelete`/`OnRename` are wired only in
  Build Regions; the Regions/Teams/Objective inspectors are **unwired** — rename/delete silently
  no-op. Needs wiring in all three + end-to-end verification of rename/delete/coord-patch.
- **C14 — Dedupe activity code-behind.** The repeated `Post/Patch/Delete/Send` HTTP trio
  (Build/Objective/Teams) + the `Index`/`CollectDescendants` region-tree walkers (3–4 activities)
  should collapse into a shared `MapApiClient` and/or `EditorActivityBase` / static `RegionNode`
  helpers.
