using System.Collections.Concurrent;
using System.Text;

namespace PgmStudio.Api.Services;

/// <summary>A library row's card: the picture's name in <see cref="Drawings"/>, which is known before anything
/// is drawn, how the client shows it, and the drawing itself. <paramref name="Draw"/> answers a PNG, or SVG text
/// where the studio has no sprites, or null where the row's document draws nothing.</summary>
public sealed record LibraryCard(string Name, string Look, Func<byte[]?> Draw)
{
    /// <summary>A grid of block sprites, shown cell for cell.</summary>
    public const string Sprites = "block-picture";

    /// <summary>A rendered view of a volume, smoothed like a photograph.</summary>
    public const string Rendered = "block-render";
}

/// <summary>
/// Where every library card is served from. A list carries each row's card as an <c>&lt;img&gt;</c> naming
/// <c>GET /api/cards/{name}</c> (<see cref="Offer"/>), so listing a library draws nothing and a browser asks
/// only for the cards it shows. A card not yet kept is held here until it is drawn, by whichever comes first:
/// the browser asking for it, or <see cref="LibraryCardDrawing"/> drawing every held card in the background.
///
/// <para>A card is named by what draws it, so its picture never changes under its name and is answered with a
/// year's cache. Drawing takes one of <see cref="Slots"/> at a time, so a page of cold cards cannot take every
/// core from the requests beside it.</para>
/// </summary>
public static class LibraryCards
{
    /// <summary>How many cards are drawn at once: half the cores, and at least one.</summary>
    public static readonly int Slots = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>How long a card nobody holds waits before every library is listed again to find it.</summary>
    public static readonly TimeSpan RelistAfter = TimeSpan.FromSeconds(30);

    private static readonly ConcurrentDictionary<string, LibraryCard> Held = new();
    private static readonly SemaphoreSlim Drawing = new(Slots, Slots);
    private static readonly SemaphoreSlim Arrived = new(0, 1);
    private static long _relisted;

    /// <summary>The markup a list carries for <paramref name="card"/>: an image of its route, or nothing for a
    /// card already found to draw nothing. A card not yet kept is held for drawing.</summary>
    public static string Offer(LibraryCard card)
    {
        if (Drawings.SizeOf(card.Name) is { } size)
        {
            if (size == 0) return "";
        }
        else if (Held.TryAdd(card.Name, card) && Arrived.CurrentCount == 0)
        {
            try { Arrived.Release(); }
            catch (SemaphoreFullException) { }
        }
        return $"<img class=\"{card.Look}\" alt=\"\" loading=\"lazy\" decoding=\"async\" src=\"/api/cards/{card.Name}\">";
    }

    /// <summary>The picture kept under <paramref name="name"/>, drawn now if it is held and not yet kept.
    /// <c>Known</c> is false for a name no list has offered since this process started and no file holds;
    /// a known card that draws nothing answers a null picture.</summary>
    public static async Task<(bool Known, byte[]? Picture)> FindAsync(string name, CancellationToken ct)
    {
        if (Drawings.TryFind(name, out var kept)) return (true, kept);
        if (!Held.TryGetValue(name, out var card)) return (false, null);
        await Drawing.WaitAsync(ct);
        try
        {
            var picture = Drawings.Of(name, () => DrawOrNothing(card));
            if (Drawings.SizeOf(name) is not null) Held.TryRemove(name, out _);
            return (true, picture);
        }
        finally
        {
            Drawing.Release();
        }
    }

    /// <summary>The names held for drawing, at this moment.</summary>
    public static IReadOnlyList<string> Waiting => [.. Held.Keys];

    /// <summary>Waits until a list offers a card that is not kept.</summary>
    public static Task ArrivalAsync(CancellationToken ct) => Arrived.WaitAsync(ct);

    /// <summary>Whether every library should be listed again to find a card nobody holds: true at most once
    /// per <see cref="RelistAfter"/>, so asking for names that are not cards costs one listing, not one each.</summary>
    public static bool ClaimRelist()
    {
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _relisted);
        return (last == 0 || now - last >= (long)RelistAfter.TotalMilliseconds)
               && Interlocked.CompareExchange(ref _relisted, now, last) == last;
    }

    /// <summary>The content type a kept card is served as.</summary>
    public static string ContentTypeOf(ReadOnlySpan<byte> picture) =>
        picture.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47]) ? "image/png" : "image/svg+xml";

    /// <summary>Whether <paramref name="name"/> is shaped like a card's name: 64 lowercase hex digits.</summary>
    public static bool IsName(string? name) =>
        name is { Length: 64 } && name.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>SVG text as the bytes a card keeps.</summary>
    public static byte[]? Svg(string svg) => svg.Length == 0 ? null : Encoding.UTF8.GetBytes(svg);

    /// <summary>A row's stored document is hand-editable and can fail to draw in more ways than a parse error;
    /// such a row lists without a picture rather than failing its library.</summary>
    private static byte[]? DrawOrNothing(LibraryCard card)
    {
        try { return card.Draw(); }
        catch (Exception unreadable) when (unreadable is not OutOfMemoryException) { return null; }
    }
}
