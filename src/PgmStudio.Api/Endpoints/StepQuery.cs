using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Endpoints;

/// <summary>The <c>step</c> query word the posted ground reads share: how far down the modifier stack the ground
/// is read.</summary>
internal static class StepQuery
{
    public static readonly QueryWord Word = new("step",
        "Read the ground after the first N modifiers that are not switched off; 0 is the shapes alone. Absent "
        + "reads the whole stack.", Min: 0);

    /// <summary>The posted layout cut to the stack's first <paramref name="step"/> steps, or as it stands where
    /// no step was asked for.</summary>
    public static string Cut(string layoutJson, int? step) =>
        step is { } asked ? SketchSteps.Through(layoutJson, Math.Max(0, asked)) : layoutJson;
}
