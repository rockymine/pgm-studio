using System.Text.Json;

namespace PgmStudio.Vocabulary.Tests;

/// <summary>
/// The answer every gate in the studio returns, and the questions it exists to answer once.
/// </summary>
public class FindingsTests
{
    private static readonly Finding Refusal = new("PL1", "there is no land to build");
    private static readonly Finding Complaint = new("PL3", "nothing wins the match", Severity.Complaint);

    /// <summary>
    /// <b>A count is not a verdict.</b> A gate reporting only refusals answers an empty list for a clean
    /// document, so <c>Count &gt; 0</c> reads as "refused" and happens to be right; a gate reporting complaints
    /// too answers a non-empty list for a document that is perfectly good, and the same expression blocks it.
    /// Both were being read the same way. <see cref="Findings.Refuses"/> is the question, and it is the whole
    /// reason this is a type rather than a list.
    /// </summary>
    [Test]
    public async Task A_complaint_alone_is_not_a_refusal_however_many_there_are()
    {
        var complaints = Findings.Of(Complaint, Complaint);

        await Assert.That(complaints.Count).IsEqualTo(2);
        await Assert.That(complaints.Refuses).IsFalse();
        await Assert.That(complaints.Refusals).IsEmpty();
        await Assert.That(complaints.Complaints.Count()).IsEqualTo(2);

        var mixed = Findings.Of(Complaint, Refusal);
        await Assert.That(mixed.Refuses).IsTrue();
        await Assert.That(mixed.Refusals.Single().Rule).IsEqualTo("PL1");
        await Assert.That(mixed.Complaints.Single().Rule).IsEqualTo("PL3");
    }

    /// <summary>Nothing wrong is a value rather than an absence, so a gate with nothing to say never returns
    /// null and a caller never checks for it.</summary>
    [Test]
    public async Task Nothing_wrong_is_a_value()
    {
        await Assert.That(Findings.None.Count).IsEqualTo(0);
        await Assert.That(Findings.None.Refuses).IsFalse();
        await Assert.That(Findings.Of().Count).IsEqualTo(0);
        await Assert.That(Findings.None.Summary).IsEmpty();
    }

    /// <summary>Two gates over one document read as one answer, and neither loses its findings or their
    /// order.</summary>
    [Test]
    public async Task Two_gates_answers_join()
    {
        var joined = Findings.Of(Complaint).And(Findings.Of(Refusal));

        await Assert.That(joined.Select(finding => finding.Rule)).IsEquivalentTo(new[] { "PL3", "PL1" });
        await Assert.That(joined.Refuses).IsTrue();
        await Assert.That(Findings.None.And(joined)).IsEqualTo(joined);
        await Assert.That(joined.And(Findings.None)).IsEqualTo(joined);
    }

    /// <summary>
    /// <b>A field is only findable if it says which document it is in.</b> A house style bound twice onto one
    /// sketch reports <c>doorHead.block</c> from either, and an author reading that cannot tell which of the two
    /// to fix — so a gate run over a sub-object roots what it found. A finding with no field of its own takes
    /// the root itself rather than <c>roomStyles.cage.</c> with nothing after it.
    /// </summary>
    [Test]
    public async Task A_gate_over_a_sub_object_roots_the_field_it_names()
    {
        var rooted = Findings.Of(
            new Finding("HS1", "not a stair", Field: "doorHead.block"),
            new Finding("HS2", "too short")).Under("roomStyles.cage");

        await Assert.That(rooted[0].Field).IsEqualTo("roomStyles.cage.doorHead.block");
        await Assert.That(rooted[1].Field).IsEqualTo("roomStyles.cage");

        // Everything else about each finding survives the rooting.
        await Assert.That(rooted[0].Rule).IsEqualTo("HS1");
        await Assert.That(rooted[1].Message).IsEqualTo("too short");
    }

    /// <summary>The one line beside the findings on the wire is every sentence, in order.</summary>
    [Test]
    public async Task The_summary_is_every_sentence()
    {
        await Assert.That(Findings.Of(Refusal, Complaint).Summary)
            .IsEqualTo("there is no land to build; nothing wins the match");
    }

    /// <summary>A derivation whose findings are the reasons its answer is no writes them as refusals — that is
    /// how it asks its own question — and hands them out as complaints, because nothing it says stops the work.
    /// The downgrade must touch the severity and nothing else; a rule id or a subject list lost on the way out
    /// would take the finding's whole point with it.</summary>
    [Test]
    public async Task Complaints_keep_everything_but_the_severity()
    {
        var refusal = new Finding("HJ2", "half on", Severity.Refusal, "wings", ["0", "1"], "G172");

        var carried = Findings.Of(refusal).AsComplaints();

        await Assert.That(carried.Refuses).IsFalse();
        await Assert.That(carried[0]).IsEqualTo(refusal with { Severity = Severity.Complaint });
    }

    /// <summary>A complaint downgraded again is the same complaint, so a boundary crossed twice does not have to
    /// know whether the one before it already ran.</summary>
    [Test]
    public async Task Downgrading_a_complaint_changes_nothing()
    {
        var already = Findings.Of(Complaint);

        await Assert.That(already.AsComplaints()).IsEquivalentTo(already);
    }

    /// <summary>
    /// <b>A decline is neither of the other two, and each question it is asked has a different answer.</b> It
    /// does not stop the work, so <see cref="Findings.Refuses"/> is false and it rides on the success. It is
    /// not a remark either, so it is not among the complaints — the one thing it says is that a piece of what
    /// the author posted is gone, and a caller counting complaints would read that as "nothing was lost".
    /// </summary>
    [Test]
    public async Task A_decline_stops_nothing_and_is_not_a_remark()
    {
        var declined = new Finding("DR-SITE", "boulder 'b1' has no ground at (8, 24)", Severity.Decline,
            Subjects: ["b1"]);

        var carried = Findings.Of(Complaint, declined);

        await Assert.That(carried.Refuses).IsFalse();
        await Assert.That(carried.Refusals).IsEmpty();
        await Assert.That(carried.Complaints.Single().Rule).IsEqualTo("PL3");
        await Assert.That(carried.Single(finding => finding.Severity == Severity.Decline)).IsEqualTo(declined);
    }

    /// <summary>Downgrading at a boundary leaves a decline alone. The operation says "none of these stops the
    /// work", which a decline already does not — rewriting it as a complaint would lose the only thing it
    /// carries, and the boundary that runs it is not the place that knows what was dropped.</summary>
    [Test]
    public async Task Downgrading_leaves_a_decline_a_decline()
    {
        var declined = new Finding("DR-KEEP", "tree 't5' stands in a door's approach", Severity.Decline);

        await Assert.That(Findings.Of(declined).AsComplaints()[0].Severity).IsEqualTo(Severity.Decline);
    }

    /// <summary>A finding goes onto the wire under the keys <c>docs/refusals.md</c> states, and writes the
    /// optional ones only when it has something to say — so a reader never has to tell an absent subject from
    /// an empty one. The record itself is what is serialized, wherever it is serialized, so there is no second
    /// rendering that could answer different keys.</summary>
    [Test]
    public async Task The_wire_keys_are_the_documented_ones()
    {
        var bare = Keys(Refusal);
        await Assert.That(bare.Keys.Order()).IsEquivalentTo(new[] { "message", "rule", "severity" });
        await Assert.That(bare["severity"].GetString()).IsEqualTo("refusal");

        var full = Keys(new Finding("HJ2", "half on", Severity.Complaint, "wings", ["0", "1"], "G172"));
        await Assert.That(full.Keys.Order())
            .IsEquivalentTo(new[] { "cites", "field", "message", "rule", "severity", "subjects" });
        await Assert.That(full["severity"].GetString()).IsEqualTo("complaint");
        await Assert.That(full["cites"].GetString()).IsEqualTo("G172");
    }

    /// <summary>The two computed properties are answers to questions the wire already carries — the subjects
    /// and the severity — so writing them would put a second copy of each on the wire, free to disagree with
    /// the field it was derived from.</summary>
    [Test]
    public async Task The_computed_answers_are_not_written()
    {
        var keys = Keys(new Finding("HJ2", "half on", Severity.Complaint, Subjects: ["0"])).Keys;

        await Assert.That(keys).DoesNotContain("subjectIds");
        await Assert.That(keys).DoesNotContain("refuses");
    }

    /// <summary>One finding as the keys a caller reads, under the web defaults every serializer in the studio
    /// uses.</summary>
    private static Dictionary<string, JsonElement> Keys(Finding finding) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(finding, new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
}
