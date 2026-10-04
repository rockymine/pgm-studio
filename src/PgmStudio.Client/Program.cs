using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PgmStudio.Client;

// The UI is English-only and its coordinate inputs are plain `<input type="number">`, whose value is
// always dot-decimal per the HTML spec. Blazor WASM otherwise adopts the browser's culture, so under a
// comma-decimal locale a fractional value (e.g. a 0.5 symmetry centre or spawn point) renders as "0,5" —
// invalid for a number input (the field blanks) — and the invariant DOM string mis-parses back. Pin the
// culture to invariant so every numeric field round-trips fractional coordinates consistently.
var invariant = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = invariant;
CultureInfo.DefaultThreadCurrentUICulture = invariant;
CultureInfo.CurrentCulture = invariant;
CultureInfo.CurrentUICulture = invariant;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<PgmStudio.Client.Components.TerrainLibraryClient>();
// Who the browser is signed in as, asked once per page load; the shell opens a page read-only from it.
builder.Services.AddScoped<PgmStudio.Client.Components.StudioAccess>();
// Loaded once and shared: the material schema is a build constant, so every editor asks the same answer.
builder.Services.AddScoped<PgmStudio.Client.Components.MaterialSchema>();
// The rules and scored terms the server can cite, asked once and shared by the rules page and every check list.
builder.Services.AddScoped<PgmStudio.Client.Components.RuleBook>();

await builder.Build().RunAsync();
