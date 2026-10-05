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
/// <summary>How deep a check reads. Eight of the sketch rules are read off the <b>rasterized spans</b> rather
/// than off the document — what stacks over what, what a layer's slab drives into, what is standable and
/// unreached — so answering them walks every column of the board's extent, which on a played-size board is
/// seconds rather than milliseconds.
///
/// <para><see cref="Document"/> is the reading a write takes: everything a pass over the JSON can answer, and
/// nothing that needs ground. <see cref="Ground"/> is both, and is what a read asking for the board's whole
/// state takes — <c>GET /map/{slug}/findings</c> and the finish. A caller taking the shallower one is told
/// which rules it did not run rather than left to read an absence.</para></summary>
public enum LayoutReading
{
    /// <summary>Both readings. Every rule the gate has.</summary>
    Ground,

    /// <summary>The document alone. <see cref="SketchLayoutCheck.GroundRules"/> are not answered.</summary>
    Document,
}

public static class SketchLayoutCheck
{
    /// <summary>The rules read off the rasterized spans, which a <see cref="LayoutReading.Document"/> check
    /// does not answer. Stated here, beside the code that skips them, because it is what the answer names to
    /// a caller who took the shallower reading. What the caller is actually handed is
    /// <c>SketchMaterialGate.GroundRules</c>, which is this list and the one rule that gate skips with them —
    /// a rule id in <c>Minecraft</c>, which this project does not see.</summary>
    public static readonly string[] GroundRules =
    [
        SketchRules.StackedInOneLayer, SketchRules.LayersOverlap, SketchRules.MassUnreached,
        SketchRules.DrawnOverSubtraction, SketchRules.ReliefOverStatedTop,
        SketchRules.ThemeHiddenUnderAnother, SketchRules.SeatedOnNothing,
        // Raised by the theme gate rather than here, because it needs the theme registry as well as the
        // ground; named here because this is the list a caller taking the shallower reading is handed, and a
        // rule left unwalked is unwalked whichever gate owns it.
        SketchRules.ThemeShowsOnlyItsEdge,
    ];

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
    public static Findings Check(string layoutJson, LayoutReading reading = LayoutReading.Ground) =>
        Check(SketchLayout.Stated(layoutJson), reading);

    /// <summary>
    /// Whether a board carries any finish at all — a theme registry, a relief, or props — and which of the
    /// three it does not, or null where it carries at least one.
    ///
    /// <para>Asked at the <b>finish</b> rather than in <see cref="Check(SketchLayout?, LayoutReading)"/>,
    /// because a board mid-draw has every right to be bare and only finishing declares the drawing done. That is the same
    /// reason <c>SK6</c> and <c>SK7</c> live at that stage: it is the last point where what a board does not
    /// have can still be said.</para>
    /// </summary>
    public static Finding? Unfinished(SketchLayout? layout)
    {
        if (layout is null) return null;

        var absent = new List<string>();
        if (layout.Themes is not { Count: > 0 }) absent.Add("no palettes");
        if (layout.Relief is not { Count: > 0 }) absent.Add("no terraform");
        if (!HasProps(layout)) absent.Add("no props");
        if (absent.Count < 3) return null;

        return new Finding(SketchRules.NoFinish,
            "the layout has " + string.Join(", ", absent.Take(absent.Count - 1)) + " and " + absent[^1],
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

    public static Findings Check(SketchLayout? layout, LayoutReading reading = LayoutReading.Ground)
    {
        if (layout is null) return Findings.None;

        // SK2 first and alone: a board's cost is paid per column of its extent, so a board past the ceiling
        // must be refused before anything walks one. Read off the shapes' own boxes across the symmetry
        // orbit, which costs a pass over the document and no ground at all.
        if (TooLarge(layout) is { } oversized) return new List<Finding> { oversized };

        var findings = new List<Finding>();

        // The ones read off the rasterized spans. Each walks every column of the board's extent, so they
        // are the whole cost of a check and the whole of what a write's reading leaves out.
        if (reading is LayoutReading.Ground)
        {
            // SK9 — a layer holds one span per column, so a second one drawn over the first is not in the world.
            foreach (var (layerId, lost, kept) in SketchRasterizer.StackedInOneLayer(layout))
                findings.Add(new Finding(SketchRules.StackedInOneLayer,
                    $"shapes '{lost}' and '{kept}' overlap on layer '{layerId}'",
                    Severity.Decline, Subjects: [lost, kept]));

            // SK25 — the band is one ring and a ring is filled even-odd, so a stroke that laps itself
            // cancels the lap and builds a hole where the two windings cross.
            foreach (var (layerId, shape, x, z) in SketchRasterizer.StrokesLappingThemselves(layout))
                findings.Add(new Finding(SketchRules.StrokeLapsItself,
                    $"the band of shape '{shape}' on layer '{layerId}' overlaps itself near ({x}, {z})",
                    Severity.Complaint, Subjects: [shape]));

            // SK26 — nothing says a shape is a flight, so the tilt is what is read: a climb whose end has
            // nowhere to arrive is walkable at every tread and is not a way up anything.
            foreach (var (layerId, shape, end, x, z, top, drop) in SketchRasterizer.FlightsEndingAtADrop(layout))
                findings.Add(new Finding(SketchRules.FlightEndsAtADrop,
                    $"shape '{shape}' on layer '{layerId}' ends its {end} end at ({x}, {z}) at course {top} "
                    + $"with ground {drop} blocks below its last tread, more than 1 block",
                    Severity.Complaint, Subjects: [shape]));

            // SK10 — the stack is what puts air between two slabs, so a pair whose spans meet builds as one mass.
            foreach (var (lower, upper, courses, x, z, cells) in SketchRasterizer.OverlappingLayerSpans(layout))
                findings.Add(new Finding(SketchRules.LayersOverlap,
                    $"layers '{lower}' and '{upper}' overlap over {Wording.Count(cells, "column")} and share "
                    + $"{Wording.Count(courses, "course")} at ({x}, {z}), more than 1 course",
                    Severity.Complaint, Subjects: [lower, upper]));

            // SK16 — a made thing that asked for the ground and found none. The board builds where it was drawn.
            foreach (var (thing, cells) in SketchRasterizer.SeatedOnNothing(layout))
                findings.Add(new Finding(SketchRules.SeatedOnNothing,
                    $"made thing '{thing}' seats on the ground and has no ground under any of its "
                    + Wording.Count(cells, "column"),
                    Severity.Complaint, Subjects: [thing]));

            // SK11 — ground with sky over it and no way onto it. Roofed ground is a room and stays silent.
            foreach (var (places, x, z, y) in SketchRasterizer.DetachedMasses(layout))
                findings.Add(new Finding(SketchRules.MassUnreached,
                    $"island of {places} places at ({x}, {y}, {z}) has open sky over it and no route onto it "
                    + "from the largest island",
                    Severity.Complaint));

            // SK14 — a relief solves a surface over every column of its group, so an override add that does not
            // stand out of that field builds to the field rather than to the top it stated.
            foreach (var (shape, layerId, groupId, top) in SketchRasterizer.ReliefOverridesStatedTop(layout))
                findings.Add(new Finding(SketchRules.ReliefOverStatedTop,
                    $"override add '{shape}' on layer '{layerId}' states a top at y{top}, and group "
                    + $"'{groupId}' states terraform that sets the top of the same columns",
                    Severity.Complaint, Subjects: [shape]));

            // SK15 — the taller add wins the column and the paint follows what forms the surface, so where the
            // smaller shape is also the shorter its theme lands on none of the ground the two share.
            foreach (var (layerId, standing, hidden, standingTheme, hiddenTheme, cells, x, z) in
                     SketchRasterizer.ThemesHiddenUnderAnother(layout))
                findings.Add(new Finding(SketchRules.ThemeHiddenUnderAnother,
                    $"override add '{hidden}' on layer '{layerId}' overlaps taller override add '{standing}' "
                    + $"over {Wording.Count(cells, "column")} from ({x}, {z}), and states palette '{hiddenTheme}' "
                    + $"where '{standing}' states '{standingTheme}'",
                    Severity.Complaint, Subjects: [standing, hidden]));

            // SK13 — a subtract states the board's negative space, and an add over one is silent either way it
            // lands: it draws nothing, or it puts the ground back.
            foreach (var (add, addLayer, subtract, subtractLayer, survives, cells, x, z) in
                     SketchRasterizer.AddsOverSubtracts(layout))
                findings.Add(new Finding(SketchRules.DrawnOverSubtraction,
                    survives
                        ? $"add '{add}' on layer '{addLayer}' fills {Wording.Count(cells, "column")} from ({x}, {z}) "
                          + $"that subtract '{subtract}' on layer '{subtractLayer}' takes away"
                        : $"add '{add}' on layer '{addLayer}' draws nothing over {Wording.Count(cells, "column")} "
                          + $"from ({x}, {z}) where subtract '{subtract}' on layer '{subtractLayer}' takes them away",
                    survives ? Severity.Refusal : Severity.Complaint, Subjects: [add, subtract]));
        }

        // SK20 — the list order and base_y disagree about which layer is on top. Read over the plain layers
        // only: a made thing's slices are a way of holding one sculpture, not a stack, and SK18's exemption
        // is the same one.
        foreach (var (lower, upper) in OutOfOrder(layout))
            findings.Add(new Finding(SketchRules.StackOutOfOrder,
                $"layer '{upper}' is listed after layer '{lower}' and has a lower `base_y`",
                Severity.Complaint, Subjects: [lower, upper]));

        // SK19 — a placement naming a recipe the document does not state. A refusal rather than a complaint:
        // every read of the dressing refuses it anyway, so a document carrying one is one no world can be
        // built from. The save still stores it and says so, because a save that fails halfway through
        // authoring is worse than a board with a fault in it; the finish is where it stops.
        foreach (var (subject, key) in UnstatedRecipes(layout))
            findings.Add(new Finding(SketchRules.RecipeNotStated,
                $"prop '{subject}' names the recipe '{key}', which the sketch does not have",
                Severity.Refusal, Field: "dressing", Subjects: [subject]));

        var mode = SketchLayout.MirrorModeOf(layout);
        double centerX = layout.Setup?.Center?.Cx ?? 0, centerZ = layout.Setup?.Center?.Cz ?? 0;

        if (!Modes.Contains(mode))
            findings.Add(new Finding(SketchRules.NamesNothing,
                $"the layout's `setup.mirror_mode` '{mode}' is not one of {string.Join(", ", Modes)}",
                Severity.Complaint, Field: "setup.mirror_mode"));

        var shapeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (shape, where) in Shapes(layout))
        {
            if (shape.Id.Length > 0) shapeIds.Add(shape.Id);
            var kind = shape.Type ?? "";

            if (!Kinds.Contains(kind))
            {
                findings.Add(new Finding(SketchRules.NamesNothing,
                    $"{Named(shape)} names the kind '{kind}', which is not one of {string.Join(", ", Kinds)}",
                    Severity.Complaint, Field: $"{where}.type", Subjects: Ids(shape)));
            }
            else if (Empty(shape) is { } why)
            {
                findings.Add(new Finding(SketchRules.DrawsNothing,
                    $"{Named(shape)} {why}",
                    Severity.Complaint, Field: where, Subjects: Ids(shape)));
            }

            if (PerVertexHeightUnread(shape) is { } unread)
                findings.Add(new Finding(SketchRules.PerVertexHeightUnread,
                    $"{Named(shape)} {unread}",
                    Severity.Complaint, Field: $"{where}.anchor_heights", Subjects: Ids(shape)));

            if (Unbuildable(shape) is { } height)
                findings.Add(new Finding(SketchRules.UnbuildableHeight,
                    $"{Named(shape)} {height}",
                    Severity.Complaint, Field: where, Subjects: Ids(shape)));

            // SK24 — a shape saying what paints it twice. A refusal rather than a complaint: the build has a
            // defined answer (the material, the narrower of the two), so the world is fine and the document is
            // the thing that is wrong, and a theme nothing reads is exactly the silence the rest of this class
            // exists to end.
            if (shape.Theme is not null && shape.Material is not null)
                findings.Add(new Finding(SketchRules.PaintStatedTwice,
                    $"{Named(shape)} states both palette '{shape.Theme}' and a material",
                    Severity.Refusal, Field: $"{where}.material", Subjects: Ids(shape)));

        }

        var groups = new HashSet<string>(SketchLayout.GroupIds(layout), StringComparer.Ordinal);
        foreach (var (group, _, where) in Groups(layout))
            foreach (var named in group.ShapeIds.Where(id => !shapeIds.Contains(id)))
                findings.Add(new Finding(SketchRules.NamesNothing,
                    $"group '{group.Id}' lists shape '{named}', which the layout does not carry",
                    Severity.Complaint, Field: $"{where}.shapeIds",
                    Subjects: group.Id is { Length: > 0 } id ? [id] : null));

        // SK17 — a shape no group lists. The fan is read off each mirroring group's shapeIds, so a shape no
        // list names is built where it was drawn and nowhere else. Only where the board fans at all, and
        // never for a layer stating no groups (the whole of that layer mirrors) or for a role-tagged room
        // piece (never listed, by design). A shape drawing nothing is SK4's to report, and a subtract taking
        // nothing away is nobody's: having no image is a fault about what a shape does to the world, so a
        // shape that does nothing to it has no fault to have (CutsNothing).
        if (Symmetry.OrbitAxes(mode).Length > 0)
            foreach (var (layer, index) in SketchLayout.Stack(layout).Select((layer, at) => (layer, at)))
            {
                if (layer.Groups.Count == 0) continue;
                var listed = new HashSet<string>(layer.Groups.SelectMany(group => group.ShapeIds), StringComparer.Ordinal);
                foreach (var (shape, at) in layer.Shapes.Select((shape, at) => (shape, at)))
                {
                    if (shape.Role is not null || shape.Id.Length == 0 || listed.Contains(shape.Id)) continue;
                    if (!Kinds.Contains(shape.Type ?? "") || Empty(shape) is not null) continue;
                    if (CutsNothing(shape, layer)) continue;
                    findings.Add(new Finding(SketchRules.ShapeInNoGroup,
                        $"shape '{shape.Id}' on layer '{layer.Id}' is listed in none of the layer's "
                        + Wording.Count(layer.Groups.Count, "group"),
                        Severity.Complaint, Field: $"layers[{index}].layout.shapes[{at}]", Subjects: [shape.Id]));
                }
            }

        // SK28 — a group that declines the fan while standing wholly inside one orbit image. The orbit is
        // fanned per group, so `mirrors: false` builds the group once. That is correct for a landmark on the
        // symmetry centre, which is already its own image, so the footprint decides rather than the flag: a
        // group whose bounds meet any of their images straddles the centre and is left alone, and one
        // disjoint from every image cannot be its own and is built for one team only.
        if (Symmetry.OrbitAxes(mode) is { Length: > 0 } orbit)
            foreach (var (layer, index) in SketchLayout.Stack(layout).Select((layer, at) => (layer, at)))
                foreach (var (group, at) in layer.Groups.Select((group, at) => (group, at)))
                {
                    if (group.Mirrors) continue;
                    var listed = new HashSet<string>(group.ShapeIds, StringComparer.Ordinal);
                    var boxes = layer.Shapes.Where(shape => listed.Contains(shape.Id))
                                            .Select(Bounds).OfType<(double MinX, double MinZ, double MaxX, double MaxZ)>()
                                            .ToList();
                    if (boxes.Count == 0) continue;
                    var body = (MinX: boxes.Min(b => b.MinX), MinZ: boxes.Min(b => b.MinZ),
                                MaxX: boxes.Max(b => b.MaxX), MaxZ: boxes.Max(b => b.MaxZ));
                    if (orbit.Select(axis => Turned(body, axis, centerX, centerZ)).Any(image => Meets(body, image)))
                        continue;
                    findings.Add(new Finding(SketchRules.BuiltOnOneImage,
                        $"group '{group.Id}' on layer '{layer.Id}' states "
                        + "`mirrors` false and touches none of its "
                        + Wording.Count(orbit.Length, "symmetry copy", "symmetry copies"),
                        Severity.Complaint, Field: $"layers[{index}].layout.groups[{at}].mirrors",
                        Subjects: group.Id is { Length: > 0 } id ? [id] : null));
                }

        // SK12 — one id, two groups. The relief is stored under the id and so is a placement's group, so a
        // board carrying it twice has no single answer to either. The layers are named because they decide
        // which way it goes wrong: within one layer the last of them takes the terrain and the rest build
        // flat, across two every one of them takes it over its own footprint.
        foreach (var group in Groups(layout).Where(entry => entry.Group.Id is { Length: > 0 })
                                             .GroupBy(entry => entry.Group.Id!, StringComparer.Ordinal)
                                             .Where(group => group.Count() > 1)
                                             .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var layers = group.Select(entry => entry.Layer).Distinct(StringComparer.Ordinal).ToList();
            findings.Add(new Finding(SketchRules.GroupIdTwice,
                $"{group.Count()} groups have the id '{group.Key}' on layer{(layers.Count == 1 ? "" : "s")} "
                + $"{Wording.Ids(layers)}, more than 1",
                Severity.Complaint, Subjects: [group.Key]));
        }

        foreach (var orphan in (layout.Relief ?? []).Keys.Where(key => !groups.Contains(key)).OrderBy(key => key, StringComparer.Ordinal))
            findings.Add(new Finding(SketchRules.NamesNothing,
                $"the terraform of the layout names group '{orphan}', which the layout does not have",
                Severity.Complaint, Field: $"relief.{orphan}"));

        // A landform outside the four words is not a word this reads, and the gate that would have judged the
        // ground against it (RL1) skips a relief that states nothing — so a typo or the wrong case turns that
        // gate off rather than failing it. Same rule as every other name matching nothing.
        foreach (var (id, word) in (layout.Relief ?? [])
                     .Where(entry => entry.Value?.Landform is { Length: > 0 } stated && !Landform.IsKnown(stated))
                     .Select(entry => (entry.Key, entry.Value!.Landform!))
                     .OrderBy(entry => entry.Key, StringComparer.Ordinal))
            findings.Add(new Finding(SketchRules.NamesNothing,
                $"group '{id}' names the landform '{word}', which is not one of {string.Join(", ", Landform.All)}",
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
                $"{on.Count} shape{(on.Count == 1 ? " names" : "s name")} palette '{named}', "
                + "which the layout does not have",
                Severity.Complaint, Field: "themes", Subjects: [.. on.Where(id => id.Length > 0)]));

        if (layout.MapTheme is { Length: > 0 } mapTheme && !themes.Contains(mapTheme))
            findings.Add(new Finding(SketchRules.NamesNothing,
                $"the layout's `mapTheme` names palette '{mapTheme}', which the layout does not have",
                Severity.Complaint, Field: "mapTheme"));

        findings.AddRange(PlateausPaintedApart(layout));

        return findings;
    }

    /// <summary>SK27 — every compiled component whose plateaus do not agree on what paints them. A plan
    /// component spanning several surfaces compiles to one shape per surface, each carrying the component's
    /// own name, so the set of shapes sharing a component name is one landform and a theme stated per shape
    /// paints it as several.
    ///
    /// <para>Reported per component and not per shape: the fix is one decision over the whole flight, and the
    /// shapes are named in the finding so the riser can be found on the canvas. A component whose plateaus all
    /// state the same paint — or state none, and so all take the map default — says nothing.</para></summary>
    private static IEnumerable<Finding> PlateausPaintedApart(SketchLayout layout)
    {
        var mapDefault = layout.MapTheme is { Length: > 0 } stated ? stated : "";

        foreach (var (layer, index) in SketchLayout.Stack(layout).Select((layer, index) => (layer, index)))
        {
            var plateaus = new SortedDictionary<string, List<(string Shape, Paint Paint, int Surface)>>(StringComparer.Ordinal);
            foreach (var shape in layer.Shapes)
            {
                if (Component(shape) is not ({ } anchor, var surface)) continue;
                var paint = shape.Material is not null ? new Paint(true, "")
                    : new Paint(false, shape.Theme is { Length: > 0 } named ? named : mapDefault);
                (plateaus.TryGetValue(anchor, out var steps) ? steps : plateaus[anchor] = [])
                    .Add((shape.Id, paint, surface));
            }

            foreach (var (anchor, steps) in plateaus)
            {
                if (steps.Count < 2) continue;
                var painted = steps.Select(step => step.Paint).Distinct().ToList();
                if (painted.Count < 2) continue;

                var climb = steps.OrderBy(step => step.Surface).ToList();
                yield return new Finding(SketchRules.PlateausPaintedApart,
                    $"island '{anchor}' on layer '{layer.Id}' has {steps.Count} plateaus from "
                    + $"surface {climb[0].Surface} to {climb[^1].Surface} with "
                    + $"{painted.Count} different paints, more than 1",
                    Severity.Complaint, Field: $"layers[{index}].layout.shapes",
                    Subjects: [.. climb.Select(step => step.Shape).Where(id => id.Length > 0)]);
            }
        }
    }

    /// <summary>What one plateau states it is painted with — a registry id, the empty id standing for the map
    /// default, or its own material instead of a theme. A shape holding both is <c>SK24</c>'s, so the two are
    /// exclusive here and the flag is enough to keep a material apart from a theme that shares its name.</summary>
    private readonly record struct Paint(bool OwnMaterial, string Theme);

    /// <summary>The plan component a compiled terrain shape belongs to and the surface it stands at, or null
    /// for a shape the compiler did not emit.
    ///
    /// <para>The compiler names a terrain shape <c>{component}-{surface}</c>, numbered past the first where
    /// one surface breaks into several patches, and gives it that surface as its thickness over a floor it
    /// states nothing about. So the component is read back by requiring the name's trailing number to be the
    /// shape's own <see cref="SketchShape.BaseHeight"/>: a drawn shape whose id happens to end in a number
    /// agrees with it only by coincidence, and a drawn shape sits on a stated <see cref="SketchShape.Floor"/>
    /// where a compiled one never does. The ringed form is tried second, so a component whose own name ends in
    /// a number keeps it.</para></summary>
    private static (string Anchor, int Surface)? Component(SketchShape shape)
    {
        if (shape.Type != ShapeKinds.Polygon || shape.Operation != "add") return null;
        if (shape.Role is not null || shape.Floor is not null || shape.Vertices is not { Length: > 0 }) return null;
        if (shape.BaseHeight is not { } thickness || thickness != Math.Floor(thickness)) return null;

        var surface = (int)thickness;
        return Trailing(shape.Id, surface) is { } anchor ? (anchor, surface)
            : Trailing(Trailing(shape.Id) ?? "", surface) is { } ringed ? (ringed, surface)
            : null;
    }

    /// <summary>The part of an id before its final <c>-number</c>, or null where it does not end in one. A
    /// number's own minus sign is part of it, so an id ending <c>--3</c> is that id cut before a surface of
    /// −3 rather than before a ring of 3. <paramref name="expected"/> is the number the caller is looking for;
    /// absent, any number matches, which is how a ring suffix is taken off before the surface underneath it is
    /// read.</summary>
    private static string? Trailing(string id, int? expected = null)
    {
        var cut = id.LastIndexOf('-');
        if (cut <= 0 || cut == id.Length - 1) return null;
        if (id[cut - 1] == '-') cut--;
        if (cut <= 0 || !int.TryParse(id.AsSpan(cut + 1), out var number)) return null;
        return expected is null || number == expected ? id[..cut] : null;
    }

    /// <summary>The one refusal, measured before any ground is walked: a board whose extent across the
    /// symmetry orbit is past <see cref="SketchRules.MaxBoardColumns"/>. A shape far out on one side widens
    /// the board by twice its distance, which is why the orbit is measured rather than the drawing. Null
    /// where the board fits, and for a board with no bounded shape in it at all.</summary>
    private static Finding? TooLarge(SketchLayout layout)
    {
        var mode = SketchLayout.MirrorModeOf(layout);
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
            $"the layout spans {Span(extent.MaxX - extent.MinX)} by {Span(extent.MaxZ - extent.MinZ)} columns "
            + $"across its symmetry copies, {columns:N0} columns, more than {SketchRules.MaxBoardColumns:N0} "
            + "columns");

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

    private static IEnumerable<(SketchGroup Group, string Layer, string Where)> Groups(SketchLayout layout)
    {
        foreach (var (layer, index) in SketchLayout.Stack(layout).Select((layer, index) => (layer, index)))
            foreach (var (group, at) in layer.Groups.Select((group, at) => (group, at)))
                yield return (group, layer.Id ?? $"layer{index}", $"layers[{index}].layout.groups[{at}]");
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
            ? $"has {Wording.Count(shape.Vertices?.Length ?? 0, "point")}, less than 3"
            : Area(shape.Vertices) > 0
                ? null
                : $"has {Wording.Count(shape.Vertices.Length, "point")} that enclose no area",
        ShapeKinds.Circle => shape.Radius > 0
            ? null
            : $"has a radius of {shape.Radius ?? 0:0.##} blocks",
        ShapeKinds.Polyline => shape.Radius > 0
            ? shape.Vertices is { Length: >= 2 }
                ? null
                : $"has {Wording.Count(shape.Vertices?.Length ?? 0, "point")}, less than 2"
            : $"has a radius of {shape.Radius ?? 0:0.##} blocks",
        ShapeKinds.Rectangle => (shape.MaxX ?? 0) - (shape.MinX ?? 0) != 0 && (shape.MaxZ ?? 0) - (shape.MinZ ?? 0) != 0
            ? null
            : "has no area",
        _ => null,
    };

    // A column the world cannot hold, said as the fact that makes it one.
    private static string? Unbuildable(SketchShape shape)
    {
        double floor = shape.Floor ?? 0, top = floor + (shape.BaseHeight ?? 0);
        if (shape.BaseHeight < 0) return $"has a `base_height` of {shape.BaseHeight:0.##} blocks, less than 0 blocks";
        if (floor < 0) return $"has a `floor` of {floor:0.##} blocks, less than 0 blocks";
        if (top >= WorldHeight) return $"has a top of {top:0.##} blocks, more than {WorldHeight - 1} blocks";
        return null;
    }

    // The ground a shape covers, before the orbit fans it — its own outline's bounding box.
    /// <summary>Whether a <b>subtract</b> takes nothing away, because no add on its layer reaches the ground
    /// it covers. A subtract states negative space and reaches only its own layer, so one over ground no add
    /// places removes nothing at any height — and a shape that removes nothing is the same silence
    /// <see cref="Empty"/> already gives a shape that draws nothing.
    ///
    /// <para>The case is ordinary rather than exotic. A compile declares a buffer over every enclosed void
    /// (<see cref="Plan.PlanVoids"/>) so a ring of pieces at one surface cannot fuse across its own hole; where
    /// the ring is at several surfaces the union never bridges the hole to begin with and the cut lands on
    /// nothing. It is then exactly the shape that goes ungrouped, since a regroup assigns a subtract by what
    /// it overlaps — so the shape with no effect is the one this rule would otherwise name.</para>
    ///
    /// <para>Answered on bounds and only where they are disjoint, which is sound in the direction it is
    /// used: boxes that do not overlap belong to shapes that do not either, whatever their outlines. Boxes
    /// that merely touch are disjoint — a cut whose edge runs along a piece's edge shares a line and no
    /// ground.</para></summary>
    private static bool CutsNothing(SketchShape shape, SketchLayer layer)
    {
        if (!string.Equals(shape.Operation, "subtract", StringComparison.Ordinal)) return false;
        if (Bounds(shape) is not { } cut) return false;

        foreach (var other in layer.Shapes)
        {
            if (ReferenceEquals(other, shape)) continue;
            if (string.Equals(other.Operation, "subtract", StringComparison.Ordinal)) continue;
            if (Bounds(other) is not { } add) continue;
            if (Math.Min(cut.MaxX, add.MaxX) > Math.Max(cut.MinX, add.MinX)
             && Math.Min(cut.MaxZ, add.MaxZ) > Math.Max(cut.MinZ, add.MinZ)) return false;
        }
        return true;
    }

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

    // Whether two boxes share any ground. Touching along an edge counts: a footprint that meets its own
    // image at the centre line straddles it, which is the case SK28 leaves alone.
    private static bool Meets(
        (double MinX, double MinZ, double MaxX, double MaxZ) a,
        (double MinX, double MinZ, double MaxX, double MaxZ) b) =>
        a.MinX <= b.MaxX && b.MinX <= a.MaxX && a.MinZ <= b.MaxZ && b.MinZ <= a.MaxZ;

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

    /// <summary>Why a stated <c>anchor_heights</c> is not read, or null where it is — the TIN is built over
    /// the shape's own ring, which only a polygon or a lasso has, and only where the array is the length of
    /// that ring. Read the same way <c>SketchRasterizer.HeightFn</c> decides it, so the two cannot disagree
    /// about which shapes vary.</summary>
    private static string? PerVertexHeightUnread(SketchShape shape)
    {
        if (shape.AnchorHeights is not { Length: > 0 } stated) return null;
        var least = shape.Type is ShapeKinds.Polyline ? 2 : 3;
        if (shape.Type is not (ShapeKinds.Polygon or ShapeKinds.Lasso or ShapeKinds.Polyline))
            return $"states {stated.Length} `anchor_heights` and is a {shape.Type ?? "shape"}";
        return shape.Vertices is { } vertices && vertices.Length >= least && vertices.Length == stated.Length
            ? null
            : $"states {stated.Length} `anchor_heights` and {Wording.Count(shape.Vertices?.Length ?? 0, "point")}, "
            + "not the same number";
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

    /// <summary>A shape as a message names it: by its id, or as a shape where it has none.</summary>
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
