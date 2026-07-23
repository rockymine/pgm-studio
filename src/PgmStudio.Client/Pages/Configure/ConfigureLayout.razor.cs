using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Pages.Configure;

public partial class ConfigureLayout
{
    [Parameter] public string? MapName { get; set; }
    [Parameter] public string Crumb { get; set; } = "Configure";
    [Parameter] public string ActivePhaseId { get; set; } = "";
    /// <summary>Highest phase index reached — phases past it are locked/dimmed. Use -1 to lock all (landing).</summary>
    [Parameter] public int Furthest { get; set; }

    [Parameter] public string PhaseIcon { get; set; } = "";
    [Parameter] public string PhaseTitle { get; set; } = "";
    [Parameter] public IReadOnlyList<string> Steps { get; set; } = Array.Empty<string>();
    [Parameter] public int CurrentStep { get; set; }
    /// <summary>Highest step the user may jump to via the flow bar — later steps are locked/dimmed.
    /// Default unlimited (every step is freely navigable).</summary>
    [Parameter] public int MaxStep { get; set; } = int.MaxValue;

    [Parameter] public bool BackEnabled { get; set; }
    [Parameter] public bool NextEnabled { get; set; } = true;
    [Parameter] public string NextLabel { get; set; } = "Next";
    /// <summary>Topbar save indicator text (Saved · Saving… · Unsaved); null/empty hides it (e.g. the
    /// landing, which has no save model).</summary>
    [Parameter] public string? SaveStatus { get; set; }

    [Parameter] public EventCallback<string> OnJumpPhase { get; set; }
    [Parameter] public EventCallback<int> OnStep { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }
    [Parameter] public EventCallback OnNext { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
