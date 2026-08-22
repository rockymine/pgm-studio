using NJsonSchema;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace PgmStudio.Api.Endpoints;

/// <summary>A route that draws a picture, and the closed set of views it draws — the first being the one it
/// draws unasked. Empty for a route with one picture and nothing to choose between.</summary>
internal sealed record PngPreview(string[] Views);

/// <summary>
/// Publishes how to ask a preview route for its picture.
///
/// <para><c>format</c>, <c>view</c> and <c>scale</c> are read straight off the query string by
/// <see cref="PngAnswer"/> rather than bound to a request record, which is right — a magnification is not
/// part of the question a preview is asked — and it left them in no parameter list at all. So the routes
/// declared <c>image/png</c> as an answer over an empty <c>parameters</c>: the schema said a picture could
/// come back and nothing said how to get one, which is the one thing a schema exists to remove.</para>
///
/// <para>The declaration is driven by the metadata <see cref="Answers.AlsoPng"/> attaches, so it reaches
/// exactly the routes that answer a picture and carries each one's own view names — the same array the
/// endpoint hands <see cref="PngAnswer.AnsweredAsync"/>, so the enum a caller reads and the names a refusal
/// lists cannot disagree.</para>
/// </summary>
internal sealed class PngQuery : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        if (context is not AspNetCoreOperationProcessorContext aspNet) return true;
        if (aspNet.ApiDescription.ActionDescriptor.EndpointMetadata
                .OfType<PngPreview>().FirstOrDefault() is not { } preview) return true;

        var operation = context.OperationDescription.Operation;

        // The picture goes beside the answer the endpoint's own response type already declared, not over it:
        // a second Produces for 200 replaces the first, which is what had these routes publishing an image as
        // the only thing they can hand back.
        if (operation.Responses.TryGetValue("200", out var answered))
            answered.Content["image/png"] = new OpenApiMediaType
            {
                Schema = new JsonSchema { Type = JsonObjectType.String, Format = "binary" },
            };

        operation.Parameters.Add(Word(PngAnswer.Format, ["png"],
            "Ask for the picture instead of the SVG-in-JSON. Absent answers the JSON."));

        if (preview.Views.Length > 1)
            operation.Parameters.Add(Word(PngAnswer.View_, preview.Views,
                $"Which view to draw. Absent draws '{preview.Views[0]}'. Only read with format=png."));

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = PngAnswer.Scale,
            Kind = OpenApiParameterKind.Query,
            IsRequired = false,
            Schema = new JsonSchema
            {
                Type = JsonObjectType.Integer,
                Minimum = 1,
                Maximum = PngAnswer.MaxScale,
            },
            Description =
                $"How many times its own size to draw the picture, 1 to {PngAnswer.MaxScale}. A magnification "
                + "rather than a redraw: the same view, at more pixels. Absent, and anything outside the "
                + "range, draws at 1 — a scale is how the answer is looked at rather than part of the "
                + "question, so a bad one costs a bigger picture and not the picture.",
        });
        return true;
    }

    /// <summary>One query word out of a closed set, published as the set it comes from.</summary>
    private static OpenApiParameter Word(string name, string[] words, string description)
    {
        var schema = new JsonSchema { Type = JsonObjectType.String };
        foreach (var word in words) schema.Enumeration.Add(word);
        return new OpenApiParameter
        {
            Name = name,
            Kind = OpenApiParameterKind.Query,
            IsRequired = false,
            Schema = schema,
            Description = description,
        };
    }
}
