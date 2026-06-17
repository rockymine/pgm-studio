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

> **Status:** backend landed (generator + persistence + orbit-fill + export gate, all unit-tested);
> frontend authoring UI not yet built. New maps only — e.g. `thunder_blank` (a no-xml copy of thunder).
> Existing corpus maps stay region-first and are untouched by any of this.

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
  **Implemented** as `SymmetryExpander.Expand`, applied **by default** at the top of
  `IntentGenerator.Apply`: when `MapIntent.Symmetry` (`SymmetryIntent{Mode, CenterX, CenterZ}`) is set, it
  rotates/reflects the authored spawn(s) and wool(s) onto the other teams *before* any slice runs.
  **Orbit-order convention:** `Teams` are listed in orbit order — step `k` maps the unit owned by
  `Teams[i]` onto `Teams[(i+k) % n]` (and a wool's capturing-team / monument team shifts by the same `k`).
  An already-authored team is never overwritten, so any orbit member stays hand-correctable. Build areas
  are **not** orbited (a flat union with no per-team identity; seeded symmetric from the islands, §6).
  Yaw is transformed by running its facing vector through the same op. *(Not yet orbited: build holes,
  observer spawn — both intentionally global.)*
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
| confirmed symmetry (no teams) | **team count** from the symmetry order (`rot_90` → 4, `mirror_*` → 2…) **and the teams themselves** — `SymmetryExpander.SynthesizeTeams` assigns palette colours (`TeamPalette`: red/blue/green/yellow…), team 0 anchored to the authored spawn's id. Author-provided teams win. |
| team count | **monuments per wool**: 2-team → 1, N-team → N−1 (every team *except the owner* must capture it) |
| spawn point (+ optional protection) for team 0 | the other teams' spawns/protection via orbit, plus spawn-protection wiring (template 2) |
| build rectangles (over-void bridges) on one side | the other sides' build rects via orbit (+ dedup of centre/axis pieces), auto-**union** + void filter on the negative (template 1) |
| wool-room rect, owner | defense + build/break wiring (templates 3, 4); deny-enter for the defender |

**Auto-unioning over hand-grouping.** The author never builds union/complement structure by hand. The
generator unions the regions a template needs (bridges for void, spawn rects for protection, etc.) and
applies the filter to the union/complement. This is why the shaping activities **stop showing the
region tree** (§7): there is no author-managed structure to show — structure is an artifact.

**Coordinate flooring — match how PGM parses each field, don't normalize blindly.** The wool slice floors
the wool `<location>` to a block but passes the monument block coordinates through *raw*. This asymmetry
is deliberate and grounded in the PGM parser (`/media/sf_repos/PGM`):

| Field | PGM parse path | Floors? | So the generator… |
|---|---|---|---|
| `<wool location="x,y,z">` | `XMLUtils.parseVector` → raw `Vector`, kept for proximity distance | **no** (never block-snapped) | **floors it** — keep the wool's goal reference block-aligned |
| `<monument><block>x,y,z</block>` | `BlockRegion(Vector)` → `new Vector(getBlockX(), getBlockY(), getBlockZ())` | **yes** (PGM floors itself) | **leaves it raw** — re-flooring would be redundant |

The rule: floor a coordinate iff PGM *won't*. A `<block>`/`<point>` region is already block-snapped by its
region ctor; a bare proximity `Vector` is not. (Verified by static read of `wool/WoolModule`,
`regions/RegionParser`, `regions/BlockRegion`; the generated XML exports valid.)

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
- **The y=0 terrain footprint is the void-filter substrate — it is *not* turned into build regions.**
  PGM's void filter (`block = not(void)` applied to the *negative* of the build group) makes any column
  that has a block at the surface automatically editable; the islands' terrain therefore **is** the
  buildable area, with **no `region` rows generated for it** (see PGM `regions/VoidFilter` + `BlockRegion`).
  `/buildability` and `/traversability` both run on this y=0 footprint. So **`BuildIntent.Areas` are only
  the over-void extensions** — the bridges/platforms the author wants buildable *across the void* between
  islands — never the islands themselves. (This corrects the earlier "`/islands` → build areas" framing:
  islands seed team count/positions and the buildability overlay, not build rectangles.)
- **symmetry + island count → team count & positions** (4 symmetric islands ⇒ 4 teams). The count feeds
  team synthesis (§4); the positions seed where the author drops each side's spawn.
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
     build             ← over-void bridges between islands (terrain itself is auto-buildable via the void filter)
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

### 6a. The minimal World step — scan-layer selection (settles ND2)

"Configure" (the old scan step) is the most unintuitive part of the editor; ND2 strips the 01-World step
to **Scan → Islands → Symmetry**, just enough to seed team count + spawn positions + a confirmed symmetry.
The author should **confirm, not configure** — so the studio produces one good detection layer automatically:

- **Detection runs on an auto-cleaned base layer.** The `Base` (lowest-solid) extractor reads bottom-up, so on
  decorated terrain it "looks up" into overlapping tree branches and water and the island picture jumps. The
  fix is a **fixed, corpus-derived noise exclusion** baked into the base extractor. The reference project's
  curated `map_layouts.json` (231 maps) justifies the set — the block ids it hand-excluded were
  **water/lava (30 hits) · cobweb (19) · foliage = leaves/logs (9) · redstone lines (6)** (plus a few
  map-specific decor blocks). So the cleaned base = `Base` minus `{water, lava, leaves, logs, saplings,
  tallgrass, vines, lily_pad, redstone_wire, tripwire, cobweb}` (extends the existing `{36}` piston-marker
  default). *(The exact foliage id set may want a render-comparison pass during `N01`.)*
- **Tiny islands are pruned — which is why most "exclusions" don't matter.** `IslandDetector` already drops
  components below `minIslandSize` (=10): **0 of 2780 corpus islands are < 10 blocks.** So 1×1/2×2 specks —
  cobwebs, redstone dots, lone saplings — never become islands regardless. Excluding *them* is cosmetic; the
  island-affecting noise is the **connected** masses (foliage canopies, water bodies), which the cleaned base
  removes. This is why `thunder`'s `[30]` (cobweb) exclusion doesn't change its islands.
- **Floating structures over void are pruned by a Y-discontinuity, not a height threshold.** The base scan is
  bottom-up, so a decorative structure floating over void (no terrain beneath) has its *lowest* block "shot
  into" by the laser and joins the picture far above the play surface (e.g. `mame_i_shrunk_the_pvpers`'
  built **eagles** at Y≈50–82 over a play surface at Y≈0). A global height cap fails (the Y-histogram isn't
  cleanly bimodal). The fix is **height-aware connectivity**: when detecting islands, two adjacent base cells
  join only if their Y is *continuous* (|ΔY| ≤ ~3); a **stark Y jump** breaks the link, so the floating mass
  becomes its own connected component and is pruned as a **height outlier above the terrain's dominant Y band**
  (or as a mass with no support below). Validated on mame's lowest-solid layer: height-aware components isolate
  the eagle masses (median Y≈70) for pruning while the play surface stays whole.
- **Water is the usual "bridge" — and the cleaned base already removes it.** On `mame` the four islands are
  joined at Y=0 by **5,796 `water_still` cells** (the reference dodged it with `bedrock`). But water is in the
  noise set, so the cleaned base separates them on its own: island detection on mame's lowest-solid gives one
  blob with all blocks, `[6700, 6700, 1902, 1902]` with water/lava removed, and **`[6700, 6700, 1894, 1894]`
  with the full noise set — byte-identical to the bedrock layer's islands.** So the two cleanup passes compose:
  height-aware connectivity drops the floating eagles, the water/lava exclusion drops the bridges, and the
  result matches what hand-picking `bedrock` would have produced — no fallback needed for this case.
- **Layer fallback is just a safety net now.** Because the cleaned base recovers bedrock-quality islands on the
  motivating case, the bedrock/y0 fallback is demoted to a cheap last resort: if the cleaned base still reads
  degenerately (one giant island, or no symmetry where the island count suggests it), retry on `bedrock`/`y0`.
  Still studio-chosen, not user-chosen — "confirm, not configure."
- **Surface is kept as a visual aid; segments ride along.** The top **surface** layer is messy for detection
  but **essential for orientation** — the author toggles to it to see the map they built (wool rooms, spawns).
  So the World canvas offers **Base (detection) · Surface (visual) · Segments** views over the same map.
- **No user block-exclude UI, and no `P8` for the common case.** Because the clean-up is a fixed default (not
  per-map config), the happy path never triggers a pipeline re-scan. A rare map that still needs a bespoke
  exclude/override remains a `P8`-gated escape hatch, out of the minimal flow.
- **Island exclusion needs no re-scan — confirmed in code.** `PATCH /configure/{slug}/exclude-island` only
  drops the cached `symmetry_json` artifact; `islands_json` is untouched and symmetry recomputes minus the
  excluded ids (B7). Symmetry is detectable on **99%** (332/335) of corpus maps once islands are clean, so the
  detected order reliably seeds the team count.

**P7 fits here.** The clean base is the `Base` per-layer extractor with an expanded default ignored-block set,
and Surface/Segments stay distinct extractors used for their own roles. That settles P7's deferred "consolidate
the layer extractors vs keep the exact per-layer ones" question in favour of **keeping them** — each has a
distinct solid-policy and purpose (detection / visual / vertical). (P7's other half, byte-parity of a
segment-derived surface, is unaffected.) **Build split:** the extraction-model work (expand
`LayerExtractors.Base` exclude · `IslandDetector` height-aware connectivity + floating-mass prune ·
degenerate-read fallback) is task **`A5`**; the World step that surfaces it is **`N01`**.

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
- **Playability gate (export gate) — implemented.** `GET /map/{slug}/xml` now runs `Traversability.Check`
  before rendering, and for **intent-authored maps** (those with a `map_intent_json` blob) returns **HTTP
  409** with the failure message + the isolated spawn/wool points when the chain isn't `connected`. A
  valid, mirror-correct document can still be *unplayable* (islands not bridged); this is the only check
  that catches it. The gate is scoped to intent maps on purpose: corpus maps have no intent (and may have
  no scan layers) and keep exporting unconditionally, unchanged.

---

## 10. Scope & non-goals

- **New maps only.** No migration of existing maps to the intent model; no intent inferred from the
  corpus.
- **Symmetric, simpler maps first.** The generator targets clean symmetric CTW layouts (the
  thunder_blank class). Highly irregular/asymmetric maps may not be expressible at first — that's
  acceptable; the bar is "a valid map PGM can load," not "every map."
- **Generated structure may differ from a human's.** Canonical generator output (auto-unions, template
  filters) is the goal, not byte-matching an existing map.
- **Build-area "holes" (complement) — supported.** `BuildIntent.Holes` are no-build cutouts subtracted
  from the area union: the build slice emits `buildable = complement(build-area, hole…)` (PGM `complement`
  = first child minus the rest) and wraps *that* in the void negative; with no holes it stays a plain
  union. Holes orbit alongside areas on symmetric maps and read back as `build` (the categorizer walks the
  complement subtree). **Why now, not YAGNI:** the region-categorized corpus survey (350 maps) found
  **16/233 build maps (~7%)** use a *genuine inner* complement (a real cutout), well above the earlier
  "only 3" estimate — and per the authoring rule, a `complement` is **deliberate intent** (unlike a union
  overlap, which PGM ignores and we may freely re-decompose), so it must be expressible and preserved.

---

## 11. Open decisions

- ~~**Intent schema location/typing**~~ — **resolved.** Typed C# model `MapIntent` (+ `SymmetryIntent`)
  in `PgmStudio.Pgm/Editing/`, persisted as the camelCase `map_intent_json` blob; this doc is the contract.
- ~~**Team-count inference detail**~~ — **resolved.** `SymmetryExpander` derives the count from the mode
  (`SuggestedTeamCount`: `rot_90`→4, else→2) **and synthesizes the teams** from `TeamPalette`
  (red/blue/green/yellow…) when none are listed; author-listed teams win. Still open: the **correction UX**
  (override the count / recolour) — a frontend concern.
- ~~**Build holes (complement = author intent)**~~ — **resolved (implemented).** `BuildIntent.Holes`
  emits `buildable = complement(build-area, holes…)`, orbits with the areas, and reads back as `build`
  (mirror + XML round-trip tested). Overturns the old §10 YAGNI on the strength of the ~7% corpus finding.
- **Partial/invalid intent** — how the generator + UI handle an incomplete map (draft of a draft):
  generate what's valid, surface what's missing. (Backend tolerates it: null/empty slices are skipped.)
- **First vertical slice** — recommend **Teams on thunder_blank** (exercises symmetry→count,
  orbit-fill, auto-wiring, and idempotent regeneration in one slice). Backend ready; UI not started.
- **Yaw under reflection** — the facing-vector reflection is exact, but whether a *mirrored* spawn should
  face the symmetric direction or be re-aimed at map centre is an authoring-taste call to revisit with the UI.

---

## 12. The authoring wizard shell — navigation & gating (settles ND1)

The Configure wizard (route `/maps/{id}/configure`, UI label **Configure**;
see `routing-and-ia.md`) is a **guided wizard**, not the free-form region editor. Its chrome is three
levels (the concept page's `NavModelSection`), and this section pins where the flow overview, the
pre-flight checks, and phase locking actually live.

**Three-level navigation**

1. **Activity rail** (left, unchanged from the existing editor) — the **six phases**:
   `0 Map Info · 1 World · 2 Teams · 3 Build · 4 Wools · 5 Review & Export`. A completed phase carries
   a green dot, the current one a left bar, a locked one is dimmed. The rail **logo returns to the
   landing screen**. Jump to any *unlocked* phase here.
2. **Flow bar** — its **own strip above the workspace** (never inside the canvas sub-bar, which keeps
   its draw toolbar). Left-to-right: a **phase-identity cluster** (the current phase's icon + name,
   so the strip always names where you are) · the phase's **sub-steps** (check = done, accent
   underline = current; a **single-step phase shows no sub-steps** — just the phase name, e.g. Map
   Info) · **Back / Next** on the right, **always present**, with **Back disabled at the first step**.
3. **Back / Next** advances one sub-step / phase at a time (the linear path).

Every phase uses this same shell — including **Map Info** (the form-only phase 0; its Back is disabled
as the entry point). There is **no per-step "Save & continue" button**: each phase persists its slice via
`PUT /map/{slug}/intent`, but *when* that fires (autosave-on-change vs save-on-Next vs explicit) and
*how the save / dirty state is shown* (topbar vs flow-bar vs a global indicator) is the wizard's **save
model — deferred to ND4** (it applies to all phases, ties into the idempotent regenerate-on-save of §3
and the resolve-at-save Mojang lookup).

**The landing / home screen (ND3 — `LandingSection`).** `/maps/new` (the Import landing) opens here,
and the rail logo returns here. It is **a workspace with its own flow bar** (phase = **Import**) walking three sub-steps —
**Source → Found → Plan** — the same three-level chrome as every phase, so the entry feels like the rest of
the wizard. Only **Found** has a central canvas (the scanned-world preview); **Source** and **Plan** are
panel-only. The sub-steps:

1. **Source — pick a world.** *Now:* **open a local folder** — a world folder under the maps roots that
   has `region/*.mca` but **no `map.xml`** (the new-map candidates); the studio scans it directly. A
   folder-path field + "Browse" is the manual fallback. *Later (deferred):* a **download-link field** —
   paste an Overcast / S3 `//download` zip URL and the studio fetches + imports it (the `import-from-url`
   half of **`B8`**). The field is shown on the landing screen but disabled until B8 lands. This whole
   step exists so we can **validate the new-map approach on real terrain folders before any download
   integration.**
2. **Found — the import brief.** Folder + file list (`region/` count, `level.dat`, `map.xml` absent), the
   **top-down render**, and the detection summary — islands (on the cleaned base, ND2 §6a), symmetry,
   suggested team count, plus the world features the scan already found (wools / resources / chests /
   spawners). This is the **seed of the guidance model**: exactly what the author *confirms* in the phases
   that follow (e.g. the detected island count → team count in World/Teams). Nothing here is final.
3. **Plan — the flow + Start.** The six-phase overview (each phase, its one-line purpose, the
   Build⇄Traversability caveat) and a **Start authoring → Map Info** action (the `Next` on this last
   sub-step, like Review's XML→Export).

Backend: the local **open-folder source** (list xml-less world folders under the roots → create the map
record → `POST /map/{slug}/scan-world`, which exists) is the now-increment of **`B8`**; the URL download
is the later increment.

**Where the checks live.** Validation is **not a phase of its own** (§9; the concept page's
`ValidateSection` is a reference, not a step):
- **Buildability is a live layer toggle inside Build** — the `Buildable` overlay chip on the canvas
  sub-bar, immediate feedback as bridges are drawn (it is the seed + feedback of §6).
- The **buildability and traversability maps**, plus the four **pre-flight checks** (round-trip ·
  mirror · buildability · traversability, §9), render in the **Review** phase, where the whole map is
  validated on entry / on demand. They are not re-run continuously on every edit.

**Review & Export is one phase with three sub-steps** walked by the flow bar:
`Pre-flight → Region tree → XML`. Pre-flight is the checks + the two maps (above); Region tree is the
read-only generated structure (§7 — the inspect/debug surface); XML is the segmented serialized output.
**Export is the flow bar's `Next` on the final (XML) sub-step**, enabled only when the Pre-flight gate
is open (HTTP 409 otherwise, §9) — there is no separate Export button on Pre-flight. On the concept page
these are still three sections (`ReviewSection` / `TreeSection` / `XmlSection`, the `N05`/`N07`/`N06`
build units) but they are one phase. *(Map Info, by contrast, is a single-step phase — no sub-steps.)*

**Phase locking — by prerequisite slice (not a rigid line).** A phase unlocks once the intent slice it
depends on exists; you may always jump *back* to any **done** phase to edit it:

| Phase | Unlocks when |
|-------|--------------|
| 0 Map Info | always (entry) |
| 1 World | map exists (Map Info saved) |
| 2 Teams | World symmetry **confirmed** (seeds team count/positions) |
| 3 Build | Teams defined (spawns placed) |
| 4 Wools | Build slice exists — **build must precede wools** (traversability is computed over the build geometry, §6) |
| 5 Review | all required slices present (teams + build + wools) — i.e. there is a complete map to check |

**"Review needs a connected map" is the *export gate*, not the phase lock.** Review unlocks on
*completeness*; the **connectivity** requirement is enforced at export: `GET /map/{slug}/xml` runs
`Traversability.Check` and returns **HTTP 409** for intent maps whose spawn↔wool chain isn't connected
(§9). Locking Review behind connectivity would be circular — the connectivity check runs *inside*
Review. A failed traversability/buildability result there links **back to Build** (the Build⇄Traversability
loop, §6); a failed round-trip/mirror is a generator bug surfaced in the validate log.
