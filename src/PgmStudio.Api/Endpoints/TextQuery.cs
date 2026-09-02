using Microsoft.AspNetCore.Http;
using NJsonSchema;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace PgmStudio.Api.Endpoints;

/// <summary>A route that answers its reading as characters on <c>?format=text</c>, beside whatever it
/// answers by default — a picture, or a JSON document.</summary>
internal sealed record TextTwin;

/// <summary>
/// Publishes how to ask a route for its text twin.
///
/// <para>A picture encodes a height as a shade and asks a reader to estimate it; a grid of characters states
/// it, and two neighbouring cells are subtracted rather than judged. So a read that draws a picture, or
/// answers a document a tool sums, also answers the same reading as text where a reader with no eye on a
/// picture can act on it — and the schema says so, beside the media type, rather than leaving the word to be
/// discovered by sending the request. The mirror of <see cref="PngQuery"/>, driven by the metadata
/// <see cref="Answers.AlsoText"/> attaches.</para>
/// </summary>
internal sealed class TextQuery : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        if (context is not AspNetCoreOperationProcessorContext aspNet) return true;
        if (!aspNet.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<TextTwin>().Any()) return true;

        var operation = context.OperationDescription.Operation;
        if (operation.Responses.TryGetValue("200", out var answered))
            answered.Content["text/plain"] = new OpenApiMediaType
            {
                Schema = new JsonSchema { Type = JsonObjectType.String },
            };

        // A route that already declares `format` for its picture keeps one word carrying both choices
        // rather than two words of the same name.
        var existing = operation.Parameters.FirstOrDefault(parameter => parameter.Name == TextAnswer.Format);
        if (existing is not null)
        {
            existing.Schema ??= new JsonSchema { Type = JsonObjectType.String };
            if (!existing.Schema.Enumeration.Contains(TextAnswer.Text)) existing.Schema.Enumeration.Add(TextAnswer.Text);
            existing.Description = (existing.Description ?? "").TrimEnd('.')
                + "; `text` answers the same reading as characters, one per block or cell, with its scale, its "
                + "extent and its key on the first lines.";
            return true;
        }
        operation.Parameters.Add(QueryWords.Declared(new QueryWord(TextAnswer.Format,
            "Ask for the reading as characters instead of the default answer: one per block or cell, with "
            + "its scale, its extent and its key on the first lines. Absent answers the default.",
            [TextAnswer.Text])));
        return true;
    }
}

/// <summary>The <c>?format=text</c> half a read with a text twin shares: whether it was asked for, and how a
/// text answer is written.</summary>
internal static class TextAnswer
{
    internal const string Format = "format", Text = "text";

    public static bool Wanted(HttpContext http) =>
        string.Equals(http.Request.Query[Format], Text, StringComparison.OrdinalIgnoreCase);

    public static async Task WriteAsync(HttpContext http, string text, CancellationToken ct)
    {
        http.Response.ContentType = "text/plain; charset=utf-8";
        await http.Response.WriteAsync(text, ct);
    }
}
