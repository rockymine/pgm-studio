# Region composition in a CTW map

What a map's regions actually **are**, activity by activity, measured on the corpus — and the one
composition pattern that is not a union. This is the reference for what a region-bearing step is composing,
not for the surface that composes it: the authoring tools are `../tools/configure.md` (which states intent
and derives the graph) and `../tools/edit.md` (which edits the graph directly).

The wiring templates the patterns below resolve to are `filter-region-wiring.md`; the categories that scope
a region to a step are `region-categorization.md` §3.

## The division of labour

```
draw a primitive  →  group primitives into a structure  →  the engine wires the rule
   (author)              (author — the judgement call)        (template)
```

**The author groups** — which primitives form a wool room, a build area, a spawn region — because that is
human judgement. **The engine wires** — the correct filter and apply-rule by role, from the template
catalog. An author does not write filters; the four v1 templates (spawn protection, wool-room defence,
wool-room edit, build/void enforcement) cover the valid-map path, and a custom filter constructor is
deliberately not offered.

That split is what the intent model later made total: state the spawn and the monuments, and the whole graph
is derived rather than grouped (`new-map-authoring.md` §1).

## What each activity is made of

*Grounded in the corpus of 350 maps.*

### Spawns

The spawn **point** — where players materialise — and the spawn **area** around it are different things and
compose differently.

A spawn point is an **inline, unnamed, unwired primitive**, and the corpus is unanimous about it. Across
1,288 spawns the region is a `point` (593), a `cylinder` (491), a `cuboid` (160) or a `block` (43) — with one
`mirror` outlier — and **not one of them is targeted by an apply-rule**. Nothing is attached to the place a
player appears; the protection is a separate region around it.

The spawn **area** is a `rectangle`/`cylinder` per team, optionally unioned into an "all spawns" group for
shared mechanics. Its wiring is `enter = only-<team>` for protection, plus `block_break = only-iron` and
`block_place = only-iron-cause-world` where the map has the iron/gold replenish blocks, usually with a
renewable behind them.

### Build

The primitives are `rectangle`s over the void that must be crossed, or floating terrain sections. They are
unioned, and the **negative** of that union is the not-build area. Max build height is a scalar on the map,
not a region.

**The two halves of the wiring differ, and the asymmetry is the interesting part.** `block_place = not(void)`
is a flat prohibition: you cannot build into the void, so gaps cannot be bridged outside a build region.
`block_break` is *not* the mirror of it. Terrain and trees overhang the void, and a map that denied breaking
there would leave an overhanging canopy permanently in the way — so the real filter allows a curated material
set:

```
any( all( any(leaves, log[, tnt]), void ), not(void) )
```

*Break allowed if it is a tree material **and** in the void, **or** it is not in the void at all.* An
overhanging tree can be cleared to make a crossing while the void floor itself stays unbreakable.
`docs/pgm/template.xml`'s `block-break-void-filter` is the canonical form; `annealing_iv` adds `tnt` to the
allowlist. The template carries the allowlist so no author hand-writes the nested `any`/`all`/`void`.

### Objectives

Three primitives per wool: the wool **spawn location**, the **monument** (always a `block` — where a team
delivers), and the **wool-room** region the wool sits in.

The rooms carry a grouping the other activities do not: **the same room primitives feed two groups.** One per
**defending team**, because `enter` differs per room — the defender is locked *out* of their own rooms
(`yellows-woolrooms enter=only-purple`) — and one over **all** wool rooms, for mechanics that apply
everywhere, such as cobwebs being breakable in any room. One primitive, two groupings, and neither is
derivable from the other.

Beyond that: `enter = only-<enemy-of-defender>` on the room, block exceptions on it, an optional kit on
enter, and the wool **renewable/spawner** where no chest or mob source exists — keyed to a player-trigger
region, which is usually the wool room itself.

When teams exist and spawn safely, and wools can be obtained, defended and captured at their monuments, the
map is **valid**. Everything else a region can do is optional polish.

## The carve-out — the composition that is not a union

Grouping is not only "combine these". It is also "this area, **minus** these", and the corpus needs it.

`annealing_iv` is the canonical case. It is a 4-team map where each team captures its three enemy wools at
monuments standing **inside its own spawn**, and its spawn region is a `complement` rather than a union:

```
not-spawns                    (negative)
  spawns                      (union)        block_break = only-iron   ← spawn edit-protection
    spawns__anon_0            (complement)   = spawn areas − 12 monuments
      spawns__anon_0__anon_0  (union)        blue/red/green/yellow-spawn    [spawn]
      blue-team-red-wool …    (block ×12)    the monuments                  [monument]
```

The *why* is the rule sitting on it. `block_break = only-iron` on `spawns` would block **placing the captured
wool** on a monument that stands inside a spawn — so the author subtracts the monuments from the protected
region. The monuments are not *grouped into* spawn; they are **holes** in it. Geometry, not concept.

Two things follow that any composition surface has to support.

**A group has two member sets, not one** — the union members and the subtracted carve-outs.

**Regions are a shared pool, and a structure may reference across activities.** `spawns` is a Spawns-step
structure that references Objectives-step monuments, and the referenced region keeps its home step: the
monument still belongs to Objectives and merely appears as a carve-out here. A per-step primitive list stays
step-scoped; a reference search has to span everything.

Because "spawn minus the monuments inside it" is mechanical and recurring, the spawn-protection and build
templates can detect regions whose footprint falls inside the protected area and **offer** the carve-out. The
author states the intent; the engine proposes the subtraction.

## A note on the split view

The **primitives / composed** split — leaf shapes on one side, the structures grouping them on the other,
each annotated with its wiring — was designed as the authoring view for exactly the reason above: a tree
buries what you drew one level deeper every time you group it. **The view it was for is not being built.**
Hand-authoring regions and their filters is the burden the intent model exists to remove, so the Edit tool
never grew the create/update/delete surface this split was the render input for, and the route that answered
it is gone. `RegionAuthoringEncoder.EncodeAuthoring` remains as a derivation, reviewable through
`tools/PgmStudio.RoundTrip --authoring-fixture`; the argument above is kept because it is a true statement
about what a tree hides, not because a screen is coming.

## Cross-references

- `region-categorization.md` — the `category` and `roles` that scope a region to a step.
- `filter-region-wiring.md` — the templates the engine applies after grouping.
- `filter-patterns.md` — the same patterns measured across the corpus, with prevalence.
- `Domain/MapModel.cs` — the Region / Wool / Spawn / ApplyRule shapes, and `Domain/Filter.cs` the filter;
  the types are the specification.
