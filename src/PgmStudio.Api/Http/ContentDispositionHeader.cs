namespace PgmStudio.Api.Http;

/// <summary>
/// Builds a <c>Content-Disposition: attachment</c> header value that survives HTTP transport when the
/// filename contains non-ASCII characters (HTTP header values must be ASCII). Emits an ASCII-sanitised
/// <c>filename=</c> for legacy clients plus an RFC 5987 <c>filename*=UTF-8''…</c> carrying the real name
/// percent-encoded as UTF-8 — without this a map whose slug has a non-ASCII glyph (e.g. "röntgen")
/// throws when assigned to the header.
/// </summary>
public static class ContentDispositionHeader
{
    public static string Attachment(string fileName)
    {
        // Printable ASCII only (0x20–0x7E), minus the quote/backslash that would break the quoted-string,
        // so control chars and non-ASCII collapse to '_' in the legacy fallback.
        var ascii = new string(fileName
            .Select(ch => ch is >= ' ' and < (char)0x7F && ch is not '"' and not '\\' ? ch : '_')
            .ToArray());
        var encoded = Uri.EscapeDataString(fileName);
        return $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{encoded}";
    }
}
