using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Structural validity checks PGM enforces at load. A wool is parsed with parseRequiredRegionProperty,
/// so a monument-less wool is an unloadable map — <see cref="MapValidity"/> flags it as an error before
/// export, matching PGM's "Missing required region 'monument'".
/// </summary>
public sealed class MapValidityTests
{
    private static Dict Wool(string color, params Dict[] monuments) => new()
    {
        ["id"] = color, ["color"] = color, ["monuments"] = monuments.Cast<object?>().ToList(),
    };

    private static Dict MonumentAt(double x, double y, double z) => new()
    {
        ["team"] = "blue", ["location"] = new Dict { ["x"] = x, ["y"] = y, ["z"] = z },
    };

    [Test]
    public async Task A_wool_with_a_monument_is_valid()
    {
        var doc = new Dict { ["wools"] = new List<object?> { Wool("red", MonumentAt(10, 1, 10)) } };
        var r = MapValidity.Check(doc);
        await Assert.That(r.Valid).IsTrue();
        await Assert.That(r.Issues.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_wool_with_no_monument_is_an_error()
    {
        var doc = new Dict { ["wools"] = new List<object?> { Wool("red") } };
        var r = MapValidity.Check(doc);
        await Assert.That(r.Valid).IsFalse();
        await Assert.That(r.Issues.Count).IsEqualTo(1);
        await Assert.That(r.Issues[0].Kind).IsEqualTo("wool_monument");
        await Assert.That(r.Issues[0].Subject).IsEqualTo("red");
        await Assert.That(r.Issues[0].Severity).IsEqualTo("error");
    }

    [Test]
    public async Task A_monument_by_region_reference_counts()
    {
        var mon = new Dict { ["team"] = "blue", ["monument_region"] = "red-monument" };
        var doc = new Dict { ["wools"] = new List<object?> { Wool("red", mon) } };
        await Assert.That(MapValidity.Check(doc).Valid).IsTrue();
    }

    [Test]
    public async Task Each_monument_less_wool_is_reported_separately()
    {
        var doc = new Dict { ["wools"] = new List<object?> { Wool("red"), Wool("blue", MonumentAt(1, 1, 1)), Wool("green") } };
        var r = MapValidity.Check(doc);
        await Assert.That(r.Valid).IsFalse();
        await Assert.That(r.Errors.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task No_wools_is_vacuously_valid()
    {
        await Assert.That(MapValidity.Check(new Dict()).Valid).IsTrue();
    }
}
