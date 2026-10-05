using System.Security.Cryptography;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Api.Services;

/// <summary>
/// The game's own block sprites, for the reads that draw with them — or the reason there are none.
///
/// <para>The sprites are Mojang's, so the studio never ships them. It reads them out of the 1.8.9 client jar,
/// from one of two places: a jar the operator already has (<c>Textures:Jar</c>), or Mojang's own server, and
/// only once the operator has said they accept Mojang's EULA (<c>Textures:AcceptMojangEula</c>). A download is
/// checked against the SHA-1 Mojang's launcher metadata declares for that jar and kept under
/// <c>Textures:Cache</c>, so it happens once per machine. Nothing it reads is served: what leaves the studio is
/// a picture drawn with the sprites.</para>
/// </summary>
public sealed class BlockTextureStore(IConfiguration configuration, IHttpClientFactory clients,
                                      ILogger<BlockTextureStore> log)
{
    /// <summary>The 1.8.9 client jar, as Mojang's version metadata names it.</summary>
    public const string JarUrl =
        "https://launcher.mojang.com/v1/objects/3870888a6c3d349d3771a3e9d16c9bf5e076b908/client.jar";

    /// <summary>The hash that metadata declares for it — the jar's own name on Mojang's object store.</summary>
    public const string JarSha1 = "3870888a6c3d349d3771a3e9d16c9bf5e076b908";

    /// <summary>The named client the download goes through.</summary>
    public const string ClientName = "mojang-jar";

    /// <summary>Which jar the sprites come from, for the name a picture drawn with them is kept under.</summary>
    public string Identity => configuration["Textures:Jar"] is { Length: > 0 } own && File.Exists(own)
        ? $"{own} {new FileInfo(own).Length} {File.GetLastWriteTimeUtc(own).Ticks}"
        : JarSha1;

    /// <summary>The sprites a picture is drawn with, or palette colours where the studio has none.</summary>
    public async Task<PictureSprites> ForPicturesAsync(CancellationToken ct) =>
        (await GetAsync(ct)).Set is { } set ? new PictureSprites(set, Identity) : PictureSprites.Flat;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private BlockTextureSet? _loaded;

    /// <summary>The sprites, or why there are none: <c>Set</c> is null exactly when <c>Reason</c> says what to
    /// configure or what failed.</summary>
    public async Task<(BlockTextureSet? Set, string? Reason)> GetAsync(CancellationToken ct)
    {
        if (_loaded is { } ready) return (ready, null);
        await _gate.WaitAsync(ct);
        try
        {
            if (_loaded is { } raced) return (raced, null);
            if (await JarAsync(ct) is not { } jar)
                return (null, "the studio has no block textures");
            await using var stream = File.OpenRead(jar);
            _loaded = BlockTextureSet.FromJar(stream);
            log.LogInformation("Block textures: {Count} sprites read from {Jar}", _loaded.Count, jar);
            return (_loaded, null);
        }
        catch (Exception fault) when (fault is HttpRequestException or IOException or InvalidDataException
                                          or FormatException or TaskCanceledException)
        {
            log.LogWarning(fault, "Block textures did not read");
            return (null, "the studio's block textures do not read");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The jar to read: the operator's own, or the cached download, fetched now if it is allowed and
    /// missing. Null where neither is permitted.</summary>
    private async Task<string?> JarAsync(CancellationToken ct)
    {
        if (configuration["Textures:Jar"] is { Length: > 0 } own)
            return File.Exists(own) ? own : throw new IOException($"Textures:Jar names {own}, which does not exist");
        if (!configuration.GetValue<bool>("Textures:AcceptMojangEula")) return null;

        var folder = configuration["Textures:Cache"] is { Length: > 0 } stated
            ? stated
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pgm-studio", "textures");
        var cached = Path.Combine(folder, $"client-1.8.9-{JarSha1}.jar");
        if (File.Exists(cached)) return cached;

        log.LogInformation("Block textures: downloading the 1.8.9 client jar from {Url}", JarUrl);
        var bytes = await clients.CreateClient(ClientName).GetByteArrayAsync(JarUrl, ct);
        var hash = Convert.ToHexStringLower(SHA1.HashData(bytes));
        if (hash != JarSha1)
            throw new InvalidDataException($"the jar Mojang served hashes to {hash}, where its metadata declares {JarSha1}");
        Directory.CreateDirectory(folder);
        var partial = cached + ".part";
        await File.WriteAllBytesAsync(partial, bytes, ct);
        File.Move(partial, cached, overwrite: true);
        return cached;
    }
}

/// <summary>The sprites a picture is drawn with — null where it is drawn in palette colours — and the name they
/// go by in a kept picture's name (<see cref="Drawings"/>).</summary>
public sealed record PictureSprites(BlockTextureSet? Set, string Identity)
{
    public static readonly PictureSprites Flat = new(null, "flat");
}
