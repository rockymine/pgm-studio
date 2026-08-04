# tools/decorate

Prototypes for the **dressing pass** — the decoration stage that runs last in realize, after
`TerrainPainter` (G34's prop-stamps slice, **G161**; modelled in `docs/world-export/decoration.md`). The
pass itself ships; these pages are where its shapes were worked out, and they stay as the fastest visual
check that a model change is real.

## `prototype.html`

One self-contained page (no dependencies — open it in any browser) walking the four concepts: a
paint-aware **flora overlay**, a drag-a-line **path** tool, **boulder** scatter, and **trees** (vanilla
templates + a Catmull-Rom limb-spline grower). Every figure is emitted by the real algorithm the stage
runs — deterministic noise mirroring `PatternNoise` (hash-from-cell), true distance-to-segment path
bands, blue-noise scatter, Catmull-Rom limb splines swept with the path's own disc. Nothing is hand-drawn, so it is the fastest check that a model
change is real — the same discipline as `tools/compose/showcase.cs` being `model.md`'s live twin. The C#
implementations are the authority; this is the sketch they came from.

Open directly:

```
xdg-open tools/decorate/prototype.html      # or just open the file in a browser
```

§6 (Symmetry & fairness, G162) dresses a **real generated board** rather than a synthetic one — the
free-scatter-vs-orbit-fanned comparison that shows why gameplay-affecting dressing must be mirrored.

## `dress-map.cs`

Composes a board with the real generator and emits its terrain + symmetry + objectives as `map.json`, which
§6 of the prototype embeds. Run from the repo root:

```
dotnet run tools/decorate/dress-map.cs -- <playersPerTeam> <seed>   # defaults: 24 7 → tools/decorate/map.json
```

The committed `map.json` is 40 players / seed 2 (`rot_180`), the board §6 dresses.
