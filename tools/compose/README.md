# tools/compose

Three gates over the composer. All are file-based .NET 10 scripts that reference
`PgmStudio.Pgm` directly (via the `#:project` directive); run them from the **repo
root**. Each answers a question a running app cannot: whether the composer still
produces what it produced yesterday.

```sh
dotnet run tools/compose/fingerprints.cs
dotnet run tools/compose/unit-fingerprint.cs
dotnet run tools/compose/reproduction-gate.cs
```

Rendering a composed board to look at it is not one of those questions — the studio
draws them. `GET /shapes/catalog` returns every card with its SVG, `/shapes/probe`
renders a single emission, and the compose page shows as many boards as anyone asks
for. A page checked in beside those is a second copy, free to disagree with the app it
describes.

## `fingerprints.cs`

Records what the current composer makes of a fixed set of requests into
`composer-fingerprints.json`, which **is** committed. Run it after deliberately changing
the pipeline *and* bumping `ComposerVersion.Current`: the file is meant to be
regenerated, not defended, and the version is what says a move was intended.

## `unit-fingerprint.cs`

Hashes allocate→fill over every symmetry × preset × seed — box rects, hub form, flip,
per-wool fill, every joint and its offer, the spawn facing, then every emitted piece.
The check a refactor runs against: a pure structure pass must leave the total
bit-identical, and a behavioural change should move exactly the presets it claims to.

## `reproduction-gate.cs`

Composes a board sweep and checks every board reads back as producible — no box the
emitters cannot reproduce, no unit rule violated.

## Two traps

`out/` is ignored (see `.gitignore`); generated artifacts never land in a tracked path.

`dotnet run <script>.cs` caches the compiled app keyed on the **script's** content, not
on the referenced `PgmStudio.Pgm` sources — so re-running an unchanged script after a
composer edit reports the old numbers, silently. `rm -rf
~/.local/share/dotnet/runfile/<script>-*` before trusting a measurement. Building
`src/PgmStudio.Pgm` alone does **not** invalidate that cache.

None of these are in `PgmStudio.slnx`, so `dotnet build` at the root does not compile
them and a rename in `src/` can break one silently — `tools/build-scripts.sh` builds
every script in `tools/` and is what catches that (`B227`).
