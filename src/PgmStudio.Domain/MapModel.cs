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
    public List<KitItem> Items = [];
    public List<KitArmor> Armor = [];
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
    public string Gamemode = "ctw";
    public string Objective = "";
    public int? MaxBuildHeight;
    public List<Author> Authors = [];
    public List<Kit> Kits = [];
    public List<Team> Teams = [];
    public List<Spawn> Spawns = [];
    public Spawn? ObserverSpawn;
    public List<Wool> Wools = [];
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
}
