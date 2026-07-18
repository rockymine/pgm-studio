# CTW monument-surround corpus analysis

**Goal:** characterise how authors build and label wool monuments across the corpus, to ground the
authoring-flow extractor that auto-suggests monument positions from a world. This is the pattern study
behind the `MonumentSuggester` backend (`PgmStudio.Minecraft`) and the
`docs/contracts/monument-suggestion.md` contract. Reproduce with the scripts in `scripts/`
(`monument_corpus_analysis.py`, `sign_text_analysis.py`, `scoped_analysis.py` — need the `nbt` lib in
`/root/ctw-venv`; they read the `PublicMaps/ctw` + `CommunityMaps/ctw` worlds and
`pgm-map-studio-output/*/xml_data.json` for ground-truth monuments).

**Corpus:** 345 maps · **1723 monuments** (4 maps skipped — non-1.8 chunk format). Ground truth =
resolved monument `<block>` coords from `xml_data.json`.

---

## How authors build monuments

### The monument block is air
**1701 / 1723 (98.7%)** monument blocks are air — PGM's placement-region convention (the wool is marked
"placed" when an objective wool block appears in the region). The 1.3% exceptions are
region-resolved / pre-filled centres; treat "centre is air" as a strong but not absolute prior.

### Q1 — Signs label ~two-thirds of monuments
| scope | monuments with a keyword sign |
|---|---|
| strict 3×3×5 slice (±1 h, ±2 v) | 1162 / 1723 (**67.4%**) |
| wider box (±2 h, dy −3..+2) | 1166 / 1723 (67.7%) |
| any sign (no text filter), wider box | 1176 / 1723 (68.3%) |

A monument's sign is always immediately adjacent — widening the search barely changes the count.
**~1/3 of monuments carry no sign**, which caps any sign-based detector at ≈68% recall.

### Q2 — Sign placement relative to the monument block
| dy (sign − monument) | keyword signs |
|---|---|
| −1 (mounted on the block **below** the monument) | **1333** |
| +1 (mounted on the block **above**) | 844 |
| 0 (same level) | 194 |
| other (−3, −2, +2) | 28 |

The dominant `(dx,dy,dz)` offsets are the four cardinal neighbours at **dy=−1** — `(0,−1,±1)`, `(±1,−1,0)`
— i.e. a wall sign on the side of the pedestal block, then the same four at **dy=+1**. So a labelling
sign sits beside the monument, one level below or above it.

### Q3 — Block directly below / above the monument
| below (pedestal) | share |  | above (cap) | share |
|---|---|---|---|---|
| bedrock | 33.5% |  | stained glass | 18.6% |
| stained clay | 15.8% |  | air | 13.4% |
| stained glass | 13.8% |  | barrier (166) | 11.4% |
| wool | 11.1% |  | slab | 11.1% |
| air | 9.3% |  | bedrock | 9.2% |
| (long tail) | … |  | stained clay / wool / … | … |

A pedestal prior of **{bedrock, stained clay, stained glass, wool}** covers ~74% of monuments; ~9% float
on air. The cap is usually decorative (glass / barrier / slab) or open.

### Q4 — Armour stands
**54 / 1723 (3.1%)** — the `Ruediger_LP` style: an armour stand with wool on its head, or a `CustomName`
like "Place X WOOL here!". A minority decoration; **signs are the dominant label**.

---

## What makes the detector work

### Invert the sign's facing — don't look near signs
A monument is the air cell a sign *points at*, not merely a cell near a sign. Anchoring on proximity is
the difference between a usable detector and one that emits ~20k false sites (an order of magnitude more
than the 1723 real monuments). The corpus gives a clean inverse-facing map for wall signs:

```
data 2 → monument = sign + (0,0,+1) + up      data 3 → sign + (0,0,−1) + up
data 4 → monument = sign + (+1,0,0) + up      data 5 → sign + (−1,0,0) + up
```

i.e. **monument = sign + facing ± up** — `+ up` when the sign is at the pedestal level (`SignBelow`),
`− up` when it is at the cap level (`SignAbove`). A label sign can sit *beside* the monument (offset by
its facing) or *in the monument's own column*; both placements are predicted and validated against air +
the declared pedestal/cap, so only the real one survives.

### Classify the sign text — don't keyword-match
~Half of all `wool` / `monument` / colour keyword signs on a map are not monument labels — they are the
rest of CTW signage:

```
RED TEAM ONLY            team barrier      Kill sheep to get wool!       wool source
Back to the woolroom     navigation        Wool Monument is behind you   directional
VICTORY MONUMENT         lobby             Only Reds Beyond this Point   no-build line
```

Notably **"monument" is a poor keyword** — it appears in only ~6% of real labels and occurs as often in
non-labels. What real labels look like, vs the false-positive signage:

| feature of a monument label | share |  | discriminative *non-label* words |
|---|---|---|---|
| contains a colour (word / § code / JSON) | 93% |  | `team only`, `only` |
| contains "wool" | 91% |  | `room` / `woolroom`, `wool room` |
| colour **and** "wool" | 89% |  | `base`, `use` / `use before`, `before` |
| "place … here" instruction | 54% |  | `portal`, `teleport`, `pick up` |
| contains "monument" | 6% |  | `reds`/`blues`, `red team`, `victory monument` |

The canonical label is **"Place \<COLOUR\> WOOL here!"** (top bigrams `wool here`, `place the`,
`<colour> wool`). A classifier that accepts `colour + wool` / `place <colour> … here` / short
`<colour> monument` and rejects the barrier / navigation / source / lobby phrasing keeps essentially all
real labels while discarding the off-site signage.

### Scope to the author's box
A small-margin box around the monument cluster keeps ~all real labels while excluding ~96% of the
off-site keyword signs. Its main value is **scan cost** (only the boxed volume is decoded) and
**recall safety** — inside a trusted box the text filter can run looser without a whole-map false-positive
explosion.

### The style menu is the precision lever
Declaring the monument's **pedestal** (block below), **label** (how it's marked) and **cap** (block
above) lets the detector require a specific signature — the single biggest precision gain. The corpus
supplies the menu:

- **Pedestal:** bedrock 33% · stained clay 16% · stained glass 14% · wool 11% · none/floating 9%.
- **Label:** sign-below 34% · sign-above 16% · armour stand 3% · none ~47% (so nearly half need a
  non-label fallback).
- **Most common (below → above) styles:** bedrock→glass 8.0% · floating (air→air) 7.2% · clay→glass 6.2%
  · bedrock→bedrock 6.0% · glass→slab 4.6% · wool→barrier 4.2%.

### Colour from the stained block, not the sign text
A wool / stained-clay / stained-glass pedestal or cap encodes the wool colour in its data nibble. It is
**per-cell unambiguous** (no sign-attribution needed) and so robust exactly where signs are hardest to
attribute (packed clusters), and it is the only colour source for label-free monuments. So colour is
`ColorFromStain(below) ?? ColorFromStain(above) ?? labelColour`; the stained block and a
correctly-attributed sign agree in practice, and the rare genuine conflict (e.g. arabia: a magenta glass
pedestal under a "Purple Wool" sign — magenta is correct) resolves to the placed block and is flagged in
the suggestion's evidence.

---

## Implemented backend: `MonumentSuggester`

`Suggest(chunks, BlockBox, MonumentStyle)` returns ranked `MonumentSuggestion`s (air block + inferred
wool colour + confidence + evidence). `MonumentStyle` has three dimensions — **`PedestalKind`** (below),
**`LabelKind`** (sign-below / sign-above / armour stand / none), **`CapKind`** (above). It inverts the
learned facing geometry, runs the text classifier (`IsMonumentLabel`), requires the declared pedestal
under and cap over an air cell, and adds armour-stand and pure-geometry fallbacks. Armour-stand
direction is read from payload: **wool-on-head ⇒ monument above** the stand; **name-only ⇒ monument
below** it. For label-free monuments the pedestal+cap pair is the signal, and a stained pedestal/cap
yields the colour. Validate via RoundTrip `--suggest-monuments[-corpus]`.

**Corpus results** (box = monument clusters + margin ±8):

| style declared? | precision | recall | FP | colour correct |
|---|---|---|---|---|
| **auto (style declared)** | **96.6%** | 57.8% | **35** | 92.2% |
| Any (undeclared) | 82.1% | 56.9% | 214 | 91.2% |

Declaring the style takes precision **82% → 97%** and false positives **214 → 35** at negligible recall
cost — the corpus-scale confirmation that the style menu is the precision lever. Per-map spot checks reach
100%/100% with the style declared: thunder (signs on bedrock), kanto (signs beside a wool cap), pigland
(glass pedestal + wool-on-head stand), dragons_hearth (name-only stand above), lupa/lupain (glass cap, no
sign), nutrient (wool pedestal + in-column "v \<colour\> Wool v" sign).

### Why `layer_segment.parquet` can't drive this
`layer_segment` stores only `(world_x, world_z, world_y_start, world_y_end)` — per-column solid-run
extents, with **no block ids/data, no tile entities (signs), no entities**. The decisive monument signals
— pedestal *material*, sign *text* + *facing*, armour-stand NBT — are all absent, so it cannot detect or
classify monuments; neither can the cached `layer.parquet` (surface block per column only). The suggester
reads the Anvil world directly, bounded to the author's box. `layer_segment`'s reuse is **downstream**:
it (with `/buildability`) establishes where buildable ground / air gaps are, so it's the right artifact
to **snap/validate** a suggested monument onto a surface — not to find it.

---

## Limitations
- **Unlabelled monuments (~1/3)** surface only via the geometry/`None` path — low confidence, author-
  confirmed.
- **Only wall signs carry a reliable facing**, so sign-post-only maps fall back to the geometry path.
- **Packed clusters** whose signs sit beside at the same level (`dy=0`) can attribute to a neighbour —
  the author corrects it.
- A **colour-coded block left/right at the same `y`** (rather than below/above) is not read for colour.
