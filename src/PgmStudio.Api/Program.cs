using FastEndpoints;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFastEndpoints();

// Data access: one DataOptions (singleton) + a scoped PgmDb/readers per request.
var connectionString = builder.Configuration.GetConnectionString("PgmStudio")
    ?? "Server=localhost;Database=pgm_studio;User ID=pgm;Password=pgm_dev_pw;";
builder.Services.AddSingleton(PgmDataOptions.ForConnectionString(connectionString));
builder.Services.AddScoped<PgmDb>();
builder.Services.AddScoped<MapRepository>();
builder.Services.AddScoped<MapReader>();
builder.Services.AddScoped<MapWriter>();
builder.Services.AddScoped<WorldFeatureWriter>();
builder.Services.AddScoped<PgmStudio.Api.Services.FeatureData>();

// Mojang username/uuid resolution for the Overview authors UI (typed HttpClient).
builder.Services.AddHttpClient<PgmStudio.Api.Services.MojangClient>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(5);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("pgm-studio/1.0");
});

// B8 import-from-url (docs/contracts/new-map-authoring.md §12): a hardcoded SSRF allowlist + a dedicated
// imports root (kept out of the curated corpus) + bounded extraction.
var importRoot = builder.Configuration["Import:Root"] ?? "/media/sf_repos/pgm-studio-imports";
var importHosts = builder.Configuration.GetSection("Import:AllowedHosts").Get<string[]>()
    ?? PgmStudio.Api.Services.ImportPolicy.DefaultAllowedHosts;
builder.Services.AddSingleton(new PgmStudio.Api.Services.ImportPolicy { AllowedHosts = importHosts, Root = importRoot });
// Dedicated client for the import fetch — NO auto-redirect (a redirect could bounce past the allowlist).
builder.Services.AddHttpClient("import", c => c.Timeout = TimeSpan.FromSeconds(120))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

// Corpus roots used to locate a map's Minecraft world (<root>/<slug>/region) for the world scan; the
// imports root is searched too so scan-world finds B8-imported worlds.
var mapsRoots = (builder.Configuration.GetSection("MapsRoots").Get<string[]>()
        ?? ["/media/sf_repos/CommunityMaps/ctw", "/media/sf_repos/PublicMaps/ctw"])
    .Append(importRoot).ToArray();
builder.Services.AddSingleton(new PgmStudio.Api.Services.MapsRoots(mapsRoots));

var app = builder.Build();

// Serve the hosted Blazor WebAssembly client.
app.UseBlazorFrameworkFiles();
// Blazor's import map points our wwwroot JS modules at fingerprinted names (e.g.
// studio-canvas.<hash>.js), but UseStaticFiles only serves the real names. Rewrite a fingerprinted
// /js/... request back to its real file so the modules resolve (the framework files are served
// separately by UseBlazorFrameworkFiles).
app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value;
    if (p is not null && p.StartsWith("/js/"))
    {
        var m = System.Text.RegularExpressions.Regex.Match(p, @"^(.*)\.[0-9a-z]{8,}(\.js)$");
        if (m.Success) ctx.Request.Path = m.Groups[1].Value + m.Groups[2].Value;
    }
    await next();
});
// In Development, force revalidation of static assets (the hand-written wwwroot CSS/JS are
// served unfingerprinted and otherwise get a heuristic cache with no Cache-Control, so edits
// don't show up on reload). no-cache = "cache but revalidate via ETag" → 200 when changed, 304 otherwise.
var staticFileOptions = new StaticFileOptions();
if (app.Environment.IsDevelopment())
{
    staticFileOptions.OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers["Cache-Control"] = "no-cache, must-revalidate";
}
app.UseStaticFiles(staticFileOptions);

// All API endpoints live under /api (mirrors the Python app's /api/... surface).
app.UseFastEndpoints(c => c.Endpoints.RoutePrefix = "api");

// SPA fallback: anything not matched by an API route or a static file serves the Blazor host page.
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program; // exposed for WebApplicationFactory in tests
