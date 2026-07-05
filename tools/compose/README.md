# tools/compose

Throwaway-but-maintained dev drivers for the composer. Both are file-based .NET 10
scripts that reference `PgmStudio.Pgm` directly (via the `#:project` directive) and
depend on `Composer.ComposeStages` / `Composer.Compose`.

Run them from the **repo root**:

```sh
dotnet run tools/compose/matrix.cs
dotnet run tools/compose/gallery-gen.cs
```

Builds on the shared-folder checkout are slow on a cold cache (allow several minutes
for the first run).

## matrix.cs

Composes a fixed grid of `(players, teams, symmetry, seed)` cases and prints a
per-case verification matrix: piece count, zone count, stone count, error-severity
lint findings, closure-hole count, and whether a mid cut was applied. Ends with a
`composed=/threw=` tally. Emits nothing to disk — stdout only.

## gallery-gen.cs

Renders a curated set of composed plans to a single self-contained HTML review
gallery. Each plan is fanned into its full-map symmetry orbit and drawn as an inline
SVG in the plan editor's visual language, grouped into families with per-card stats.
Writes `tools/compose/out/composer-review.html` (git-ignored) and prints the card
count plus any unexpected compose failures.

`out/` is ignored (see `.gitignore`); generated artifacts never land in a tracked path.

## Build-cache gotcha (important)

`dotnet run <script>.cs` caches the compiled app keyed on the **script's** content, not on
the referenced `PgmStudio.Pgm` sources. So if you change only the composer and re-run an
**unchanged** script, you get a **stale render against the old DLL** — silently. Symptom: the
output bytes don't change after a composer edit. Fix: bust the script's cache by touching it
(edit any line — e.g. the `build-cache bust:` comment at the top of `gallery-gen.cs`) or run a
freshly-named throwaway script. `dotnet build src/PgmStudio.Pgm` alone does **not** invalidate
the run-file cache.
