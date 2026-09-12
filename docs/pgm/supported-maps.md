# What the studio will read, and what it refuses

A map the studio cannot fully read is refused rather than partly parsed, because the failure mode of the
alternative is silent: a map that loads, exports and plays without the thing that was dropped. Four gates
decide it, three in `MapParser.EnsureSupported` and one in `MapParser.EnsureShopsReadable`, all raising
`UnsupportedMapException`. `--scan-out-all` skips-and-logs them; everything else treats a refusal as a
refusal.

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
`destroyables` (DTM), `cores` (DTC), `control-points` and `king` (CP/KotH), `score` (TDM), `payloads` and
`flags` (CTF). `MapParser` reads the tags it names rather than enumerating the root, so a module it does not
parse would vanish on round-trip — the map would export cleanly and be unplayable, missing the only thing it
is played for. Its presence therefore refuses the map. Auxiliary modules (`blitz`, `ffa`, `rage`) modify play
rather than the goal and are not objectives, so they gate nothing. When a parser lands, its tag joins
`ParsedObjectiveModules` and its maps become readable.

Read today: `wools`, `destroyables`, `cores`, `control-points`, `king` and `score`. `control-points` and
`king` are one PGM module under two spellings and arrived together; `control-points.md` is what it reads.
**Refused: `flags`, `payloads`, and a `<score>` carrying a `<box>`.** The last is the one exception to the
gate reading root elements only: a scorebox is its own objective inside an element that *is* read, so
skipping that check would export a scorebox map without its boxes. A payload is a furnace minecart players
push around the board, and none of the geometry describing one is read.

**No unreadable shop.** `shops` is not an objective module at all — its map tag is auxiliary and carries no
gamemode, so a board keeps its goal whether or not the menu survives. What a fourth gate exists for is the
fifteen bedwars boards whose every building block is bought: an icon dropped in silence there is a map nobody
can play. `MapParser.EnsureShopsReadable` therefore refuses a map whose shop states something this reader
cannot carry — a child inside a shop, category, icon or payment outside the set PGM reads, or an item
attribute PGM acts on that is not carried (the grenade behaviour, the modern `components` syntax, the legacy
potion list, the class-picker tag, and the four that turn an element into a written book, a player head, a
firework or a banner). A `<shopkeeper>` whose place is neither coordinates nor one region reference refuses on
the same rule, because the alternative is an entity spawned at the origin. Unlike the three gates above, this
one reads the document **after** variants and constants are resolved, because the corpus writes whole
categories inside an `<if variant>`. `shops.md` is what it reads. No corpus map is refused by it.

Swept over every `map.xml` in both corpora — 1,622 directories, which is more than the 350-slug set the
round-trip harness runs because it counts the nested category folders too — **1,329 parse**. The 293 refused
are 146 for `flags`, 75 for a scorebox, 37 below the proto floor, 27 for a modern world and 9 for
`payloads`. Of the maps that carry a control point or a score module, all 286 also survive the XML round trip
with every point and every knob unchanged. Of the 39 that carry a shop or a keeper, 35 parse — the other four
are refused by the gates above, three for a scorebox and one for a 1.21 world — and all 35 survive both
codecs with every shop id, category, icon and keeper intact.

**The three module gates read the map's own body, before any include is spliced**, and that ordering is
load-bearing. A module arriving from an `<include>` is not at risk of being lost: the export re-emits the
reference and the server resolves it again (`include-resolution.md`). Gating after the splice would reject 82
corpus maps that today parse and re-export perfectly. The shop gate is the exception and reads what the
document holds at the point the shops are parsed, spliced fragments included — it judges content rather than
presence, and content that arrived from a fragment is content this reader would carry or drop just the same.

**A refusal is the answer, not a problem to work around.** The standing rule is that a malformed or
out-of-range map is rejected rather than accommodated by weakening the schema — the schema is what makes
every downstream derivation able to assume what it assumes.
