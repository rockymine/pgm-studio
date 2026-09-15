# Shops and shopkeepers — buying things in the middle of a match

A shop is a menu a player opens by right-clicking an entity standing on the map, and buys things out of with
the items in their inventory. Nothing about it is an objective: PGM tags a map carrying one `shops` and the
tag is auxiliary, with no gamemode behind it, so a shop board is still played for whatever its wools, cores or
hills say. What a shop changes is the economy — what a player can hold at minute ten that they could not hold
at minute one — and on the fifteen bedwars boards whose every building block is bought, dropping one would
leave a map nobody can play.

The two elements are separate and both are top-level: `<shops>` holds the menus and `<shopkeepers>` the
entities that open them, joined by a shop id. This document is the contract — what PGM does, read from
`tc.oc.pgm.shops`, and what authors actually build, measured over both corpora.

## 1. One module, two elements

`ShopModule.parse` reads the document twice. First every `<shops>` container, flattened with
`XMLUtils.flattenElements(root, "shops")`, which takes the container's children whatever they are named and
recurses through nested containers — the same flatten every objective group gets, so attributes cascade from
an outer container to each leaf. Each leaf is a `Shop`: a required `id`, an optional `name`, and its
categories. Then every `<shopkeepers>` container the same way, each leaf a `ShopKeeper`.

The module returns **null when there are no shops at all**, so a document with keepers and no menus builds no
shop module and spawns nobody. That is not the same as a document with no shops *of its own*: `<include>`
resolution runs before the module parses, so a map can state only keepers and take every menu from a shared
fragment. Fifteen corpus maps do exactly that, all of them under `other/bedwars/`, all including
`4-team-bedwars` or `8-team-bedwars` from the server's includes directory.

A keeper naming a shop id nothing defines is an `InvalidXMLException` at load: *"No shop with id '…' could be
found"*. The check runs over the document **after** includes are spliced, which is why the fifteen load.

## 2. The menu: a shop, its categories, its icons

A `Shop` is an id, a display name defaulting to the id, and **at least one** `<category>` — PGM refuses a shop
with none. A `Category` is an id, an icon, a filter deciding who sees the tab, and **one to twenty-eight**
`<item>` children; both bounds are errors when broken.

Twenty-eight is not arbitrary. `ShopMenu` opens a six-row chest: row 0 is the category row, nine slots wide
and paginated seven at a time once a shop has more than nine categories; row 1 is a grey divider carrying one
green pane under the selected tab; and rows 2 to 5 hold the icons, seven to a row starting at column 1. Four
rows of seven is the cap.

**A category element is itself an item.** `parser.item(category).required()` reads the stack off the same
element the `id` sits on, so `<category id="blocks" name="`aBlocks" material="hard clay">` is both the tab and
the block drawn in the tab's slot. There is no separate name for a category: what the menu shows is the
icon's display name. Every item flag is set on that stack (`applyItemFlags`), so a category icon never shows
its enchantments or attributes however it is written.

**The icons are filtered per viewer, twice.** `getVisibleCategories` drops a tab whose filter denies the
player and `getVisibleIcons` drops an icon whose filter does; an empty tab draws a "no items" marker rather
than an error. 489 of the corpus's 907 icons carry a filter, which is how a shop sells an upgrade once.

## 3. What an icon costs

A `Payment` is a price, a currency material and a chat colour, and an icon carries a list of them. Every
payment in the list is taken, so several entries are a price in several currencies **at once** — not a choice
between them. PGM refuses two payments in one currency inside a purchasable, because the affordability check
walks the storage slots once per payment and would double-count.

Prices are stated two ways and `ShopModule.parsePayments` reads both: `<payment>` children where there are
any, and **otherwise the icon element itself**, whose own `price`/`currency` are parsed as one payment. 766 of
907 corpus icons take the second form and 105 the first; the other 36 state neither and are free.

`isFree` is "no payments, or none with a price above zero", and a free icon is affordable without looking at
the inventory at all.

A payment can also be a **stack** rather than a material: `parsePayment` reads an `<item>` child beside the
`price`, and refuses a payment with a price above zero that carries neither that child nor a `currency`. No
corpus payment uses it — 0 of the 156 `<payment>` elements in the two corpora — and the studio refuses a map
that does, on the rule §8 states for every unread child.

**`color` on an icon is the price's colour, not the item's.** The same element is read as an item and as a
payment, and the two both want `color` — but `KitParser.parseItemMeta` reads it only when the stack's meta is
`LeatherArmorMeta`, and `parsePayment` reads it always, as a `ChatColor` name defaulting to gold. A leather
icon stating a hex would fail the payment parse and one stating a colour name would fail the dye parse, so an
icon that loads has stated a payment colour. All 326 corpus icons that carry one state a colour name
(`yellow` 76, `dark green` 69, `white` 66, `red` 63, `aqua` 32, `blue` 19, `gray` 1) and none of them is
leather armour.

## 4. What buying one does

An icon with no action **is** its item: PGM wraps the stack in an `ItemKit` with an overflow warning and marks
the icon **stackable**, so shift-buying takes as many as a full stack holds — `maxStackSize / amountPerPurchase`
purchases in one payment. An icon that names an action is not stackable whatever the action does, because PGM
cannot know that running it twice means twice as much.

The action is one reference under two attribute names. `parser.action(MatchPlayer.class, icon, "action", "kit")`
takes whichever is present, `action` first; both resolve through `ActionParser.parseReference` against one
feature namespace; and `KitDefinition extends ActionDefinition<MatchPlayer>`, so a kit id resolves as an
action and an action id naming a kit is the same lookup. 388 corpus icons spell it `action` and 167 `kit`.

## 5. The keeper

A `<shopkeeper>` states the shop it opens, an optional label, an optional `mob`, and where it stands.
**PGM spawns the entity itself**, at match load, from the element — there is nothing in the world file for a
keeper, no NBT to write and no block to place. `ShopKeeper.spawn` loads the chunk, spawns the entity, names
it, sets a `SHOP_KEEPER` metadata value holding the shop id, keeps it loaded when no player is near, and
freezes it through the NMS hack. `ShopMatchModule` then cancels every `EntityDamageEvent` on it and, for the
vehicle forms, damage, entry, destruction and collision — so a keeper cannot be killed, pushed, ridden or
moved, and an observer clicking one is ignored.

The mob defaults to `Villager` (`XMLUtils.parseEntityTypeAttribute(shopkeeper, "mob", Villager.class)`), which
is what 222 of the corpus's 298 keepers take by saying nothing. Of the 76 that name one, 19 are a witch, 14
spell out `Villager`, 10 a blaze, 10 a snowman, 8 a mushroom cow, 6 a creeper, 5 a skeleton, 2 an iron golem
and one each a zombie and a spider. The label defaults to the shop's id in grey and is colourised, so
``name="`b`lItem Shop"`` is a blue bold sign over the entity's head.

**Where it stands is a PGM point provider**, `pointParser.parseSingle(shopkeeper, …)`, and the grammar is the
one every spawn uses. An element with no children is parsed as a point region and its own text is the vector;
an element with children is a container, and exactly one provider must come out of it or the parse fails. The
corpus writes three forms:

| form | count | example |
|---|---|---|
| coordinates as the element's text | 244 | `<shopkeeper yaw="90">4.5,7,88.5</shopkeeper>` |
| a `<point>` child | 42 | `<shopkeeper …><point yaw="135">2.5,6,-46.5</point></shopkeeper>` |
| a `<region id="…"/>` child | 12 | `<shopkeeper shop="balls-shop"><region id="lime-spawn-shop-1" yaw="135"/></shopkeeper>` |

**The facing rides on whichever element states it, and it descends.** `PointProviderAttributes` are parsed at
the keeper and inherited by the child, so a `yaw` on the keeper reaches a `<region>` inside it and one on the
child overrides it. 244 of 298 keepers state a yaw; 54 leave the entity facing wherever the provider puts it.
Every corpus keeper's coordinates are block centres — `x.5, y, z.5` — because the entity is spawned exactly
where the point says and a whole number puts half of it in the next block.

## 6. What authors build

Across both corpora, **39 map directories carry a shop or a keeper**: 24 declare `<shops>`, 38 declare
`<shopkeepers>`, and the fifteen bedwars boards that declare only keepers take their menus from an include.
Between them they hold **50 shops, 104 categories, 907 icons and 298 keepers**.

**A shop is usually one tab.** 32 of 50 shops have a single category; 7 have two, 4 have four, and the widest
— `ctw/mame_i_shrunk_the_pvpers`' item shop — has eight. The median category holds 8 icons and two categories
hit the 28-icon cap exactly.

**The economy is a dropped currency, not a score.** Of the currencies named, emerald leads at 225 icons,
then nether star (126), gold ingot (118), gold nugget (86), iron ingot (68), diamond (48), emerald block (46)
and redstone block (46). A long tail pays in tools — `iron pickaxe`, `stone axe`, `gold spade` — which is the
upgrade-ladder pattern: the icon costs the previous tier plus a coin, so buying a diamond pickaxe consumes the
gold one.

**Prices are small numbers.** 121 icons cost 1, then 8 (66), 4 (65), 3 (61), 30 (55) and 2 (53).

**Two shops per board is the shape**, one selling items and one selling team upgrades, and the second is where
the actions are: `<item material="anvil" name="`e`lProtection I" … action="add-protection" filter="protection=0"/>`
buys a permanent enchantment for the whole team, gated by a variable filter so it can be bought once.

**Keepers stand in pairs at the spawns.** Every corpus board with two shops puts one of each at each team's
spawn, flanking the point players arrive on — `dtcm/apple_smash` writes them as `-134.5,52,-7.5` "On Left"
and `-134.5,52,8.5` "On Right", both facing east into the room, with the red spawn between them.

## 7. The conventional shop, in one block

```xml
<shops>
  <shop id="item-shop" name="Items">
    <category id="blocks" name="`aBlocks" material="hard clay">
      <item material="wood" amount="32" price="1" currency="gold nugget"/>
      <item material="stained clay" team-color="true" amount="16" price="1" currency="gold nugget"/>
      <item material="golden apple" amount="1" price="2" currency="gold nugget"/>
    </category>
  </shop>
</shops>
<shopkeepers>
  <shopkeepers name="`b`lItem Shop" shop="item-shop">
    <shopkeeper yaw="-90">131.5,14,339.5</shopkeeper>
    <shopkeeper yaw="90">-142.5,14,333.5</shopkeeper>
  </shopkeepers>
</shopkeepers>
```

A menu of one tab, three things to buy in a currency players pick up off the ground, and one villager at each
spawn wearing the shop's name.

## 8. What the studio does with one

**It reads one, stores it and writes it back.** `MapParser.ParseShops` reads the catalogue into
`Domain.Shop`/`ShopCategory`/`ShopIcon`/`ShopPayment` and `ParseShopkeepers` reads the entities into
`Domain.Shopkeeper`; `XmlWriter` re-emits both blocks; `shop` and `shopkeeper` store them (`M0038`). The shop
id a keeper names is **carried rather than resolved**, because a map may take its menus from an include this
parser reads without splicing.

**One item shape, everywhere an item is written.** A kit item, a kit's armour piece, a category's icon and a
shop icon are all `ItemSpec` — material, amount, damage, name, lore, dye, enchantments and stored
enchantments, the unbreakable/team-colour/prevent-sharing/locked flags, a projectile and a consumable
reference, the hidden item-flag words, potion effects, attribute modifiers and the can-place-on/can-destroy
matchers. One reader (`MapParser.ParseItemSpec`), one writer (`XmlWriter.WriteItemSpec`), one stored object
(`kit_item.spec_json` and the icons inside `shop.categories_json`). A shop learning to spell `team-color`
differently from a kit is the failure that shape exists against.

**A shop the studio cannot fully read refuses the map** (`MapParser.EnsureShopsReadable`, and
`supported-maps.md` for the gate beside the other three). What refuses: a child inside a shop, category, icon
or payment outside the set PGM reads; and an item attribute PGM acts on that the reader does not carry — the
grenade behaviour, the modern `components` syntax, the legacy potion list, the class-picker tag, and the four
that turn an element into a written book, a player head, a firework or a banner. A keeper whose place is
neither coordinates nor one region reference refuses on the same rule, because the alternative is an entity
spawned at the origin.

Over both corpora, **35 of the 39 shop-carrying maps parse and survive both codecs** with every shop id,
category, icon and keeper unchanged — 45 shops, 99 categories, 877 icons and 272 keepers. The other four are
refused by gates that have nothing to do with shops: three carry a scorebox and one ships a 1.21 world.

## 9. And what it builds

An agent authors a board that sells things by adding an array to the intent it already posts to
`PUT /api/map/{slug}/intent`. There is no second endpoint and no new document:

```json
"shops": [
  {
    "id": "item-shop",
    "name": "Quartermaster",
    "keeper": { "name": "`b`lQuartermaster", "mob": "Villager" },
    "categories": [
      {
        "id": "blocks",
        "material": "hard clay",
        "name": "`aBuilding",
        "items": [
          { "material": "wood", "amount": 32, "payments": [{ "price": 1, "currency": "gold nugget" }] },
          { "material": "stained clay", "amount": 16, "teamColor": true,
            "payments": [{ "price": 1, "currency": "gold nugget" }] },
          { "material": "diamond pickaxe", "name": "`6Diamond Pickaxe",
            "payments": [{ "price": 1, "currency": "gold pickaxe" },
                         { "price": 8, "currency": "gold nugget", "color": "green" }] }
        ]
      }
    ]
  }
]
```

**A price is a list, because PGM takes every entry in it.** Two payments are a cost in two currencies at once
rather than a choice between them, which is the upgrade ladder the third icon above writes: the diamond
pickaxe costs the gold one plus eight nuggets, so buying it consumes the tier below. The intent holds the
same list `Domain.ShopIcon` reads back, and the writer decides the spelling — a single payment rides on the
icon element, several become `<payment>` children, because an element can only state one of each attribute.
An icon stating no payment is free, which is what an empty list already means to PGM.

**An icon can trigger an action instead of handing over its stack.** `"action"` is the feature id, under the
one name PGM resolves both of its spellings through, and the stack stays what the menu draws — an anvil
labelled *Protection I* that buys a team enchantment rather than an anvil. An icon with no action **is** its
stack and PGM marks it stackable; one that names an action is never stackable, because running it twice need
not mean twice as much. The studio authors no `<actions>` block, so an id here names something only an
`<include>` can define, and `SH1` refuses a board where nothing does.

**A keeper that names no place is derived, and one that names a place stands there.** With neither `at` nor
`region`, `ShopGenerator` puts one keeper per shop at **every team's spawn** — on the spawn's own floor,
beside the point players arrive on, and turned to face them. A team that has to reach a shop between lives
reaches it at its spawn; a plan-compiled intent carries no other place that is reliably indoors, reliably
level and reliably the team's own; and the corpus builds the same thing by hand on every board with more
than one keeper (§6). A keeper stating `at` — a block, moved to its centre — or `region` — an id the map
already holds — is one keeper standing there, because a shop building in the middle of a board is one shop
for everybody rather than a villager per spawn. A keeper carrying both is answered by `at`, which resolves on
its own where a region id has to be found. `yaw` is the facing either way, and on a derived keeper it
overrides the look back at the spawn point.

Three details make the derived position land where it should. Blocks are counted **from the block the spawn
point stands in**, so a keeper lands on a block centre and the two sides of the spawn are the same distance
out. Several shops **flank** the point, alternating sides two blocks out and stepping two further for each
pair, so a second menu stands opposite the first rather than inside it. And each keeper is **held inside the
spawn's room** — the stated footprint less the course its wall stands in, or the protection ground less that
wall and the clean ring a piece keeps outside it (`WX1`) — so a narrow hall pulls the villager in rather than
putting it through a wall. The minimum is one block: a keeper on the spawn point itself would be standing in
the players.

**Nothing is stamped.** A keeper is an entity PGM spawns from the element, so the slice writes no blocks, no
entity data and nothing the world build has to resolve — which is why it runs beside the objective generators
instead of waiting for the terrain the way a capture point's pad does. A shop board exports the moment the
intent is stored.

The catalogue is written as stated, under one rule: **nothing is written that cannot load.** PGM refuses a
shop with no category and a category with no icon, so a tab whose items all fall away is left out, a shop
left with no tab goes with it, and a shop dropped that way takes its keeper too. A menu that is not there is
better than one that fails the map at load.

## 10. Where the money comes from

A shop is priced in a material nobody starts with. **764 of the corpus's 907 icons are priced in something no
spawn kit carries** — emerald 225, nether star 126, gold ingot 118, gold nugget 86 — and a board that mints
none of it is a menu nobody can open an account with. A generated board has two item sources of its own, and
neither is one: the spawn kit `TeamsGenerator` writes, and the kill reward `MapStandards` derives from that
kit's own blocks.

**What mints it is a spawner.** Of the 429 corpus entries that yield one of the eight commonest shop
currencies, **374 are `<spawner>` items** — against 31 block-drop rules and 24 kill-rewards — and 17 of the 39
shop-carrying maps carry spawners. A `<spawner>` is a clock and a place: it drops its items into a region
every so often while a player stands in another, up to a cap on how many may lie uncollected. PGM spawns the
stack itself, so the studio writes no blocks for one.

An agent states them as a second array on the same intent:

```json
"spawners": [
  {
    "id": "mid-emeralds",
    "at": { "x": 0, "y": 12, "z": 0 },
    "reach": 5,
    "protect": 6,
    "delay": "30s",
    "maxEntities": 8,
    "drops": [{ "material": "emerald", "amount": 1 }]
  }
]
```

**Three regions are minted rather than authored**, because PGM's element names two of them by id and takes no
coordinates, and the third is a rule rather than an attribute. `SpawnerGenerator` writes a `point` on the
centre of the block `at` names — the drop lands exactly where the point says, and a whole number would put it
on a corner — a `cylinder` based there as the ground a player has to be standing on, and a `cuboid` around it
as the ground nobody may take. All three are named for the spawner, so regenerating replaces them and leaves
the wool rooms' own spawners, which share the document's one `<spawners>` list, exactly where they stand.

**The reach is local rather than the whole map.** 1,004 of the corpus's 1,335 stated player-regions resolve to
a region of the map's own — 374 rectangles, 271 cuboids, 162 cylinders, and the rest unions, spheres and
transforms — against 331 naming `everywhere`, an id defined in an include. So a generator runs while somebody
is standing by, which is also what stops one nobody visits burying its own ground in what nobody collected.
The cylinder is **radius 5, height 3**: the modal radius of the corpus's 162 cylindrical player-regions and
the modal height of the same set, and exactly what `ctw/mame_i_shrunk_the_pvpers` keeps its gold-nugget
generators at.

**The ground around the drop is kept.** A generator whose block can be mined out, or walled in so nobody can
reach the stack, is one any player can switch off — so the slice writes a cuboid around each drop and one
`<apply block="never">` over the union of them. The box is **six blocks a side and five tall**, centred on the
drop with both ends inside it: 72 dedicated protection boxes across 45 corpus maps cluster at two, four and
six a side and three to five tall, and six by five is the widest of those clusters and
`mame_i_shrunk_the_pvpers`'s exactly. A board holding that ground some other way states `"protect": 0` and
gets the generator without the rule.

The two rates a spawner takes when the board says nothing are the corpus's, measured over the 276 spawners
that drop one of those currencies. The delay is **`10s`**, their median, against a spread running from a
second to a minute. The cap is **5**, their mode — 54 state it, 44 state eight, and 40 state none at all. A
spawner with nothing to drop is left out rather than written as a generator that fires forever and hands over
nothing.

**The drop's height is the author's.** `at.y` is written verbatim, so the coordinate comes from a `column`
read of the ground it is meant to sit on — there is no way yet to say *on the ground here* and have the
export solve it, the way a capture point's pad is cut into whatever the world build found (`PG16`).

**And a spawner cannot yet state a filter.** `mame_i_shrunk_the_pvpers` states its two generators four
times each, on the same regions, with a shorter delay behind `after-30m`, `after-60m` and `after-90m` — a
rate that climbs as the match runs. 369 of the corpus's 1,432 spawners carry a `filter`, and it is a
reference to a `<filters>` feature the intent does not author, so a ladder like that is hand-written XML
(`PG15`).

**What is not built** is on the board. Each sentence becomes false when its task ships:

- **`TC9`** — the configure tool has no step for a shop; the API is the way in.
