using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Set one of a play piece's settings by its id: a goal's name or structure, a wool's colour, a spawn's facing, a
/// control point's shape and how it is taken, and which of a piece's stamps run (<see cref="StampSwitches"/>).
///
/// <para><b>What a unit shares, every image takes.</b> A goal's design, material, lava, float and leak are the
/// same goal for every team, so setting one sets it on every image of the unit unless <c>alone</c>; a name and a
/// colour belong to the team the image is for, so they are set on the named image only. A control point's shape,
/// points, capture time and capture mode are the point's on every image, and its name the named image's. A spawn's facing turns
/// every image as a turn does. A value stating nothing puts the setting back to its default.</para>
/// </summary>
/// <summary>What a setting becomes: text for a name, a colour, a design or a material, a whole number for the
/// rest, a flag for whether a core is open; none of the three puts the setting back to its default.</summary>
public readonly record struct PieceValue(string? Text = null, int? Number = null, bool? Flag = null)
{
    /// <summary>Whether the value states nothing.</summary>
    public bool IsDefault => Text is null && Number is null && Flag is null;
}

public static class PlayPieceSettings
{
    /// <summary>The intent with the setting <paramref name="key"/> of the piece <paramref name="id"/> made
    /// <paramref name="value"/>, or the findings that stopped it and a null intent.</summary>
    public static (MapIntent? Intent, Findings Refused) Apply(
        MapIntent intent, string id, string key, PieceValue value, bool alone = false, SymmetryIntent? board = null)
    {
        PieceValue? stated = value.IsDefault ? null : value;
        if (StampSwitches.All.Contains(key)) return Switched(intent, id, key, stated, alone, intent.Symmetry ?? board);

        if (intent.Spawns.Any(spawn => spawn.Id == id))
        {
            if (key != PieceSettings.Facing) return NotOf(key, "a spawn");
            if (stated?.Number is not { } quarters) return Unreadable(key, "a number of quarter turns");
            return PlayPieceMove.Face(intent, id, quarters, alone, board);
        }

        if ((intent.Wools ?? []).FindIndex(wool => wool.Id == id) is var woolAt and >= 0)
        {
            if (key != PieceSettings.Color) return NotOf(key, "a wool");
            var color = Text(stated);
            if (color.Length > 0 && !WoolColors.IsColor(color)) return Unreadable(key, "a wool colour");
            var wools = new List<WoolIntent>(intent.Wools!) { [woolAt] = intent.Wools![woolAt] with { Color = color } };
            return (intent with { Wools = wools }, Findings.None);
        }

        if ((intent.Destroyables ?? []).FindIndex(goal => goal.Id == id) is var goalAt and >= 0)
        {
            var goals = intent.Destroyables!;
            if (key == PieceSettings.Name)
                return (intent with { Destroyables = Named(goals, goalAt, goals[goalAt] with { Name = Text(stated) }) },
                        Findings.None);
            Func<DestroyableIntent, DestroyableIntent>? set = key switch
            {
                PieceSettings.Style when stated is null => goal => goal with { Style = DestroyableStyles.Slug(ObjectiveDefaults.Style) },
                PieceSettings.Style when DestroyableStyles.TryParse(Text(stated), out var style) =>
                    goal => goal with { Style = DestroyableStyles.Slug(style) },
                PieceSettings.Materials when stated is null => goal => goal with { Materials = ObjectiveDefaults.Materials },
                PieceSettings.Materials when DestroyableMaterials.All.Contains(Text(stated)) =>
                    goal => goal with { Materials = Text(stated) },
                PieceSettings.Float when Blocks(stated, ObjectiveDefaults.DestroyableFloat) is { } air =>
                    goal => goal with { Float = air, Box = null },
                _ => null,
            };
            if (set is null)
                return key is PieceSettings.Style or PieceSettings.Materials or PieceSettings.Float
                    ? Unreadable(key, key == PieceSettings.Float ? "a number of blocks from 0 to 64" : "one the vocabulary names")
                    : NotOf(key, "a destroyable");
            return (intent with { Destroyables = Unit(goals, goalAt, goal => goal.Stamp, alone, set) }, Findings.None);
        }

        if ((intent.Cores ?? []).FindIndex(core => core.Id == id) is var coreAt and >= 0)
        {
            var cores = intent.Cores!;
            if (key == PieceSettings.Name)
                return (intent with { Cores = Named(cores, coreAt, cores[coreAt] with { Name = Text(stated) }) }, Findings.None);
            Func<CoreIntent, CoreIntent>? set = key switch
            {
                PieceSettings.Lava when Ranged(stated, ObjectiveDefaults.CoreLava, ObjectiveDefaults.MinCoreLava,
                    ObjectiveDefaults.MaxCoreLava) is { } lava => core => core with { Lava = lava, Box = null },
                PieceSettings.LavaHeight when Ranged(stated, ObjectiveDefaults.CoreLavaHeight, ObjectiveDefaults.MinCoreLavaHeight,
                    ObjectiveDefaults.MaxCoreLavaHeight) is { } height => core => core with { LavaHeight = height, Box = null },
                PieceSettings.OpenTop when stated is null || stated.Value.Flag is not null =>
                    core => core with { OpenTop = stated?.Flag is true, Box = null },
                PieceSettings.Float when Blocks(stated, ObjectiveDefaults.CoreFloat) is { } air =>
                    core => core with { Float = air, Box = null },
                PieceSettings.Leak when Blocks(stated, ObjectiveDefaults.CoreLeak) is { } leak => core => core with { Leak = leak },
                _ => null,
            };
            if (set is null)
                return key is PieceSettings.Lava or PieceSettings.LavaHeight or PieceSettings.OpenTop
                           or PieceSettings.Float or PieceSettings.Leak
                    ? Unreadable(key, key == PieceSettings.OpenTop ? "true or false" : "a number in its range")
                    : NotOf(key, "a core");
            return (intent with { Cores = Unit(cores, coreAt, core => core.Stamp, alone, set) }, Findings.None);
        }

        if ((intent.ControlPoints ?? []).FindIndex(point => point.Id == id) is var pointAt and >= 0)
        {
            var points = intent.ControlPoints!;
            if (key == PieceSettings.Name)
                return (intent with { ControlPoints = Named(points, pointAt, points[pointAt] with { Name = Text(stated) }) },
                        Findings.None);
            Func<ControlPointIntent, ControlPointIntent>? set = key switch
            {
                PieceSettings.Shape when stated is null => point => point with { Shape = ControlPointShapes.Square },
                PieceSettings.Shape when ControlPointShapes.All.Contains(Text(stated)) =>
                    point => point with { Shape = Text(stated) },
                PieceSettings.Points when Ranged(stated, (int)ObjectiveDefaults.ControlPointPoints, 1, 100) is { } paid =>
                    point => point with { Points = paid },
                PieceSettings.CaptureTime when stated is null => point => point with { CaptureTime = ObjectiveDefaults.ControlPointCaptureTime },
                PieceSettings.CaptureTime when Ranged(stated, 0, 1, 600) is { } seconds =>
                    point => point with { CaptureTime = $"{seconds}s" },
                PieceSettings.CaptureMode when stated is null => point => point with { CaptureMode = CaptureModes.Hold },
                PieceSettings.CaptureMode when CaptureModes.All.Contains(Text(stated)) =>
                    point => point with { CaptureMode = Text(stated) },
                _ => null,
            };
            if (set is null)
                return key switch
                {
                    PieceSettings.Shape => Unreadable(key, "'square' or 'disc'"),
                    PieceSettings.Points => Unreadable(key, "a whole number of points from 1 to 100"),
                    PieceSettings.CaptureTime => Unreadable(key, "a whole number of seconds from 1 to 600"),
                    PieceSettings.CaptureMode => Unreadable(key, "'hold' or 'touch'"),
                    _ => NotOf(key, "a control point"),
                };
            return (intent with { ControlPoints = Unit(points, pointAt, point => point.Stamp, alone, set) }, Findings.None);
        }

        if (PlayPieces.Of(intent).Any(piece => piece.Id == id)) return NotOf(key, "this kind of piece");
        return (null, Findings.Of(new Finding(RequestRules.NoSuchSubject,
            $"the map has no play piece with the id '{id}'", Field: "id", Subjects: [id])));
    }

    /// <summary>The intent with the stamp <paramref name="word"/> of the piece <paramref name="id"/> switched on or
    /// off — a flag, true or nothing for on — on every image of its unit unless <paramref name="alone"/>.</summary>
    private static (MapIntent? Intent, Findings Refused) Switched(
        MapIntent intent, string id, string word, PieceValue? stated, bool alone, SymmetryIntent? symmetry)
    {
        if (stated is { Flag: null }) return Unreadable(word, "true or false");
        var on = stated?.Flag ?? true;
        string[]? Set(string[]? off) => Switch(off, word, on);
        var kind = PlayPieces.Of(intent).FirstOrDefault(piece => piece.Id == id).Kind;
        if (kind is null)
            return (null, Findings.Of(new Finding(RequestRules.NoSuchSubject,
                $"the map has no play piece with the id '{id}'", Field: "id", Subjects: [id])));
        if (!StampSwitches.Of(kind).Contains(word)) return NotOf(word, $"a {kind}");

        switch (kind)
        {
            case PlayPieceKinds.WoolRoom:
                var wools = intent.Wools!;
                return (intent with { Wools = Unit(wools, wools.FindIndex(wool => wool.Id == id), wool => wool.Stamp, alone,
                    wool => wool with { StampsOff = Set(wool.StampsOff) }) }, Findings.None);
            case PlayPieceKinds.Destroyable:
                var goals = intent.Destroyables!;
                return (intent with { Destroyables = Unit(goals, goals.FindIndex(goal => goal.Id == id), goal => goal.Stamp, alone,
                    goal => goal with { StampsOff = Set(goal.StampsOff) }) }, Findings.None);
            case PlayPieceKinds.Core:
                var cores = intent.Cores!;
                return (intent with { Cores = Unit(cores, cores.FindIndex(core => core.Id == id), core => core.Stamp, alone,
                    core => core with { StampsOff = Set(core.StampsOff) }) }, Findings.None);
            case PlayPieceKinds.ControlPoint:
                var points = intent.ControlPoints!;
                return (intent with { ControlPoints = Unit(points, points.FindIndex(point => point.Id == id), point => point.Stamp, alone,
                    point => point with { StampsOff = Set(point.StampsOff) }) }, Findings.None);
            case PlayPieceKinds.Wall:
                var walls = intent.Structures!.Walls;
                return (intent with { Structures = intent.Structures with { Walls = Unit(walls, walls.FindIndex(wall => wall.Id == id),
                    wall => wall.Stamp, alone, wall => wall with { StampsOff = Set(wall.StampsOff) }) } }, Findings.None);
            default:
                var build = intent.Build!;
                return (intent with { Build = build with { Areas = PlayPieceMove.AreaImages(build.Areas,
                    build.Areas.FindIndex(area => area.Id == id), symmetry, alone,
                    (area, _) => area with { StampsOff = Set(area.StampsOff) }) } }, Findings.None);
        }
    }

    /// <summary>The switched-off list <paramref name="off"/> with <paramref name="word"/> switched on or off; null
    /// where nothing is left off.</summary>
    public static string[]? Switch(string[]? off, string word, bool on)
    {
        var next = (off ?? []).Where(stamp => stamp != word).Concat(on ? [] : [word]).ToArray();
        return next.Length == 0 ? null : next;
    }

    /// <summary>The list with <paramref name="change"/> applied to the piece at <paramref name="at"/> and,
    /// unless <paramref name="alone"/>, to every other image of its unit.</summary>
    private static List<T> Unit<T>(List<T> pieces, int at, Func<T, StampId> stampOf, bool alone, Func<T, T> change)
    {
        var unit = stampOf(pieces[at]);
        return [.. pieces.Select((piece, index) => index == at
            || !alone && !string.IsNullOrEmpty(unit.Kind) && stampOf(piece).Identity == unit.Identity
            ? change(piece) : piece)];
    }

    private static List<T> Named<T>(List<T> pieces, int at, T named) => new(pieces) { [at] = named };

    private static string Text(PieceValue? value) => value?.Text?.Trim() ?? "";

    /// <summary>A number of blocks from 0 to 64, the default where none is stated, or null where it is not one.</summary>
    private static int? Blocks(PieceValue? value, int fallback) => Ranged(value, fallback, 0, 64);

    private static int? Ranged(PieceValue? value, int fallback, int min, int max) =>
        value is null ? fallback
        : value.Value.Number is { } stated && stated >= min && stated <= max ? stated
        : null;

    private static (MapIntent? Intent, Findings Refused) NotOf(string key, string what) =>
        (null, Findings.Of(new Finding(RequestRules.Unreadable,
            $"'{key}' is not a setting of {what}", Field: "key")));

    private static (MapIntent? Intent, Findings Refused) Unreadable(string key, string takes) =>
        (null, Findings.Of(new Finding(RequestRules.Unreadable,
            $"the value of '{key}' is not {takes}", Field: "value")));
}
