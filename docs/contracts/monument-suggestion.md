# Monument suggestion (world → monument positions)

*Spec — the authoring-flow **"which monument style? + box"** extractor: from a world, a box the author
draws, and a declared monument style, suggest monument block positions with inferred wool colours, for
the author to confirm. Status: **backend complete** (`src/PgmStudio.Minecraft/MonumentSuggester.cs`);
UI not built — this is the contract for the **Monuments** step of the Wools activity (`docs/contracts/
new-map-authoring.md` §Wools). The pattern study behind it (no code) is `docs/monument-patterns.md`
(scripts in `scripts/`).*

## What it does

Given the world (Anvil region chunks), a `BlockBox` (the region the author boxes around the monument
area), and a `MonumentStyle`, return ranked `MonumentSuggestion`s — each a predicted **air** monument
block, an inferred wool colour, a confidence, and the evidence it came from. The author confirms/places
them (a monument is air on a well-formed map: PGM marks the wool "placed" when an objective wool block
appears in the placement region).

**Why a box + a style.** Scoping to the boxed monument area keeps the scan cheap and removes off-site
signage (team barriers, wool-room and lobby signs) that would otherwise produce phantom hits. Declaring
the style lets the detector require a specific signature — the main lever for precise suggestions.

## Inputs

### `BlockBox` (the scan region)
Inclusive world-coordinate box: `(MinX, MinY, MinZ, MaxX, MaxY, MaxZ)` — the shared inclusive-AABB value
type (`PgmStudio.Domain.BlockBox`). The author draws it around a monument cluster; here it bounds both the
block scan and the candidate anchors. Call `Suggest` **once per box** — if a map's monuments fall in
separate groups, the author boxes (and the UI calls) each group. `Expand(m)`, `Contains(x,y,z)` and
`IntersectsChunk(cx,cz)` are provided; the scan internally adds a 2-block margin so an anchor at the box
edge still resolves.

### `MonumentStyle` — the three menu dimensions
All default to `Any`. The UI presents one dropdown per dimension; the options are the enum values.

| dimension | meaning | values |
|---|---|---|
| **`PedestalKind`** | block directly **below** the monument | `Any` · `Bedrock` · `StainedClay` · `StainedGlass` · `Wool` · `Floating` (air below) |
| **`LabelKind`** | how the monument is marked | `Any` · `SignBelow` · `SignAbove` · `ArmorStand` · `None` |
| **`CapKind`** | block directly **above** the monument | `Any` · `Open` (air) · `StainedGlass` · `StainedClay` · `Bedrock` · `Slab` · `Barrier` · `Wool` · `Sign` |

## Detection anchors (what each `LabelKind` does)

- **`SignBelow`** — a colour-label wall sign at the **pedestal's level** (one below the monument),
  beside it and facing it, *or* in the monument's own column just under it. Geometry: `monument =
  sign + facing + up`.
- **`SignAbove`** — a wall sign at the **cap's level** (one above), beside and facing the monument, *or*
  in-column directly capping it. Geometry: `monument = sign + facing − up` (in-column: `sign − up`).
- **`ArmorStand`** — an armour stand near the monument. Disambiguated by payload: **wool on the head ⇒
  monument just above** the stand; **name-only ⇒ monument below** it (a down-pointing marker).
- **`None`** — no label; pure geometry: an air cell sitting on the declared pedestal under the declared
  cap. Use for unlabelled monuments; pair with a *specific* pedestal/cap for precision (otherwise it is
  low-confidence and noisy).
- **`Any`** — try the sign and armour-stand anchors (does **not** run the geometry-only pass).

A sign only anchors if its text reads like a monument label — `MonumentSuggester.IsMonumentLabel`
accepts `"<colour> wool"`, `"place <colour> … here"`, or a short `"<colour> … monument"`, and rejects
barrier / navigation / source signage (`team only`, `woolroom`, `behind you`, `kill … wool`, …). Arrow
and ascii decoration are stripped before classifying, so `"---> Red Wool --->"` still passes. Only wall
signs carry a reliable facing; sign posts are not used to predict positions.

## Colour derivation

A stained block (wool / stained clay / stained glass) directly below or above the monument is the
**placed** colour and is **authoritative** — it wins over parsed sign text. Order:

```
colour = ColorFromStain(below) ?? ColorFromStain(above) ?? labelColour   // sign text / stand head / custom name
```

(The data nibble of wool/clay/glass uses the standard colour order; `silver` = light gray.) When the
block colour and the label colour genuinely conflict, the suggestion's `Evidence` carries a
`[block:X ≠ label:Y]` note so the author can adjudicate; the block colour is used.

## Output — `MonumentSuggestion`

| field | meaning |
|---|---|
| `X, Y, Z` | the predicted (air) monument block |
| `Color` | inferred wool colour slug (PGM names; `silver`=light gray), or `null` |
| `Confidence` | `0..1` (sign + matching pedestal highest; pure geometry lowest) |
| `Source` | `"sign"` · `"armorstand"` · `"geometry"` |
| `PedestalId, PedestalData` | the block directly below |
| `SignX, SignY, SignZ` | the anchor sign, if any |
| `Evidence` | decoded sign text / armour-stand custom name (+ any colour-conflict flag) |

Returned **highest-confidence first**. Suggestions at the same cell are merged; independent agreeing
signs raise the confidence.

## API & usage

```csharp
List<MonumentSuggestion> MonumentSuggester.Suggest(
    IEnumerable<AnvilRegion.Chunk> chunks, BlockBox box, MonumentStyle style);
```

Helpers: `MonumentSuggester.IsMonumentLabel(text)`, `ColorFromText(text)`, `ClassifyPedestal(belowId)` /
`ClassifyCap(aboveId)` (block id → kind). The
caller supplies chunks (e.g. `mcas.SelectMany(AnvilRegion.ReadChunks)`); chunks outside the box are
skipped.

Explore / validate from the harness (`tools/PgmStudio.RoundTrip`):

```
# one map — derives a box from the map's monument clusters and prints suggestions + a match summary
dotnet run --project tools/PgmStudio.RoundTrip -- --suggest-monuments <regionDir> <xml_data.json> \
    [--auto-style | --pedestal <Kind> --label <Kind> --cap <Kind>] [--margin <N>]
# whole corpus
dotnet run --project tools/PgmStudio.RoundTrip -- --suggest-monuments-corpus [same flags]
```

`--auto-style` reads each cluster's actual pedestal/cap to simulate the author declaring the style.

## UI mapping (Monuments step)

1. Author **boxes the monument area** on the canvas → `BlockBox` (one call per boxed group).
2. Author picks the **style** — three dropdowns (pedestal / label / cap), defaulting to `Any`.
3. Run → suggestions render as **ghost monument markers** (colour + confidence + evidence on hover);
   the author **confirms** (places the monument block, the capturing team derived by orbit per the Wools
   flow) or **dismisses**. Low-confidence (`None`/geometry) suggestions are shown distinctly for a quick
   confirm.

## Scope & limitations

- **Needs the world.** `layer_segment.parquet` (and the cached `layer.parquet`) cannot drive this — they
  carry no block materials, signs or entities. Reuse them *downstream* to snap a confirmed monument onto
  buildable ground.
- **Recall is bounded by labelling.** Unlabelled monuments surface only via the `None`/geometry path
  (low confidence, author-confirmed).
- **Packed clusters** whose signs sit beside at the same level (`dy=0`) can attribute to a neighbour —
  an accepted outlier; the author corrects it. (A colour-coded block left/right at the same `y`, rather
  than below/above, is likewise not yet read for colour.)
