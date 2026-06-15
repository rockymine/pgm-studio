# New-map authoring: the declarative intent model

How a **new** map is authored by stating *intent* — teams, spawns, build space, wools — and having
the system **generate** the regions, filters, and apply-rules, instead of editing the region/filter
graph by hand. This is the forward direction of the app; everything else (parse → derive category →
display) is the reverse.

Read alongside:
- `region-categorization.md` — the **reverse** mapping (structure → meaning). The generator here is
  its mirror image.
- `filter-region-wiring.md` — the **wiring templates** the generator emits (build/void, spawn
  protection, wool-room defense, wool-room build/break).
- `../region-data-flow.md` — **persistence + entity-replace + the `map_artifact` sidecar**. The
  intent model lives where the draft bucket lives, and for the same reasons.

> **Status:** design (decision: go declarative). New maps only — e.g. `thunder_blank` (a no-xml copy
> of thunder). Existing corpus maps stay region-first and are untouched by any of this.

---

## 1. Why — the inversion

The whole app today flows **structure → meaning**: parse XML → `regions`/`filters`/`apply_rules` →
*derive* category, subtype, owner, build areas (`RegionCategorizer`). It has to, because the corpus
only ever gave us structure and intent had to be recovered from it.

New-map authoring flows the other way — **meaning → structure**: the author states intent and the
system *generates* the graph, which then serializes to PGM-loadable XML. The generator is the mirror
of the categorizer; we already understand the mapping in both directions (categorization + the wiring
templates prove it). We just haven't built the forward path.

**The corpus was the sample, not the target.** Per the standing TODO warning, over-fitting the
generator to reproduce any existing map's exact structure is the failure mode. The generator is
driven by **author intent + round-trip validity**, not by matching a specific map. It is allowed to
emit a *different* (simpler, canonical) structure than a human author wrote, as long as it parses and
plays.

---

## 2. The intent model — the source of truth

A compact, declarative description of *what the author wants*, independent of how it's realized as
regions/filters:

```
intent = {
  meta:   { name, authors[], contributors[] },  // usernames → uuids; version 1.0.0, proto 1.5.0,
                                                //   gamemode ctw, objective all auto-derived
  teams:  { count, maxPlayers, kit },          // one size for all teams (symmetric map)
  spawns: [ { team, point, protection? } ],     // protection optional per team
  build:  { maxHeight?, areas[] },              // buildable rects (footprints + bridges alike)
  wools:  [ { color, owner, roomRegion, spawnRegion, monuments[] } ],
  symmetry: <ref to the confirmed symmetry mode>,
}
```

**Map identity.** `name` + `authors`/`contributors` are authored (the latter as Minecraft usernames,
resolved to uuids via `MojangClient` at save; the contribution attribute is unused). `version` (1.0.0),
`gamemode` (ctw), and the `objective` text are auto-derived; `proto` (1.5.0) is fixed at XML export.

Notes that fall out of the model:
- **One authored unit per symmetry orbit.** The author defines team 0's spawn/protection, one
  build/bridge set, one wool — symmetry-fill (orbit) produces the rest. The intent stores the
  *authored* unit plus the symmetry; the orbit members are generated, not stored.
- **`maxPlayers` is one number.** On a symmetric map every team has the same cap. **`minPlayers` is
  dropped** (not needed). **Kit is preselected** (a sensible default, overridable).
- Geometry the author draws (spawn point, protection rect, bridge rects, wool-room rect, monument
  blocks) is stored as plain coords in the intent, **not** as `region` rows. Regions are derived.

---

## 3. Where intent lives — and why regeneration is free

`region-data-flow.md` §2 establishes the load-bearing constraint: **every save drops and recreates
all region/filter/apply-rule rows** (`MapWriter.SaveDocAsync` = `DeleteEntities` → `FromDict` →
`WriteEntities`), and **editor-only state must live outside the codec** or the next save wipes it.

That constraint is what makes the declarative model clean rather than expensive:

- **Intent is a `map_artifact` blob** (`map_intent_json`), exactly like `region_drafts_json`. It
  survives `SaveDocAsync` (artifacts are kept), and the codec/categorizer never see it — it is *not*
  part of the PGM document.
- **Regions are a derived projection of intent.** The generator turns intent into a PGM document
  dict and persists it through the **normal codec path** (`SaveDocAsync`). The generated regions are
  fully canonical (they round-trip, they categorize, the existing tree/canvas/inspector render them
  unchanged) — they're just *output*, not the source of truth.
- **Idempotent regeneration is the existing save path.** Because every save already wipes and
  rewrites all rows, "the author corrected the team count / moved a spawn" is just: mutate intent →
  regenerate doc → `SaveDocAsync`. No duplicate regions, no orphaned filters, no diffing. This is the
  "define once, correct anytime" property — and we get it for free from a behavior that already
  exists.

```
author edits ─▶ intent blob (source of truth)
                   │  generate (§4)
                   ▼
              PGM document dict ──SaveDocAsync──▶ region/filter/apply_rule rows (canonical, derived)
                   │                                   │  read + RegionCategorizer
                   └──────────── mirror check ─────────┘  (should recover the intent — §9)
```

---

## 4. The generator — mirror of the categorizer

`generate(intent) → PGM document dict`. It composes primitives the backend already has (region CRUD,
filter CRUD, apply-rules, spawns, wools, monuments, kits) using two engines that already exist:

- **Symmetry-fill (orbit).** The confirmed symmetry (`GET /symmetry`, `POST /regions/{id}/orbit`)
  rotates/mirrors the authored unit into every orbit position. Already used for single drawn regions
  (F3); here it's applied to whole authored units.
- **Wiring templates (F1, `filter-region-wiring.md`).** The four catalog templates are the generator's
  building blocks:
  1. **Build/void** — union the positive build areas, apply `block_place=deny(void)` to the complement.
  2. **Spawn protection** — `enter=only-<team>` on the protection zone.
  3. **Wool-room defense** — `enter=not-<owner>` on the wool-room region.
  4. **Wool-room build/break** — team-checked block filter on the wool-room region.

**Auto-derivations (the "system does the hard work" part):**

| Author states | System derives |
|---|---|
| confirmed symmetry | suggested **team count** from the symmetry order (`rot_90` → 4, `mirror_*` → 2…), author-correctable |
| team count | **monuments per wool**: 2-team → 1, N-team → N−1 (every team *except the owner* must capture it) |
| spawn point (+ optional protection) for team 0 | the other teams' spawns/protection via orbit, plus spawn-protection wiring (template 2) |
| a few bridge rectangles | auto-**union** + void filter on the complement (template 1) |
| wool-room rect, owner | defense + build/break wiring (templates 3, 4); deny-enter for the defender |

**Auto-unioning over hand-grouping.** The author never builds union/complement structure by hand. The
generator unions the regions a template needs (bridges for void, spawn rects for protection, etc.) and
applies the filter to the union/complement. This is why the shaping activities **stop showing the
region tree** (§7): there is no author-managed structure to show — structure is an artifact.

---

## 5. The authoring flow (per activity)

Each shaping activity edits a **slice of intent** through a simple form + canvas, not the region tree.

1. **Teams & spawns.** Symmetry order suggests team count (correctable). Set one `maxPlayers`; pick a
   kit (preselected). Place team 0's spawn point; toggle "wrap in a protection zone" and draw/accept
   it. Orbit fills the other teams; templates wire protection. A team's **spawn point and its
   protection zone are one unit in one place** — fixing the current split where they appear under
   different tree branches.
2. **Build.** Set max build height; mark the positive build area; draw a few bridge rectangles. System
   unions them and applies the void filter to the negative/complement (template 1).
3. **Wools.** Per wool: define the wool **spawn**, the **room** region, the **monuments**. Monument
   count is pre-filled from team count (§4) and adjustable. System wires room defense + build/break and
   monument capture.

End state: a complete, valid PGM document an author produced by *defining each thing once*.

---

## 6. World analysis — seeding, validation, and why the steps are ordered this way

The new-map target is a **logical map authored on top of existing terrain** (the `thunder_blank`
class — a world with blocks but no `map.xml`). That terrain is what the read-side analyses
(`/islands`, `/buildability`, `/traversability`) operate on, so they become the **bridge between the
physical world and the logical intent**: the world *seeds* the intent, and the intent is *validated*
back against the world. Each analysis plays both roles.

**Seed (world → authoring inputs)** — so no step starts blank:
- **`/islands` → build areas.** Each island is a `bounds` rectangle; pre-fill `BuildIntent.Areas` from
  them (the author trims/merges rather than drawing from scratch).
- **symmetry + island count → team count & positions** (4 symmetric islands ⇒ 4 teams).
- **`/buildability` → placement snap/validate.** Spawns, wool rooms, and monuments must sit on
  `buildable` columns; the overlay shows where solid ground is.

**Validate (intent → feedback):**
- **`/buildability`** re-run *after* the build slice shows the *actual* buildable map produced by the
  void enforcement — catching unintended `never`/`void_denied`.
- **`/traversability`** checks the **spawn↔wool chain is connected** over *walkable surface ∪
  bridgeable buildable*. (Live on a half-authored `thunder_blank` it reports `connected: false —
  "… not reachable … check build regions / bridgeable gaps."`)

**This dependency chain fixes the step order** (and confirms `meta → teams → build → wools`):

```
world analysis (islands / buildability / symmetry)   ← step 0, precomputed (M7 import)
        │ seeds
   teams / spawns      (spawns validated onto buildable/surface columns)
        │
     build             ← seeded from islands; bridges fill the gaps between them
        │ (the build + bridge geometry IS the navigability substrate)
     wools             (rooms / monuments on buildable ground)
        │
  traversability       ← closing GATE: chain connected? if not, loop back to build
```

The load-bearing rule: **build must precede wools and the traversability gate**, because
traversability is computed over the build/bridge geometry — a wool's reachability is undefined until
the bridges exist. So:
- **buildability is a live overlay *within* the Build step** (seed + immediate feedback), not a step.
- **traversability is a closing validation gate, not a step** — and it's *iterative*: its failure
  message sends the author back to Build to add bridges (Build ⇄ Traversability is a loop).
- a natural **Review/Validate phase** falls out: run traversability (+ buildability) on the finished
  intent before export, and only a connected map should export (the **export gate**, §9).

> Without a world (Y=0 / surface columns) these analyses can't run, so a truly from-scratch map gets
> no seeding or connectivity validation — another reason the first target is the terrain-backed
> `thunder_blank` class.

---

## 7. Coexistence — the tree view doesn't die, it moves

- **Gated by intent.** A map "is intent-authored" iff it has a `map_intent_json` blob. New maps
  (thunder_blank) get one; existing corpus maps don't and keep the current **region-first** editing
  unchanged. Nothing about the corpus path changes.
- **The shaping activities (Teams/Build/Objective) drop `RegionTree`** for intent maps and render
  bespoke intent panels (form + canvas + a small draft list). The data-model tree was never an
  intuitive authoring surface — annealing_iv's `negative → union → complement → union → child` spawn
  structure is the proof: faithful to the data, hostile to an author.
- **The Regions activity keeps the full tree** as the read-only structure/inspection view (and the
  debugging surface for what the generator produced). That's where the hierarchy belongs.

---

## 8. Canvas implications (separate, lands independently)

The full-map canvas fights small edits: placing one block or a small region on a whole-map zoom is
imprecise. New-map authoring needs **per-side focus** — zoom/fit to one team's quadrant or a
restricted view-box while defining that team's regions, since the author works one orbit unit at a
time. This is an independent canvas capability and can land before the generator.

**Landed — canvas focus controls.** The editor canvas toolbar has a **Fit island** dropdown (zooms to
an island's bbox) and a **reset-zoom** button (whole-map view), so the author can frame one side while
working it.

**Landed — the side-view slice (setting Y on a flat map).** The plan canvas is top-down, so a drawn
point/block region has no obvious Y. The region inspector therefore shows a **side-view slice** — a
localised vertical cross-section of the terrain at the selection (`/segments` windowed to the column ±10,
or a rectangle's footprint):
- **Point/block** → a **draggable Y line** sets the region's Y (persisted as a `coords` patch); this is
  how the author lifts a spawn point / wool-spawn / monument off `y=0` onto the terrain surface.
- **Rectangle** → display-only (read the terrain profile under a wool room / build area).
- **Four inspect directions** (`Z− Z+ X− X+`) — the camera on either side of each axis — so the author
  can read the near face from whichever direction is unobstructed. The same four-way control drives the
  Build-Regions step-1 side-view.

How it fits the flow: after placing a point in the plan view (often via orbit-fill, §4), select it and
drag the Y line in the slice to seat it on the surface — the canvas gives plan position, the slice gives
height. Buildability/traversability (§6) then validate that the placement is on solid, reachable ground.

---

## 9. Validation — the mirror property and the playability gate

Three checks, all reusing what exists:
- **Round-trip:** `generate(intent)` → document → XML must pass the codec round-trip harness (the same
  350/350 guard). A generated map that doesn't round-trip is a generator bug.
- **Mirror consistency:** `RegionCategorizer.DeriveFacets(generate(intent))` should **recover the
  intent's classification** (the spawn protection reads back as `spawn/protection`, the wool room as
  `wool/room`, the build union as `build`, monuments as `wool/monument`). Generator and categorizer are
  inverses; this is the strongest test that generation produced *correct* structure, not just *valid*
  structure.
- **Playability gate (export gate):** `/traversability` must report the spawn↔wool chain `connected`
  before the map can export (§6). A valid, mirror-correct document can still be *unplayable* (islands
  not bridged); this is the only check that catches it. Block/warn on export when it isn't connected,
  pointing the author at the disconnected gaps.

---

## 10. Scope & non-goals

- **New maps only.** No migration of existing maps to the intent model; no intent inferred from the
  corpus.
- **Symmetric, simpler maps first.** The generator targets clean symmetric CTW layouts (the
  thunder_blank class). Highly irregular/asymmetric maps may not be expressible at first — that's
  acceptable; the bar is "a valid map PGM can load," not "every map."
- **Generated structure may differ from a human's.** Canonical generator output (auto-unions, template
  filters) is the goal, not byte-matching an existing map.
- **No build-area "holes" (complement build regions).** The build slice emits `negative(union(areas))`
  only. Corpus survey: 234 maps have build regions, 32 (14%) use a `complement` in the build structure —
  but **45/49 of those are the void wrapper merely spelled as a complement** (`base − buildable rects`),
  i.e. semantically identical to our `negative(union)`. Only **3** maps use a genuine "build base − holes"
  form, and the holes are regions we already generate and protect separately (wool rooms, observer spawn).
  So holes add ~nothing here. If a target map ever needs a real no-build cutout, add `Holes: List<Rect>`
  to `BuildIntent` and emit `complement(union(areas), hole…)` (mirror still holds). Until then: YAGNI.

---

## 11. Open decisions

- **Intent schema location/typing** — a typed C# model + a JSON shape for the blob; where the
  contract lives (here vs `data-model.md`).
- **Team-count inference detail** — exact symmetry-order → count table, and the correction UX.
- **Partial/invalid intent** — how the generator + UI handle an incomplete map (draft of a draft):
  generate what's valid, surface what's missing.
- **First vertical slice** — recommend **Teams on thunder_blank** (exercises symmetry→count,
  orbit-fill, auto-wiring, and idempotent regeneration in one slice).
