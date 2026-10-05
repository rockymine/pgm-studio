using FastEndpoints;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// <c>GET /api/glossary</c> — <b>every word the studio uses, defined once.</b> The rules, findings and screens
/// use these words without explaining them, so this is what a person or an agent reads before working with the
/// studio. <c>?term=board</c> answers the term a word names, by its own name or by another name it is called;
/// a word the glossary does not define is an empty list, not a 404.
/// </summary>
public sealed class GlossaryEndpoint : EndpointWithoutRequest<List<GlossaryTerm>>
{
    public override void Configure()
    {
        Get("/glossary");
        Description(b => b.Reads(
            new QueryWord("term", "One word, by its own name or another name it is called — `board` answers "
                + "`layout`. Absent is every term.")));
    }

    public override Task HandleAsync(CancellationToken ct) =>
        Send.OkAsync(Query<string>("term", isRequired: false) is { } word ? [.. Glossary.Find(word)] : [.. Glossary.Terms], ct);
}
