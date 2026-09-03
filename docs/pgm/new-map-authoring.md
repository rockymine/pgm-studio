# The declarative intent model

Why authoring a map runs **meaning → structure** rather than the other way, and the laws that follow from
it. This is the rationale record for `MapIntent`; it does not describe the tool that edits one — that is
`../tools/configure.md`, and the type itself (`Pgm/Authoring/MapIntent.cs`) is the specification of what an
intent holds.

Read alongside `region-categorization.md` (the reverse mapping, structure → meaning — the generator here is
its mirror image) and `filter-region-wiring.md` (the wiring templates the generator emits).

---

## 1. The inversion

The app's original direction is **structure → meaning**: parse XML → `regions` / `filters` / `apply_rules` →
*derive* category, subtype, owner, build areas (`RegionCategorizer`). It has to be, because the corpus only
ever gave structure, and intent had to be recovered from it.

Authoring runs the other way. The author states intent and the system *generates* the graph, which then
serializes to PGM-loadable XML. The generator is the mirror of the categorizer, and the mapping was already
understood in both directions before it was built — categorization and the wiring templates are the proof.

**The corpus was the sample, not the target.** Over-fitting the generator to reproduce a specific map's exact
structure is the failure mode. It is driven by author intent plus round-trip validity, and it is allowed to
emit a *different*, simpler, canonical structure than a human wrote, as long as the map parses and plays.

That inversion is what the whole studio rests on. Before it, authoring meant editing the region graph by hand,
and the corpus is the argument against that. `annealing_iv` states "players may not build in a spawn, except
on the monuments that stand in one" as
`negative not-spawns → union spawns → complement → union of four spawn rectangles, minus twelve monument
blocks` — five levels, one of them subtracting a dozen leaves from a union. It is faithful to the data and
hostile to an author. Stating the spawn and the monuments and deriving that graph is what made a wizard
possible at all.

## 2. Why regeneration is free

`region-data-flow.md` §2 establishes the load-bearing constraint: **every save drops and recreates all
region / filter / apply-rule rows** (`MapWriter.SaveDocAsync` = `DeleteEntities` → `FromDict` →
`WriteEntities`), and editor-only state must therefore live outside the codec or the next save wipes it.

That constraint is what makes the declarative model cheap rather than expensive.

**Intent is a `map_artifact` blob** (`map_intent_json`), like the region-draft sidecar. It survives
`SaveDocAsync`, and the codec and categorizer never see it — it is not part of the PGM document. Its presence
is also what *makes* a map intent-authored, which is how the export gate knows to apply itself and how a
corpus map is left alone.

**Regions are a derived projection.** The generator turns intent into a document dict and persists it through
the normal codec path. The generated regions are fully canonical — they round-trip, they categorize, the tree
and canvas render them unchanged — they are simply output rather than the source of truth.

**Idempotent regeneration is the existing save path.** Because every save already wipes and rewrites every
row, "the author corrected the team count" is just: mutate intent → regenerate → save. No duplicate regions,
no orphaned filters, no diffing. Define once, correct anytime — obtained for free from behaviour that already
existed.

```
author edits ─▶ intent blob (source of truth)
                   │  generate
                   ▼
              PGM document dict ──SaveDocAsync──▶ region/filter/apply_rule rows (canonical, derived)
                   │                                   │  read + RegionCategorizer
                   └──────────── mirror check ─────────┘  (should recover the intent — §6)
```

## 3. Where orbit happens, and why it happens in more than one place

Every orbit in the studio computes the same transform through the same leaf (`PgmStudio.Geom.Symmetry`), but
it is asked three different questions, and conflating them is what makes the code look scattered.

**The plan fans.** A plan *is* one orbit unit by definition — the author draws half a board — so
`PlanCompiler` reads each placement once and emits one per orbit image, taking the team from the orbit index.
Every kind fans there: spawns, wools, iron, destroyables, cores, build zones, walls. Nothing about a plan is
ambiguous, because the plan is authored in the frame the orbit is defined in.

**Configure assigns by coverage.** An imported world is not authored half-first; the author drops a spawn on a
real island. Which team an orbit image belongs to is then a *spatial* question — the island it lands on — and
only the client has the islands. So the spawn, protection, wool-spawn and wool-room steps compute the orbit
through `OrbitAssignment` and **store the copies as real intent entries**, keyed by the anchor each image
covers rather than by orbit order. Storing rather than deriving is deliberate: the assignment is a judgement,
so it must be visible and hand-correctable.

**Generation fills what is still missing.** `SymmetryExpander` runs at the top of every projection and
completes an intent that carries only one unit — the property that makes "a symmetry plus one spawn" a whole
map. It maps orbit position to team *in list order*, which is the weaker rule, so it never overwrites: an
authored or already-assigned team is left alone. On a plan-built or fully-configured map it therefore does
nothing at all.

**The canvases only draw.** The JS ghosts — `plan-doc`'s mirror images, the sketch mirror layer, the editor's
`setAuthorMirror` — are previews. They store nothing and decide nothing.

**A position and a cell do not turn the same way, and the leaf answers both.** A board whose extent is even
turns about a **cell boundary**, not about a block: the rot_180 image of the position `x` is `−x`, but the
image of the *cell* `x` — the unit square from `x` to `x+1` — is the cell `−1−x`. Anything with a footprint
therefore takes `Symmetry.Cell`/`Symmetry.CellRect`, which reflect the cell's middle and floor, while
`Symmetry.Point`/`Symmetry.Rect` stay for what is genuinely a position: a prop's offset from its own anchor, a
drawn outline, a facing. Reflecting a footprint as a position and flooring the result afterwards lands its
image one block short of where the terrain puts it — the ground mirrors and the stamp does not, nothing fails,
and the map is a block asymmetric in play. That is what a goal's anchor and a building's wing rectangle each
did until `B250`; both now convert to the cell first and turn the cell, and `ObjectiveFootprint.AnchorCell` is
the one conversion the compiler's fan and the world's stamper share so the two cannot round apart.

The rule that keeps this coherent is that **coverage is a property of the entity, not of the tier**. Every
objective orbits in all three, or the answer to "is this map fair?" changes depending on which tool the author
happened to use. That rule was broken once and cost a whole class of map: `Expand` rebuilt the intent by
naming its fields, so the four slices added after it was written — destroyables, cores, island teams and the
plan's stamped structures — were deleted on any intent carrying a symmetry, silently. It now expands with
`intent with { … }`, so carrying is the default and only a transform is spelled out.

One consequence is worth stating because it surprises people: **a compiled intent carries no symmetry on
purpose.** The field is what switches the expander on, and the expander rebuilds an intent from a fixed
property set; setting it on an intent that is already fanned would delete the structure directives it exists
to deliver. So a plan-built map confirms its symmetry in Configure, and a rebuild clears that confirmation
again (`IntentCarry`).

## 4. Coordinate flooring — match how PGM parses each field

Do not normalize blindly. The wool slice floors the wool `<location>` to a block but passes the monument block
coordinates through **raw**, and the asymmetry is grounded in the PGM parser (`/media/sf_repos/PGM`):

| Field | PGM parse path | Floors? | So the generator… |
|---|---|---|---|
| `<wool location="x,y,z">` | `XMLUtils.parseVector` → raw `Vector`, kept for proximity distance | **no** (never block-snapped) | **floors it** — keep the wool's goal reference block-aligned |
| `<monument><block>x,y,z</block>` | `BlockRegion(Vector)` → `new Vector(getBlockX(), getBlockY(), getBlockZ())` | **yes** (PGM floors itself) | **leaves it raw** — re-flooring would be redundant |

The rule: **floor a coordinate iff PGM will not.** A `<block>` or `<point>` region is already block-snapped by
its region constructor; a bare proximity `Vector` is not. Verified by static read of `wool/WoolModule`,
`regions/RegionParser` and `regions/BlockRegion`; the generated XML exports valid.

## 5. The buildable substrate is the terrain, not a region

PGM's void filter — `block = not(void)` applied to the *negative* of the build group — makes any column with a
block at the surface automatically editable. The islands' terrain therefore **is** the buildable area, and
**no region rows are generated for it** (PGM `regions/VoidFilter` + `BlockRegion`).

So `BuildIntent.Areas` are **only the over-void extensions** — the bridges and platforms the author wants
buildable *across* the void between islands — never the islands themselves. `Holes` are the no-build cutouts
subtracted from that union, emitted as a PGM `complement`, and they are genuine authored intent rather than an
incidental overlap: the region-categorized corpus survey found **16 of 233 build maps (~7%)** using a real
inner complement, which is why the shape has to be expressible and preserved rather than re-decomposed.

Two consequences follow, and both are load-bearing elsewhere. The y=0 footprint is what `/editability` and
`/traversability` read, which is why **an intent authored against no scanned world cannot pass the export
gate** — there is no substrate for either to run on. And **build must precede the objectives**, because
traversability is computed over the build and bridge geometry: a wool's reachability is undefined until the
bridges exist, so a failed gate sends the author back to Build rather than to the wool.

## 5a. Four invariants the emitted contract must hold

Whatever produces a map — the intent generators, the editors, a hand-written document — the same four things
have to be true of what comes out, because the readers below assume them.

**Wools are grouped by colour, with deterministic ids.** PGM's `<wools>` block nests by colour, and a map's
wool ids are derived from the colour and the index rather than minted per session, so re-emitting the same map
produces the same document rather than a diff of renamed ids. This is also why the wool codec is the one entity
that bypasses the flat `MapXml` path (`destroyables-and-cores.md` §9): the grouped shape carries wool-level
fields the flat row cannot hold.

**The region and filter registries are id-keyed.** Both are a flat dictionary from id to definition, with
synthetic ids minted for anonymous elements at parse time, so every later reference — a wiring, a compound
member, an objective's region — is a lookup rather than a search.

**A compound's `children` and a transform's `source_id` are string id references**, never inlined definitions.
A region that contains another names it; nothing is duplicated by value, which is what lets
`RegionBoundsDeriver` recompute derived bounds after a DB rebuild without re-parsing the XML.

**A wool's owner team is derived from its capturing `monument.team`**, not stated twice. The monument says who
captures, and the ownership falls out of it — so the two can never disagree, because there is only one of them.

**A monument's `location` is what the build produced, not what it was asked for — on a sketch-originated
map.** The block a wool is won on is one of the blocks the capturing team's own spawn structure *stamps*, so
`WorldBuilder` fills every monument's location from the air cell that stamp left and an authored one is
replaced. Three paths take a location — `POST /map/{slug}/wools/{woolId}/monuments`,
`PATCH …/monuments/{monumentId}` and `PUT /map/{slug}/intent` — and every read answers it, which is why the
replacement is now reported as `OB25` rather than made silently. On a map the studio did **not** build the
location is authoritative: `GET /xml` renders the stored document, and there a monument naming a region
carries its position in that region, so `WoolEditor` moves the `<block>` with it.

## 5b. Void enforcement, decoupled from the build area (B132)

`BuildIntent.Areas` and void enforcement look like one decision — declare where a player may build, and the
void everywhere else follows — but `BuildGenerator.Apply` used to make that literal: it returned before it
ever reached the void-filter wiring when `Areas` was empty, so a map that declared no buildable rectangles at
all got **no void enforcement of any kind**. `approaches.md` states the intended behaviour as settled law — a
void gap with no build region over it is permanent, which is what makes a channel a control on flow rather
than a delay — so the empty-`Areas` case was exercising exactly the opposite of what the void is for, silently,
on every board that never thought to draw a build rectangle.

The corpus says the fix is not "enforce by default". Measured over the 112 `dtcm` maps in `CommunityMaps`: 102
declare a `maxbuildheight`, 68 enforce the void somewhere (56 by the inline `deny(void)` idiom — 31 as
`block-place`, 25 as `block` — the rest through a named `not(void)` filter referenced indirectly), 82 carry a
hard `block="never"` region, and 100 of 112 carry one or the other. Nearly every real destroy map restricts
building somewhere, but by a hard `never` region about as often as by the void — so a studio default of
"enforce the void everywhere a map states nothing" would ship boards stricter than a meaningful slice of the
corpus and change every existing map's behaviour at once. What the enforcing maps show instead is a *stated*
idiom, `alpine_mining_ii`'s own:

```xml
<complement id="build-filter"><everywhere/><union id="obs-spawn">…</union></complement>
<apply block-place="deny(void)" message="You may not build outside of the map!" region="build-filter"/>
```

Two things in it carry over exactly. The rule is scoped to **everywhere minus a small exclusion** (the
observer spawn, which the map's own comment says must stay editable if a player somehow reaches it), never to
a drawn build rectangle — the opposite shape from `Areas`. And it is **`block-place`, not `block`**: a player
may still *break* a block hanging over the void, only *placing* one out there is denied, which is what lets a
destroyable float over open void without its own goal becoming unbreakable.

So `BuildIntent.VoidEnforcement` states this as its own knob: null (the default, and every map's behaviour
before B132) leaves the void bridgeable outside any declared build area; a `VoidEnforcementIntent` — even an
empty one — wires `block-place="deny(void)"` over `everywhere` minus its `Exclusions`, whether or not `Areas`
is populated, and alongside the legacy `not-build-area` void-filter wiring when both are declared, since the two
scope different regions and neither implies the other. `BuildGenerator` emits `negative(exclusion…)` rather
than `complement(everywhere, union(exclusion…))` for this — PGM's `<negative>` already unions its children
before negating, so the two are the same region with one fewer node. An empty `Exclusions` list still
materialises a named `void-enforcement-area` region (typed `everywhere`) rather than referencing the bare
`everywhere` builtin inline, so a second `Apply` can find and clear exactly what the first one wrote.

The plan compiler does not yet derive a `VoidEnforcement` from anything in the plan model — a hand-authored or
agent-authored intent states it directly, the same way `Symmetry` and `Meta` are stated rather than derived.
`SymmetryExpander` orbits `Exclusions` exactly as it orbits `Areas`/`Holes`, so a symmetric map's exclusion
drawn on one side is excluded on every side.

## 6. What validation proves

Three checks, each answering a different question.

**Round-trip** — `generate(intent)` → document → XML must pass the codec round-trip. A generated map that does
not round-trip is a generator bug.

**Mirror consistency** — `RegionCategorizer.DeriveFacets(generate(intent))` should recover the intent's own
classification: the spawn protection reads back as `spawn/protection`, the wool room as `wool/room`, the build
union as `build`, monuments as `wool/monument`. Generator and categorizer are inverses, and this is the
strongest available test that generation produced *correct* structure rather than merely *valid* structure.

**The playability gate** — a valid, mirror-correct document can still be unplayable, because nothing above
asks whether the islands are bridged. `Traversability.Check` does, and `GET /map/{slug}/xml` answers **409**
for an intent-authored map whose spawn↔wool chain is not connected. It is scoped to intent maps on purpose:
a corpus map has no intent, may have no scan layers, and exports unconditionally.

## 6a. A stored intent outlives the shape it was written in

The intent is an artifact on the map row, so a document written months ago is read by today's record — and a
record that has since gained a field cannot read what came before it. That already happened once, silently:
`meta.authors` was a list of usernames before an author could carry a contribution note, and today's
`AuthorIntent` is an object, so every intent from then failed to deserialize **whole**. One map's authors made
everything about that map unreadable — `GET /map/{slug}/intent` answered 400, `POST .../sketch/columns`
answered *could not build layout*, and the sketch canvas reported "no WebGL" for it. Sixteen of the
forty-three stored intents in the development database were in that state.

So the intent reads the retired shape and writes the current one — a bare string is the name it always was
(`AuthorIntentJson`). This is the discipline the studio already keeps for stored documents:
`HouseStyleJson.Upgrade` and `TerrainThemeJson.Upgrade` carry retired names forward, and `RQ3` is a complaint
rather than a refusal for the same reason (`docs/refusals.md`). A migration would have fixed the rows in one
database and still failed on the next document exported before the change; upgrading on read fixes both, and
the document is stored in today's shape the first time it is saved.

**This is not a second accepted format on the wire.** Nothing writes the old shape, no endpoint accepts it as
an alternative, and the writer emits one thing. It is a reader that can still read what the studio itself
wrote.

## 7. Scope

**New maps only.** No migration of existing maps to the intent model, and no intent inferred from a finished
one — recovering meaning from a corpus map's structure is the categorizer's direction, and it stops at
categories.

**Generated structure may differ from a human's.** Canonical output — auto-unions, template filters — is the
goal, not byte-matching an existing map. The author never builds union or complement structure by hand; the
generator unions what a template needs and applies the filter to the union or complement. That is why the
shaping steps show no region tree: there is no author-managed structure to show.

**Partial intent is tolerated.** Null and empty slices are skipped rather than refused, so a half-authored map
still generates what it has — a roomless wool still produces its objective and its monuments, losing only the
room region and its wiring. Completeness is a question the tool asks at its own gates, not one the generator
enforces.

**Symmetric maps first.** The generator targets clean symmetric layouts; a highly irregular map may not be
expressible, and the bar is "a valid map PGM can load" rather than "every map".
