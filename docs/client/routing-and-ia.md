# Routing and information architecture

Where everything lives in the URL, what each surface is called, and how a reader gets from the landing to a
map and back. It owns four things nothing else does: the **URL law**, the **route table**, the **labels** the
UI shows against the names the code uses, and the **collections** the maps page fans into. It does not
describe what any tool does — that is `../tools/`, starting at `flow.md`, which also owns the difference
between a map's stage and the layers it carries.

## The URL law

**The map is the resource, so it lives in the path, and the mode is a trailing segment.** Never `?map=…`: a
query parameter reads as an optional filter, and an editor without a map is not a page at all.

**The id is the on-disk map directory name** — maps are already clean slugs (`thunder`, `pigland`,
`dragons_hearth`), so `/maps/thunder/edit` works and matches the `api/map/{slug}` calls beside it. The pretty
`<name>` out of the XML is shown in the UI and never put in a URL: spaces and capitals force encoding and are
not stable across a rename.

**Query parameters are transient view state only** — selection, zoom, the active layer, an open panel, the
phase a tool opens on (`?phase=info`), the row a listing should highlight (`?just={slug}`). The maps page's
`?stage=` fits the rule: it selects which collection is shown, not which map.

## The routes

| Route | Component | Is |
|---|---|---|
| `/` | `Index` | the landing — seven cards over live counts |
| `/maps` | `Maps` | the map collections; `?stage=plan\|sketch\|configure` selects one, absent means Maps |
| `/maps/{slug}/plan` | `PlanTool` | the plan tool on a map |
| `/maps/{slug}/sketch` | `SketchTool` | the sketch tool |
| `/maps/{slug}/configure` | `ConfigureTool` | the configure wizard |
| `/maps/{slug}/edit` | `EditTool` | the region editor for a map that already has a `map.xml` |
| `/maps/new` | `ConfigureTool` | the same tool with no slug: its Import phase, which is how a world becomes a map row |
| `/plan-editor` | `PlanTool` | the same tool with no map: a candidate plan row, no phase host |
| `/generator` | `GeneratorTool` | the composer's browse-and-pin gallery |
| `/catalog` | `CatalogTool` | the shape catalog |
| `/library` · `/library/{tab}` | `LibraryTool` | the style, theme, part and room library |
| `/design` | `Design` | the component showcase |
| `/not-found` | `NotFound` | 404 |

Two of them carry a tool twice, and both times the slug-less route is the origination surface: a map has no id
until Import creates its row, and a plan has no map until it is authored onto one. Blazor discovers routes from
`@page`, so this table is the only inventory there is — nothing in the app enumerates them.

## The collections

`?stage=` selects one of four, and they are not the same kind of question. Two list **a layer a map holds**
and two list **a stage a map stands at**, which is why a map appears in more than one.

| List | Shows | Primary action |
|---|---|---|
| **Plans** (`?stage=plan`) | every map holding a plan, including ones long since built | New plan |
| **Sketches** (`?stage=sketch`) | every map holding a drawn sketch, including ones already configured | New sketch |
| **Configuring** (`?stage=configure`) | maps standing at `configure` — terrain but no finished `map.xml` | Import a world |
| **Maps** (`/maps`) | maps standing at `edit` — a finished `map.xml` | — |

A map keeps every layer it has ever had, so "every map with a plan" and "every map at the plan stage" are
different collections; each list says which in its own blurb. `GET /api/maps[?stage=…]` serves them and
`GET /api/maps/stage-counts` the landing tallies, each counting exactly what its list does — so a card and the
page it opens cannot disagree.

## The landing

Seven cards in two groups. The first four are where authoring starts — **Plan a layout** (the Plans
collection), **Browse generated layouts** (`/generator`), **Shape catalog** (`/catalog`) and **Style and theme
library** (`/library`) — three of which need no map at all. The last three are the map lifecycle — **Sketch**,
**Configure**, **Edit** — each deep-linking into its collection and carrying that collection's live count.

## Labels against code names

The visible label is deliberately decoupled from the concept the code is built on, in one place:

| UI label | Code name | Where |
|---|---|---|
| **Configure** | **authoring** — `MapIntent`, the intent model, `../pgm/new-map-authoring.md` | `/maps/{slug}/configure` |
| **Sketch** | sketch | `/maps/{slug}/sketch` |
| **Edit** | the region editor | `/maps/{slug}/edit` |

The reason is that "authoring" names what the tool *does* to a map and "Configure" names what a person came to
do, and the two audiences are different. Renaming the concept to match the label would have touched the intent
model, its endpoints and every document that reasons about it; renaming the label costs nothing. Nowhere else
in the studio does a label differ from its type name, and this one is worth the exception.

**"New" is not a discriminator.** Sketch and Configure both produce a new map, so a label built on "new" would
separate nothing. The axes that do separate them are the artifact (geometry against configuration) and the
lifecycle position (no `map.xml` yet against has one), which is why the labels are verbs.

## Exits

**A tool leaves through the collection it belongs to, not through the landing.** The topbar's home link is the
exit, and each of the four map tools names its own list: Sketch → *Sketches*, Plan → *Plans*, Configure →
*Configuring*, Edit → *Maps*. The surfaces that hold no map — the generator, the catalog, the library, the
design showcase and the maps page itself — go to *Studio* instead, because there is no collection above them.
The plan tool sits on both sides: opened on a map it returns to Plans, opened at `/plan-editor` on a bare
candidate it returns to Studio.

Beside that link the topbar carries the trail — the map's name, then the tool or phase, dimmed. Neither is a
link: the map is already open, so a second way to it would be a way to nowhere.

One exit is not a link at all. **Finishing a sketch** rasterizes the layout, advances the map to `configure`,
and lands on the Configure collection with `?just={slug}`, which highlights the row and offers *Continue to
Configure* — rather than force-marching into the wizard, since finishing the geometry and starting the
configuration are two decisions.
