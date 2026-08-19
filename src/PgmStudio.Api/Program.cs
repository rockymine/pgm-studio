using System.Globalization;
using System.Text.Json;
using FastEndpoints;
using PgmStudio.Domain;
using PgmStudio.Api.Http;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

// Every number this process reads or writes is dot-separated, whatever the host's regional settings say.
// The server has no user-facing text to localise, but it does parse and format numbers on three boundaries
// that must agree with the client byte for byte — query/route values, map.xml attributes and SVG geometry —
// and each of those defaults to the ambient culture. Under a comma-decimal locale that silently reinterprets
// a dot as a group separator, so "0.55" becomes 55: a valid number, a hundred times too large, and correct
// again the moment the same build runs on a dot-decimal machine. The client pins itself the same way, which
// is what makes the two ends one wire format rather than two locale-dependent ones.
var invariant = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = invariant;
CultureInfo.DefaultThreadCurrentUICulture = invariant;
CultureInfo.CurrentCulture = invariant;
CultureInfo.CurrentUICulture = invariant;

var builder = WebApplication.CreateBuilder(args);

// The host launches the built DLL from an arbitrary working directory (the repo root in dev, often /
// for a service), so the default ContentRoot-relative appsettings probe can miss them. Also load them
// from next to the binary, then re-apply env vars last so env still overrides file config.
builder.Configuration
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false)
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

builder.Services.AddFastEndpoints();

// Data access: one DataOptions (singleton) + a scoped PgmDb/readers per request. The connection string
// is a secret — it comes from User Secrets in dev (dotnet user-secrets) or the ConnectionStrings__PgmStudio
// / PGM_STUDIO_DB env var in prod; never committed.
var connectionString = builder.Configuration.GetConnectionString("PgmStudio")
    ?? Environment.GetEnvironmentVariable("PGM_STUDIO_DB")
    ?? throw new InvalidOperationException(
        "No database connection. Set it via User Secrets (dotnet user-secrets set \"ConnectionStrings:PgmStudio\" ...) " +
        "in development, or the ConnectionStrings__PgmStudio / PGM_STUDIO_DB environment variable in production.");
// Schema-drift guard: refuse to serve against a database behind the migrations this build needs,
// so requests fail loudly at startup with the fix instead of throwing "Unknown column" mid-request.
// The database lifecycle stays explicit — this never auto-applies migrations.
PgmStudio.Migrations.SchemaMigrator.AssertUpToDate(connectionString);

builder.Services.AddSingleton(PgmDataOptions.ForConnectionString(connectionString));
builder.Services.AddScoped<PgmDb>();
builder.Services.AddScoped<MapRepository>();
builder.Services.AddScoped<MapArtifactStore>();
builder.Services.AddScoped<PgmStudio.Data.Plan.PlanStore>();
builder.Services.AddScoped<PgmStudio.Data.Theme.ThemeStore>();
builder.Services.AddScoped<PgmStudio.Api.Services.ThemeLibrary>();
builder.Services.AddScoped<PgmStudio.Data.Theme.RoomStyleStore>();
builder.Services.AddScoped<PgmStudio.Data.Theme.HousePartStore>();
builder.Services.AddScoped<PgmStudio.Api.Services.RoomStyleLibrary>();
builder.Services.AddScoped<PgmStudio.Api.Services.HousePartLibrary>();
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

// B8 import-from-url (docs/tools/configure.md, the Import phase): a hardcoded SSRF allowlist + a dedicated
// imports root (kept out of the curated corpus) + bounded extraction.
var importRoot = builder.Configuration["Import:Root"] ?? Path.Combine(Path.GetTempPath(), "pgm-studio-imports");
var importHosts = builder.Configuration.GetSection("Import:AllowedHosts").Get<string[]>()
    ?? PgmStudio.Api.Services.ImportPolicy.DefaultAllowedHosts;
builder.Services.AddSingleton(new PgmStudio.Api.Services.ImportPolicy { AllowedHosts = importHosts, Root = importRoot });
// Dedicated client for the import fetch — NO auto-redirect (a redirect could bounce past the allowlist).
builder.Services.AddHttpClient("import", c => c.Timeout = TimeSpan.FromSeconds(120))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

// Corpus roots used to locate a map's Minecraft world (<root>/<slug>/region) for the world scan; the
// imports root is searched too so scan-world finds B8-imported worlds.
var mapsRoots = (builder.Configuration.GetSection("MapsRoots").Get<string[]>() ?? [])
    .Append(importRoot).ToArray();
builder.Services.AddSingleton(new PgmStudio.Api.Services.MapsRoots(mapsRoots));
// The last surface each island's relief settled on, so a preview resumes rather than rebuilding from flat.
// A singleton because it is a head start across requests; it can only save sweeps, never change an answer.
builder.Services.AddSingleton<PgmStudio.Api.Services.ReliefPreviewCache>();

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

// Nothing leaves /api as a stack trace. Every gate in the studio answers in one shape
// (docs/refusals.md), and a document that will not parse is supposed to arrive as a finding — but that
// holds only for the faults an endpoint thought to catch, and each of them names `JsonException` alone.
// Anything else escaped as a raw .NET or MySQL error: nine endpoints answered one to an empty body, one
// of them returning the database's own "Column 'name' cannot be null" to the caller.
//
// So the envelope is guaranteed here rather than asked of 159 endpoint classes. A JsonException is the
// document's fault and answers 400 in the canonical shape; anything else is the studio's own and stays a
// 500, because dressing a bug as a bad request sends an author hunting a mistake they did not make. The
// trace is logged, never sent. An endpoint that catches its own fault still answers first — this only
// sees what got past one.
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex) when (ctx.Request.Path.StartsWithSegments("/api") && !ctx.Response.HasStarted)
    {
        var document = ex is JsonException;
        if (!document)
            ctx.RequestServices.GetRequiredService<ILoggerFactory>()
               .CreateLogger("PgmStudio.Api.Unhandled")
               .LogError(ex, "unhandled at {Method} {Path}", ctx.Request.Method, ctx.Request.Path);

        await PgmStudio.Api.Endpoints.Refusals.WriteAsync(
            ctx,
            document ? 400 : 500,
            document ? "unreadable document" : "unhandled fault",
            [new Finding(
                document ? PgmStudio.Domain.RequestRules.Unreadable
                         : PgmStudio.Domain.RequestRules.Unhandled,
                document
                    ? ex.Message
                    : "the studio failed to answer this request, and the fault is its own rather than the "
                      + "document's — the detail is in the server log")],
            ctx.RequestAborted);
    }
});

// And the mirror of that guarantee on the other severity: a complaint an endpoint's gate produced rides on
// whatever success the endpoint answers, put there here rather than left to each endpoint to remember. A
// dropped complaint fails nothing, so nothing but this can catch one (docs/refusals.md, "What a success
// carries").
app.Use(PgmStudio.Api.Endpoints.Complaints.CarryAsync);

// All API endpoints live under /api.
app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api";
    // A field the DTO declares non-nullable and the body did not carry is refused by name before any handler
    // reads it — see RequiredFields for why the annotation alone does not hold.
    c.Endpoints.Configurator = ep => ep.PreProcessor<PgmStudio.Api.Endpoints.RequiredFields>(Order.Before);
    // Query/route/form values bind through the ambient culture unless told otherwise; this makes the
    // wire boundary itself invariant rather than leaning on the process-wide pin above.
    c.Binding.UseInvariantNumbers();
});

// SPA fallback: anything not matched by an API route or a static file serves the Blazor host page.
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program; // exposed for WebApplicationFactory in tests
