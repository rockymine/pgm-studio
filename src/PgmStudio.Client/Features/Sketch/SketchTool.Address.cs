using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>Where the author is, kept in the address: the phase by the word the rail shows it under and the step by
/// its name (<c>?phase=draw&amp;step=layout</c>), so a reload or a shared link opens where the author was.</summary>
public partial class SketchTool
{
    /// <summary>The step of the phase the address names, by the word the bar shows it under.</summary>
    [SupplyParameterFromQuery] public string? Step { get; set; }

    /// <summary>The word the address names each phase by, which is the rail's word for it.</summary>
    private static readonly Dictionary<string, string> PhaseWords = new()
    {
        ["info"] = "identity", ["draw"] = "draw", ["relief"] = "terraform", ["theme"] = "palette",
        ["dressing"] = "decoration", ["review"] = "review", ["history"] = "history",
    };

    /// <summary>Whether the phase and step the tool was opened at have been taken up. Until they have, nothing is
    /// written over the address, which is still being read.</summary>
    private bool addressTaken;

    private string? addressWritten;

    private string? StepWord => active switch
    {
        "info" => SketchInfoPhase.Steps[infoStep],
        "draw" => DrawSteps[drawStep],
        "review" => views.Placing ? ReviewSteps[0] : ReviewSteps[1],
        _ => null,
    };

    /// <summary>Write the phase and step on screen into the address, replacing the entry rather than adding one.</summary>
    private void WriteAddress()
    {
        if (!addressTaken || !PhaseWords.TryGetValue(active, out var phase)) return;
        var step = StepWord?.ToLowerInvariant();
        var written = $"{phase}/{step}";
        if (written == addressWritten) return;
        addressWritten = written;
        Nav.NavigateTo(Nav.GetUriWithQueryParameters(new Dictionary<string, object?> { ["phase"] = phase, ["step"] = step }),
            replace: true);
    }

    /// <summary>Open on the phase and step the address names: a step the caller does not have, such as Places for
    /// someone who is not an admin, opens on the phase's first step they do.</summary>
    private async Task TakeAddressAsync()
    {
        var phase = PhaseWords.FirstOrDefault(word => word.Value == Phase).Key;
        int Named(IReadOnlyList<string> steps)
        {
            for (var index = 0; index < steps.Count; index++)
                if (string.Equals(steps[index], Step, StringComparison.OrdinalIgnoreCase)) return index;
            return -1;
        }
        switch (phase)
        {
            case "info": infoStep = Math.Max(0, Named(SketchInfoPhase.Steps)); break;
            // The step already up is not stepped to again: that would put down a tool armed while the page opened.
            case "draw" when Named(DrawSteps) is var step and >= 0 && Math.Max(step, FirstDrawStep) != drawStep:
                await StepDraw(step);
                break;
            case "relief": await GoRelief(); break;
            case "theme": await GoTheme(); break;
            case "dressing": await GoDressing(); break;
            case "review":
                await GoReview();
                if (Named(ReviewSteps) == 1) await ShowNotes();
                break;
            case "history": await GoHistory(); break;
        }
        addressTaken = true;
        WriteAddress();
    }
}
