using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/cards/{name} — one library card, drawn now if no file holds it yet. A card is named by what
/// draws it, so it never changes under its name and is answered with a year's cache; a name no list has offered
/// is a 404.</summary>
public sealed class LibraryCardEndpoint(LibraryCardSource source) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/cards/{name}");
        Description(b => b.Produces(200, typeof(byte[]), "image/png", "image/svg+xml").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var name = Route<string>("name") ?? "";
        var (known, picture) = LibraryCards.IsName(name) ? await LibraryCards.FindAsync(name, ct) : (false, null);
        if (!known && LibraryCards.IsName(name) && LibraryCards.ClaimRelist())
        {
            foreach (var (_, cards) in await source.AllAsync(ct))
                foreach (var card in cards) LibraryCards.Offer(card);
            (_, picture) = await LibraryCards.FindAsync(name, ct);
        }
        if (picture is null)
        {
            await Refusals.NotFoundAsync(HttpContext, "card", ct, name);
            return;
        }
        HttpContext.Response.ContentType = LibraryCards.ContentTypeOf(picture);
        HttpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        await HttpContext.Response.Body.WriteAsync(picture, ct);
    }
}

/// <summary>GET /api/library — every library's count and the cards of its newest three rows, which is what its
/// front page shows; a library's rows are its own list's.</summary>
public sealed class LibraryCountsEndpoint(LibraryCardSource source) : EndpointWithoutRequest<List<LibraryCount>>
{
    public override void Configure() { Get("/library"); }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync([.. (await source.AllAsync(ct)).Select(library => new LibraryCount(
            library.List, library.Cards.Count,
            [.. library.Cards.Select(LibraryCards.Offer).Where(card => card.Length > 0).Take(3)]))], ct);
}
