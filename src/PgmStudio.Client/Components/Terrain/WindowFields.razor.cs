using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>
/// The controls one window opening states — its form, what it is made of, where it sits and how often it
/// repeats — for whichever wall is being cut: a room's own, a storey style's, or the gable above them.
///
/// <para><b>The host block is the pair a window's material has to agree with.</b> A window set into a band of
/// its own carries that band's block, and <c>HS4</c> refuses a band in a material the window is not — so the
/// two are offered together rather than one being reachable and the other not.</para>
/// </summary>
public partial class WindowFields
{
    /// <summary>The opening being edited.</summary>
    [Parameter, EditorRequired] public RoomWindowDto Window { get; set; } = default!;

    /// <summary>The opening as the edit leaves it. The owner holds the style, so it writes the change.</summary>
    [Parameter] public EventCallback<RoomWindowDto> WindowChanged { get; set; }

    /// <summary>The block palette both pickers offer.</summary>
    [Parameter] public IReadOnlyList<PaintBlockDto> Blocks { get; set; } = [];

    /// <summary>What the sill number is measured from, which differs by where the wall is.</summary>
    [Parameter] public string SillTitle { get; set; } = "The course above the floor the opening starts at.";

    private bool Glazing => WindowForms.Canonical(Window.Form) != WindowForms.None;

    private Task Edit(RoomWindowDto window) => WindowChanged.InvokeAsync(window);

    /// <summary>Switching form carries the block with it only where the new form can use it — a lattice needs
    /// stairs and a band needs slabs, and a pane block turned into a stair facing is a solid patch of wall — so
    /// each form brings its own default block rather than inheriting the last one.</summary>
    private Task SetForm(string picked)
    {
        var form = WindowForms.Canonical(picked);
        return Edit(Window with { Form = form, Block = DefaultBlock(form), Data = 0 });
    }

    private static int DefaultBlock(string form) => form switch
    {
        WindowForms.StairLattice => 53,      // oak stairs
        WindowForms.SlabBanded => 126,       // wooden slab
        _ => 102,                            // glass pane
    };

    private Task PickBlock(PaintBlockDto block) => Edit(Window with { Block = block.Id, Data = block.Data });

    private Task PickHost(PaintBlockDto block)
        => Edit(Window with { HostBlock = block.Id, HostData = block.Data });

    /// <summary>Give the opening a band of its own, starting from the window's own material — which is what
    /// <c>HS4</c> asks for, so the first state the control offers is the one that passes.</summary>
    private Task HostBand() => Edit(Window with { HostBlock = Window.Block, HostData = Window.Data });

    private Task ClearHost() => Edit(Window with { HostBlock = -1, HostData = 0 });

    private Task SetSill(ChangeEventArgs e)
        => Edit(Window with { Sill = Math.Clamp(Parse(e, Window.Sill), 1, 16) });

    private Task SetWidth(ChangeEventArgs e)
        => Edit(Window with { Width = Math.Clamp(Parse(e, Window.Width), 1, 8) });

    private Task SetHeight(ChangeEventArgs e)
        => Edit(Window with { Height = Math.Clamp(Parse(e, Window.Height), 1, 8) });

    private Task SetSpacing(ChangeEventArgs e)
        => Edit(Window with { Spacing = Math.Clamp(Parse(e, Window.Spacing), 0, 16) });

    private static int Parse(ChangeEventArgs e, int fallback)
        => int.TryParse(e.Value?.ToString(), out var value) ? value : fallback;
}
