# relief — the interior-elevation prototype

The live twin of `docs/world-export/relief.md`. Every figure and every number in that document is
emitted by this tool from the algorithms in this folder, so the two cannot drift: when the prose and the
prototype disagree, suspect the prose.

```bash
dotnet run --project tools/relief                        # → tools/relief/out/*.png + report.txt
dotnet run --project tools/relief -- <dir>               # write the figures somewhere else
dotnet run --project tools/relief -- --measure <region>  # the same readback over one built world
dotnet run --project tools/relief -- --corpus  <mapsDir> # …and over every world under a directory
```

The two reading modes are what keep the model honest. `--corpus` over the community destroy-the-monument
maps is where that document's §12 table comes from, and it says the boards this design produces are at the
flat, open extreme of what actually gets shipped.

It is deliberately thin — one project and no database. **The solver is not here.** It is
`PgmStudio.Geom.Relief`, the shipped one, and this tool draws its figures by calling it: the argument that a
dependency-free solver belongs in the `Geom` leaf was made and won, and a second copy living here would only
be a fork that drifts. It did drift, in the direction that matters least and is hardest to notice — the fork
never gained the guard that discards a warm-start resume which has not settled, so it reported a resumed solve
1614 cells off the settled answer where the shipped solver is exact.

`PgmStudio.Minecraft` is referenced so the readback can be run over built worlds.

A third fill choice lived here too, comparing straight-line and on-land weighting against the relaxation. The
comparison is settled and its measurement is written down — `docs/world-export/relief.md` §3, with the numbers
— so the knob and its figure are gone rather than kept as a fork's worth of code to redraw a closed
decision.

| File | Holds |
|---|---|
| `Terrain.cs` | what a solved surface can be asked about (passability tiers, reachable places, scarps, fords, detours, symmetry) and the operations on it (erect, route, grade, stair, fill depressions, flow, carve, fold) |
| `Render.cs` | the two views a relief is judged in — topographic from above, blocks from an angle — plus the section and step-map diagnostics |
| `Png.cs` · `Text.cs` | a pixel buffer, a PNG writer and a 5×7 font, so the tool has no image dependency |
| `Measure.cs` | the ground surface of a built world, read out of Anvil and handed to the same analysis — how the readback gets calibrated against maps people shipped |
| `Program.cs` | the ten figures, the measurements printed beside them, and the two reading modes |

## The figures

| Figure | Answers |
|---|---|
| `01-today` | what a 50×44 L-shaped room can already state about its height, and the same intent said as marks |
| `03-vocabulary` | the five kinds of mark on a 90×135 board |
| `04-knobs` | reach, grain, block step — and the stair repair the coarse step needs |
| `05-scale` | what a 45×30 room costs a player at four amplitudes of relief |
| `06-erected` | a monolith, a mesa and a quarry composited onto a solved field, in blocks |
| `07-routes` | a drawn line against a routed one, grading on terraced ground, and a channel found by flow |
| `08-scarp` | the same ten-block drop at three grades, and what each one lets through |
| `09-map` | a whole 192×128 rot_180 map designed with the vocabulary, in plan and in blocks, and the same design lifted to the corpus median |
| `10-island` | solved per shape against solved per island, and a compound held against excluded |

`report.txt` carries the measurements the pictures cannot state.
