using System.Text;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Names every operation after its route: the verb, then each word of the path, and a path that ends in a
/// parameter ends in <c>by</c> and its name — <c>PUT /map/{slug}/source</c> is <c>putMapSource</c>,
/// <c>GET /map/{slug}</c> is <c>getMapBySlug</c>.
///
/// <para>The name is what a generated client calls the method, so it is derived from what a caller already
/// knows — the route — rather than from the class that serves it, which no caller sees. A parameter inside
/// the path is left out of the name because the method takes it as an argument either way; the trailing one
/// stays, because <c>GET /maps</c> and <c>GET /maps/{id}</c> are otherwise one name.</para>
/// </summary>
internal sealed class OperationNames : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        var description = context.OperationDescription;
        description.Operation.OperationId = Of(description.Method, description.Path);
        return true;
    }

    public static string Of(string verb, string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(segment => segment != "api").ToList();
        var name = new StringBuilder(verb.ToLowerInvariant());
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            if (segment.StartsWith('{'))
            {
                if (index == segments.Count - 1) name.Append("By").Append(Capitalized(segment.Trim('{', '}')));
                continue;
            }
            foreach (var word in segment.Split('-', '_', '.'))
                if (word.Length > 0) name.Append(Capitalized(word));
        }
        return name.ToString();
    }

    private static string Capitalized(string word) => char.ToUpperInvariant(word[0]) + word[1..];
}
