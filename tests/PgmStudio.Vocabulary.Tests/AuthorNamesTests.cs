namespace PgmStudio.Vocabulary.Tests;

/// <summary>
/// What a map's author may be called. The rule is here rather than in either half because the browser decides
/// what to do with a row as it is typed and the API decides what reaches the document: a name one accepts and
/// the other drops is exactly the fault this ends (<c>TC2</c>).
/// </summary>
public class AuthorNamesTests
{
    [Test]
    [Arguments("rockymine")]
    [Arguments("Notch")]
    [Arguments("Dinnerbone_")]
    [Arguments("jeb_")]
    [Arguments("A1b")]                      // three characters, the shortest Mojang issues
    [Arguments("0123456789abcdef")]         // sixteen, the longest
    public async Task An_account_name_is_letters_digits_and_underscore(string name)
    {
        await Assert.That(AuthorNames.IsAccountName(name)).IsTrue();
        await Assert.That(AuthorNames.IsWritable(name)).IsTrue().Because("an account name is a name");
    }

    [Test]
    [Arguments("Opus 5")]                   // a space, which Minecraft never allows
    [Arguments("Haiku 4.5")]                // and a point, for a subversion
    [Arguments("ab")]                       // under three
    [Arguments("0123456789abcdefg")]        // over sixteen
    [Arguments("Jean-Luc")]
    [Arguments("O'Brien")]
    public async Task A_name_that_is_not_an_account_is_still_a_name(string name)
    {
        await Assert.That(AuthorNames.IsAccountName(name)).IsFalse()
            .Because("no Minecraft account is called that, so nothing is asked of Mojang");
        await Assert.That(AuthorNames.IsWritable(name)).IsTrue()
            .Because("PGM takes a pseudonym as a whole author");
        await Assert.That(AuthorNames.Refuse(name)).IsNull();
    }

    [Test]
    [Arguments("")]
    [Arguments(" leading")]
    [Arguments("trailing ")]
    [Arguments("two  spaces")]
    [Arguments("<script>")]
    [Arguments("line\nbreak")]
    public async Task A_string_that_is_not_a_name_is_refused_with_a_reason(string name)
    {
        await Assert.That(AuthorNames.IsWritable(name)).IsFalse();
        await Assert.That(AuthorNames.Refuse(name)).IsNotNull();
    }

    /// <summary>The cap is what stops the field being pasted into, and it names the length it saw so the
    /// author can see how far over they are.</summary>
    [Test]
    public async Task A_name_past_the_cap_is_refused_and_the_reason_says_how_long_it_was()
    {
        var book = new string('a', AuthorNames.MaxLength + 40);

        await Assert.That(AuthorNames.IsWritable(book)).IsFalse();
        await Assert.That(AuthorNames.Refuse(book)).Contains(book.Length.ToString());
        await Assert.That(AuthorNames.IsWritable(new string('a', AuthorNames.MaxLength))).IsTrue()
            .Because("the cap itself is allowed");
    }
}
