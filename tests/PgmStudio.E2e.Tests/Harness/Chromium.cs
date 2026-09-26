using System.Text.RegularExpressions;
using PuppeteerSharp;

namespace PgmStudio.E2e.Tests.Harness;

/// <summary>
/// Finds a Chromium and launches it headless over CDP. The first source that answers wins, and the run says
/// which it took:
/// <list type="number">
/// <item><c>E2E_CHROMIUM</c> — an executable path, for a browser somewhere else entirely;</item>
/// <item>the newest <c>chromium-&lt;rev&gt;</c> build under <c>PLAYWRIGHT_BROWSERS_PATH</c>, the directory a cloud
/// container ships its browser in;</item>
/// <item><c>google-chrome</c>, <c>chromium</c> or <c>chromium-browser</c> on <c>PATH</c>;</item>
/// <item>a download through PuppeteerSharp's <see cref="BrowserFetcher"/>.</item>
/// </list>
/// </summary>
public static partial class Chromium
{
    private static readonly string[] OnPath = ["google-chrome", "chromium", "chromium-browser"];

    public static async Task<IBrowser> LaunchAsync()
    {
        var (source, executable) = await ResolveAsync();
        Console.WriteLine($"· chromium from {source}: {executable}");

        var arguments = new List<string>();
        // Chromium refuses to start its sandbox as root, which is what a container runs as.
        if (Environment.UserName == "root") arguments.Add("--no-sandbox");

        return await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = executable,
            HeadlessMode = HeadlessMode.True,
            Args = [.. arguments],
            DefaultViewport = null,
        });
    }

    private static async Task<(string Source, string Executable)> ResolveAsync()
    {
        if (Environment.GetEnvironmentVariable("E2E_CHROMIUM") is { Length: > 0 } named) return ("E2E_CHROMIUM", named);
        if (NewestUnderBrowsersPath() is { } installed) return ("PLAYWRIGHT_BROWSERS_PATH", installed);
        if (FirstOnPath() is { } system) return ("PATH", system);

        var fetched = await new BrowserFetcher().DownloadAsync();
        return ("a BrowserFetcher download", fetched.GetExecutablePath());
    }

    /// <summary>The newest build under <c>PLAYWRIGHT_BROWSERS_PATH</c>, in any of the three per-platform layouts.</summary>
    private static string? NewestUnderBrowsersPath()
    {
        var root = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return null;

        return Directory.EnumerateDirectories(root)
            .Select(directory => (Directory: directory, Match: Revision().Match(Path.GetFileName(directory))))
            .Where(entry => entry.Match.Success)
            .Select(entry => (Revision: int.Parse(entry.Match.Groups[1].Value), Binary: new[]
            {
                Path.Combine(entry.Directory, "chrome-linux", "chrome"),
                Path.Combine(entry.Directory, "chrome-mac", "Chromium.app", "Contents", "MacOS", "Chromium"),
                Path.Combine(entry.Directory, "chrome-win", "chrome.exe"),
            }.FirstOrDefault(File.Exists)))
            .Where(entry => entry.Binary != null)
            .OrderByDescending(entry => entry.Revision)
            .Select(entry => entry.Binary)
            .FirstOrDefault();
    }

    private static string? FirstOnPath()
    {
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries);
        return OnPath
            .SelectMany(name => directories.Select(directory => Path.Combine(directory, name)))
            .FirstOrDefault(File.Exists);
    }

    [GeneratedRegex(@"^chromium-(\d+)$")]
    private static partial Regex Revision();
}
