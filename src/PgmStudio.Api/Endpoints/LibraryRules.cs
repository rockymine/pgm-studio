using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// The library's own refusals: what a pattern is, what a slot binds, what a row is called and whose it is. A single
/// block is not a pattern, a slot holds one block or one pattern and never both, the library holds each pattern
/// once, a name is one row's, and a row the seed folder states is changed in the folder.
/// </summary>
internal static class LibraryRules
{
    /// <summary>A pattern saved that lays one block — a <c>solid</c>, or a <c>laidLog</c>, which is one log laid
    /// along its run. A block is named by its id and variant, so it needs no row: a slot holds it directly. 400.</summary>
    /// <remarks>Bind the block in the slot itself — a theme bucket's or a course's <c>block</c> — instead of saving it. <c>GET /api/terrain/blocks</c> lists the blocks.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Request, RuleConcern.Material)]
    public const string OneBlock = "LB1";

    /// <summary>A theme bucket or a course names both a block and a pattern, and a slot is filled by one. 400.</summary>
    /// <remarks>Send the block with <c>styleId</c> 0, or the pattern with no <c>block</c>.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Request, RuleConcern.Material)]
    public const string BlockAndPattern = "LB2";

    /// <summary>A pattern saved whose material the library already holds under another row. The library holds each
    /// pattern once, so two names never draw the same thing. 409, naming the row that holds it.</summary>
    /// <remarks>Bind the pattern the finding names, or rename that one. A material differing in anything at all — a seed, a scale, a block — is a different pattern and saves.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request, RuleConcern.Material)]
    public const string PatternHeld = "LB3";

    /// <summary>A library row's name is letters, digits, spaces, dashes and underscores, with no space at either
    /// end and none doubled — a name a person types to name the row from a map's source. 400.</summary>
    /// <remarks>Rename it with only those characters: <c>mesa-bryce</c> or <c>Mesa Bryce</c>, not <c>Mesa (Bryce)</c>.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Request)]
    public const string NameCharacters = "LB4";

    /// <summary>A library row's name another row of the same kind already carries, compared without case. A map's
    /// source names a row by its name, so one name is one row. 409, naming the row that carries it.</summary>
    /// <remarks>Choose another name, or edit the row the finding names.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request)]
    public const string NameTaken = "LB5";

    /// <summary>A library row the seed folder states, edited or deleted. The row is the folder's: every start
    /// rewrites it to the entry it holds, so an edit would be gone by the next one. 409, naming the entry.</summary>
    /// <remarks>Save a copy under a name of its own and change that, or change the entry in the seed folder, <c>src/PgmStudio.Minecraft/Library</c>. A row's <c>seeded</c> says whether it is the folder's.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request)]
    public const string SeededRow = "LB6";
}

/// <summary>What editing or deleting a library row refuses where the seed folder states it:
/// <see cref="LibraryRules.SeededRow"/>, on every kind.</summary>
public static class SeededRows
{
    /// <summary>True when the row carrying <paramref name="seedKey"/> is the folder's and the refusal has been
    /// written; false for an author's row, and for a row that does not exist, which the caller answers itself.</summary>
    public static async Task<bool> RefusedAsync(HttpContext http, string? seedKey, string what, CancellationToken ct)
    {
        if (seedKey is null) return false;
        await Refusals.WriteAsync(http, 409, "seeded row",
            [new Finding(LibraryRules.SeededRow,
                $"this {what} is the seed folder's entry `{seedKey}`, rewritten on every start: save a copy and change "
                + "that, or change the folder", Subjects: [seedKey])], ct);
        return true;
    }
}

/// <summary>What a library row's name has to be, on every save of every kind: <see cref="LibraryRules.NameCharacters"/>
/// and <see cref="LibraryRules.NameTaken"/>.</summary>
public static partial class LibraryNaming
{
    /// <summary>True when the name is refused and the refusal has been written: 400 for its characters, 409 for a
    /// name another row of the kind in <paramref name="held"/> carries. <paramref name="self"/> is the row being
    /// edited, which may keep its own name.</summary>
    public static async Task<bool> RefusedAsync(
        HttpContext http, string? name, long? self, IEnumerable<(long Id, string Name)> held, CancellationToken ct)
    {
        if (!Valid(name))
        {
            await Refusals.WriteAsync(http, 400, "invalid name",
                [new Finding(LibraryRules.NameCharacters,
                    $"`{name}` is not a library name: letters, digits, spaces, dashes and underscores, with no space "
                    + "at either end and none doubled", Field: "name")], ct);
            return true;
        }
        if (held.FirstOrDefault(row => row.Id != self
                && string.Equals(row.Name, name, StringComparison.OrdinalIgnoreCase)) is not { Name: { } taken } other)
            return false;
        await Refusals.WriteAsync(http, 409, "name taken",
            [new Finding(LibraryRules.NameTaken, $"`{taken}` (row {other.Id}) already carries this name",
                Field: "name", Subjects: [taken])], ct);
        return true;
    }

    /// <summary>Text made into a name a library row may carry: a <c>+</c> is spelled <c>plus</c>, any other
    /// character a name may not hold becomes a space, and spaces are collapsed and trimmed — <c>Extreme hills+ M</c>
    /// is <c>Extreme hills plus M</c>, <c>Mesa (Bryce)</c> is <c>Mesa Bryce</c>.</summary>
    public static string Tidy(string text)
    {
        var spelled = new System.Text.StringBuilder(text.Length);
        foreach (var character in text)
            spelled.Append(character == '+' ? " plus "
                : char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character.ToString() : " ");
        var tidy = string.Join(' ', spelled.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return tidy.Length == 0 ? "unnamed" : tidy;
    }

    /// <summary>Whether a name is one a library row may carry.</summary>
    public static bool Valid(string? name) => name is { Length: > 0 } && Shape().IsMatch(name);

    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Za-z0-9_-]+( [A-Za-z0-9_-]+)*$")]
    private static partial System.Text.RegularExpressions.Regex Shape();
}

/// <summary>The checks <see cref="LibraryRules"/> names that read nothing but the request.</summary>
internal static class LibraryGate
{
    /// <summary>A pattern's params, refused where they lay one block.</summary>
    public static Findings Pattern(string kind, string paramsJson)
    {
        if (Slots.IsBlockKind(kind)) return Refused(kind);
        try
        {
            return Slots.AsBlock(TerrainThemeJson.DeserializeMaterial(paramsJson)) is null
                ? Findings.None
                : Refused(kind);
        }
        catch { return Findings.None; }
    }

    /// <summary>Every slot of a save, refused where one names both a block and a pattern.</summary>
    public static Findings Bindings(IEnumerable<(string Where, long StyleId, SlotBlockDto? Block)> slots)
        => new(slots.Where(slot => slot.StyleId != 0 && slot.Block is not null)
            .Select(slot => new Finding(LibraryRules.BlockAndPattern,
                $"{slot.Where} names both a block and pattern {slot.StyleId}; a slot is filled by one",
                Field: slot.Where)));

    /// <summary>A building's courses, refused where one names both.</summary>
    public static Findings Courses(IEnumerable<RoomCourseDto> courses)
        => Bindings(courses.Select(course => ($"courses[{course.Part} {course.Ordinal}]", course.StyleId, course.Block)));

    /// <summary>A theme's buckets, refused where one names both.</summary>
    public static Findings Buckets(IEnumerable<ThemeBucketDto> buckets)
        => Bindings(buckets.Select(bucket => ($"buckets[{bucket.Bucket}]", bucket.StyleId, bucket.Block)));

    private static Findings Refused(string kind) => Findings.Of(new Finding(LibraryRules.OneBlock,
        $"a `{kind}` lays one block, and a single block is not a pattern: bind it in the slot as a block",
        Field: "params"));
}
