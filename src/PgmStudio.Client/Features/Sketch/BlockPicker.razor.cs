using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The block chooser behind every solid material in the Theme rail. A block is an (id, data) pair, and the
/// three sixteen-colour families are the only place data carries meaning here — so the list offers each block
/// id once and the shade is picked from a swatch row rather than hunted for among forty-eight dropdown lines.
/// <see cref="ShowColors"/> is off where the shade is not the author's to set: a team tint takes it from the
/// team that owns the cell.
/// </summary>
public partial class BlockPicker
{
    [Parameter] public IReadOnlyList<PaintBlockDto> Blocks { get; set; } = [];
    [Parameter] public int Id { get; set; }
    [Parameter] public int Data { get; set; }
    [Parameter] public bool ShowColors { get; set; } = true;
    [Parameter] public EventCallback<PaintBlockDto> OnPick { get; set; }

    /// <summary>One option per block id — a sixteen-shade family collapses to a single line.</summary>
    private IEnumerable<IGrouping<string, PaintBlockDto>> Groups =>
        Blocks.GroupBy(b => b.Id).Select(shades => shades.First()).GroupBy(b => b.Group);

    /// <summary>The chosen block's shades, when it has more than one — what the swatch row offers.</summary>
    private IReadOnlyList<PaintBlockDto>? Family
    {
        get
        {
            var shades = Blocks.Where(b => b.Id == Id).ToList();
            return shades.Count > 1 ? shades : null;
        }
    }

    private PaintBlockDto? Current =>
        Blocks.FirstOrDefault(b => b.Id == Id && b.Data == Data) ?? Blocks.FirstOrDefault(b => b.Id == Id);

    private string Hex => Current?.Hex ?? "#555555";
    private string Name => Current?.Name ?? $"Block {Id}";
    private string SelectedKey => Id.ToString();

    private static string Key(PaintBlockDto block) => block.Id.ToString();

    // A family reads as its family name; everything else as the block's own.
    private bool IsFamily(PaintBlockDto block) => Blocks.Count(b => b.Id == block.Id) > 1;
    private string OptionLabel(PaintBlockDto block) => IsFamily(block) ? block.Group : block.Name;

    private Task ChooseBlock(ChangeEventArgs e)
    {
        if (!int.TryParse((string?)e.Value, out var id)) return Task.CompletedTask;
        // hold the shade when the new block has one to match, so switching clay → wool keeps the colour
        var pick = Blocks.FirstOrDefault(b => b.Id == id && b.Data == Data) ?? Blocks.FirstOrDefault(b => b.Id == id);
        return pick is null ? Task.CompletedTask : Pick(pick);
    }

    private Task Pick(PaintBlockDto block) => OnPick.InvokeAsync(block);
}
