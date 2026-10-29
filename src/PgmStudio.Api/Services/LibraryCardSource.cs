using PgmStudio.Api.Endpoints;
using PgmStudio.Data.Theme;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>Every library's cards, each in the order its list answers them — newest first — and named by the
/// route that list answers at. Read by the library's front page, by <see cref="LibraryCardDrawing"/> and by a
/// card route asked for a name nothing holds; each list endpoint names its rows' cards through the same
/// <c>CardOf</c> it does.</summary>
public sealed class LibraryCardSource(ThemeStore styles, ThemeLibrary themes, RoomStyleLibrary houses,
    HousePartLibrary parts, PropStyleLibrary props, BlockTextureStore textures)
{
    public async Task<IReadOnlyList<(string List, IReadOnlyList<LibraryCard> Cards)>> AllAsync(CancellationToken ct)
    {
        var sprites = await textures.ForPicturesAsync(ct);
        return
        [
            ("styles", [.. (await styles.ListStylesAsync(null, ct)).Select(row => ThemeLibraryMapping.CardOf(row, sprites))]),
            ("themes", [.. (await themes.ComposeAllAsync(ct)).Select(entry => ThemeLibraryMapping.CardOf(entry.Theme, sprites))]),
            ("biome-patterns", [.. (await styles.ListBiomesAsync(null, ct)).Select(row => BiomeLibraryMapping.CardOf(row.Params, sprites))]),
            ("room-styles", [.. (await houses.ComposeAllAsync(ct)).Select(entry => RoomStylePreview.CardOf(entry.Style, sprites))]),
            ("roof-styles", [.. (await parts.ComposeRoofsAsync(ct)).Select(entry => RoomStylePreview.CardOf(entry.Style, sprites, RoomParts.Roof))]),
            ("storey-styles", [.. (await parts.ComposeStoreysAsync(ct)).Select(entry => RoomStylePreview.CardOf(entry.Style, sprites, RoomParts.Wall))]),
            ("porch-styles", [.. (await parts.ComposePorchesAsync(ct)).Select(entry => RoomStylePreview.CardOf(entry.Style, sprites))]),
            ("tree-styles", [.. (await props.ListTreesAsync(sprites, ct)).Select(entry => entry.Card)]),
            ("boulder-styles", [.. (await props.ListBouldersAsync(sprites, ct)).Select(entry => entry.Card)]),
        ];
    }
}

/// <summary>Draws every card a list holds for drawing, one at a time, so a deploy's new cards are kept before a
/// browser asks for most of them: every library is listed a little after start, and each card a list offers
/// later — a row saved or edited — is drawn when it arrives.</summary>
public sealed class LibraryCardDrawing(IServiceScopeFactory scopes, ILogger<LibraryCardDrawing> log) : BackgroundService
{
    /// <summary>How long after start the libraries are first listed, so the start's own work goes first.</summary>
    public static readonly TimeSpan AfterStart = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(AfterStart, stoppingToken);
            try
            {
                using var scope = scopes.CreateScope();
                foreach (var (_, cards) in await scope.ServiceProvider.GetRequiredService<LibraryCardSource>().AllAsync(stoppingToken))
                    foreach (var card in cards) LibraryCards.Offer(card);
            }
            catch (Exception failed) when (failed is not OperationCanceledException)
            {
                log.LogWarning(failed, "Library cards: the libraries did not list, so cards are drawn as they are asked for");
            }
            while (!stoppingToken.IsCancellationRequested)
            {
                var waiting = LibraryCards.Waiting;
                try
                {
                    foreach (var name in waiting) await LibraryCards.FindAsync(name, stoppingToken);
                    if (waiting.Count > 0) log.LogInformation("Library cards: {Count} kept ahead of being asked for", waiting.Count);
                }
                catch (Exception failed) when (failed is not OperationCanceledException)
                {
                    log.LogWarning(failed, "Library cards: drawing ahead stopped, and resumes when a list offers a card");
                }
                await LibraryCards.ArrivalAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
    }
}
