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

await builder.Build().RunAsync();
