using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// <c>OB36</c> — an imported world's flag must start where its scan found a banner, because PGM refuses to load
/// a flag with none at its starting post.
/// </summary>
public sealed class MapExportComposerFlagTests
{
    private static Dict Doc(params FlagPostIntent[] posts)
    {
        var doc = new Dict { ["name"] = "m", ["version"] = "1.0.0" };
        IntentGenerator.Apply(doc, new MapIntent
        {
            Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
            Spawns =
            [
                new SpawnIntent { Team = "red", Point = new Pt(-20, 5, 0) },
                new SpawnIntent { Team = "blue", Point = new Pt(20, 5, 0) },
            ],
            Flags = [new FlagIntent { Name = "Flag", Posts = [.. posts] }],
        });
        return doc;
    }

    private static readonly FlagPostIntent Mid = new() { Name = "Mid", At = new Pt(0.5, 9, 0.5) };
    private static readonly FlagPostIntent East = new() { Name = "East", At = new Pt(-11.5, 12, 3.5) };

    [Test]
    public async Task A_flag_whose_first_post_has_no_banner_is_OB36()
    {
        var result = MapExportComposer.Compose(Doc(Mid, East), null, isIntent: true, null, null, null, [],
                                               banners: [(-12, 12, 3)]);

        await Assert.That(result.Refusal?.Error).IsEqualTo("flag without a banner");
        var finding = result.Refusal!.Findings.Single();
        await Assert.That(finding.Rule).IsEqualTo(ObjectiveRules.FlagWithoutBanner);
        await Assert.That(finding.Message).Contains("0, 9, 0").Because("the first post is where the flag starts");
    }

    // Asked with no scanned ground, so the walk refuses next: what is proved is that OB36 lets it through.
    [Test]
    public async Task A_flag_starting_on_a_scanned_banner_passes()
    {
        var result = MapExportComposer.Compose(Doc(Mid, East), null, isIntent: true, null, null, null, [],
                                               banners: [(0, 9, 0)]);

        await Assert.That(result.Refusal?.Error).IsNotEqualTo("flag without a banner");
        await Assert.That(MapExportComposer.BannerlessFlags(Doc(Mid, East), [(0, 9, 0)])).IsEmpty();
    }

    [Test]
    public async Task A_composite_starts_at_its_fallback()
    {
        var doc = Doc(Mid, East);
        var post = (Dict)((Dict)((List<object?>)doc["flags"]!)[0]!)["post"]!;
        var children = (List<object?>)post["posts"]!;
        ((Dict)children[1]!)["id"] = "east";
        post["fallback"] = "east";

        var east = MapExportComposer.BannerlessFlags(doc, [(-12, 12, 3)]);
        var mid = MapExportComposer.BannerlessFlags(doc, [(0, 9, 0)]);

        await Assert.That(east).IsEmpty();
        await Assert.That(mid.Single().Rule).IsEqualTo(ObjectiveRules.FlagWithoutBanner);
    }
}
