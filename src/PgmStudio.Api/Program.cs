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

// Corpus roots used to locate a map's Minecraft world (<root>/<slug>/region) for the world scan.
var mapsRoots = builder.Configuration.GetSection("MapsRoots").Get<string[]>()
    ?? ["/media/sf_repos/CommunityMaps/ctw", "/media/sf_repos/PublicMaps/ctw"];
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
app.UseStaticFiles();

// All API endpoints live under /api (mirrors the Python app's /api/... surface).
app.UseFastEndpoints(c => c.Endpoints.RoutePrefix = "api");

// SPA fallback: anything not matched by an API route or a static file serves the Blazor host page.
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program; // exposed for WebApplicationFactory in tests
