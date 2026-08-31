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

public sealed class KitItem
{
    public int Slot;
    public string Material = "";
    public int Amount = 1;
    public int ItemDamage;
    public bool Unbreakable;
    public bool TeamColor;
    public string Enchantments = "";        // comma-joined "name:level"
}

public sealed class KitArmor
{
    public string SlotName = "";            // helmet | chestplate | leggings | boots
    public string Material = "";
    public bool Unbreakable;
    public bool TeamColor;
    public string Enchantments = "";
}

public sealed class Kit
{
    public string Id = "";
    public bool Force;                        // <kit force="true"> — re-applied every tick (reset kits)
    public bool Clear;                        // <clear/> — empties inventory and armour before the kit is given
    public List<KitItem> Items = [];
    public List<KitArmor> Armor = [];
    public List<KitEffect> Effects = [];      // <effect duration=… amplifier=…>type</effect>
}

public sealed class KitEffect
{
    public string Type = "";        // potion effect, e.g. "damage resistance"
    public string Duration = "";    // "oo" (infinite), "0", or a number of ticks/seconds
    public int Amplifier;
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
    /// <c>CtwStandards</c>).
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

    // Standard CTW boilerplate (added to generated maps at export; see CtwStandards). Not round-tripped
    // from corpus maps, so these stay empty for parsed maps.
    public List<string> ItemKeep = [];        // materials kept on death
    public List<string> ItemRemove = [];      // materials removed on death (team-coloured armor)
    public List<string> ToolRepair = [];      // tool/weapon materials auto-repaired
    public List<KillReward> KillRewards = []; // items granted per kill
    public string? HungerDepletion;           // null = no <hunger>; "off"/"on" → <hunger><depletion>…</depletion></hunger>

    /// <summary>This map's gamemodes, derived from its objective modules — see
    /// <see cref="Domain.Gamemodes"/>, which owns the rule.</summary>
    public IReadOnlyList<string> Gamemodes => Domain.Gamemodes.From(
        Wools.Count > 0, Destroyables.Any(d => d.IsObjective), Cores.Count > 0);
}
