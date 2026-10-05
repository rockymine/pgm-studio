using System.Security.Cryptography;
using PgmStudio.Data.Map;

namespace PgmStudio.Api.Services;

/// <summary>
/// The pictures notes carry, as files named by the SHA-256 of their bytes under <c>Notes:Pictures</c>. This is
/// the only code that reads or writes that folder. A picture is written once, before the row that names it,
/// and never changed; <see cref="SweepAsync"/> removes the files no message names, so a write that never
/// reached its row leaves an orphan the sweep takes rather than a row naming nothing.
/// </summary>
public sealed class NotePictures
{
    /// <summary>The largest picture kept: a 1920 × 1080 PNG of a busy board, with room.</summary>
    public const int Largest = 8 * 1024 * 1024;

    /// <summary>How old an unnamed file must be before a sweep removes it: long enough for the note that
    /// uploaded it to have been written.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromHours(1);

    private static readonly (string Extension, string ContentType)[] Kinds = [(".webp", "image/webp"), (".png", "image/png")];

    public NotePictures(IConfiguration configuration) =>
        Folder = configuration["Notes:Pictures"] is { Length: > 0 } stated
            ? stated
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pgm-studio", "pictures");

    /// <summary>Where the pictures are kept.</summary>
    public string Folder { get; }

    /// <summary>The kind of picture <paramref name="bytes"/> is — its extension and content type — or null
    /// where it is neither a WebP nor a PNG.</summary>
    public static (string Extension, string ContentType)? KindOf(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8) ? Kinds[0]
        : bytes.Length >= 8 && bytes[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) ? Kinds[1]
        : null;

    /// <summary>Whether <paramref name="hash"/> is shaped like a picture's name: 64 lowercase hex digits.</summary>
    public static bool IsHash(string? hash) =>
        hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>Keep <paramref name="bytes"/>, answering its hash. The caller has checked its kind.</summary>
    public async Task<string> SaveAsync(byte[] bytes, CancellationToken ct = default)
    {
        var (extension, _) = KindOf(bytes) ?? throw new ArgumentException("the picture is not a WebP or a PNG");
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var path = Path.Combine(Folder, hash + extension);
        if (File.Exists(path)) return hash;
        Directory.CreateDirectory(Folder);
        var writing = path + "." + Guid.NewGuid().ToString("N") + ".part";
        await File.WriteAllBytesAsync(writing, bytes, ct);
        File.Move(writing, path, overwrite: true);
        return hash;
    }

    /// <summary>Whether a picture is kept under <paramref name="hash"/>.</summary>
    public bool Has(string hash) => Find(hash) is not null;

    /// <summary>The file kept under <paramref name="hash"/> and its content type, or null.</summary>
    public (string Path, string ContentType)? Find(string hash)
    {
        if (!IsHash(hash)) return null;
        foreach (var (extension, type) in Kinds)
        {
            var path = Path.Combine(Folder, hash + extension);
            if (File.Exists(path)) return (path, type);
        }
        return null;
    }

    /// <summary>Remove every picture no message names that is older than <see cref="Grace"/>, and every
    /// half-written file as old. Answers how many went.</summary>
    public async Task<int> SweepAsync(MapNoteStore notes, CancellationToken ct = default)
    {
        if (!Directory.Exists(Folder)) return 0;
        var named = (await notes.PicturesAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var before = DateTime.UtcNow - Grace;
        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(Folder))
        {
            var name = Path.GetFileName(file);
            var hash = name.Split('.')[0];
            if (!name.EndsWith(".part", StringComparison.Ordinal) && named.Contains(hash)) continue;
            if (File.GetLastWriteTimeUtc(file) > before) continue;
            File.Delete(file);
            removed++;
        }
        return removed;
    }
}

/// <summary>Sweeps the note pictures once an hour, the first time a minute after the studio starts.</summary>
public sealed class NotePictureSweep(IServiceScopeFactory scopes, ILogger<NotePictureSweep> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var removed = await scope.ServiceProvider.GetRequiredService<NotePictures>()
                        .SweepAsync(scope.ServiceProvider.GetRequiredService<MapNoteStore>(), stoppingToken);
                    if (removed > 0) log.LogInformation("Note pictures: removed {Count} no note names", removed);
                }
                catch (Exception failed) when (failed is not OperationCanceledException)
                {
                    log.LogWarning(failed, "Note pictures: the sweep failed, and runs again in an hour");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { }
    }
}
