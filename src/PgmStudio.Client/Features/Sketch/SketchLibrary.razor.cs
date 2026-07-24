using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Features.Sketch;

public partial class SketchLibrary
{
    [Parameter] public IReadOnlyList<LibraryItem> Items { get; set; } = [];
    [Parameter] public EventCallback<string> OnArm { get; set; }
}
