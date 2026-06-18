# Routing & information architecture

> **Status: landed — including the staged landing + dashboard.** Settles the URL shape + user-facing
> labels for the three map surfaces, and the entry point that fans out to them. **Decouples the
> visible label from the code/concept name:** the code keeps **"authoring"** (the `N` series,
> `new-map-authoring.md`, the intent model) — the UI says **"Configure"**. Supersedes the *route names*
> in `new-map-authoring.md` §12; the §12 *interaction* design (three-level nav, Import sub-steps) is
> unchanged. Open sub-decisions are flagged inline.

## The model: a map is one resource; the verbs are modes on it

There is **one noun — a map** — and three things you do to it. They are **stages of a lifecycle**, not
parallel apps:

```
Sketch  ──▶  Configure  ──▶  Edit
(draw          (built/blank      (refine an
 geometry       world → map.xml   existing
 from nothing)  via intent)       map.xml)
```

- **Sketch** makes the **geometry** (the physical world) by drawing 2-D shapes — from nothing.
- **Configure** makes the **configuration** (`map.xml`) for a world that already has geometry but no
  XML — either one you just sketched, or one imported/built in Minecraft. This is the intent-driven
  **authoring wizard** (`new-map-authoring.md`).
- **Edit** modifies the XML of a map that **already has one** — the region-first existing-map editor.

> **"New" is not the discriminator.** Both Sketch *and* Configure produce a new map, so leaning on
> "new" to separate them is the trap. The real axes are **artifact** (geometry vs config) and
> **lifecycle** (no XML yet vs has XML). That is why the labels are verbs, not "New Map".

## Stage — the lifecycle marker (how a map is filed)

The lifecycle above is **stored**, not inferred: `map.stage` ∈ `{sketch, configure, edit}`
(`Contracts.MapStage`). It's the discriminator that lets a user re-open a draft, so each surface lists
**only its own** maps. Deriving it from artifacts was rejected — a sketch-in-progress *is* cleanly
derivable (`sketch_layout_json` with no `layer_parquet`), but **Configure and Edit are not
distinguishable from data**: the intent wizard regenerates real `region`/`team` rows on every save, and
the `map_intent_json` artifact never clears, so a half-configured map and a finished one look identical.
A stored marker is unambiguous and a trivial `WHERE stage = ?`.

Transitions (each set by the endpoint that performs the step):

- **sketch** — seeded by `POST /api/sketch` (sketch-create).
- **configure** — set when a world gains geometry but not a finished `map.xml`: `import-folder` /
  `import-url`, and **sketch-finish** (`POST /api/map/{slug}/sketch/finish`) which advances
  `sketch → configure`.
- **edit** — the default (full-XML corpus import); the eventual `configure → edit` step lands with the
  Configure wizard's **Export** (today a stub — `M0004` backfilled existing rows by the rule above:
  intent-authored or geometry-without-regions → `configure`, sketch-only → `sketch`, else `edit`).

## Landing & exits

- **Landing (`/`)** replaces the old bare redirect: a hero over three **cards** (Sketch · Configure ·
  Edit, the shared `.card` component) that deep-link into `/maps?stage=…`, each showing a live count
  from `stage-counts`.
- **Overviews** share one page (`Home.razor`); the **activity rail is the stage switcher** and each
  stage carries its own primary action (Sketch → New-sketch, Configure → Import) and resume target
  (`/maps/{id}/{stage}`).
- **Exits are consistent.** Every editor's topbar **home breadcrumb** returns to *its* stage overview
  (Sketch → `?stage=sketch`, Configure → `?stage=configure`, Edit → `/maps`), which in turn carries a
  *Studio* crumb back to `/`. **Sketch-finish no longer force-marches** into the wizard: it rasterizes,
  advances the stage, and lands on the Configure overview with a *Continue to Configure* offer
  (`?just={slug}` highlights the row + shows the callout).

## Route table

The **map is the resource → it lives in the path**; the **mode is a trailing segment**. Never
`?map=…` — a query param reads as an optional filter, but the editor is meaningless without a map.

| Route                    | Label (UI)  | What it is                                         | Lives in           | Status |
| ------------------------ | ----------- | ------------------------------------------------- | ------------------ | ------ |
| `/`                      | *Studio*    | **landing** — three lifecycle cards + live counts | `Index.razor`      | live   |
| `/maps`                  | **Edit**    | staged dashboard, default stage = `edit`          | `Home.razor`       | live   |
| `/maps?stage=sketch`     | **Sketch**  | sketch-draft overview + New-sketch                | `Home.razor`       | live   |
| `/maps?stage=configure`  | **Configure** | configure-stage overview + Import                | `Home.razor`       | live   |
| `/maps/{id}/edit`        | **Edit**    | existing-map region editor (activities)           | `Editor.razor`     | live†  |
| `/maps/{id}/configure`   | **Configure** | new-map intent wizard — the six phases          | `ConfigureWizard`  | live   |
| `/maps/{id}/sketch`      | **Sketch**  | sketch tool — draw geometry                       | `SketchEditor`     | live   |
| `/maps/new`              | *(entry)*   | originate a map: **Import** a world folder (Sketch now originates from the Sketch overview) | `ConfigureLanding` | live‡  |
| `/concepts`              | —           | the authoring concept mock (`Authoring.razor`)    | `Authoring.razor`  | live   |
| `/design`                | **Design**  | design-system showcase                            | `Design.razor`     | live   |
| `/not-found`             | —           | 404                                               | `NotFound.razor`   | live   |

† `Edit` is the **route-renamed** `/editor/{slug}` (the editor internals stay frozen) — a rename, not a refit.
‡ `/maps/new` is now **Import-only** — the "Sketch from scratch" origination moved to the Sketch
overview's New-sketch action; the download-link source stays parked there.

Supporting API: `GET /api/maps[?stage=…]` (the staged list; an invalid/absent stage returns all) and
`GET /api/maps/stage-counts` (the landing-card tallies).

## The `{id}`

Use the **on-disk map directory name** as the path id — maps are already clean slugs (`thunder`,
`pigland`, `dragons_hearth`), so `/maps/thunder/edit` just works and matches the existing
`api/map/{slug}` calls. Show the pretty `<name>` from the XML in the UI; **never** put the display
name ("Annealing IV") in the URL — spaces/caps force encoding and aren't stable.

## Origination — two pre-id surfaces

Sketch and Import both *originate* a map, so neither has an id yet. They split by stage rather than
sharing one page:

- **Import** — the `new-map-authoring.md` §12 landing at `/maps/new` (**Source → Found → Plan**),
  reached from the **Configure overview**'s *Import a world* action. Picking an xml-less world folder
  creates the map record at stage `configure` (a slug); **Start authoring** enters
  `/maps/{slug}/configure`.
- **Sketch** — originates from the **Sketch overview**'s *New-sketch* action: `POST /api/sketch`
  creates a stage-`sketch` draft (a slug) → `/maps/{slug}/sketch`. Drawing, then **Finish**, rasterizes
  the geometry, advances the map to stage `configure`, and returns to the Configure overview.

So origination is reached from the stage overviews (themselves reached from the landing cards), and
`/maps/{id}/configure` is the per-map wizard both paths feed.

## Label ↔ code mapping (the decoupling)

| UI label    | Code / concept (unchanged) | Where it lives                                                |
| ----------- | -------------------------- | ------------------------------------------------------------- |
| **Configure** | **authoring** — `N` series, `new-map-authoring.md`, intent model | the wizard at `/maps/{id}/configure` |
| **Edit**    | the existing editor        | `Editor.razor` + `EditorActivities/*` at `/maps/{id}/edit`     |
| **Sketch**  | `sketch_api` / sketch pages | `S2`, at `/maps/new`                                          |

> **Collision to resolve (not blocking):** the **Edit** editor already has an internal **"Configure"
> activity** (`ConfigureActivity`, the scan/setup pass — `settings-2` icon). With **Configure** now a
> top-level mode, that inner activity should be renamed (e.g. **Setup** / **Scan**) so the word means
> one thing. Track under the editor's activity work, not `NS`.

## Query params = view state only

Query params are for **transient, bookmarkable view state** — never identity:
`/maps/thunder/edit?region=blue_spawn&zoom=2&layer=wool`. Selection, zoom, active layer, open panel.
The map id and the mode stay in the path. The dashboard `?stage=` filter fits this rule — it selects
*which* collection view, not *which* map.

## Migration

The IA surface was small (Blazor discovers `@page`; no central route table; nav is plain `href`s), and
the whole pass — IA rename, the Configure wizard + Sketch routes, and the staged landing/dashboard — is
**landed**.

**Landed:**

1. **`Editor.razor`** — `@page "/editor/{Slug}"` → `@page "/maps/{Slug}/edit"`; breadcrumb home → `/maps`.
   No back-compat alias kept (nothing was live yet). The activity rail's inner **"Configure"** activity
   was renamed **"Setup"** to free the word for the top-level mode (`Editor.razor.cs` + the switch case).
2. **`Home.razor`** — `@page "/maps"`; now the **staged dashboard** keyed on `?stage=` (default `edit`);
   the activity rail is the stage switcher; per-stage primary action + resume target.
3. **`Index.razor`** — `@page "/"` is now the **landing** (three lifecycle cards + `stage-counts`), no
   longer a redirect.
4. **`Authoring.razor`** (the concept mock) → `/concepts`; breadcrumb home → `/maps`.
5. **Stage** — `map.stage` column (`M0004`) + `MapStage`; `GET /api/maps?stage=` and
   `/api/maps/stage-counts`; stage seeded/advanced at sketch-create, import, and sketch-finish.
6. **Sketch origination** moved off `/maps/new` (now Import-only) to the Sketch overview's New-sketch.
7. **Exits** — editor home breadcrumbs point at their stage overview; sketch-finish lands on the
   Configure overview with a *Continue* offer instead of force-navigating into the wizard.
8. **Docs** — route strings reworded in `new-map-authoring.md` §12, `CLAUDE.md`, `TODO.md`,
   `monument-candidate-store.md`; `FEATURES.md` gained an "App shell & routing" entry. The concept
   *name* "authoring" stays everywhere; only route strings changed.

**Open:** `configure → edit` has no live trigger yet — it lands with the Configure wizard's **Export**
step (currently a stub).
