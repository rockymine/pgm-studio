# Include resolution — reading the map as the server plays it

A PGM map pulls shared rules in with `<include id="…"/>`, and the fragment's body is not in the map
folder: PGM resolves it from `config.getIncludesDirectory()`, a directory that belongs to the server.
So a map repository is not a complete description of its maps, and a studio reading only the repository
is reading the map **as its author wrote it**, not as it is played.

That distinction is the whole of this document. Both readings are legitimate and the studio does both;
what matters is never confusing them, because one of them must not be written back out.

## 1. The library is configuration, because it is configuration in PGM

`IncludeLibrary.Open(directory)` reads a directory of `<id>.xml` fragments, and returns `null` when the
path is unset or missing — the default, and the state every consumer already handles. Nothing is
vendored into this repository: the fragments are another project's source, they change as that project
edits them, and a copy here would go stale silently. The reference library is
[OvercastCommunity/PublicMaps](https://github.com/OvercastCommunity/PublicMaps) `includes/`.

Resolution is **recursive**, because fragments include each other: `global` pulls in `sound-keys` and
`overcast-pvp`, and `kotf-comp` reaches `gapple-kill-reward` through `conquest-comp`. A cycle or a
missing id resolves to nothing rather than throwing — an unresolvable fragment leaves the document
exactly as it would be with no library at all.

One fragment is spliced into **every** map without any map naming it: `global`, which PGM adds at the
root (`MapIncludeProcessorImpl.getGlobalInclude`). It goes at the head, so a map's own declarations come
after it and can override its constants.

## 2. A resolved parse is for analysis and must never be exported

`MapParser.Parse(path)` reads the map as written. `MapParser.Parse(path, library)` splices first and
reads the map as played. The second is an **analysis** read, and re-exporting one would corrupt it: the
include references are still recorded and still emitted, so the output would carry the fragments'
content inline *and* reference the fragments again, and the server would apply everything twice.
`MapXml.ResolvedIncludes` is non-empty exactly on that read, which is how a caller can tell. Export
parses without a library, which is the default everywhere.

Splicing happens **after** the supported-range gates and **before** variant and constant resolution —
PGM's own order, and each boundary is load-bearing:

- **Gates first.** The unread-objective gate exists to stop a map's goal being lost in silence on
  round-trip. A module arriving from a fragment is not at risk of that, because the export re-emits the
  reference and the server resolves it again. Gating after the splice would reject **82 corpus maps**
  that today parse and re-export perfectly, on account of the `<score>` and `<flags>` that `bridge`,
  `touchdown`, `ffb` and `flag-battles` bring.
- **Variants and constants after.** A fragment declares its knobs as `<constants fallback="true">` and a
  map tunes it by declaring one itself, so both sets have to be in the document before substitution runs.

## 3. Repeated top-level blocks merge

A hand-written map has one `<filters>` and one `<regions>`; a resolved one has the fragments' blocks
beside its own. Reading only the first would silently drop whichever lost the race — the map's own,
whenever a fragment was spliced ahead of it, which is what `global` guarantees.

So `<filters>`, `<regions>` and `<kits>` are read from **every** block, accumulating into one registry
the way PGM merges them, with apply rules concatenated in document order because PGM stops at the first
rule that decides. (`<wools>`, `<modes>`, `<destroyables>` and `<cores>` already flattened across
repeated groups.) The invariant this buys is checkable and checked: **resolving a map can only ever
increase its counts.** A decrease means content was dropped, and that is exactly the bug the first
implementation had.

## 4. What resolution is worth

`--resolve-includes <dir>` parses every corpus map twice and reports the difference, which is the
honest measure: **367 of 563 maps change**. The shape of the change is filters above all (345 maps),
then regions and fill actions (68 each), apply rules (55) and kits (28).

Two negative results are worth as much as the positive ones, and both correct a plausible expectation:

- **No map gains a gamemode.** 82 maps take their objective from a fragment, but `<score>` and
  `<flags>` have no parser here, so splicing them in changes nothing that is read. Resolution is not
  what closes that gap; a parser for those modules is.
- **No water-lane verdict changes.** The corpus reports the same 19 lanes across the same 15 maps
  either way (`--water-lanes --includes-dir <dir>`), because the include form's signal is the reference
  and the region, not the behaviour behind them.

What resolution genuinely buys is rule-level fidelity for an imported map: ad_astra reads as 22 filters
and 11 apply rules as written, and 95 filters and 17 apply rules as played, and only the second answers
what a player may do where. Analysis that reasons about rules should resolve; analysis that reasons
about geometry — the islands, the layout generator, the seed corpus — reads what maps declare
themselves and does not need to.

Every id the corpus references resolves against the reference library, so the unresolved-include warning
(`MapValidity`) is what it was designed to be: a report that *this* run had no library, not a permanent
gap.
