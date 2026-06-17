namespace PgmStudio.Api.Services;

/// <summary>
/// Safeguards + target for B8 import-from-url (docs/contracts/new-map-authoring.md §12). Only the
/// <see cref="AllowedHosts"/> are fetched server-side (SSRF guard); extraction is bounded (zip-bomb)
/// and writes to a dedicated imports root kept separate from the curated corpus.
/// Built in Program.cs from config (<c>Import:*</c>) with hardcoded defaults.
/// </summary>
public sealed class ImportPolicy
{
    /// <summary>Hosts the server is permitted to fetch from. Hardcoded default; override via
    /// config <c>Import:AllowedHosts</c>.</summary>
    public required IReadOnlyList<string> AllowedHosts { get; init; }

    /// <summary>Writable root for imported worlds — <c>&lt;Root&gt;/&lt;slug&gt;/region</c>; separate from
    /// the curated corpus so abandoned/untrusted imports never mix with it.</summary>
    public required string Root { get; init; }

    public long MaxDownloadBytes { get; init; } = 256L * 1024 * 1024;       // compressed download cap
    public long MaxUncompressedBytes { get; init; } = 1024L * 1024 * 1024;  // total uncompressed (zip-bomb)
    public long MaxEntryBytes { get; init; } = 64L * 1024 * 1024;           // per .mca
    public int MaxEntries { get; init; } = 5000;

    public bool HostAllowed(string host) =>
        AllowedHosts.Any(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase));

    /// <summary>Hardcoded default allowlist — the Overcast/OCC S3 bucket the sample world came from.</summary>
    public static readonly string[] DefaultAllowedHosts = ["occ-maps.s3.ca-central-1.amazonaws.com"];
}
