using System.Reflection;
using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Minecraft.Houses;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// What a style document says when it means "not this part", and what the reader does with the two ways of
/// saying it.
///
/// <para>A style's parts split into nullable and not, and nothing in the JSON marks which — so a document read
/// as though a null dropped any part, and for the non-nullable ones it left the record holding a null the
/// stamper then dereferenced. It surfaced as an unhandled 500 with no rule id, no field and no subject, on
/// every surface that reads a style: previewing one, and exporting a map that had bound one.</para>
/// </summary>
public sealed class HouseStyleJsonNullPartTests
{
    /// <summary>The invariant, asked of every part rather than of the five that happened to crash: a part the
    /// record cannot hold a null for is refused by its own name, and a part that can is left alone. Driven off
    /// the type's own nullability so a part added later is covered without this file being touched — and so a
    /// part whose nullability is *changed* is caught here rather than in a map six weeks later.</summary>
    [Test]
    public async Task Every_part_is_refused_or_accepted_by_what_the_record_can_hold()
    {
        var nullability = new NullabilityInfoContext();
        var parts = typeof(HouseStyle).GetProperties()
            .Where(p => !p.PropertyType.IsValueType && p.PropertyType != typeof(string))
            .ToList();
        await Assert.That(parts).IsNotEmpty();

        foreach (var part in parts)
        {
            var field = char.ToLowerInvariant(part.Name[0]) + part.Name[1..];
            var mustBePresent = nullability.Create(part).ReadState is NullabilityState.NotNull;
            var read = () => HouseStyleJson.Deserialize($"{{\"{field}\":null}}");

            if (mustBePresent)
            {
                var fault = Assert.Throws<DocumentFault>(() => read());
                await Assert.That(fault!.Field).IsEqualTo(field);
            }
            else
            {
                await Assert.That(read()).IsNotNull();
            }
        }
    }

    /// <summary>The refusal names where it is, not just what it is. A part nested inside another reports the
    /// path an author can find in their own document — <c>roof.gableWindows</c>, not a bare
    /// <c>gableWindows</c> that appears nowhere at the top level.</summary>
    [Test]
    public async Task A_nested_part_is_refused_by_its_path()
    {
        var fault = Assert.Throws<DocumentFault>(
            () => HouseStyleJson.Deserialize("{\"roof\":{\"gableWindows\":null}}"));

        await Assert.That(fault!.Field).IsEqualTo("roof.gableWindows");
    }

    /// <summary>An absent document and an empty one are the same fault to an author, and both have to arrive as
    /// the parse failure every caller catches. This is the one that was escaping: <c>JsonNode.Parse</c> raises
    /// <see cref="ArgumentNullException"/> on a null string, which is the single type no endpoint names, so the
    /// fault left the gate above as a stack trace instead of a refusal.</summary>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task A_missing_document_is_a_parse_failure_and_not_an_argument_fault(string? json)
    {
        await Assert.That(() => HouseStyleJson.Deserialize(json!)).Throws<JsonException>();
    }

    /// <summary>The round trip is what stops the refusal above from being too eager. Serialization writes a
    /// null for every part that is allowed one, so a style that has just been written out has to read back —
    /// refusing an explicit null outright would break every stored style on the next read.</summary>
    [Test]
    public async Task A_style_the_writer_produced_reads_back()
    {
        var written = HouseStyleJson.Serialize(HousePresets.Cottage.Style);

        await Assert.That(HouseStyleJson.Deserialize(written)).IsEqualTo(HousePresets.Cottage.Style);
    }
}
