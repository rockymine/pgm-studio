# Water lanes — the route that opens mid-match

A **water lane** is a gap between islands that becomes bridgeable part-way through a match, adding a
late way to reach the wool. It is a CTW idea in practice and a PGM one in mechanism, so nothing about
it is specific to the objective a map happens to carry. Three separate authors name the mechanic in
their own boss-bar text, and "bridgeable" is the word they all reach for: piorun's *Middle lane will
become bridgeable*, dominion's *Crescent Void gaps will be bridgeable in*, vesuvius's *Farlanes will
become bridgeable*.

This doc owns the mechanism, the four wirings the corpus authors it in, how the studio detects each,
and the one it authors. It graduated out of `destroyables-and-cores.md`, whose §10 retains only a
pointer here: the oldest wiring *is* a destroyable, which is why the two were documented together
until the phantom classifier made this one buildable.

Read alongside:
- `destroyables-and-cores.md` — the phantom destroyable (OB16) the first wiring is made of.
- `../tools/plan.md` — the plan schema. A lane is a zone kind.
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

That is the entire trick. Fill `y=0` with water three quarters of an hour into a match and a gap
nobody could bridge becomes buildable, so players reach ground that did not exist as a route before.
Water is incidental to it — any block would do — and the authors chose water because it reads as a
lane and does not obstruct.

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

What it carries is more than a fill. Resolved (`PublicMaps/includes/water-lanes.xml`), it is six
`<apply>` rules, a `<block-drops>` rule, a match-scoped variable, an action pair and the mode that
starts them. Four of the applies protect the lane: a blacklist of blocks denied where the lane was
previously air (anything that breaks by proxy without firing a block-break event — ladders, doors,
rails, plants, redstone), doors denied a second time over the region grown one block upward and
without the was-air condition, because a door stands two blocks tall and each is filtered separately,
pistons and other non-participating block movement denied on the same previously-air ground, and
breaking the water while holding a bucket denied. `<block-drops>` replaces a broken lane block with
stationary water. The fifth apply is the one that opens the lane —
`block="allow(water_lanes_initiated=1)"` over a resize of the region spanning the full column height,
so the deny that kept players out lifts the moment the match variable flips. The sixth denies sponge
placement everywhere rather than only in the lane, since a sponge anywhere drains it.

The opening is two steps rather than one, which is why the fragment holds two numbers. A `<mode>` fires
`after="${water-lane-timer}"` and does nothing but set `water_lanes_initiated` to 1, announcing itself
five minutes ahead (`show-before="5m"`). The `<fill>` that actually places the water sits on a
15-minute `<pulse>` gated on that variable, so it does nothing for the whole first stretch of the
match, lands the water once the mode has run, and re-runs every fifteen minutes afterwards — which is
what refills a lane players have drained. The timer is `water-lane-timer`, declared `fallback` — the
fragment's default is **45m** and a map overrides it by declaring the constant itself, which
`rushers_vs_defenders` does at 30m.

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

When a lane opens is read from the map in every form, the include one included: the fragment declares
`water-lane-timer` as a `fallback` constant, so the map's own value wins and its absence means the
fragment's 45m. That is why `MapXml.Constants` survives substitution — a constant a map never
interpolates is not dead text, it is the setting handed to a rule living outside the document.

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

A lane is authored as a **zone kind** in the plan (`../tools/plan.md`). Both kinds are a rect over the
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

## 5. Resolving the fragment, and why neither half of this document waits on it

The body is resolvable when a library is configured (`IncludeLibrary`, `include-resolution.md`), and
reading it is where §2's account of the fragment's rules, its 45-minute mode and its 15-minute refill
pulse comes from. Neither half of this document depends on that, though, and it is worth being exact
about why.

Detection needs the include's *presence*, not its body: `<include id="water-lanes"/>` plus the matching
region is the signal, and the corpus verdicts are **identical read resolved or unresolved** — the
ranking puts the include form above the fill the fragment brings, and both name the same region.
Authoring needs less still, because the server does the resolving; the studio emits an id and a region.
Even the timing, the one thing that genuinely lives in the fragment, reaches the map as an overridable
constant.

What resolution changes for a lane map is the surrounding picture rather than the lane: ad_astra reads
as 22 filters and 11 apply rules as written, and 95 filters and 17 apply rules as played.
