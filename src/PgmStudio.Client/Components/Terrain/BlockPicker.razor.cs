using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Components;

/// <summary>
/// The block chooser behind every solid material. A block is an (id, data) pair, and the pair means two
/// different things depending on the block: for the sixteen-colour families (wool, stained clay, stained glass)
/// the data value is a <em>shade</em> of one block, so the list offers the family once and the shade is a swatch
/// click rather than a hunt through forty-eight dropdown lines; everywhere else it names a <em>different</em>
/// block (andesite is stone, 1:5), so those are listed under their own names.
/// <para>The offered list is a curated shortlist, not the set of legal blocks — a material may name any id — so
/// the pair itself stays editable underneath, and a block the list does not carry is typed instead of being
/// unreachable.</para>
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

    /// <summary>The fewest shades of one id that reads as a colour family rather than a set of blocks. The
    /// stained families carry sixteen; the widest variant family (stone) carries seven.</summary>
    private const int FamilyShades = 8;

    /// <summary>One option per offered block, with a colour family collapsed to a single line.</summary>
    private IEnumerable<IGrouping<string, PaintBlockDto>> Groups =>
        Blocks.Where(block => !IsFamily(block) || block.Data == 0)
              .GroupBy(block => block.Group);

    /// <summary>The chosen block's shades, when it belongs to a colour family — what the swatch row offers.</summary>
    private IReadOnlyList<PaintBlockDto>? Family
    {
        get
        {
            var shades = Blocks.Where(block => block.Id == Id).ToList();
            return shades.Count >= FamilyShades ? shades : null;
        }
    }

    private PaintBlockDto? Current => Blocks.FirstOrDefault(block => block.Id == Id && block.Data == Data);

    // A block off the offered list still shows something: its family's colour where it has one, else the base
    // block's, else the neutral chip. The material's own preview is what shows its true colour.
    private PaintBlockDto? Nearest => Current ?? Blocks.FirstOrDefault(block => block.Id == Id);

    private string Hex => Nearest?.Hex ?? "#555555";
    private string Name => Current?.Name ?? (Nearest is null ? $"Block {Id}:{Data}" : $"{Nearest.Name} {Id}:{Data}");

    /// <summary>Whether the chosen pair is one the list does not offer — it gets an option of its own so the
    /// select still shows what is set instead of silently reading as some other block.</summary>
    private bool IsOffList => Current is null;

    private string SelectedKey => Current is null ? Key(Id, Data) : Key(Current.Id, FamilyBase(Current));

    private static string Key(int id, int data) => $"{id}:{data}";
    private static string Key(PaintBlockDto block) => Key(block.Id, block.Data);

    // A colour family is chosen as its base entry; the shade comes from the swatch row.
    private int FamilyBase(PaintBlockDto block) => IsFamily(block) ? 0 : block.Data;

    private bool IsFamily(PaintBlockDto block) => Blocks.Count(other => other.Id == block.Id) >= FamilyShades;
    private string OptionLabel(PaintBlockDto block) => IsFamily(block) ? block.Group : block.Name;

    private Task ChooseBlock(ChangeEventArgs e)
    {
        var parts = ((string?)e.Value ?? "").Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var id) || !int.TryParse(parts[1], out var data))
            return Task.CompletedTask;
        // A colour family holds the shade already set, so switching clay → wool keeps the colour.
        var keepShade = Blocks.Count(block => block.Id == id) >= FamilyShades
            ? Blocks.FirstOrDefault(block => block.Id == id && block.Data == Data)
            : null;
        var pick = keepShade ?? Blocks.FirstOrDefault(block => block.Id == id && block.Data == data);
        return pick is null ? Task.CompletedTask : Pick(pick);
    }

    private Task SetId(ChangeEventArgs e) => SetPair(Parse(e, Id), Data);
    private Task SetData(ChangeEventArgs e) => SetPair(Id, Math.Clamp(Parse(e, Data), 0, 15));

    // A hand-typed pair may name a block the list does not carry, so the picked value is synthesized rather
    // than looked up — the shortlist is an offer, not a constraint.
    private Task SetPair(int id, int data)
        => id == Id && data == Data
            ? Task.CompletedTask
            : Pick(Blocks.FirstOrDefault(block => block.Id == id && block.Data == data)
                   ?? new PaintBlockDto(id, data, $"Block {id}:{data}", "Custom", Hex));

    private static int Parse(ChangeEventArgs e, int fallback)
        => int.TryParse((string?)e.Value, out var value) && value >= 0 ? value : fallback;

    private Task Pick(PaintBlockDto block) => OnPick.InvokeAsync(block);
}
