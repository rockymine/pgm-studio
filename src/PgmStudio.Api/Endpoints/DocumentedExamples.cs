using Newtonsoft.Json.Linq;
using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Hands out each body a tool document sends to a route as that operation's example (<see cref="DocumentedBodies"/>).
///
/// <para>Every one is a body <c>DocumentedBodyTests</c> posts and sees accepted, so <c>/api-docs</c> and the kit
/// show a request known to work rather than one written to look right — and an example that stops working fails
/// a test before it reaches a reader. Each is filed under the document and line it is written at.</para>
/// </summary>
internal sealed class DocumentedExamples : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        var operation = context.OperationDescription;
        if (operation.Operation.RequestBody?.Content is not { } content) return true;

        foreach (var block in DocumentedBodies.All)
        {
            if (block.Verb is null || block.Path != operation.Path
                || !string.Equals(block.Verb, operation.Method, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var (media, body) in content)
                if (media.Contains("json", StringComparison.OrdinalIgnoreCase))
                    body.Examples[block.Where] = new OpenApiExample
                    {
                        Summary = block.Route != block.Path ? $"{block.Where}, sent to {block.Route}" : block.Where,
                        Value = JToken.Parse(block.Json),
                    };
        }
        return true;
    }
}
