namespace PgmStudio.Pgm.Compose;

/// <summary>Thrown when the composer cannot produce a plan satisfying its hard invariants within its bounded
/// retry budget — a structural failure of the request (an impossible combination of inputs), not a transient
/// one a caller should retry.</summary>
public sealed class ComposeException(string message) : Exception(message);
