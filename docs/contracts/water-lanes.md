# Water lanes — the route that opens mid-match

A **water lane** is a gap between islands that becomes bridgeable part-way through a match, adding a
late way to reach the wool. It is a CTW idea in practice and a PGM one in mechanism, so nothing about
it is specific to the objective a map happens to carry. Three separate authors name the mechanic in
their own boss-bar text, and "bridgeable" is the word they all reach for: piorun's *Middle lane will
become bridgeable*, dominion's *Crescent Void gaps will be bridgeable in*, vesuvius's *Farlanes will
become bridgeable*.

This doc owns the mechanism, the four wirings the corpus authors it in, how the studio detects each,
and the one it authors. It graduated out of `destroyables-and-cores.md` §14, which retains only a
pointer here: the oldest wiring *is* a destroyable, which is why the two were documented together
until the phantom classifier made this one buildable.

Read alongside:
- `destroyables-and-cores.md` — the phantom destroyable (OB16) the first wiring is made of.
- `plan-editor.md` — the plan schema. A lane is a zone kind.
- `new-map-authoring.md` §5 — the build slice a lane is deliberately not part of.

---

## 1. The mechanism is the void filter, and it reads y=0 live

Everything here follows from four lines of PGM. From `filters/matcher/block/VoidFilter.java`:

```java
return block.getY() == 0
    || (!WorldProblemListener.wasBlock36(world, x, 0, z)
        && world.getBlockAt(x, 0, z).getType() == Material.AIR);
```

A column is **void** iff the block at `(x, 0, z)` is air and was not a block-36 marker, so
`<apply block="deny(void)">` denies building in that whole column. The decisive detail is that
`getBlockAt` is evaluated **at query time, not at load**: put anything non-air at `y=0` and the column
stops being void from that instant.

That is the entire trick. Fill `y=0` with water fifteen minutes into a match and a gap nobody could
bridge becomes buildable, so players reach ground that did not exist as a route before. Water is
incidental to it — any block would do — and the authors chose water because it reads as a lane and
does not obstruct.

Two consequences shape everything below. First, **a lane is always the single block layer at `y=0`**:
a region that misses the void layer cannot open a route however it is filled. Second, **a lane is a
set of rectangles, not a path**. Every corpus instance is a union of cuboids spanning `y = 0..1`;
shapes vary (vesuvius unions an elongated `right` and `left` straight, newgen_classic four compact
~7×8 patches one per team) and a corner lane is expressed the same way, as more rectangles.

The same `y=0` rule is why a stained-glass slab at the world floor reads as a build region, and why
`wasBlock36` exists at all — the invisible marker declares "buildable" without placing a visible
block.

**The water bucket is unrelated, and well camouflaged as related.** It is a universal CTW movement
tool (place water under yourself to cancel fall damage), carried by roughly half the corpus `ctw/` maps,
almost none of which have a lane. At that base rate, "most lane maps carry a bucket" is what noise looks
like.

---

## 2. Four wirings, and only one worth authoring

The corpus writes the same mechanic four ways. Three open a route mid-match; the fourth carries the
name without the behaviour.

**Phantom — a hidden destroyable swapped by a mode.** The oldest. A `<destroyables show="false">`
whose materials are `air` (or `air;water`) over a `y=0` region, plus a `<mode>` that swaps it to water
at a match time. Ownership is vestigial: authors split one lane into per-team halves purely because
`owner` is required, so piorun's `mid-blue` and `mid-red` share an edge and are one straight lane.
This is a destroyable used to script the world rather than to state a goal — OB16's phantom, and the
reason a lane looked like a DTM objective until the classifier landed. dominion carries a `<!-- TODO:
replace fake lanes destroyables with fill actions -->`: an author migrating away from it in place.

**Fill — an inline fill action on a trigger.** No destroyable and no mode; an `<action>` containing a
`<fill>`, fired by a `<trigger>`:

```xml
<actions>
  <trigger scope="match" filter="add-side-lanes">
    <action><fill region="lanes" material="water" filter="only-air"/></action>
  </trigger>
</actions>
```

`FillAction` takes `region` + `material` (+ optional `filter`, `update`, `events`) and writes the
blocks directly. The `filter="only-air"` some maps pass is a guard on *which blocks may be
overwritten*, so the fill cannot eat terrain; it says nothing about when the lane opens.

**Include — a shared fragment plus a conventional region id.** The newest, and the cleanest factoring
of the three. The behaviour is lifted out entirely: the map declares only *where* the lanes are, under
an agreed id, and pulls the rule in.

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

Every map that writes it follows this shape, and **not one applies anything to its lane regions** —
zero `<apply>` on `water-lanes` / `blue-lanes` / `red-lanes` across the whole set. Most contain no
fill, no destroyable and no mode at all. The fragment carries the entire mechanism, keyed by the
region id alone.

**Static — the name without the behaviour.** A region under the lane naming convention that nothing
fills, swaps or applies, appearing only inside the negative that the void rule denies. It opens
nothing. The water already stands in those columns, and the id keeps the void rule *off* them so that
draining the water does not make the ground unbuildable — moai says so in a comment: *Applying regions
to allow building when water removed*. Reported as its own form rather than folded in with the three
that open, because calling it a mid-match route would be false.

---

## 3. Detection

`WaterLaneDetector` (`PgmStudio.Pgm.Detect`) reads a parsed `MapXml` and returns one `WaterLane` per
lane: its form, the region that holds it, when it opens in the map's own words, its `y=0` footprint,
and the element that carried the verdict.

The geometric test is the same for all four and comes straight from §1: **a region contains a cuboid
whose Y span covers `y=0`**, or a flat `<rectangle>` (unbounded in Y, so it always does). Nothing else
qualifies, and that single condition is what separates a lane from every other body of water in a map
— a decorative pool, a flag-status indicator, an ornamental moat. It is a discriminator rather than a
heuristic: the columns under water above the floor were never void, so the rule that a lane suspends
never applied to them.

Each form is then one further fact:

| Form | The signal |
|---|---|
| `Include` | `<include id="water-lanes"/>` **and** a region under the matching id |
| `Fill` | a `<fill material="water">` whose region reaches the void layer |
| `Phantom` | a `show="false"` destroyable over the void layer, in a mode whose material is water |
| `Static` | a region named after the convention that none of the above claimed |

The forms are **ranked in that order** and a region is reported once, under the strongest that claims
it: a map that both fills a region and names it under the convention has one lane, not two. A claim
covers the named region and its whole subtree, so a union claimed by one wiring cannot be re-reported
through its own leaves. Two further suppressions apply to `Static` alone, since it is the only form
read off a name rather than a mechanism: it yields to a claim from below (a lane-named union whose
children are filled one by one is that map's fill wiring), and it yields to a claim on the same ground
(a lane commonly carries a flat companion region covering the identical footprint under a
near-identical name, which is one lane described twice).

A transform is its own place, not a second visit to its source — reflecting a lane produces a second
lane somewhere else — so a `<mirror>` contributes its own derived footprint rather than collapsing
onto the region it reflects.

Run it over a corpus with `dotnet run --project tools/PgmStudio.RoundTrip -- --water-lanes`.

**Measured over 563 maps (403 within the supported range): 19 lanes across 15 maps** — 3 `Include`
(ad_astra, bridgid_ii, rushers_vs_defenders), 5 `Fill` (cannonquad_ii, icecream_sandwiched_ii, lupa,
malupa, tulip_mania_ii), 4 `Phantom` (dominion, newgen_classic, piorun, vesuvius) and 3 `Static`
(hearts_of_atlantis, moai, veld).

Two of those are worth stating plainly, because both were found by the `y=0` rule rather than by
looking for lanes. cannonquad_ii is a **blitz** map, not CTW: it fills a `y=0` `center-ground` region
with water on a trigger, which is the mechanic exactly, in a gamemode nothing predicted. And
hearts_of_atlantis is **DTCM**. A lane is a property of the void filter, so it belongs to no gamemode.

---

## 4. Authoring — the include form, and it costs one string and a region

The studio authors the `Include` form and nothing else. Reading a fragment is not required to emit
one: the server resolves it at load, so a map states the id and the behaviour arrives with it. That
makes the newest wiring also the cheapest — no `<actions>`/`<fill>`/`<trigger>` writer, no mode, no
hidden destroyable.

A lane is authored as a **zone kind** in the plan (`plan-editor.md`). Both kinds are a rect over the
void saying where players may bridge; they differ only in *when*:

```jsonc
"zones": [
  { "id": "mid-band", "rect": [-3, -5, 6, 10] },                    // open from the first tick
  { "id": "lane-e",   "kind": "water-lane", "rect": [3, -2, 2, 4] } // opens mid-match
]
```

The default kind is **not stored** — a build zone writes no `kind` at all — so a plan of build zones
serialises byte-for-byte as it did before the field existed. That matters beyond tidiness: a composed
plan's JSON is its identity (`ComposerFingerprint`), and a field appearing on every zone would read as
a geometry change in every board.

`PlanCompiler` routes the two apart. Build zones fan into `BuildIntent.Areas` as before; water lanes
fan into `WaterLaneIntent.Rects`, by the same symmetry and the same dedup, so a lane drawn on one side
opens on every side. A lane's holes are dropped rather than carried: the fragment fills a flat region
and has nowhere to put a cutout, so honouring one would promise something the export cannot deliver.

`WaterLaneGenerator` then emits the region — one `y=0` cuboid per lane (`min y = 0`, `max y = 1`,
which is that layer alone since PGM cuboid bounds are half-open), unioned under `water-lanes` when
there are several and named `water-lanes` directly when there is one. The include is paired to the
region at export: a map has it iff it has the region, so the two cannot drift into a region no server
rule fills or a fragment matching nothing.

**A lane is never added to the buildable region, and that is the whole point.** The build slice wires
`deny(void)` over everything outside the build area, so a lane left out of it is closed at kickoff and
opens the instant the fragment floods it. Adding a lane to the build area would open it from the first
tick and destroy the mechanic. Nothing else needs wiring — the generated `<apply>` set is untouched,
matching the corpus maps, which apply nothing to their lane regions either.

For the same reason a lane takes no part in the derivations that describe the **starting** board:
gap connectivity, frontline, buildable regions, walkable surface and the fanned graph all read
`PlanModel.BuildZones`, not `Zones`. Treating a lane as a connection would tell the lint the map is
joined up at a tick when it is not.

One lint is the lane's own. **WL1 — a water lane covers void, never terrain.** A lane opens because
water at `y=0` stops the columns reading as void; over a piece those columns already hold terrain, so
that part of the rect changes nothing and the drawn lane overstates the route it adds. (A build zone
may overlap terrain by design, so the rule is the lane's alone.) `BZ5` applies to both kinds — a lane
reaching a spawn is the same fault arriving later, and later is worse, because the defenders have
already committed to the map they read at the first tick.

---

## 5. What the include's body would add, and why it is not needed

The fragment's content is unread and unreadable here: PGM resolves an include from
`config.getIncludesDirectory()`, a server directory that ships with neither the map nor the corpus.
Whatever `water-lanes` defines — the timer, the fill, the messages — has never entered a document this
studio has analysed.

That gap is real and belongs to the include problem generally, not to lanes: **198 of 208 `ctw/` maps
(95%)** reference an include, `gapple-kill-reward` alone accounts for 304 of the 371 referencing maps
across all gamemodes, and PGM splices a `global` include into every map at the root that no `map.xml`
mentions. `MapXml.Includes` records every id a map
references and `MapValidity` warns per unresolved id, so an analysis that is reading an incomplete map
says so. Closing it is a fetch — obtaining the include library — not a code change.

It gates neither half of this document. Detection needs the include's *presence*, not its body:
`<include id="water-lanes"/>` plus the matching region is the signal. Authoring needs even less,
because the server does the resolving. What the body would add is the one thing neither needs — the
lane's timing, which an `Include`-form lane reports as unknown rather than guessing.
