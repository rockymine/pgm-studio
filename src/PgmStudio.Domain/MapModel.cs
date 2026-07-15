namespace PgmStudio.Domain;

/// <summary>A 3D point with finite components (wool/monument locations).</summary>
public readonly record struct Vec3(double X, double Y, double Z);

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
    /// The <c>&lt;gamemode&gt;</c> element verbatim, or empty when the map declares none — which is the
    /// common case. It is a free-text label PGM never reads to decide anything, so it is <b>not</b> the
    /// gamemode; <see cref="Gamemodes"/> is. Kept because it is the author's own word and sometimes says
    /// what no module can (a CTW map labelled <c>ad</c> is played attack/defend), and because it should
    /// round-trip. Never default it: inventing "ctw" for a map that declared nothing is a guess that reads
    /// as a fact.
    /// </summary>
    public string DeclaredGamemode = "";
    public string Objective = "";
    public int? MaxBuildHeight;
    public List<Author> Authors = [];
    public List<Kit> Kits = [];
    public List<Team> Teams = [];
    public List<Spawn> Spawns = [];
    public Spawn? ObserverSpawn;
    public List<Wool> Wools = [];
    public List<Destroyable> Destroyables = [];
    public List<ObjectiveMode> Modes = [];
    public List<WoolSpawner> Spawners = [];
    public List<Renewable> Renewables = [];
    public List<BlockDropRule> BlockDropRules = [];
    public Dictionary<string, Filter> Filters = new();
    public Dictionary<string, Region> Regions = new();
    public List<ApplyRule> ApplyRules = [];

    // Standard CTW boilerplate (added to generated maps at export; see CtwStandards). Not round-tripped
    // from corpus maps, so these stay empty for parsed maps.
    public List<string> Includes = [];        // <include id="…"/> — shared server-defined snippets
    public List<string> ItemKeep = [];        // materials kept on death
    public List<string> ItemRemove = [];      // materials removed on death (team-coloured armor)
    public List<string> ToolRepair = [];      // tool/weapon materials auto-repaired
    public List<KillReward> KillRewards = []; // items granted per kill
    public string? HungerDepletion;           // null = no <hunger>; "off"/"on" → <hunger><depletion>…</depletion></hunger>

    /// <summary>
    /// The map's gamemodes, derived from which objective modules it carries. This is the gamemode — PGM
    /// never reads <c>&lt;gamemode&gt;</c> to decide it; each module contributes a tag when it parses, and
    /// the mode falls out of which ones did.
    /// <para>It is a <b>set</b>, not a scalar: CTW, DTM and DTC coexist, and both corpora keep a
    /// <c>mixed/</c> directory for the maps that prove it. Nothing may assume a map has exactly one.</para>
    /// <para>One deliberate deviation from PGM: a module contributes only if it holds a <b>real</b>
    /// objective. PGM tags a map DTM the moment <c>DestroyableModule</c> parses anything, but a map whose
    /// every destroyable is a phantom is not DTM whatever PGM's tag says — those maps are pure CTW that
    /// happen to script their build floor with the destroyable element.</para>
    /// </summary>
    public IReadOnlyList<string> Gamemodes
    {
        get
        {
            var modes = new List<string>();
            if (Wools.Count > 0) modes.Add("ctw");
            if (Destroyables.Any(d => d.IsObjective)) modes.Add("dtm");
            return modes;
        }
    }
}
