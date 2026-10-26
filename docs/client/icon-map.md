# The icon map

The client draws two sets of icons: Lucide, vendored as a subset under `wwwroot/js/studio/vendor/`, and the
studio's own glyphs, registered in `icons/terrain-icons.js` as `terrain-*`. The studio's own glyphs are the
direction — a monument, a wool room and a mound are things no general icon set draws — and this document is the
map the move is made from: what every icon stands for today, where one icon stands for two things or one thing
is drawn two ways, and the order the move takes. The counts were measured over the client source; the moves are
open work, `C109`.

## What is drawn today

**Five hundred and eleven icon assignments name 137 icons: 115 Lucide and 22 of the studio's own.** Every name in
use is in the vendored subset or the registry, so nothing renders blank. The vendored subset holds 152 names, 37
of them never drawn, because the vendor scan takes every kebab-case literal in the client as a candidate; the
registry holds 25 glyphs, three of them (`terrain-land`, `terrain-marks`, `terrain-rooms`) referenced nowhere. Of
the 240 glossary terms, 82 are drawn by some icon.

**Icons are named in tables, not at the call site.** `SketchModifierState.Icon`, `PropKindNames`,
`SketchStructuralRow.Icon`, `RegionNode.Icon`, `LibraryKinds`, `ConfigurePhases`, `ImportPhase` and a dozen
smaller ones map a concept to a name, and every one is a literal the vendor scan can see. Two pairs of tables
state the same icons twice and agree only because nobody has changed one: the props in `DressingTools.All` and
`PropKindNames.All`, and the pieces in the Layout dock's marker list and `SketchStructuralRow.Icon`.

## Where one picture means two things

**An icon that stands for two things says neither.** The worst cases are the general pictures a domain thing was
drawn with because no specific one existed:

| icon | stands for |
|---|---|
| `layers` | an island, a sketch group, the Palettes library, a palette part, a room part, the Section view, the 3-D layer switch, the scan summary |
| `box` | the defence chest, the chest prop, imported chests, region files, the Box solid, a cuboid region, the Blocks overlay, the 3-D view |
| `mountain` | the Terraform phase, a boulder, the Contours overlay, the palette's ground and edges |
| `eye` | the Review phase, the observer piece, show and hide, the View only tag |
| `flag` | a capture point, a flag and the Flags phase, imported wool monuments |
| `dot` | a spawn, an observer spawn, a wool spawn, a wool monument, a flag post, the point tool, a point region |
| `rectangle-horizontal` | the rectangle tool, a place box, a rectangle region, a wool room, spawn protection, a build region, a house wing |
| `goal` · `gem` · `pickaxe` · `brick-wall` | Wools and Score · monument and ore · iron cube and the Build phase · a wall piece and Storeys |

**A thing drawn two ways reads as two things.** A wool is drawn six ways (`goal`, `dot`, `square`, `palette`,
`crosshair`, `sparkles`) and a seventh, `◆`, on the world canvas; a spawn three (`dot`, `crosshair`, `★`); a wool
monument four (`dot`, `flag`, `link`, `⊕`); a build region four (`rectangle-horizontal`, `hammer`, `pickaxe`,
`square-dashed`); a house is `home` in one table and `house` in another, two Lucide names for one glyph; a point
pressed on the canvas is `pin` in Places, `crosshair` in Notes and `dot` in Configure.

## What moves, and in what order

**Actions, statuses and plain geometry stay Lucide.** Close, add, delete, move, the eye as a switch, undo, the
shape tools and the region types are the vocabulary every tool shares, and a general icon set draws them well.
What moves is the studio's own things: pieces, objectives, zones, props, library kinds, phases, the island, the
group, the paint rule, the palette's parts and the glyphs the world canvas draws as text.

**The order is collisions first.** The first priority is every thing in the two tables above, each given one glyph
of its own: `terrain-land` for the island, which exists and is unused; a group, an override and a paint rule; a
glyph each for wool, wool spawn, wool monument, spawn, observer, score box, capture point, flag post, monument,
iron cube, build region, water lane, spawn room, wool room, protection, approach wall and chest; and boulder,
palette, pattern, tree, house, stroke, roof, storey, biome, porch and doorway for the library. The second is the
things whose Lucide picture works but is generic: core and casing, team, fluid and flora, course, window, the
map.xml regions and filters, symmetry. The third is the things with no icon yet that a reader meets on the canvas
or in a dock: the room and zone pieces the Layout dock shows as swatches, the play pieces the canvas draws as
colour, the world canvas's `★ ◆ ⊕`, and the Terraform families.

**The scheme is settled; the drawings are being chosen.** There are three prefixes: `pgm-` for the game's things,
`deco-` for decoration and the library, and `terrain-` for ground forms, because a core is not terrain. A phase reuses
the glyph of its headline thing — Cores draws the core, Wools the wool, Terraform the mound. One point glyph serves
Places, Notes and Configure, since a point is one coordinate wherever it is pressed. The canvas keeps drawing pieces
as colour, and the review phase's camera stays the one glyph drawn on it. A glyph is a PGM or Minecraft thing, not a
general metaphor: a flag is a banner, a spawn a bed, a capture point a beacon, a core an obsidian casing round lava.
The first drafts, one for every thing in the collision lists above, wait on the author's choice before any is
registered (`C109`).
