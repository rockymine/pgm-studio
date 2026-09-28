using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Export;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The contributors the studio credits on a map it authors, beside the people the author states: the studio
/// itself, as the tool the map was made with, and whoever built a copied tree that stands in the world.
///
/// <para>Written into the document's <c>authors</c> list as contributors on the way to <c>map.xml</c>, and
/// never stored: a tree removed from the board takes its builder's credit with it. A person the map already
/// credits, in either role, is not credited again. A builder the caller resolved to an account is written by
/// uuid, which is how <c>map.xml</c> names a player; everyone else is written as the pseudonym they are.</para>
/// </summary>
public static class StudioCredits
{
    /// <summary>The studio, credited by the address it is reached at.</summary>
    public const string Studio = "pgmstudio.de";

    /// <summary>What the studio is credited with.</summary>
    public const string StudioContribution = "Map tool";

    /// <summary>What a tree's builder is credited with.</summary>
    public const string TreeContribution = "Trees";

    /// <summary>Everyone a copied tree recipe in <paramref name="layoutJson"/> names as its builder, placed or
    /// not — the names a caller resolves to accounts before the build, since which trees land is the build's
    /// answer. Throws <see cref="DressingParseException"/> on a dressing document that does not parse, the way
    /// the build itself does.</summary>
    public static IReadOnlyList<string> Named(string layoutJson) =>
    [
        .. DressingScope.DocOf(layoutJson).Styles.Values.OfType<TreeStyle>()
            .Where(style => style.Form == TreeForm.Copied)
            .Select(style => style.Builder?.Trim() ?? "")
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>Add the studio and every tree builder in <paramref name="treeBuilders"/> to
    /// <paramref name="doc"/>'s <c>authors</c> as contributors. <paramref name="accounts"/> holds the builders
    /// the caller resolved, keyed by the name as stated.</summary>
    public static void Apply(
        Dict doc, IReadOnlyList<string> treeBuilders,
        IReadOnlyDictionary<string, (string Uuid, string Name)>? accounts = null)
    {
        var people = doc.GetValueOrDefault("authors") is IEnumerable<object?> stated ? stated.ToList() : [];
        var credited = people.OfType<Dict>().ToList();

        void Credit(string name, string contribution)
        {
            var (uuid, shown) = accounts is not null && Account(accounts, name) is { } account ? account : ("", name);
            if (credited.Any(person => Same(person, "uuid", uuid) || Same(person, "name", shown) || Same(person, "name", name)))
                return;
            var entry = new Dict { ["uuid"] = uuid, ["name"] = shown, ["role"] = "contributor", ["contribution"] = contribution };
            people.Add(entry);
            credited.Add(entry);
        }

        foreach (var builder in treeBuilders) Credit(builder, TreeContribution);
        Credit(Studio, StudioContribution);
        doc["authors"] = people;
    }

    private static (string Uuid, string Name)? Account(IReadOnlyDictionary<string, (string Uuid, string Name)> accounts, string name)
        => accounts.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)) is
           { Key: not null, Value: var account } ? account : null;

    private static bool Same(Dict person, string field, string value)
        => value.Length > 0 && person.GetValueOrDefault(field) is string held
           && string.Equals(held.Trim(), value, StringComparison.OrdinalIgnoreCase);
}
