using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>
/// Binds one saved style. Everywhere a style is bound asks the same question of the same list — which of the
/// library's materials does this part resolve through — so the grouping, the unbound row and the picture of
/// what is bound are decided here rather than at each site.
/// </summary>
public partial class StyleSelect
{
    [Inject] public MaterialSchema Schema { get; set; } = default!;

    [Parameter] public IReadOnlyList<StyleDto> Styles { get; set; } = [];

    /// <summary>The bound style's row id; 0 is unbound.</summary>
    [Parameter] public long Value { get; set; }
    [Parameter] public EventCallback<long> ValueChanged { get; set; }

    /// <summary>What the unbound row says — what this part does when nothing is bound to it.</summary>
    [Parameter] public string Unbound { get; set; } = "Unbound";

    /// <summary>What the part is, on hover.</summary>
    [Parameter] public string? Title { get; set; }

    [Parameter] public bool Disabled { get; set; }

    /// <summary>What the row carries after the swatch — an extent, a way to remove the binding.</summary>
    [Parameter] public RenderFragment? Trailing { get; set; }

    private IReadOnlyList<SelectOption> Rows =>
    [
        .. Styles.Select(style => new SelectOption(
            style.Id.ToString(), style.Name, Group: Schema.NameOf(style.Kind))),
    ];

    private StyleDto? Bound => Styles.FirstOrDefault(style => style.Id == Value);

    private Task Picked(string value)
        => ValueChanged.InvokeAsync(long.TryParse(value, out var id) ? id : 0);
}
