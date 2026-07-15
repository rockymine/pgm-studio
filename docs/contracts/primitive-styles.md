# Primitive drawing styles across the four editors — audit & unification (CV9)

Status: **landed** (`CV9`). This is the cross-editor inventory of *how a drawable primitive is rendered
and styled* in **Sketch**, **Edit**, **Configure**, and **Plan**, and the design that unified it. It
widens the scope of `canvas-interaction.md` §10 (which only compares Edit vs Configure) to all four
surfaces. §6's conclusion is now implemented — the shared helper is `render/primitive-style.js`
(`primitiveStyle`), and `renderShape` has the `point` case.

Read alongside `canvas-interaction.md` (the shared-canvas contract) and `new-map-authoring.md`
(the wizard phases). The renderers audited: `render/shape-render.js` (shared),
`render/sketch-render.js`, `canvas/editor-canvas.js`, `canvas/plan-canvas.js`.

---

## 0. What "a primitive" means in each editor (the semantic frame)

The four editors draw axis-aligned/radial shapes, but a shape *means* a different thing in each —
and the visual style already encodes that meaning. This is the fact that frames the whole refactor:
**style is a function of the primitive's semantic tier, not of the editor.** The same tier looks the
same wherever it appears; different editors just populate different tiers.

| Editor | What a drawn shape *is* | Background | Colour carries |
|---|---|---|---|
| **Sketch** | a **terrain shape** in a boolean vocabulary (add / subtract / override) that later rasterises into the base | none (blank authoring grid) | the **operation** (add=teal, subtract=red) |
| **Edit** | a **real `map.xml` region** (the source of truth) | immutable rasterised terrain | nothing — uniform slate; a region has no team meaning at this layer |
| **Configure** | a **region intent** (dummy node) — the same primitives as Edit but **derived/suggested** | immutable rasterised terrain | the **team / dye** it belongs to (derived) |
| **Plan** | a **rectangle piece coloured by role** — some pieces are true terrain + XML regions, some are annotation, some are technical/visualization-only | none (cell grid) | the **role** (+ surface-height tint) |

The through-line: **Sketch and Plan are where the author *decides* geometry/symmetry; Edit and
Configure are where it is *shown* (real) and *suggested* (derived).** Plan is the odd one out — it
draws rectangles only and layers three *tiers of realness* on top (terrain / annotation / technical),
which is exactly the visual vocabulary a unified primitive-style descriptor needs to express.

---

## 1. Shape-type inventory — which renderer, which SVG element

`renderShape(type, boundsOrPoly, toSvg, attrs)` (`render/shape-render.js:21-53`) is the **shared**
type→element dispatch. It is imported by `editor-canvas.js` (Edit+Configure) and `sketch-render.js`
(Sketch). **Plan does not use it** — `plan-canvas.js` draws every piece as a `<rect>` directly and
adds its own hatch patterns and objective markers.

| type | branch | SVG element | anchor |
|---|---|---|---|
| polygon (`.exterior`/`.polygons`) | polygon path | `<path fill-rule=evenodd>` | `shape-render.js:25-30` |
| `cylinder` / `circle` / `sphere` | `RADIAL_TYPES` | `<ellipse>` | `shape-render.js:9,36-44` |
| `rectangle` / `cuboid` / `block` / **`point`** / … | fallthrough | `<rect>` | `shape-render.js:46-52` |

Per-editor type coverage:

- **Sketch** (`sketch-render.js:33-56`): `rectangle`→rect, `circle`→ellipse, `polygon`/`lasso`→inline
  `<path>` (Bézier-capable, bypasses `renderShape`). Library primitives (`shape-library.js:36-52`)
  are **not** new types — `instantiate()` emits plain `rectangle`/`polygon` specs (n-gons and
  polyominoes are polygons; the `I` bar is a rectangle; `holesquare` is add-rect + subtract-rect).
- **Edit / Configure** (`editor-canvas.js:976-1003`): `rectangle`/`cuboid`→rect, radial→ellipse,
  and **`point` with `marker:true`** is intercepted *before* `renderShape` and drawn as a fixed-size
  `<circle>` (`editor-canvas.js:986-997`). Composite/transform types (`union`/`intersect`/`negative`/
  `complement`) are filtered out before render (`editor-canvas.js:60,1064`).
- **Plan** (`plan-canvas.js:429-478`): every piece/zone is a `<rect>`; objective markers are a
  `<circle>` (spawn, with a facing line) or a rounded `<rect>` (wool/iron).

### 1a. The `point` gap (the concrete CV9 bug)

`renderShape` has **no `point` case** — a bare point falls through to the `<rect>` branch and renders
as a 1×1 block that shrinks with zoom. The only reason Configure spawns look right is the `marker:true`
opt-in that swaps in a fixed-radius circle *outside* `renderShape`. So a "point" is a rect on Edit and
a circle on Configure — same type, two looks, because the fix lives in one caller instead of the shared
renderer.

---

## 2. Style inventory — the visual language

Each editor has its **own** style function; there is no shared style descriptor. But the styles fall
into a small, consistent vocabulary:

| tier / treatment | meaning | fill | stroke | where |
|---|---|---|---|---|
| **solid, opaque** | real buildable terrain / real region | role/dye/team colour, `fill-opacity 0.7–0.85` | solid, same colour | Plan generating pieces (`plan-canvas.js:442-457`) |
| **translucent, dashed** | a region / an area (not solid ground) | colour @ `0.20` (Edit/Configure) / accent @ `0.22` (Plan zone) | dashed (`4,2` region · `7 4` zone) | `editor-canvas.js:1012-1016`; `plan-canvas.js:402-421` |
| **hatched, dashed** | technical / visualization-only (teaches behaviour: intended holes, dock points) | diagonal/crossed hatch pattern | dashed, same colour (`5 4`) | Plan buffer/connector (`plan-canvas.js:429-440,303-321`) |
| **boolean-tinted** | terrain add vs subtract | teal (add) / red (sub) @ `0.28`; `6 3` dash if override | solid | Sketch (`sketch-render.js:19-27`) |
| **fixed-size marker** | a point objective (spawn / wool source) | team/dye/marker colour @ `0.85–1.0`, radius **fixed** (not zoom-scaled) | ink/`marker-stroke` | `editor-canvas.js:986-997`; `plan-canvas.js:461-478` |
| **ghost / derived** | a non-editable symmetry-orbited or cross-layer preview | colour @ `0.06–0.08`, finer dash | faint | `editor-canvas.js:1008-1011`; `plan-canvas.js:360-400`; sketch `sketch-render.js:85-109` |

The exact style knobs, per editor:

- **Edit / Configure** — `#regionAttrs(color, ghost)` (`editor-canvas.js:1007-1017`): region fill
  `0.20` + dash `4,2`; ghost fill `0.06` + dash `2,3`; selected → solid, width `2.5`
  (`editor-canvas.js:1035-1039`). Marker circle `r 6/5` by `primary` (`:986-997`).
- **Sketch** — `shapeAttrs()` (`sketch-render.js:19-27`): fill `0.28`, width `1.2`, add/sub colour,
  `override`→`6 3` dash. Islands/mirror/ghost-islands each have their own attrs
  (`sketch-render.js:71-109`).
- **Plan** — inline per-role in the renderer: generating pieces solid `0.7` + surface-height `tint()`
  toward white (`plan-canvas.js:442-457`); annotation pieces hatched `0.9` + dashed `5 4`
  (`:429-440`); build zone translucent-accent `0.22` + dashed `7 4` with cut-out holes (`:402-421`).

---

## 3. Colour source — the real Edit-vs-Configure-vs-rest divergence

| editor | colour source |
|---|---|
| **Edit** | **none** — real tree regions carry no `color`; `region.color ?? var(--canvas-region)` always falls back to slate (`editor-canvas.js:978`, `--canvas-region` `tokens.css:99,197`). Every Edit region is uniform slate. |
| **Configure** | **team / dye hex** — every dummy node is tinted `GameColors.ChatHex(team)` or `DyeHex(color)` (`ProtectionPhase.razor.cs:218-234`, `SpawnPhase.razor.cs:291-297`, `WoolRoomPhase.razor.cs:192-209`, …). |
| **Sketch** | **operation** — add teal `--canvas-add-*`, subtract red `--canvas-sub-*` (`tokens.css:68-71`). |
| **Plan** | **role** — `ROLE_COLORS` (piece grey, spawn purple, wool-room green, buffer orange, connector teal; `plan-doc.js:21`), lightened by surface height. |

---

## 4. Icons — the sidebar/inspector inconsistency

`RegionNode.Icon(type)` (`Models/RegionNode.cs:85-103`) is the **canonical** type→Lucide map
(`point→dot`, `block→square`, `rectangle→rectangle-horizontal`, `cylinder→cylinder`, …). Most
Configure phases consume it dynamically, but several **hardcode** an icon that disagrees with the
node's real type:

| razor | hardcoded | node type | verdict |
|---|---|---|---|
| `SpawnPhase.razor:26,36,62,108` | `cylinder` | `point` (marker) | **mismatch** — should be `dot` |
| `WoolMonumentsPhase.razor:28,51,107` | `square` | `point` (marker) | **mismatch** — should be `dot` |
| `WoolSpawnPhase.razor:24,47,70` | `dot` | `point` (marker) | matches canonical |
| `ProtectionPhase` / `WoolRoomPhase` / `BuildLayerPhase` | `rectangle-horizontal` | `rectangle` | matches |

The point-markers are the hotspot: they render as circles but their sidebar icons are hardcoded to
`cylinder` / `square` instead of the canonical `point→dot`.

---

## 5. Duplication the refactor should collapse

- **Add/sub colour constants x3**: `sketch-render.js:12-15` (committed), `sketch-draw-controller.js:19-22`
  (previews), plus raw tokens in `components.css`. A recolour needs all three.
- **Plan role colours x2**: `plan-doc.js:21` (`ROLE_COLORS`) and `PlanEditor.razor.cs:100-112`
  (toolbar/inspector palette) — two hand-kept copies of the same five hexes.
- **Two style functions that are 90% the same**: `#regionAttrs` (Edit/Configure) and `shapeAttrs`
  (Sketch) differ only in fill-opacity (0.20 vs 0.28), dash pattern, and colour source.

---

## 6. Unification conclusion

"Draw a primitive" can become **one data-driven thing** by separating three inputs that are today
tangled into each editor's bespoke draw code:

1. **shape** — `{rectangle | radial | polygon | point}`. Fixes the `point` gap by giving `renderShape`
   a real `point` case (a dot/circle sized in *screen* units), so the `marker:true` workaround in
   `editor-canvas.js` collapses into the shared renderer.
2. **colour** — supplied by the caller from its own semantic source (Edit: none/slate · Configure:
   team/dye · Sketch: operation · Plan: role). The renderer never decides colour.
3. **treatment** — one enum over the §2 vocabulary: `region` (translucent dashed) · `terrain`
   (solid opaque) · `technical` (hatched) · `marker` (fixed-size) · `ghost` (faint derived). Each
   editor picks a treatment per primitive instead of hand-writing fill/stroke/dash.

A single `primitiveStyle({ colour, treatment, selected, primary })` helper then replaces
`#regionAttrs`, `shapeAttrs`, and the inline plan role-styling; `renderShape` grows the `point` case
and stays the shared element factory. Icons route through `RegionNode.Icon` everywhere (delete the
hardcoded `cylinder`/`square` in `SpawnPhase`/`WoolMonuments`), so `point→dot` is consistent.

Scope note: Plan's hatch patterns and surface-height tint are genuinely Plan-specific and stay in
`plan-canvas.js`; the win is the *shared* pieces — the `point` render, the style vocabulary enum, the
colour-is-caller-supplied rule, and the single icon map — not forcing Plan through `renderShape`.
