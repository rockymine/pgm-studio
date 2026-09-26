using System.Text.Json;
using System.Text.RegularExpressions;
using PuppeteerSharp;
using PuppeteerSharp.Input;

namespace PgmStudio.E2e.Tests.Harness;

/// <summary>
/// A browser page, in a context of its own, that records everything that went wrong on it.
///
/// A page is healthy only if it raises no uncaught exception, logs no console error, and issues no failed or
/// 4xx/5xx request — the class of break a unit test cannot see (a boot failure, a route that no longer
/// resolves, a dead asset). <see cref="Faults"/> holds the ones that count; <see cref="Allowed"/> holds those
/// <see cref="AllowedFaults"/> tolerates, so a run can still report what it chose to ignore.
/// </summary>
public sealed partial class StudioPage : IAsyncDisposable
{
    /// <summary>
    /// Faults that are known, explained, and not this suite's business. Every entry is coverage given up, so
    /// each carries its reason.
    /// </summary>
    public static readonly IReadOnlyList<(Regex Match, string Why)> AllowedFaults =
    [
        // ERR_ABORTED is the browser cancelling a request still in flight, which on a route sweep is the sweep
        // itself moving on — the page is judged and left while a background fetch is open. It says nothing
        // about the page's health and fires nondeterministically, so tolerating it removes a flake rather than
        // coverage: a request that actually fails or answers 4xx/5xx is a different event and still counts.
        (AbortedRequest(), "a fetch cancelled by navigating on — an artifact of the sweep, not a page fault"),
    ];

    public const int DefaultTimeout = 30000;

    private readonly IBrowserContext context;
    private readonly object gate = new();
    private readonly List<string> faults = [];
    private readonly List<string> allowed = [];

    private StudioPage(IBrowserContext context, IPage page)
    {
        this.context = context;
        Page = page;
        page.PageError += (_, error) => Note($"uncaught: {error.Message}");
        page.Console += (_, console) =>
        {
            if (console.Message.Type != ConsoleType.Error) return;
            // The browser echoes every failed subresource to the console WITHOUT its URL, which is both
            // unattributable and a duplicate of the request events below — those carry the URL and decide.
            if (console.Message.Text.StartsWith("Failed to load resource", StringComparison.Ordinal)) return;
            Note($"console: {console.Message.Text}");
        };
        page.RequestFailed += (_, failed) => Note($"request failed ({failed.Request.FailureText}): {failed.Request.Url}");
        page.Response += (_, answered) =>
        {
            if ((int)answered.Response.Status >= 400) Note($"HTTP {(int)answered.Response.Status}: {answered.Response.Url}");
        };
    }

    public IPage Page { get; }
    public IMouse Mouse => Page.Mouse;
    public IKeyboard Keyboard => Page.Keyboard;
    public string Url => Page.Url;

    /// <summary>The un-allowed faults recorded since the last <see cref="ClearFaults"/>.</summary>
    public IReadOnlyList<string> Faults { get { lock (gate) return [.. faults]; } }

    /// <summary>The tolerated faults recorded since the last <see cref="ClearFaults"/>.</summary>
    public IReadOnlyList<string> Allowed { get { lock (gate) return [.. allowed]; } }

    /// <summary>Opens a page in a fresh browser context, so no spec inherits another's storage.</summary>
    public static async Task<StudioPage> OpenAsync(IBrowser browser, int width = 1600, int height = 900)
    {
        var context = await browser.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportAsync(new ViewPortOptions { Width = width, Height = height });
        return new StudioPage(context, page);
    }

    /// <summary>Records a fault, sorted into the allowed or the counted list.</summary>
    public void Note(string text)
    {
        lock (gate) (AllowedFaults.Any(entry => entry.Match.IsMatch(text)) ? allowed : faults).Add(text);
    }

    /// <summary>Drops everything recorded so far — between routes, so a fault is attributed to one page.</summary>
    public void ClearFaults()
    {
        lock (gate)
        {
            faults.Clear();
            allowed.Clear();
        }
    }

    /// <summary>Navigates to a studio path (or an absolute URL) and waits for the network to go idle.</summary>
    public Task GotoAsync(string pathOrUrl, int timeout = DefaultTimeout) =>
        Page.GoToAsync(pathOrUrl.StartsWith("http", StringComparison.Ordinal) ? pathOrUrl : $"{Studio.Base}{pathOrUrl}",
            new NavigationOptions { Timeout = timeout, WaitUntil = [WaitUntilNavigation.Networkidle0] });

    /// <summary>Waits for a CSS selector to match a visible element (or, with <paramref name="hidden"/>, for none to).</summary>
    public Task WaitForSelectorAsync(string css, int timeout = DefaultTimeout, bool hidden = false) =>
        Page.WaitForSelectorAsync(css, new WaitForSelectorOptions { Timeout = timeout, Visible = !hidden, Hidden = hidden });

    /// <summary><see cref="WaitForSelectorAsync"/> that answers whether it happened instead of throwing.</summary>
    public async Task<bool> TryWaitForSelectorAsync(string css, int timeout = DefaultTimeout)
    {
        try
        {
            await WaitForSelectorAsync(css, timeout);
            return true;
        }
        catch (WaitTaskTimeoutException) { return false; }
    }

    /// <summary>Waits until a page function returns truthy; answers whether it did.</summary>
    public async Task<bool> TryWaitForFunctionAsync(string function, int timeout = DefaultTimeout)
    {
        try
        {
            await Page.WaitForFunctionAsync(function, new WaitForFunctionOptions { Timeout = timeout });
            return true;
        }
        catch (WaitTaskTimeoutException) { return false; }
    }

    /// <summary>Runs a page function and reads its result back as JSON.</summary>
    public Task<JsonElement> EvaluateAsync(string function, params object[] arguments) =>
        Page.EvaluateFunctionAsync<JsonElement>(function, arguments);

    public Task<T> EvaluateAsync<T>(string function, params object[] arguments) =>
        Page.EvaluateFunctionAsync<T>(function, arguments);

    /// <summary>
    /// Elements matching <paramref name="css"/>, optionally only those whose text contains
    /// <paramref name="hasText"/> (case-insensitive, whitespace-normalized).
    /// </summary>
    public Locator Locator(string css, string? hasText = null) => new Locator(this, []).Locate(css, hasText);

    public Task ClickAsync(string css, int timeout = DefaultTimeout) => Locator(css).ClickAsync(timeout);

    /// <summary>The raw text content of the first element matching <paramref name="css"/>, once one exists.</summary>
    public Task<string> TextContentAsync(string css) => Locator(css).TextContentAsync();

    /// <summary>
    /// Whether the page shows <paramref name="text"/> anywhere in its body — a case-insensitive,
    /// whitespace-normalized substring of the document's text, script and style excluded.
    /// </summary>
    public Task<bool> ShowsTextAsync(string text) => EvaluateAsync<bool>("""
        needle => {
          const norm = s => s.replace(/\s+/g, " ").trim().toLowerCase();
          const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, {
            acceptNode: node => node.parentElement?.closest("script, style, template, noscript")
              ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT,
          });
          let text = "";
          for (let node = walker.nextNode(); node; node = walker.nextNode()) text += node.nodeValue;
          return norm(text).includes(norm(needle));
        }
        """, text);

    /// <summary>Waits until the page's address ends with <paramref name="suffix"/>.</summary>
    public async Task WaitForUrlEndingAsync(string suffix, int timeout = DefaultTimeout)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
        while (!Page.Url.EndsWith(suffix, StringComparison.Ordinal))
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"the address never ended with {suffix}: {Page.Url}");
            await Task.Delay(100);
        }
    }

    /// <summary>A pending wait for a successful response whose URL contains <paramref name="fragment"/>.</summary>
    public Task<IResponse> WaitForOkResponseAsync(string fragment, int timeout = DefaultTimeout) =>
        Page.WaitForResponseAsync(response => response.Url.Contains(fragment, StringComparison.Ordinal) && response.Ok,
            new WaitForOptions { Timeout = timeout });

    public Task ScreenshotAsync(string file) =>
        Page.ScreenshotAsync(Path.Combine(Studio.TmpDir, file), new ScreenshotOptions { FullPage = false });

    public static Task Pause(int milliseconds) => Task.Delay(milliseconds);

    public async ValueTask DisposeAsync()
    {
        await Page.CloseAsync();
        await context.CloseAsync();
    }

    [GeneratedRegex(@"request failed \(net::ERR_ABORTED\)")]
    private static partial Regex AbortedRequest();
}
