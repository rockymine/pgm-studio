using PgmStudio.Geom;

namespace PgmStudio.Domain;

public sealed class Team
{
    public string Id = "";
    public string Color = "";
    public int MaxPlayers;
    public int MinPlayers;
    public string Name = "";
    public string DyeColor = "";
}

public sealed class Author
{
    public string Uuid = "";
    public string Role = "author";          // "author" | "contributor"
    public string Contribution = "";
    public string Name = "";                // resolved Mojang username — display cache; uuid is canonical
}

/// <summary>
/// One item stack as PGM spells it — the statement <c>KitParser.parseItem</c> reads off an element, wherever
/// that element sits. A kit item is this plus a slot, a kit's armour piece is this plus a slot name, and a
/// shop icon is this plus a price: the stack itself is one shape and one reader
/// (<c>MapParser.ParseItemSpec</c>), because a second copy of it is how a shop learns to spell
/// <c>team-color</c> differently from a kit.
/// <para><c>Material</c> is the only required field. Everything else is unset by default and is written back
/// only when the map stated it.</para>
/// </summary>
public sealed class ItemSpec
{
    public string Material = "";
    public int Amount = 1;
    public int Damage;
    /// <summary>The display name, colour codes and all (PGM colourises <c>`</c> escapes).</summary>
    public string Name = "";
    /// <summary>The lore, exactly as authored: PGM splits it on <c>|</c> into lines, so the separator is
    /// part of the value rather than a list this reader has to rebuild.</summary>
    public string Lore = "";
    /// <summary>Leather-armour dye, as the hex PGM parses. Only leather carries one — on any other material
    /// PGM never reads the attribute, which is what lets a shop icon use <c>color</c> for its price
    /// (<see cref="ShopPayment.Color"/>).</summary>
    public string Color = "";
    public string Enchantments = "";        // comma-joined "name:level"
    /// <summary>The enchantments stored <em>in</em> the item rather than applied to it — an enchanted book's
    /// contents, spelled <c>stored-enchantment</c>. Same encoding as <see cref="Enchantments"/>.</summary>
    public string StoredEnchantments = "";
    public bool Unbreakable;
    public bool TeamColor;
    /// <summary>Whether the item cannot be dropped or handed to another player.</summary>
    public bool PreventSharing;
    /// <summary>Whether the item is fixed in its slot.</summary>
    public bool Locked;
    /// <summary>A <c>&lt;projectiles&gt;</c> feature id — an element the studio does not parse, kept so the
    /// reference round-trips.</summary>
    public string Projectile = "";
    /// <summary>A <c>&lt;consumables&gt;</c> feature id, on the same terms as <see cref="Projectile"/>.</summary>
    public string Consumable = "";
    /// <summary>The item-flag words the map hid, each written <c>show-&lt;word&gt;="false"</c>:
    /// <c>attributes</c>, <c>enchantments</c>, <c>unbreakable</c>, <c>can-destroy</c>, <c>can-place-on</c>,
    /// <c>other</c>. PGM's own default is to show every one, so an empty list is a map that hid nothing.</summary>
    public List<string> Hidden = [];
    /// <summary>The potion effects the stack carries — a brewed potion's contents rather than anything the
    /// holder gains for holding it.</summary>
    public List<PotionEffect> Effects = [];
    public List<ItemAttribute> Attributes = [];
    /// <summary>What the item may be placed on, as PGM's material matcher spells it: a material name per
    /// entry, or the single word <c>all-blocks</c> for <c>&lt;all-blocks/&gt;</c>. Empty = no restriction.</summary>
    public List<string> CanPlaceOn = [];
    /// <summary>What the item may break, on the same terms as <see cref="CanPlaceOn"/>.</summary>
    public List<string> CanDestroy = [];
}

/// <summary>A potion effect, as <c>&lt;effect duration=… amplifier=…&gt;type&lt;/effect&gt;</c>. One shape
/// for both subjects PGM applies it to: a kit grants one to the player, an item carries one in the bottle.</summary>
public sealed class PotionEffect
{
    public string Type = "";        // potion effect, e.g. "damage resistance"
    public string Duration = "";    // "oo" (infinite), "0", or a number of ticks/seconds
    public int Amplifier;
}

/// <summary>An attribute modifier on an item — <c>&lt;attribute operation="add"
/// amount="0.01"&gt;generic.movementSpeed&lt;/attribute&gt;</c>.</summary>
public sealed class ItemAttribute
{
    public string Attribute = "";   // the modified attribute, e.g. "generic.movementSpeed"
    public string Operation = "";   // add | base | multiply; "" = PGM's add
    public double Amount;
}

/// <summary>One item of a kit: the stack, and the inventory slot it is put in.</summary>
public sealed class KitItem
{
    public int Slot;
    public ItemSpec Item = new();
}

/// <summary>One armour piece of a kit: the stack, and which of the four slots wears it.</summary>
public sealed class KitArmor
{
    public string SlotName = "";            // helmet | chestplate | leggings | boots
    public ItemSpec Item = new();
}

public sealed class Kit
{
    public string Id = "";
    public bool Force;                        // <kit force="true"> — re-applied every tick (reset kits)
    public bool Clear;                        // <clear/> — empties inventory and armour before the kit is given
    public List<KitItem> Items = [];
    public List<KitArmor> Armor = [];
    public List<PotionEffect> Effects = [];   // <effect duration=… amplifier=…>type</effect>
}

public sealed class Spawn
{
    public string Team = "";
    public string Kit = "";
    public double Yaw;
    public Region? Region;
}

public sealed class Wool
{
    public string Team = "";
    public string Color = "";
    public Vec3 Location;
    public Vec3 Monument;
    public string? MonumentRegionId;
    public string? WoolRoomRegion;
}

/// <summary>
/// What a destroyable that is not an objective is really doing. Authors borrow the element to script the
/// world, because it is the only one that carries a <see cref="ObjectiveMode"/>.
/// </summary>
public enum PhantomKind
{
    /// <summary>Not a phantom — a real objective.</summary>
    None,
    /// <summary>A timed block-swap: a mode replaces its blocks at a match time. The common case is the
    /// pre-game build floor, erased at 0s, but the target is not always air (water lanes, a wool disco
    /// floor).</summary>
    BlockSwap,
    /// <summary>A trigger: breaking it fires a filter. No mode, so nothing swaps.</summary>
    Trigger,
}

/// <summary>
/// A DTM objective: the blocks matching <see cref="Materials"/> inside <see cref="RegionId"/>, owned by one
/// team and broken by every other. Called a destroyable, never a monument — "monument" is the CTW wool
/// monument throughout this codebase. The region is a loose box drawn <i>around</i> the structure, so it
/// legitimately holds mostly air; the goal is the matching blocks within it, not the box.
/// <para>Not every destroyable is an objective — see <see cref="IsObjective"/>.</para>
/// </summary>
public sealed class Destroyable
{
    public string Id = "";              // XML id; generated on parse when unauthored, so refs always resolve
    public string Name = "";            // required by PGM
    public string Owner = "";           // the DEFENDING team (XML attr `owner`)
    public string RegionId = "";
    public string Materials = "";       // ';'-separated match patterns, each `name[:data]`
    public double? Completion;          // null = 1.0 (the whole structure); a fraction, not a percentage
    public bool Show = true;            // false ⇒ not an objective at all but a scripted block-swap region
    public bool ModeChanges;            // true = every mode applies; mutually exclusive with Modes
    public List<string>? Modes;         // an explicit mode set; null = none (or all, when ModeChanges)

    /// <summary>Whether the owning team may put a broken block back. <b>PGM's default is true</b> and this
    /// mirrors it, so an imported map reads as the map it is; what the studio <em>authors</em> is
    /// <see cref="ObjectiveDefaults.Repairable"/>'s answer, which is not the same question.
    /// <para>A core has no such attribute at all — PGM cancels only the owner <em>breaking</em> its own
    /// casing, and a block placed back into the casing region passes every check — so on a core the mode
    /// ladder is the only pressure there is.</para></summary>
    public bool Repairable = true;

    /// <summary>
    /// Whether this is a goal at all. The test is exact and semantic rather than heuristic: <b>a goal
    /// players cannot see is not a goal</b>. Authors reach for the destroyable element to script the world
    /// — it is the only one that carries a mode — and hide the result with <c>show="false"</c>. Neither
    /// <c>completion="0%"</c> nor <c>required="false"</c> identifies these: most non-required destroyables
    /// are genuine, and one real objective completes at 50% while crumbling to air.
    /// <para>Never present a non-objective as one: it is a marker, not a monument. It is still load-bearing
    /// and must not be dropped — lose a build-floor phantom and its glass is never erased, so the map keeps
    /// a solid bridge between the teams and plays wrong, which is worse than missing a goal.</para>
    /// </summary>
    public bool IsObjective => Show;

    /// <summary>What this destroyable is doing when it is not an objective.</summary>
    public PhantomKind Phantom =>
        Show ? PhantomKind.None
        : ModeChanges || Modes is { Count: > 0 } ? PhantomKind.BlockSwap
        : PhantomKind.Trigger;
}

/// <summary>
/// A DTC objective: a casing of <see cref="Material"/> enclosing lava, owned by one team and breached by
/// every other. It leaks when a lava block falls to <c>Y ≤ region.min.y − leak</c> within ±15 blocks
/// horizontally of the core — so the core floats, and <see cref="Leak"/> only means anything against the
/// height it floats at: players must dig <c>max(0, leak − float)</c> blocks into the terrain below it.
/// <para>The XML spells the owning attribute <c>team</c> rather than <c>owner</c>, a PGM inconsistency
/// with a standing TODO in their source. We mirror the XML and call the field Owner.</para>
/// </summary>
public sealed class Core
{
    public string Id = "";              // XML id; generated on parse when unauthored
    public string Name = "";            // optional — PGM auto-names per team: "Core", "Core 2", …
    public string Owner = "";           // the DEFENDING team (XML attr `team`)
    public string RegionId = "";
    public string Material = "";        // empty = obsidian, which is effectively universal anyway
    public int? Leak;                   // null = 5
    public bool ModeChanges;
    public List<string>? Modes;
}

/// <summary>Which of PGM's two spellings a control point is written as. The element is not a different
/// objective — one parser builds both — but it chooses the defaults PGM applies to every attribute the
/// point leaves unset, and it is what the export re-emits.</summary>
public enum ControlPointElement
{
    /// <summary><c>&lt;control-points&gt;&lt;control-point/&gt;</c> — CP. Progress snaps back the moment
    /// the last player steps off, and the point has no neutral state.</summary>
    ControlPoints,
    /// <summary><c>&lt;king&gt;&lt;hills&gt;&lt;hill/&gt;</c> — KotH. Partial progress is kept, the point
    /// passes through a neutral state between owners, and capture progress is shown.</summary>
    King,
}

/// <summary>
/// A CP/KotH objective: a region a team owns by standing in it, paying out score for as long as it holds.
/// Every knob below is the author's or unset — <c>null</c> and <c>""</c> mean "PGM's default for this
/// element", which is not the same value for a hill and a point and is therefore never materialised here.
/// <c>docs/pgm/control-points.md</c> carries the default table and the state machine they drive.
/// <para>Only <see cref="CaptureRegionId"/> decides play. The two display regions are cosmetic: PGM
/// enumerates their colour-affected blocks once at match load and recolours them to the owning team's dye,
/// drawing capture progress across the progress region as a pie.</para>
/// </summary>
public sealed class ControlPoint
{
    public string Id = "";                      // XML id; generated on parse when unauthored
    public string Name = "";                    // "" lets PGM auto-name: "Hill", "Hill 2", …
    public ControlPointElement Element;

    /// <summary>Where a player has to stand. Required by PGM, which tests the block a player's feet are in
    /// against the region at that block's centre.</summary>
    public string CaptureRegionId = "";
    /// <summary>Blocks showing capture progress as a pie. Optional, and must be block-bounded.</summary>
    public string ProgressRegionId = "";
    /// <summary>Blocks showing the owner. Optional, block-bounded, and PGM subtracts the progress region
    /// from it — a block belongs to at most one of the two.</summary>
    public string OwnerRegionId = "";
    /// <summary>A filter narrowing which blocks of the display regions are recoloured. "" = every
    /// colour-affected material, which is PGM's default and what almost every map takes.</summary>
    public string VisualMaterialsFilterId = "";

    public string InitialOwner = "";            // "" = unowned at match start
    public string CaptureTime = "";             // a duration; "" = PGM's 30s
    public string CaptureRule = "";             // exclusive | majority | lead; "" = exclusive
    public string CaptureFilterId = "";         // which teams may own it
    public string PlayerFilterId = "";          // which players count toward the lead

    /// <summary>Shorthand for <see cref="Recovery"/> and <see cref="Decay"/> together; PGM refuses it
    /// alongside either.</summary>
    public bool? Incremental;
    public double? Recovery;                    // the owner pushing progress back down
    public double? Decay;                       // progress bleeding away with nobody on the point
    public double? OwnedDecay;                  // an owned point drifting back to neutral
    public double? Contested;                   // progress bleeding away while contested

    public double? TimeMultiplier;              // how much faster a crowd captures
    public bool? NeutralState;                  // whether the point passes through unowned between owners
    public bool Permanent;                      // PGM's default is false; written only when true

    public double? Points;                      // per second, to the owner
    public double? OwnerPoints;                 // one-off, on capture
    public double? PointsGrowth;                // seconds per doubling of Points

    public bool? ShowProgress;
    /// <summary>Whether owning this point wins the match outright. <b>PGM's default is true</b> at every
    /// proto the studio reads, so <c>null</c> is a goal that ends the match on capture — which is why the
    /// corpus writes <c>required="false"</c> on all but a handful of points.</summary>
    public bool? Required;
    /// <summary>PGM's default is true; <c>false</c> clears every show option, and one of those is
    /// <c>stats</c>, without which <c>GoalMatchModule</c> never registers the point as a goal at all.</summary>
    public bool Show = true;
}

/// <summary>
/// The <c>&lt;score&gt;</c> module: the match-level score configuration a control point pays into. Its
/// presence is what makes scoring happen at all — <c>ControlPoint.tickScore</c> looks up
/// <c>ScoreMatchModule</c>, and PGM builds none for a document with no <c>&lt;score&gt;</c> element, so a
/// map whose every point names a <c>points</c> rate and declares no score scores nothing for the whole
/// match. Every field is the author's or unset.
/// </summary>
public sealed class ScoreConfig
{
    public int? Initial;
    /// <summary>The score that ends the match, and the ending a KotH map actually uses: only a handful of
    /// corpus KotH maps set a <c>&lt;time&gt;</c> limit instead.</summary>
    public int? Limit;
    public bool? EnforceLimit;
    public int? Kills;
    public int? Deaths;
    /// <summary>The mercy rule's threshold and its floor — <c>&lt;mercy min="…"&gt;n&lt;/mercy&gt;</c>.</summary>
    public int? Mercy;
    public int? MercyMin;
    public string Display = "";                 // "" = numerical
    public string ScoreboardFilterId = "";
    /// <summary>The legacy <c>&lt;king/&gt;</c> marker, which zeroed the default kill and death scores.
    /// Those already default to zero at proto 1.3.6 and above, so on every map the studio reads it changes
    /// nothing and is kept only so it round-trips.</summary>
    public bool King;
}

/// <summary>
/// A shop: a menu a player opens by right-clicking a <see cref="Shopkeeper"/>, holding one or more
/// categories of things to buy. Nothing about it is geometry and nothing about it is an objective — PGM tags
/// a map carrying one <c>shops</c> and plays it exactly as it would otherwise — but a map whose blocks are
/// all bought loses everything if a shop is dropped, which is why the studio reads one rather than skipping
/// it. <c>docs/pgm/shops.md</c> is the contract.
/// <para>At least one category is required by PGM, and every shop is referenced by id: a
/// <c>&lt;shopkeeper&gt;</c> names one and so does an <c>&lt;open-shop&gt;</c> action.</para>
/// </summary>
public sealed class Shop
{
    public string Id = "";
    /// <summary>The menu's title. "" lets PGM fall back to the id, which is what it shows.</summary>
    public string Name = "";
    public List<ShopCategory> Categories = [];
}

/// <summary>
/// One tab of a shop: the icon that selects it, the filter deciding who sees it, and up to 28 things to buy
/// (<c>Category.MAX_ICONS</c>).
/// <para>The category element <b>is</b> its own icon — PGM reads the item off the same element the id sits
/// on — so the tab has no name of its own beyond the icon's display name.</para>
/// </summary>
public sealed class ShopCategory
{
    public string Id = "";
    /// <summary>The stack drawn in the menu's category row. Required by PGM.</summary>
    public ItemSpec Icon = new();
    /// <summary>Who sees this tab. "" = everyone, which is PGM's default.</summary>
    public string FilterId = "";
    public List<ShopIcon> Icons = [];
}

/// <summary>
/// One thing to buy: the stack shown in the menu, what it costs, who may see it, and what buying it does.
/// <para><see cref="ActionId"/> is empty for the ordinary case, and that is not a missing value: an icon
/// with no action is a <b>simple item</b>, which PGM hands over as an item kit and marks <em>stackable</em>
/// so it can be bought a full stack at a time. An icon that names an action is not stackable, whatever it
/// does.</para>
/// </summary>
public sealed class ShopIcon
{
    /// <summary>The stack the menu draws — and, for an icon with no <see cref="ActionId"/>, the stack the
    /// buyer receives.</summary>
    public ItemSpec Item = new();

    /// <summary>What it costs. Every payment is taken, so several entries are a price in several currencies
    /// at once; PGM refuses two payments in the same currency. Empty, or every price zero, is free.</summary>
    public List<ShopPayment> Payments = [];

    /// <summary>Who may see and buy it. "" = everyone.</summary>
    public string FilterId = "";

    /// <summary>What buying it does, as a feature id — an action or a kit, which PGM resolves through one
    /// lookup: <c>parser.action(icon, "action", "kit")</c> takes either attribute, ids share one namespace,
    /// and <c>KitDefinition</c> <i>is</i> an <c>ActionDefinition</c>. So the two attributes are one
    /// reference under two spellings and this is the one field for it.</summary>
    public string ActionId = "";
}

/// <summary>A price in one currency: how many of <see cref="Currency"/> the buyer pays.</summary>
public sealed class ShopPayment
{
    public int Price;
    /// <summary>The material paid in. Empty is legal only at a zero price, which is free.</summary>
    public string Currency = "";
    /// <summary>The chat colour the price is drawn in. "" = PGM's gold.
    /// <para>On a shop icon <c>color</c> is <b>this</b> rather than the item's leather dye
    /// (<see cref="ItemSpec.Color"/>), and not by choice: PGM parses the icon element as both an item and a
    /// payment, reads the dye only when the stack is leather armour, and reads the payment colour as a
    /// <c>ChatColor</c> name always — so a leather icon stating a hex would fail to load as a payment and
    /// one stating a colour name would fail to load as a dye. All 326 corpus icons that state it state a
    /// colour name.</para></summary>
    public string Color = "";
}

/// <summary>
/// A shopkeeper: the entity standing on the map that a player right-clicks to open a <see cref="Shop"/>.
/// <b>The studio writes no blocks and no entity data for one</b> — PGM spawns it itself at match load, from
/// this element, and freezes it: the keeper takes no damage, cannot be pushed, moved or entered, and is
/// removed with the match. So the whole of a keeper is the XML.
/// <para>Where it stands is a PGM <i>point provider</i>, which the corpus writes two ways: coordinates as
/// the element's own text (<see cref="Location"/>), or a reference to a region to stand in
/// (<see cref="RegionId"/>). <see cref="Yaw"/> is which way it faces and rides on the keeper either way,
/// because PGM's point attributes descend from the element to the region inside it.</para>
/// </summary>
public sealed class Shopkeeper
{
    /// <summary>The shop this keeper opens, by id. <b>Not resolved here</b>: a map may take its shops from an
    /// <c>&lt;include&gt;</c> the studio reads without splicing (15 corpus maps do), so a keeper naming a shop
    /// this document does not hold is a complete map rather than a broken one.</summary>
    public string ShopId = "";

    /// <summary>The name floating over it. "" lets PGM label it with the shop's id in grey.</summary>
    public string Name = "";

    /// <summary>The entity to spawn, as a Bukkit entity type. "" = PGM's villager, which is what 222 of the
    /// corpus's 298 keepers take.</summary>
    public string Mob = "";

    /// <summary>Where it stands, when the keeper states coordinates. Null when it names a region instead.</summary>
    public Vec3? Location;

    /// <summary>The region it stands in, when the keeper names one instead of coordinates.</summary>
    public string RegionId = "";

    /// <summary>Which way it faces, in degrees. Null = PGM's own default, which is not zero but whatever the
    /// point provider resolves to, so it is never materialised here.</summary>
    public double? Yaw;
}

/// <summary>
/// A scheduled change to an objective's material at a match time. Declarative — no world or structure
/// impact — but it is what makes a <c>show="false"</c> destroyable a timed block-swap rather than a goal.
/// </summary>
public sealed class ObjectiveMode
{
    public string Id = "";              // generated on parse when unauthored
    public string Name = "";            // may carry `-prefixed colour codes
    public string After = "";           // a duration; required by PGM
    public string Material = "";        // the swap target; empty when the mode carries an action instead
    public string ShowBefore = "";      // countdown lead-in; empty = PGM's 60s default
    public string FilterId = "";
    public string ActionId = "";        // refs an <actions> feature we do not parse; kept so it round-trips
}

public sealed class SpawnerItem
{
    public string Material = "";
    public int Damage;
    public int Amount = 1;
}

public sealed class WoolSpawner
{
    public string SpawnRegion = "";
    public string PlayerRegion = "";
    public string Delay = "";
    public int? MaxEntities;
    public List<SpawnerItem> Items = [];
}

public sealed class ApplyRule
{
    public string EnterFilter = "";
    public string LeaveFilter = "";
    public string BlockFilter = "";
    public string BlockPlaceFilter = "";
    public string BlockBreakFilter = "";
    public string BlockPhysicsFilter = "";
    public string BlockPlaceAgainstFilter = "";
    public string UseFilter = "";
    public string FilterId = "";
    public string RegionId = "";
    public string Kit = "";
    public string LendKit = "";
    public string Velocity = "";
    public string Message = "";
}

public sealed class Renewable
{
    public string RegionId = "";
    public double Rate = 1.0;
    public string RenewFilter = "";
    public string ReplaceFilter = "";
    public bool Grow;
    public int? AvoidPlayers;
}

public sealed class BlockDropItem
{
    public string Material = "";
    public int Damage;
    public int Amount = 1;
    public double Chance = 1.0;
}

public sealed class BlockDropRule
{
    public string RegionId = "";
    public string FilterId = "";
    /// <summary>Inline <c>&lt;filter&gt;</c> as an any-of-materials match (e.g. the spawn-kit blocks). When
    /// non-empty it's emitted inline instead of the <see cref="FilterId"/> reference.</summary>
    public List<string> FilterMaterials = [];
    public string Replacement = "";
    public bool WrongTool;
    public List<BlockDropItem> Items = [];
}

/// <summary>
/// A <c>&lt;fill&gt;</c> action — writing <see cref="Material"/> over every block of <see cref="RegionId"/>
/// when the action fires. Read-only: the studio parses the region/material pair so an imported map's scripted
/// world changes are visible, but authors nothing through it and does not round-trip the enclosing
/// <c>&lt;actions&gt;</c> tree (the trigger, the filter, the event map).
/// <para>What makes it worth reading at all is that a fill over a <c>y=0</c> region is the whole of a
/// second-generation water lane: the columns stop being void the instant the fill lands, so a gap that was
/// unbridgeable becomes buildable (see <c>docs/pgm/water-lanes.md</c>).</para>
/// </summary>
public sealed class FillAction
{
    public string Id = "";
    public string RegionId = "";
    public string Material = "";

    /// <summary>The fill's own <c>filter</c> — a guard on which blocks it may overwrite (<c>only-air</c> keeps
    /// a fill off terrain). It says nothing about when the fill runs.</summary>
    public string FilterId = "";

    /// <summary>When the fill runs, as the map states it: the <c>filter</c> of the <c>&lt;trigger&gt;</c> that
    /// fires it, or the <c>duration</c> of an inline <c>&lt;after&gt;</c>. Empty when the trigger states the
    /// condition in a form this parser does not read — an unknown time is left blank rather than filled in
    /// with the block guard, which is a different question.</summary>
    public string Trigger = "";
}

/// <summary>A <c>&lt;kill-reward&gt;</c> — items granted to a player for a kill.</summary>
public sealed class KillReward
{
    public List<KillRewardItem> Items = [];
}

public sealed class KillRewardItem
{
    public string Material = "";
    public int Amount = 1;
    public int Damage;
    public bool TeamColor;
}

/// <summary>The parsed PGM map — the flat parser domain (mirrors datatypes.MapXml).</summary>
public sealed class MapXml
{
    public string Name = "";
    public string Version = "";

    /// <summary>
    /// The map's own <c>&lt;gamemode&gt;</c> elements verbatim, in document order — empty when the map
    /// declares none, which is the common case. PGM parses this as a <b>repeated</b> element and never reads
    /// it to decide which modules run, so it is <b>not</b> the gamemode; <see cref="Gamemodes"/> is. Kept
    /// because it is the author's own word and sometimes says what no module can (a CTW map labelled
    /// <c>ad</c> is played attack/defend), and because it should round-trip. Never default it: inventing
    /// "ctw" for a map that declared nothing is a guess that reads as a fact. It is still validated on the
    /// way in by PGM's own closed enum (<see cref="Gamemodes.IsKnownId"/>) — an id outside that set is not
    /// merely an odd label, it is a map that fails to load.
    /// </summary>
    public List<string> DeclaredGamemode = [];
    public string Objective = "";

    /// <summary>When the map was made, as <c>yyyy-mm-dd</c>. The author's own statement — a map that does not
    /// say carries the empty string and writes no element, since a date nobody stated is a date nobody
    /// knows.</summary>
    public string Created = "";

    /// <summary>How finished the map is: <c>development</c> or <c>production</c>. Round-tripped verbatim;
    /// what the studio writes onto a map it authors is <c>MetaGenerator.Phase</c>.</summary>
    public string Phase = "";
    public int? MaxBuildHeight;
    public List<Author> Authors = [];
    public List<Kit> Kits = [];
    public List<Team> Teams = [];
    public List<Spawn> Spawns = [];
    public Spawn? ObserverSpawn;
    public List<Wool> Wools = [];
    public List<Destroyable> Destroyables = [];
    public List<Core> Cores = [];
    public List<ControlPoint> ControlPoints = [];

    /// <summary>The shops a player can buy from, and the keepers that open them. Two lists rather than one
    /// nesting the other, because PGM keeps them apart and so does the XML: a shop is referenced by id from
    /// a keeper, from an <c>&lt;open-shop&gt;</c> action, and from another map's <c>&lt;include&gt;</c> — and
    /// 15 corpus maps state keepers whose shops arrive from outside the document altogether.</summary>
    public List<Shop> Shops = [];
    public List<Shopkeeper> Shopkeepers = [];

    /// <summary>The <c>&lt;score&gt;</c> module, or <c>null</c> when the map declares none — which is a
    /// different map, not an empty configuration: with no element PGM loads no score module at all and
    /// nothing on the map can score.</summary>
    public ScoreConfig? Score;
    public List<ObjectiveMode> Modes = [];
    public List<WoolSpawner> Spawners = [];
    public List<Renewable> Renewables = [];
    public List<BlockDropRule> BlockDropRules = [];
    public Dictionary<string, Filter> Filters = new();
    public Dictionary<string, Region> Regions = new();
    public List<ApplyRule> ApplyRules = [];

    /// <summary>
    /// The <c>&lt;include id="…"/&gt;</c> ids the map pulls in, in document order. Both directions use this
    /// list: a parsed map records what it references, and a generated map states what it wants spliced (see
    /// <c>MapStandards</c>).
    /// <para>An id is all we hold. PGM resolves a fragment out of <c>config.getIncludesDirectory()</c> — a
    /// server directory that ships with neither the map nor the corpus — so the body is unavailable and the
    /// rules it defines never enter the document analysed here. <c>MapValidity</c> warns for exactly
    /// that reason; <c>docs/pgm/water-lanes.md</c> §3 covers the one id whose meaning is known without
    /// the body.</para>
    /// </summary>
    public List<string> Includes = [];

    /// <summary>The include ids actually resolved into this document, empty when the map was parsed without a
    /// library (the default) or when nothing referenced could be found. Non-empty marks an <b>analysis</b>
    /// read: the document describes the map as played and must not be re-exported, because the fragments'
    /// content is now inline while <see cref="Includes"/> still references them.</summary>
    public List<string> ResolvedIncludes = [];

    /// <summary>The <c>&lt;fill&gt;</c> actions the map scripts, flattened out of <c>&lt;actions&gt;</c>.
    /// Read-only (see <see cref="FillAction"/>) — parsed for analysis, never emitted.</summary>
    public List<FillAction> Fills = [];

    /// <summary>
    /// The map's <c>&lt;constant&gt;</c> declarations, id → value. Kept after substitution because a constant
    /// is not only a text macro: a shared fragment declares its knobs as <c>fallback</c> constants, and a map
    /// tunes the fragment by declaring one of them itself. So a constant the map never interpolates is still
    /// meaningful — it is the setting handed to a rule that lives outside the document.
    /// </summary>
    public Dictionary<string, string> Constants = new(StringComparer.Ordinal);

    // Standard boilerplate (added to generated maps at export; see MapStandards). Not round-tripped
    // from corpus maps, so these stay empty for parsed maps.
    public List<string> ItemKeep = [];        // materials kept on death
    public List<string> ItemRemove = [];      // materials removed on death (team-coloured armor)
    public List<string> ToolRepair = [];      // tool/weapon materials auto-repaired
    public List<KillReward> KillRewards = []; // items granted per kill
    public string? HungerDepletion;           // null = no <hunger>; "off"/"on" → <hunger><depletion>…</depletion></hunger>

    /// <summary>This map's gamemodes, derived from its objective modules — see
    /// <see cref="Domain.Gamemodes"/>, which owns the rule.</summary>
    public IReadOnlyList<string> Gamemodes => Domain.Gamemodes.From(
        hasWools: Wools.Count > 0,
        hasRealDestroyable: Destroyables.Any(d => d.IsObjective),
        hasCores: Cores.Count > 0,
        hasControlPoints: ControlPoints.Any(p => p.Element == ControlPointElement.ControlPoints),
        hasKing: ControlPoints.Any(p => p.Element == ControlPointElement.King),
        scoresKillsOrDeaths: Score is { } s && ((s.Kills ?? 0) != 0 || (s.Deaths ?? 0) != 0));
}
