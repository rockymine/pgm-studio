using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Export;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The contributors the export adds to a map beside the people the author states: whoever built a copied tree
/// that stands in the world. The studio credits nobody else, itself included.
///
/// <para>Written into the document's <c>authors</c> list as contributors on the way to <c>map.xml</c>, and
/// never stored: a tree removed from the board takes its builder's credit with it. A person the map already
/// credits, in either role, is not credited again. A builder the caller resolved to an account is written by
/// uuid, which is how <c>map.xml</c> names a player; everyone else is written as the pseudonym they are.</para>
/// </summary>
public static class TreeBuilderCredits
{
    /// <summary>What a tree's builder is credited with — the copied trees only, since a vanilla tree on the same
    /// map is not theirs.</summary>
    public const string TreeContribution = "Original builder of the copied trees";

    /// <summary>The most builders one export resolves to accounts. A map's recipes can name anyone and its
    /// export is open to anyone, so the lookups one request can set off are bounded; a builder past the bound
    /// is credited by name.</summary>
    public const int MaxResolved = 4;

    /// <summary>The first <see cref="MaxResolved"/> builders the copied tree recipes in
    /// <paramref name="layoutJson"/> name, placed or not — the names a caller resolves to accounts before the
    /// build, since which trees land is the build's answer. Throws <see cref="DressingParseException"/> on a
    /// dressing document that does not parse, the way the build itself does.</summary>
    public static IReadOnlyList<string> Named(string layoutJson) =>
    [
        .. DressingScope.DocOf(layoutJson).Styles.Values.OfType<TreeStyle>()
            .Where(style => style.Form == TreeForm.Copied)
            .Select(style => style.Builder?.Trim() ?? "")
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxResolved),
    ];

    /// <summary>Add every tree builder in <paramref name="treeBuilders"/> to
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
        doc["authors"] = people;
    }

    private static (string Uuid, string Name)? Account(IReadOnlyDictionary<string, (string Uuid, string Name)> accounts, string name)
        => accounts.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)) is
           { Key: not null, Value: var account } ? account : null;

    private static bool Same(Dict person, string field, string value)
        => value.Length > 0 && person.GetValueOrDefault(field) is string held
           && string.Equals(held.Trim(), value, StringComparison.OrdinalIgnoreCase);
}
