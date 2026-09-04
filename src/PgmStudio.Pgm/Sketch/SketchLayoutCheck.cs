using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// What a sketch layout says that the build cannot honour.
///
/// <para>Most of it is read from the document alone, before any ground is realized. Four findings measure
/// the ground the document rasterizes to instead — a stack drawn where a stack cannot go (<c>SK9</c>), two
/// layers claiming one column's blocks (<c>SK10</c>), a mass nothing joins to the board (<c>SK11</c>), and a
/// shape drawn over ground a subtract takes away (<c>SK13</c>) — because none of the four is visible in what
/// the document says, only in what it builds.</para>
///
/// <para>The rasterizer is set algebra over shapes, so a shape it cannot read contributes no ground rather
/// than failing: a kind nobody has, a polygon of two vertices and a circle of no radius each rasterize to
/// nothing, and a mirror mode nobody has fans the board onto itself, which leaves a map that states two
/// halves standing on one. Every one of those builds, and the author or the agent that wrote it is told
/// nothing — the picture simply has less in it than the document asked for. That silence is what this
/// answers: the findings are <b>complaints</b>, because the board did build, and each names the field that
/// named nothing.</para>
///
/// <para><b>Two things here refuse.</b> A board's cost is paid per column of its <em>extent</em>, drawn or
/// not, so an extent past <see cref="SketchRules.MaxBoardColumns"/> does not fail slowly — it takes the
/// machine with it. That is measured across the symmetry orbit, because a shape far out on one side widens
/// the board by twice its distance, and it is measured here rather than inside the rasterizer so a caller is
/// refused before the first column is walked. And a shape that <em>fills</em> ground a subtract takes away is
/// refused, because a subtract is the board's statement of its own negative space: what a body encircles is
/// ground players go round and a board's walls are drawn to guard, so it may be redrawn but never papered
/// over. An add that draws nothing there is the same rule's other half and only complains.</para>
/// </summary>
public static class SketchLayoutCheck
{
    /// <summary>Every placement naming a recipe the document's registry has no entry for, as the placement's
    /// own id and the key it named.
    ///
    /// <para>Read off the raw JSON rather than the typed document, because a layout carries its dressing as
    /// an opaque element — the model lives in <c>Minecraft</c>, which this project does not see — and because
    /// what is being asked is a question about two <em>names</em> and needs nothing else parsed.</para></summary>
    private static IEnumerable<(string Subject, string Key)> UnstatedRecipes(SketchLayout layout)
    {
        if (layout.Dressing is not { ValueKind: JsonValueKind.Object } dressing) yield break;
        if (!dressing.TryGetProperty("props", out var props) || props.ValueKind != JsonValueKind.Array) yield break;

        var stated = new HashSet<string>(StringComparer.Ordinal);
        if (dressing.TryGetProperty("styles", out var styles) && styles.ValueKind == JsonValueKind.Object)
            foreach (var entry in styles.EnumerateObject()) stated.Add(entry.Name);

        var index = 0;
        foreach (var prop in props.EnumerateArray())
        {
            var at = index++;
            if (prop.ValueKind != JsonValueKind.Object) continue;
            // Only the clicked kinds name a recipe. A stroke's `style` is the word for its edge — worn,
            // rough, stepping stones — and names nothing in the registry.
            if (!prop.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String
                || kind.GetString() is not ("tree" or "boulder" or "house")) continue;
            if (prop.TryGetProperty("style", out var style) && style.ValueKind == JsonValueKind.String
                && style.GetString() is { Length: > 0 } key && !stated.Contains(key))
            {
                var id = prop.TryGetProperty("id", out var stated_id) && stated_id.ValueKind == JsonValueKind.String
                    ? stated_id.GetString() ?? $"#{at}" : $"#{at}";
                yield return (id, key);
            }
        }
    }

    /// <summary>The shape kinds the rasterizer draws (<c>SketchRasterizer.RingOf</c>). Anything else rings
    /// empty, which is the same board as a shape that was never drawn.</summary>
    private static readonly string[] Kinds = ShapeKinds.All;

    /// <summary>The symmetry modes <see cref="Symmetry"/> knows. An unknown one is not refused there — it
    /// answers order 2 and the identity transform — so a board asking for one is built unmirrored.</summary>
    private static readonly string[] Modes =
        ["none", "mirror_x", "mirror_z", "mirror_d1", "mirror_d2", "rot_90", "rot_180"];

    /// <summary>The world's own height, restated here because <c>Pgm</c> cannot see
    /// <c>VoxelWorld.MaxHeight</c>, and pinned to it by <c>SketchLayoutCheckPinTests</c> in the one test
    /// project that sees both.</summary>
    public const int WorldHeight = 256;

    /// <summary>Read a layout as posted. A body that is not a layout at all is not this gate's to report —
    /// that is the request's own fault (<c>RQ1</c>), answered where the body is read.</summary>
    public static Findings Check(string layoutJson) => Check(SketchLayout.Stated(layoutJson));

    /// <summary>
    /// Whether a board carries any finish at all — a theme registry, a relief, or props — and which of the
    /// three it does not, or null where it carries at least one.
    ///
    /// <para>Asked at the <b>finish</b> rather than in <see cref="Check(SketchLayout?)"/>, because a board
    /// mid-draw has every right to be bare and only finishing declares the drawing done. That is the same
    /// reason <c>SK6</c> and <c>SK7</c> live at that stage: it is the last point where what a board does not
    /// have can still be said.</para>
    /// </summary>
    public static Finding? Unfinished(SketchLayout? layout)
    {
        if (layout is null) return null;

        var absent = new List<string>();
        if (layout.Themes is not { Count: > 0 }) absent.Add("no theme registry, so every column paints the built-in finish");
        if (layout.Relief is not { Count: > 0 }) absent.Add("no relief, so the ground is as flat as the shapes stated it");
        if (!HasProps(layout)) absent.Add("nothing placed on it — no tree, boulder, path or building");
        if (absent.Count < 3) return null;

        return new Finding(SketchRules.NoFinish,
            "the board is finished carrying no finish: " + string.Join("; ", absent)
            + ". A board of bare ground is a legitimate one, so this stops nothing — but it exports as raw "
            + "stone, and nothing later says so",
            Severity.Complaint);
    }

    /// <summary>Whether the dressing document holds a prop. The document is carried as an opaque snapshot, so
    /// this reads the one key the pass reads rather than deserializing a shape this project does not own.
    /// </summary>
    private static bool HasProps(SketchLayout layout) =>
        layout.Dressing is { ValueKind: JsonValueKind.Object } dressing
        && dressing.TryGetProperty("props", out var props)
        && props.ValueKind == JsonValueKind.Array
        && props.GetArrayLength() > 0;

    public static Findings Check(SketchLayout? layout)
    {
        if (layout is null) return Findings.None;

        // SK2 first and alone: a board's cost is paid per column of its extent, so a board past the ceiling
        // must be refused before anything walks one. Read off the shapes' own boxes across the symmetry
        // orbit, which costs a pass over the document and no ground at all.
        if (TooLarge(layout) is { } oversized) return new List<Finding> { oversized };

        var findings = new List<Finding>();

        // SK9 — a layer holds one span per column, so a second one drawn over the first is not in the world.
        foreach (var (layerId, lost, kept) in SketchRasterizer.StackedInOneLayer(layout))
            findings.Add(new Finding(SketchRules.StackedInOneLayer,
                $"'{lost}' and '{kept}' stack over the same ground on layer '{layerId}', and a layer holds "
                + $"one span per column — the world keeps '{kept}' and '{lost}' is not in it. Move '{kept}' "
                + "to its own layer, or clamp walls around the lower shape rather than drawing over it",
                Severity.Decline, Subjects: [lost, kept]));

        // SK10 — the stack is what puts air between two slabs, so a pair whose spans meet builds as one mass.
        foreach (var (lower, upper, courses, x, z, cells) in SketchRasterizer.OverlappingLayerSpans(layout))
            findings.Add(new Finding(SketchRules.LayersOverlap,
                $"layers '{lower}' and '{upper}' are driven {courses} block(s) into each other over {cells} "
                + $"column(s) — deepest at ({x}, {z}) — so they build as one solid mass where they meet and "
                + $"the gap between the two layers is not in the world there. Raise the base_y of '{upper}', "
                + "or lower what stands on it",
                Severity.Complaint, Subjects: [lower, upper]));

        // SK20 — the list order and base_y disagree about which layer is on top. Read over the plain layers
        // only: a made thing's slices are a way of holding one sculpture, not a stack, and SK18's exemption
        // is the same one.
        foreach (var (lower, upper) in OutOfOrder(layout))
            findings.Add(new Finding(SketchRules.StackOutOfOrder,
                $"layer '{upper}' is drawn after '{lower}' and its ground starts below it, so the list order "
                + "and base_y disagree about which is on top. The world is built from base_y and comes out "
                + $"as stated; the document is what reads wrong. Move '{upper}' before '{lower}', or correct "
                + "its base_y",
                Severity.Complaint, Subjects: [lower, upper]));

        // SK16 — a made thing that asked for the ground and found none. The board builds where it was drawn.
        foreach (var (thing, cells) in SketchRasterizer.SeatedOnNothing(layout))
            findings.Add(new Finding(SketchRules.SeatedOnNothing,
                $"'{thing}' seats on the ground and none of its {cells} column(s) has any under it, so it "
                + "stands at the height it was drawn. Move it over ground, or take its `seat` off",
                Severity.Complaint, Subjects: [thing]));

        // SK19 — a placement naming a recipe the document does not state. A refusal rather than a complaint:
        // every read of the dressing refuses it anyway, so a document carrying one is one no world can be
        // built from. The save still stores it and says so, because a save that fails halfway through
        // authoring is worse than a board with a fault in it; the finish is where it stops.
        foreach (var (subject, key) in UnstatedRecipes(layout))
            findings.Add(new Finding(SketchRules.RecipeNotStated,
                $"placement '{subject}' names the recipe '{key}', which this document's dressing does not "
                + "state — pull it into `dressing.styles` under that key, or name one the registry has",
                Severity.Refusal, Field: "dressing", Subjects: [subject]));

        // SK11 — ground with sky over it and no way onto it. Roofed ground is a room and stays silent.
        foreach (var (places, x, z, y) in SketchRasterizer.DetachedMasses(layout))
            findings.Add(new Finding(SketchRules.MassUnreached,
                $"{places} place(s) of standable ground around ({x}, {z}) @{y} have open sky over them and no "
                + "route onto them from the rest of the board — draw the way up, or leave it if a detached "
                + "group is what this is",
                Severity.Complaint));

        // SK14 — a relief solves a surface over every column of its group, so an override add that does not
        // stand out of that field builds to the field rather than to the top it stated.
        foreach (var (shape, layerId, groupId, top) in SketchRasterizer.ReliefOverridesStatedTop(layout))
            findings.Add(new Finding(SketchRules.ReliefOverStatedTop,
                $"'{shape}' on layer '{layerId}' is an override add stating a top of y{top}, and group "
                + $"'{groupId}' carries a relief that solves a surface through it — the world builds it to "
                + "whatever the relief says. Give it \"height_mode\": \"level\" with \"skirt\": 0 to hold "
                + "the top it states, or \"relief_scope\": \"exclude\" to keep its ground out of the solve",
                Severity.Complaint, Subjects: [shape]));

        // SK15 — the taller add wins the column and the smaller one wins the paint, so where the smaller is
        // also the shorter the world holds one shape's ground in another's material.
        foreach (var (layerId, built, painted, builtTheme, paintedTheme, cells, x, z) in
                 SketchRasterizer.PaintedByAnotherShape(layout))
            findings.Add(new Finding(SketchRules.PaintedByAnotherShape,
                $"'{built}' builds {cells} column(s) on layer '{layerId}' that '{painted}' paints — from "
                + $"({x}, {z}). The taller shape wins the ground and the smaller wins the theme, so what "
                + $"stands there is '{built}' finished in '{paintedTheme}' rather than '{builtTheme}'. Cut "
                + $"'{painted}' out of '{built}'s footprint, or give the two one theme",
                Severity.Complaint, Subjects: [built, painted]));

        // SK13 — a subtract states the board's negative space, and an add over one is silent either way it
        // lands: it draws nothing, or it puts the ground back.
        foreach (var (add, addLayer, subtract, subtractLayer, survives, cells, x, z) in
                 SketchRasterizer.AddsOverSubtracts(layout))
            findings.Add(new Finding(SketchRules.DrawnOverSubtraction,
                survives
                    ? $"'{add}' fills {cells} column(s) that '{subtract}' takes away — from ({x}, {z}) — so "
                      + $"the negative space the board states there is ground in the world. "
                      + (addLayer == subtractLayer
                          ? "An override add beats a subtract on its own layer"
                          : $"'{add}' is on layer '{addLayer}' and the subtract on '{subtractLayer}', and a "
                            + "subtract reaches only the layer it is on")
                    : $"'{add}' draws nothing over {cells} column(s) — from ({x}, {z}) — because '{subtract}' "
                      + "takes them away, and a subtract beats every plain add on its layer whatever order "
                      + "the two are written in. The shape is on the canvas and not in the world",
                survives ? Severity.Refusal : Severity.Complaint, Subjects: [add, subtract]));

        var mode = layout.Setup?.MirrorMode ?? "rot_180";
        double centerX = layout.Setup?.Center?.Cx ?? 0, centerZ = layout.Setup?.Center?.Cz ?? 0;

        if (!Modes.Contains(mode))
            findings.Add(new Finding(SketchRules.NamesNothing,
                $"the board states mirror mode '{mode}', which is not a mode the studio knows, so it is "
                + "built unmirrored — every shape stands once, on one side",
                Severity.Complaint, Field: "setup.mirror_mode"));

        var shapeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (shape, where) in Shapes(layout))
        {
            if (shape.Id.Length > 0) shapeIds.Add(shape.Id);
            var kind = shape.Type ?? "";

            if (!Kinds.Contains(kind))
            {
                findings.Add(new Finding(SketchRules.NamesNothing,
                    $"{Named(shape)} states kind '{kind}', which is not a kind the studio draws — it has "
                    + $"{Kinds.Length} ({string.Join(", ", Kinds)}) — so it draws no ground",
                    Severity.Complaint, Field: $"{where}.type", Subjects: Ids(shape)));
            }
            else if (Empty(shape) is { } why)
            {
                findings.Add(new Finding(SketchRules.DrawsNothing,
                    $"{Named(shape)} {why}, so it draws no ground",
                    Severity.Complaint, Field: where, Subjects: Ids(shape)));
            }

            if (Unbuildable(shape) is { } height)
                findings.Add(new Finding(SketchRules.UnbuildableHeight,
                    $"{Named(shape)} {height}, and the world is {WorldHeight} blocks tall — the column is cut "
                    + "to fit rather than built as stated",
                    Severity.Complaint, Field: where, Subjects: Ids(shape)));

        }

        var groups = new HashSet<string>(SketchLayout.GroupIds(layout), StringComparer.Ordinal);
        foreach (var (group, where) in Groups(layout))
            foreach (var named in group.ShapeIds.Where(id => !shapeIds.Contains(id)))
                findings.Add(new Finding(SketchRules.NamesNothing,
                    $"group '{group.Id}' lists shape '{named}', which the layout does not carry",
                    Severity.Complaint, Field: $"{where}.shapeIds",
                    Subjects: group.Id is { Length: > 0 } id ? [id] : null));

        // SK17 — a shape no group lists. The fan is read off each mirroring group's shapeIds, so a shape no
        // list names is built where it was drawn and nowhere else. Only where the board fans at all, and
        // never for a layer stating no groups (the whole of that layer mirrors) or for a role-tagged room
        // piece (never listed, by design). A shape drawing nothing is SK4's to report.
        if (Symmetry.OrbitAxes(mode).Length > 0)
            foreach (var (layer, index) in SketchLayout.Stack(layout).Select((layer, at) => (layer, at)))
            {
                if (layer.Groups.Count == 0) continue;
                var listed = new HashSet<string>(layer.Groups.SelectMany(group => group.ShapeIds), StringComparer.Ordinal);
                foreach (var (shape, at) in layer.Shapes.Select((shape, at) => (shape, at)))
                {
                    if (shape.Role is not null || shape.Id.Length == 0 || listed.Contains(shape.Id)) continue;
                    if (!Kinds.Contains(shape.Type ?? "") || Empty(shape) is not null) continue;
                    findings.Add(new Finding(SketchRules.ShapeInNoGroup,
                        $"'{shape.Id}' on layer '{layer.Id}' is in none of the layer's {layer.Groups.Count} "
                        + "group(s), and the symmetry orbit is fanned per group — the shape is built once, "
                        + "where it was drawn, and has no image on the other side. Its group's relief and "
                        + "keep-clear go with the list, so it takes neither. List it in the group whose "
                        + "ground it is part of",
                        Severity.Complaint, Field: $"layers[{index}].layout.shapes[{at}]", Subjects: [shape.Id]));
                }
            }

        // SK12 — one id, two groups. The relief is stored under the id and so is a placement's group, so a
        // board carrying it twice has no single answer to either.
        foreach (var group in Groups(layout).Select(entry => entry.Group)
                                             .Where(group => group.Id is { Length: > 0 })
                                             .GroupBy(group => group.Id!, StringComparer.Ordinal)
                                             .Where(group => group.Count() > 1)
                                             .OrderBy(group => group.Key, StringComparer.Ordinal))
            findings.Add(new Finding(SketchRules.GroupIdTwice,
                $"{group.Count()} groups answer to the id '{group.Key}', so terrain and placements stored "
                + "under it have no single group to belong to — the first one solved takes them and the "
                + "rest build flat. Give each group its own id",
                Severity.Complaint, Subjects: [group.Key]));

        foreach (var orphan in (layout.Relief ?? []).Keys.Where(key => !groups.Contains(key)).OrderBy(key => key, StringComparer.Ordinal))
            findings.Add(new Finding(SketchRules.NamesNothing,
                $"a relief is stated for group '{orphan}', which the layout does not carry, so that "
                + "elevation is not built",
                Severity.Complaint, Field: $"relief.{orphan}"));

        // A landform outside the four words is not a word this reads, and the gate that would have judged the
        // ground against it (RL1) skips a relief that states nothing — so a typo or the wrong case turns that
        // gate off rather than failing it. Same rule as every other name matching nothing.
        foreach (var (id, word) in (layout.Relief ?? [])
                     .Where(entry => entry.Value?.Landform is { Length: > 0 } stated && !Landform.IsKnown(stated))
                     .Select(entry => (entry.Key, entry.Value!.Landform!))
                     .OrderBy(entry => entry.Key, StringComparer.Ordinal))
            findings.Add(new Finding(SketchRules.NamesNothing,
                $"group '{id}' says its ground is '{word}', which is not one of the {Landform.All.Length} "
                + $"landforms ({string.Join(", ", Landform.All)}) — so nothing measures the ground against "
                + "it and the relief reads as one stating no landform at all",
                Severity.Complaint, Field: $"relief.{id}.landform", Subjects: [id]));

        // A theme scope resolves shape → map default, and a shape naming a registry entry that is not there
        // falls all the way through to whatever the map default happens to be — which paints a board and says
        // nothing, exactly the silence the three names above are reported for. Reported once per name rather
        // than once per shape: a board that mistyped one key wants one sentence, not thirty.
        var themes = new HashSet<string>(
            (IEnumerable<string>?)layout.Themes?.Keys ?? [], StringComparer.Ordinal);
        var missing = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (shape, _) in Shapes(layout))
            if (shape.Theme is { Length: > 0 } named && !themes.Contains(named))
                (missing.TryGetValue(named, out var on) ? on : missing[named] = []).Add(shape.Id);
        foreach (var (named, on) in missing)
            findings.Add(new Finding(SketchRules.NamesNothing,
                $"{on.Count} shape{(on.Count == 1 ? " paints" : "s paint")} with theme '{named}', which the layout's "
                + $"registry does not carry{(themes.Count == 0 ? " (it states no themes at all)" : "")} — those "
                + "cells take the map default instead",
                Severity.Complaint, Field: "themes", Subjects: [.. on.Where(id => id.Length > 0)]));

        if (layout.MapTheme is { Length: > 0 } mapTheme && !themes.Contains(mapTheme))
            findings.Add(new Finding(SketchRules.NamesNothing,
                $"the map default is theme '{mapTheme}', which the layout's registry does not carry — every "
                + "cell no shape scope claims takes unthemed stone instead",
                Severity.Complaint, Field: "mapTheme"));

        return findings;
    }

    /// <summary>The one refusal, measured before any ground is walked: a board whose extent across the
    /// symmetry orbit is past <see cref="SketchRules.MaxBoardColumns"/>. A shape far out on one side widens
    /// the board by twice its distance, which is why the orbit is measured rather than the drawing. Null
    /// where the board fits, and for a board with no bounded shape in it at all.</summary>
    private static Finding? TooLarge(SketchLayout layout)
    {
        var mode = layout.Setup?.MirrorMode ?? "rot_180";
        double centerX = layout.Setup?.Center?.Cx ?? 0, centerZ = layout.Setup?.Center?.Cz ?? 0;
        var extent = (MinX: double.MaxValue, MinZ: double.MaxValue, MaxX: double.MinValue, MaxZ: double.MinValue);

        foreach (var (shape, _) in Shapes(layout))
        {
            if (Bounds(shape) is not { } box) continue;
            // The shape as drawn, then one image per orbit axis. Read through OrbitAxes rather than through
            // Symmetry.Point's k, whose image index only varies for rot_90 — a mirror's k=0 is already the
            // mirrored copy, so counting images that way loses the half the author actually drew.
            Cover(box);
            foreach (var axis in Symmetry.OrbitAxes(mode)) Cover(Turned(box, axis, centerX, centerZ));
        }

        if (extent.MaxX <= extent.MinX || Columns(extent) is not { } columns
            || columns <= SketchRules.MaxBoardColumns) return null;

        return new Finding(SketchRules.BoardTooLarge,
            $"the board spans {Span(extent.MaxX - extent.MinX)}×{Span(extent.MaxZ - extent.MinZ)} columns "
            + $"({columns:N0}) across its symmetry orbit, more ground than the studio will realize — draw "
            + "it smaller, or nearer the symmetry centre");

        void Cover((double MinX, double MinZ, double MaxX, double MaxZ) box)
            => extent = (Math.Min(extent.MinX, box.MinX), Math.Min(extent.MinZ, box.MinZ),
                         Math.Max(extent.MaxX, box.MaxX), Math.Max(extent.MaxZ, box.MaxZ));
    }

    // Every shape the layout carries, with the path to it — the layers a stacked sketch holds, and the
    // single top-level layout a legacy one does (the same two the group walk reads).
    private static IEnumerable<(SketchShape Shape, string Where)> Shapes(SketchLayout layout)
    {
        foreach (var (layer, index) in SketchLayout.Stack(layout).Select((layer, index) => (layer, index)))
            foreach (var (shape, at) in layer.Shapes.Select((shape, at) => (shape, at)))
                yield return (shape, $"layers[{index}].layout.shapes[{at}]");
    }

    private static IEnumerable<(SketchGroup Group, string Where)> Groups(SketchLayout layout)
    {
        foreach (var (layer, index) in SketchLayout.Stack(layout).Select((layer, index) => (layer, index)))
            foreach (var (group, at) in layer.Groups.Select((group, at) => (group, at)))
                yield return (group, $"layers[{index}].layout.groups[{at}]");
    }

    // Twice the signed area a ring encloses, absolute — the shoelace sum. Zero says the vertices are
    // collinear however many of them there are, which is the same "no ground" a rectangle of no width has.
    private static double Area(double[][] vertices)
    {
        var sum = 0.0;
        for (var i = 0; i < vertices.Length; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Length];
            if (a.Length < 2 || b.Length < 2) return 0;
            sum += (a[0] * b[1]) - (b[0] * a[1]);
        }
        return Math.Abs(sum);
    }

    // Why a shape of a known kind still draws nothing, or null where it draws something.
    private static string? Empty(SketchShape shape) => shape.Type switch
    {
        ShapeKinds.Polygon or ShapeKinds.Lasso => shape.Vertices is not { Length: >= 3 }
            ? $"is a {shape.Type} with {shape.Vertices?.Length ?? 0} vertices, under the three that enclose ground"
            : Area(shape.Vertices) > 0
                ? null
                : $"is a {shape.Type} of {shape.Vertices.Length} vertices enclosing no area — every point is on one line",
        ShapeKinds.Circle => shape.Radius > 0 ? null : $"is a circle of radius {shape.Radius ?? 0:0.##}",
        ShapeKinds.Polyline => shape.Radius > 0
            ? shape.Vertices is { Length: >= 2 } ? null : $"is a path of {shape.Vertices?.Length ?? 0} points"
            : $"is a path of width {shape.Radius ?? 0:0.##}",
        ShapeKinds.Rectangle => (shape.MaxX ?? 0) - (shape.MinX ?? 0) != 0 && (shape.MaxZ ?? 0) - (shape.MinZ ?? 0) != 0
            ? null
            : "is a rectangle with no area",
        _ => null,
    };

    // A column the world cannot hold, said as the fact that makes it one.
    private static string? Unbuildable(SketchShape shape)
    {
        double floor = shape.Floor ?? 0, top = floor + (shape.BaseHeight ?? 0);
        if (shape.BaseHeight < 0) return $"states a base_height of {shape.BaseHeight:0.##}, which is below nothing";
        if (floor < 0) return $"stands its floor at y={floor:0.##}";
        if (top >= WorldHeight) return $"reaches y={top:0.##}";
        return null;
    }

    // The ground a shape covers, before the orbit fans it — its own outline's bounding box.
    private static (double MinX, double MinZ, double MaxX, double MaxZ)? Bounds(SketchShape shape) => shape.Type switch
    {
        ShapeKinds.Rectangle => (Math.Min(shape.MinX ?? 0, shape.MaxX ?? 0), Math.Min(shape.MinZ ?? 0, shape.MaxZ ?? 0),
                        Math.Max(shape.MinX ?? 0, shape.MaxX ?? 0), Math.Max(shape.MinZ ?? 0, shape.MaxZ ?? 0)),
        ShapeKinds.Circle => Around(shape.CenterX ?? 0, shape.CenterZ ?? 0, Math.Abs(shape.Radius ?? 0)),
        ShapeKinds.Polygon or ShapeKinds.Lasso or ShapeKinds.Polyline => shape.Vertices is { Length: > 0 } vertices
            ? (vertices.Min(v => v[0]) - Reach(shape), vertices.Min(v => v[1]) - Reach(shape),
               vertices.Max(v => v[0]) + Reach(shape), vertices.Max(v => v[1]) + Reach(shape))
            : null,
        _ => null,
    };

    // One orbit image of a box: transform its four corners about the centre and re-bound, since a rotation
    // turns a rectangle into a new axis-aligned one.
    private static (double MinX, double MinZ, double MaxX, double MaxZ) Turned(
        (double MinX, double MinZ, double MaxX, double MaxZ) box, string axis, double cx, double cz)
    {
        (double X, double Z)[] corners =
        [
            Symmetry.Apply(box.MinX, box.MinZ, axis, cx, cz), Symmetry.Apply(box.MinX, box.MaxZ, axis, cx, cz),
            Symmetry.Apply(box.MaxX, box.MinZ, axis, cx, cz), Symmetry.Apply(box.MaxX, box.MaxZ, axis, cx, cz),
        ];
        return (corners.Min(c => c.X), corners.Min(c => c.Z), corners.Max(c => c.X), corners.Max(c => c.Z));
    }

    private static double Reach(SketchShape shape) => shape.Type == ShapeKinds.Polyline ? Math.Abs(shape.Radius ?? 0) : 0;

    private static (double, double, double, double) Around(double x, double z, double radius)
        => (x - radius, z - radius, x + radius, z + radius);

    // The columns an extent covers, saturating rather than overflowing: a board stated in millions of blocks
    // per side is refused for its size, not answered with a wrapped negative.
    private static double? Columns((double MinX, double MinZ, double MaxX, double MaxZ) extent)
    {
        var columns = (extent.MaxX - extent.MinX) * (extent.MaxZ - extent.MinZ);
        return double.IsFinite(columns) ? columns : double.MaxValue;
    }

    private static string Span(double side) => double.IsFinite(side) ? side.ToString("N0") : "∞";

    private static string Named(SketchShape shape) => shape.Id.Length > 0 ? $"shape '{shape.Id}'" : "a shape";

    private static IReadOnlyList<string>? Ids(SketchShape shape) => shape.Id.Length > 0 ? [shape.Id] : null;

    /// <summary>Consecutive pairs of plain layers whose base_y falls rather than rises, each named by the one
    /// that is out of place. Made layers are skipped rather than compared: a sculpture's slices share one
    /// footprint and have no stacking order between them.</summary>
    private static IEnumerable<(string Lower, string Upper)> OutOfOrder(SketchLayout layout)
    {
        var plain = SketchLayout.Stack(layout).Where(layer => !layer.IsMade).ToList();
        for (var i = 1; i < plain.Count; i++)
            if (plain[i].BaseY < plain[i - 1].BaseY)
                yield return (plain[i - 1].Id ?? "", plain[i].Id ?? "");
    }
}
