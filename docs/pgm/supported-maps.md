# What the studio will read, and what it refuses

A map the studio cannot fully read is refused rather than partly parsed, because the failure mode of the
alternative is silent: a map that loads, exports and plays without the thing that was dropped. Three gates
decide it, all in `MapParser.EnsureSupported`, all raising `UnsupportedMapException`. `--scan-out-all`
skips-and-logs them; everything else treats a refusal as a refusal.

**Proto ≥ 1.4.0.** The studio is built on PGM's **id-based** regions, filters and kits — a registry keyed by
id, referenced by name from everywhere else. The older positional format states the same things inline and
anonymously, which is not a dialect of the same model but a different one, and supporting both would double
every registry path for maps nobody is still authoring. A map with no parseable `proto` at all is refused on
the same rule. In the corpus this excludes `kytriak_te` (proto 1.3.0, anonymous teams).

**No modern worlds.** A map declaring `min-server-version >= 1.13.0` ships a post-"flattening" world, whose
blocks are a per-section palette rather than the numeric id and data nibble the Anvil reader decodes. The XML
would parse; the world would not, and every derivation that reads terrain would answer about an empty map. In
the corpus this excludes `allure` (1.21.10).

**No unread objective module.** This is the gate that matters most, and it is not about the format at all.
PGM's objective modules are the ones contributing a **non-auxiliary `Gamemode` MapTag**: `wools` (CTW),
`destroyables` (DTM), `cores` (DTC), `control-points`, `king`, `payloads`, `flags` (CTF) and `score` (TDM).
`MapParser` reads the tags it names rather than enumerating the root, so a module it does not parse would
vanish on round-trip — the map would export cleanly and be unplayable, missing the only thing it is played
for. Its presence therefore refuses the map. Auxiliary modules (`blitz`, `ffa`, `rage`) modify play rather
than the goal and are not objectives, so they gate nothing. When a parser lands, its tag joins
`ParsedObjectiveModules` and its maps become readable. In the corpus this excludes `3084` and `lost_haven`
(both `control-points`).

**The gates read the map's own body, before any include is spliced**, and that ordering is load-bearing. A
module arriving from an `<include>` is not at risk of being lost: the export re-emits the reference and the
server resolves it again (`include-resolution.md`). Gating after the splice would reject 82 corpus maps that
today parse and re-export perfectly.

Of the 350-slug corpus, four maps are excluded by these gates and the rest parse.

**A refusal is the answer, not a problem to work around.** The standing rule is that a malformed or
out-of-range map is rejected rather than accommodated by weakening the schema — the schema is what makes
every downstream derivation able to assume what it assumes.
