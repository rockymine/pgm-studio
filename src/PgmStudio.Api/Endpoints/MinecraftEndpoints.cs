using PgmStudio.Contracts;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Render;

using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// GET /api/minecraft/player?name=|uuid= — resolve a Minecraft username or UUID to {uuid, name}. The editor
/// uses it to turn a typed username into the canonical uuid a <c>map.xml</c> stores (and to resolve a stored
/// uuid back to a name).
///
/// <para>404 means <b>no account is called that</b>, which is not the same as an error: PGM takes a person as
/// an account or as a pseudonym, so the editor keeps the typed name and stores it as the second kind. A name
/// that is not shaped like an account, and a host that could not be reached, both answer the same way and for
/// the same reason — the credits stand either way.</para>
/// </summary>
public sealed class PlayerLookupEndpoint(PlayerLookup players) : EndpointWithoutRequest<PlayerDto>
{
    public override void Configure()
    {
        Get("/minecraft/player");
        Description(b => b.Refuses(404).Reads(
            new QueryWord("uuid", "The account to look up, by its uuid. It wins over `name` where both are given."),
            new QueryWord("name", "The account to look up, by its username. One of the two is required.")));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var uuid = HttpContext.Request.Query["uuid"].ToString().Trim();
        var name = HttpContext.Request.Query["name"].ToString().Trim();
        var query = !string.IsNullOrEmpty(uuid) ? uuid : name;
        if (string.IsNullOrEmpty(query))
        {
            await Refusals.UnreadableAsync(HttpContext, "no player named",
                "a lookup takes either a name or a uuid, and neither was given", ct);
            return;
        }
        if (await players.ResolveAsync(query, ct) is { } found)
        {
            await Send.OkAsync(new PlayerDto(found.Uuid, found.Name), ct);
            return;
        }
        await Refusals.WriteAsync(HttpContext, 404, "no player",
            [new Finding(RequestRules.NoSuchSubject,
                $"no Minecraft account is called '{query}' — store it as a pseudonym instead, which PGM reads "
                + "as a whole author")], ct);
    }
}

/// <summary>GET /api/minecraft/player/{uuid}/head — the front of the player's head as an 8×8 PNG, their face with
/// the hat over it as the game draws it (<see cref="SkinHead"/>), served from the studio's own origin so a browser
/// draws a head without asking a third party. Kept for a day; 404 where the player has no skin or Mojang cannot
/// be reached, and the client draws the player's initial instead.</summary>
public sealed class PlayerHeadEndpoint(PlayerLookup players) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/minecraft/player/{uuid}/head");
        Description(b => b.Png().Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var uuid = Route<string>("uuid") ?? "";
        if (await players.SkinAsync(uuid, ct) is not { } skin || Front(skin) is not { } head)
        {
            HttpContext.Response.Headers.CacheControl = "public, max-age=300";
            await Refusals.WriteAsync(HttpContext, 404, "no skin",
                [new Finding(RequestRules.NoSuchSubject,
                    $"no skin could be had for '{uuid}' — it names no account, or Mojang did not answer")], ct);
            return;
        }
        HttpContext.Response.Headers.CacheControl = "public, max-age=86400";
        HttpContext.Response.ContentType = "image/png";
        await HttpContext.Response.Body.WriteAsync(PngWriter.Encode(SkinHead.Size, SkinHead.Size, head), ct);
    }

    /// <summary>The head's front, or null where the bytes kept are not a skin.</summary>
    private static byte[]? Front(byte[] skin)
    {
        try { return SkinHead.Front(PngReader.Decode(skin)); }
        catch (Exception fault) when (fault is FormatException or InvalidDataException) { return null; }
    }
}
