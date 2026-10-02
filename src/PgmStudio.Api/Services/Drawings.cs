using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace PgmStudio.Api.Services;

/// <summary>
/// Every picture the studio draws from stored documents — a library card, a world render, the eye's view —
/// drawn once per distinct input and kept as a file under <c>Drawings:Folder</c>, so a picture outlives the
/// request, the world it was drawn from and a restart.
///
/// <para>A picture's name is the SHA-256 of what drew it: the drawer, the build of the studio, and the input's
/// own bytes. An edited row, a changed board or a deploy that changes a drawer is therefore a different name, and
/// no stale picture can be served; nothing is ever invalidated. Concurrent askers of one name wait on one
/// drawing, and a drawing that throws is not kept.</para>
///
/// <para>The folder is held under <c>Drawings:Budget</c> bytes by removing the pictures least recently asked
/// for. A picture that has nothing to draw is kept as an empty file, so asking again is as cheap.</para>
/// </summary>
public static class Drawings
{
    private const long DefaultBudget = 512L * 1024 * 1024;

    private static readonly ConcurrentDictionary<string, Lazy<byte[]?>> Drawing = new();
    private static readonly Lock Trimming = new();
    private static readonly string Build = BuildStamp();
    private static long _held = -1;

    /// <summary>Where the pictures are kept.</summary>
    public static string Folder { get; private set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pgm-studio", "drawings");

    /// <summary>How many bytes of pictures the folder holds before the least recently asked for are removed.</summary>
    public static long Budget { get; private set; } = DefaultBudget;

    /// <summary>Reads <c>Drawings:Folder</c> and <c>Drawings:Budget</c>; called once at startup.</summary>
    public static void Configure(IConfiguration configuration)
    {
        if (configuration["Drawings:Folder"] is { Length: > 0 } folder) Folder = folder;
        if (configuration.GetValue<long?>("Drawings:Budget") is { } budget and > 0) Budget = budget;
    }

    /// <summary>The name of the picture <paramref name="drawer"/> draws from <paramref name="input"/>.
    /// <paramref name="drawer"/> carries every option that changes the picture and is not in the input.</summary>
    public static string Name(string drawer, ReadOnlySpan<byte> input)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(Build + "\0" + drawer + "\0"));
        hash.AppendData(input);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>The name of the picture <paramref name="drawer"/> draws from <paramref name="input"/>.</summary>
    public static string Name(string drawer, string input) => Name(drawer, Encoding.UTF8.GetBytes(input));

    /// <summary>The picture kept under <paramref name="name"/>, if one is. A kept picture with nothing to draw
    /// answers true with a null picture.</summary>
    public static bool TryFind(string name, out byte[]? picture)
    {
        var path = PathOf(name);
        try
        {
            var bytes = File.ReadAllBytes(path);
            picture = bytes.Length == 0 ? null : bytes;
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            return true;
        }
        catch (Exception missing) when (missing is FileNotFoundException or DirectoryNotFoundException)
        {
            picture = null;
            return false;
        }
    }

    /// <summary>Keep <paramref name="picture"/> under <paramref name="name"/>; null keeps "nothing to draw".</summary>
    public static void Keep(string name, byte[]? picture)
    {
        var path = PathOf(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var writing = path + "." + Guid.NewGuid().ToString("N") + ".part";
        File.WriteAllBytes(writing, picture ?? []);
        File.Move(writing, path, overwrite: true);
        if (Volatile.Read(ref _held) < 0) Trim();
        if (Interlocked.Add(ref _held, picture?.Length ?? 0) > Budget) Trim();
    }

    /// <summary>The picture kept under <paramref name="name"/>, or <paramref name="draw"/>'s, kept, when there is
    /// none. Concurrent askers of one name wait on one drawing.</summary>
    public static byte[]? Of(string name, Func<byte[]?> draw)
    {
        if (TryFind(name, out var kept)) return kept;
        var drawing = Drawing.GetOrAdd(name, _ => new Lazy<byte[]?>(() =>
        {
            if (TryFind(name, out var already)) return already;
            var picture = draw();
            Keep(name, picture);
            return picture;
        }, LazyThreadSafetyMode.ExecutionAndPublication));
        try { return drawing.Value; }
        finally { Drawing.TryRemove(new KeyValuePair<string, Lazy<byte[]?>>(name, drawing)); }
    }

    /// <summary>An SVG card <paramref name="drawer"/> draws from <paramref name="input"/>, drawn once.</summary>
    public static string Svg(string drawer, string input, Func<string> draw) =>
        Of(Name(drawer, input), () => Encoding.UTF8.GetBytes(draw())) is { } bytes
            ? Encoding.UTF8.GetString(bytes) : "";

    /// <summary>Two levels of folders by the name's first four digits keep any one folder small.</summary>
    private static string PathOf(string name) => Path.Combine(Folder, name[..2], name[2..4], name);

    /// <summary>Remove the pictures least recently asked for until the folder holds three quarters of its
    /// budget.</summary>
    private static void Trim()
    {
        lock (Trimming)
        {
            var files = new DirectoryInfo(Folder).Exists
                ? new DirectoryInfo(Folder).EnumerateFiles("*", SearchOption.AllDirectories)
                    .Where(file => !file.Name.EndsWith(".part", StringComparison.Ordinal)).ToList()
                : [];
            var held = files.Sum(file => file.Length);
            foreach (var oldest in files.OrderBy(file => file.LastWriteTimeUtc))
            {
                if (held <= Budget * 3 / 4) break;
                try { oldest.Delete(); held -= oldest.Length; }
                catch (IOException) { }
            }
            Interlocked.Exchange(ref _held, held);
        }
    }

    /// <summary>The identity of every studio assembly this process runs, so a build that changes a drawer
    /// names its pictures afresh.</summary>
    private static string BuildStamp()
    {
        var seen = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var waiting = new Stack<Assembly>([typeof(Drawings).Assembly]);
        while (waiting.TryPop(out var assembly))
        {
            if (!seen.TryAdd(assembly.GetName().Name!, assembly)) continue;
            foreach (var reference in assembly.GetReferencedAssemblies())
                if (reference.Name?.StartsWith("PgmStudio.", StringComparison.Ordinal) == true
                    && !seen.ContainsKey(reference.Name))
                    waiting.Push(Assembly.Load(reference));
        }
        return string.Join(",", seen.Values
            .Select(assembly => assembly.ManifestModule.ModuleVersionId.ToString("N"))
            .Order(StringComparer.Ordinal));
    }
}
