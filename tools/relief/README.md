# relief — the interior-elevation prototype

The live twin of `docs/contracts/sketch-relief.md`. Every figure and every number in that document is
emitted by this tool from the algorithms in this folder, so the two cannot drift: when the prose and the
prototype disagree, suspect the prose.

```bash
dotnet run --project tools/relief          # → tools/relief/out/*.png + report.txt
dotnet run --project tools/relief -- <dir> # write the figures somewhere else
```

It is deliberately self-contained — one project, no database, no world, and only `PgmStudio.Geom` referenced,
for the ear-clip triangulation the "what a shape can state today" panel needs to be the real one rather than
an imitation of it. The solver here is the candidate for `Geom.Relief`; keeping it dependency-free is the
argument that it belongs in that leaf.

| File | Holds |
|---|---|
| `Relief.cs` | the footprint mask, the four mark kinds, the three fills, and the finishing (reach, grain, step, symmetry fold) |
| `Terrain.cs` | what a solved surface can be asked about (steps, walkable regions, scarps, symmetry) and the operations on it (erect, route, grade, fill depressions, flow, carve) |
| `Render.cs` | the two views a relief is judged in — topographic from above, blocks from an angle — plus the section and step-map diagnostics |
| `Png.cs` · `Text.cs` | a pixel buffer, a PNG writer and a 5×7 font, so the tool has no image dependency |
| `Program.cs` | the eight figures and the measurements printed beside them |

## The figures

| Figure | Answers |
|---|---|
| `01-today` | what an L-shaped room can already state about its height, and the same intent said as marks |
| `02-interpolators` | whether a fill reaches through a six-block slot it should not |
| `03-vocabulary` | the four kinds of mark on a 62×92 board |
| `04-knobs` | reach, grain, block step — and the stair repair the coarse step needs |
| `05-scale` | how much relief a 30×20 room takes before it stops being one piece of walkable ground |
| `06-erected` | a monolith, a mesa and a quarry composited onto a solved field, in blocks |
| `07-routes` | a drawn line against a routed one, grading on terraced ground, and a channel found by flow |
| `08-fairness` | a whole rot_180 map, and every cell that would differ from its mirror without the fold |

`report.txt` carries the measurements the pictures cannot state.
