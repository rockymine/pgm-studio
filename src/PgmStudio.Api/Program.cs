using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.Swagger;
using PgmStudio.Domain;
using PgmStudio.Api.Endpoints;
using PgmStudio.Api.Http;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Vocabulary;

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

// The API describes itself, at /api-docs, from the routes and DTOs rather than from a table anyone keeps by
// hand. Two audiences read it. A person opens the page, expands a route and sends a request without writing a
// client — which is the only way to look at what a route answers short of curl. An agent reads
// /api/openapi/v1.json and gets the paths, the verbs and the shapes it may post, so what the studio accepts
// is discoverable from the studio instead of from prose that nothing verifies.
//
// The XML documentation files carry each endpoint's own <summary> into the document, which is why the
// generated page reads as the endpoint's docstring rather than as a list of paths.
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = doc =>
    {
        doc.DocumentName = "v1";
        doc.Title = "pgm-studio";
        doc.Version = "v1";
        doc.Description =
            "The studio's whole surface. Every route is anonymous and rooted at /api. A refusal answers "
            + "{error, message, findings[]} whichever route raised it — GET /api/rules explains any rule id "
            + "a finding carries. docs/tools/flow.md is the map over the four levels a map is described at.";
    };
    // Group by the first path segment under the prefix — the resource the route is about: `map`, `plan`,
    // `terrain`, `room-styles`. The segment after it is a route parameter across most of the surface and
    // would tag half the page `{Slug}`.
    o.AutoTagPathSegmentIndex = 1;
    o.ShortSchemaNames = true;
    // The generator reads a string enum off its converter attribute but takes the naming policy from the
    // options, so without this the document lists Severity as `Refusal`/`Complaint` while the wire writes
    // `refusal`/`complaint` — a schema a generated client would fail against.
    o.SerializerSettings = json =>
        json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    // A field that takes one of a closed set of words publishes them, rather than crossing as a bare string
    // an agent has to learn by being refused one. PgmStudio.Api.Endpoints.WordSetSchemas says how.
    o.DocumentSettings += doc => doc.SchemaSettings.SchemaProcessors.Add(new WordSetSchemas());
    // One key rides on every success that has one, written by middleware rather than by any record — so the
    // document says so once, here. PgmStudio.Api.Endpoints.ComplaintChannel says how.
    o.DocumentSettings += doc => doc.OperationProcessors.Add(new ComplaintChannel());
    // A route that answers a picture says how to ask for one, beside the media type that says it can.
    // PgmStudio.Api.Endpoints.PngQuery says how.
    o.DocumentSettings += doc => doc.OperationProcessors.Add(new PngQuery());
    // And a route that reads a word off the request rather than binding it says which words, and what each
    // takes. PgmStudio.Api.Endpoints.QueryWords says how.
    o.DocumentSettings += doc => doc.OperationProcessors.Add(new QueryWords());
    // Keyed on the tag exactly as the generator emits it, which title-cases the path segment.
    o.TagDescriptions = tags =>
    {
        tags["Map"] = "One map, at every level it is described at: the document and its entities, the plan, "
                    + "the sketch, the intent, the analysis reads over its ground, and the world and map.xml "
                    + "it exports to.";
        tags["Maps"] = "The map list, and what originates one.";
        // Six routes take a posted plan and answer three different kinds of thing, which is what a caller
        // reading six summaries in a row is otherwise left to work out.
        tags["Plan"] = "The board as cell rectangles, posted and answered without storing anything — the loop "
                     + "a plan is iterated in. Six routes take one, and they answer three kinds of thing. "
                     + "One TRANSFORMS: compile turns a plan into the layout and intent a map is built from, "
                     + "and is the only one of the five a caller acts on. Two JUDGE: evaluate scores the "
                     + "board against the rule law, and feasibility is a studio diagnostic reporting the "
                     + "composer's own limits rather than anything about the board. Three PROJECT: inspect "
                     + "derives the geometry a canvas draws, columns builds the world the plan would make, "
                     + "and ascii draws the board as characters — the one read that shows a relation between "
                     + "two rectangles, and the one a caller with no image viewer can act on.";
        tags["Plans"] = "The stored plan library, the generator's kept candidates included.";
        tags["Diagnostics"] = "What the studio can say about itself rather than about a map. Read these to "
                            + "find out what the tool cannot do yet; a finding here is never a fault in the "
                            + "document that was posted, and acting on one as though it were means editing a "
                            + "board to satisfy a limitation that is the studio's.";
        tags["Compose"] = "The layout generator: roll a board from a player count, a symmetry and a seed.";
        tags["Shapes"] = "The shape vocabulary a composed board is built from.";
        tags["Terrain"] = "Materials, themes, patterns, and the previews over them.";
        tags["Themes"] = "The terrain-theme library, shared across every map.";
        tags["Room-Styles"] = "The room-style library — what a wool cage or a spawn hall is built out of.";
        tags["Rules"] = "Every rule the studio can cite, with what it means and what to do about it: the "
                      + "answer to an id carried by any finding.";
        tags["Configure"] = "The Configure wizard's own state.";
        tags["Objectives"] = "The objective vocabulary a map may state.";
        tags["Sketch"] = "Originating a map from a drawing rather than from a plan.";
        tags["Styles"] = "The terrain-style library — a stack of materials a theme's bucket paints with.";
        tags["Storey-Styles"] = "House-part library: one storey of a building, its wall and its openings.";
        tags["Roof-Styles"] = "House-part library: a roof's form, pitch and materials.";
        tags["Porch-Styles"] = "House-part library: what stands in front of a door.";
        tags["Minecraft"] = "The block palette and what the studio knows about each block.";
        tags["Health"] = "Liveness.";
    };
});

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
        // A caller that hung up is not a fault: there is nothing to report and nobody to answer. Reported as
        // one it would be a stack trace per abandoned request — and the sketch tool abandons one on every
        // edit that outruns its autosave — burying the faults this envelope exists for.
        if (PgmStudio.Api.Http.ClientDisconnect.Explains(ctx, ex))
        {
            ctx.RequestServices.GetRequiredService<ILoggerFactory>()
               .CreateLogger("PgmStudio.Api.Unhandled")
               .LogDebug("{Method} {Path} was abandoned by its caller", ctx.Request.Method, ctx.Request.Path);
            return;
        }

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

// And the mirror of that guarantee on the two severities that do not stop the work: a complaint or a decline
// an endpoint's gate produced rides on whatever success the endpoint answers, put there here rather than left
// to each endpoint to remember. A dropped one fails nothing, so nothing but this can catch it
// (docs/refusals.md, "What a success carries").
app.Use(PgmStudio.Api.Endpoints.Complaints.CarryAsync);

// All API endpoints live under /api.
app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api";
    // A discriminated union declares nothing above its discriminator: every field a material body carries
    // belongs to the leaf its `kind` names, so TerrainMaterial has no property of its own and the generator
    // reads that as an empty request. The schema it renders is complete either way — the `kind` mapping over
    // all fourteen leaves — so refusing the declaration would drop the one route whose body IS the union.
    c.Endpoints.AllowEmptyRequestDtos = true;
    c.Endpoints.Configurator = ep =>
    {
        // A field the DTO declares non-nullable and the body did not carry is refused by name before any
        // handler reads it — see RequiredFields for why the annotation alone does not hold.
        ep.PreProcessor<PgmStudio.Api.Endpoints.RequiredFields>(Order.Before);
        // And the two answers every route can give whatever else it does, so the document says so once
        // rather than leaving each endpoint to remember: a document that will not read is 400 and the
        // studio's own fault is 500, both in the envelope the middleware above guarantees.
        ep.Description(b => b
            .Produces<PgmStudio.Contracts.RefusalDto>(400, "application/json")
            .Produces<PgmStudio.Contracts.RefusalDto>(500, "application/json"));
    };
    // Query/route/form values bind through the ambient culture unless told otherwise; this makes the
    // wire boundary itself invariant rather than leaning on the process-wide pin above.
    c.Binding.UseInvariantNumbers();
    // A binder that refuses is a gate like any other, and answers the one envelope docs/refusals.md
    // describes: RQ1 per field, named, so a caller parses a binding failure with the same reader it
    // already has for a gate's. The binder's own {statusCode, message, errors} shape is the one thing on
    // the surface a caller would need a second parser for.
    c.Errors.UseRefusalEnvelope();
});

// The generated document and the page over it. Both are served from the app's own assets — nothing is
// fetched at view time — and they are registered before the SPA fallback so it does not claim them.
app.UseSwaggerGen(
    c => c.Path = "/api/openapi/{documentName}.json",
    c =>
    {
        c.Path = "/api-docs";
        c.DocumentPath = "/api/openapi/v1.json";
        c.DocExpansion = "list";
    });

// SPA fallback: anything not matched by an API route or a static file serves the Blazor host page.
app.MapFallbackToFile("index.html");

// The library's built-in presets. Idempotent and keyed by name — a row already there is updated in place and
// keeps the id maps and themes depend on, and nothing is ever deleted — so a studio nobody has run the seeder
// against stops being a state the app can be in. A failure here is reported and never fatal: an empty library
// is a usable studio, and refusing to serve over one would be worse than opening on it.
await using (var seeding = app.Services.CreateAsyncScope())
{
    var seed = new PgmStudio.Api.Services.LibrarySeed(
        seeding.ServiceProvider.GetRequiredService<PgmStudio.Data.Theme.ThemeStore>(),
        seeding.ServiceProvider.GetRequiredService<PgmStudio.Data.Theme.RoomStyleStore>(),
        seeding.ServiceProvider.GetRequiredService<PgmStudio.Data.Theme.HousePartStore>());
    try
    {
        var tally = await seed.SeedAsync();
        app.Logger.LogInformation(
            "library seeded: {StylesAdded} style(s), {RoomsAdded} part(s) and house(s), {ThemesAdded} theme(s) added",
            tally.StylesAdded, tally.RoomsAdded, tally.ThemesAdded);
    }
    catch (Exception fault)
    {
        app.Logger.LogWarning(fault, "the library could not be seeded; it opens on whatever is stored");
    }
}

app.Run();

public partial class Program; // exposed for WebApplicationFactory in tests
