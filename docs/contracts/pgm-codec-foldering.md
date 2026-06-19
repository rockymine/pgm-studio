# `PgmStudio.Pgm` foldering plan: the codec roundtrip, verified file-by-file

> The concrete, actionable plan for the §5.1 reorg in `project-structure.md`. Captured from a
> file-by-file read of all 30 `Pgm` files. The refactor is a **clean-session, folders-only job** (no
> logic changes) — like `geometry-consolidation.md`, don't bolt it onto feature work. Verified churn is
> **tiny** (2 `Api` `using` lines + file moves); the codec itself does not move.

## 1. What `Pgm` actually is, verified

`Pgm`'s charter is **understand the `map.xml` and write it back**, with the JSON tree as the round-trip
currency and *the entry point for everything*. The read confirms that exactly. The project has **two
layers**: the **codec** (9 files at the root — the charter) and the **three authoring tools** (the 20-file
`Editing/` folder — edit/configure/sketch).

### 1a. The codec — the verified roundtrip (9 root files)

The pivot type is **`MapXml`** (the typed domain model, in `PgmStudio.Domain`). The loose
**`Dict`** (`Dictionary<string,object?>`, the `xml_data.json` tree) is the persisted/wire currency.
Everything is a pure function between `XML ⇄ MapXml ⇄ Dict`:

```
            MapParser.Parse(path) / ParseXmlString(xml)
   map.xml ───────────────────────────────────────────────►  MapXml          [XML → domain]
            (orchestrates RegionParser + FilterParser via Xml helpers)
                                                                 │
                                            Serializer.ToDict(m) │  ▲ Deserializer.FromDict(d)
                                       [domain → JSON tree]      ▼  │  [JSON tree → domain]
                                                               Dict (xml_data.json)
                                                                 │  ▲
                                      JsonTree.FromJson(string) ─┘  └─ (persisted blob / POST body)
   map.xml  ◄───────────────────────────────────────────────  MapXml
            XmlWriter.ToXml(m)                  [domain → XML]
```

| File | LOC | Role | Public entry | Direction |
|---|---|---|---|---|
| `MapParser.cs` | 448 | top-level parser; orchestrates the two registry parsers | `Parse(path)`, `ParseXmlString(xml)` → `MapXml` | XML → domain |
| `RegionParser.cs` | 369 | `<regions>` → flat `Region` registry + apply-rules; synthetic ids for anon | `ParseRegionsElem(XElement)` → `(regions, applyRules)` | XML → domain |
| `FilterParser.cs` | 171 | `<filters>` → flat `Filter` registry; seeds `never`/`always` | `ParseFiltersElem(XElement)` → `filters` | XML → domain |
| `Xml.cs` | 50 | `internal` XElement/attr/text/coord helpers | (internal, parse-side) | — |
| `Serializer.cs` | 309 | domain → JSON tree (`xml_data.json` shape); + single-entity encoders for the importer's column split | `ToDict(MapXml)` → `Dict`; `RegionToDict`/`FilterToDict`/`ApplyRuleToDict` | domain → Dict |
| `Deserializer.cs` | 293 | inverse of `Serializer` | `FromDict(Dict)` → `MapXml` | Dict → domain |
| `XmlWriter.cs` | 478 | domain → PGM `map.xml` string (top-level/inline ref logic, synthetic-id elision) | `ToXml(MapXml)` → `string` | domain → XML |
| `JsonTree.cs` | 95 | **two jobs** (see note): JSON-string→tree **+** structural tree compare | `FromJson`/`FromJsonLenient`; `DeepEquals`/`Canonical`/`DiffKeys` | Dict util |
| `RegionBoundsDeriver.cs` | 77 | recompute derived `bounds_2d` for compound/transform regions after a **DB** rebuild (the parser does it at parse time; persistence only stores primitive bounds) | `Derive(registry)` | Dict-read helper |

**Who drives the codec (it is the hub):** `Import/Program.cs` (parse→serialize on ingest),
`Import/MapImporter.cs` (`FromJson`→`FromDict`→rows), `Data/MapReader.cs` (`ToDict`+`RegionBoundsDeriver`
to rebuild the doc for the editor), `Data/MapWriter.cs` (`FromDict` on save), `Api/MapXmlEndpoint.cs`
(`FromDict`+`XmlWriter` for the export endpoint), `Api/WriteEndpoints.cs` (`FromJson` on POSTed edits),
and the `RoundTrip` parity harness (all of it).

**`JsonTree` note (optional split):** `FromJson`/`FromJsonLenient` are **production codec** (Api, Data,
Import). `DeepEquals`/`Canonical`/`DiffKeys` are **roundtrip-verification** — used only by
`Import/Program.cs` (drift check on ingest) and the parity harness. Two cohesive jobs in one 95-line file;
splitting into `JsonTree` (parse) + `JsonTreeCompare` (verify) is *optional* and low value. Leave unless it grows.

### 1b. The 20 `Editing/` files — three tools wearing one folder

Classified by the Dict they touch and the tool that drives them (full rationale in `project-structure.md` §5.1):

- **Edit tool** (doc-dict CRUD): `ApplyRuleEditor`, `FilterEditor`, `RegionEditor`, `SpawnEditor`,
  `TeamEditor`, `WoolEditor`, `RegionBuilder`, `DocAccess`, `EditException`, `SymmetryAuthoring`.
- **Configure tool** (intent → doc): `MapIntent`, `IntentGenerator`, `IntentNaming`, `MetaGenerator`,
  `TeamsGenerator`, `WoolGenerator`, `BuildGenerator`, `TeamPalette`, `SymmetryExpander`.
- **Sketch tool** (layout → cells): `SketchRasterizer`.

## 2. Target structure

Folder = namespace (C# convention; the project already half-follows it). **The codec stays flat at the
root** — it *is* the project identity, its namespace (`PgmStudio.Pgm.Serializer`, …) is the natural name,
and keeping it there is zero-churn. Only the three tools get folders+namespaces:

```
PgmStudio.Pgm/                          namespace PgmStudio.Pgm          ← the codec (charter)
│   MapParser.cs  RegionParser.cs  FilterParser.cs  Xml.cs              (parse:      XML → MapXml)
│   Serializer.cs  Deserializer.cs                                      (transcode:  MapXml ⇄ Dict)
│   XmlWriter.cs                                                        (write:      MapXml → XML)
│   JsonTree.cs  RegionBoundsDeriver.cs                                 (Dict utils / reconstruct)
│
├── Editing/                            namespace PgmStudio.Pgm.Editing  ← EDIT tool (doc-dict CRUD)
│     ApplyRuleEditor  FilterEditor  RegionEditor  SpawnEditor  TeamEditor  WoolEditor
│     RegionBuilder  DocAccess  EditException  SymmetryAuthoring
│
├── Authoring/                          namespace PgmStudio.Pgm.Authoring ← CONFIGURE tool (intent → doc)
│     MapIntent  IntentGenerator  IntentNaming  MetaGenerator
│     TeamsGenerator  WoolGenerator  BuildGenerator  TeamPalette  SymmetryExpander
│
└── Sketch/                             namespace PgmStudio.Pgm.Sketch    ← SKETCH tool (layout → cells)
      SketchRasterizer
```

(The codec's three sub-phases — parse / transcode / write — are *conceptual*; they don't need folders. If
the root ever feels crowded, a `Codec/` folder that **keeps** the `PgmStudio.Pgm` namespace, files-only,
is the zero-churn way to group them. Don't give it its own namespace — that churns every codec caller.)

## 3. Migration — concrete steps + verified churn

The reorg is mechanical and the dependency tracing makes the blast radius exact. **Two external `using`
lines change; nothing else outside `Pgm` does.**

1. **Create `Sketch/`**; move `SketchRasterizer.cs` in; change `…Editing` → `…Sketch`.
   *Caller to update: `Api/Endpoints/SketchEndpoints.cs`* — change `using PgmStudio.Pgm.Editing;` →
   `using PgmStudio.Pgm.Sketch;` (it uses no other `Editing` type — verify on the spot).
2. **Create `Authoring/`**; move the 9 configure files in; change `…Editing` → `…Authoring`.
   *Caller to update: `Api/Endpoints/AuthoringIntentEndpoints.cs`* — it uses `MapIntent`/`IntentGenerator`/
   `TeamsGenerator`; switch/add `using PgmStudio.Pgm.Authoring;`.
3. **`Editing/` keeps its namespace.** The remaining `using PgmStudio.Pgm.Editing;` consumers —
   `RegionEndpoints`, `SpawnAndRuleEndpoints`, `WoolAndFilterEndpoints`, `WriteEndpoints`,
   `Services/FilterWiring` — are untouched (their types stay in `Editing`).
4. **Mirror the test tree.** `tests/PgmStudio.Pgm.Tests` mirrors `src/` one-class-per-unit; move the
   corresponding test files into matching `Editing/`/`Authoring/`/`Sketch/` folders + namespaces.

**Non-obvious churn checks (all verified clean):**
- `Client` does **not** reference any moved type — the `BuildGenerator`/`MetaGenerator` hits in
  `Configure/*.razor.cs` are **comments**, and `Client` refs only `Contracts`+`Geom`. No client change.
- `Data`'s only `MapIntent` hit is the string constant `ArtifactKind.MapIntentJson` — **not** the type.
- `SymmetryExpander`, `WoolGenerator`, `BuildGenerator`, `MetaGenerator`, `TeamPalette`, `IntentNaming`
  have **no external code consumers** (Pgm-internal, or comment-only).

**Verification after the move (no behaviour should change):**
- `./tools/dev.sh restart` builds clean (the `using` updates are the only compile risk).
- `tools/PgmStudio.RoundTrip --parity` over the corpus — byte-identical roundtrip proves the codec
  and the generators are untouched (`JsonTree.DeepEquals`/`Canonical` are exactly this check).
- The `Pgm.Tests` project runs green (`dotnet run --project tests/PgmStudio.Pgm.Tests`).

## 4. Sequence

Land in this order (each independently shippable, lowest-risk first): **`Sketch/`** (1 caller) →
**`Authoring/`** (1 caller). Each is a commit; run the parity harness between them. The optional `JsonTree`
split (§1a note) is a separate, deferrable call.
