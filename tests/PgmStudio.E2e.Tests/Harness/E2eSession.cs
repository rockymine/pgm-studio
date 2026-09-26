using PuppeteerSharp;
using TUnit.Core.Interfaces;

namespace PgmStudio.E2e.Tests.Harness;

/// <summary>
/// What every spec shares for the length of a run: the API client, one browser, and the seeded fixtures.
/// Created once per test session, before the first spec, and injected into each spec class.
/// </summary>
public sealed class E2eSession : IAsyncInitializer, IAsyncDisposable
{
    private IBrowser? browser;
    private Seed? seed;

    public StudioApi Api { get; } = new(Studio.Base);

    public Seed Seed => seed ?? throw new InvalidOperationException("the session has not been seeded");

    public async Task InitializeAsync()
    {
        seed = await Seed.CreateAsync(Api);
        browser = await Chromium.LaunchAsync();
    }

    /// <summary>A fault-recording page in a context of its own, so no spec inherits another's storage.</summary>
    public Task<StudioPage> NewPageAsync(int width = 1600, int height = 900) =>
        StudioPage.OpenAsync(browser ?? throw new InvalidOperationException("the browser is not launched"), width, height);

    public async ValueTask DisposeAsync()
    {
        if (browser != null) await browser.CloseAsync();
    }
}
