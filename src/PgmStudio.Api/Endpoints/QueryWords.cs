using NJsonSchema;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace PgmStudio.Api.Endpoints;

/// <summary>One query word a route reads off the request rather than binding to a record, and what a caller
/// may put in it.</summary>
/// <param name="Name">The word as the query string spells it.</param>
/// <param name="Description">What it does, and what an absent one means — the sentence the schema publishes.</param>
/// <param name="Choices">The closed set it takes, or null for a free value. Published as the parameter's
/// enum, so a caller reads the words rather than learning them by being refused one.</param>
/// <param name="Min">The smallest accepted value, for a number.</param>
/// <param name="Max">The largest. A word carrying either is published as an integer.</param>
internal sealed record QueryWord(
    string Name, string Description, string[]? Choices = null, int? Min = null, int? Max = null);

/// <summary>The query words a route reads, carried as route metadata for <see cref="QueryWords"/> to
/// publish.</summary>
internal sealed record DeclaredQuery(IReadOnlyList<QueryWord> Words);

/// <summary>
/// Publishes the query words a route reads straight off the request.
///
/// <para>A word bound to a request record is described by the record and needs nothing here. These are the
/// others: the ones a route reads off <c>HttpContext.Request.Query</c> because they are not part of the
/// question being asked — a picture's magnification, which view of an answer to draw, how big a section's
/// cut is. Reading them that way is right, and it is exactly why they reach no parameter list unless
/// something puts them there: the route publishes an answer over an empty <c>parameters</c>, and a caller
/// reading the schema to decide how to call it is told nothing.</para>
///
/// <para>Which leaves the one instruction an authoring brief cannot drop — <b>read the schema, not a
/// document</b> — false at exactly the routes whose extra knobs are worth having. So the words travel with
/// the route as metadata, declared where they are read.</para>
/// </summary>
internal sealed class QueryWords : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        if (context is not AspNetCoreOperationProcessorContext aspNet) return true;

        var declared = aspNet.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<DeclaredQuery>().ToList();
        if (declared.Count == 0) return true;

        foreach (var word in declared.SelectMany(entry => entry.Words))
            context.OperationDescription.Operation.Parameters.Add(Declared(word));
        return true;
    }

    internal static OpenApiParameter Declared(QueryWord word)
    {
        var schema = word.Min is not null || word.Max is not null
            ? new JsonSchema { Type = JsonObjectType.Integer, Minimum = word.Min, Maximum = word.Max }
            : new JsonSchema { Type = JsonObjectType.String };
        foreach (var choice in word.Choices ?? []) schema.Enumeration.Add(choice);

        return new OpenApiParameter
        {
            Name = word.Name,
            Kind = OpenApiParameterKind.Query,
            IsRequired = false,
            Schema = schema,
            Description = word.Description,
        };
    }
}
