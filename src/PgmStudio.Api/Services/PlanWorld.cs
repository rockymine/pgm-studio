using System.Text.Json;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Services;

/// <summary>
/// The four lines that stand between a posted plan document and the pair a world is built from. Every
/// whole-map preview of a plan starts here — the painted render and the 3-D columns both — and they must start
/// the same way, because a preview compiled differently from the export is a picture of a map nobody will
/// play.
/// </summary>
internal static class PlanWorld
{
    /// <summary>The layout (serialized as the sketch endpoints read it) and intent a plan compiles to, or
    /// <c>null</c> when the document cannot be read at all. A plan that parses but is incoherent still
    /// compiles here — the structural gate is <c>/plan/compile</c>'s, and a preview draws what it is given.</summary>
    public static (string LayoutJson, MapIntent Intent)? Compile(string planJson)
    {
        var plan = PlanModel.Parse(planJson);
        if (plan is null) return null;
        var (layout, intent) = PlanCompiler.Compile(plan);
        return (JsonSerializer.Serialize(layout, SketchLayout.Json), intent);
    }
}
