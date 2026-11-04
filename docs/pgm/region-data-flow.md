# Region data flow: persistence, derivation, and drawn-vs-wired display

How a region travels from the editor to the database and back, why its **category is never
stored**, why **all region rows are dropped and rewritten on every save**, and how the canvas tells a
drawn region from a wired one. Read alongside `region-categorization.md`
(what the categories *mean*) — this doc is about *where the data lives and when it's computed*.

---

## 1. What is persisted

A map's regions live as **relational rows** in the `region` table (the hybrid model — see
`CLAUDE.md`): `region_key`, `type`, `bounds_json`, `coords_json`, `child_ref_ids_json`, `source_id`.
That's the **canonical PGM geometry/structure** and nothing else.

**The category is *not* a column and is *not* stored anywhere.** Neither is `subtype` or the rule
`wiring` — they are all **derived on read** by `RegionCategorizer` from *usage* (which `spawns[]` /
`wools[]` / `spawners[]` / `apply_rules[]` reference the region). This is the contract's rule:
*"category is derived, never persisted; `region_categories` is only a store of user overrides."*

---

## 2. The save path is *entity-replace* — and why

`MapWriter.SaveDocAsync(mapId, doc)` does, in one transaction:

1. `DeleteEntitiesAsync(mapId)` — **delete every** author/team/kit/region/filter/wool/spawn/spawner
   row for the map (features and `map_artifact` blobs are **kept**).
2. `Deserializer.FromDict(doc)` → rebuild the domain `MapXml` from the document dict.
3. `WriteEntitiesAsync` + `WriteWoolsFromDocAsync` — **re-insert** all rows from scratch.

So **every edit drops and recreates all region rows.** This is deliberate:

- **The codec is the source of truth.** Writes go `doc dict → FromDict → rows` through the exact same
  serializer used for round-trip parity (350/350). There is no second "diff/patch the rows" code path
  to keep correct; replace-from-document is provably consistent with the canonical format.
- **No identity churn that matters.** Rows use a surrogate `id` (identity), but regions are addressed
  by **`region_key`** (the map-level id like `build-area` or `ui-rect-1`), which is **stable across the
  replace** because it comes from the document. References (`child_ref_ids_json`, `source_id`, spawn/
  wool region keys) are by key, so they survive.
- **Editor-only metadata has nowhere to ride.** `FromDict` only understands the PGM map format, so
  anything not in that format (e.g. `region_categories`) is **dropped** — `RegionEditor.cs` notes
  *"region_categories is an editor-only undo hint; it is not persisted (FromDict drops it)."*

**Key implication:** you **cannot** stash editor state in a column on `region` (or any entity table) —
the next save wipes it unless it round-trips through the codec, which would pollute the canonical
format. Editor-only state must live **outside** the entity-replace path, as a `map_artifact` blob rather
than a region column.

---

## 3. The read path + when derivation happens

`MapReader.ReadDocAsync(map)` rebuilds the full document dict from the rows (regions, filters,
spawns, wools, spawners, apply-rules…). It does **not** produce `region_categories`.

**Derivation is not an event — it is recomputed on every read.** `RegionCategorizer` runs, statelessly,
only when an endpoint asks for it; the result is returned and thrown away (never cached, never written
back). Call sites:

| endpoint | what it derives |
|---|---|
| `GET /regions/tree` (`AuthoringEndpoint`) | `Categorize` (grouping) + `DeriveFacets` (per-node category / subtype / wiring) — **the one the editor sidebars + canvas hit on every load/reload** |
| `GET /regions` (`AnalysisEndpoints`) | `DeriveFacets` (facets + counts) |

So when wiring changes (R1: you attach a filter / spawn / wool → that saves an `apply_rule`/`spawn`/
`wool` row), there is nothing to "re-run": the **next** `/regions/tree` fetch rebuilds the doc *with*
that row and re-derives, so the region's category/subtype update automatically.

A freshly drawn region reads as **`other`** for the same reason: at draw time the saved doc has the
region row but **no** rule/spawn/wool referencing it, so the next derivation has no signal — until R1
lets you save that wiring.

---

## 4. `Categorize` vs `DeriveFacets` (overrides vs pure derivation)

- `DeriveFacets(doc)` → `{id: {category, subtype, roles}}` — **pure derivation**, no overrides. This is
  the parity-checked output and what each **tree node** carries (`node.category`, `node.subtype`,
  `node.wiring`).
- `Categorize(doc)` → flat `{id: category}` = `DeriveFacets` **+ `region_categories` overrides** applied
  on top. Used for tree **grouping**.

Because `region_categories` isn't persisted (§2), the two agree in practice today. The **canvas** filters
by `node.category` (the pure value), so a drawn region stays `other` until something wires it.

---

## 5. How the canvas displays a region: wired vs just drawn

The canvas (`studio-canvas.js` → `WorldCanvas`) renders the **primitive** nodes (compounds excluded;
their primitive children carry the classification) selected by this rule, walking the **whole** tree
(objective/spawn/build regions nest inside rule-containers in the "other" group, so a group-name filter
would miss them):

```
render n  ⟺  n is primitive AND n.category ∈ {the step's categories}        // WIRED
```

- **Wired region** — e.g. a rectangle referenced by `enter=only-red`, or a region in `spawns[]`. Its
  derived `category` (`spawn`/`wool`/`build`) matches the activity, so it renders.
- **Just-drawn region** — `category == "other"` until a rule, spawn or wool references it, so no activity
  draws it; its F3 orbit counterparts are the same.
- **A drawn region that gets wired** — once something references it, derivation gives it a real category
  and the next `/regions/tree` renders it under that activity, with nothing to clean up.
