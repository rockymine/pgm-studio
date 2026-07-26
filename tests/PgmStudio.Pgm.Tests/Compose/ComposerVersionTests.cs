using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The composer version's own guard. A <see cref="ComposeDescriptor"/> is a generated plan's identity, and its
/// contract is that it reproduces that plan exactly <b>within one composer version</b> — which only holds if
/// <see cref="ComposerVersion.Current"/> moves whenever the pipeline's output does. It is a hand-written
/// constant, so nothing enforced that, and it stayed put through several geometry changes.
///
/// <para>This asserts the bookkeeping, never the layouts: a changed board is always allowed, it just has to
/// arrive with a changed version. Regenerate the record with <c>dotnet run tools/compose/fingerprints.cs</c>.</para>
/// </summary>
public sealed class ComposerVersionTests
{
    private const string Regenerate = "dotnet run tools/compose/fingerprints.cs";

    private static ComposerFingerprintFile Recorded() =>
        ComposerFingerprintFile.Parse(
            File.ReadAllText(PlanTestSupport.RepoPath(ComposerFingerprint.FilePath)))
        ?? throw new InvalidOperationException("the fingerprint record does not parse");

    /// <summary>The version the record was taken under is the version the composer reports. A bump on its own
    /// fails here until the record is regenerated, which is the point: the bump and the geometry it stands for
    /// are recorded together or not at all.</summary>
    [Test]
    public async Task The_recorded_fingerprints_belong_to_the_current_composer()
    {
        await Assert.That(Recorded().ComposerVersion).IsEqualTo(ComposerVersion.Current)
            .Because($"the recorded digest is for a different composer — regenerate it: {Regenerate}");
    }

    /// <summary>Every recorded board still composes to what was recorded. A difference here means the pipeline
    /// changed the geometry a given (request, seed) produces while the version stood still — so every descriptor
    /// stored under it now claims a reproducibility it has not got.</summary>
    [Test]
    public async Task Composed_geometry_matches_the_record_for_this_version()
    {
        var recorded = Recorded();
        if (recorded.ComposerVersion != ComposerVersion.Current) return;   // reported by the test above

        var fresh = ComposerFingerprint.Compute();
        await Assert.That(fresh.Boards.Keys).IsEquivalentTo(recorded.Boards.Keys)
            .Because($"the fingerprint request set changed — regenerate: {Regenerate}");

        var moved = fresh.Boards
            .Where(b => recorded.Boards.GetValueOrDefault(b.Key) != b.Value)
            .Select(b => $"{b.Key}: recorded {recorded.Boards.GetValueOrDefault(b.Key) ?? "—"}, composed {b.Value}")
            .ToList();

        await Assert.That(moved).IsEmpty().Because(
            $"the composer's output moved while ComposerVersion.Current stayed at '{ComposerVersion.Current}'. "
            + $"Bump it, then regenerate the record ({Regenerate}). Changed: {string.Join("; ", moved)}");
    }

    /// <summary>The digest is a function of the request alone — composing the same request twice gives the same
    /// plan. Without this the guard above could fail for a reason that has nothing to do with a version.</summary>
    [Test]
    public async Task Composing_the_same_request_twice_gives_the_same_plan()
    {
        foreach (var request in ComposerFingerprint.Requests().Take(4))
        {
            string first, second;
            try
            {
                first = Composer.ComposeStages(request).Plan.ToJson();
                second = Composer.ComposeStages(request).Plan.ToJson();
            }
            catch (ComposeException) { continue; }
            await Assert.That(second).IsEqualTo(first)
                .Because($"{ComposerFingerprint.Key(request)} is not deterministic");
        }
    }
}
