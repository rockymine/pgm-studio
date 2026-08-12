# What the first fifteen maps got wrong

A review of the boards under `CommunityMaps/ctw` and `CommunityMaps/dtcm` built by `tools/mapgen` from the
specs in `specs/`, read against what the corpus actually ships. It is a **pool, not a board**: the entries
carry doc-local `MG` ids so one can be referred to, and an entry moves onto `TODO.md` with a real id when it
becomes the focus. The order is the pipeline's — board, objectives, ground, paint, dressing, then the reading
back — rather than the order the faults were noticed.

The common thread is worth stating once, because most of the entries are instances of it. The tool reaches
for a **random** answer wherever an author would reach for a **deliberate** one: where a tree goes, which
wood it is cut from, where a building stands, what the ground does, which of the nineteen palette families
paints a wall. Randomness is the right tool for the grain on a surface and the wrong one for everything a
player navigates by. A map is a designed thing, and every one of these is somewhere the design was left to a
seed.

## The board

**MG1 — A destroy map needs a board drawn for it, not a capture board with its wool taken out.**
`objective_mode` composes a capture-the-wool board and rewrites its markers: the wool placement the generator
budgeted and sited becomes a monument, and for `dtcm` a core two cells along. The board underneath is
unchanged — the same lanes, the same hub, the same two rooms at the same distance, sized by a budget that was
solving for a wool run. That is not what a destroy map is. The corpus is the evidence and it was read for the
wrong thing: `--island-study` was run over all 368 destroy worlds to count islands and never to look at their
*shape*. The 320 readable ones are sitting there with their footprints, their monument sitings, their
approach geometry and their build regions, and the way to a destroy board is to read them and compose for
them. The retarget is a shortcut that produced fifteen capture boards wearing three different hats.

**MG2 — A single blanket theme wastes the one thing the layout already gives away.** Every spec paints one
`theme` over the whole map, so a board reads as one material from edge to edge. A shape carries its own
`theme` and its own height, and the two together are how a board says *this part is built and that part is
grown*: a grid-like quarter laid in coursed stone against a quarter of natural ground, a plateau that is
plainly a platform against a slope that is plainly a hillside. Deliberately differing the paint per shape,
keyed to what that shape is for, is available now and unused.

## The objectives

**MG3 — A monument or a core may never stand over void.** Some do. An objective in void cannot be broken:
without a build region under it there is nothing for a player to stand on or mine through, so the goal is
unreachable and the map is unwinnable. This is a hard invariant, not a quality note, and it belongs in the
tool as a refusal — a spec that sites a goal off the ground should fail to build rather than write a board
that cannot be played. The check is cheap: the rasterized ground is already computed before dressing, and the
goal's anchor either has a column under it or does not.

**MG18 — An obsidian goal needs a kit that can mine obsidian, and every destroy map in this batch fails
it.** The plan compiler defaults a destroyable's material to obsidian, which over half the corpus also uses,
and the generated spawn kit carries an **iron** pickaxe. An iron pickaxe does not mine obsidian slowly — it
does not mine it at all, breaking nothing and dropping nothing. So every monument and every core in the
seven destroy boards is indestructible and every one of those maps is unwinnable, in the same way and for the
same reason as a goal standing over void (MG3).

The corpus does not merely prefer the pairing, it never breaks it. Of 312 destroy maps, **86 name obsidian in
a goal's materials and all 86 carry a diamond pickaxe — 100%, no exceptions.** Among the 226 whose goal is
some softer material, 65% carry one anyway, so a diamond pickaxe is common everywhere and *mandatory* where
the goal is obsidian. Only 30 maps carry both a diamond and an iron pickaxe, so the usual shape is a
substitution rather than an addition: the destroy kit is the capture kit with its pickaxe upgraded, which is
what makes it a kit **variant** rather than a second kit.

Two ways to be right, and the tool should do both: pair the kit to the goal material when it builds a destroy
map, and refuse to write a map whose goal material no tool in its kit can break. The second is the one that
survives someone later choosing a different material.

**MG4 — A spawn must not face its exit into the void.** The spawn's yaw decides which wall its door is cut
through, and a spawn on the edge of a piece with its yaw pointing outward puts the only way out over the
drop. The direction is settable and is currently inherited from whatever the compiler fanned. It should be
chosen against the ground: face the spawn at the board, not off it.

## The ground

**MG5 — A relief must not move ground an objective is standing on.** On the capture maps the solved surface
overrides the spawn and wool placements, so a room ends up cut into a slope the relief invented after the
room was sited. The machinery to prevent it is already there and unused: a shape states `relief_scope` —
`hold` pins it at its own stated top so the surrounding surface is solved *knowing where it has to arrive*,
and `exclude` keeps it out of the solve altogether — and `height_mode` with `skirt` sits a stated platform
into the terrain rather than on it. Every structural shape the plan compiler projects (`spawn-*`, `wool-*`)
is exactly the case those words were written for, and none of them sets either.

**MG6 — Relief and dressing compete, and the competition is currently settled by luck.** Steep ground is
unplantable, so the same spec at a harsher relief silently loses its forest — one scarp took a board from 785
leaves to none. The tool reports the two numbers that distinguish "no site was acceptable" from "the pass
dropped what was offered", but nothing reconciles them: the relief is solved first and the trees take
whatever is left. Deliberate placement (MG9) largely dissolves this, since a chosen grove sits where the
ground was chosen to suit it.

## The dressing

**MG7 — Nothing may be placed inside anything else.** Buildings stand inside buildings, trees grow inside
trees, and trees grow through buildings. The village pass keeps buildings a margin apart and the forest pass
keeps trees a margin from buildings *placed before it*, but the margins are small, the checks are
site-against-site rather than footprint-against-footprint, and the symmetry fan places an orbit image that
nothing tested. Overlap has to be decided against the **occupied cells** of everything already standing,
after fanning, not against the anchor a prop was requested at.

**MG8 — An L or a T house is buildable and unauthorable.** `Footprint` holds wings, `HouseStamper` walks them
as one landmass, and the roof of a building is the union of its wings' roofs with the cross-gable built where
they meet (G172). Nothing can ask for it: `HouseProp.Points` is exactly two opposite corners and
`HouseProp.Footprint()` returns one rectangle, and every `new Footprint(...)` in the tree is single-winged. So
two abutting rectangles in the same style do not merge — they are stamped as two buildings that happen to
touch, with two roofs colliding. Reaching the wing model from the dressing path is the single change that
turns a row of boxes into a village.

**MG9 — A forest is placed, not scattered.** Trees are sampled uniformly over every ground cell that passes a
filter, which is why they read as noise: no groves, no treeline, no clearing, nothing thicker where the map
wants cover and nothing bare where it wants sightlines. Trees are cover, and cover is a gameplay decision. The
same argument applies to buildings, which are currently dropped wherever the ground happens to be level.

**MG10 — A tree's parts must agree with each other.** An oak-profiled tree is being built with spruce logs.
The `template` form takes a `Species` — which names the wood, the canopy profile and the proportions
together — and the `grown` form takes a `Wood`; the tool draws from one list of names for both and hands a
species name to a wood field. The result is a tree whose silhouette and timber come from different plants.

**MG11 — Density is a design decision and is currently a number nobody chose.** One board came out at 17,629
leaves, a closed canopy with the terrain, the buildings and the routes all buried under it; another at 173,
which is a bare map with a shrub on it. Roughly 1,000–5,000 on a board of this size reads as wooded. The
README now says so, but nothing enforces or even reports against a target.

**MG12 — The wall decoration is the same everywhere.** Every spec dresses its risers the same way, so every
board's edges read identically whatever else changes about it. The pattern vocabulary — voronoi, cell, noise,
turbulence, electric, over nineteen families — is barely sampled, and the choice is not tied to what the wall
is (a cliff face, a built retaining wall, the side of a platform).

## The XML

**MG14 — The export composer is bypassed, so a generated map carries none of the boilerplate every corpus
map carries.** `tools/mapgen` builds its document and calls `XmlWriter.ToXml(Deserializer.FromDict(doc))`
directly. The path a map is supposed to take is `MapXmlComposer.Compose(doc, isIntent: true, …)`, and
everything it does is therefore missing: `CtwStandards.Apply` (the keep / repair / remove rules derived from
the spawn kit, hunger depletion off, and the shared golden-apple kill-reward include),
`WaterLaneGenerator.EnsureInclude`, `ResourceRenewables.Apply`, `StructureRenewables.Apply`, and the
reordering that puts the `not-build-area` rule **last** — which matters because PGM stops at the first
applicator that decides, and that rule is the one keeping players out of the void (MG3). None of this is
unbuilt: `CtwStandards` derives its lists from the corpus at N=199 and `XmlWriter` already emits all four
elements. The generated maps simply never go through the composer, and one call is most of the fix.

The measure of how wrong that is, over the 365 corpus capture maps outside this batch:

| element | corpus maps carrying it | in a generated map |
|---|---|---|
| `<itemremove>` | 364 (99%) | absent |
| `<toolrepair>` | 345 (94%) | absent |
| `<itemkeep>` | 299 (81%) | absent |
| `<hunger><depletion>off` | 297 (81%) | absent |
| `<armorkeep>` | 3 (1%) | absent, and unwritable |

The hyphenated spellings PGM also accepts — `item-keep`, `tool-repair`, `item-remove` — are used by **no**
corpus map at all, and the writer already emits the unhyphenated forms the corpus uses.

**MG15 — Spawn armour goes in `<itemremove>`, the way the corpus does it.** *(Decided by the author.)*
Leather kit armour currently drops and lies on the ground, because nothing says otherwise. `<itemremove>`
destroys the stack when it spawns as an entity (`ItemSpawnEvent`), so the armour still leaves the body on
death but never litters the field and cannot be worn by the killer; it works because the kit re-applies
team-coloured armour on respawn. That is the convention at 99% of the corpus, and it is already what
`CtwStandards` derives — `m.ItemRemove = kit.Armor.Select(a => a.Material)`. So this entry needs no new code
beyond MG14: routing through the composer produces it. The alternative, `<armorkeep>`, keeps the armour on
the body and is not being taken — three maps in 365 use it, and the writer could not emit it anyway, having
no armour list.

**MG16 — Tools that wear out are meant to be repairable, and a generated map repairs nothing.**
`<toolrepair>` lists materials whose pickup repairs the tool already in the inventory rather than stacking a
second one — picking up a sword restores the held sword's durability by the picked-up one's remaining hits
and the pickup is cancelled. Without it a kit's sword, axe and pickaxe simply break and the player is
disarmed until the next death. 94% of the corpus carries it; `CtwStandards` already derives the list as the
kit's tools and weapons, identified by the material's last word.

One thing to check while wiring it, because the two overlap: the generated spawn kit marks its tools
`unbreakable="true"`, and an unbreakable tool never wears down, so `toolrepair` has nothing to repair on it.
The corpus carries both at once, which suggests the two are belt and braces rather than alternatives — but
whether the generated kit should keep `unbreakable` once `toolrepair` is present is a real question and
should be answered against what the corpus kits do, not assumed.

**MG17 — The derivation is silent when a map has no kit.** Everything `CtwStandards` derives sits behind
`if (m.Kits.FirstOrDefault() is { } kit)`; only the kill-reward include and hunger-off happen
unconditionally. Every map in this batch carries a spawn kit, so MG14 alone fixes them — but a kitless map
would pass through the composer and come out with no keep, repair or remove rules and no warning that its
loadout rules are missing. Worth a report line rather than silence, since the elements are near-universal.

## Reading it back

**MG13 — The maps were judged from one view.** Every map was checked with `--topdown` at three pixels to the
block, which shows the plan and hides everything about the third dimension: whether a drop is walkable,
whether a room's floor sits where the relief left it, whether a goal has ground under it. The other readings
exist and were not used — `--heightmap`, `--contour`, `--surface`, `--traversability`, `--buildings`,
`--structures` in `tools/PgmStudio.RoundTrip`, and the relief prototype's own topographic, blocks-from-an-
angle, section and step-map renders in `tools/relief`. Several of the faults above would have been visible in
the first one of those that was run.

## Still to come

The author is adding to this. New entries take the next free `MG` id and slot into the pipeline section they
belong to.
