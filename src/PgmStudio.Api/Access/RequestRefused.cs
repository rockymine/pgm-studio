using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Access;

/// <summary>
/// A refusal raised below an endpoint, where the code deciding it has no response to write to. The API's fault
/// envelope in <c>Program.cs</c> answers it with its status and its finding, logging nothing,
/// since it is the request's answer rather than the studio's fault.
/// </summary>
public sealed class RequestRefused(int status, string error, Finding finding) : Exception(finding.Message)
{
    public int Status { get; } = status;
    public string Error { get; } = error;
    public Finding Finding { get; } = finding;
}
