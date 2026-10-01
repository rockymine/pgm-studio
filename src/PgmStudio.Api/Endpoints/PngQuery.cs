using NJsonSchema;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace PgmStudio.Api.Endpoints;

/// <summary>A route that draws a picture, and the closed set of views it draws — the first being the one it
/// draws unasked. Empty for a route with one picture and nothing to choose between.</summary>
internal sealed record PngPreview(string[] Views);

/// <summary>A route that draws its reading as a picture beside the JSON it answers by default, at the scale the
/// route itself reads.</summary>
internal sealed record PictureTwin;

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
        var metadata = aspNet.ApiDescription.ActionDescriptor.EndpointMetadata;
        var preview = metadata.OfType<PngPreview>().FirstOrDefault();
        var twin = metadata.OfType<PictureTwin>().Any();
        if (preview is null && !twin) return true;

        var operation = context.OperationDescription.Operation;

        // The picture goes beside the answer the endpoint's own response type already declared, not over it:
        // a second Produces for 200 replaces the first, which is what had these routes publishing an image as
        // the only thing they can hand back.
        if (operation.Responses.TryGetValue("200", out var answered))
            answered.Content["image/png"] = new OpenApiMediaType
            {
                Schema = new JsonSchema { Type = JsonObjectType.String, Format = "binary" },
            };

        var words = preview is not null ? Words(preview) :
        [
            new QueryWord(PngAnswer.Format,
                "Ask for the reading as a picture instead of the JSON answer. Absent answers the JSON.", ["png"]),
        ];
        foreach (var word in words)
            operation.Parameters.Add(QueryWords.Declared(word));
        return true;
    }

    /// <summary>The three words <see cref="PngAnswer"/> reads, as the shared declaration every other route's
    /// query words travel as — so a caller meets one shape whether a knob is a picture's or a read's.</summary>
    internal static IEnumerable<QueryWord> Words(PngPreview preview)
    {
        yield return new QueryWord(PngAnswer.Format,
            "Ask for the picture instead of the SVG-in-JSON. Absent answers the JSON.", ["png"]);

        if (preview.Views.Length > 1)
            yield return new QueryWord(PngAnswer.View_,
                $"Which view to draw. Absent draws '{preview.Views[0]}'. Only read with format=png.",
                preview.Views);

        yield return new QueryWord(PngAnswer.Scale,
            $"How many times its own size to draw the picture, 1 to {PngAnswer.MaxScale}. A magnification "
            + "rather than a redraw: the same view, at more pixels. Absent, and anything outside the range, "
            + "draws at 1 — a scale is how the answer is looked at rather than part of the question, so a bad "
            + "one costs a bigger picture and not the picture.",
            Min: 1, Max: PngAnswer.MaxScale);
    }
}
