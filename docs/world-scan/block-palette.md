# Block palette — one colour per Java 1.8 block

The studio renders a Minecraft world from above in several places: the surface overlay on a map's layers,
the sketch terrain preview, the terrain-paint picker's swatches, and the monument slice dump. All four ask
the same question — given a legacy block id and its four-bit metadata, what single colour stands for that
block — and all four read the same answer, so a swatch shown in a picker is the colour the export places.

`PgmStudio.Minecraft.BlockPalette` is that answer. It covers block ids 0–197, the whole 1.8 set, and the
metadata sub-types within them.

## The lookup pipeline

A lookup runs three steps, and stops at the first that answers.

**Normalize.** The raw metadata goes through `BlockVariants.Normalize`, which keeps the bits that pick the
block's appearance and drops the bits that carry placement state. This step is what makes an exact match
viable at all: a legacy block packs its sub-type and its state into one nibble, so an east-west oak log is
`17:8` and a leaf block marked persistent is `18:4`. A table keyed on the raw nibble misses both.

**Match the sub-type, then the block.** The normalized pair indexes the sub-type table. A block whose
metadata claims no sub-type — an out-of-range value, a double plant's upper half, any of the two hundred
blocks whose metadata is pure state — falls to the block's base colour instead. Every id in 0–197 has one,
so a real 1.8 block always resolves here.

**Hash.** An id outside the table gets a stable colour derived from its id and data. Two unknown blocks
therefore stay visually distinct rather than sharing a placeholder, which matters when the reason a block is
unknown is that a map uses a mod or a later version's id.

Negative data is the caller's "any variant" convention. It skips the sub-type step entirely and goes
straight to the base, so a wool of unknown colour answers with the family's own colour and name — `Wool`,
not `White Wool` — while `Name(35, 14)` is `Red Wool`. Colour and name resolve through the same steps, so a
swatch and its caption can never disagree about which block they describe.

## Which metadata bits are the sub-type

Most blocks have no visual sub-type and normalize to zero whatever their metadata: stairs (facing and half),
water and lava (flow level), rails (shape), repeaters (delay), crops (growth), and everything else that
stores only placement. The blocks that do carry a sub-type are these, and nothing else.

| Blocks | Sub-type bits | What the other bits hold |
|---|---|---|
| Logs `17`, `162`; leaves `18`, `161` | `0–1` | Log axis (and the all-bark forms `12–15`); leaf persistence and decay-check flags |
| Dirt `3`, sandstone `24`, red sandstone `179`, grass `31`, stone bricks `98`, prismarine `168` | `0–1` | — |
| Stone `1`, planks `5`, saplings `6`, silverfish blocks `97`, wood slabs `125`/`126`, red sandstone slabs `181`/`182` | `0–2` | Sapling growth stage; slab upper-half flag |
| Stone slab `44`, double stone slab `43` | `0–2` | Slab upper-half flag; the double slab's seamless forms reuse `0–2`, landing each on its ordinary twin |
| Sand `12`, sponge `19`, cobblestone wall `139` | `0` | — |
| Wool `35`, carpet `171`, stained glass `95`, panes `160`, stained clay `159`, flowers `38`, mushroom blocks `99`/`100` | `0–3` | — |

Three blocks need arithmetic a mask cannot express. The **anvil** `145` keeps its damage in bits 2–3 and its
facing in bits 0–1, so the sub-type is the metadata shifted right by two. The **quartz block** `155` numbers
its pillar's three axes `2`, `3` and `4` over one texture, so those three collapse onto one entry. The
**double plant** `175` marks its upper half in bit 3 and fills the rest with facing rather than with the
plant, so an upper half cannot resolve to a sub-type at all and deliberately lands on the base colour.

## Where a colour comes from

Each colour is the alpha-masked mean of that block's 16×16 texture in the 1.8 asset set. Alpha is a mask at
a 50% threshold rather than a weight, so glass, panes, iron bars, torches and cobweb average the material
instead of fading toward the background they mostly are. Where a block's faces differ, the colour is taken
from the face a top-down render sees: a log shows its rings, grass and podzol their tops, hay its bound end.

Three families depart from the raw texture mean on purpose, because the mean is the wrong answer for them.

**Biome-tinted blocks** carry no colour of their own in the asset — grass, foliage, vines, sugar cane, lily
pad and water are greyscale textures multiplied by a tint the game samples from the biome. A static render
has no biome to sample, so these carry a fixed temperate (plains/forest) tint. Spruce and birch leaves are
the exception within the exception: the game gives them a constant tint everywhere, and they take it.

**Ores** are four-fifths stone matrix by pixel count, so their raw means differ by a few units and every ore
prints the same grey. Each instead takes its accent — gold's yellow, lapis's blue, diamond's cyan — which is
what distinguishes them to a reader and what the render exists to show.

**The three dye families are three ramps, not one.** Wool, stained glass and stained clay wear the same
sixteen dyes over different materials: wool is the dye on a fibrous white, stained glass is the saturated dye
itself, and stained clay is the dye burnt into terracotta and heavily muted. Carpet follows wool and panes
follow glass. Collapsing them onto a single ramp — the in-game map-colour behaviour — prints a brown-clay
floor the same colour as brown wool.

## Storage

Sub-type colours live in a flat array indexed by the packed key `(blockId << 4) | meta`, base colours in a
second array indexed by the id alone. A legacy id is a byte and metadata a nibble, so the sub-type table is a
fixed 4096 slots — small enough to stay resident, dense enough that a per-cell lookup is two array reads with
no hashing, no tuple key and no allocation. Hex strings are formatted once at construction and returned
interned, so rendering a world's whole surface allocates nothing per cell; a caller that writes pixels rather
than markup can take `PackedRgb` and skip the string entirely.

## Checking the table against real assets

`tools/palette/texture-average.cs` reads a directory of 1.8 block textures, decodes each PNG (with no image
dependency — a non-interlaced 16×16 PNG is a zlib stream plus five filter cases), and computes the same
alpha-masked first-frame mean the table is authored from.

```
dotnet run tools/palette/texture-average.cs -- --dump  <textureDir>   # every texture's mean
dotnet run tools/palette/texture-average.cs -- --rows  <textureDir>   # palette rows, ready to paste
dotnet run tools/palette/texture-average.cs -- --check <textureDir>   # diff the shipped table, non-zero on drift
```

`--check` covers the entries whose colour *is* their texture mean, which is most of the table; it names the
texture each entry reads, so an unmapped entry is visible as a gap rather than a silent pass. The tinted,
accented and face-chosen entries above are outside its map by definition — they are decisions, not
measurements, and a tool that averaged them would report drift on every one.
