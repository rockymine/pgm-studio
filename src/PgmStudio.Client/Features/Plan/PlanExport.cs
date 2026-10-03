using System.Text;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Features.Plan;

/// <summary>The plan document as a request body, read off the bridge at the moment it is sent.</summary>
internal static class PlanExport
{
    public static async Task<HttpContent> BodyAsync(IJSObjectReference bridge)
        => new StringContent(await bridge.InvokeAsync<string>("exportJson"), Encoding.UTF8, "application/json");
}
