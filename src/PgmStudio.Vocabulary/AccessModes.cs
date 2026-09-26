namespace PgmStudio.Vocabulary;

/// <summary>
/// How a deployment decides who may write. <c>open</c> signs every request in as the local admin — a studio
/// on one person's machine, the test suites and the e2e harness; never a public deployment. <c>invited</c>
/// leaves reading open to anyone and lets only people on the whitelist write.
/// </summary>
public static class AccessModes
{
    public const string Open = "open";
    public const string Invited = "invited";

    public static readonly string[] All = [Open, Invited];
}
