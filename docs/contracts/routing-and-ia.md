# Routing & information architecture

> **Status: the IA pass is landed; the Configure wizard pages (`NS`) + Sketch (`S2`) are the open
> routes.** Settles the URL shape + user-facing labels for the three map surfaces. **Decouples the
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

## Route table

The **map is the resource → it lives in the path**; the **mode is a trailing segment**. Never
`?map=…` — a query param reads as an optional filter, but the editor is meaningless without a map.

| Route                    | Label (UI)  | What it is                                         | Today              | Lands in |
| ------------------------ | ----------- | ------------------------------------------------- | ------------------ | -------- |
| `/maps`                  | *Maps*      | dashboard / map list (entry)                      | `Home.razor`       | live     |
| `/`                      | —           | redirect → `/maps`                                | `Index.razor`      | live     |
| `/maps/{id}/edit`        | **Edit**    | existing-map region editor (activities)           | `Editor.razor`     | live†    |
| `/maps/{id}/configure`   | **Configure** | new-map intent wizard — the six phases          | —                  | `NS`     |
| `/maps/new`              | *(entry)*   | originate a map: **Import** a world folder (now) · **Sketch** (later) | — | `NS` / `S2` |
| `/concepts`              | —           | the authoring concept mock (`Authoring.razor`)    | `Authoring.razor`  | live     |
| `/design`                | **Design**  | design-system showcase                            | `Design.razor`     | live‡    |
| `/not-found`             | —           | 404                                               | `NotFound.razor`   | live     |

† `Edit` is the **route-renamed** `/editor/{slug}` (the editor internals stay frozen) — a rename, not a refit.
‡ Open: whether `/design` also moves under `/concepts` (flagged in `NS`).

## The `{id}`

Use the **on-disk map directory name** as the path id — maps are already clean slugs (`thunder`,
`pigland`, `dragons_hearth`), so `/maps/thunder/edit` just works and matches the existing
`api/map/{slug}` calls. Show the pretty `<name>` from the XML in the UI; **never** put the display
name ("Annealing IV") in the URL — spaces/caps force encoding and aren't stable.

## `/maps/new` — the one pre-id surface

Sketch and Import both *originate* a map, so neither has an id yet — they share the standard REST
"new resource" entry, `/maps/new`:

- **Import** (now, `NS` + `B8` open-folder) is the `new-map-authoring.md` §12 landing — **Source →
  Found → Plan**. Picking an xml-less world folder creates the map record (a slug); **Start authoring**
  then enters `/maps/{slug}/configure`.
- **Sketch** (later, `S2`) is the second origination path — draw geometry, save → a slug → likewise
  flows into `/maps/{slug}/configure`.

So `/maps/new` answers "how do I get a map?", and `/maps/{id}/configure` is the per-map wizard that
both feed.

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
The map id and the mode stay in the path.

## Migration

The IA surface was small (Blazor discovers `@page`; no central route table; nav is plain `href`s), and
the pass below is **landed**. What remains is the `NS` wizard build (`/maps/{id}/configure` + `/maps/new`).

**Landed:**

1. **`Editor.razor`** — `@page "/editor/{Slug}"` → `@page "/maps/{Slug}/edit"`; breadcrumb home → `/maps`.
   No back-compat alias kept (nothing was live yet). The activity rail's inner **"Configure"** activity
   was renamed **"Setup"** to free the word for the top-level mode (`Editor.razor.cs` + the switch case).
2. **`Home.razor`** — `@page "/"` → `@page "/maps"`; list-row `href` → `maps/{slug}/edit`; footer
   "Authoring" → **"Concepts"** → `/concepts`.
3. **`Index.razor`** (new) — `@page "/"` redirects to `/maps`.
4. **`Authoring.razor`** (the concept mock) → `/concepts`; breadcrumb home → `/maps`.
5. **Docs** — route strings reworded in `new-map-authoring.md` §12, `CLAUDE.md`, `TODO.md`,
   `monument-candidate-store.md`; `FEATURES.md` gained an "App shell & routing" entry. The concept
   *name* "authoring" stays everywhere; only route strings changed.

**Open:** the `NS` Configure wizard claims `/maps/{id}/configure` + `/maps/new`; `S2` adds Sketch under
`/maps/new`.
