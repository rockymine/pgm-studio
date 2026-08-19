namespace PgmStudio.Api.Endpoints;

/// <summary>
/// The request body as text, for the routes whose body <em>is</em> a document rather than a wrapper DTO — a
/// plan, a layout, a material, a theme, a compiled intent.
///
/// <para>It is two lines, which is why it is worth having a name: a helper this small is always cheaper to
/// retype than to find, and retyped copies drift in the small ways copies do — a different name at each site,
/// and nowhere to change the reading if it ever needs changing.</para>
///
/// <para>A route whose body is a <b>DTO</b> does not come here: it declares the DTO, and
/// <see cref="RequiredFields"/> then refuses a field the body did not carry by name, before any handler runs.
/// Reading the body as text is what puts a route outside that guarantee, so it is worth being a deliberate
/// choice rather than a habit.</para>
/// </summary>
internal static class RawBody
{
    public static async Task<string> ReadAsync(HttpContext http, CancellationToken ct)
    {
        using var reader = new StreamReader(http.Request.Body);
        return await reader.ReadToEndAsync(ct);
    }
}
