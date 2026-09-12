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

An agent authors a board that sells things by adding one array to the intent it already posts to
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
          { "material": "wood", "amount": 32, "price": 1, "currency": "gold nugget" },
          { "material": "stained clay", "amount": 16, "price": 1, "currency": "gold nugget", "teamColor": true },
          { "material": "golden apple", "name": "`6Golden Apple", "price": 2, "currency": "gold nugget" }
        ]
      }
    ]
  }
]
```

**The keeper carries no position, and that is the whole of the placement rule.** `ShopGenerator` puts one
keeper per shop at **every team's spawn** — on the spawn's own floor, beside the point players arrive on, and
turned to face them. A team that has to reach a shop between lives reaches it at its spawn; a plan-compiled
intent carries no other place that is reliably indoors, reliably level and reliably the team's own; and the
corpus builds the same thing by hand on every board with more than one keeper (§6).

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

The catalogue is written as stated, with two rules of its own. A shop with **no category** is left out rather
than written as a menu PGM refuses to load. And an item that states neither a price nor a currency is **free**,
written with no payment at all, because that is what an empty payment list already means to PGM.

**What is not built** is on the board in `BACKLOG.md`. Each sentence becomes false when its task ships:

- **`PG11`** — a keeper can only stand at a spawn. A shop building in the middle of a board, or a keeper at a
  wool room, has no way to be stated.
- **`PG12`** — an icon that costs two currencies at once, or that triggers an action or a kit instead of
  handing over its stack, is read and re-emitted but cannot be authored.
- **`PG13`** — a keeper naming a shop no document holds is a map PGM refuses at load unless an include
  provides it, and nothing says so.
- **`PG14`** — a studio-authored board mints no currency. The spawn kit and the kill reward derived from it
  are the only item sources a generated board has, so the only thing a shop can be priced in is the kit's own
  wood.
- **`TC9`** — the configure tool has no step for a shop; the API is the way in.
