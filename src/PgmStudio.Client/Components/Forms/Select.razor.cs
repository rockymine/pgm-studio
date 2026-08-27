using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace PgmStudio.Client.Components;

/// <summary>
/// One thing a <see cref="Select"/> offers: the value it writes, the word it is offered under, what it is in
/// a sentence, and the heading it sits under where the list is grouped.
/// </summary>
/// <param name="Value">What the select writes when this row is picked.</param>
/// <param name="Label">The word the row is offered under.</param>
/// <param name="Note">What the row is, on hover — a sentence a bare option has nowhere to put.</param>
/// <param name="Group">The heading this row sits under, or null in an ungrouped list.</param>
public readonly record struct SelectOption(string Value, string Label, string? Note = null, string? Group = null);

/// <summary>
/// The studio's dropdown. Options are values rather than markup, so grouping, labelling and the note a row
/// carries are decided once here rather than at every site that offers a list.
/// </summary>
public partial class Select
{
    [Parameter] public IReadOnlyList<SelectOption> Options { get; set; } = [];

    /// <summary>The value currently written, as the option's own string.</summary>
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    /// <summary>A first row standing for "none of these" — what an unbound field offers.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>What <see cref="Placeholder"/> writes; the empty string unless a caller says otherwise.</summary>
    [Parameter] public string PlaceholderValue { get; set; } = "";

    /// <summary>What the control as a whole is, on hover.</summary>
    [Parameter] public string? Title { get; set; }

    [Parameter] public bool Disabled { get; set; }

    /// <summary>The narrower control a panel row uses, which is every row that shares its line with a number
    /// or a swatch.</summary>
    [Parameter] public bool Slim { get; set; } = true;

    private string Cls => Slim ? "field-input field-input--slim" : "field-input";

    private Task Changed(ChangeEventArgs e) => ValueChanged.InvokeAsync(e.Value as string ?? "");

    /// <summary>A closed set of words as options, each under the sentence that describes it.</summary>
    public static IReadOnlyList<SelectOption> Words(
        IEnumerable<string> words, Func<string, string> label, Func<string, string>? note = null)
        => [.. words.Select(word => new SelectOption(word, label(word), note?.Invoke(word)))];
}
