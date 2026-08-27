using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Library;

/// <summary>
/// One row of an editor's outline: a piece of the document that can be picked, and what it says about itself
/// without being opened. <paramref name="Depth"/> indents a nested piece — a material's bands, a house's
/// storeys — so a nest reads as a tree rather than as boxes inside boxes.
/// </summary>
/// <param name="Id">What <c>Selected</c> matches, and what the editor switches its fields on.</param>
/// <param name="Title">The row's label.</param>
/// <param name="Icon">The lucide glyph, where the row has no colour to show instead.</param>
/// <param name="Badge">What the piece states at a glance — bound, a count, a form.</param>
/// <param name="Swatch">A CSS colour standing in for the glyph, where the row resolves to one.</param>
/// <param name="Depth">How far in the row sits; 0 is a top-level piece.</param>
public sealed record EditorPart(
    string Id, string Title, string Icon = "dot", string? Badge = null, string? Swatch = null, int Depth = 0);

/// <summary>
/// The frame every library editor is laid out in — the outline, the fields beside it and the preview
/// companion. It owns the name, the outline and where each column sits; each kind supplies its own outline,
/// picture and fields.
///
/// <para><see cref="Nests"/> is the one thing that differs between kinds. A nesting document — a material's
/// bands inside a stack's layers, a house's parts — draws the node the outline has picked, under a header
/// naming it. A flat one draws every section at once, and the outline scrolls to one instead of choosing
/// which exists: hiding eleven of a theme's fourteen controls behind a click buys nothing a document that
/// fits on a screen needs.</para>
/// </summary>
public partial class LibraryEditor
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public LibraryKind Kind { get; set; } = default!;
    [Parameter] public string Name { get; set; } = "";
    [Parameter] public EventCallback<string> NameChanged { get; set; }

    [Parameter] public IReadOnlyList<EditorPart> Outline { get; set; } = [];
    [Parameter] public string? Selected { get; set; }
    [Parameter] public EventCallback<string> OnSelect { get; set; }

    /// <summary>The picture every field is judged by.</summary>
    [Parameter] public RenderFragment? Preview { get; set; }

    /// <summary>The views the preview offers, as chips over it.</summary>
    [Parameter] public RenderFragment? Views { get; set; }

    /// <summary>What the view is taken over, in the dock at the foot of the stage — the sample a building is
    /// drawn on. A different question from which view, so a different bar.</summary>
    [Parameter] public RenderFragment? Dock { get; set; }

    /// <summary>What the picture is showing, under it.</summary>
    [Parameter] public string? Footnote { get; set; }

    /// <summary>The fields of the picked outline row.</summary>
    [Parameter] public RenderFragment? Fields { get; set; }

    /// <summary>Save, copy and forget.</summary>
    [Parameter] public RenderFragment? Actions { get; set; }

    /// <summary>What the last save or refusal said.</summary>
    [Parameter] public string? Note { get; set; }

    /// <summary>Whether the document nests. A nesting one is edited a node at a time; a flat one lays every
    /// section out at once and the outline becomes a way to reach one rather than a way to choose it.</summary>
    [Parameter] public bool Nests { get; set; } = true;

    private EditorPart SelectedPart =>
        Outline.FirstOrDefault(part => part.Id == Selected) ?? Outline.FirstOrDefault() ?? new("", Kind.Title);

    private string DocCls => Nests ? "lib-doc" : "lib-doc lib-doc--sheet";

    private Task OnNameInput(ChangeEventArgs e) => NameChanged.InvokeAsync(e.Value as string ?? "");

    private static string Indent(EditorPart part) => part.Depth == 0 ? "" : $"padding-left:{8 + part.Depth * 14}px";

    /// <summary>Picking a row selects it either way; on a flat document it also brings the section into view,
    /// because there the row names something already drawn rather than something to draw instead.</summary>
    private async Task Choose(string id)
    {
        await OnSelect.InvokeAsync(id);
        if (!Nests) await JS.InvokeVoidAsync("studio.scrollIntoView", SectionAnchor(id));
    }

    /// <summary>The element id a flat editor gives the section a row reaches — one spelling, so the row and
    /// the section it scrolls to cannot disagree about it.</summary>
    public static string SectionAnchor(string partId) => $"lib-section-{partId}";

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");
}
