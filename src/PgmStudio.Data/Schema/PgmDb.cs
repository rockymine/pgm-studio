using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.MySql;

namespace PgmStudio.Data.Schema;

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
    public ITable<DestroyableRow> Destroyables => this.GetTable<DestroyableRow>();
    public ITable<CoreRow> Cores => this.GetTable<CoreRow>();
    public ITable<ModeRow> Modes => this.GetTable<ModeRow>();
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
    public ITable<CoreCandidateRow> CoreCandidates => this.GetTable<CoreCandidateRow>();
    public ITable<SegmentRow> Segments => this.GetTable<SegmentRow>();
    public ITable<MapArtifactRow> Artifacts => this.GetTable<MapArtifactRow>();
    public ITable<SymmetryRow> Symmetries => this.GetTable<SymmetryRow>();
    public ITable<MinecraftPlayerRow> MinecraftPlayers => this.GetTable<MinecraftPlayerRow>();
    public ITable<PlanRow> Plans => this.GetTable<PlanRow>();
    public ITable<StyleRow> Styles => this.GetTable<StyleRow>();
    public ITable<ThemeRow> Themes => this.GetTable<ThemeRow>();
    public ITable<ThemeBucketRow> ThemeBuckets => this.GetTable<ThemeBucketRow>();
    public ITable<RoomStyleRow> RoomStyles => this.GetTable<RoomStyleRow>();
    public ITable<RoomStyleCourseRow> RoomStyleCourses => this.GetTable<RoomStyleCourseRow>();
    public ITable<RoofStyleRow> RoofStyles => this.GetTable<RoofStyleRow>();
    public ITable<RoofStyleCourseRow> RoofStyleCourses => this.GetTable<RoofStyleCourseRow>();
    public ITable<StoreyStyleRow> StoreyStyles => this.GetTable<StoreyStyleRow>();
    public ITable<StoreyStyleCourseRow> StoreyStyleCourses => this.GetTable<StoreyStyleCourseRow>();
    public ITable<PorchStyleRow> PorchStyles => this.GetTable<PorchStyleRow>();
    public ITable<RoomStyleStoreyRow> RoomStyleStoreys => this.GetTable<RoomStyleStoreyRow>();

    /// <summary>
    /// Run <paramref name="write"/> as one unit: either everything it writes lands or none of it does.
    ///
    /// <para>The studio replaces rather than patches — a document, a map's features and an artifact are each
    /// stored by deleting what is there and writing what was posted — so a fault between the two halves
    /// leaves a map with its old state gone and its new state half written, which no read can tell from a
    /// map that was authored that way.</para>
    ///
    /// <para><b>Nesting joins rather than fails.</b> A connection carries one transaction and
    /// <see cref="DataConnection.BeginTransactionAsync"/> throws on a second, but one writer legitimately
    /// calls another over the same connection — a finished sketch writes its feature rows and its artifacts
    /// through two of them — so the outermost call owns the boundary and an inner one runs inside it.</para>
    /// </summary>
    public async Task InOneWriteAsync(Func<Task> write, CancellationToken ct = default)
    {
        if (Transaction is not null) { await write(); return; }
        await using var tx = await BeginTransactionAsync(ct);
        await write();
        await tx.CommitAsync(ct);
    }

    /// <inheritdoc cref="InOneWriteAsync(Func{Task}, CancellationToken)"/>
    /// <remarks>For a write that answers something — the id a row was inserted under, or whether the row it
    /// meant to replace was there at all. The answer is produced inside the boundary and handed back after
    /// it closes, so a caller reads it without holding the transaction open to do so.</remarks>
    public async Task<T> InOneWriteAsync<T>(Func<Task<T>> write, CancellationToken ct = default)
    {
        if (Transaction is not null) return await write();
        await using var tx = await BeginTransactionAsync(ct);
        var answered = await write();
        await tx.CommitAsync(ct);
        return answered;
    }
}

/// <summary>Builds linq2db <see cref="DataOptions"/> for the MariaDB/MySqlConnector provider.</summary>
public static class PgmDataOptions
{
    public static DataOptions ForConnectionString(string connectionString) =>
        new DataOptions().UseMySql(connectionString, MySqlVersion.MariaDB10, MySqlProvider.MySqlConnector, o => o);
}
