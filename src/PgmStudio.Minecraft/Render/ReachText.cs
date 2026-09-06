namespace PgmStudio.Minecraft.Render;

/// <summary>
/// Which standing ground no player can get to, as the lines a reader acts on.
///
/// <para>It is deliberately <b>not</b> a fault. An island drawn to be looked at, a side platform players
/// spawn on and never leave, a shelf standing above the build ceiling: each is ground an author meant, and a
/// board carrying one is not wrong. What this answers is where that ground is and why it is out of reach, so
/// whoever holds the board can say whether they meant it — which is a question only they can answer, and one
/// nothing else in the studio puts in front of them.</para>
///
/// <para>The partition is <see cref="TraversabilityRender"/>'s own: the same navigable components the
/// picture is coloured by, minus the one the board is played on, minus every component a marker sits on, and
/// minus every component the map opens to bridging. Sharing the computation is the point — a picture that
/// says a patch is a second colour and a reading that says nobody can reach it must not be able to
/// disagree.</para>
/// </summary>
public static class ReachText
{
    /// <summary>The reading, or a line saying the board has none. Never null: a board every player can cross
    /// is an answer rather than an absence, and a caller that has to tell an empty reading from a failed one
    /// by the length of the body is a caller that will get it wrong.</summary>
    public static string Render(TraversabilityRender.Result read, int? maxBuildHeight)
    {
        var lines = new List<string>
        {
            $"components {read.ComponentCount} · navigable {read.NavigableCount} columns"
            + $" · bridgeable {read.BridgeableCount}"
            + (maxBuildHeight is { } ceiling ? $" · build ceiling y{ceiling}" : " · no build ceiling stated"),
            "",
        };

        if (read.OutOfReach.Count == 0)
        {
            lines.Add("every patch of standing ground is either walked to from the main board, carries a spawn "
                      + "or an objective, or is opened to bridging by a build zone.");
            return string.Join("\n", lines);
        }

        lines.Add($"{read.OutOfReach.Count} patch(es) of standing ground no player can get to. None of this is "
                  + "a fault — scenery and a side observer island read exactly like this — but a shape stranded "
                  + "by accident reads like it too, and only the author can tell them apart.");
        lines.Add("");
        lines.Add("  cells  floor  reason          box");

        foreach (var patch in read.OutOfReach)
            lines.Add($"  {patch.Cells,5}  {"y" + patch.Floor,5}  {patch.Reason,-14}  "
                      + $"x {patch.MinX}..{patch.MaxX}, z {patch.MinZ}..{patch.MaxZ}");

        lines.Add("");
        lines.Add("  no-build-zone   nothing the map opens to bridging reaches it, and it is not walked to");
        lines.Add("  above-ceiling   its ground stands over the map's maxbuildheight and cannot be built up to");
        return string.Join("\n", lines);
    }

    /// <summary>The one line a driver prints beside the file it wrote — the whole reading reduced to what
    /// decides whether anyone opens it.</summary>
    public static string Summary(TraversabilityRender.Result read)
        => read.OutOfReach.Count == 0
            ? "reach: every patch reachable"
            : $"reach: {read.OutOfReach.Count} patch(es) out of reach, "
              + $"{read.OutOfReach.Sum(patch => patch.Cells)} column(s), largest at "
              + $"({read.OutOfReach[0].MinX}, {read.OutOfReach[0].MinZ})";
}
