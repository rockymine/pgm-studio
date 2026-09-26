using System.Diagnostics;
using System.Text.Json;
using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// Who may write, as the browser shows it.
///
/// The suite's own server is open, so every page there stays fully editable and the whitelist page answers
/// the local admin. A second server over the same database runs invited, where this browser is signed out:
/// every map page must say it is read-only, grey its fields, keep the tools that only look and drop the ones
/// that draw, and the pages that start a map or keep the whitelist must offer nothing to press.
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class AccessSpec(E2eSession session)
{
    private const string ReadState = """
        () => ({
          banner: document.querySelector(".topbar .readonly-tag")?.getAttribute("title") ?? null,
          signIn: !!document.querySelector('.app-nav a.account-signin[href*="api/auth/discord"]'),
          greyed: document.querySelectorAll("fieldset.readonly-fieldset[disabled]").length,
          select: !!document.querySelector('.canvas-dock button[aria-label="Select"]'),
          rectangle: !!document.querySelector('.canvas-dock button[aria-label="Rectangle"]'),
          inspector: document.querySelectorAll(".workspace-inspector, .workspace-scroll").length,
          account: document.querySelector(".app-nav-right")?.textContent?.replace(/\s+/g, " ").trim() ?? "",
          users: !!document.querySelector('.app-nav a[href="admin/users"]'),
        })
        """;

    [Test, NotInParallel(Order = 1)]
    public async Task WritingFollowsWhoIsSignedIn()
    {
        var seed = session.Seed;
        var checks = new Checks("access");
        await using var page = await session.NewPageAsync();

        async Task Visit(string baseUrl, string path)
        {
            page.ClearFaults();
            await page.GotoAsync($"{baseUrl}{path}");
            await page.WaitForSelectorAsync(
                ".app-nav-right .account-local, .app-nav-right .account-signin, .app-nav-right .account", 20000);
        }

        // ── the suite's own server: open, the local admin ──
        checks.Section("an open studio stays editable");
        await Visit(Studio.Base, $"/maps/{seed.SketchSlug}/sketch");
        await page.TryWaitForSelectorAsync(".canvas-dock", 20000);
        var state = await page.EvaluateAsync(ReadState);
        var banner = Banner(state);
        checks.Add("no view-only tag", banner == null, banner ?? "");
        var greyed = state.GetProperty("greyed").GetInt32();
        checks.Add("no field is greyed", greyed == 0, $"{greyed} disabled fieldset(s)");
        checks.Add("the drawing tools are there", state.GetProperty("rectangle").GetBoolean());
        var account = state.GetProperty("account").GetString() ?? "";
        checks.Add("the studio bar names the local admin", account.Contains("local", StringComparison.Ordinal), account);
        checks.Add("the studio bar links the whitelist for an admin", state.GetProperty("users").GetBoolean());

        await Visit(Studio.Base, "/admin/users");
        var adminPage = await page.EvaluateAsync("""
            () => ({
              add: !!document.querySelector('input[placeholder="Minecraft name or uuid"]'),
              refused: /Only an admin/.test(document.body.textContent),
            })
            """);
        checks.Add("the whitelist page answers the local admin",
            adminPage.GetProperty("add").GetBoolean() && !adminPage.GetProperty("refused").GetBoolean());
        checks.Add("no page fault", page.Faults.Count == 0, string.Join(" | ", page.Faults.Take(3)));

        // ── a second server over the same database, invited, with this browser signed out ──
        var port = new Uri(Studio.Base).Port + 1;
        var invited = $"http://localhost:{port}";
        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { Studio.ApiDll, "--urls", $"http://0.0.0.0:{port}" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["Access__Mode"] = "invited";
        using var server = Process.Start(start) ?? throw new InvalidOperationException("could not start the invited server");
        // Drained rather than read: a server writing into a full pipe blocks.
        server.OutputDataReceived += (_, _) => { };
        server.ErrorDataReceived += (_, _) => { };
        server.BeginOutputReadLine();
        server.BeginErrorReadLine();
        try
        {
            var up = false;
            for (var attempt = 0; attempt < 60 && !up; attempt++)
            {
                up = await StudioApi.IsHealthy(invited);
                if (!up) await StudioPage.Pause(1000);
            }
            checks.Add("the invited server came up", up, invited);

            // The sketch opens on its canvas; Configure opens on its info page, which has no canvas to keep a dock on.
            foreach (var (name, path, canvas) in new[]
            {
                ("sketch", $"/maps/{seed.SketchSlug}/sketch", true),
                ("configure", $"/maps/{seed.MapSlug}/configure", false),
            })
            {
                checks.Section($"a signed-out visitor sees the {name} tool read-only");
                await Visit(invited, path);
                await page.TryWaitForSelectorAsync(".topbar .readonly-tag", 20000);
                if (canvas) await page.TryWaitForSelectorAsync(".canvas-dock", 20000);
                await page.TryWaitForSelectorAsync("fieldset.readonly-fieldset", 10000);
                state = await page.EvaluateAsync(ReadState);
                banner = Banner(state);
                greyed = state.GetProperty("greyed").GetInt32();
                checks.Add("the tool bar says view only, and why",
                    (banner ?? "").Contains("not signed in", StringComparison.Ordinal), banner ?? "no tag");
                checks.Add("the studio bar offers the sign-in", state.GetProperty("signIn").GetBoolean());
                checks.Add("the panels are greyed", greyed > 0,
                    $"{greyed} disabled fieldset(s) over {state.GetProperty("inspector").GetInt32()} panel(s)");
                if (canvas) checks.Add("the tools that only look stay", state.GetProperty("select").GetBoolean());
                checks.Add("the tools that draw are gone", !state.GetProperty("rectangle").GetBoolean());
                checks.Add("the studio bar hides the whitelist from a visitor", !state.GetProperty("users").GetBoolean());
                checks.Add("opening it writes nothing and faults nothing", page.Faults.Count == 0,
                    string.Join(" | ", page.Faults.Take(3)));
            }

            checks.Section("a signed-out visitor cannot start a map or keep the whitelist");
            await Visit(invited, "/maps?stage=plan");
            await page.TryWaitForSelectorAsync("button.action-btn--primary", 20000);
            var newPlan = await page.EvaluateAsync(
                "() => [...document.querySelectorAll('button')].find(b => /New plan/.test(b.textContent))?.disabled ?? null");
            checks.Add("New plan is greyed", newPlan.ValueKind == JsonValueKind.True, newPlan.GetRawText());

            await Visit(invited, "/admin/users");
            await page.TryWaitForFunctionAsync("() => /Only an admin|Minecraft name/.test(document.body.textContent)", 20000);
            var refused = await page.EvaluateAsync<bool>("""
                () => /Only an admin/.test(document.body.textContent)
                  && !document.querySelector('input[placeholder="Minecraft name or uuid"]')
                """);
            checks.Add("the whitelist page shows nothing of the list", refused);
        }
        finally
        {
            server.Kill(entireProcessTree: true);
            await server.WaitForExitAsync();
        }

        checks.Finish();
    }

    private static string? Banner(JsonElement state) =>
        state.GetProperty("banner") is { ValueKind: JsonValueKind.String } banner ? banner.GetString() : null;
}
