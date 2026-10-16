using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Set one of a play piece's settings by its id: a goal's name or structure, a wool's colour, a spawn's facing.
///
/// <para><b>What a unit shares, every image takes.</b> A goal's design, material, lava, float and leak are the
/// same goal for every team, so setting one sets it on every image of the unit unless <c>alone</c>; a name and a
/// colour belong to the team the image is for, so they are set on the named image only. A spawn's facing turns
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

        if (PlayPieces.Of(intent).Any(piece => piece.Id == id)) return NotOf(key, "this kind of piece");
        return (null, Findings.Of(new Finding(RequestRules.NoSuchSubject,
            $"the map has no play piece with the id '{id}'", Field: "id", Subjects: [id])));
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
