using PgmStudio.Api.Http;

namespace PgmStudio.Api.Tests;

/// <summary>Attachment Content-Disposition stays ASCII-safe (HTTP headers reject non-ASCII) while
/// carrying the real UTF-8 name via the RFC 5987 filename* parameter.</summary>
public sealed class ContentDispositionHeaderTests
{
    [Test]
    public async Task AsciiName_isPlainFilename()
    {
        var h = ContentDispositionHeader.Attachment("thunder.xml");
        await Assert.That(h).IsEqualTo("attachment; filename=\"thunder.xml\"; filename*=UTF-8''thunder.xml");
    }

    [Test]
    public async Task NonAsciiName_isAsciiSafe_withUtf8FilenameStar()
    {
        var h = ContentDispositionHeader.Attachment("röntgen.xml");
        // Every emitted character must be ASCII or the header assignment throws.
        await Assert.That(h.All(c => c < 0x80)).IsTrue();
        // Legacy fallback collapses the non-ASCII glyph; filename* carries the percent-encoded UTF-8.
        await Assert.That(h).IsEqualTo("attachment; filename=\"r_ntgen.xml\"; filename*=UTF-8''r%C3%B6ntgen.xml");
    }

    [Test]
    public async Task QuotesAndControlChars_areSanitised()
    {
        var h = ContentDispositionHeader.Attachment("a\"b\n.xml");
        await Assert.That(h.All(c => c < 0x80)).IsTrue();
        await Assert.That(h).Contains("filename=\"a_b_.xml\"");
    }
}
