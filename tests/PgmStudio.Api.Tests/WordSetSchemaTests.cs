using System.Reflection;
using System.Text.Json;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A field that takes one of a closed set of words publishes them.
///
/// <para><c>PgmStudio.Vocabulary</c> exists because three parties have to spell a <c>map.stage</c>, a
/// <c>style.kind</c>, a theme bucket, a room part or a roof form identically, and the words are
/// <c>const string</c>s rather than a C# enum because the party writing one furthest down cannot see
/// <c>Contracts</c>. So nothing in the compiler carries the tie to the wire: a <c>[WordSet]</c> forgotten on
/// a new field leaves it a bare <c>string</c> in the document, which is what every one of them was, and no
/// build breaks. This reads the generated document and holds each marked field to the words its declaring
/// class publishes — and, from the other side, asserts the marks are there at all.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class WordSetSchemaTests
{
    /// <summary>The word sets the wire carries. All ten of them: <c>MapStage</c> and the nine in
    /// <c>TerrainVocabulary</c>. A set added to that project and never marked on a field is a set the
    /// document says nothing about, so it is named here rather than counted.</summary>
    private static readonly Type[] Sets =
    [
        typeof(MapStage), typeof(MaterialKind), typeof(ThemeBuckets), typeof(RimEdgeModes),
        typeof(RoomParts), typeof(RoofForms), typeof(PorchEdges), typeof(WindowForms),
        typeof(DoorHeadForms), typeof(DoorHeadFills), typeof(Landform), typeof(EditZone),
    ];

    /// <summary>The fields marked today. Nothing in the compiler can say a field <em>ought</em> to be
    /// marked — an unmarked <c>string</c> is what every one of these was, and is indistinguishable from a
    /// field that genuinely takes free text — so the count is what holds a mark to being kept: a mark
    /// dropped fails here.
    ///
    /// <para>It is a count of <b>declarations</b>, not of published fields. A record that inherits its
    /// fields carries the mark once, on the base, and publishes it through <c>allOf</c> — so consolidating
    /// two records into one lowers this while the wire gains nothing and loses nothing. Lower it only for
    /// that reason, and never because a mark went missing.</para></summary>
    private const int Published = 18;

    /// <summary>Every marked field publishes exactly the words its class declares, in that order — so a word
    /// added to a set reaches the document with no second edit, and one removed cannot linger there.</summary>
    [Test]
    public async Task A_marked_field_publishes_the_words_its_set_declares()
    {
        var schemas = (await DocumentAsync()).GetProperty("components").GetProperty("schemas");

        var checkedFields = 0;
        foreach (var record in Records())
            foreach (var property in record.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (Declared(property) is not { } declaring) continue;
                if (!schemas.TryGetProperty(record.Name, out var schema)) continue;
                // A record that inherits its fields publishes only its own; the marked ones are checked on
                // the base that declares them, which is where the attribute is.
                if (!schema.TryGetProperty("properties", out var properties)) continue;
                if (!properties.TryGetProperty(Wire(property), out var field)) continue;

                var published = field.TryGetProperty("enum", out var words)
                    ? words.EnumerateArray().Select(word => word.GetString()).ToList()
                    : [];
                await Assert.That(published).IsEquivalentTo(Words.Of(declaring))
                    .Because($"{record.Name}.{Wire(property)} takes {declaring.Name}'s words");
                checkedFields++;
            }

        await Assert.That(checkedFields).IsGreaterThanOrEqualTo(Published)
            .Because($"{checkedFields} field(s) publish a word set, and {Published} did");
    }

    /// <summary>And every set reaches the wire. A word set nothing marks is one the document is silent about
    /// — the state all ten were in — and counting marked fields alone would never say so.</summary>
    [Test]
    public async Task Every_word_set_is_published_by_something()
    {
        var marked = Records()
            .SelectMany(record => record.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(Declared)
            .OfType<Type>()
            .ToHashSet();

        await Assert.That(Sets.Where(set => !marked.Contains(set)).Select(set => set.Name)).IsEmpty();
        await Assert.That(marked.Where(set => !Sets.Contains(set)).Select(set => set.Name)).IsEmpty();
        await Assert.That(Sets.Where(set => Words.Of(set).Count == 0).Select(set => set.Name)).IsEmpty();
    }

    private static IEnumerable<Type> Records() =>
        typeof(PgmStudio.Contracts.MapSummary).Assembly.GetTypes().Where(type => type.IsClass);

    private static Type? Declared(PropertyInfo property) =>
        (property.GetCustomAttribute<WordSetAttribute>()
         ?? property.DeclaringType?.GetConstructors()
             .SelectMany(constructor => constructor.GetParameters())
             .FirstOrDefault(parameter => parameter.Name == property.Name)
             ?.GetCustomAttribute<WordSetAttribute>())?.Declaring;

    private static string Wire(PropertyInfo property) =>
        property.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name
        ?? char.ToLowerInvariant(property.Name[0]) + property.Name[1..];

    private static async Task<JsonElement> DocumentAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        return JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json")).RootElement.Clone();
    }
}
