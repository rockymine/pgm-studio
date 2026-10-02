using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// The library's own refusals: what a pattern is, and what a slot binds. A single block is not a pattern, a
/// slot holds one block or one pattern and never both, and the library holds each pattern once.
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
