using System.Text.Json;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// An author written before the contribution note existed. Sixteen of the forty-three intents stored in the
/// dev database carry <c>"authors": ["rockymine", …]</c> — the username alone — and every one of them was
/// unreadable: the intent endpoint answered 400, the 3-D preview's build answered "could not build layout",
/// and the canvas reported "no WebGL" for it. One map's authors made everything else about it unreadable.
/// </summary>
public sealed class AuthorIntentJsonTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task An_author_stored_as_a_bare_string_reads_as_the_name_it_always_was()
    {
        var intent = JsonSerializer.Deserialize<MapIntent>(
            """{"meta":{"name":"Bridgid II","authors":["rockymine","Ruediger_LP"],"contributors":[]}}""", Web)!;

        await Assert.That(intent.Meta!.Authors.Select(a => a.Name))
            .IsEquivalentTo(new[] { "rockymine", "Ruediger_LP" });
        await Assert.That(intent.Meta.Authors[0].Contribution).IsNull();
    }

    [Test]
    public async Task Todays_object_form_still_reads_whole()
    {
        var intent = JsonSerializer.Deserialize<MapIntent>(
            """{"meta":{"authors":[{"name":"rockymine","contribution":"layout"}]}}""", Web)!;

        await Assert.That(intent.Meta!.Authors.Single().Name).IsEqualTo("rockymine");
        await Assert.That(intent.Meta.Authors.Single().Contribution).IsEqualTo("layout");
    }

    [Test]
    public async Task Writing_is_unchanged_so_a_document_is_stored_in_todays_shape_once_it_is_saved()
    {
        // The upgrade is a read: a document read from the old shape and written back carries the new one,
        // which is what makes this a migration that happens on its own rather than one nobody runs.
        var intent = JsonSerializer.Deserialize<MapIntent>(
            """{"meta":{"authors":["rockymine"]}}""", Web)!;

        var written = JsonSerializer.Serialize(intent, Web);
        await Assert.That(written).Contains("""{"name":"rockymine"}""");
        await Assert.That(JsonSerializer.Deserialize<MapIntent>(written, Web)!.Meta!.Authors.Single().Name)
            .IsEqualTo("rockymine");
    }
}
