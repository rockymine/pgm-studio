using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Components;

/// <summary>What fills a slot: one block, a saved pattern, or nothing.</summary>
public readonly record struct SlotFill(long StyleId, SlotBlockDto? Block)
{
    public static SlotFill None => new(0, null);

    /// <summary>Whether anything fills the slot.</summary>
    public bool Bound => StyleId != 0 || Block is not null;

    /// <summary>What fills the slot, as a name: the block's, or the pattern's. Null where nothing does, or the
    /// pattern is not in <paramref name="styles"/>.</summary>
    public string? Name(IReadOnlyList<StyleDto> styles, IReadOnlyList<PaintBlockDto> blocks)
    {
        var id = StyleId;
        return Block is { } block
            ? blocks.FirstOrDefault(offered => offered.Id == block.Id && offered.Data == block.Data)?.Name
              ?? $"block {block.Id}:{block.Data}"
            : styles.FirstOrDefault(style => style.Id == id)?.Name;
    }
}

/// <summary>
/// Fills one slot with a block or a pattern. A single block is not a pattern and is never saved as one, so
/// every place a material is bound asks the same question of the same two lists — which block, or which of the
/// library's patterns — and the grouping, the unbound row and the picture of what is bound are decided here
/// rather than at each site.
/// </summary>
public partial class SlotSelect
{
    [Inject] public MaterialSchema Schema { get; set; } = default!;

    [Parameter] public IReadOnlyList<StyleDto> Styles { get; set; } = [];

    /// <summary>The blocks the picker offers.</summary>
    [Parameter] public IReadOnlyList<PaintBlockDto> Blocks { get; set; } = [];

    /// <summary>The bound pattern's row id; 0 where a block fills the slot or nothing does.</summary>
    [Parameter] public long Value { get; set; }

    /// <summary>The block filling the slot, or null where a pattern does or nothing does.</summary>
    [Parameter] public SlotBlockDto? Block { get; set; }

    [Parameter] public EventCallback<SlotFill> ValueChanged { get; set; }

    /// <summary>What the unbound row says — what this part does when nothing is bound to it. Null offers no
    /// unbound row, for a slot that is always filled.</summary>
    [Parameter] public string? Unbound { get; set; } = "Unbound";

    /// <summary>What the part is, on hover.</summary>
    [Parameter] public string? Title { get; set; }

    [Parameter] public bool Disabled { get; set; }

    /// <summary>What the row carries after the swatch — an extent, a way to remove the binding.</summary>
    [Parameter] public RenderFragment? Trailing { get; set; }

    private const string BlockKey = "block";

    /// <summary>The block a slot takes the moment a block is chosen: stone, which is what unpainted ground
    /// already is.</summary>
    private static readonly SlotBlockDto Stone = new(1, 0, Laid: false);

    private IReadOnlyList<SelectOption> Rows =>
    [
        new SelectOption(BlockKey, "A single block"),
        .. Styles.Select(style => new SelectOption(
            style.Id.ToString(), style.Name, Group: Schema.NameOf(style.Kind))),
    ];

    private string SelectedKey => Block is not null ? BlockKey : Value.ToString();

    private StyleDto? Bound => Block is null ? Styles.FirstOrDefault(style => style.Id == Value) : null;

    /// <summary>Whether a block has an axis to lay: the two log blocks.</summary>
    private static bool Lays(int id) => id is 17 or 162;

    private Task Picked(string value) => value == BlockKey
        ? ValueChanged.InvokeAsync(new SlotFill(0, Block ?? Stone))
        : ValueChanged.InvokeAsync(new SlotFill(long.TryParse(value, out var id) ? id : 0, null));

    private Task PickBlock(PaintBlockDto picked)
        => ValueChanged.InvokeAsync(new SlotFill(0,
            new SlotBlockDto(picked.Id, picked.Data, Lays(picked.Id) && (Block?.Laid ?? false))));

    private Task SetLaid(ChangeEventArgs change)
        => Block is { } block
            ? ValueChanged.InvokeAsync(new SlotFill(0, block with { Laid = change.Value is true }))
            : Task.CompletedTask;
}
