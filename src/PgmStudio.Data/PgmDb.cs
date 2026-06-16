using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.MySql;

namespace PgmStudio.Data;

/// <summary>The linq2db data connection over the MariaDB schema (MySqlConnector provider).</summary>
public sealed class PgmDb : DataConnection
{
    public PgmDb(DataOptions options) : base(options) { }

    public ITable<MapRow> Maps => this.GetTable<MapRow>();
    public ITable<AuthorRow> Authors => this.GetTable<AuthorRow>();
    public ITable<TeamRow> Teams => this.GetTable<TeamRow>();
    public ITable<KitRow> Kits => this.GetTable<KitRow>();
    public ITable<KitItemRow> KitItems => this.GetTable<KitItemRow>();
    public ITable<KitArmorRow> KitArmor => this.GetTable<KitArmorRow>();
    public ITable<RegionRow> Regions => this.GetTable<RegionRow>();
    public ITable<FilterRow> Filters => this.GetTable<FilterRow>();
    public ITable<WoolRow> Wools => this.GetTable<WoolRow>();
    public ITable<MonumentRow> Monuments => this.GetTable<MonumentRow>();
    public ITable<SpawnRow> Spawns => this.GetTable<SpawnRow>();
    public ITable<MapSpawnerRow> MapSpawners => this.GetTable<MapSpawnerRow>();
    public ITable<RenewableRow> Renewables => this.GetTable<RenewableRow>();
    public ITable<BlockDropRuleRow> BlockDropRules => this.GetTable<BlockDropRuleRow>();
    public ITable<ApplyRuleRow> ApplyRules => this.GetTable<ApplyRuleRow>();
    public ITable<WoolBlockRow> WoolBlocks => this.GetTable<WoolBlockRow>();
    public ITable<ResourceBlockRow> ResourceBlocks => this.GetTable<ResourceBlockRow>();
    public ITable<ChestItemRow> ChestItems => this.GetTable<ChestItemRow>();
    public ITable<SpawnerBlockRow> SpawnerBlocks => this.GetTable<SpawnerBlockRow>();
    public ITable<MonumentCandidateRow> MonumentCandidates => this.GetTable<MonumentCandidateRow>();
    public ITable<LayerSegmentRow> LayerSegments => this.GetTable<LayerSegmentRow>();
    public ITable<MapArtifactRow> Artifacts => this.GetTable<MapArtifactRow>();
}

/// <summary>Builds linq2db <see cref="DataOptions"/> for the MariaDB/MySqlConnector provider.</summary>
public static class PgmDataOptions
{
    public static DataOptions ForConnectionString(string connectionString) =>
        new DataOptions().UseMySql(connectionString, MySqlVersion.MariaDB10, MySqlProvider.MySqlConnector, o => o);
}
