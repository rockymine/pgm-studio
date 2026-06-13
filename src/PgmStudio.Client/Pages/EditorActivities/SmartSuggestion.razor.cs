using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class SmartSuggestion
{
    /// <summary>Card title — should read like an intelligent prompt (e.g. "Smart Suggestion").</summary>
    [Parameter] public string Header { get; set; } = "Smart Suggestion";
    /// <summary>Lucide icon shown top-left; defaults to the sparkle.</summary>
    [Parameter] public string Icon { get; set; } = "sparkle";
    /// <summary>Optional neutral badge in the header (e.g. the detected symmetry "rot 90").</summary>
    [Parameter] public string? Badge { get; set; }
    [Parameter] public string AcceptLabel { get; set; } = "Accept";
    [Parameter] public string RejectLabel { get; set; } = "Dismiss";
    /// <summary>Disables both buttons while the accept action is in flight.</summary>
    [Parameter] public bool Busy { get; set; }
    [Parameter] public EventCallback OnAccept { get; set; }
    [Parameter] public EventCallback OnReject { get; set; }
    /// <summary>The card body — info text + preview supplied by the caller.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
