using LucideBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace PgmStudio.Client.Components;

/// <summary>A lucide glyph by its kebab-case name, rendered as an inline <c>&lt;svg class="lucide lucide-NAME"&gt;</c>.
/// The glyph is 16px at a 1.5 stroke; the stylesheet sizes every one through <c>svg.lucide</c>, so the width and
/// height attributes only hold the box until it lands. An extra <c>class</c> joins the lucide classes, and the
/// glyph is <c>aria-hidden</c> unless the caller gives it an accessible name. A name lucide does not know renders
/// an empty glyph and logs an error, which the e2e smoke sweep fails a page on.</summary>
public sealed class Icon : LucideIcon
{
    [Inject] private ILogger<Icon> Logger { get; set; } = default!;

    public Icon()
    {
        Size = 16;
        StrokeWidth = 1.5;
    }

    /// <summary>The class and attributes are derived from what the caller passed on this render, so the
    /// derived values of the last one are cleared before the new parameters land.</summary>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ClassName = null;
        AdditionalAttributes = null;
        return base.SetParametersAsync(parameters);
    }

    protected override void OnParametersSet()
    {
        var attributes = AdditionalAttributes is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(AdditionalAttributes);
        var ownClass = attributes.Remove("class", out var given) ? given?.ToString() : null;
        ClassName = string.IsNullOrWhiteSpace(ownClass) ? $"lucide lucide-{Name}" : $"lucide lucide-{Name} {ownClass}";

        var named = attributes.Keys.Any(key => key.StartsWith("aria-", StringComparison.Ordinal) || key is "role" or "title");
        if (!named) attributes["aria-hidden"] = "true";
        AdditionalAttributes = attributes;
    }

    protected override string SvgContent
    {
        get
        {
            var markup = base.SvgContent;
            if (markup.Length == 0) Logger.LogError("lucide has no icon named \"{Name}\"", Name);
            return markup;
        }
    }
}
