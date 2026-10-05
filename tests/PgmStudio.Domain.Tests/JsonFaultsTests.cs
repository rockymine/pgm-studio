using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// A reader's fault as a finding: the field it stopped at in front, then a predicate about it — and
/// System.Text.Json's own faults read for what they are rather than passed on as its sentence.
/// </summary>
public sealed class JsonFaultsTests
{
    private sealed record Stroke(double Radius, int Seed, List<double>? Points = null);

    private static Exception FaultOf(string json)
    {
        try { JsonSerializer.Deserialize<Stroke>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch (Exception fault) { return fault; }
        throw new InvalidOperationException("the document read");
    }

    [Test]
    [Arguments("""{"radius":"wide"}""", "radius", "`radius` is not a number")]
    [Arguments("""{"seed":1.5}""", "seed", "`seed` is not a whole number")]
    [Arguments("""{"points":3}""", "points", "`points` is not a list")]
    public async Task A_value_of_the_wrong_type_says_what_its_field_takes(string json, string field, string message)
    {
        var finding = JsonFaults.Said(RequestRules.Unreadable, FaultOf(json));

        await Assert.That(finding.Field).IsEqualTo(field);
        await Assert.That(finding.Message).IsEqualTo(message);
    }

    [Test]
    public async Task A_body_that_is_not_json_says_where_it_stopped_and_names_no_field()
    {
        var finding = JsonFaults.Said(RequestRules.Unreadable, FaultOf("{ not json"));

        await Assert.That(finding.Field).IsNull();
        await Assert.That(finding.Message).StartsWith("the request's body is not JSON at line 1, position ");
        await Assert.That(finding.Message).DoesNotContain("Path:");
    }

    /// <summary>A document posted as one member of the body is named under that member, field and edit
    /// alike, so the caller can find it in what it sent.</summary>
    [Test]
    public async Task A_document_fault_is_said_under_its_member_with_its_edit()
    {
        var edit = DocumentEdit.Of(MapDocuments.Request, "beams", DocumentEdit.Set, new { block = -1 }, "set `beams` to {\"block\":-1}");
        var finding = JsonFaults.Said(RequestRules.Unreadable, new DocumentFault("beams", "is stated as null", edit), "styleJson");

        await Assert.That(finding.Field).IsEqualTo("styleJson.beams");
        await Assert.That(finding.Message).IsEqualTo("`styleJson.beams` is stated as null");
        await Assert.That(finding.Edit!.Path).IsEqualTo("styleJson.beams");
    }

    [Test]
    public async Task A_fault_naming_no_field_is_said_of_the_document()
    {
        var finding = JsonFaults.Said(RequestRules.Unreadable, new JsonException("is empty"), document: "the plan");

        await Assert.That(finding.Field).IsNull();
        await Assert.That(finding.Message).IsEqualTo("the plan is empty");
    }

    /// <summary>A fault that is not a reader's is its own sentence, said as it stands.</summary>
    [Test]
    public async Task Any_other_fault_is_said_as_it_stands()
    {
        var finding = JsonFaults.Said(RequestRules.Unreadable, new ArgumentException("`at` holds 2 pixels, not 1"));

        await Assert.That(finding.Message).IsEqualTo("`at` holds 2 pixels, not 1");
    }
}
