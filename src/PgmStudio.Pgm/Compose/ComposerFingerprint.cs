using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// A short digest of what a fixed set of requests composes to, recorded alongside the
/// <see cref="ComposerVersion"/> that produced it.
///
/// <para>This exists to keep one promise honest: a <see cref="ComposeDescriptor"/> reproduces its plan exactly
/// <b>within one composer version</b>. The version is a hand-written constant, so nothing stops a pipeline
/// change from moving the geometry while the constant stays put — and then a stored descriptor claims a
/// reproducibility it does not have. Comparing this digest against the recorded one catches exactly that.</para>
///
/// <para>It is <b>not</b> a golden-output gate. Nothing here says the geometry may not change; layouts are
/// expected to keep evolving. It says only that <em>when</em> the geometry changes the version must change with
/// it, and the recorded digest is meant to be regenerated freely whenever that happens.</para>
/// </summary>
public static class ComposerFingerprint
{
    /// <summary>
    /// The requests the digest covers — a spread of player counts across both supported two-team symmetries.
    ///
    /// <para>The set is a <b>tripwire, not a proof</b>: it catches a change to anything the composer draws
    /// often, and its sensitivity is exactly its coverage. That is why it is not two boards per cohort, which
    /// was the first cut and missed a real change — the bay cap only binds on a two-legged hub, a form roughly
    /// one 20-player board in sixteen carries, so a handful of boards had none to move. Widening it to
    /// <see cref="SeedsPerCohort"/> seeds puts the rarer wide-board forms in range. A change that moves only a
    /// form rarer still can slip through; the answer to that is more seeds, and the cost is linear.</para>
    /// </summary>
    public static IEnumerable<ComposeRequest> Requests()
    {
        foreach (var players in new[] { 8, 12, 20 })
            foreach (var symmetry in new[] { "rot_180", "mirror_z" })
                for (ulong seed = 1; seed <= SeedsPerCohort; seed++)
                    yield return new ComposeRequest(players, 2, symmetry, seed);
    }

    /// <summary>Seeds per (player count, symmetry). The whole set composes in a few seconds, which is what
    /// keeps the guard in the unit suite rather than in a tool nobody runs.</summary>
    public const int SeedsPerCohort = 12;

    /// <summary>The key a request is recorded under: <c>12p/rot_180/s3</c>.</summary>
    public static string Key(ComposeRequest r) => $"{r.PlayersPerTeam}p/{r.Symmetry}/s{r.Seed}";

    /// <summary>Compose every <see cref="Requests"/> entry and digest its plan. A request the composer refuses
    /// records <c>refused</c> — a change from refusing to composing (or back) is itself a geometry change.</summary>
    public static ComposerFingerprintFile Compute()
    {
        var boards = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var request in Requests())
        {
            string digest;
            try { digest = Digest(Composer.ComposeStages(request).Plan.ToJson()); }
            catch (ComposeException) { digest = "refused"; }
            boards[Key(request)] = digest;
        }
        return new ComposerFingerprintFile(ComposerVersion.Current, boards);
    }

    private static string Digest(string planJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(planJson)))[..16];

    /// <summary>The recorded digest, relative to the repo root.</summary>
    public static readonly string[] FilePath = ["tools", "compose", "composer-fingerprints.json"];

    /// <summary>How the record is written. <c>NewLine</c> is pinned because this file is committed: left to
    /// <see cref="Environment.NewLine"/> it would rewrite every line the first time it was regenerated from a
    /// different platform, burying the digests that actually moved in a whole-file diff.</summary>
    public static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { WriteIndented = true, NewLine = "\n" };
}

/// <summary>The recorded digest: the composer version it was taken under, and one entry per request.</summary>
public sealed record ComposerFingerprintFile(
    string ComposerVersion,
    [property: JsonPropertyName("boards")] IReadOnlyDictionary<string, string> Boards)
{
    public string ToJson() => JsonSerializer.Serialize(this, ComposerFingerprint.Json);

    public static ComposerFingerprintFile? Parse(string json) =>
        JsonSerializer.Deserialize<ComposerFingerprintFile>(json, ComposerFingerprint.Json);
}
