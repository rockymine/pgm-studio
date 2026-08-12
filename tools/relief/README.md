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
| `Terrain.cs` | what a solved surface can be asked about (passability tiers, reachable places, scarps, fords, detours, symmetry) and the operations on it (erect, route, grade, stair, fill depressions, flow, carve, fold) |
| `Render.cs` | the two views a relief is judged in — topographic from above, blocks from an angle — plus the section and step-map diagnostics |
| `Png.cs` · `Text.cs` | a pixel buffer, a PNG writer and a 5×7 font, so the tool has no image dependency |
| `Program.cs` | the eight figures and the measurements printed beside them |

## The figures

| Figure | Answers |
|---|---|
| `01-today` | what a 50×44 L-shaped room can already state about its height, and the same intent said as marks |
| `02-interpolators` | whether a fill reaches through an eight-block slot it should not |
| `03-vocabulary` | the five kinds of mark on a 90×135 board |
| `04-knobs` | reach, grain, block step — and the stair repair the coarse step needs |
| `05-scale` | what a 45×30 room costs a player at four amplitudes of relief |
| `06-erected` | a monolith, a mesa and a quarry composited onto a solved field, in blocks |
| `07-routes` | a drawn line against a routed one, grading on terraced ground, and a channel found by flow |
| `08-scarp` | the same ten-block drop at three grades, and what each one lets through |
| `09-map` | a whole 192×128 rot_180 map designed with the vocabulary, in plan and in blocks |

`report.txt` carries the measurements the pictures cannot state.
