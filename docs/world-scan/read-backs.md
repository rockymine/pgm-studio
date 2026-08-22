# Reading a built world back

Everything a caller does with the studio runs through the API, and the API describes itself — except the one
thing done *after* building, which is looking at what was built. Eight renderers sit in
`PgmStudio.Minecraft/Render/`, and until `WS6` they reached a caller only through `PgmStudio.RoundTrip`'s
flags: a capability no schema named, so a brief had to carry a table of them and an agent had to know a .NET
binary existed at all.

They answer over HTTP now, one route each, and what each one draws is written once — in
`WorldReadCatalog` — and served twice: as the endpoint summary the schema publishes, and as
`PgmStudio.RoundTrip --help`.

## What it reads, and where the world comes from

**The world is built for the request.** A map that ships its own region files has one on disk; a
sketch-authored map's exists only as the layout and the intent it derives from, which is the same position
`GET /map/{slug}/export` is in, and it builds one too. A map with no stored sketch layout is a **404** — not a
fault, but a statement that there is no world here to build.

**The build runs no gate, deliberately.** A board that fails one is exactly the board somebody needs to look
at, and a read-back that refuses the broken case is never there when it is wanted. `OB17`, `EX1`, `OB24` and
the rest belong at the door a map ships through, not at the window somebody looks in through.

**The map document is projected from the resolved intent** rather than composed through the export. The
overlays need one — the spawns, goals and apply rules a picture draws on top of the terrain — and the
projection is the version that agrees with the world the build just made: spawns snapped to the structures it
placed, goal locations filled in from the cubes it cast. Going through the export would lose the world to the
first gate that fired. A document that will not project costs the overlays and not the picture.

## The reads

Every route is `GET /api/map/{slug}/…`, every picture is `image/png`, and every one takes `scale` — pixels a
block, 1 to 16, default 4, clamped rather than refused.

| Route | Also | Answers |
|---|---|---|
| `render/topdown` | `--topdown --layer …` | the board from above, one question per image. `layer` = `ground` · `structure` · `foliage` · `objectives` · `combined`; `material` colours by the real palette rather than by category; `ymax` looks under a roof or a canopy |
| `render/section` | `--section` | a vertical cut with a Y scale. `axis` = `x`\|`z`, `from`/`to` its extent, `at` the other coordinate, `ymin`/`ymax` the courses drawn |
| `render/heightmap` | `--heightmap` | elevation as tone, contour lines every `contour` blocks (default 4); `grey` drops the tone where a board's own palette fights the height reading |
| `render/surface` | `--surface` | the paint, as the tone families `TerrainPalette.Families` names |
| `render/traversability` | `--traversability-map` | the navigable components, with the spawns and goals on them |
| `render/structures` | `--structures` | the building census by block material, `minarea` the smallest counted (default 16) |
| `render/mirror` | `--mirror` | the board against its own symmetry; `mode` overrides the one the map states |
| `column` | `--column` | one or more columns bedrock-to-sky, every block named, as `text/plain`. `?at=x,z`, repeated |

`column` answers characters rather than JSON for the reason the plan grid and the flow account do: it is read
by a person or an agent rather than parsed, and it is the one read a caller with no image reader can act on.

## Three of them mislead, and each has cost a reader a conclusion

A caveat met *after* a conclusion has already cost the conclusion, so each rides in its own route's summary
rather than in a document somebody may not have open.

**`traversability` reads an approach wall's cobweb course as impassable** (`B99`). Every board carrying one
reports its wool room isolated, and the export gate — which navigates the columns rather than this picture —
passes it. A wall is meant to be crossed over the top, cutting the web with the shears the kit carries.

**`structures` cannot see a town this studio built** (`B149`). It finds roofs by material, and its terrain
list swallows stone, cobble, sandstone, stone brick, quartz and stained clay — so a cottage roofed in any of
them reads as ground. On a studio-built world take `render/topdown?layer=structure`, which reads the
provenance sidecar and draws what the build recorded itself placing.

**`section` samples one plane** (`B129`). Anything a few blocks either side of the cut is not in the picture,
so a cut through a house that misses its walls reads as floor, air, roof — a correct reading of that plane
rather than a broken building.

And one that is not a fault: **`surface`'s magenta is not a material.** It is the honest answer for a block no
tone family claims, and the legend says how many there were.

## What each read is for

`column` is the workhorse and the only honest answer: every picture beside it is a projection, and this is
what is actually at a coordinate. It is the read to reach for when a picture and a document disagree.

`topdown` keeps no Y at all — a riser, a ramp's step heights, a stamped room's floor and a goal's clearance
are none of them in it. `section` and `column` are the two that keep it, which is why every shipped roof fault
was visible in a section and invisible from above.

`heightmap` answers whether a relief solved into the shape it was drawn as, and shows a flat pad butted
against a hill as the ruled edge it is. `surface` answers whether a board's paint is the palette it was
authored from — a whole tone family taken where two members were meant reads as the noise it is. `mirror`
answers whether a board somebody believes is symmetric actually is.

## Limits

The build is paid per request; nothing is cached. A large board is the same cost as an export, which is what
it is.

There is no read that keeps Y over a *region*: `section` cuts one plane and `column` reads one cell, and the
depth-projected mode that would sit between them is `B129`.
